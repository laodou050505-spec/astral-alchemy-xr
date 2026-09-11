# 星界炼金术 · Astral Alchemy XR

面向 PICO XR 的星界炼金防守游戏。玩家在世界空间竞技场内，通过控制器射线选择两种元素、投入炼金坩埚并将炼制法术发射至来袭敌人，以守护星核。

## 核心体验

- 独立的不透明启动封面，开始后才进入竞技场。
- 6DoF 世界空间场景：坩埚、元素球、星核、敌人与信息牌均可围绕观察。
- 元素配方、正确/错误炼制、敌人弱点与增益、星核血量、胜败结算。
- PICO 控制器射线交互；无头显时提供桌面鼠标键盘回退。

## 操作

- 左手扳机：指向并选择元素球。
- 右手扳机：蓄力并发射已炼制法术。
- 桌面回退：鼠标选择与施法；`R` 重新居中。
- 启动页：`Enter` / 空格 / XR 扳机开始。

## Unity / PICO 配置

- Unity 6000.5.5f1
- PICO XR SDK 6.0.0
- Android ARM64、IL2CPP、UnityPlayerActivity、Input System-only、PXR Loader、Multiview
- 包名：`com.astralworkshop.alchemyxr`

## 打开工程

用 Unity Hub 添加本工程目录并以 Unity 6000.5.5f1 打开。构建场景顺序为：

1. `Assets/Scenes/AstralAlchemyTitle.unity`
2. `Assets/Scenes/AstralAlchemyArena.unity`

> 不提交 `Library`、`Builds`、`Logs` 与本地预览文件。首次打开工程时 Unity 会重新导入。

## 已知验证范围

已通过桌面完整玩法冒烟测试与 PICO XR 模拟器安装/启动检查。没有实物头显，因此尚未验证 6DoF 真机跟踪、控制器舒适度、震动反馈与真实设备帧率。
