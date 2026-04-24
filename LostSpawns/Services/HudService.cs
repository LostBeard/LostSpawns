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

    public bool IsInitialized { get; private set; }

    /// <summary>True while the pause menu is on top of the screen stack.</summary>
    public bool IsPaused => _ui.Screens.ActiveScreen == "pause";

    /// <summary>True while the inventory screen is on top of the screen stack.</summary>
    public bool IsInventoryOpen => _ui.Screens.ActiveScreen == "inventory";

    /// <summary>True while any modal overlay (pause, inventory) covers the HUD.</summary>
    public bool IsAnyMenuOpen => IsPaused || IsInventoryOpen;

    /// <summary>Fired when the player clicks Resume in the pause menu.</summary>
    public event Action? OnResumeClicked;

    /// <summary>Fired when the player clicks Settings in the pause menu.</summary>
    public event Action? OnSettingsClicked;

    /// <summary>Fired when the player clicks Quit to Menu in the pause menu.</summary>
    public event Action? OnQuitToMenuClicked;

    public HudService(GameUIService ui, PlayerStatsService stats, InventoryService inventory)
    {
        _ui = ui;
        _stats = stats;
        _inventory = inventory;
        _inventory.OnInventoryChanged += SyncInventoryToHud;
        _inventory.OnActiveHotbarChanged += SyncActiveHotbar;
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
        settings.OnClick = () => OnSettingsClicked?.Invoke();
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
        if (_renderer != null)
            _renderer.OnPostRender = null;
    }
}
