using PvPArenas.Common.Game.LoadoutSelector;
using Terraria.ModLoader.Config.UI;

namespace PvPArenas.Core.Configs.ConfigElements.LoadoutItems;

internal sealed class LoadoutAccessoriesElement : ObjectElement
{
    public override void OnBind()
    {
        base.OnBind();

        TextDisplayFunction = () =>
        {
            string baseLabel = Label ?? MemberInfo?.Name ?? "LoadoutAccessories";
            string summary = AccessoriesSummary(Value as LoadoutAccessories);
            return string.IsNullOrWhiteSpace(summary) ? baseLabel : $"{baseLabel} {summary}";
        };
    }

    private static string AccessoriesSummary(LoadoutAccessories a)
    {
        if (a == null)
            return "";

        return TagUtilities.Join(
            TagUtilities.Tag(a.Accessory1),
            TagUtilities.Tag(a.Accessory2),
            TagUtilities.Tag(a.Accessory3),
            TagUtilities.Tag(a.Accessory4),
            TagUtilities.Tag(a.Accessory5));
    }
}
