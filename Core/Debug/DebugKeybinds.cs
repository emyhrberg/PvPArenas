#if DEBUG
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PvPArenas.Common.Game;
using Terraria;
using Terraria.ModLoader;

namespace PvPArenas.Core.Debug;

[Autoload(Side = ModSide.Client)]
internal sealed class DebugKeybinds : ModSystem
{
    internal static readonly Color MessageColor = new(80, 210, 255);

    private bool numPad1Released = true;

    public override void OnWorldLoad() =>
        numPad1Released = true;

    public override void PostUpdateEverything()
    {
        if (Main.gameMenu ||
            !PressedWithControl(
                Keys.NumPad1,
                ref numPad1Released))
        {
            return;
        }

        RoundManager manager =
            ModContent.GetInstance<RoundManager>();

        RoundManager.AdminAction? action =
            manager.CurrentPhase switch
            {
                RoundManager.RoundPhase.WaitingForPlayers =>
                    RoundManager.AdminAction.StartVoting,
                RoundManager.RoundPhase.VotingOrEndScreen =>
                    RoundManager.AdminAction.EndVoting,
                RoundManager.RoundPhase.FreezeCountdown =>
                    RoundManager.AdminAction.StartRound,
                RoundManager.RoundPhase.Playing =>
                    RoundManager.AdminAction.EndRound,
                _ => null
            };

        if (action.HasValue)
            RoundManager.RequestAdminAction(action.Value);
    }

    private static bool PressedWithControl(
        Keys key,
        ref bool released)
    {
        if (Main.keyState.IsKeyUp(key))
        {
            released = true;
            return false;
        }

        if (!released || !Main.keyState.IsKeyDown(key))
            return false;

        released = false;

        bool control =
            Main.keyState.IsKeyDown(Keys.LeftControl) ||
            Main.keyState.IsKeyDown(Keys.RightControl);

        bool shift =
            Main.keyState.IsKeyDown(Keys.LeftShift) ||
            Main.keyState.IsKeyDown(Keys.RightShift);

        bool alt =
            Main.keyState.IsKeyDown(Keys.LeftAlt) ||
            Main.keyState.IsKeyDown(Keys.RightAlt);

        return control && !shift && !alt;
    }
}
#endif
