using PvPArenas.Common.Game;
using PvPArenas.Common.Game.BossVoting;
using PvPArenas.Common.Game.LoadoutSelector;
using PvPArenas.Core.Compat;
using System;
using System.IO;
using Terraria.ID;

namespace PvPArenas;

public sealed class PvPArenas : Mod
{
    internal enum PacketType : byte
    {
        CastVote,
        AdminRoundAction,
        SelectLoadout
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        PacketType type = (PacketType)reader.ReadByte();
        if (Main.netMode != NetmodeID.Server)
            return;

        switch (type)
        {
            case PacketType.CastVote:
                ModContent.GetInstance<BossVoteSystem>().CastVote(whoAmI, reader.ReadByte());
                break;

            case PacketType.AdminRoundAction:
                RoundManager.AdminAction action = (RoundManager.AdminAction)reader.ReadByte();
                if (!Enum.IsDefined(action))
                    return;
                if (!ErkySSCCompat.IsAdmin(whoAmI, out string reason))
                {
                    Log.Warn($"Rejected Arenas Game Manager action from player {whoAmI}: {reason}");
                    return;
                }

                ModContent.GetInstance<RoundManager>().ExecuteAdminAction(action, whoAmI);
                break;

            case PacketType.SelectLoadout:
                ArenaPlayer.HandleLoadoutSelect(whoAmI, reader.ReadByte());
                break;
        }
    }
}

