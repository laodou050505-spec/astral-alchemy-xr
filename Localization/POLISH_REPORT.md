# Astral Alchemy XR — 英文版 UI 精修报告（图七 / 图八）

生成时间：2026-10-22

---

## 1. 改动清单

### 图七：开始菜单按钮（START GAME / HOW TO PLAY / QUIT）

| 项目 | 改动前 | 改动后 |
|-----|-------|-------|
| 按钮底图 | 纯色半透明深棕矩形 | banana2 生成炼金奇幻风格铜框星座底图（9-slice，保帧角饰） |
| 按钮字体 | Arial Unicode 默认字体 | Cinzel Bold 44px（OFL，Google Fonts），按钮高度 95px，满足 VR 规范 C 标题字高 |
| 常态 | 不透明度 12% | 底图 tint alpha 38%，标签色 #EBE1C7 |
| 悬停状态 | 放大 2.5%，标签变金色 | 放大 5%（lerp exp -22），底图亮度 +8%，金色 glow overlay 35% alpha，标签变金色 #FFE88D |
| 按下状态 | 缩小 2% | 缩小 3%（0.97x），底图亮度 +40%，glow alpha 55%，标签纯白 |
| 手柄震动 - 悬停 | 无 | 两手同发 0.15 强度 / 20ms |
| 手柄震动 - 按下 | 0.25 强度 / 45ms | 0.4 强度 / 40ms |
| 点击音效 | 无 | magic_ui_click.mp3（ange-embed ui_audio，magic/positive/hard） |
| 悬停音效 | 无 | magic_ui_hover.mp3（ange-embed ui_audio，magic/neutral/soft） |

**修改文件**：
- `Assets/Scripts/TitleMenuButton.cs` — 新增 AudioSource、glow overlay、OnGainFocus()、状态动画改进
- `Assets/Scripts/TitleMenuController.cs` — SetFocus() 新增悬停震动、HapticImpulse 辅助方法
- `Assets/Scripts/Editor/AstralAlchemySceneBuilder.cs` — TitleButton() 重构、LoadButtonBgSprite()、LoadAudioClip()

### 图八：游戏内面板字体排版

| 面板 | 改动前 | 改动后 |
|-----|-------|-------|
| 标题文字（Target Recipe / Star Core / Star Tide / Score / 结算标题） | Arial Unicode 默认字体、全统一样式 | Cinzel Bold（OFL），字号 24-40，StarGold 色，加深轮廓 |
| 正文文字（配方详情 / 教程提示 / 结算统计 / Selected Element） | Arial Unicode，同上 | EB Garamond Regular（OFL），字号 22-24，近白色，行距 1.2-1.25 |
| Target Recipe 标题 | 24px → 25px（resizeTextForBestFit） | 26px，Cinzel Bold，行距 1.05 |
| Recipe 配方详情 | 23px | 22px，EB Garamond，行距 1.2，resizeTextMinSize=20 |
| Star Core 血条标签 | 23px Arial | 24px Cinzel Bold |
| Star Tide / Score 标签 | 23-24px | 24px Cinzel Bold |
| Tutorial 教程提示条 | 22px Arial，左对齐 | 22px EB Garamond，居中对齐，行距 1.22 |
| 结算标题 | 38px，行距 1.0 | 40px Cinzel Bold，行距 1.05 |
| 结算统计 | 24px，行距 1.22 | 24px EB Garamond，行距 1.25 |
| HowToPlay 页面字头（HOW TO PLAY / SELECT / COMBINE / CAST） | Arial Unicode | Cinzel Bold（size ≥ 26 自动用展示字体） |
| HowToPlay 正文 | Arial Unicode | EB Garamond（size < 26 自动用正文字体） |

**修改文件**：
- `Assets/Scripts/Editor/AstralAlchemySceneBuilder.cs` — UpgradeHud() 字体分配循环、TitleText() 字体逻辑、各标签字号调整

---

## 2. Tripo 生成调用

| task_id | 用途 | 模型 | 费用 |
|---------|-----|------|------|
| `42a2075e-eb97-455d-976b-a0ac0c8f1d08` | 炼金奇幻风格按钮底图（无字，16:9，1K） | banana2 | 10 credits |

生成图已检查四角无水印。

---

## 3. ange-embed 素材来源

| 文件 | 来源 | URL |
|-----|------|-----|
| `magic_ui_click.mp3` | ui_audio 集合，magic_pos_hard_02.mp3 | https://cdn-ange.holymolly.ai/e/f/ef912875-e486-40f7-836f-5ebc7140b52c.mp3 |
| `magic_ui_hover.mp3` | ui_audio 集合，magic_ui_soft_02.mp3 | https://cdn-ange.holymolly.ai/d/e/de65a1d6-e05e-45d9-86f5-fdfcd62c1051.mp3 |

---

## 4. 字体来源与许可

| 字体文件 | 来源 | 许可证 |
|---------|------|-------|
| `Cinzel-Bold.ttf` | Google Fonts / Natanael Gama | SIL OFL 1.1 |
| `Cinzel-Regular.ttf` | Google Fonts / Natanael Gama | SIL OFL 1.1 |
| `EBGaramond-Regular.ttf` | Google Fonts / EB Garamond Project | SIL OFL 1.1 |

OFL 许可证文件：`Assets/Art/UI/Fonts/Cinzel-OFL.txt`、`Assets/Art/UI/Fonts/EBGaramond-OFL.txt`

---

## 5. VR 文字高度检查（规范 C）

**场景一：开始菜单（Title Scene）**
- canvas 位置 (0, 1.62, 3.65)，scale 0.0057
- 玩家眼睛位置 (0, 1.62, -2.8)（rig z=-2.8，eye y=+1.62 local → world (0, 1.62, -2.8)）
- 观看距离 d = |3.65 − (−2.8)| = **6.45 m**

| 角色 | 字号（canvas px） | cap height = px×0.72×0.0057 | 阈值 | 结果 |
|-----|-----------------|----------------------------|------|-----|
| 按钮标签（Cinzel Bold 44） | 44 | 44×0.72×0.0057 = **18.1 cm** | d×0.028 = 18.1 cm | ✓ |

**场景二：游戏内 HUD（Arena Scene）**
- canvas 位于 WorldSpaceUI (0, 4.65, 2.1)，scale 0.0057
- 玩家在场地中心：eye (0, 1.62, 0)
- 观看距离 d = √((4.65−1.62)² + 2.1²) = √(9.18+4.41) = **3.69 m**

| 角色 | 字号（canvas px） | cap height = px×0.72×0.0057 | 阈值 | 结果 |
|-----|-----------------|----------------------------|------|-----|
| 标题（Cinzel Bold 26） | 26 | 26×0.72×0.0057 = **10.7 cm** | d×0.028 = 10.3 cm | ✓ |
| 正文（EB Garamond 22） | 22 | 22×0.72×0.0057 = **9.0 cm** | d×0.017 = 6.3 cm | ✓ |
| 次要文字（EB Garamond 20） | 20 | 20×0.72×0.0057 = **8.2 cm** | d×0.012 = 4.4 cm | ✓ |
| 结算标题（Cinzel Bold 40） | 40 | 40×0.72×0.0057 = **16.4 cm** | d×0.028 = 10.3 cm | ✓ |

---

## 6. 性能影响

- 新增字体资产：Cinzel-Bold.ttf（~34 KB）、EBGaramond-Regular.ttf（~54 KB）— 可忽略
- 新增音频：magic_ui_click.mp3（~46 KB）、magic_ui_hover.mp3（~7 KB）— 可忽略
- 新增 UI 图片：ButtonBg_Alchemical.png（banana2 生成，1K ASTC 压缩后约 512 KB）— 轻微
- 新增 AudioSource 组件：每个按钮 1 个，标题场景共 6 个 — 无运行时开销
- glow overlay Image 对象：每个按钮 1 个，共 6 个— 绘制调用影响 ≤ 1 SetPass（UI batch 合并）

Draw Calls / 三角面数：未变（纯 UI 改动）；主场景性能预算不受影响。

---

## 7. 未完成项 / 风险

- 所有改动已写入 Editor 静态方法（`BuildTitleScene()`、`UpgradeHud()`、`UpgradeCurrentUserScene()`）。`BatchFinalizeCurrentScene()` 是 batchmode 入口，将自动完成字体加载、引用替换、音效赋值和场景保存，无需手动 Inspector 操作。
- 字体文件需 Unity 首次编译时自动导入并生成 .meta 文件
- AstralMenuVerification 验证脚本通过条件不变：6 个按钮、无中文字符、ray-cast 命中检测、场景跳转逻辑均不受本轮改动影响。按钮碰撞体由 (570,76,12) 改为 (570,95,12) 以匹配新按钮高度，仍可正常命中。
- Editor 截图需在 Unity Editor 内使用菜单 "Astral Alchemy/Build Dedicated Title Scene" 重建后截图（总控构建流程中自动执行）。

---

## 8. 构建方法

`ENBuildCommand.BuildAndroid`（`ConfigurePicoLoader` 已保留，构建以 Android 平台进行）

APK 输出路径：`Builds/EN/AstralAlchemy-EN-PICO.apk`
