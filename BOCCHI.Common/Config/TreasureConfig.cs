using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("treasure", GroupOrder = 20)]
public class TreasureConfig : IAutoConfig
{
    /// <summary>
    /// Download community carrot/coffer pads and pot timers; anonymously upload what you see.
    /// </summary>
    [Checkbox(Order = 0, Section = "shared_maps")]
    public bool EnableSharedMaps { get; set; } = true;

    [Checkbox(Order = 1, Section = "radar")]
    public bool DrawLineToBronzeChests { get; set; } = true;

    [Checkbox(Order = 2, Section = "radar")]
    public bool DrawLineToSilverChests { get; set; } = true;

    [Checkbox(Order = 3, Section = "radar")]
    public bool DrawLineToCarrots { get; set; } = true;

    [Checkbox(Order = 4, Section = "radar")]
    public bool ShowPercentageActiveTreasureCount { get; set; } = false;

    /// <summary>Cast Return to base camp after Treasure Hunt or Carrot Hunt finishes.</summary>
    [Checkbox(Order = 5, Section = "completion")]
    public bool ReturnToBaseCampAfterHunt { get; set; } = true;

    /// <summary>Play an MP3 when Treasure Hunt finishes.</summary>
    [Checkbox(Order = 6, Section = "completion")]
    public bool PlaySoundOnHuntComplete { get; set; } = true;

    /// <summary>MP3 name (without extension) from the plugin Sounds folder. Default Moogle.</summary>
    [Mp3SoundSelect(Order = 7, Section = "completion")]
    public string HuntCompleteSound { get; set; } = "Moogle";

    /// <summary>
    ///     Carrot Hunt: empty pads stay skipped; after each Fortune Carrot use, every pad
    ///     must be checked again (respawns). When a full pass finds none, keep checking
    ///     until Stop or out of Fortune Carrots.
    /// </summary>
    [Checkbox(Order = 8, Section = "carrot_hunt")]
    public bool LoopCarrotHunt { get; set; } = false;

    /// <summary>Cast Treasure Sight at hunt start and on the interval below; abort early when Sight reports none left.</summary>
    [Checkbox(Order = 9, Section = "treasure_hunt")]
    public bool CastTreasureSightDuringHunt { get; set; } = true;

    /// <summary>Recast Treasure Sight every N hunt locations checked (opened or empty pads).</summary>
    [IntRange(1, 50, Order = 10, Indent = 1, Requires = nameof(CastTreasureSightDuringHunt), Section = "treasure_hunt")]
    public int TreasureSightEveryNLocations { get; set; } = 10;

    [IntRange(1, 50, Order = 11, Section = "treasure_hunt")]
    public int HuntMaxLevel { get; set; } = 50;

    /// <summary>Only visit silver coffers on Treasure Hunt (skip bronze pads).</summary>
    [Checkbox(Order = 12, Section = "treasure_hunt")]
    public bool HuntSilverChestsOnly { get; set; } = false;

    /// <summary>
    ///     Illegal Mode auto-hunt and Mob Farmer yield-to-hunt: start when bronze fill is at least
    ///     this percent of 30 (or silver meets its own threshold).
    /// </summary>
    [IntRange(0, 100, Order = 13, Section = "treasure_hunt")]
    public int HuntMinBronzePercent { get; set; } = 50;

    /// <summary>
    ///     Illegal Mode auto-hunt and Mob Farmer yield-to-hunt: start when silver fill is at least
    ///     this percent of 8 (or bronze meets its own threshold).
    /// </summary>
    [IntRange(0, 100, Order = 14, Section = "treasure_hunt")]
    public int HuntMinSilverPercent { get; set; } = 50;

    /// <summary>
    ///     How close (yalms) before an empty pad is trusted and skipped. Lower walks closer
    ///     (safer when chests load late); higher skips from farther away.
    /// </summary>
    [FloatRange(10f, 60f, Order = 15, Section = "treasure_hunt")]
    public float EmptyPadTrustDistance { get; set; } = 60f;

    /// <summary>Pause treasure hunting during Ashkin / unsafe weather windows (South Horn).</summary>
    [Checkbox(Order = 16, Section = "treasure_hunt")]
    public bool SkipUnsafeTreasureWindows { get; set; } = true;

    /// <summary>Use real Ninja Hide near high-knowledge hostiles while hunting coffers or carrots.</summary>
    [Checkbox(Order = 17, Section = "ninja_hide")]
    public bool UseNinjaHideOnDangerousRoutes { get; set; } = false;

    /// <summary>While Hide is up, swap to Phantom Thief and cast Occult Sprint for move speed.</summary>
    [Checkbox(Order = 18, Section = "ninja_hide")]
    public bool UseOccultSprintWhileHidden { get; set; } = false;

    /// <summary>Gearset number (1-based) that equips Ninja. 0 = already on Ninja only.</summary>
    [IntRange(0, 100, Order = 19, Section = "ninja_hide")]
    public int NinjaGearsetNumber { get; set; } = 0;

    /// <summary>Hide when mob knowledge ≥ player knowledge + this offset.</summary>
    [IntRange(-5, 10, Order = 20, Section = "ninja_hide")]
    public int KnowledgeHideOffset { get; set; } = 0;

    /// <summary>Start Hide when a knowledge threat is within this distance (yalms).</summary>
    [FloatRange(5f, 40f, Order = 21, Section = "ninja_hide")]
    public float KnowledgeThreatEnterDistance { get; set; } = 10f;

    /// <summary>Clear Hide requirement when threats are beyond this distance (yalms).</summary>
    [FloatRange(10f, 60f, Order = 22, Section = "ninja_hide")]
    public float KnowledgeThreatExitDistance { get; set; } = 20f;

    /// <summary>
    /// Authored segment id the last South Horn hunt opened on. The next hunt rotates to the
    /// following segment so consecutive runs never start in the same place.
    /// </summary>
    [ConfigHidden]
    public string? LastSouthHornStartSegment { get; set; }
}
