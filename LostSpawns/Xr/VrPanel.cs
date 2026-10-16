using System.Drawing;
using System.Numerics;
using SpawnDev.GameUI;
using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Input;
using SpawnDev.SpawnJS.JSObjects;

namespace LostSpawns.Xr;

/// <summary>
/// A GameUI panel floating in the room, to be pointed at: its children are ordinary GameUI labels, buttons and panels laid out in panel pixels
/// (1 px = <see cref="WorldScale"/> metres), the ray of a controller meets it and the trigger clicks. Ported from SpawnScene's in-headset menu,
/// which GameUI's own hit test did not fit (that ignores WorldScale): placement, hit test and drawing use one transform.
/// </summary>
public sealed class VrPanel
{
    private const float CursorSize = 12;

    private readonly GameInput _input = new();
    private readonly UIPanel _cursor;
    private bool _triggerWasDown;
    private Matrix4x4 _model = Matrix4x4.Identity;

    /// <summary>The panel; add buttons and labels to it in panel pixels.</summary>
    public UIWorldPanel Panel { get; }
    public float PanelWidth { get; }
    public float PanelHeight { get; }
    public float WorldScale { get; }
    /// <summary>Drawn and pointed at.</summary>
    public bool IsOpen { get; set; }
    /// <summary>Only drawn; the ray passes through it (a readout, not a menu).</summary>
    public bool ReadOnly { get; set; }
    /// <summary>Where the ray meets the panel this frame, in panel pixels (null: not on the panel).</summary>
    public Vector2? Cursor { get; private set; }

    public VrPanel(float width, float height, float worldScale = 0.001f)
    {
        PanelWidth = width;
        PanelHeight = height;
        WorldScale = worldScale;
        Panel = new UIWorldPanel
        {
            PanelWidth = width, PanelHeight = height, WorldScale = worldScale,
            BackgroundColor = Color.FromArgb(225, 14, 18, 26),
            BorderColor = Color.FromArgb(255, 70, 110, 150), BorderWidth = 2, CornerRadius = 12,
        };
        _cursor = new UIPanel
        {
            Width = CursorSize, Height = CursorSize, CornerRadius = CursorSize / 2, BorderWidth = 0,
            BackgroundColor = Color.FromArgb(240, 120, 220, 255), Visible = false,
        };
    }

    /// <summary>Where the panel is, in the room (row vectors: local X = its right, Y = up, Z toward the viewer).</summary>
    public Matrix4x4 Model { get => _model; set => _model = value; }

    /// <summary>A transform that puts a panel at <paramref name="position"/>, standing upright, its face turned to <paramref name="toward"/>.</summary>
    public static Matrix4x4 FacingUpright(Vector3 position, Vector3 toward)
    {
        var back = toward - position;
        back.Y = 0;
        back = back.LengthSquared() < 1e-8f ? Vector3.UnitZ : Vector3.Normalize(back);
        var right = Vector3.Cross(Vector3.UnitY, back);
        return new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            0, 1, 0, 0,
            back.X, back.Y, back.Z, 0,
            position.X, position.Y, position.Z, 1);
    }

    /// <summary>A transform that puts a panel at <paramref name="position"/>, its face turned to <paramref name="toward"/> exactly (tilted too: a wrist display).</summary>
    public static Matrix4x4 Facing(Vector3 position, Vector3 toward)
    {
        var back = toward - position;
        back = back.LengthSquared() < 1e-8f ? Vector3.UnitZ : Vector3.Normalize(back);
        var right = Vector3.Cross(Vector3.UnitY, back);
        right = right.LengthSquared() < 1e-8f ? Vector3.UnitX : Vector3.Normalize(right);
        var up = Vector3.Cross(back, right);
        return new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            up.X, up.Y, up.Z, 0,
            back.X, back.Y, back.Z, 0,
            position.X, position.Y, position.Z, 1);
    }

    /// <summary>Where a ray meets the panel, in panel pixels (x right, y down), or null if it misses.</summary>
    public Vector2? RayToPanelPixel(Vector3 rayOrigin, Vector3 rayDirection, out float distance)
    {
        distance = 0;
        var drawn = Matrix4x4.CreateScale(PanelWidth * WorldScale, PanelHeight * WorldScale, 1f) * _model;
        if (!Matrix4x4.Invert(drawn, out var inv)) return null;
        var o = Vector3.Transform(rayOrigin, inv);
        var d = Vector3.TransformNormal(rayDirection, inv);
        if (MathF.Abs(d.Z) < 1e-8f) return null;
        float t = -o.Z / d.Z;
        if (t <= 0) return null;
        var hit = o + d * t;
        if (hit.X < -0.5f || hit.X > 0.5f || hit.Y < -0.5f || hit.Y > 0.5f) return null;
        // t is along the (unit) world direction pushed through the inverse, so the metres are t scaled back
        distance = Vector3.Distance(rayOrigin, Vector3.Transform(hit, drawn));
        return new Vector2((hit.X + 0.5f) * PanelWidth, (0.5f - hit.Y) * PanelHeight);
    }

    /// <summary>One frame of pointing. Returns true while the ray is on the open panel (so the trigger belongs to it, not to the game).</summary>
    public bool Step(Vector3? rayOrigin, Vector3? rayDirection, bool triggerDown, float dt)
    {
        Vector2? px = null;
        if (IsOpen && !ReadOnly && rayOrigin is { } o && rayDirection is { } d)
            px = RayToPanelPixel(o, d, out _);
        Cursor = px;

        _input.Poll();   // no providers: clears last frame's pointers
        if (px is { } p)
        {
            _input.AddPointer(new Pointer
            {
                Type = PointerType.Controller,
                ScreenPosition = p,
                IsPressed = triggerDown,
                WasPressed = triggerDown && !_triggerWasDown,
                WasReleased = !triggerDown && _triggerWasDown,
            });
        }
        _triggerWasDown = triggerDown;
        if (IsOpen) Panel.Update(_input, dt);
        return px != null;
    }

    /// <summary>Draw the panel (and the cursor) into an eye view; <paramref name="roomViewProjection"/> is the eye's room view times its projection.</summary>
    public void Draw(UIRenderer renderer, Matrix4x4 roomViewProjection, GPUCommandEncoder encoder, GPUTextureView colorTarget, GPUTextureView depthTarget)
    {
        if (!IsOpen) return;
        if (!ReadOnly)
        {
            // The cursor is the last child so it draws on top
            if (_cursor.Parent != Panel) Panel.AddChild(_cursor);
            else if (Panel.Children[^1] != _cursor) { Panel.RemoveChild(_cursor); Panel.AddChild(_cursor); }
            _cursor.Visible = Cursor != null;
            if (Cursor is { } c) { _cursor.X = c.X - CursorSize / 2; _cursor.Y = c.Y - CursorSize / 2; }
        }
        Panel.WorldTransform = _model;
        Panel.DrawWorld(renderer, roomViewProjection, encoder, colorTarget, depthTarget);
    }
}
