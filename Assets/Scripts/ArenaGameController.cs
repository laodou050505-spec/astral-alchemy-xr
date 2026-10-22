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
        [Tooltip("First round teaches the complete left-hand select / combine / right-hand cast loop before the timer and waves begin.")]
        public bool tutorialMode = true;

        private const string TutorialCompletedKey = "AstralAlchemy.TutorialCompleted";

        [Header("UI")]
        public Text recipeTitle;
        public Text recipeDetail;
        public Text scoreLabel;
        public Text coreLabel;
        public Text timerLabel;
        public Text statusLabel;
        public Text selectionLabel;
        public Image[] recipeTokens;
        [Tooltip("Fire, Frost, Storm, Bloom, Void, Star. Used by the selected-element HUD instead of flat colour blocks.")]
        public Sprite[] elementIcons;
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
        public AudioSource negativeAudio;

        public bool IsEnded { get; private set; }
        public ElementKind CurrentRecipeA => currentRecipeA;
        public ElementKind CurrentRecipeB => currentRecipeB;
        public bool IsSpellReady => spellReady;

        // 震动反馈引用，由 Start() 自动查找。可留空（无 XR 设备时不报错）。
        private PicoControllerInput picoInput;
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
        private TutorialStage tutorialStage;
        private AstralEnemy tutorialTarget;
        private float nextHudRefresh;

        private enum TutorialStage { Off, SelectFirst, SelectSecond, CastSpell }

        private readonly ElementKind[] rotations = { ElementKind.Fire, ElementKind.Frost, ElementKind.Storm, ElementKind.Bloom, ElementKind.Star, ElementKind.Void };

        private void Start()
        {
            timeRemaining = roundDuration;
            elapsed = 0f;
            spawnCooldown = initialEnemyDelay;
            if (resultPanel != null) resultPanel.SetActive(false);
            if (music != null) music.Play();
            picoInput = FindFirstObjectByType<PicoControllerInput>();
            var tutorialCompleted = PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1;
            if (tutorialMode && !tutorialCompleted) BeginTutorial();
            else ChooseRecipe();
            RefreshHud();
        }

        private void Update()
        {
            if (IsEnded) return;
            // The first minute should test mastery, not whether the player could
            // decipher the controls under pressure. The clock and enemy tide start
            // only after the complete interaction loop has been performed once.
            if (tutorialStage != TutorialStage.Off)
            {
                if (Time.unscaledTime >= nextHudRefresh) { RefreshHud(); nextHudRefresh = Time.unscaledTime + .08f; }
                return;
            }
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
            if (Time.unscaledTime >= nextHudRefresh) { RefreshHud(); nextHudRefresh = Time.unscaledTime + .08f; }
        }

        public void ReceiveElement(ElementOrb orb)
        {
            if (IsEnded || orb == null) return;
            if (spellReady)
            {
                SetStatus("Two elements selected — cast “" + preparedSpellName + "” first", new Color(1f, .76f, .22f));
                RefreshHud();
                return;
            }

            if (tutorialStage == TutorialStage.SelectFirst && orb.element != currentRecipeA)
            {
                SetStatus("Tutorial 1/3\nAim left hand ray at\nthe glowing " + ElementPalette.DisplayName(currentRecipeA) + " orb", ElementPalette.ColorOf(currentRecipeA));
                orb.ShowRejected();
                return;
            }
            if (tutorialStage == TutorialStage.SelectSecond && orb.element != currentRecipeB)
            {
                SetStatus("Tutorial 2/3\nFirst element added\nNow select " + ElementPalette.DisplayName(currentRecipeB), ElementPalette.ColorOf(currentRecipeB));
                orb.ShowRejected();
                return;
            }
            orb.PulseToCauldron(cauldron.transform);
            // 元素被坩埚接受 → 左手短促震动确认
            picoInput?.TriggerHaptic(true, false, 0.28f, 0.048f);
            if (!hasFirst)
            {
                first = orb.element;
                hasFirst = true;
                if (tutorialStage == TutorialStage.SelectFirst)
                {
                    tutorialStage = TutorialStage.SelectSecond;
                    SetTutorialDemand(currentRecipeB);
                    SetStatus("Tutorial 2/3\nGood! [Pull Left Trigger]\nto select " + ElementPalette.DisplayName(currentRecipeB), ElementPalette.ColorOf(currentRecipeB));
                }
                else SetStatus("First element added — add another to brew", ElementPalette.ColorOf(first));
                RefreshHud();
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
            RefreshHud();
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
            SetStatus("Brew complete! Aim right · [Hold Trigger] to charge · [Release] to cast “" + preparedSpellName + "”", new Color(1f, .76f, .21f));
            if (positiveAudio != null) positiveAudio.Play();
            // 炼制成功 → 双手中强度震动，提醒立即蓄力施法
            picoInput?.TriggerHaptic(true, true, 0.44f, 0.075f);
            if (tutorialStage == TutorialStage.SelectSecond)
            {
                tutorialStage = TutorialStage.CastSpell;
                SetTutorialDemand(null);
                SpawnTutorialTarget();
                SetStatus("Tutorial 3/3\nAim right · [Hold Trigger] to charge\n[Release] to cast “" + preparedSpellName + "”", new Color(1f, .76f, .21f));
            }
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
            SetStatus(“Miscast: “” + recipe.name + “” — “ + recipe.role, recipe.color);
            if (negativeAudio != null) negativeAudio.Play();
            // 误配 → 双手强震动，比炼制成功更重，提醒玩家注意后果
            picoInput?.TriggerHaptic(true, true, 0.65f, 0.11f);
            StartCoroutine(ApplyMiscastEffect(recipe));
        }

        private IEnumerator ApplyMiscastEffect(ElementPalette.Recipe recipe)
        {
            var enemies = FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None);
            if (recipe.style == "wildfire")
            {
                foreach (var enemy in enemies) { enemy.Hit(ElementKind.Fire, 14f); enemy.ApplyBuff(AstralEnemy.BuffType.Frenzy, 9f); }
                SetStatus("Wildfire Sprouts: Rain of fire — survivors enter Frenzy", recipe.color);
            }
            else if (recipe.style == "backfire" || recipe.style == "overload")
            {
                foreach (var enemy in enemies) enemy.ApplyBuff(recipe.style == "backfire" ? AstralEnemy.BuffType.Overcharge : AstralEnemy.BuffType.Frenzy, 9f);
                if (starCore != null) starCore.Damage(7f);
                SetStatus("Alchemical Backfire: Core -7, enemies gain speed & power", recipe.color);
            }
            else if (recipe.style == "rift")
            {
                activeMiscastShields++;
                enemyShielded = true;
                foreach (var enemy in enemies) enemy.ApplyBuff(AstralEnemy.BuffType.Ward, 12f);
                if (starCore != null) starCore.Damage(7f);
                SetStatus("Darkflame Rift: Enemies gain Void Shield, Core -7", recipe.color);
            }
            else if (recipe.style == "flashfreeze")
            {
                foreach (var enemy in enemies) enemy.Freeze(6.5f);
                if (starCore != null) starCore.Heal(8f);
                SetStatus("Polar Day Storm: All enemies frozen, Core +8", recipe.color);
            }
            else if (recipe.style == "pollen")
            {
                if (starCore != null) starCore.Heal(18f);
                foreach (var enemy in enemies) enemy.ApplyBuff(AstralEnemy.BuffType.Siphon, 10f);
                SetStatus("Meteor Pollen: Core +18, survivors gain Siphon", recipe.color);
            }
            else if (recipe.style == "bubbleburst")
            {
                foreach (var orb in elementOrbs) if (orb != null) orb.Supply();
                SetStatus("Comet Frost Mist: All six element orbs resupplied", recipe.color);
            }
            else if (recipe.style == "vine")
            {
                foreach (var enemy in enemies) enemy.Freeze(3.5f);
                SpawnEnemy(AstralEnemy.BuffType.Ward);
                SetStatus("Stareater Vine: Enemies ensnared, a Shield enemy is summoned", recipe.color);
            }
            else
            {
                foreach (var enemy in enemies) enemy.Hit(ElementKind.Star, 12f);
                SetStatus(recipe.name + ": Entropy wave sweeps the arena", recipe.color);
            }
            yield return new WaitForSeconds(3.2f);
            activeMiscastShields = Mathf.Max(0, activeMiscastShields - 1);
            enemyShielded = activeMiscastShields > 0;
        }

        public void CastAtRay(Ray ray, float charge = .8f)
        {
            if (!spellReady || IsEnded)
            {
                SetStatus("First brew two matching elements in the cauldron", new Color(.78f, .84f, 1f));
                return;
            }
            spellReady = false;
            hasFirst = false;
            var perfect = charge >= .72f && charge <= 1.16f;
            var chargeMultiplier = perfect ? 1.9f : Mathf.Lerp(.7f, 1.45f, Mathf.Clamp01(charge / 1.55f));
            var hitSomething = false;
            var isWeaknessHit = false;
            var impactPoint = ray.origin + ray.direction * 5f;
            var castMessage = “”” + preparedSpellName + “” cast — brew again to prepare the next shot”;
            var castColor = ElementPalette.ColorOf(preparedSpellElement);
            if (Physics.Raycast(ray, out var hit, 60f))
            {
                var enemy = hit.collider.GetComponentInParent<AstralEnemy>();
                if (enemy != null)
                {
                    impactPoint = enemy.transform.position + Vector3.up * 1.05f;
                    enemy.Hit(preparedSpellElement, (preparedRecipe.damage + tide * .18f) * chargeMultiplier);
                    hitSomething = true;
                    isWeaknessHit = preparedSpellElement == enemy.weakness;
                    castMessage = isWeaknessHit
                        ? “Weakness hit! “ + ElementPalette.DisplayName(enemy.weakness) + “ deals amplified damage”
                        : (enemy.IsShielded ? “Shield blocked — damage reduced” : “Hit! Enemy takes damage”);
                    castColor = isWeaknessHit ? new Color(1f, .76f, .2f) : new Color(.78f, .88f, 1f);
                }
            }
            if (!hitSomething)
            {
                var closest = FindClosestEnemy(ray);
                if (closest != null)
                {
                    impactPoint = closest.transform.position + Vector3.up * 1.05f;
                    closest.Hit(preparedSpellElement, (preparedRecipe.damage * .72f + tide * .12f) * chargeMultiplier);
                    hitSomething = true;
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
                castMessage = “Perfect release! “” + preparedSpellName + “” damage boosted”;
                castColor = new Color(1f, .86f, .38f);
            }

            // ── 命中震动反馈（右手，强度分三档）──
            // 弱点命中：强震 + 若完美释放则左手同步震动（双重奖励感）
            // 普通命中：中等震动
            // 未命中：极轻微震动提示手柄已响应
            if (isWeaknessHit)
            {
                picoInput?.TriggerHaptic(false, true, 0.72f, 0.11f);
                if (perfect) picoInput?.TriggerHaptic(true, false, 0.52f, 0.09f);
            }
            else if (hitSomething)
            {
                picoInput?.TriggerHaptic(false, true, 0.42f, 0.07f);
                if (perfect) picoInput?.TriggerHaptic(true, false, 0.34f, 0.07f);
            }
            else
            {
                picoInput?.TriggerHaptic(false, true, 0.16f, 0.038f);
            }

            SetStatus(castMessage, castColor);
            if (tutorialStage == TutorialStage.CastSpell)
            {
                if (hitSomething) CompleteTutorial();
                else
                {
                    spellReady = true;
                    SetStatus("Tutorial 3/3 — missed!\nAim right at the enemy\n[Hold Trigger] · [Release] to cast", new Color(1f, .55f, .2f));
                }
            }
            RefreshHud();
        }

        private void BeginTutorial()
        {
            tutorialStage = TutorialStage.SelectFirst;
            // A fixed, visually distinct opening recipe makes the first interaction
            // teachable. Later recipe rotations remain random and replayable.
            currentRecipeA = ElementKind.Fire;
            currentRecipeB = ElementKind.Frost;
            suggestedRecipeIndex = FindSuggestedRecipe(currentRecipeA, currentRecipeB);
            recipeClock = 0f;
            SetTutorialDemand(currentRecipeA);
            EnsureElementAvailable(currentRecipeA, "Tutorial");
            EnsureElementAvailable(currentRecipeB, "Tutorial");
            SetStatus("Tutorial 1/3\nAim left hand at the red Fire orb\n[Pull Trigger] to add it", ElementPalette.ColorOf(currentRecipeA));
        }

        private int FindSuggestedRecipe(ElementKind a, ElementKind b)
        {
            for (var i = 0; i < ElementPalette.SuggestedRecipeCount; i++)
            {
                var recipe = ElementPalette.GetSuggestedRecipe(i);
                if (ElementPalette.IsPair(a, b, recipe.a, recipe.b)) return i;
            }
            return 0;
        }

        private void SetTutorialDemand(ElementKind? demanded)
        {
            foreach (var orb in elementOrbs)
                if (orb != null) orb.SetDemand(demanded.HasValue && orb.element == demanded.Value);
        }

        private void SpawnTutorialTarget()
        {
            if (tutorialTarget != null) return;
            tutorialTarget = SpawnEnemy();
            if (tutorialTarget == null) return;
            tutorialTarget.name = "TutorialEnemy_AimAtWeaknessAbove";
            tutorialTarget.transform.position = new Vector3(0f, .4f, 4.15f);
            tutorialTarget.ConfigureType(AstralEnemy.EnemyType.Wisp);
            tutorialTarget.health = 28f;
            tutorialTarget.weakness = preparedSpellElement;
            tutorialTarget.SetTutorialHold(true);
            tutorialTarget.RefreshVisuals();
        }

        private void CompleteTutorial()
        {
            tutorialStage = TutorialStage.Off;
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            PlayerPrefs.Save();
            if (tutorialTarget != null) tutorialTarget.SetTutorialHold(false);
            elapsed = 0f;
            spawnCooldown = 3.5f;
            recipeClock = 0f;
            SetTutorialDemand(null);
            SetStatus("Tutorial Complete! The element above an enemy is its weakness — a matching spell deals 2.25× damage", new Color(1f, .8f, .26f));
        }

        private void CreateElementProjectile(Vector3 origin, Vector3 impact, ElementKind primary, ElementKind secondary)
        {
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = "ElementProjectile_" + ElementPalette.DisplayName(primary) + "_" + ElementPalette.DisplayName(secondary);
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

        private AstralEnemy SpawnEnemy(AstralEnemy.BuffType forcedBuff = AstralEnemy.BuffType.None)
        {
            if (enemyPrefab == null || enemySpawnPoints == null || enemySpawnPoints.Length == 0) return null;
            var point = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Length)];
            var enemy = Instantiate(enemyPrefab, point.position, point.rotation);
            enemy.name = "Enemy_" + (defeated + FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None).Length + 1);
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
            EnsureElementAvailable(behavior.weakness, "Enemy Weakness");
            return behavior;
        }

        public void SpawnEnemyForTesting()
        {
            SpawnEnemy();
        }

        public void ForceCompleteTutorialForTesting()
        {
            if (tutorialStage == TutorialStage.Off) return;
            tutorialStage = TutorialStage.Off;
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            PlayerPrefs.Save();
            hasFirst = false;
            spellReady = false;
            SetTutorialDemand(null);
            ChooseRecipe();
        }

        private void ChooseRecipe()
        {
            suggestedRecipeIndex = Random.Range(0, ElementPalette.SuggestedRecipeCount);
            var recipe = ElementPalette.GetSuggestedRecipe(suggestedRecipeIndex);
            currentRecipeA = recipe.a;
            currentRecipeB = recipe.b;
            recipeClock = 0f;
            foreach (var orb in elementOrbs) if (orb != null) orb.SetDemand(orb.element == currentRecipeA || orb.element == currentRecipeB);
            EnsureElementAvailable(currentRecipeA, "Suggested Recipe");
            EnsureElementAvailable(currentRecipeB, "Suggested Recipe");
        }

        private void EnsureElementAvailable(ElementKind kind, string reason)
        {
            var orb = elementOrbs.Find(item => item != null && item.element == kind);
            if (orb == null || orb.IsAvailable) return;
            orb.Supply();
            SetStatus("Orbital Supply: " + ElementPalette.DisplayName(kind) + " (" + reason + ")", ElementPalette.ColorOf(kind));
        }

        public void RefreshHud()
        {
            if (recipeTitle != null) recipeTitle.text = tutorialStage switch
            {
                TutorialStage.SelectFirst => "Tutorial 1/3 · Select Element",
                TutorialStage.SelectSecond => "Tutorial 2/3 · Complete Brew",
                TutorialStage.CastSpell => "Tutorial 3/3 · Aim & Cast",
                _ => "Target Recipe"
            };
            if (recipeDetail != null)
            {
                var suggested = ElementPalette.GetSuggestedRecipe(suggestedRecipeIndex);
                recipeDetail.text = tutorialStage switch
                {
                    TutorialStage.SelectFirst => "Aim left hand at red Fire orb\n[Pull Trigger] to add it",
                    TutorialStage.SelectSecond => "Fire added · Now select Frost\nTwo elements auto-brew",
                    TutorialStage.CastSpell => "Aim right hand at enemy · [Hold Trigger]\n[Release] to cast “" + preparedSpellName + "”",
                    _ => ElementPalette.DisplayName(currentRecipeA) + "  +  " + ElementPalette.DisplayName(currentRecipeB) + "   →   " + suggested.name + "\n" + suggested.role + "\nElement above enemy = Weakness"
                };
            }
            if (scoreLabel != null) scoreLabel.text = "Score  " + score.ToString("0000") + "\n" + defeated + "/" + defeatTarget + " Defeated";
            if (coreLabel != null) coreLabel.text = "Star Core";
            if (timerLabel != null) timerLabel.text = "Star Tide  " + Mathf.CeilToInt(Mathf.Max(0f, timeRemaining)) + "s";
            // The bound StarCoreWorldHealthBar drives the visible fill, damage
            // trail and numeric value every frame. Do not overwrite that feedback
            // here with an abrupt eight-Hz jump.
            if (tideFill != null) tideFill.fillAmount = tide / 100f;
            if (recipeTokens != null && recipeTokens.Length >= 2)
            {
                var hasPair = spellReady;
                SetElementToken(recipeTokens[0], hasFirst || hasPair, first);
                SetElementToken(recipeTokens[1], hasPair, second);
            }
            if (selectionLabel != null)
            {
                var firstName = hasFirst || spellReady ? ElementPalette.DisplayName(first) : "—";
                var secondName = spellReady ? ElementPalette.DisplayName(second) : "—";
                selectionLabel.text = "Selected  " + firstName + "  +  " + secondName;
            }
        }

        private void SetElementToken(Image token, bool selected, ElementKind kind)
        {
            if (token == null) return;
            var index = (int)kind;
            if (selected && elementIcons != null && index >= 0 && index < elementIcons.Length && elementIcons[index] != null)
            {
                token.sprite = elementIcons[index];
                token.color = Color.white;
                token.preserveAspect = true;
                token.enabled = true;
                return;
            }

            // A dim astral sigil communicates an empty slot without falling back to
            // the red/blue square placeholders used by the old prototype HUD.
            var emptyIndex = (int)ElementKind.Star;
            token.sprite = elementIcons != null && emptyIndex < elementIcons.Length ? elementIcons[emptyIndex] : null;
            token.color = new Color(.55f, .6f, .82f, token.sprite != null ? .16f : 0f);
            token.preserveAspect = true;
            token.enabled = token.sprite != null;
        }

        public void SetStatus(string message, Color color)
        {
            if (statusLabel == null) return;
            statusLabel.text = message;
            // Preserve the element hue while lifting dark saturated colors for readable instructions.
            statusLabel.color = Color.Lerp(color, Color.white, .58f);
        }

        public void EndGame(bool victory)
        {
            if (IsEnded) return;
            IsEnded = true;
            if (resultPanel != null) resultPanel.SetActive(true);
            if (recipeTitle != null) recipeTitle.transform.parent.gameObject.SetActive(false);
            if (scoreLabel != null) scoreLabel.transform.parent.gameObject.SetActive(false);
            if (statusLabel != null) statusLabel.transform.parent.gameObject.SetActive(false);
            if (resultTitle != null) resultTitle.text = victory ? "Star Core Defended!" : "Star Core Fallen";
            if (resultStats != null) resultStats.text = victory
                ? "Score  " + score + "\nEnemies Defeated  " + defeated + "\n[Pull Any Trigger] · Play Again"
                : "The alchemy barrier was breached\nEnemies Defeated  " + defeated + "\n[Pull Any Trigger] · Try Again";
            SetStatus(victory ? "Victory: The Astral Alchemy Forge holds steady" : "Defeat: Next round — focus on correct recipes first", victory ? new Color(1f, .76f, .2f) : new Color(1f, .25f, .32f));
            if (music != null) music.Stop();

            // ── 胜负演出 ──
            if (victory)
            {
                // 胜利：双手中强震动 + 三段金色粒子爆发
                picoInput?.TriggerHaptic(true, true, 0.62f, 0.18f);
                StartCoroutine(VictoryBurst());
            }
            else
            {
                // 失败：双手重震动 + 核心光源红色闪烁
                picoInput?.TriggerHaptic(true, true, 0.82f, 0.16f);
                CameraShaker.Shake(0.035f, 0.30f);
                StartCoroutine(DefeatCoreFlash());
            }
        }

        /// <summary>胜利时三段金色粒子爆发。</summary>
        private System.Collections.IEnumerator VictoryBurst()
        {
            if (castBurst == null) yield break;
            for (var i = 0; i < 3; i++)
            {
                castBurst.transform.position = new Vector3(0f, 1.6f, 2f);
                var main = castBurst.main;
                main.startColor = new Color(1f, .88f, .32f);
                main.startSize = new ParticleSystem.MinMaxCurve(.18f, .46f);
                var emission = castBurst.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)90) });
                castBurst.Play(true);
                yield return new WaitForSeconds(0.45f);
            }
        }

        /// <summary>失败时核心灯光红色双闪。</summary>
        private System.Collections.IEnumerator DefeatCoreFlash()
        {
            if (starCore == null || starCore.coreLight == null) yield break;
            for (var i = 0; i < 2; i++)
            {
                starCore.coreLight.color = new Color(1f, .08f, .04f);
                starCore.coreLight.intensity = 3.2f;
                yield return new WaitForSeconds(0.14f);
                starCore.coreLight.color = new Color(.9f, .03f, .08f);
                starCore.coreLight.intensity = 0.45f;
                yield return new WaitForSeconds(0.14f);
            }
        }

        /// <summary>
        /// 星核受伤通知：由 StarCoreController.Damage() 调用，触发震动和相机震动。
        /// </summary>
        public void NotifyCoreDamaged(float amount)
        {
            var amp = Mathf.Clamp(0.30f + amount * 0.020f, 0.30f, 0.85f);
            picoInput?.TriggerHaptic(true, true, amp, 0.12f);
            CameraShaker.Shake(Mathf.Clamp(0.012f + amount * 0.0012f, 0.012f, 0.038f), 0.22f);
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
                desktop.transform.position = new Vector3(0f, 0f, -4.7f);
                desktop.transform.rotation = Quaternion.identity;
            }
            SetStatus("Reset to start position — facing the alchemy table", new Color(.72f, .88f, 1f));
        }
    }
}
