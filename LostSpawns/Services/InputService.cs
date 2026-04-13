using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;
using System.Numerics;

namespace LostSpawns.Services;

/// <summary>
/// Tracks keyboard and mouse input state using BlazorJS ActionEvent patterns.
/// Uses += / -= on Window.OnKeyDown, Window.OnKeyUp, and document.OnMouseMove.
/// </summary>
public class InputService : IAsyncDisposable
{
    private readonly BlazorJSRuntime _js;
    private Window? _window;
    private Document? _document;

    // Movement key state
    public bool Forward { get; private set; }
    public bool Back { get; private set; }
    public bool Left { get; private set; }
    public bool Right { get; private set; }
    public bool Jump { get; private set; }
    public bool Sprint { get; private set; }

    /// <summary>Set of currently-pressed key codes (e.g. "KeyW", "Space", "ShiftLeft").</summary>
    public HashSet<string> KeysDown { get; } = new(StringComparer.Ordinal);

    // Accumulated mouse deltas since last ConsumeMouseDelta() call
    public double MouseDeltaX { get; private set; }
    public double MouseDeltaY { get; private set; }

    /// <summary>True when pointer lock is active (mouse captured by canvas).</summary>
    public bool IsPointerLocked { get; private set; }

    /// <summary>Normalized XZ move vector: X=strafe, Y=forward.</summary>
    public Vector2 MoveVector => new(
        (Right ? 1f : 0f) - (Left ? 1f : 0f),
        (Forward ? 1f : 0f) - (Back ? 1f : 0f)
    );

    public InputService(BlazorJSRuntime js)
    {
        _js = js;
    }

    /// <summary>Call once from the game canvas component to attach all listeners.</summary>
    public void Attach()
    {
        _window = _js.Get<Window>("window");
        _document = _js.Get<Document>("document");

        _window!.OnKeyDown += OnKeyDown;
        _window!.OnKeyUp += OnKeyUp;
        _window!.OnMouseMove += OnMouseMove;
        _document!.OnPointerLockChange += OnPointerLockChange;
    }

    /// <summary>Call on dispose to detach all listeners.</summary>
    public void Detach()
    {
        if (_window != null)
        {
            _window.OnKeyDown -= OnKeyDown;
            _window.OnKeyUp -= OnKeyUp;
        }
        if (_window != null)
        {
            _window.OnMouseMove -= OnMouseMove;
        }
        if (_document != null)
        {
            _document.OnPointerLockChange -= OnPointerLockChange;
        }
    }

    /// <summary>Call once per frame to consume accumulated mouse deltas.</summary>
    public (double dx, double dy) ConsumeMouseDelta()
    {
        var (dx, dy) = (MouseDeltaX, MouseDeltaY);
        MouseDeltaX = 0;
        MouseDeltaY = 0;
        return (dx, dy);
    }

    private void OnKeyDown(KeyboardEvent e)
    {
        if (!e.Repeat) SetKey(e.Key, true);
        KeysDown.Add(e.Code);
    }

    private void OnKeyUp(KeyboardEvent e)
    {
        SetKey(e.Key, false);
        KeysDown.Remove(e.Code);
    }

    private void SetKey(string key, bool down)
    {
        switch (key)
        {
            case "w": case "W": Forward = down; break;
            case "s": case "S": Back = down; break;
            case "a": case "A": Left = down; break;
            case "d": case "D": Right = down; break;
            case " ": Jump = down; break;
            case "Shift": Sprint = down; break;
        }
    }

    private void OnPointerLockChange()
    {
        bool wasLocked = IsPointerLocked;
        IsPointerLocked = _document?.PointerLockElement != null;
        if (wasLocked && !IsPointerLocked)
        {
            // Pointer lock lost - consume any stale deltas
            MouseDeltaX = 0;
            MouseDeltaY = 0;
        }
    }

    private void OnMouseMove(MouseEvent e)
    {
        // Only accumulate mouse movement when pointer is locked
        if (!IsPointerLocked) return;
        MouseDeltaX += e.MovementX;
        MouseDeltaY += e.MovementY;
    }

    public ValueTask DisposeAsync()
    {
        Detach();
        _window?.Dispose();
        _document?.Dispose();
        return ValueTask.CompletedTask;
    }
}
