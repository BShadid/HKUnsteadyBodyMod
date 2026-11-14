using System.Collections;
using GlobalEnums;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Stops a fast launch from skipping through room exits and solid ground.
    ///
    /// Room exits (TransitionPoint) are trigger areas that fire when the Knight
    /// overlaps them. At high launch speeds the Knight can move further in one
    /// physics step than an exit is wide, so they're on one side of it, then past
    /// it, and never touch it. After every physics step, this traces the path they just
    /// travelled; if it crossed an exit they didn't touch, they're put back inside
    /// that exit, and the game's own exit check fires on the next step.
    ///
    /// While the charm is worn, it also turns on continuous collision detection
    /// for the Knight, which stops them passing through walls and floors.
    /// </summary>
    internal class LaunchGuard : MonoBehaviour
    {
        // Only trace steps longer than normal movement (a dash is about 0.4 per step).
        private const float MinStep = 0.35f;

        // Ignore jumps longer than this: those are teleports (hazard respawns,
        // benches, room loads), not launches. A 4x Ludicrous launch is about 3 per step.
        private const float MaxStep = 15f;

        // How far into the exit to place them, so they're clearly overlapping it.
        private const float Inset = 0.4f;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[16];
        private ContactFilter2D _filter;

        private HeroController _hero;
        private Rigidbody2D _body;
        private Collider2D _collider;
        private CollisionDetectionMode2D? _originalMode;

        private Vector2 _lastPosition;
        private bool _haveLast;

        private void OnEnable()
        {
            _filter = new ContactFilter2D { useTriggers = true };
            StartCoroutine(AfterEachPhysicsStep());
        }

        private ImpactListener _listener;

        private void OnDestroy()
        {
            SetContinuous(false);
            if (_listener != null) Destroy(_listener);
        }

        private void LateUpdate() => Momentum.DuringEntry(HeroController.instance);

        private IEnumerator AfterEachPhysicsStep()
        {
            var wait = new WaitForFixedUpdate();
            while (true)
            {
                yield return wait;
                Check();
            }
        }

        private void Check()
        {
            HeroController hero = HeroController.instance;
            if (hero == null)
            {
                _haveLast = false;
                return;
            }

            if (hero != _hero)
            {
                _hero = hero;
                _body = hero.GetComponent<Rigidbody2D>();
                _collider = hero.GetComponent<Collider2D>();
                _originalMode = null;
                _haveLast = false;
                if (hero.GetComponent<ImpactListener>() == null) _listener = hero.gameObject.AddComponent<ImpactListener>();
            }
            if (_body == null || _collider == null) return;

            bool worn = Launch.Active;
            SetContinuous(worn);

            Vector2 now = _body.position;
            bool playing = GameManager.instance != null
                && GameManager.instance.gameState == GameState.PLAYING
                && !hero.cState.transitioning;
            bool walkIn = Momentum.PushingEntry;

            if (worn && (playing || walkIn) && _haveLast)
            {
                Vector2 step = now - _lastPosition;
                float distance = step.magnitude;

                if (distance > MinStep && distance < MaxStep)
                {
                    Vector2 direction = step / distance;
                    if (FindSkippedExit(_lastPosition, direction, distance, walkIn, out float at, out TransitionPoint exit))
                    {
                        // Normally: put them inside the exit so it fires. During a walk-in the
                        // room isn't ready to send them on, so stop them just short of it instead;
                        // the launch resumes once they're finished entering.
                        float placeAt = walkIn ? Mathf.Max(0f, at - Inset) : Mathf.Min(at + Inset, distance);
                        Vector2 place = _lastPosition + direction * placeAt;
                        _body.position = place;
                        hero.transform.position = new Vector3(place.x, place.y, hero.transform.position.z);
                        now = place;

                        if (walkIn)
                        {
                            Momentum.BlockEntry(hero);
                            UnsteadyBodyMod.Verbose($"Stopped short of {exit.name} during the walk-in");
                        }
                        else
                        {
                            UnsteadyBodyMod.Verbose($"Caught a skipped exit: {exit.name} to {exit.targetScene}");
                        }
                    }
                }
            }

            _lastPosition = now;
            _haveLast = true;
        }

        private bool FindSkippedExit(Vector2 from, Vector2 direction, float distance, bool includeTouching, out float at, out TransitionPoint exit)
        {
            at = 0f;
            exit = null;

            Bounds bounds = _collider.bounds;
            Vector2 centreOffset = (Vector2)bounds.center - _body.position;
            Vector2 size = (Vector2)bounds.size * 0.9f;

            int count = Physics2D.BoxCast(from + centreOffset, size, 0f, direction, _filter, _hits, distance);

            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider == null || hit.distance <= 0f) continue; // started inside it: not skipped

                TransitionPoint gate = hit.collider.GetComponent<TransitionPoint>();
                if (gate == null || gate.isADoor || string.IsNullOrEmpty(gate.targetScene)) continue;

                // Already touching it now? Then the game will handle it (except
                // during a walk-in, when it can't yet).
                if (!includeTouching && _collider.Distance(hit.collider).isOverlapped) continue;

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    exit = gate;
                }
            }

            if (exit == null) return false;
            at = nearest;
            return true;
        }

        private void SetContinuous(bool on)
        {
            if (_body == null) return;

            if (on)
            {
                if (_originalMode == null) _originalMode = _body.collisionDetectionMode;
                _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
            else if (_originalMode != null)
            {
                _body.collisionDetectionMode = _originalMode.Value;
                _originalMode = null;
            }
        }
    }
}
