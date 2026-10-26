# 地图10：初始岛

[总索引](../README.md)

状态：未完成

## 地图简介

Map10 已接入固定版官方地图定义、导航网格、原生地形网格，以及地图战斗实体所需的 11 个官方怪物 idle 模型与动画帧，共 309 帧。另已提取地图引用的 21 个官方阶段、13 个波次阵形、对应战场网格，以及按官方阵形、怪物组、等级属性和技能道具配置展开的战斗编队。角色进入地图、实体交互、剧情、战斗、HUD 和存档恢复尚未串成可玩流程。

## 完整还原部分

地图公共能力由 `OfficialMainlineMapRuntime` 组装 HUD、区域／雾、四类雕像、导航、体力、探寻、小地图七个独立状态模块；本图资源、剧情和进度按地图 ID 注入。模块边界、共享规则与未接入项见[主线地图公共模块](../features/mainline-map-modules.md)。按地图 ID 装载本图资源，沿用本图存档与回执。

固定 APK 转换出的 Map10 定义和数据来源位于 [`unity/Assets/Official/World/Map/10`](../../unity/Assets/Official/World/Map/10/)。`OfficialMap10TerrainView` 读取官方地形顶点载荷并绑定 1,404 个批次及 166 张原始纹理；`OfficialMap10MonsterView` 按原始骨骼采样帧播放官方模型。`scripts/prepare_unity_map10_rendering.py` 校验地形、模型、骨骼序列和纹理来源哈希。Map10 的 11 个战斗展示怪物共有 309 帧 idle 动画，按官方地图实体 `Stages` 映射至模型 ID。`OfficialMap10MiniMapView` 从固定版官方 geometry 和五张原始纹理构建九个小地图批次，`scripts/prepare_unity_map10_minimap.py` 校验固定 APK 来源、输入哈希和资源绑定。`scripts/prepare_unity_map10_battle_data.py` 从固定 APK 提取 21 个阶段（20 个实体阶段和切换阶段 9910）、13 个阵形及战场网格，并按 `StageMonsterArrayInfoParser.GenerateMonsters` 展开各波怪物；属性使用官方 `BattleFormulaHandler.CalcMonsterBattleProps` 与固定 `battle-rules.json` 等级表，技能道具通过 `TbSkillInfo` 映射到 `skill-rules.json` 战斗实体。首段 stage 1033（GameId 1000）由阵形 1205 展开为三只 3281，站位为 (10,13)、(12,12)、(11,13)，技能 14235/14236 分别映射至实体 18140/18150。固定版本、公式源码、输入输出哈希记录于 [`map10-battles-source-manifest.json`](../../assets/reference/official-map/map10-battles-source-manifest.json)。Unity 检查入口为 [`OfficialMap10TerrainTools.cs`](../../unity/Assets/Game/Editor/OfficialMap10TerrainTools.cs) 和 [`OfficialMap10Tools.cs`](../../unity/Assets/Game/Editor/OfficialMap10Tools.cs)，分别验证地形/怪物载荷及原生小地图、官方地图 catalog；官方 60×60 导航、移动掩码 65、最大寻路步数 27 及本地相邻格路径检查仍保留在 `OfficialMap10Tools`。这些编队数据尚未由 Map10 运行时装配为战斗检查点。原始 Android `MapGround_10` / `MiniMap_10` prefab 因平台与 CAB 依赖未直接用于 Windows。

`OfficialMap10EnvironmentView` 在 Map10 运行时实例化 4 个官方水面网格，并将法线、焦散、泡沫、交界噪声、岸线遮罩及原材质纹理缩放绑定到水效材质；15 只蝴蝶使用官方 Spine 网格帧、`loop1`/`loop2` 动画和场景飞行/旋转曲线。`scripts/prepare_unity_map10_rendering.py` 由固定版配置、材质来源清单和原始纹理生成运行载荷；`rendering-source-manifest.json` 记录输入/输出 SHA256、材质属性、贴图绑定与重建 Shader 哈希。固定版只有 Android GLES 编译字节码，没有可移植 Windows Shader；经用户确认，`Map10Water.shader` 与 `Map10Butterfly.shader` 按官方材质参数、关键字和贴图重建，保留这些来源值，但不保证与 GLES 逐像素相同。`OfficialMap10TerrainTools.ValidateEnvironment` 在 Unity 中核对 4 个水面、15 个蝴蝶、2 段动画和 19 个运行时渲染器。

`OfficialWorldMapAssetCatalog` 与 `OfficialWorldMapAssetProvider` 拆分到同名脚本，修复 Unity Resources 目录资产的脚本绑定；`OfficialMap10Tools.Validate` 同时核对运行时目录加载。地图战斗在官方 `EvaluateProps().OutOfFightBattleProps` 流程中接入装备目录，避免使用未转换的 Base 属性作为最终战斗属性。`OfficialMap10PlayModeAcceptance.Run` 提供隔离入图、HUD、对话推进、实体和环境载荷检查入口；该入口尚未成功完成运行验收。

`OfficialMap10UiTools` 保留原 Sprite 输入哈希、尺寸、PPU、轴心／边框检查与显式目录绑定；`StreamingAssets/Map10Audio` 保存43个原剧情语音／演出／BGM事件的本地音频与映射，输入切片和输出哈希由 `map10-audio-source-manifest.json` 记录。音频来源、三段 NPC 演出及小地图转换的五项 Python 检查通过。运行数据／对话来源检查仍各有一项失败：现有 `assets/official/adventure.json` 实际 SHA256 为 `a6820d65b7498af98f242ab797c24f5dc92646b0638f09da8331c793a0340b4e`，原清单期望 `84b92c87a8cde16a1f1cb377ca8c03629ed2e4f522af04637d5db2e260f90aec`；未改动该输入或预期哈希。结构与原对话几何检查可通过，不据此声明全部来源检查或地图音频实际播放通过。

Windows D3D11 构建已复现并修复水面 shader 的 `TRANSFORM_TEX` 复合参数展开错误：对传入的 UV 表达式整体加括号，避免 `.xy` 被应用到末尾标量。保留原有参数、纹理及 UV 计算表达式；原生重建文件的输出哈希同步到 `rendering-source-manifest.json.environment.water.material.reconstructionSha256`。Windows Player 构建通过且无 shader 编译错误；该项不代表地图水面动态画面与完整路线已验收。

## 自定义实现

`OfficialMainlineMapRuntime.RunStoryGraph` 对 `RemoveMapExclusiveNpcNode` 只保留一个分支：配置删除 playable 时先播放，再删除 NPC 并沿 `Exit` 继续；未配置时直接删除。执行顺序依据官方 `RemoveMapEntitiesByCfgNode.PlayPlayables/OnPlayableStop`。工程启用 Unity 内置 `unitywebrequest` 与 `unitywebrequestaudio` 模块，供 `OfficialMap10AudioPlayer` 从本地文件 URI 解码音频；模块启用不代表音频播放已验收。

地形与怪物动画使用官方原始网格、纹理、骨骼帧数据，经 Unity MeshRenderer 适配为 Windows 原生资产；未自绘或用静态图替代。现有本地地图进度 DTO、地图寻路和战斗执行器尚未由 Map10 运行时调用。

## 尚未实现

- 小地图玩家标记与区域遮罩、WorldPanel 和实体 UI 的 native 资源接线、Map10 场景 catalog 与摄像机绑定待完成。
- 82 个放置实体的原始模型/状态绑定、角色外观换装、寻路移动、点击交互及地图进度存档待接入。
- `FirstMeetChaos`、地图教学、其余官方剧情、对话与镜头演出待接入。
- 怪物属性、技能和编队已按官方表生成，但尚未接入 Map10 战斗检查点、角色模型、原始战斗 HUD、步骤演出及战斗结算后的阶段/奖励推进；已确认首段剧情战斗即使失败也按成功继续。
- 新角色至地图出口的完整路线、重新载入后的状态恢复、Windows 实际运行和画面验收未完成。Map10 相关 Python 测试 24 项通过；Unity 编辑器已通过水面/蝴蝶载荷、shader 导入与 19 个运行时渲染器构造检查。最新战斗属性接线尚未通过 Unity 复验，完整 Play Mode 仍未验收。
