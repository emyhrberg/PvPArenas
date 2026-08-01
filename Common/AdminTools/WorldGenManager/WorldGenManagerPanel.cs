using Microsoft.Xna.Framework.Graphics;
using PvPArenas.Common.AdminTools.GameManager;
using PvPArenas.Common.AdminTools.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

internal sealed class WorldGenManagerPanel : UIDraggablePanel
{
    private enum Section
    {
        Passes,
        World,
        Visuals,
        Debug
    }

    private readonly UIList passList;
    private readonly UIElement pageHost;
    private readonly Dictionary<Section, UIElement> pages = [];
    private readonly HashSet<string> selectedPasses = new(StringComparer.OrdinalIgnoreCase);
    private Section activeSection;
    private string armedSelection;
    private WorldClearAction? armedAction;
    private uint armedUntil;

    protected override float MinResizeW => 980f;
    protected override float MinResizeH => 650f;
    protected override float MaxResizeW => 1600f;
    protected override float MaxResizeH => 1050f;

    internal WorldGenManagerPanel() : base("World Gen Manager")
    {
        Width.Set(1280f, 0f);
        Height.Set(840f, 0f);
        HAlign = .5f;
        Top.Set(35f, 0f);
        Content.SetPadding(12f);

        WorldGenSummary summary = new()
        {
            Width = { Percent = 1f },
            Height = { Pixels = 112f }
        };
        Content.Append(summary);

        UIElement tabs = new()
        {
            Top = { Pixels = 122f },
            Width = { Percent = 1f },
            Height = { Pixels = 44f }
        };
        Content.Append(tabs);

        AddTab(tabs, Section.Passes, "GENERATION PASSES", "Choose one or more numbered vanilla passes");
        AddTab(tabs, Section.World, "WORLD CLEANUP", "Clear tiles, walls, liquids, wiring, paint, or everything");
        AddTab(tabs, Section.Visuals, "VISUALS", "Locally hide scenery while inspecting the world");
        AddTab(tabs, Section.Debug, "DEBUG INFO", "Compact live world data arranged in columns");

        pageHost = new UIElement
        {
            Top = { Pixels = 176f },
            Width = { Percent = 1f },
            Height = { Pixels = -176f, Percent = 1f }
        };
        Content.Append(pageHost);

        pages[Section.Passes] = BuildPassesPage(out passList);
        pages[Section.World] = BuildWorldPage();
        pages[Section.Visuals] = BuildVisualsPage();
        pages[Section.Debug] = new WorldGenDebugView
        {
            Width = { Percent = 1f },
            Height = { Percent = 1f }
        };

        RebuildList();
        ShowSection(Section.Passes);
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<WorldGenManagerUISystem>().Close();

    protected override void OnRefreshPanelLeftClick()
    {
        RebuildList();
        WorldGenManagerNetHandler.RequestStatus();
    }

    private WorldGenPassRunner Runner => ModContent.GetInstance<WorldGenPassRunner>();

    private UIElement BuildPassesPage(out UIList list)
    {
        UIElement page = FullPage();
        page.Append(SectionTitle("Generation passes", "Select any number of passes. They always run top-to-bottom in the numbered vanilla order."));

        page.Append(CommandButton(() => "Tested only", () => "Select the passes explicitly tested for live-world use",
            () => !Runner.Busy, () => false, SelectTested, 0f, 58f, .16f));
        page.Append(CommandButton(() => "Select all", () => "Select every vanilla pass; risky passes still require confirmation",
            () => !Runner.Busy, () => false, SelectAll, .17f, 58f, .16f));
        page.Append(CommandButton(() => "Clear selection", () => "Uncheck every generation pass",
            () => !Runner.Busy && selectedPasses.Count > 0, () => false, ClearSelection, .34f, 58f, .16f));

        ArenaGameCommandButton run = CommandButton(RunLabel, RunTooltip,
            () => !Runner.Busy && selectedPasses.Count > 0,
            () => SelectedInOrder().Any(WorldGenPassRunner.IsDangerous),
            RunSelected, .52f, 58f, .48f);
        page.Append(run);

        UIPanel listPanel = new()
        {
            Top = { Pixels = 108f },
            Width = { Percent = 1f },
            Height = { Pixels = -108f, Percent = 1f },
            BackgroundColor = new Color(16, 22, 52) * .96f,
            BorderColor = new Color(78, 104, 190) * .8f
        };
        listPanel.SetPadding(8f);
        page.Append(listPanel);

        list = new UIList
        {
            Width = { Pixels = -28f, Percent = 1f },
            Height = { Percent = 1f },
            ListPadding = 5f
        };
        UIScrollbar scrollbar = new()
        {
            Left = { Pixels = -20f, Percent = 1f },
            Width = { Pixels = 20f },
            Height = { Percent = 1f }
        };
        list.SetScrollbar(scrollbar);
        listPanel.Append(list);
        listPanel.Append(scrollbar);
        return page;
    }

    private UIElement BuildWorldPage()
    {
        UIElement page = FullPage();
        page.Append(SectionTitle("World cleanup", "Every action creates a backup, pauses gameplay, saves, and resends all world sections to multiplayer clients."));

        WorldClearAction[] actions = Enum.GetValues<WorldClearAction>();
        for (int i = 0; i < actions.Length; i++)
        {
            WorldClearAction action = actions[i];
            int column = i % 2;
            int row = i / 2;
            page.Append(new WorldCleanupCard(action, () => IsActionArmed(action), () => !Runner.Busy,
                () => RequestClear(action))
            {
                Left = { Pixels = column == 0 ? 0f : 8f, Percent = column * .5f },
                Top = { Pixels = 64f + row * 112f },
                Width = { Pixels = -8f, Percent = .5f },
                Height = { Pixels = 102f }
            });
        }
        return page;
    }

    private static UIElement BuildVisualsPage()
    {
        UIElement page = FullPage();
        page.Append(SectionTitle("Visual inspection", "These switches affect only this client and do not change or save the world."));

        page.Append(CommandButton(() => "Show all scenery", () => "Enable every scenery layer",
            () => true, () => false, () => WorldGenVisualSystem.SetAll(true), 0f, 58f, .24f));
        page.Append(CommandButton(() => "Hide all scenery", () => "Disable every listed scenery layer",
            () => true, () => false, () => WorldGenVisualSystem.SetAll(false), .25f, 58f, .24f));

        (WorldVisualLayer Layer, string Label, string Description)[] layers =
        [
            (WorldVisualLayer.Background, "Background", "Surface and cavern background artwork"),
            (WorldVisualLayer.Clouds, "Clouds", "Surface cloud sprites"),
            (WorldVisualLayer.Sky, "Sky color", "The solid sky-color draw behind scenery"),
            (WorldVisualLayer.SunAndMoon, "Sun and moon", "Celestial bodies and their vanilla draw pass"),
            (WorldVisualLayer.Stars, "Stars", "The star field drawn behind the world")
        ];

        for (int i = 0; i < layers.Length; i++)
        {
            int column = i % 2;
            int row = i / 2;
            var item = layers[i];
            page.Append(new WorldVisualToggle(item.Layer, item.Label, item.Description)
            {
                Left = { Pixels = column == 0 ? 0f : 8f, Percent = column * .5f },
                Top = { Pixels = 112f + row * 86f },
                Width = { Pixels = -8f, Percent = .5f },
                Height = { Pixels = 76f }
            });
        }
        return page;
    }

    private void AddTab(UIElement parent, Section section, string label, string tooltip)
    {
        int index = (int)section;
        parent.Append(new WorldGenTabButton(label, tooltip, () => activeSection == section, () => ShowSection(section))
        {
            Left = { Pixels = index == 0 ? 0f : 5f, Percent = index * .25f },
            Width = { Pixels = -5f, Percent = .25f },
            Height = { Percent = 1f }
        });
    }

    private void ShowSection(Section section)
    {
        activeSection = section;
        pageHost.RemoveAllChildren();
        pageHost.Append(pages[section]);
        if (section == Section.Debug)
            ModContent.GetInstance<WorldGenDebugStats>().RestartScan();
    }

    private void RebuildList()
    {
        passList.Clear();
        IReadOnlyList<string> names = Runner.PassNames;
        selectedPasses.RemoveWhere(pass => !names.Contains(pass, StringComparer.OrdinalIgnoreCase));
        for (int index = 0; index < names.Count; index++)
        {
            string name = names[index];
            passList.Add(new WorldGenPassRow(index + 1, name,
                () => selectedPasses.Contains(name),
                () => SelectedRunIndex(name),
                () => TogglePass(name)));
        }
    }

    private void TogglePass(string name)
    {
        if (!selectedPasses.Add(name))
            selectedPasses.Remove(name);
        Disarm();
    }

    private void SelectTested()
    {
        selectedPasses.Clear();
        foreach (string pass in Runner.PassNames.Where(WorldGenPassRunner.IsTested))
            selectedPasses.Add(pass);
        Disarm();
    }

    private void SelectAll()
    {
        selectedPasses.Clear();
        foreach (string pass in Runner.PassNames)
            selectedPasses.Add(pass);
        Disarm();
    }

    private void ClearSelection()
    {
        selectedPasses.Clear();
        Disarm();
    }

    private string[] SelectedInOrder() => Runner.PassNames.Where(selectedPasses.Contains).ToArray();

    private int SelectedRunIndex(string name)
    {
        int index = 0;
        foreach (string pass in Runner.PassNames)
        {
            if (!selectedPasses.Contains(pass))
                continue;
            index++;
            if (pass.Equals(name, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return 0;
    }

    private void RunSelected()
    {
        string[] passes = SelectedInOrder();
        string signature = string.Join('\n', passes);
        bool dangerous = passes.Any(WorldGenPassRunner.IsDangerous);
        if (dangerous && !(armedSelection == signature && Main.GameUpdateCount <= armedUntil))
        {
            armedSelection = signature;
            armedAction = null;
            armedUntil = Main.GameUpdateCount + 300;
            Main.NewText($"{passes.Count(WorldGenPassRunner.IsDangerous)} risky pass(es) selected. Click CONFIRM within 5 seconds.", Color.OrangeRed);
            return;
        }

        Disarm();
        if (!WorldGenManagerNetHandler.RequestRunPasses(passes, out string error))
            Main.NewText(error, Color.OrangeRed);
    }

    private void RequestClear(WorldClearAction action)
    {
        if (!IsActionArmed(action))
        {
            armedAction = action;
            armedSelection = null;
            armedUntil = Main.GameUpdateCount + 300;
            Main.NewText($"{WorldGenPassRunner.ClearActionName(action)} is destructive. Click the same card within 5 seconds to confirm.", Color.OrangeRed);
            return;
        }

        Disarm();
        if (!WorldGenManagerNetHandler.RequestClear(action, out string error))
            Main.NewText(error, Color.OrangeRed);
    }

    private string RunLabel()
    {
        string[] passes = SelectedInOrder();
        if (passes.Length == 0)
            return "Select generation passes";
        string signature = string.Join('\n', passes);
        return armedSelection == signature && Main.GameUpdateCount <= armedUntil
            ? $"CONFIRM {passes.Length} PASS{(passes.Length == 1 ? "" : "ES")}"
            : $"RUN {passes.Length} PASS{(passes.Length == 1 ? "" : "ES")} IN ORDER";
    }

    private string RunTooltip()
    {
        if (Runner.Busy)
            return Runner.Status;
        string[] passes = SelectedInOrder();
        if (passes.Length == 0)
            return "Check one or more numbered passes below";
        int risky = passes.Count(WorldGenPassRunner.IsDangerous);
        return risky > 0
            ? $"Runs top-to-bottom with one backup. {risky} risky pass(es) require a second click."
            : "Runs the selected tested passes top-to-bottom with one automatic backup.";
    }

    private bool IsActionArmed(WorldClearAction action) => armedAction == action && Main.GameUpdateCount <= armedUntil;

    private void Disarm()
    {
        armedSelection = null;
        armedAction = null;
    }

    private static UIElement FullPage() => new()
    {
        Width = { Percent = 1f },
        Height = { Percent = 1f }
    };

    private static UIElement SectionTitle(string title, string subtitle)
    {
        UIElement header = new()
        {
            Width = { Percent = 1f },
            Height = { Pixels = 54f }
        };
        header.Append(new UIText(title, .95f, true)
        {
            TextColor = new Color(255, 220, 135)
        });
        header.Append(new UIText(subtitle, .68f)
        {
            Top = { Pixels = 28f },
            TextColor = new Color(174, 216, 226)
        });
        return header;
    }

    private static ArenaGameCommandButton CommandButton(Func<string> label, Func<string> tooltip,
        Func<bool> enabled, Func<bool> danger, Action action, float leftPercent, float top, float widthPercent) => new(
            label, tooltip, enabled, danger, action)
        {
            Left = { Percent = leftPercent },
            Top = { Pixels = top },
            Width = { Pixels = -5f, Percent = widthPercent },
            Height = { Pixels = 42f }
        };
}

internal sealed class WorldGenSummary : UIPanel
{
    internal WorldGenSummary()
    {
        SetPadding(0f);
        BackgroundColor = new Color(20, 27, 62) * .95f;
        BorderColor = new Color(78, 104, 190) * .8f;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        Rectangle box = GetDimensions().ToRectangle();
        WorldGenPassRunner runner = ModContent.GetInstance<WorldGenPassRunner>();
        Draw(spriteBatch, runner.Status, box.X + 14, box.Y + 10, Color.White, .88f, box.Width - 28);
        Draw(spriteBatch, $"Job: {(string.IsNullOrWhiteSpace(runner.ActivePass) ? "None" : runner.ActivePass)}   |   Seed: {(runner.Seed == 0 ? "—" : runner.Seed)}",
            box.X + 14, box.Y + 40, Color.LightGray, .70f, box.Width - 28);
        Draw(spriteBatch, $"Progress: {runner.Progress:P0}   |   Elapsed: {runner.Elapsed.TotalSeconds:F1}s   |   Mode: {(Main.netMode == NetmodeID.MultiplayerClient ? "Server-authoritative multiplayer" : Main.netMode == NetmodeID.Server ? "Dedicated server" : "Singleplayer")}",
            box.X + 14, box.Y + 65, Color.LightBlue, .68f, box.Width - 28);
        string backup = runner.BackupAvailable ? "Backup ready for this job" : "A full world backup is created before every generation or cleanup job";
        Draw(spriteBatch, backup, box.X + 14, box.Y + 89, new Color(174, 216, 226), .60f, box.Width - 28);
    }

    private static void Draw(SpriteBatch batch, string text, float x, float y, Color color, float scale, float maxWidth)
    {
        float width = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(batch, text, new Vector2(x, y), color, scale);
    }
}

internal sealed class WorldGenTabButton : UIPanel
{
    private readonly string label;
    private readonly string tooltip;
    private readonly Func<bool> selected;
    private readonly Action action;

    internal WorldGenTabButton(string label, string tooltip, Func<bool> selected, Action action)
    {
        this.label = label;
        this.tooltip = tooltip;
        this.selected = selected;
        this.action = action;
        SetPadding(0f);
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        SoundEngine.PlaySound(SoundID.MenuTick);
        action();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsMouseHovering)
            return;
        Main.LocalPlayer.mouseInterface = true;
        Main.instance.MouseText(tooltip);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        bool active = selected();
        BackgroundColor = active ? new Color(74, 98, 180) : IsMouseHovering ? new Color(52, 69, 128) : new Color(32, 43, 83);
        BorderColor = IsMouseHovering ? Color.Yellow : active ? new Color(130, 164, 255) : Color.Black;
        base.DrawSelf(spriteBatch);
        Rectangle box = GetDimensions().ToRectangle();
        DrawCentered(spriteBatch, label, box, active ? Color.White : new Color(190, 205, 235), .75f);
    }

    internal static void DrawCentered(SpriteBatch spriteBatch, string text, Rectangle box, Color color, float scale)
    {
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
        if (size.X > box.Width - 12f)
        {
            scale *= (box.Width - 12f) / size.X;
            size = FontAssets.MouseText.Value.MeasureString(text) * scale;
        }
        Utils.DrawBorderString(spriteBatch, text, new Vector2(box.Center.X, box.Center.Y - size.Y * .5f + 2f), color, scale, .5f);
    }
}

internal sealed class WorldGenPassRow : UIElement
{
    private readonly int order;
    private readonly string name;
    private readonly Func<bool> selected;
    private readonly Func<int> runIndex;
    private readonly Action action;

    internal WorldGenPassRow(int order, string name, Func<bool> selected, Func<int> runIndex, Action action)
    {
        this.order = order;
        this.name = name;
        this.selected = selected;
        this.runIndex = runIndex;
        this.action = action;
        Width.Set(0f, 1f);
        Height.Set(44f, 0f);
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        SoundEngine.PlaySound(SoundID.MenuTick);
        action();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsMouseHovering)
            return;
        Main.LocalPlayer.mouseInterface = true;
        string safety = WorldGenPassRunner.IsDangerous(name)
            ? "Risky: this vanilla pass is untested live or can rewrite a large part of the world."
            : "Tested for live-world use.";
        string extra = name == "Floating Island Houses" ? " Floating Islands is automatically added first when needed." : "";
        Main.instance.MouseText($"Vanilla order #{order:00}. Selected passes execute in this top-to-bottom order. {safety}{extra}");
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Rectangle box = GetDimensions().ToRectangle();
        bool active = selected();
        bool danger = WorldGenPassRunner.IsDangerous(name);
        Color fill = danger
            ? active ? new Color(135, 46, 58) : IsMouseHovering ? new Color(103, 39, 50) : new Color(76, 31, 42)
            : active ? new Color(65, 91, 169) : IsMouseHovering ? new Color(48, 65, 120) : new Color(34, 45, 86);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, box, fill);

        Rectangle check = new(box.X + 10, box.Y + 10, 24, 24);
        DrawOutline(spriteBatch, check, active ? new Color(145, 190, 255) : new Color(112, 132, 175));
        if (active)
        {
            Rectangle mark = new(check.X + 5, check.Y + 5, check.Width - 10, check.Height - 10);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, mark, Color.White);
        }

        Utils.DrawBorderString(spriteBatch, $"{order:00}", new Vector2(box.X + 45, box.Y + 11), new Color(174, 216, 226), .70f);
        DrawFit(spriteBatch, name, new Vector2(box.X + 82, box.Y + 10), Color.White, .78f, Math.Max(50f, box.Width - 265f));

        string badge = danger ? "RISK" : "TESTED";
        Color badgeColor = danger ? new Color(255, 165, 120) : new Color(145, 230, 175);
        DrawFit(spriteBatch, badge, new Vector2(box.Right - 158, box.Y + 12), badgeColor, .58f, 66f);
        int execution = runIndex();
        if (execution > 0)
            DrawFit(spriteBatch, $"RUN {execution}", new Vector2(box.Right - 82, box.Y + 12), Color.LightBlue, .60f, 70f);
    }

    internal static void DrawOutline(SpriteBatch batch, Rectangle rect, Color color)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        batch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), color);
        batch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), color);
        batch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), color);
        batch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), color);
    }

    private static void DrawFit(SpriteBatch batch, string text, Vector2 position, Color color, float scale, float maxWidth)
    {
        float width = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(batch, text, position, color, scale);
    }
}

internal sealed class WorldCleanupCard : UIPanel
{
    private readonly WorldClearAction action;
    private readonly Func<bool> armed;
    private readonly Func<bool> enabled;
    private readonly Action clicked;

    internal WorldCleanupCard(WorldClearAction action, Func<bool> armed, Func<bool> enabled, Action clicked)
    {
        this.action = action;
        this.armed = armed;
        this.enabled = enabled;
        this.clicked = clicked;
        SetPadding(0f);
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        if (!enabled())
            return;
        SoundEngine.PlaySound(SoundID.MenuTick);
        clicked();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsMouseHovering)
            return;
        Main.LocalPlayer.mouseInterface = true;
        Main.instance.MouseText(Description(action, true));
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        bool available = enabled();
        bool confirm = armed();
        BackgroundColor = !available ? new Color(45, 45, 55) * .72f
            : confirm ? new Color(165, 42, 56)
            : IsMouseHovering ? new Color(110, 42, 54) : new Color(76, 31, 42);
        BorderColor = IsMouseHovering && available ? Color.Yellow : confirm ? Color.OrangeRed : Color.Black;
        base.DrawSelf(spriteBatch);

        Rectangle box = GetDimensions().ToRectangle();
        string title = confirm ? $"CONFIRM: {WorldGenPassRunner.ClearActionName(action)}" : WorldGenPassRunner.ClearActionName(action);
        DrawFit(spriteBatch, title, new Vector2(box.X + 14, box.Y + 12), available ? Color.White : Color.Gray, .82f, box.Width - 28f);
        DrawFit(spriteBatch, Description(action, false), new Vector2(box.X + 14, box.Y + 47), new Color(224, 190, 194), .63f, box.Width - 28f);
        DrawFit(spriteBatch, "BACKUP + TWO-CLICK CONFIRM", new Vector2(box.X + 14, box.Bottom - 25), new Color(255, 174, 118), .53f, box.Width - 28f);
    }

    private static string Description(WorldClearAction action, bool detailed) => action switch
    {
        WorldClearAction.Tiles => detailed ? "Removes every block and placed object. Walls, liquids, and wiring remain; chest, sign, and tile-entity registrations are cleaned up." : "Blocks + placed objects; keeps walls/liquid/wires",
        WorldClearAction.Walls => detailed ? "Removes every background wall while leaving blocks, liquids, and wiring unchanged." : "Background walls only",
        WorldClearAction.Liquids => detailed ? "Drains water, lava, honey, and shimmer everywhere and resets Terraria's liquid queues." : "Water, lava, honey + shimmer",
        WorldClearAction.Wiring => detailed ? "Removes all four wire colors, actuators, and actuated states without changing terrain." : "All wire colors + actuators",
        WorldClearAction.PaintAndCoatings => detailed ? "Removes tile and wall paint, echo coating, and illuminant coating across the world." : "Tile/wall paint + coatings",
        WorldClearAction.Everything => detailed ? "Resets all tilemap data: blocks, walls, liquids, wiring, slopes, paint, and coatings, then removes tile-bound entities." : "Complete tilemap reset",
        _ => "World cleanup"
    };

    private static void DrawFit(SpriteBatch batch, string text, Vector2 position, Color color, float scale, float maxWidth)
    {
        float width = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(batch, text, position, color, scale);
    }
}

internal sealed class WorldVisualToggle : UIPanel
{
    private readonly WorldVisualLayer layer;
    private readonly string label;
    private readonly string description;

    internal WorldVisualToggle(WorldVisualLayer layer, string label, string description)
    {
        this.layer = layer;
        this.label = label;
        this.description = description;
        SetPadding(0f);
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        WorldGenVisualSystem.SetShown(layer, !WorldGenVisualSystem.IsShown(layer));
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsMouseHovering)
            return;
        Main.LocalPlayer.mouseInterface = true;
        Main.instance.MouseText($"{description}. Local visual setting; no world data is changed.");
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        bool shown = WorldGenVisualSystem.IsShown(layer);
        BackgroundColor = IsMouseHovering ? new Color(50, 68, 127) : new Color(30, 41, 79);
        BorderColor = IsMouseHovering ? Color.Yellow : new Color(78, 104, 190) * .8f;
        base.DrawSelf(spriteBatch);
        Rectangle box = GetDimensions().ToRectangle();
        Rectangle toggle = new(box.X + 14, box.Y + 18, 40, 40);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, toggle, shown ? new Color(66, 145, 102) : new Color(125, 48, 58));
        WorldGenPassRow.DrawOutline(spriteBatch, toggle, shown ? new Color(160, 245, 190) : new Color(255, 150, 150));
        WorldGenTabButton.DrawCentered(spriteBatch, shown ? "✓" : "×", toggle, Color.White, .85f);
        Utils.DrawBorderString(spriteBatch, label, new Vector2(box.X + 68, box.Y + 11), Color.White, .82f);
        Utils.DrawBorderString(spriteBatch, shown ? "VISIBLE" : "HIDDEN", new Vector2(box.X + 68, box.Y + 39),
            shown ? new Color(145, 230, 175) : new Color(255, 155, 155), .60f);
    }
}
