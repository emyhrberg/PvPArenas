using PvPArenas.Common.Game.LoadoutSelector;
using PvPArenas.Core.Compat;
using PvPArenas.Core.Configs.ConfigElements;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace PvPArenas.Core.Configs;

internal sealed class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    #region Fields
    [Header("BossFights")]
    [Expand(true)]
    [CustomModConfigItem(typeof(BossFightPresetListElement))]
    public List<BossFightPreset> FightPresets = Common.Game.LoadoutSelector.FightPresets.CreateFightPresets();

    [Header("RoundTime")]

    [DefaultValue(600), Range(1, 3600)]
    public int RoundDurationSeconds = 600;

    [DefaultValue(20)]
    [Range(0, 300)]
    public int FreezeCountdownSeconds = 20;

    [DefaultValue(30)]
    [Range(5, 300)]
    public int VotingDurationSeconds = 30;

    [Header("GemRewards")]

    [DefaultValue(10), Range(0, 150)]
    public int VictoryGemReward = 10;
    #endregion

    #region Hooks
    public override void OnLoaded() => EnsureSandboxPreset();

    public override void OnChanged() => EnsureSandboxPreset();

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
    {
        string reason = "";
        bool accepted = Main.netMode == NetmodeID.SinglePlayer || ErkySSCCompat.IsAdmin(whoAmI, out reason);
        message = NetworkText.FromLiteral(accepted ? "Saved" : reason);
        return accepted;
    }
    #endregion

    private void EnsureSandboxPreset()
    {
        // TODO
    }
}
