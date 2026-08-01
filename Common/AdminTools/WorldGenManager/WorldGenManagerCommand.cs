using System;
using System.Linq;
using Terraria.ID;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

internal sealed class WorldGenManagerCommand : ModCommand
{
    public override CommandType Type => CommandType.Chat | CommandType.Console;
    public override string Command => "worldgen";
    public override string Usage => "/worldgen <ui|status|list [page]|run <pass name>>";
    public override string Description => "Runs a vanilla world-generation pass in the loaded world.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        WorldGenPassRunner runner = ModContent.GetInstance<WorldGenPassRunner>();
        if (args.Length == 0)
        {
            caller.Reply(Usage, Color.LightGray);
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "ui":
            case "open":
                if (!Main.dedServ)
                    ModContent.GetInstance<WorldGenManagerUISystem>().Toggle();
                else
                    caller.Reply("The World Gen Manager UI is available in-game; use status/list/run from the server console.", Color.OrangeRed);
                break;

            case "status":
                caller.Reply($"{runner.Status} | pass={runner.ActivePass} seed={runner.Seed} progress={runner.Progress:P0} elapsed={runner.Elapsed.TotalSeconds:F1}s", Color.LightBlue);
                break;

            case "list":
                PrintList(caller, runner, args);
                break;

            case "run":
                if (args.Length < 2)
                {
                    caller.Reply("Usage: /worldgen run <exact pass name>", Color.OrangeRed);
                    return;
                }
                bool confirmed = args[^1].Equals("confirm", StringComparison.OrdinalIgnoreCase);
                string name = string.Join(' ', args.Skip(1).Take(args.Length - 1 - (confirmed ? 1 : 0)));
                if (WorldGenPassRunner.IsDangerous(name) && !confirmed)
                {
                    caller.Reply($"'{name}' is untested or destructive. Run '/worldgen run {name} confirm' to continue.", Color.OrangeRed);
                    return;
                }
                if (!WorldGenManagerNetHandler.RequestRunPasses([name], out string error))
                    caller.Reply(error, Color.OrangeRed);
                break;

            default:
                caller.Reply(Usage, Color.LightGray);
                break;
        }
    }

    private static void PrintList(CommandCaller caller, WorldGenPassRunner runner, string[] args)
    {
        const int pageSize = 12;
        int pages = Math.Max(1, (int)Math.Ceiling(runner.PassNames.Count / (double)pageSize));
        int page = args.Length > 1 && int.TryParse(args[1], out int parsed) ? Math.Clamp(parsed, 1, pages) : 1;
        string passes = string.Join(", ", runner.PassNames.Skip((page - 1) * pageSize).Take(pageSize));
        caller.Reply($"Vanilla passes {page}/{pages}: {passes}", Color.LightGray);
    }
}
