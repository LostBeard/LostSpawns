using System.Drawing;
using System.Numerics;
using LostSpawns.Rendering;
using LostSpawns.Services;
using LostSpawns.Xr;
using SpawnDev.GameUI;
using SpawnDev.GameUI.Elements;

namespace LostSpawns.Pages;

/// <summary>
/// The game in the headset. The world is drawn by <see cref="VrWorld"/>; this is the play: the right controller is the player's arm (its ray is
/// where tools hit and blocks go), the triggers and buttons stand in for the mouse and the keys, the player's numbers are on a panel over the left
/// wrist, and a menu (X) holds the hotbar, the quick actions and the crafting list. The desktop's own handlers do the work, aimed along the
/// controller (the camera is put there each frame).
/// </summary>
public partial class Game
{
    private VrUi? _vrUi;
    private VrPanel? _vrWrist, _vrMenu, _vrToast, _vrFlash, _vrPack, _vrHelp, _vrSettings, _vrVignette;
    private UIButton? _vrTurnMode, _vrTurnSpeed, _vrSnapStep, _vrVignetteButton, _vrDrawRadius;
    private float _vrVignetteLevel;
    private UIPanel? _vrVigOuter, _vrVigInner;
    private UILabel? _vrWristHeader, _vrWristHeld, _vrToastLabel, _vrMenuStatus;
    private readonly (UIPanel Fill, UIPanel Back)[] _vrBars = new (UIPanel, UIPanel)[5];
    private readonly UIButton[] _vrHotbarButtons = new UIButton[9];
    private readonly UIButton[] _vrPackButtons = new UIButton[InventoryService.BackpackSize];
    private readonly List<(UIButton Button, LostSpawns.Services.CraftingRecipe Recipe)> _vrCraftButtons = new();
    private UIButton? _vrRespawn;
    private double _vrLastTime = -1;
    private bool _vrRtWas, _vrLtWas, _vrAWas, _vrBWas, _vrXWas, _vrYWas;
    private float _vrRepeat;
    private float _vrFlashLevel;
    private float _vrToastAge = 99f;
    private bool _vrHandlersAttached;
    private float _vrStandHeight;
    private Vector3? _vrStepFrom;
    private float _vrStepDistance;
    private bool _vrStepAlt;

    private static readonly string[] BarNames = { "HP", "Food", "Water", "Stamina", "Warmth" };
    private static readonly Color[] BarColors =
    {
        Color.FromArgb(255, 210, 60, 60), Color.FromArgb(255, 220, 150, 50), Color.FromArgb(255, 70, 150, 230),
        Color.FromArgb(255, 90, 200, 90), Color.FromArgb(255, 235, 120, 60),
    };

    private void EnsureVrUi()
    {
        if (_vrUi != null || _vr == null) return;
        _vrUi = new VrUi();
        BuildVrFlash();
        BuildVrWrist();
        BuildVrToast();
        BuildVrMenu();
        BuildVrPack();
        BuildVrHelp();
        BuildVrSettings();
        BuildVrVignette();
        _vrUi.Panels.Add(_vrFlash!);
        _vrUi.Panels.Add(_vrVignette!);
        _vrUi.Panels.Add(_vrWrist!);
        _vrUi.Panels.Add(_vrToast!);
        _vrUi.Panels.Add(_vrMenu!);
        _vrUi.Panels.Add(_vrPack!);
        _vrUi.Panels.Add(_vrHelp!);
        _vrUi.Panels.Add(_vrSettings!);
        _vr.Ui = _vrUi;
        _vr.UiRenderer = () => Hud.UiRenderer;
        _vr.HeldProvider = () => new HeldView(Inventory.ActiveItem?.Id, Hud.SwingArc, Hud.SwingStrength, 0f, 0f);
        AttachVrHandlers();
        ApplyVrOptions();
        _vr.OnTurnModeToggled += smooth => { Vr.Options.SmoothTurn = smooth; Vr.SaveOptions(); };
        Hud.Notify("Press X for the menu: hotbar, crafting, controls");
    }

    private void AttachVrHandlers()
    {
        if (_vrHandlersAttached) return;
        _vrHandlersAttached = true;
        Stats.OnDamageTaken += VrOnDamage;
        Hud.OnNotified += VrOnNotified;
        Wildlife.OnEntityHit += VrOnEntityHit;
    }

    private void DetachVrHandlers()
    {
        Input.KeysDown.Remove("ControlLeft");
        if (!_vrHandlersAttached) return;
        _vrHandlersAttached = false;
        Stats.OnDamageTaken -= VrOnDamage;
        Hud.OnNotified -= VrOnNotified;
        Wildlife.OnEntityHit -= VrOnEntityHit;
    }

    private void VrOnDamage(float amount)
    {
        _vrFlashLevel = MathF.Min(1f, 0.45f + amount * 2.5f);
        Vr.Session.Pulse(true, 0.8f, 140);
        Vr.Session.Pulse(false, 0.8f, 140);
    }

    private void VrOnEntityHit(WanderingEntity e) => Vr.Session.Pulse(false, 0.7f, 60);

    private void VrOnNotified(string text, SpawnDev.GameUI.Elements.NotificationType type)
    {
        if (_vrToastLabel == null) return;
        _vrToastLabel.Text = text;
        _vrToastLabel.Color = type switch
        {
            SpawnDev.GameUI.Elements.NotificationType.Success => Color.FromArgb(255, 130, 230, 130),
            SpawnDev.GameUI.Elements.NotificationType.Warning => Color.FromArgb(255, 245, 210, 90),
            SpawnDev.GameUI.Elements.NotificationType.Damage => Color.FromArgb(255, 255, 120, 110),
            SpawnDev.GameUI.Elements.NotificationType.Achievement => Color.FromArgb(255, 255, 215, 90),
            _ => Color.FromArgb(255, 225, 235, 245),
        };
        _vrToastAge = 0f;
    }

    // --- The panels ---

    private void BuildVrFlash()
    {
        _vrFlash = new VrPanel(1500, 900) { ReadOnly = true };
        _vrFlash.Panel.BackgroundColor = Color.FromArgb(0, 200, 20, 20);
        _vrFlash.Panel.BorderWidth = 0;
        _vrFlash.Panel.CornerRadius = 0;
    }

    private void BuildVrWrist()
    {
        _vrWrist = new VrPanel(300, 226) { ReadOnly = true, IsOpen = true };
        var p = _vrWrist.Panel;
        _vrWristHeader = new UILabel { X = 12, Y = 8, Text = "", FontSize = FontSize.Body, Color = Color.FromArgb(255, 225, 235, 245) };
        p.AddChild(_vrWristHeader);
        for (int i = 0; i < _vrBars.Length; i++)
        {
            float y = 40 + i * 30;
            p.AddChild(new UILabel { X = 12, Y = y, Text = BarNames[i], FontSize = FontSize.Caption, Color = Color.FromArgb(255, 200, 210, 222) });
            var back = new UIPanel { X = 92, Y = y + 2, Width = 190, Height = 18, CornerRadius = 4, BorderWidth = 0, BackgroundColor = Color.FromArgb(255, 40, 46, 56) };
            var fill = new UIPanel { X = 92, Y = y + 2, Width = 190, Height = 18, CornerRadius = 4, BorderWidth = 0, BackgroundColor = BarColors[i] };
            p.AddChild(back);
            p.AddChild(fill);
            _vrBars[i] = (fill, back);
        }
        _vrWristHeld = new UILabel { X = 12, Y = 196, Text = "", FontSize = FontSize.Caption, Color = Color.FromArgb(255, 235, 215, 160) };
        p.AddChild(_vrWristHeld);
    }

    private void BuildVrToast()
    {
        _vrToast = new VrPanel(620, 64, 0.0013f) { ReadOnly = true };
        _vrToastLabel = new UILabel { X = 20, Y = 14, Width = 580, Height = 36, Text = "", FontSize = FontSize.Heading, Color = Color.White };
        _vrToast.Panel.AddChild(_vrToastLabel);
    }

    private void BuildVrMenu()
    {
        _vrMenu = new VrPanel(800, 620, 0.0011f);
        var p = _vrMenu.Panel;
        p.AddChild(new UILabel { X = 24, Y = 16, Text = "Lost Spawns", FontSize = FontSize.Heading, Color = Color.FromArgb(255, 235, 240, 248) });
        _vrMenuStatus = new UILabel { X = 24, Y = 56, Width = 740, Height = 24, Text = "", FontSize = FontSize.Caption, Color = Color.FromArgb(255, 170, 190, 210) };
        p.AddChild(_vrMenuStatus);

        p.AddChild(new UILabel { X = 24, Y = 92, Text = "Hotbar (A / B change it)", FontSize = FontSize.Caption, Color = Color.FromArgb(255, 200, 210, 222) });
        for (int i = 0; i < 9; i++)
        {
            int slot = i;
            var b = new UIButton { Text = "", X = 24 + (i % 3) * 128, Y = 118 + (i / 3) * 58, Width = 120, Height = 50 };
            b.OnClick = () => Inventory.ActiveHotbarIndex = slot;
            _vrHotbarButtons[i] = b;
            p.AddChild(b);
        }

        // Quick actions: the desktop's own keys
        Button(p, "Eat", 24, 300, () => HandleQuickEat());
        Button(p, "Drink", 152, 300, () => HandleQuickDrink());
        Button(p, "Heal", 280, 300, () => HandleQuickHeal());
        Button(p, "Feed fire", 24, 358, () => HandleFeedFire());
        Button(p, "Sleep", 152, 358, () => HandleSleep());
        Button(p, "Drop item", 280, 358, () => HandleDropActive());
        Button(p, "Save", 24, 416, () => HandleQuickSave());
        Button(p, "Backpack", 152, 416, () => { _vrPack!.Model = _vrMenu!.Model; _vrMenu.IsOpen = false; _vrPack.IsOpen = true; });
        Button(p, "Leave VR", 280, 416, () => { _ = Vr.Session.ExitAsync(); });
        Button(p, "Close", 24, 474, () => _vrMenu!.IsOpen = false);
        Button(p, "Settings", 152, 474, () => { _vrSettings!.Model = _vrMenu!.Model; _vrMenu.IsOpen = false; _vrSettings.IsOpen = true; RefreshVrSettingsText(); });
        Button(p, "Controls", 280, 474, () => { _vrHelp!.Model = _vrMenu!.Model; _vrMenu.IsOpen = false; _vrHelp.IsOpen = true; });

        _vrRespawn = new UIButton { Text = "Respawn", X = 24, Y = 540, Width = 376, Height = 60, Visible = false };
        _vrRespawn.OnClick = VrRespawn;
        p.AddChild(_vrRespawn);

        p.AddChild(new UILabel { X = 430, Y = 92, Text = "Crafting", FontSize = FontSize.Caption, Color = Color.FromArgb(255, 200, 210, 222) });
        int n = 0;
        foreach (var recipe in Crafter.Recipes)
        {
            var captured = recipe;
            var b = new UIButton { Text = recipe.DisplayName, X = 430 + (n % 2) * 180, Y = 118 + (n / 2) * 52, Width = 170, Height = 44 };
            b.OnClick = () =>
            {
                if (Crafter.TryCraft(captured)) Hud.NotifySuccess($"Crafted {captured.Output.Name}");
                else Hud.NotifyWarning($"Missing materials for {captured.DisplayName}");
            };
            _vrCraftButtons.Add((b, recipe));
            p.AddChild(b);
            n++;
        }
    }

    // The backpack: a click on a slot swaps it with the slot of the hotbar in the hand
    private void BuildVrPack()
    {
        _vrPack = new VrPanel(800, 400, 0.0011f);
        var p = _vrPack.Panel;
        p.AddChild(new UILabel { X = 24, Y = 16, Text = "Backpack", FontSize = FontSize.Heading, Color = Color.FromArgb(255, 235, 240, 248) });
        p.AddChild(new UILabel { X = 24, Y = 56, Width = 740, Text = "Pick a slot to swap it with the hotbar slot in your hand", FontSize = FontSize.Caption, Color = Color.FromArgb(255, 170, 190, 210) });
        for (int i = 0; i < InventoryService.BackpackSize; i++)
        {
            int slot = i;
            var b = new UIButton { Text = "", X = 24 + (i % InventoryService.BackpackColumns) * 94, Y = 88 + (i / InventoryService.BackpackColumns) * 52, Width = 88, Height = 46 };
            b.OnClick = () => Inventory.MoveSlot(false, slot, true, Inventory.ActiveHotbarIndex);
            _vrPackButtons[i] = b;
            p.AddChild(b);
        }
        Button(p, "Back", 24, 320, () => { _vrMenu!.Model = _vrPack!.Model; _vrPack.IsOpen = false; _vrMenu.IsOpen = true; });
    }

    // The headset's own settings: how to turn, how far to draw, whether the edges darken while moving; kept between visits
    private void BuildVrSettings()
    {
        _vrSettings = new VrPanel(800, 470, 0.0011f);
        var p = _vrSettings.Panel;
        var o = Vr.Options;
        p.AddChild(new UILabel { X = 24, Y = 16, Text = "VR settings", FontSize = FontSize.Heading, Color = Color.FromArgb(255, 235, 240, 248) });

        UIButton Row(string name, float y, Action onClick, float width = 460)
        {
            var b = new UIButton { Text = name, X = 24, Y = y, Width = width, Height = 52 };
            b.OnClick = () => { onClick(); Vr.SaveOptions(); RefreshVrSettingsText(); };
            p.AddChild(b);
            return b;
        }
        void Small(string text, float x, float y, Action onClick)
        {
            var b = new UIButton { Text = text, X = x, Y = y, Width = 60, Height = 52 };
            b.OnClick = () => { onClick(); Vr.SaveOptions(); RefreshVrSettingsText(); };
            p.AddChild(b);
        }

        _vrTurnMode = Row("", 76, () => { o.SmoothTurn = !o.SmoothTurn; if (_vr != null) _vr.SmoothTurn = o.SmoothTurn; });
        _vrTurnSpeed = Row("", 136, () => { }, 380);
        Small("-", 416, 136, () => { o.TurnDegreesPerSecond = MathF.Max(40f, o.TurnDegreesPerSecond - 15f); ApplyVrOptions(); });
        Small("+", 484, 136, () => { o.TurnDegreesPerSecond = MathF.Min(240f, o.TurnDegreesPerSecond + 15f); ApplyVrOptions(); });
        _vrSnapStep = Row("", 196, () => { o.SnapDegrees = o.SnapDegrees >= 60f ? 15f : o.SnapDegrees + 15f; ApplyVrOptions(); });
        _vrVignetteButton = Row("", 256, () => o.ComfortVignette = !o.ComfortVignette);
        _vrDrawRadius = Row("", 316, () => { }, 380);
        Small("-", 416, 316, () => Vr.DrawRadius = o.DrawRadius = Math.Max(VrWorld.MinDrawRadius, o.DrawRadius - 1));
        Small("+", 484, 316, () => Vr.DrawRadius = o.DrawRadius = Math.Min(10, o.DrawRadius + 1));
        p.AddChild(new UILabel { X = 560, Y = 330, Width = 230, Text = "(next time you enter VR)", FontSize = FontSize.Caption, Color = Color.FromArgb(255, 170, 190, 210) });
        Button(p, "Back", 24, 400, () => { _vrMenu!.Model = _vrSettings!.Model; _vrSettings.IsOpen = false; _vrMenu.IsOpen = true; });
    }

    private void ApplyVrOptions()
    {
        if (_vr == null) return;
        var o = Vr.Options;
        _vr.SmoothTurn = o.SmoothTurn;
        _vr.TurnDegreesPerSecond = o.TurnDegreesPerSecond;
        _vr.SnapTurnDegrees = o.SnapDegrees;
    }

    private void RefreshVrSettingsText()
    {
        var o = Vr.Options;
        _vrTurnMode!.Text = "Turning: " + (o.SmoothTurn ? "smooth" : "in steps");
        _vrTurnSpeed!.Text = $"Smooth turn speed: {o.TurnDegreesPerSecond:F0} deg/s";
        _vrSnapStep!.Text = $"Step turn size: {o.SnapDegrees:F0} deg";
        _vrVignetteButton!.Text = "Comfort vignette: " + (o.ComfortVignette ? "on" : "off");
        _vrDrawRadius!.Text = $"Draw distance: {o.DrawRadius} columns";
    }

    // The edges of the view go dark while the sticks move the player: a ring close in front of the eyes, wider the harder the push
    private void BuildVrVignette()
    {
        _vrVignette = new VrPanel(1300, 1300) { ReadOnly = true };
        var p = _vrVignette.Panel;
        p.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
        p.BorderWidth = 0;
        // GameUI caps a ring at 255 px: two rings, one inside the other, make the wide band
        _vrVigOuter = new UIPanel { X = 0, Y = 0, Width = 1300, Height = 1300, BackgroundColor = Color.FromArgb(0, 0, 0, 0), BorderColor = Color.FromArgb(240, 0, 0, 0), CornerRadius = 650, BorderWidth = 1 };
        _vrVigInner = new UIPanel { BackgroundColor = Color.FromArgb(0, 0, 0, 0), BorderColor = Color.FromArgb(240, 0, 0, 0), BorderWidth = 1, Visible = false };
        p.AddChild(_vrVigOuter);
        p.AddChild(_vrVigInner);
    }

    // How to play, for anyone handed the headset
    private void BuildVrHelp()
    {
        _vrHelp = new VrPanel(800, 520, 0.0011f);
        var p = _vrHelp.Panel;
        p.AddChild(new UILabel { X = 24, Y = 16, Text = "Controls", FontSize = FontSize.Heading, Color = Color.FromArgb(255, 235, 240, 248) });
        string[] lines =
        {
            "Left stick: walk (click it, or squeeze a grip, to run)",
            "Right stick: turn (click it to switch smooth / snap turning)",
            "Right trigger: swing the tool - chop, mine, hit (hold to keep going); or just swing it",
            "Left trigger: eat or drink what is in your hand, or place a block",
            "A / B: next / previous hotbar slot",
            "Y: feed a campfire, or drink from water you point at",
            "Crouch for real to sneak: animals and the infected notice you from half as far",
            "X: this menu (hotbar, eat, drink, heal, sleep, backpack, crafting)",
            "Point the right controller at a button and pull its trigger.",
            "The numbers over your left wrist: health, food, water, stamina, warmth.",
            "Stand by a fire at night. Wolves hunt in the dark; the infected live in the ruins.",
        };
        for (int i = 0; i < lines.Length; i++)
            p.AddChild(new UILabel { X = 24, Y = 70 + i * 32, Width = 760, Text = lines[i], FontSize = FontSize.Body, Color = Color.FromArgb(255, 205, 215, 228) });
        Button(p, "Back", 24, 460, () => { _vrMenu!.Model = _vrHelp!.Model; _vrHelp.IsOpen = false; _vrMenu.IsOpen = true; });
    }

    private static string Short(string name, int max) => name.Length <= max ? name : name[..(max - 1)] + ".";

    private static void Button(UIPanel parent, string text, float x, float y, Action onClick)
    {
        var b = new UIButton { Text = text, X = x, Y = y, Width = 120, Height = 50 };
        b.OnClick = onClick;
        parent.AddChild(b);
    }

    private void VrRespawn()
    {
        HandleRespawnClicked();
        // The player is back at the spawn: the room is carried there
        _vr?.Start(Renderer.Camera.Position, Renderer.Camera.Yaw);
        _vrStepFrom = null;
        _vrMenu!.IsOpen = false;
    }

    // --- One frame of play in the headset (after the world was drawn) ---

    private void VrGameFrame(XrFrameData f)
    {
        if (_vr == null || !_vr.Ready) return;
        EnsureVrUi();
        float dt = _vrLastTime < 0 ? 0f : (float)Math.Clamp((f.Time - _vrLastTime) / 1000.0, 0.0, 0.1);
        _vrLastTime = f.Time;
        var m = _vr.WorldFromRoomNow ?? Matrix4x4.Identity;
        var head = _vr.HeadInWorld;

        // The aim is the right controller's ray; without one, where the head looks. The desktop camera is put there so the game's own handlers use it.
        Vector3 aimOrigin, aimDir;
        if (_vr.RightHand is { } hand) { aimOrigin = hand.Position; aimDir = hand.Direction; }
        else { aimOrigin = head; aimDir = Vector3.Normalize(Vector3.TransformNormal(Vector3.Transform(-Vector3.UnitZ, f.HeadOrientation), m)); }
        var (yaw, pitch) = XrMath.AnglesOf(aimDir);
        Renderer.Camera.Position = aimOrigin;
        Renderer.Camera.Yaw = yaw;
        Renderer.Camera.Pitch = Math.Clamp(pitch, -89f, 89f);

        bool dead = Hud.IsDead;
        if (dead && !_vrMenu!.IsOpen) { _vrPack!.IsOpen = false; _vrHelp!.IsOpen = false; _vrSettings!.IsOpen = false; OpenVrMenu(f); }
        _vrRespawn!.Visible = dead;

        // The menu: X opens and closes it; the right ray points at it and the trigger clicks
        if (f.LeftPrimary && !_vrXWas && !dead)
        {
            if (_vrMenu!.IsOpen || _vrPack!.IsOpen || _vrHelp!.IsOpen || _vrSettings!.IsOpen) { _vrMenu.IsOpen = false; _vrPack!.IsOpen = false; _vrHelp!.IsOpen = false; _vrSettings!.IsOpen = false; } else OpenVrMenu(f);
        }
        _vrXWas = f.LeftPrimary;
        Vector3? ro = f.RightHasRay ? f.RightRayOrigin : null;
        Vector3? rd = f.RightHasRay ? f.RightRayDirection : null;
        var pointed = _vrSettings!.IsOpen ? _vrSettings : _vrHelp!.IsOpen ? _vrHelp : _vrPack!.IsOpen ? _vrPack : _vrMenu!;
        bool menuOpen = _vrMenu!.IsOpen || _vrPack!.IsOpen || _vrHelp!.IsOpen || _vrSettings.IsOpen;
        bool onMenu = menuOpen && pointed.Step(ro, rd, f.RightTrigger, dt);
        _vrUi!.ShowRay = menuOpen && ro != null;
        if (ro is { } rayO && rd is { } rayD)
        {
            _vrUi.RayOrigin = rayO;
            _vrUi.RayDirection = rayD;
            _vrUi.RayHovering = onMenu;
            if (pointed.RayToPanelPixel(rayO, rayD, out float hitDistance) != null) _vrUi.RayHit = hitDistance;
        }

        // Play: only with the menu shut and the player alive
        bool playing = !menuOpen && !dead;
        if (playing)
        {
            if (f.RightTrigger)
            {
                _vrRepeat -= dt;
                if (!_vrRtWas || _vrRepeat <= 0f)
                {
                    _vrRepeat = 0.4f;
                    Hud.TriggerSwing(1f);
                    HandleInteract();
                }
            }
            if (f.LeftTrigger && !_vrLtWas) VrUseOrPlace();
            if (f.RightPrimary && !_vrAWas) Inventory.ActiveHotbarIndex = (Inventory.ActiveHotbarIndex + 1) % 9;
            if (f.RightSecondary && !_vrBWas) Inventory.ActiveHotbarIndex = (Inventory.ActiveHotbarIndex + 8) % 9;
            if (f.LeftSecondary && !_vrYWas) HandleFeedFire();
        }
        VrPhysicalSwing(f, dt, aimOrigin, aimDir, playing);
        _vrRtWas = f.RightTrigger;
        _vrLtWas = f.LeftTrigger;
        _vrAWas = f.RightPrimary;
        _vrBWas = f.RightSecondary;
        _vrYWas = f.LeftSecondary;
        // A press is the moment the browser lets sound start (idempotent after the first)
        if (f.RightTrigger || f.LeftTrigger || f.LeftPrimary || f.RightPrimary)
        {
            Audio.EnsureActive();
            Audio.MasterVolume = Settings.MasterVolume;
        }

        VrSurvivalTick(dt, f);
        VrUpdatePanels(f, dt, m, head);
    }

    // A real swing: the controller moving fast with the working end of the tool at something (an animal, a block) strikes it, as the trigger would
    private Vector3? _vrHandRoomPrev;
    private Vector3 _vrHandVelocity;
    private float _vrSwingCooldown;

    private void VrPhysicalSwing(XrFrameData f, float dt, Vector3 aimOrigin, Vector3 aimDir, bool allowed)
    {
        _vrSwingCooldown = MathF.Max(0f, _vrSwingCooldown - dt);
        if (!f.RightHasRay || dt <= 0f) { _vrHandRoomPrev = null; return; }
        // The speed is measured in the room (walking with the stick moves the whole room through the world, not the hand through the room)
        var now = f.RightRayOrigin;
        if (_vrHandRoomPrev is { } prev) _vrHandVelocity = Vector3.Lerp(_vrHandVelocity, (now - prev) / dt, 0.5f);
        _vrHandRoomPrev = now;
        if (!allowed || _vrSwingCooldown > 0f || _vrHandVelocity.Length() < 2.4f) return;

        var held = Inventory.ActiveItem;
        bool tool = held != null && HeldModels.For(held.Id) != null;
        var reach = tool ? Vector3.Normalize(aimDir + Vector3.UnitY * 0.9f) : aimDir;
        var tip = aimOrigin + reach * (tool ? 0.55f : 0.2f);

        Vector3? target = null;
        float nearest = 0.9f;
        foreach (var e in Wildlife.Entities)
        {
            var centre = new Vector3(e.Position.X, e.Position.Y + EntityService.AimHeight(e.Kind), e.Position.Z);
            float d = Vector3.Distance(tip, centre);
            if (d < nearest) { nearest = d; target = centre; }
        }
        if (target == null)
        {
            for (float t = 0f; t <= 0.3f; t += 0.15f)
            {
                var p = tip + reach * t;
                int bx = (int)MathF.Floor(p.X), by = (int)MathF.Floor(p.Y), bz = (int)MathF.Floor(p.Z);
                if (World.IsSolidAt(bx, by, bz)) { target = new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f); break; }
            }
        }
        if (target is not { } struck) return;

        // Looked at from the hand, so the desktop handler finds what the tool met
        var (yaw, pitch) = XrMath.AnglesOf(struck - aimOrigin);
        Renderer.Camera.Yaw = yaw;
        Renderer.Camera.Pitch = Math.Clamp(pitch, -89f, 89f);
        _vrSwingCooldown = 0.5f;
        Hud.TriggerSwing(1f);
        Vr.Session.Pulse(false, 0.6f, 40);
        HandleInteract();
    }

    // The left trigger: use what is in the hand (eat it, drink it) or put it down as a block
    private void VrUseOrPlace()
    {
        var item = Inventory.ActiveItem;
        if (item != null && Inventory.Effects.ContainsKey(item.Id) && Inventory.TryUseSlot(fromHotbar: true, index: Inventory.ActiveHotbarIndex)) return;
        HandlePlaceBlock();
    }

    // What the desktop frame does besides moving: the clock, hunger and warmth, the fires, loot, the buildings' loot and the autosave
    private void VrSurvivalTick(float dt, XrFrameData f)
    {
        bool sprinting = (f.LeftThumb || f.LeftGrip || f.RightGrip) && (f.LeftStick.LengthSquared() > 0.05f) && Stats.Stamina > 0.05f && Stats.BleedSecondsRemaining <= 0;
        Wildlife.DayNumber = Time.DayNumber;
        float ambient = ComputeAmbientTemperature(dt);
        Stats.StaminaRegenBlocked = IsHostileNearby(Renderer.Camera.Position, 6f);
        Stats.Tick(dt, ambient, sprinting);
        // Crouching for real (the head well below the tallest it has been lately) is sneaking: the desktop's Ctrl, which the handlers look for
        _vrStandHeight = MathF.Max(_vrStandHeight * 0.9995f, f.HeadPosition.Y);
        bool sneaking = f.HeadPosition.Y < _vrStandHeight - 0.4f;
        if (sneaking) Input.KeysDown.Add("ControlLeft"); else Input.KeysDown.Remove("ControlLeft");
        Hud.IsSneaking = sneaking;
        Wildlife.WolfAggroRange = (Fires.GetWarmthBonusAt(Renderer.Camera.Position) > 0.01f ? 5f : 10f) * (sneaking ? 0.5f : 1f);
        Wildlife.BearAggroRange = sneaking ? 7.5f : 15f;
        Wildlife.InfectedAggroRange = sneaking ? 9f : 18f;
        Fires.FuelDecayMultiplier = 1f + Weather.RainIntensity * 1.5f;
        Fires.Tick(dt, Renderer.Camera.Position, Inventory);
        Pois.Tick(dt, Renderer.Camera.Position);
        Ground.Tick(Renderer.Camera.Position, Inventory);
        _saveTimer += dt;
        if (_saveTimer >= SaveService.AutoSaveIntervalSeconds)
        {
            _saveTimer = 0;
            // Where the head is and looks (the camera is at the right hand)
            var (headAt, headYaw, headPitch) = _vr!.Pose;
            Save.SaveNow(headAt, headYaw, Math.Clamp(headPitch, -80f, 80f));
        }
        // The HUD's timers (swings, toasts, numbers) run on this too
        Hud.Update(dt, Renderer.Camera.Position, Renderer.Camera.Yaw);

        // The sounds: weather, danger, the night, the fire, and a footfall for each stride walked
        Audio.UpdateRainAmbient(Weather.RainIntensity);
        TickDangerAudio();
        TickThunderQueue(dt);
        TickAmbientAudio(dt);
        var head = _vr!.HeadInWorld;
        if (_vrStepFrom is { } from)
        {
            float dx = head.X - from.X, dz = head.Z - from.Z;
            _vrStepDistance += MathF.Sqrt(dx * dx + dz * dz);
            if (_vrStepDistance >= 1.7f)
            {
                _vrStepDistance = 0f;
                _vrStepAlt = !_vrStepAlt;
                if (IsInWater() || Weather.RainIntensity > 0.25f) Audio.PlayWaterStep(_vrStepAlt);
                else Audio.PlayStepSurface(GetUnderfootBlock(), _vrStepAlt);
            }
        }
        _vrStepFrom = head;
    }

    private void OpenVrMenu(XrFrameData f)
    {
        var fwd = Vector3.Transform(-Vector3.UnitZ, f.HeadOrientation);
        fwd.Y = 0;
        fwd = fwd.LengthSquared() < 1e-8f ? -Vector3.UnitZ : Vector3.Normalize(fwd);
        var at = f.HeadPosition + fwd * 0.75f - Vector3.UnitY * 0.12f;
        _vrMenu!.Model = VrPanel.FacingUpright(at, f.HeadPosition);
        _vrMenu.IsOpen = true;
    }

    private void VrUpdatePanels(XrFrameData f, float dt, Matrix4x4 m, Vector3 head)
    {
        var headRoom = f.HeadPosition;
        var fwdRoom = Vector3.Transform(-Vector3.UnitZ, f.HeadOrientation);
        var upRoom = Vector3.Transform(Vector3.UnitY, f.HeadOrientation);
        var rightRoom = Vector3.Transform(Vector3.UnitX, f.HeadOrientation);

        // The numbers, over the left wrist (or low on the left of the view when that hand is not tracked)
        float[] values = { Stats.Health, Stats.Hunger, Stats.Thirst, Stats.Stamina, Stats.Temperature };
        for (int i = 0; i < _vrBars.Length; i++)
            _vrBars[i].Fill.Width = MathF.Max(2f, 190f * Math.Clamp(values[i], 0f, 1f));
        int minutes = (int)(Time.DayFraction * 24f * 60f);
        _vrWristHeader!.Text = $"Day {Time.DayNumber + 1}   {minutes / 60:D2}:{minutes % 60:D2}   Lv {Stats.Level}";
        var held = Inventory.ActiveItem;
        _vrWristHeld!.Text = held == null ? $"[{Inventory.ActiveHotbarIndex + 1}] empty hands" : $"[{Inventory.ActiveHotbarIndex + 1}] {held.Name}" + (held.Count > 1 ? $" x{held.Count}" : "");
        _vrWrist!.Model = f.LeftHasRay
            ? VrPanel.Facing(f.LeftRayOrigin + Vector3.UnitY * 0.13f + (headRoom - f.LeftRayOrigin) * 0.12f, headRoom)
            : VrPanel.Facing(headRoom + fwdRoom * 0.55f - rightRoom * 0.32f - upRoom * 0.30f, headRoom);

        // The latest message, low in front of the eyes for a few seconds
        _vrToastAge += dt;
        _vrToast!.IsOpen = _vrToastAge < 3.5f;
        _vrToast.Model = VrPanel.Facing(headRoom + fwdRoom * 0.9f - upRoom * 0.22f, headRoom);

        // A hit reddens the view for a moment
        _vrFlashLevel = MathF.Max(0f, _vrFlashLevel - dt * 2.2f);
        _vrFlash!.IsOpen = _vrFlashLevel > 0.01f;
        _vrFlash.Panel.BackgroundColor = Color.FromArgb((int)(150 * _vrFlashLevel), 200, 20, 20);
        _vrFlash.Model = VrPanel.Facing(headRoom + fwdRoom * 0.35f, headRoom);

        if (_vrPack!.IsOpen)
            for (int i = 0; i < InventoryService.BackpackSize; i++)
            {
                var it = Inventory.Backpack[i];
                _vrPackButtons[i].Text = it == null ? "-" : Short(it.Name, 9) + (it.Count > 1 ? $" x{it.Count}" : "");
            }

        // The comfort vignette follows how hard the sticks are pushed
        float effort = Vr.Options.ComfortVignette ? _vr!.StickEffort : 0f;
        _vrVignetteLevel += (effort - _vrVignetteLevel) * MathF.Min(1f, dt * 5f);
        _vrVignette!.IsOpen = _vrVignetteLevel > 0.02f;
        // The panel is 1.3 m square, 0.27 m from the eyes: the open middle shrinks from 0.4 m to 0.17 m a side (about 110 to 38 degrees across)
        float hole = 0.40f - 0.23f * _vrVignetteLevel;
        float band = MathF.Max(1f, 650f - hole * 1000f);
        float outer = MathF.Min(band, 250f), inner = band - outer;
        _vrVigOuter!.BorderWidth = outer;
        _vrVigInner!.Visible = inner > 1f;
        if (inner > 1f)
        {
            _vrVigInner.X = _vrVigInner.Y = outer;
            _vrVigInner.Width = _vrVigInner.Height = 1300f - 2f * outer;
            _vrVigInner.CornerRadius = MathF.Max(0f, 650f - outer);
            _vrVigInner.BorderWidth = inner;
        }
        _vrVignette.Model = VrPanel.Facing(headRoom + fwdRoom * 0.27f, headRoom);

        // The menu's live parts
        if (_vrMenu!.IsOpen)
        {
            _vrMenuStatus!.Text = Hud.IsDead ? "You died." : $"HP {(int)(Stats.Health * 100)}%   Food {(int)(Stats.Hunger * 100)}%   Water {(int)(Stats.Thirst * 100)}%   Warmth {(int)(Stats.Temperature * 100)}%";
            for (int i = 0; i < 9; i++)
            {
                var it = i < Inventory.Hotbar.Count ? Inventory.Hotbar[i] : null;
                string name = it == null ? "-" : Short(it.Name, 9) + (it.Count > 1 ? $" x{it.Count}" : "");
                _vrHotbarButtons[i].Text = (i == Inventory.ActiveHotbarIndex ? "> " : "") + (i + 1) + " " + name;
            }
            foreach (var (button, recipe) in _vrCraftButtons) button.Enabled = Crafter.CanCraft(recipe);
        }
    }
}
