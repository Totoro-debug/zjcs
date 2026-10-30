# 15 通天塔入口

[总索引](../README.md)

状态：未完成

## 地图简介

15 通天塔入口，范围按下列原配置与本地决策维护。

## 原始官方实现

原目录：通天塔/群落/通天塔入口。场景类型：`map_world`；原区域配置1项，BGM配置15。

原地形：`Map_Tower15_SnowFloor1/MapGround_Tower15_SnowFloor1`。 后继地图：`42001`。

871 客户端／Common 的 `MapRegionInfo.IsUnlockTimeReached` 按对应位阶实际解锁时间加 `LimitServerRankUnlockDayOffset` 计算开放时刻；当前位阶还会应用原合服加速。入口区域原配置为服务端位阶 5、偏移 14 天。来源：[`MapRegionInfo.cs`](../../.tools/source-packages/current/871_156598/official-code/common-source/Common/MapRegionInfo.cs)、[`MapUtils.cs`](../../.tools/source-packages/current/871_156598/official-code/common-source/MapUtils.cs)。离线存档没有服务端位阶开放时间与合服记录，因此按用户确认保留位阶 5 和 14 天时长，改为玩家访问入口后手动发起本地倒计时；不应用合服加速，也不从角色创建日或位阶升级时自动计时。

## 完整还原部分

本图仍未完成全部楼层与机关迁移；本次已完成入口地图的 Unity 运行时数据接入、原生第一层地形绑定、26 个入口实体目录、区域进度、导航和本地位置存档。

`OfficialMainlineMapRuntime` 现在注册 Map15，并从 `OfficialMapDefinition` 读取 25×30 官方导航网格、入口 101 和 26 个放置实体；地图在存档已解锁时可通过共享宿主进入，地图对象、实体点击、路径移动和冷读位置恢复沿用同一套主线运行时。

地图组静态数据已打包到 `unity/Assets/Game/Resources/Map15/Group/`：60 张地图各自保留原地图行、区域行、导航／放置数据和放置实体组件，另有共享 82 种 EC 模板；合计 118 区、2,899 个放置和 23,291 个原始组件。其 adventure 表格输入取自 `871_156598`，该版本以 `871_156598` 为基线，更新记录未包含 map／map_region 表；实体模板仍来自 APK `871_156598`。目录索引保留官方后继地图、地形路径和 BGM，来源及输入／输出哈希见 [`map15-group-data-source-manifest.json`](../../assets/reference/official-map/map15-group-data-source-manifest.json)。入口组数据已由 `prepare_unity_map15_runtime_data.py` 转换为共享地图定义和实体目录；完整 60 张后继地图仍只保留静态目录，尚未逐图接入可切换运行时。

`OfficialMap15GroupDataCatalog` 从 Unity `Resources` 读取目录、地图放置和 EC 模板数组，校验版本、入口后继、区域数、放置／组件闭合及模板引用。原 EC 组件内容保存在逐组件 JSON 字符串中供后续官方逻辑绑定；该数据访问层尚未创建场景对象，也未接入交互、战斗或存档。

官方美术源已切换到用户指定的最新版 `871_156598`。其官方 YooAsset 清单及缓存包含 29 个通天塔 prefab／Map15 地形根 bundle；加入下列 14 个交互／界面 bundle 后，CAB 依赖检查解析 1,196 个序列化文件，未发现缺失引用。AssetRipper 1.3.14 已将闭包导出为 27,037 个对象。GUID 分析覆盖 1,041 个动态 prefab／场景根，包括 Map15 楼层和 Bronze／Snow／Utopia 地形、`Scenes/map_world.unity`、459 个 WorldMap UI prefab、确认框／小游戏 UI 及 ActionWorld prefab；闭合到 2,720 个源文件（约 157 MB），未发现未解析资源 GUID。完整场景／UI闭包涉及 226 个 MonoScript，尚未整体绑定或接入运行时。

14 个官方地形根的 871 原生依赖已闭合到 794 个文件，其中 31 个 MonoScript 绑定到已迁移的官方脚本／Unity package，10 个 shader 绑定到可验证的 Windows 资源；753 个 Unity 资产已迁移到 `unity/Assets/Official/World/Map/15/NativeTerrain/` 并导入 Unity 工程。其中两个官方透明遮罩 shader 按 `defaultpackage_unityshaders.bundle` 中的 GLES3 原程序转换，分别保留两个 Sprite 光照变体和 `TransparentMask` 渲染 pass。资源来源、转换器、程序变体、绑定及逐文件输出哈希见 [`map15-native-terrain-source-manifest.json`](../../assets/reference/official-map/map15-native-terrain-source-manifest.json)。正式工程 Unity shader 编译结果与实际地形渲染尚未通过验收，故此项不代表地形已通过运行验证。

用户更正的 14 个资源名已按 `871_156598` 的 DefaultPackage 清单及本地 CDN 收据核验：通天塔解谜 action prefab、`confirmdialog`／`minigame`／`worldmap` UI prefab、`map_world` 场景、8 个共享 bundle，以及 `map_14_play` 实体 prefab 均在清单且缓存字节校验通过；43 个 Map15／交互根 bundle 的 CAB 输入另存于隔离验证目录的版本收据。Map14 实体 prefab 保留为跨地图参考，不纳入 Map15 运行根。官方 UI 与场景已经完成原生 AssetRipper 导出和 GUID 资源闭合分析，但尚未绑定工程脚本、恢复 Windows 平台资源或接入运行时。

已按 `871_156598` 的固定源码哈希迁移 `Pond`、`WaterMaskGenerator`、`WaterConfig` 与 `TransparentMask` 到官方 `NativeWater` 代码目录；`Pond`、`WaterMaskGenerator` 加命名空间引用以调用现有官方 `WaterMaskRenderer`。`TransparentMask` 保留原逻辑，并适配 Unity 2022 的 `UnityEngine.Object`、剔除参数输出和 `RenderTargetIdentifier` 构造 API。`Pond`、`WaterMaskGenerator`、`TransparentMask` 的原始 MonoScript GUID 已映射到迁移脚本 meta；输入／输出哈希及绑定见 [`map15-water-script-source-manifest.json`](../../assets/reference/official-map/map15-water-script-source-manifest.json)。这些代码尚未在新 Map15 prefab 中重绑 MonoScript，水面／池塘材质、mask 贴图、排序层和窗口尺寸行为也尚未实际验证。

当前已执行地图组生成器回归测试、官方 EC 闭合校验、871 原生地形输出哈希回归测试和 `check_unity_project.py` 静态元数据检查。Unity 隔离工程已完成 C# 编译；编译期间还发现并修复 `Region` 嵌套类型与官方字段同名导致的 CS0542。随后首次验收因稀疏 worktree 缺少仓库级战斗 HUD 来源清单而未进入验证；补入该清单后，Unity 在 Burst 程序集重载时报告 `OutOfMemoryException` 并退出，`ProjectTools.Validate` 与 Map15 Play Mode 断言均未完成。正式工程当前仍有其他地图验证会话占用，Map15 shader 编译、实际场景交互、运行画面和 Windows Player 验证尚未执行。

## 自定义实现

按用户明确确认，沿用龙之国的单机自动解谜方案。固定版客户端／Common 的 `StageResultAutoPuzzleComponentImplement`、`StageResultResetPuzzleComponentImplement` 及相关请求保留目标、费用和结果标记，缺少服务端执行器；本地适配不标为原服务端逻辑。依据为 [`MyStageResultAutoPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultAutoPuzzle.cs)、[`MyStageResultResetPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultResetPuzzle.cs) 及对应 Common 标记组件。通天塔 5 处火炮终端只清除原 `NeedRemoveLoEntityIds`，2 处特殊锁及 5 处普通锁终端只解开原 `LockLoEntityIds`，使用原交互费用（道具 325×1），不补发炮、失败演出或奖励。重置仅恢复指定目标，保留其他状态、背包、奖励凭据和角色位置；原配置仅 4 个终端有重置分支，保留其余终端的原菜单，不新增重置按钮。

离线入口规则按用户确认自定义为“达到位阶 5 后可手动发起；发起时写入本地倒计时起点，经过 14 天开放”。计时起点与开放状态进入现有主线 JSON 存档；位阶升级、入口出现记录和读取入口状态都不会自动开始倒计时。暂不应用服务端合服加速。`OfflineMainlineProgressSession.StartMap15Preparation`、`GetMap15PreparationState` 与 `RefreshMap15Preparation` 承载该规则；旧存档中已开放的状态保留为已开放。Map14 入口实体 103 已接入官方 `Map14/UI/CostItemConfirmDialog`：访问入口会显示位阶要求或剩余天数，达到位阶 5 且未开始时可手动开始本地 14 天倒计时；已开放状态当前提示重新进入地图，Map14 到 Map15 的直接场景切换仍未完成。

六处遗迹沿固定客户端／Common `WorldRuinDialog`、`GameRuinEnterNextStageDialog`、`GameNextStageUIHandler`、`FightRuinConfigComponentImplement`、`StageBehStageRuinComponent` 与 RPC 10160 核对原 20 层配置、战斗与奖励数据；服务端推进／结算执行器未取得。按用户明确确认的离线方案，进入后立即执行当前未探索层，沿原 `StageId`／层序推进；战斗事务挂起并回放，战败保留当前层供重试。胜利后按原 `GameNextStageUIShowDelayTime=0.8` 秒返回层推进或奖励阶段。奖励只按原追加物配置领取本层奖励；最后一层原奖励结算后标记已探索，退出保留已领取奖励及进度，不增加额外奖励或随机规则。遗迹 17–19 的层配置原 `MapClassId=42062` 而放置于 42063，配置不改写。经用户明确确认的背景修正只把两条 `EnterNextStageUIBgImg` 的加载映射到固定版同名 `BigMapStageBg` 原图，原始配置仍保留 `MapStageBg` 路径。

资格、位置与部分机关标记是明确的测试夹具，不代表普通角色游玩进度。

手动火炮采用用户明确确认的单机处理：读取原 `ActionBulletComponent` 的速度、10 格上限和可飞地形，按当前镜面 `TurnInfo` 转向，命中后执行原目标阶段，遇障碍停止。固定版 `MyActionFireBullet`／Common 请求未包含服务端碰撞执行器，故不将本地碰撞规则标为原服务端实现。每次成功事务只保留一份有界发射记录，保存失败时随目标、任务和背包全部回滚；加载存档不再次执行命中。在明确的资格、相邻前置机关及镜面状态夹具下，检查五处原炮台场地的全部 11 座火炮、9 次真实目标命中、21 段镜面转向和 JSON 保存／拒绝保存回滚，共 181 项通过；这不是连续游玩路线。原炮弹、炮口、镜面受击、震屏及四个原音效需接入。

用户已明确允许炮弹／炮口的 11 个、元素连线的 39 个原 `ParticleSystem.NoiseModule` 发射器采用现有 FastNoiseLite 采样，共 50 个。原 bundle 参数、GLES 程序和固定 APK 的 `libunity.so` 已核对，未确定该原生模块内部的三维粒子噪声算法；此适配保留原素材、频率、强度、滚动、曲线及阻尼，仅噪声采样使用本地方案，粒子扰动轨迹与 Unity 存在差异。此授权只涉及上述 50 个发射器，不把 FastNoiseLite 标为原算法；镜头震屏仍使用已精确迁移的原 `Mathf.PerlinNoise`。

## 尚未实现

将运行时数据访问层接入其余逐图场景、区域开放、实体和交互，迁移 81 条主线；入口与幻想阶梯分开，六处遗迹、各楼层连接、小地图、原地形／灯光／水面逐图绑定与验证。

按原地图配置恢复场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口；原 prefab、骨骼、材质、粒子、动画及音频需在 Unity / Windows 逐项恢复并绑定。

- 完成本图独立的本地资源绑定、直接地图切换、正常游玩、战斗／交互结算、失败回滚和完整楼层退出流程。
- 在 Unity 编辑器实际运行，并验证 Windows 目标表现；

## 代码与资源入口

[地图组数据生成器](../../scripts/prepare_unity_map15_group_data.py)、[入口运行时数据生成器](../../scripts/prepare_unity_map15_runtime_data.py)、[固定来源实体闭合审计](../../scripts/verify_map15_entities.py)、[Unity 地图组运行时目录](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap15GroupDataCatalog.cs)、[主线地图运行时](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs)、[地形原生迁移生成器](../../scripts/prepare_unity_map15_native_terrain.py)、[地形来源清单](../../assets/reference/official-map/map15-native-terrain-source-manifest.json)、[地形输出哈希回归测试](../../scripts/test_prepare_unity_map15_native_terrain.py)、[闭合分析回归测试](../../scripts/test_prepare_unity_map15_native_assets.py)、[Play Mode 数据验收入口](../../unity/Assets/Game/Editor/Map15/OfficialMap15GroupDataAcceptance.cs)、[Unity 地图组索引](../../unity/Assets/Game/Resources/Map15/Group/group-catalog.json)、[官方来源清单](../../assets/reference/official-map/map15-group-data-source-manifest.json)

[原机关与场景配置](../../assets/official/map15-cannons.json)、[原元素连线配置](../../assets/official/map15-element-link.json)、[原遗迹配置](../../assets/official/map15-ruins.json)、[原界面配置](../../assets/official/map15-ui.json)

[原地形／环境来源清单](../../assets/reference/official-map/tower-environment-source-manifest.json)、[原界面来源清单](../../assets/reference/official-map/tower-ui-source-manifest.json)、[通天塔地图数据](../../assets/official/regions/15.json)、[`MyStageResultAutoPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultAutoPuzzle.cs)、[`MyStageResultResetPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultResetPuzzle.cs)
