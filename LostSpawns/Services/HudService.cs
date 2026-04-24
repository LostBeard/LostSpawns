using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.GameUI;
using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Rendering;
using System.Drawing;
using System.Numerics;
using Microsoft.AspNetCore.Components;

namespace LostSpawns.Services;

/// <summary>
/// Wires SpawnDev.GameUI into the Lost Spawns render loop.
/// Creates the survival game HUD layout and renders it as a WebGPU
/// overlay pass on top of the voxel scene.
///
/// Initialization: call Init() after RenderService.Init() so the
/// same GPUDevice/Queue are available.
///
/// Per-frame: call Update(dt) from the game loop, then the OnPostRender
/// callback handles the GPU overlay pass automatically.
/// </summary>
public class HudService : IDisposable
{
    private readonly GameUIService _ui;
    private readonly PlayerStatsService _stats;
    private readonly InventoryService _inventory;
    private readonly SettingsService _settings;
    private RenderService? _renderer;
    private UIGrid? _backpackGrid;
    private UIGrid? _inventoryHotbarRow;

    // HUD elements
    public UIStatusHUD StatusHUD { get; private set; } = null!;
    public UIHotbar Hotbar { get; private set; } = null!;
    public UICrosshair Crosshair { get; private set; } = null!;
    public UICompass Compass { get; private set; } = null!;
    public UIMapPanel Minimap { get; private set; } = null!;
    public UIScreenOverlay ScreenOverlay { get; private set; } = null!;
    public UINotificationStack Notifications { get; private set; } = null!;

    private UIProgressBar? _loadingBar;
    private UILabel? _loadingStatus;

    // Last-seen stat values for threshold-cross detection. Start at 1.0 (fed/hydrated)
    // so the first tick below 0.5 fires a "Peckish"/"Moist" toast.
    private float _lastHungerSeen = 1f;
    private float _lastThirstSeen = 1f;

    public bool IsInitialized { get; private set; }

    /// <summary>True while the pause menu is on top of the screen stack.</summary>
    public bool IsPaused => _ui.Screens.ActiveScreen == "pause";

    /// <summary>True while the inventory screen is on top of the screen stack.</summary>
    public bool IsInventoryOpen => _ui.Screens.ActiveScreen == "inventory";

    /// <summary>True while the in-game settings overlay is on top of the screen stack.</summary>
    public bool IsSettingsOpen => _ui.Screens.ActiveScreen == "settings";

    /// <summary>True while the initial terrain-loading overlay is showing.</summary>
    public bool IsLoading => _ui.Screens.ActiveScreen == "loading";

    /// <summary>True while any modal overlay (pause, inventory, settings, loading) covers the HUD.</summary>
    public bool IsAnyMenuOpen => IsPaused || IsInventoryOpen || IsSettingsOpen || IsLoading;

    /// <summary>Fired when the player clicks Resume in the pause menu.</summary>
    public event Action? OnResumeClicked;

    /// <summary>Fired when the player clicks Quit to Menu in the pause menu.</summary>
    public event Action? OnQuitToMenuClicked;

    public HudService(GameUIService ui, PlayerStatsService stats, InventoryService inventory, SettingsService settings)
    {
        _ui = ui;
        _stats = stats;
        _inventory = inventory;
        _settings = settings;
        _inventory.OnInventoryChanged += SyncInventoryToHud;
        _inventory.OnActiveHotbarChanged += SyncActiveHotbar;
        _stats.OnDamageTaken += HandleDamageTaken;
        _stats.OnHealed += HandleHealed;
    }

    private void HandleDamageTaken(float amount)
    {
        // Red flash intensity scales with damage size (capped at ~0.6 alpha).
        // 0.05 HP (small hit) -> alpha ~55, 0.3 HP (heavy) -> alpha ~150.
        int alpha = Math.Clamp((int)(amount * 500f), 40, 180);
        ScreenOverlay?.Flash(System.Drawing.Color.FromArgb(alpha, 220, 20, 20), 0.5f);
        NotifyDamage($"-{(int)(amount * 100f)} HP");
    }

    private void HandleHealed(float amount)
    {
        // Soft green flash for heals worth noticing (small ticks don't flash).
        if (amount < 0.03f) return;
        int alpha = Math.Clamp((int)(amount * 300f), 30, 120);
        ScreenOverlay?.Flash(System.Drawing.Color.FromArgb(alpha, 60, 200, 80), 0.4f);
    }

    /// <summary>
    /// Initialize the HUD. Call after RenderService.Init().
    /// Uses the same GPUDevice and queue that renders the voxel scene.
    /// </summary>
    public void Init(RenderService renderer, ElementReference canvasRef)
    {
        _renderer = renderer;

        if (renderer.Device == null || renderer.Queue == null)
            throw new InvalidOperationException("RenderService must be initialized before HudService");

        // Initialize GameUI with the same WebGPU device (overlay on same canvas)
        _ui.Init(renderer.Device, renderer.Queue, renderer.CanvasFormat,
            canvasRef, renderer.CanvasWidth, renderer.CanvasHeight);

        // Build the HUD layout
        BuildHUD();

        // Hook into the render loop (called after voxel pass, same encoder)
        renderer.OnPostRender = OnPostRender;

        IsInitialized = true;
    }

    private void BuildHUD()
    {
        var root = new UIAnchorPanel
        {
            Width = _renderer!.CanvasWidth,
            Height = _renderer.CanvasHeight,
        };
        _ui.Screens.Register("hud", root);
        _ui.Screens.Push("hud");

        // === Status bars (bottom-left) ===
        // Driven by PlayerStatsService. Values push here in Update() so gameplay
        // systems can write to the stats and the bars animate automatically.
        StatusHUD = new UIStatusHUD { Width = 180 };
        SyncStatsToHud();
        root.AddAnchored(StatusHUD, Anchor.BottomLeft, offsetX: 16, offsetY: -16);

        // === Hotbar (bottom-center) ===
        Hotbar = new UIHotbar { SlotCount = InventoryService.HotbarSize };
        Hotbar.SelectedSlot = _inventory.ActiveHotbarIndex;
        // UIHotbar handles 1-9 keys + scroll wheel + click internally; we just push
        // the widget's selection into InventoryService so gameplay code / the
        // inventory screen / the HUD share one canonical "active slot" value.
        Hotbar.OnSlotChanged = idx => _inventory.ActiveHotbarIndex = idx;
        root.AddAnchored(Hotbar, Anchor.BottomCenter, offsetY: -12);

        // === Crosshair (center) ===
        Crosshair = new UICrosshair();
        root.AddAnchored(Crosshair, Anchor.Center);

        // === Compass (top-center) ===
        Compass = new UICompass { Width = 200 };
        root.AddAnchored(Compass, Anchor.TopCenter, offsetY: 12);

        // === Minimap (top-right) ===
        Minimap = new UIMapPanel { Width = 160, Height = 160 };
        Minimap.AddMarker(new MapMarker
        {
            Id = "spawn",
            Label = "Spawn",
            WorldPosition = Vector2.Zero,
            Type = MapMarkerType.POI,
            AlwaysShowLabel = true,
        });
        root.AddAnchored(Minimap, Anchor.TopRight, offsetX: -12, offsetY: 12);

        // === Notification stack (below the minimap, top-right) ===
        // Slides in from the right; fades + drops after DefaultDuration seconds.
        // Any gameplay system can call Hud.Notify* to surface feedback without
        // reaching into GameUI internals.
        Notifications = new UINotificationStack { Width = 260 };
        root.AddAnchored(Notifications, Anchor.TopRight, offsetX: -12, offsetY: 180);

        // === Screen overlay (damage flash, fade effects) ===
        ScreenOverlay = new UIScreenOverlay();

        // === Pause menu (registered but not pushed; Game.razor pushes on Escape) ===
        _ui.Screens.Register("pause", BuildPauseMenu());

        // === Inventory screen (registered but not pushed; Game.razor pushes on I) ===
        _ui.Screens.Register("inventory", BuildInventoryScreen());

        // === Settings overlay (pushed from pause menu or wherever) ===
        _ui.Screens.Register("settings", BuildSettingsScreen());

        // === Loading overlay (pushed by Game.razor during initial chunk load) ===
        _ui.Screens.Register("loading", BuildLoadingScreen());

        // Push current inventory state into both the persistent hotbar and the
        // inventory screen's grid, so first paint matches the data model.
        SyncInventoryToHud();
    }

    private UIElement BuildPauseMenu()
    {
        var anchor = new UIAnchorPanel
        {
            Width = _renderer!.CanvasWidth,
            Height = _renderer.CanvasHeight,
        };

        // Centered vertical panel with title + buttons.
        var panel = new UIPanel
        {
            Width = 280,
            Height = 280,
            CornerRadius = 8,
        };

        var title = new UILabel
        {
            Text = "PAUSED",
            FontSize = FontSize.Heading,
            Width = 280,
            Height = 40,
            X = 0,
            Y = 18,
            Align = TextAlign.Center,
        };
        panel.AddChild(title);

        var resume = new UIButton
        {
            Text = "Resume",
            Width = 220,
            Height = 44,
            X = 30,
            Y = 80,
        };
        resume.OnClick = () => OnResumeClicked?.Invoke();
        panel.AddChild(resume);

        var settings = new UIButton
        {
            Text = "Settings",
            Width = 220,
            Height = 44,
            X = 30,
            Y = 136,
        };
        // Opens the in-game overlay ON TOP of the pause menu. Pause stays rendered
        // behind (dimmed) so the player sees context. Close -> pops back to pause.
        // Previous behavior (navigate to /settings) tore down the game; gone now.
        settings.OnClick = () => ShowSettings();
        panel.AddChild(settings);

        var quit = new UIButton
        {
            Text = "Quit to Menu",
            Width = 220,
            Height = 44,
            X = 30,
            Y = 192,
        };
        quit.OnClick = () => OnQuitToMenuClicked?.Invoke();
        panel.AddChild(quit);

        anchor.AddAnchored(panel, Anchor.Center);
        return anchor;
    }

    /// <summary>Push the pause menu onto the screen stack (dims HUD behind it).</summary>
    public void ShowPauseMenu()
    {
        if (_ui.Screens.ActiveScreen == "pause") return;
        _ui.Screens.Push("pause");
    }

    /// <summary>Pop the pause menu (back to HUD).</summary>
    public void HidePauseMenu()
    {
        if (_ui.Screens.ActiveScreen != "pause") return;
        _ui.Screens.Pop();
    }

    /// <summary>Toggle the inventory screen.</summary>
    public void ToggleInventory()
    {
        if (IsInventoryOpen) _ui.Screens.Pop();
        else _ui.Screens.Push("inventory");
    }

    /// <summary>Pop the inventory screen (back to HUD).</summary>
    public void HideInventory()
    {
        if (_ui.Screens.ActiveScreen != "inventory") return;
        _ui.Screens.Pop();
    }

    /// <summary>Push the in-game settings overlay on top of whatever is below.</summary>
    public void ShowSettings()
    {
        if (_ui.Screens.ActiveScreen == "settings") return;
        _ui.Screens.Push("settings");
    }

    /// <summary>Pop the settings overlay (back to whatever was below, usually pause menu).</summary>
    public void HideSettings()
    {
        if (_ui.Screens.ActiveScreen != "settings") return;
        _ui.Screens.Pop();
    }

    /// <summary>Push the loading overlay. Call before starting long-running chunk generation.</summary>
    public void ShowLoadingScreen()
    {
        if (_ui.Screens.ActiveScreen == "loading") return;
        _ui.Screens.Push("loading");
    }

    /// <summary>Pop the loading overlay. Call when chunk generation finishes.</summary>
    public void HideLoadingScreen()
    {
        if (_ui.Screens.ActiveScreen != "loading") return;
        _ui.Screens.Pop();
    }

    /// <summary>
    /// Update the progress bar + status label on the loading overlay.
    /// No-op if the overlay isn't the active screen (safe to call unconditionally).
    /// </summary>
    public void SetLoadingProgress(float fraction, string status)
    {
        if (_loadingBar != null) _loadingBar.Value = Math.Clamp(fraction, 0f, 1f);
        if (_loadingStatus != null) _loadingStatus.Text = status;
    }

    private UIElement BuildLoadingScreen()
    {
        var anchor = new UIAnchorPanel
        {
            Width = _renderer!.CanvasWidth,
            Height = _renderer.CanvasHeight,
        };

        var panel = new UIPanel
        {
            Width = 520,
            Height = 200,
            CornerRadius = 10,
        };

        var title = new UILabel
        {
            Text = "LOADING TERRAIN",
            FontSize = FontSize.Heading,
            Width = 520,
            Height = 36,
            X = 0,
            Y = 32,
            Align = TextAlign.Center,
        };
        panel.AddChild(title);

        _loadingBar = new UIProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            ShowPercentage = true,
            Width = 460,
            Height = 28,
            X = 30,
            Y = 96,
        };
        panel.AddChild(_loadingBar);

        _loadingStatus = new UILabel
        {
            Text = "Preparing world…",
            FontSize = FontSize.Caption,
            Width = 520,
            Height = 20,
            X = 0,
            Y = 140,
            Align = TextAlign.Center,
        };
        panel.AddChild(_loadingStatus);

        anchor.AddAnchored(panel, Anchor.Center);
        return anchor;
    }

    private UIElement BuildSettingsScreen()
    {
        var anchor = new UIAnchorPanel
        {
            Width = _renderer!.CanvasWidth,
            Height = _renderer.CanvasHeight,
        };

        var panel = new UIPanel
        {
            Width = 420,
            Height = 360,
            CornerRadius = 10,
        };

        var title = new UILabel
        {
            Text = "SETTINGS",
            FontSize = FontSize.Heading,
            Width = 420,
            Height = 36,
            X = 0,
            Y = 16,
            Align = TextAlign.Center,
        };
        panel.AddChild(title);

        // Draw Distance slider
        var drawLabel = new UILabel
        {
            Text = $"Draw Distance: {_settings.DrawDistance} chunks",
            Width = 380,
            Height = 20,
            X = 20,
            Y = 72,
        };
        panel.AddChild(drawLabel);

        var drawSlider = new UISlider
        {
            MinValue = 4,
            MaxValue = 32,
            Value = _settings.DrawDistance,
            Width = 380,
            Height = 20,
            X = 20,
            Y = 96,
        };
        drawSlider.OnChanged = v =>
        {
            int chunks = (int)MathF.Round(v);
            _settings.SaveVideo(chunks, _settings.FieldOfView, _settings.Vsync);
            drawLabel.Text = $"Draw Distance: {chunks} chunks";
        };
        panel.AddChild(drawSlider);

        // Field of View slider
        var fovLabel = new UILabel
        {
            Text = $"Field of View: {(int)_settings.FieldOfView}°",
            Width = 380,
            Height = 20,
            X = 20,
            Y = 140,
        };
        panel.AddChild(fovLabel);

        var fovSlider = new UISlider
        {
            MinValue = 50,
            MaxValue = 120,
            Value = _settings.FieldOfView,
            Width = 380,
            Height = 20,
            X = 20,
            Y = 164,
        };
        fovSlider.OnChanged = v =>
        {
            float fov = MathF.Round(v);
            _settings.SaveVideo(_settings.DrawDistance, fov, _settings.Vsync);
            fovLabel.Text = $"Field of View: {(int)fov}°";
        };
        panel.AddChild(fovSlider);

        // V-Sync checkbox
        var vsync = new UICheckbox
        {
            Text = "V-Sync",
            IsChecked = _settings.Vsync,
            Width = 380,
            Height = 28,
            X = 20,
            Y = 210,
        };
        vsync.OnChanged = on =>
            _settings.SaveVideo(_settings.DrawDistance, _settings.FieldOfView, on);
        panel.AddChild(vsync);

        // Close button
        var close = new UIButton
        {
            Text = "Close",
            Width = 220,
            Height = 44,
            X = 100,
            Y = 280,
        };
        close.OnClick = () => HideSettings();
        panel.AddChild(close);

        anchor.AddAnchored(panel, Anchor.Center);
        return anchor;
    }

    private UIElement BuildInventoryScreen()
    {
        // Center-anchored so the panel naturally retargets to VR WorldSpace / AR
        // ViewAnchored without any screen-space pixel offsets that wouldn't translate.
        var anchor = new UIAnchorPanel
        {
            Width = _renderer!.CanvasWidth,
            Height = _renderer.CanvasHeight,
        };

        var panel = new UIPanel
        {
            // Sized to fit a 4x8 backpack grid (8 cols * 52 = 416 + padding) + hotbar row below.
            Width = 460,
            Height = 380,
            CornerRadius = 10,
        };

        var title = new UILabel
        {
            Text = "INVENTORY",
            FontSize = FontSize.Heading,
            Width = 460,
            Height = 36,
            X = 0,
            Y = 16,
            Align = TextAlign.Center,
        };
        panel.AddChild(title);

        // Backpack grid: 8 cols x 4 rows, 48px cells with 4px gap. Drag-drop enabled so the
        // player can rearrange items between backpack slots and onto the hotbar below.
        _backpackGrid = new UIGrid
        {
            Columns = InventoryService.BackpackColumns,
            Rows = InventoryService.BackpackRows,
            CellSize = 48,
            CellGap = 4,
            Width = InventoryService.BackpackColumns * 52, // CellSize + CellGap
            Height = InventoryService.BackpackRows * 52,
            X = 22,
            Y = 64,
            EnableDragDrop = true,
        };
        _backpackGrid.OnCellClicked = idx =>
        {
            _backpackGrid!.SelectedIndex = idx;
        };
        _backpackGrid.OnDragStart = idx =>
        {
            var item = _inventory.GetBackpack(idx);
            if (item is null) return;
            _ui.DragDrop.BeginDrag(new InventoryDragData(false, idx), item.Name);
        };
        panel.AddChild(_backpackGrid);

        // Hotbar row below the backpack as a UIGrid (1 row x HotbarSize cols) so it
        // participates in drag-drop. The separate persistent HUD hotbar (UIHotbar at
        // BottomCenter) stays read-only - you rearrange in the inventory screen, the
        // in-game bar reflects the result.
        _inventoryHotbarRow = new UIGrid
        {
            Columns = InventoryService.HotbarSize,
            Rows = 1,
            CellSize = 44,
            CellGap = 4,
            Width = InventoryService.HotbarSize * 48,
            Height = 44,
            X = (460 - (InventoryService.HotbarSize * 48)) / 2,
            Y = 290,
            EnableDragDrop = true,
        };
        _inventoryHotbarRow.OnCellClicked = idx =>
        {
            _inventoryHotbarRow!.SelectedIndex = idx;
        };
        _inventoryHotbarRow.OnDragStart = idx =>
        {
            var item = _inventory.GetHotbar(idx);
            if (item is null) return;
            _ui.DragDrop.BeginDrag(new InventoryDragData(true, idx), item.Name);
        };
        panel.AddChild(_inventoryHotbarRow);

        // Register both grids as drop targets. On drop, read the grid's HoveredIndex
        // (updated each frame during drag) to find the destination cell; if the drop
        // fell inside the grid bounds but outside any cell, HoveredIndex is -1 and
        // we treat it as a cancel.
        _ui.DragDrop.RegisterTarget(_backpackGrid, (data, _) =>
        {
            if (data is InventoryDragData src && _backpackGrid!.HoveredIndex >= 0)
                _inventory.MoveSlot(src.FromHotbar, src.Index, false, _backpackGrid.HoveredIndex);
        });
        _ui.DragDrop.RegisterTarget(_inventoryHotbarRow, (data, _) =>
        {
            if (data is InventoryDragData src && _inventoryHotbarRow!.HoveredIndex >= 0)
                _inventory.MoveSlot(src.FromHotbar, src.Index, true, _inventoryHotbarRow.HoveredIndex);
        });

        var closeLabel = new UILabel
        {
            Text = "Press I or Esc to close",
            FontSize = FontSize.Caption,
            Width = 460,
            Height = 20,
            X = 0,
            Y = 350,
            Align = TextAlign.Center,
        };
        panel.AddChild(closeLabel);

        anchor.AddAnchored(panel, Anchor.Center);
        return anchor;
    }

    private void SyncActiveHotbar(int idx)
    {
        if (Hotbar != null) Hotbar.SelectedSlot = idx;
        if (_inventoryHotbarRow != null) _inventoryHotbarRow.SelectedIndex = idx;

        // Small toast so changing slots feels responsive. Empty slot -> no message.
        var item = _inventory.Hotbar[idx];
        if (item != null)
            Notify($"Equipped: {item.Name}");
    }

    /// <summary>Push a toast notification onto the HUD. Fades and drops automatically.</summary>
    public void Notify(string text, NotificationType type = NotificationType.Info)
        => Notifications?.Push(text, type);

    /// <summary>Green-accent success toast (pickup, craft success, XP gain).</summary>
    public void NotifySuccess(string text) => Notify(text, NotificationType.Success);

    /// <summary>Yellow-accent warning toast (low stamina, nearby danger).</summary>
    public void NotifyWarning(string text) => Notify(text, NotificationType.Warning);

    /// <summary>Red-accent damage toast (hit, break, loss).</summary>
    public void NotifyDamage(string text) => Notify(text, NotificationType.Damage);

    /// <summary>Purple-accent achievement toast (milestone, unlock).</summary>
    public void NotifyAchievement(string text) => Notify(text, NotificationType.Achievement);

    private void SyncInventoryToHud()
    {
        if (Hotbar != null)
        {
            for (int i = 0; i < InventoryService.HotbarSize; i++)
            {
                var item = _inventory.Hotbar[i];
                Hotbar.SetSlot(i, item?.Name);
            }
        }

        if (_inventoryHotbarRow != null)
        {
            for (int i = 0; i < InventoryService.HotbarSize; i++)
            {
                var item = _inventory.Hotbar[i];
                _inventoryHotbarRow.SetCell(i, item?.Name);
            }
        }

        if (_backpackGrid != null)
        {
            for (int i = 0; i < InventoryService.BackpackSize; i++)
            {
                var item = _inventory.Backpack[i];
                _backpackGrid.SetCell(i, item?.Name);
            }
        }
    }

    private void SyncStatsToHud()
    {
        StatusHUD.Health = _stats.Health;
        StatusHUD.Stamina = _stats.Stamina;
        StatusHUD.Hunger = _stats.Hunger;
        StatusHUD.Thirst = _stats.Thirst;
        StatusHUD.Temperature = _stats.Temperature;

        // Persistent low-health vignette. Ramps in from HP 0.3 down to 0 so the
        // effect intensifies as the player bleeds out. SetPersistent replaces the
        // same key each frame; ClearPersistent removes it when health recovers.
        if (_stats.Health < 0.3f)
        {
            // 0.3 HP -> alpha 20 (barely visible), 0.0 HP -> alpha 120 (heavy red).
            float t = 1f - (_stats.Health / 0.3f);
            int alpha = Math.Clamp((int)(20 + t * 100f), 20, 120);
            ScreenOverlay?.SetPersistent("lowHealth",
                System.Drawing.Color.FromArgb(alpha, 180, 0, 0));
        }
        else
        {
            ScreenOverlay?.ClearPersistent("lowHealth");
        }

        // Threshold-cross toasts for hunger + thirst. Fires exactly once on the
        // frame the value drops past each threshold (0.5 / 0.3 / 0.1). Recovery
        // above a threshold resets it, so eating/drinking and then starving
        // again re-fires the message chain.
        CheckHungerThreshold();
        CheckThirstThreshold();
    }

    private void CheckHungerThreshold()
    {
        float cur = _stats.Hunger, prev = _lastHungerSeen;
        if (prev > 0.5f && cur <= 0.5f) Notify("Peckish");
        if (prev > 0.3f && cur <= 0.3f) NotifyWarning("Hungry");
        if (prev > 0.1f && cur <= 0.1f) NotifyDamage("Starving!");
        _lastHungerSeen = cur;
    }

    private void CheckThirstThreshold()
    {
        float cur = _stats.Thirst, prev = _lastThirstSeen;
        if (prev > 0.5f && cur <= 0.5f) Notify("Moist");
        if (prev > 0.3f && cur <= 0.3f) NotifyWarning("Thirsty");
        if (prev > 0.1f && cur <= 0.1f) NotifyDamage("Dehydrated!");
        _lastThirstSeen = cur;
    }

    /// <summary>
    /// Update HUD state from game data. Call per frame from the game loop.
    /// </summary>
    public void Update(float deltaTime, Vector3 cameraPosition, float cameraYaw)
    {
        if (!IsInitialized) return;

        // Update viewport if canvas resized
        if (_renderer!.CanvasWidth != _ui.ViewportWidth || _renderer.CanvasHeight != _ui.ViewportHeight)
            _ui.SetViewport(_renderer.CanvasWidth, _renderer.CanvasHeight);

        // Push current player stats into the StatusHUD bars. Cheap field copy;
        // UIStatusHUD handles its own dirty/animation tracking internally.
        SyncStatsToHud();

        // Update compass bearing from camera yaw
        Compass.Bearing = cameraYaw;

        // Update minimap player position (XZ plane + altitude)
        Minimap.PlayerPosition = new Vector2(cameraPosition.X, cameraPosition.Z);
        Minimap.PlayerAltitude = cameraPosition.Y;
        Minimap.PlayerRotation = cameraYaw * MathF.PI / 180f;

        // Update screen overlay effects
        ScreenOverlay.Update(deltaTime);

        // GameUI per-frame update (input polling, animations, focus)
        _ui.Update(deltaTime);
    }

    /// <summary>
    /// Called by RenderService after the voxel render pass.
    /// Renders the GameUI overlay on the same command encoder.
    /// </summary>
    private void OnPostRender(GPUCommandEncoder encoder, GPUTextureView colorTarget,
        int viewportWidth, int viewportHeight)
    {
        if (!IsInitialized || !_ui.Renderer.IsReady) return;

        try
        {
            // Begin GameUI frame
            _ui.BeginRender(viewportWidth, viewportHeight);

            // Draw the HUD screen stack
            _ui.Screens.Draw(_ui.Renderer);

            // Draw screen overlay effects (damage flash, etc.) on top
            ScreenOverlay.Draw(_ui.Renderer, viewportWidth, viewportHeight);

            // Flush to GPU (appends a render pass with LoadOp.Load to the encoder)
            _ui.EndRender(encoder, colorTarget);
        }
        catch (Exception ex)
        {
            // GPU context may have been lost - log but don't crash the render loop
            Console.WriteLine($"[HudService] Render error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _inventory.OnInventoryChanged -= SyncInventoryToHud;
        _inventory.OnActiveHotbarChanged -= SyncActiveHotbar;
        _stats.OnDamageTaken -= HandleDamageTaken;
        _stats.OnHealed -= HandleHealed;
        if (_renderer != null)
            _renderer.OnPostRender = null;
    }
}
