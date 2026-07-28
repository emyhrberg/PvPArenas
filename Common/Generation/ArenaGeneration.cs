using PvPArenas.Common.Game;
using PvPArenas.Common.Game.LoadoutSelector;
using System;
using Terraria.ID;

namespace PvPArenas.Common.Generation;

/// <summary>
/// Resolves configured arenas from existing world terrain. It does not create,
/// clear, or synchronize tiles; custom world generation remains a later feature.
/// </summary>
internal static class ArenaGeneration
{
    private const int WorldMargin = 20;

    internal static bool TryResolve(BossFightPreset preset, out ArenaLayout layout, out string failure)
    {
        layout = null;
        failure = "";

        if (preset?.Boss?.Type <= NPCID.None)
        {
            failure = "The boss preset is missing a valid NPC.";
            return false;
        }

        int width = Math.Clamp(preset.ArenaWidthTiles, 100, Main.maxTilesX - WorldMargin * 2);
        int height = Math.Clamp(preset.ArenaHeightTiles, 100, Main.maxTilesY - WorldMargin * 2);

        if (!TryFindArenaCenter(preset.ArenaKind, width, height, out Point center, out failure))
            return false;

        Rectangle bounds = CenteredBounds(center, width, height);
        Rectangle redSpawnColumn = new(bounds.Left, bounds.Top, ArenaLayout.SpawnBoxWidth, bounds.Height);
        Rectangle blueSpawnColumn = new(bounds.Right - ArenaLayout.SpawnBoxWidth, bounds.Top,
            ArenaLayout.SpawnBoxWidth, bounds.Height);

        if (!TryFindGroundInSpawnBox(redSpawnColumn, out Point redSpawn)
            || !TryFindGroundInSpawnBox(blueSpawnColumn, out Point blueSpawn))
        {
            failure = $"Grounded Red and Blue spawn positions could not be resolved inside the {preset.ArenaKind} arena.";
            return false;
        }

        layout = new ArenaLayout(bounds, blueSpawn, redSpawn);
        Log.Info($"Resolved {preset.ArenaKind} arena for NPC type {preset.Boss.Type} at {bounds}; "
            + $"blue={layout.BlueSpawn}, red={layout.RedSpawn}, boss={layout.BossSpawn}.");
        return true;
    }

    private static bool TryFindArenaCenter(ArenaKind kind, int width, int height, out Point center,
        out string failure)
    {
        switch (kind)
        {
            case ArenaKind.WorldCenterSurface:
                return TryFindSurfaceCenter(height, out center, out failure);
            case ArenaKind.UndergroundJungle:
                return TryFindUndergroundJungleCenter(width, height, out center, out failure);
            case ArenaKind.JungleTemple:
                return TryFindTempleCenter(width, height, out center, out failure);
            default:
                center = default;
                failure = $"Unsupported arena kind: {kind}.";
                return false;
        }
    }

    private static bool TryFindSurfaceCenter(int arenaHeight, out Point center, out string failure)
    {
        int x = Main.maxTilesX / 2;
        if (!TryFindGround(x, WorldMargin, Main.maxTilesY - WorldMargin, out int groundY))
        {
            center = default;
            failure = "No ground was found near the center of the world.";
            return false;
        }

        center = new Point(x, groundY - arenaHeight / 4);
        failure = "";
        return true;
    }

    private static bool TryFindUndergroundJungleCenter(int width, int height, out Point center,
        out string failure)
    {
        int scanTop = Math.Clamp((int)Main.worldSurface + 40, WorldMargin, Main.maxTilesY - WorldMargin - 1);
        int scanBottom = Math.Clamp(Main.maxTilesY - 180, scanTop + 1, Main.maxTilesY - WorldMargin);
        long sumX = 0;
        long sumY = 0;
        int count = 0;

        for (int x = WorldMargin; x < Main.maxTilesX - WorldMargin; x += 3)
        for (int y = scanTop; y < scanBottom; y += 3)
        {
            if (!IsUndergroundJungle(Main.tile[x, y]))
                continue;

            sumX += x;
            sumY += y;
            count++;
        }

        if (count == 0)
        {
            center = default;
            failure = "No underground Jungle terrain was found in this world.";
            return false;
        }

        Point biomeCenter = new((int)(sumX / count), (int)(sumY / count));
        Rectangle search = CenteredBounds(biomeCenter, width, height);
        if (!TryFindOpenBossSpace(search, biomeCenter, out center))
            center = biomeCenter;

        failure = "";
        return true;
    }

    private static bool TryFindTempleCenter(int width, int height, out Point center, out string failure)
    {
        if (!TryFindTempleBounds(out Rectangle temple))
        {
            center = default;
            failure = "No Jungle Temple terrain was found in this world.";
            return false;
        }

        Point templeCenter = temple.Center;
        Rectangle search = Rectangle.Intersect(temple, CenteredBounds(templeCenter, width, height));
        if (!TryFindOpenBossSpace(search, templeCenter, out center))
            center = templeCenter;

        failure = "";
        return true;
    }

    private static Rectangle CenteredBounds(Point center, int width, int height)
    {
        int left = Math.Clamp(center.X - width / 2, WorldMargin,
            Main.maxTilesX - WorldMargin - width);
        int top = Math.Clamp(center.Y - height / 2, WorldMargin,
            Main.maxTilesY - WorldMargin - height);
        return new Rectangle(left, top, width, height);
    }

    private static bool TryFindGroundInSpawnBox(Rectangle column, out Point spawn)
    {
        int preferredX = column.Center.X;
        int maxOffset = column.Width / 2;
        for (int offset = 0; offset <= maxOffset; offset++)
        {
            int leftX = preferredX - offset;
            if (leftX >= column.Left && TryFindGround(leftX,
                column.Top + ArenaLayout.SpawnBoxHeight, column.Bottom, out int leftY))
            {
                spawn = new Point(leftX, leftY);
                return true;
            }

            int rightX = preferredX + offset;
            if (offset > 0 && rightX < column.Right
                && TryFindGround(rightX, column.Top + ArenaLayout.SpawnBoxHeight, column.Bottom,
                    out int rightY))
            {
                spawn = new Point(rightX, rightY);
                return true;
            }
        }

        spawn = default;
        return false;
    }

    private static bool TryFindGround(int x, int startY, int endY, out int groundY)
    {
        int start = Math.Clamp(startY, WorldMargin, Main.maxTilesY - WorldMargin);
        int end = Math.Clamp(endY, start + 1, Main.maxTilesY - WorldMargin);
        for (int y = start; y < end; y++)
        {
            if (!WorldGen.SolidTile(x, y) || WorldGen.SolidTile(x, y - 1)
                || WorldGen.SolidTile(x, y - 2) || WorldGen.SolidTile(x, y - 3))
                continue;

            groundY = y;
            return true;
        }

        groundY = 0;
        return false;
    }

    private static bool TryFindOpenBossSpace(Rectangle area, Point preferred, out Point result)
    {
        result = preferred;
        int bestDistance = int.MaxValue;
        for (int x = area.Left + 5; x < area.Right - 5; x += 2)
        for (int y = area.Top + 6; y < area.Bottom - 6; y += 2)
        {
            if (!IsOpenBossSpace(x, y))
                continue;

            int distance = Math.Abs(x - preferred.X) + Math.Abs(y - preferred.Y);
            if (distance >= bestDistance)
                continue;

            result = new Point(x, y);
            bestDistance = distance;
        }

        return bestDistance != int.MaxValue;
    }

    private static bool IsOpenBossSpace(int centerX, int centerY)
    {
        for (int x = centerX - 3; x <= centerX + 3; x++)
        for (int y = centerY - 4; y <= centerY + 4; y++)
        {
            Tile tile = Main.tile[x, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                return false;
        }

        return true;
    }

    private static bool IsUndergroundJungle(Tile tile) =>
        tile.WallType == WallID.JungleUnsafe
        || tile.HasTile && tile.TileType is TileID.JungleGrass or TileID.Hive;

    private static bool TryFindTempleBounds(out Rectangle temple)
    {
        int left = Main.maxTilesX;
        int top = Main.maxTilesY;
        int right = -1;
        int bottom = -1;

        for (int x = WorldMargin; x < Main.maxTilesX - WorldMargin; x += 2)
        for (int y = WorldMargin; y < Main.maxTilesY - WorldMargin; y += 2)
        {
            Tile tile = Main.tile[x, y];
            if ((!tile.HasTile || tile.TileType != TileID.LihzahrdBrick)
                && tile.WallType != WallID.LihzahrdBrickUnsafe)
                continue;

            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }

        if (right <= left || bottom <= top)
        {
            temple = Rectangle.Empty;
            return false;
        }

        temple = new Rectangle(left, top, right - left + 1, bottom - top + 1);
        return true;
    }
}
