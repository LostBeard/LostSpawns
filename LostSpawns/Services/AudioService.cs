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

    public void Dispose()
    {
        try { _ctx?.Close(); } catch { }
        _ctx?.Dispose();
        _ctx = null;
    }
}
