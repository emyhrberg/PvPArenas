//using Arenas.Common.Generation;
//using Arenas.Common.Rounds;
//using Microsoft.Xna.Framework;
//using SubworldLibrary;
//using System;
//using System.Collections.Generic;
//using Terraria.GameContent.Generation;
//using Terraria.ID;
//using Terraria.IO;
//using Terraria.WorldBuilding;

//namespace Arenas.Common;

///// <summary>
///// The single logical Arenas world. In multiplayer its child server stays alive and is
///// reused while rounds use the same preset biome. It is regenerated when the preset biome
///// changes, for an explicit World Gen Manager action, or during crash recovery.
///// </summary>
//public sealed class ArenasSubworld : Subworld
//{
//    public const int FixedWidth = 4200;
//    public const int FixedHeight = 1200;

//    internal static ArenaLayout GeneratedLayout { get; private set; }
//    internal static int ActiveSeed => ArenaSubworldCoordinator.ActiveRequest.Seed;

//    public override int Width => FixedWidth;
//    public override int Height => FixedHeight;
//    public override bool ShouldSave => false;
//    public override bool NoPlayerSaving => true;
//    public override bool NormalUpdates => true;

//    public override List<GenPass> Tasks =>
//    [
//        new PassLegacy("Arenas: Controlled World Generation", GenerateControlledWorld, 0.98f),
//        new PassLegacy("Arenas: Publish World State", PublishWorldState, 0.02f)
//    ];

//    private static void GenerateControlledWorld(GenerationProgress progress, GameConfiguration configuration)
//    {
//        ArenaSubworldRequest request = ArenaSubworldCoordinator.ActiveRequest;
//        GeneratedLayout = null;
//        if (Main.maxTilesX != FixedWidth || Main.maxTilesY != FixedHeight)
//            throw new InvalidOperationException($"Subworld Library created {Main.maxTilesX}x{Main.maxTilesY}; Arenas requires exactly {FixedWidth}x{FixedHeight}.");

//        Log.Info($"[WorldGen0] Controlled generation request={request.WorldRequestId}, mode={request.GenerationMode}, target='{request.TargetStep}', seed={request.Seed}.");
//        ArenaWorldGenerationSystem.Generate(request.Seed, request.GenerationMode, request.TargetStep, progress);
//        GeneratedLayout = ArenaWorldGenerationSystem.GeneratedLayout;

//        if (request.GenerationMode == ArenaGenerationMode.Full && GeneratedLayout == null)
//            throw new InvalidOperationException("Complete Arenas generation finished without publishing the combat layout.");
//    }

//    private static void PublishWorldState(GenerationProgress progress, GameConfiguration configuration)
//    {
//        ArenaSubworldRequest request = ArenaSubworldCoordinator.ActiveRequest;
//        progress.Message = "Publishing Arenas world state";
//        if (GeneratedLayout != null)
//        {
//            GeneratedLayout.Validate(Main.maxTilesX, Main.maxTilesY);
//            Main.spawnTileX = GeneratedLayout.RedSpawn.X;
//            Main.spawnTileY = GeneratedLayout.RedSpawn.Y;
//        }
//        else
//        {
//            Point previewSpawn = FindPreviewSpawn();
//            Main.spawnTileX = previewSpawn.X;
//            Main.spawnTileY = previewSpawn.Y;
//            Log.Info($"[WorldGenFinal] No combat layout exists at this prefix; preview spawn={previewSpawn}.");
//        }

//        progress.Set(1f);
//        Log.Info($"[WorldGenFinal] request={request.WorldRequestId}, mode={request.GenerationMode}, layout={(GeneratedLayout == null ? "none" : GeneratedLayout.ArenaArea.ToString())}.");
//    }

//    private static Point FindPreviewSpawn()
//    {
//        int x = FixedWidth / 2;
//        for (int y = 50; y < FixedHeight - 50; y++)
//            if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType])
//                return new Point(x, Math.Max(20, y - 3));
//        return new Point(x, FixedHeight / 5);
//    }

//    public override void OnEnter()
//    {
//        SubworldSystem.noReturn = true;
//        SubworldSystem.hideUnderworld = true;
//    }

//    public override void OnLoad()
//    {
//        SubworldSystem.noReturn = true;
//        SubworldSystem.hideUnderworld = true;
//        ArenaSubworldRequest request = ArenaSubworldCoordinator.ActiveRequest;
//        bool matchReady = request.GenerationMode == ArenaGenerationMode.Full && GeneratedLayout != null;
//        Log.Chat($"[Arena] Arenas world loaded ({FixedWidth}x{FixedHeight}); request={request.WorldRequestId}, matchReady={matchReady}.");

//        if (Main.netMode != NetmodeID.MultiplayerClient)
//        {
//            ArenaWorldSystem.InitializeSubworld(GeneratedLayout, matchReady);
//            ArenaSubworldCoordinator.QueueSubworldReady(matchReady);
//        }

//        if (Main.netMode == NetmodeID.SinglePlayer && matchReady)
//            Main.QueueMainThreadAction(() => ArenaMapReveal.Request(GeneratedLayout, ArenaRoundSystem.GenerationId));
//    }

//    public override void OnUnload()
//    {
//        GeneratedLayout = null;
//    }

//    public override bool GetLight(Tile tile, int x, int y, ref Terraria.Utilities.FastRandom rand, ref Vector3 color)
//    {
//        color.X = Math.Max(color.X, 0.004f);
//        color.Y = Math.Max(color.Y, 0.004f);
//        color.Z = Math.Max(color.Z, 0.004f);
//        return false;
//    }
//}
