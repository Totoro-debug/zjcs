# 地图10：初始岛

[总索引](../README.md)

状态：未完成

## 地图简介

Map10 已接入固定版官方地图定义、导航网格、原生地形网格，以及地图战斗实体所需的 11 个官方怪物 idle 模型与动画帧，共 309 帧。另已提取地图引用的 21 个官方阶段、13 个波次阵形及对应战场网格数据。角色进入地图、实体交互、剧情、战斗、HUD 和存档恢复尚未串成可玩流程。

## 完整还原部分

固定 APK 转换出的 Map10 定义和数据来源位于 [`unity/Assets/Official/World/Map/10`](../../unity/Assets/Official/World/Map/10/)。`OfficialMap10TerrainView` 读取官方地形顶点载荷并绑定 1,404 个批次及 166 张原始纹理；`OfficialMap10MonsterView` 按原始骨骼采样帧播放官方模型。`scripts/prepare_unity_map10_rendering.py` 校验地形、模型、骨骼序列和纹理来源哈希。Map10 的 11 个战斗展示怪物共有 309 帧 idle 动画，按官方地图实体 `Stages` 映射至模型 ID。`scripts/prepare_unity_map10_battle_data.py` 从固定 APK 的 `TbStageInfo` 和 `TbStageMonsterArrayInfo` 提取 20 个实体阶段及 Map10 切换阶段 9910，并打包 13 个所需阵形和战场网格；固定版本和输入输出哈希记录于 [`map10-battles-source-manifest.json`](../../assets/reference/official-map/map10-battles-source-manifest.json)。Unity 检查入口为 [`OfficialMap10TerrainTools.cs`](../../unity/Assets/Game/Editor/OfficialMap10TerrainTools.cs)，验证地形批次、纹理和怪物帧载荷；官方 60×60 导航、移动掩码 65、最大寻路步数 27 及本地相邻格路径检查仍保留在 `OfficialMap10Tools`。原始 Android `MapGround_10` / `MiniMap_10` prefab 因平台与 CAB 依赖未直接用于 Windows。

## 自定义实现

地形与怪物动画使用官方原始网格、纹理、骨骼帧数据，经 Unity MeshRenderer 适配为 Windows 原生资产；未自绘或用静态图替代。现有本地地图进度 DTO、地图寻路和战斗执行器尚未由 Map10 运行时调用。

## 尚未实现

- 官方水面动态 shader、泡沫/焦散/波纹和蝴蝶动画尚未接入 Windows 运行时。
- 原始 MiniMap、WorldPanel 和实体 UI 的 native 资源接线、Map10 场景 catalog 与摄像机绑定待完成。
- 82 个放置实体的原始模型/状态绑定、角色外观换装、寻路移动、点击交互及地图进度存档待接入。
- `FirstMeetChaos`、地图教学、其余官方剧情、对话与镜头演出待接入。
- 已提取地图阶段、阵形和战场网格，但地图实体本地战斗编队、官方角色/怪物属性与技能、原始战斗 HUD、战斗结算后的阶段/奖励推进待接入；已确认首段剧情战斗即使失败也按成功继续。
- 新角色至地图出口的完整路线、重新载入后的状态恢复、Windows 实际运行和画面验收未完成。
