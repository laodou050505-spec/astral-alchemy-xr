# Astral Alchemy XR — 英文版交付报告

生成时间：2026-10-22

---

## 1. 英文工程来源

- **源工程**：`/Users/ld/Documents/Codex/2026-08-12/w-x/outputs/AstralAlchemyXR`
- **选择理由**：交接文档第 8 节指定的构建镜像路径，即最新权威版本。
- **克隆方式**：APFS CoW (`cp -cR`) 排除 Library/Temp/Logs/Builds/obj/build，磁盘仅占差量。
- **英文工程路径**：`/Users/ld/Documents/Codex/EnglishEditions/AstralAlchemy-EN`
- **英文产品名**：Astral Alchemy XR
- **英文包名**：`com.astralworkshop.alchemyxr.en`

---

## 2. 改动清单

### 运行时脚本（Assets/Scripts/）

| 文件 | 改动说明 |
|------|---------|
| `ElementPalette.cs` | `ChineseName()` 保留为向后兼容别名，新增 `DisplayName()` 返回英文元素名；StableRecipes 12 条、Miscasts 9 条全部改为英文名/角色描述；默认配方名 "Entropy Overflow" |
| `ArenaGameController.cs` | 全部玩家可见字符串改为英文：教程三阶段提示、HUD 标签、星核状态、错误配方反馈、Buff 描述、波次信息、结算文字、重试提示；`ChineseName()` 调用全部换成 `DisplayName()` |
| `AstralEnemy.cs` | `"敌人突破防线 · 星核 -"` → `"Enemy breach! Core -"` |
| `ElementOrbVisualFX.cs` | `"元素能量微粒_"` → `"ElementParticle_"` |
| `ElementSpellProjectile.cs` | `"元素法术尾迹"` → `"ElementSpellTrail"` |

### 编辑器脚本（Assets/Scripts/Editor/）

| 文件 | 改动说明 |
|------|---------|
| `AstralAlchemySceneBuilder.cs` | 所有玩家可见默认文本英文化（9 条 HUD 文本、结算文本、productName）；内部对象名（197 处 CJK）保留（规范：对象名不属于玩家可见文字） |
| `AstralMenuVerification.cs` | 验证字符串 `"教学 1/3"` → `"Tutorial 1/3"` |

### 工程配置

| 文件 | 改动说明 |
|------|---------|
| `ProjectSettings/ProjectSettings.asset` | `productName: Astral Alchemy XR`；Android 包名：`com.astralworkshop.alchemyxr.en` |
| `Assets/AppIcon/AppIcon.png` | 原版已删除；新建 1024×1024 深靛蓝占位图（RGB 20,10,60） |

### 新建文件

| 文件 | 说明 |
|------|------|
| `Assets/Editor/ENBuildCommand.cs` | 英文版专用 Android APK 构建脚本，执行方法 `ENBuildCommand.BuildAndroid` |
| `Localization/EN_TEXT_TABLE.md` | 完整翻译表（236 行，含术语表、元素名、配方、教程、HUD、战斗反馈、结算、应用设置） |

---

## 3. 图片资源核查

所有图片已人工查看，**零 Tripo 调用**：

| 图片路径 | 是否含烘焙中文 | 结论 |
|---------|--------------|------|
| `Assets/Art/UI/GeneratedUI/TitleRedesign20260908/Logo.png` | 否（原本 Tripo 提示词已用英文） | 无需重做 |
| `Assets/Art/UI/GeneratedUI/TitleRedesign20260908/Background_Feathered.png` | 否（纯美术，无文字） | 无需处理 |
| `Assets/Art/UI/GeneratedUI/NanoBanana_Refined/SidePanelClean/SidePanelClean_Cropped.png` | 否（框架，无字） | 无需处理 |
| `Assets/Art/UI/GeneratedUI/NanoBanana_Refined/StatusPanel/StatusPanel_Cropped.png` | 否（框架，无字） | 无需处理 |
| `Assets/Art/UI/GeneratedUI/NanoBanana_Refined/TitlePanel/TitlePanel_Open.png` | 否（框架，无字） | 无需处理 |
| 元素图标（Fire/Frost/Storm/Bloom/Star/Void） | 否（纯视觉图标） | 无需处理 |

**Tripo 调用次数：0**

---

## 4. 字体说明

原版使用 `ArialUnicode.ttf`（传统 Unity Text，非 TMP）。该字体支持完整 ASCII 拉丁字符集，英文 HUD 显示无需额外字体导入。原版未嵌入中文专属回退字体，故英文版直接沿用现有字体资产即可。

如后续需要风格更匹配的英文字体（例如 Cinzel 替代标题、Rajdhani 替代 HUD），建议从 Google Fonts 取 SIL OFL 授权字体，将 .ttf 和 OFL.txt 同放 `Assets/Fonts/EN/`，再生成 TMP Font Asset。

---

## 5. CJK 扫描结果

扫描范围：`Assets/` 下所有 `.cs` / `.unity` / `.prefab` / `.asset` / `.txt` / `.json` 文件，以及 `ProjectSettings/ProjectSettings.asset`。

| 类别 | CJK 行数 | 说明 |
|-----|---------|------|
| 运行时 .cs（排除注释） | 0 | ✓ |
| 场景 .unity | 0 | ✓ |
| Prefab .prefab | 0 | ✓ |
| ScriptableObject .asset | 0 | ✓ |
| Resources 文本文件 | 0 | ✓ |
| ProjectSettings.asset | 0 | ✓ |
| 编辑器脚本（非注释，内部对象名） | 197 | 全为 GameObject.Find/Name 内部标识符，规范允许保留 |

**玩家可见文字 CJK：0**
注释中的中文（代码说明）未计入上表，不影响游戏显示。

---

## 6. 截图清单

截图存放目录：`VisualQA/EN-20261022/`

| 文件名 | 画面 | 验收状态 |
|--------|------|---------|
| *(待 APK 构建后补全)* | | |

> **未验证边界**：实体 PICO 头显未测试；以下截图依赖 Unity 编辑器/batchmode 模式生成，待槽位空闲后补全。

---

## 7. APK 信息

| 项目 | 内容 |
|-----|------|
| APK 路径 | `Builds/EN/AstralAlchemy-EN-PICO.apk`（待构建） |
| APK 大小 | 待构建 |
| SHA-256 | 待构建 |
| 版本号 | 待构建（与源工程一致） |
| 构建时间 | 待构建 |

---

## 8. 已验证项

- [x] 运行时 C# 脚本无玩家可见 CJK（0 行）
- [x] 场景文件无 CJK（AstralAlchemyTitle.unity、AstralAlchemyArena.unity 各 0 行）
- [x] ProjectSettings 包名/产品名已改为英文
- [x] 图片资产无烘焙中文（人工逐张查看）
- [x] ElementPalette 配方/元素名全部英文化
- [x] Tutorial 三阶段提示文本英文化
- [x] 结算文字英文化
- [x] 验证脚本字符串同步更新
- [x] EN_TEXT_TABLE.md 完整翻译表

## 9. 未验证边界

- APK 尚未构建（等待 Unity 槽位）
- 实体 PICO 头显设备未测试（无设备访问）
- VisualQA 截图尚未生成（依赖 Unity 编辑器模式）
- AstralMenuVerification 自动验证脚本完整运行结果（需要 Unity 编辑器）
- 长英文字符串在小屏幕模式下的排版溢出情况（需实际截图验证）

---

## 10. 不确定翻译说明

| 原文 | 英文译法 | 说明 |
|-----|---------|------|
| 星潮 | Tide / Tide Countdown | 游戏机制：随时间累积的星力浪潮，译为 "Tide" 兼顾宇宙感和直觉 |
| 星尘分数 | Score | 简化后更符合 UI 惯例 |
| 魔法液面 | *(内部对象名，不显示)* | 不影响游戏 |
| 星核完整度 | Star Core | 规范要求精简，完整版为 "Star Core Integrity" |
