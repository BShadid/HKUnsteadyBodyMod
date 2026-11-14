using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Unsteady Body (Unsteady side up) + Heavy Blow: every nail strike that lands
    /// on an enemy that can take damage, and launches the Knight, sets off an
    /// explosion at the point of impact. (An upward strike from the ground doesn't
    /// launch them, so it doesn't explode: the safe way to gain Soul.)
    ///
    /// It's the game's own explosion, the one its exploding enemies and spore bombs
    /// use ("Gas Explosion Recycle M"), so it looks and sounds like it belongs.
    /// A fresh copy is made each time rather than borrowed from the game's shared
    /// supply, because changing a borrowed one would change the game's explosions too.
    ///
    /// Every enemy in the blast, including the one struck, takes damage equal to the
    /// nail's, and the struck enemy is pushed back harder (see Launch.EnemyPushFactor).
    /// The blast is 1.75 times the size of the game's.
    ///
    /// It hurts the Knight too, but without the game's usual knockback, which would
    /// cancel their launch: the game's own damage from the blast is blocked, and the
    /// mod takes the health itself (see BlastHurtsKnight). They can outrun it: the
    /// blast can only hurt them in its first half-second.
    /// </summary>
    internal static class JellyBlast
    {
        private const string ExplosionName = "Gas Explosion Recycle M";
        private const int HeavyBlowCharm = 15;

        // Blast damage as a multiple of the nail's damage.
        private const float DamageMultiplier = 1f;

        // Blast size compared with the game's own (1.75 = three quarters larger across).
        private const float BlastScale = 1.75f;

        private const int EnemyLayer = 11;

        private static GameObject _prefab;
        private static bool _searched;
        private static bool _loadedAll;
        private static bool _blasting; // the blast's own hits shouldn't set off more blasts

        internal static void Hook()
        {
            On.HealthManager.Hit += OnHit;
            On.HeroController.TakeDamage += OnHeroTakeDamage;
        }

        internal static void Unhook()
        {
            On.HealthManager.Hit -= OnHit;
            On.HeroController.TakeDamage -= OnHeroTakeDamage;
        }

        // ---------- The game's own blast damage is blocked ----------
        // The game's damage would cancel the Knight's launch, so any hit on them that
        // comes from a blast, or from an explosion effect in the moments after one
        // (such as its lingering puff), is blocked. BlastHurtsKnight deals the damage
        // instead, without the knockback.

        /// <summary>Lets one blast hit through to the game's normal damage (used when it's fatal).</summary>
        internal static bool AllowNextHit;

        // Testing confirmed the game's own blast damage always comes from the explosion
        // we spawned (or something inside it), so that's all we block: no time window,
        // no guessing by name. A real jelly or other explosion still hurts normally.
        private static readonly List<GameObject> _blasts = new List<GameObject>();
        private static bool _reportedBlock;

        private static void OnHeroTakeDamage(On.HeroController.orig_TakeDamage orig, HeroController self,
            GameObject go, GlobalEnums.CollisionSide damageSide, int damageAmount, int hazardType)
        {
            if (AllowNextHit)
            {
                AllowNextHit = false;
                HurtAndMuffle(orig, self, go, damageSide, damageAmount, hazardType);
                return;
            }
            if (go != null && FromBlast(go))
            {
                if (!_reportedBlock)
                {
                    _reportedBlock = true;
                    UnsteadyBodyMod.Verbose($"Blocked the game's own blast damage (source: {PathOf(go.transform)})");
                }
                return;
            }
            HurtAndMuffle(orig, self, go, damageSide, damageAmount, hazardType);
        }

        // Any real hit on the Knight muffles the Geiger clicks too, like the rest of the game's sound.
        private static void HurtAndMuffle(On.HeroController.orig_TakeDamage orig, HeroController self,
            GameObject go, GlobalEnums.CollisionSide damageSide, int damageAmount, int hazardType)
        {
            PlayerData pd = PlayerData.instance;
            int before = pd != null ? pd.health + pd.healthBlue : 0;
            orig(self, go, damageSide, damageAmount, hazardType);
            if (pd != null && pd.health + pd.healthBlue < before) GeigerCounter.Muffle();
        }

        private static bool FromBlast(GameObject go)
        {
            _blasts.RemoveAll(b => b == null);
            foreach (GameObject b in _blasts)
            {
                if (go == b || go.transform.IsChildOf(b.transform)) return true;
            }
            return false;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private static void OnHit(On.HealthManager.orig_Hit orig, HealthManager self, HitInstance hit)
        {
            bool armed = !_blasting
                && hit.AttackType == AttackTypes.Nail
                && Launch.Active
                && PlayerData.instance != null && PlayerData.instance.GetBool("equippedCharm_" + HeavyBlowCharm)
                && self != null && !self.IsInvincible
                && StrikeLaunches(hit.Direction);

            // A harder push for the struck enemy. Enemy knockback speed scales with
            // this, and the game stops knocked-back enemies at walls.
            if (armed) hit.MagnitudeMultiplier *= Launch.EnemyPushFactor();

            orig(self, hit);

            if (armed)
            {
                Explode(self);
                AngledLaunch.Start(HeroController.instance, self);
            }
        }

        /// <summary>
        /// The blast only goes off when the strike also launches the Knight. An upward
        /// strike from the ground doesn't move them, so it's a safe way to gain Soul.
        /// Directions: 0 right, 90 up, 180 left, 270 down.
        /// </summary>
        private static bool StrikeLaunches(float direction)
        {
            HeroController hero = HeroController.instance;
            float d = Mathf.Repeat(direction, 360f);
            bool up = d > 45f && d < 135f;
            bool down = d > 225f && d < 315f;

            if (up) return UnsteadyBodyMod.Settings.UpwardSlam && hero != null && !hero.cState.onGround;
            if (down) return UnsteadyBodyMod.Settings.PogoLaunch;
            return true; // sideways strikes always launch
        }

        private static void Explode(HealthManager struck)
        {
            GameObject prefab = FindPrefab();
            if (prefab == null) return;

            // The point of impact: the edge of the enemy nearest the Knight.
            Vector3 point = struck.transform.position;
            HeroController hero = HeroController.instance;
            Collider2D body = struck.GetComponent<Collider2D>();
            if (hero != null && body != null)
            {
                Vector2 edge = body.ClosestPoint(hero.transform.position);
                point = new Vector3(edge.x, edge.y, struck.transform.position.z - 0.01f);
            }

            GameObject blast = Spawn(prefab, point);
            if (blast == null) return;
            _blasts.Add(blast);

            DamageEnemies(blast, point);
        }

        // A copy of the game's explosion, made switched off so its scripts don't
        // start until the part that hurts the Knight is gone.
        private static GameObject Spawn(GameObject prefab, Vector3 point)
        {
            var holder = new GameObject("Unsteady Body Blast Holder");
            holder.SetActive(false);
            GameObject blast = Object.Instantiate(prefab, holder.transform);
            blast.name = ExplosionName + " (Unsteady Body)";

            foreach (MonoBehaviour m in blast.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (m != null && m.GetType().Name == "DamageHero") Object.DestroyImmediate(m);
            }

            blast.transform.SetParent(null, false);
            blast.transform.position = point;
            blast.transform.localScale = new Vector3(BlastScale, BlastScale, 1f);
            blast.AddComponent<BlastHurtsKnight>();
            blast.SetActive(true);
            Object.Destroy(holder);

            // The explosion normally returns itself to the game's supply when done;
            // this copy isn't from there, so make sure it goes away regardless.
            Object.Destroy(blast, 3f);
            return blast;
        }

        internal static float RadiusOf(GameObject blast)
        {
            CircleCollider2D circle = blast.GetComponent<CircleCollider2D>();
            return circle != null ? circle.radius * Mathf.Abs(blast.transform.lossyScale.x) : 2.5f * BlastScale;
        }

        private static void DamageEnemies(GameObject blast, Vector3 point)
        {
            float radius = RadiusOf(blast);

            int damage = Mathf.Max(1, Mathf.RoundToInt(PlayerData.instance.nailDamage * DamageMultiplier));
            var done = new HashSet<HealthManager>();

            _blasting = true;
            try
            {
                foreach (Collider2D c in Physics2D.OverlapCircleAll(point, radius, 1 << EnemyLayer))
                {
                    HealthManager hm = c.GetComponent<HealthManager>() ?? c.GetComponentInParent<HealthManager>();
                    if (hm == null || !done.Add(hm) || hm.IsInvincible) continue;

                    Vector2 away = (Vector2)hm.transform.position - (Vector2)point;
                    float angle = away.sqrMagnitude > 0.0001f ? Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg : 0f;
                    if (angle < 0f) angle += 360f;

                    hm.Hit(new HitInstance
                    {
                        Source = blast,
                        AttackType = AttackTypes.Generic,
                        DamageDealt = damage,
                        Multiplier = 1f,
                        Direction = angle,
                        MagnitudeMultiplier = 1f,
                        IgnoreInvulnerable = false,
                    });
                }
            }
            finally
            {
                _blasting = false;
            }
        }

        // The game's explosion, from what's already loaded. If it isn't loaded yet,
        // load the game's resources once and look again.
        private static GameObject FindPrefab()
        {
            if (_prefab != null) return _prefab;

            _prefab = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == ExplosionName && !g.scene.IsValid());

            if (_prefab == null && !_loadedAll)
            {
                _loadedAll = true;
                Resources.LoadAll<GameObject>("");
                _prefab = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(g => g.name == ExplosionName && !g.scene.IsValid());
            }

            if (!_searched)
            {
                _searched = true;
                if (_prefab != null) UnsteadyBodyMod.Verbose("Found the explosion.");
                else UnsteadyBodyMod.Instance?.LogWarn("Couldn't find the explosion; no blasts.");
            }
            return _prefab;
        }
    }
}
