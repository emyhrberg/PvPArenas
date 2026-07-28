using System.Linq;
using Terraria.ModLoader.Config;

namespace PvPArenas.Core.Configs.ConfigElements.LoadoutItems;

internal static class TagUtilities
{
    internal static string Tag(ItemDefinition definition) => definition?.Type > 0 ? $"[i:{definition.Type}]" : "";
    internal static string Join(params string[] parts) => string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
}
