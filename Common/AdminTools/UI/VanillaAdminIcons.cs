using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;

namespace PvPArenas.Common.AdminTools.UI;

/// <summary>A vanilla UI texture plus an optional frame from one of Terraria's icon atlases.</summary>
internal readonly record struct AdminUIIcon(Asset<Texture2D> Asset, int Columns = 1, int Rows = 1,
    int Column = 0, int Row = 0)
{
    internal Rectangle Source(Texture2D texture)
    {
        int columns = Math.Max(1, Columns);
        int rows = Math.Max(1, Rows);
        int width = texture.Width / columns;
        int height = texture.Height / rows;
        return new Rectangle(
            Math.Clamp(Column, 0, columns - 1) * width,
            Math.Clamp(Row, 0, rows - 1) * height,
            width,
            height);
    }
}

/// <summary>
/// Lazy vanilla asset catalog for PvP admin UI. This mirrors ErkySSC's fitted icon approach
/// while keeping PvPArenas independent of that optional mod at compile time.
/// </summary>
internal static class VanillaAdminIcons
{
    private static readonly Dictionary<string, Asset<Texture2D>> Cache = [];

    internal static AdminUIIcon PlayPause => UI("IconPlayPause");
    internal static AdminUIIcon Pause => UI("IconMismatchPause");
    internal static AdminUIIcon MixedSeed => UI("IconMixedSeed");
    internal static AdminUIIcon NewlyGenerated => UI("IconNewlyGenerated");
    internal static AdminUIIcon Reset => UI("IconReset");
    internal static AdminUIIcon Snapshot => UI("IconSnapshot");
    internal static AdminUIIcon Rank => UI("Bestiary/Icon_Rank_Light");
    internal static AdminUIIcon Reforge => UI("Reforge_1");
    internal static AdminUIIcon Camera => UI("Camera_1");
    internal static AdminUIIcon Warning => UI("UI_quickicon1");
    internal static AdminUIIcon WorldSize => UI("WorldCreation/IconSizeLarge");
    internal static AdminUIIcon Difficulty => UI("WorldCreation/IconDifficultyExpert");

    internal static AdminUIIcon Info(int index) => UI($"InfoIcon_{Math.Clamp(index, 0, 13)}");
    internal static AdminUIIcon Tag(int column, int row) => Frame("Bestiary/Icon_Tags_Shadow", 16, 5, column, row);
    internal static AdminUIIcon InfiniteCategory(int index) => Frame("Creative/Infinite_Icons", 9, 1, index, 0);

    internal static AdminUIIcon ForPass(string name)
    {
        if (Has(name, "Reset") || Has(name, "Cleanup")) return Reset;
        if (Has(name, "Floating") || Has(name, "Cloud") || Has(name, "Sky")) return Tag(8, 1);
        if (Has(name, "Dungeon")) return Tag(0, 1);
        if (Has(name, "Hive") || Has(name, "Bee")) return Tag(1, 2);
        if (Has(name, "Jungle") || Has(name, "Mud") || Has(name, "Temple")) return Tag(0, 0);
        if (Has(name, "Ice") || Has(name, "Snow") || Has(name, "Glacier")) return Tag(10, 1);
        if (Has(name, "Desert") || Has(name, "Sand") || Has(name, "Pyramid") || Has(name, "Oasis")) return Tag(4, 0);
        if (Has(name, "Ocean") || Has(name, "Beach") || Has(name, "Water")) return Info(1);
        if (Has(name, "Underworld") || Has(name, "Hell") || Has(name, "Lava")) return Tag(12, 1);
        if (Has(name, "Mushroom")) return Tag(9, 2);
        if (Has(name, "Corrupt") || Has(name, "Evil") || Has(name, "Shadow")) return Tag(7, 0);
        if (Has(name, "Crimson") || Has(name, "Blood")) return Tag(11, 0);
        if (Has(name, "Gem") || Has(name, "Ore") || Has(name, "Shin") || Has(name, "Crystal")) return NewlyGenerated;
        if (Has(name, "Tree") || Has(name, "Plant") || Has(name, "Flower") || Has(name, "Herb")) return Tag(0, 0);
        if (Has(name, "Wall") || Has(name, "Rock") || Has(name, "Stone") || Has(name, "Dirt") || Has(name, "Clay")) return Tag(1, 0);
        if (Has(name, "Wire") || Has(name, "Trap")) return Reforge;
        return MixedSeed;
    }

    internal static AdminUIIcon ForCleanup(WorldGenManager.WorldClearAction action) => action switch
    {
        WorldGenManager.WorldClearAction.Tiles => Tag(1, 0),
        WorldGenManager.WorldClearAction.Walls => Tag(2, 0),
        WorldGenManager.WorldClearAction.Liquids => Info(1),
        WorldGenManager.WorldClearAction.Wiring => Reforge,
        WorldGenManager.WorldClearAction.PaintAndCoatings => InfiniteCategory(6),
        WorldGenManager.WorldClearAction.Everything => Warning,
        _ => Reset
    };

    internal static AdminUIIcon ForVisual(WorldGenManager.WorldVisualLayer layer) => layer switch
    {
        WorldGenManager.WorldVisualLayer.Background => Tag(0, 0),
        WorldGenManager.WorldVisualLayer.Clouds => Tag(8, 1),
        WorldGenManager.WorldVisualLayer.Sky => Info(1),
        WorldGenManager.WorldVisualLayer.SunAndMoon => Tag(4, 1),
        WorldGenManager.WorldVisualLayer.Stars => NewlyGenerated,
        _ => Camera
    };

    internal static AdminUIIcon ForDebugSection(string title) => title switch
    {
        "WORLD" => MixedSeed,
        "LAYERS" => WorldSize,
        "GEN VARS" => NewlyGenerated,
        "WORLDGEN FLAGS" => Warning,
        "TILE SCAN" => Snapshot,
        "LIQUIDS" => Info(1),
        "WIRING + SHAPE" => Reforge,
        "TOP TILES" => Tag(1, 0),
        "TOP WALLS" => Tag(2, 0),
        "ENTITIES" => Tag(15, 3),
        "NEARBY BIOME" => Tag(0, 0),
        "HOVERED TILE" => Info(13),
        _ => Info(4)
    };

    internal static void DrawFitted(SpriteBatch spriteBatch, AdminUIIcon icon, Rectangle box,
        Color color, bool allowUpscale = false)
    {
        Texture2D texture = icon.Asset?.Value;
        if (texture == null || texture.Width <= 0 || texture.Height <= 0 || box.Width <= 0 || box.Height <= 0)
            return;

        Rectangle source = icon.Source(texture);
        if (source.Width <= 0 || source.Height <= 0)
            return;

        float scale = Math.Min(box.Width / (float)source.Width, box.Height / (float)source.Height);
        if (!allowUpscale)
            scale = Math.Min(scale, 1f);

        Vector2 size = source.Size() * scale;
        Vector2 position = box.Center.ToVector2() - size * .5f;
        spriteBatch.Draw(texture, position, source, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private static AdminUIIcon UI(string path) => Frame(path, 1, 1, 0, 0);

    private static AdminUIIcon Frame(string path, int columns, int rows, int column, int row)
    {
        if (Main.dedServ)
            return default;
        if (!Cache.TryGetValue(path, out Asset<Texture2D> asset))
        {
            asset = Main.Assets.Request<Texture2D>($"Images/UI/{path}");
            Cache[path] = asset;
        }
        return new AdminUIIcon(asset, columns, rows, column, row);
    }

    private static bool Has(string name, string value) =>
        name?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
}
