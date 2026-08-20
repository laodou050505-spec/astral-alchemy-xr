using UnityEngine;

namespace AstralAlchemy
{
    [ExecuteAlways]
    public class AstralEnemy : MonoBehaviour
    {
        public enum EnemyType { Sprite, Wisp, Brute }
        public enum BuffType { None, Ward, Frenzy, Overcharge, Siphon }

        public EnemyType enemyType;
        public ElementKind weakness;
        public float health = 32f;
        public float moveSpeed = .62f;
        public float touchDamage = 8f;
        public Renderer[] tintRenderers;
        public Transform visual;
        public GameObject spriteVisual;
        public GameObject wispVisual;
        public GameObject bruteVisual;
        public ParticleSystem defeatBurst;
        public Light weaknessLight;
        public Transform footRing;
        public Transform independentStatusFx;
        public ParticleSystem hitBurst;

        private Transform target;
        private bool shielded;
        private float shieldTimer;
        private bool dead;
        private float frozenTimer;
        private float buffTimer;
        private BuffType buffType;
        private float baseMoveSpeed;
        private float baseTouchDamage;
        private Vector3 independentFxBasePosition;
        private Quaternion independentFxBaseRotation;
        private float independentFxPhase;

        public bool IsShielded => shielded;

        private void OnEnable()
        {
            RefreshVisuals();
        }

        private void OnValidate()
        {
            RefreshVisuals();
        }

        private void Start()
        {
            var core = FindFirstObjectByType<StarCoreController>();
            target = core != null ? core.TargetTransform : null;
            FaceTargetHorizontally(true);
            RefreshVisuals();
            baseMoveSpeed = moveSpeed;
            baseTouchDamage = touchDamage;
            if (independentStatusFx != null)
            {
                independentFxBasePosition = independentStatusFx.localPosition;
                independentFxBaseRotation = independentStatusFx.localRotation;
                independentFxPhase = Mathf.Abs(transform.position.x * .73f + transform.position.z * .41f);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (dead || target == null) return;
            if (frozenTimer > 0f)
            {
                frozenTimer -= Time.deltaTime;
                if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 2f) * 1.5f);
                return;
            }
            if (buffTimer > 0f)
            {
                buffTimer -= Time.deltaTime;
                if (buffTimer <= 0f) ClearBuff();
            }
            if (shieldTimer > 0f)
            {
                shieldTimer -= Time.deltaTime;
                if (shieldTimer <= 0f)
                {
                    shielded = false;
                    ApplyWeaknessColor();
                }
            }
            var direction = target.position - transform.position;
            direction.y = 0f;
            // Enemy roots must never inherit pitch/roll from spawn markers or LookAt calls.
            var yaw = transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (direction.magnitude > 1.5f)
            {
                transform.position += direction.normalized * moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 5f * Time.deltaTime);
            }
            else
            {
                var core = target.GetComponentInParent<StarCoreController>();
                if (core != null)
                {
                    core.Damage(touchDamage);
                    dead = true;
                    var game = FindFirstObjectByType<ArenaGameController>();
                    if (game != null) game.SetStatus("敌人突破防线 · 星核 -" + Mathf.RoundToInt(touchDamage), new Color(1f, .26f, .32f));
                    Destroy(gameObject);
                }
            }
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 4.5f + transform.position.x)) * .08f, 0f);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 4.5f) * 4f);
            }
            if (independentStatusFx != null)
            {
                var phase = Time.time * 1.35f + independentFxPhase;
                independentStatusFx.localPosition = independentFxBasePosition + new Vector3(
                    Mathf.Sin(phase * .71f) * .035f,
                    Mathf.Sin(phase) * .075f,
                    Mathf.Cos(phase * .83f) * .03f);
                independentStatusFx.localRotation = independentFxBaseRotation * Quaternion.Euler(
                    Mathf.Sin(phase * .61f) * 3f,
                    phase * 10f,
                    Mathf.Cos(phase * .57f) * 3f);
            }
            if (footRing != null) footRing.Rotate(0f, 18f * Time.deltaTime, 0f, Space.Self);
        }

        private void FaceTargetHorizontally(bool immediate)
        {
            if (target == null) return;
            var direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f) return;
            var desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = immediate ? desired : Quaternion.Slerp(transform.rotation, desired, 5f * Time.deltaTime);
        }

        public void Hit(ElementKind spell, float damage)
        {
            if (dead) return;
            var multiplier = spell == weakness ? 2.25f : .72f;
            if (shielded) multiplier *= .35f;
            health -= damage * multiplier;
            PlayHitFeedback(spell, spell == weakness);
            if (health <= 0f) Defeat();
        }

        public void PlayHitFeedback(ElementKind spell, bool weaknessHit)
        {
            if (hitBurst != null)
            {
                var main = hitBurst.main;
                main.startColor = ElementPalette.ColorOf(spell);
                main.startSize = weaknessHit ? .19f : .12f;
                hitBurst.transform.position = transform.position + Vector3.up * 1f;
                hitBurst.Play(true);
            }
            if (visual != null) StartCoroutine(HitPunch(weaknessHit ? 1.16f : 1.08f));
        }

        private System.Collections.IEnumerator HitPunch(float strength)
        {
            var original = visual.localScale;
            for (var t = 0f; t < .1f; t += Time.deltaTime)
            {
                visual.localScale = original * Mathf.Lerp(1f, strength, t / .1f);
                yield return null;
            }
            for (var t = 0f; t < .16f; t += Time.deltaTime)
            {
                visual.localScale = original * Mathf.Lerp(strength, 1f, t / .16f);
                yield return null;
            }
            if (visual != null) visual.localScale = original;
        }

        public void Shield(float duration)
        {
            shielded = true;
            shieldTimer = duration;
            ApplyTint(new Color(.42f, .15f, 1f), 2.1f);
        }

        public void Freeze(float duration)
        {
            frozenTimer = Mathf.Max(frozenTimer, duration);
            ApplyTint(new Color(.42f, .9f, 1f), 2.2f);
        }

        public void ApplyBuff(BuffType type, float duration)
        {
            buffType = type;
            buffTimer = Mathf.Max(buffTimer, duration);
            moveSpeed = baseMoveSpeed;
            touchDamage = baseTouchDamage;
            switch (type)
            {
                case BuffType.Ward: Shield(duration); ApplyTint(new Color(.58f, .25f, 1f), 2.4f); break;
                case BuffType.Frenzy: moveSpeed *= 1.46f; touchDamage *= 1.34f; ApplyTint(new Color(1f, .24f, .1f), 2.5f); break;
                case BuffType.Overcharge: moveSpeed *= 1.55f; touchDamage *= 1.18f; ApplyTint(new Color(1f, .78f, .2f), 2.5f); break;
                case BuffType.Siphon: moveSpeed *= 1.08f; touchDamage *= 1.12f; ApplyTint(new Color(1f, .38f, .78f), 2.3f); break;
            }
        }

        public void ConfigureType(EnemyType type)
        {
            enemyType = type;
            if (spriteVisual != null) spriteVisual.SetActive(type == EnemyType.Sprite);
            if (wispVisual != null) wispVisual.SetActive(type == EnemyType.Wisp);
            if (bruteVisual != null) bruteVisual.SetActive(type == EnemyType.Brute);
            // Keep three readable silhouettes. Scaling the enemy root also scales its
            // capsule collider, so visual size and hit target remain synchronized.
            transform.localScale = type switch
            {
                EnemyType.Wisp => Vector3.one * .84f,
                EnemyType.Brute => Vector3.one * 1.16f,
                _ => Vector3.one
            };
            if (type == EnemyType.Wisp) { health = 38f; moveSpeed = 1.16f; touchDamage = 6f; }
            else if (type == EnemyType.Brute) { health = 105f; moveSpeed = .45f; touchDamage = 14f; }
            else { health = 58f; moveSpeed = .7f; touchDamage = 8f; }
            baseMoveSpeed = moveSpeed;
            baseTouchDamage = touchDamage;
        }

        private void ClearBuff()
        {
            buffType = BuffType.None;
            moveSpeed = baseMoveSpeed;
            touchDamage = baseTouchDamage;
            ApplyWeaknessColor();
        }

        private void Defeat()
        {
            dead = true;
            if (defeatBurst != null)
            {
                defeatBurst.transform.parent = null;
                defeatBurst.Play(true);
                var main = defeatBurst.main;
                Destroy(defeatBurst.gameObject, main.duration + main.startLifetime.constantMax + .25f);
            }
            var game = FindFirstObjectByType<ArenaGameController>();
            if (game != null) game.OnEnemyDefeated(this);
            Destroy(gameObject);
        }

        public void RefreshVisuals()
        {
            var color = ElementPalette.ColorOf(weakness);
            ApplyTint(color, 1.5f);
        }

        private void ApplyWeaknessColor()
        {
            RefreshVisuals();
        }

        private void ApplyTint(Color color, float emissionIntensity)
        {
            if (weaknessLight != null)
            {
                weaknessLight.color = color;
                weaknessLight.intensity = shielded ? .65f : .48f;
            }
            if (tintRenderers == null) return;
            foreach (var renderer in tintRenderers)
            {
                if (renderer == null) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                block.SetColor("_BaseColor", color);
                block.SetColor("_EmissionColor", color * emissionIntensity);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
