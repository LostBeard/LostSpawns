using System.Numerics;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace LostSpawns.Xr;

/// <summary>
/// A WebXR session (immersive-vr or immersive-ar) on the page's own thread - WebXR is not available in a worker. The world is drawn by WebGPU and copied to the headset's WebGL layer
/// (<see cref="WebGlXrBlit"/>): the WebGL layer is what every headset browser has. Each animation frame of the session produces an <see cref="XrFrameData"/>.
/// </summary>
public sealed class XrSession : IDisposable
{
    private readonly SpawnJSRuntime _js;
    private XRSession? _session;
    private XRReferenceSpace? _refSpace;
    private ActionCallback<double, XRFrame?>? _rafCallback;
    private HTMLCanvasElement? _glCanvas;
    private WebGL2RenderingContext? _glContext;
    private XRWebGLLayer? _layer;
    private bool? _vrSupported, _arSupported;
    private int _frames;

    public XrSession(SpawnJSRuntime js) => _js = js;

    public bool IsActive => _session != null;
    public string? Mode { get; private set; }
    /// <summary>"opaque" (VR), "alpha-blend" (passthrough AR: the layer's alpha shows the camera feed) or "additive" (see-through displays).</summary>
    public string? EnvironmentBlendMode { get; private set; }
    public WebGL2RenderingContext? GL => _glContext;
    public XRWebGLLayer? Layer => _layer;
    public int FramesDrawn => _frames;

    /// <summary>Fired each animation frame of the session.</summary>
    public event Action<XrFrameData>? OnFrame;
    /// <summary>Fired when the session ends, however it ends (the headset's own button, <see cref="ExitAsync"/>).</summary>
    public event Action? OnEnded;

    public async Task<bool> IsVrSupportedAsync() => _vrSupported ??= await IsSupportedAsync("immersive-vr");
    public async Task<bool> IsArSupportedAsync() => _arSupported ??= await IsSupportedAsync("immersive-ar");

    private async Task<bool> IsSupportedAsync(string mode)
    {
        try
        {
            using var navigator = _js.Get<Navigator>("navigator");
            using var xr = navigator.XR;
            return xr != null && await xr.IsSessionSupported(mode);
        }
        catch { return false; }
    }

    /// <summary>Enter a session. Call it straight from the click that asks for it: an await before it can spend the click's user activation.</summary>
    public async Task EnterAsync(string mode)
    {
        if (_session != null) return;
        using var navigator = _js.Get<Navigator>("navigator");
        using var xr = navigator.XR ?? throw new InvalidOperationException("WebXR is not available in this browser");

        _session = await xr.RequestSession(mode, new XRSessionInit
        {
            RequiredFeatures = new[] { "local-floor" },
            // Hands as input where the headset has them (a browser without them ignores the word)
            OptionalFeatures = new[] { "hand-tracking" },
        });
        Mode = mode;
        try { EnvironmentBlendMode = _session.EnvironmentBlendMode; } catch { EnvironmentBlendMode = null; }
        _session.OnEnd += OnSessionEnd;
        // A hand has no buttons: a pinch (thumb to index finger) is a select, thumb to middle finger a squeeze
        _session.OnSelectStart += e => OnHandGesture(e, true, squeeze: false);
        _session.OnSelectEnd += e => OnHandGesture(e, false, squeeze: false);
        _session.OnSqueezeStart += e => OnHandGesture(e, true, squeeze: true);
        _session.OnSqueezeEnd += e => OnHandGesture(e, false, squeeze: true);
        _refSpace = await _session.RequestReferenceSpace("local-floor");

        using var doc = _js.Get<Document>("document");
        _glCanvas = doc.CreateElement<HTMLCanvasElement>("canvas");
        _glContext = _glCanvas.GetContext<WebGL2RenderingContext>("webgl2", new WebGLContextAttributes { XrCompatible = true })
            ?? throw new InvalidOperationException("Could not make a WebGL2 context the headset can use");
        _layer = new XRWebGLLayer(_session, _glContext);
        _session.UpdateRenderState(new XRRenderStateInit { BaseLayer = _layer, DepthNear = 0.05, DepthFar = 3000 });

        _frames = 0;
        _rafCallback = new ActionCallback<double, XRFrame?>(OnAnimationFrame);
        _session.RequestAnimationFrame(_rafCallback);
        Console.WriteLine($"[Xr] {mode} session started (blend mode {EnvironmentBlendMode ?? "unknown"})");
    }

    public async Task ExitAsync()
    {
        if (_session == null) return;
        try { await _session.End(); } catch { }
        Cleanup();
    }

    /// <summary>End the session from inside a frame (a controller button): the end waits for the frame to finish.</summary>
    public void RequestEnd() => _ = EndAfterThisFrameAsync();

    private async Task EndAfterThisFrameAsync()
    {
        await Task.Yield();
        try { if (_session != null) await _session.End(); }
        catch (Exception ex) { Console.WriteLine($"[Xr] end failed: {ex.Message}"); }
    }

    private void OnSessionEnd(XRSessionEvent e)
    {
        Console.WriteLine("[Xr] session ended");
        // Listeners first, while the session's GL context is still alive
        try { OnEnded?.Invoke(); }
        catch (Exception ex) { Console.WriteLine($"[Xr] session-ended listener failed: {ex.Message}"); }
        Cleanup();
    }

    private void Cleanup()
    {
        _rafCallback?.Dispose();
        _rafCallback = null;
        _layer?.Dispose(); _layer = null;
        _glContext?.Dispose(); _glContext = null;
        _glCanvas?.Dispose(); _glCanvas = null;
        _refSpace?.Dispose(); _refSpace = null;
        if (_session != null)
        {
            _session.OnEnd -= OnSessionEnd;
            _session.Dispose();
            _session = null;
        }
        Mode = null;
        EnvironmentBlendMode = null;
    }

    private void OnAnimationFrame(double time, XRFrame? frame)
    {
        if (_session == null || _refSpace == null || frame == null) return;
        _session.RequestAnimationFrame(_rafCallback!);   // the next frame first, so the loop continues whatever this one does
        _frames++;
        try
        {
            using var pose = frame.GetViewerPose(_refSpace);
            if (pose == null) return;
            var views = pose.Views;
            using var head = pose.Transform;
            var hp = head.Position; var ho = head.Orientation;
            var data = new XrFrameData
            {
                Time = time,
                HeadPosition = new Vector3((float)hp.X, (float)hp.Y, (float)hp.Z),
                HeadOrientation = new Quaternion((float)ho.X, (float)ho.Y, (float)ho.Z, (float)ho.W),
                Views = new XrViewData[views.Length],
            };
            for (int i = 0; i < views.Length; i++)
            {
                var view = views[i];
                using var projection = view.ProjectionMatrix;
                using var transform = view.Transform;
                var p = transform.Position; var o = transform.Orientation;
                data.Views[i] = new XrViewData
                {
                    Eye = view.Eye,
                    Projection = XrMath.FromWebXrMatrix((float[])projection),
                    RoomView = XrMath.ViewFromPose(new Vector3((float)p.X, (float)p.Y, (float)p.Z), new Quaternion((float)o.X, (float)o.Y, (float)o.Z, (float)o.W)),
                    Viewport = _layer!.GetViewport(view),
                };
            }
            ReadControllers(data, frame);
            OnFrame?.Invoke(data);
            foreach (var v in views) v.Dispose();
        }
        catch (Exception ex)
        {
            if (_frames < 5 || _frames % 600 == 0) Console.WriteLine($"[Xr] frame error: {ex.Message}");
        }
    }

    // What each hand is doing with its fingers (set by the session's select and squeeze events: only a HAND's - a controller's trigger is read from its gamepad)
    private volatile bool _pinchLeft, _pinchRight, _squeezeLeft, _squeezeRight;

    private void OnHandGesture(XRInputSourceEvent e, bool down, bool squeeze)
    {
        try
        {
            using var source = e.InputSource;
            using var hand = source.Hand;
            if (hand == null) return;
            bool left = source.Handedness == "left";
            if (squeeze) { if (left) _squeezeLeft = down; else _squeezeRight = down; }
            else { if (left) _pinchLeft = down; else _pinchRight = down; }
        }
        catch { /* an event for a source that is gone */ }
    }

    // A hand as an input source: where it points (the ray its aim pose gives), and its gestures as the buttons they stand for - a pinch is the trigger, a squeeze the other button
    private void ReadHand(XrFrameData data, XRInputSource source, XRFrame frame, bool left)
    {
        ReadRay(data, source, frame, left);
        if (left) { data.LeftIsHand = true; data.LeftTrigger = _pinchLeft; data.LeftPrimary = _squeezeLeft; }
        else { data.RightIsHand = true; data.RightTrigger = _pinchRight; data.RightPrimary = _squeezeRight; }
    }

    // The thumbsticks and buttons of the controllers (xr-standard: the stick is axes 2 and 3 - 0 and 1 are a touchpad - the trigger button 0, the grip 1, A/X 4, B/Y 5)
    private void ReadControllers(XrFrameData data, XRFrame frame)
    {
        using var sources = _session!.InputSources;
        for (int i = 0; i < sources.Length; i++)
        {
            using var source = sources[i];
            var handedness = source.Handedness;
            bool left = handedness == "left";
            if (!left && handedness != "right") continue;
            using (var hand = source.Hand)
            {
                if (hand != null) { ReadHand(data, source, frame, left); continue; }
            }
            using var gamepad = source.Gamepad;
            if (gamepad == null) continue;
            var axes = gamepad.Axes;
            int a = axes.Length >= 4 ? 2 : 0;
            if (axes.Length >= a + 2)
            {
                var stick = new Vector2((float)axes[a], (float)axes[a + 1]);
                if (left) data.LeftStick = stick; else data.RightStick = stick;
            }
            var buttons = gamepad.Buttons;
            bool trigger = buttons.Length > 0 && buttons[0].Pressed;
            bool grip = buttons.Length > 1 && buttons[1].Pressed;
            bool secondary = buttons.Length > 5 && buttons[5].Pressed;
            bool primary = buttons.Length > 4 && buttons[4].Pressed;
            bool thumb = buttons.Length > 3 && buttons[3].Pressed;
            foreach (var b in buttons) b.Dispose();
            PlayPendingPulse(gamepad, left);
            if (left) { data.LeftTrigger = trigger; data.LeftGrip = grip; data.LeftPrimary = primary; data.LeftSecondary = secondary; data.LeftThumb = thumb; }
            else { data.RightTrigger = trigger; data.RightGrip = grip; data.RightPrimary = primary; data.RightSecondary = secondary; data.RightThumb = thumb; }
            ReadRay(data, source, frame, left);
        }
    }

    // Where the controller points: its target ray in the room (an origin and a direction)
    private void ReadRay(XrFrameData data, XRInputSource source, XRFrame frame, bool left)
    {
        try
        {
            using var space = source.TargetRaySpace;
            if (space == null) return;
            using var pose = frame.GetPose(space, _refSpace!);
            if (pose == null) return;
            using var transform = pose.Transform;
            var p = transform.Position; var o = transform.Orientation;
            var origin = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
            var direction = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, new Quaternion((float)o.X, (float)o.Y, (float)o.Z, (float)o.W)));
            if (left) { data.LeftRayOrigin = origin; data.LeftRayDirection = direction; data.LeftHasRay = true; }
            else { data.RightRayOrigin = origin; data.RightRayDirection = direction; data.RightHasRay = true; }
        }
        catch (Exception ex)
        {
            if (_frames < 5) Console.WriteLine($"[Xr] no controller ray: {ex.Message}");
        }
        // Where the controller is held (the palm), which is behind the ray's origin: the wrist is a little further back still
        try
        {
            using var grip = source.GripSpace;
            if (grip == null) return;
            using var pose = frame.GetPose(grip, _refSpace!);
            if (pose == null) return;
            using var transform = pose.Transform;
            var p = transform.Position;
            var position = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
            if (left) { data.LeftGripPosition = position; data.LeftHasGrip = true; }
            else { data.RightGripPosition = position; data.RightHasGrip = true; }
        }
        catch (Exception ex)
        {
            if (_frames < 5) Console.WriteLine($"[Xr] no controller ray: {ex.Message}");
        }
    }

    // --- Haptics: a request made between frames is played on that controller's actuator when the frame reads its gamepad ---
    private (float Intensity, int Ms)? _pulseLeft, _pulseRight;

    /// <summary>Buzz a controller (intensity 0..1, milliseconds) at the next frame. A headset without haptics ignores it.</summary>
    public void Pulse(bool left, float intensity, int milliseconds)
    {
        if (left) _pulseLeft = (intensity, milliseconds); else _pulseRight = (intensity, milliseconds);
    }

    private void PlayPendingPulse(Gamepad gamepad, bool left)
    {
        var request = left ? _pulseLeft : _pulseRight;
        if (request is not { } p) return;
        if (left) _pulseLeft = null; else _pulseRight = null;
        try
        {
            var actuators = gamepad.HapticActuators;
            if (actuators == null || actuators.Length == 0) return;
            var first = actuators[0];
            if (!first.PulseSupported) { foreach (var a in actuators) a.Dispose(); return; }
            // The reference stays in use until the pulse call returns
            first.Pulse(p.Intensity, p.Ms).ContinueWith(_ => { foreach (var a in actuators) a.Dispose(); });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Xr] haptic pulse failed: {ex.Message}");
        }
    }

    public void Dispose() => Cleanup();
}

/// <summary>One frame of a session.</summary>
public sealed class XrFrameData
{
    public XrViewData[] Views { get; set; } = System.Array.Empty<XrViewData>();
    /// <summary>The headset in the room (local-floor space).</summary>
    public Vector3 HeadPosition { get; set; }
    public Quaternion HeadOrientation { get; set; } = Quaternion.Identity;
    /// <summary>The frame's time, milliseconds.</summary>
    public double Time { get; set; }
    /// <summary>Thumbsticks (x right, y down: pushed forward reads -1); zero without a controller.</summary>
    public Vector2 LeftStick { get; set; }
    public Vector2 RightStick { get; set; }
    public bool LeftGrip { get; set; }
    public bool RightGrip { get; set; }
    public bool LeftTrigger { get; set; }
    public bool RightTrigger { get; set; }
    /// <summary>X (the left controller's button 4) held; A (the right one's) is RightPrimary.</summary>
    public bool LeftPrimary { get; set; }
    public bool RightPrimary { get; set; }
    /// <summary>Y (the left controller's button 5) held; B (the right one's) is RightSecondary.</summary>
    public bool LeftSecondary { get; set; }
    public bool RightSecondary { get; set; }
    /// <summary>A thumbstick pressed in (xr-standard button 3).</summary>
    public bool LeftThumb { get; set; }
    public bool RightThumb { get; set; }
    /// <summary>Where each controller points, in the room (a unit direction); Has says it is tracked this frame.</summary>
    public bool LeftHasRay { get; set; }
    /// <summary>That side is a tracked HAND (no thumbstick, no buttons: gestures stand in for them).</summary>
    public bool LeftIsHand { get; set; }
    public bool RightIsHand { get; set; }
    public bool RightHasRay { get; set; }
    public Vector3 LeftRayOrigin { get; set; }
    public Vector3 LeftRayDirection { get; set; }
    /// <summary>Where each controller is held, in the room (null-like: Has says the headset gave it).</summary>
    public Vector3 LeftGripPosition { get; set; }
    public Vector3 RightGripPosition { get; set; }
    public bool LeftHasGrip { get; set; }
    public bool RightHasGrip { get; set; }
    public Vector3 RightRayOrigin { get; set; }
    public Vector3 RightRayDirection { get; set; }
}

/// <summary>One eye of a frame.</summary>
public sealed class XrViewData
{
    public string Eye { get; set; } = "none";
    /// <summary>The eye's projection (row-vector form of WebXR's OpenGL one).</summary>
    public Matrix4x4 Projection { get; set; }
    /// <summary>Room -> eye.</summary>
    public Matrix4x4 RoomView { get; set; }
    /// <summary>Where the eye's image goes in the layer's framebuffer.</summary>
    public XRViewport Viewport { get; set; } = null!;
}
