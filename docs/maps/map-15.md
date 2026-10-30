# 15 通天塔入口

[总索引](../README.md)

状态：未完成

## 地图简介

15 通天塔入口，范围按下列原配置与本地决策维护。

## 原始官方实现

原目录：通天塔/群落/通天塔入口。场景类型：`map_world`；原区域配置1项，BGM配置15。

原地形：`Map_Tower15_SnowFloor1/MapGround_Tower15_SnowFloor1`。 后继地图：`42001`。

## 完整还原部分

尚无本图完整 Unity 游玩流程的完成结论；共享代码和转换数据不计作整图完成。

地图组静态数据已从固定版 `854_156383` 打包到 `unity/Assets/Game/Resources/Map15/Group/`：60 张地图各自保留原地图行、区域行、导航／放置数据和放置实体组件，另有共享 82 种 EC 模板；合计 118 区、2,899 个放置和 23,291 个原始组件。目录索引保留官方后继地图、地形路径和 BGM，来源及输入／输出哈希见 [`map15-group-data-source-manifest.json`](../../assets/reference/official-map/map15-group-data-source-manifest.json)。该数据尚未接入可切换地图与实体加载的运行时，不能视为地图、区域开放或实体已可游玩。

`OfficialMap15GroupDataCatalog` 从 Unity `Resources` 读取目录、地图放置和 EC 模板数组，校验版本、入口后继、区域数、放置／组件闭合及模板引用。原 EC 组件内容保存在逐组件 JSON 字符串中供后续官方逻辑绑定；该数据访问层尚未创建场景对象，也未接入交互、战斗或存档。

官方美术源已切换到用户指定的最新版 `871_156598`。其官方 YooAsset 清单及缓存包含 29 个通天塔 prefab／Map15 地形根 bundle；加入下列 14 个交互／界面 bundle 后，CAB 依赖检查解析 1,196 个序列化文件，未发现缺失引用。AssetRipper 1.3.14 已将闭包导出为 27,037 个对象。GUID 分析覆盖 1,041 个动态 prefab／场景根，包括 Map15 楼层和 Bronze／Snow／Utopia 地形、`Scenes/map_world.unity`、459 个 WorldMap UI prefab、确认框／小游戏 UI 及 ActionWorld prefab；闭合到 2,720 个源文件（约 157 MB），未发现未解析资源 GUID。闭包涉及 226 个 MonoScript；已取得的反编译类、现有官方迁移脚本与 Unity package 仍须逐类绑定和校验序列化字段。导出目前仅位于隔离验证目录，尚未筛选、转换和接入受 Git 管理的 Unity 资产；AssetRipper 输出也未核验脚本绑定、shader／平台兼容及运行表现，因此不代表原 UI 或场景已还原。

用户更正的 14 个资源名已按 `871_156598` 的 DefaultPackage 清单及本地 CDN 收据核验：通天塔解谜 action prefab、`confirmdialog`／`minigame`／`worldmap` UI prefab、`map_world` 场景、8 个共享 bundle，以及 `map_14_play` 实体 prefab 均在清单且缓存字节校验通过；43 个 Map15／交互根 bundle 的 CAB 输入另存于隔离验证目录的版本收据。Map14 实体 prefab 保留为跨地图参考，不纳入 Map15 运行根。官方 UI 与场景已经完成原生 AssetRipper 导出和 GUID 资源闭合分析，但尚未绑定工程脚本、恢复 Windows 平台资源或接入运行时。

已按 `871_156598` 的固定源码哈希迁移 `Pond`、`WaterMaskGenerator`、`WaterConfig` 与 `TransparentMask` 到官方 `NativeWater` 代码目录；`Pond`、`WaterMaskGenerator` 加命名空间引用以调用现有官方 `WaterMaskRenderer`。`TransparentMask` 保留原逻辑，并适配 Unity 2022 的 `UnityEngine.Object`、剔除参数输出和 `RenderTargetIdentifier` 构造 API。`Pond`、`WaterMaskGenerator`、`TransparentMask` 的原始 MonoScript GUID 已映射到迁移脚本 meta；输入／输出哈希及绑定见 [`map15-water-script-source-manifest.json`](../../assets/reference/official-map/map15-water-script-source-manifest.json)。这些代码尚未在新 Map15 prefab 中重绑 MonoScript，水面／池塘材质、mask 贴图、排序层和窗口尺寸行为也尚未实际验证。

当前已执行地图组生成器回归测试、官方 EC 闭合校验和 `check_unity_project.py` 静态元数据检查。Unity 隔离工程已完成 C# 编译；编译期间还发现并修复 `Region` 嵌套类型与官方字段同名导致的 CS0542。随后首次验收因稀疏 worktree 缺少仓库级战斗 HUD 来源清单而未进入验证；补入该清单后，Unity 在 Burst 程序集重载时报告 `OutOfMemoryException` 并退出，`ProjectTools.Validate` 与 Map15 Play Mode 断言均未完成。实际场景交互、运行画面和 Windows Player 验证尚未执行。

## 自定义实现

按用户明确确认，沿用龙之国的单机自动解谜方案。固定版客户端／Common 的 `StageResultAutoPuzzleComponentImplement`、`StageResultResetPuzzleComponentImplement` 及相关请求保留目标、费用和结果标记，缺少服务端执行器；本地适配不标为原服务端逻辑。依据为 [`MyStageResultAutoPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultAutoPuzzle.cs)、[`MyStageResultResetPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultResetPuzzle.cs) 及对应 Common 标记组件。通天塔 5 处火炮终端只清除原 `NeedRemoveLoEntityIds`，2 处特殊锁及 5 处普通锁终端只解开原 `LockLoEntityIds`，使用原交互费用（道具 325×1），不补发炮、失败演出或奖励。重置仅恢复指定目标，保留其他状态、背包、奖励凭据和角色位置；原配置仅 4 个终端有重置分支，保留其余终端的原菜单，不新增重置按钮。

六处遗迹沿固定客户端／Common `WorldRuinDialog`、`GameRuinEnterNextStageDialog`、`GameNextStageUIHandler`、`FightRuinConfigComponentImplement`、`StageBehStageRuinComponent` 与 RPC 10160 核对原 20 层配置、战斗与奖励数据；服务端推进／结算执行器未取得。按用户明确确认的离线方案，进入后立即执行当前未探索层，沿原 `StageId`／层序推进；战斗事务挂起并回放，战败保留当前层供重试。胜利后按原 `GameNextStageUIShowDelayTime=0.8` 秒返回层推进或奖励阶段。奖励只按原追加物配置领取本层奖励；最后一层原奖励结算后标记已探索，退出保留已领取奖励及进度，不增加额外奖励或随机规则。遗迹 17–19 的层配置原 `MapClassId=42062` 而放置于 42063，配置不改写。经用户明确确认的背景修正只把两条 `EnterNextStageUIBgImg` 的加载映射到固定版同名 `BigMapStageBg` 原图，原始配置仍保留 `MapStageBg` 路径。

资格、位置与部分机关标记是明确的测试夹具，不代表普通角色游玩进度。

手动火炮采用用户明确确认的单机处理：读取原 `ActionBulletComponent` 的速度、10 格上限和可飞地形，按当前镜面 `TurnInfo` 转向，命中后执行原目标阶段，遇障碍停止。固定版 `MyActionFireBullet`／Common 请求未包含服务端碰撞执行器，故不将本地碰撞规则标为原服务端实现。每次成功事务只保留一份有界发射记录，保存失败时随目标、任务和背包全部回滚；加载存档不再次执行命中。在明确的资格、相邻前置机关及镜面状态夹具下，检查五处原炮台场地的全部 11 座火炮、9 次真实目标命中、21 段镜面转向和 JSON 保存／拒绝保存回滚，共 181 项通过；这不是连续游玩路线。原炮弹、炮口、镜面受击、震屏及四个原音效需接入。

用户已明确允许炮弹／炮口的 11 个、元素连线的 39 个原 `ParticleSystem.NoiseModule` 发射器采用现有 FastNoiseLite 采样，共 50 个。原 bundle 参数、GLES 程序和固定 APK 的 `libunity.so` 已核对，未确定该原生模块内部的三维粒子噪声算法；此适配保留原素材、频率、强度、滚动、曲线及阻尼，仅噪声采样使用本地方案，粒子扰动轨迹与 Unity 存在差异。此授权只涉及上述 50 个发射器，不把 FastNoiseLite 标为原算法；镜头震屏仍使用已精确迁移的原 `Mathf.PerlinNoise`。

## 尚未实现

将运行时数据访问层接入逐图场景、区域开放、实体和交互，迁移81条主线；入口与幻想阶梯分开，六处遗迹、各楼层连接、小地图、原地形／灯光／水面逐图绑定与验证。

按原地图配置恢复场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口；原 prefab、骨骼、材质、粒子、动画及音频需在 Unity / Windows 逐项恢复并绑定。

- 完成本图独立的本地资源绑定、玩家入口、正常游玩、战斗／交互结算、失败回滚、退出与冷读恢复。
- 在 Unity 编辑器实际运行，并验证 Windows 目标表现；

## 代码与资源入口

[地图组数据生成器](../../scripts/prepare_unity_map15_group_data.py)、[固定来源实体闭合审计](../../scripts/verify_map15_entities.py)、[Unity 地图组运行时目录](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap15GroupDataCatalog.cs)、[官方原生资产 GUID 闭合分析器](../../scripts/prepare_unity_map15_native_assets.py)、[闭合分析回归测试](../../scripts/test_prepare_unity_map15_native_assets.py)、[Play Mode 数据验收入口](../../unity/Assets/Game/Editor/Map15/OfficialMap15GroupDataAcceptance.cs)、[Unity 地图组索引](../../unity/Assets/Game/Resources/Map15/Group/group-catalog.json)、[官方来源清单](../../assets/reference/official-map/map15-group-data-source-manifest.json)

[原机关与场景配置](../../assets/official/map15-cannons.json)、[原元素连线配置](../../assets/official/map15-element-link.json)、[原遗迹配置](../../assets/official/map15-ruins.json)、[原界面配置](../../assets/official/map15-ui.json)

[原地形／环境来源清单](../../assets/reference/official-map/tower-environment-source-manifest.json)、[原界面来源清单](../../assets/reference/official-map/tower-ui-source-manifest.json)、[通天塔地图数据](../../assets/official/regions/15.json)、[`MyStageResultAutoPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultAutoPuzzle.cs)、[`MyStageResultResetPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultResetPuzzle.cs)
