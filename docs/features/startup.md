# 登录前启动画面

[总索引](../README.md)

状态：未完成（启动主流程已在 Play Mode 和 Windows 测试版验证；弹窗附属表现与仅对应已提交工程的发布尚未完成）

## 功能简介

启动游戏后先显示原版健康游戏提示及淡入淡出，再显示《杖剑传说》启动背景、标志、适龄提示、版号说明和资源进度条。本地资源加载完成后进入已有的存档选择，随后沿用新建存档／继续游戏流程。

本页只覆盖登录前的启动阶段；接续的官方位面选择界面转换见[设置、拍照与本地存档](settings-and-save.md)，角色创建、开场演出及后续玩法见[新建存档与角色创建](character-creation.md)。

## 原始官方实现

官方 BootSplash、ThemeCanvas、LaunchPanel 和 AgeTipsDialog 定义健康提示、默认主题、标志、适龄弹窗及资源进度显示；在线初始化／更新与本地加载的差异见下文。

## 完整还原部分

固定来源为官方 APK 内 `871_156598` 的 Boot／Launch 资源；本模块没有使用云端其他版本。恢复 `BootSplash`、`ThemeCanvas`、`LaunchRoot/LaunchCanvas`、`AgeTipsDialog` 四个官方 prefab 中的 80 个对象层级、24 张 Sprite、原始锚点／尺寸／颜色、已迁移组件的引用和中文文本。使用原版默认主题背景与中文标志，不以截图充当界面；尚未迁移的弹窗组件见下文。

`LaunchPanel`、`LaunchUIRoot`、`AgeTipGo` 和 `AgeTipsDialog` 迁移原字段、显隐与按钮逻辑。健康提示沿用原 `BootSplashFadeIn` 的 2 秒及 `BootSplashFadeOut` 的约 1.183 秒曲线。适龄按钮打开原弹窗，确认按钮关闭弹窗；按钮的原 Animator 状态、触发器、缩放曲线和过渡也恢复为 Unity 原生资产。

恢复原动态／静态 FZY4K TMP 字体、字形表、FaceInfo、Atlas 和材质。流式字体按 APK 原 `FontAssetFontConfig` 的长度／MD5 核验，复用已迁移的同字节官方 TTF；正确处理官方 TMP 字体额外的序列化字段。单描边使用官方 `TMPEffect.DoSetup` 的 `pointSize / fontSize / atlasPadding` 换算及 Clamp01；原 TMP GLES 的描边和 UI 裁剪分支转换为 Windows HLSL。Sprite 保留原边框、轴心、PPU、顶点和三角形。

运行绑定入口为 `Resources/Startup/OfficialStartupAssets`。原始载荷位于 `unity/Assets/Official/Startup/`；原生 prefab、字体、材质、Sprite、动画和控制器位于 `unity/Assets/Game/Resources/Startup/`；官方逻辑位于 `Runtime/Official/Startup/`，本地加载适配位于 `Runtime/Offline/Startup/`。提取入口为 `scripts/prepare_official_startup.py`，原生恢复入口为 `Zjcs.Editor.Startup.OfficialStartupTools.Restore`；输入对象、CAB／bundle、版本、处理方式与哈希见[来源清单](../../assets/reference/startup/source-manifest.json)。新增素材尚未生成完整备份快照。

已在 Unity 2022.3.57f1c2 的 D3D11 Play Mode 实际查看健康提示、背景、原弹窗和进度条，模拟指针经过 UI 射线命中、按下／抬起／点击事件，验证适龄弹窗打开与确认、450×800／675×1200 缩放、900×800 宽窗及 360×1000 长窗的原边框与比例界限、启动资源释放、新建存档接续官方开场；另一次独立 Editor 冷启动已回读隔离草稿。验证使用独立公司名／产品名的测试存档，未覆盖用户数据。

静态检查 `python -B scripts/check_unity_project.py`、Unity 导入／编译和 `Zjcs.Editor.ProjectTools.Validate` 通过。当前工作区的游戏测试版已构建到 `D:/Desktop/杖剑传说/杖剑传说.exe`，桌面快捷方式指向该文件。已真实启动该 Windows Player，查看实际 D3D11 渲染的健康提示、背景／标志、满进度条、适龄弹窗和存档选择；模拟 EventSystem 射线、按下／抬起／点击完成弹窗打开和确认，验证启动层释放。检查前后原用户目录的 6 个数据文件 SHA256 一致。

复验入口为 `Zjcs.Editor.Startup.StartupPlayModeValidation.Run`；Windows Player 可传 `--zjcs-validate-startup <临时输出目录>`，输出运行画面及结果后退出，不新建存档。原生资产和显式加载绑定的输出哈希由 `scripts/record_startup_native_sources.py` 记录到[原生来源清单](../../assets/reference/startup/native-source-manifest.json)。测试日志、截图和隔离草稿在结论写回本页后清理。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

`scripts/prepare_official_startup.py` 优先核对官方 APK 的 Boot／Launch 包内清单、实际载荷、CAB 和 PPtr，所需资源均在固定 `871_156598` APK 中，无需混用云端版本。恢复 BootSplash、ThemeCanvas、LaunchRoot/LaunchCanvas 和 AgeTipsDialog 的 80 个对象、24 张 Sprite、原字体／材质、启动曲线及按钮 Animator；字体额外字段、流式字体配置长度／MD5、原顶点／三角形和 TMP 裁剪／描边程序均核实后转换。Windows 使用 Unity 原生 prefab、Sprite、字体、HLSL 和显式 `Startup/OfficialStartupAssets` 引用。输入对象、代码／APK／bundle／字体与输出哈希见[原始来源](../../assets/reference/startup/source-manifest.json)，导入后的资源和运行绑定见[原生来源](../../assets/reference/startup/native-source-manifest.json)，实际流程与发布边界见[功能页](startup.md)。

## 自定义实现

离线版以 `Resources.LoadAsync` 加载实际本地创角和角色目录，进度条反映加载进度；资源失败时停在启动页显示错误。加载成功后显示原“正在初始化”文本，切换到现有本地存档入口。用户正在阅读适龄弹窗时等待其关闭。没有联网更新、区服选择、客户端修复或登录请求；这些属于本地启动流程适配，不声称服务器登录逻辑已还原。

Windows 使用原 1080×1920 参考尺寸与缩放参数，沿用原 CanvasWidthAdjuster 的 0.45～121/160 比例界限和两像素边框补偿；屏幕安全区域按桌面完整客户区处理。版本文本标明 `871.156383 · 离线版`。季节主题服务器选择不参与本地启动，使用原 `ThemeCanvas` 已配置的默认背景。

## 尚未实现

- 适龄弹窗的原 `PopupWindow` 显示／隐藏曲线、模糊背景及点击遮罩关闭尚未接入；当前通过原适龄按钮打开、原确认按钮关闭。原 `ButtonSound`／`PopupWindowSound` 点击及开合音效、版号文本的 `OpenUrlHandler` 链接点击也尚未迁移。已核对 APK prefab 中这些组件及官方代码，不将当前弹窗声明为完整原版表现。
- 与仅含已提交代码／资源的工程完全对应的发布尚未完成。 Windows 验证使用隔离复制的当前工作区，包含其他会话尚未提交的既有依赖。隔离检出任务开始时的提交 `0f7e9e3` 无法通过编译，缺少已被引用的 `Zjcs.Offline.State`、`ProfileSlotCatalog`、`OfficialEquipmentCatalog` 等既有实现；因此桌面输出是当前工作区测试版，不以其构建成功推定提交后的完整工程可独立重建。
