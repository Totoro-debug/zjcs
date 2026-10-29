# Unity 工程与启动流程

[总索引](../README.md)

状态：未完成

## 功能简介

工程入口为 `unity/`，面向 Windows 10/11 离线桌面；负责启动场景、编辑器工具、构建链与窗口适配。启动画面、存档选择、创角及游戏模块分别由所属页维护。

## 原始官方实现

原游戏为 Unity 客户端，AssetBundle 包头为 `2022.3.57f1`。原脚本、资源及 Android 依赖须恢复成 Windows 可用资产，不能以空框架、占位脚本或直接复制 bundle 替代。

## 完整还原部分

- 工程固定 `2022.3.57f1c2`，配置 450×800 可缩放竖屏、文本序列化、可见 `.meta` 和 `Bootstrap.unity` 构建场景。
- `ProjectTools.Configure` 配置工程；`ProjectTools.Validate` 检查版本、场景、窗口和相关资源／规则绑定。`BuildWindowsGame` 构建游戏测试版；`BuildFrameworkWindows` 仅为空框架入口。
- Windows Standalone 的 8 个应用图标尺寸槽使用官方 APK 的 `res/mipmap-xxxhdpi/app_icon.png`；原图及哈希见[官方图标来源清单](../../assets/reference/application-icon-source-manifest.json)。
- `unity-tools.json` 记录编辑器／工程路径，本机编辑器为 `D:/Tool/Unity/Editor/Unity.exe`；Hub 可选。`scripts/unity.ps1` 提供 `info`、`open` 和 `initialize`，启动前核对版本。
- `scripts/unity_internal_display.ps1` 按内置屏硬件身份定位，支持隔离工程，不激活窗口或改变系统主屏。内置屏停用／身份不唯一时停止，不回退外接主屏。本机登录定位守护位于 `%LOCALAPPDATA%/UnityInternalDisplay/`，状态为 `status.json`，只匹配本仓库下工程；保持启用。

当前检查覆盖工程静态元数据、Editor／Player 编译、ProjectTools 校验、Windows D3D11 测试构建和启动窗口定位。校验仍有递归 DTO 序列化深度及 Edit Mode 清理提示；这些检查不证明完整游戏完成。启动画面见[启动](startup.md)，原位面列表的本地存档适配见[设置与存档](settings-and-save.md)，开场见[创角](character-creation.md)，地图战斗输入见[初始岛](../maps/map-10.md)。

## 自定义实现

Windows 使用可缩放竖屏与本地启动链；资源、规则和存档随游戏在本地运行。图形 API 使用 Unity 默认选择。

`Zjcs.Editor.Startup.SaveSelectionWindowsBuild.Run` 包装 `BuildWindowsGame`，使用本机已安装的 Windows Mono 支持和增量基准；无论成功或失败都恢复原脚本后端设置，不把该适配视为原 Android 后端实现。

当前桌面交付为用户授权的工作区测试版，包含其他会话尚未提交的依赖；不计作与已提交源码／素材完全对应的逐功能完成发布。测试使用隔离存档，不覆盖已有用户数据。

## 尚未实现

- 完整已提交工程从创角到家园、地图、战斗、退出及冷启动恢复的端到端运行与输入验证。
- 各模块尚缺的官方 UI、模型、动画、音频、渲染及第三方依赖恢复，具体缺口见所属页。
- 清除校验中的序列化深度和 Edit Mode 清理提示，并验证受影响运行路径。
- 与已提交源码、素材和文档一致的逐功能／地图 Windows 发布。

## 代码与资源入口

[ProjectTools.cs](../../unity/Assets/Game/Editor/ProjectTools.cs) · [unity-tools.json](../../unity-tools.json) · [unity.ps1](../../scripts/unity.ps1) · [内置屏启动工具](../../scripts/unity_internal_display.ps1)。共用操作见[素材管理](../asset-management.md)。
