using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Unsteady Body + Heavy Blow: an exploding strike on an enemy throws the Knight
    /// straight away from that enemy, along the real line between their centres,
    /// instead of along one of the four directions the game uses.
    ///
    /// The game's own push for that strike only knows four directions, so it's
    /// cleared, and this drives the Knight along the true angle itself, at the same
    /// speed and for the same time as a sideways launch at the current settings.
    /// When it runs out, the sideways part fades away through the coast (Glide) and
    /// the upward part is capped at about a jump's speed, so they don't keep
    /// rocketing up.
    ///
    /// Strikes on walls, spikes and other terrain stay four-directional.
    /// </summary>
    internal static class AngledLaunch
    {
        // Upward speed they keep when the push ends (about a jump's).
        private const float MaxUpwardCarry = 15f;

        // The game's own push for the same strike can arrive just after this one
        // starts; ignore it for this long.
        private const float IgnoreOwnPushSeconds = 0.1f;

        private static bool _active;
        private static Vector2 _direction;
        private static float _speed;
        private static float _stepsLeft;
        private static float _startTime = -100f;

        internal static bool Active => _active;

        /// <summary>True just after a launch starts, while the game's own push for the same strike might still arrive.</summary>
        internal static bool IgnoringOwnPush => _active && Time.time - _startTime < IgnoreOwnPushSeconds;

        internal static void Start(HeroController hero, HealthManager enemy)
        {
            if (hero == null || enemy == null) return;

            Vector2 from = CentreOf(enemy.gameObject);
            Vector2 to = CentreOf(hero.gameObject);
            Vector2 away = to - from;
            if (away.sqrMagnitude < 0.0001f) return; // right on top of each other: leave the game's push alone
            _direction = away.normalized;

            Launch.Apply(hero);
            _speed = Mathf.Abs(hero.RECOIL_HOR_VELOCITY);
            _stepsLeft = Mathf.Max(1f, hero.RECOIL_HOR_STEPS);

            // Clear the game's four-direction push for this strike.
            HeroControllerStates s = hero.cState;
            s.recoilingLeft = false;
            s.recoilingRight = false;
            s.bouncing = false;
            Glide.Stop();

            _active = true;
            _startTime = Time.time;
            LaunchSounds.Launch(hero);
        }

        internal static void Stop() => _active = false;

        /// <summary>Called after the game's own physics step for the Knight.</summary>
        internal static void AfterStep(HeroController hero)
        {
            if (!_active) return;

            HeroControllerStates s = hero.cState;
            if (!Launch.Active || s.dashing || s.backDashing || s.superDashing || s.recoiling
                || s.transitioning || s.hazardRespawning || hero.controlReqlinquished)
            {
                _active = false;
                return;
            }

            Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
            if (body == null) { _active = false; return; }

            // Keep the game's own push for this strike from coming back.
            s.recoilingLeft = false;
            s.recoilingRight = false;
            s.bouncing = false;

            body.velocity = _direction * _speed;

            _stepsLeft -= 1f;
            if (_stepsLeft > 0f) return;

            // Run out: coast sideways, keep a capped amount of upward speed.
            _active = false;
            Vector2 v = _direction * _speed;
            body.velocity = new Vector2(v.x, Mathf.Min(v.y, MaxUpwardCarry));
            if (!s.onGround) Glide.Coast(Mathf.Abs(v.x), v.x >= 0f ? 1 : -1);
        }

        private static Vector2 CentreOf(GameObject go)
        {
            Collider2D c = go.GetComponent<Collider2D>();
            return c != null ? (Vector2)c.bounds.center : (Vector2)go.transform.position;
        }
    }
}
