#if DEBUG
using Terraria;
using Terraria.ModLoader;

namespace PvPArenas.Core.Debug;

[Autoload(Side = ModSide.Client)]
internal sealed class DebugPlayer : ModPlayer
{
    private const string Banner =
        "--------- DEBUG KEYBINDS (PVPARENAS) -----------\n" +
        "Ctrl+Numpad1: Advance phase";

    public override void OnEnterWorld()
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer)
            return;

        Main.NewText(Banner, DebugKeybinds.MessageColor);
    }
}
#endif
