using Terraria.ModLoader.Config;

namespace PvPArenas.Common.Game.LoadoutSelector;

public sealed class ArenaLoadoutOption
{
    public string Name = "Loadout";

    [Expand(true)]
    public Loadout Loadout = new();

    public ArenaLoadoutOption()
    {
    }

    public ArenaLoadoutOption(
        string name,
        Loadout loadout)
    {
        Name = name;
        Loadout = loadout;
    }
}