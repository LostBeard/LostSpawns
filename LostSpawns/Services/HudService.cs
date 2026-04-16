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
    private RenderService? _renderer;

    // HUD elements
    public UIStatusHUD StatusHUD { get; private set; } = null!;
    public UIHotbar Hotbar { get; private set; } = null!;
    public UICrosshair Crosshair { get; private set; } = null!;
    public UICompass Compass { get; private set; } = null!;
    public UIMapPanel Minimap { get; private set; } = null!;
    public UIScreenOverlay ScreenOverlay { get; private set; } = null!;

    public bool IsInitialized { get; private set; }

    public HudService(GameUIService ui)
    {
        _ui = ui;
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
        StatusHUD = new UIStatusHUD { Width = 180 };
        StatusHUD.Health = 0.85f;
        StatusHUD.Stamina = 0.6f;
        StatusHUD.Hunger = 0.45f;
        StatusHUD.Thirst = 0.7f;
        StatusHUD.Temperature = 0.5f; // comfortable
        root.AddAnchored(StatusHUD, Anchor.BottomLeft, offsetX: 16, offsetY: -16);

        // === Hotbar (bottom-center) ===
        Hotbar = new UIHotbar { SlotCount = 9 };
        Hotbar.SetSlot(0, "Axe");
        Hotbar.SetSlot(1, "Pick");
        Hotbar.SetSlot(4, "Bandage");
        Hotbar.SetSlot(8, "Map");
        Hotbar.SelectedSlot = 0;
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

        // === Screen overlay (damage flash, fade effects) ===
        ScreenOverlay = new UIScreenOverlay();
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
        if (_renderer != null)
            _renderer.OnPostRender = null;
    }
}
