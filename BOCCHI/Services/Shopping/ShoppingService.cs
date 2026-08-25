using System.Runtime.InteropServices;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.Shopping;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Services;
using BOCCHI.MobFarmer.Services;
using BOCCHI.Treasure.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using System.Numerics;

namespace BOCCHI.Services.Shopping;

/// <summary>
///     Automatic Antiquarian currency shopping — AOCCH functionality rebuilt on BOCCHI
///     primitives. When <see cref="ShoppingConfig.EnableAutoShop"/> is on and a configured
///     currency threshold is met, travels to the Expedition Antiquarian, opens the right
///     menu entry and tab (verified against the catalog via live ATK reads), then buys each
///     configured target Keep → Buy → Keep Buying in priority order, honouring per-territory
///     reserves. Stops when nothing actionable remains.
/// </summary>
public sealed class ShoppingService : IOnUpdate, IDisposable
{
    private const float VendorInteractionRange = 3.25f;
    private static readonly TimeSpan StepRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan AutoStartCooldown = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MenuOpenTimeout = TimeSpan.FromSeconds(3);
    private const int MaxMenuOpenAttempts = 3;

    private enum Phase
    {
        Idle,
        Traveling,
        OpeningMenu,
        Navigating,
        Buying,
        Closing,
    }

    private readonly ShoppingConfig config;
    private readonly IZoneProvider zones;
    private readonly IObjectTable objects;
    private readonly IPlayer player;
    private readonly IGameGui gui;
    private readonly ICondition condition;
    private readonly IVNavmeshIpc vnav;
    private readonly ShopInspectorController inspector;
    private readonly ShopPageMatcher matcher;
    private readonly ShopPurchaseController purchases;
    private readonly IAutomator automator;
    private readonly IMobFarmer farmer;
    private readonly ITreasureHunter hunter;
    private readonly ICarrotHunter carrotHunter;
    private readonly IPotsTreasureMode potsTreasure;
    private readonly IChainFactory chains;
    private readonly IChainManager chainManager;
    private readonly IPathfinder pathfinder;
    private readonly ITargetManager targets;
    private readonly ILogger<ShoppingService> logger;
    private readonly object gate = new();

    private Phase phase = Phase.Idle;
    private string status = "Idle";
    private int completedPurchaseCount;
    private DateTimeOffset nextStepAt = DateTimeOffset.MinValue;
    private DateTimeOffset autoStartBlockedUntil = DateTimeOffset.MinValue;

    // Current group being worked (menu index + tab id).
    private ShopPageDefinition? desiredPage;
    private ShopTabDefinition? desiredTab;
    private DateTimeOffset matchedAt = DateTimeOffset.MinValue;
    private bool stableLogged;

    // Menu-open retry bookkeeping.
    private bool menuOpenPending;
    private DateTimeOffset menuOpenStartedAt = DateTimeOffset.MinValue;
    private int menuOpenAttempts;

    // Active aethernet teleport task to base camp (null = none).
    private Task<ChainResult>? teleportTask;

    public ShoppingService(
        ShoppingConfig config,
        IZoneProvider zones,
        IObjectTable objects,
        IPlayer player,
        IGameGui gui,
        ICondition condition,
        IVNavmeshIpc vnav,
        ShopInspectorController inspector,
        ShopPurchaseController purchases,
        IAutomator automator,
        IMobFarmer farmer,
        ITreasureHunter hunter,
        ICarrotHunter carrotHunter,
        IPotsTreasureMode potsTreasure,
        IChainFactory chains,
        IChainManager chainManager,
        IPathfinder pathfinder,
        ITargetManager targets,
        ILogger<ShoppingService> logger)
    {
        this.config = config;
        this.zones = zones;
        this.objects = objects;
        this.player = player;
        this.gui = gui;
        this.condition = condition;
        this.vnav = vnav;
        this.inspector = inspector;
        this.purchases = purchases;
        this.automator = automator;
        this.farmer = farmer;
        this.hunter = hunter;
        this.carrotHunter = carrotHunter;
        this.potsTreasure = potsTreasure;
        this.chains = chains;
        this.chainManager = chainManager;
        this.pathfinder = pathfinder;
        this.targets = targets;
        this.logger = logger;

        purchases.Completed += OnPurchaseCompleted;
    }

    public void Dispose()
    {
        purchases.Completed -= OnPurchaseCompleted;
        // Never leave Illegal Mode suspended across plugin unload.
        try
        {
            automator.SetSuspendedForShopping(false);
        }
        catch
        {
            // Plugin teardown — automator may already be disposed.
        }
    }

    public bool IsRunning => phase != Phase.Idle;

    public string Status
    {
        get
        {
            lock (gate)
            {
                return status;
            }
        }
    }

    public string TriggerStatus { get; private set; } = "Automatic shopping disabled.";

    /// <summary>True when auto-shopping should kick off right now (idle window assumed checked by caller).</summary>
    public bool ShouldAutoStart(out string reason)
    {
        if (!config.EnableAutoShop)
        {
            reason = "Automatic shopping disabled.";
            return false;
        }

        if (DateTimeOffset.UtcNow < autoStartBlockedUntil)
        {
            reason = "Automatic shopping cooldown active.";
            return false;
        }

        if (IsRunning)
        {
            reason = Status;
            return false;
        }

        if (!TryGetPagesForZone(out var pages))
        {
            reason = "No catalog for this territory.";
            return false;
        }

        if (condition[ConditionFlag.InCombat] || condition[ConditionFlag.BetweenAreas]
            || condition[ConditionFlag.OccupiedInQuestEvent] || condition[ConditionFlag.Casting])
        {
            reason = "Blocked: player busy.";
            return false;
        }

        if (farmer.Running || hunter.Running || carrotHunter.Running || potsTreasure.Running)
        {
            reason = "Blocked: another automation mode is running.";
            return false;
        }

        // Shopping only runs under Illegal Mode (user requirement) — never standalone.
        if (!automator.IsIllegalMode)
        {
            reason = "Waiting for Illegal Mode.";
            return false;
        }

        if (automator.SuspendedForTreasure)
        {
            reason = "Blocked: treasure hunt owns movement.";
            return false;
        }

        foreach (var page in pages)
        {
            var available = AvailableCurrency(page.CurrencyItemId);
            if (available <= 0 || CurrencyCount(page.CurrencyItemId) < config.GetThreshold(TerritoryKey(), page.CurrencyItemId))
            {
                continue;
            }

            foreach (var tab in page.Tabs)
            {
                if (HasActionableTarget(page, tab, available))
                {
                    reason = "Threshold met with actionable targets.";
                    return true;
                }
            }
        }

        reason = "Waiting for a currency threshold.";
        return false;
    }

    /// <summary>Manual start (also used by auto-start). False with Status set when blocked.</summary>
    public bool Start()
    {
        if (IsRunning)
        {
            return true;
        }

        if (!config.EnableAutoShop)
        {
            SetStatus("Automatic shopping disabled.");
            return false;
        }

        if (!TryGetPagesForZone(out _))
        {
            SetStatus("Failed: shopping requires South Horn or North Horn.");
            return false;
        }

        if (!ShouldAutoStart(out var why) && !why.Contains("Threshold met"))
        {
            // Manual start bypasses thresholds; only hard blocks apply.
            if (why.StartsWith("Blocked") || why.Contains("cooldown"))
            {
                SetStatus(why);
                return false;
            }
        }

        completedPurchaseCount = 0;
        desiredPage = null;
        desiredTab = null;
        menuOpenPending = false;
        menuOpenAttempts = 0;
        phase = Phase.Navigating;
        // Pause Illegal Mode's pipeline — shopping owns vnav until it finishes.
        automator.SetSuspendedForShopping(true);
        SetStatus("Starting automatic shopping.");
        logger.Info("[Shopping] op=start");
        return true;
    }

    public void Stop(string reason, bool cooldown = true)
    {
        vnav.Stop();
        purchases.Cancel(reason);
        if (teleportTask != null)
        {
            chainManager.CancelAll();
            teleportTask = null;
        }
        automator.SetSuspendedForShopping(false);
        phase = Phase.Idle;
        desiredPage = null;
        desiredTab = null;
        menuOpenPending = false;
        menuOpenAttempts = 0;
        SetStatus(reason);
        if (cooldown)
        {
            autoStartBlockedUntil = DateTimeOffset.UtcNow + AutoStartCooldown;
        }

        logger.Info($"[Shopping] op=stop reason=\"{reason}\"");
    }

    public void Update()
    {
        RefreshTriggerStatus();

        // Turning the toggle off aborts a run in progress (AOCCH behavior).
        if (phase != Phase.Idle && !config.EnableAutoShop)
        {
            Stop("Stopped: auto shopping disabled.");
            return;
        }

        if (phase == Phase.Idle)
        {
            // Auto-start: thresholds + idle checks are all inside ShouldAutoStart.
            if (ShouldAutoStart(out _) && Start())
            {
                logger.Info("[Shopping] op=auto-start");
            }

            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            Stop("Stopped: left the Occult Crescent.");
            return;
        }

        if (condition[ConditionFlag.InCombat])
        {
            Stop("Stopped: combat started.");
            return;
        }

        if (DateTimeOffset.UtcNow < nextStepAt)
        {
            return;
        }

        switch (phase)
        {
            case Phase.Traveling:
                TickTravel();
                break;
            case Phase.OpeningMenu:
                TickOpenMenu();
                break;
            case Phase.Navigating:
                TickNavigate();
                break;
            case Phase.Buying:
                TickBuy();
                break;
            case Phase.Closing:
                TryCloseShop();
                break;
        }
    }

    // ---------------------------------------------------------------- travel

    private void TickTravel()
    {
        // A teleport chain to base camp is running — wait for it to finish.
        if (teleportTask != null)
        {
            if (!teleportTask.IsCompleted)
            {
                SetStatus("Shopping | Teleporting to base camp.");
                return;
            }

            if (teleportTask.Result.IsSuccess)
            {
                logger.Info("[Shopping] op=teleport-done result=success");
            }
            else
            {
                logger.Warn($"[Shopping] op=teleport-done result={teleportTask.Result.State}");
            }

            teleportTask = null;
            nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
            SetStatus("Shopping | Teleport finished; locating vendor.");
            return;
        }

        if (!TryFindVendor(out var vendor))
        {
            // Vendor only spawns at base camp. Prefer the Return spell (fast, no aetheryte
            // walk); fall back to the Lifestream aethernet hop while Return is on cooldown.
            var zone = zones.GetZone();
            if (!zone.IsOccultCrescentZone())
            {
                Stop("Skipped: outside Occulent Crescent.".Replace("Occulent", "Occult"));
                return;
            }

            if (zone.IsInBasecamp())
            {
                // At camp but vendor missing — retry shortly rather than stopping outright
                // (vendor may be temporarily untargetable).
                SetStatus("Shopping | Waiting for vendor at base camp.");
                nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
                return;
            }

            if (Actions.Return.CanCast())
            {
                var returnChain = ReturnToBaseCamp.Append(
                    chains.Create("Shopping::Return"),
                    zones,
                    condition,
                    gui,
                    pathfinder,
                    vnav);
                teleportTask = chainManager.Manage(returnChain);
                SetStatus("Shopping | Casting Return to base camp.");
                logger.Info("[Shopping] op=return-start");
                return;
            }

            var vendorData = zone.GetShoppingVendor();
            var placeNameId = vendorData?.PreferredAethernetId ?? zone.GetMainAetheryte().Id;
            if (!zone.IsUsableAethernetDestination(placeNameId))
            {
                Stop("Skipped: Return on cooldown and base camp aethernet unavailable.");
                return;
            }

            teleportTask = chainManager.Manage(
                chains.Create($"Shopping::Teleport({placeNameId})")
                    .Then<AethernetTeleportChain, uint>(placeNameId));
            SetStatus("Shopping | Teleporting to base camp.");
            logger.Info($"[Shopping] op=teleport-start placeName={placeNameId}");
            return;
        }

        var distance = vendor.Position.Distance2D(player.Position);
        if (distance > VendorInteractionRange)
        {
            if (vnav.IsNavmeshReady() && EzThrottler.Throttle("Shopping::Approach", 1000))
            {
                vnav.PathfindAndMoveCloseTo(vendor.Position.GetApproachPosition(player.Position, 2.5f), false, 1.5f);
            }

            SetStatus($"Shopping | Approaching vendor ({distance:0.0}y).");
            return;
        }

        vnav.Stop();
        phase = Phase.OpeningMenu;
        nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
    }

    private unsafe void TickOpenMenu()
    {
        var snapshot = inspector.Snapshot;
        if (snapshot.IsSelectIconStringOpen || snapshot.IsShopExchangeCurrencyOpen)
        {
            menuOpenPending = false;
            phase = Phase.Navigating;
            return;
        }

        if (menuOpenPending)
        {
            if (DateTimeOffset.UtcNow - menuOpenStartedAt < MenuOpenTimeout)
            {
                SetStatus($"Shopping | Waiting for vendor menu ({menuOpenAttempts}/{MaxMenuOpenAttempts}).");
                return;
            }

            menuOpenPending = false;
            nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
            if (menuOpenAttempts >= MaxMenuOpenAttempts)
            {
                Stop($"Failed: vendor menu did not open after {MaxMenuOpenAttempts} attempts.");
                return;
            }
        }

        if (!TryFindVendor(out var vendor) || vendor == null)
        {
            Stop("Skipped: vendor disappeared before interaction.");
            return;
        }

        if (condition[ConditionFlag.Mounted])
        {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23); // dismount
            nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
            return;
        }

        if (!EzThrottler.Throttle("Shopping::Interact", 750))
        {
            return;
        }

        // Set target first — interaction without a target is unreliable (AOCCH does the same).
        targets.Target = vendor;
        unsafe
        {
            TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)vendor.Address, false);
        }
        menuOpenPending = true;
        menuOpenStartedAt = DateTimeOffset.UtcNow;
        menuOpenAttempts++;
        SetStatus($"Shopping | Opening vendor menu ({menuOpenAttempts}/{MaxMenuOpenAttempts}).");
    }

    // ------------------------------------------------------------ navigation

    private void TickNavigate()
    {
        var snapshot = inspector.Snapshot;

        if (snapshot.IsSelectIconStringOpen)
        {
            if (desiredPage != null && !snapshot.MenuEntries.Any(e => e.Index == desiredPage.MenuIndex))
            {
                Stop($"Failed: vendor menu lacks expected entry {desiredPage.MenuIndex}.");
                return;
            }

            var menuIndex = desiredPage?.MenuIndex ?? SelectFirstAffordableMenuIndex(snapshot);
            if (menuIndex == null)
            {
                StopCompleted("No affordable catalog page for current currency.");
                return;
            }

            desiredPage ??= FindPage(menuIndex.Value);
            if (desiredPage == null)
            {
                Stop($"Failed: no catalog definition for menu index {menuIndex.Value}.");
                return;
            }

            if (EzThrottler.Throttle("Shopping::MenuSelect", 600))
            {
                try
                {
                    new AddonMaster.SelectIconString(gui.GetAddonByName("SelectIconString", 1).Address)
                        .Entries[desiredPage.MenuIndex].Select();
                    SetStatus($"Shopping | Opening \"{desiredPage.MenuLabel}\".");
                    nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
                    ResetStability();
                }
                catch (Exception ex)
                {
                    logger.Warn($"[Shopping] op=menu-select-failed err=\"{ex.Message}\"");
                }
            }

            return;
        }

        if (!snapshot.IsShopExchangeCurrencyOpen)
        {
            // Neither window open — go find/interact with the vendor again.
            phase = Phase.Traveling;
            return;
        }

        if (!matcher.TryMatch(ShopCatalog.Pages, snapshot, out var match, out var matchReason) || match == null)
        {
            ResetStability();
            SetStatus($"Shopping | Waiting for known shop page ({matchReason}).");
            nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
            return;
        }

        if (match.Page.MenuIndex != desiredPage?.MenuIndex)
        {
            ResetStability();
            if (TryCloseShop())
            {
                SetStatus("Shopping | Returning to vendor menu.");
                nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
            }

            return;
        }

        if (match.Tab.TabId != desiredTab?.TabId)
        {
            desiredTab ??= PickBestTab(match.Page);
            if (desiredTab == null)
            {
                StopCompleted($"No actionable targets on page \"{match.Page.MenuLabel}\".");
                return;
            }

            ResetStability();
            if (TrySelectTab(desiredTab.TabId))
            {
                SetStatus($"Shopping | Switching to tab \"{desiredTab.TabLabel}\".");
                nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
            }

            return;
        }

        if (matchedAt == DateTimeOffset.MinValue)
        {
            matchedAt = DateTimeOffset.UtcNow;
            SetStatus("Shopping | Waiting for tab settle.");
            return;
        }

        if (DateTimeOffset.UtcNow - matchedAt < TimeSpan.FromMilliseconds(250))
        {
            return;
        }

        if (!stableLogged)
        {
            logger.Info($"[Shopping] op=navigation-stable menu={match.Page.MenuIndex} tab={match.Tab.TabId}");
            stableLogged = true;
        }

        phase = Phase.Buying;
    }

    // ---------------------------------------------------------------- buying

    private void TickBuy()
    {
        if (purchases.IsBusy)
        {
            SetStatus("Shopping | Waiting for purchase result.");
            return;
        }

        var snapshot = inspector.Snapshot;
        if (!snapshot.IsShopExchangeCurrencyOpen)
        {
            phase = Phase.Navigating;
            return;
        }

        if (!matcher.TryMatch(ShopCatalog.Pages, snapshot, out var match, out _) || match == null
            || match.Page.MenuIndex != desiredPage?.MenuIndex || match.Tab.TabId != desiredTab?.TabId)
        {
            phase = Phase.Navigating;
            return;
        }

        var available = AvailableCurrency(match.Page.CurrencyItemId);
        var target = SelectNextTarget(snapshot, match.Page, match.Tab, available);
        if (target == null)
        {
            // Page/tab done — move to the next one or finish.
            if (AdvanceToNextGroup())
            {
                return;
            }

            if (TryCloseShop())
            {
                phase = Phase.Closing;
                nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
                return;
            }

            StopCompleted(BuildDoneMessage());
            return;
        }

        var (entry, quantity, intent, targetState) = target.Value;
        if (!purchases.TryBuy(entry, quantity))
        {
            if (purchases.LastCompletionKind == PurchaseCompletionKind.StopShopping)
            {
                Stop($"Stopped: {purchases.LastStatus}");
            }
            else
            {
                nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
            }

            return;
        }

        pendingIntent = intent;
        pendingTargetIndex = targetState;
        pendingQuantity = quantity;
        SetStatus($"Shopping | Buying {quantity}× {entry.ItemName} ({intent}).");
    }

    private ShoppingConfig? cfg => config; // readability alias used below

    // ------------------------------------------------------- target selection

    private (LiveShopEntry Entry, int Quantity, string Intent, int ConfigIndex)? SelectNextTarget(
        LiveShopSnapshot snapshot, ShopPageDefinition page, ShopTabDefinition tab, int available)
    {
        var candidates = config.Targets
            .Select((t, i) => (T: t, I: i))
            .Where(x => MatchesTerritory(x.T.TerritoryKey)
                        && x.T.MenuIndex == page.MenuIndex && x.T.TabId == tab.TabId)
            .OrderBy(x => x.T.Priority);

        foreach (var (target, index) in candidates)
        {
            var live = snapshot.ShopEntries.FirstOrDefault(e => e.ItemId == target.ItemId);
            var def = tab.Items.FirstOrDefault(d => d.ItemId == target.ItemId);
            if (live == null || def == null || live.RowIndex != def.RowIndex || live.Cost != def.Cost || live.Cost > (uint)available)
            {
                continue;
            }

            var count = (int)ItemCount(target.ItemId);

            // Keep → Buy → KeepBuying, deterministic order like AOCCH.
            if (target.KeepAmount > 0 && count < target.KeepAmount)
            {
                var qty = BatchQuantity(live, target.KeepAmount - count, available);
                if (qty > 0)
                {
                    return (live, qty, "Keep", index);
                }
            }

            if (target.BuyAmount > 0)
            {
                var qty = BatchQuantity(live, target.BuyAmount - Math.Min(count, target.BuyAmount), available);
                if (qty > 0)
                {
                    return (live, qty, "Buy", index);
                }
            }

            if (target.KeepBuying)
            {
                var qty = BatchQuantity(live, int.MaxValue, available);
                if (qty > 0)
                {
                    return (live, qty, "Keep Buying", index);
                }
            }
        }

        // Legacy allowlist fallback when no structured targets exist.
        foreach (var itemId in config.PreferredItemIds)
        {
            if (!ShopCatalog.TryGet(itemId, out var def))
            {
                continue;
            }

            var live = snapshot.ShopEntries.FirstOrDefault(e => e.ItemId == itemId);
            if (live == null || live.Cost > (uint)available)
            {
                continue;
            }

            var qty = BatchQuantity(live, 1, available);
            if (qty > 0)
            {
                return (live, qty, "Preferred", -1);
            }
        }

        return null;
    }

    private static int BatchQuantity(LiveShopEntry entry, int desired, int available)
    {
        if (desired <= 0 || entry.Cost == 0 || available < (int)entry.Cost)
        {
            return 0;
        }

        var maxAffordable = available / (int)entry.Cost;
        var batchLimit = entry.MaxStackSize == 999u ? 99 : 1;
        return Math.Max(1, Math.Min(Math.Min(desired, maxAffordable), batchLimit));
    }

    private bool HasActionableTarget(ShopPageDefinition page, ShopTabDefinition tab, int available)
    {
        if (available <= 0)
        {
            return false;
        }

        foreach (var target in config.Targets)
        {
            if (!MatchesTerritory(target.TerritoryKey) || target.MenuIndex != page.MenuIndex || target.TabId != tab.TabId)
            {
                continue;
            }

            var def = tab.Items.FirstOrDefault(i => i.ItemId == target.ItemId);
            if (def == null || def.Cost > (uint)available)
            {
                continue;
            }

            var count = (int)ItemCount(target.ItemId);
            if ((target.KeepAmount > 0 && count < target.KeepAmount) || target.BuyAmount > 0 || target.KeepBuying)
            {
                return true;
            }
        }

        return config.PreferredItemIds.Count > 0;
    }

    /// <summary>Pick another actionable page/tab, else false to finish.</summary>
    private bool AdvanceToNextGroup()
    {
        if (!TryGetPagesForZone(out var pages))
        {
            return false;
        }

        foreach (var page in pages.OrderBy(p => p.CurrencyItemId).ThenBy(p => p.MenuIndex))
        {
            var available = AvailableCurrency(page.CurrencyItemId);
            foreach (var tab in page.Tabs)
            {
                if (!HasActionableTarget(page, tab, available))
                {
                    continue;
                }

                if (page.MenuIndex == desiredPage?.MenuIndex && tab.TabId == desiredTab?.TabId)
                {
                    continue;
                }

                desiredPage = page;
                desiredTab = tab;
                ResetStability();
                phase = Phase.Navigating;
                SetStatus($"Shopping | Moving to \"{page.MenuLabel}\" / \"{tab.TabLabel}\".");
                return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------ purchase results

    private int pendingTargetIndex = -1;
    private string pendingIntent = string.Empty;
    private int pendingQuantity;

    private void OnPurchaseCompleted(PurchaseCompletionKind kind)
    {
        if (phase != Phase.Buying)
        {
            return;
        }

        switch (kind)
        {
            case PurchaseCompletionKind.Success:
                completedPurchaseCount += Math.Max(1, pendingQuantity);
                if (pendingTargetIndex >= 0 && pendingIntent == "Buy")
                {
                    var t = config.Targets[pendingTargetIndex];
                    t.BuyAmount = Math.Max(0, t.BuyAmount - Math.Max(1, pendingQuantity));
                }

                break;
            case PurchaseCompletionKind.SkipTarget:
                // Mark target as exhausted for this run so we do not loop forever.
                if (pendingTargetIndex >= 0)
                {
                    skippedThisRun.Add(pendingTargetIndex);
                    if (pendingIntent == "Buy")
                    {
                        config.Targets[pendingTargetIndex].BuyAmount = 0;
                    }
                }

                break;
            case PurchaseCompletionKind.StopShopping:
                Stop($"Stopped: {purchases.LastStatus}");
                return;
        }

        pendingTargetIndex = -1;
        pendingIntent = string.Empty;
        nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
    }

    private readonly HashSet<int> skippedThisRun = [];

    // ---------------------------------------------------------------- helpers

    private int? SelectFirstAffordableMenuIndex(LiveShopSnapshot snapshot)
    {
        foreach (var entry in snapshot.MenuEntries.OrderBy(e => e.Index))
        {
            var page = ShopCatalog.Pages.FirstOrDefault(p => p.MenuIndex == entry.Index);
            if (page == null)
            {
                continue;
            }

            if (AvailableCurrency(page.CurrencyItemId) >= page.Tabs.SelectMany(t => t.Items).Select(i => (int)i.Cost).DefaultIfEmpty(int.MaxValue).Min())
            {
                return entry.Index;
            }
        }

        return snapshot.MenuEntries.Count > 0 ? snapshot.MenuEntries[0].Index : null;
    }

    private ShopPageDefinition? FindPage(int menuIndex) =>
        ShopCatalog.Pages.FirstOrDefault(p => p.MenuIndex == menuIndex);

    private ShopTabDefinition? PickBestTab(ShopPageDefinition page)
    {
        var available = AvailableCurrency(page.CurrencyItemId);
        foreach (var tab in page.Tabs.OrderBy(t => t.TabId))
        {
            if (HasActionableTarget(page, tab, available))
            {
                return tab;
            }
        }

        return null;
    }

    private unsafe bool TrySelectTab(int tabId)
    {
        var addon = (AtkUnitBase*)gui.GetAddonByName("ShopExchangeCurrency", 1).Address;
        if (addon == null || !addon->IsReady)
        {
            return false;
        }

        var values = (AtkValue*)Marshal.AllocHGlobal(4 * sizeof(AtkValue));
        if (values == null)
        {
            return false;
        }

        try
        {
            for (var i = 0; i < 4; i++)
            {
                values[i] = default;
            }

            values[0].SetInt(4);
            values[1].SetInt(-1);
            values[2].SetInt(1);
            values[3].SetInt(tabId);
            return addon->FireCallback(4, values, true);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)values);
        }
    }

    private unsafe bool TryCloseShop()
    {
        var addon = (AtkUnitBase*)gui.GetAddonByName("ShopExchangeCurrency", 1).Address;
        if (addon == null || !addon->IsReady)
        {
            return true;
        }

        addon->FireCallbackInt(-1);
        nextStepAt = DateTimeOffset.UtcNow + StepRetryDelay;
        return true;
    }

    private bool TryFindVendor(out IGameObject? vendor)
    {
        var zoneVendor = zones.GetZone().GetShoppingVendor();
        if (zoneVendor is not { } vendorData)
        {
            vendor = null;
            return false;
        }

        vendor = objects
                .Where(o => o is { ObjectKind: ObjectKind.EventNpc, IsTargetable: true } && o.BaseId == vendorData.DataId)
                .OrderBy(o => o.Position.Distance2D(player.Position))
                .FirstOrDefault();
        return vendor != null;
    }

    private bool TryGetPagesForZone(out IReadOnlyList<ShopPageDefinition> pages)
    {
        pages = ShopCatalog.Pages;
        return zones.GetZone().IsOccultCrescentZone() && pages.Count > 0;
    }

    private string TerritoryKey()
    {
        var zoneId = zones.GetZone().ZoneId;
        return zoneId == ZoneId.Unknown ? "SouthHorn" : zoneId.ToString();
    }

    private bool MatchesTerritory(string key) =>
        string.Equals(key, TerritoryKey(), StringComparison.OrdinalIgnoreCase);

    private static unsafe int ItemCount(uint itemId)
    {
        var inventory = InventoryManager.Instance();
        return inventory == null ? 0 : inventory->GetInventoryItemCount(itemId);
    }

    private static int CurrencyCount(uint itemId) => (int)ItemCount(itemId);

    private int AvailableCurrency(uint currencyItemId) =>
        Math.Max(0, CurrencyCount(currencyItemId) - config.GetReserve(TerritoryKey(), currencyItemId));

    private void RefreshTriggerStatus()
    {
        if (phase != Phase.Idle)
        {
            return;
        }

        TriggerStatus = ShouldAutoStart(out var reason) ? reason : reason;
    }

    private void StopCompleted(string reason) => Stop(reason, cooldown: false);

    private string BuildDoneMessage() =>
        completedPurchaseCount > 0
            ? $"Completed automatic shopping run. purchases={completedPurchaseCount}."
            : "No shopping targets remain.";

    private void SetStatus(string s)
    {
        lock (gate)
        {
            status = s;
        }
    }

    private void ResetStability()
    {
        matchedAt = DateTimeOffset.MinValue;
        stableLogged = false;
    }
}
