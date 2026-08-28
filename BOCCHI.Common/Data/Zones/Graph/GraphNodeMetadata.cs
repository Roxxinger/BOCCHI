using BOCCHI.Common.Data.Aethernet;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Graph;

public interface INodeMetadata;
public class BlankNodeMetadata : INodeMetadata;

public class CarrotNodeMetadata : INodeMetadata
{
    public int Level { get; set; }
}

public class PotChestNodeMetadata : INodeMetadata
{
    public int FateId { get; set; }

    public int Level { get; set; }
}

public class RerollPotChestNodeMetadata : INodeMetadata
{
    public int Level { get; set; }
}

public class ActivityNodeMetadata : INodeMetadata
{
    public int Id { get; set; }

    public uint? PreferredAethernetId { get; set; }

    /// <summary>Live LGB registration size for CE nodes; 0 until geometry is applied.</summary>
    public float CombatRadius { get; set; }

    /// <summary>Circle or axis-aligned square join area.</summary>
    public ActivityAreaShape AreaShape { get; set; } = ActivityAreaShape.Circle;

    /// <summary>Stand radius when tighter than CombatRadius; 0 → use CombatRadius.</summary>
    public float StandRadius { get; set; }
}

public class TeleportNodeMetadata : INodeMetadata
{
    public uint AetheryteId { get; set; } = 0;

    public Vector3 Destination { get; set; } = Vector3.Zero;

    /// <summary>Solid body / Lifestream radius for this pad (matches <c>AethernetData.DeadRadius</c>).</summary>
    public float DeadRadius { get; set; } = AethernetData.DefaultDeadRadius;
}

public enum TreasureType
{
    Silver,
    Bronze
}

public class TreasureNodeMetadata : INodeMetadata
{
    public TreasureType Type { get; set; }

    public int Level { get; set; }
}
