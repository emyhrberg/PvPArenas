using System;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace PvPArenas.Common.AdminTools.UI;

internal sealed class UIResizeHandle() : UIImageButton(Ass.IconResize)
{
    private Vector2 lastMouse;
    private bool dragging;

    internal event Action<Vector2> Dragged;

    public override void OnInitialize()
    {
        HAlign = VAlign = 1f;
        Left.Set(-2f, 0f);
        Top.Set(-2f, 0f);
        Width.Set(26f, 0f);
        Height.Set(26f, 0f);
    }

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);
        dragging = true;
        lastMouse = Main.MouseScreen;
    }

    public override void LeftMouseUp(UIMouseEvent evt)
    {
        base.LeftMouseUp(evt);
        dragging = false;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!dragging || !Main.mouseLeft)
        {
            dragging = false;
            return;
        }

        Vector2 delta = Main.MouseScreen - lastMouse;
        lastMouse = Main.MouseScreen;
        if (delta != Vector2.Zero)
            Dragged?.Invoke(delta);
        if (IsMouseHovering)
            Main.instance.MouseText("Resize");
    }
}
