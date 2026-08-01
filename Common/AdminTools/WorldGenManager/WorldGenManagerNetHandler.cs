using PvPArenas.Core.Compat;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

internal enum WorldClearAction : byte
{
    Tiles,
    Walls,
    Liquids,
    Wiring,
    PaintAndCoatings,
    Everything
}

internal static class WorldGenManagerNetHandler
{
    private enum RequestKind : byte
    {
        Status,
        RunPasses,
        ClearWorld
    }

    private const int MaxRequestedPasses = 100;

    internal static void RequestStatus()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = BeginRequest(RequestKind.Status);
        packet.Send();
    }

    internal static bool RequestRunPasses(IReadOnlyList<string> passes, out string error)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return ModContent.GetInstance<WorldGenPassRunner>().TryRun(passes, out error);

        error = "";
        if (passes == null || passes.Count == 0)
        {
            error = "Select at least one world-generation pass.";
            return false;
        }
        if (passes.Count > MaxRequestedPasses)
        {
            error = $"At most {MaxRequestedPasses} passes can be requested at once.";
            return false;
        }

        ModPacket packet = BeginRequest(RequestKind.RunPasses);
        packet.Write((byte)passes.Count);
        foreach (string pass in passes)
            packet.Write(pass ?? "");
        packet.Send();
        ModContent.GetInstance<WorldGenPassRunner>().SetAwaitingServer("World-generation request sent to server");
        return true;
    }

    internal static bool RequestClear(WorldClearAction action, out string error)
    {
        if (!Enum.IsDefined(action))
        {
            error = "Unknown world cleanup action.";
            return false;
        }
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return ModContent.GetInstance<WorldGenPassRunner>().TryClear(action, out error);

        error = "";
        ModPacket packet = BeginRequest(RequestKind.ClearWorld);
        packet.Write((byte)action);
        packet.Send();
        ModContent.GetInstance<WorldGenPassRunner>().SetAwaitingServer("World cleanup request sent to server");
        return true;
    }

    internal static void HandleRequest(BinaryReader reader, int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        RequestKind kind = (RequestKind)reader.ReadByte();
        if (!Enum.IsDefined(kind))
            return;
        if (!ErkySSCCompat.IsAdmin(whoAmI, out string reason))
        {
            SendNotice(whoAmI, $"World Gen Manager request rejected: {reason}", Color.OrangeRed);
            return;
        }

        WorldGenPassRunner runner = ModContent.GetInstance<WorldGenPassRunner>();
        switch (kind)
        {
            case RequestKind.Status:
                SendStatus(runner, whoAmI);
                break;

            case RequestKind.RunPasses:
            {
                int count = reader.ReadByte();
                if (count <= 0 || count > MaxRequestedPasses)
                {
                    SendNotice(whoAmI, "Invalid world-generation pass count.", Color.OrangeRed);
                    return;
                }

                List<string> passes = new(count);
                for (int i = 0; i < count; i++)
                    passes.Add(reader.ReadString());
                if (!runner.TryRun(passes, out string error))
                    SendNotice(whoAmI, error, Color.OrangeRed);
                else
                    SendStatus(runner);
                break;
            }

            case RequestKind.ClearWorld:
            {
                WorldClearAction action = (WorldClearAction)reader.ReadByte();
                if (!Enum.IsDefined(action))
                {
                    SendNotice(whoAmI, "Unknown world cleanup action.", Color.OrangeRed);
                    return;
                }
                if (!runner.TryClear(action, out string error))
                    SendNotice(whoAmI, error, Color.OrangeRed);
                else
                    SendStatus(runner);
                break;
            }
        }
    }

    internal static void HandleStatus(BinaryReader reader)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModContent.GetInstance<WorldGenPassRunner>().ApplyNetworkStatus(
            reader.ReadBoolean(),
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadInt32(),
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadBoolean());
    }

    internal static void SendStatus(WorldGenPassRunner runner, int toClient = -1)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        ModPacket packet = ModContent.GetInstance<PvPArenas>().GetPacket();
        packet.Write((byte)PvPArenas.PacketType.WorldGenStatus);
        packet.Write(runner.Busy);
        packet.Write(runner.Status ?? "Idle");
        packet.Write(runner.ActivePass ?? "");
        packet.Write(runner.Seed);
        packet.Write(runner.Progress);
        packet.Write(runner.Elapsed.TotalSeconds);
        packet.Write(!string.IsNullOrWhiteSpace(runner.BackupPath));
        packet.Send(toClient);
    }

    private static ModPacket BeginRequest(RequestKind kind)
    {
        ModPacket packet = ModContent.GetInstance<PvPArenas>().GetPacket();
        packet.Write((byte)PvPArenas.PacketType.WorldGenRequest);
        packet.Write((byte)kind);
        return packet;
    }

    private static void SendNotice(int toClient, string message, Color color)
    {
        ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(message), color, toClient);
        SendStatus(ModContent.GetInstance<WorldGenPassRunner>(), toClient);
    }
}
