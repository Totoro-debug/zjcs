# 命运果实探寻

[总索引](../README.md)

状态：未完成

## 功能简介

修复生产果树并满足原个人条件后，在允许探寻的公开区域使用命运果实115；可查看地区、次数、概率和历史。

原 HUD、确认、令牌选择、概率、奖励详情与历史入口已接入 Unity。完整实体交互、演出和 Windows 发布仍未完成。

## 原始官方实现

官方命运果实探寻读取令牌、地区／等级、实体组、随机包、奖励、保底及条件；Explore 窗口负责令牌选择、数量、结果与历史，服务器生成部分按下文已确认规则接线。

### 待迁移的规则与表现

保留原单次／多次按钮，单批最多10次，令牌功能开放后可选择真实持有令牌。消耗、每日上限、生命、区域、剧情／副本和待领奖状态均校验。

首次使用原引导实体组51，之后按原探寻组、地区、权重、保底、数量和额外数据生成实体；奖励经过原地图交互、战斗和领奖取得，不直接将预览入包。

原 `WorldExploreAction`、`AppearAction`、角色动作、MyPlayable轨道、粒子和音效须接入。动态实体、每日次数、保底、历史、果实／令牌和随机在同一事务保存，失败回滚，重开不重抽。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_world_explore.py` 从固定 APK／校验原版 CDN 的 `prefabs_ui_map_world`、`prefabs_ui_map_explore`、`prefabs_ecentity_mapworld_actionworld_actionworldexplorer` 读取原 WorldExploreBtn、八份探寻UI层级、序列化控件绑定及动作22002／22003／22041／22042。未自绘图片、重排确认窗或设计概率卡片。20个实体组图标及六种令牌图标按原Icon／RawIcon地址取得，重复素材复用现有输出。

独立输出 `assets/official/world-explore/`；`assets/reference/world-explore/source-manifest.json` 登记原bundle／对象／URL、版本、代码与配置输入SHA256、复用纹理及全部输出SHA256。规则使用 explore_token_group、explore_entity_group、map_explore_limit、rule_tip 及既有固定版 adventure／map-entities／rules 转换。原语言包提供地区、实体、规则、时间格式等文本，RuleTipNumer和TextReplacer按原代码处理编号及参数。

## 完整还原部分

已接入 Unity 本地规则层：原个人与区域条件、7种令牌、65条实体组变体、原权重与保底记录、首次组51、生成实体的等级／阶位与额外奖励数据、原费用、每日上限、累计产出限制、持续时间和180天历史。动态实体、随机、费用、记录与首次固定奖励以同一存档事务保存；重复请求不重抽，整批失败全部回滚。

原触碰奖励实体按配置读取奖励规则、条件、等级与阶位倍率，以及随机奖励包。探寻来源免体力；奖励只在领取时入包，宝箱开启计数与领取记录同时保存。协力宝箱的本地单人奖励结算保留原完成状态及 `DestroyOnEnd=false`，开启后直至原有效期届满才移除。该结算层已经接通，原交互动画和实际鼠标开启流程仍待验证。

已恢复原 `WorldExploreBtn`、`ExploreConfirmDialog`、`ExploreSelectTokenDialog`、`ExploreDetailDialog`、`ExploreRewardPreviewItemDetailDialog` 与奖励列表的 prefab 层级、绑定、布局、文本及页签动画。令牌按原品质顺序使用原拖动／回收算法；概率使用原候选、权重归一化、分组排序和保底显示分支。历史按原实体组变体编号保存；每日次数按原 `DailyResetTime=(08:00:00)` 重置。概率／历史页签绑定原 `ToggleOn／ToggleOff` 动画曲线、时序、层级和布局。来源见 [主线模块来源清单](../../assets/reference/official-map/mainline-modules-source-manifest.json)。

探寻和采集共用原 `AwardListPanel`。森之国奖励候选按本图各地区 `DropItems` 绑定，并由原 `ItemIconManagerConfig` 选择物品模板；品质框、光效、名称和数量使用官方数据。弹窗模糊、七档权重、TMP 渐变及叠层顺序沿用官方参数。

## 自定义实现

未取得 `OperPlayerWorldExplore` 服务端体，用户允许本地结算：候选取当前Map／Area的原地区 `DropItems` 与原宝物选择器交集；多保底同时命中按 `ExplorePriorities` 及原配置次序选择。

实体落在同Area最近的可通行、允许地表、未占用格，范围使用原 `RefreshDistance`／`ExploreEntityMaxDist`。`LastMapRefreshOnly` 替换前一同组结果，保留原数量上限、持续时间和180天历史，过期按本地时间清理。附近空位不足时整批回滚费用、随机和记录，显示原 `WORLD_EXPLORE_POS_NOT_ENOUGH` 提示。连续生成可能先占满距离1的格，玩家须清理附近实体或移动后再试；未擅自扩大原生成距离。

实体组54「协力宝箱」7050沿用户确认方案，保留原概率、累计产出上限20次、原奖励和有效期，改为本地玩家独自完成原交互；原客户端的两人要求不再作为本地开启条件。此差异尚待运行流程接入与验证。

固定 APK 客户端、Common 程序集及原探寻／固定奖励表已核对；服务端 `OperPlayerWorldExplore` 执行体未取得，无法证实引导奖励的发放时机。用户明确确认：首次成功命运探寻发放原 `WorldExploreGuideFixedRewardId=5` 的物品56“命运果实·原初”×1，全局只发一次；沿用 `fixed_award:5` 领取记录，跨地图或原剧情入口均不重复。奖励不按既有探寻次数追溯补发。奖励、费用、随机、实体和领取记录一并保存，失败全部回滚。

原奖励表缺少小宝箱8200的711、大宝箱8201的721／722、稀有宝箱8202的732／735、传说宝箱8203的742／745。用户明确确认沿用客户端 `RewardRuleInfoParser.GetRewardDropInfosByRuleList` 跳过空引用的行为，其余原奖励保留；没有编造额外奖励。8200仍执行710／713／714／716／717，715未被现行实体模板、山之国覆盖或探寻配置引用。

探寻实体8364“遗失女神”使用独立的 `WorldTouchDialogLostGoddness`／`StageResultAwardLostGoddess` 领奖流程，不沿用地图失落女神像激活。服务器实现未取得，用户确认：按原 `LostGoddnessAwardWeights` 抽可用类型；人物升原配置1级并保留等级内经验；已拥有且未满阶技能等概率免费升1阶；永久属性按原410／Level1及原属性组权重发放；封顶或无候选的类型排除后重抽。奖励、随机、原完成状态和响应内容一同保存，重复领取不重抽。地图交互现已识别生成的8364并调用本地原子事务，使用官方技能升阶／人物升级／永久属性结果 prefab 显示结算，关闭按钮绑定为模拟 UGUI 可操作路径。

### 已确认的本地接线与边界

Map_Action_PeachExplore_1／2／3沿原map_sfx_01的Wwise Event→Action→Sound取得内嵌DIDX／DATA媒体53374149、990872803、88127107，经vgmstream解码再转换OGG。清单保留原分包凭据、媒体输入哈希、偏移处理和输出哈希。运行时使用显式加载绑定，无网络获取。服务端RPC缺失、用户批准的本地结算裁决与尚未确认的引导礼包触发，分别写入对应功能／地图文档，不冒称官方服务端实现。

## 尚未实现

- 遗失女神三类结果窗已接入；原奖励粒子、泛光、音效及完整道具详情仍待补齐。技能升阶结果已绑定官方 `ItemIconSkill` 的来源／目标图标、品质底框及星级，完整运行表现尚未接通。
- 生成实体的地图显示、碰撞／寻路、小地图绑定，以及宝箱、战斗、副本、商店和活动实体的完整交互仍待完成。部分模型的原材质／UV绑定及动态实体剧情标签仍有缺项。
- 原动作、MyPlayable、粒子及音效尚未全部接入；完整交互、存档恢复和 Windows Player 发布仍未完成。

## 代码与资源入口

固定 APK 原规则由 `scripts/prepare_unity_world_exploration.py` 重新解码，输出 `Resources/World/Mainline/exploration.json`；包含7种令牌、65条实体组变体、28种完整实体模板、68条递归条件、奖励规则、随机奖励包、宝物选择器、等级映射及每表原字节哈希。来源见 `assets/reference/official-map/world-exploration-source-manifest.json`；`JsonUtility` 对原始 JSON 空对象引用的处理已逐字段校正，避免将原空条件误当成未迁移的条件。

探寻入口由公共 `OfficialMainlineExplorationModule` 管理实体、窗口、详情和遮罩，并从 `OfficialMainlineUiCatalog` 读取原生界面。动态实体复用 `OfficialMap10EntityView` 与 Map11 Entities 目录；当前模型覆盖和原生资源范围见所属来源清单。

遗失女神结算使用原触碰窗、技能升阶／人物升级／永久属性结果窗、属性行及 `ItemIconSkill`。`OfficialLostGoddessResultPopup` 按本地事务结果绑定对应 prefab、技能图标、品质与属性行；粒子、泛光、音效和完整道具详情仍待接入。原生来源与动画绑定见所属来源清单。

官方输入统一使用 `official-source.json` 选定的当前 APK 与配套云端资源；输入位置与完整性凭据见[官方来源](../../assets/reference/SOURCES.md)。已有转换成果仍按所属来源清单记录真实输入哈希。

共享保底依据为 `Common.StandardRandomGroup`、`RandomObj` 和 `PlayerExploreRecordDataWrap.RecordResult`。命中累计、连续未中计数及保底冲突需按这些实现迁移；组54的20次限制是累计产出次数，不能改成场上实体上限。
