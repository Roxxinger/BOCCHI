using System.Runtime.InteropServices;
using BOCCHI.Common.Data.Shopping;
using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Services.Logger;

namespace BOCCHI.Services.Shopping;

public enum PurchaseCompletionKind
{
    None,
    Success,
    SkipTarget,
    StopShopping,
}

/// <summary>
///     Fires purchase callbacks on ShopExchangeCurrency, handles the SelectYesno confirmation,
///     verifies success via inventory deltas and classifies failures from the chat log.
///     Port of AOCCH's ShopPurchaseController onto BOCCHI primitives.
/// </summary>
public sealed class ShopPurchaseController : IDisposable
{
    private static readonly TimeSpan PurchaseTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ConfirmationRetryDelay = TimeSpan.FromMilliseconds(500);

    private enum AttemptState
    {
        PendingDispatch,
        PollingForOutcomeOrConfirmation,
    }

    private sealed class Attempt
    {
        public required LiveShopEntry Entry { get; init; }
        public int Quantity { get; init; }
        public uint ExpectedCurrencyDelta => Entry.Cost * (uint)Quantity;
        public int PreCurrency { get; set; }
        public int PreTarget { get; set; }
        public DateTimeOffset Deadline { get; init; }
        public AttemptState State { get; set; }
        public int Confirmations { get; set; }
        public DateTimeOffset NextConfirmAt { get; set; } = DateTimeOffset.MinValue;
    }

    private readonly IFramework framework;
    private readonly IChatGui chatGui;
    private readonly IGameGui gameGui;
    private readonly ILogger<ShopPurchaseController> logger;
    private readonly object gate = new();

    private Attempt? active;

    public ShopPurchaseController(IFramework framework, IChatGui chatGui, IGameGui gameGui, ILogger<ShopPurchaseController> logger)
    {
        this.framework = framework;
        this.chatGui = chatGui;
        this.gameGui = gameGui;
        this.logger = logger;

        framework.Update += OnUpdate;
        chatGui.LogMessage += OnChatLog;
    }

    public bool IsBusy
    {
        get
        {
            lock (gate)
            {
                return active != null;
            }
        }
    }

    public string LastStatus { get; private set; } = "Idle";

    public PurchaseCompletionKind LastCompletionKind { get; private set; } = PurchaseCompletionKind.None;

    /// <summary>Raised when a purchase finishes (success or failure). Argument = completion kind.</summary>
    public event Action<PurchaseCompletionKind>? Completed;

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        chatGui.LogMessage -= OnChatLog;
    }

    public void Cancel(string reason)
    {
        lock (gate)
        {
            active = null;
            LastStatus = reason;
            LastCompletionKind = PurchaseCompletionKind.StopShopping;
        }
    }

    /// <summary>
    ///     Starts a purchase for a live shop row. Returns false with LastStatus set if rejected.
    /// </summary>
    public unsafe bool TryBuy(LiveShopEntry entry, int quantity)
    {
        if (quantity <= 0)
        {
            SetOutcome("Failed: quantity must be positive.", PurchaseCompletionKind.StopShopping);
            return false;
        }

        var inventory = InventoryManager.Instance();
        if (inventory == null)
        {
            SetOutcome("Failed: inventory unavailable.", PurchaseCompletionKind.StopShopping);
            return false;
        }

        lock (gate)
        {
            if (active != null)
            {
                LastStatus = $"Failed: already purchasing item {active.Entry.ItemId}.";
                LastCompletionKind = PurchaseCompletionKind.StopShopping;
                return false;
            }

            active = new Attempt
            {
                Entry = entry,
                Quantity = quantity,
                PreTarget = inventory->GetInventoryItemCount(entry.ItemId),
                PreCurrency = inventory->GetInventoryItemCount(entry.CurrencyItemId),
                Deadline = DateTimeOffset.UtcNow + PurchaseTimeout,
                State = AttemptState.PendingDispatch,
            };
            LastStatus = $"Dispatching purchase of {quantity}× {entry.ItemName}.";
            LastCompletionKind = PurchaseCompletionKind.None;
        }

        logger.Info($"[ShopPurchase] op=begin itemId={entry.ItemId} row={entry.RowIndex} qty={quantity} cost={entry.Cost} currency={entry.CurrencyItemId}");
        return true;
    }

    private void OnUpdate(IFramework _)
    {
        Attempt? attempt;
        lock (gate)
        {
            attempt = active;
        }

        if (attempt == null)
        {
            return;
        }

        if (DateTimeOffset.UtcNow >= attempt.Deadline)
        {
            // After a confirmation was sent, a timeout most likely means "bought but log missed".
            var kind = attempt.Confirmations > 0 ? PurchaseCompletionKind.SkipTarget : PurchaseCompletionKind.StopShopping;
            Complete($"{(kind == PurchaseCompletionKind.SkipTarget ? "Skipped" : "Failed")}: timeout.", kind);
            return;
        }

        switch (attempt.State)
        {
            case AttemptState.PendingDispatch:
                TickDispatch(attempt);
                break;
            case AttemptState.PollingForOutcomeOrConfirmation:
                TickPoll(attempt);
                break;
        }
    }

    private unsafe void TickDispatch(Attempt attempt)
    {
        var addon = (AtkUnitBase*)gameGui.GetAddonByName("ShopExchangeCurrency", 1).Address;
        if (addon == null || !addon->IsReady)
        {
            return;
        }

        FireBuyCallback(addon, attempt.Entry.RowIndex, attempt.Quantity);
        attempt.State = AttemptState.PollingForOutcomeOrConfirmation;
        logger.Debug($"[ShopPurchase] op=callback-fired itemId={attempt.Entry.ItemId} row={attempt.Entry.RowIndex} qty={attempt.Quantity}");
    }

    private unsafe void TickPoll(Attempt attempt)
    {
        var inventory = InventoryManager.Instance();
        if (inventory != null)
        {
            var targetNow = inventory->GetInventoryItemCount(attempt.Entry.ItemId);
            var currencyNow = inventory->GetInventoryItemCount(attempt.Entry.CurrencyItemId);
            if (targetNow > attempt.PreTarget || currencyNow < attempt.PreCurrency)
            {
                Complete($"Success: purchased {attempt.Entry.ItemName}.", PurchaseCompletionKind.Success);
                return;
            }
        }

        if (!TryGetReadyAddon("SelectYesno", out var yesno))
        {
            return;
        }

        if (DateTimeOffset.UtcNow < attempt.NextConfirmAt)
        {
            return;
        }

        yesno->FireCallbackInt(0);
        attempt.Confirmations++;
        attempt.NextConfirmAt = DateTimeOffset.UtcNow + ConfirmationRetryDelay;
        logger.Info($"[ShopPurchase] op=confirm itemId={attempt.Entry.ItemId} count={attempt.Confirmations}");
    }

    private void Complete(string status, PurchaseCompletionKind kind)
    {
        Attempt? done;
        lock (gate)
        {
            done = active;
            active = null;
            LastStatus = status;
            LastCompletionKind = kind;
        }

        if (done == null)
        {
            return;
        }

        if (kind == PurchaseCompletionKind.Success)
        {
            logger.Info($"[ShopPurchase] op=complete outcome={kind} itemId={done.Entry.ItemId} status=\"{status}\"");
        }
        else
        {
            logger.Warn($"[ShopPurchase] op=complete outcome={kind} itemId={done.Entry.ItemId} status=\"{status}\"");
        }
        try
        {
            Completed?.Invoke(kind);
        }
        catch (Exception ex)
        {
            logger.Warn($"[ShopPurchase] op=completed-handler-failed err=\"{ex.Message}\"");
        }
    }

    private void SetOutcome(string status, PurchaseCompletionKind kind)
    {
        lock (gate)
        {
            LastStatus = status;
            LastCompletionKind = kind;
        }
    }

    /// <summary>
    ///     Classifies game-log exchange failures while a purchase is in flight. Message ids are
    ///     the verified AOCCH set (1939–5283).
    /// </summary>
    private void OnChatLog(ILogMessage message)
    {
        Attempt? attempt;
        lock (gate)
        {
            attempt = active;
        }

        var eventId = message.LogMessageId;
        if (attempt == null || eventId is < 1939u or > 5283u)
        {
            return;
        }

        PurchaseCompletionKind kind = eventId switch
        {
            1939u => PurchaseCompletionKind.StopShopping, // cannot carry any more
            1940u or 3737u or 3974u or 3978u or 5283u => PurchaseCompletionKind.StopShopping, // inventory full
            1941u or 1942u or 5282u => PurchaseCompletionKind.SkipTarget, // not enough items/currency
            1943u or 3739u or 3740u or 3976u or 3977u or 3979u => PurchaseCompletionKind.SkipTarget, // unique restriction
            3736u or 3738u => PurchaseCompletionKind.SkipTarget, // required item equipped
            3975u => PurchaseCompletionKind.SkipTarget, // not enough currency
            _ => PurchaseCompletionKind.SkipTarget,
        };

        Complete($"Skipped/failed: game log event {eventId}.", kind);
    }

    private unsafe bool TryGetReadyAddon(string name, out AtkUnitBase* addon)
    {
        addon = (AtkUnitBase*)gameGui.GetAddonByName(name, 1).Address;
        return addon != null && addon->IsReady;
    }

    /// <summary>ShopExchangeCurrency buy callback: (int 0, uint rowIndex, int quantity).</summary>
    private static unsafe void FireBuyCallback(AtkUnitBase* addon, uint rowIndex, int quantity)
    {
        var values = (AtkValue*)Marshal.AllocHGlobal(4 * sizeof(AtkValue));
        if (values == null)
        {
            return;
        }

        try
        {
            for (var i = 0; i < 4; i++)
            {
                values[i] = default;
            }

            values[0].SetInt(0);
            values[1].SetUInt(rowIndex);
            values[2].SetInt(quantity);
            addon->FireCallback(4, values, true);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)values);
        }
    }
}
