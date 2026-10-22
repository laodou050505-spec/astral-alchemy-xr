// 英文版专用构建脚本 - AstralAlchemy-EN
// 输出路径：Builds/EN/AstralAlchemy-EN-PICO.apk
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using ByteDance.PICO.XR;
using System.IO;
using System.Linq;

public static class ENBuildCommand
{
    private const string TitleScene = "Assets/Scenes/AstralAlchemyTitle.unity";
    private const string ArenaScene = "Assets/Scenes/AstralAlchemyArena.unity";
    private const string ApkPath = "Builds/EN/AstralAlchemy-EN-PICO.apk";

    /// <summary>Entry point for batchmode: -executeMethod ENBuildCommand.BuildAndroid</summary>
    public static void BuildAndroid()
    {
        Directory.CreateDirectory("Builds/EN");

        // 应用 AppIcon（若存在）
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AppIcon/AppIcon.png");
        if (icon != null)
        {
            // 使用 BuildTargetGroup（Unity 6000.5.5f1 兼容）
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { icon });
            AssetDatabase.SaveAssets();
            Debug.Log("EN_BUILD: AppIcon applied.");
        }
        else
        {
            Debug.LogWarning("EN_BUILD: AppIcon.png not found — proceeding without custom icon.");
        }

        // 配置 Android 构建
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

        // 与原版 BuildAndroidApk 一致：构建前注册 PICO 设置并为 Android 分配 PXR_Loader，
        // 否则 PICO SDK 不会写入 pvr.app.type 等清单声明，应用会以平面模式启动
        ConfigurePicoLoader();

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("EN_BUILD: Failed to switch to Android build target.");
            EditorApplication.Exit(2);
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = new[] { TitleScene, ArenaScene },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None   // Release build (no Development flag)
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("EN_BUILD: APK built successfully -> " + ApkPath);
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("EN_BUILD: APK build FAILED: " + report.summary.result);
            EditorApplication.Exit(1);
        }
    }

    // 照搬原版 AstralAlchemySceneBuilder.ConfigurePicoLoader
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
        Debug.Log("EN_BUILD: PICO loader configured, active loaders = " + manager.activeLoaders.Count);
    }
}
