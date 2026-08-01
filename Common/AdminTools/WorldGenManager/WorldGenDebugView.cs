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

/// <summary>
/// Live, scrolling dump of every world/tile statistic the manager can read. Rebuilt each
/// frame from cheap globals (layers, GenVars, flags, hovered tile, scene metrics, entities)
/// plus the budgeted world histogram from <see cref="WorldGenDebugStats"/>.
/// </summary>
internal sealed class WorldGenDebugView : UIPanel
{
    private const float RowScale = .52f;
    private const float HeaderScale = .62f;
    private const float RowHeight = 16f;
    private const float HeaderGap = 8f;

    private static readonly Color HeaderColor = new(255, 214, 120);
    private static readonly Color LabelColor = new(176, 196, 222);
    private static readonly Color ValueColor = Color.White;

    private readonly List<(string Text, Color Color, float Scale, bool Header)> lines = [];
    private float scroll;
    private float contentHeight;

    internal WorldGenDebugView()
    {
        SetPadding(0f);
        BackgroundColor = new Color(16, 22, 52) * .96f;
        BorderColor = new Color(78, 104, 190) * .8f;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsMouseHovering)
            return;

        Main.LocalPlayer.mouseInterface = true;
        if (PlayerInput.ScrollWheelDeltaForUI != 0)
            scroll = Math.Clamp(
                scroll - Math.Sign(PlayerInput.ScrollWheelDeltaForUI) * RowHeight * 3f,
                0f,
                Math.Max(0f, contentHeight - (GetInnerDimensions().Height - 12f)));
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        BuildLines();

        Rectangle box = GetInnerDimensions().ToRectangle();
        float y = box.Y + 6f - scroll;
        contentHeight = 6f;

        foreach ((string text, Color color, float scale, bool header) in lines)
        {
            float lineHeight = header ? RowHeight + HeaderGap : RowHeight;
            float drawY = header ? y + HeaderGap : y;

            if (drawY + lineHeight >= box.Y && drawY <= box.Bottom)
                DrawClipped(spriteBatch, text, box.X + 8f, drawY, color, scale, box.Width - 16f);

            y += lineHeight;
            contentHeight += lineHeight;
        }
    }

    private void BuildLines()
    {
        lines.Clear();

        Header("WORLD");
        Row("Size", $"{Main.maxTilesX} x {Main.maxTilesY} tiles");
        Row("Name / Seed", $"{Main.worldName}  |  {Main.ActiveWorldFileData?.SeedText ?? "?"}");
        Row("Spawn tile", $"{Main.spawnTileX}, {Main.spawnTileY}");
        Row("Dungeon tile", $"{Main.dungeonX}, {Main.dungeonY}  (side {GenVars.dungeonSide})");
        Row("Time / Day", $"{Main.time:F0}  |  {(Main.dayTime ? "day" : "night")}");

        Header("LAYERS (tile Y)");
        Row("Surface", $"{(int)Main.worldSurface}");
        Row("Rock layer", $"{(int)Main.rockLayer}");
        Row("Underworld top", $"{Main.maxTilesY - 200}");
        Row("Space bottom", $"{(int)(Main.worldSurface * 0.35)}");

        Header("GEN VARS");
        Row("worldSurface lo/hi", $"{GenVars.worldSurfaceLow:F0} / {GenVars.worldSurfaceHigh:F0}");
        Row("rockLayer lo/hi", $"{GenVars.rockLayerLow:F0} / {GenVars.rockLayerHigh:F0}");
        Row("Jungle origin/min/max", $"{GenVars.jungleOriginX} / {GenVars.jungleMinX} / {GenVars.jungleMaxX}");
        Row("Snow origin L/R", $"{GenVars.snowOriginLeft} / {GenVars.snowOriginRight}");
        Row("Dungeon loc/side", $"{GenVars.dungeonLocation} / {GenVars.dungeonSide}");
        Row("Living tree log", $"{GenVars.logX}, {GenVars.logY}");
        Row("Sky lakes", $"{GenVars.skyLakes}");
        Row("Crimson left", $"{GenVars.crimsonLeft}");
        Row("Ocean cave treasure", $"{GenVars.numOceanCaveTreasure}");
        Rectangle desert = GenVars.UndergroundDesertLocation;
        Row("Underground desert", $"{desert.X},{desert.Y} {desert.Width}x{desert.Height}");

        Header("WORLDGEN FLAGS");
        Row("gen / generatingWorld", $"{WorldGen.gen} / {WorldGen.generatingWorld}");
        Row("noTileActions / noMapUpdate", $"{WorldGen.noTileActions} / {WorldGen.noMapUpdate}");
        Row("drunk / getGood / anniv", $"{WorldGen.drunkWorldGen} / {WorldGen.getGoodWorldGen} / {WorldGen.tenthAnniversaryWorldGen}");
        Row("bees / remix / noTraps", $"{WorldGen.notTheBees} / {WorldGen.remixWorldGen} / {WorldGen.noTrapsWorldGen}");
        Row("starve / everything", $"{WorldGen.dontStarveWorldGen} / {WorldGen.everythingWorldGen}");

        WorldGenDebugStats.Snapshot snap = ModContent.GetInstance<WorldGenDebugStats>().Latest;
        float progress = ModContent.GetInstance<WorldGenDebugStats>().ScanProgress;
        Header($"TILE SCAN  ({progress:P0}, {snap.SweepSeconds:F1}s/sweep)");
        Row("Active / Air", $"{snap.Active:N0} / {snap.Air:N0}");
        Row("Walls", $"{snap.Walls:N0}");
        Row("Water / Lava", $"{snap.Water:N0} / {snap.Lava:N0}");
        Row("Honey / Shimmer", $"{snap.Honey:N0} / {snap.Shimmer:N0}");
        Row("Wire R/B/G/Y", $"{snap.RedWire:N0} / {snap.BlueWire:N0} / {snap.GreenWire:N0} / {snap.YellowWire:N0}");
        Row("Actuators / Actuated", $"{snap.Actuators:N0} / {snap.Actuated:N0}");
        Row("Half / Slopes", $"{snap.HalfBricks:N0} / {snap.Slopes:N0}");
        Row("Painted / Coated", $"{snap.Painted:N0} / {snap.Coated:N0}");

        Header("TOP TILES");
        foreach (WorldGenDebugStats.TypeCount entry in snap.TopTiles)
            Row(TileName(entry.Type), $"{entry.Count:N0}");

        Header("TOP WALLS");
        foreach (WorldGenDebugStats.TypeCount entry in snap.TopWalls)
            Row(WallName(entry.Type), $"{entry.Count:N0}");

        Header("ENTITIES");
        Row("Chests", $"{Main.chest.Count(chest => chest != null)}");
        Row("Signs", $"{Main.sign.Count(sign => sign != null)}");
        Row("Tile entities", $"{TileEntity.ByID.Count}");
        Row("NPCs active", $"{Main.npc.Count(npc => npc.active)}");
        Row("Items active", $"{Main.item.Count(item => item.active)}");
        Row("Projectiles active", $"{Main.projectile.Count(projectile => projectile.active)}");

        Header("BIOME (near player)");
        SceneMetrics metrics = Main.SceneMetrics;
        Row("Jungle", $"{metrics.JungleTileCount}  (>= {SceneMetrics.JungleTileThreshold})");
        Row("Evil / Blood", $"{metrics.EvilTileCount} / {metrics.BloodTileCount}");
        Row("Holy / Snow", $"{metrics.HolyTileCount} / {metrics.SnowTileCount}");
        Row("Sand / Mushroom", $"{metrics.SandTileCount} / {metrics.MushroomTileCount}");
        Row("Dungeon / Meteor", $"{metrics.DungeonTileCount} / {metrics.MeteorTileCount}");
        Row("Shimmer / Graveyard", $"{metrics.ShimmerTileCount} / {metrics.GraveyardTileCount}");

        BuildHoveredTile();
    }

    private void BuildHoveredTile()
    {
        Header("HOVERED TILE");
        int x = (int)(Main.MouseWorld.X / 16f);
        int y = (int)(Main.MouseWorld.Y / 16f);
        if (!WorldGen.InWorld(x, y))
        {
            Row("Position", "outside world");
            return;
        }

        Tile tile = Main.tile[x, y];
        Row("Position", $"{x}, {y}");
        Row("Tile", tile.HasTile ? $"{TileName(tile.TileType)} ({tile.TileType})" : "none");
        if (tile.HasTile)
            Row("Frame X/Y", $"{tile.TileFrameX} / {tile.TileFrameY}");
        Row("Wall", tile.WallType != WallID.None ? $"{WallName(tile.WallType)} ({tile.WallType})" : "none");
        Row("Liquid", tile.LiquidAmount > 0 ? $"{tile.LiquidType} {tile.LiquidAmount}/255" : "none");
        Row("Slope / Half", $"{tile.Slope} / {tile.IsHalfBlock}");
        Row("Wire R/B/G/Y", $"{tile.RedWire}/{tile.BlueWire}/{tile.GreenWire}/{tile.YellowWire}");
        Row("Actuator / Actuated", $"{tile.HasActuator} / {tile.IsActuated}");
        Row("Paint / Echo", $"{tile.TileColor} / {tile.IsTileInvisible}");
    }

    private void Header(string text) => lines.Add((text, HeaderColor, HeaderScale, true));

    private void Row(string label, string value)
    {
        lines.Add(($"{label}:", LabelColor, RowScale, false));
        lines.Add(($"    {value}", ValueColor, RowScale, false));
    }

    private static string TileName(int type) =>
        TileID.Search.TryGetName(type, out string name) ? name : $"Tile {type}";

    private static string WallName(int type) =>
        WallID.Search.TryGetName(type, out string name) ? name : $"Wall {type}";

    private static void DrawClipped(SpriteBatch batch, string text, float x, float y, Color color, float scale, float maxWidth)
    {
        float width = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(batch, text, new Vector2(x, y), color, scale);
    }
}
