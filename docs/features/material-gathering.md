# 素材秘境与快速采集

[总索引](../README.md)

状态：未完成

## 功能简介

游记素材秘境包含铁石矿洞、罗拉金矿、历战之地、时光沙海四类采集场。对应官方秘境类型为2、4、1、3；解锁条件是该类 `UnlockStages` 中任一 Type 18 秘境战斗关卡有通关记录。

当前官方配置载荷、离线事务服务和主城游记入口已接入。采集面板由 871 热更新导出的官方 prefab 显示，单次、快速和宝箱按钮已绑定本地事务；首次采集可进入，可计数材料被击破时会在同一事务中推进对应秘境的 Type 18 解锁关卡计数，均为用户确认的离线规则。完整动作表现和本功能 Play Mode 验证仍未完成。

## 原始官方实现

素材秘境读取原采集类型、工具、次数、关卡、奖励与 MopUp 表现；原动作事件、工具挂点和切换演出随配置选择。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

来源链：MopUpPerformer.mApeendSkins → AppearanceAddressUtils.GetPlayerPartToolPrefabPath → 原 Assets/Prefabs/Appearance/PlayerPart/workTool/M_gaoZi_R_01.prefab；AppearanceEffectPart／SkeletonGraphicFollower 指向 pWeapon_R2，按 W_jian_R/W_jian_R 的 Map_GaoZi 附件决定显隐。保留原 -90° 子变换、局部缩放和层序。原 workTools.skel／atlas 位于 character_worktools.bundle，真实 M_gaoZi.png 由 character_worktools_default.bundle 提取；没有将工具名当作角色皮肤或自行绘制工具。

prepare_gathering_actions.py 从三类材料原 SkeletonAnimReferenceAsset 取得 21 个角色动作，沿男女原 SkeletonData 的拆分动画引用取回，复用官方职业武器的跟随网格转换器按 24 fps 转换。assets/official/gathering/ 保存男女动作及工具贴图；assets/reference/dungeon-ui/gathering-actions-source-manifest.json 登记原 CDN URL、bundle／对象、脚本与输入／输出 SHA256。原皮肤、外观部件、UV、颜色、网格和附件显示条件保持。运行时先接入各材料真实待机与工具；攻击／开箱动作的队列和转场接线另行完成，不把已转换动作等同于全部表现接通。

`prepare_gathering_motion.py` 从固定854_156383的 `prefabs_ui_mopup` 读取五个MopUpPanel动画的原事件，跟随49个实际使用的 `MopUpSkeletonGraphicMaterial.mSkeleton` 转换原待机、受击及消失动画。角色动作沿既有 `gathering-actions-source-manifest.json`，材质、工具及PlayerPos沿原绑定迁移；新增本地网格位于 `assets/official/gathering/targets/`。输入包、对象、原代码及输出哈希见 [gathering-motion-source-manifest.json](../../assets/reference/dungeon-ui/gathering-motion-source-manifest.json)，未使用自绘或近似动作。

`prepare_gathering_feedback.py` 接入同包的 `MopUpFloatingIconItem`、`MopUpFloatingIconItemCrit`、`MopUpFloatingIconItemQuick` 三套原奖励飘字预制体及原完成事件。普通／暴击数字复用已转换的 DamageNumber、CritDamageNumber 原位图字形和图集，另四张装饰贴图逐字节核验后复用现有资源；没有新增或自绘图片。原版本、URL、prefab、组件、曲线、字体对象、图集输出和原C#代码哈希在 的 `data.source` 中随生成绑定保存；对应位图字体的原提取来源继续见 `battle-hud/source-manifest.json`。

同一配置还包含官方 `stage_map_location` 中可用的秘境解锁关卡位置。客户端按 `Player.GetStageCount` 检查逐关通关，但离线档案尚无逐关记录；53个解锁关卡中仅23个有非空区域映射，另30个为无表行或空 `RegionInfos`。Home 当前区域满进度判断是临时代理，需依功能页待确认项处理。

`scripts/prepare_material_gathering_ui_assets.py` 从当前版本 `871_156598` 的 `defaultpackage_assets_image_ui_notes_mopup.bundle` 提取68个原始 UI Sprite，输入来自 APK ZIP 中与 `854_156383` 一致的官方同名包，bundle SHA256 为 `12080a2c5a32c5e2741b792a6cd20c1b469a765a1c4afd83175f349bfa6cf7ae`。Unity 图像载荷在 `unity/Assets/Official/Gathering/UI/`，对象路径、逐图 SHA256 和来源 URL 记于 `assets/reference/dungeon-ui/gathering-ui-source-manifest.json`。

## 完整还原部分

- 当前热更新 `871_156598` 的 `mop_up_dungeon`、`mop_up_material`、攻击/击破/目标生成 `reward_rule`、`level_number` 与 `GameSettings.MopUp` 已提取到 `unity/Assets/Game/Resources/Official/Progression/material-gathering.json`。运行时由 `OfficialMaterialGatheringCatalog.LoadDefault` 校验输出 SHA256。
- 秘境工具、攻/暴击/暴伤/额外产出属性、解锁关卡、初始目标队列、目标耐久及攻/击破奖励来自官方表；伤害按 `BattleFormulaHandler.GetMopUpDamage` 的浮动、暴击、暴伤和取整顺序计算。奖励数量保留官方阶位倍率与小数随机，再按用户确认的额外产出比例向下取整。
- `OfflineMaterialGatheringProgress` 已进入角色主存档的规范化和校验；`OfflineMaterialGatheringService.Gather` 将工具、耐久、随机状态、目标进度和背包奖励放在同一 `ApplyInventoryTransaction` 中。普通操作一次一击，快速操作最多64击，工具不足时只提交可完成的攻击，目标击破后停止当前目标；免费宝箱不扣工具并作为采集记录。
- `NewbieRewardMaterials` 初始队列结束后，目标按各秘境 `GenerateMaterialRuleId` 指向的官方 `reward_rule.IdList/WeightList` 抽取；目录校验候选材料和权重均有效。目标生成规则为13000（铁石：900/901/903，800/150/50）、13001（金矿：910/911/913，900/50/50）、13002（历战：920–927，200/200/200/200/75/75/20/30）、13003（时光沙海：930/931/933，800/150/50）。
- 主城 `TravelNotesPanel.NormalMaterialEntry` 打开官方 `MopUpEntryPanel`，卡片 `StoneEntryItem/SilverEntryItem/SkillEntryItem/SandEntryItem` 对应类型2/4/1/3；首次采集卡片可进入。`HomeHudView.MaterialGathering` 将 `OnceBtn/QuickBtn/GetBtn` 接到 `HomeFeatureRuntime.GatherMaterial`，缺少工具时禁用耗材按钮，工具和背包经离线事务扣发，写档成功才刷新界面/提示奖励。官方 AnimatorController 的 `GoAhead/GoBack` 转场分别接入攻击与目标击破/宝箱领取。FunctionType 84 按官方 dungeon ID 检查快速开放；快速设置保存到 `OfflineMaterialGatheringProgress.QuickEnabled`。
- 官方 UI 根 prefab 与非脚本依赖由 `scripts/prepare_material_gathering_assets.py` 从完整的 `871_156598` CAB 闭包迁移。两个根 prefab、19 项依赖及其哈希、三个导出引用映射记录于 `assets/reference/dungeon-ui/material-gathering-prefab-source-manifest.json`。92个导出的 MonoScript GUID 中73个在现有工程/包缓存可解析，19个确实缺脚本（含官方 `MopUpContent`、`MopUpPanel`、`MopUpPerformer`、背景/入口等）；本地界面逻辑由 Home partial 接管，尚未验证缺失脚本组件对实际渲染/状态的影响。
- 快速采集的开放状态由调用方传入；普通/快速事务不包含官方每日免费次数、每日三次或体力机制，符合已确认的离线规则。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

`scripts/prepare_material_gathering_assets.py` 从当前 `871_156598` 已核验的399包 CAB 闭包导出并迁移官方 `MopUpEntryPanel`、`MopUpPanel` 及19项非脚本依赖；完整闭包的184项外部序列化引用全部解析。Prefab 与 Unity Resources 运行映射、三项 AssetRipper GUID 重定向、92个导出 MonoScript GUID 及当前工程内仍未解析的19个 MonoScript GUID、所有输出 SHA256 记于 `assets/reference/dungeon-ui/material-gathering-prefab-source-manifest.json`。入口与采集按钮由 `HomeHudView.MaterialGathering.cs` 接入本地服务；剩余脚本驱动状态/动作尚未迁移，也未完成 Unity Play Mode 检验。

## 自定义实现

- 未取得服务端业务程序集，官方服务器 `OperPlayerMopUp` 结算细节无法复原。按用户确认的离线决策，每条攻击/击破奖励规则按 `WeightList` 抽取一个 `IdList` 项，数量按 `CountList`、阶位倍率和小数随机计算；额外产出同时作用于攻击和击破奖励，向下取整。当前 `871_156598` 采集奖励的 `Percent` 均为1.0。本地事务不得在写盘失败时播放奖励表现。
- 用户确认普通点击每击消耗一份对应工具、快速按实际攻击数消耗；免费宝箱不耗工具且计为采集。
- 用户确认首次采集可进入，并在可计数材料目标击破时推进 Type 18 `UnlockStages`：每次击破将顺序中当前最高已记录项的下一项计数加一；推进到末项后继续累计末项。此离线规则替代了缺失的 Type 18 正式战斗入口。
- 新增 `scripts/prepare_material_gathering.py`，从当前解码表生成 Unity 运行配置；`material-gathering-source-manifest.json` 记录 manifest、源表 SHA256 和输出 SHA256。配置源为当前 `871_156598`，不混用固定 APK 的历史资源。
- 官方解锁条件已核实：`MopUpDungeonInfoParser.GetUnlockDungeonHighestLevel` 按 `UnlockStages` 逆序检查 `Player.GetStageCount(stageId) > 0`。53 个 ID 均为官方 `stage` 表 Type 18 `NormalMaterial`，各自配置 `GameId/Level/SubRank`，且 `PreStage=0`、`Repeat=-1`、`CanMopUp=false`；它们是材料秘境战斗关卡，不是属性或道具解锁条件。Type 25 `NormalMaterialMopUp` 才是地图上的采集入口节点。`stage_map_location` 仅用于未解锁提示定位：23 个 ID 有非空区域信息，20 行为空，另 10 个无表行。
- 服务端业务程序集未取得。客户端 `MopUpContent` 请求只带 `DungeonType/IsQuick`；响应含 `IsBreak/MopUpCount`，而官方客户端回调只更新采集数据/奖励，不调用 `AddStageCount`，故服务端计数时机无法从客户端确定。按用户确认的离线差异，首次采集允许进入；可计数目标击破时，在同一个背包/采集存档事务中将该秘境 `UnlockStages` 中当前最高已记录关卡的下一项计数加一；到末项后继续累计末项。阶段序列依据官方 `UnlockStages` 顺序。

### 已确认的本地接线与边界

`scripts/prepare_material_gathering.py` 从当前热更新 `871_156598` 的解码 Luban 表生成 `unity/Assets/Game/Resources/Official/Progression/material-gathering.json`，包括四条秘境、17条目标、34条攻击/击破/目标生成奖励规则、所需阶位数值以及 `GameSettings.MopUp` 的伤害浮动和单次上限。目标生成规则13000–13003由 `MopUpDungeonInfo.GenerateMaterialRuleId` 引用；本地服务在五个 `NewbieRewardMaterials` 初始目标后按该规则的 `IdList/WeightList` 抽取后续目标。输出来源及 SHA256 记录在 `material-gathering-source-manifest.json`，运行时由 `OfficialMaterialGatheringCatalog.LoadDefault` 校验。服务器业务程序集不在当前官方代码包中；攻击/击破奖励按用户确认的离线规则，从 `IdList` 按 `WeightList` 抽取一项并使用官方数量算法，不能据此宣称取得服务端实现。

## 尚未实现

- prefab 导出列出92个 MonoScript GUID，其中73个由工程/包缓存解析，19个官方客户端脚本仍缺；主城本地绑定覆盖采集事务，官方 StateSwitcher、目标切换表现、角色展示及其余脚本驱动状态还没有等价迁移。当前国度角色模型与工具 Spine 4.2 原生动画、49个目标动画、受击/消失/切换特效、浮标和采集音效尚未接入运行时。已有动作载荷 `assets/official/gathering/{male,female}.json` 与 `assets/official/gathering/targets/` 仍在独立素材目录，来源见 `gathering-motion-source-manifest.json`、`gathering-actions-source-manifest.json`。
- 静态元数据检查已通过；隔离 Unity Editor 批处理运行中的采集规则验收通过：四类首次采集、工具消耗、奖励发放、`UnlockStages` 顺序推进及重载恢复均核对成功。导入期间 Spine 编辑器后处理有 `IngestSpineAtlas` 空引用异常，未阻止 C# 编译和规则验收，但不能据此视为资源导入整体无错误。Play Mode 鼠标输入、实际画面及写入失败验证尚未完成；批处理规则验收不代替这些验证。

## 代码与资源入口

[官方秘境表 `MopUpDungeonInfo`](../../.tools/source-packages/current/871_156598/official-code/common-source/Common/MopUpDungeonInfo.cs)、[解锁解析 `MopUpDungeonInfoParser`](../../.tools/source-packages/current/871_156598/official-code/common-source/Common/MopUpDungeonInfoParser.cs)、[逐 stage 计数 `Player.GetStageCount`](../../.tools/source-packages/current/871_156598/official-code/common-source/Common/Player.cs)、[关卡配置 `StageInfo`](../../.tools/source-packages/current/871_156598/official-code/common-source/Common/StageInfo.cs)、[材料表 `MopUpMaterialInfo`](../../.tools/source-packages/current/871_156598/official-code/common-source/Common/MopUpMaterialInfo.cs)、[初始队列 `PlayerMopUpDataWrap`](../../.tools/source-packages/current/871_156598/official-code/common-source/Common/PlayerMopUpDataWrap.cs)、[解锁与快速伤害 `MopUpUtils`](../../.tools/source-packages/current/871_156598/official-code/common-source/Common/MopUpUtils.cs)、[伤害公式 `BattleFormulaHandler`](../../.tools/source-packages/current/871_156598/official-code/common-source/Common/BattleFormulaHandler.cs)、[客户端面板 `MopUpContent`](../../.tools/source-packages/current/871_156598/official-code/client-source/MopUpContent.cs)、[入口卡片 `NormalMaterialDungeonEntryItem`](../../.tools/source-packages/current/871_156598/official-code/client-source/NormalMaterialDungeonEntryItem.cs)。

Unity 接入还包括 [HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)、[HomeHudView.MaterialGathering.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.MaterialGathering.cs) 和官方运行 prefab `Resources/Gathering/{MopUpEntryPanel,MopUpPanel}.prefab`；prefab 来源及转换哈希见 [material-gathering-prefab-source-manifest.json](../../assets/reference/dungeon-ui/material-gathering-prefab-source-manifest.json)。规则载荷为 [material-gathering.json](../../unity/Assets/Game/Resources/Official/Progression/material-gathering.json)，来源及哈希见 [material-gathering-source-manifest.json](../../unity/Assets/Game/Resources/Official/Progression/material-gathering-source-manifest.json)。
