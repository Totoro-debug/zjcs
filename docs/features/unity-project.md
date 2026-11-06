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

## 自定义实现

Windows 使用本地启动和可缩放竖屏窗口；游戏资源、规则与存档由本地工程提供。

## 尚未实现

- 从启动、创角进入主城／地图、完成战斗、退出并冷启动恢复的完整 Player 流程。
- 尚缺的原生资源、脚本、渲染及第三方依赖接入；具体缺口见所属功能／地图页。
- 与已提交代码和资源一致的逐功能／地图 Windows 发布。

## 代码与资源入口

[ProjectTools.cs](../../unity/Assets/Game/Editor/ProjectTools.cs) · [unity-tools.json](../../unity-tools.json) · [unity.ps1](../../scripts/unity.ps1)。共用验证、窗口定位及发布操作见[素材管理](../asset-management.md)。
