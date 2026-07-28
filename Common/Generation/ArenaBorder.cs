using PvPArenas.Common.Game;
using System;
using Terraria.ID;

namespace PvPArenas.Common.Generation;

/// <summary>Performs and synchronizes server-side arena tile preparation.</summary>
internal static class ArenaBorder
{
    internal const int Thickness = 3;
    private const int SpawnClearPadding = 3;

    internal static void ClearSpawnBoxes(ArenaLayout layout)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || layout == null)
            return;

        Rectangle red = SpawnClearArea(layout.RedSpawnBox);
        Rectangle blue = SpawnClearArea(layout.BlueSpawnBox);
        int cleared = ClearArea(red) + ClearArea(blue);

        FrameArea(red);
        FrameArea(blue);
        SyncArea(red);
        SyncArea(blue);

        Log.Info($"Cleared {cleared} occupied spawn-area cells; red={red}, blue={blue}, "
            + $"visual border plus {SpawnClearPadding}-tile padding, walls preserved.");
    }

    internal static void Place(ArenaLayout layout)
    {
        // Disabled: arena boundaries are visual shader borders and must not mutate world tiles.
        /*
        if (Main.netMode == NetmodeID.MultiplayerClient || layout == null)
            return;

        Rectangle bounds = layout.ArenaBounds;
        Rectangle outer = bounds;
        outer.Inflate(Thickness, Thickness);

        for (int x = outer.Left; x < outer.Right; x++)
        {
            for (int y = outer.Top; y < outer.Bottom; y++)
            {
                if (bounds.Contains(x, y) || !WorldGen.InWorld(x, y, 5))
                    continue;

                Tile tile = Main.tile[x, y];
                tile.ClearTile();
                tile.HasTile = true;
                tile.TileType = TileID.LihzahrdBrick;
                tile.LiquidAmount = 0;
            }
        }

        Frame(outer, bounds);
        Sync(outer);
        Log.Info($"Placed the {Thickness}-tile Lihzahrd Brick arena border around {bounds}.");
        */
    }

    private static Rectangle SpawnClearArea(Rectangle spawnBox)
    {
        Rectangle area = ArenaSpawnBoxes.BorderOuterTileArea(spawnBox);
        area.Inflate(SpawnClearPadding, SpawnClearPadding);
        return Rectangle.Intersect(area,
            new Rectangle(5, 5, Math.Max(0, Main.maxTilesX - 10), Math.Max(0, Main.maxTilesY - 10)));
    }

    private static int ClearArea(Rectangle area)
    {
        int changed = 0;
        for (int x = area.Left; x < area.Right; x++)
        for (int y = area.Top; y < area.Bottom; y++)
        {
            Tile tile = Main.tile[x, y];
            bool occupied = tile.HasTile || tile.LiquidAmount > 0 || tile.RedWire || tile.BlueWire
                || tile.GreenWire || tile.YellowWire || tile.HasActuator || tile.IsActuated;

            // Let vanilla remove multi-tile registrations such as chests and tile
            // entities, then force-clear protected terrain without dropping items.
            if (tile.HasTile)
                WorldGen.KillTile(x, y, noItem: true);

            tile.ClearTile();
            tile.ClearBlockPaintAndCoating();
            tile.RedWire = false;
            tile.BlueWire = false;
            tile.GreenWire = false;
            tile.YellowWire = false;
            tile.HasActuator = false;
            tile.IsActuated = false;
            tile.LiquidAmount = 0;
            tile.LiquidType = LiquidID.Water;

            if (occupied)
                changed++;
        }

        return changed;
    }

    private static void FrameArea(Rectangle area)
    {
        int left = Math.Clamp(area.Left, 1, Main.maxTilesX - 2);
        int top = Math.Clamp(area.Top, 1, Main.maxTilesY - 2);
        int right = Math.Clamp(area.Right - 1, 1, Main.maxTilesX - 2);
        int bottom = Math.Clamp(area.Bottom - 1, 1, Main.maxTilesY - 2);

        if (left <= right && top <= bottom)
            WorldGen.RangeFrame(left, top, right, bottom);
    }

    private static void SyncArea(Rectangle area)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        SendStrip(area.Left - 1, area.Top - 1, area.Width + 2, area.Height + 2);
    }

    private static void Frame(Rectangle outer, Rectangle bounds)
    {
        int left = Math.Clamp(outer.Left, 1, Main.maxTilesX - 2);
        int top = Math.Clamp(outer.Top, 1, Main.maxTilesY - 2);
        int right = Math.Clamp(outer.Right - 1, 1, Main.maxTilesX - 2);
        int bottom = Math.Clamp(outer.Bottom - 1, 1, Main.maxTilesY - 2);

        if (left <= right && top <= bottom)
            WorldGen.RangeFrame(left, top, right, bottom);
    }

    private static void Sync(Rectangle outer)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        // The four ring strips, inflated by one tile so re-framed neighbors sync too.
        SendStrip(outer.Left - 1, outer.Top - 1, outer.Width + 2, Thickness + 2);
        SendStrip(outer.Left - 1, outer.Bottom - Thickness - 1, outer.Width + 2, Thickness + 2);
        SendStrip(outer.Left - 1, outer.Top - 1, Thickness + 2, outer.Height + 2);
        SendStrip(outer.Right - Thickness - 1, outer.Top - 1, Thickness + 2, outer.Height + 2);
    }

    private static void SendStrip(int x, int y, int width, int height)
    {
        const int ChunkSize = 50;
        int left = Math.Clamp(x, 0, Main.maxTilesX - 1);
        int top = Math.Clamp(y, 0, Main.maxTilesY - 1);
        int right = Math.Clamp(x + width, 0, Main.maxTilesX);
        int bottom = Math.Clamp(y + height, 0, Main.maxTilesY);

        for (int chunkX = left; chunkX < right; chunkX += ChunkSize)
            for (int chunkY = top; chunkY < bottom; chunkY += ChunkSize)
                NetMessage.SendTileSquare(-1, chunkX, chunkY,
                    Math.Min(ChunkSize, right - chunkX), Math.Min(ChunkSize, bottom - chunkY));
    }
}
