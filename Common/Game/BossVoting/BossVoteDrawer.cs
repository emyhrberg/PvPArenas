using PvPArenas.Common.AdminTools.UI;
using PvPArenas.Core;
using PvPArenas.Core.Configs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.ID;
using PvPArenas.Common.Game.LoadoutSelector;
using PvPArenas.Common.UI;

namespace PvPArenas.Common.Game.BossVoting;

/// <summary>Draws the intermission boss vote panel above the end screen.</summary>
internal static class BossVoteDrawer
{
    private const int DesignWidth = 520, HeaderHeight = 82, RowHeight = 46, RowGap = 3, BottomPadding = 8;
    private static readonly Color PanelFill = new(45, 61, 132), PanelEdge = new(70, 89, 165), RowFill = new(30, 43, 98), RowHover = new(68, 86, 158);
    private static readonly Color DarkEdge = new(6, 12, 38), Yellow = new(246, 216, 72), HoverEdge = new(244, 209, 74), Selected = new(104, 222, 72);
    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;
    private static UIEntranceAnimation entrance;
    private static float animAlpha = 1f;

    public static void Draw(int top = 120)
    {
        List<(int PresetIndex, BossFightPreset Preset)> presets = BossVoteSystem.VotablePresets();
        if (presets.Count == 0) return;
        BossVoteSystem voteSystem = ModContent.GetInstance<BossVoteSystem>();
        RoundManager manager = ModContent.GetInstance<RoundManager>();

        entrance.Advance();
        animAlpha = entrance.Alpha;
        top -= entrance.SlideOffset;

        int designHeight = HeaderHeight + presets.Count * RowHeight + Math.Max(0, presets.Count - 1) * RowGap + BottomPadding;
        float scale = Math.Min(1f, Math.Min((Main.screenWidth - 12f) / DesignWidth, (Main.screenHeight - top - 2f) / designHeight));
        int S(float value) => Math.Max(1, (int)MathF.Round(value * scale));

        int panelWidth = S(DesignWidth);
        Rectangle panel = new((Main.screenWidth - panelWidth) / 2, top, panelWidth, S(designHeight));

        // Main Background panel
        DrawPanel(panel, PanelFill, PanelEdge, S(9));

        Utils.DrawBorderStringBig(Main.spriteBatch, "Boss Vote",
            new Vector2(panel.Center.X, panel.Y + S(4)), Yellow * animAlpha, .66f * scale, .5f, 0f);
        Text("Choose the next boss - majority wins!", new Vector2(panel.Center.X, panel.Y + S(38)),
            Color.White, .78f * scale, panel.Width - S(28));

        Rectangle track = new(panel.X + S(14), panel.Y + S(58), panel.Width - S(28), S(20));
        int votingSeconds = Math.Max(1, ModContent.GetInstance<ServerConfig>().VotingDurationSeconds);
        float progress = Math.Clamp(manager.RemainingTicks / (votingSeconds * 60f), 0f, 1f);
        DrawProgressBar(track, progress);

        Point mouse = new(Main.mouseX, Main.mouseY);
        if (panel.Contains(mouse)) Main.LocalPlayer.mouseInterface = true;
        int localVote = voteSystem.LocalVote;
        for (int i = 0; i < presets.Count; i++)
        {
            Rectangle row = new(panel.X + S(12), panel.Y + S(HeaderHeight + i * (RowHeight + RowGap)),
                panel.Width - S(24), S(RowHeight));
            bool hover = row.Contains(mouse), selected = localVote == i;

            // Main preset panel
            DrawPanel(row, hover && !selected ? RowHover : RowFill, selected ? Selected : hover ? HoverEdge : DarkEdge, S(12));
            if (hover)
            {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) BossVoteSystem.RequestVote(i);
            }

            Rectangle icon = new(row.X + S(8), row.Y + S(5), S(36), S(36));
            //DrawPanel(icon, new Color(34, 49, 111), DarkEdge, S(5));
            DrawBossHead(presets[i].Preset.Boss?.Type ?? 0, icon, animAlpha);
            //DrawVoteState(icon, selected, hover, scale);

            Color nameColor = selected ? new Color(206, 255, 142) : hover ? Yellow : Color.White;
            Text(PresetName(presets[i].Preset), new Vector2(row.X + S(54), row.Y + S(12)),
                nameColor, .82f * scale, S(220), 0f);

            Rectangle counter = new(row.Right - S(42), row.Y + S(6), S(34), S(34));
            //DrawPanel(counter, new Color(6, 11, 35), DarkEdge, S(5));
            int count = voteSystem.VoteCount(i);
            Text(count.ToString(), new Vector2(counter.Center.X, counter.Y + S(7)),
                count > 0 ? Yellow : Color.Gray, .8f * scale, counter.Width - S(8));
            DrawVoters(row, counter, voteSystem.VotersFor(i), mouse, scale, S);
        }
    }

    private static string PresetName(BossFightPreset preset) =>
        preset == null ? "Boss" : preset.IsSandbox() ? "Sandbox" : preset.Boss?.DisplayName ?? "Boss";

    private static void DrawVoters(Rectangle row, Rectangle counter, IReadOnlyList<byte> voters, Point mouse, float scale, Func<float, int> s)
    {
        if (voters.Count == 0) return;
        int size = s(26), right = counter.X - s(6) - size, minimum = row.X + s(245);
        float step = voters.Count == 1 ? 0f : Math.Min(s(29),
            Math.Max(0, right - minimum) / (float)(voters.Count - 1));
        float start = right - step * (voters.Count - 1);
        for (int i = 0; i < voters.Count; i++)
        {
            int id = voters[i];
            if (id < 0 || id >= Main.maxPlayers) continue;
            Player player = Main.player[id];
            if (player?.active != true) continue;
            Rectangle tile = new((int)MathF.Round(start + step * i) - 6, row.Y + s(10), size, size);
            Color team = player.team > 0 && player.team < Main.teamColor.Length ? Main.teamColor[player.team] : Color.Gray;
            //DrawPanel(tile, Color.Lerp(player.shirtColor, team, .25f), DarkEdge, s(5));
            Main.MapPlayerRenderer.DrawPlayerHead(Main.Camera, player,
                tile.Center.ToVector2() - new Vector2(2f, 2f), animAlpha,
                .6f * size / 26f, team * animAlpha);
            if (tile.Contains(mouse)) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText(player.name); }
        }
    }

    private static void DrawProgressBar(Rectangle track, float progress)
    {
        UISlider.DrawBar(Main.spriteBatch, PvPFramework.Core.Utilities.Ass.Slider.Value, track, new Color(5, 10, 35) * animAlpha);
        UISlider.DrawBar(Main.spriteBatch, PvPFramework.Core.Utilities.Ass.SliderHighlight.Value, track, DarkEdge * animAlpha);
        Rectangle inner = track; inner.Inflate(-4, -4);
        int width = (int)MathF.Round(inner.Width * progress); if (width <= 0) return;
        Rectangle fill = new(inner.X, inner.Y, Math.Min(inner.Width, Math.Max(12, width)), inner.Height);
        UISlider.DrawBar(Main.spriteBatch, PvPFramework.Core.Utilities.Ass.Slider.Value, fill, Yellow * animAlpha);
        UISlider.DrawBar(Main.spriteBatch, PvPFramework.Core.Utilities.Ass.SliderHighlight.Value, fill, new Color(255, 239, 145) * animAlpha);
    }

    internal static void DrawBossHead(int type, Rectangle box, float opacity = 1f)
    {
        int head = type >= 0 && type < NPCID.Sets.BossHeadTextures.Length
            ? NPCID.Sets.BossHeadTextures[type]
            : -1;
        if (head >= 0 && head < TextureAssets.NpcHeadBoss.Length)
        {
            Texture2D texture = TextureAssets.NpcHeadBoss[head].Value;
            float scale = Math.Min((box.Width - 8f) / texture.Width, (box.Height - 8f) / texture.Height);
            Main.spriteBatch.Draw(texture, box.Center.ToVector2(), null, Color.White * opacity, 0f,
                texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            return;
        }

        if (type <= 0 || type >= TextureAssets.Npc.Length)
            return;

        Main.instance.LoadNPC(type);
        Texture2D npc = TextureAssets.Npc[type].Value;
        Rectangle source = new(0, 0, npc.Width, npc.Height / Math.Max(1, Main.npcFrameCount[type]));
        float fallbackScale = Math.Min((box.Width - 8f) / source.Width, (box.Height - 8f) / source.Height);
        Main.spriteBatch.Draw(npc, box.Center.ToVector2(), source, Color.White * opacity, 0f,
            source.Size() / 2f, fallbackScale, SpriteEffects.None, 0f);
    }

    private static void DrawVoteState(Rectangle icon, bool selected, bool hover, float scale)
    {
        Texture2D texture = (selected ? hover ? Ass.IconCheckOnHover : Ass.IconCheckOn : hover ? Ass.IconCheckOffHover : Ass.IconCheckOff).Value;
        float drawScale = Math.Max(.75f, scale);
        Main.spriteBatch.Draw(texture, new Vector2(icon.Right - texture.Width * drawScale, icon.Bottom - texture.Height * drawScale), null, Color.White * animAlpha, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
    }

    private static void DrawPanel(Rectangle rectangle, Color fill, Color edge, int corner)
    {
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBackground, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, corner, corner, corner, corner, fill * animAlpha);
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBorder, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, corner, corner, corner, corner, edge * animAlpha);
    }

    internal static void Text(string value, Vector2 position, Color color, float scale, float maxWidth, float anchor = .5f)
    {
        float width = FontAssets.MouseText.Value.MeasureString(value).X * scale;
        if (width > maxWidth) scale *= maxWidth / width;
        Utils.DrawBorderString(Main.spriteBatch, value, position, color * animAlpha, scale, anchor);
    }
}
