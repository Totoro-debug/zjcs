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

当前 Windows 版本已发布到 `D:/Desktop/杖剑传说/杖剑传说.exe`，桌面快捷方式指向该入口。交付文件已与提交后的代码、资源所构建的验证包逐项核对 SHA-256，并从正式路径启动，完成模拟输入与进程重启检查，全程使用隔离存档。运行覆盖及现有缺项分别见[日常副本](dungeons.md)、[伙伴绑定](partners.md)、[共享成长计算](character.md#属性来源与共享计算边界)、[主线公共模块](world-map.md#主线地图公共模块)和[存档选择](settings-and-save.md#启动存档选择界面)，不代表完整游戏流程完成。

## 自定义实现

Windows 使用本地启动和可缩放竖屏窗口；游戏资源、规则与存档由本地工程提供。

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

[ProjectTools.cs](../../unity/Assets/Game/Editor/ProjectTools.cs) · [unity-tools.json](../../unity-tools.json) · [unity.ps1](../../scripts/unity.ps1)。共用验证、窗口定位及发布操作见[素材管理](../asset-management.md)。
