using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPArenas.Common.Game.LoadoutSelector;

/// <summary>
/// Full-item picker shown when a slot of a "Sandbox mode" loadout is clicked. Isolates the
/// chosen slot, hides the rest of the loadout panel, and presents a searchable, scrollable
/// grid of every item. Picking one writes it into the local sandbox loadout store.
/// </summary>
internal static class SandboxLoadoutEditor
{
    private const int DesignWidth = 540, DesignHeight = 300;
    private const int HeaderHeight = 34, SearchHeight = 28, CellStep = 38, IconSize = 32, ScrollbarWidth = 12, Padding = 12;

    private static readonly Color PanelFill = new(45, 61, 132);
    private static readonly Color PanelEdge = new(70, 89, 165);
    private static readonly Color SlotFill = new(30, 43, 98);
    private static readonly Color DarkEdge = new(6, 12, 38);
    private static readonly Color HoverEdge = new(244, 209, 74);
    private static readonly Color Yellow = new(246, 216, 72);

    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;

    private static bool open;
    private static BossFightPreset preset;
    private static int loadoutIndex;
    private static SandboxSlot slot;

    private static string search = "";
    private static string filterSource;
    private static readonly List<int> filtered = [];
    private static float scrollRows;
    private static bool draggingScrollbar;

    internal static bool IsOpen => open;
    internal static SandboxSlot EditingSlot => slot;

    internal static void Open(BossFightPreset targetPreset, int targetLoadoutIndex, SandboxSlot targetSlot)
    {
        preset = targetPreset;
        loadoutIndex = targetLoadoutIndex;
        slot = targetSlot;
        search = "";
        filterSource = null;
        scrollRows = 0f;
        draggingScrollbar = false;
        open = true;
        RebuildFilter();
    }

    internal static void Close()
    {
        open = false;
        preset = null;
    }

    /// <summary>Opens the picker for a clicked sandbox slot. Returns true if the click was consumed.</summary>
    internal static bool HandleSlotClick(BossFightPreset targetPreset, int targetLoadoutIndex, SandboxSlot targetSlot,
        Rectangle cell, Point mouse)
    {
        if (!cell.Contains(mouse))
            return false;

        Main.LocalPlayer.mouseInterface = true;

        if (!Main.mouseLeft || !Main.mouseLeftRelease)
            return false;

        Main.mouseLeftRelease = false;
        Open(targetPreset, targetLoadoutIndex, targetSlot);
        return true;
    }

    internal static void DrawOverlay(int top, float alpha)
    {
        if (!open || preset == null)
        {
            open = false;
            return;
        }

        PlayerInput.WritingText = true;
        string typed = Main.GetInputText(search);
        if (typed != null && typed != search)
        {
            search = typed;
            RebuildFilter();
        }

        float scale = Math.Min(1f, Math.Min((Main.screenWidth - 12f) / DesignWidth,
            (Main.screenHeight - top - 4f) / DesignHeight));
        int S(float value) => Math.Max(1, (int)MathF.Round(value * scale));

        Rectangle panel = new((Main.screenWidth - S(DesignWidth)) / 2, top, S(DesignWidth), S(DesignHeight));
        DrawPanel(panel, PanelFill, PanelEdge, S(9), alpha);

        Point mouse = new(Main.mouseX, Main.mouseY);
        if (panel.Contains(mouse))
            Main.LocalPlayer.mouseInterface = true;

        // Header: the slot being edited + a Back button.
        Item current = SlotItem();
        Rectangle iconBox = new(panel.X + S(Padding), panel.Y + S(4), S(28), S(28));
        DrawPanel(iconBox, SlotFill, DarkEdge, S(5), alpha);
        if (!current.IsAir)
            ItemSlot.DrawItemIcon(current, 31, Main.spriteBatch, iconBox.Center.ToVector2(), scale * .8f,
                iconBox.Width - S(6), Color.White * alpha);

        Text($"Choose an item for {SlotLabel(slot)}", new Vector2(iconBox.Right + S(8), panel.Y + S(9)),
            Color.White, .8f * scale, panel.Width - S(120), 0f);

        Rectangle backButton = new(panel.Right - S(Padding) - S(58), panel.Y + S(5), S(58), S(24));
        bool backHover = backButton.Contains(mouse);
        DrawPanel(backButton, backHover ? PanelEdge : SlotFill, backHover ? HoverEdge : DarkEdge, S(5), alpha);
        Text("Back", new Vector2(backButton.Center.X, backButton.Y + S(4)), Color.White, .74f * scale, backButton.Width - S(8));
        if (backHover)
        {
            Main.LocalPlayer.mouseInterface = true;
            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease = false;
                Close();
                return;
            }
        }

        // Search bar.
        Rectangle searchBar = new(panel.X + S(Padding), panel.Y + S(HeaderHeight), panel.Width - S(Padding) * 2, S(SearchHeight));
        DrawPanel(searchBar, SlotFill, DarkEdge, S(5), alpha);
        string shown = string.IsNullOrEmpty(search)
            ? "Search items..."
            : search + (Main.GlobalTimeWrappedHourly % 1f < .5f ? "|" : "");
        Text(shown, new Vector2(searchBar.X + S(8), searchBar.Y + S(5)),
            string.IsNullOrEmpty(search) ? Color.Gray : Color.White, .78f * scale, searchBar.Width - S(16), 0f);

        // Grid area.
        Rectangle grid = new(panel.X + S(Padding), searchBar.Bottom + S(6),
            panel.Width - S(Padding) * 2 - S(ScrollbarWidth) - S(4), panel.Bottom - searchBar.Bottom - S(6) - S(Padding));
        DrawGrid(grid, scale, S, mouse, alpha);
    }

    private static void DrawGrid(Rectangle grid, float scale, Func<float, int> S, Point mouse, float alpha)
    {
        int step = S(CellStep);
        int columns = Math.Max(1, grid.Width / step);
        int visibleRows = Math.Max(1, grid.Height / step);
        int totalRows = (filtered.Count + columns - 1) / columns;
        int maxScroll = Math.Max(0, totalRows - visibleRows);

        if (grid.Contains(mouse) && PlayerInput.ScrollWheelDeltaForUI != 0)
            scrollRows -= Math.Sign(PlayerInput.ScrollWheelDeltaForUI);
        scrollRows = Math.Clamp(scrollRows, 0, maxScroll);
        int firstRow = (int)Math.Round(scrollRows);

        for (int row = 0; row < visibleRows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = (firstRow + row) * columns + column;
                if (index >= filtered.Count)
                    break;

                Rectangle cell = new(grid.X + column * step, grid.Y + row * step, S(IconSize), S(IconSize));
                int type = filtered[index];
                Item sample = ContentSamples.ItemsByType[type];

                Texture2D back = TextureAssets.InventoryBack.Value;
                Main.spriteBatch.Draw(back, cell.Center.ToVector2(), null, Color.White * alpha, 0f,
                    back.Size() / 2f, cell.Width / (float)back.Width, SpriteEffects.None, 0f);
                ItemSlot.DrawItemIcon(sample, 31, Main.spriteBatch, cell.Center.ToVector2(), scale * .82f,
                    cell.Width - 6f, Color.White * alpha);

                if (!cell.Contains(mouse))
                    continue;

                Main.LocalPlayer.mouseInterface = true;
                Main.HoverItem = sample.Clone();
                Main.hoverItemName = sample.Name;

                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    Main.mouseLeftRelease = false;
                    PickItem(type);
                    return;
                }
            }
        }

        DrawScrollbar(grid, S, mouse, firstRow, visibleRows, totalRows, maxScroll, alpha);
    }

    private static void DrawScrollbar(Rectangle grid, Func<float, int> S, Point mouse, int firstRow, int visibleRows,
        int totalRows, int maxScroll, float alpha)
    {
        Rectangle track = new(grid.Right + S(4), grid.Y, S(ScrollbarWidth), grid.Height);
        DrawPanel(track, SlotFill, DarkEdge, S(4), alpha);

        if (maxScroll <= 0)
            return;

        float ratio = visibleRows / (float)totalRows;
        int handleHeight = Math.Max(S(16), (int)(track.Height * ratio));
        int travel = track.Height - handleHeight;
        int handleY = track.Y + (int)(travel * (firstRow / (float)maxScroll));
        Rectangle handle = new(track.X, handleY, track.Width, handleHeight);
        DrawPanel(handle, PanelEdge, HoverEdge, S(4), alpha);

        bool overTrack = track.Contains(mouse);
        if (overTrack && Main.mouseLeft && Main.mouseLeftRelease)
            draggingScrollbar = true;
        if (!Main.mouseLeft)
            draggingScrollbar = false;

        if (draggingScrollbar && travel > 0)
        {
            float grabbed = Math.Clamp((mouse.Y - track.Y - handleHeight / 2f) / travel, 0f, 1f);
            scrollRows = grabbed * maxScroll;
            Main.LocalPlayer.mouseInterface = true;
        }
    }

    private static void PickItem(int type)
    {
        Item sample = ContentSamples.ItemsByType[type];
        int stack = sample.maxStack > 1 ? sample.maxStack : 1;
        LocalSandboxLoadouts.SetSlot(preset, loadoutIndex, slot, type, stack);
        ArenaPlayer.RequestLoadoutSelect(loadoutIndex);
        LoadoutPreviewDrawer.Invalidate();
        Close();
    }

    private static Item SlotItem()
    {
        Loadout loadout = LocalSandboxLoadouts.Get(preset, loadoutIndex);
        int type = slot.Kind switch
        {
            SandboxSlotKind.Head => loadout.Armor?.Head?.Type ?? 0,
            SandboxSlotKind.Body => loadout.Armor?.Body?.Type ?? 0,
            SandboxSlotKind.Legs => loadout.Armor?.Legs?.Type ?? 0,
            SandboxSlotKind.Accessory1 => loadout.Accessories?.Accessory1?.Type ?? 0,
            SandboxSlotKind.Accessory2 => loadout.Accessories?.Accessory2?.Type ?? 0,
            SandboxSlotKind.Accessory3 => loadout.Accessories?.Accessory3?.Type ?? 0,
            SandboxSlotKind.Accessory4 => loadout.Accessories?.Accessory4?.Type ?? 0,
            SandboxSlotKind.Accessory5 => loadout.Accessories?.Accessory5?.Type ?? 0,
            SandboxSlotKind.GrapplingHook => loadout.Equipment?.GrapplingHook?.Type ?? 0,
            SandboxSlotKind.Mount => loadout.Equipment?.Mount?.Type ?? 0,
            SandboxSlotKind.Inventory => slot.Index < (loadout.Inventory?.Count ?? 0)
                ? loadout.Inventory[slot.Index]?.Item?.Type ?? 0
                : 0,
            _ => 0
        };

        Item item = new();
        if (type > 0)
            item.SetDefaults(type);
        return item;
    }

    private static string SlotLabel(SandboxSlot target) => target.Kind switch
    {
        SandboxSlotKind.Head => "Helmet",
        SandboxSlotKind.Body => "Chestplate",
        SandboxSlotKind.Legs => "Leggings",
        SandboxSlotKind.Accessory1 or SandboxSlotKind.Accessory2 or SandboxSlotKind.Accessory3
            or SandboxSlotKind.Accessory4 or SandboxSlotKind.Accessory5 => "an accessory",
        SandboxSlotKind.GrapplingHook => "the grappling hook",
        SandboxSlotKind.Mount => "the mount",
        _ => $"slot {target.Index + 1}"
    };

    private static void RebuildFilter()
    {
        if (filterSource == search)
            return;
        filterSource = search;

        filtered.Clear();
        string query = search?.Trim() ?? "";
        int count = ItemLoader.ItemCount;
        for (int type = 1; type < count; type++)
        {
            if (!ContentSamples.ItemsByType.TryGetValue(type, out Item sample) || sample.IsAir)
                continue;
            if (query.Length > 0 && (sample.Name?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                continue;
            filtered.Add(type);
        }

        scrollRows = 0f;
    }

    private static void DrawPanel(Rectangle rectangle, Color fill, Color edge, int corner, float alpha)
    {
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBackground, rectangle.X, rectangle.Y,
            rectangle.Width, rectangle.Height, corner, corner, corner, corner, fill * alpha);
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBorder, rectangle.X, rectangle.Y,
            rectangle.Width, rectangle.Height, corner, corner, corner, corner, edge * alpha);
    }

    private static void Text(string value, Vector2 position, Color color, float scale, float maxWidth, float anchor = .5f)
    {
        float width = FontAssets.MouseText.Value.MeasureString(value).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(Main.spriteBatch, value, position, color, scale, anchor);
    }
}
