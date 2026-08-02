using Microsoft.Xna.Framework.Graphics;
using PvPArenas.Common.AdminTools.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;
using Terraria.WorldBuilding;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

/// <summary>Compact live world diagnostics arranged as hoverable multi-column cards.</summary>
internal sealed class WorldGenDebugView : UIPanel
{
    private sealed record DebugRow(string Label, string Value, string Details);
    private sealed record DebugSection(string Title, string Details, AdminUIIcon Icon, List<DebugRow> Rows);

    private const float CardGap = 10f;
    private const float CardHeaderHeight = 29f;
    private const float RowHeight = 20f;
    private const float CardPadding = 8f;
    private const float LabelScale = .62f;
    private const float ValueScale = .62f;
    private const float HeaderScale = .75f;

    private static readonly Color HeaderColor = new(255, 218, 130);
    private static readonly Color LabelColor = new(176, 202, 232);
    private static readonly Color ValueColor = Color.White;

    private readonly List<DebugSection> sections = [];
    private float scroll;
    private float contentHeight;

    internal WorldGenDebugView()
    {
        SetPadding(8f);
        BackgroundColor = new Color(13, 18, 45) * .98f;
        BorderColor = new Color(78, 104, 190) * .8f;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsMouseHovering)
            return;

        Main.LocalPlayer.mouseInterface = true;
        if (PlayerInput.ScrollWheelDeltaForUI != 0)
        {
            float viewport = GetInnerDimensions().Height;
            scroll = Math.Clamp(scroll - Math.Sign(PlayerInput.ScrollWheelDeltaForUI) * 70f,
                0f, Math.Max(0f, contentHeight - viewport));
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        BuildSections();

        Rectangle viewport = GetInnerDimensions().ToRectangle();
        int columnCount = viewport.Width >= 900 ? 3 : viewport.Width >= 560 ? 2 : 1;
        float columnWidth = (viewport.Width - CardGap * (columnCount - 1)) / columnCount;
        float[] columnBottoms = Enumerable.Repeat(viewport.Y - scroll, columnCount).ToArray();

        foreach (DebugSection section in sections)
        {
            int column = ShortestColumn(columnBottoms);
            float height = CardHeaderHeight + section.Rows.Count * RowHeight + CardPadding;
            Rectangle card = new(
                (int)(viewport.X + column * (columnWidth + CardGap)),
                (int)columnBottoms[column],
                (int)columnWidth,
                (int)height);
            DrawSection(spriteBatch, viewport, card, section);
            columnBottoms[column] += height + CardGap;
        }

        contentHeight = columnBottoms.Max() + scroll - viewport.Y;
        float maxScroll = Math.Max(0f, contentHeight - viewport.Height);
        if (scroll > maxScroll)
            scroll = maxScroll;
    }

    private void DrawSection(SpriteBatch spriteBatch, Rectangle viewport, Rectangle card, DebugSection section)
    {
        if (!card.Intersects(viewport))
            return;

        Rectangle clipped = Rectangle.Intersect(card, viewport);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, clipped, new Color(25, 34, 73) * .96f);
        Rectangle iconBox = new(card.X + 7, card.Y + 5, 19, 19);
        if (iconBox.Intersects(viewport))
            VanillaAdminIcons.DrawFitted(spriteBatch, section.Icon, iconBox, Color.White, allowUpscale: true);
        DrawClippedLine(spriteBatch, viewport, section.Title, new Vector2(card.X + 32, card.Y + 6), HeaderColor, HeaderScale, card.Width - 41f);

        Rectangle header = new(card.X, card.Y, card.Width, (int)CardHeaderHeight);
        if (viewport.Contains(Main.MouseScreen.ToPoint()) && header.Contains(Main.MouseScreen.ToPoint()))
            Main.instance.MouseText(section.Details);

        for (int i = 0; i < section.Rows.Count; i++)
        {
            DebugRow row = section.Rows[i];
            int y = card.Y + (int)CardHeaderHeight + i * (int)RowHeight;
            Rectangle rowBox = new(card.X + 5, y, card.Width - 10, (int)RowHeight);
            if (rowBox.Bottom < viewport.Top || rowBox.Top > viewport.Bottom)
                continue;

            if (viewport.Contains(Main.MouseScreen.ToPoint()) && rowBox.Contains(Main.MouseScreen.ToPoint()))
            {
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, Rectangle.Intersect(rowBox, viewport), new Color(70, 91, 160) * .48f);
                Main.instance.MouseText($"{row.Label}: {row.Details}");
            }

            float labelWidth = card.Width * .52f;
            DrawClippedLine(spriteBatch, viewport, row.Label, new Vector2(card.X + 9, y + 3), LabelColor, LabelScale, labelWidth - 12f);
            DrawRightAligned(spriteBatch, viewport, row.Value, new Vector2(card.Right - 9, y + 3), ValueColor, ValueScale, card.Width - labelWidth - 12f);
        }

        DrawBorder(spriteBatch, viewport, card, new Color(78, 104, 190) * .72f);
    }

    private void BuildSections()
    {
        sections.Clear();

        Add("WORLD", "Active world identity, dimensions, spawn, dungeon, and clock.",
            Row("Size", $"{Main.maxTilesX}×{Main.maxTilesY}", $"{Main.maxTilesX:N0} by {Main.maxTilesY:N0} tiles"),
            Row("Name", Trim(Main.worldName, 17), Main.worldName),
            Row("Seed", Trim(Main.ActiveWorldFileData?.SeedText ?? "?", 17), Main.ActiveWorldFileData?.SeedText ?? "Unknown"),
            Row("Spawn", $"{Main.spawnTileX},{Main.spawnTileY}", $"Tile {Main.spawnTileX}, {Main.spawnTileY}"),
            Row("Dungeon", $"{Main.dungeonX},{Main.dungeonY}", $"Tile {Main.dungeonX}, {Main.dungeonY}; side {GenVars.dungeonSide}"),
            Row("Clock", Main.dayTime ? "DAY" : "NIGHT", $"World time {Main.time:F0}; {(Main.dayTime ? "day" : "night")}"));

        Add("LAYERS", "Key vertical world layers expressed as tile Y coordinates.",
            Row("Surface", $"{(int)Main.worldSurface}", $"Main.worldSurface = {Main.worldSurface:F2}"),
            Row("Rock", $"{(int)Main.rockLayer}", $"Main.rockLayer = {Main.rockLayer:F2}"),
            Row("Space end", $"{(int)(Main.worldSurface * .35)}", "Approximate lower edge of space"),
            Row("Underworld", $"{Main.maxTilesY - 200}", "Approximate top of the Underworld"));

        Rectangle desert = GenVars.UndergroundDesertLocation;
        Add("GEN VARS", "Live GenVars prerequisites used by vanilla generation passes. Hover a row for exact values.",
            Row("Surface lo/hi", $"{GenVars.worldSurfaceLow:F0}/{GenVars.worldSurfaceHigh:F0}", $"{GenVars.worldSurfaceLow:F2} / {GenVars.worldSurfaceHigh:F2}"),
            Row("Rock lo/hi", $"{GenVars.rockLayerLow:F0}/{GenVars.rockLayerHigh:F0}", $"{GenVars.rockLayerLow:F2} / {GenVars.rockLayerHigh:F2}"),
            Row("Jungle O/L/R", $"{GenVars.jungleOriginX}/{GenVars.jungleMinX}/{GenVars.jungleMaxX}", "Jungle origin, minimum X, and maximum X"),
            Row("Snow L/R", $"{GenVars.snowOriginLeft}/{GenVars.snowOriginRight}", "Snow origin left and right"),
            Row("Dungeon loc", $"{GenVars.dungeonLocation}/{GenVars.dungeonSide}", "Dungeon location and side"),
            Row("Living log", $"{GenVars.logX},{GenVars.logY}", "Living-tree log coordinate"),
            Row("Sky lakes", $"{GenVars.skyLakes}", "Configured number of sky lakes"),
            Row("Desert", $"{desert.Width}×{desert.Height}", $"{desert.X},{desert.Y} size {desert.Width}×{desert.Height}"));

        Add("WORLDGEN FLAGS", "Current generation and secret-seed switches.",
            BoolRow("Generating", WorldGen.gen || WorldGen.generatingWorld, $"gen={WorldGen.gen}; generatingWorld={WorldGen.generatingWorld}"),
            BoolRow("Tile actions off", WorldGen.noTileActions, $"noTileActions={WorldGen.noTileActions}"),
            BoolRow("Map updates off", WorldGen.noMapUpdate, $"noMapUpdate={WorldGen.noMapUpdate}"),
            BoolRow("Drunk", WorldGen.drunkWorldGen, $"drunkWorldGen={WorldGen.drunkWorldGen}"),
            BoolRow("For the worthy", WorldGen.getGoodWorldGen, $"getGoodWorldGen={WorldGen.getGoodWorldGen}"),
            BoolRow("Anniversary", WorldGen.tenthAnniversaryWorldGen, $"tenthAnniversaryWorldGen={WorldGen.tenthAnniversaryWorldGen}"),
            BoolRow("Bees / Remix", WorldGen.notTheBees || WorldGen.remixWorldGen, $"notTheBees={WorldGen.notTheBees}; remix={WorldGen.remixWorldGen}"),
            BoolRow("No traps / Zenith", WorldGen.noTrapsWorldGen || WorldGen.everythingWorldGen, $"noTraps={WorldGen.noTrapsWorldGen}; everything={WorldGen.everythingWorldGen}"));

        WorldGenDebugStats stats = ModContent.GetInstance<WorldGenDebugStats>();
        WorldGenDebugStats.Snapshot snap = stats.Latest;
        Add("TILE SCAN", "Budgeted full-world scan; values update after each complete sweep.",
            Row("Scan", $"{stats.ScanProgress:P0}", $"{stats.ScanProgress:P1} complete; last sweep {snap.SweepSeconds:F2} seconds"),
            CountRow("Active", snap.Active),
            CountRow("Air", snap.Air),
            CountRow("Walls", snap.Walls),
            Row("Scanned", Compact(snap.ScannedTiles), $"{snap.ScannedTiles:N0} tile positions in the last complete sweep"));

        Add("LIQUIDS", "Tile positions containing each liquid type.",
            CountRow("Water", snap.Water),
            CountRow("Lava", snap.Lava),
            CountRow("Honey", snap.Honey),
            CountRow("Shimmer", snap.Shimmer));

        Add("WIRING + SHAPE", "World-wide wiring, actuator, shape, paint, and coating counts.",
            CountRow("Red wire", snap.RedWire),
            CountRow("Blue wire", snap.BlueWire),
            CountRow("Green wire", snap.GreenWire),
            CountRow("Yellow wire", snap.YellowWire),
            CountRow("Actuators", snap.Actuators),
            CountRow("Actuated", snap.Actuated),
            CountRow("Half blocks", snap.HalfBricks),
            CountRow("Slopes", snap.Slopes),
            CountRow("Painted", snap.Painted),
            CountRow("Coated", snap.Coated));

        Add("TOP TILES", "Most common active tile types from the last complete scan.",
            snap.TopTiles.Select(entry => Row(Trim(TileName(entry.Type), 19), Compact(entry.Count), $"{TileName(entry.Type)} ({entry.Type}): {entry.Count:N0}")).ToArray());

        Add("TOP WALLS", "Most common wall types from the last complete scan.",
            snap.TopWalls.Select(entry => Row(Trim(WallName(entry.Type), 19), Compact(entry.Count), $"{WallName(entry.Type)} ({entry.Type}): {entry.Count:N0}")).ToArray());

        Add("ENTITIES", "Current active world object and entity counts.",
            CountRow("Chests", Main.chest.Count(chest => chest != null)),
            CountRow("Signs", Main.sign.Count(sign => sign != null)),
            CountRow("Tile entities", TileEntity.ByID.Count),
            CountRow("NPCs", Main.npc.Count(npc => npc.active)),
            CountRow("Items", Main.item.Count(item => item.active)),
            CountRow("Projectiles", Main.projectile.Count(projectile => projectile.active)));

        SceneMetrics metrics = Main.SceneMetrics;
        Add("NEARBY BIOME", "Scene metrics around the local player; these determine biome activation.",
            Row("Jungle", Compact(metrics.JungleTileCount), $"{metrics.JungleTileCount:N0}; threshold {SceneMetrics.JungleTileThreshold:N0}"),
            Row("Evil / Blood", $"{Compact(metrics.EvilTileCount)}/{Compact(metrics.BloodTileCount)}", $"Evil {metrics.EvilTileCount:N0}; Blood {metrics.BloodTileCount:N0}"),
            Row("Holy / Snow", $"{Compact(metrics.HolyTileCount)}/{Compact(metrics.SnowTileCount)}", $"Holy {metrics.HolyTileCount:N0}; Snow {metrics.SnowTileCount:N0}"),
            Row("Sand / Shroom", $"{Compact(metrics.SandTileCount)}/{Compact(metrics.MushroomTileCount)}", $"Sand {metrics.SandTileCount:N0}; Mushroom {metrics.MushroomTileCount:N0}"),
            Row("Dungeon / Meteor", $"{Compact(metrics.DungeonTileCount)}/{Compact(metrics.MeteorTileCount)}", $"Dungeon {metrics.DungeonTileCount:N0}; Meteor {metrics.MeteorTileCount:N0}"),
            Row("Shimmer / Grave", $"{Compact(metrics.ShimmerTileCount)}/{Compact(metrics.GraveyardTileCount)}", $"Shimmer {metrics.ShimmerTileCount:N0}; Graveyard {metrics.GraveyardTileCount:N0}"));

        BuildHoveredTile();
    }

    private void BuildHoveredTile()
    {
        int x = (int)(Main.MouseWorld.X / 16f);
        int y = (int)(Main.MouseWorld.Y / 16f);
        if (!WorldGen.InWorld(x, y))
        {
            Add("HOVERED TILE", "Tile beneath the mouse cursor in the world.", Row("Position", "OUTSIDE", "Mouse is outside the world bounds"));
            return;
        }

        Tile tile = Main.tile[x, y];
        List<DebugRow> rows =
        [
            Row("Position", $"{x},{y}", $"Tile {x}, {y}"),
            Row("Tile", tile.HasTile ? Trim(TileName(tile.TileType), 15) : "NONE", tile.HasTile ? $"{TileName(tile.TileType)} ({tile.TileType})" : "No active tile"),
            Row("Wall", tile.WallType != WallID.None ? Trim(WallName(tile.WallType), 15) : "NONE", tile.WallType != WallID.None ? $"{WallName(tile.WallType)} ({tile.WallType})" : "No wall"),
            Row("Liquid", tile.LiquidAmount > 0 ? $"{tile.LiquidType}:{tile.LiquidAmount}" : "NONE", tile.LiquidAmount > 0 ? $"{tile.LiquidType}, {tile.LiquidAmount}/255" : "No liquid"),
            Row("Slope / Half", $"{tile.Slope}/{YesNo(tile.IsHalfBlock)}", $"Slope {tile.Slope}; half block {tile.IsHalfBlock}"),
            Row("Wire R/B/G/Y", $"{Bit(tile.RedWire)}{Bit(tile.BlueWire)}{Bit(tile.GreenWire)}{Bit(tile.YellowWire)}", $"Red {tile.RedWire}; Blue {tile.BlueWire}; Green {tile.GreenWire}; Yellow {tile.YellowWire}"),
            Row("Actuator", $"{YesNo(tile.HasActuator)}/{YesNo(tile.IsActuated)}", $"Has actuator {tile.HasActuator}; actuated {tile.IsActuated}"),
            Row("Paint / Echo", $"{tile.TileColor}/{YesNo(tile.IsTileInvisible)}", $"Tile paint {tile.TileColor}; invisible coating {tile.IsTileInvisible}")
        ];
        if (tile.HasTile)
            rows.Insert(2, Row("Frame", $"{tile.TileFrameX},{tile.TileFrameY}", $"Frame X {tile.TileFrameX}; frame Y {tile.TileFrameY}"));
        Add("HOVERED TILE", "Live data for the tile beneath the mouse cursor in the world.", rows.ToArray());
    }

    private void Add(string title, string details, params DebugRow[] rows) =>
        sections.Add(new DebugSection(title, details, VanillaAdminIcons.ForDebugSection(title), [.. rows]));

    private static DebugRow Row(string label, string value, string details) => new(label, value, details);
    private static DebugRow CountRow(string label, long count) => Row(label, Compact(count), $"{count:N0}");
    private static DebugRow BoolRow(string label, bool value, string details) => Row(label, value ? "YES" : "NO", details);

    private static int ShortestColumn(float[] bottoms)
    {
        int result = 0;
        for (int i = 1; i < bottoms.Length; i++)
            if (bottoms[i] < bottoms[result])
                result = i;
        return result;
    }

    private static string Compact(long value)
    {
        if (Math.Abs(value) >= 1_000_000_000) return $"{value / 1_000_000_000d:0.#}B";
        if (Math.Abs(value) >= 1_000_000) return $"{value / 1_000_000d:0.#}M";
        if (Math.Abs(value) >= 1_000) return $"{value / 1_000d:0.#}K";
        return value.ToString();
    }

    private static string Trim(string value, int max) => string.IsNullOrEmpty(value) || value.Length <= max ? value ?? "" : value[..(max - 1)] + "…";
    private static string YesNo(bool value) => value ? "Y" : "N";
    private static int Bit(bool value) => value ? 1 : 0;

    private static string TileName(int type) => TileID.Search.TryGetName(type, out string name) ? name : $"Tile {type}";
    private static string WallName(int type) => WallID.Search.TryGetName(type, out string name) ? name : $"Wall {type}";

    private static void DrawClippedLine(SpriteBatch batch, Rectangle viewport, string text, Vector2 position,
        Color color, float scale, float maxWidth)
    {
        if (position.Y < viewport.Top || position.Y + 18f > viewport.Bottom)
            return;
        float width = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(batch, text, position, color, scale);
    }

    private static void DrawRightAligned(SpriteBatch batch, Rectangle viewport, string text, Vector2 right,
        Color color, float scale, float maxWidth)
    {
        if (right.Y < viewport.Top || right.Y + 18f > viewport.Bottom)
            return;
        float width = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (width > maxWidth)
        {
            scale *= maxWidth / width;
            width = maxWidth;
        }
        Utils.DrawBorderString(batch, text, new Vector2(right.X - width, right.Y), color, scale);
    }

    private static void DrawBorder(SpriteBatch batch, Rectangle viewport, Rectangle rect, Color color)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        DrawPart(new Rectangle(rect.X, rect.Y, rect.Width, 1));
        DrawPart(new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1));
        DrawPart(new Rectangle(rect.X, rect.Y, 1, rect.Height));
        DrawPart(new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height));

        void DrawPart(Rectangle part)
        {
            Rectangle clipped = Rectangle.Intersect(part, viewport);
            if (clipped.Width > 0 && clipped.Height > 0)
                batch.Draw(pixel, clipped, color);
        }
    }
}
