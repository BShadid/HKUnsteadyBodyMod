using System.Reflection;
using GlobalEnums;
using Modding;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Experimental, off by default: carries an unfinished launch through a room exit.
    ///
    /// When the Knight leaves a room mid-launch, this records how much of the launch
    /// was left. When they have finished entering the next room (and the game has handed
    /// control back), it restarts the launch for just that remaining part, in the same
    /// direction. It only carries over when the exit matches the launch: a sideways
    /// launch through a side exit, or a pogo launch through a top exit.
    ///
    /// It doesn't carry over if a cutscene or anything else has taken control on
    /// entry, or if the room took too long to load.
    /// </summary>
    internal static class Momentum
    {
        private const float MaxLoadSeconds = 10f;
        private const int MinSteps = 2;
        private const float MinPogoSeconds = 0.05f;

        private enum Kind { None, Left, Right, Up }

        private static Kind _kind;
        private static float _remaining; // physics steps for sideways, seconds for pogo
        private static float _leftAt;

        // Walk-in: when entering through a side exit, the game walks them in for
        // 0.33 s at running speed. While carrying a sideways launch, that walk
        // runs at launch speed instead, and the time comes off what's left.
        private static bool _entryPushing;
        private static bool _entryBlocked;
        private static float _entryStart;
        private static float _entryEnd;

        /// <summary>True while the walk-in is being driven at launch speed.</summary>
        internal static bool PushingEntry => _entryPushing && !_entryBlocked;

        internal static void Hook()
        {
            On.HeroController.LeaveScene += OnLeaveScene;
            On.HeroController.FinishedEnteringScene += OnFinishedEnteringScene;
        }

        internal static void Unhook()
        {
            On.HeroController.LeaveScene -= OnLeaveScene;
            On.HeroController.FinishedEnteringScene -= OnFinishedEnteringScene;
            _kind = Kind.None;
        }

        private static void OnLeaveScene(On.HeroController.orig_LeaveScene orig, HeroController self, GatePosition? gate)
        {
            Record(self, gate);
            orig(self, gate);
        }

        private static void Record(HeroController hero, GatePosition? gate)
        {
            _kind = Kind.None;
            _entryPushing = false;
            _entryBlocked = false;
            if (!UnsteadyBodyMod.Settings.CarryMomentum || !Launch.Active || gate == null) return;

            if (hero.cState.recoilingRight && gate == GatePosition.right
                || hero.cState.recoilingLeft && gate == GatePosition.left)
            {
                int done = ReflectionHelper.GetField<HeroController, int>(hero, "recoilSteps");
                float left = hero.RECOIL_HOR_STEPS - done;
                if (left < MinSteps) return;
                _kind = hero.cState.recoilingRight ? Kind.Right : Kind.Left;
                _remaining = left;
            }
            else if (hero.cState.bouncing && gate == GatePosition.top && UnsteadyBodyMod.Settings.PogoLaunch)
            {
                float elapsed = ReflectionHelper.GetField<HeroController, float>(hero, "bounceTimer");
                float left = hero.BOUNCE_TIME - elapsed;
                if (left < MinPogoSeconds) return;
                _kind = Kind.Up;
                _remaining = left;
            }

            if (_kind != Kind.None) _leftAt = Time.unscaledTime;
        }

        /// <summary>
        /// Called every frame. Drives the game's room entry at launch speed:
        /// the side walk-in for a sideways launch, or the hop up from below for a
        /// pogo launch. When the hop ends, it finishes the entry straight away
        /// instead of waiting for them to land, so the pogo carries on at once.
        /// </summary>
        internal static void DuringEntry(HeroController hero)
        {
            if (hero == null || _kind == Kind.None) return;
            if (!UnsteadyBodyMod.Settings.CarryMomentum || !Launch.Active) return;

            var state = ReflectionHelper.GetField<HeroController, HeroTransitionState>(hero, "transitionState");

            if (_kind == Kind.Up && state == HeroTransitionState.DROPPING_DOWN && _entryPushing)
            {
                FinishEntryNow(hero);
                return;
            }

            if (_entryBlocked || state != HeroTransitionState.ENTERING_SCENE) return;

            if (!_entryPushing)
            {
                _entryPushing = true;
                _entryStart = Time.time;
                LaunchSounds.Launch(hero);
            }

            Launch.Apply(hero);
            if (_kind == Kind.Up)
            {
                Vector2 hop = ReflectionHelper.GetField<HeroController, Vector2>(hero, "transition_vel");
                float entrySpeed = Launch.PogoSpeedAt(hero, hero.BOUNCE_TIME - _remaining);
                ReflectionHelper.SetField(hero, "transition_vel", new Vector2(hop.x, Mathf.Max(entrySpeed, hop.y)));
            }
            else
            {
                float speed = hero.RECOIL_HOR_VELOCITY;
                ReflectionHelper.SetField(hero, "transition_vel", new Vector2(_kind == Kind.Right ? speed : -speed, 0f));
            }
        }

        // The game only ends an entry from below when they land. Calling the same
        // method it would call then, as soon as the hop is over, hands control back
        // mid-air and lets OnFinishedEnteringScene resume the pogo.
        private static readonly MethodInfo FinishedEnteringScene =
            typeof(HeroController).GetMethod("FinishedEnteringScene", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void FinishEntryNow(HeroController hero)
        {
            if (FinishedEnteringScene == null) return;
            try { FinishedEnteringScene.Invoke(hero, new object[] { true, false }); }
            catch (System.Exception e) { UnsteadyBodyMod.Instance?.LogWarn("Couldn't finish the room entry early: " + e.Message); }
        }

        /// <summary>An exit is ahead during the walk-in: stop pushing until the entry finishes.</summary>
        internal static void BlockEntry(HeroController hero)
        {
            if (!PushingEntry) return;
            _entryBlocked = true;
            _entryEnd = Time.time;
            ReflectionHelper.SetField(hero, "transition_vel", Vector2.zero);
        }

        private static void OnFinishedEnteringScene(On.HeroController.orig_FinishedEnteringScene orig,
            HeroController self, bool setHazardMarker, bool preventRunBob)
        {
            orig(self, setHazardMarker, preventRunBob);

            Kind kind = _kind;
            _kind = Kind.None;

            if (_entryPushing)
            {
                float pushed = (_entryBlocked ? _entryEnd : Time.time) - _entryStart;
                _entryPushing = false;
                _entryBlocked = false;
                if (kind == Kind.Up)
                {
                    _remaining -= pushed;
                    if (_remaining < MinPogoSeconds) return;
                }
                else
                {
                    _remaining -= pushed / Time.fixedDeltaTime;
                    if (_remaining < MinSteps) return;
                }
            }

            if (kind == Kind.None) return;

            if (!UnsteadyBodyMod.Settings.CarryMomentum || !Launch.Active) return;
            if (Time.unscaledTime - _leftAt > MaxLoadSeconds) return;
            if (self.controlReqlinquished || self.cState.transitioning) return;
            if (GameManager.instance == null || GameManager.instance.gameState != GameState.PLAYING) return;

            Launch.Apply(self);
            Rigidbody2D body = self.GetComponent<Rigidbody2D>();
            if (body == null) return;

            if (kind == Kind.Up)
            {
                self.cState.bouncing = true;
                float elapsed = Mathf.Max(0f, self.BOUNCE_TIME - _remaining);
                ReflectionHelper.SetField(self, "bounceTimer", elapsed);
                body.velocity = new Vector2(body.velocity.x, Launch.PogoSpeedAt(self, elapsed));
            }
            else
            {
                bool right = kind == Kind.Right;
                self.cState.recoilingRight = right;
                self.cState.recoilingLeft = !right;
                ReflectionHelper.SetField(self, "recoilLarge", false);
                ReflectionHelper.SetField(self, "recoilSteps", Mathf.Max(0, Mathf.FloorToInt(self.RECOIL_HOR_STEPS - _remaining)));
                body.velocity = new Vector2(right ? self.RECOIL_HOR_VELOCITY : -self.RECOIL_HOR_VELOCITY, body.velocity.y);
            }

            UnsteadyBodyMod.Verbose($"Carried momentum into {GameManager.instance.sceneName} ({kind})");
        }
    }
}
