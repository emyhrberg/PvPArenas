using System.Collections.Generic;
using Terraria.UI;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

[Autoload(Side = ModSide.Client)]
internal sealed class WorldGenManagerUISystem : ModSystem
{
    private UserInterface ui;
    private UIState state;

    internal bool IsActive => ui?.CurrentState == state;
    internal void Toggle()
    {
        bool opening = !IsActive;
        ui?.SetState(opening ? state : null);
        if (opening)
            WorldGenManagerNetHandler.RequestStatus();
    }
    internal void Close() => ui?.SetState(null);

    public override void OnWorldLoad()
    {
        ui = new UserInterface();
        state = new UIState();
        state.Append(new WorldGenManagerPanel());
        ui.SetState(null);
    }

    public override void UpdateUI(GameTime gameTime) => ui?.Update(gameTime);

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index < 0)
            return;
        layers.Insert(index, new LegacyGameInterfaceLayer("Arenas: World Gen Manager", () =>
        {
            if (IsActive)
                ui.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
            return true;
        }, InterfaceScaleType.UI));
    }
}
