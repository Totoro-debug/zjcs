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

当前官方输入位于 `.tools/source-packages/current/884_156692/`：10 个解密程序集、客户端／共享等五组反编译源码、448 张完整配置表，解析失败为 0。通用 `official-code/`／`official-data/` 缓存的对应文件已更新，无对应旧文件及旧字节保留；游戏单机适配源码保持原状。五包输入包含 21,030 个官方文件与两份 APK 内置依赖，具备新旧目录及逐项原始／解码 SHA256。既有游戏素材保持原实际来源。

当前 AOT 类型资料包含全部 57,638 个类型及 148 个程序集身份，原生方法镜像、Wwise／Spine／渲染组件字段描述均已保留；描述 DLL 的方法体为桩，不属于可运行玩法实现。本次资源范围包含 2,347 个变化包、114 个缺少字节凭据的已引用包，含依赖共 11,236 个包。APK 与当前云端的 88,308 条路径／地址对应齐全；较早来源的 1 个未对应 bundle 名称及既有工程成果完整保留。输入与生成物凭据见[准备来源清单](../../assets/reference/official-map/current-resource-preparation-source-manifest.json)。

本批结果保存在独立转换缓存，尚未更新游戏运行资产及绑定，不计作功能／地图完成发布。

原生资源结果为 `native-conversion/native-export-complete/`，共 186,919 个文件，包含 prefab、场景、材质、纹理、动画及原始脚本载荷；3 个原版空字体图集保持零尺寸 Alpha8 数据和原 GUID。`raw-resources/` 包含 149 个原始文件对应的媒体与数据，共 25,479 个文件，其中 12,665 份为 WAV，44 份保留循环资料。`component-fields/` 的有效清单包含主导出器报缺的全部 466 种类型、8,768 个组件及 18,506 条托管引用，未解决项为 0，原始字节与 CAB／PathID 身份均保留。上述目录均位于当前版本的 `native-conversion/` 下。

当前检查覆盖工程静态元数据、Editor／Player 编译、ProjectTools 校验、Windows D3D11 测试构建和启动窗口定位。校验仍有递归 DTO 序列化深度及 Edit Mode 清理提示；这些检查不证明完整游戏完成。启动画面见[启动](startup.md)，原位面列表的本地存档适配见[设置与存档](settings-and-save.md)，开场见[创角](character-creation.md)，地图战斗输入见[初始岛](../maps/map-10.md)。

## 自定义实现

Windows 使用可缩放竖屏与本地启动链；资源、规则和存档随游戏在本地运行。图形 API 使用 Unity 默认选择。

`Zjcs.Editor.Startup.SaveSelectionWindowsBuild.Run` 包装 `BuildWindowsGame`，使用本机已安装的 Windows Mono 支持和增量基准；无论成功或失败都恢复原脚本后端设置，不把该适配视为原 Android 后端实现。

当前桌面交付为用户授权的工作区测试版，包含其他会话尚未提交的依赖；不计作与已提交源码／素材完全对应的逐功能完成发布。测试使用隔离存档，不覆盖已有用户数据。

## 尚未实现

- 完整已提交工程从创角到家园、地图、战斗、退出及冷启动恢复的端到端运行与输入验证。
- 各模块尚缺的官方 UI、模型、动画、音频、渲染及第三方依赖恢复，具体缺口见所属页。
- 新版批量原生转换结果与旧工程资产的逐对象对应、补充字段及托管引用的原生对象回填、稳定 GUID／脚本绑定、Windows shader／精确 mip 和实际运行验证。
- 清除校验中的序列化深度和 Edit Mode 清理提示，并验证受影响运行路径。
- 与已提交源码、素材和文档一致的逐功能／地图 Windows 发布。

## 代码与资源入口

[ProjectTools.cs](../../unity/Assets/Game/Editor/ProjectTools.cs) · [unity-tools.json](../../unity-tools.json) · [unity.ps1](../../scripts/unity.ps1) · [内置屏启动工具](../../scripts/unity_internal_display.ps1)。共用操作见[素材管理](../asset-management.md)。
