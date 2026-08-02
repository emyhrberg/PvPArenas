using PvPArenas.Common.AdminTools.UI;
using PvPArenas.Common.Game;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;

namespace PvPArenas.Common.AdminTools.GameManager;

internal sealed class ArenaGameManagerPanel : UIDraggablePanel
{
    protected override float MinResizeW => 360f;
    protected override float MinResizeH => 270f;
    protected override float MaxResizeW => 520f;
    protected override float MaxResizeH => 400f;

    public ArenaGameManagerPanel() : base(Language.GetTextValue("Mods.PvPArenas.Tools.ArenaGameManagerPanel.Title"))
    {
        Width.Set(420, 0);
        Height.Set(290, 0);
        HAlign = .5f;
        Top.Set(90, 0);
        Content.SetPadding(0);

        Content.Append(new UIText("Round Controls", .85f)
        {
            Left = { Pixels = 11 },
            Top = { Pixels = 11 },
            TextColor = new Color(255, 228, 140)
        });

        ArenaGameStatusPanel status = new()
        {
            Left = { Pixels = 10 },
            Top = { Pixels = 32 },
            Width = { Pixels = -20, Percent = 1f },
            Height = { Pixels = 78 }
        };
        Content.Append(status);

        status.Append(new ArenaGameCommandButton(
            RoundButtonLabel,
            RoundButtonTooltip,
            () => Manager.CurrentPhase != RoundManager.RoundPhase.Generating,
            () => Manager.CurrentPhase == RoundManager.RoundPhase.Playing,
            ToggleRound,
            () => Manager.CurrentPhase == RoundManager.RoundPhase.Playing
                ? VanillaAdminIcons.Pause
                : VanillaAdminIcons.PlayPause)
        {
            Left = { Pixels = 8 },
            Top = { Pixels = 32 },
            Width = { Pixels = -10, Percent = 1f / 3f },
            Height = { Pixels = 38 }
        });

        status.Append(new ArenaGameCommandButton(
            VotingButtonLabel,
            VotingButtonTooltip,
            () => Manager.CurrentPhase != RoundManager.RoundPhase.Generating,
            () => Manager.CurrentPhase == RoundManager.RoundPhase.VotingOrEndScreen,
            ToggleVoting,
            () => VanillaAdminIcons.PlayPause)
        {
            Left = { Pixels = 3, Percent = 1f / 3f },
            Top = { Pixels = 32 },
            Width = { Pixels = -10, Percent = 1f / 3f },
            Height = { Pixels = 38 }
        });

        status.Append(new ArenaGameCommandButton(
            () => "Set Waiting",
            IdleButtonTooltip,
            () => Manager.CurrentPhase != RoundManager.RoundPhase.Generating
                && !(Manager.CurrentPhase == RoundManager.RoundPhase.WaitingForPlayers && Manager.IsIdleHeld),
            () => Manager.CurrentPhase == RoundManager.RoundPhase.Playing,
            () => RoundManager.RequestAdminAction(RoundManager.AdminAction.SetIdle),
            () => VanillaAdminIcons.Pause)
        {
            Left = { Pixels = -2, Percent = 2f / 3f },
            Top = { Pixels = 32 },
            Width = { Pixels = -10, Percent = 1f / 3f },
            Height = { Pixels = 38 }
        });

        Content.Append(new UIText("Team Balancing", .85f)
        {
            Left = { Pixels = 11 },
            Top = { Pixels = 121 },
            TextColor = new Color(255, 228, 140)
        });

        ArenaTeamBalancePanel teams = new()
        {
            Left = { Pixels = 10 },
            Top = { Pixels = 142 },
            Width = { Pixels = -20, Percent = 1f },
            Height = { Pixels = 103 }
        };
        Content.Append(teams);

        teams.Append(new ArenaGameCommandButton(
            () => "Auto Balance Teams",
            () => Manager.CurrentPhase is RoundManager.RoundPhase.WaitingForPlayers
                or RoundManager.RoundPhase.VotingOrEndScreen
                ? "Randomly split all players evenly between Red and Blue"
                : "Teams can only be balanced between rounds",
            () => Manager.CurrentPhase is RoundManager.RoundPhase.WaitingForPlayers
                or RoundManager.RoundPhase.VotingOrEndScreen,
            () => false,
            () => RoundManager.RequestAdminAction(RoundManager.AdminAction.AutoBalanceTeams),
            () => VanillaAdminIcons.MixedSeed)
        {
            Left = { Pixels = 8 },
            Top = { Pixels = 66 },
            Width = { Pixels = -16, Percent = 1f },
            Height = { Pixels = 29 }
        });
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<ArenaGameManagerUISystem>().Close();

    protected override void OnRefreshPanelLeftClick()
    {
    }

    private static RoundManager Manager => ModContent.GetInstance<RoundManager>();

    private static string RoundButtonLabel() => Manager.CurrentPhase == RoundManager.RoundPhase.Playing
        ? "End Round"
        : "Start Round";

    private static string VotingButtonLabel() => Manager.CurrentPhase == RoundManager.RoundPhase.VotingOrEndScreen
        ? "End Voting"
        : "Start Voting";

    private static string RoundButtonTooltip() => Manager.CurrentPhase switch
    {
        RoundManager.RoundPhase.Playing => "End the current round and begin voting",
        RoundManager.RoundPhase.FreezeCountdown => "Start the round now",
        RoundManager.RoundPhase.VotingOrEndScreen => "End voting and prepare the selected round",
        RoundManager.RoundPhase.Generating => "The arena is being prepared",
        _ => "Prepare and start a round"
    };

    private static string VotingButtonTooltip() => Manager.CurrentPhase switch
    {
        RoundManager.RoundPhase.VotingOrEndScreen => "End voting and prepare the selected round",
        RoundManager.RoundPhase.Playing => "End the current round and begin voting",
        RoundManager.RoundPhase.Generating => "The arena is being prepared",
        _ => "Begin a new vote"
    };

    private static string IdleButtonTooltip() => Manager.CurrentPhase switch
    {
        RoundManager.RoundPhase.Generating => "The arena is being prepared",
        RoundManager.RoundPhase.WaitingForPlayers when Manager.IsIdleHeld => "The game is already waiting",
        RoundManager.RoundPhase.Playing => "Stop the round and hold the game in Waiting until an admin starts it",
        _ => "Hold the game in Waiting until an admin starts it"
    };

    private static void ToggleRound()
    {
        RoundManager.AdminAction action = Manager.CurrentPhase == RoundManager.RoundPhase.Playing
            ? RoundManager.AdminAction.EndRound
            : RoundManager.AdminAction.StartRound;
        RoundManager.RequestAdminAction(action);
    }

    private static void ToggleVoting()
    {
        RoundManager.AdminAction action = Manager.CurrentPhase == RoundManager.RoundPhase.VotingOrEndScreen
            ? RoundManager.AdminAction.EndVoting
            : RoundManager.AdminAction.StartVoting;
        RoundManager.RequestAdminAction(action);
    }
}
