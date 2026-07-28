using PvPArenas.Common.Game.LoadoutSelector;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;

namespace PvPArenas.Core.Configs.ConfigElements.LoadoutItems;

internal sealed class LoadoutArmorElement : ObjectElement
{
    public override void OnBind()
    {
        base.OnBind();

        TextDisplayFunction = () =>
        {
            string baseLabel = Label ?? MemberInfo?.Name ?? "LoadoutArmor";
            string summary = ArmorSummary(Value as LoadoutArmor);
            return string.IsNullOrWhiteSpace(summary) ? baseLabel : $"{baseLabel} {summary}";
        };
    }

    private static string ArmorSummary(LoadoutArmor a)
    {
        if (a == null)
            return "";

        return TagUtilities.Join(
            TagUtilities.Tag(a.Head),
            TagUtilities.Tag(a.Body),
            TagUtilities.Tag(a.Legs));
    }
}
