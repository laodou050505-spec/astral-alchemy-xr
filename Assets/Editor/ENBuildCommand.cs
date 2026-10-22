// 英文版专用构建脚本 - AstralAlchemy-EN
// 输出路径：Builds/EN/AstralAlchemy-EN-PICO.apk
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

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
}
