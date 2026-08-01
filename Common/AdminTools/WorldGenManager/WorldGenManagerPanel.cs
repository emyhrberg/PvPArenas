using PvPArenas.Common.AdminTools.GameManager;
using PvPArenas.Common.AdminTools.UI;
using System;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

internal sealed class WorldGenManagerPanel : UIDraggablePanel
{
    private readonly UIList passList;
    private string selectedPass;
    private string armedPass;
    private uint armedUntil;

    private const float LeftWidth = 300f;

    protected override float MinResizeW => 720f;
    protected override float MinResizeH => 500f;
    protected override float MaxResizeW => 1200f;
    protected override float MaxResizeH => 900f;

    internal WorldGenManagerPanel() : base("World Gen Manager")
    {
        Width.Set(920f, 0f);
        Height.Set(720f, 0f);
        HAlign = .5f;
        Top.Set(60f, 0f);
        Content.SetPadding(8f);

        WorldGenSummary summary = new()
        {
            Left = { Pixels = 0f },
            Width = { Pixels = LeftWidth },
            Height = { Pixels = 92f }
        };
        Content.Append(summary);

        ArenaGameCommandButton run = new(
            () => string.IsNullOrEmpty(selectedPass) ? "Select a pass" : IsArmed ? $"CONFIRM: {selectedPass}" : $"Run: {selectedPass}",
            RunTooltip,
            () => !Runner.Busy && !string.IsNullOrEmpty(selectedPass),
            () => WorldGenPassRunner.IsDangerous(selectedPass),
            RunSelected)
        {
            Left = { Pixels = 0f },
            Top = { Pixels = 101f },
            Width = { Pixels = LeftWidth },
            Height = { Pixels = 38f }
        };
        Content.Append(run);

        passList = new UIList
        {
            Left = { Pixels = 0f },
            Top = { Pixels = 149f },
            Width = { Pixels = LeftWidth - 24f },
            Height = { Pixels = -149f, Percent = 1f },
            ListPadding = 3f
        };
        UIScrollbar scrollbar = new()
        {
            Left = { Pixels = LeftWidth - 20f },
            Top = { Pixels = 149f },
            Width = { Pixels = 20f },
            Height = { Pixels = -149f, Percent = 1f }
        };
        passList.SetScrollbar(scrollbar);
        Content.Append(passList);
        Content.Append(scrollbar);

        WorldGenDebugView debug = new()
        {
            Left = { Pixels = LeftWidth + 12f },
            Top = { Pixels = 0f },
            Width = { Pixels = -(LeftWidth + 12f), Percent = 1f },
            Height = { Percent = 1f }
        };
        Content.Append(debug);

        RebuildList();
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<WorldGenManagerUISystem>().Close();
    protected override void OnRefreshPanelLeftClick() => RebuildList();

    private WorldGenPassRunner Runner => ModContent.GetInstance<WorldGenPassRunner>();

    private void RebuildList()
    {
        passList.Clear();
        foreach (string name in Runner.PassNames)
        {
            string rowName = name;
            passList.Add(new WorldGenPassRow(rowName, () => selectedPass, () =>
            {
                selectedPass = rowName;
                armedPass = null;
            }));
        }
        selectedPass ??= Runner.TryResolvePass("Life Crystals", out string priority)
            ? priority
            : Runner.PassNames.Count > 0 ? Runner.PassNames[0] : null;
    }

    private void RunSelected()
    {
        if (WorldGenPassRunner.IsDangerous(selectedPass) && !IsArmed)
        {
            armedPass = selectedPass;
            armedUntil = Main.GameUpdateCount + 300;
            Main.NewText($"'{selectedPass}' is untested or destructive. Click CONFIRM within 5 seconds to run it.", Color.OrangeRed);
            return;
        }
        armedPass = null;
        if (!Runner.TryRun(selectedPass, out string error))
            Main.NewText(error, Color.OrangeRed);
    }

    private string RunTooltip()
    {
        if (Runner.Busy)
            return Runner.Status;
        if (selectedPass == "Floating Island Houses")
            return "Creates new vanilla Floating Islands first, then builds their houses. A backup is created automatically.";
        return WorldGenPassRunner.IsDangerous(selectedPass)
            ? IsArmed
                ? "Click now to confirm. This pass can overwrite most or all of the world."
                : "Untested or destructive vanilla pass. Click once to arm it; a backup is created automatically."
            : "Run this vanilla pass with a new random seed. A backup is created automatically.";
    }

    private bool IsArmed => armedPass == selectedPass && Main.GameUpdateCount <= armedUntil;
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
        Draw(spriteBatch, runner.Status, box.X + 10, box.Y + 8, Color.White, .7f, box.Width - 20);
        Draw(spriteBatch, $"Pass: {runner.ActivePass}   Seed: {runner.Seed}", box.X + 10, box.Y + 31, Color.LightGray, .58f, box.Width - 20);
        Draw(spriteBatch, $"Progress: {runner.Progress:P0}   Elapsed: {runner.Elapsed.TotalSeconds:F1}s", box.X + 10, box.Y + 51, Color.LightBlue, .58f, box.Width - 20);
        string backup = string.IsNullOrWhiteSpace(runner.BackupPath) ? "Backup: created before each run" : $"Backup: {runner.BackupPath}";
        Draw(spriteBatch, backup, box.X + 10, box.Y + 70, new Color(174, 216, 226), .48f, box.Width - 20);
    }

    private static void Draw(SpriteBatch batch, string text, float x, float y, Color color, float scale, float maxWidth)
    {
        float width = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(batch, text, new Vector2(x, y), color, scale);
    }
}

internal sealed class WorldGenPassRow : UIElement
{
    private readonly string name;
    private readonly Func<string> selected;
    private readonly Action action;

    internal WorldGenPassRow(string name, Func<string> selected, Action action)
    {
        this.name = name;
        this.selected = selected;
        this.action = action;
        Width.Set(0f, 1f);
        Height.Set(32f, 0f);
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
        Main.instance.MouseText(WorldGenPassRunner.IsDangerous(name)
            ? "Destructive pass"
            : name == "Floating Island Houses" ? "Runs Floating Islands, then Floating Island Houses" : "Select pass");
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Rectangle box = GetDimensions().ToRectangle();
        bool active = name == selected();
        Color fill = WorldGenPassRunner.IsDangerous(name)
            ? active ? new Color(155, 45, 58) : new Color(95, 32, 42)
            : active ? new Color(70, 96, 176) : new Color(38, 50, 92);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, box, fill);
        Utils.DrawBorderString(spriteBatch, name, new Vector2(box.X + 9, box.Y + 7), Color.White, .66f);
    }
}
