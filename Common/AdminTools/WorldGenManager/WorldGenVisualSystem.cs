using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Linq;
using System.Reflection;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

internal enum WorldVisualLayer
{
    Background,
    Clouds,
    Sky,
    SunAndMoon,
    Stars
}

[Autoload(Side = ModSide.Client)]
internal sealed class WorldGenVisualSystem : ModSystem
{
    private static bool showBackground = true;
    private static bool showClouds = true;
    private static bool showSky = true;
    private static bool showSunAndMoon = true;
    private static bool showStars = true;

    private Hook drawBackgroundHook;
    private Hook drawCloudHook;

    internal static bool IsShown(WorldVisualLayer layer) => layer switch
    {
        WorldVisualLayer.Background => showBackground,
        WorldVisualLayer.Clouds => showClouds,
        WorldVisualLayer.Sky => showSky,
        WorldVisualLayer.SunAndMoon => showSunAndMoon,
        WorldVisualLayer.Stars => showStars,
        _ => true
    };

    internal static void SetShown(WorldVisualLayer layer, bool shown)
    {
        switch (layer)
        {
            case WorldVisualLayer.Background: showBackground = shown; break;
            case WorldVisualLayer.Clouds: showClouds = shown; break;
            case WorldVisualLayer.Sky: showSky = shown; break;
            case WorldVisualLayer.SunAndMoon: showSunAndMoon = shown; break;
            case WorldVisualLayer.Stars: showStars = shown; break;
        }
    }

    internal static void SetAll(bool shown)
    {
        showBackground = shown;
        showClouds = shown;
        showSky = shown;
        showSunAndMoon = shown;
        showStars = shown;
    }

    public override void Load()
    {
        MethodInfo drawBackground = typeof(Main).GetMethod("DrawBG", BindingFlags.Instance | BindingFlags.NonPublic);
        if (drawBackground != null)
            drawBackgroundHook = new Hook(drawBackground, new Action<Action<Main>, Main>(DrawBackgroundDetour));
        else
            Log.Warn("World Gen Manager could not find Main.DrawBG; the background toggle is unavailable.");

        MethodInfo drawCloud = typeof(Main)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(method => method.Name.StartsWith("<DrawSurfaceBG>g__DrawCloud|", StringComparison.Ordinal)
                && method.GetParameters().Length == 3);
        if (drawCloud != null)
            drawCloudHook = new Hook(drawCloud,
                new Action<Action<int, Color, float>, int, Color, float>(DrawCloudDetour));
        else
            Log.Warn("World Gen Manager could not find DrawSurfaceBG's cloud helper; the cloud toggle is unavailable.");

        On_Main.DrawSunAndMoon += DrawSunAndMoon;
        On_Main.DrawStarsInBackground += DrawStars;
        IL_Main.DoDraw += PatchSkyDraw;
    }

    public override void Unload()
    {
        IL_Main.DoDraw -= PatchSkyDraw;
        On_Main.DrawStarsInBackground -= DrawStars;
        On_Main.DrawSunAndMoon -= DrawSunAndMoon;
        drawCloudHook?.Dispose();
        drawBackgroundHook?.Dispose();
        drawCloudHook = null;
        drawBackgroundHook = null;
        SetAll(true);
    }

    public override void OnWorldUnload() => SetAll(true);

    private static void DrawBackgroundDetour(Action<Main> orig, Main self)
    {
        if (Main.gameMenu || showBackground)
            orig(self);
    }

    private static void DrawCloudDetour(Action<int, Color, float> orig, int index, Color color, float yOffset)
    {
        if (Main.gameMenu || showClouds)
            orig(index, color, yOffset);
    }

    private static void DrawSunAndMoon(On_Main.orig_DrawSunAndMoon orig, Main self,
        Main.SceneArea sceneArea, Color moonColor, Color sunColor, float mushroomInfluence)
    {
        if (Main.gameMenu || showSunAndMoon)
            orig(self, sceneArea, moonColor, sunColor, mushroomInfluence);
    }

    private static void DrawStars(On_Main.orig_DrawStarsInBackground orig, Main self,
        Main.SceneArea sceneArea, bool artificial)
    {
        if (Main.gameMenu || showStars)
            orig(self, sceneArea, artificial);
    }

    private static void PatchSkyDraw(ILContext il)
    {
        ILCursor cursor = new(il);
        if (!cursor.TryGotoNext(MoveType.Before,
            instruction => instruction.MatchLdsfld<Main>("spriteBatch"),
            instruction => instruction.MatchLdloc(25),
            _ => true,
            instruction => instruction.MatchLdloc(26),
            instruction => instruction.MatchLdsfld<Main>("ColorOfTheSkies"),
            _ => true))
        {
            Log.Warn("World Gen Manager could not locate Terraria's sky draw; the sky toggle is unavailable.");
            return;
        }

        ILLabel afterDraw = il.DefineLabel();
        cursor.Emit(OpCodes.Call, typeof(WorldGenVisualSystem).GetMethod(nameof(ShouldDrawSky), BindingFlags.NonPublic | BindingFlags.Static));
        cursor.Emit(OpCodes.Brfalse, afterDraw);
        cursor.Index += 6;
        cursor.MarkLabel(afterDraw);
    }

    private static bool ShouldDrawSky() => Main.gameMenu || showSky;
}
