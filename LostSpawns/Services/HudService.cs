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
    private readonly WorldTimeService _worldTime;
    private readonly CraftingService _crafting;
    private readonly WeatherService _weather;
    private readonly EntityService _entities;
    private readonly CampfireService _fires;
    private readonly GroundItemService _ground;

    // Rain particle state. Each particle stores its current Y, per-particle speed,
    // and X (randomized once at spawn). Updated in Update(dt); drawn in OnPostRender
    // using the UI renderer's DrawRect so the streaks land on top of the voxel scene.
    private readonly float[] _rainX = new float[140];
    private readonly float[] _rainY = new float[140];
    private readonly float[] _rainSpeed = new float[140];

    // Breath-puff particles for cold weather. Shorter-lived + fewer than rain;
    // emitted only when the player's temperature is low. Each particle rises
    // a few pixels as it fades. Age runs 0 -> 1 (1 = dead).
    private const int BreathMax = 8;
    private readonly float[] _breathX = new float[BreathMax];
    private readonly float[] _breathY = new float[BreathMax];
    private readonly float[] _breathAge = new float[BreathMax];
    private float _breathEmitTimer = 0f;

    // Night stars. Fixed positions seeded once (so the sky doesn't churn every
    // frame). Visible only at night; alpha scales with how dark the sky is.
    private const int StarCount = 60;
    private readonly float[] _starX = new float[StarCount];
    private readonly float[] _starY = new float[StarCount];
    private readonly float[] _starSize = new float[StarCount];
    private bool _starsSeeded;

    // One-shot impact markers (e.g. arrow hit point). World-space position +
    // spawn timestamp; projected each frame and faded out. Up to 4 concurrent.
    private const int ImpactMax = 4;
    private readonly System.Numerics.Vector3[] _impactPos = new System.Numerics.Vector3[ImpactMax];
    private readonly DateTime[] _impactSpawned = new DateTime[ImpactMax];
    private int _impactIdx;

    // Floating damage numbers - rise + fade from a world-space anchor.
    private const int DamageNumberMax = 8;
    private readonly System.Numerics.Vector3[] _dmgPos = new System.Numerics.Vector3[DamageNumberMax];
    private readonly DateTime[] _dmgSpawned = new DateTime[DamageNumberMax];
    private readonly float[] _dmgValue = new float[DamageNumberMax];
    private int _dmgIdx;

    /// <summary>Spawn a floating "-NN" red number rising from worldPos. Lifetime ~900ms.</summary>
    public void ShowDamageNumber(System.Numerics.Vector3 worldPos, float amount)
    {
        _dmgPos[_dmgIdx] = worldPos;
        _dmgValue[_dmgIdx] = amount;
        _dmgSpawned[_dmgIdx] = DateTime.UtcNow;
        _dmgIdx = (_dmgIdx + 1) % DamageNumberMax;
    }

    /// <summary>
    /// Spawn an impact marker at a world-space position. Renders a brief
    /// white cross that fades over ~350ms. Used by bow hits so the player
    /// sees where the arrow landed without needing projectile physics.
    /// </summary>
    public void ShowImpactMark(System.Numerics.Vector3 worldPos)
    {
        _impactPos[_impactIdx] = worldPos;
        _impactSpawned[_impactIdx] = DateTime.UtcNow;
        _impactIdx = (_impactIdx + 1) % ImpactMax;
    }

    // Blood splatter ring buffer. Each splatter is 4 particles that drift
    // diagonally outward + fall, fading over ~800ms. Spawned from
    // ShowBloodSplatter when an entity takes a non-fatal hit.
    private const int BloodMax = 12;
    private readonly System.Numerics.Vector3[] _bloodPos = new System.Numerics.Vector3[BloodMax];
    private readonly System.Numerics.Vector3[] _bloodVel = new System.Numerics.Vector3[BloodMax];
    private readonly DateTime[] _bloodSpawned = new DateTime[BloodMax];
    private int _bloodIdx;
    private readonly Random _bloodRng = new();

    /// <summary>
    /// Spawn 3 blood droplets at the given world position. Each gets a
    /// randomized outward/upward velocity so the splatter reads as organic
    /// spray rather than a particle fountain.
    /// </summary>
    public void ShowBloodSplatter(System.Numerics.Vector3 worldPos)
    {
        for (int i = 0; i < 3; i++)
        {
            double angle = _bloodRng.NextDouble() * Math.PI * 2;
            float speed = 1.8f + (float)_bloodRng.NextDouble() * 1.2f;
            _bloodPos[_bloodIdx] = worldPos;
            _bloodVel[_bloodIdx] = new System.Numerics.Vector3(
                (float)Math.Cos(angle) * speed,
                1.5f + (float)_bloodRng.NextDouble() * 0.8f,
                (float)Math.Sin(angle) * speed);
            _bloodSpawned[_bloodIdx] = DateTime.UtcNow;
            _bloodIdx = (_bloodIdx + 1) % BloodMax;
        }
    }
    private bool _rainSeeded;
    private readonly Random _rainRng = new();
    private RenderService? _renderer;
    private UIGrid? _backpackGrid;
    private UIGrid? _inventoryHotbarRow;
    private UILabel? _clockLabel;
    private readonly List<(UIButton Button, UILabel Status)> _craftingRows = new();

    // HUD elements
    public UIStatusHUD StatusHUD { get; private set; } = null!;
    public UIHotbar Hotbar { get; private set; } = null!;
    public UICrosshair Crosshair { get; private set; } = null!;
    public UICompass Compass { get; private set; } = null!;
    public UIMapPanel Minimap { get; private set; } = null!;
    public UIScreenOverlay ScreenOverlay { get; private set; } = null!;
    public UINotificationStack Notifications { get; private set; } = null!;

    /// <summary>
    /// Small prompt centered just below the crosshair ("[E] Chop", "Need a Pick", ...).
    /// Game.razor sets Text each frame based on what the player is aimed at; empty
    /// string hides it visually (UILabel of empty text draws nothing).
    /// </summary>
    public UILabel InteractionPrompt { get; private set; } = null!;

    private UIProgressBar? _loadingBar;
    private UILabel? _loadingStatus;
    private UILabel? _debugLabel;
    private UILabel? _bleedLabel;
    private UIProgressBar? _xpBar;
    private UILabel? _xpLabel;
    private UILabel? _comboLabel;
    private UILabel? _sneakIndicator;

    /// <summary>Set true while CTRL is held so HUD can show a sneak indicator. Game.razor pushes this each frame.</summary>
    public bool IsSneaking { get; set; }

    /// <summary>Current kill-streak; Game.razor pushes this each frame so HudService can render the live combo badge.</summary>
    public int CurrentCombo { get; set; }

    /// <summary>UTC expiry of the current combo window; HUD hides the badge once now > this.</summary>
    public DateTime ComboExpiry { get; set; } = DateTime.MinValue;

    /// <summary>Toggle the debug HUD line (FPS + coords + level). Hot key F3.</summary>
    public void ToggleDebug()
    {
        if (_debugLabel is null) return;
        _debugLabel.Visible = !_debugLabel.Visible;
    }

    private UILabel? _helpLabel;
    private UILabel? _achievementsLabel;

    /// <summary>Toggle the J achievement-list overlay.</summary>
    public void ToggleAchievements()
    {
        if (_achievementsLabel is null) return;
        _achievementsLabel.Visible = !_achievementsLabel.Visible;
        if (_achievementsLabel.Visible) RefreshAchievementsList();
    }

    private void RefreshAchievementsList()
    {
        if (_achievementsLabel is null) return;
        // Each row: [X] unlocked or [ ] locked. Format kept short so the
        // panel doesn't overflow.
        string check = "[X]";
        string lockd = "[ ]";
        // Compute count for header.
        int n = (_stats.FirstKillAwarded ? 1 : 0)
              + (_stats.FirstFireAwarded ? 1 : 0)
              + (_stats.FirstCookAwarded ? 1 : 0)
              + (_stats.FirstWolfAwarded ? 1 : 0)
              + (_stats.FirstSleepAwarded ? 1 : 0)
              + (_stats.HunterAwarded ? 1 : 0)
              + (_stats.BowmanAwarded ? 1 : 0)
              + (_stats.FirstAidAwarded ? 1 : 0)
              + (_stats.GourmetAwarded ? 1 : 0)
              + (_stats.VeteranAwarded ? 1 : 0)
              + (_stats.CenturionAwarded ? 1 : 0)
              + (_stats.SurvivorAwarded ? 1 : 0)
              + (_stats.ResilientAwarded ? 1 : 0)
              + (_stats.PackHunterAwarded ? 1 : 0)
              + (_stats.CompletionistAwarded ? 1 : 0);
        var sb = new System.Text.StringBuilder();
        sb.Append($"ACHIEVEMENTS  {n} / 15  ({n * 100 / 15}%)\n\n");
        sb.Append((_stats.FirstKillAwarded ? check : lockd)).Append(" First Kill\n");
        sb.Append((_stats.FirstFireAwarded ? check : lockd)).Append(" First Fire\n");
        sb.Append((_stats.FirstCookAwarded ? check : lockd)).Append(" First Cook\n");
        sb.Append((_stats.FirstWolfAwarded ? check : lockd)).Append(" First Wolf\n");
        sb.Append((_stats.FirstSleepAwarded ? check : lockd)).Append(" First Sleep\n");
        sb.Append((_stats.HunterAwarded ? check : lockd)).Append(" Hunter (Deer)\n");
        sb.Append((_stats.BowmanAwarded ? check : lockd)).Append(" Bowman\n");
        sb.Append((_stats.FirstAidAwarded ? check : lockd)).Append(" First Aid\n");
        sb.Append((_stats.GourmetAwarded ? check : lockd)).Append(" Gourmet (10 cooks)\n");
        sb.Append((_stats.VeteranAwarded ? check : lockd)).Append(" Veteran (Lv 5)\n");
        sb.Append((_stats.CenturionAwarded ? check : lockd)).Append(" Centurion (100 kills)\n");
        sb.Append((_stats.SurvivorAwarded ? check : lockd)).Append(" Survivor (Day 7)\n");
        sb.Append((_stats.ResilientAwarded ? check : lockd)).Append(" Resilient (3 deaths)\n");
        sb.Append((_stats.PackHunterAwarded ? check : lockd)).Append(" Pack Hunter (5 wolves/night)\n");
        sb.Append((_stats.CompletionistAwarded ? check : lockd)).Append(" Completionist (all 14)\n");
        _achievementsLabel.Text = sb.ToString();
    }

    /// <summary>Toggle the F1 controls reference overlay.</summary>
    public void ToggleHelp()
    {
        if (_helpLabel is null) return;
        _helpLabel.Visible = !_helpLabel.Visible;
    }
    private float _fpsSmoothed;
    private float _fpsAccumTime;

    // Last-seen stat values for threshold-cross detection. Start at 1.0 (fed/hydrated)
    // so the first tick below 0.5 fires a "Peckish"/"Moist" toast. Temperature starts
    // at 0.5 (comfortable) so the first cold/hot crossing fires on drift.
    private float _lastHungerSeen = 1f;
    private float _lastThirstSeen = 1f;
    private float _lastTempSeen = 0.5f;
    private float _lastStaminaSeen = 1f;

    public bool IsInitialized { get; private set; }

    /// <summary>UTC timestamp of the most recent successful save. Game.razor
    /// pushes this each frame from SaveService.LastSaveTime so the debug
    /// HUD can display "saved Xs ago". DateTime.MinValue means no save yet.</summary>
    public DateTime LastSaveTime { get; set; } = DateTime.MinValue;

    /// <summary>True while the pause menu is on top of the screen stack.</summary>
    public bool IsPaused => _ui.Screens.ActiveScreen == "pause";

    /// <summary>True while the inventory screen is on top of the screen stack.</summary>
    public bool IsInventoryOpen => _ui.Screens.ActiveScreen == "inventory";

    /// <summary>True while the in-game settings overlay is on top of the screen stack.</summary>
    public bool IsSettingsOpen => _ui.Screens.ActiveScreen == "settings";

    /// <summary>True while the crafting screen is on top of the screen stack.</summary>
    public bool IsCraftingOpen => _ui.Screens.ActiveScreen == "crafting";

    /// <summary>True while the initial terrain-loading overlay is showing.</summary>
    public bool IsLoading => _ui.Screens.ActiveScreen == "loading";

    /// <summary>True while any modal overlay (pause, inventory, settings, loading, crafting, death) covers the HUD.</summary>
    public bool IsAnyMenuOpen => IsPaused || IsInventoryOpen || IsSettingsOpen || IsLoading || IsCraftingOpen || IsDead;

    /// <summary>Fired when the player clicks Resume in the pause menu.</summary>
    public event Action? OnResumeClicked;

    /// <summary>Fired when the player clicks Quit to Menu in the pause menu.</summary>
    public event Action? OnQuitToMenuClicked;

    /// <summary>Fired when the player clicks Respawn on the death screen.</summary>
    public event Action? OnRespawnClicked;

    /// <summary>True while the death screen is on top of the screen stack.</summary>
    public bool IsDead => _ui.Screens.ActiveScreen == "death";

    public HudService(GameUIService ui, PlayerStatsService stats, InventoryService inventory, SettingsService settings, WorldTimeService worldTime, CraftingService crafting, WeatherService weather, EntityService entities, CampfireService fires, GroundItemService ground)
    {
        _ui = ui;
        _stats = stats;
        _inventory = inventory;
        _settings = settings;
        _worldTime = worldTime;
        _crafting = crafting;
        _weather = weather;
        _entities = entities;
        _fires = fires;
        _ground = ground;
        // Seed breath puffs past their lifetime so they aren't rendered as a
        // ghost ring at (0, 0) on the first few frames before the emitter
        // has had a chance to spawn real ones.
        for (int i = 0; i < BreathMax; i++) _breathAge[i] = 2f;
        _inventory.OnInventoryChanged += SyncInventoryToHud;
        _inventory.OnActiveHotbarChanged += SyncActiveHotbar;
        _inventory.OnItemConsumed += HandleItemConsumed;
        _inventory.OnItemPickedUp += HandleItemPickedUp;
        _stats.OnDamageTaken += HandleDamageTaken;
        _stats.OnHealed += HandleHealed;
        _stats.OnLevelUp += HandleLevelUp;
        _weather.OnLightningStrike += HandleLightningStrike;
        _entities.OnEntityKilled += HandleEntityDespawned;
        _ground.OnPickedUpId += HandleLootPickedUpId;
    }

    private void HandleLootPickedUpId(int id)
    {
        // Clear the minimap marker matching the picked-up loot's id. Without
        // this the "loot.N" marker would persist until explicitly pruned.
        Minimap?.RemoveMarker($"loot.{id}");
    }

    private void HandleLevelUp(int newLevel)
    {
        NotifyAchievement($"Level {newLevel}!");
    }

    private void HandleEntityDespawned(WanderingEntity e)
    {
        // Prevent dead-entity markers from sticking on the minimap after
        // the entity is gone. The entity loop in Update only re-adds
        // markers for live entities, so without explicit removal killed
        // entities leave stale dots.
        Minimap?.RemoveMarker($"entity.{e.Id}");
    }

    private void HandleLightningStrike()
    {
        // Bright white + pale-blue flash, 0.25s fade. Alpha deliberately high so
        // the strike reads as a real lightning bolt through closed eyes.
        ScreenOverlay?.Flash(System.Drawing.Color.FromArgb(230, 240, 245, 255), 0.25f);
    }

    private void HandleItemPickedUp(InventoryItem item)
    {
        // Soft green edge flash - confirms the chop paid off without being as
        // loud as a full damage flash. 0.25s fade to match the existing
        // heal flash cadence.
        ScreenOverlay?.Flash(System.Drawing.Color.FromArgb(60, 80, 200, 100), 0.25f);
    }

    private void HandleItemConsumed(string name, string verb, ItemEffect effect)
    {
        // Phrase the toast to match the dominant effect: food shows "+XX% Hunger" etc.
        string bump =
            effect.Hunger > 0 ? $"+{(int)(effect.Hunger * 100)}% Hunger" :
            effect.Thirst > 0 ? $"+{(int)(effect.Thirst * 100)}% Thirst" :
            effect.Health > 0 ? $"+{(int)(effect.Health * 100)}% HP" :
            effect.Stamina > 0 ? $"+{(int)(effect.Stamina * 100)}% Stamina" :
            "";
        string msg = string.IsNullOrEmpty(bump) ? $"{verb} {name}" : $"{verb} {name} ({bump})";
        NotifySuccess(msg);
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
        StatusHUD = new UIStatusHUD { Width = 180, ShowAllPercentages = true };
        SyncStatsToHud();
        root.AddAnchored(StatusHUD, Anchor.BottomLeft, offsetX: 16, offsetY: -16);

        // === XP bar + level label (above status HUD) ===
        _xpBar = new UIProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            ShowPercentage = false,
            Width = 180,
            Height = 8,
        };
        root.AddAnchored(_xpBar, Anchor.BottomLeft, offsetX: 16, offsetY: -160);
        _xpLabel = new UILabel
        {
            Text = "Lv 1",
            FontSize = FontSize.Caption,
            Width = 180,
            Height = 14,
            Align = TextAlign.Center,
            Color = System.Drawing.Color.FromArgb(235, 200, 240, 160),
        };
        root.AddAnchored(_xpLabel, Anchor.BottomLeft, offsetX: 16, offsetY: -170);

        // === Bleed badge (above status HUD) ===
        // Only visible while bleed is active. Pulses red so the player can't
        // miss it. Game.razor's frame Push pulls from PlayerStatsService.
        _bleedLabel = new UILabel
        {
            Text = "",
            FontSize = FontSize.Caption,
            Width = 180,
            Height = 22,
            Align = TextAlign.Center,
            Visible = false,
            Color = System.Drawing.Color.FromArgb(255, 220, 40, 40),
        };
        root.AddAnchored(_bleedLabel, Anchor.BottomLeft, offsetX: 16, offsetY: -200);

        // === Combo badge (above bleed badge) ===
        // Shows live "COMBO Nx  Ys" while a kill chain is active. Fades
        // when the window expires. Game.razor pushes CurrentCombo +
        // ComboExpiry each kill.
        _comboLabel = new UILabel
        {
            Text = "",
            FontSize = FontSize.Caption,
            Width = 180,
            Height = 22,
            Align = TextAlign.Center,
            Visible = false,
            Color = System.Drawing.Color.FromArgb(240, 255, 210, 90),
        };
        root.AddAnchored(_comboLabel, Anchor.BottomLeft, offsetX: 16, offsetY: -225);

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

        // === Interaction prompt (just below crosshair) ===
        // Label shows "[E] Chop" / "Need a Pick" / etc; empty string hides it.
        // Game.razor writes Text every frame based on the current raycast target.
        InteractionPrompt = new UILabel
        {
            Text = "",
            FontSize = FontSize.Caption,
            Width = 200,
            Height = 20,
            Align = TextAlign.Center,
            Color = System.Drawing.Color.FromArgb(220, 230, 230, 240),
        };
        root.AddAnchored(InteractionPrompt, Anchor.Center, offsetY: 28);

        // Sneak indicator below the interaction prompt - small "SNEAK"
        // pill that's only visible when CTRL is held. Player gets a clear
        // confirmation that the stealth state is active.
        _sneakIndicator = new UILabel
        {
            Text = "SNEAK",
            FontSize = FontSize.Caption,
            Width = 80,
            Height = 16,
            Align = TextAlign.Center,
            Color = System.Drawing.Color.FromArgb(220, 150, 200, 230),
            Visible = false,
        };
        root.AddAnchored(_sneakIndicator, Anchor.Center, offsetY: 50);

        // F1 help overlay - hidden by default; ToggleHelp flips visibility.
        _helpLabel = new UILabel
        {
            Text =
                "WASD move  SHIFT sprint  SPACE jump  Scroll change slot\n" +
                "LMB attack / chop  RMB place / shoot bow\n" +
                "E interact  F feed fire / drink  Z sleep near fire\n" +
                "CTRL sneak (slower + halves wolf sensing)\n" +
                "G quick-eat  T quick-drink  Q drop active item\n" +
                "I inventory  J achievements  C craft  ESC pause\n" +
                "F1 help  F3 debug  M mute",
            FontSize = FontSize.Body,
            Width = 560,
            Height = 140,
            Align = TextAlign.Center,
            Color = System.Drawing.Color.FromArgb(230, 230, 230, 240),
            Visible = false,
        };
        root.AddAnchored(_helpLabel, Anchor.Center, offsetY: 60);

        // J achievement-list overlay - hidden by default; ToggleAchievements
        // flips visibility and refreshes the list each open.
        _achievementsLabel = new UILabel
        {
            Text = "",
            FontSize = FontSize.Caption,
            Width = 320,
            Height = 360,
            Align = TextAlign.Left,
            Color = System.Drawing.Color.FromArgb(235, 220, 230, 240),
            Visible = false,
        };
        root.AddAnchored(_achievementsLabel, Anchor.Center, offsetX: -240, offsetY: 0);

        // === Compass (top-center) ===
        Compass = new UICompass { Width = 200 };
        root.AddAnchored(Compass, Anchor.TopCenter, offsetY: 12);

        // === Clock (top-left) - "HH:MM Phase Day N" tick-updated from WorldTimeService ===
        _clockLabel = new UILabel
        {
            Text = "06:00  Dawn  Day 1",
            FontSize = FontSize.Body,
            Width = 260,
            Height = 24,
            Align = TextAlign.Left,
        };
        root.AddAnchored(_clockLabel, Anchor.TopLeft, offsetX: 16, offsetY: 16);

        // === Debug line (below the clock) - FPS + precise coords. Dev-ish but
        // useful for any player who wants to know "where exactly am I?" ===
        _debugLabel = new UILabel
        {
            Text = "",
            FontSize = FontSize.Caption,
            Width = 260,
            Height = 18,
            Align = TextAlign.Left,
            Color = System.Drawing.Color.FromArgb(200, 180, 200, 210),
        };
        root.AddAnchored(_debugLabel, Anchor.TopLeft, offsetX: 16, offsetY: 44);

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

        // === Death screen (pushed from Game.razor on PlayerStats.OnDied) ===
        _ui.Screens.Register("death", BuildDeathScreen());

        // === Crafting screen (pushed by Game.razor on C key) ===
        _ui.Screens.Register("crafting", BuildCraftingScreen());

        // Subscribe to inventory changes so the craft buttons' enabled state
        // tracks available materials in real time.
        _inventory.OnInventoryChanged += RefreshCraftingButtons;
        RefreshCraftingButtons();

        // Push current inventory state into both the persistent hotbar and the
        // inventory screen's grid, so first paint matches the data model.
        SyncInventoryToHud();
    }

    private UILabel? _pauseStatsLabel;

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
            Height = 330,
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

        // Mini stats block at bottom of the pause panel so the player gets
        // a quick run overview without dismissing the menu. Refreshed in
        // ShowPauseMenu so the values are fresh on every open.
        _pauseStatsLabel = new UILabel
        {
            Text = "",
            FontSize = FontSize.Caption,
            Width = 260,
            Height = 60,
            X = 10,
            Y = 250,
            Align = TextAlign.Center,
            Color = System.Drawing.Color.FromArgb(220, 210, 220, 230),
        };
        panel.AddChild(_pauseStatsLabel);

        anchor.AddAnchored(panel, Anchor.Center);
        return anchor;
    }

    private void RefreshPauseStats()
    {
        if (_pauseStatsLabel is null) return;
        int s = (int)_stats.PlayTimeSeconds;
        string time = $"{s / 3600:D2}:{(s % 3600) / 60:D2}:{s % 60:D2}";
        _pauseStatsLabel.Text =
            $"Day {_worldTime.DayNumber}  Lv {_stats.Level}  XP {_stats.Experience}\n" +
            $"Kills {_stats.Kills}  Deaths {_stats.Deaths}  Best Combo {_stats.BestCombo}x\n" +
            $"Playtime {time}";
    }

    /// <summary>Push the pause menu onto the screen stack (dims HUD behind it).</summary>
    public void ShowPauseMenu()
    {
        if (_ui.Screens.ActiveScreen == "pause") return;
        RefreshPauseStats();
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

    /// <summary>Toggle the crafting screen (C key).</summary>
    public void ToggleCrafting()
    {
        if (IsCraftingOpen) _ui.Screens.Pop();
        else _ui.Screens.Push("crafting");
    }

    /// <summary>Pop the crafting screen.</summary>
    public void HideCrafting()
    {
        if (!IsCraftingOpen) return;
        _ui.Screens.Pop();
    }

    private UIElement BuildCraftingScreen()
    {
        var anchor = new UIAnchorPanel
        {
            Width = _renderer!.CanvasWidth,
            Height = _renderer.CanvasHeight,
        };

        int rowCount = _crafting.Recipes.Count;
        float rowHeight = 56;
        float rowGap = 8;
        int panelWidth = 440;
        int panelHeight = (int)(72 + rowCount * (rowHeight + rowGap) + 36);

        var panel = new UIPanel
        {
            Width = panelWidth,
            Height = panelHeight,
            CornerRadius = 10,
        };

        var title = new UILabel
        {
            Text = "CRAFTING",
            FontSize = FontSize.Heading,
            Width = panelWidth,
            Height = 36,
            X = 0,
            Y = 16,
            Align = TextAlign.Center,
        };
        panel.AddChild(title);

        // Build one row per recipe: label (ingredients + output) + craft button.
        // _craftingRows is pre-populated in parallel so RefreshCraftingButtons can
        // flip Enabled per row based on live inventory.
        _craftingRows.Clear();
        for (int i = 0; i < rowCount; i++)
        {
            var recipe = _crafting.Recipes[i];
            float rowY = 64 + i * (rowHeight + rowGap);

            string ingredients = string.Join(" + ",
                recipe.Inputs.Select(inp => $"{inp.Count}x {ItemShortName(inp.Id)}"));
            var status = new UILabel
            {
                Text = $"{ingredients}  ->  {recipe.Output.Name}",
                FontSize = FontSize.Body,
                Width = 260,
                Height = (int)rowHeight,
                X = 20,
                Y = (int)rowY + 14,
                Align = TextAlign.Left,
            };
            panel.AddChild(status);

            var button = new UIButton
            {
                Text = recipe.DisplayName,
                Width = 120,
                Height = (int)rowHeight,
                X = panelWidth - 140,
                Y = (int)rowY,
            };
            var captured = recipe; // avoid loop variable capture
            button.OnClick = () =>
            {
                if (_crafting.TryCraft(captured))
                    NotifySuccess($"Crafted {captured.Output.Name}");
                else
                    NotifyWarning($"Missing materials for {captured.DisplayName}");
            };
            panel.AddChild(button);

            _craftingRows.Add((button, status));
        }

        var closeHint = new UILabel
        {
            Text = "Press C or Esc to close",
            FontSize = FontSize.Caption,
            Width = panelWidth,
            Height = 20,
            X = 0,
            Y = panelHeight - 26,
            Align = TextAlign.Center,
        };
        panel.AddChild(closeHint);

        anchor.AddAnchored(panel, Anchor.Center);
        return anchor;
    }

    private void RefreshCraftingButtons()
    {
        if (_craftingRows.Count == 0) return;
        for (int i = 0; i < _craftingRows.Count && i < _crafting.Recipes.Count; i++)
        {
            var recipe = _crafting.Recipes[i];
            bool can = _crafting.CanCraft(recipe);
            _craftingRows[i].Button.Enabled = can;
            // Level-gate hint: if the player is below RequiredLevel the
            // status label surfaces "Needs Lv N" instead of a generic
            // shortage message. Helps the player plan advancement.
            if (!can && _stats.Level < recipe.RequiredLevel)
            {
                _craftingRows[i].Status.Text = $"Needs Lv {recipe.RequiredLevel}";
                _craftingRows[i].Status.Color = System.Drawing.Color.FromArgb(
                    220, 240, 170, 70);
            }
        }
    }

    // Pretty-print an item id for recipe rows. "material.wood" -> "Wood".
    private static string ItemShortName(string id)
    {
        int dot = id.IndexOf('.');
        string tail = dot >= 0 && dot + 1 < id.Length ? id[(dot + 1)..] : id;
        return tail.Length == 0 ? id : char.ToUpper(tail[0]) + tail[1..];
    }

    /// <summary>Push the death screen. Call from PlayerStats.OnDied.</summary>
    public void ShowDeathScreen()
    {
        if (_ui.Screens.ActiveScreen == "death") return;

        // Refresh the stats summary before pushing so the latest lifetime
        // counts show up. A dedicated label keeps the constructor / push
        // logic cheap - we only recompute on death, not every frame.
        if (_deathStats is not null)
        {
            int s = (int)_stats.PlayTimeSeconds;
            string time = $"{s / 3600:D2}:{(s % 3600) / 60:D2}:{s % 60:D2}";
            // Count achievement bools to show progress out of 15.
            int achvs = (_stats.FirstKillAwarded ? 1 : 0)
                      + (_stats.FirstFireAwarded ? 1 : 0)
                      + (_stats.FirstCookAwarded ? 1 : 0)
                      + (_stats.FirstWolfAwarded ? 1 : 0)
                      + (_stats.FirstSleepAwarded ? 1 : 0)
                      + (_stats.VeteranAwarded ? 1 : 0)
                      + (_stats.CenturionAwarded ? 1 : 0)
                      + (_stats.SurvivorAwarded ? 1 : 0)
                      + (_stats.BowmanAwarded ? 1 : 0)
                      + (_stats.HunterAwarded ? 1 : 0)
                      + (_stats.GourmetAwarded ? 1 : 0)
                      + (_stats.ResilientAwarded ? 1 : 0)
                      + (_stats.FirstAidAwarded ? 1 : 0)
                      + (_stats.PackHunterAwarded ? 1 : 0)
                      + (_stats.CompletionistAwarded ? 1 : 0);
            _deathStats.Text =
                $"Lv {_stats.Level}  XP {_stats.Experience}  T {time}  Deaths {_stats.Deaths}  Best Combo {_stats.BestCombo}x\n" +
                $"Day {_worldTime.DayNumber}  Kills: {_stats.Kills}   R:{_stats.RabbitKills}  B:{_stats.BoarKills}  C:{_stats.CrowKills}  W:{_stats.WolfKills}  D:{_stats.DeerKills}\n" +
                $"Achievements: {achvs} / 15";
        }

        _ui.Screens.Push("death");
        // Monochrome tint so the world behind is visibly frozen.
        ScreenOverlay?.SetPersistent("death",
            System.Drawing.Color.FromArgb(100, 30, 10, 10));
    }

    private UILabel? _deathStats;

    /// <summary>Pop the death screen and clear the death tint.</summary>
    public void HideDeathScreen()
    {
        if (_ui.Screens.ActiveScreen != "death") return;
        _ui.Screens.Pop();
        ScreenOverlay?.ClearPersistent("death");
    }

    private UIElement BuildDeathScreen()
    {
        var anchor = new UIAnchorPanel
        {
            Width = _renderer!.CanvasWidth,
            Height = _renderer.CanvasHeight,
        };

        var panel = new UIPanel
        {
            Width = 360,
            Height = 330,
            CornerRadius = 10,
        };

        var title = new UILabel
        {
            Text = "YOU DIED",
            FontSize = FontSize.Title,
            Width = 360,
            Height = 48,
            X = 0,
            Y = 24,
            Align = TextAlign.Center,
            Color = System.Drawing.Color.FromArgb(255, 240, 60, 60),
        };
        panel.AddChild(title);

        var sub = new UILabel
        {
            Text = "The wasteland claimed you.",
            FontSize = FontSize.Caption,
            Width = 360,
            Height = 20,
            X = 0,
            Y = 78,
            Align = TextAlign.Center,
        };
        panel.AddChild(sub);

        // Lifetime-stats summary. Populated each time ShowDeathScreen runs
        // so the Level / XP / Kills breakdown matches the moment of death.
        _deathStats = new UILabel
        {
            Text = "",
            FontSize = FontSize.Body,
            Width = 360,
            Height = 80,
            X = 0,
            Y = 110,
            Align = TextAlign.Center,
            Color = System.Drawing.Color.FromArgb(230, 220, 210, 200),
        };
        panel.AddChild(_deathStats);

        var respawn = new UIButton
        {
            Text = "Respawn",
            Width = 220,
            Height = 48,
            X = 70,
            Y = 250,
        };
        respawn.OnClick = () => OnRespawnClicked?.Invoke();
        panel.AddChild(respawn);

        anchor.AddAnchored(panel, Anchor.Center);
        return anchor;
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

        // Random tip from a small pool. Picked once per loading screen
        // construction; the screen builds fresh each Init so each game
        // launch sees a different tip.
        var tips = new[]
        {
            "F1 shows the controls reference.",
            "Z near a fueled fire skips you to dawn.",
            "Wolves only spawn at night - stay near a fire.",
            "Cooked meat heals + fills hunger more than raw.",
            "Q drops the active hotbar item to the ground.",
            "Boars charge if you damage them - back off or commit.",
            "Feathers from crows are arrows for the bow.",
            "Fur Coat gives passive warmth wherever you are.",
            "Stone Axe / Pick at Level 3 doubles your durability.",
            "G quick-eats the best food in your inventory.",
            "3 Leaves crafts a Field Bandage - cures bleeding.",
            "Wolf bites cause bleeding - sleep is blocked until treated.",
            "Chaining kills within 8s gives bonus XP (up to 3x).",
            "Nights get deadlier past Day 4. Past Day 7 it's brutal.",
            "Near a campfire wolves have to get closer before aggroing.",
            "Cooked meat also warms you. Water cools you.",
            "Lightning strikes during storms take 10% off every fire.",
            "Pack Hunter: kill 5 wolves in one night for the achievement.",
        };
        var tipLabel = new UILabel
        {
            Text = "Tip: " + tips[new Random().Next(tips.Length)],
            FontSize = FontSize.Caption,
            Width = 520,
            Height = 20,
            X = 0,
            Y = 168,
            Align = TextAlign.Center,
            Color = System.Drawing.Color.FromArgb(220, 200, 220, 240),
        };
        panel.AddChild(tipLabel);

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
        // Right-click / secondary action: consume the item if it has an effect entry.
        // Silently ignores non-consumables so clicking an Axe doesn't spam warnings.
        _backpackGrid.OnCellSecondary = idx => _inventory.TryUseSlot(false, idx);
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
        _inventoryHotbarRow.OnCellSecondary = idx => _inventory.TryUseSlot(true, idx);
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

    /// <summary>
    /// Project active campfires into screen space and render them as a stack
    /// of pulsing orange rects that fake a flame. Drawn BEFORE entities so a
    /// wandering creature can block line-of-sight to the fire without its
    /// flame leaking over the billboard. Particles are procedural per-frame -
    /// the phase jitter combined with the sin() gives enough randomness for
    /// the pulse to feel alive without any particle state.
    /// </summary>
    private void DrawCampfires(int viewportWidth, int viewportHeight)
    {
        if (_renderer == null || _fires.Fires.Count == 0) return;

        float aspect = (float)viewportWidth / viewportHeight;
        var vp = _renderer.Camera.GetVpMatrix(aspect);

        float t = (float)(DateTime.UtcNow.Ticks % TimeSpan.TicksPerSecond) / TimeSpan.TicksPerSecond;
        float pulse = 0.8f + 0.2f * MathF.Sin(t * MathF.PI * 2f);

        foreach (var f in _fires.Fires)
        {
            var worldPos = new System.Numerics.Vector4(
                f.Position.X, f.Position.Y + 0.4f, f.Position.Z, 1f);
            var clip = System.Numerics.Vector4.Transform(worldPos, vp);
            if (clip.W <= 0.001f) continue;

            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            float screenX = (ndcX * 0.5f + 0.5f) * viewportWidth;
            float screenY = (1f - (ndcY * 0.5f + 0.5f)) * viewportHeight;
            if (screenX < -80 || screenX > viewportWidth + 80) continue;
            if (screenY < -80 || screenY > viewportHeight + 80) continue;

            float dist = MathF.Max(0.1f, clip.W);
            float size = Math.Clamp(120f / dist, 10f, 90f) * pulse;

            // Ember glow survives to 0 so extinguished fires are still visible
            // on the ground as a dead pile; flame + tip fade with fuel so a
            // low-fuel fire reads as almost out at a glance.
            int flameAlpha = (int)Math.Clamp(220 * f.Fuel, 0, 220);
            int tipAlpha   = (int)Math.Clamp(200 * f.Fuel, 0, 200);
            var ember  = System.Drawing.Color.FromArgb(220, 120, 30, 10);
            var flame  = System.Drawing.Color.FromArgb(flameAlpha, 240, 140, 40);
            var tip    = System.Drawing.Color.FromArgb(tipAlpha, 255, 220, 120);

            float x = screenX - size / 2f;
            float yBottom = screenY + size * 0.3f;

            // 3 stacked rects fake flame layers without needing a particle system.
            _ui.Renderer.DrawRect(x, yBottom - size * 0.30f, size, size * 0.30f, ember);
            _ui.Renderer.DrawRect(
                x + size * 0.15f, yBottom - size * 0.60f,
                size * 0.70f, size * 0.30f, flame);
            _ui.Renderer.DrawRect(
                x + size * 0.32f, yBottom - size * 0.85f,
                size * 0.36f, size * 0.25f, tip);

            // Smoke wisps: 3 gray puffs rising above the flame, each phase-
            // offset so they don't stack. Higher rects = older = lower alpha.
            // Only render for fueled fires - extinguished piles don't smoke.
            // Low-fuel fires smoke darker + denser (dying embers produce
            // oily smoke); fresh fires smoke paler.
            if (f.Fuel > 0.02f)
            {
                float smokeTime = t * 3f + f.Id * 0.7f;
                // Tint darker as fuel drops. 170 at full fuel, 80 at empty.
                byte smokeTint = (byte)(80 + (int)(f.Fuel * 90f));
                for (int s = 0; s < 3; s++)
                {
                    float phase = (smokeTime + s * 0.33f) % 1f;
                    float riseY = phase * size * 2.2f;
                    float drift = MathF.Sin(phase * 6f + s) * size * 0.15f;
                    // Dying fires smoke more visibly. Multiplier grows as
                    // fuel drops so a low-fuel fire actually smokes MORE.
                    float densityMul = 1f + (1f - f.Fuel) * 0.6f;
                    int sAlpha = (int)(120 * (1f - phase) * f.Fuel * densityMul);
                    if (sAlpha <= 0) continue;
                    var smoke = System.Drawing.Color.FromArgb(sAlpha, smokeTint, smokeTint, (byte)(smokeTint + 5));
                    _ui.Renderer.DrawRect(
                        x + size * 0.35f + drift,
                        yBottom - size * 0.95f - riseY,
                        size * 0.30f, size * 0.18f, smoke);
                }
            }

            // Fuel bar under the ember. Always shown so the player can see
            // that a fire needs feeding before it's too late. Bar color
            // shifts red at low fuel so the warning reads at a glance.
            float barW = size;
            float barH = 4f;
            float barX = x;
            float barY = yBottom + 2f;
            _ui.Renderer.DrawRect(barX, barY, barW, barH,
                System.Drawing.Color.FromArgb(180, 20, 20, 20));
            float fuelRatio = Math.Clamp(f.Fuel, 0, 1);
            System.Drawing.Color fuelColor = fuelRatio < 0.20f
                ? System.Drawing.Color.FromArgb(230, 230, 70, 50)
                : fuelRatio < 0.45f
                    ? System.Drawing.Color.FromArgb(230, 240, 180, 60)
                    : System.Drawing.Color.FromArgb(230, 140, 230, 90);
            _ui.Renderer.DrawRect(barX, barY, barW * fuelRatio, barH, fuelColor);

            // Cook progress: thin orange bar below the fuel bar, only
            // visible for the closest active fire (where cook progress
            // actually ticks). Shows "X% to cooked" at a glance.
            var nearestFire = _fires.FindNearest(_renderer.Camera.Position);
            if (nearestFire == f && _fires.CookProgressRatio > 0.01f)
            {
                float cookBarY = barY + barH + 1f;
                float cookRatio = Math.Clamp(_fires.CookProgressRatio, 0f, 1f);
                _ui.Renderer.DrawRect(barX, cookBarY, barW, 3f,
                    System.Drawing.Color.FromArgb(140, 20, 20, 20));
                _ui.Renderer.DrawRect(barX, cookBarY, barW * cookRatio, 3f,
                    System.Drawing.Color.FromArgb(230, 240, 130, 40));
            }
        }
    }

    /// <summary>
    /// Narrow durability bar painted just above the active hotbar slot when
    /// the equipped item has a UsesRemaining counter. Fill ratio = remaining
    /// / 100 (the starter use count; good-enough assumption since all tools
    /// spawn at 100 - a proper scheme would store MaxUses per-item).
    ///
    /// Position is computed from the hotbar's known layout (9 slots @ 48px
    /// + 4px gap, anchored BottomCenter -12) so we don't have to route the
    /// slot rect back out of UIHotbar.
    /// </summary>
    private void DrawDurabilityBar(int viewportWidth, int viewportHeight)
    {
        var active = _inventory.ActiveItem;
        if (active is null || active.UsesRemaining is null) return;

        int remaining = active.UsesRemaining.Value;
        const int assumedMax = 100;
        float ratio = Math.Clamp(remaining / (float)assumedMax, 0f, 1f);

        const float slotSize = 48f;
        const float slotGap = 4f;
        const int slotCount = 9;
        float rowWidth = slotCount * slotSize + (slotCount - 1) * slotGap;
        float leftEdge = (viewportWidth - rowWidth) * 0.5f;
        float slotStride = slotSize + slotGap;
        float slotX = leftEdge + _inventory.ActiveHotbarIndex * slotStride;

        // Bar sits in the top ~3px of the slot box. Hotbar bottom = vh-12;
        // top = vh-12-slotSize = vh-60. Bar at y = vh-60+2 for a 3px slice.
        float barY = viewportHeight - 60 + 2;
        float barH = 3f;
        float barW = slotSize - 4;
        float barX = slotX + 2;

        _ui.Renderer.DrawRect(barX, barY, barW, barH,
            System.Drawing.Color.FromArgb(200, 30, 30, 30));

        // Color ramps green -> yellow -> red as durability drops; below
        // ~15% the red also pulses so imminent breakage is impossible to
        // miss in peripheral vision.
        System.Drawing.Color full;
        if (ratio > 0.6f) full = System.Drawing.Color.FromArgb(230, 80, 220, 120);
        else if (ratio > 0.3f) full = System.Drawing.Color.FromArgb(230, 220, 200, 80);
        else if (ratio > 0.15f) full = System.Drawing.Color.FromArgb(230, 230, 80, 80);
        else
        {
            float pulse = 0.55f + 0.45f * MathF.Sin(
                (float)Environment.TickCount * 0.012f);
            int a = (int)(255 * pulse);
            full = System.Drawing.Color.FromArgb(a, 255, 40, 40);
        }
        _ui.Renderer.DrawRect(barX, barY, barW * ratio, barH, full);
    }

    /// <summary>
    /// Project each GroundItem into screen space and draw a small colored
    /// marker so the player can see dropped loot before they step on it.
    /// Color comes from the item's ItemCategory via the existing GlyphColor
    /// table so Food bags look orange, Material bags tan, etc.
    /// </summary>
    private void DrawGroundItems(int viewportWidth, int viewportHeight)
    {
        if (_renderer == null || _ground.Items.Count == 0) return;

        float aspect = (float)viewportWidth / viewportHeight;
        var vp = _renderer.Camera.GetVpMatrix(aspect);

        foreach (var g in _ground.Items)
        {
            var worldPos = new System.Numerics.Vector4(
                g.Position.X, g.Position.Y + 0.2f, g.Position.Z, 1f);
            var clip = System.Numerics.Vector4.Transform(worldPos, vp);
            if (clip.W <= 0.001f) continue;

            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            float screenX = (ndcX * 0.5f + 0.5f) * viewportWidth;
            float screenY = (1f - (ndcY * 0.5f + 0.5f)) * viewportHeight;
            if (screenX < -50 || screenX > viewportWidth + 50) continue;
            if (screenY < -50 || screenY > viewportHeight + 50) continue;

            float dist = MathF.Max(0.1f, clip.W);
            float size = Math.Clamp(32f / dist, 4f, 22f);

            var tint = CategoryColor(g.Payload)
                       ?? System.Drawing.Color.FromArgb(230, 220, 220, 220);

            float x = screenX - size / 2f;
            float y = screenY - size / 2f;

            // Pulsing halo behind the bag so pickups stand out even in tall
            // grass. Sin wave keyed to wall-clock time so all loot pulses in
            // unison - reads as "drop" not "particle".
            double t = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
            float pulse = 0.5f + 0.5f * MathF.Sin((float)t * 3f);
            int haloAlpha = (int)(55 + 35 * pulse);
            float haloGrow = size * 0.4f * pulse;
            _ui.Renderer.DrawRect(
                x - haloGrow, y - haloGrow,
                size + haloGrow * 2, size + haloGrow * 2,
                System.Drawing.Color.FromArgb(haloAlpha, tint.R, tint.G, tint.B));

            _ui.Renderer.DrawRect(x, y, size, size, tint);
            // Dark bottom shadow so the bag reads as sitting on ground not floating.
            _ui.Renderer.DrawRect(x, y + size, size, 2f,
                System.Drawing.Color.FromArgb(180, 10, 10, 15));
        }
    }

    /// <summary>
    /// Render impact markers. White crosses that expand and fade over ~350ms
    /// using wall-clock time so the animation runs independently of the main
    /// update tick. Past-lifetime slots skip projection + draw.
    /// </summary>
    private void DrawImpactMarks(int viewportWidth, int viewportHeight)
    {
        if (_renderer == null) return;
        float aspect = (float)viewportWidth / viewportHeight;
        var vp = _renderer.Camera.GetVpMatrix(aspect);
        var now = DateTime.UtcNow;
        for (int i = 0; i < ImpactMax; i++)
        {
            if (_impactSpawned[i] == default) continue;
            float age = (float)(now - _impactSpawned[i]).TotalSeconds;
            if (age >= 0.35f) continue;

            var worldPos = new System.Numerics.Vector4(
                _impactPos[i].X, _impactPos[i].Y + 0.8f, _impactPos[i].Z, 1f);
            var clip = System.Numerics.Vector4.Transform(worldPos, vp);
            if (clip.W <= 0.001f) continue;
            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            float screenX = (ndcX * 0.5f + 0.5f) * viewportWidth;
            float screenY = (1f - (ndcY * 0.5f + 0.5f)) * viewportHeight;

            float life = age / 0.35f;
            int alpha = (int)(255 * (1f - life));
            float size = 10f + life * 20f;
            var color = System.Drawing.Color.FromArgb(alpha, 255, 255, 255);
            _ui.Renderer.DrawRect(screenX - size * 0.5f, screenY - 1f, size, 2f, color);
            _ui.Renderer.DrawRect(screenX - 1f, screenY - size * 0.5f, 2f, size, color);
        }
    }

    /// <summary>
    /// Simple held-tool "viewmodel" painted in the bottom-right. Category
    /// color drives the tint; the bob phase from camera bob gives it a
    /// subtle walk sway. No actual 3D model - just a tilted rect that reads
    /// as "something's in my hand".
    /// </summary>
    private void DrawViewmodel(int viewportWidth, int viewportHeight)
    {
        var item = _inventory.ActiveItem;
        if (item is null) return;

        // Only render for categories that represent held tools / placeables.
        // Food items don't need a held appearance (the consume flow fires
        // its own audio + toast).
        bool held = item.Category == ItemCategory.Tool
                 || item.Category == ItemCategory.Marker
                 || item.Category == ItemCategory.Medical;
        if (!held) return;

        var tint = CategoryColor(item)
                   ?? System.Drawing.Color.FromArgb(220, 210, 210, 220);

        float baseX = viewportWidth - 220f;
        float baseY = viewportHeight - 210f;
        // Tie subtle bob to camera-bob current X/Y so the viewmodel sways
        // with the player's walk cycle; idle ~zero.
        float bobX = _bobCurrentX * 6f;
        float bobY = _bobCurrentY * 6f;

        // One-shot swing arc - quick up + over, recovery back to rest.
        float swingAge = (float)(DateTime.UtcNow - _swingStart).TotalSeconds;
        if (swingAge >= 0 && swingAge < 0.22f)
        {
            float t = swingAge / 0.22f;           // 0..1
            // Ease-out: fast outbound, slow return. Parabola peak at t=0.4.
            float arc = MathF.Sin(t * MathF.PI);  // 0->1->0 across 0..1
            bobX -= arc * 40f;
            bobY -= arc * 60f;
        }

        // Main tool body (rotated-looking via an offset rect)
        _ui.Renderer.DrawRect(baseX + bobX + 20, baseY + bobY, 80, 120, tint);
        _ui.Renderer.DrawRect(baseX + bobX + 10, baseY + bobY + 30, 100, 18,
            System.Drawing.Color.FromArgb(240, 90, 60, 30)); // wood haft crossbar

        // Item label above the viewmodel rect
        float textPx = 16f;
        float textW = _ui.Renderer.MeasureText(item.Name, textPx);
        _ui.Renderer.DrawText(
            item.Name, baseX + bobX + 60 - textW * 0.5f, baseY + bobY - 18,
            textPx, System.Drawing.Color.FromArgb(230, 230, 230, 240));
    }

    // Camera-bob offset fields owned by Game.razor, shared for the viewmodel
    // sway. Setters kept internal so callers can push their current tick
    // offsets without exposing raw mutable state.
    private float _bobCurrentX;
    private float _bobCurrentY;
    public void SetBobOffset(float bobX, float bobY)
    {
        _bobCurrentX = bobX;
        _bobCurrentY = bobY;
    }

    // Viewmodel swing offset - timestamp-driven so the animation runs on
    // wall clock (same pattern as impact marks) without extra tick plumbing.
    private DateTime _swingStart;
    /// <summary>Trigger a one-shot viewmodel swing animation. Duration ~220ms.</summary>
    public void TriggerSwing() => _swingStart = DateTime.UtcNow;

    /// <summary>
    /// Render rising floating damage numbers above hit positions. Each number
    /// rises ~30px and fades over ~900ms. Past-lifetime slots skip.
    /// </summary>
    private void DrawDamageNumbers(int viewportWidth, int viewportHeight)
    {
        if (_renderer == null) return;
        float aspect = (float)viewportWidth / viewportHeight;
        var vp = _renderer.Camera.GetVpMatrix(aspect);
        var now = DateTime.UtcNow;
        for (int i = 0; i < DamageNumberMax; i++)
        {
            if (_dmgSpawned[i] == default) continue;
            float age = (float)(now - _dmgSpawned[i]).TotalSeconds;
            if (age >= 0.9f) continue;

            var worldPos = new System.Numerics.Vector4(
                _dmgPos[i].X, _dmgPos[i].Y + 1.4f, _dmgPos[i].Z, 1f);
            var clip = System.Numerics.Vector4.Transform(worldPos, vp);
            if (clip.W <= 0.001f) continue;
            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            float screenX = (ndcX * 0.5f + 0.5f) * viewportWidth;
            float screenY = (1f - (ndcY * 0.5f + 0.5f)) * viewportHeight;

            float life = age / 0.9f;
            int alpha = (int)(255 * (1f - life));
            float rise = life * 30f;
            string txt = $"-{(int)(_dmgValue[i] * 100)}";
            float pixelSize = 18f;
            float tw = _ui.Renderer.MeasureText(txt, pixelSize);
            _ui.Renderer.DrawText(
                txt,
                screenX - tw * 0.5f,
                screenY - rise,
                pixelSize,
                System.Drawing.Color.FromArgb(alpha, 240, 80, 70));
        }
    }

    private void DrawBloodSplatters(int viewportWidth, int viewportHeight)
    {
        if (_renderer == null) return;
        float aspect = (float)viewportWidth / viewportHeight;
        var vp = _renderer.Camera.GetVpMatrix(aspect);
        var now = DateTime.UtcNow;
        const float Lifetime = 0.8f;
        for (int i = 0; i < BloodMax; i++)
        {
            if (_bloodSpawned[i] == default) continue;
            float age = (float)(now - _bloodSpawned[i]).TotalSeconds;
            if (age >= Lifetime) continue;

            // Integrate position: initial splatter + vel*t + gravity*t^2.
            var p = _bloodPos[i];
            var v = _bloodVel[i];
            float wx = p.X + v.X * age;
            float wy = p.Y + v.Y * age - 9f * age * age * 0.5f;
            float wz = p.Z + v.Z * age;

            var clip = System.Numerics.Vector4.Transform(
                new System.Numerics.Vector4(wx, wy, wz, 1f), vp);
            if (clip.W <= 0.001f) continue;
            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            float screenX = (ndcX * 0.5f + 0.5f) * viewportWidth;
            float screenY = (1f - (ndcY * 0.5f + 0.5f)) * viewportHeight;
            if (screenX < -10 || screenX > viewportWidth + 10) continue;
            if (screenY < -10 || screenY > viewportHeight + 10) continue;

            float life = age / Lifetime;
            int alpha = (int)(220 * (1f - life));
            float size = Math.Clamp(8f / MathF.Max(0.1f, clip.W), 2f, 6f);
            _ui.Renderer.DrawRect(
                screenX - size * 0.5f, screenY - size * 0.5f, size, size,
                System.Drawing.Color.FromArgb(alpha, 160, 20, 20));
        }
    }

    private void DrawEntityBillboards(int viewportWidth, int viewportHeight)
    {
        if (_renderer == null || _entities.Entities.Count == 0) return;

        // Rebuild VP matrix exactly like RenderService does so billboards line
        // up with what the voxel pipeline drew this frame.
        float aspect = (float)viewportWidth / viewportHeight;
        var vp = _renderer.Camera.GetVpMatrix(aspect);

        foreach (var e in _entities.Entities)
        {
            // Offset the sample point up a bit so the billboard tracks the
            // entity's "chest" rather than its feet - reads more natural in view.
            var worldPos = new System.Numerics.Vector4(
                e.Position.X, e.Position.Y + 0.8f, e.Position.Z, 1f);

            var clip = System.Numerics.Vector4.Transform(worldPos, vp);
            if (clip.W <= 0.001f) continue; // behind camera

            // Clip -> NDC -> pixel. Y flipped (voxel pipeline uses reversed Z /
            // row-major matmul convention).
            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            float screenX = (ndcX * 0.5f + 0.5f) * viewportWidth;
            float screenY = (1f - (ndcY * 0.5f + 0.5f)) * viewportHeight;
            if (screenX < -50 || screenX > viewportWidth + 50) continue;
            if (screenY < -50 || screenY > viewportHeight + 50) continue;

            // Size shrinks with distance but clamps so close-ups aren't obscene.
            // Tough wolves (day-scaled HP boost) render visibly larger so
            // alphas in the late-game pack don't look identical to the
            // Day 1 baseline. Scales by MaxHealth / 1.8 (base wolf HP).
            float dist = MathF.Max(0.1f, clip.W);
            float size = Math.Clamp(90f / dist, 6f, 60f);
            if (e.Kind == EntityKind.Wolf && e.MaxHealth > 1.8f)
            {
                float hpScale = MathF.Min(e.MaxHealth / 1.8f, 1.6f);
                size *= hpScale;
            }

            // Per-kind color. A recent hit (HitFlashTimer > 0) overrides with
            // a bright white flash so the player gets a clear "swing landed"
            // confirmation before the billboard settles back to the kind tint.
            // Per-instance ColorJitter tweaks RGB by +/- 25 so a herd doesn't
            // look like clones - one rabbit might be lighter, another darker.
            int jitter = (int)((e.ColorJitter - 0.5f) * 50f);
            var color = e.HitFlashTimer > 0
                ? System.Drawing.Color.FromArgb(250, 255, 255, 255)
                : e.Kind switch
            {
                EntityKind.Boar   => System.Drawing.Color.FromArgb(230, Math.Clamp(140 + jitter, 80, 200), Math.Clamp(90 + jitter, 50, 150), Math.Clamp(60 + jitter / 2, 30, 110)),
                EntityKind.Crow   => System.Drawing.Color.FromArgb(230, Math.Clamp(30 + jitter / 2, 10, 80), Math.Clamp(30 + jitter / 2, 10, 80), Math.Clamp(30 + jitter / 2, 10, 80)),
                EntityKind.Wolf   => System.Drawing.Color.FromArgb(230, Math.Clamp(130 + jitter, 80, 200), Math.Clamp(130 + jitter, 80, 200), Math.Clamp(150 + jitter, 100, 220)),
                EntityKind.Deer   => System.Drawing.Color.FromArgb(230, Math.Clamp(170 + jitter, 110, 220), Math.Clamp(120 + jitter, 70, 180), Math.Clamp(80 + jitter, 40, 140)),
                _                 => System.Drawing.Color.FromArgb(230, Math.Clamp(200 + jitter, 140, 240), Math.Clamp(190 + jitter, 130, 230), Math.Clamp(180 + jitter, 120, 220)),
            };

            float x = screenX - size / 2f;
            float y = screenY - size / 2f;

            // Ground shadow: a dark translucent oval beneath the billboard
            // that helps the entity read as "standing on terrain" rather
            // than "floating". Crows skip this because they ARE floating.
            // Shadow offset shifts left/right based on the sun's screen
            // x-position so shadows actually point away from the light.
            if (e.Kind != EntityKind.Crow)
            {
                float shadowY = y + size + size * 0.05f;
                float shadowW = size * 0.9f;
                float shadowH = size * 0.15f;
                // Sun arc angle: 0 at dawn, 1 at dusk (for sun-visible window).
                float frac = _worldTime.DayFraction;
                float sunU = (frac - 0.05f) / 0.50f;
                float shadowOffset = 0f;
                if (sunU > 0 && sunU < 1)
                    shadowOffset = (sunU - 0.5f) * size * -0.4f; // sun on right -> shadow on left
                _ui.Renderer.DrawRect(
                    x + (size - shadowW) * 0.5f + shadowOffset, shadowY,
                    shadowW, shadowH,
                    System.Drawing.Color.FromArgb(110, 10, 10, 10));
            }

            // Per-kind silhouette: extra rects that stick out above / below
            // the body to suggest ears, snout, wings, etc. All decorations
            // use the same body color so hit flashes apply uniformly.
            switch (e.Kind)
            {
                case EntityKind.Rabbit:
                    // Body + two small ears on top.
                    _ui.Renderer.DrawRect(x, y, size, size, color);
                    float earW = size * 0.18f;
                    float earH = size * 0.35f;
                    _ui.Renderer.DrawRect(x + size * 0.20f, y - earH, earW, earH, color);
                    _ui.Renderer.DrawRect(x + size * 0.62f, y - earH, earW, earH, color);
                    break;
                case EntityKind.Boar:
                    // Wider body + snout sticking right.
                    _ui.Renderer.DrawRect(x, y + size * 0.1f, size, size * 0.9f, color);
                    _ui.Renderer.DrawRect(x + size * 0.85f, y + size * 0.35f, size * 0.25f, size * 0.35f, color);
                    break;
                case EntityKind.Crow:
                    // Narrow body + two wing rects flared down-left/right.
                    _ui.Renderer.DrawRect(x + size * 0.3f, y, size * 0.4f, size, color);
                    _ui.Renderer.DrawRect(x, y + size * 0.4f, size * 0.4f, size * 0.3f, color);
                    _ui.Renderer.DrawRect(x + size * 0.6f, y + size * 0.4f, size * 0.4f, size * 0.3f, color);
                    break;
                case EntityKind.Deer:
                    // Tall narrow body + small head on top + two antler prongs.
                    _ui.Renderer.DrawRect(x + size * 0.15f, y + size * 0.25f, size * 0.7f, size * 0.75f, color);
                    _ui.Renderer.DrawRect(x + size * 0.30f, y, size * 0.40f, size * 0.30f, color);
                    // Antlers - thin verticals above head, slightly splayed.
                    _ui.Renderer.DrawRect(x + size * 0.32f, y - size * 0.25f, size * 0.06f, size * 0.25f, color);
                    _ui.Renderer.DrawRect(x + size * 0.62f, y - size * 0.25f, size * 0.06f, size * 0.25f, color);
                    break;
                case EntityKind.Wolf:
                    // Body + head (upper-left block) + tail (upper-right thin).
                    _ui.Renderer.DrawRect(x, y + size * 0.2f, size, size * 0.8f, color);
                    _ui.Renderer.DrawRect(x - size * 0.1f, y, size * 0.45f, size * 0.45f, color);
                    _ui.Renderer.DrawRect(x + size * 0.95f, y + size * 0.15f, size * 0.18f, size * 0.35f, color);
                    // Glowing red eyes at night - tiny pulsing dots centered
                    // on the head block. Pulse phase derived from entity Id
                    // so packs of wolves don't flicker in unison.
                    if (_worldTime.IsNight && e.HitFlashTimer <= 0)
                    {
                        float eyePulse = 0.7f + 0.3f * MathF.Sin(
                            (float)Environment.TickCount * 0.008f + e.Id);
                        int eyeAlpha = (int)(230 * eyePulse);
                        var eyeColor = System.Drawing.Color.FromArgb(eyeAlpha, 255, 50, 30);
                        float eyeSize = MathF.Max(2f, size * 0.08f);
                        _ui.Renderer.DrawRect(
                            x + size * 0.02f, y + size * 0.15f, eyeSize, eyeSize, eyeColor);
                        _ui.Renderer.DrawRect(
                            x + size * 0.22f, y + size * 0.15f, eyeSize, eyeSize, eyeColor);
                    }
                    break;
                default:
                    _ui.Renderer.DrawRect(x, y, size, size, color);
                    break;
            }

            // Thin dark outline around the bounding box of the main body so
            // the silhouette pops against terrain. Extra-limb rects don't
            // get their own outline - keeps the code simple + reads fine.
            var border = System.Drawing.Color.FromArgb(200, 10, 10, 15);
            _ui.Renderer.DrawRect(x, y, size, 2, border);
            _ui.Renderer.DrawRect(x, y + size - 2, size, 2, border);
            _ui.Renderer.DrawRect(x, y, 2, size, border);
            _ui.Renderer.DrawRect(x + size - 2, y, 2, size, border);

            // HP bar above the body when damaged. Bar fill normalizes to the
            // entity's MaxHealth so wolves (1.8 HP) and boars (1.5 HP) read as
            // "full" when undamaged instead of already showing a partial bar.
            // Bar color shifts green -> yellow -> red as HP drops so the
            // player reads "low / near-kill" at a glance.
            float maxHp = MathF.Max(e.MaxHealth, 0.001f);
            if (e.Health < maxHp)
            {
                float barW = size;
                float barH = 4f;
                float barX = x;
                float barY = y - 8f;
                float hpRatio = Math.Clamp(e.Health / maxHp, 0f, 1f);
                _ui.Renderer.DrawRect(barX, barY, barW, barH,
                    System.Drawing.Color.FromArgb(200, 40, 10, 10));
                System.Drawing.Color hpColor = hpRatio < 0.33f
                    ? System.Drawing.Color.FromArgb(255, 230, 60, 60)
                    : hpRatio < 0.66f
                        ? System.Drawing.Color.FromArgb(255, 240, 200, 60)
                        : System.Drawing.Color.FromArgb(255, 120, 220, 100);
                _ui.Renderer.DrawRect(barX, barY, barW * hpRatio, barH, hpColor);
            }

            // Name label above the billboard. Text scales with billboard size
            // (so distance matters) but clamps so far-off labels stay legible
            // and close-up ones don't balloon. Sits above the HP bar so they
            // don't overlap.
            string label = e.Kind.ToString();
            float textPx = Math.Clamp(22f * (size / 40f), 10f, 22f);
            float textW = _ui.Renderer.MeasureText(label, textPx);
            float textX = screenX - textW / 2f;
            float textY = (e.Health < maxHp ? y - 24f : y - 16f);
            _ui.Renderer.DrawText(label, textX, textY, textPx,
                System.Drawing.Color.FromArgb(230, 235, 235, 240));

            // Aggro indicator: red pulsing "!" above charging entities.
            // Lets the player spot incoming threats from far away before the
            // billboard grows large enough to read body language.
            if (e.Alert == AlertMode.Charge)
            {
                double t = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
                int alpha = 200 + (int)(MathF.Sin((float)t * 8f) * 55f);
                alpha = Math.Clamp(alpha, 150, 255);
                string alert = "!";
                float alertPx = Math.Clamp(32f * (size / 40f), 14f, 36f);
                float alertW = _ui.Renderer.MeasureText(alert, alertPx);
                _ui.Renderer.DrawText(alert,
                    screenX - alertW / 2f, textY - alertPx - 2f, alertPx,
                    System.Drawing.Color.FromArgb(alpha, 240, 60, 60));
            }
        }
    }

    private void UpdateRain(float dt)
    {
        if (_weather.RainIntensity <= 0.01f) return;
        if (_renderer == null) return;

        int vw = _renderer.CanvasWidth;
        int vh = _renderer.CanvasHeight;
        if (!_rainSeeded)
        {
            for (int i = 0; i < _rainX.Length; i++)
            {
                _rainX[i] = (float)(_rainRng.NextDouble() * vw);
                _rainY[i] = (float)(_rainRng.NextDouble() * vh);
                _rainSpeed[i] = 600f + (float)(_rainRng.NextDouble() * 400f);
            }
            _rainSeeded = true;
        }

        for (int i = 0; i < _rainX.Length; i++)
        {
            _rainY[i] += _rainSpeed[i] * dt;
            if (_rainY[i] > vh)
            {
                _rainY[i] = -20f;
                _rainX[i] = (float)(_rainRng.NextDouble() * vw);
            }
        }
    }

    /// <summary>
    /// Seed + paint a field of tiny white star dots across the upper half of
    /// the viewport. Alpha scales with how dark the sky is, so twilight
    /// shows only the brightest and full night shows the whole field. The
    /// positions are fixed once so the sky doesn't churn per frame.
    /// </summary>
    private void DrawStars(int viewportWidth, int viewportHeight)
    {
        if (!_starsSeeded)
        {
            var rng = new Random(17); // deterministic seed - same sky every session
            for (int i = 0; i < StarCount; i++)
            {
                _starX[i] = (float)rng.NextDouble() * viewportWidth;
                // Stars cluster in the upper 55% of screen so they look like sky.
                _starY[i] = (float)rng.NextDouble() * viewportHeight * 0.55f;
                _starSize[i] = rng.NextDouble() < 0.15 ? 3f : 2f;
            }
            _starsSeeded = true;
        }

        // Fade in during dusk, peak at midnight-ish, fade out at dawn.
        float t = _worldTime.DayFraction;
        float darkness;
        if (t < 0.10f) darkness = 1f - (t / 0.10f);          // dawn pre-sunrise
        else if (t < 0.55f) darkness = 0f;                    // day
        else if (t < 0.75f) darkness = (t - 0.55f) / 0.20f;   // dusk
        else darkness = 1f;                                   // night

        if (darkness <= 0.01f) return;
        int baseAlpha = (int)(230 * darkness);

        for (int i = 0; i < StarCount; i++)
        {
            int alpha = Math.Clamp(baseAlpha - (_starSize[i] < 3 ? 50 : 0), 0, 255);
            _ui.Renderer.DrawRect(_starX[i], _starY[i], _starSize[i], _starSize[i],
                System.Drawing.Color.FromArgb(alpha, 240, 240, 230));
        }
    }

    /// <summary>
    /// Render the sun + moon as small bright circles that arc across the
    /// sky based on DayFraction. Rough projection: x sweeps left-to-right
    /// over the day, y dips lowest at noon (midpoint) and apex matches.
    /// Day fraction 0.0-0.5 = sun visible. 0.5-1.0 = moon visible.
    /// </summary>
    private void DrawSunMoon(int viewportWidth, int viewportHeight)
    {
        float t = _worldTime.DayFraction;
        // Sun arc: rises at 0.05, sets at 0.55. Parabolic Y so noon (0.30)
        // is highest in the sky.
        if (t > 0.05f && t < 0.55f)
        {
            float u = (t - 0.05f) / 0.50f;          // 0..1 across the arc
            float x = u * viewportWidth;
            float y = viewportHeight * 0.30f * (1f - 4f * (u - 0.5f) * (u - 0.5f)); // peak at u=0.5
            float size = 28f;
            _ui.Renderer.DrawRect(x - size * 0.5f, y, size, size,
                System.Drawing.Color.FromArgb(220, 255, 220, 130));
        }
        // Moon arc: rises at 0.55, sets at 1.05 (wraps to 0.05).
        else if (t > 0.55f || t < 0.05f)
        {
            float tt = t > 0.55f ? t : t + 1f;
            float u = (tt - 0.55f) / 0.50f;
            float x = u * viewportWidth;
            float y = viewportHeight * 0.30f * (1f - 4f * (u - 0.5f) * (u - 0.5f));
            float size = 22f;
            _ui.Renderer.DrawRect(x - size * 0.5f, y, size, size,
                System.Drawing.Color.FromArgb(220, 230, 235, 255));
        }
    }

    /// <summary>
    /// When the player is cold enough their breath should fog - emit small
    /// rising puffs near screen-center (the camera direction). Age each
    /// puff toward 1 (dead). No cap check needed: we re-use the oldest
    /// index so the array acts as a ring buffer.
    /// </summary>
    private void UpdateBreath(float dt)
    {
        // Age existing puffs.
        for (int i = 0; i < BreathMax; i++)
            if (_breathAge[i] < 1f) _breathAge[i] = MathF.Min(1f, _breathAge[i] + dt * 0.8f);

        // Emit only when temperature is low enough. Cadence ~0.6s between
        // puffs - matches a slow, visible breath rhythm.
        if (_stats.Temperature > 0.30f || _renderer == null)
        {
            _breathEmitTimer = 0;
            return;
        }
        _breathEmitTimer += dt;
        if (_breathEmitTimer < 0.6f) return;
        _breathEmitTimer = 0;

        // Find the oldest (highest age) slot and reuse it.
        int oldest = 0;
        for (int i = 1; i < BreathMax; i++)
            if (_breathAge[i] > _breathAge[oldest]) oldest = i;

        int vw = _renderer.CanvasWidth;
        int vh = _renderer.CanvasHeight;
        _breathX[oldest] = vw * 0.5f + ((float)_rainRng.NextDouble() - 0.5f) * 40f;
        _breathY[oldest] = vh * 0.55f + ((float)_rainRng.NextDouble() - 0.5f) * 10f;
        _breathAge[oldest] = 0f;
    }

    private void DrawBreath(int viewportWidth, int viewportHeight)
    {
        for (int i = 0; i < BreathMax; i++)
        {
            float age = _breathAge[i];
            if (age >= 1f) continue;
            // Alpha peaks at mid-life, fades toward the end.
            int alpha = (int)(150 * (1f - age) * (age < 0.5f ? age * 2f : 1f));
            float rise = age * 40f;
            float size = 6f + age * 10f;
            _ui.Renderer.DrawRect(
                _breathX[i] - size * 0.5f,
                _breathY[i] - rise - size * 0.5f,
                size, size,
                System.Drawing.Color.FromArgb(alpha, 240, 240, 245));
        }
    }

    private void DrawRain(int viewportWidth, int viewportHeight)
    {
        float t = _weather.RainIntensity;
        if (t <= 0.01f) return;

        // Fewer visible streaks at low intensity so drizzle vs downpour looks
        // different. Alpha scales with intensity too.
        int count = (int)(_rainX.Length * t);
        int alpha = (int)(120 * t);
        var color = System.Drawing.Color.FromArgb(alpha, 140, 170, 200);
        for (int i = 0; i < count; i++)
        {
            _ui.Renderer.DrawRect(_rainX[i], _rainY[i], 2f, 14f, color);
        }
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
                _inventoryHotbarRow.SetCell(i, FormatCellLabel(item), CategoryColor(item));
            }
        }

        if (_backpackGrid != null)
        {
            for (int i = 0; i < InventoryService.BackpackSize; i++)
            {
                var item = _inventory.Backpack[i];
                _backpackGrid.SetCell(i, FormatCellLabel(item), CategoryColor(item));
            }
        }
    }

    private static string? FormatCellLabel(InventoryItem? item)
    {
        if (item is null) return null;
        // Prefix a glyph per category so slots are quickly recognizable even before
        // reading the name. ASCII-only - font atlas is guaranteed to have these.
        string glyph = item.Category switch
        {
            ItemCategory.Food     => "*",
            ItemCategory.Drink    => "~",
            ItemCategory.Medical  => "+",
            ItemCategory.Tool     => "/",
            ItemCategory.Material => "#",
            ItemCategory.Marker   => "!",
            _ => "",
        };
        string stack = item.Count > 1 ? $" x{item.Count}" : "";
        return string.IsNullOrEmpty(glyph) ? $"{item.Name}{stack}" : $"{glyph} {item.Name}{stack}";
    }

    private static System.Drawing.Color? CategoryColor(InventoryItem? item)
    {
        if (item is null) return null;
        return item.Category switch
        {
            ItemCategory.Food     => System.Drawing.Color.FromArgb(255, 240, 170, 80),  // warm orange
            ItemCategory.Drink    => System.Drawing.Color.FromArgb(255, 120, 180, 240), // cyan blue
            ItemCategory.Medical  => System.Drawing.Color.FromArgb(255, 240, 120, 120), // pale red
            ItemCategory.Tool     => System.Drawing.Color.FromArgb(255, 200, 200, 210), // muted gray
            ItemCategory.Material => System.Drawing.Color.FromArgb(255, 220, 190, 140), // sandy tan
            ItemCategory.Marker   => System.Drawing.Color.FromArgb(255, 240, 220, 110), // warm yellow
            _ => null,
        };
    }

    private void SyncStatsToHud()
    {
        StatusHUD.Health = _stats.Health;
        StatusHUD.Stamina = _stats.Stamina;
        StatusHUD.Hunger = _stats.Hunger;
        StatusHUD.Thirst = _stats.Thirst;
        StatusHUD.Temperature = _stats.Temperature;

        // XP progress toward next level. Formula mirrors PlayerStatsService:
        // level n requires (n-1)^2 * 50 XP. Progress = (xp - prev) / (next - prev).
        if (_xpBar is not null && _xpLabel is not null)
        {
            int lv = _stats.Level;
            int prev = (lv - 1) * (lv - 1) * 50;
            int next = lv * lv * 50;
            int span = Math.Max(1, next - prev);
            float progress = Math.Clamp((_stats.Experience - prev) / (float)span, 0f, 1f);
            _xpBar.Value = progress;
            _xpLabel.Text = $"Lv {lv}   {_stats.Experience - prev} / {span} XP";
        }

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

        // Persistent blue vignette when body temperature is cold. Ramps in from
        // Temperature 0.3 down to 0. Complements the red low-HP vignette from
        // hypothermia damage.
        if (_stats.Temperature < 0.30f)
        {
            float t = 1f - (_stats.Temperature / 0.30f);
            int alpha = Math.Clamp((int)(15 + t * 90f), 15, 110);
            ScreenOverlay?.SetPersistent("cold",
                System.Drawing.Color.FromArgb(alpha, 80, 140, 220));
        }
        else
        {
            ScreenOverlay?.ClearPersistent("cold");
        }

        // Threshold-cross toasts for hunger + thirst + temperature + stamina. Fires
        // exactly once on the frame the value crosses each threshold. Recovery on
        // the other side resets it.
        CheckHungerThreshold();
        CheckThirstThreshold();
        CheckTemperatureThreshold();
        CheckStaminaThreshold();
    }

    private void CheckStaminaThreshold()
    {
        float cur = _stats.Stamina, prev = _lastStaminaSeen;
        // Fire "Winded" once when stamina hits empty mid-sprint. Comes back "Rested"
        // when it recovers above 0.50 so the next sprint opportunity is obvious.
        if (prev > 0.05f && cur <= 0.05f) NotifyWarning("Winded");
        if (prev < 0.50f && cur >= 0.50f) Notify("Rested");
        _lastStaminaSeen = cur;
    }

    private void CheckTemperatureThreshold()
    {
        float cur = _stats.Temperature, prev = _lastTempSeen;

        // Cold side (dropping below thresholds)
        if (prev > 0.30f && cur <= 0.30f) Notify("Cold");
        if (prev > 0.15f && cur <= 0.15f) NotifyDamage("Freezing!");

        // Hot side (rising above thresholds)
        if (prev < 0.70f && cur >= 0.70f) Notify("Hot");
        if (prev < 0.85f && cur >= 0.85f) NotifyDamage("Heatstroke!");

        _lastTempSeen = cur;
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
        if (prev > 0.5f && cur <= 0.5f) Notify("Damp");
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

        // Refresh the clock label from WorldTimeService (cheap string format).
        // Tint the text by phase so day/night reads at a glance from the
        // top-left corner without needing to parse the string.
        if (_clockLabel != null)
        {
            _clockLabel.Text = $"{_worldTime.ClockString}  {_worldTime.PhaseName}  Day {_worldTime.DayNumber}";
            _clockLabel.Color = _worldTime.PhaseName switch
            {
                "Dawn"  => System.Drawing.Color.FromArgb(240, 255, 190, 120),
                "Day"   => System.Drawing.Color.FromArgb(240, 240, 240, 230),
                "Dusk"  => System.Drawing.Color.FromArgb(240, 255, 150, 110),
                "Night" => System.Drawing.Color.FromArgb(240, 140, 180, 235),
                _       => System.Drawing.Color.FromArgb(240, 220, 220, 220),
            };
        }

        // FPS smoothing: low-pass over ~0.5s. Updates the debug line only a few
        // times a second so the text doesn't flicker on every frame.
        if (deltaTime > 0)
        {
            float instantFps = 1f / deltaTime;
            _fpsSmoothed = _fpsSmoothed == 0 ? instantFps : (_fpsSmoothed * 0.9f + instantFps * 0.1f);
        }
        _fpsAccumTime += deltaTime;
        if (_debugLabel != null && _fpsAccumTime > 0.25f)
        {
            _fpsAccumTime = 0;
            int s = (int)_stats.PlayTimeSeconds;
            string time = $"{s / 3600:D2}:{(s % 3600) / 60:D2}:{s % 60:D2}";
            string phase = _worldTime.PhaseName;
            string rain = _weather.RainIntensity > 0.02f
                ? $"  Rain {(int)(_weather.RainIntensity * 100)}%"
                : "";
            int achvCount = (_stats.FirstKillAwarded ? 1 : 0)
                          + (_stats.FirstFireAwarded ? 1 : 0)
                          + (_stats.FirstCookAwarded ? 1 : 0)
                          + (_stats.FirstWolfAwarded ? 1 : 0)
                          + (_stats.FirstSleepAwarded ? 1 : 0)
                          + (_stats.VeteranAwarded ? 1 : 0)
                          + (_stats.CenturionAwarded ? 1 : 0)
                          + (_stats.SurvivorAwarded ? 1 : 0)
                          + (_stats.BowmanAwarded ? 1 : 0)
                          + (_stats.HunterAwarded ? 1 : 0)
                          + (_stats.GourmetAwarded ? 1 : 0)
                          + (_stats.ResilientAwarded ? 1 : 0)
                          + (_stats.FirstAidAwarded ? 1 : 0)
                          + (_stats.PackHunterAwarded ? 1 : 0)
                          + (_stats.CompletionistAwarded ? 1 : 0);
            // Save age: "saved 4s ago" or "never" before first write.
            string savedAge = LastSaveTime == DateTime.MinValue
                ? "never"
                : $"{(int)(DateTime.UtcNow - LastSaveTime).TotalSeconds}s ago";
            _debugLabel.Text =
                $"{(int)_fpsSmoothed} fps    " +
                $"X {cameraPosition.X,6:F1} Y {cameraPosition.Y,6:F1} Z {cameraPosition.Z,6:F1}    " +
                $"D{_worldTime.DayNumber} [{phase}]  Lv {_stats.Level}  XP {_stats.Experience}  Kills {_stats.Kills}  Achv {achvCount}/15  T {time}{rain}  saved {savedAge}";
        }

        // Update compass bearing from camera yaw
        Compass.Bearing = cameraYaw;

        // Bleed badge: shown while BleedSecondsRemaining > 0, pulses by sin
        // wave so the player notices it. Hidden the rest of the time.
        if (_bleedLabel != null)
        {
            float remaining = _stats.BleedSecondsRemaining;
            if (remaining > 0)
            {
                _bleedLabel.Visible = true;
                _bleedLabel.Text = $"BLEEDING  {remaining:F1}s";
                // Pulse alpha 160-255 at ~3Hz so it reads as "urgent" without
                // being distracting. Uses UTC ticks so it's frame-rate
                // independent.
                double t = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
                int alpha = 200 + (int)(MathF.Sin((float)t * 6f) * 55f);
                alpha = Math.Clamp(alpha, 160, 255);
                _bleedLabel.Color = System.Drawing.Color.FromArgb(alpha, 220, 40, 40);
            }
            else if (_bleedLabel.Visible)
            {
                _bleedLabel.Visible = false;
            }
        }

        // Sneak indicator visibility tracks IsSneaking which Game.razor pushes
        // each frame from CTRL key state.
        if (_sneakIndicator != null)
            _sneakIndicator.Visible = IsSneaking;

        // Combo badge: live "COMBO Nx  Ys" while a kill chain is active.
        // CurrentCombo and ComboExpiry are pushed from Game.razor on each
        // kill. Shows only for streak >= 2 (1 is just "a kill").
        if (_comboLabel != null)
        {
            var now = DateTime.UtcNow;
            float left = (float)(ComboExpiry - now).TotalSeconds;
            if (CurrentCombo >= 2 && left > 0)
            {
                _comboLabel.Visible = true;
                _comboLabel.Text = $"{CurrentCombo}x COMBO  {left:F1}s";
                // Color intensifies with streak: 2x warm yellow, 3-4x
                // orange, 5x+ bright red. Gives a visual "you're cooking"
                // cue as the chain grows.
                _comboLabel.Color = CurrentCombo >= 5
                    ? System.Drawing.Color.FromArgb(245, 255, 120, 80)
                    : CurrentCombo >= 3
                        ? System.Drawing.Color.FromArgb(240, 255, 180, 80)
                        : System.Drawing.Color.FromArgb(235, 255, 220, 100);
            }
            else if (_comboLabel.Visible)
            {
                _comboLabel.Visible = false;
            }
        }

        // Update minimap player position (XZ plane + altitude)
        Minimap.PlayerPosition = new Vector2(cameraPosition.X, cameraPosition.Z);
        Minimap.PlayerAltitude = cameraPosition.Y;
        Minimap.PlayerRotation = cameraYaw * MathF.PI / 180f;

        // Upsert a minimap marker per entity. Removing + re-adding every frame
        // is cheap for ~5 entities and keeps the marker list in sync with the
        // mutable entity list (births, deaths, wanders). Marker ID convention:
        // "entity.{id}" so we don't collide with POIs like "spawn".
        foreach (var e in _entities.Entities)
        {
            string markerId = $"entity.{e.Id}";
            // EntityKind -> marker type + label colour (live code path since the
            // marker tint comes from the map widget's MarkerType -> color table).
            var markerType = e.Kind switch
            {
                EntityKind.Boar => MapMarkerType.Enemy,       // red-ish, threat-coded
                EntityKind.Wolf => MapMarkerType.Enemy,       // also threat - night predator
                EntityKind.Crow => MapMarkerType.POI,         // neutral, passes through
                EntityKind.Deer => MapMarkerType.OtherPlayer, // peaceful prey
                _ => MapMarkerType.OtherPlayer,               // Rabbit: friendly-coded
            };
            Minimap.AddMarker(new MapMarker
            {
                Id = markerId,
                Label = e.Kind.ToString(),
                WorldPosition = new Vector2(e.Position.X, e.Position.Z),
                Type = markerType,
            });
        }

        // Upsert a marker for each active campfire so the player can navigate
        // back to base at any time. Extinguished fires (Fuel <= 0) drop off
        // the map so you're not misled about warmth availability.
        foreach (var f in _fires.Fires)
        {
            if (f.Fuel <= 0) continue;
            Minimap.AddMarker(new MapMarker
            {
                Id = $"fire.{f.Id}",
                Label = "Fire",
                WorldPosition = new Vector2(f.Position.X, f.Position.Z),
                Type = MapMarkerType.POI,
            });
        }

        // Ground loot markers - one per uncollected drop. Removed once the
        // ground item is picked up (the player dot hits them), so stale
        // markers can't accumulate.
        foreach (var g in _ground.Items)
        {
            Minimap.AddMarker(new MapMarker
            {
                Id = $"loot.{g.Id}",
                Label = "Loot",
                WorldPosition = new Vector2(g.Position.X, g.Position.Z),
                Type = MapMarkerType.POI,
            });
        }

        // Warm orange screen tint when the player is inside a fire's warmth
        // aura. Scales intensity with the bonus so a barely-in-range fire
        // gives only a faint glow while sitting in one paints a clear cozy
        // tone. Cleared immediately when walking away.
        float warmth = _fires.GetWarmthBonusAt(cameraPosition);
        if (warmth > 0.02f)
        {
            int alpha = Math.Clamp((int)(warmth * 120f), 10, 70);
            ScreenOverlay?.SetPersistent("fireglow",
                System.Drawing.Color.FromArgb(alpha, 230, 120, 40));
        }
        else
        {
            ScreenOverlay?.ClearPersistent("fireglow");
        }

        // Storm gloom: a dim slate-blue overlay during heavy rain so bad
        // weather actually darkens the world. Gentle at light drizzle,
        // noticeable at downpour. Kicks in past 0.30 intensity so clear
        // skies don't tint.
        float stormRain = _weather.RainIntensity;
        if (stormRain > 0.30f)
        {
            // 0.30 rain -> alpha 10, 1.0 rain -> alpha 80.
            int alpha = Math.Clamp((int)((stormRain - 0.30f) / 0.70f * 70f) + 10, 10, 80);
            ScreenOverlay?.SetPersistent("stormgloom",
                System.Drawing.Color.FromArgb(alpha, 30, 40, 60));
        }
        else
        {
            ScreenOverlay?.ClearPersistent("stormgloom");
        }

        // Golden-hour tint at dawn and dusk. Peaks at the exact twilight
        // fraction and fades out through the edge. Suppressed during heavy
        // rain so storm gloom reads cleanly.
        float frac = _worldTime.DayFraction;
        float goldenAlpha = 0f;
        if (frac >= 0.02f && frac <= 0.14f)
        {
            float t = (frac - 0.02f) / 0.12f; // 0..1 across dawn
            goldenAlpha = MathF.Sin(t * MathF.PI) * 0.7f;
        }
        else if (frac >= 0.52f && frac <= 0.64f)
        {
            float t = (frac - 0.52f) / 0.12f; // 0..1 across dusk
            goldenAlpha = MathF.Sin(t * MathF.PI) * 0.7f;
        }
        if (goldenAlpha > 0.02f && stormRain < 0.3f)
        {
            int alpha = (int)Math.Clamp(goldenAlpha * 50f, 0, 50);
            ScreenOverlay?.SetPersistent("goldenhour",
                System.Drawing.Color.FromArgb(alpha, 255, 180, 100));
        }
        else
        {
            ScreenOverlay?.ClearPersistent("goldenhour");
        }

        // Moonlight tint during deep night (0.68-0.92 day fraction). Pale
        // blue overlay peaks at midnight. Sky darkening makes silhouettes
        // read properly at night. Also suppressed during heavy rain so
        // storm gloom doesn't double up.
        float moonAlpha = 0f;
        if (frac >= 0.68f && frac <= 0.92f)
        {
            float t = (frac - 0.68f) / 0.24f;
            moonAlpha = MathF.Sin(t * MathF.PI) * 0.8f;
        }
        if (moonAlpha > 0.02f && stormRain < 0.3f)
        {
            int alpha = (int)Math.Clamp(moonAlpha * 35f, 0, 35);
            ScreenOverlay?.SetPersistent("moonlight",
                System.Drawing.Color.FromArgb(alpha, 80, 100, 160));
        }
        else
        {
            ScreenOverlay?.ClearPersistent("moonlight");
        }

        // Update screen overlay effects
        ScreenOverlay.Update(deltaTime);

        // Rain particles: falling 2D streaks that land in OnPostRender. Seeded once,
        // then recycled when they fall off the bottom of the viewport.
        UpdateRain(deltaTime);

        // Breath puffs in cold - emitted only when player's temperature is low.
        UpdateBreath(deltaTime);

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

            // Stars paint first so everything else (terrain canvas is already
            // drawn, UI screens, fires, entities) sits on top. Only visible
            // at night + twilight; during day the draw is a cheap no-op.
            DrawStars(viewportWidth, viewportHeight);
            DrawSunMoon(viewportWidth, viewportHeight);

            // Campfires render first so entity billboards paint on top - a
            // critter standing in front of the fire occludes it naturally.
            DrawCampfires(viewportWidth, viewportHeight);

            // Ground items render between fires and entities - they're on the
            // ground so wildlife and fire particles both correctly sit above.
            DrawGroundItems(viewportWidth, viewportHeight);

            // Entity billboards: 2D colored squares at each entity's projected
            // screen position. Renders on top of terrain, below rain + flashes.
            DrawEntityBillboards(viewportWidth, viewportHeight);

            // Arrow impact marks render on top of the entity layer so the
            // white cross is always visible even mid-billboard.
            DrawImpactMarks(viewportWidth, viewportHeight);

            // Blood splatter particles - small dark red specks that
            // arc out + fall with gravity. Renders behind damage numbers so
            // the number stays legible on top of the splatter.
            DrawBloodSplatters(viewportWidth, viewportHeight);

            // Floating damage numbers rise above the hit point - rendered
            // last so they sit on top of all world geometry.
            DrawDamageNumbers(viewportWidth, viewportHeight);

            // Durability bar above the active hotbar slot for tools that can
            // break. Small but always-visible so the player isn't surprised.
            DrawDurabilityBar(viewportWidth, viewportHeight);

            // Simple held-tool viewmodel in the bottom-right - communicates
            // "you have this equipped" without a full 3D rig.
            DrawViewmodel(viewportWidth, viewportHeight);

            // Rain particles render on top of the voxel scene but below menus.
            DrawRain(viewportWidth, viewportHeight);

            // Breath puffs render after rain so they're never occluded by the
            // streaks - the player's own breath should always be visible.
            DrawBreath(viewportWidth, viewportHeight);

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
        _inventory.OnInventoryChanged -= RefreshCraftingButtons;
        _inventory.OnActiveHotbarChanged -= SyncActiveHotbar;
        _inventory.OnItemConsumed -= HandleItemConsumed;
        _inventory.OnItemPickedUp -= HandleItemPickedUp;
        _stats.OnDamageTaken -= HandleDamageTaken;
        _stats.OnHealed -= HandleHealed;
        _stats.OnLevelUp -= HandleLevelUp;
        _weather.OnLightningStrike -= HandleLightningStrike;
        _entities.OnEntityKilled -= HandleEntityDespawned;
        _ground.OnPickedUpId -= HandleLootPickedUpId;
        if (_renderer != null)
            _renderer.OnPostRender = null;
    }
}
