using System.Numerics;

namespace BOCCHI.Common.Services;

/// <summary>
///     Small random 2D offset around a pathfind target so loops do not retrace the
///     exact same line every run, making movement look more natural. The offset is
///     intentionally bounded and applied only to the walked target — never to the
///     activity's true interact position — so it stays within the interaction radius.
/// </summary>
public static class PathJitter
{
    private static readonly Random Random = new();

    /// <summary>
    ///     Offsets <paramref name="point"/> in the XZ plane by a uniform disk of radius
    ///     <paramref name="radius"/> meters. Returns the point unchanged when
    ///     <paramref name="radius"/> &lt;= 0 (disabled).
    /// </summary>
    public static Vector3 Roll(Vector3 point, float radius)
    {
        if (radius <= 0f)
        {
            return point;
        }

        // Uniform disk sampling (sqrt) prevents clustering near the centre.
        double angle = Random.NextDouble() * Math.PI * 2.0;
        double r = radius * Math.Sqrt(Random.NextDouble());

        return new Vector3(
            point.X + (float)(Math.Cos(angle) * r),
            point.Y,
            point.Z + (float)(Math.Sin(angle) * r));
    }
}