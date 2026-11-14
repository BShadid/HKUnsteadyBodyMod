using System.Linq;
using System.Reflection;
using GlobalEnums;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Attached to each blast. During the blast's first half-second (the time the
    /// game's own explosion can hurt anything), if the Knight is inside it, they take
    /// a mask of damage, once. The damage skips the game's usual knockback and hit
    /// freeze, which would cancel their launch, but keeps the rest of the normal hit
    /// feedback: the hurt sound, the screen flash and shake, and the muffled audio.
    /// A launch fast enough to clear the blast in time escapes it entirely.
    ///
    /// Fair-play rules: no damage while they're already invulnerable (just after another
    /// hit) or Shade Cloak dashing; overcharming doubles it, as the game does; and a
    /// fatal hit goes through the game's normal damage so death works as usual.
    /// </summary>
    internal class BlastHurtsKnight : MonoBehaviour
    {
        private const int Damage = 1;
        private const float DangerSeconds = 0.5f;

        private float _age;
        private bool _done;

        private void FixedUpdate()
        {
            if (_done) return;
            _age += Time.fixedDeltaTime;
            if (_age > DangerSeconds)
            {
                _done = true;
                return;
            }

            HeroController hero = HeroController.instance;
            Collider2D body = hero != null ? hero.GetComponent<Collider2D>() : null;
            if (body == null) return;

            Vector2 centre = transform.position;
            float radius = JellyBlast.RadiusOf(gameObject);
            if (Vector2.Distance(body.ClosestPoint(centre), centre) > radius) return;

            _done = true;
            Hurt(hero);
        }

        private void Hurt(HeroController hero)
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return;
            if (hero.cState.invulnerable || hero.cState.shadowDashing || pd.GetBool("isInvincible")) return;

            int damage = Damage * (pd.GetBool("overcharmed") ? 2 : 1);

            if (pd.health + pd.healthBlue <= damage)
            {
                // Fatal: let the game handle it properly.
                JellyBlast.AllowNextHit = true;
                hero.TakeDamage(gameObject, CollisionSide.other, damage, 1);
                return;
            }

            hero.TakeHealth(damage);
            PlayHurtSound(hero);
            PlayHitFeedback(hero);
        }

        // The rest of the game's normal hit feedback, without the knockback:
        // - the Knight's damage effect (screen-edge flash, camera shake, the crack
        //   and dark shards), started by its "DAMAGE" event;
        // - the muffled sound ("Damaged" audio snapshot for a moment, then easing
        //   back), started by "DAMAGE LOWPASS" on the game's audio manager.
        private static void PlayHitFeedback(HeroController hero)
        {
            FieldInfo f = typeof(HeroController).GetField("damageEffectFSM", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            (f?.GetValue(hero) as PlayMakerFSM)?.SendEvent("DAMAGE");

            GameObject audioManager = GameObject.Find("_GameManager/AudioManager");
            PlayMakerFSM muffle = audioManager != null
                ? audioManager.GetComponents<PlayMakerFSM>().FirstOrDefault(m => m.FsmName == "Hero Damaged Effects")
                : null;
            muffle?.SendEvent("DAMAGE LOWPASS");
            GeigerCounter.Muffle();
        }

        private static void PlayHurtSound(HeroController hero)
        {
            HeroAudioController audio = hero.GetComponent<HeroAudioController>();
            if (audio == null) return;
            FieldInfo f = typeof(HeroAudioController).GetField("takeHit", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f?.GetValue(audio) is AudioSource source && source.clip != null) source.PlayOneShot(source.clip);
        }
    }
}
