using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Scales the Knight's own recoil and pogo values while Unsteady Body is worn,
    /// and puts them back when it isn't.
    ///
    /// How the base game uses these values:
    /// - Sideways recoil pushes them at RECOIL_HOR_VELOCITY for RECOIL_HOR_STEPS
    ///   physics steps (vanilla: 8 steps, about 0.16 s).
    /// - A pogo holds them at BOUNCE_VELOCITY going up for BOUNCE_TIME seconds
    ///   (vanilla: 0.25 s), or until they bump their head on a ceiling.
    ///
    /// Distance travelled is speed x time, so each preset raises both. Speed is
    /// kept to roughly crystal-dash territory so they don't pass through thin
    /// walls; most of the extra distance comes from making the push last longer.
    /// </summary>
    internal static class Launch
    {
        internal static readonly string[] StrengthNames = { "Wobbly", "Comical", "Absurd", "Ludicrous" };

        //                                   Wobbly Comical Absurd Ludicrous
        private static readonly float[] SideSpeed = { 5f, 8f, 10f, 10f };
        private static readonly float[] SideTime  = { 2f, 4f, 7f, 12f };
        private static readonly float[] PogoSpeed = { 2f, 2.5f, 3f, 3f };
        private static readonly float[] PogoTime  = { 2f, 4f, 6f, 10f };

        // "Speed limit" menu option. Faster settings multiply the speed and divide
        // the duration by the same amount, so they travel the same distance in less
        // time. Above 1x they can pass through thin walls and platforms.
        internal static readonly string[] SpeedLimitNames = { "On", "Off: 2x faster", "Off: 4x faster" };
        private static readonly float[] SpeedBoost = { 1f, 2f, 4f };

        // Downward speed for the upward-strike slam. The game caps falling speed
        // on the next physics step, so this effectively means "fall at full speed now".
        private const float SlamSpeed = 40f;

        // Vanilla values, read from the Knight the first time we see them.
        private static HeroController _hero;
        private static float _recoilVelocity;
        private static float _recoilSteps;
        private static float _bounceVelocity;
        private static float _bounceTime;

        /// <summary>The charm is equipped and turned over to Unsteady Body.</summary>
        internal static bool Active => UnsteadyBodyMod.Local.Unsteady
            && PlayerData.instance != null && PlayerData.instance.equippedCharm_14;

        internal static void Apply(HeroController hero)
        {
            if (hero == null) return;
            if (hero != _hero) Capture(hero);

            bool worn = Active;
            bool pogo = worn && UnsteadyBodyMod.Settings.PogoLaunch;
            int s = Mathf.Clamp(UnsteadyBodyMod.Settings.Strength, 0, StrengthNames.Length - 1);
            float boost = SpeedBoost[Mathf.Clamp(UnsteadyBodyMod.Settings.SpeedLimit, 0, SpeedBoost.Length - 1)];

            hero.RECOIL_HOR_VELOCITY = _recoilVelocity * (worn ? SideSpeed[s] * boost : 1f);
            hero.RECOIL_HOR_STEPS = _recoilSteps * (worn ? SideTime[s] / boost : 1f);
            hero.BOUNCE_VELOCITY = _bounceVelocity * (pogo ? PogoSpeed[s] * boost : 1f);
            hero.BOUNCE_TIME = _bounceTime * (pogo ? PogoTime[s] / boost : 1f);
        }

        /// <summary>
        /// How much harder an enemy is pushed when struck with Unsteady Body and Heavy
        /// Blow both equipped: the square root of the Knight's own speed multiplier, so it
        /// rises with Launch strength and the speed limit, but much more gently.
        /// </summary>
        internal static float EnemyPushFactor()
        {
            int s = Mathf.Clamp(UnsteadyBodyMod.Settings.Strength, 0, StrengthNames.Length - 1);
            float boost = SpeedBoost[Mathf.Clamp(UnsteadyBodyMod.Settings.SpeedLimit, 0, SpeedBoost.Length - 1)];
            return Mathf.Sqrt(SideSpeed[s] * boost);
        }

        internal static void Slam(HeroController hero)
        {
            Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
            if (body != null && body.velocity.y > -SlamSpeed)
            {
                body.velocity = new Vector2(body.velocity.x, -SlamSpeed);
            }
        }

        internal static void RestoreVanilla()
        {
            if (_hero != null)
            {
                _hero.RECOIL_HOR_VELOCITY = _recoilVelocity;
                _hero.RECOIL_HOR_STEPS = _recoilSteps;
                _hero.BOUNCE_VELOCITY = _bounceVelocity;
                _hero.BOUNCE_TIME = _bounceTime;
            }
            _hero = null;
        }

        private static void Capture(HeroController hero)
        {
            _hero = hero;
            _recoilVelocity = hero.RECOIL_HOR_VELOCITY;
            _recoilSteps = hero.RECOIL_HOR_STEPS;
            _bounceVelocity = hero.BOUNCE_VELOCITY;
            _bounceTime = hero.BOUNCE_TIME;

            UnsteadyBodyMod.Verbose(
                $"Vanilla values: recoil {_recoilVelocity} for {_recoilSteps} steps, " +
                $"pogo {_bounceVelocity} for {_bounceTime}s");
        }
    }
}
