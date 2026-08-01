using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Terraria.GameContent.Generation;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

internal sealed class WorldGenPassRunner : ModSystem
{
    private const string ConfigurationPath = "Terraria.GameContent.WorldBuilding.Configuration.json";
    private const int MapUpdatesPerTick = 25_000;
    private static readonly HashSet<string> TestedPasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Life Crystals", "Gems", "Floating Islands", "Floating Island Houses"
    };

    private readonly object stateLock = new();
    private GenerationProgress progress;
    private Stopwatch timer;
    private string status = "Idle";
    private string activePass = "";
    private string backupPath = "";
    private int seed;
    private int mapRefreshIndex = -1;
    private int lastLoggedPercent = -10;
    private bool busy;

    internal bool Busy { get { lock (stateLock) return busy; } }
    internal string Status { get { lock (stateLock) return status; } }
    internal string ActivePass { get { lock (stateLock) return activePass; } }
    internal string BackupPath { get { lock (stateLock) return backupPath; } }
    internal int Seed { get { lock (stateLock) return seed; } }
    internal double Progress => progress?.TotalProgress ?? 0d;
    internal TimeSpan Elapsed => timer?.Elapsed ?? TimeSpan.Zero;
    internal IReadOnlyList<string> PassNames => WorldGen.VanillaGenPasses.Keys.ToArray();

    internal static bool IsDangerous(string name) => !string.IsNullOrWhiteSpace(name) && !TestedPasses.Contains(name);

    internal bool TryResolvePass(string input, out string name)
    {
        name = WorldGen.VanillaGenPasses.Keys.FirstOrDefault(candidate =>
            candidate.Equals(input?.Trim(), StringComparison.OrdinalIgnoreCase));
        return name != null;
    }

    internal bool TryRun(string requestedPass, out string error)
    {
        error = "";
        if (Main.netMode != NetmodeID.SinglePlayer)
        {
            error = "World Gen Manager currently supports singleplayer only.";
            return false;
        }
        if (!TryResolvePass(requestedPass, out string passName))
        {
            error = $"Unknown vanilla world generation pass: {requestedPass}";
            return false;
        }
        lock (stateLock)
        {
            if (busy)
            {
                error = $"'{activePass}' is already running.";
                return false;
            }
            busy = true;
            activePass = passName;
            seed = Random.Shared.Next(1, int.MaxValue);
            status = "Saving and backing up the world";
            backupPath = "";
            progress = new GenerationProgress();
            timer = Stopwatch.StartNew();
            lastLoggedPercent = -10;
        }

        bool gameplayUpdates = Main.CanUpdateGameplay;
        try
        {
            WorldFile.SaveWorld();
            lock (stateLock)
                backupPath = CreateBackup();
            WorldGenConfiguration configuration = WorldGenConfiguration.FromEmbeddedPath(ConfigurationPath);
            WorldGen.Hooks.ProcessWorldGenConfig(ref configuration);
            LiveGenerationState previous = LiveGenerationState.Capture();
            Main.ToggleGameplayUpdates(false);
            SetStatus($"Running {passName}");
            Log.Info($"[WorldGenManager] START pass='{passName}' seed={seed} backup='{backupPath}'");
            Main.NewText($"World Gen Manager: running '{passName}' with seed {seed}.", Color.Orange);
            ThreadPool.QueueUserWorkItem(_ => RunWorker(passName, seed, configuration, previous));
            return true;
        }
        catch (Exception exception)
        {
            timer?.Stop();
            Main.ToggleGameplayUpdates(gameplayUpdates);
            SetFailed(exception);
            error = exception.Message;
            return false;
        }
    }

    public override void PostUpdateEverything()
    {
        if (Busy && Main.GameUpdateCount % 60 == 0)
        {
            int percent = Math.Clamp((int)(Progress * 100d), 0, 100);
            if (percent >= lastLoggedPercent + 10)
            {
                lastLoggedPercent = percent;
                Log.Info($"[WorldGenManager] {ActivePass}: {percent}% {progress?.Message}");
            }
        }

        if (mapRefreshIndex < 0 || Main.Map == null)
            return;

        int total = Main.maxTilesX * Main.maxTilesY;
        int stop = Math.Min(total, mapRefreshIndex + MapUpdatesPerTick);
        for (; mapRefreshIndex < stop; mapRefreshIndex++)
        {
            int x = mapRefreshIndex % Main.maxTilesX;
            int y = mapRefreshIndex / Main.maxTilesX;
            if (Main.Map.IsRevealed(x, y))
                Main.Map.UpdateType(x, y);
        }
        if (mapRefreshIndex >= total)
            mapRefreshIndex = -1;
    }

    public override void OnWorldUnload()
    {
        mapRefreshIndex = -1;
        progress = null;
    }

    private void RunWorker(string passName, int runSeed, WorldGenConfiguration configuration, LiveGenerationState previous)
    {
        Exception failure = null;
        try
        {
            PrepareLiveGeneration(configuration);
            WorldGenerator generator = new(runSeed, configuration);
            if (passName.Equals("Floating Island Houses", StringComparison.OrdinalIgnoreCase))
                generator.Append(WorldGen.VanillaGenPasses["Floating Islands"]);
            generator.Append(WorldGen.VanillaGenPasses[passName]);
            generator.GenerateWorld(progress);

            SetStatus("Framing tiles and walls");
            WorldGen.RangeFrame(1, 1, Main.maxTilesX - 2, Main.maxTilesY - 2);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            previous.Restore();
        }

        Main.QueueMainThreadAction(() => FinishOnMainThread(passName, runSeed, failure, previous.GameplayUpdates));
    }

    private void FinishOnMainThread(string passName, int runSeed, Exception failure, bool gameplayUpdates)
    {
        Main.ToggleGameplayUpdates(gameplayUpdates);
        timer?.Stop();

        if (failure != null)
        {
            SetFailed(failure);
            Main.NewText($"World Gen Manager failed in '{passName}'. Exit without saving and restore the backup.", Color.Red);
            return;
        }

        try
        {
            SetStatus("Saving generated world");
            Main.instance.ClearCachedTileDraws();
            Lighting.Clear();
            Main.instance.waterfallManager?.FindWaterfalls(true);
            WorldFile.SaveWorld();
            mapRefreshIndex = 0;
            lock (stateLock)
            {
                busy = false;
                status = $"Completed {passName} in {timer.Elapsed.TotalSeconds:F1}s";
            }
            Log.Info($"[WorldGenManager] END pass='{passName}' seed={runSeed} elapsed={timer.Elapsed.TotalSeconds:F1}s backup='{backupPath}'");
            Main.NewText($"World Gen Manager: '{passName}' completed in {timer.Elapsed.TotalSeconds:F1}s.", Color.LightGreen);
        }
        catch (Exception exception)
        {
            SetFailed(exception);
            Main.NewText("World generation finished, but final refresh/save failed. Restore the backup if needed.", Color.Red);
        }
    }

    private void PrepareLiveGeneration(WorldGenConfiguration configuration)
    {
        GenVars.configuration = configuration;
        GenVars.structures = new StructureMap();
        GenVars.worldSurface = Main.worldSurface;
        GenVars.worldSurfaceHigh = Math.Max(201d, Main.worldSurface - 25d);
        GenVars.worldSurfaceLow = Math.Max(201d, GenVars.worldSurfaceHigh - 75d);
        GenVars.rockLayer = Main.rockLayer;
        GenVars.rockLayerLow = Math.Max(Main.worldSurface, Main.rockLayer - 25d);
        GenVars.rockLayerHigh = Math.Min(Main.maxTilesY - 300d, Main.rockLayer + 25d);
        GenVars.skyLakes = 1 + (Main.maxTilesX > 6000 ? 1 : 0) + (Main.maxTilesX > 8000 ? 1 : 0);
        GenVars.UndergroundDesertLocation = Rectangle.Empty;

        // Port of WorldGen.GenerateWorld's GenVars initialization (Terraria/WorldGen.cs
        // ~7597-7657), minus the surface/rock-layer zeroing (kept live above, since the
        // vanilla Terrain pass would otherwise fill them) and minus PreWorldGen /
        // AddGenPassesFromLoadTime (we append only the requested pass). Without this,
        // isolated passes read stale/zero prerequisites and place features off-screen,
        // at the world origin, or no-op entirely.
        GenVars.desertHiveHigh = Main.maxTilesY;
        GenVars.desertHiveLow = 0;
        GenVars.desertHiveLeft = Main.maxTilesX;
        GenVars.desertHiveRight = 0;
        GenVars.copper = 7;
        GenVars.iron = 6;
        GenVars.silver = 9;
        GenVars.gold = 8;
        GenVars.dungeonSide = 0;
        GenVars.dungeonLocation = 0;
        GenVars.jungleHut = (ushort)Main.rand.Next(5);
        GenVars.shellStartXLeft = 0;
        GenVars.shellStartYLeft = 0;
        GenVars.shellStartXRight = 0;
        GenVars.shellStartYRight = 0;
        GenVars.PyrX = null;
        GenVars.PyrY = null;
        GenVars.numPyr = 0;
        GenVars.jungleMinX = -1;
        GenVars.jungleMaxX = -1;
        GenVars.jungleOriginX = 0;
        GenVars.snowMinX = new int[Main.maxTilesY];
        GenVars.snowMaxX = new int[Main.maxTilesY];
        GenVars.snowTop = 0;
        GenVars.snowBottom = 0;
        GenVars.snowOriginLeft = 0;
        GenVars.snowOriginRight = 0;
        GenVars.logX = -1;
        GenVars.logY = -1;
        GenVars.beachBordersWidth = 275;
        GenVars.beachSandRandomCenter = GenVars.beachBordersWidth + 5 + 40;
        GenVars.beachSandRandomWidthRange = 20;
        GenVars.beachSandDungeonExtraWidth = 40;
        GenVars.beachSandJungleExtraWidth = 20;
        GenVars.oceanWaterStartRandomMin = 220;
        GenVars.oceanWaterStartRandomMax = GenVars.oceanWaterStartRandomMin + 40;
        GenVars.oceanWaterForcedJungleLength = 275;
        GenVars.leftBeachEnd = 0;
        GenVars.rightBeachStart = 0;
        GenVars.evilBiomeBeachAvoidance = GenVars.beachSandRandomCenter + 60;
        GenVars.evilBiomeAvoidanceMidFixer = 50;
        GenVars.lakesBeachAvoidance = GenVars.beachSandRandomCenter + 20;
        GenVars.smallHolesBeachAvoidance = GenVars.beachSandRandomCenter + 20;
        GenVars.surfaceCavesBeachAvoidance = GenVars.beachSandRandomCenter + 20;
        GenVars.surfaceCavesBeachAvoidance2 = GenVars.beachSandRandomCenter + 20;

        WorldGen.drunkWorldGen = Main.drunkWorld;
        WorldGen.getGoodWorldGen = Main.getGoodWorld;
        WorldGen.tenthAnniversaryWorldGen = Main.tenthAnniversaryWorld;
        WorldGen.dontStarveWorldGen = Main.dontStarveWorld;
        WorldGen.notTheBees = Main.notTheBeesWorld;
        WorldGen.remixWorldGen = Main.remixWorld;
        WorldGen.noTrapsWorldGen = Main.noTrapsWorld;
        WorldGen.everythingWorldGen = Main.zenithWorld;
        WorldGen.generatingWorld = true;
        WorldGen.gen = true;
        WorldGen.noTileActions = true;
        WorldGen.noMapUpdate = true;
    }

    private static string CreateBackup()
    {
        WorldFileData world = Main.ActiveWorldFileData ?? throw new InvalidOperationException("No active world file.");
        string safeName = string.Concat(world.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        string directory = Path.Combine(Main.SavePath, "WorldGenManagerBackups", $"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(directory);
        CopyWorldFile(world.Path, world.IsCloudSave, directory, required: true);
        CopyWorldFile(Path.ChangeExtension(world.Path, ".twld"), world.IsCloudSave, directory, required: false);
        return directory;
    }

    private static void CopyWorldFile(string source, bool cloud, string directory, bool required)
    {
        if (!FileUtilities.Exists(source, cloud))
        {
            if (required)
                throw new FileNotFoundException("The active world file could not be backed up.", source);
            return;
        }

        string destination = Path.Combine(directory, FileUtilities.GetFileName(source));
        if (cloud)
        {
            if (!FileUtilities.CopyToLocal(source, destination))
                throw new IOException($"Failed to copy cloud world file '{source}' to '{destination}'.");
        }
        else
        {
            File.Copy(FileUtilities.GetFullPath(source, false), destination, true);
        }
    }

    private void SetStatus(string value)
    {
        lock (stateLock)
            status = value;
    }

    private void SetFailed(Exception exception)
    {
        lock (stateLock)
        {
            busy = false;
            status = $"Failed: {exception.GetType().Name}: {exception.Message}";
        }
        Log.Error($"[WorldGenManager] {status}\n{exception}");
    }

    private sealed class LiveGenerationState
    {
        internal bool GameplayUpdates;
        internal UnifiedRandom GenRandom;
        internal UnifiedRandom MainRandom;
        internal WorldGenConfiguration Configuration;
        internal StructureMap Structures;
        internal double WorldSurfaceLow, WorldSurface, WorldSurfaceHigh, RockLayerLow, RockLayer, RockLayerHigh;
        internal int SkyLakes;
        internal Rectangle UndergroundDesert;
        internal bool Drunk, Good, Anniversary, Starve, Bees, Remix, NoTraps, Everything;
        internal bool GeneratingWorld, Gen, NoTileActions, NoMapUpdate;
        internal bool TileSolid56, TileSolid225, TileSolid484;

        internal static LiveGenerationState Capture() => new()
        {
            GameplayUpdates = Main.CanUpdateGameplay,
            GenRandom = WorldGen._genRand,
            MainRandom = Main.rand,
            Configuration = GenVars.configuration,
            Structures = GenVars.structures,
            WorldSurfaceLow = GenVars.worldSurfaceLow,
            WorldSurface = GenVars.worldSurface,
            WorldSurfaceHigh = GenVars.worldSurfaceHigh,
            RockLayerLow = GenVars.rockLayerLow,
            RockLayer = GenVars.rockLayer,
            RockLayerHigh = GenVars.rockLayerHigh,
            SkyLakes = GenVars.skyLakes,
            UndergroundDesert = GenVars.UndergroundDesertLocation,
            Drunk = WorldGen.drunkWorldGen,
            Good = WorldGen.getGoodWorldGen,
            Anniversary = WorldGen.tenthAnniversaryWorldGen,
            Starve = WorldGen.dontStarveWorldGen,
            Bees = WorldGen.notTheBees,
            Remix = WorldGen.remixWorldGen,
            NoTraps = WorldGen.noTrapsWorldGen,
            Everything = WorldGen.everythingWorldGen,
            GeneratingWorld = WorldGen.generatingWorld,
            Gen = WorldGen.gen,
            NoTileActions = WorldGen.noTileActions,
            NoMapUpdate = WorldGen.noMapUpdate,
            TileSolid56 = Main.tileSolid[56],
            TileSolid225 = Main.tileSolid[225],
            TileSolid484 = Main.tileSolid[484]
        };

        internal void Restore()
        {
            WorldGen._genRand = GenRandom;
            Main.rand = MainRandom;
            GenVars.configuration = Configuration;
            GenVars.structures = Structures;
            GenVars.worldSurfaceLow = WorldSurfaceLow;
            GenVars.worldSurface = WorldSurface;
            GenVars.worldSurfaceHigh = WorldSurfaceHigh;
            GenVars.rockLayerLow = RockLayerLow;
            GenVars.rockLayer = RockLayer;
            GenVars.rockLayerHigh = RockLayerHigh;
            GenVars.skyLakes = SkyLakes;
            GenVars.UndergroundDesertLocation = UndergroundDesert;
            WorldGen.drunkWorldGen = Drunk;
            WorldGen.getGoodWorldGen = Good;
            WorldGen.tenthAnniversaryWorldGen = Anniversary;
            WorldGen.dontStarveWorldGen = Starve;
            WorldGen.notTheBees = Bees;
            WorldGen.remixWorldGen = Remix;
            WorldGen.noTrapsWorldGen = NoTraps;
            WorldGen.everythingWorldGen = Everything;
            WorldGen.generatingWorld = GeneratingWorld;
            WorldGen.gen = Gen;
            WorldGen.noTileActions = NoTileActions;
            WorldGen.noMapUpdate = NoMapUpdate;
            Main.tileSolid[56] = TileSolid56;
            Main.tileSolid[225] = TileSolid225;
            Main.tileSolid[484] = TileSolid484;
        }
    }
}
