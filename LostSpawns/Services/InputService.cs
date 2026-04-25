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

    /// <summary>Fired whenever the Escape key is pressed (once per press, not on repeat).</summary>
    public event Action? OnEscapePressed;

    /// <summary>Fired whenever pointer-lock state changes. Argument: new locked state.</summary>
    public event Action<bool>? OnPointerLockChanged;

    /// <summary>Fired whenever the inventory-toggle key is pressed (default I/Tab).</summary>
    public event Action? OnInventoryTogglePressed;

    /// <summary>Fired on E key press. Used for "interact / break block in crosshair" gameplay.</summary>
    public event Action? OnInteractPressed;

    /// <summary>Fired on C key press. Toggles the crafting screen.</summary>
    public event Action? OnCraftingTogglePressed;

    /// <summary>Fired on F key press. Feed-fire / secondary interact (reserved for future contextual actions).</summary>
    public event Action? OnFeedFirePressed;

    /// <summary>Fired on Z key press. Sleep / rest action (context-sensitive).</summary>
    public event Action? OnSleepPressed;

    /// <summary>Fired on G key press. Quick-eat the best food in inventory.</summary>
    public event Action? OnQuickEatPressed;

    /// <summary>Fired on T key press. Quick-drink the first water in inventory.</summary>
    public event Action? OnQuickDrinkPressed;

    /// <summary>Fired on H key press. Quick-bandage the first medical item in inventory.</summary>
    public event Action? OnQuickHealPressed;

    /// <summary>Fired on F3 key press. Debug HUD toggle.</summary>
    public event Action? OnDebugTogglePressed;

    /// <summary>Fired on B key press. Terraform mode toggle (sphere carve/build). Rebound from F4 to avoid browser F-key collisions.</summary>
    public event Action? OnTerraformTogglePressed;

    /// <summary>
    /// Fired on mouse wheel scroll. Arg = sign of deltaY (+1 = scroll down,
    /// -1 = scroll up). Used by terraform mode to adjust brush radius;
    /// Game.razor decides whether to consume or let the hotbar handle it.
    /// </summary>
    public event Action<int>? OnWheelScrolled;

    /// <summary>Fired on M key press. Mute / unmute audio.</summary>
    public event Action? OnMuteTogglePressed;

    /// <summary>Fired on F1 key press. Toggles the help overlay.</summary>
    public event Action? OnHelpTogglePressed;

    /// <summary>Fired on J key press. Toggles the achievement-list overlay.</summary>
    public event Action? OnAchievementsTogglePressed;

    /// <summary>Fired on F5 key press. Manual save (in addition to 10s auto-save).</summary>
    public event Action? OnQuickSavePressed;

    /// <summary>Fired on Q key press. Drop active hotbar item to ground.</summary>
    public event Action? OnDropPressed;

    /// <summary>Fired on left mouse-down while pointer is locked (in-game). Break-block action.</summary>
    public event Action? OnLeftClickPressed;

    /// <summary>Fired on right mouse-down while pointer is locked (in-game). Place-block action.</summary>
    public event Action? OnRightClickPressed;

    /// <summary>Dev-only: fired on F9 to simulate taking damage. Lets the probe exercise the damage flow without gameplay content.</summary>
    public event Action? OnDebugDamagePressed;

    /// <summary>Dev-only: fired on F10 to simulate healing.</summary>
    public event Action? OnDebugHealPressed;

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
        _window!.OnMouseDown += OnMouseDown;
        _window!.OnMouseUp += OnMouseUp;
        _window!.OnWheel += OnWheel;
        _document!.OnPointerLockChange += OnPointerLockChange;
    }

    /// <summary>Call on dispose to detach all listeners.</summary>
    public void Detach()
    {
        if (_window != null)
        {
            _window.OnKeyDown -= OnKeyDown;
            _window.OnKeyUp -= OnKeyUp;
            _window.OnMouseMove -= OnMouseMove;
            _window.OnMouseDown -= OnMouseDown;
            _window.OnMouseUp -= OnMouseUp;
            _window.OnWheel -= OnWheel;
        }
        if (_document != null)
        {
            _document.OnPointerLockChange -= OnPointerLockChange;
        }
    }

    /// <summary>True while LMB is held. Used by terraform paint-stroke.</summary>
    public bool IsLeftButtonDown { get; private set; }

    /// <summary>True while RMB is held. Used by terraform paint-stroke.</summary>
    public bool IsRightButtonDown { get; private set; }

    private void OnMouseDown(MouseEvent e)
    {
        // Only fire the gameplay click events while pointer is locked - prevents
        // stray clicks on UI (pause menu, inventory) from breaking blocks in the
        // world behind the modal. UI widgets get their mouse events through
        // GameUIService's own canvas listener, not this path.
        if (!IsPointerLocked) return;
        if (e.Button == MouseButton.PrimaryButton)
        {
            IsLeftButtonDown = true;
            OnLeftClickPressed?.Invoke();
        }
        else if (e.Button == MouseButton.SecondaryButton)
        {
            IsRightButtonDown = true;
            OnRightClickPressed?.Invoke();
        }
    }

    private void OnMouseUp(MouseEvent e)
    {
        // Always clear button state on mouseup, even when not pointer-locked,
        // so a button held down during a pointer-lock exit doesn't get stuck
        // "on" until the next click. Browser fires mouseup reliably on
        // unlock so this stays in sync with reality.
        if (e.Button == MouseButton.PrimaryButton) IsLeftButtonDown = false;
        else if (e.Button == MouseButton.SecondaryButton) IsRightButtonDown = false;
    }

    private void OnWheel(WheelEvent e)
    {
        // Only fire while pointer is locked so wheel scrolling pages outside
        // the canvas (settings overlay, browser) doesn't trigger gameplay
        // wheel handlers. Direction = sign(deltaY); positive = scroll down.
        if (!IsPointerLocked) return;
        if (e.DeltaY == 0) return;
        OnWheelScrolled?.Invoke(e.DeltaY > 0 ? 1 : -1);
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
        if (!e.Repeat && e.Key == "Escape")
            OnEscapePressed?.Invoke();
        // Inventory toggle: I or Tab. Tab is a common browser focus key so Game.razor
        // handles the actual open/close logic - this only fires the intent.
        if (!e.Repeat && (e.Key == "i" || e.Key == "I" || e.Key == "Tab"))
            OnInventoryTogglePressed?.Invoke();

        // Interact / break block in crosshair on E.
        if (!e.Repeat && (e.Key == "e" || e.Key == "E"))
            OnInteractPressed?.Invoke();

        // Crafting screen toggle on C.
        if (!e.Repeat && (e.Key == "c" || e.Key == "C"))
            OnCraftingTogglePressed?.Invoke();

        // Feed fire on F - context-sensitive, handler checks proximity.
        if (!e.Repeat && (e.Key == "f" || e.Key == "F"))
            OnFeedFirePressed?.Invoke();

        // Sleep / skip-to-dawn on Z - context-sensitive, handler checks fire.
        if (!e.Repeat && (e.Key == "z" || e.Key == "Z"))
            OnSleepPressed?.Invoke();

        // F-key bindings call PreventDefault so the browser's built-in
        // F-key actions (Help on F1, Find on F3, Refresh on F5, DevTools-
        // adjacent F10 menu in Firefox, etc.) don't fire alongside our
        // handler. F11/F12 are intentionally NOT bound - those are
        // browser-reserved at the OS level on most platforms.

        // Debug HUD toggle on F3 - was Minecraft convention but Chrome's
        // "Find in page" uses F3 too; PreventDefault stops the find bar.
        if (!e.Repeat && e.Key == "F3")
        {
            e.PreventDefault();
            OnDebugTogglePressed?.Invoke();
        }

        // Terraform mode toggle on B (rebound from F4 to avoid Edge
        // collision with address-bar focus + the general F-key footgun).
        if (!e.Repeat && (e.Key == "b" || e.Key == "B"))
            OnTerraformTogglePressed?.Invoke();

        // Mute toggle on M.
        if (!e.Repeat && (e.Key == "m" || e.Key == "M"))
            OnMuteTogglePressed?.Invoke();

        // Help overlay on F1. Some browsers open their own Help on F1;
        // PreventDefault keeps the in-game overlay as the only response.
        if (!e.Repeat && e.Key == "F1")
        {
            e.PreventDefault();
            OnHelpTogglePressed?.Invoke();
        }

        // Achievement-list overlay on J.
        if (!e.Repeat && (e.Key == "j" || e.Key == "J"))
            OnAchievementsTogglePressed?.Invoke();

        // Manual quick-save on F5 - PreventDefault is critical here:
        // F5 is the universal browser refresh shortcut, and refreshing
        // mid-game blows away in-flight state. Our handler still saves
        // first, but suppressing the refresh keeps the player in the run.
        if (!e.Repeat && e.Key == "F5")
        {
            e.PreventDefault();
            OnQuickSavePressed?.Invoke();
        }

        // Drop active item on Q.
        if (!e.Repeat && (e.Key == "q" || e.Key == "Q"))
            OnDropPressed?.Invoke();

        // Quick-eat best food in inventory on G.
        if (!e.Repeat && (e.Key == "g" || e.Key == "G"))
            OnQuickEatPressed?.Invoke();

        // Quick-drink first water in inventory on T.
        if (!e.Repeat && (e.Key == "t" || e.Key == "T"))
            OnQuickDrinkPressed?.Invoke();

        // Quick-bandage on H. Pulls the first medical item (bandage) and
        // uses it. Parallel to G (eat) / T (drink) - emergency triage during
        // a fight without opening the inventory screen.
        if (!e.Repeat && (e.Key == "h" || e.Key == "H"))
            OnQuickHealPressed?.Invoke();

        // Dev hooks until real damage / healing sources exist.
        // F10 toggles the Firefox menu bar; PreventDefault on both for
        // consistency with the rest of our F-key bindings.
        if (!e.Repeat && e.Key == "F9")
        {
            e.PreventDefault();
            OnDebugDamagePressed?.Invoke();
        }
        if (!e.Repeat && e.Key == "F10")
        {
            e.PreventDefault();
            OnDebugHealPressed?.Invoke();
        }
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
        if (wasLocked != IsPointerLocked)
            OnPointerLockChanged?.Invoke(IsPointerLocked);
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
