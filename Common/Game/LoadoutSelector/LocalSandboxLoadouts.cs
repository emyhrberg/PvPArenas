using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ID;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;

namespace PvPArenas.Common.Game.LoadoutSelector;

/// <summary>Identifies one editable slot inside a sandbox loadout.</summary>
internal enum SandboxSlotKind : byte
{
    Head, Body, Legs,
    Accessory1, Accessory2, Accessory3, Accessory4, Accessory5,
    GrapplingHook, Mount,
    Inventory
}

/// <summary>A single sandbox slot: an equipment kind, or an inventory index when Kind == Inventory.</summary>
internal readonly record struct SandboxSlot(SandboxSlotKind Kind, int Index = 0);

/// <summary>
/// Per-player local store of customized "Sandbox mode" loadouts. Preset loadouts are
/// server config and shared, so a player's freely-built sandbox loadouts live here on the
/// client and are merged in by <see cref="ArenaPlayer.ResolveBaseLoadout"/>.
/// </summary>
internal static class LocalSandboxLoadouts
{
    internal const int InventorySlots = 30;

    private static string Folder => Path.Combine(Main.SavePath, "PvPArenas");
    private static string FilePath => Path.Combine(Folder, "SandboxLoadouts.nbt");

    private static readonly Dictionary<string, Loadout> store = [];
    private static bool loaded;

    /// <summary>Returns a fresh clone of the stored sandbox loadout for the given option (empty if unset).</summary>
    internal static Loadout Get(BossFightPreset preset, int loadoutIndex)
    {
        Load();
        return Clone(store.TryGetValue(Key(preset, loadoutIndex), out Loadout stored) ? stored : null);
    }

    internal static void SetSlot(BossFightPreset preset, int loadoutIndex, SandboxSlot slot, int itemType, int stack)
    {
        Load();

        string key = Key(preset, loadoutIndex);
        if (!store.TryGetValue(key, out Loadout loadout))
            store[key] = loadout = Empty();

        ItemDefinition definition = new(Math.Max(ItemID.None, itemType));

        switch (slot.Kind)
        {
            case SandboxSlotKind.Head: loadout.Armor.Head = definition; break;
            case SandboxSlotKind.Body: loadout.Armor.Body = definition; break;
            case SandboxSlotKind.Legs: loadout.Armor.Legs = definition; break;
            case SandboxSlotKind.Accessory1: loadout.Accessories.Accessory1 = definition; break;
            case SandboxSlotKind.Accessory2: loadout.Accessories.Accessory2 = definition; break;
            case SandboxSlotKind.Accessory3: loadout.Accessories.Accessory3 = definition; break;
            case SandboxSlotKind.Accessory4: loadout.Accessories.Accessory4 = definition; break;
            case SandboxSlotKind.Accessory5: loadout.Accessories.Accessory5 = definition; break;
            case SandboxSlotKind.GrapplingHook: loadout.Equipment.GrapplingHook = definition; break;
            case SandboxSlotKind.Mount: loadout.Equipment.Mount = definition; break;
            case SandboxSlotKind.Inventory:
                if (slot.Index >= 0 && slot.Index < loadout.Inventory.Count)
                    loadout.Inventory[slot.Index] = new LoadoutItem { Item = definition, Stack = Math.Max(1, stack) };
                break;
        }

        Save();
    }

    private static Loadout Empty()
    {
        Loadout loadout = new()
        {
            Armor = new(),
            Accessories = new(),
            Equipment = new(),
            Inventory = []
        };

        for (int i = 0; i < InventorySlots; i++)
            loadout.Inventory.Add(new LoadoutItem { Item = new ItemDefinition(ItemID.None), Stack = 1 });

        return loadout;
    }

    private static Loadout Clone(Loadout source)
    {
        Loadout loadout = Empty();
        if (source == null)
            return loadout;

        loadout.Armor.Head = source.Armor?.Head ?? loadout.Armor.Head;
        loadout.Armor.Body = source.Armor?.Body ?? loadout.Armor.Body;
        loadout.Armor.Legs = source.Armor?.Legs ?? loadout.Armor.Legs;
        loadout.Accessories.Accessory1 = source.Accessories?.Accessory1 ?? loadout.Accessories.Accessory1;
        loadout.Accessories.Accessory2 = source.Accessories?.Accessory2 ?? loadout.Accessories.Accessory2;
        loadout.Accessories.Accessory3 = source.Accessories?.Accessory3 ?? loadout.Accessories.Accessory3;
        loadout.Accessories.Accessory4 = source.Accessories?.Accessory4 ?? loadout.Accessories.Accessory4;
        loadout.Accessories.Accessory5 = source.Accessories?.Accessory5 ?? loadout.Accessories.Accessory5;
        loadout.Equipment.GrapplingHook = source.Equipment?.GrapplingHook ?? loadout.Equipment.GrapplingHook;
        loadout.Equipment.Mount = source.Equipment?.Mount ?? loadout.Equipment.Mount;

        for (int i = 0; i < InventorySlots && i < (source.Inventory?.Count ?? 0); i++)
        {
            LoadoutItem entry = source.Inventory[i];
            if (entry?.Item == null)
                continue;
            loadout.Inventory[i] = new LoadoutItem { Item = entry.Item, Stack = Math.Max(1, entry.Stack) };
        }

        return loadout;
    }

    private static string Key(BossFightPreset preset, int loadoutIndex)
    {
        string name = loadoutIndex >= 0 && loadoutIndex < (preset?.Loadouts?.Count ?? 0)
            ? preset.Loadouts[loadoutIndex]?.Name
            : null;
        return $"sandbox:{preset?.Boss?.Type ?? 0}:{loadoutIndex}:{name}";
    }

    private static void Load()
    {
        if (loaded)
            return;
        loaded = true;

        if (!System.IO.File.Exists(FilePath))
            return;

        try
        {
            TagCompound root = TagIO.FromFile(FilePath);
            foreach (TagCompound tag in root.GetList<TagCompound>("Loadouts"))
                store[tag.GetString("Key")] = Deserialize(tag);
        }
        catch (Exception e)
        {
            Log.Error($"Sandbox loadout load failed: {e}");
        }
    }

    private static void Save()
    {
        if (Main.dedServ)
            return;

        try
        {
            Directory.CreateDirectory(Folder);
            TagCompound root = new()
            {
                ["Loadouts"] = store.Select(pair =>
                {
                    TagCompound tag = Serialize(pair.Value);
                    tag["Key"] = pair.Key;
                    return tag;
                }).ToList()
            };
            TagIO.ToFile(root, FilePath);
        }
        catch (Exception e)
        {
            Log.Error($"Sandbox loadout save failed: {e}");
        }
    }

    private static TagCompound Serialize(Loadout loadout)
    {
        List<int> inventoryTypes = [];
        List<int> inventoryStacks = [];
        for (int i = 0; i < InventorySlots; i++)
        {
            LoadoutItem entry = i < (loadout.Inventory?.Count ?? 0) ? loadout.Inventory[i] : null;
            inventoryTypes.Add(entry?.Item?.Type ?? ItemID.None);
            inventoryStacks.Add(Math.Max(1, entry?.Stack ?? 1));
        }

        return new TagCompound
        {
            ["Equip"] = new List<int>
            {
                loadout.Armor?.Head?.Type ?? 0, loadout.Armor?.Body?.Type ?? 0, loadout.Armor?.Legs?.Type ?? 0,
                loadout.Accessories?.Accessory1?.Type ?? 0, loadout.Accessories?.Accessory2?.Type ?? 0,
                loadout.Accessories?.Accessory3?.Type ?? 0, loadout.Accessories?.Accessory4?.Type ?? 0,
                loadout.Accessories?.Accessory5?.Type ?? 0,
                loadout.Equipment?.GrapplingHook?.Type ?? 0, loadout.Equipment?.Mount?.Type ?? 0
            },
            ["InvTypes"] = inventoryTypes,
            ["InvStacks"] = inventoryStacks
        };
    }

    private static Loadout Deserialize(TagCompound tag)
    {
        Loadout loadout = Empty();
        List<int> equip = tag.GetList<int>("Equip").ToList();
        int E(int i) => i < equip.Count ? equip[i] : 0;

        loadout.Armor.Head = new ItemDefinition(E(0));
        loadout.Armor.Body = new ItemDefinition(E(1));
        loadout.Armor.Legs = new ItemDefinition(E(2));
        loadout.Accessories.Accessory1 = new ItemDefinition(E(3));
        loadout.Accessories.Accessory2 = new ItemDefinition(E(4));
        loadout.Accessories.Accessory3 = new ItemDefinition(E(5));
        loadout.Accessories.Accessory4 = new ItemDefinition(E(6));
        loadout.Accessories.Accessory5 = new ItemDefinition(E(7));
        loadout.Equipment.GrapplingHook = new ItemDefinition(E(8));
        loadout.Equipment.Mount = new ItemDefinition(E(9));

        List<int> types = tag.GetList<int>("InvTypes").ToList();
        List<int> stacks = tag.GetList<int>("InvStacks").ToList();
        for (int i = 0; i < InventorySlots && i < types.Count; i++)
            loadout.Inventory[i] = new LoadoutItem
            {
                Item = new ItemDefinition(types[i]),
                Stack = i < stacks.Count ? Math.Max(1, stacks[i]) : 1
            };

        return loadout;
    }
}
