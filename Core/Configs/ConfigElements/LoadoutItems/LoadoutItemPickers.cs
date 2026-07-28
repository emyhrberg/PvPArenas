using Terraria.ID;

namespace PvPArenas.Core.Configs.ConfigElements.LoadoutItems;

internal sealed class ItemHeadDefinitionElement : FilteredItemDefinitionElement
{
    protected override bool IsValidItem(int type) => type == ItemID.None || ContentSamples.ItemsByType.TryGetValue(type, out Item item) && item.headSlot >= 0;
}

internal sealed class ItemBodyDefinitionElement : FilteredItemDefinitionElement
{
    protected override bool IsValidItem(int type) => type == ItemID.None || ContentSamples.ItemsByType.TryGetValue(type, out Item item) && item.bodySlot >= 0;
}

internal sealed class ItemLegsDefinitionElement : FilteredItemDefinitionElement
{
    protected override bool IsValidItem(int type) => type == ItemID.None || ContentSamples.ItemsByType.TryGetValue(type, out Item item) && item.legSlot >= 0;
}

internal sealed class ItemAccessoryDefinitionElement : FilteredItemDefinitionElement
{
    protected override bool IsValidItem(int type) => type == ItemID.None || ContentSamples.ItemsByType.TryGetValue(type, out Item item) && item.accessory;
}

internal sealed class ItemMountDefinitionElement : FilteredItemDefinitionElement
{
    protected override bool IsValidItem(int type) => type == ItemID.None || ContentSamples.ItemsByType.TryGetValue(type, out Item item) && item.mountType != MountID.None;
}

internal sealed class ItemGrapplingHookDefinitionElement : FilteredItemDefinitionElement
{
    protected override bool IsValidItem(int type)
    {
        if (type == ItemID.None)
            return true;
        int projectile = ContentSamples.ItemsByType.TryGetValue(type, out Item item) ? item.shoot : ProjectileID.None;
        return projectile > ProjectileID.None && projectile < Main.projHook.Length && Main.projHook[projectile];
    }
}
