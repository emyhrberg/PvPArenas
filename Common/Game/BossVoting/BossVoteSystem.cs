using PvPArenas.Common.Game.LoadoutSelector;
using PvPArenas.Core.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ID;

namespace PvPArenas.Common.Game.BossVoting;

/// <summary>Server-authoritative boss voting during the intermission, synced through world data.</summary>
internal sealed class BossVoteSystem : ModSystem
{
    private readonly Dictionary<int, int> votes = [];
    private readonly List<List<byte>> voters = [];

    /// <summary>Valid configured presets, paired with their FightPresets index.</summary>
    internal static List<(int PresetIndex, BossFightPreset Preset)> VotablePresets()
    {
        List<(int, BossFightPreset)> result = [];
        var presets = ModContent.GetInstance<ServerConfig>().FightPresets;
        if (presets == null)
            return result;

        for (int i = 0; i < presets.Count; i++)
            if (IsVotable(presets[i]))
                result.Add((i, presets[i]));
        return result;
    }

    internal static bool IsVotable(BossFightPreset preset)
    {
        if (preset == null || !Enum.IsDefined(preset.ArenaKind))
            return false;

        // Sandbox arenas have no boss NPC but are still a selectable arena.
        if (preset.IsSandbox())
            return true;

        return preset.Boss.Type > NPCID.None && preset.Boss.Type < NPCLoader.NPCCount;
    }

    internal IReadOnlyList<byte> VotersFor(int option) => option >= 0 && option < voters.Count ? voters[option] : [];
    internal int VoteCount(int option) => option >= 0 && option < voters.Count ? voters[option].Count : 0;

    internal int LocalVote
    {
        get
        {
            for (int i = 0; i < voters.Count; i++)
                if (voters[i].Contains((byte)Main.myPlayer))
                    return i;
            return -1;
        }
    }

    internal static void RequestVote(int option)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = ModContent.GetInstance<PvPArenas>().GetPacket();
            packet.Write((byte)PvPArenas.PacketType.CastVote);
            packet.Write((byte)option);
            packet.Send();
            return;
        }

        ModContent.GetInstance<BossVoteSystem>().CastVote(Main.myPlayer, option);
    }

    internal void CastVote(int playerId, int option)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;
        if (ModContent.GetInstance<RoundManager>().CurrentPhase != RoundManager.RoundPhase.VotingOrEndScreen)
            return;
        if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId]?.active != true)
            return;
        if (option < 0 || option >= VotablePresets().Count)
            return;

        votes[playerId] = option;
        RebuildVoters();
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
    }

    /// <summary>Clears votes for a fresh intermission. The caller is responsible for syncing.</summary>
    internal void Reset()
    {
        votes.Clear();
        RebuildVoters();
    }

    /// <summary>Returns the majority-voted FightPresets index (random tiebreak), or -1 without options.</summary>
    internal int ResolveWinner()
    {
        var votable = VotablePresets();
        if (votable.Count == 0)
            return -1;

        RebuildVoters();
        int best = 0;
        for (int i = 0; i < votable.Count; i++)
            best = System.Math.Max(best, VoteCount(i));

        List<int> winners = [];
        for (int i = 0; i < votable.Count; i++)
            if (best == 0 || VoteCount(i) == best)
                winners.Add(i);

        return votable[winners[Main.rand.Next(winners.Count)]].PresetIndex;
    }

    private void RebuildVoters()
    {
        int options = VotablePresets().Count;
        voters.Clear();
        for (int i = 0; i < options; i++)
            voters.Add([]);

        foreach ((int playerId, int option) in votes)
            if (option >= 0 && option < options && Main.player[playerId]?.active == true)
                voters[option].Add((byte)playerId);
    }

    public override void ClearWorld()
    {
        votes.Clear();
        voters.Clear();
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write((byte)voters.Count);
        foreach (List<byte> group in voters)
        {
            writer.Write((byte)group.Count);
            foreach (byte voter in group)
                writer.Write(voter);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        voters.Clear();
        for (int i = reader.ReadByte(); i > 0; i--)
        {
            List<byte> group = [];
            for (int n = reader.ReadByte(); n > 0; n--)
                group.Add(reader.ReadByte());
            voters.Add(group);
        }
    }
}
