using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.ID;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

/// <summary>
/// Budgeted full-world tile scanner. Sweeps a slice of the tilemap each tick and, when a
/// full pass completes, publishes an immutable snapshot of every aggregate tile statistic.
/// Cheap per-frame globals (layers, GenVars, flags, hovered tile) are read directly by the
/// debug view instead — only the world-wide histograms live here.
/// </summary>
internal sealed class WorldGenDebugStats : ModSystem
{
    internal readonly record struct TypeCount(int Type, long Count);

    internal sealed class Snapshot
    {
        internal long Active, Air, Walls;
        internal long Water, Lava, Honey, Shimmer;
        internal long RedWire, BlueWire, GreenWire, YellowWire, Actuators, Actuated;
        internal long HalfBricks, Slopes, Painted, Coated;
        internal long ScannedTiles;
        internal double SweepSeconds;
        internal TypeCount[] TopTiles = [];
        internal TypeCount[] TopWalls = [];
    }

    private const int TilesPerTick = 180_000;

    private long[] tileCounts;
    private long[] wallCounts;
    private Snapshot working = new();
    private int cursor;
    private uint sweepStartTick;

    internal Snapshot Latest { get; private set; } = new();
    internal float ScanProgress => Main.maxTilesX <= 0
        ? 0f
        : cursor / (float)(Main.maxTilesX * Main.maxTilesY);

    public override void OnWorldLoad()
    {
        tileCounts = new long[TileLoader.TileCount];
        wallCounts = new long[WallLoader.WallCount];
        working = new Snapshot();
        Latest = new Snapshot();
        cursor = 0;
        sweepStartTick = Main.GameUpdateCount;
    }

    public override void OnWorldUnload()
    {
        tileCounts = null;
        wallCounts = null;
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || tileCounts == null || Main.maxTilesX <= 0)
            return;

        int width = Main.maxTilesX;
        int total = width * Main.maxTilesY;
        int stop = Math.Min(total, cursor + TilesPerTick);

        for (; cursor < stop; cursor++)
        {
            Tile tile = Main.tile[cursor % width, cursor / width];
            if (tile == null)
                continue;

            if (tile.HasTile)
            {
                working.Active++;
                if (tile.TileType < tileCounts.Length)
                    tileCounts[tile.TileType]++;
                if (tile.IsHalfBlock)
                    working.HalfBricks++;
                if (tile.Slope != SlopeType.Solid)
                    working.Slopes++;
                if (tile.TileColor != 0)
                    working.Painted++;
                if (tile.IsTileInvisible)
                    working.Coated++;
            }
            else
            {
                working.Air++;
            }

            if (tile.WallType != WallID.None)
            {
                working.Walls++;
                if (tile.WallType < wallCounts.Length)
                    wallCounts[tile.WallType]++;
            }

            if (tile.LiquidAmount > 0)
            {
                switch (tile.LiquidType)
                {
                    case LiquidID.Water: working.Water++; break;
                    case LiquidID.Lava: working.Lava++; break;
                    case LiquidID.Honey: working.Honey++; break;
                    case LiquidID.Shimmer: working.Shimmer++; break;
                }
            }

            if (tile.RedWire) working.RedWire++;
            if (tile.BlueWire) working.BlueWire++;
            if (tile.GreenWire) working.GreenWire++;
            if (tile.YellowWire) working.YellowWire++;
            if (tile.HasActuator) working.Actuators++;
            if (tile.IsActuated) working.Actuated++;
        }

        if (cursor < total)
            return;

        working.ScannedTiles = total;
        working.SweepSeconds = (Main.GameUpdateCount - sweepStartTick) / 60d;
        working.TopTiles = TopOf(tileCounts, 10);
        working.TopWalls = TopOf(wallCounts, 6);
        Latest = working;

        working = new Snapshot();
        Array.Clear(tileCounts);
        Array.Clear(wallCounts);
        cursor = 0;
        sweepStartTick = Main.GameUpdateCount;
    }

    private static TypeCount[] TopOf(long[] counts, int take)
    {
        List<TypeCount> list = new(counts.Length);
        for (int type = 0; type < counts.Length; type++)
            if (counts[type] > 0)
                list.Add(new TypeCount(type, counts[type]));
        return [.. list.OrderByDescending(entry => entry.Count).Take(take)];
    }
}
