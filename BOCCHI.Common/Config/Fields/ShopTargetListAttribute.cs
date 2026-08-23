using BOCCHI.Common.Config;
using BOCCHI.Common.Config.Renderers;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config.Fields;

/// <summary>Renders the structured Antiquarian shopping target list.</summary>
public sealed class ShopTargetListAttribute()
    : UIFieldAttribute(typeof(ShopTargetListRenderer));
