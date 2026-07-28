using PvPArenas.Common.Game.LoadoutSelector;
using PvPArenas.Core.Configs;
using PvPFramework.Common.EndScreen;
using PvPFramework.Common.Scoreboard;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.Enums;

namespace PvPArenas.Common.Game.Score;

internal static class ArenaEndScreen
{
    internal static void Present(Team winningTeam, int winningPlayerId)
    {
        EndScreenSummary summary = new();
        Team[] teams = Main.player.Where(player => player?.active == true).Select(player => (Team)player.team)
            .Where(team => team is Team.Red or Team.Blue)
            .Distinct()
            .ToArray();

        foreach (Team team in teams)
        {
            summary.Scores.Add(new TeamScoreEntry(team, team == winningTeam ? 1 : 0));
            summary.Results[team] = winningTeam == Team.None
                ? EndScreenResult.Tie
                : team == winningTeam ? EndScreenResult.Victory : EndScreenResult.Defeat;
        }

        Player[] players = Main.player.Where(player => player?.active == true
                && (Team)player.team is Team.Red or Team.Blue)
            .OrderByDescending(player => player.GetModPlayer<ArenaPlayer>().BossDamage)
            .ThenBy(player => player.whoAmI)
            .ToArray();
        Dictionary<long, int> damageRanks = players.Select(player => player.GetModPlayer<ArenaPlayer>().BossDamage)
            .Distinct()
            .OrderByDescending(damage => damage)
            .Select((damage, rank) => (damage, rank))
            .ToDictionary(entry => entry.damage, entry => entry.rank);

        uint gemReward = (uint)Math.Max(0, ModContent.GetInstance<ServerConfig>().VictoryGemReward);
        foreach (Player player in players)
        {
            ScoreboardEntry stats = ScoreboardService.GetPlayerStats(player);
            long bossDamage = player.GetModPlayer<ArenaPlayer>().BossDamage;
            string role = DamageTitle(bossDamage, damageRanks[bossDamage]);
            summary.Players.Add(new EndScreenPlayerStats(
                stats.PlayerId, stats.Team, stats.Name, stats.Kills, stats.Deaths,
                Clamp(stats.Damage), 0, 0, 0, 0, 0, 0, Clamp(bossDamage), 0, 0, 0,
                role, $"{bossDamage} damage"));

            if (winningTeam != Team.None && (Team)player.team == winningTeam && gemReward > 0)
                summary.PlayerRewards[stats.PlayerId] = gemReward;
        }

        EndScreenService.Present(summary);
    }

    private static string DamageTitle(long damage, int rank)
    {
        if (damage <= 0)
            return "Witness";

        return rank switch
        {
            0 => "God",
            1 => "Legend",
            2 => "Hero",
            3 => "Slayer",
            4 => "Hunter",
            _ => "Fighter"
        };
    }

    private static uint Clamp(long value) => (uint)Math.Clamp(value, 0L, uint.MaxValue);
}
