using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Plays the game's own sounds for launches and impacts: the dash whoosh when
    /// a launch starts, and the hard-landing thud when a launch hits something solid.
    /// Clips come from the Knight's own audio sources, so they follow the game's
    /// sound-effects volume.
    /// </summary>
    internal static class LaunchSounds
    {
        private const float ImpactCooldown = 0.2f;
        private static float _lastImpact = -10f;

        internal static void Launch(HeroController hero) => Play(hero, false);

        /// <summary>The charm being turned over: a soft landing, like setting it down.</summary>
        internal static void Flip(HeroController hero)
        {
            if (hero == null) return;
            HeroAudioController audio = hero.GetComponent<HeroAudioController>();
            if (audio == null) audio = hero.GetComponentInChildren<HeroAudioController>();
            AudioSource source = audio != null ? audio.softLanding : null;
            if (source != null && source.clip != null) source.PlayOneShot(source.clip);
        }

        internal static void Impact(HeroController hero)
        {
            if (Time.time - _lastImpact < ImpactCooldown) return;
            _lastImpact = Time.time;
            Play(hero, true);
        }

        private static void Play(HeroController hero, bool impact)
        {
            if (!UnsteadyBodyMod.Settings.Sounds || hero == null) return;

            HeroAudioController audio = hero.GetComponent<HeroAudioController>();
            if (audio == null) audio = hero.GetComponentInChildren<HeroAudioController>();
            if (audio == null) return;

            AudioSource source = impact ? audio.hardLanding : audio.dash;
            if (source != null && source.clip != null) source.PlayOneShot(source.clip);
        }
    }

    /// <summary>
    /// Sits on the Knight and listens for collisions. An impact counts when they hit
    /// terrain mid-launch (sideways recoil or pogo) hard enough, measured head-on
    /// into the surface, so skimming along a floor doesn't thud.
    /// </summary>
    internal class ImpactListener : MonoBehaviour
    {
        // Head-on speed needed to count as an impact. Running is about 8, a normal
        // fall tops out around 20, and launches are 18 to 150.
        private const float MinImpactSpeed = 15f;

        private const int TerrainLayer = 8;

        private bool _wasLaunching;

        // Recorded before each physics step, because the game cancels a pogo the
        // moment the Knight bumps their head, possibly before we hear about the collision.
        private void FixedUpdate()
        {
            HeroController hero = HeroController.instance;
            _wasLaunching = hero != null && hero.gameObject == gameObject && Launch.Active
                && (hero.cState.recoilingLeft || hero.cState.recoilingRight || Glide.Active || AngledLaunch.Active
                    || (hero.cState.bouncing && UnsteadyBodyMod.Settings.PogoLaunch));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_wasLaunching || collision.contactCount == 0) return;

            GameObject other = collision.gameObject;
            if (other.layer != TerrainLayer && !other.CompareTag("HeroWalkable")) return;

            Vector2 normal = collision.GetContact(0).normal;
            float headOn = Mathf.Abs(Vector2.Dot(collision.relativeVelocity, normal));
            if (headOn < MinImpactSpeed) return;

            LaunchSounds.Impact(HeroController.instance);
        }
    }
}
