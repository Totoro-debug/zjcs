# Unity 工程与启动流程

[总索引](../README.md)

状态：未完成

## 功能简介

工程入口为 `unity/`，提供 Windows 10/11 离线版本的启动场景、编辑器工具和构建配置。启动界面、存档选择、创角及各游戏模块由所属页维护。

## 原始官方实现

客户端基于 Unity；原 Android 资源和脚本须恢复为 Windows 可用资产并按模块接入。官方来源、转换凭据及资源绑定见[来源总表](../../assets/reference/SOURCES.md)和各模块来源清单。

## 完整还原部分

- 工程使用 Unity `2022.3.57f1c2`，启动场景为 `Bootstrap.unity`，窗口基准为 450×800，可缩放竖屏显示。
- `ProjectTools.Configure` 配置工程；`ProjectTools.Validate` 检查工程及已接入模块的数据绑定；`BuildWindowsGame` 提供 Windows Player 构建入口。
- `scripts/unity.ps1` 提供环境信息、打开工程和初始化入口；编辑器路径由 `unity-tools.json` 配置。
- 官方客户端源码归档供模块迁移时查阅。IL2CPP 原生方法的描述桩不是可运行实现，相关游戏代码仍须按模块恢复并接入。

当前 Windows 版本发布到 `D:/Desktop/ZjcsGame/Zjcs.exe`，桌面快捷方式指向该入口。游戏代码对应提交 `809cbc88f8`；完整构建后复用资源缓存更新脚本，交付的 7,106 个文件与验证包逐项核对 SHA-256。静态元数据检查、Unity 导入／编译和 `ProjectTools.Validate` 通过；元素调和规则测试通过。隔离包与正式路径分别以静音模拟输入验证存档列表、创角草稿和冷重开，并已查看实际画面。该范围不代表家园及整个游戏已完成；功能缺项见各所属页。

## 自定义实现

Windows 使用本地启动和可缩放竖屏窗口；游戏资源、规则与存档由本地工程提供。

### Windows 构建路径

正式交付目录为 `D:/Desktop/ZjcsGame/`，入口为 `Zjcs.exe`，Unity 数据目录为 `Zjcs_Data`；空框架构建同样使用 `Zjcs.exe`。采用独立的 `ZjcsGame` 目录，避免 Windows 不区分大小写导致与源码目录 `zjcs` 冲突。工程产品名、初始化与正式构建均使用 `Zjcs`，防止 Burst 按中文产品名生成调试目录。`WindowsBuildNamingPolicy` 在所有 Windows BuildPipeline 入口（包括菜单及验证构建）执行前统一产品名并检查完整输出路径和已有目录；`WindowsBuildPathValidation` 在构建后递归检查文件及目录，遇到非 ASCII 路径即报错。主线与地图验证构建默认入口也统一为 `Zjcs.exe`；游戏内中文内容保留。旧版调试目录只改为 ASCII 名称，不删除既有文件或存档。

当前发布目录已将遗留中文 Burst 调试目录改为 `LegacyChinese_BurstDebugInformation_DoNotShip`，递归检查不存在非 ASCII 路径；入口和游戏载荷沿用现有发布包，未使用含其他会话未提交修改的工作区覆盖游戏。

构建命名验证已通过：Unity `2022.3.57f1c2` 在隔离最小工程中加载本项目实际命名回调和 Burst `1.8.18`，以旧中文产品名执行真实 Windows BuildPipeline，成功生成 `Zjcs.exe`、`Zjcs_Data` 和 `Zjcs_BurstDebugInformation_DoNotShip`，全部输出路径为 ASCII；中文目标路径和目录内遗留中文项均会被拒绝。此构建仅验证工具链，不是游戏发布包。正式工程导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 均以 0 退出；静态元数据检查通过（152,100 个 GUID）。本次仅修改构建命名，未执行游戏交互或 Play Mode 回归。

### 隔离验证工程装配

[`validation_assemblies.py`](../../scripts/validation_assemblies.py) 集中处理运行时程序集依赖闭包、嵌套程序集归属、按名称或 GUID 绑定的 `.asmref`、代码与 `.meta` 复制，以及 MessagePack 和世界初始化配置等隐式输入。17 个原有装配入口已接入该公共 module，地图验证不再从伙伴星命页面脚本导入复制实现。

通常通过 `stage_runtime_modules(project, runtime, roots, allowed_external=...)` 完成解析、外部程序集检查和复制；返回实际选中的程序集名称。省略外部白名单的入口继续由各自 Unity 包提供外部依赖。主线启动入口通过 `resolve_runtime_modules` 和 `copy_modules` 保留包程序集优先及 Spine 载荷处理；小地图和完整技能图保留按实际资源追加程序集的流程。各入口继续选择自身官方资源、包、验证代码和工作区，来源版本与哈希校验保持原约定。

当前验证范围：

- [`test_validation_assemblies.py`](../../scripts/test_validation_assemblies.py) 的三个测试覆盖循环依赖、嵌套程序集隔离、两种 `.asmref`、源码与元数据字节、外部依赖失败前置检查、排除规则及隐式输入。
- 主线启动 78 个程序集、6,899 个装配文件与原生伙伴战斗 19 个程序集、5,810 个装配文件，重构前后路径及 SHA-256 完全一致。
- Unity `2022.3.57f1c2` 已完成主线客户端上下文 Play Mode 验证（Map10–16）、原生伙伴战斗数据验证、正式工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate`；静态检查 `python -B scripts/check_unity_project.py` 通过。
- 原星命页面装配在重构前即被既有资源哈希校验拒绝，具体为 `C_none_0.asset.meta` 与页面来源清单不符；该入口的完整页面验证仍未通过。公共装配另经原生伙伴战斗工程验证，未调整来源清单或绕过校验。

验证装配工具只负责隔离工程的依赖准备；模拟输入、界面画面与完整游戏流程仍按所属功能页验证，不以工具检查替代。

## 尚未实现

- 从启动、创角进入主城／地图、完成战斗、退出并冷启动恢复的完整 Player 流程。
- 尚缺的原生资源、脚本、渲染及第三方依赖接入；具体缺口见所属功能／地图页。
- 其余未完成功能／地图在完整实现与验证后的 Windows 发布。

## 代码与资源入口

[ProjectTools.cs](../../unity/Assets/Game/Editor/ProjectTools.cs) · [WindowsBuildNamingPolicy.cs](../../unity/Assets/Game/Editor/WindowsBuildNamingPolicy.cs) · [unity-tools.json](../../unity-tools.json) · [unity.ps1](../../scripts/unity.ps1)。共用验证、窗口定位及发布操作见[素材管理](../asset-management.md)。
