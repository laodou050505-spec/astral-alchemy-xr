using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AstralAlchemy
{
    public class ArenaGameController : MonoBehaviour
    {
        [Header("Gameplay")]
        public StarCoreController starCore;
        public CauldronController cauldron;
        public List<ElementOrb> elementOrbs = new List<ElementOrb>();
        public Transform[] enemySpawnPoints;
        public GameObject enemyPrefab;
        public int defeatTarget = 12;
        public float roundDuration = 90f;
        [Tooltip("Matches the Three.js intro grace period before the first enemy enters.")]
        public float initialEnemyDelay = 8.5f;

        [Header("UI")]
        public Text recipeTitle;
        public Text recipeDetail;
        public Text scoreLabel;
        public Text coreLabel;
        public Text timerLabel;
        public Text statusLabel;
        public Text selectionLabel;
        public Image[] recipeTokens;
        public Image coreFill;
        public Image tideFill;
        public GameObject resultPanel;
        public Text resultTitle;
        public Text resultStats;
        public GameObject crosshair;

        [Header("Feedback")]
        public ParticleSystem castBurst;
        public AudioSource music;
        public AudioSource spellAudio;
        public AudioSource positiveAudio;

        public bool IsEnded { get; private set; }
        public ElementKind CurrentRecipeA => currentRecipeA;
        public ElementKind CurrentRecipeB => currentRecipeB;
        public bool IsSpellReady => spellReady;
        private ElementKind first;
        private ElementKind second;
        private bool hasFirst;
        private bool spellReady;
        private ElementKind preparedSpellElement;
        private string preparedSpellName;
        private float timeRemaining;
        private int score;
        private int defeated;
        private float tide;
        private float spawnCooldown;
        private float elapsed;
        private bool enemyShielded;
        private int activeMiscastShields;
        private ElementPalette.Recipe preparedRecipe;
        private int suggestedRecipeIndex;
        private float recipeClock;

        private readonly ElementKind[] rotations = { ElementKind.Fire, ElementKind.Frost, ElementKind.Storm, ElementKind.Bloom, ElementKind.Star, ElementKind.Void };

        private void Start()
        {
            timeRemaining = roundDuration;
            elapsed = 0f;
            spawnCooldown = initialEnemyDelay;
            if (resultPanel != null) resultPanel.SetActive(false);
            if (music != null) music.Play();
            ChooseRecipe();
            RefreshHud();
        }

        private void Update()
        {
            if (IsEnded) return;
            timeRemaining -= Time.deltaTime;
            elapsed += Time.deltaTime;
            spawnCooldown -= Time.deltaTime;
            recipeClock += Time.deltaTime;
            if (spawnCooldown <= 0f)
            {
                SpawnEnemy();
                // Three.js starts at 3.15s and gradually accelerates to 0.9s.
                spawnCooldown = Mathf.Max(.9f, 3.15f - elapsed * .018f);
            }
            if (recipeClock >= 14f) ChooseRecipe();
            if (timeRemaining <= 0f) EndGame(starCore != null && starCore.Health > 0f);
            RefreshHud();
        }

        public void ReceiveElement(ElementOrb orb)
        {
            if (IsEnded || orb == null) return;
            if (spellReady)
            {
                SetStatus("已选满两枚元素 · 请先释放「" + preparedSpellName + "」", new Color(1f, .76f, .22f));
                return;
            }
            orb.PulseToCauldron(cauldron.transform);
            if (!hasFirst)
            {
                first = orb.element;
                hasFirst = true;
                SetStatus("第一枚元素已入锅 · 再投入一枚完成炼制", ElementPalette.ColorOf(first));
                return;
            }
            second = orb.element;
            hasFirst = false;
            var recipe = ElementPalette.GetRecipe(first, second);
            if (!recipe.unstable) CompleteCorrectRecipe(recipe);
            else
            {
                CompleteWrongRecipe(recipe);
                second = default;
            }
        }

        private ElementKind currentRecipeA;
        private ElementKind currentRecipeB;

        private void CompleteCorrectRecipe(ElementPalette.Recipe recipe)
        {
            spellReady = true;
            preparedSpellElement = first;
            preparedRecipe = recipe;
            preparedSpellName = recipe.name;
            cauldron.Correct(first, second);
            tide = Mathf.Min(100f, tide + 16f);
            SetStatus("炼制成功 · 右键 / 空格释放「" + preparedSpellName + "」", new Color(1f, .76f, .21f));
            if (positiveAudio != null) positiveAudio.Play();
            if (ElementPalette.IsPair(recipe.a, recipe.b, currentRecipeA, currentRecipeB))
            {
                score += 260;
                tide = Mathf.Min(100f, tide + 8f);
            }
        }

        private void CompleteWrongRecipe(ElementPalette.Recipe recipe)
        {
            var miscastA = first;
            var miscastB = second;
            cauldron.Rampage(miscastA, miscastB);
            SetStatus("错误配方「" + recipe.name + "」· " + recipe.role, recipe.color);
            StartCoroutine(ApplyMiscastEffect(recipe));
        }

        private IEnumerator ApplyMiscastEffect(ElementPalette.Recipe recipe)
        {
            var enemies = FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None);
            if (recipe.style == "wildfire")
            {
                foreach (var enemy in enemies) { enemy.Hit(ElementKind.Fire, 14f); enemy.ApplyBuff(AstralEnemy.BuffType.Frenzy, 9f); }
                SetStatus("炽芽暴走：火雨清场，幸存敌人进入狂暴", recipe.color);
            }
            else if (recipe.style == "backfire" || recipe.style == "overload")
            {
                foreach (var enemy in enemies) enemy.ApplyBuff(recipe.style == "backfire" ? AstralEnemy.BuffType.Overcharge : AstralEnemy.BuffType.Frenzy, 9f);
                if (starCore != null) starCore.Damage(7f);
                SetStatus("炼金反噬：星核 -7，敌人速度与攻击提升", recipe.color);
            }
            else if (recipe.style == "rift")
            {
                activeMiscastShields++;
                enemyShielded = true;
                foreach (var enemy in enemies) enemy.ApplyBuff(AstralEnemy.BuffType.Ward, 12f);
                if (starCore != null) starCore.Damage(7f);
                SetStatus("黯焰裂缝：敌人获得虚空盾，星核 -7", recipe.color);
            }
            else if (recipe.style == "flashfreeze")
            {
                foreach (var enemy in enemies) enemy.Freeze(6.5f);
                if (starCore != null) starCore.Heal(8f);
                SetStatus("极昼风暴：全场冻结，星核 +8", recipe.color);
            }
            else if (recipe.style == "pollen")
            {
                if (starCore != null) starCore.Heal(18f);
                foreach (var enemy in enemies) enemy.ApplyBuff(AstralEnemy.BuffType.Siphon, 10f);
                SetStatus("流星花粉：星核 +18，残敌获得汲能", recipe.color);
            }
            else if (recipe.style == "bubbleburst")
            {
                foreach (var orb in elementOrbs) if (orb != null) orb.Supply();
                SetStatus("彗星霜雾：六色元素泡泡已全部补齐", recipe.color);
            }
            else if (recipe.style == "vine")
            {
                foreach (var enemy in enemies) enemy.Freeze(3.5f);
                SpawnEnemy(AstralEnemy.BuffType.Ward);
                SetStatus("食星藤蔓：敌人被缠绕，但唤醒一只护盾怪", recipe.color);
            }
            else
            {
                foreach (var enemy in enemies) enemy.Hit(ElementKind.Star, 12f);
                SetStatus(recipe.name + "：失控星尘横扫全场", recipe.color);
            }
            yield return new WaitForSeconds(3.2f);
            activeMiscastShields = Mathf.Max(0, activeMiscastShields - 1);
            enemyShielded = activeMiscastShields > 0;
        }

        public void CastAtRay(Ray ray, float charge = .8f)
        {
            if (!spellReady || IsEnded)
            {
                SetStatus("先按配方将两枚正确元素投入坩埚", new Color(.78f, .84f, 1f));
                return;
            }
            spellReady = false;
            hasFirst = false;
            var perfect = charge >= .72f && charge <= 1.16f;
            var chargeMultiplier = perfect ? 1.9f : Mathf.Lerp(.7f, 1.45f, Mathf.Clamp01(charge / 1.55f));
            var hitSomething = false;
            var impactPoint = ray.origin + ray.direction * 5f;
            var castMessage = "「" + preparedSpellName + "」已释放 · 再次炼制以准备下一发";
            var castColor = ElementPalette.ColorOf(preparedSpellElement);
            if (Physics.Raycast(ray, out var hit, 60f))
            {
                var enemy = hit.collider.GetComponentInParent<AstralEnemy>();
                if (enemy != null)
                {
                    impactPoint = enemy.transform.position + Vector3.up * 1.05f;
                    enemy.Hit(preparedSpellElement, (preparedRecipe.damage + tide * .18f) * chargeMultiplier);
                    hitSomething = true;
                    castMessage = preparedSpellElement == enemy.weakness
                        ? "弱点命中 · " + ElementPalette.ChineseName(enemy.weakness) + " 元素造成强化伤害"
                        : (enemy.IsShielded ? "护盾生效 · 伤害被削弱" : "法术命中 · 敌人受到伤害");
                    castColor = preparedSpellElement == enemy.weakness ? new Color(1f, .76f, .2f) : new Color(.78f, .88f, 1f);
                }
            }
            if (!hitSomething)
            {
                var closest = FindClosestEnemy(ray);
                if (closest != null)
                {
                    impactPoint = closest.transform.position + Vector3.up * 1.05f;
                    closest.Hit(preparedSpellElement, (preparedRecipe.damage * .72f + tide * .12f) * chargeMultiplier);
                }
            }
            CreateElementProjectile(ray.origin + ray.direction * .42f, impactPoint, preparedSpellElement, preparedRecipe.a == preparedSpellElement ? preparedRecipe.b : preparedRecipe.a);
            if (castBurst != null)
            {
                castBurst.transform.position = impactPoint + Vector3.up * .12f;
                var main = castBurst.main;
                main.startColor = ElementPalette.ColorOf(preparedSpellElement);
                var emission = castBurst.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(perfect ? 110 : 72)) });
                castBurst.Play(true);
            }
            if (spellAudio != null) spellAudio.Play();
            tide = Mathf.Min(100f, tide + 7f);
            if (perfect)
            {
                score += 150;
                tide = Mathf.Min(100f, tide + 16f);
                castMessage = "精准释放 · 「" + preparedSpellName + "」伤害提升";
                castColor = new Color(1f, .86f, .38f);
            }
            SetStatus(castMessage, castColor);
        }

        private void CreateElementProjectile(Vector3 origin, Vector3 impact, ElementKind primary, ElementKind secondary)
        {
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = "元素融合弹_" + ElementPalette.ChineseName(primary) + "_" + ElementPalette.ChineseName(secondary);
            var collider = bolt.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var projectile = bolt.AddComponent<ElementSpellProjectile>();
            projectile.Launch(origin, impact, primary, secondary);
        }

        private AstralEnemy FindClosestEnemy(Ray ray)
        {
            AstralEnemy best = null;
            var bestScore = -1f;
            foreach (var enemy in FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None))
            {
                var direction = (enemy.transform.position - ray.origin).normalized;
                var alignment = Vector3.Dot(ray.direction, direction);
                if (alignment < .35f) continue;
                var score = alignment / Mathf.Max(.1f, Vector3.Distance(ray.origin, enemy.transform.position));
                if (score > bestScore) { best = enemy; bestScore = score; }
            }
            return best;
        }

        public void OnEnemyDefeated(AstralEnemy enemy)
        {
            defeated++;
            score += 125;
            tide = Mathf.Min(100f, tide + 9f);
            if (defeated >= defeatTarget) EndGame(true);
        }

        private void SpawnEnemy(AstralEnemy.BuffType forcedBuff = AstralEnemy.BuffType.None)
        {
            if (enemyPrefab == null || enemySpawnPoints == null || enemySpawnPoints.Length == 0) return;
            var point = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Length)];
            var enemy = Instantiate(enemyPrefab, point.position, point.rotation);
            enemy.name = "敌人_" + (defeated + FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None).Length + 1);
            var behavior = enemy.GetComponent<AstralEnemy>();
            var flatForward = starCore != null ? starCore.TargetTransform.position - enemy.transform.position : -point.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude > .001f) enemy.transform.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            var roll = Random.value;
            var type = elapsed > 28f && roll > .77f ? AstralEnemy.EnemyType.Brute : roll < .31f ? AstralEnemy.EnemyType.Wisp : AstralEnemy.EnemyType.Sprite;
            behavior.ConfigureType(type);
            behavior.weakness = Random.value < .55f ? (Random.value < .5f ? currentRecipeA : currentRecipeB) : rotations[Random.Range(0, rotations.Length)];
            if (forcedBuff != AstralEnemy.BuffType.None) behavior.ApplyBuff(forcedBuff, 10f);
            else if (enemyShielded) behavior.ApplyBuff(AstralEnemy.BuffType.Ward, 2f);
            else behavior.RefreshVisuals();
            EnsureElementAvailable(behavior.weakness, "敌人弱点");
        }

        public void SpawnEnemyForTesting()
        {
            SpawnEnemy();
        }

        private void ChooseRecipe()
        {
            suggestedRecipeIndex = Random.Range(0, ElementPalette.SuggestedRecipeCount);
            var recipe = ElementPalette.GetSuggestedRecipe(suggestedRecipeIndex);
            currentRecipeA = recipe.a;
            currentRecipeB = recipe.b;
            recipeClock = 0f;
            foreach (var orb in elementOrbs) if (orb != null) orb.SetDemand(orb.element == currentRecipeA || orb.element == currentRecipeB);
            EnsureElementAvailable(currentRecipeA, "推荐配方");
            EnsureElementAvailable(currentRecipeB, "推荐配方");
        }

        private void EnsureElementAvailable(ElementKind kind, string reason)
        {
            var orb = elementOrbs.Find(item => item != null && item.element == kind);
            if (orb == null || orb.IsAvailable) return;
            orb.Supply();
            SetStatus("星轨补给 · " + ElementPalette.ChineseName(kind) + "（" + reason + "）", ElementPalette.ColorOf(kind));
        }

        public void RefreshHud()
        {
            if (recipeTitle != null) recipeTitle.text = "目标配方";
            if (recipeDetail != null)
            {
                var suggested = ElementPalette.GetSuggestedRecipe(suggestedRecipeIndex);
                recipeDetail.text = ElementPalette.ChineseName(currentRecipeA) + "  +  " + ElementPalette.ChineseName(currentRecipeB) + "   →   " + suggested.name + "\n" + suggested.role;
            }
            if (scoreLabel != null) scoreLabel.text = "星尘分数  " + score.ToString("0000") + "   击退 " + defeated + "/" + defeatTarget;
            if (coreLabel != null && starCore != null) coreLabel.text = "星核完整度  " + Mathf.CeilToInt(starCore.Health) + "%";
            if (timerLabel != null) timerLabel.text = "星潮倒计时  " + Mathf.CeilToInt(Mathf.Max(0f, timeRemaining)) + " s";
            if (coreFill != null && starCore != null) coreFill.fillAmount = starCore.Health / starCore.maxHealth;
            if (tideFill != null) tideFill.fillAmount = tide / 100f;
            if (recipeTokens != null && recipeTokens.Length >= 2)
            {
                var hasPair = spellReady;
                recipeTokens[0].color = hasFirst || hasPair ? ElementPalette.ColorOf(first) : new Color(.12f, .1f, .18f, .75f);
                recipeTokens[1].color = hasPair ? ElementPalette.ColorOf(second) : new Color(.12f, .1f, .18f, .75f);
            }
            if (selectionLabel != null)
            {
                var firstName = hasFirst || spellReady ? ElementPalette.ChineseName(first) : "待选择";
                var secondName = spellReady ? ElementPalette.ChineseName(second) : "待选择";
                selectionLabel.text = "已选元素  " + firstName + "  +  " + secondName + (spellReady ? "  ·  待释放" : string.Empty);
            }
        }

        public void SetStatus(string message, Color color)
        {
            if (statusLabel == null) return;
            statusLabel.text = message;
            statusLabel.color = color;
        }

        public void EndGame(bool victory)
        {
            if (IsEnded) return;
            IsEnded = true;
            if (resultPanel != null) resultPanel.SetActive(true);
            if (resultTitle != null) resultTitle.text = victory ? "星核守住了！" : "星核已熄灭";
            if (resultStats != null) resultStats.text = victory
                ? "本局星尘分数  " + score + "\n击退敌人  " + defeated + "\n按 R 重新开始"
                : "敌人突破了炼金防线\n本局击退敌人  " + defeated + "\n按 R 再试一次";
            SetStatus(victory ? "胜利结算：星界炼金台稳定运转" : "失败结算：下一局优先完成正确配方", victory ? new Color(1f, .76f, .2f) : new Color(1f, .25f, .32f));
            if (music != null) music.Stop();
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        public void RecenterDesktop()
        {
            var desktop = FindFirstObjectByType<DesktopPlayerController>();
            if (desktop == null) return;
            var start = desktop.GetComponent<DesignPlayerStart>();
            if (start != null) start.ResetToDesignStart(true);
            else
            {
                desktop.transform.position = new Vector3(0f, 0f, -9.45f);
                desktop.transform.rotation = Quaternion.identity;
            }
            SetStatus("已回到设计起点 · 面向炼金台", new Color(.72f, .88f, 1f));
        }
    }
}
