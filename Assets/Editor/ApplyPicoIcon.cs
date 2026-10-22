#if UNITY_EDITOR
using UnityEditor; using UnityEngine;
// EN版修改：用 BuildTargetGroup 替代 NamedBuildTarget（Unity 6000.5.5f1 兼容）
public static class ApplyPicoIcon { public static void Run() { var icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AppIcon/AppIcon.png"); if(icon==null){ Debug.LogWarning("EN_BUILD: AppIcon.png missing, skipping icon setup."); return; } PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android,new[]{icon}); AssetDatabase.SaveAssets(); Debug.Log("PICO_ICON_APPLIED"); } }
#endif
