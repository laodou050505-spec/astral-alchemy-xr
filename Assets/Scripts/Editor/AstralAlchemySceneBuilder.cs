using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AstralAlchemy;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public static class AstralAlchemySceneBuilder
{
    private const string ScenePath = "Assets/Scenes/AstralAlchemyArena.unity";
    private const string TitleScenePath = "Assets/Scenes/AstralAlchemyTitle.unity";
    private const string MaterialFolder = "Assets/Materials";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string TitleUiTexturePath = "Assets/Art/UI/GeneratedUI/NanoBanana_Refined/TitlePanel/TitlePanel_Open.png";
    private const string ResultFramePieceRoot = "Assets/Art/UI/GeneratedUI/NanoBanana_Refined/TitlePanel/TitlePanel_Frame_";
    private const string SideUiTexturePath = "Assets/Art/UI/GeneratedUI/NanoBanana_Refined/SidePanelClean/SidePanelClean_Cropped.png";
    private const string StatusUiTexturePath = "Assets/Art/UI/GeneratedUI/NanoBanana_Refined/StatusPanel/StatusPanel_Cropped.png";
    private const string IconFolder = "Assets/Art/UI/GeneratedUI/NanoBanana_Refined/ElementIcons";
    private const string TitleFeatherPath = "Assets/Art/UI/GeneratedUI/TitleRedesign20260908/Background_Feathered.png";
    private const string ChineseFontPath = "Assets/Art/UI/Fonts/ArialUnicode.ttf";
    private static Font chineseFont;
    private static readonly Color Ink = new Color(.025f, .018f, .09f);
    private static readonly Color Indigo = new Color(.055f, .03f, .18f);
    private static readonly Color Violet = new Color(.16f, .045f, .32f);
    private static readonly Color Copper = new Color(.56f, .27f, .11f);
    private static readonly Color CopperBright = new Color(.94f, .52f, .24f);
    private static readonly Color StarGold = new Color(1f, .68f, .15f);
    private static readonly Color Cyan = new Color(.12f, .85f, 1f);
    private static bool pendingPreviewCapture;
    private static bool finalPipelineRequested;

    [MenuItem("Astral Alchemy/Inspect Current User Layout")]
    public static void InspectCurrentUserLayout()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath) throw new Exception("Wrong scene open: " + scene.path);
        Debug.Log("ASTRAL_LAYOUT_BEGIN");
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.scene != scene) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            if (source == null) continue;
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (root != go) continue;
            var path = AssetDatabase.GetAssetPath(source);
            if (!path.Contains("Assets/Art/Models") && !path.Contains("Assets/星界炼金幻想风")) continue;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var boundsText = "none";
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                boundsText = bounds.size.ToString("F3");
            }
            Debug.Log($"ASTRAL_LAYOUT name={root.name} source={path} pos={root.transform.position:F3} rot={root.transform.eulerAngles:F2} scale={root.transform.lossyScale:F4} bounds={boundsText}");
        }
        Debug.Log("ASTRAL_LAYOUT_END");
    }

    [MenuItem("Astral Alchemy/Upgrade Current User Scene _F6")]
    public static void UpgradeCurrentUserScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            throw new Exception("Refusing to modify a different scene. Open " + ScenePath + " first.");

        var camera = Camera.main;
        var game = UnityEngine.Object.FindFirstObjectByType<ArenaGameController>();
        if (camera == null || game == null) throw new Exception("Current user scene is missing Main Camera or ArenaGameController.");

        UpgradeMagicSky(camera);
        UpgradeFloatingProps();
        UpgradeStarCoreFromAstrolabe();
        UpgradeEnemyPresentation();
        UpgradeEnvironmentSway();
        UpgradeDesktopCrosshair(camera);
        UpgradeStarTwinkle();
        RemoveHandHeldModels();
        UpgradeHud(camera, game);
        UpgradeDesignPlayerStart(camera, game);
        UpgradeArenaRuneHeight();
        UpgradeElementTargets();
        UpgradeElementVisuals();
        AddForwardElementOrbVariants(game);
        UpgradeParticleMaterials();

        var previewEnemies = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.scene == scene && go.name == "PreviewOnly_战斗构图敌人");
        if (previewEnemies != null) previewEnemies.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("AstralAlchemy current user scene upgraded without changing user-authored prop positions or rotations.");
    }

    // Batch-only finalization path used when the editor UI is unavailable. It deliberately
    // opens and upgrades the authored scene instead of rebuilding a new demo scene.
    public static void BatchFinalizeCurrentScene()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) throw new Exception("Could not open authored scene: " + ScenePath);
            BuildTitleScene();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureBuildScenes();
            UpgradeCurrentUserScene();
            ValidateDemo();
            finalPipelineRequested = true;
            RunDesktopGameplaySmokeTest();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(2);
        }
    }

    private static void UpgradeArenaRuneHeight()
    {
        // The thin star-map rings were authored at their parent origin, under the deck surface.
        // Raise only procedural deck markings; user-placed props and the arena transform stay intact.
        var raised = 0;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.gameObject.scene.IsValid()) continue;
            if (!t.name.StartsWith("星图圆环_R")) continue;
            var p = t.localPosition;
            if (p.y < .395f) { p.y = .405f; t.localPosition = p; EditorUtility.SetDirty(t); raised++; }
        }
        if (raised > 0) Debug.Log("AstralAlchemy arena rune rings raised above deck: " + raised);
    }

    private static void UpgradeFloatingProps()
    {
        var tokens = new[] { "ec479770", "88ac9992", "eb6df566", "97dec2b6" };
        var animatedRoots = new HashSet<GameObject>();
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.scene.IsValid()) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            if (source == null) continue;
            var path = AssetDatabase.GetAssetPath(source);
            var token = tokens.FirstOrDefault(path.Contains);
            if (string.IsNullOrEmpty(token)) continue;
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (root == null || !animatedRoots.Add(root)) continue;
            var motion = root.GetComponent<AstralFloatRotate>() ?? root.AddComponent<AstralFloatRotate>();
            var prominent = token == "97dec2b6";
            motion.floatAmplitude = prominent ? .06f : .045f;
            motion.floatFrequency = prominent ? .58f : .52f;
            motion.rotationSpeed = prominent ? 8f : 14f;
            motion.rotationAxisSpeed = prominent ? new Vector3(.27f, 1f, .19f) : new Vector3(.32f, 1f, .25f);
            motion.breatheAmount = prominent ? .006f : 0f;
            motion.phaseOffset = animatedRoots.Count * .41f;
            EditorUtility.SetDirty(root);
        }
        Debug.Log("AstralAlchemy floating props upgraded: " + animatedRoots.Count);
    }

    private static GameObject FindPrefabRootByToken(Transform parent, string token)
    {
        if (parent == null) return null;
        foreach (var node in parent.GetComponentsInChildren<Transform>(true))
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(node.gameObject);
            if (source == null || !AssetDatabase.GetAssetPath(source).Contains(token)) continue;
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(node.gameObject);
            if (root != null) return root;
        }
        return null;
    }

    private static Transform FindChildDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (var node in parent.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node;
        return null;
    }

    private static void UpgradeStarCoreFromAstrolabe()
    {
        var root = GameObject.Find("StarCore_可调整星核");
        if (root == null) return;
        var controller = root.GetComponent<StarCoreController>();
        var astrolabe = FindPrefabRootByToken(root.transform, "97dec2b6");
        if (controller == null || astrolabe == null) return;
        astrolabe.name = "星象仪星核_可调整";
        var motion = astrolabe.GetComponent<AstralFloatRotate>() ?? astrolabe.AddComponent<AstralFloatRotate>();
        // Preserve the user's current star-core placement. Motion is captured from this
        // authored position, so future upgrades never snap it back to a generated center.
        motion.floatAmplitude = 0f;
        motion.positionSway = Vector3.zero;
        motion.floatFrequency = .42f;
        motion.rotationSpeed = 0f;
        motion.rotationAxisSpeed = new Vector3(1f, 1f, 0f);
        motion.rotationDegreesPerCycle = 1080f;
        motion.rotationCycleSeconds = 30f;
        motion.breatheAmount = 0f;
        controller.coreAnchor = astrolabe.transform;
        controller.coreRenderer = astrolabe.GetComponentsInChildren<Renderer>(true).FirstOrDefault();
        var oldCore = FindChildDeep(root.transform, "星核发光体"); if (oldCore != null) oldCore.gameObject.SetActive(false);
        var oldOrbit = FindChildDeep(root.transform, "星核旋转星环"); if (oldOrbit != null) oldOrbit.gameObject.SetActive(false);
        var lightNode = FindChildDeep(astrolabe.transform, "星核随动点光_可调整");
        if (lightNode == null) { lightNode = New("星核随动点光_可调整", astrolabe.transform).transform; lightNode.localPosition = new Vector3(0f, .9f, -.18f); }
        var point = lightNode.GetComponent<Light>();
        if (point == null) point = lightNode.gameObject.AddComponent<Light>();
        // Preserve the authored adjustable light values; only guarantee it remains attached to the star core.
        point.type = LightType.Point;
        controller.coreLight = point;
        // The previous small bar above the spinning model competed with the single
        // authoritative top HUD bar and was difficult to read in PICO. Remove it;
        // UpgradeHud binds the real health value to the large top bar instead.
        var oldBar = FindChildDeep(astrolabe.transform, "星核血条_可调整");
        if (oldBar != null) UnityEngine.Object.DestroyImmediate(oldBar.gameObject);
        EditorUtility.SetDirty(root); EditorUtility.SetDirty(astrolabe);
        Debug.Log("AstralAlchemy astrolabe promoted to the only visible star core.");
    }

    private static void UpgradeWorldHealthBarVisuals(Transform barNode, StarCoreWorldHealthBar health)
    {
        if (barNode == null || health == null || health.fill == null) return;
        var fillParent = health.fill.transform.parent;
        var trailNode = fillParent.Find("星核损失滞后条");
        if (trailNode == null)
        {
            var trailGo = NewUi("星核损失滞后条", fillParent);
            var trailRect = trailGo.GetComponent<RectTransform>();
            trailRect.anchorMin = Vector2.zero; trailRect.anchorMax = Vector2.one;
            trailRect.offsetMin = new Vector2(4f, 4f); trailRect.offsetMax = new Vector2(-4f, -4f);
            var trail = trailGo.AddComponent<Image>();
            trail.color = new Color(1f, .67f, .12f, .95f);
            trail.type = Image.Type.Filled; trail.fillMethod = Image.FillMethod.Horizontal; trail.fillAmount = 1f;
            trailNode = trailGo.transform;
        }
        trailNode.SetSiblingIndex(Mathf.Max(0, health.fill.transform.GetSiblingIndex()));
        health.fill.transform.SetAsLastSibling();
        health.damageTrail = trailNode.GetComponent<Image>();

        var valueNode = barNode.Find("星核血量数值");
        var value = valueNode != null ? valueNode.GetComponent<Text>() : null;
        if (value == null)
        {
            value = Text("100 / 100", barNode, new Vector2(180f, 28f), new Vector2(0f, 0f), 20, Color.white, TextAnchor.MiddleCenter);
            value.name = "星核血量数值";
        }
        value.rectTransform.anchoredPosition = Vector2.zero;
        value.rectTransform.sizeDelta = new Vector2(180f, 28f);
        value.fontSize = 20;
        value.raycastTarget = false;
        health.valueLabel = value;
        EditorUtility.SetDirty(health);
    }

    private static void UpgradeEnemyPresentation()
    {
        const string enemyPath = "Assets/Prefabs/Enemy_Astral.prefab";
        BuildEnemyPrefab(CreateMaterials());
        if (AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath) == null) return;
        var contents = PrefabUtility.LoadPrefabContents(enemyPath);
        try
        {
            var enemy = contents.GetComponent<AstralEnemy>();
            var disc = FindChildDeep(contents.transform, "脚下弱点标记");
            if (disc != null) disc.gameObject.SetActive(false);
            var ring = FindChildDeep(contents.transform, "敌人状态光环");
            if (ring != null)
            {
                var rotate = ring.GetComponent<ContinuousLocalRotate>() ?? ring.gameObject.AddComponent<ContinuousLocalRotate>();
                rotate.axis = Vector3.up; rotate.degreesPerSecond = 0f;
                var segments = ring.Cast<Transform>().ToArray();
                for (var i = 0; i < segments.Length; i++) if (i % 2 == 1) UnityEngine.Object.DestroyImmediate(segments[i].gameObject);
                ring.localPosition = new Vector3(0f, .16f, 0f);
                ring.gameObject.SetActive(true);
                if (enemy != null) enemy.footRing = ring;
            }
            var independentFx = FindChildDeep(contents.transform, "敌人独立状态反馈");
            if (enemy != null && independentFx != null) enemy.independentStatusFx = independentFx;
            var hit = FindChildDeep(contents.transform, "敌人受击星爆");
            if (hit == null)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/M_SoftMagicParticle.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_AstralCyan.mat");
                hit = CreateParticles("敌人受击星爆", contents.transform, new Vector3(0f, 1.05f, 0f), mat, 62, 2.8f, .16f, .95f).transform;
                hit.GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (enemy != null)
            {
                enemy.hitBurst = hit.GetComponent<ParticleSystem>();
                enemy.tintRenderers = enemy.tintRenderers.Where(r => r != null && r.gameObject.activeSelf).ToArray();
            }
            var headOrb = FindChildDeep(contents.transform, "头顶弱点徽记");
            if (headOrb != null)
            {
                var bob = headOrb.GetComponent<BaseSway>() ?? headOrb.gameObject.AddComponent<BaseSway>();
                bob.height = .035f; bob.frequency = .64f; bob.pitch = .8f; bob.yaw = 1.2f; bob.phase = .83f;
            }
            var crown = FindChildDeep(contents.transform, "头顶弱点六芒环");
            if (crown != null)
            {
                var bob = crown.GetComponent<BaseSway>() ?? crown.gameObject.AddComponent<BaseSway>();
                bob.height = .022f; bob.frequency = .48f; bob.pitch = .45f; bob.yaw = 1.05f; bob.phase = 2.34f;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, enemyPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        Debug.Log("AstralAlchemy enemy presentation upgraded: tail-down model, rotating ring and stronger hit burst.");
    }

    private static void AlignVisualBottom(Transform model, float localGround)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0 || model.parent == null) return;
        Physics.SyncTransforms();
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        var bottom = model.parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        model.localPosition += Vector3.up * (localGround - bottom.y);
    }

    private static void UpgradeEnvironmentSway()
    {
        var environment = GameObject.Find("EnvironmentProps_可调整环境道具")?.transform;
        if (environment == null) return;
        var candidates = new List<Transform>();
        foreach (var node in environment.GetComponentsInChildren<Transform>(true))
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(node.gameObject);
            if (source == null) continue;
            var path = AssetDatabase.GetAssetPath(source);
            if (path.Contains("ed01b67b") || path.Contains("ff7abdef") || path.Contains("b70e1838") || path.Contains("8ce8b4a5"))
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(node.gameObject);
                if (root != null && !candidates.Contains(root.transform)) candidates.Add(root.transform);
            }
        }
        for (var i = 0; i < candidates.Count; i++)
        {
            var sway = candidates[i].GetComponent<BaseSway>() ?? candidates[i].gameObject.AddComponent<BaseSway>();
            sway.height = .055f; sway.frequency = .34f + i * .055f; sway.pitch = 1.1f; sway.yaw = 1.65f; sway.phase = i * 1.57f;
            EditorUtility.SetDirty(candidates[i]);
        }
        Debug.Log("AstralAlchemy environment props upgraded with staggered base sway: " + candidates.Count);
    }

    private static void UpgradeDesktopCrosshair(Camera camera)
    {
        var old = GameObject.Find("桌面准星世界标记");
        if (old != null) { var renderer = old.GetComponent<Renderer>(); if (renderer != null) renderer.enabled = false; }
        var root = GameObject.Find("桌面十字准星_可调整");
        if (root != null) return;
        root = New("桌面十字准星_可调整", camera.transform);
        root.transform.localPosition = new Vector3(0f, 0f, 1.68f);
        var gold = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_StarGold.mat");
        var cyan = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_AstralCyan.mat");
        foreach (var position in new[] { new Vector3(0f, .052f, 0f), new Vector3(0f, -.052f, 0f), new Vector3(.052f, 0f, 0f), new Vector3(-.052f, 0f, 0f) })
        {
            var arm = Primitive("十字准星臂", PrimitiveType.Cube, root.transform, position, Mathf.Abs(position.x) > 0f ? new Vector3(.04f, .012f, .004f) : new Vector3(.012f, .04f, .004f), gold);
            UnityEngine.Object.DestroyImmediate(arm.GetComponent<Collider>());
        }
        var center = Primitive("十字准星中心", PrimitiveType.Cube, root.transform, Vector3.zero, Vector3.one * .019f, cyan);
        center.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        UnityEngine.Object.DestroyImmediate(center.GetComponent<Collider>());
    }

    private static void UpgradeStarTwinkle()
    {
        var stars = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(go => go.scene.IsValid() && go.name.StartsWith("星点_")).OrderBy(go => go.name).ToArray();
        for (var i = 0; i < stars.Length; i++)
        {
            if (i % 3 != 0) continue;
            var twinkle = stars[i].GetComponent<StarTwinkle>() ?? stars[i].AddComponent<StarTwinkle>();
            twinkle.starRenderer = stars[i].GetComponent<Renderer>(); twinkle.color = i % 3 == 0 ? StarGold : Cyan;
            twinkle.minIntensity = .04f; twinkle.maxIntensity = 6.4f; twinkle.speed = .32f + (i % 9) * .16f; twinkle.phase = i * 1.741f; twinkle.scalePulse = .45f;
        }
        RenderSettings.ambientSkyColor = new Color(.075f, .06f, .16f); RenderSettings.ambientEquatorColor = new Color(.052f, .022f, .1f); RenderSettings.ambientGroundColor = new Color(.012f, .007f, .026f); RenderSettings.fogDensity = .0054f;
        var key = GameObject.Find("星光主光")?.GetComponent<Light>(); if (key != null) key.intensity = .82f;
        var fill = GameObject.Find("星空轮廓光")?.GetComponent<Light>(); if (fill != null) fill.intensity = .36f;
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_MagicNightSky.mat"); if (sky != null) { sky.SetFloat("_NebulaStrength", .29f); EditorUtility.SetDirty(sky); }
        Debug.Log("AstralAlchemy magic sky brightened with staggered star twinkle.");
    }

    private static void UpgradeMagicSky(Camera camera)
    {
        const string skyMaterialPath = "Assets/Materials/M_MagicNightSky.mat";
        var shader = Shader.Find("AstralAlchemy/MagicNightSky");
        if (shader == null) throw new Exception("Magic night sky shader has not imported yet.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(skyMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, skyMaterialPath);
        }
        else material.shader = shader;
        material.SetColor("_ZenithColor", new Color(.008f, .004f, .045f));
        material.SetColor("_HorizonColor", new Color(.105f, .025f, .18f));
        material.SetColor("_StarColor", new Color(1f, .72f, .28f));
        material.SetColor("_NebulaColor", new Color(.34f, .06f, .52f));
        material.SetFloat("_StarDensity", .9875f);
        material.SetFloat("_NebulaStrength", .24f);
        EditorUtility.SetDirty(material);

        var environment = GameObject.Find("EnvironmentProps_可调整环境道具")?.transform;
        if (environment == null) throw new Exception("EnvironmentProps root missing.");
        var sky = GameObject.Find("魔法夜空_可调整");
        if (sky == null)
        {
            sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sky.name = "魔法夜空_可调整";
            sky.transform.SetParent(environment, false);
        }
        sky.transform.localPosition = new Vector3(0f, 10f, 5f);
        sky.transform.localRotation = Quaternion.identity;
        sky.transform.localScale = Vector3.one * 72f;
        var skyCollider = sky.GetComponent<Collider>();
        if (skyCollider != null) UnityEngine.Object.DestroyImmediate(skyCollider);
        var skyRenderer = sky.GetComponent<Renderer>();
        skyRenderer.sharedMaterial = material;
        skyRenderer.shadowCastingMode = ShadowCastingMode.Off;
        skyRenderer.receiveShadows = false;
        skyRenderer.lightProbeUsage = LightProbeUsage.Off;
        skyRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.008f, .004f, .035f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.09f, .075f, .2f);
        RenderSettings.ambientEquatorColor = new Color(.065f, .025f, .12f);
        RenderSettings.ambientGroundColor = new Color(.012f, .008f, .03f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = .006f;
        RenderSettings.fogColor = new Color(.016f, .008f, .055f);
    }

    private static void UpgradeHud(Camera camera, ArenaGameController game)
    {
        var canvas = game.recipeTitle != null ? game.recipeTitle.GetComponentInParent<Canvas>() : null;
        if (canvas == null) throw new Exception("Gameplay HUD canvas missing.");
        canvas.name = "主战斗信息牌_世界空间_深靛星金";
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        // Desktop framing includes the raised HUD. XR supplies its own tracked projection.
        camera.fieldOfView = 64f;
        camera.transform.localRotation = Quaternion.identity;
        canvas.sortingOrder = 50;
        canvas.overrideSorting = true;
        // Core recipe, star-core and end-state information belongs beside the physical
        // arena. It must be walk-around world content, never a camera-attached screen.
        var follower = canvas.GetComponent<CameraSpaceHudFollower>();
        if (follower != null) UnityEngine.Object.DestroyImmediate(follower);
        var worldUi = GameObject.Find("WorldSpaceUI_舒适视区界面")?.transform;
        if (worldUi != null && canvas.transform.parent != worldUi)
            canvas.transform.SetParent(worldUi, false);
        // Place the complete world-space HUD on the rear upper arc of the arena.
        // The canvas remains a real spatial object: players can walk around it,
        // while the raised position keeps the cauldron and star-core unobstructed.
        canvas.transform.localPosition = new Vector3(0f, 4.65f, 2.1f);
        // The canvas front faces the design start on -Z.  Keeping its authored
        // front (Y=0) avoids mirrored Chinese text in the PICO compositor.
        canvas.transform.localRotation = Quaternion.identity;
        canvas.transform.localScale = Vector3.one * .0057f;
        var canvasRect = canvas.GetComponent<RectTransform>();

        // Resolve from the serialized gameplay references first. Earlier authored
        // scenes used slightly different localized node names, while the bindings
        // themselves remain authoritative and survive user layout edits.
        var leftPanel = game.recipeTitle != null ? game.recipeTitle.transform.parent : canvas.transform.Find("左侧配方仪");
        var rightPanel = game.scoreLabel != null ? game.scoreLabel.transform.parent : canvas.transform.Find("右侧星核仪");
        var statusPanel = game.statusLabel != null ? game.statusLabel.transform.parent : canvas.transform.Find("中央状态提示");
        var resultPanel = game.resultPanel != null ? game.resultPanel.transform : canvas.transform.Find("胜败结算面板");
        if (resultPanel != null)
        {
            foreach (var staleBackdrop in resultPanel.GetComponentsInChildren<Image>(true)
                .Where(image => image.transform != resultPanel && image.name == "结算文字底色遮罩").ToArray())
                UnityEngine.Object.DestroyImmediate(staleBackdrop.gameObject);
        }
        if (rightPanel != null)
        {
            foreach (var staleHealth in rightPanel.GetComponents<StarCoreWorldHealthBar>())
                UnityEngine.Object.DestroyImmediate(staleHealth);
            foreach (var staleValue in rightPanel.GetComponentsInChildren<Text>(true).Where(text => text.name == "星核血量数值").ToArray())
                UnityEngine.Object.DestroyImmediate(staleValue.gameObject);
            foreach (var staleTrail in rightPanel.GetComponentsInChildren<Image>(true).Where(image => image.name == "星核损失滞后条").ToArray())
                UnityEngine.Object.DestroyImmediate(staleTrail.gameObject);
        }
        RestylePanel(leftPanel, new Vector2(380f, 380f), new Vector2(-500f, 42f));
        RestylePanel(rightPanel, new Vector2(380f, 380f), new Vector2(500f, 42f));
        RestylePanel(statusPanel, new Vector2(600f, 140f), new Vector2(0f, 45f));
        ArrangeWraparoundHud(canvas, leftPanel, rightPanel, statusPanel);
        var healthPanel = canvas.transform.Find("星核血量条_顶部");
        if (healthPanel == null || healthPanel == rightPanel)
        {
            var newHealth = Panel("星核血量条_顶部", canvas.transform, new Vector2(640f, 119f), new Vector2(0f, 270f), Color.white, LoadUiSprite(StatusUiTexturePath));
            healthPanel = newHealth.transform;
        }
        if (game.coreLabel != null && game.coreLabel.transform.parent != healthPanel)
            game.coreLabel.transform.SetParent(healthPanel, false);
        var coreBarRoot = game.coreFill != null ? game.coreFill.transform.parent : null;
        if (coreBarRoot != null && coreBarRoot.parent != healthPanel)
            coreBarRoot.SetParent(healthPanel, false);
        RestylePanel(healthPanel, new Vector2(640f, 119f), new Vector2(0f, 206f));
        ApplyGeneratedPanelArt(healthPanel, LoadUiSprite(StatusUiTexturePath), .96f);
        if (game.coreLabel != null) SetTextLayout(game.coreLabel, new Vector2(560f, 30f), new Vector2(0f, 28f), 23, 20, 25);
        SetRect(game.coreFill != null ? game.coreFill.transform.parent : null, new Vector2(540f, 25f), new Vector2(0f, -16f));
        RestylePanel(resultPanel, new Vector2(760f, 263f), Vector2.zero);
        ApplyGeneratedPanelArt(leftPanel, LoadUiSprite(SideUiTexturePath), .96f);
        ApplyGeneratedPanelArt(rightPanel, LoadUiSprite(SideUiTexturePath), .96f);
        ApplyGeneratedPanelArt(statusPanel, LoadUiSprite(StatusUiTexturePath), .96f);
        // Give the instruction strip actual interior height while retaining its
        // authored border thickness: slice the frame instead of stretching it.
        var statusImage = statusPanel.GetComponent<Image>();
        var statusImporter = AssetImporter.GetAtPath(StatusUiTexturePath) as TextureImporter;
        if (statusImporter != null && statusImporter.spriteBorder == Vector4.zero)
        {
            statusImporter.spriteBorder = new Vector4(42f, 18f, 42f, 18f);
            statusImporter.SaveAndReimport();
        }
        statusImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StatusUiTexturePath);
        statusImage.type = Image.Type.Sliced;
        statusImage.preserveAspect = false;
        ApplyGeneratedPanelArt(resultPanel, LoadUiSprite(TitleUiTexturePath), .97f);
        BuildOpenResultFrame(resultPanel);

        SetTextLayout(game.recipeTitle, new Vector2(306f, 48f), new Vector2(0f, 113f), 24, 22, 25);
        SetTextLayout(game.recipeDetail, new Vector2(306f, 86f), new Vector2(0f, 46f), 23, 21, 24);
        SetTextLayout(game.timerLabel, new Vector2(310f, 46f), new Vector2(0f, 91f), 24, 22, 25);
        SetRect(game.tideFill != null ? game.tideFill.transform.parent : null, new Vector2(260f, 20f), new Vector2(0f, 38f));
        SetTextLayout(game.scoreLabel, new Vector2(310f, 70f), new Vector2(0f, -20f), 23, 21, 24);

        if (game.recipeTitle != null)
        {
            game.recipeTitle.text = "Target Recipe";
            game.recipeTitle.fontSize = 25;
        }
        if (game.recipeDetail != null)
        {
            game.recipeDetail.fontSize = 23;
            game.recipeDetail.resizeTextForBestFit = true;
            game.recipeDetail.resizeTextMinSize = 21;
            game.recipeDetail.resizeTextMaxSize = 24;
        }
        var selected = leftPanel != null ? leftPanel.Find("已选元素文字")?.GetComponent<Text>() : null;
        if (selected == null && leftPanel != null)
        {
            selected = Text("Selected Element", leftPanel, new Vector2(278f, 42f), new Vector2(0f, -48f), 19, new Color(.88f, .92f, 1f), TextAnchor.MiddleCenter);
            selected.name = "SelectedElementLabel";
        }
        if (selected != null)
        {
            SetTextLayout(selected, new Vector2(306f, 50f), new Vector2(0f, -46f), 21, 20, 22);
            selected.resizeTextForBestFit = true;
            selected.resizeTextMinSize = 20;
            selected.resizeTextMaxSize = 22;
        }
        var tokenA = leftPanel != null ? leftPanel.Find("元素令牌A") : null;
        var tokenB = leftPanel != null ? leftPanel.Find("元素令牌B") : null;
        SetRect(tokenA, new Vector2(60f, 60f), new Vector2(-66f, -116f));
        SetRect(tokenB, new Vector2(60f, 60f), new Vector2(66f, -116f));
        var plus = leftPanel != null ? leftPanel.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.text == "+") : null;
        SetTextLayout(plus, new Vector2(54f, 46f), new Vector2(0f, -116f), 30, 27, 31);
        game.selectionLabel = selected;
        game.elementIcons = LoadElementIcons();

        if (healthPanel != null && game.starCore != null && game.coreFill != null)
        {
            var healthFeedback = healthPanel.GetComponent<StarCoreWorldHealthBar>() ?? healthPanel.gameObject.AddComponent<StarCoreWorldHealthBar>();
            healthFeedback.coreTarget = null;
            healthFeedback.core = game.starCore;
            healthFeedback.fill = game.coreFill;
            healthFeedback.stayUprightToCamera = false;
            healthFeedback.valuePrefix = "";
            UpgradeWorldHealthBarVisuals(healthPanel, healthFeedback);
            if (healthFeedback.valueLabel != null)
            {
                SetTextLayout(healthFeedback.valueLabel, new Vector2(180f, 25f), new Vector2(0f, -16f), 18, 17, 19);
                healthFeedback.valueLabel.color = Color.white;
            }
        }
        // The result board is intentionally open: the illustration supplies only
        // the astral frame and star field.  Give the title and the three-line
        // summary independent reading zones instead of stacking them over a
        // translucent inner card.
        SetTextLayout(game.resultTitle, new Vector2(610f, 52f), new Vector2(0f, 88f), 38, 34, 40);
        SetTextLayout(game.resultStats, new Vector2(560f, 126f), new Vector2(0f, -18f), 24, 22, 25);
        if (game.resultTitle != null) game.resultTitle.lineSpacing = 1f;
        if (game.resultStats != null) game.resultStats.lineSpacing = 1.22f;
        foreach (var text in canvas.GetComponentsInChildren<Text>(true))
        {
            text.font = GetChineseFont();
            // The generated frames carry the visual weight; keep copy lighter
            // and high-contrast so Chinese glyphs remain legible in XR.
            text.fontStyle = FontStyle.Normal;
            text.color = text == game.recipeTitle || text == game.scoreLabel || text == game.resultTitle ? StarGold : new Color(.93f, .94f, 1f);
            var outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.005f, .002f, .02f, .96f);
            outline.effectDistance = new Vector2(.85f, -.85f);
        }
        if (game.statusLabel != null) game.statusLabel.color = new Color(.78f, .9f, 1f);
        if (game.statusLabel != null)
        {
            SetTextLayout(game.statusLabel, new Vector2(500f, 96f), new Vector2(0f, 14f), 22, 21, 22);
            game.statusLabel.fontSize = 22;
            game.statusLabel.resizeTextForBestFit = true;
            game.statusLabel.resizeTextMinSize = 21;
            game.statusLabel.resizeTextMaxSize = 22;
            game.statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            game.statusLabel.verticalOverflow = VerticalWrapMode.Truncate;
        }
        AddHudCornerOrnaments(leftPanel);
        AddHudCornerOrnaments(rightPanel);
        AddHudCornerOrnaments(resultPanel);
        AddHudTitleRule(leftPanel, -1f);
        AddHudTitleRule(rightPanel, 1f);
        Canvas.ForceUpdateCanvases();
    }

    private static void ArrangeWraparoundHud(Canvas canvas, params Transform[] panels)
    {
        var start = UnityEngine.Object.FindFirstObjectByType<DesignPlayerStart>();
        if (start == null) throw new Exception("Wraparound HUD requires the authored player start.");
        var eye = canvas.transform.InverseTransformPoint(start.designEyePosition);
        var radius = -eye.z;
        foreach (var panel in panels)
        {
            if (panel == null) continue;
            var position = panel.localPosition;
            var horizontalOffset = position.x - eye.x;
            if (radius <= Mathf.Abs(horizontalOffset))
                throw new Exception("HUD panel is outside the player-centered layout arc: " + panel.name);
            // Keep the approved height and lateral spacing, but put the boards
            // on a horizontal arc around the opening player position.
            position.z = eye.z + Mathf.Sqrt(radius * radius - horizontalOffset * horizontalOffset);
            panel.localPosition = position;
            // UI's readable front is local -Z. Face it toward the player by
            // yawing only; no roll, pitch, or runtime camera following.
            var awayFromEye = new Vector3(horizontalOffset, 0f, position.z - eye.z);
            panel.localRotation = Quaternion.LookRotation(awayFromEye, Vector3.up);
            Debug.Log($"ASTRAL_WRAPAROUND {panel.name} world={panel.position:F3} rotation={panel.localEulerAngles:F2}");
        }
    }

    public static void ApplyWraparoundHudToCurrentScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var game = UnityEngine.Object.FindFirstObjectByType<ArenaGameController>();
        if (game == null || game.recipeTitle == null || game.scoreLabel == null || game.statusLabel == null)
            throw new Exception("Gameplay HUD bindings are missing.");
        var canvas = game.recipeTitle.GetComponentInParent<Canvas>();
        ArrangeWraparoundHud(canvas, game.recipeTitle.transform.parent, game.scoreLabel.transform.parent, game.statusLabel.transform.parent);
        AddHudTitleRule(game.recipeTitle.transform.parent, -1f);
        AddHudTitleRule(game.scoreLabel.transform.parent, 1f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        ValidateDemo();
    }

    private static void UpgradeDesignPlayerStart(Camera camera, ArenaGameController game)
    {
        var rig = GameObject.Find("PlayerRig_桌面与PICO共用");
        if (rig == null || camera == null) return;
        var start = rig.GetComponent<DesignPlayerStart>() ?? rig.AddComponent<DesignPlayerStart>();
        start.eyeCamera = camera;
        // Authoritative gameplay opening pose. Both desktop and PICO tracking
        // derive from this same near cauldron composition.
        start.designEyePosition = new Vector3(0f, 2.65f, -4.7f);
        start.designYaw = 0f;
        start.firstLandmark = game.cauldron != null ? game.cauldron.transform : GameObject.Find("Cauldron_可调整坩埚")?.transform;
        var desktop = rig.GetComponent<DesktopPlayerController>();
        if (desktop != null) desktop.designStart = start;
        rig.transform.position = new Vector3(0f, 0f, -4.7f);
        rig.transform.rotation = Quaternion.identity;
        camera.transform.localPosition = new Vector3(0f, 2.65f, 0f);
        camera.transform.localRotation = Quaternion.identity;
        EditorUtility.SetDirty(start);
        EditorUtility.SetDirty(rig);
    }

    private static void ApplyGeneratedPanelArt(Transform panel, Sprite sprite, float opacity = 1f)
    {
        if (panel == null) return;
        if (sprite == null) return;
        var image = panel.GetComponent<Image>();
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = new Color(1f, 1f, 1f, opacity);
        var outline = panel.GetComponent<Outline>();
        // Nano Banana art has its own star-gold edge. A generated Unity outline
        // creates the black rectangle visible in the user's screenshots.
        if (outline != null) UnityEngine.Object.DestroyImmediate(outline);
    }

    private static void BuildOpenResultFrame(Transform panel)
    {
        if (panel == null) return;
        var rootImage = panel.GetComponent<Image>();
        if (rootImage != null) UnityEngine.Object.DestroyImmediate(rootImage);

        // Render only the four opaque perimeter bands. No graphic covers the
        // middle of the frame, so the result text sits directly over the world.
        const float sourceWidth = 1367f;
        const float sourceHeight = 459f;
        const float innerLeft = 299f;
        const float innerRight = 1068f;
        const float innerTop = 64f;
        const float innerBottom = 365f;
        var panelSize = panel.GetComponent<RectTransform>().sizeDelta;
        var leftWidth = panelSize.x * innerLeft / sourceWidth;
        var rightWidth = panelSize.x * (sourceWidth - innerRight) / sourceWidth;
        var topHeight = panelSize.y * innerTop / sourceHeight;
        var bottomHeight = panelSize.y * (sourceHeight - innerBottom) / sourceHeight;
        var middleHeight = panelSize.y - topHeight - bottomHeight;

        ConfigureResultFramePiece(panel, "结算框_上边", ResultFramePieceRoot + "Top.png",
            new Vector2(panelSize.x, topHeight), new Vector2(0f, (panelSize.y - topHeight) * .5f));
        ConfigureResultFramePiece(panel, "结算框_下边", ResultFramePieceRoot + "Bottom.png",
            new Vector2(panelSize.x, bottomHeight), new Vector2(0f, -(panelSize.y - bottomHeight) * .5f));
        ConfigureResultFramePiece(panel, "结算框_左边", ResultFramePieceRoot + "Left.png",
            new Vector2(leftWidth, middleHeight), new Vector2(-(panelSize.x - leftWidth) * .5f, (bottomHeight - topHeight) * .5f));
        ConfigureResultFramePiece(panel, "结算框_右边", ResultFramePieceRoot + "Right.png",
            new Vector2(rightWidth, middleHeight), new Vector2((panelSize.x - rightWidth) * .5f, (bottomHeight - topHeight) * .5f));
    }

    private static void ConfigureResultFramePiece(Transform panel, string name, string spritePath, Vector2 size, Vector2 position)
    {
        var existing = panel.Find(name);
        var piece = existing != null ? existing.gameObject : NewUi(name, panel);
        piece.layer = panel.gameObject.layer;
        piece.transform.localPosition = Vector3.zero;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = Vector3.one;
        var rect = piece.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.SetSiblingIndex(0);
        var raw = piece.GetComponent<RawImage>();
        if (raw != null) UnityEngine.Object.DestroyImmediate(raw);
        var image = piece.GetComponent<Image>() ?? piece.AddComponent<Image>();
        image.sprite = LoadUiSprite(spritePath);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = new Color(1f, 1f, 1f, .97f);
        image.raycastTarget = false;
    }

    private static void SetRect(Transform target, Vector2 size, Vector2 position)
    {
        if (target == null) return;
        var rect = target.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void SetTextLayout(Text label, Vector2 size, Vector2 position, int fontSize, int minSize, int maxSize)
    {
        if (label == null) return;
        SetRect(label.transform, size, position);
        label.fontSize = fontSize;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = minSize;
        label.resizeTextMaxSize = maxSize;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.lineSpacing = 1.05f;
    }

    private static void AddHudTitleRule(Transform panel, float direction)
    {
        if (panel == null) return;
        var existing = panel.Find("星金标题分隔线");
        var line = existing != null ? existing.gameObject : NewUi("星金标题分隔线", panel);
        var rect = line.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(176f, 2f);
        // Use the gutters between full content bounds, including wrapped text.
        // Left: recipe bottom 3, selection top -21; right: tide bottom 28,
        // score top 15. Keep the rules fully inside these reserved gutters.
        rect.anchoredPosition = new Vector2(0f, direction < 0f ? -9f : 21.5f);
        var image = line.GetComponent<Image>() ?? line.AddComponent<Image>();
        image.color = new Color(1f, .55f, .18f, .72f);
        image.raycastTarget = false;
    }

    private static void ClampHudToCanvas(Transform panel, float halfCanvasWidth, float margin)
    {
        if (panel == null) return;
        var rect = panel.GetComponent<RectTransform>();
        var halfWidth = rect.sizeDelta.x * .5f;
        var direction = Mathf.Sign(rect.anchoredPosition.x);
        rect.anchoredPosition = new Vector2(direction * (halfCanvasWidth - halfWidth - margin), rect.anchoredPosition.y);
    }

    private static void RestylePanel(Transform panel, Vector2 size, Vector2 position)
    {
        if (panel == null) return;
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var image = panel.GetComponent<Image>();
        if (image != null) image.color = new Color(.018f, .01f, .075f, .9f);
        var outline = panel.GetComponent<Outline>();
        if (outline != null) UnityEngine.Object.DestroyImmediate(outline);
    }

    private static void RestylePanelPreservingLayout(Transform panel)
    {
        if (panel == null) return;
        var image = panel.GetComponent<Image>();
        if (image != null) image.color = new Color(.018f, .01f, .075f, .9f);
        var outline = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.88f, .44f, .15f, .96f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private static void AddHudCornerOrnaments(Transform panel)
    {
        if (panel == null || panel.Find("星金角饰组") != null) return;
        var group = NewUi("星金角饰组", panel);
        var rect = group.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        var corners = new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) };
        foreach (var corner in corners)
        {
            var rune = NewUi("星金符角", group.transform);
            var runeRect = rune.GetComponent<RectTransform>();
            runeRect.anchorMin = runeRect.anchorMax = new Vector2((corner.x + 1f) * .5f, (corner.y + 1f) * .5f);
            runeRect.sizeDelta = new Vector2(28f, 6f);
            runeRect.anchoredPosition = new Vector2(-corner.x * 8f, -corner.y * 8f);
            runeRect.localRotation = Quaternion.Euler(0f, 0f, corner.x * corner.y * 45f);
            rune.AddComponent<Image>().color = new Color(1f, .64f, .22f, .92f);
        }
        group.transform.SetAsFirstSibling();
    }

    private static void UpgradeElementTargets()
    {
        foreach (var orb in UnityEngine.Object.FindObjectsByType<ElementOrb>(FindObjectsSortMode.None))
        {
            var collider = orb.GetComponent<SphereCollider>();
            if (collider == null) collider = orb.gameObject.AddComponent<SphereCollider>();
            collider.radius = .92f;
            EditorUtility.SetDirty(orb);
        }
    }

    private static void UpgradeElementVisuals()
    {
        var count = 0;
        foreach (var orb in UnityEngine.Object.FindObjectsByType<ElementOrb>(FindObjectsSortMode.None))
        {
            var fx = orb.GetComponent<ElementOrbVisualFX>() ?? orb.gameObject.AddComponent<ElementOrbVisualFX>();
            fx.coreRenderer = orb.coreRenderer;
            fx.shellRenderer = orb.shellRenderer;
            if (orb.coreRenderer != null)
            {
                var coreMaterial = orb.coreRenderer.sharedMaterial;
                if (coreMaterial != null)
                {
                    if (coreMaterial.HasProperty("_Metallic")) coreMaterial.SetFloat("_Metallic", 0f);
                    if (coreMaterial.HasProperty("_Glossiness")) coreMaterial.SetFloat("_Glossiness", .06f);
                    if (coreMaterial.HasProperty("_Smoothness")) coreMaterial.SetFloat("_Smoothness", .06f);
                    if (coreMaterial.HasProperty("_EmissionColor")) coreMaterial.SetColor("_EmissionColor", ElementPalette.ColorOf(orb.element) * 3.2f);
                    coreMaterial.EnableKeyword("_EMISSION");
                    EditorUtility.SetDirty(coreMaterial);
                }
                var inner = orb.transform.Find("内发光能量核");
                if (inner == null)
                {
                    var innerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    innerGo.name = "内发光能量核";
                    innerGo.transform.SetParent(orb.transform, false);
                    innerGo.transform.localPosition = Vector3.zero;
                    innerGo.transform.localScale = Vector3.one * .22f;
                    var innerCollider = innerGo.GetComponent<Collider>();
                    if (innerCollider != null) UnityEngine.Object.DestroyImmediate(innerCollider);
                    innerGo.GetComponent<Renderer>().sharedMaterial = coreMaterial;
                    inner = innerGo.transform;
                }
                fx.innerCoreRenderer = inner.GetComponent<Renderer>();
            }
            fx.pulseSpeed = orb.element == ElementKind.Storm ? 3.4f : orb.element == ElementKind.Void ? 1.35f : 2.1f;
            fx.pulseAmount = orb.element == ElementKind.Fire ? .32f : .22f;
            fx.GetComponent<ElementOrb>().motionPhase = count * .73f + (int)orb.element * .31f;
            if (orb.shellRenderer != null) orb.shellRenderer.sharedMaterial = GetElementShellMaterial(orb.element);
            EditorUtility.SetDirty(orb);
            count++;
        }
        Debug.Log("AstralAlchemy element visual FX upgraded: " + count);
    }

    private static void AddForwardElementOrbVariants(ArenaGameController game)
    {
        var orbit = GameObject.Find("ElementOrbit_可调整元素环")?.transform;
        if (orbit == null || game == null) return;
        var existing = UnityEngine.Object.FindObjectsByType<ElementOrb>(FindObjectsSortMode.None).ToList();
        var variantsRoot = orbit.Find("前排追加元素球_可调整");
        if (variantsRoot == null) variantsRoot = New("前排追加元素球_可调整", orbit).transform;
        const int desiredOrbCount = 24;
        if (existing.Count < desiredOrbCount)
        {
            var templates = existing.GroupBy(o => o.element).Select(g => g.First()).ToArray();
            for (var i = existing.Count; i < desiredOrbCount; i++)
            {
                var template = templates[i % templates.Length];
                var clone = UnityEngine.Object.Instantiate(template.gameObject, variantsRoot);
                clone.name = "追加元素球_" + ElementPalette.ChineseName(template.element) + "_" + (i + 1);
                clone.transform.localPosition = new Vector3(Mathf.Lerp(-3.1f, 3.1f, (i % templates.Length) / Mathf.Max(1f, templates.Length - 1f)), 1.75f + (i % 2) * .42f, 2.2f);
                var clonedOrb = clone.GetComponent<ElementOrb>();
                clonedOrb.motionPhase = i * 1.17f;
            }
        }
        existing = UnityEngine.Object.FindObjectsByType<ElementOrb>(FindObjectsSortMode.None)
            .OrderBy(o => (int)o.element)
            .ThenBy(o => o.name)
            .ToList();
        var positions = new[]
        {
            new Vector3(-3.95f, 3.2f, 1.15f), new Vector3(3.95f, 3.2f, 1.15f),
            new Vector3(-3.55f, 2.37f, 1.35f), new Vector3(3.55f, 2.37f, 1.35f),
            new Vector3(-3.1f, 2.83f, 1.62f), new Vector3(3.1f, 2.83f, 1.62f),
            new Vector3(-2.7f, 2.03f, 1.86f), new Vector3(2.7f, 2.03f, 1.86f),
            new Vector3(-2.25f, 3.07f, 2.05f), new Vector3(2.25f, 3.07f, 2.05f),
            new Vector3(-4.25f, 1.81f, 2.22f), new Vector3(4.25f, 1.81f, 2.22f),
            new Vector3(-3.55f, 1.67f, 2.45f), new Vector3(3.55f, 1.67f, 2.45f),
            new Vector3(-2.85f, 1.83f, 2.62f), new Vector3(2.85f, 1.83f, 2.62f),
            new Vector3(-1.95f, 2.31f, 2.78f), new Vector3(1.95f, 2.31f, 2.78f),
            new Vector3(-1.35f, 3.42f, 2.34f), new Vector3(1.35f, 3.42f, 2.34f),
            new Vector3(-4.35f, 2.65f, 2.72f), new Vector3(4.35f, 2.65f, 2.72f),
            new Vector3(-.72f, 1.92f, 3.02f), new Vector3(.72f, 1.92f, 3.02f)
        };
        for (var i = 0; i < existing.Count && i < positions.Length; i++)
        {
            existing[i].transform.position = orbit.TransformPoint(positions[i]);
            existing[i].motionPhase = i * .83f;
            existing[i].gatherForward = 0f;
            existing[i].gatherHorizontal = .08f;
            EditorUtility.SetDirty(existing[i].transform);
        }
        game.elementOrbs = existing;
        EditorUtility.SetDirty(game);
        Debug.Log("AstralAlchemy element bubbles moved in front of the cauldron and expanded to " + game.elementOrbs.Count + ".");
    }

    // Controller tracking/rays are still required for PICO interaction, but the
    // decorative models parented under them are intentionally omitted.  In the
    // simulator they can sit close to the tracked camera and dominate the view.
    private static void RemoveHandHeldModels()
    {
        var left = GameObject.Find("LeftController_引力手")?.transform;
        var right = GameObject.Find("RightController_施法杖")?.transform;
        if (left == null || right == null) return;
        var removed = 0;
        foreach (var controller in new[] { left, right })
        {
            // The LineRenderer is on the controller itself and is preserved as the
            // visible interaction ray. Only visual child objects are removed.
            for (var i = controller.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(controller.GetChild(i).gameObject);
                removed++;
            }
            EditorUtility.SetDirty(controller);
        }
        Debug.Log("AstralAlchemy removed " + removed + " controller-mounted visual models; controller rays remain active.");
    }

    private static Material GetElementShellMaterial(ElementKind element)
    {
        var path = MaterialFolder + "/M_ElementShell_" + element + ".mat";
        var shader = Shader.Find("AstralAlchemy/ElementEnergyShell");
        if (shader == null) throw new Exception("Element energy shell shader has not imported yet.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else material.shader = shader;
        var color = ElementPalette.ColorOf(element);
        material.SetColor("_Color", new Color(color.r, color.g, color.b, .055f));
        material.SetColor("_EmissionColor", color * .8f);
        material.SetFloat("_RimPower", element == ElementKind.Void ? 2.4f : 3.2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void UpgradeParticleMaterials()
    {
        const string resourcesFolder = "Assets/Resources";
        const string path = resourcesFolder + "/M_SoftMagicParticle.mat";
        Directory.CreateDirectory(resourcesFolder);
        var shader = Shader.Find("AstralAlchemy/SoftMagicParticle");
        if (shader == null) throw new Exception("Soft magic particle shader has not imported yet.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else material.shader = shader;
        material.SetFloat("_Intensity", 1.2f);
        EditorUtility.SetDirty(material);
        var count = 0;
        foreach (var particle in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            var renderer = particle.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) continue;
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
            count++;
        }
        Debug.Log("AstralAlchemy soft particle materials upgraded: " + count);
    }

    [MenuItem("Astral Alchemy/Build Demo Scene")]
    public static void BuildDemoScene()
    {
        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.10f, .11f, .24f);
        RenderSettings.ambientEquatorColor = new Color(.10f, .055f, .16f);
        RenderSettings.ambientGroundColor = new Color(.012f, .008f, .035f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = .007f;
        RenderSettings.fogColor = new Color(.018f, .012f, .07f);

        var root = New("星界炼金竞技场_DEMO");
        var arena = New("Arena_可调整竞技场", root.transform);
        var cauldronRoot = New("Cauldron_可调整坩埚", root.transform);
        var coreRoot = New("StarCore_可调整星核", root.transform);
        var elementOrbit = New("ElementOrbit_可调整元素环", root.transform);
        var spawnsRoot = New("EnemySpawns_可调整出生点", root.transform);
        var environment = New("EnvironmentProps_可调整环境道具", root.transform);
        var worldUi = New("WorldSpaceUI_舒适视区界面", root.transform);
        var effects = New("Effects_反馈特效", root.transform);
        var audioRoot = New("Audio_音乐与音效", root.transform);
        var systems = New("Systems_玩法系统", root.transform);

        var mats = CreateMaterials();
        BuildSky(environment.transform, mats);
        BuildArena(arena.transform, mats);
        var cauldron = BuildCauldron(cauldronRoot.transform, mats);
        var core = BuildStarCore(coreRoot.transform, mats);
        var orbs = BuildElementOrbit(elementOrbit.transform, mats);
        var spawnPoints = BuildSpawnPoints(spawnsRoot.transform, mats);
        BuildEnvironmentProps(environment.transform, mats);
        var enemyPrefab = BuildEnemyPrefab(mats);
        var cameraRig = BuildPlayerRig(root.transform, mats);
        var hud = BuildWorldUi(worldUi.transform, mats);
        var feedback = BuildFeedback(effects.transform, mats);
        var sources = BuildAudio(audioRoot.transform);

        var controller = systems.AddComponent<ArenaGameController>();
        controller.starCore = core;
        controller.cauldron = cauldron;
        controller.elementOrbs = orbs;
        controller.enemySpawnPoints = spawnPoints;
        controller.enemyPrefab = enemyPrefab;
        controller.recipeTitle = hud.recipeTitle;
        controller.recipeDetail = hud.recipeDetail;
        controller.scoreLabel = hud.score;
        controller.coreLabel = hud.core;
        controller.timerLabel = hud.timer;
        controller.statusLabel = hud.status;
        controller.recipeTokens = hud.tokens;
        controller.coreFill = hud.coreFill;
        controller.tideFill = hud.tideFill;
        controller.resultPanel = hud.resultPanel;
        controller.resultTitle = hud.resultTitle;
        controller.resultStats = hud.resultStats;
        controller.crosshair = hud.crosshair;
        controller.castBurst = feedback.cast;
        controller.music = sources.music;
        controller.spellAudio = sources.spell;
        controller.positiveAudio = sources.positive;

        var previewEnemies = New("PreviewOnly_战斗构图敌人", root.transform);
        var previewPositions = new[] { new Vector3(-4.8f, .42f, 5.8f), new Vector3(-1.75f, .42f, 7.0f), new Vector3(4.8f, .42f, 5.8f) };
        for (var i = 0; i < previewPositions.Length; i++)
        {
            var previewEnemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            previewEnemy.name = "预览敌人_" + (i + 1);
            previewEnemy.transform.SetParent(previewEnemies.transform);
            previewEnemy.transform.localPosition = previewPositions[i];
            FaceHorizontally(previewEnemy.transform, core.transform.position);
            var enemy = previewEnemy.GetComponent<AstralEnemy>();
            enemy.ConfigureType(i == 1 ? AstralEnemy.EnemyType.Brute : i == 2 ? AstralEnemy.EnemyType.Wisp : AstralEnemy.EnemyType.Sprite);
            enemy.weakness = (ElementKind)i;
            enemy.RefreshVisuals();
        }
        previewEnemies.SetActive(false);

        // EnemySpawns remain visible and adjustable in the Scene hierarchy, but
        // enemies enter during play so the opening beat matches the Three.js demo.
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        BuildSettings(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        BuildTitleScene();
        Selection.activeGameObject = root;
        Debug.Log("Astral Alchemy scene built: " + ScenePath);
    }

    // THESIS: a full-bleed observatory, separate generated lettering, and a deliberate menu.
    // OWN-WORLD: midnight blue, engraved star-gold, cyan alchemy; user-supplied game menus guide the hierarchy.
    // STORY: recognize the game, learn the two-hand loop, then enter the arena.
    // FIRST VIEWPORT: title and three live menu actions on the left; astrolabe key art on the right.
    // FORM: user-pinned conventional game title menu. FINISH: verify both UI and input paths.
    [MenuItem("Astral Alchemy/Build Dedicated Title Scene")]
    public static void BuildTitleScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        EnsureTitleFeatherAsset();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.02f, .005f, .05f);
        RenderSettings.fog = false;
        var root = New("TitleRoot_独立启动阶段");
        var rig = New("TitlePlayerRig_桌面与PICO共用", root.transform);
        rig.transform.position = new Vector3(0f, 0f, -2.8f);
        rig.AddComponent<PXR_Manager>();
        var cameraGo = New("Main Camera", rig.transform);
        cameraGo.tag = "MainCamera";
        cameraGo.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = .05f; camera.farClipPlane = 40f; camera.fieldOfView = 58f;
        cameraGo.AddComponent<AudioListener>();
        cameraGo.AddComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
        var start = rig.AddComponent<DesignPlayerStart>();
        start.eyeCamera = camera; start.designEyePosition = new Vector3(0f, 1.62f, -2.8f); start.designYaw = 0f;
        var titleCanvasGo = New("TitleCanvas_EnglishCover", root.transform);
        var canvas = titleCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; canvas.sortingOrder = 100; canvas.overrideSorting = true;
        titleCanvasGo.AddComponent<CanvasScaler>();
        // Preserve the cover placement and scale approved in the 2026-09-09
        // simulator reference. Feathering affects only the background pixels.
        titleCanvasGo.transform.position = new Vector3(0f, 1.62f, 3.65f);
        titleCanvasGo.transform.localRotation = Quaternion.identity;
        titleCanvasGo.transform.localScale = Vector3.one * .0057f;
        titleCanvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(1600f, 900f);
        const string artRoot = "Assets/Art/UI/GeneratedUI/TitleRedesign20260908/";
        var background = Panel("Observatory_KeyArt", titleCanvasGo.transform, new Vector2(1600f, 914f), Vector2.zero,
            Color.white, LoadTitleSprite(TitleFeatherPath));
        background.GetComponent<Image>().raycastTarget = false;
        var logo = Panel("ASTRAL_ALCHEMY_GeneratedLogo", titleCanvasGo.transform, new Vector2(670f, 315f), new Vector2(-425f, 217f),
            Color.white, LoadTitleSprite(artRoot + "Logo.png"));
        logo.GetComponent<Image>().raycastTarget = false;
        // EN: 标语去除 // TitleText("MASTER THE ELEMENTS. DEFEND THE STAR.", titleCanvasGo.transform, new Vector2(720f, 35f), new Vector2(-400f, 25f), 23, new Color(.66f, .83f, .9f));
        var menu = root.AddComponent<TitleMenuController>(); menu.designStart = start;
        menu.mainMenu = NewUi("MainMenu", titleCanvasGo.transform);
        menu.helpPage = Panel("HowToPlay", titleCanvasGo.transform, new Vector2(760f, 710f), new Vector2(410f, 10f), new Color(.015f, .028f, .065f, .98f), null);
        menu.quitPage = Panel("QuitConfirmation", titleCanvasGo.transform, new Vector2(720f, 470f), new Vector2(410f, 0f), new Color(.015f, .028f, .065f, .98f), null);
        var entries = new List<TitleMenuButton>();
        entries.Add(TitleButton("START GAME", menu.mainMenu.transform, new Vector2(-435f, -85f), TitleMenuButton.Command.Start));
        entries.Add(TitleButton("HOW TO PLAY", menu.mainMenu.transform, new Vector2(-435f, -180f), TitleMenuButton.Command.Help));
        entries.Add(TitleButton("QUIT", menu.mainMenu.transform, new Vector2(-435f, -275f), TitleMenuButton.Command.Quit));
        TitleText("HOW TO PLAY", menu.helpPage.transform, new Vector2(650f, 55f), new Vector2(0f, 278f), 36, StarGold);
        TitleText("SELECT", menu.helpPage.transform, new Vector2(650f, 40f), new Vector2(0f, 186f), 26, Cyan);
        TitleText("Point your LEFT controller at an element.\nPull the trigger to send it into the cauldron.", menu.helpPage.transform, new Vector2(650f, 76f), new Vector2(0f, 124f), 25, Color.white);
        TitleText("COMBINE", menu.helpPage.transform, new Vector2(650f, 40f), new Vector2(0f, 47f), 26, Cyan);
        TitleText("Match the two elements shown in the recipe.\nThe cauldron prepares your spell.", menu.helpPage.transform, new Vector2(650f, 76f), new Vector2(0f, -15f), 25, Color.white);
        TitleText("CAST", menu.helpPage.transform, new Vector2(650f, 40f), new Vector2(0f, -92f), 26, Cyan);
        TitleText("Aim with your RIGHT controller. Hold the trigger\nto charge, then release. Protect the star core!", menu.helpPage.transform, new Vector2(650f, 76f), new Vector2(0f, -154f), 25, Color.white);
        entries.Add(TitleButton("BACK", menu.helpPage.transform, new Vector2(0f, -273f), TitleMenuButton.Command.Back));
        TitleText("LEAVE THE OBSERVATORY?", menu.quitPage.transform, new Vector2(630f, 80f), new Vector2(0f, 127f), 33, StarGold);
        TitleText("Return to your headset home.", menu.quitPage.transform, new Vector2(630f, 50f), new Vector2(0f, 50f), 26, Color.white);
        entries.Add(TitleButton("CANCEL", menu.quitPage.transform, new Vector2(0f, -55f), TitleMenuButton.Command.CancelQuit));
        entries.Add(TitleButton("QUIT GAME", menu.quitPage.transform, new Vector2(0f, -150f), TitleMenuButton.Command.ConfirmQuit));
        menu.buttons = entries.ToArray(); menu.startTarget = entries[0].GetComponent<Collider>();
        menu.helpPage.SetActive(false); menu.quitPage.SetActive(false);
        TitleText("POINT AT A BUTTON  /  PULL EITHER TRIGGER", titleCanvasGo.transform, new Vector2(1460f, 35f), new Vector2(0f, -422f), 23, new Color(.77f, .84f, .89f));
        var left = New("Left_MenuPointer", rig.transform); var right = New("Right_MenuPointer", rig.transform);
        menu.leftController = left.transform; menu.rightController = right.transform;
        var rayMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_TitlePointer.mat");
        if (rayMaterial == null)
        {
            rayMaterial = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(rayMaterial, MaterialFolder + "/M_TitlePointer.mat");
        }
        menu.leftRay = left.AddComponent<LineRenderer>(); SetupLine(menu.leftRay, rayMaterial); menu.leftRay.enabled = false;
        menu.rightRay = right.AddComponent<LineRenderer>(); SetupLine(menu.rightRay, rayMaterial); menu.rightRay.enabled = false;
        start.firstLandmark = logo.transform;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, TitleScenePath);
        AssetDatabase.ImportAsset(TitleScenePath, ImportAssetOptions.ForceSynchronousImport); AssetDatabase.SaveAssets();
        Debug.Log("AstralAlchemy dedicated English title built: generated art/logo and six working menu actions.");
    }

    private static Text TitleText(string value, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color)
    {
        var text = Text(value, parent, size, position, fontSize, color, TextAnchor.MiddleLeft);
        text.fontStyle = FontStyle.Normal; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        var outline = text.GetComponent<Outline>(); if (outline != null) outline.effectDistance = new Vector2(.5f, -.5f);
        return text;
    }

    private static TitleMenuButton TitleButton(string value, Transform parent, Vector2 position, TitleMenuButton.Command command)
    {
        var go = Panel("Button_" + command, parent, new Vector2(570f, 76f), position, new Color(.24f, .18f, .075f, .12f), null);
        var button = go.AddComponent<TitleMenuButton>(); button.command = command; button.background = go.GetComponent<Image>();
        var collider = go.GetComponent<BoxCollider>(); collider.size = new Vector3(570f, 76f, 12f); collider.center = new Vector3(0f, 0f, -6f);
        button.label = TitleText(value, go.transform, new Vector2(520f, 60f), new Vector2(8f, 0f), 32, new Color(.9f, .92f, .96f));
        return button;
    }

    private static Sprite LoadTitleSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new Exception("Missing generated title asset: " + path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
        if (path == TitleFeatherPath)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureTitleFeatherAsset()
    {
        const string sourcePath = "Assets/Art/UI/GeneratedUI/TitleRedesign20260908/Background.png";
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(sourcePath)))
                throw new Exception("Unable to read title cover: " + sourcePath);
            var pixels = texture.GetPixels32();
            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
            {
                var u = x / (float)(texture.width - 1);
                var v = y / (float)(texture.height - 1);
                // Broad top/bottom fade, narrower sides, rounded soft corners.
                // The outermost pixels are fully transparent; the center and
                // all separately rendered lettering retain their brightness.
                var horizontal = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) / .085f);
                var vertical = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / .17f);
                var index = y * texture.width + x;
                pixels[index].a = (byte)Mathf.RoundToInt(pixels[index].a * horizontal * vertical);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var png = texture.EncodeToPNG();
            // Compare generated content so future tuning actually takes effect,
            // while unchanged builds avoid rewriting/reimporting the asset.
            if (!File.Exists(TitleFeatherPath) || !File.ReadAllBytes(TitleFeatherPath).SequenceEqual(png))
            {
                File.WriteAllBytes(TitleFeatherPath, png);
                AssetDatabase.ImportAsset(TitleFeatherPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    public static void ApplyTitleFeatherAndCapture()
    {
        EnsureTitleFeatherAsset();
        var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        var canvas = GameObject.Find("TitleCanvas_EnglishCover").transform;
        var background = canvas.Find("Observatory_KeyArt").GetComponent<Image>();
        var oldFeather = canvas.Find("封面边缘羽化");
        if (oldFeather != null) UnityEngine.Object.DestroyImmediate(oldFeather.gameObject);
        background.sprite = LoadTitleSprite(TitleFeatherPath);
        Camera.main.backgroundColor = Color.black;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory("Preview/CoverRefinement");
        Canvas.ForceUpdateCanvases();
        CaptureCameraToPng(Camera.main, "Preview/CoverRefinement/Title_Feathered_20260909.png");
        Debug.Log("ASTRAL_TITLE_FEATHER: cover pose preserved at " + canvas.position.ToString("F4")
            + ", scale=" + canvas.localScale.ToString("F4") + "; title-only refinement saved and captured.");
    }

    public static void ApplyCoverAndHudRefinement()
    {
        EnsureTitleFeatherAsset();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UpgradeHud(Camera.main, UnityEngine.Object.FindFirstObjectByType<ArenaGameController>());
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        BuildTitleScene(); ValidateDemo();
    }

    [MenuItem("Astral Alchemy/Validate Demo _F8")]
    public static void ValidateDemo()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var errors = new List<string>();
        string[] names = { "Arena_可调整竞技场", "Cauldron_可调整坩埚", "StarCore_可调整星核", "ElementOrbit_可调整元素环", "EnemySpawns_可调整出生点", "EnvironmentProps_可调整环境道具", "WorldSpaceUI_舒适视区界面" };
        foreach (var name in names) if (GameObject.Find(name) == null) errors.Add("Missing hierarchy node: " + name);
        if (UnityEngine.Object.FindFirstObjectByType<ArenaGameController>() == null) errors.Add("Missing ArenaGameController");
        if (UnityEngine.Object.FindFirstObjectByType<PXR_Manager>() == null) errors.Add("Missing PXR_Manager");
        if (UnityEngine.Object.FindObjectsByType<ElementOrb>(FindObjectsSortMode.None).Length < 6) errors.Add("Missing element orbs");
        var core = UnityEngine.Object.FindFirstObjectByType<StarCoreController>();
        if (core == null || core.coreAnchor == null || core.coreAnchor.name != "星象仪星核_可调整") errors.Add("Astrolabe is not configured as the star core target");
        if (GameObject.Find("星核随动点光_可调整")?.GetComponent<Light>() == null) errors.Add("Adjustable star-core point light is missing");
        var gameController = UnityEngine.Object.FindFirstObjectByType<ArenaGameController>();
        var topHealth = gameController != null && gameController.coreLabel != null
            ? gameController.coreLabel.GetComponentInParent<StarCoreWorldHealthBar>()
            : null;
        if (topHealth == null || topHealth.core == null || topHealth.fill == null) errors.Add("Authoritative top star-core health bar is missing or unbound");
        if (gameController != null && gameController.scoreLabel != null && gameController.coreLabel != null && gameController.scoreLabel.transform.parent == gameController.coreLabel.transform.parent)
            errors.Add("Right information panel and top star-core health bar were merged");
        if (GameObject.Find("桌面十字准星_可调整") == null) errors.Add("Desktop crosshair upgrade is missing");
        var hudCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (hudCanvas == null || (Camera.main != null && hudCanvas.transform.IsChildOf(Camera.main.transform))) errors.Add("Core HUD must remain world-space, not camera-attached");
        var start = UnityEngine.Object.FindFirstObjectByType<DesignPlayerStart>();
        if (start == null || start.eyeCamera == null || start.firstLandmark == null) errors.Add("Authoritative design player start is missing");
        if (EditorBuildSettings.scenes.All(s => s.path != ScenePath || !s.enabled)) errors.Add("Gameplay scene is not enabled in build settings");
        if (EditorBuildSettings.scenes.All(s => s.path != TitleScenePath || !s.enabled) || EditorBuildSettings.scenes.FirstOrDefault().path != TitleScenePath) errors.Add("Dedicated title scene is not the first build scene");
        if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0) errors.Add("Android ARM64 is not enabled");
        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP) errors.Add("Android IL2CPP is not enabled");
        var settings = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (settings == null || settings.AssignedSettings == null || settings.AssignedSettings.activeLoaders.Count == 0) errors.Add("No Android XR loader configured");
        if (!PlayerSettings.runInBackground) errors.Add("Run In Background must be enabled for PICO XR");
        if (errors.Count > 0) throw new Exception("AstralAlchemy validation failed:\n" + string.Join("\n", errors));
        Debug.Log("AstralAlchemy validation passed. Dedicated title, world-space gameplay, Input System, ARM64, IL2CPP and XR loader are present.");
    }

    [MenuItem("Astral Alchemy/Capture Desktop Preview _F7")]
    public static void CaptureDesktopPreview()
    {
        // Unity 6 can crash inside Camera.Render when this editor method is invoked
        // with -batchmode -nographics (native DynamicVBO / shadow-map path).  A
        // PICO simulator window is the required XR visual evidence anyway, so do
        // not attempt an invalid headless capture.  Interactive editor captures
        // remain unchanged.
        if (Application.isBatchMode)
        {
            Debug.Log("AstralAlchemy preview skipped: batchmode/nographics cannot safely call Camera.Render. Use the PICO simulator window for XR visual validation.");
            return;
        }
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camera = Camera.main;
        if (camera == null) throw new Exception("Main camera missing for preview capture");
        var previewEnemies = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == "PreviewOnly_战斗构图敌人" && go.scene.IsValid());
        var previewWasActive = previewEnemies != null && previewEnemies.activeSelf;
        if (previewEnemies != null) previewEnemies.SetActive(true);
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode == RenderMode.WorldSpace) canvas.worldCamera = camera;
        }
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None))
        {
            enemy.RefreshVisuals();
        }
        Canvas.ForceUpdateCanvases();
        var previousTarget = camera.targetTexture;
        var texture = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Preview");
        File.WriteAllBytes("Preview/AstralAlchemyArena_EditorPreview.png", image.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(image);
        if (previewEnemies != null) previewEnemies.SetActive(previewWasActive);
        Debug.Log("AstralAlchemy desktop preview captured: Preview/AstralAlchemyArena_EditorPreview.png");
    }

    public static void CaptureFinalUiEvidence()
    {
        try
        {
            Directory.CreateDirectory("Preview");
            var titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            if (!titleScene.IsValid() || Camera.main == null) throw new Exception("Title camera is unavailable for UI evidence.");
            CaptureCameraToPng(Camera.main, "Preview/UI_Final_Title.png");

            var gameplayScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!gameplayScene.IsValid() || Camera.main == null) throw new Exception("Gameplay camera is unavailable for UI evidence.");
            var game = UnityEngine.Object.FindFirstObjectByType<ArenaGameController>();
            if (game != null)
            {
                game.RefreshHud();
                if (game.resultPanel != null) game.resultPanel.SetActive(false);
            }
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.renderMode == RenderMode.WorldSpace) canvas.worldCamera = Camera.main;
            Canvas.ForceUpdateCanvases();
            CaptureCameraToPng(Camera.main, "Preview/UI_Final_Gameplay.png");
            // A level gaze shows the complete raised HUD; this preview-only
            // camera change is never serialized into the gameplay scene.
            var authoredRotation = Camera.main.transform.rotation;
            var start = UnityEngine.Object.FindFirstObjectByType<DesignPlayerStart>();
            Camera.main.transform.rotation = Quaternion.Euler(0f, start != null ? start.designYaw : 0f, 0f);
            CaptureCameraToPng(Camera.main, "Preview/UI_Wraparound_Gameplay.png");
            Camera.main.transform.rotation = authoredRotation;
            Debug.Log("AstralAlchemy final UI evidence captured: Preview/UI_Final_Title.png and Preview/UI_Final_Gameplay.png");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(4);
        }
    }

    public static void CaptureResultPanelEvidence()
    {
        try
        {
            Directory.CreateDirectory("Preview/CoverRefinement");
            var gameplayScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!gameplayScene.IsValid() || Camera.main == null) throw new Exception("Gameplay camera is unavailable for result-panel evidence.");
            var game = UnityEngine.Object.FindFirstObjectByType<ArenaGameController>();
            if (game == null || game.resultPanel == null) throw new Exception("Result panel is unavailable for evidence.");
            if (game.recipeTitle != null) game.recipeTitle.transform.parent.gameObject.SetActive(false);
            if (game.scoreLabel != null) game.scoreLabel.transform.parent.gameObject.SetActive(false);
            if (game.statusLabel != null) game.statusLabel.transform.parent.gameObject.SetActive(false);
            game.resultPanel.SetActive(true);
            if (game.resultTitle != null) game.resultTitle.text = "Star Core Fallen";
            if (game.resultStats != null) game.resultStats.text = "Enemy breached the alchemy line\nEnemies repelled: 2\nPress any trigger · Try again";
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.renderMode == RenderMode.WorldSpace) canvas.worldCamera = Camera.main;
            var start = UnityEngine.Object.FindFirstObjectByType<DesignPlayerStart>();
            Camera.main.transform.rotation = Quaternion.Euler(0f, start != null ? start.designYaw : 0f, 0f);
            Canvas.ForceUpdateCanvases();
            CaptureCameraToPng(Camera.main, "Preview/CoverRefinement/HUD_Result_Open.png");
            var framePieces = game.resultPanel.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.StartsWith("结算框_")).ToArray();
            foreach (var piece in framePieces)
            {
                piece.enabled = false;
                Canvas.ForceUpdateCanvases();
                CaptureCameraToPng(Camera.main, "Preview/CoverRefinement/HUD_Result_Without_" + piece.name + ".png");
                piece.enabled = true;
            }
            foreach (var image in framePieces) image.enabled = false;
            Canvas.ForceUpdateCanvases();
            CaptureCameraToPng(Camera.main, "Preview/CoverRefinement/HUD_Result_NoFrame_Diagnostic.png");
            foreach (var image in framePieces) image.enabled = true;
            Debug.Log("AstralAlchemy result-panel evidence captured: Preview/CoverRefinement/HUD_Result_Open.png");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(4);
        }
    }

    public static void InspectFinalHudLayout()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var game = UnityEngine.Object.FindFirstObjectByType<ArenaGameController>();
        var canvas = game != null && game.recipeTitle != null ? game.recipeTitle.GetComponentInParent<Canvas>() : null;
        Debug.Log("ASTRAL_HUD canvas=" + (canvas != null ? canvas.transform.position + " scale=" + canvas.transform.lossyScale : "missing"));
        if (game != null)
        {
            DumpHudNode("recipe", game.recipeTitle != null ? game.recipeTitle.transform.parent : null);
            DumpHudNode("score", game.scoreLabel != null ? game.scoreLabel.transform.parent : null);
            DumpHudNode("core", game.coreLabel != null ? game.coreLabel.transform.parent : null);
            DumpHudNode("status", game.statusLabel != null ? game.statusLabel.transform.parent : null);
            DumpHudNode("result", game.resultPanel != null ? game.resultPanel.transform : null);
        }
        EditorApplication.Exit(0);
    }

    private static void DumpHudNode(string label, Transform node)
    {
        var rect = node != null ? node.GetComponent<RectTransform>() : null;
        Debug.Log("ASTRAL_HUD " + label + " node=" + (node != null ? node.name : "missing") + " active=" + (node != null && node.gameObject.activeSelf) + " pos=" + (rect != null ? rect.anchoredPosition : Vector2.zero) + " size=" + (rect != null ? rect.sizeDelta : Vector2.zero));
    }

    private static void CaptureCameraToPng(Camera camera, string path)
    {
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var texture = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        UnityEngine.Object.DestroyImmediate(pixels);
        UnityEngine.Object.DestroyImmediate(texture);
    }

    [MenuItem("Astral Alchemy/Capture Model Orientation Sheet")]
    public static void CaptureModelOrientationSheet()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.44f, .46f, .52f);
        RenderSettings.ambientEquatorColor = new Color(.25f, .23f, .3f);
        RenderSettings.ambientGroundColor = new Color(.09f, .07f, .12f);
        RenderSettings.fog = false;
        var root = New("模型朝向检查");
        var mats = CreateMaterials();
        var cameraGo = New("Main Camera", root.transform);
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .03f, .08f);
        camera.transform.position = new Vector3(0f, 4.8f, -12f);
        camera.transform.LookAt(new Vector3(0f, 1.4f, 0f));
        camera.fieldOfView = 45f;
        var light = New("检查主光", root.transform).AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.color = new Color(1f, .85f, .72f);
        light.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
        var fill = New("检查轮廓光", root.transform).AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = .65f;
        fill.color = new Color(.42f, .52f, 1f);
        fill.transform.rotation = Quaternion.Euler(25f, 155f, 0f);

        var tokens = new[] { "ed6677da", "97dec2b6", "d97755a2", "44e2ce0f", "21f0700e", "1e53acfb" };
        var names = new[] { "坩埚", "星核", "怪物A", "幽灵", "重型怪", "法杖" };
        for (var row = 0; row < tokens.Length; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var holder = New(names[row] + "_Y" + (col * 90), root.transform);
                holder.transform.position = new Vector3((col - 1.5f) * 3.1f, 0f, row * 3.1f);
                InstantiateModelByToken(tokens[row], holder.transform, Vector3.zero, new Vector3(0f, col * 90f, 0f), 2.1f, 2.35f, names[row] + "_模型");
            }
        }
        camera.transform.position = new Vector3(0f, 10.5f, -14.5f);
        camera.transform.LookAt(new Vector3(0f, 4.6f, 7.5f));
        camera.fieldOfView = 51f;
        var texture = new RenderTexture(1800, 1600, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Preview");
        File.WriteAllBytes("Preview/ModelOrientationSheet.png", image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(image);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("AstralAlchemy model orientation sheet captured: Preview/ModelOrientationSheet.png");
    }

    [MenuItem("Astral Alchemy/Capture Approved Enemy Orientation Sheet")]
    public static void CaptureApprovedEnemyOrientationSheet()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.32f, .28f, .42f);
        RenderSettings.ambientEquatorColor = new Color(.18f, .14f, .24f);
        RenderSettings.ambientGroundColor = new Color(.05f, .035f, .08f);
        RenderSettings.fog = false;
        var root = New("最终敌人朝向检查");
        var cameraGo = New("Main Camera", root.transform); cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .012f, .07f);
        camera.transform.position = new Vector3(0f, 4.3f, -12f); camera.transform.LookAt(new Vector3(0f, 1.25f, 0f)); camera.fieldOfView = 42f;
        var key = New("检查主光", root.transform).AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.4f; key.color = new Color(1f, .78f, .6f); key.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        var fill = New("检查轮廓光", root.transform).AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .75f; fill.color = new Color(.38f, .48f, 1f); fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
        var tokens = new[] { "d97755a2", "21f0700e", "6583f3cf" };
        var labels = new[] { "中型角怪", "重型巨怪", "圆形小怪" };
        for (var row = 0; row < tokens.Length; row++)
        for (var col = 0; col < 4; col++)
        {
            var holder = New(labels[row] + "_Y" + col * 90, root.transform);
            holder.transform.position = new Vector3((col - 1.5f) * 2.55f, 0f, row * .15f);
            var height = row == 1 ? 2.35f : row == 2 ? 1.25f : 1.85f;
            InstantiateModelByToken(tokens[row], holder.transform, Vector3.zero, new Vector3(0f, col * 90f, 0f), height, row == 1 ? 1.9f : 1.45f, labels[row] + "_模型");
            holder.transform.position += Vector3.up * row * 2.8f;
        }
        camera.transform.position = new Vector3(0f, 5.6f, -13f); camera.transform.LookAt(new Vector3(0f, 3.5f, 0f));
        var texture = new RenderTexture(1800, 1200, 24, RenderTextureFormat.ARGB32); camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
        Directory.CreateDirectory("Preview"); File.WriteAllBytes("Preview/ApprovedEnemyOrientationSheet.png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(image);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("AstralAlchemy approved enemy orientation sheet captured: Preview/ApprovedEnemyOrientationSheet.png");
    }

    [MenuItem("Astral Alchemy/Capture Hand Tool Orientation Sheet")]
    public static void CaptureHandToolOrientationSheet()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.34f, .3f, .48f);
        RenderSettings.ambientEquatorColor = new Color(.18f, .13f, .28f);
        RenderSettings.ambientGroundColor = new Color(.035f, .02f, .07f);
        RenderSettings.fog = false;
        var root = New("十二号手持道具方向检查");
        var cameraGo = New("Main Camera", root.transform); cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.018f, .008f, .055f);
        camera.transform.position = new Vector3(0f, 2.7f, -8.2f); camera.transform.LookAt(new Vector3(0f, 1.25f, 0f)); camera.fieldOfView = 42f;
        var key = New("检查主光", root.transform).AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.5f; key.color = new Color(1f, .76f, .54f); key.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        var fill = New("检查轮廓光", root.transform).AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .8f; fill.color = new Color(.28f, .55f, 1f); fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
        for (var col = 0; col < 4; col++)
        {
            var holder = New("十二号工具_Y" + col * 90, root.transform);
            holder.transform.position = new Vector3((col - 1.5f) * 2.15f, 0f, 0f);
            InstantiateModelByToken("41c79ac8", holder.transform, Vector3.zero, new Vector3(0f, col * 90f, 0f), 2.1f, 1.65f, "十二号叉状工具");
        }
        var texture = new RenderTexture(1600, 700, 24, RenderTextureFormat.ARGB32); camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
        Directory.CreateDirectory("Preview"); File.WriteAllBytes("Preview/HandTool12_OrientationSheet.png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(image);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("AstralAlchemy hand tool orientation sheet captured: Preview/HandTool12_OrientationSheet.png");
    }

    [MenuItem("Astral Alchemy/Run Desktop Gameplay Smoke Test")]
    public static void RunDesktopGameplaySmokeTest()
    {
        ClearEditorConsole();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        smokeFrame = 0;
        smokeStep = -1;
        smokeFailure = null;
        smokeEnteredPlayMode = false;
        previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.update -= SmokeUpdate;
        EditorApplication.update += SmokeUpdate;
        EditorApplication.isPlaying = true;
    }

    private static int smokeFrame;
    private static int smokeStep;
    private static string smokeFailure;
    private static bool previousEnterPlayModeOptionsEnabled;
    private static EnterPlayModeOptions previousEnterPlayModeOptions;
    private static bool smokeEnteredPlayMode;
    private static float smokeExpectedCoreFill;
    private static double smokeCoreDamageTime;

    private static void SmokeUpdate()
    {
        if (EditorApplication.isPlaying) smokeEnteredPlayMode = true;
        if (!EditorApplication.isPlaying && !smokeEnteredPlayMode) return;
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= SmokeUpdate;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            if (!string.IsNullOrEmpty(smokeFailure))
            {
                Debug.LogError("AstralAlchemy gameplay smoke test failed: " + smokeFailure);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log("AstralAlchemy desktop gameplay smoke test passed: wrong recipe, correct recipe, prepared spell, cast, shield, core damage, victory, failure and restart.");
            if (finalPipelineRequested)
            {
                finalPipelineRequested = false;
                EditorApplication.delayCall += RunFinalBuilds;
            }
            else if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        smokeFrame++;
        if (smokeFrame < 30) return;
        var game = UnityEngine.Object.FindFirstObjectByType<ArenaGameController>();
        if (game == null) { smokeFailure = "ArenaGameController missing"; EditorApplication.isPlaying = false; return; }
        if (smokeStep == 0 && smokeFrame == 30)
        {
            // The smoke test covers the complete free-play loop. Disable the
            // tutorial only on the runtime instance after Start has initialized.
            game.tutorialMode = false;
            if (game.CurrentRecipeA == ElementKind.Fire && game.CurrentRecipeB == ElementKind.Frost)
                game.ForceCompleteTutorialForTesting();
            game.SpawnEnemyForTesting();
            game.SpawnEnemyForTesting();
            var previewEnemiesNow = UnityEngine.Object.FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None);
            var previewCore = UnityEngine.Object.FindFirstObjectByType<StarCoreController>();
            if (previewEnemiesNow.Length > 0)
            {
                previewEnemiesNow[0].ConfigureType(AstralEnemy.EnemyType.Sprite);
                previewEnemiesNow[0].transform.position = new Vector3(-2.55f, .42f, 3.8f);
                if (previewCore != null) FaceHorizontally(previewEnemiesNow[0].transform, previewCore.TargetTransform.position);
            }
            if (previewEnemiesNow.Length > 1)
            {
                previewEnemiesNow[1].ConfigureType(AstralEnemy.EnemyType.Brute);
                previewEnemiesNow[1].transform.position = new Vector3(2.55f, .42f, 3.8f);
                if (previewCore != null) FaceHorizontally(previewEnemiesNow[1].transform, previewCore.TargetTransform.position);
            }
        }
        var core = UnityEngine.Object.FindFirstObjectByType<StarCoreController>();
        var orbs = UnityEngine.Object.FindObjectsByType<ElementOrb>(FindObjectsSortMode.None);
        var currentA = game.CurrentRecipeA;
        var currentB = game.CurrentRecipeB;
        if (smokeStep == -1)
        {
            if (UnityEngine.Object.FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None).Length != 0)
                smokeFailure = "Tutorial began with active enemies before the player learned the loop";
            if (game.statusLabel == null || !game.statusLabel.text.Contains("教学 1/3"))
                smokeFailure = "Tutorial opening instruction is missing";
            var fire = orbs.FirstOrDefault(o => o != null && o.element == ElementKind.Fire && o.IsAvailable);
            var frost = orbs.FirstOrDefault(o => o != null && o.element == ElementKind.Frost && o.IsAvailable);
            if (fire == null || frost == null) { smokeFailure = "Tutorial elements are unavailable"; EditorApplication.isPlaying = false; return; }
            game.ReceiveElement(fire);
            if (game.statusLabel == null || !game.statusLabel.text.Contains("教学 2/3")) smokeFailure = "Tutorial did not advance after the first element";
            game.ReceiveElement(frost);
            if (!game.IsSpellReady || game.statusLabel == null || !game.statusLabel.text.Contains("教学 3/3"))
                smokeFailure = "Tutorial did not prepare the first spell";
            var target = UnityEngine.Object.FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None).FirstOrDefault(e => e.name.Contains("教学敌人"));
            if (target == null) { smokeFailure = "Tutorial cast target did not appear"; EditorApplication.isPlaying = false; return; }
            game.CastAtRay(new Ray(Camera.main.transform.position, target.transform.position + Vector3.up - Camera.main.transform.position), .9f);
            if (game.IsSpellReady || game.statusLabel == null || !game.statusLabel.text.Contains("教学完成"))
                smokeFailure = "Tutorial did not complete after a successful cast";
            game.tutorialMode = false;
            smokeStep = 0;
            smokeFrame = 0;
            return;
        }
        if (smokeStep == 0)
        {
            var aimOrb = orbs.FirstOrDefault(o => o != null && o.IsAvailable);
            if (aimOrb == null || Camera.main == null)
            {
                smokeFailure = "Element orb aim-assist test setup missing";
            }
            else
            {
                var aimRay = new Ray(Camera.main.transform.position, aimOrb.transform.position - Camera.main.transform.position);
                if (ElementOrb.FindBestTarget(aimRay, 50f, 10f) != aimOrb)
                    smokeFailure = "Element orb aim-assist could not select a visible orb";
            }
            var wrong = Tuple.Create(ElementKind.Fire, ElementKind.Void);
            FeedPair(game, orbs, wrong.Item1, wrong.Item2);
            smokeStep = 1; smokeFrame = 0; return;
        }
        if (smokeStep == 1 && smokeFrame > 80)
        {
            if (!game.statusLabel.text.Contains("黯焰裂缝") && !game.statusLabel.text.Contains("错误配方")) smokeFailure = "Wrong recipe feedback missing";
            var buffedEnemy = UnityEngine.Object.FindObjectsByType<AstralEnemy>(FindObjectsSortMode.None).FirstOrDefault(e => e.IsShielded);
            if (buffedEnemy == null) smokeFailure = "Wrong recipe did not visibly shield enemies";
            // One supplied bubble per element may still be travelling from the first
            // test pair, so target any known stable pair that has two active orbs.
            var stablePair = FindAvailableStablePair(orbs);
            if (stablePair == null) { smokeFailure = "No active stable element pair available"; EditorApplication.isPlaying = false; return; }
            FeedPair(game, orbs, stablePair.Item1, stablePair.Item2);
            smokeStep = 2; smokeFrame = 0; return;
        }
        if (smokeStep == 2 && smokeFrame > 80)
        {
            if (!game.IsSpellReady) smokeFailure = "Correct recipe did not prepare a spell";
            var lockedA = game.CurrentRecipeA;
            var lockedB = game.CurrentRecipeB;
            var thirdOrb = orbs.FirstOrDefault(o => o != null && o.IsAvailable && o.element != lockedA && o.element != lockedB);
            if (thirdOrb != null)
            {
                game.ReceiveElement(thirdOrb);
                if (!game.IsSpellReady || !thirdOrb.IsAvailable) smokeFailure = "A third element replaced or consumed the prepared two-element spell";
            }
            if (game.selectionLabel == null || game.selectionLabel.text.Contains("待选择")) smokeFailure = "Selected two-element HUD did not show the prepared pair";
            if (game.elementIcons == null || game.elementIcons.Length != 6 || game.elementIcons.Any(icon => icon == null))
                smokeFailure = "One or more element HUD icons are missing";
            if (game.recipeTokens == null || game.recipeTokens.Length < 2 || game.recipeTokens[0] == null || game.recipeTokens[1] == null || game.recipeTokens[0].sprite == null || game.recipeTokens[1].sprite == null)
                smokeFailure = "Selected elements did not render as icons";
            CaptureRuntimePreview();
            var enemy = UnityEngine.Object.FindFirstObjectByType<AstralEnemy>();
            var ray = enemy != null ? new Ray(Camera.main.transform.position, enemy.transform.position - Camera.main.transform.position) : new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            game.CastAtRay(ray);
            if (UnityEngine.Object.FindFirstObjectByType<ElementSpellProjectile>() == null) smokeFailure = "Element-specific spell projectile was not created";
            smokeStep = 3; smokeFrame = 0; return;
        }
        if (smokeStep == 3 && smokeFrame > 10)
        {
            var shieldEnemy = UnityEngine.Object.FindFirstObjectByType<AstralEnemy>();
            if (shieldEnemy == null)
            {
                game.SpawnEnemyForTesting();
                shieldEnemy = UnityEngine.Object.FindFirstObjectByType<AstralEnemy>();
            }
            var probeObject = new GameObject("GameplaySmoke_EnemyProbe");
            var probe = probeObject.AddComponent<AstralEnemy>();
            probe.tintRenderers = Array.Empty<Renderer>();
            probe.health = 1000f;
            probe.weakness = ElementKind.Fire;
            probe.Hit(ElementKind.Fire, 10f);
            var weaknessDamage = 1000f - probe.health;
            probe.health = 1000f;
            probe.Shield(.25f);
            probe.Hit(ElementKind.Frost, 10f);
            var shieldedDamage = 1000f - probe.health;
            UnityEngine.Object.Destroy(probeObject);
            if (weaknessDamage < 20f) smokeFailure = "Enemy weakness multiplier not applied";
            if (shieldedDamage >= 4f) smokeFailure = "Enemy shield buff did not reduce damage";
            if (shieldEnemy != null) shieldEnemy.Shield(1f);
            if (shieldEnemy == null || !shieldEnemy.IsShielded) smokeFailure = "Enemy shield feedback unavailable";
            if (core == null) smokeFailure = "Star core missing";
            var healthBefore = core != null ? core.Health : 0f;
            if (core != null) core.Damage(12f);
            if (core != null && core.Health >= healthBefore) smokeFailure = "Star core did not take damage";
            smokeExpectedCoreFill = core != null ? core.Health / Mathf.Max(.01f, core.maxHealth) : 0f;
            smokeCoreDamageTime = EditorApplication.timeSinceStartup;
            smokeStep = 4; smokeFrame = 0; return;
        }
        if (smokeStep == 4 && EditorApplication.timeSinceStartup - smokeCoreDamageTime > .55d)
        {
            var worldHealth = game.coreLabel != null ? game.coreLabel.GetComponentInParent<StarCoreWorldHealthBar>() : null;
            if (worldHealth == null || worldHealth.fill == null) smokeFailure = "World star-core health bar is not bound";
            else if (worldHealth.fill.fillAmount > smokeExpectedCoreFill + .04f) smokeFailure = "World star-core health bar did not visibly decrease after damage";
            game.EndGame(true);
            if (!game.IsEnded) smokeFailure = "Victory state did not end game";
            game.Restart();
            smokeStep = 5; smokeFrame = 0; return;
        }
        if (smokeStep == 5 && smokeFrame > 40)
        {
            game = UnityEngine.Object.FindFirstObjectByType<ArenaGameController>();
            core = UnityEngine.Object.FindFirstObjectByType<StarCoreController>();
            if (game == null || core == null) { smokeFailure = "Restart did not reload scene"; EditorApplication.isPlaying = false; return; }
            core.Damage(200f);
            if (!game.IsEnded) smokeFailure = "Failure state did not end game";
            EditorApplication.isPlaying = false;
        }
    }

    private static void ClearEditorConsole()
    {
        var logEntries = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
        logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
    }

    private static void CaptureRuntimePreview()
    {
        // See CaptureDesktopPreview: a direct Camera.Render call is unsafe under
        // Unity batchmode -nographics. Gameplay smoke validation must continue
        // even when a visual capture is unavailable.
        if (Application.isBatchMode)
        {
            Debug.Log("AstralAlchemy runtime preview skipped in batchmode; gameplay smoke test continues.");
            return;
        }
        var camera = Camera.main;
        if (camera == null) return;
        Canvas.ForceUpdateCanvases();
        var previousTarget = camera.targetTexture;
        var texture = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Preview");
        File.WriteAllBytes("Preview/AstralAlchemyArena_RuntimePreview.png", image.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(image);
        Debug.Log("AstralAlchemy runtime preview captured: Preview/AstralAlchemyArena_RuntimePreview.png");
    }

    private static void RunFinalBuilds()
    {
        var exitCode = 0;
        try
        {
            BuildMacPlayer();
            BuildAndroidApk();
            Debug.Log("AstralAlchemy FINAL PIPELINE PASSED: validation, gameplay smoke test, runtime preview, macOS build and Android APK build.");
        }
        catch (Exception exception)
        {
            exitCode = 3;
            Debug.LogException(exception);
            Debug.LogError("AstralAlchemy FINAL PIPELINE FAILED: " + exception.Message);
        }
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
    }

    private static Tuple<ElementKind, ElementKind> FindWrongPair(ElementKind a, ElementKind b)
    {
        foreach (ElementKind x in Enum.GetValues(typeof(ElementKind)))
            foreach (ElementKind y in Enum.GetValues(typeof(ElementKind)))
                if (ElementPalette.GetRecipe(x, y).unstable) return Tuple.Create(x, y);
        return Tuple.Create(ElementKind.Fire, ElementKind.Frost);
    }

    private static void FeedPair(ArenaGameController game, ElementOrb[] orbs, ElementKind a, ElementKind b)
    {
        var firstOrb = orbs.FirstOrDefault(o => o != null && o.element == a);
        var secondOrb = orbs.FirstOrDefault(o => o != null && o != firstOrb && o.element == b);
        if (firstOrb == null || secondOrb == null) { smokeFailure = "Required element orb unavailable: " + a + "," + b; return; }
        if (!firstOrb.IsAvailable) firstOrb.Supply(.5f);
        if (!secondOrb.IsAvailable) secondOrb.Supply(.5f);
        game.ReceiveElement(firstOrb);
        game.ReceiveElement(secondOrb);
    }

    private static Tuple<ElementKind, ElementKind> FindAvailableStablePair(ElementOrb[] orbs)
    {
        foreach (var a in rotationsForSmoke)
        foreach (var b in rotationsForSmoke)
        {
            if (ElementPalette.GetRecipe(a, b).unstable) continue;
            var first = orbs.FirstOrDefault(o => o != null && o.element == a && o.IsAvailable);
            var second = orbs.FirstOrDefault(o => o != null && o != first && o.element == b && o.IsAvailable);
            if (first != null && second != null) return Tuple.Create(a, b);
        }
        return null;
    }

    private static readonly ElementKind[] rotationsForSmoke = { ElementKind.Fire, ElementKind.Frost, ElementKind.Storm, ElementKind.Bloom, ElementKind.Star, ElementKind.Void };

    [MenuItem("Astral Alchemy/Build macOS Test Player")]
    public static void BuildMacPlayer()
    {
        Directory.CreateDirectory("Builds/macOS");
        BuildTitleScene();
        EnsureBuildScenes();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { TitleScenePath, ScenePath },
            locationPathName = "Builds/macOS/AstralAlchemyXR.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("macOS player build failed: " + report.summary.result);
        Debug.Log("AstralAlchemy macOS test player built: Builds/macOS/AstralAlchemyXR.app");
    }

    [MenuItem("Astral Alchemy/Build PICO Android APK")]
    public static void BuildAndroidApk()
    {
        Directory.CreateDirectory("Builds/Android");
        // Re-apply the approved 1024px alchemy icon immediately before every
        // Android build so the emulator and exported APK cannot retain an old icon.
        ApplyPicoIcon.Run();
        BuildTitleScene();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureBuildScenes();
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        ConfigurePicoLoader();
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            throw new Exception("Unable to switch Unity to the Android build target");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { TitleScenePath, ScenePath },
            locationPathName = "Builds/Android/AstralAlchemyXR.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("PICO Android APK build failed: " + report.summary.result);
        Debug.Log("AstralAlchemy PICO Android APK built: Builds/Android/AstralAlchemyXR.apk");
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
        var result = new Dictionary<string, Material>();
        result["ink"] = Mat("M_InkNight", Ink, .15f, .65f);
        result["indigo"] = Mat("M_IndigoStone", Indigo, .18f, .45f);
        result["violet"] = Mat("M_VioletEnamel", Violet, .4f, .58f);
        result["copper"] = Mat("M_AgedCopper", Copper, .72f, .35f);
        result["copperBright"] = Mat("M_CopperEdge", CopperBright, .85f, .52f);
        result["gold"] = Emissive("M_StarGold", StarGold, 1.15f);
        result["cyan"] = Emissive("M_AstralCyan", Cyan, 1.05f);
        result["purple"] = Emissive("M_AstralPurple", new Color(.58f, .13f, 1f), 1.05f);
        result["glass"] = Transparent("M_MagicGlass", new Color(.26f, .08f, .48f, .3f));
        return result;
    }

    private static void BuildSky(Transform parent, Dictionary<string, Material> mats)
    {
        // The camera clear color is the reliable mobile/XR sky. A Standard sphere
        // viewed from inside produced back-face and lighting artefacts in preview.
        for (var i = 0; i < 140; i++)
        {
            var phi = i * 2.399963f;
            var y = Mathf.Lerp(2f, 24f, (i % 17) / 16f);
            var radius = Mathf.Sqrt(Mathf.Max(0f, 30f * 30f - y * y));
            var star = Primitive("星点_" + i.ToString("000"), PrimitiveType.Sphere, parent, new Vector3(Mathf.Cos(phi) * radius, y, Mathf.Sin(phi) * radius), Vector3.one * ((i % 11 == 0) ? .105f : .052f), i % 9 == 0 ? mats["gold"] : mats["cyan"]);
            star.GetComponent<Collider>().enabled = false;
        }

        var keyLight = New("星光主光", parent).AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.color = new Color(1f, .78f, .58f);
        keyLight.intensity = 1.02f;
        keyLight.shadows = LightShadows.Soft;
        keyLight.transform.localRotation = Quaternion.Euler(42f, -28f, 0f);

        var fillLight = New("星空轮廓光", parent).AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(.3f, .2f, .8f);
        fillLight.intensity = .42f;
        fillLight.shadows = LightShadows.None;
        fillLight.transform.localRotation = Quaternion.Euler(24f, 145f, 0f);
    }

    private static void BuildArena(Transform parent, Dictionary<string, Material> mats)
    {
        Primitive("竞技场主体", PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(9f, .32f, 9f), mats["indigo"]);
        Primitive("古铜外缘", PrimitiveType.Cylinder, parent, new Vector3(0f, .18f, 0f), new Vector3(9.35f, .08f, 9.35f), mats["copper"]);
        Primitive("内圈平台", PrimitiveType.Cylinder, parent, new Vector3(0f, .28f, 0f), new Vector3(7.6f, .06f, 7.6f), mats["ink"]);
        for (var r = 2; r <= 7; r += 2)
        {
            var ring = CreateRing("星图圆环_R" + r, parent, r, .025f, r % 4 == 0 ? mats["gold"] : mats["copperBright"]);
            ring.transform.localPosition = new Vector3(0f, .405f, 0f);
        }
        for (var i = 0; i < 12; i++)
        {
            var angle = i * Mathf.PI * 2f / 12f;
            var line = Primitive("星图刻线_" + i.ToString("00"), PrimitiveType.Cube, parent, new Vector3(Mathf.Sin(angle) * 3.5f, .38f, Mathf.Cos(angle) * 3.5f), new Vector3(.025f, .015f, 7f), mats["copperBright"]);
            line.transform.rotation = Quaternion.Euler(0f, i * 30f, 0f);
            line.GetComponent<Collider>().enabled = false;
        }
        // Keep the arena's sightline open. The previous evenly spaced ring put a
        // column directly between the desktop camera and the cauldron.
        var postPositions = new[]
        {
            new Vector3(-7.2f, .4f, -3.8f), new Vector3(7.2f, .4f, -3.8f),
            new Vector3(-8.1f, .4f, 2.2f), new Vector3(8.1f, .4f, 2.2f),
            new Vector3(-5.4f, .4f, 7.0f), new Vector3(5.4f, .4f, 7.0f)
        };
        for (var i = 0; i < postPositions.Length; i++)
        {
            var post = New("古铜观测柱_" + (i + 1), parent);
            post.transform.localPosition = postPositions[i];
            post.transform.LookAt(new Vector3(0f, 1.2f, 1.4f));
            Primitive("柱身", PrimitiveType.Cylinder, post.transform, new Vector3(0f, 1.5f, 0f), new Vector3(.28f, 1.5f, .28f), mats["copper"]);
            Primitive("符文灯", PrimitiveType.Sphere, post.transform, new Vector3(0f, 3.12f, 0f), Vector3.one * .28f, i % 2 == 0 ? mats["cyan"] : mats["purple"]);
        }
    }

    private static CauldronController BuildCauldron(Transform parent, Dictionary<string, Material> mats)
    {
        // Match the Three.js composition: the cauldron is the near foreground anchor.
        parent.localPosition = new Vector3(0f, .0f, -3.45f);
        parent.localScale = Vector3.one * 1.28f;
        var visual = New("坩埚动画根节点_可单独调整", parent);
        var model = InstantiateModelByToken("ed6677da", visual.transform, Vector3.zero, new Vector3(90f, 180f, 0f), 1.38f, 1.75f, "桌面模型_炼金坩埚_立正朝玩家");
        if (model == null)
        {
            Primitive("锅体", PrimitiveType.Sphere, visual.transform, new Vector3(0f, .65f, 0f), new Vector3(1.2f, .72f, 1.2f), mats["copper"]);
            Primitive("锅口", PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.35f, .12f, 1.35f), mats["copperBright"]);
        }
        Primitive("左机械脚", PrimitiveType.Capsule, visual.transform, new Vector3(-.48f, .12f, 0f), new Vector3(.23f, .35f, .23f), mats["copperBright"]).transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        Primitive("右机械脚", PrimitiveType.Capsule, visual.transform, new Vector3(.48f, .12f, 0f), new Vector3(.23f, .35f, .23f), mats["copperBright"]).transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        var liquid = Primitive("魔法液面", PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 1.12f, 0f), new Vector3(.82f, .025f, .82f), mats["purple"]);
        var light = New("坩埚点光", visual.transform).AddComponent<Light>();
        light.type = LightType.Point; light.range = 3.2f; light.intensity = .42f; light.color = new Color(.55f, .14f, 1f);
        light.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        var correct = CreateParticles("正确配方_星尘爆发", visual.transform, new Vector3(0f, 1.25f, 0f), mats["gold"], 55, 1.8f, .12f, 2.8f);
        var steam = CreateParticles("错误配方_暴走蒸汽", visual.transform, new Vector3(0f, 1.2f, 0f), mats["purple"], 85, 3.2f, .18f, 2.2f);
        correct.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var cauldron = parent.gameObject.AddComponent<CauldronController>();
        cauldron.visualRoot = visual.transform;
        cauldron.liquidRenderer = liquid.GetComponent<Renderer>();
        cauldron.correctBurst = correct;
        cauldron.rampageSteam = steam;
        cauldron.cauldronLight = light;
        return cauldron;
    }

    private static StarCoreController BuildStarCore(Transform parent, Dictionary<string, Material> mats)
    {
        // The star core sits behind the cauldron, floating on the far side of the arena.
        parent.localPosition = new Vector3(0f, .72f, 2.25f);
        var pedestal = Primitive("星核基座", PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(1.45f, .28f, 1.45f), mats["copper"]);
        var core = Primitive("星核发光体", PrimitiveType.Sphere, parent, new Vector3(0f, 1.82f, 0f), Vector3.one * .42f, mats["gold"]);
        var model = InstantiateModelByToken("97dec2b6", parent, new Vector3(0f, 1.82f, 0f), new Vector3(0f, 270f, 0f), 1.28f, 1.55f, "桌面模型_星核_正面朝玩家");
        var orbitRoot = New("星核旋转星环", parent);
        orbitRoot.transform.localPosition = new Vector3(0f, 1.82f, 0f);
        var ringB = CreateRing("能量光环", orbitRoot.transform, .94f, .022f, mats["cyan"]);
        ringB.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = New("星核主光", parent).AddComponent<Light>();
        light.transform.localPosition = new Vector3(0f, 1.82f, 0f); light.type = LightType.Point; light.color = StarGold; light.range = 4.2f; light.intensity = .55f;
        var controller = parent.gameObject.AddComponent<StarCoreController>();
        controller.coreRenderer = core.GetComponent<Renderer>(); controller.coreLight = light; controller.orbitRoot = orbitRoot.transform;
        return controller;
    }

    private static List<ElementOrb> BuildElementOrbit(Transform parent, Dictionary<string, Material> mats)
    {
        // Keep bubbles in the left/right air lanes, leaving the cauldron and core readable.
        parent.localPosition = new Vector3(0f, .15f, 0f);
        var list = new List<ElementOrb>();
        for (var i = 0; i < 12; i++)
        {
            var element = (ElementKind)(i % 6);
            var lane = i / 6;
            var slot = i % 6;
            var holder = New("元素球_" + ElementPalette.ChineseName(element) + "_" + (lane + 1), parent);
            var side = lane == 0 ? -1f : 1f;
            var xOffsets = new[] { 2.8f, 4.0f, 5.25f, 3.25f, 4.65f, 5.85f };
            var heights = new[] { 1.85f, 2.55f, 3.35f, 4.0f, 2.0f, 3.0f };
            var depths = new[] { -.55f, .65f, 1.25f, .2f, -1.1f, 1.75f };
            holder.transform.localPosition = new Vector3(side * xOffsets[slot], heights[slot], depths[slot]);
            var core = Primitive("元素内核", element == ElementKind.Frost ? PrimitiveType.Cube : PrimitiveType.Sphere, holder.transform, Vector3.zero, Vector3.one * .34f, Emissive("M_Element_" + element, ElementPalette.ColorOf(element), 1.25f));
            if (element == ElementKind.Frost) core.transform.localRotation = Quaternion.Euler(35f, 45f, 12f);
            var shell = Primitive("透明泡泡外壳", PrimitiveType.Sphere, holder.transform, Vector3.zero, Vector3.one * .57f, mats["glass"]);
            shell.GetComponent<Collider>().enabled = false;
            var halo = CreateRing("需求高亮环", holder.transform, .54f, .04f, mats["gold"]);
            halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            halo.SetActive(false);
            var collider = holder.AddComponent<SphereCollider>(); collider.radius = .64f;
            var orb = holder.AddComponent<ElementOrb>(); orb.element = element; orb.coreRenderer = core.GetComponent<Renderer>(); orb.shellRenderer = shell.GetComponent<Renderer>(); orb.demandHalo = halo;
            list.Add(orb);
        }
        return list;
    }

    private static Transform[] BuildSpawnPoints(Transform parent, Dictionary<string, Material> mats)
    {
        var points = new Transform[5];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = Mathf.Lerp(-40f, 40f, i / 4f) * Mathf.Deg2Rad;
            var marker = New("敌人出生点_" + (i + 1).ToString("00"), parent);
            marker.transform.localPosition = new Vector3(Mathf.Sin(angle) * 10.8f, .45f, Mathf.Cos(angle) * 10.8f + 1.6f);
            marker.transform.LookAt(new Vector3(0f, .45f, 1.8f));
            points[i] = marker.transform;
        }
        return points;
    }

    private static void BuildEnvironmentProps(Transform parent, Dictionary<string, Material> mats)
    {
        InstantiateModelByToken("ed01b67b", parent, new Vector3(-15f, -.25f, 37f), new Vector3(0f, 165f, 0f), 4.2f, 5.5f, "远景神殿_桌面模型");
        InstantiateModelByToken("ff7abdef", parent, new Vector3(-15f, .35f, 24f), new Vector3(0f, 20f, 0f), 1.1f, 1.35f, "左侧星象圆环_桌面模型");
        InstantiateModelByToken("b70e1838", parent, new Vector3(12.5f, .35f, 10f), new Vector3(0f, -18f, 0f), .75f, 1.3f, "右侧炼金桌_桌面模型");
        InstantiateModelByToken("88ac9992", parent, new Vector3(12.5f, .95f, 10f), new Vector3(0f, -35f, 0f), .2f, .34f, "法术书_桌面模型");
        InstantiateModelByToken("ec479770", parent, new Vector3(12.1f, .9f, 10.15f), Vector3.zero, .24f, .32f, "药剂瓶_桌面模型");
        InstantiateModelByToken("eb6df566", parent, new Vector3(12.85f, .9f, 10.2f), new Vector3(0f, 20f, 0f), .25f, .45f, "星图卷轴_桌面模型");
        InstantiateModelByToken("8ce8b4a5", parent, new Vector3(-12.5f, .48f, 9.5f), new Vector3(0f, 25f, 0f), .42f, .6f, "古铜摆件_桌面模型");
        InstantiateModelByToken("1e624f67", parent, new Vector3(-11.9f, .42f, 9.8f), new Vector3(0f, 18f, 0f), .48f, .78f, "元素圆盆_桌面模型");
        var key = New("主要模型位置说明", parent);
        key.transform.localPosition = new Vector3(0f, .02f, 0f);
    }

    private static GameObject BuildEnemyPrefab(Dictionary<string, Material> mats)
    {
        var root = New("EnemyPrefab_星界怪物");
        var visual = New("怪物动画根节点", root.transform);
        // The orientation sheet uses Y=270 on a stationary holder. Runtime enemy roots turn
        // toward the core, so Y=90 compensates that root yaw and keeps the face toward travel.
        var spriteModel = InstantiateModelByToken("d97755a2", visual.transform, Vector3.zero, new Vector3(0f, 90f, 0f), 1.62f, 1.45f, "中型角怪_正面朝目标");
        var wispModel = InstantiateModelByToken("6583f3cf", visual.transform, Vector3.zero, new Vector3(0f, 90f, 0f), 1.05f, 1.1f, "圆形小怪_正面朝目标");
        var bruteModel = InstantiateModelByToken("21f0700e", visual.transform, Vector3.zero, new Vector3(0f, 90f, 0f), 2.05f, 1.72f, "重型巨怪_正面朝目标");
        if (spriteModel == null) spriteModel = Primitive("怪物替代体", PrimitiveType.Capsule, visual.transform, new Vector3(0f, 1f, 0f), new Vector3(.9f, 1f, .9f), mats["violet"]);
        if (wispModel != null) wispModel.SetActive(false);
        if (bruteModel != null) bruteModel.SetActive(false);
        var eyeA = Primitive("左弱点眼", PrimitiveType.Sphere, visual.transform, new Vector3(-.28f, 1.45f, .4f), Vector3.one * .12f, mats["cyan"]);
        var eyeB = Primitive("右弱点眼", PrimitiveType.Sphere, visual.transform, new Vector3(.28f, 1.45f, .4f), Vector3.one * .12f, mats["cyan"]);
        var independentFx = New("敌人独立状态反馈", root.transform);
        var weaknessBadge = Primitive("头顶弱点徽记", PrimitiveType.Sphere, independentFx.transform, new Vector3(0f, 2.18f, 0f), Vector3.one * .24f, mats["purple"]);
        var weaknessCrown = CreateRing("头顶弱点六芒环", independentFx.transform, .24f, .035f, mats["purple"]);
        weaknessCrown.transform.localPosition = new Vector3(0f, 2.18f, 0f);
        var weaknessCore = Primitive("胸口弱点能量核", PrimitiveType.Sphere, visual.transform, new Vector3(0f, 1.18f, .44f), Vector3.one * .18f, mats["purple"]);
        // Three.js uses a restrained horizontal rune ring at the feet, not a vertical billboard.
        var aura = CreateRing("敌人状态光环", root.transform, .72f, .035f, mats["purple"]);
        aura.transform.localPosition = new Vector3(0f, .16f, 0f);
        aura.transform.localRotation = Quaternion.identity;
        var auraLight = New("敌人弱点轮廓光", independentFx.transform).AddComponent<Light>();
        auraLight.type = LightType.Point; auraLight.range = 2.4f; auraLight.intensity = .42f; auraLight.color = new Color(.42f, .15f, 1f);
        var collider = root.AddComponent<CapsuleCollider>(); collider.height = 2.2f; collider.radius = .72f; collider.center = new Vector3(0f, 1.05f, 0f);
        var enemy = root.AddComponent<AstralEnemy>(); enemy.visual = visual.transform; enemy.spriteVisual = spriteModel; enemy.wispVisual = wispModel; enemy.bruteVisual = bruteModel; enemy.tintRenderers = new[] { eyeA.GetComponent<Renderer>(), eyeB.GetComponent<Renderer>(), weaknessBadge.GetComponent<Renderer>(), weaknessCore.GetComponent<Renderer>() }.Concat(aura.GetComponentsInChildren<Renderer>()).Concat(weaknessCrown.GetComponentsInChildren<Renderer>()).ToArray(); enemy.weaknessLight = auraLight; enemy.footRing = aura.transform; enemy.independentStatusFx = independentFx.transform;
        var burst = CreateParticles("敌人消散", root.transform, new Vector3(0f, 1f, 0f), mats["purple"], 35, 1.4f, .12f, 1.4f); burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); enemy.defeatBurst = burst;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/Enemy_Astral.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildInitialEnemies(Transform parent, GameObject prefab)
    {
        // Preview enemies sit on the far rim like the Three.js standard combat shot.
        var positions = new[] { new Vector3(-5.15f, .4f, 3.6f), new Vector3(0f, .4f, 4.45f), new Vector3(5.15f, .4f, 3.6f) };
        for (var i = 0; i < positions.Length; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "场景预置敌人_" + (i + 1);
            go.transform.SetParent(parent);
            go.transform.localPosition = positions[i];
            FaceHorizontally(go.transform, new Vector3(0f, .4f, 2.55f));
            var enemy = go.GetComponent<AstralEnemy>();
            enemy.weakness = (ElementKind)(i % 6);
            enemy.RefreshVisuals();
        }
    }

    private static void FaceHorizontally(Transform subject, Vector3 targetPosition)
    {
        if (subject == null) return;
        var direction = targetPosition - subject.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < .001f) return;
        subject.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static GameObject BuildPlayerRig(Transform parent, Dictionary<string, Material> mats)
    {
        var rig = New("PlayerRig_桌面与PICO共用", parent);
        rig.transform.localPosition = new Vector3(0f, 0f, -9.45f);
        rig.AddComponent<PXR_Manager>();
        var desktop = rig.AddComponent<DesktopPlayerController>();
        var camera = New("Main Camera", rig.transform);
        camera.tag = "MainCamera";
        camera.transform.localPosition = new Vector3(0f, 3.65f, 0f);
        camera.transform.localRotation = Quaternion.identity;
        var cam = camera.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.012f, .008f, .045f); cam.nearClipPlane = .05f; cam.farClipPlane = 120f; cam.fieldOfView = 48f;
        camera.AddComponent<AudioListener>(); camera.AddComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
        desktop.playerCamera = cam;
        var crosshair = Primitive("桌面准星世界标记", PrimitiveType.Sphere, camera.transform, new Vector3(0f, 0f, 1.7f), Vector3.one * .012f, mats["gold"]); crosshair.GetComponent<Collider>().enabled = false;
        var left = New("LeftController_引力手", rig.transform); left.transform.localPosition = new Vector3(-.9f, 2.72f, 1.18f); left.transform.localRotation = Quaternion.Euler(12f, 0f, -30f);
        var right = New("RightController_施法杖", rig.transform); right.transform.localPosition = new Vector3(.95f, 2.68f, 1.24f); right.transform.localRotation = Quaternion.Euler(8f, 0f, 28f);
        Primitive("左手护腕", PrimitiveType.Capsule, left.transform, new Vector3(0f, -.08f, 0f), new Vector3(.065f, .12f, .065f), mats["copper"]).transform.localRotation = Quaternion.Euler(76f, 0f, 0f);
        Primitive("左手引力核心", PrimitiveType.Sphere, left.transform, new Vector3(0f, .04f, .02f), Vector3.one * .045f, mats["cyan"]);
        var staff = Primitive("右手星金法杖", PrimitiveType.Cylinder, right.transform, new Vector3(0f, .09f, .02f), new Vector3(.018f, .22f, .018f), mats["copperBright"]);
        staff.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        var staffHead = Primitive("法杖星辉端", PrimitiveType.Cube, right.transform, new Vector3(-.08f, .29f, .02f), Vector3.one * .036f, mats["gold"]);
        staffHead.transform.localRotation = Quaternion.Euler(35f, 45f, 18f);
        var leftLine = left.AddComponent<LineRenderer>(); SetupLine(leftLine, mats["cyan"]);
        var rightLine = right.AddComponent<LineRenderer>(); SetupLine(rightLine, mats["gold"]);
        var xrInput = rig.AddComponent<PicoControllerInput>(); xrInput.leftController = left.transform; xrInput.rightController = right.transform; xrInput.leftRay = leftLine; xrInput.rightRay = rightLine;
        return rig;
    }

    private static HudRefs BuildWorldUi(Transform parent, Dictionary<string, Material> mats)
    {
        var canvasGo = New("主战斗HUD_世界空间", parent);
        var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 10;
        canvasGo.AddComponent<CanvasScaler>(); canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.layer = 5;
        canvas.worldCamera = Camera.main;
        canvasGo.transform.localPosition = new Vector3(0f, 3.0f, .4f); canvasGo.transform.localRotation = Quaternion.identity; canvasGo.transform.localScale = Vector3.one * .005f;
        var rt = canvasGo.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(1280f, 720f);
        var sideArt = LoadUiSprite(SideUiTexturePath);
        var leftPanel = Panel("左侧配方仪", canvasGo.transform, new Vector2(340f, 340f), new Vector2(-450f, 72f), Color.white, sideArt);
        var rightPanel = Panel("右侧星核仪", canvasGo.transform, new Vector2(340f, 340f), new Vector2(450f, 72f), Color.white, sideArt);
        var healthPanel = Panel("星核血量条_顶部", canvasGo.transform, new Vector2(640f, 119f), new Vector2(0f, 286f), Color.white, LoadUiSprite(StatusUiTexturePath));
        var statusPanel = Panel("中央状态提示", canvasGo.transform, new Vector2(590f, 110f), new Vector2(0f, -270f), Color.white, LoadUiSprite(StatusUiTexturePath));
        var resultPanel = Panel("胜败结算面板", canvasGo.transform, new Vector2(760f, 263f), Vector2.zero, Color.white, LoadUiSprite(TitleUiTexturePath));
        var recipeTitle = Text("Target Recipe", leftPanel.transform, new Vector2(320f, 42f), new Vector2(0f, 119f), 25, StarGold, TextAnchor.MiddleCenter);
        var recipeDetail = Text("Fire + Fire → Solar Burst", leftPanel.transform, new Vector2(318f, 102f), new Vector2(0f, 50f), 22, Color.white, TextAnchor.MiddleCenter);
        var selected = Text("Selected  —  +  —", leftPanel.transform, new Vector2(320f, 48f), new Vector2(0f, -41f), 19, new Color(.88f, .92f, 1f), TextAnchor.MiddleCenter);
        selected.name = "SelectedElementLabel";
        var tokenA = ImageCircle("元素令牌A", leftPanel.transform, new Vector2(-62f, -109f), 52f, ElementPalette.ColorOf(ElementKind.Fire));
        var tokenB = ImageCircle("元素令牌B", leftPanel.transform, new Vector2(62f, -109f), 52f, ElementPalette.ColorOf(ElementKind.Storm));
        Text("+", leftPanel.transform, new Vector2(54f, 46f), new Vector2(0f, -109f), 30, StarGold, TextAnchor.MiddleCenter);
        var core = Text("Star Core", healthPanel.transform, new Vector2(560f, 30f), new Vector2(0f, 28f), 23, Color.white, TextAnchor.MiddleCenter);
        var coreFill = Bar("星核血条", healthPanel.transform, new Vector2(540f, 25f), new Vector2(0f, -16f), new Color(.95f, .24f, .18f), out _);
        var timer = Text("Star Tide  90s", rightPanel.transform, new Vector2(315f, 40f), new Vector2(0f, 22f), 22, new Color(.8f, .88f, 1f), TextAnchor.MiddleCenter);
        var tideFill = Bar("星潮能量", rightPanel.transform, new Vector2(300f, 17f), new Vector2(0f, -13f), Cyan, out _);
        var score = Text("Score 0000", rightPanel.transform, new Vector2(320f, 54f), new Vector2(0f, -72f), 20, StarGold, TextAnchor.MiddleCenter);
        var status = Text("Select · Brew · Release", statusPanel.transform, new Vector2(590f, 54f), Vector2.zero, 20, new Color(.82f, .9f, 1f), TextAnchor.MiddleCenter);
        resultPanel.SetActive(false);
        var resultTitle = Text("Star Core Defended!", resultPanel.transform, new Vector2(550f, 70f), new Vector2(0f, 105f), 48, StarGold, TextAnchor.MiddleCenter);
        var resultStats = Text("Wave Score", resultPanel.transform, new Vector2(520f, 180f), new Vector2(0f, -20f), 28, Color.white, TextAnchor.MiddleCenter);
        return new HudRefs { recipeTitle = recipeTitle, recipeDetail = recipeDetail, score = score, core = core, timer = timer, status = status, tokens = new[] { tokenA, tokenB }, coreFill = coreFill, tideFill = tideFill, resultPanel = resultPanel, resultTitle = resultTitle, resultStats = resultStats, crosshair = null };
    }

    private static (ParticleSystem cast, ParticleSystem ambience) BuildFeedback(Transform parent, Dictionary<string, Material> mats)
    {
        var cast = CreateParticles("施法命中爆发", parent, new Vector3(0f, 1.4f, 0f), mats["gold"], 70, 2.2f, .1f, 2.6f); cast.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var ambience = CreateParticles("竞技场漂浮星尘", parent, new Vector3(0f, 2.8f, 0f), mats["cyan"], 140, .25f, .045f, 8f);
        var shape = ambience.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 7f;
        return (cast, ambience);
    }

    private static (AudioSource music, AudioSource spell, AudioSource positive) BuildAudio(Transform parent)
    {
        var music = New("BGM_奇幻炼金战斗", parent).AddComponent<AudioSource>(); music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Fantasy_Battle_War2.mp3"); music.loop = true; music.volume = .34f;
        var spell = New("SFX_施法", parent).AddComponent<AudioSource>(); spell.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/enchant_enchant2.mp3"); spell.volume = .7f;
        var positive = New("SFX_配方成功", parent).AddComponent<AudioSource>(); positive.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/magic-gem-chime-hard.mp3"); positive.volume = .7f;
        return (music, spell, positive);
    }

    private static void BuildSettings(Scene scene)
    {
        PlayerSettings.productName = "Astral Alchemy XR";
        PlayerSettings.companyName = "Astral Workshop";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.astralworkshop.alchemyxr");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.MTRendering = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowDistance = 34f;
        QualitySettings.pixelLightCount = 2;
        Application.targetFrameRate = 72;
        EnsureBuildScenes();
        ConfigurePicoLoader();
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
    }

    private static void EnsureBuildScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(TitleScenePath, true),
            new EditorBuildSettingsScene(ScenePath, true)
        };
    }

    private static void ConfigurePicoLoader()
    {
        var picoSettings = AssetDatabase.LoadAssetAtPath<PXR_Settings>("Assets/XR/Settings/PXR_Settings.asset");
        if (picoSettings != null)
        {
            EditorBuildSettings.AddConfigObject("ByteDance.PICO.XR.Settings", picoSettings, true);
            EditorUtility.SetDirty(picoSettings);
        }
        var settingsStore = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget")
            .Select(g => AssetDatabase.LoadAssetAtPath<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(s => s != null);
        if (settingsStore == null)
        {
            settingsStore = ScriptableObject.CreateInstance<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(settingsStore, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settingsStore, true);
        }
        if (!settingsStore.HasSettingsForBuildTarget(BuildTargetGroup.Android)) settingsStore.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        if (!settingsStore.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) settingsStore.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var manager = settingsStore.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        XRPackageMetadataStore.AssignLoader(manager, "ByteDance.PICO.XR.PXR_Loader", BuildTargetGroup.Android);
        var general = settingsStore.SettingsForBuildTarget(BuildTargetGroup.Android);
        general.InitManagerOnStart = true;
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(settingsStore);
        AssetDatabase.SaveAssets();
    }

    private static GameObject InstantiateModelByToken(string token, Transform parent, Vector3 position, Vector3 rotation, float targetHeight, float targetMaxDimension, string name)
    {
        var guid = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Art/Models", "Assets/星界炼金幻想风" })
            .Where(g => AssetDatabase.GUIDToAssetPath(g).Contains(token))
            .OrderByDescending(g => token == "41c79ac8" && AssetDatabase.GUIDToAssetPath(g).StartsWith("Assets/星界炼金幻想风/12/", StringComparison.Ordinal))
            .FirstOrDefault();
        if (string.IsNullOrEmpty(guid)) return null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        if (prefab == null) return null;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        instance.transform.localPosition = position;
        instance.transform.localEulerAngles = rotation;
        instance.transform.localScale = Vector3.one;
        NormalizeModelBounds(instance, position, targetHeight, targetMaxDimension);
        foreach (var collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
        return instance;
    }

    private static void NormalizeModelBounds(GameObject instance, Vector3 desiredPosition, float targetHeight, float targetMaxDimension)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0 || targetHeight <= 0f || targetMaxDimension <= 0f) return;
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.y < .0001f) return;
        var largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        var scale = Mathf.Min(targetHeight / bounds.size.y, targetMaxDimension / Mathf.Max(.0001f, largestDimension));
        instance.transform.localScale = Vector3.one * scale;
        Physics.SyncTransforms();
        renderers = instance.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        var localCenter = instance.transform.parent.InverseTransformPoint(bounds.center);
        var localBottom = instance.transform.parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        instance.transform.localPosition += new Vector3(
            desiredPosition.x - localCenter.x,
            desiredPosition.y - localBottom.y,
            desiredPosition.z - localCenter.z);
    }

    private static Material Mat(string name, Color color, float metallic, float smoothness)
    {
        var path = MaterialFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color; mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Glossiness", smoothness); EditorUtility.SetDirty(mat); return mat;
    }

    private static Material Emissive(string name, Color color, float intensity)
    {
        var mat = Mat(name, color, .28f, .68f); mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * intensity); return mat;
    }

    private static Material Transparent(string name, Color color)
    {
        var mat = Mat(name, color, .1f, .9f); mat.SetFloat("_Mode", 3f); mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0); mat.DisableKeyword("_ALPHATEST_ON"); mat.EnableKeyword("_ALPHABLEND_ON"); mat.renderQueue = 3000; return mat;
    }

    private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent); go.transform.localPosition = position; go.transform.localRotation = Quaternion.identity; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }

    private static GameObject CreateRing(string name, Transform parent, float radius, float thickness, Material mat)
    {
        var root = New(name, parent);
        const int segments = 48;
        for (var i = 0; i < segments; i++)
        {
            var a = i * Mathf.PI * 2f / segments;
            var piece = Primitive("环段", PrimitiveType.Cube, root.transform, new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius), new Vector3(thickness, thickness, Mathf.PI * radius * 2f / segments * .55f), mat);
            piece.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            piece.GetComponent<Collider>().enabled = false;
        }
        return root;
    }

    private static ParticleSystem CreateParticles(string name, Transform parent, Vector3 position, Material material, int maxParticles, float speed, float size, float lifetime)
    {
        var go = New(name, parent); go.transform.localPosition = position; var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.loop = false; main.duration = 1f; main.startLifetime = lifetime; main.startSpeed = speed; main.startSize = size; main.maxParticles = maxParticles;
        var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)maxParticles) });
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .35f;
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.material = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    private static GameObject New(string name, Transform parent = null)
    {
        var go = new GameObject(name); if (parent != null) go.transform.SetParent(parent); return go;
    }

    private static void SetupLine(LineRenderer line, Material mat)
    {
        line.material = mat; line.widthMultiplier = .012f; line.positionCount = 2; line.useWorldSpace = true; line.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static GameObject Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color, Sprite sprite)
    {
        var go = NewUi(name, parent); var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.anchoredPosition = position; var image = go.AddComponent<Image>(); image.color = color; if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Simple; image.preserveAspect = true; }
        return go;
    }

    private static Text Text(string text, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color, TextAnchor alignment)
    {
        var go = NewUi("文字_" + text, parent);
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.anchoredPosition = position;
        var label = go.AddComponent<Text>(); label.text = text; label.font = GetChineseFont(); label.fontSize = fontSize; label.color = color; label.alignment = alignment; label.fontStyle = FontStyle.Bold; label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0f, 0f, 0f, .9f); outline.effectDistance = new Vector2(2f, -2f);
        return label;
    }

    private static Image ImageCircle(string name, Transform parent, Vector2 position, float size, Color color)
    {
        var go = NewUi(name, parent); var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = Vector2.one * size; rect.anchoredPosition = position; var image = go.AddComponent<Image>(); image.color = Color.white;
        var iconName = name.Contains("令牌A") ? "Fire" : name.Contains("令牌B") ? "Frost" : "Star";
        var iconPath = IconFolder + "/" + iconName + ".png";
        var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport(); }
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        image.preserveAspect = true;
        return image;
    }

    private static Sprite[] LoadElementIcons()
    {
        var icons = new Sprite[6];
        foreach (var iconName in new[] { "Fire", "Frost", "Storm", "Bloom", "Void", "Star" })
        {
            var path = IconFolder + "/" + iconName + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }
        icons[(int)ElementKind.Fire] = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/Fire.png");
        icons[(int)ElementKind.Frost] = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/Frost.png");
        icons[(int)ElementKind.Storm] = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/Storm.png");
        icons[(int)ElementKind.Bloom] = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/Bloom.png");
        icons[(int)ElementKind.Void] = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/Void.png");
        icons[(int)ElementKind.Star] = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/Star.png");
        return icons;
    }

    private static Image Bar(string name, Transform parent, Vector2 size, Vector2 position, Color color, out Image background)
    {
        var bg = NewUi(name + "_底", parent); var bgRt = bg.GetComponent<RectTransform>(); bgRt.sizeDelta = size; bgRt.anchoredPosition = position; background = bg.AddComponent<Image>(); background.color = new Color(.12f, .08f, .2f, .9f);
        var fill = NewUi(name + "_填充", bg.transform); var fillRt = fill.GetComponent<RectTransform>(); fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one; fillRt.offsetMin = new Vector2(3f, 3f); fillRt.offsetMax = new Vector2(-3f, -3f); var image = fill.AddComponent<Image>(); image.color = color; image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillAmount = 1f; return image;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one;
        go.layer = parent.gameObject.layer;
        return go;
    }

    private static Sprite LoadUiSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        var needsInputAlpha = path == TitleUiTexturePath;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.textureCompression != TextureImporterCompression.CompressedHQ || importer.mipmapEnabled ||
            (needsInputAlpha && importer.alphaSource != TextureImporterAlphaSource.FromInput)))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = false;
            if (needsInputAlpha)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
            }
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Font GetChineseFont()
    {
        if (chineseFont != null) return chineseFont;
        Directory.CreateDirectory("Assets/Art/UI/Fonts");
        const string systemFont = "/System/Library/Fonts/Supplemental/Arial Unicode.ttf";
        if (!File.Exists(ChineseFontPath) && File.Exists(systemFont)) File.Copy(systemFont, ChineseFontPath, true);
        AssetDatabase.ImportAsset(ChineseFontPath, ImportAssetOptions.ForceSynchronousImport);
        chineseFont = AssetDatabase.LoadAssetAtPath<Font>(ChineseFontPath);
        if (chineseFont == null) chineseFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return chineseFont;
    }

    private struct HudRefs
    {
        public Text recipeTitle, recipeDetail, score, core, timer, status, resultTitle, resultStats;
        public Image[] tokens;
        public Image coreFill, tideFill;
        public GameObject resultPanel, crosshair;
    }
}
