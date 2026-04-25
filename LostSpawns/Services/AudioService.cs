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
    private GainNode? _master;
    private bool _resumed;

    /// <summary>
    /// Master volume [0, 1] applied to every sound via a shared GainNode.
    /// Setting this mutes / fades ALL audio in one call. Defaults to 1 so
    /// the previous call-sites see no change until a settings UI wires
    /// this up.
    /// </summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            if (_master is not null && _ctx is not null)
                _master.Gain.LinearRampToValueAtTime(_masterVolume, _ctx.CurrentTime + 0.05);
        }
    }
    private float _masterVolume = 1f;

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
            if (_ctx is null)
            {
                _ctx = new AudioContext();
                _master = _ctx.CreateGain();
                _master.Gain.SetValueAtTime(_masterVolume, _ctx.CurrentTime);
                _master.Connect(_ctx.Destination);
            }
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

    /// <summary>Destination node every sound should connect into (master gain), falling back to context destination pre-init.</summary>
    private AudioNode Destination => (AudioNode?)_master ?? _ctx!.Destination;

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
            gain.Connect(Destination);
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

    /// <summary>Wood-chop sound - warmer mid-pitch square for axe hits.</summary>
    public void PlayChop()
    {
        PlayBeep(240f, 0.10f, 0.20f, "triangle");
    }

    /// <summary>Stone-mine sound - sharp high-pitch square for pick hits.</summary>
    public void PlayMine()
    {
        PlayBeep(360f, 0.09f, 0.20f, "square");
    }

    /// <summary>Single dull kick - used on the low-HP heartbeat pulse.</summary>
    public void PlayHeartbeat()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "sine";
            double t = _ctx.CurrentTime;
            osc.Frequency.SetValueAtTime(80f, t);
            osc.Frequency.ExponentialRampToValueAtTime(50f, t + 0.15);
            gain.Gain.SetValueAtTime(0f, t);
            gain.Gain.LinearRampToValueAtTime(0.22f, t + 0.02);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.18);
            osc.Connect(gain);
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 0.18));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayHeartbeat failed: {ex.Message}");
        }
    }

    /// <summary>Heavy thud when a wolf/boar dies - low square hit plus a
    /// short rising sine "confirm" tone. Cheaper and shorter than PlayDeath
    /// since it's per-kill not per-player-death.</summary>
    public void PlayBeastFell()
    {
        if (_ctx is null) return;
        try
        {
            double t = _ctx.CurrentTime;
            using var thud = _ctx.CreateOscillator();
            using var thudGain = _ctx.CreateGain();
            thud.Type = "square";
            thud.Frequency.SetValueAtTime(75f, t);
            thud.Frequency.ExponentialRampToValueAtTime(45f, t + 0.18);
            thudGain.Gain.SetValueAtTime(0, t);
            thudGain.Gain.LinearRampToValueAtTime(0.22f, t + 0.02);
            thudGain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.22);
            thud.Connect(thudGain);
            thudGain.Connect(Destination);
            thud.Start();
            thud.Stop((float)(t + 0.22));

            using var chime = _ctx.CreateOscillator();
            using var chimeGain = _ctx.CreateGain();
            chime.Type = "sine";
            chime.Frequency.SetValueAtTime(520f, t + 0.10);
            chime.Frequency.ExponentialRampToValueAtTime(740f, t + 0.25);
            chimeGain.Gain.SetValueAtTime(0, t + 0.10);
            chimeGain.Gain.LinearRampToValueAtTime(0.12f, t + 0.14);
            chimeGain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.30);
            chime.Connect(chimeGain);
            chimeGain.Connect(Destination);
            chime.Start((float)(t + 0.10));
            chime.Stop((float)(t + 0.30));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayBeastFell failed: {ex.Message}");
        }
    }

    /// <summary>Long descending death tone - dramatic fade on player death.</summary>
    public void PlayDeath()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "triangle";
            double t = _ctx.CurrentTime;
            // 180 Hz dropping to 60 Hz over 2 seconds gives the "lights out"
            // feel. Gain hits 0.25 briefly then fades on a long tail.
            osc.Frequency.SetValueAtTime(180f, t);
            osc.Frequency.ExponentialRampToValueAtTime(60f, t + 2.0);
            gain.Gain.SetValueAtTime(0f, t);
            gain.Gain.LinearRampToValueAtTime(0.25f, t + 0.05);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 2.2);
            osc.Connect(gain);
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 2.2));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayDeath failed: {ex.Message}");
        }
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
            gain.Connect(Destination);
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

    /// <summary>Bow twang - brief descending pluck before the arrow releases.</summary>
    public void PlayBowShot()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "triangle";
            double t = _ctx.CurrentTime;
            // Fast downward bend 400 -> 220 Hz over 90ms - mimics a released string.
            osc.Frequency.SetValueAtTime(400f, t);
            osc.Frequency.ExponentialRampToValueAtTime(220f, t + 0.09);
            gain.Gain.SetValueAtTime(0f, t);
            gain.Gain.LinearRampToValueAtTime(0.15f, t + 0.01);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.11);
            osc.Connect(gain);
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 0.11));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayBowShot failed: {ex.Message}");
        }
    }

    /// <summary>Water splash - descending noise-like sawtooth.</summary>
    public void PlaySplash()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "sawtooth";
            double t = _ctx.CurrentTime;
            osc.Frequency.SetValueAtTime(900f, t);
            osc.Frequency.ExponentialRampToValueAtTime(220f, t + 0.25);
            gain.Gain.SetValueAtTime(0f, t);
            gain.Gain.LinearRampToValueAtTime(0.18f, t + 0.01);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.3);
            osc.Connect(gain);
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 0.3));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlaySplash failed: {ex.Message}");
        }
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
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 0.14));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayWhoosh failed: {ex.Message}");
        }
    }

    /// <summary>Sharp rising tone for damage taken. Base variant.</summary>
    public void PlayDamage()
    {
        PlayBeep(220f, 0.18f, 0.25f, "sawtooth");
    }

    /// <summary>Pitch-scaled damage sound. Small nips pitch up, heavy hits
    /// pitch down + longer duration so the player's ear tracks magnitude.</summary>
    public void PlayDamageAmount(float amount)
    {
        // amount is HP delta in [0,1]. Light bite: 0.05. Wolf hit: 0.18. Lethal:
        // 0.3+. Pitch 320 Hz for the lightest, dropping to 140 Hz at heavy.
        float norm = Math.Clamp(amount / 0.30f, 0f, 1f);
        float freq = 320f - norm * 180f;
        float dur = 0.14f + norm * 0.18f;
        PlayBeep(freq, dur, 0.25f, "sawtooth");
    }

    /// <summary>Gentle descending pair for HP restored from food/heal.</summary>
    public void PlayHeal()
    {
        if (_ctx is null) return;
        try
        {
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "sine";
            double t = _ctx.CurrentTime;
            osc.Frequency.SetValueAtTime(660f, t);
            osc.Frequency.ExponentialRampToValueAtTime(880f, t + 0.18);
            gain.Gain.SetValueAtTime(0f, t);
            gain.Gain.LinearRampToValueAtTime(0.12f, t + 0.02);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.22);
            osc.Connect(gain);
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 0.22));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayHeal failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Quiet low-pitched thump for each footstep. Alternating high/low
    /// phase keeps two consecutive steps from sounding identical.
    /// </summary>
    public void PlayStep(bool altPhase)
    {
        PlayBeep(altPhase ? 130f : 110f, 0.04f, 0.06f, "sine");
    }

    /// <summary>Wet wading-step footfall - higher pitch + airier than dry land.</summary>
    public void PlayWaterStep(bool altPhase)
    {
        PlayBeep(altPhase ? 380f : 340f, 0.05f, 0.045f, "sine");
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
            gain.Connect(Destination);
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
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 0.22));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayConsume failed: {ex.Message}");
        }
    }

    /// <summary>Short high-pitched chirp - used for night-ambient crickets.</summary>
    public void PlayCricket()
    {
        var rng = new Random();
        float f = 4000f + (float)rng.NextDouble() * 1200f;
        PlayBeep(f, 0.04f, 0.025f, "square");
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
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 1.2));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayWolfHowl failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Short sharp growl used when a wolf flips to Charge mode. Two stacked
    /// oscillators (low sawtooth + mid-freq square) give it a snarly rasp.
    /// </summary>
    public void PlayWolfSnarl()
    {
        if (_ctx is null) return;
        try
        {
            double t = _ctx.CurrentTime;
            using var osc1 = _ctx.CreateOscillator();
            using var gain1 = _ctx.CreateGain();
            osc1.Type = "sawtooth";
            osc1.Frequency.SetValueAtTime(95f, t);
            osc1.Frequency.ExponentialRampToValueAtTime(60f, t + 0.25);
            gain1.Gain.SetValueAtTime(0, t);
            gain1.Gain.LinearRampToValueAtTime(0.18f, t + 0.04);
            gain1.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.30);
            osc1.Connect(gain1);
            gain1.Connect(Destination);
            osc1.Start();
            osc1.Stop((float)(t + 0.30));

            using var osc2 = _ctx.CreateOscillator();
            using var gain2 = _ctx.CreateGain();
            osc2.Type = "square";
            osc2.Frequency.SetValueAtTime(220f, t);
            osc2.Frequency.ExponentialRampToValueAtTime(140f, t + 0.22);
            gain2.Gain.SetValueAtTime(0, t);
            gain2.Gain.LinearRampToValueAtTime(0.08f, t + 0.03);
            gain2.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.25);
            osc2.Connect(gain2);
            gain2.Connect(Destination);
            osc2.Start();
            osc2.Stop((float)(t + 0.25));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayWolfSnarl failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Short breathy gasp - a quick low triangle swell that reads as "out
    /// of breath". Plays when the player's stamina crosses below the low
    /// threshold and periodically while sprinting exhausted.
    /// </summary>
    public void PlayGasp()
    {
        if (_ctx is null) return;
        try
        {
            double t = _ctx.CurrentTime;
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "triangle";
            osc.Frequency.SetValueAtTime(140f, t);
            osc.Frequency.ExponentialRampToValueAtTime(95f, t + 0.25);
            gain.Gain.SetValueAtTime(0, t);
            gain.Gain.LinearRampToValueAtTime(0.10f, t + 0.05);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.30);
            osc.Connect(gain);
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 0.30));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayGasp failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Deep low growl-roar for bears - much heavier than a wolf snarl.
    /// Two stacked low oscillators (sawtooth bass + square mid) over 0.5s.
    /// </summary>
    public void PlayBearRoar()
    {
        if (_ctx is null) return;
        try
        {
            double t = _ctx.CurrentTime;
            using var bass = _ctx.CreateOscillator();
            using var bassGain = _ctx.CreateGain();
            bass.Type = "sawtooth";
            bass.Frequency.SetValueAtTime(60f, t);
            bass.Frequency.ExponentialRampToValueAtTime(40f, t + 0.45);
            bassGain.Gain.SetValueAtTime(0, t);
            bassGain.Gain.LinearRampToValueAtTime(0.22f, t + 0.06);
            bassGain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.55);
            bass.Connect(bassGain);
            bassGain.Connect(Destination);
            bass.Start();
            bass.Stop((float)(t + 0.55));

            using var mid = _ctx.CreateOscillator();
            using var midGain = _ctx.CreateGain();
            mid.Type = "square";
            mid.Frequency.SetValueAtTime(140f, t);
            mid.Frequency.ExponentialRampToValueAtTime(95f, t + 0.42);
            midGain.Gain.SetValueAtTime(0, t);
            midGain.Gain.LinearRampToValueAtTime(0.10f, t + 0.05);
            midGain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.50);
            mid.Connect(midGain);
            midGain.Connect(Destination);
            mid.Start();
            mid.Stop((float)(t + 0.50));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayBearRoar failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Quick low-pitch thump for a small mammal hitting the ground. Plays
    /// when a rabbit / deer enters flee state (panic stomp).
    /// </summary>
    public void PlayPreyThump()
    {
        if (_ctx is null) return;
        try
        {
            double t = _ctx.CurrentTime;
            using var osc = _ctx.CreateOscillator();
            using var gain = _ctx.CreateGain();
            osc.Type = "sine";
            osc.Frequency.SetValueAtTime(120f, t);
            osc.Frequency.ExponentialRampToValueAtTime(70f, t + 0.10);
            gain.Gain.SetValueAtTime(0, t);
            gain.Gain.LinearRampToValueAtTime(0.08f, t + 0.02);
            gain.Gain.ExponentialRampToValueAtTime(0.0001f, t + 0.14);
            osc.Connect(gain);
            gain.Connect(Destination);
            osc.Start();
            osc.Stop((float)(t + 0.14));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayPreyThump failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Cheerful three-blip bird chirp. High sine tones that trill up and
    /// down. Plays during dawn to celebrate the day turning over.
    /// </summary>
    public void PlayBirdChirp()
    {
        if (_ctx is null) return;
        try
        {
            double t = _ctx.CurrentTime;
            float[] freqs = { 1800f, 2100f, 1600f };
            for (int i = 0; i < freqs.Length; i++)
            {
                double start = t + i * 0.08;
                using var osc = _ctx.CreateOscillator();
                using var gain = _ctx.CreateGain();
                osc.Type = "sine";
                osc.Frequency.SetValueAtTime(freqs[i], start);
                gain.Gain.SetValueAtTime(0, start);
                gain.Gain.LinearRampToValueAtTime(0.06f, start + 0.015);
                gain.Gain.ExponentialRampToValueAtTime(0.0001f, start + 0.08);
                osc.Connect(gain);
                gain.Connect(Destination);
                osc.Start((float)start);
                osc.Stop((float)(start + 0.08));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayBirdChirp failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Two-note owl hoot - low sine pair with the second a perfect fifth
    /// above the first. Plays near midnight as atmosphere.
    /// </summary>
    public void PlayOwlHoot()
    {
        if (_ctx is null) return;
        try
        {
            double t = _ctx.CurrentTime;
            float[] freqs = { 220f, 330f };
            for (int i = 0; i < 2; i++)
            {
                double start = t + i * 0.45;
                using var osc = _ctx.CreateOscillator();
                using var gain = _ctx.CreateGain();
                osc.Type = "sine";
                osc.Frequency.SetValueAtTime(freqs[i], start);
                gain.Gain.SetValueAtTime(0, start);
                gain.Gain.LinearRampToValueAtTime(0.12f, start + 0.06);
                gain.Gain.ExponentialRampToValueAtTime(0.0001f, start + 0.35);
                osc.Connect(gain);
                gain.Connect(Destination);
                osc.Start((float)start);
                osc.Stop((float)(start + 0.35));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayOwlHoot failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Crow caw - two quick rising square blips, classic cartoon crow cry.
    /// Plays when a crow takes damage and flees.
    /// </summary>
    public void PlayCrowCaw()
    {
        if (_ctx is null) return;
        try
        {
            double t = _ctx.CurrentTime;
            for (int i = 0; i < 2; i++)
            {
                double start = t + i * 0.18;
                using var osc = _ctx.CreateOscillator();
                using var gain = _ctx.CreateGain();
                osc.Type = "square";
                osc.Frequency.SetValueAtTime(520f, start);
                osc.Frequency.ExponentialRampToValueAtTime(360f, start + 0.12);
                gain.Gain.SetValueAtTime(0, start);
                gain.Gain.LinearRampToValueAtTime(0.08f, start + 0.02);
                gain.Gain.ExponentialRampToValueAtTime(0.0001f, start + 0.15);
                osc.Connect(gain);
                gain.Connect(Destination);
                osc.Start((float)start);
                osc.Stop((float)(start + 0.15));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] PlayCrowCaw failed: {ex.Message}");
        }
    }

    // Persistent wind ambient - low sawtooth with slowly modulated gain so
    // the volume breathes up and down like real wind gusts. Started on the
    // first UpdateWindAmbient call and left running.
    private OscillatorNode? _windOsc;
    private GainNode? _windGain;
    private float _windPhase;

    /// <summary>
    /// Update the wind loop. Intensity [0,1] targets gain; phase drives a
    /// slow 0.15 Hz sine modulation so the wind gusts. Call every frame
    /// with a value derived from weather / elevation / time-of-day.
    /// </summary>
    public void UpdateWindAmbient(float intensity, float dt)
    {
        if (_ctx is null) return;
        try
        {
            if (intensity > 0.02f && _windOsc is null)
            {
                _windOsc = _ctx.CreateOscillator();
                _windGain = _ctx.CreateGain();
                _windOsc.Type = "sawtooth";
                _windOsc.Frequency.SetValueAtTime(90f, _ctx.CurrentTime);
                _windGain.Gain.SetValueAtTime(0, _ctx.CurrentTime);
                _windOsc.Connect(_windGain);
                _windGain.Connect(_ctx.Destination);
                _windOsc.Start();
            }
            _windPhase += dt * 0.15f * MathF.PI * 2f;
            if (_windGain is not null)
            {
                float gust = 0.5f + 0.5f * MathF.Sin(_windPhase);
                float target = Math.Clamp(intensity * 0.025f * gust, 0, 0.05f);
                _windGain.Gain.LinearRampToValueAtTime(target, _ctx.CurrentTime + 0.2);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] UpdateWindAmbient failed: {ex.Message}");
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
        _master?.Dispose();
        try { _ctx?.Close(); } catch { }
        _ctx?.Dispose();
        _ctx = null;
        _master = null;
    }
}
