using System.Linq;
using System.Reflection;
using GlobalEnums;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// A quiet Geiger counter, clicking while Unsteady Body (Unsteady side up) and
    /// Heavy Blow are both equipped: a hint that something volatile is going on.
    ///
    /// The clicks are made here rather than loaded from a file: each is a few
    /// milliseconds of static that dies away fast. What makes it sound like a
    /// Geiger counter is the timing: clicks arrive at random, uneven intervals,
    /// the way radiation is detected, not as a steady tick.
    ///
    /// Played through a "dry" channel (the menus' own), not the Knight's: that one
    /// adds the room's echo, which turned each click into an insect-like chatter.
    /// If no dry channel is found, it plays unmixed and follows the game's master
    /// and sound-effects volume settings itself. Kept low so it sits under the
    /// game's own audio. The mod's Sounds option turns the clicks off.
    ///
    /// Because it's on its own channel, the game's damage muffle doesn't reach it,
    /// so it's muffled here instead, with the same timing, whenever the Knight is hurt.
    ///
    /// Each click also flickers a green glow around the Knight (RadiationGlow), which
    /// stays on even with Sounds off, for anyone who mutes the clicks.
    /// </summary>
    internal class GeigerCounter : MonoBehaviour
    {
        private const int HeavyBlowCharm = 15;

        private const float ClicksPerSecond = 5f;
        private const float Volume = 0.15f;
        private const int SampleRate = 44100;
        private const int Variants = 4;

        internal static GeigerCounter Instance;

        private AudioSource _source;
        private AudioLowPassFilter _muffle;
        private AudioClip[] _clicks;
        private float _untilNext;
        private bool _routed;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // not tied to a place in the world
            _source.volume = 1f;

            _muffle = gameObject.AddComponent<AudioLowPassFilter>();
            _muffle.cutoffFrequency = OpenCutoff;
            _muffle.enabled = false;
            Instance = this;

            _clicks = new AudioClip[Variants];
            for (int i = 0; i < Variants; i++) _clicks[i] = MakeClick(i);
            _untilNext = NextInterval();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RadiationGlow.Remove();
            if (_clicks == null) return;
            foreach (AudioClip c in _clicks) if (c != null) Destroy(c);
        }

        private string _lastState = "";

        private void Update()
        {
            UpdateMuffle();

            bool ticking = ShouldTick(out string why);
            bool sound = ticking && UnsteadyBodyMod.Settings.Sounds;
            string state = !ticking ? $"off ({why})" : sound ? "on" : "on (glow only: Sounds option is off)";
            if (state != _lastState)
            {
                _lastState = state;
                UnsteadyBodyMod.Verbose("Geiger counter: " + state);
            }

            RadiationGlow.Show(ticking);
            if (!ticking) return;

            if (sound) RouteThroughDryChannel();

            _untilNext -= Time.unscaledDeltaTime;
            while (_untilNext <= 0f)
            {
                if (sound)
                {
                    AudioClip clip = _clicks[Random.Range(0, _clicks.Length)];
                    _source.pitch = Random.Range(0.97f, 1.04f);
                    _source.PlayOneShot(clip, Volume * _settingsVolume * Random.Range(0.75f, 1f));
                }
                RadiationGlow.Flicker();
                _untilNext += NextInterval();
            }
        }

        // ---------- Muffle ----------
        // The same shape as the game's damage muffle: in over 0.1 s, held 0.75 s,
        // then back over 2 s.
        private const float OpenCutoff = 22000f;
        private const float MuffledCutoff = 900f;
        private const float MuffleIn = 0.1f, MuffleHold = 0.75f, MuffleOut = 2f;
        private float _muffledAt = -100f;

        /// <summary>Muffle the clicks the way the game muffles everything when the Knight is hurt.</summary>
        internal static void Muffle()
        {
            if (Instance != null) Instance._muffledAt = Time.unscaledTime;
        }

        private void UpdateMuffle()
        {
            float t = Time.unscaledTime - _muffledAt;
            float amount; // 0 open, 1 fully muffled
            if (t < 0f || t > MuffleIn + MuffleHold + MuffleOut) amount = 0f;
            else if (t < MuffleIn) amount = t / MuffleIn;
            else if (t < MuffleIn + MuffleHold) amount = 1f;
            else amount = 1f - (t - MuffleIn - MuffleHold) / MuffleOut;

            _muffle.enabled = amount > 0f;
            // Ease the cutoff on a log scale so the fade sounds even.
            _muffle.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(OpenCutoff), Mathf.Log(MuffledCutoff), amount));
        }

        private static bool ShouldTick(out string why)
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) { why = "no save loaded"; return false; }
            if (!pd.equippedCharm_14) { why = "charm 14 not equipped"; return false; }
            if (!UnsteadyBodyMod.Local.Unsteady) { why = "charm is Steady side up"; return false; }
            if (!pd.equippedCharm_15) { why = "Heavy Blow not equipped"; return false; }
            GameManager gm = GameManager.instance;
            if (gm == null || gm.gameState != GameState.PLAYING)
            {
                why = "game state " + (gm != null ? gm.gameState.ToString() : "none");
                return false;
            }
            why = null;
            return true;
        }

        // Random, uneven gaps between clicks (the way a real counter sounds).
        private static float NextInterval() => -Mathf.Log(1f - Random.value) / ClicksPerSecond;

        // 1 when going through one of the game's channels (it applies the volume
        // settings itself); otherwise the settings, applied here.
        private float _settingsVolume = 1f;

        // Pick a channel without room echo: the menus' channel, found on the HUD's
        // sound sources. Failing that, play unmixed and apply the volume settings here.
        private void RouteThroughDryChannel()
        {
            if (_routed)
            {
                if (_source.outputAudioMixerGroup == null) _settingsVolume = SettingsVolume();
                return;
            }
            _routed = true;

            var hud = GameObject.Find("_GameCameras/HudCamera");
            var groups = hud != null
                ? hud.GetComponentsInChildren<AudioSource>(true).Select(a => a.outputAudioMixerGroup).Where(g => g != null).Distinct().ToList()
                : new System.Collections.Generic.List<UnityEngine.Audio.AudioMixerGroup>();

            var dry = groups.FirstOrDefault(g => g.name.ToUpperInvariant().Contains("UI"))
                   ?? groups.FirstOrDefault(g => g.name.ToUpperInvariant().Contains("MENU"));

            HeroController hero = HeroController.instance;
            HeroAudioController heroAudio = hero != null ? hero.GetComponent<HeroAudioController>() : null;
            string heroGroup = heroAudio?.softLanding?.outputAudioMixerGroup?.name ?? "none";

            _source.outputAudioMixerGroup = dry;
            _settingsVolume = dry != null ? 1f : SettingsVolume();
            UnsteadyBodyMod.Verbose(
                $"Geiger counter channel: {(dry != null ? dry.name : "unmixed")} " +
                $"(Knight's channel is '{heroGroup}'; HUD channels: {string.Join(", ", groups.Select(g => g.name))})");
        }

        // The game's master and sound-effects volume settings (0 to 10 each), as 0 to 1.
        private static float SettingsVolume()
        {
            object settings = GameManager.instance != null ? GameManager.instance.gameSettings : null;
            if (settings == null) return 1f;
            float master = ReadVolume(settings, "masterVolume");
            float sound = ReadVolume(settings, "soundVolume");
            return Mathf.Clamp01(master / 10f) * Mathf.Clamp01(sound / 10f);
        }

        private static float ReadVolume(object settings, string name)
        {
            FieldInfo f = settings.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            object v = f?.GetValue(settings);
            return v is float x ? x : v is int i ? i : 10f;
        }

        // How hard each click is overdriven: higher is harsher. 2.5 is "harsher",
        // 6 is "harshest" (see the preview recordings).
        private const float Drive = 2.5f;

        // One click: a crack, then a burst of static ringing at a gritty middle
        // frequency, overdriven for a harsh edge. Scaled so its loudest point is
        // full volume.
        private static AudioClip MakeClick(int variant)
        {
            var rng = new System.Random(1234 + variant * 97);
            int samples = Mathf.CeilToInt(0.014f * SampleRate);
            // Close together in tone, but not identical, so each click is still its own.
            float noiseDecay = 0.0016f + variant * 0.0001f;
            float middle = 1650f + variant * 50f;

            // A simple resonance, so the static rings at the middle frequency.
            const float r = 0.97f;
            float w = 2f * Mathf.PI * middle / SampleRate;
            float a1 = 2f * r * Mathf.Cos(w), a2 = -r * r;
            float y1 = 0f, y2 = 0f;

            var data = new float[samples];
            float peak = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t / noiseDecay);
                float ring = noise + a1 * y1 + a2 * y2;
                y2 = y1;
                y1 = ring;
                float crack = i == 0 ? 1f : i == 1 ? -0.8f : 0f;
                float mix = 0.6f * noise + 0.12f * ring + crack;
                data[i] = (float)System.Math.Tanh(Drive * mix);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            if (peak > 0f) for (int i = 0; i < samples; i++) data[i] /= peak;
            AudioClip clip = AudioClip.Create("Unsteady Body Geiger Click " + variant, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
