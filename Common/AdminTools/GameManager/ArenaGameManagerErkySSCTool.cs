using System;
using Microsoft.Xna.Framework;

namespace PvPArenas.Common.AdminTools.GameManager;

[Autoload(Side = ModSide.Client)]
internal sealed class ArenaGameManagerErkySSCTool : ModSystem
{
    private const string GameOwner = "Arenas.GameManager";
    private const string WorldGenOwner = "Arenas.WorldGenManager";

    public override void PostSetupContent() => RegisterEntries();
    public override void OnWorldLoad() => RegisterEntries();

    public override void Unload()
    {
        if (!TryGetErkySSC(out Mod mod))
            return;
        Clear(mod, GameOwner);
        Clear(mod, WorldGenOwner);
    }

    private static void RegisterEntries()
    {
        if (!TryGetErkySSC(out Mod mod))
            return;

        // Arena Game Manager
        Add(mod, GameOwner, "arena_game_manager", "Arenas: Game Manager",
            "Start and end rounds and voting", 31,
            () => ModContent.GetInstance<ArenaGameManagerUISystem>().Toggle(),
            () => ModContent.GetInstance<ArenaGameManagerUISystem>().IsActive);

        Add(mod, WorldGenOwner, "world_gen_manager", "Arenas: World Gen Manager",
            "Generate, clean, inspect, and control the loaded world's visuals", 32,
            () => ModContent.GetInstance<WorldGenManager.WorldGenManagerUISystem>().Toggle(),
            () => ModContent.GetInstance<WorldGenManager.WorldGenManagerUISystem>().IsActive);
    }

    private static void Add(Mod mod, string owner, string key, string title, string tooltip, int order, Action toggle, Func<bool> active)
    {
        try
        {
            object result = mod.Call("RegisterAdminQuickbarEntry", owner, key, title, tooltip, Ass.IconArenas,
                toggle, new Func<string>(() => active() ? "Close" : "Open"), new Func<Color>(() => Color.White), true, order, "");
            if (result is not true)
                Log.Warn($"ErkySSC rejected admin quickbar registration. owner={owner}, id={key}");
        }
        catch (Exception exception)
        {
            Log.Warn($"Failed to register {title}: {exception.Message}");
        }
    }

    private static void Clear(Mod mod, string owner)
    {
        try
        {
            object result = mod.Call("ClearAdminQuickbarEntries", owner);
            if (result is not true)
                Log.Warn($"ErkySSC rejected admin quickbar cleanup. owner={owner}");
        }
        catch (Exception exception) { Log.Warn($"Failed to clear {owner}: {exception.Message}"); }
    }

    private static bool TryGetErkySSC(out Mod mod) =>
        ModLoader.TryGetMod("ErkySSC", out mod) || ModLoader.TryGetMod("ErkySsc", out mod);
}
