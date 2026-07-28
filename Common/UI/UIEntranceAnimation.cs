using System;

namespace PvPArenas.Common.UI;

/// <summary>
/// Frame-driven entrance animation for immediate-mode panels: a fast fade-in paired with a
/// cubic ease-out slide (fast at first, settling slowly). Resets automatically after the
/// panel stops being drawn for a couple of ticks.
/// </summary>
internal struct UIEntranceAnimation
{
    private uint lastDrawTick;
    private int age;

    internal float Alpha { get; private set; }
    internal int SlideOffset { get; private set; }

    internal void Advance(int slidePixels = 36, int fadeFrames = 12, int slideFrames = 32)
    {
        if (Main.GameUpdateCount - lastDrawTick > 2)
            age = 0;
        lastDrawTick = Main.GameUpdateCount;
        age++;

        Alpha = Math.Min(1f, age / (float)fadeFrames);
        float eased = 1f - MathF.Pow(1f - Math.Min(1f, age / (float)slideFrames), 3f);
        SlideOffset = (int)MathF.Round((1f - eased) * slidePixels);
    }
}
