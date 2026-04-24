using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;

namespace LostSpawns.Services;

/// <summary>
/// Procedural audio via Web Audio API - no asset files needed. Each sound
/// is built on the fly from an OscillatorNode into a GainNode with a short
/// attack / release envelope, so PlayHit / PlayPickup / PlayLevelUp are
/// ~20 lines of parameters each.
///
/// The browser requires a user gesture before an AudioContext can play,
/// so we defer Resume() until the first pointer-lock click from Game.razor.
/// Calls before Resume happens are still issued but silent; they don't
/// queue, they just miss. Fine for gameplay feedback - you'd rather miss
/// the first beep than defer the game behind audio.
/// </summary>
public class AudioService : IDisposable
{
    private readonly BlazorJSRuntime _js;
    private AudioContext? _ctx;
    private bool _resumed;

    public AudioService(BlazorJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Ensure AudioContext exists + is resumed. Safe to call many times;
    /// the AudioContext.Resume() promise is fire-and-forget so we don't
    /// block gameplay waiting for audio to come online.
    /// </summary>
    public void EnsureActive()
    {
        try
        {
            if (_ctx is null) _ctx = new AudioContext();
            if (!_resumed)
            {
                _ = _ctx.Resume();
                _resumed = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] EnsureActive failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Play a brief tone. freq in Hz, duration in seconds, volume [0, 1].
    /// Envelope is a fixed 5ms attack + exponential release to avoid the
    /// click that raw oscillator start / stop produces.
    /// </summary>
    public void PlayBeep(float freq, float duration, float volume = 0.2f, string type = "sine")
    {
        if (_ctx is null) return; // not initialized yet - silent fail
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = type;
            osc.Frequency.SetValueAtTime(freq, _ctx.CurrentTime);
            gain.Gain.SetValueAtTime(0, _ctx.CurrentTime);
            gain.Gain.LinearRampToValueAtTime(volume, _ctx.CurrentTime + 0.005);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, _ctx.CurrentTime + duration);
            osc.Connect(gain);
            gain.Connect(_ctx.Destination);
            osc.Start();
            osc.Stop((float)(_ctx.CurrentTime + duration));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayBeep failed: {ex.Message}");
        }
    }

    /// <summary>Two-note ascending chime - used for level up and achievements.</summary>
    public void PlayLevelUp()
    {
        if (_ctx is null) return;
        PlayBeep(660f, 0.12f, 0.18f, "triangle");
        Task.Delay(100).ContinueWith(_ => PlayBeep(880f, 0.20f, 0.22f, "triangle"));
    }

    /// <summary>Short soft click for inventory pickup events.</summary>
    public void PlayPickup()
    {
        PlayBeep(520f, 0.08f, 0.14f, "triangle");
    }

    /// <summary>Low thud on successful swing hit (block or entity).</summary>
    public void PlayHit()
    {
        PlayBeep(180f, 0.10f, 0.22f, "square");
    }

    /// <summary>Short upward blip - player jump push-off.</summary>
    public void PlayJump()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "sine";
            double t = _ctx.CurrentTime;
            osc.Frequency.SetValueAtTime(180f, t);
            osc.Frequency.ExponentialRampToValueAtTime(280f, t + 0.08);
            gain.Gain.SetValueAtTime(0f, t);
            gain.Gain.LinearRampToValueAtTime(0.10f, t + 0.01);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.1);
            osc.Connect(gain);
            gain.Connect(_ctx.Destination);
            osc.Start();
            osc.Stop((float)(t + 0.1));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayJump failed: {ex.Message}");
        }
    }

    /// <summary>Landing impact thump - scales gain with fall speed.</summary>
    public void PlayLand(float intensity)
    {
        if (intensity < 0.05f) return;
        PlayBeep(80f, 0.08f, Math.Clamp(intensity * 0.25f, 0.05f, 0.3f), "triangle");
    }

    /// <summary>Quick airy whoosh - missed swing at nothing.</summary>
    public void PlayWhoosh()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "sine";
            double t = _ctx.CurrentTime;
            // Descending whistle - 500 -> 250 Hz over 120ms at low gain.
            osc.Frequency.SetValueAtTime(500f, t);
            osc.Frequency.ExponentialRampToValueAtTime(250f, t + 0.12);
            gain.Gain.SetValueAtTime(0f, t);
            gain.Gain.LinearRampToValueAtTime(0.08f, t + 0.02);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.14);
            osc.Connect(gain);
            gain.Connect(_ctx.Destination);
            osc.Start();
            osc.Stop((float)(t + 0.14));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayWhoosh failed: {ex.Message}");
        }
    }

    /// <summary>Sharp rising tone for damage taken.</summary>
    public void PlayDamage()
    {
        PlayBeep(220f, 0.18f, 0.25f, "sawtooth");
    }

    /// <summary>
    /// Quiet low-pitched thump for each footstep. Alternating high/low
    /// phase keeps two consecutive steps from sounding identical.
    /// </summary>
    public void PlayStep(bool altPhase)
    {
        PlayBeep(altPhase ? 130f : 110f, 0.04f, 0.06f, "sine");
    }

    /// <summary>Deep rumbling thunder - long triangle fade after a lightning strike.</summary>
    public void PlayThunder()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "triangle";
            double t = _ctx.CurrentTime;
            // Two-frequency rumble - starts low, wavers up, decays over
            // nearly 2 seconds for weight. Small frequency sweep gives the
            // "rolling" feel that distinguishes thunder from a single hit.
            osc.Frequency.SetValueAtTime(65f, t);
            osc.Frequency.LinearRampToValueAtTime(85f, t + 0.6);
            osc.Frequency.LinearRampToValueAtTime(55f, t + 1.6);
            gain.Gain.SetValueAtTime(0f, t);
            gain.Gain.LinearRampToValueAtTime(0.35f, t + 0.05);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 1.8);
            osc.Connect(gain);
            gain.Connect(_ctx.Destination);
            osc.Start();
            osc.Stop((float)(t + 1.8));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayThunder failed: {ex.Message}");
        }
    }

    /// <summary>Short rising chime for consuming food / water / medical.</summary>
    public void PlayConsume()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "sine";
            double t = _ctx.CurrentTime;
            osc.Frequency.SetValueAtTime(400f, t);
            osc.Frequency.ExponentialRampToValueAtTime(560f, t + 0.18);
            gain.Gain.SetValueAtTime(0f, t);
            gain.Gain.LinearRampToValueAtTime(0.15f, t + 0.02);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.22);
            osc.Connect(gain);
            gain.Connect(_ctx.Destination);
            osc.Start();
            osc.Stop((float)(t + 0.22));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayConsume failed: {ex.Message}");
        }
    }

    /// <summary>Brief crackle/pop for a campfire ember. Random pitch per call.</summary>
    public void PlayFireCrackle()
    {
        var rng = new Random();
        float f = 180f + (float)rng.NextDouble() * 120f;
        PlayBeep(f, 0.05f, 0.04f, "square");
    }

    /// <summary>
    /// Long descending sawtooth for a wolf howl - plays when a wolf spawns
    /// at night so the player hears them arrive before they see them.
    /// </summary>
    public void PlayWolfHowl()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "sawtooth";
            double t = _ctx.CurrentTime;
            // Slow pitch sweep 300 -> 180 Hz over 0.8s gives the howl its
            // bending tail; gain ramps in fast then fades for the full beat.
            osc.Frequency.SetValueAtTime(300f, t);
            osc.Frequency.ExponentialRampToValueAtTime(180f, t + 0.8);
            gain.Gain.SetValueAtTime(0, t);
            gain.Gain.LinearRampToValueAtTime(0.18f, t + 0.08);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 1.2);
            osc.Connect(gain);
            gain.Connect(_ctx.Destination);
            osc.Start();
            osc.Stop((float)(t + 1.2));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayWolfHowl failed: {ex.Message}");
        }
    }

    // Persistent rain ambient - one oscillator + gain node reused across the
    // whole session. Frequency is a high broadband hiss approximation (real
    // white noise would need a noise buffer - sawtooth at ~1200 Hz is close
    // enough for ambient rain at low volume).
    private OscillatorNode? _rainOsc;
    private GainNode? _rainGain;

    /// <summary>
    /// Update the rain ambient loop. Intensity is [0, 1]; 0 stops the loop,
    /// >0 starts it (idempotent) and sets the gain envelope. Call this every
    /// tick with WeatherService.RainIntensity; AudioService handles the
    /// start / gain-ramp / stop plumbing internally.
    /// </summary>
    public void UpdateRainAmbient(float intensity)
    {
        if (_ctx is null) return;
        try
        {
            if (intensity > 0.05f && _rainOsc is null)
            {
                // First crossing into rain - spin up the persistent loop.
                _rainOsc = _ctx.CreateOscillator();
                _rainGain = _ctx.CreateGain();
                _rainOsc.Type = "sawtooth";
                _rainOsc.Frequency.SetValueAtTime(1200f, _ctx.CurrentTime);
                _rainGain.Gain.SetValueAtTime(0f, _ctx.CurrentTime);
                _rainOsc.Connect(_rainGain);
                _rainGain.Connect(_ctx.Destination);
                _rainOsc.Start();
            }
            if (_rainGain is not null)
            {
                // Target gain scales with intensity, capped low so it reads as
                // ambient rain not a fog horn. Ramp to target with a short
                // time constant so cuts + swells feel smooth.
                float target = Math.Clamp(intensity * 0.05f, 0f, 0.06f);
                _rainGain.Gain.LinearRampToValueAtTime(target, _ctx.CurrentTime + 0.5);
            }
            if (intensity <= 0.05f && _rainOsc is not null)
            {
                // Tear down the loop once rain's fully stopped. Can't Dispose
                // a started oscillator after Stop(); let the GC clean up once
                // Stop() schedule completes.
                _rainGain?.Gain.LinearRampToValueAtTime(0f, _ctx.CurrentTime + 0.3);
                _rainOsc.Stop((float)(_ctx.CurrentTime + 0.35));
                _rainOsc.Dispose();
                _rainGain?.Dispose();
                _rainOsc = null;
                _rainGain = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] UpdateRainAmbient failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try { _rainOsc?.Stop(); } catch { }
        _rainOsc?.Dispose();
        _rainGain?.Dispose();
        try { _ctx?.Close(); } catch { }
        _ctx?.Dispose();
        _ctx = null;
    }
}
