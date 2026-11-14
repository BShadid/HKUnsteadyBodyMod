using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// When a sideways launch runs out in mid-air, the Knight coasts on instead of
    /// stopping dead.
    ///
    /// The game's recoil pushes them for a set number of physics steps, then hands
    /// sideways movement straight back to the controls. At normal recoil speeds
    /// that cut is invisible; at launch speeds they'd go from full flight to a
    /// standstill in one step, as if they'd hit an invisible wall. Instead, their
    /// speed carries on and fades away quickly, like air resistance, until it's
    /// back to running speed.
    ///
    /// The coast ends when they land, touch a wall, dash, take a hit, are
    /// launched again, or the game takes control (room changes and the like).
    /// The controls still work throughout: if the controls would move the Knight faster in
    /// the same direction, the controls win.
    /// </summary>
    internal static class Glide
    {
        // How quickly the coast fades: speed halves every this many seconds.
        private const float HalfLife = 0.1f;

        private static float _speed;
        private static int _direction;

        internal static bool Active => _speed > 0f;

        /// <summary>Start coasting sideways at this speed (used when an angled launch runs out).</summary>
        internal static void Coast(float speed, int direction)
        {
            _speed = speed;
            _direction = direction;
        }

        internal static void Stop() => _speed = 0f;

        internal static void Hook() => On.HeroController.FixedUpdate += OnFixedUpdate;

        internal static void Unhook()
        {
            On.HeroController.FixedUpdate -= OnFixedUpdate;
            _speed = 0f;
        }

        private static void OnFixedUpdate(On.HeroController.orig_FixedUpdate orig, HeroController self)
        {
            HeroControllerStates s = self.cState;
            bool wasRecoiling = s.recoilingLeft || s.recoilingRight;
            int wasDirection = s.recoilingRight ? 1 : -1;

            orig(self);

            if (!Launch.Active)
            {
                _speed = 0f;
                AngledLaunch.Stop();
                return;
            }

            // An angled launch (Unsteady Body + Heavy Blow) drives them itself while it lasts.
            AngledLaunch.AfterStep(self);
            if (AngledLaunch.Active) return;

            // A sideways launch just ran out: start coasting if they're in the air.
            if (wasRecoiling && !s.recoilingLeft && !s.recoilingRight && !s.onGround && !s.touchingWall)
            {
                _speed = Mathf.Abs(self.RECOIL_HOR_VELOCITY);
                _direction = wasDirection;
            }

            if (_speed <= 0f) return;

            if (s.onGround || s.touchingWall || s.wallSliding || s.dashing || s.backDashing || s.superDashing
                || s.recoilingLeft || s.recoilingRight || s.recoiling || s.transitioning || s.hazardRespawning
                || self.controlReqlinquished)
            {
                _speed = 0f;
                return;
            }

            _speed *= Mathf.Pow(0.5f, Time.fixedDeltaTime / HalfLife);
            if (_speed < self.RUN_SPEED)
            {
                _speed = 0f;
                return;
            }

            Rigidbody2D body = self.GetComponent<Rigidbody2D>();
            if (body == null) return;

            // The controls set their speed during the game's own step; keep whichever
            // is faster in the coasting direction.
            float steered = body.velocity.x;
            if (steered * _direction < _speed)
            {
                body.velocity = new Vector2(_direction * _speed, body.velocity.y);
            }
        }
    }
}
