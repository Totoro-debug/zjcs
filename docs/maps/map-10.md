# 地图10：初始岛

[总索引](../README.md)

状态：未完成

## 地图简介

初始岛使用主线地图公共运行时和公共战斗执行器。地图特有的交互、剧情和完整存档流程由本页维护；共享规则见[主线地图公共模块](../features/mainline-map-modules.md)与[世界地图](../features/world-map.md)。

## 原始官方实现

原地图包含导航与区域状态、地形、水面、蝴蝶、地图实体、剧情、语音、音乐和战斗关卡。Windows 运行使用已转换的本地资源及 Unity 视图组件；来源版本与资源处理记录见[来源总表](../../assets/reference/SOURCES.md)。

## 完整还原部分

- `OfficialMainlineMapRuntime` 已接入公共地图状态；Map10 使用 60×60 导航网格和五个地区，揭格与实体发现记录进入角色地图进度。
- `OfficialMap10TerrainView`、`OfficialMap10MonsterView` 与 `OfficialMap10EnvironmentView` 加载本图地形、怪物动画、水面和蝴蝶资源。
- `OfficialMap10AudioPlayer` 读取本地音频映射，支持地图音乐、语音与剧情音效。
- 官方战斗关卡、阵形与实体数据已有本地目录；本图的完整地图遭遇、事件和剧情战斗流程尚未全部接入。

## 自定义实现

- 缺少可移植的官方 GLES 着色器时，水面与蝴蝶使用按原材质参数和纹理恢复的 Windows Shader；不保证与 GLES 逐像素一致。
- `RemoveMapExclusiveNpcNode` 在配置了 playable 时先播放，再移除 NPC 并继续 Exit 分支；未配置时直接移除。
- 地图公共状态、存档恢复和单机差异见[主线地图公共模块](../features/mainline-map-modules.md)，不在此重复。

## 尚未实现

- 地图场景、摄像机、完整 HUD／小地图输入及原生面板尚未全部接入。
- NPC 与放置实体的模型状态、寻路移动、点击交互和事件触发仍有缺项。
- 首次剧情、地图教学、对白与镜头演出，以及路线战斗检查点、奖励和读档恢复尚未形成完整流程。
- 本图尚未完成 Unity Play Mode 全流程验证和 Windows Player 发布。

## 代码与资源入口

[OfficialMainlineMapRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs)、[OfficialMap10TerrainView.cs](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap10TerrainView.cs)、[OfficialMap10MonsterView.cs](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap10MonsterView.cs)、[OfficialMap10EnvironmentView.cs](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap10EnvironmentView.cs)、[OfficialMap10AudioPlayer.cs](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap10AudioPlayer.cs)。

本图官方资源来源见[来源总表](../../assets/reference/SOURCES.md)；公共地图与战斗规则分别见[主线地图公共模块](../features/mainline-map-modules.md)和[战斗执行](../features/battle-execution.md)。
