using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// A soft, sickly green glow around the Knight while Unsteady Body (Unsteady side
    /// up) and Heavy Blow are both equipped. It flickers brighter on each Geiger
    /// click and dies back down: the click rhythm, made visible. Drawn just behind
    /// the Knight so it doesn't cover them.
    /// </summary>
    internal static class RadiationGlow
    {
        private static readonly Color Green = new Color(0.55f, 1f, 0.3f);
        private const float RestingAlpha = 0.28f;
        private const float FlickerAlpha = 0.65f;
        private const float FlickerFade = 0.12f; // seconds for a flicker to die away
        private const float Diameter = 3.6f;     // world units
        // Where the nail is, relative to the Knight's center (which sits up at their
        // head), in world units. "Toward hand" is positive; negative is the other side.
        private const float NailTowardHand = 0.25f;
        private const float NailDown = 0.75f;
        // Lying on the ground: the same, a little lower.
        private const float LyingExtraDown = 0.3f;
        // The "Challenge" pose: nail drawn and held out low on the other side.
        private const float ChallengeTowardHand = -1.0f;
        private const float ChallengeDown = 1.1f;
        // The nail isn't out for the first frames of 'Challenge Start' (they're still
        // turning), and goes away partway through 'Challenge End'. Animation frame
        // numbers, counted from 0.
        private const int ChallengeDrawnFromFrame = 2;
        private const int ChallengeSheathedFromFrame = 2;
        // How quickly the glow slides to a new spot when their pose changes (seconds).
        private const float Settle = 0.05f;

        private static GameObject _glow;
        private static SpriteRenderer _renderer;
        private static HeroController _hero;
        private static Texture2D _texture;
        private static Sprite _sprite;
        private static float _flicker;

        internal static void Show(bool on)
        {
            HeroController hero = HeroController.instance;
            if (!on || hero == null)
            {
                if (_glow != null) _glow.SetActive(false);
                return;
            }

            if (_glow == null || _hero != hero) Build(hero);
            if (_glow == null) return;

            // Cutscenes (like the end of the Radiance fight) hide or fade the Knight's
            // own sprite and act things out with a stand-in. The glow follows their
            // sprite's visibility so it never hangs in the air where they used to be.
            float knightAlpha = KnightVisibility(hero);
            _glow.SetActive(knightAlpha > 0.01f);
            if (knightAlpha <= 0.01f) return;
            KeepBehindKnight(hero);
            CenterOnNail(hero);

            _flicker *= Mathf.Exp(-Time.unscaledDeltaTime / FlickerFade);
            float alpha = Mathf.Lerp(RestingAlpha, FlickerAlpha, _flicker) * knightAlpha;
            _renderer.color = new Color(Green.r, Green.g, Green.b, alpha);
        }

        private static tk2dSprite _knightSprite;
        private static float KnightVisibility(HeroController hero)
        {
            Renderer r = hero.GetComponent<Renderer>();
            if (r == null || !r.enabled || !hero.gameObject.activeInHierarchy) return 0f;
            if (_knightSprite == null || _knightSprite.gameObject != hero.gameObject)
                _knightSprite = hero.GetComponent<tk2dSprite>();
            return _knightSprite != null ? Mathf.Clamp01(_knightSprite.color.a) : 1f;
        }

        internal static void Flicker() => _flicker = Mathf.Max(_flicker, Random.Range(0.6f, 1f));

        internal static void Remove()
        {
            if (_glow != null) Object.Destroy(_glow);
            _glow = null;
            _renderer = null;
            _hero = null;
        }

        private static void Build(HeroController hero)
        {
            Remove();
            _hero = hero;

            if (_sprite == null)
            {
                const int size = 128;
                _texture = new Texture2D(size, size, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
                Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - r);
                    _texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
                _texture.Apply();
                _sprite = Sprite.Create(_texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / Diameter);
            }

            _glow = new GameObject("Unsteady Body Radiation Glow");
            _glow.layer = hero.gameObject.layer;
            _glow.transform.SetParent(hero.transform, false);
            _glow.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            _renderer = _glow.AddComponent<SpriteRenderer>();
            _renderer.sprite = _sprite;
            _renderer.color = new Color(Green.r, Green.g, Green.b, RestingAlpha);
        }

        // Placed in world space each frame from the way they're facing and the animation
        // they're playing. Testing showed their hand is on the side opposite what
        // facingRight suggested, hence the flip.
        private static tk2dSpriteAnimator _animator;
        private static readonly System.Collections.Generic.HashSet<string> _seenClips =
            new System.Collections.Generic.HashSet<string>();

        private static void CenterOnNail(HeroController hero)
        {
            string clip = CurrentClip(hero);
            float towardHand = NailTowardHand, down = NailDown;
            string pose = "normal";
            if (IsChallenge(clip) && NailIsOut(clip)) { towardHand = ChallengeTowardHand; down = ChallengeDown; pose = "challenge"; }
            else if (IsLying(clip)) { down += LyingExtraDown; pose = "lying"; }

            if (clip != null && _seenClips.Add(clip))
                UnsteadyBodyMod.Verbose($"Radiation glow: Knight animation '{clip}' -> {pose} position");

            Vector3 p = hero.transform.position;
            float side = hero.cState.facingRight ? -towardHand : towardHand;
            Vector3 target = new Vector3(p.x + side, p.y - down, p.z + 0.01f);

            Vector3 now = _glow.transform.position;
            if ((now - target).sqrMagnitude > 4f) _glow.transform.position = target; // jumped (respawn, new room)
            else _glow.transform.position = Vector3.Lerp(now, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / Settle));
        }

        private static string CurrentClip(HeroController hero)
        {
            if (_animator == null || _animator.gameObject != hero.gameObject)
                _animator = hero.GetComponent<tk2dSpriteAnimator>();
            return _animator != null && _animator.CurrentClip != null ? _animator.CurrentClip.name : null;
        }

        // During the Challenge animations, whether the nail is drawn on this frame.
        // Each frame is logged once so the cut-off frames can be tuned from a video.
        private static readonly System.Collections.Generic.HashSet<string> _seenFrames =
            new System.Collections.Generic.HashSet<string>();

        private static bool NailIsOut(string clip)
        {
            int frame = _animator != null ? _animator.CurrentFrame : 0;
            bool ending = clip.IndexOf("End", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool drawn = ending ? frame < ChallengeSheathedFromFrame : frame >= ChallengeDrawnFromFrame;
            if (_seenFrames.Add(clip + "#" + frame))
            {
                int total = _animator.CurrentClip != null && _animator.CurrentClip.frames != null ? _animator.CurrentClip.frames.Length : -1;
                UnsteadyBodyMod.Verbose($"Radiation glow: '{clip}' frame {frame} of {total} -> nail {(drawn ? "drawn" : "away")}");
            }
            return drawn;
        }

        private static bool IsChallenge(string clip) =>
            clip != null && clip.IndexOf("Challenge", System.StringComparison.OrdinalIgnoreCase) >= 0;

        // Face-down on the ground: 'Prostrate' and 'Prostrate Rise' (seen in testing),
        // plus a few likely names for other lying poses. Bench animations
        // ('Sit Fall Asleep', 'Wake To Sit') are sitting, not lying.
        private static bool IsLying(string clip)
        {
            if (clip == null) return false;
            if (clip.IndexOf("Sit", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            foreach (string word in new[] { "Prostrate", "Lie", "Lying", "Collapse", "Death", "Dead", "Wake Up Ground" })
                if (clip.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // Same drawing layer and order as the Knight. Within one layer and order, Unity
        // draws farther things first, so sitting a hair behind them (higher z) puts the
        // glow after the scenery but before the Knight. (An order of one below them put
        // it before ALL the scenery at their order, which then painted over it.)
        private static bool _logged;
        private static void KeepBehindKnight(HeroController hero)
        {
            Renderer knight = hero.GetComponent<Renderer>();
            if (knight == null) return;
            _renderer.sortingLayerID = knight.sortingLayerID;
            _renderer.sortingOrder = knight.sortingOrder;
            if (!_logged)
            {
                _logged = true;
                UnsteadyBodyMod.Verbose($"Radiation glow: layer '{knight.sortingLayerName}', order {knight.sortingOrder}, " +
                    $"Knight z {hero.transform.position.z:0.###}, glow z {_glow.transform.position.z:0.###}, Knight scale {hero.transform.lossyScale}");
            }
        }
    }
}
