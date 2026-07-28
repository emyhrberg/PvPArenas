//using Arenas.Common.AdminTools.WorldGenManager;
//using Arenas.Common.Generation;
//using Arenas.Common.Rounds;
//using SubworldLibrary;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using Terraria.Enums;
//using Terraria.ID;
//using Terraria.ModLoader.IO;

//namespace Arenas.Common;

//internal readonly record struct ArenaSubworldRequest(
//    int PresetIndex,
//    int Seed,
//    int CountdownTicks,
//    int PlayingTicks,
//    int GenerationId,
//    int ExpectedPlayers,
//    int WorldRequestId,
//    ArenaGenerationMode GenerationMode,
//    string TargetStep,
//    bool MovePlayersAfterReady)
//{
//    public static ArenaSubworldRequest Default => new(0, Environment.TickCount, -1, -1, 1, 1, 1, ArenaGenerationMode.Full, "", true);
//}

///// <summary>
///// Supervises one Arenas child server. Match requests reuse it only while the selected
///// preset uses the same generated arena; preset changes and explicit generation requests
///// evacuate every player, restart that child, and wait for its ready message.
///// </summary>
//internal sealed class ArenaSubworldCoordinator : ModSystem, ICopyWorldData
//{
//    private const string RequestKey = "!Arenas.WorldRequest.v2";
//    private const int ReturnTimeoutTicks = 10 * 60;
//    private const int RestartDelayTicks = 15;
//    private const int StartupTimeoutTicks = 10 * 60 * 60;

//    private enum Message : byte { Ready, PrepareRound, Regenerate, Heartbeat }
//    private enum TransitionState : byte { None, WaitingForMainWorld, RestartDelay, WaitingForReady }

//    private static readonly HashSet<int> roster = [];
//    private static readonly Dictionary<int, Team> rosterTeams = [];
//    private static ArenaSubworldRequest pendingRequest = ArenaSubworldRequest.Default;
//    private static ArenaSubworldRequest activeRequest = ArenaSubworldRequest.Default;
//    private static TransitionState transitionState;
//    private static int transitionTicks;
//    private static int nextWorldRequestId = 1;
//    private static bool arenaSubserverRunning;
//    private static bool arenaSubserverMatchReady;
//    private static bool readySignalPending;
//    private static bool bootstrapPending;
//    private static int bootstrapTicks;
//    private static int lastReadyPlayers = -1;
//    private static string sscWorldScope = "";
//    private static ulong lastHeartbeatTick;

//    internal static ArenaSubworldRequest ActiveRequest => activeRequest;
//    internal static bool IsTransitioning => transitionState != TransitionState.None;
//    internal static bool HasRunningArenaServer => arenaSubserverRunning;
//    internal static bool HasMatchReadyArenaServer => arenaSubserverRunning && arenaSubserverMatchReady;
//    internal static string SscWorldScope => sscWorldScope;

//    public override void PostUpdateEverything()
//    {
//        if (SubworldSystem.IsActive<ArenasSubworld>())
//        {
//            TickSubworldReadySignal();
//            TickSubworldHeartbeat();
//            TickSubworldBootstrap();
//            return;
//        }

//        if (Main.netMode == NetmodeID.Server && transitionState == TransitionState.None && arenaSubserverRunning
//            && Main.GameUpdateCount - lastHeartbeatTick > 30 * 60)
//        {
//            arenaSubserverRunning = false;
//            arenaSubserverMatchReady = false;
//            Log.Warn("Arenas child heartbeat expired; the next match request will rebuild the child world.");
//            WorldGenManagerNetHandler.ServerFailed("Arenas child heartbeat expired; the next match request will rebuild it.");
//        }

//        if (Main.netMode == NetmodeID.MultiplayerClient || SubworldSystem.AnyActive() || transitionState == TransitionState.None)
//            return;

//        transitionTicks++;
//        switch (transitionState)
//        {
//            case TransitionState.WaitingForMainWorld:
//                ApplyRosterTeams();
//                bool everyoneReturned = roster.All(id => id < 0 || id >= Main.maxPlayers || Main.player[id]?.active == true);
//                if (!everyoneReturned && transitionTicks < ReturnTimeoutTicks)
//                    return;
//                if (!everyoneReturned)
//                    Log.Warn($"Timed out waiting for all arena players to return to Main; continuing with {CountActiveRoster()}/{roster.Count} present.");
//                StopArenaSubserver();
//                transitionState = TransitionState.RestartDelay;
//                transitionTicks = 0;
//                return;

//            case TransitionState.RestartDelay:
//                if (transitionTicks < RestartDelayTicks)
//                    return;
//                StartArenaSubserver();
//                return;

//            case TransitionState.WaitingForReady:
//                if (transitionTicks < StartupTimeoutTicks)
//                    return;
//                string message = $"Arenas child server did not report ready within {StartupTimeoutTicks / 60} seconds (request {pendingRequest.WorldRequestId}).";
//                Log.Error(message);
//                transitionState = TransitionState.None;
//                arenaSubserverRunning = false;
//                arenaSubserverMatchReady = false;
//                WorldGenManagerNetHandler.ServerFailed(message);
//                if (pendingRequest.MovePlayersAfterReady)
//                    ArenaRoundSystem.MarkWorldGenerationFailed(message);
//                return;
//        }
//    }

//    internal static bool PrepareArena(int presetIndex, int countdownTicks, int playingTicks, int generationId)
//    {
//        if (Main.netMode == NetmodeID.MultiplayerClient || IsTransitioning)
//            return false;

//        if (SubworldSystem.IsActive<ArenasSubworld>())
//        {
//            if (!ArenaWorldSystem.MatchReady)
//                return false;

//            if (activeRequest.PresetIndex == presetIndex)
//            {
//                ArenaRoundSystem.PrepareExistingWorldRound(presetIndex, generationId);
//                return true;
//            }

//            CaptureActiveSubworldRoster();
//            ArenaRoundSystem.AbortForWorldRegeneration();
//            if (Main.netMode == NetmodeID.Server)
//            {
//                SendRegenerationRequestToMain(ArenaGenerationMode.Full, "", Main.rand.Next(),
//                    movePlayersAfterReady: true, presetIndex, countdownTicks, playingTicks, generationId);
//                Log.Chat($"[Arena] Preset changed from {activeRequest.PresetIndex} to {presetIndex}; regenerating in its natural biome.");
//                return true;
//            }

//            pendingRequest = NewRequest(presetIndex, countdownTicks, playingTicks, generationId,
//                ArenaGenerationMode.Full, "", movePlayersAfterReady: true);
//            BeginRestart(stopExisting: true);
//            return true;
//        }

//        if (SubworldSystem.AnyActive())
//            return false;

//        CaptureMainRoster(replace: true);
//        if (roster.Count == 0)
//            return false;

//        ArenaSubworldRequest roundRequest = NewRequest(presetIndex, countdownTicks, playingTicks, generationId,
//            ArenaGenerationMode.Full, "", movePlayersAfterReady: true);

//        if (Main.netMode == NetmodeID.Server && HasMatchReadyArenaServer
//            && activeRequest.PresetIndex == presetIndex)
//        {
//            pendingRequest = roundRequest with { WorldRequestId = activeRequest.WorldRequestId };
//            SendPrepareRoundToSubserver(pendingRequest);
//            MoveRosterIntoArena();
//            return true;
//        }

//        pendingRequest = roundRequest;
//        BeginRestart(stopExisting: arenaSubserverRunning);
//        Log.Chat($"[Arena] Preparing the reusable Arenas world before moving {roster.Count} player(s). request={pendingRequest.WorldRequestId}.");
//        return true;
//    }

//    internal static bool RequestWorldGeneration(ArenaGenerationMode mode, string targetStep, int seed)
//    {
//        if (Main.netMode == NetmodeID.MultiplayerClient || IsTransitioning)
//            return false;
//        if (mode == ArenaGenerationMode.ThroughStep && ArenaWorldGenerationCatalog.IndexOf(targetStep) < 0)
//            return false;

//        if (SubworldSystem.IsActive<ArenasSubworld>() && Main.netMode == NetmodeID.Server)
//        {
//            CaptureActiveSubworldRoster();
//            ArenaRoundSystem.AbortForWorldRegeneration();
//            SendRegenerationRequestToMain(mode, targetStep, seed,
//                movePlayersAfterReady: false, 0, -1, -1, Math.Max(1, ArenaRoundSystem.GenerationId));
//            Log.Chat($"[WorldGen] Requested Arenas restart from the child server; evacuating players to Main.");
//            return true;
//        }

//        CaptureMainRoster(replace: false);
//        ArenaRoundSystem.AbortForWorldRegeneration();
//        pendingRequest = NewRequest(0, -1, -1, Math.Max(1, ArenaRoundSystem.GenerationId), mode, targetStep, movePlayersAfterReady: false)
//            with { Seed = seed };
//        BeginRestart(stopExisting: arenaSubserverRunning);
//        WorldGenManagerNetHandler.ServerStarted(pendingRequest);
//        Log.Chat($"[WorldGen] Generation request {pendingRequest.WorldRequestId} accepted; all players will remain in Main.");
//        return true;
//    }

//    internal static void HandlePacket(BinaryReader reader, int fromWho)
//    {
//        Message message = (Message)reader.ReadByte();
//        if (Main.netMode != NetmodeID.Server || fromWho != 256)
//            return;

//        switch (message)
//        {
//            case Message.Ready when !SubworldSystem.AnyActive():
//                HandleReady(reader);
//                break;
//            case Message.PrepareRound when SubworldSystem.IsActive<ArenasSubworld>():
//                HandlePrepareRound(reader);
//                break;
//            case Message.Regenerate when !SubworldSystem.AnyActive():
//                HandleRegenerationRequest(reader);
//                break;
//            case Message.Heartbeat when !SubworldSystem.AnyActive():
//                int heartbeatRequest = reader.ReadInt32();
//                bool heartbeatReady = reader.ReadBoolean();
//                int heartbeatPreset = reader.ReadInt32();
//                if (heartbeatRequest == activeRequest.WorldRequestId || transitionState == TransitionState.None)
//                {
//                    if (heartbeatRequest != activeRequest.WorldRequestId)
//                    {
//                        activeRequest = activeRequest with { WorldRequestId = heartbeatRequest, PresetIndex = heartbeatPreset };
//                        nextWorldRequestId = Math.Max(nextWorldRequestId, heartbeatRequest);
//                        Log.Info($"Adopted an existing Arenas child after reload. request={heartbeatRequest}, matchReady={heartbeatReady}.");
//                    }
//                    lastHeartbeatTick = Main.GameUpdateCount;
//                    arenaSubserverRunning = true;
//                    arenaSubserverMatchReady = heartbeatReady;
//                    activeRequest = activeRequest with { PresetIndex = heartbeatPreset };
//                }
//                break;
//        }
//    }

//    private static void HandleReady(BinaryReader reader)
//    {
//        int requestId = reader.ReadInt32();
//        bool matchReady = reader.ReadBoolean();
//        int completedStep = reader.ReadInt32();
//        if (transitionState != TransitionState.WaitingForReady || requestId != pendingRequest.WorldRequestId)
//        {
//            Log.Warn($"Ignored stale Arenas ready message request={requestId}; expected={pendingRequest.WorldRequestId}, state={transitionState}.");
//            return;
//        }

//        activeRequest = pendingRequest;
//        nextWorldRequestId = Math.Max(nextWorldRequestId, requestId);
//        arenaSubserverRunning = true;
//        arenaSubserverMatchReady = matchReady;
//        lastHeartbeatTick = Main.GameUpdateCount;
//        transitionState = TransitionState.None;
//        transitionTicks = 0;
//        Log.Chat($"[Arena] Child server ready. request={requestId}, matchReady={matchReady}, completedStep={completedStep}.");
//        WorldGenManagerNetHandler.ServerCompleted(activeRequest, completedStep, matchReady);

//        if (!pendingRequest.MovePlayersAfterReady)
//            return;
//        if (!matchReady)
//        {
//            Log.Error("A match requested player transfer, but the generated child world is not match-ready.");
//            ArenaRoundSystem.MarkWorldGenerationFailed("The child reported ready without a validated combat layout.");
//            return;
//        }

//        SendPrepareRoundToSubserver(pendingRequest);
//        MoveRosterIntoArena();
//    }

//    private static void HandlePrepareRound(BinaryReader reader)
//    {
//        activeRequest = activeRequest with
//        {
//            PresetIndex = reader.ReadInt32(),
//            CountdownTicks = reader.ReadInt32(),
//            PlayingTicks = reader.ReadInt32(),
//            GenerationId = reader.ReadInt32(),
//            ExpectedPlayers = Math.Max(1, reader.ReadInt32())
//        };
//        QueueRoundBootstrap();
//        Log.Info($"[ArenaFlow] Child received reusable-world round request. preset={activeRequest.PresetIndex}, generation={activeRequest.GenerationId}, expected={activeRequest.ExpectedPlayers}.");
//    }

//    private static void HandleRegenerationRequest(BinaryReader reader)
//    {
//        ArenaGenerationMode mode = (ArenaGenerationMode)reader.ReadByte();
//        string target = reader.ReadString();
//        int seed = reader.ReadInt32();
//        bool movePlayersAfterReady = reader.ReadBoolean();
//        int presetIndex = reader.ReadInt32();
//        int countdownTicks = reader.ReadInt32();
//        int playingTicks = reader.ReadInt32();
//        int generationId = reader.ReadInt32();
//        ReadTransferredTeams(reader, replace: false);
//        ArenaRoundSystem.AbortForWorldRegeneration();
//        pendingRequest = NewRequest(presetIndex, countdownTicks, playingTicks, generationId,
//                mode, target, movePlayersAfterReady)
//            with { Seed = seed };
//        BeginRestart(stopExisting: true);
//        WorldGenManagerNetHandler.ServerStarted(pendingRequest);
//        Log.Chat($"[WorldGen] Main received generation request {pendingRequest.WorldRequestId} from Arenas; returning {roster.Count} player(s).");
//    }

//    private static ArenaSubworldRequest NewRequest(int preset, int countdown, int playing, int generationId,
//        ArenaGenerationMode mode, string target, bool movePlayersAfterReady) => new(
//            preset,
//            Main.rand?.Next() ?? Environment.TickCount,
//            countdown,
//            playing,
//            Math.Max(1, generationId),
//            Math.Max(1, roster.Count),
//            ++nextWorldRequestId,
//            mode,
//            target ?? "",
//            movePlayersAfterReady);

//    private static void BeginRestart(bool stopExisting)
//    {
//        arenaSubserverMatchReady = false;
//        transitionTicks = 0;
//        if (Main.netMode == NetmodeID.SinglePlayer && SubworldSystem.IsActive<ArenasSubworld>())
//        {
//            transitionState = TransitionState.RestartDelay;
//            SubworldSystem.Exit();
//            return;
//        }

//        if (stopExisting)
//        {
//            transitionState = TransitionState.WaitingForMainWorld;
//            foreach (int playerId in roster)
//                if (playerId >= 0 && playerId < Main.maxPlayers)
//                    SubworldSystem.MovePlayerToMainWorld(playerId);
//            return;
//        }

//        transitionState = TransitionState.RestartDelay;
//    }

//    private static void StartArenaSubserver()
//    {
//        transitionTicks = 0;
//        activeRequest = pendingRequest;
//        if (Main.netMode == NetmodeID.SinglePlayer)
//        {
//            transitionState = TransitionState.WaitingForReady;
//            bool entered = SubworldSystem.Enter<ArenasSubworld>();
//            Log.Info($"Single-player Arenas entry requested for world request {pendingRequest.WorldRequestId}; accepted={entered}.");
//            if (!entered)
//            {
//                transitionState = TransitionState.None;
//                WorldGenManagerNetHandler.ServerFailed("Subworld Library rejected the Arenas entry request.");
//                if (pendingRequest.MovePlayersAfterReady)
//                    ArenaRoundSystem.MarkWorldGenerationFailed("Subworld Library rejected the Arenas entry request.");
//            }
//            return;
//        }

//        int index = SubworldSystem.GetIndex<ArenasSubworld>();
//        if (index < 0)
//        {
//            transitionState = TransitionState.None;
//            WorldGenManagerNetHandler.ServerFailed("Subworld Library did not register ArenasSubworld.");
//            if (pendingRequest.MovePlayersAfterReady)
//                ArenaRoundSystem.MarkWorldGenerationFailed("Subworld Library did not register ArenasSubworld.");
//            return;
//        }

//        transitionState = TransitionState.WaitingForReady;
//        SubworldSystem.StartSubserver(index);
//        WorldGenManagerNetHandler.ServerGenerating(pendingRequest);
//        Log.Info($"Started Arenas child server index={index}, request={pendingRequest.WorldRequestId}, mode={pendingRequest.GenerationMode}.");
//    }

//    private static void StopArenaSubserver()
//    {
//        int index = SubworldSystem.GetIndex<ArenasSubworld>();
//        if (index >= 0 && arenaSubserverRunning)
//            SubworldSystem.StopSubserver(index);
//        arenaSubserverRunning = false;
//        arenaSubserverMatchReady = false;
//        Log.Info($"Stopped reusable Arenas child server index={index} for controlled regeneration.");
//    }

//    internal static void QueueSubworldReady(bool matchReady)
//    {
//        if (matchReady)
//            QueueRoundBootstrap();

//        if (Main.netMode == NetmodeID.SinglePlayer)
//        {
//            arenaSubserverRunning = true;
//            arenaSubserverMatchReady = matchReady;
//            transitionState = TransitionState.None;
//            int completed = CompletedStepFor(activeRequest);
//            WorldGenManagerNetHandler.ServerCompleted(activeRequest, completed, matchReady);
//            return;
//        }

//        readySignalPending = Main.netMode == NetmodeID.Server;
//    }

//    private static void TickSubworldReadySignal()
//    {
//        if (!readySignalPending || Main.netMode != NetmodeID.Server)
//            return;
//        try
//        {
//            using MemoryStream stream = new();
//            using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
//            {
//                writer.Write((byte)Arenas.ArenasPacketType.ArenaSubworld);
//                writer.Write((byte)Message.Ready);
//                writer.Write(activeRequest.WorldRequestId);
//                writer.Write(ArenaWorldSystem.MatchReady);
//                writer.Write(CompletedStepFor(activeRequest));
//            }
//            SubworldSystem.SendToMainServer(ModContent.GetInstance<Arenas>(), stream.ToArray());
//            readySignalPending = false;
//            Log.Info($"Sent Arenas ready signal to Main for request {activeRequest.WorldRequestId}.");
//        }
//        catch (Exception exception)
//        {
//            if (Main.GameUpdateCount % 60 == 0)
//                Log.Debug($"Waiting for child-to-main pipe before ready signal: {exception.Message}");
//        }
//    }

//    private static void TickSubworldHeartbeat()
//    {
//        if (Main.netMode != NetmodeID.Server || readySignalPending || Main.GameUpdateCount % (5 * 60) != 0)
//            return;
//        try
//        {
//            using MemoryStream stream = new();
//            using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
//            {
//                writer.Write((byte)Arenas.ArenasPacketType.ArenaSubworld);
//                writer.Write((byte)Message.Heartbeat);
//                writer.Write(activeRequest.WorldRequestId);
//                writer.Write(ArenaWorldSystem.MatchReady);
//                writer.Write(activeRequest.PresetIndex);
//            }
//            SubworldSystem.SendToMainServer(ModContent.GetInstance<Arenas>(), stream.ToArray());
//        }
//        catch (Exception exception)
//        {
//            if (Main.GameUpdateCount % (30 * 60) == 0)
//                Log.Debug($"Arenas heartbeat could not reach Main: {exception.Message}");
//        }
//    }

//    private static int CompletedStepFor(ArenaSubworldRequest request) => request.GenerationMode switch
//    {
//        ArenaGenerationMode.ClearOnly => -1,
//        ArenaGenerationMode.ThroughStep => ArenaWorldGenerationCatalog.IndexOf(request.TargetStep),
//        _ => ArenaWorldGenerationCatalog.Steps.Length - 1
//    };

//    private static void SendPrepareRoundToSubserver(ArenaSubworldRequest request)
//    {
//        using MemoryStream stream = new();
//        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
//        {
//            writer.Write((byte)Arenas.ArenasPacketType.ArenaSubworld);
//            writer.Write((byte)Message.PrepareRound);
//            writer.Write(request.PresetIndex);
//            writer.Write(request.CountdownTicks);
//            writer.Write(request.PlayingTicks);
//            writer.Write(request.GenerationId);
//            writer.Write(request.ExpectedPlayers);
//        }
//        int index = SubworldSystem.GetIndex<ArenasSubworld>();
//        SubworldSystem.SendToSubserver(index, ModContent.GetInstance<Arenas>(), stream.ToArray());
//    }

//    private static void SendRegenerationRequestToMain(ArenaGenerationMode mode, string target, int seed,
//        bool movePlayersAfterReady, int presetIndex, int countdownTicks, int playingTicks, int generationId)
//    {
//        using MemoryStream stream = new();
//        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
//        {
//            writer.Write((byte)Arenas.ArenasPacketType.ArenaSubworld);
//            writer.Write((byte)Message.Regenerate);
//            writer.Write((byte)mode);
//            writer.Write(target ?? "");
//            writer.Write(seed);
//            writer.Write(movePlayersAfterReady);
//            writer.Write(presetIndex);
//            writer.Write(countdownTicks);
//            writer.Write(playingTicks);
//            writer.Write(generationId);
//            WriteTeams(writer);
//        }
//        SubworldSystem.SendToMainServer(ModContent.GetInstance<Arenas>(), stream.ToArray());
//    }

//    private static void CaptureMainRoster(bool replace)
//    {
//        if (replace)
//        {
//            roster.Clear();
//            rosterTeams.Clear();
//        }
//        sscWorldScope = Main.ActiveWorldFileData?.Name ?? Main.worldName ?? sscWorldScope;
//        ArenaRoundSystem.AssignBalancedTeams();
//        foreach (Player player in Main.player.Where(player => player?.active == true))
//        {
//            roster.Add(player.whoAmI);
//            Team team = (Team)player.team;
//            if (team is Team.Red or Team.Blue)
//                rosterTeams[player.whoAmI] = team;
//        }
//        if (Main.netMode == NetmodeID.SinglePlayer && Main.LocalPlayer?.active == true)
//            roster.Add(Main.myPlayer);
//    }

//    private static void CaptureActiveSubworldRoster()
//    {
//        roster.Clear();
//        rosterTeams.Clear();
//        ArenaRoundSystem.AssignBalancedTeams();
//        foreach (Player player in Main.player.Where(player => player?.active == true))
//        {
//            roster.Add(player.whoAmI);
//            Team team = (Team)player.team;
//            if (team is Team.Red or Team.Blue)
//                rosterTeams[player.whoAmI] = team;
//        }
//    }

//    private static void WriteTeams(BinaryWriter writer)
//    {
//        writer.Write(rosterTeams.Count);
//        foreach ((int playerId, Team team) in rosterTeams)
//        {
//            writer.Write((byte)playerId);
//            writer.Write((byte)team);
//        }
//    }

//    private static void ReadTransferredTeams(BinaryReader reader, bool replace)
//    {
//        if (replace)
//        {
//            roster.Clear();
//            rosterTeams.Clear();
//        }
//        int count = Math.Clamp(reader.ReadInt32(), 0, Main.maxPlayers);
//        for (int i = 0; i < count; i++)
//        {
//            int playerId = reader.ReadByte();
//            Team team = (Team)reader.ReadByte();
//            if (playerId < 0 || playerId >= Main.maxPlayers || team is not (Team.Red or Team.Blue))
//                continue;
//            roster.Add(playerId);
//            rosterTeams[playerId] = team;
//        }
//    }

//    private static void MoveRosterIntoArena()
//    {
//        ApplyRosterTeams();
//        Log.Chat($"[Arena] Moving {CountActiveRoster()} player(s) into the ready Arenas server; no world regeneration is occurring.");
//        if (Main.netMode == NetmodeID.SinglePlayer)
//            return;
//        foreach (int playerId in roster.Where(id => id >= 0 && id < Main.maxPlayers && Main.player[id]?.active == true).ToArray())
//            SubworldSystem.MovePlayerToSubworld<ArenasSubworld>(playerId);
//    }

//    internal static void MoveFromMainToExistingArena(int targetPlayer)
//    {
//        if (Main.netMode != NetmodeID.Server || SubworldSystem.AnyActive() || IsTransitioning || !HasRunningArenaServer)
//            return;
//        IEnumerable<int> targets = targetPlayer >= 0
//            ? [targetPlayer]
//            : Enumerable.Range(0, Main.maxPlayers).Where(id => Main.player[id]?.active == true);
//        foreach (int playerId in targets.Where(id => id >= 0 && id < Main.maxPlayers && Main.player[id]?.active == true).ToArray())
//            SubworldSystem.MovePlayerToSubworld<ArenasSubworld>(playerId);
//    }

//    internal static void MoveFromArenaToMain(int targetPlayer)
//    {
//        if (Main.netMode == NetmodeID.SinglePlayer)
//        {
//            if (SubworldSystem.IsActive<ArenasSubworld>())
//                SubworldSystem.Exit();
//            return;
//        }
//        if (Main.netMode != NetmodeID.Server || !SubworldSystem.IsActive<ArenasSubworld>())
//            return;
//        IEnumerable<int> targets = targetPlayer >= 0 ? [targetPlayer] : Main.player.Where(player => player?.active == true).Select(player => player.whoAmI);
//        foreach (int playerId in targets.ToArray())
//            SubworldSystem.MovePlayerToMainWorld(playerId);
//    }

//    private static void QueueRoundBootstrap()
//    {
//        bootstrapPending = true;
//        bootstrapTicks = 0;
//        lastReadyPlayers = -1;
//        ArenaRoundSystem.PrepareSubworldRound(activeRequest.PresetIndex, activeRequest.GenerationId);
//    }

//    private static void TickSubworldBootstrap()
//    {
//        if (Main.netMode == NetmodeID.MultiplayerClient || !bootstrapPending || !ArenaWorldSystem.MatchReady)
//            return;
//        bootstrapTicks++;
//        int readyPlayers = Main.player.Count(player => player?.active == true);
//        if (readyPlayers != lastReadyPlayers)
//        {
//            lastReadyPlayers = readyPlayers;
//            Log.Info($"Arena player readiness changed: {readyPlayers}/{activeRequest.ExpectedPlayers}.");
//        }
//        if (readyPlayers <= 0 || readyPlayers < activeRequest.ExpectedPlayers && bootstrapTicks < ReturnTimeoutTicks)
//            return;
//        bootstrapPending = false;
//        ArenaRoundSystem.MarkSubworldReady(activeRequest.PresetIndex, activeRequest.GenerationId);
//    }

//    private static int CountActiveRoster() => roster.Count(id => id >= 0 && id < Main.maxPlayers && Main.player[id]?.active == true);

//    private static void ApplyRosterTeams()
//    {
//        foreach ((int playerId, Team team) in rosterTeams)
//        {
//            if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId]?.active != true)
//                continue;
//            Player player = Main.player[playerId];
//            if ((Team)player.team == team)
//                continue;
//            player.team = (int)team;
//            if (Main.netMode == NetmodeID.Server)
//                NetMessage.SendData(MessageID.PlayerTeam, -1, -1, null, playerId, player.team);
//        }
//    }

//    public void CopyMainWorldData()
//    {
//        TagCompound request = new()
//        {
//            ["preset"] = pendingRequest.PresetIndex,
//            ["seed"] = pendingRequest.Seed,
//            ["countdown"] = pendingRequest.CountdownTicks,
//            ["playing"] = pendingRequest.PlayingTicks,
//            ["generation"] = pendingRequest.GenerationId,
//            ["expectedPlayers"] = pendingRequest.ExpectedPlayers,
//            ["worldRequest"] = pendingRequest.WorldRequestId,
//            ["generationMode"] = (int)pendingRequest.GenerationMode,
//            ["targetStep"] = pendingRequest.TargetStep ?? "",
//            ["movePlayers"] = pendingRequest.MovePlayersAfterReady,
//            ["teamIds"] = rosterTeams.Keys.ToList(),
//            ["teams"] = rosterTeams.Values.Select(team => (int)team).ToList(),
//            ["sscWorldScope"] = sscWorldScope
//        };
//        SubworldSystem.CopyWorldData(RequestKey, request);
//        Log.Info($"Copied world request {pendingRequest.WorldRequestId} to child. mode={pendingRequest.GenerationMode}, target='{pendingRequest.TargetStep}'.");
//    }

//    public void ReadCopiedMainWorldData()
//    {
//        TagCompound request = SubworldSystem.ReadCopiedWorldData<TagCompound>(RequestKey);
//        activeRequest = request == null ? pendingRequest : new ArenaSubworldRequest(
//            request.GetInt("preset"),
//            request.GetInt("seed"),
//            request.GetInt("countdown"),
//            request.GetInt("playing"),
//            Math.Max(1, request.GetInt("generation")),
//            Math.Max(1, request.GetInt("expectedPlayers")),
//            Math.Max(1, request.GetInt("worldRequest")),
//            (ArenaGenerationMode)request.GetInt("generationMode"),
//            request.GetString("targetStep"),
//            request.GetBool("movePlayers"));
//        nextWorldRequestId = Math.Max(nextWorldRequestId, activeRequest.WorldRequestId);
//        roster.Clear();
//        rosterTeams.Clear();
//        if (request != null)
//        {
//            IList<int> ids = request.GetList<int>("teamIds");
//            IList<int> teams = request.GetList<int>("teams");
//            for (int i = 0; i < Math.Min(ids.Count, teams.Count); i++)
//            {
//                Team team = (Team)teams[i];
//                if (ids[i] is >= 0 and < Main.maxPlayers && team is Team.Red or Team.Blue)
//                {
//                    roster.Add(ids[i]);
//                    rosterTeams[ids[i]] = team;
//                }
//            }
//            sscWorldScope = request.GetString("sscWorldScope");
//        }
//        Log.Info($"Child read world request {activeRequest.WorldRequestId}. mode={activeRequest.GenerationMode}, target='{activeRequest.TargetStep}', seed={activeRequest.Seed}.");
//    }
//}
