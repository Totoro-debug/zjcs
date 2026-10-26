# Unity 工程与启动流程

[总索引](../README.md)

状态：未完成

## 功能简介

Unity 工程位于 [`unity/`](../../unity/)，目标为 Windows 10/11 离线桌面游戏，画面采用竖屏比例缩放。已按用户要求导出当前工作区测试版；完整游戏的迁移与全流程验收仍未完成，各功能和地图的实际范围见总索引。

## 完整还原部分

Unity 工程固定使用 `2022.3.57f1c2`。项目配置了 450×800 可缩放窗口、文本序列化、可见 `.meta` 和 `Bootstrap.unity` 构建场景。工程已有官方战斗规则载荷、Map10 原始数据、部分规则运行时和编辑器校验入口，详见[战斗规则](battle-execution.md)及[世界地图](world-map.md)。

默认启动先恢复并播放官方 `BootSplash`，随后显示原版 `ThemeCanvas`／`LaunchPanel` 和本地资源进度条，再进入现有存档入口。适龄提示使用原可交互弹窗，启动阶段不请求网络；素材、字体、动画与本地加载的实际验证范围见[登录前启动画面](startup.md)。

存档入口已将原 `PlanesListPanel`／`PlanesRoleItem` 转换为本地列表，保留官方卡片、头像、双页签与动画；DX12 Play Mode、实际 Windows Player 的模拟点击／滚轮、新建／续玩入口和草稿冷启动恢复通过。验证使用隔离存档，并已查看内置屏上可见 Player 的实际画面，9个用户数据文件哈希未改变。官方来源、具体测试边界及未完成部分见[设置、拍照与本地存档](settings-and-save.md)。

`ProjectTools.Configure` 配置工程设置；`ProjectTools.Validate` 检查编辑器版本、启动场景、窗口设置，并调用战斗目录、HUD 与 Map10 校验。`Zjcs.Editor.ProjectTools.BuildWindowsGame` 是游戏测试构建入口，输出 `D:/Desktop/杖剑传说/杖剑传说.exe`，桌面快捷方式指向该文件，工作目录为同名文件夹。`BuildFrameworkWindows` 只构建空启动框架，不能替代游戏构建。代码位于 [`ProjectTools.cs`](../../unity/Assets/Game/Editor/ProjectTools.cs)。

仓库根目录的 [`unity-tools.json`](../../unity-tools.json) 记录固定版本编辑器安装路径及工程路径；当前编辑器位于 `D:/Tool/Unity/Editor/Unity.exe`，实际文件版本为 `2022.3.57f1c2`。Unity Hub 为可选项，本机未安装。项目工具 [`unity.ps1`](../../scripts/unity.ps1) 提供 `info`、`open` 与 `initialize` 命令，并在启动前核对编辑器文件版本。运行 `& .\scripts/unity.ps1 info` 检查环境，`& .\scripts/unity.ps1 open` 启动工程，`& .\scripts/unity.ps1 initialize` 执行 `ProjectTools.Configure`。

各模块／地图共享 [Android → Windows 素材迁移流程](../asset-management.md#android-资源迁移到-windows)，以 [`prepare_unity_bundle_inputs.py`](../../scripts/prepare_unity_bundle_inputs.py) 检查真实源版本、平台与递归 CAB 依赖并准备转换输入。输入闭包成功不代表 Unity 原生资产、运行时依赖和 Windows 表现已经恢复；完整处理和判断依据在该共用流程中维护。

Unity 启动工具通过 [`unity_internal_display.ps1`](../../scripts/unity_internal_display.ps1) 识别内置屏的硬件标识，将编辑器与验证窗口放到该屏，独立于 Windows 主屏设置及屏幕编号。自定义验证命令先点源该脚本，再用 `Start-UnityOnInternalDisplay -Editor <编辑器路径> -ArgumentList <原参数数组>` 启动；隔离 worktree 复用仓库根目录工具。启动时隐藏窗口，定位时不激活窗口或改变前后顺序；保留现有渲染参数。内置屏未启用或识别不唯一时停止启动并报告原因，不回退到外接主屏。窗口启动阶段定位完成后可由用户自由移动。

为覆盖其他会话从隔离 worktree 直接启动 `Unity.exe` 的路径，本机还安装了随用户登录启动的窗口定位守护；脚本与 `status.json` 位于 `%LOCALAPPDATA%/UnityInternalDisplay/`，启动入口为用户 Startup 文件夹中的 `Unity 内置屏.lnk`。守护只匹配 `D:/Desktop/zjcs/` 下的 Unity 工程，将其可见窗口保持在内置屏，既有会话无需更换命令。停用时删除该启动快捷方式并结束 `status.json` 记录的守护进程即可；可用仓库脚本的 `-WatchProjectRoot D:/Desktop/zjcs` 参数手动启动。

启动工具已用相同版本 Unity 的临时空工程完成真实 GUI 检查：外接屏为系统主屏、内置 BOE 面板为副屏时，编辑器主窗口完整位于内置屏工作区；检查后关闭临时实例。对既有隔离 worktree 的实际导入／编译窗口检查曾检出位于外接屏，定位后同一检查通过。绕过项目启动脚本、直接启动仓库 `.tools/` 下临时空工程的检查也通过，编辑器主窗口完整位于内置屏；随后关闭该临时实例。上述检查只验证启动与窗口定位，不计为游戏功能、渲染效果或 Play Mode 验收。

静态 metadata 检查通过，Editor 和 Windows Player 脚本编译通过，`ProjectTools.Validate` 已正常退出并完成规则、HUD 与 Map10 资源检查；该校验仍输出递归 DTO 序列化深度及部分 Edit Mode 销毁提示，不能据此认定完整运行验收通过。最终 Windows D3D11 构建成功，未报告 C# 或 shader 编译错误。已真实启动 Player，查看存档入口画面，用鼠标点击“新建存档”创建草稿并进入官方开场场景；关闭后重启，已确认存档条目显示。完整创角、家园、地图和战斗流程的实际范围继续由所属页维护。

开场组件 `OpeningAttachmentFollower`、`OpeningMeshCopy` 的私有渲染器字段分别命名为 `skeletonRenderer`、`meshRenderer`，`OpeningDamageFloat`、`OpeningBattlePresentation` 的私有相机字段命名为 `battleCamera`，消除与 Unity `Component.renderer`／`Component.camera` 重名产生的四条 CS0108 警告。英雄雕像等级属性读取使用进度服务已加载的 `OfficialCharacterCatalog.GetLevelProps`，阶段条件列表使用 `Count`。运行时程序集编译失败时，`-executeMethod` 可能同时报告编辑器验证类找不到；应先修复编译错误，再检查实际入口。

`OfficialMap10BattlePlayModeAcceptance.RunRealInput` 在快捷聊天、窗口缩放和 HUD 截图检查全部结束后才恢复地图战斗执行，避免短战斗提前结束、销毁 HUD 并使检查持有失效的 `RectTransform`。静态 metadata 检查（40,688 个 GUID）、Unity Editor 导入／编译、`ProjectTools.Validate` 及该入口的 D3D11 Play Mode 回归通过；模拟指针经过实际射线命中和按下／抬起／点击事件，覆盖开始战斗、快捷聊天、450×800／675×1200 布局、技能图标、时间轴和返回地图，并实际查看运行画面。验证使用临时档案，不覆盖用户存档；该范围不代表完整地图或游戏迁移完成。

`CharacterOpeningVisualPlayModeAcceptance.Run` 的 D3D11 可见 Play Mode 回归通过，实际装配官方开场战场和录像单位、播放战前／战后剧情，检查技能／状态特效、18 个时间轴项目与 25 个伤害飘字，并查看单位和伤害运行画面。该检查验证开场组件的渲染及快进交接，不替代完整创角输入与存档恢复验收。

## 自定义实现

`Zjcs.Editor.Startup.SaveSelectionWindowsBuild.Run` 包装现有 `BuildWindowsGame`，显式使用本机已安装的 Windows Mono 支持，与既有桌面版本保持相同脚本后端并复用增量缓存；构建完成或失败后恢复工程原先的脚本后端设置。避免工程中的 IL2CPP 设置与仅安装 Mono 的编辑器及旧输出目录冲突。

Windows 渲染后端按用户要求固定为 DX12（Direct3D12），关闭自动图形 API，仅配置 `Direct3D12`。这属于 Windows 平台适配；迁移依据的官方 Android APK 在 `data.unity3d/globalgamemanagers` 的 `BuildSettings.m_GraphicsAPIs` 为 `[11]`，对应 `OpenGLES3`，不能将此编号解释为 DX11。官方材质、shader 和画面参数继续按原版恢复。

[`WindowsGraphicsSettings.cs`](../../unity/Assets/Game/Editor/WindowsGraphicsSettings.cs) 提供配置与验证入口，并在 Windows 构建前检查项目图形 API 及编辑器实际设备，防止用其他后端或无图形模式代替 DX12 验证。`unity.ps1 open`／`initialize` 显式传入 `-force-d3d12`；初始化先配置 DX12，再调用 `ProjectTools.Configure`。自定义 Unity 验证命令也传入 `-force-d3d12`，继续使用内置屏定位工具。Player 使用打包的 DX12 配置，正常启动不依赖额外命令行参数。

显式 `--zjcs-validate-startup <输出目录>` 诊断由 [`WindowsGraphicsValidation.cs`](../../unity/Assets/Game/Runtime/Offline/Startup/WindowsGraphicsValidation.cs) 在场景加载前核对实际 DX12 设备并启用后台运行，使现有启动验证可以在内置屏、不抢占前台的情况下执行截图和模拟指针。该行为仅在传入诊断参数时启用。

DX12 的 Unity 导入／编译、`ProjectTools.Validate`、Map10 战斗模拟指针回归和开场战斗视觉回归已通过；已查看地图、战斗 HUD、技能／状态特效与伤害数字的实际运行截图。战斗验证先创建 Game 窗口，再检查 `Renderer.isVisible`，避免未产生渲染帧时误判怪物不可见；模拟指针分支跳过系统前台激活与真实鼠标操作。Windows Player 构建成功，无 C# 或 shader 编译错误。设备日志为 `NVIDIA GeForce RTX 5060 Laptop GPU / Direct3D 12 [level 12.1]`；这验证 DX12 兼容性，不构成与 D3D11 的性能对比或完整游戏验收。

共享工作区静态 metadata 检查通过（41,192 个 GUID）。导出的 Player 不带 `-force-d3d12` 启动，已确认默认使用 DX12；原版适龄弹窗通过实际 EventSystem 射线命中及按下／抬起／点击事件完成开关，并进入存档选择。实际查看了弹窗与存档入口截图，原有 7 个用户数据文件的 SHA256 保持不变。窗口须在启动验证首帧前显示于内置屏；隐藏窗口中仅有逻辑通过或黑色截图不计为画面验证。桌面目录继续交付当前工作区测试版，完整游戏及逐功能源码一致发布的缺项保留。

本地单机规则沿用已确认的离线处理范围；外观、界面、动画、音效及流程以可恢复的官方 Unity 内容为准。其他功能页中记录的旧引擎实现不能据此视为 Unity 已接入。

存档选择属于离线启动适配，已有用户存档不用于测试；本次新建的测试槽在验证后清理。本次应用户要求导出包含其他会话未提交迁移代码与资源的当前工作区测试版；独立构建修复单独提交，保留其他会话未提交内容。这份测试导出不计为与已提交源码完整对应的逐功能完成发布。

## 尚未实现

- 从完整创角到家园、地图、战斗与退出恢复的全流程真实输入验收。
- 官方 UI、角色、地图、动画、音频、渲染管线及第三方运行依赖的恢复和绑定。
- 已有完整角色全部进度档案的 Windows 冷启动后进入家园、地图及后续状态回读验收；存档列表入口点击与独立草稿冷启动恢复已验证。
- Map10 原始 prefab bundle 的 Unity 版本兼容性处理及编辑器验证。
- 清除工程校验中的序列化深度与 Edit Mode 清理提示。
- 对应完整已提交源码和素材的逐功能 Windows 发布；当前工作区测试导出不替代该要求。
