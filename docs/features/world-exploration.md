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

已恢复原 `WorldExploreBtn`、`ExploreConfirmDialog`、`ExploreSelectTokenDialog`、`ExploreDetailDialog`、`ExploreRewardPreviewItemDetailDialog` 与奖励列表的 prefab 层级、绑定、布局、文本及页签动画。原 LoopScrollRect 程序集反编译恢复9个类，令牌按原品质顺序排列并使用原拖动／回收算法；概率使用原候选、权重归一化、分组排序和保底显示分支。历史保存原实体组变体编号，避免将组编号误用于名称及品质。每日次数按原 `DailyResetTime=(08:00:00)` 重置。概率／历史两个页签的四份原动画保留 `ToggleOn／ToggleOff` 片段名，以唯一哈希目录保存并显式绑定原 prefab，修复片段以哈希文件名导入后无法重新播放的问题；原曲线、时序、层级和布局不变。来源与输出哈希见 [mainline-modules-source-manifest.json](../../assets/reference/official-map/mainline-modules-source-manifest.json)。

探寻和采集共用原 `AwardListPanel`。森之国当前奖励范围绑定661条道具记录，按原 `ItemIconManagerConfig` 选择普通、装备和宝具模板；宝具候选素材范围取本图各地区 `DropItems` 并集，未把其他地图未接入的素材计为完成。原品质框、高品质光效、名称和数量均从官方数据绑定。弹窗模糊的采样顺序与七个原权重、TMP渐变的原45度旋转和顶点计算由 APK ARM64 实现核对后迁移；不以静态截图代替窗口。

隔离 Unity 编辑器规则检查覆盖个人条件、首次令牌限制、首次引导与全局一次奖励、重复请求、批量失败回滚、冷启动恢复、原实体等级、同时满足多个保底、协力宝箱累计上限20、五分钟实体过期、180天历史清理、旧剧情奖励记录兼容、奖励失败完整回滚、宝箱重复领取、协力宝箱开启状态及08:00重置边界。图形 Play Mode 已通过原 HUD／确认／概率／详情、令牌拖动与选择、实际果实和令牌扣除、引导宝箱地图点击免体力领取及十连结果保存；已检查实际渲染画面。历史分页、叠层窗口遮罩、Map10／Map11公共模块回归及动态实体冷启动恢复均已通过。叠层遮罩按原 WindowMaskBase 在激活后恢复独立排序，实际截图已确认覆盖下层弹窗。尚无完整功能或 Windows Player 发布结论。

## 自定义实现

未取得 `OperPlayerWorldExplore` 服务端体，用户允许本地结算：候选取当前Map／Area的原地区 `DropItems` 与原宝物选择器交集；多保底同时命中按 `ExplorePriorities` 及原配置次序选择。

实体落在同Area最近的可通行、允许地表、未占用格，范围使用原 `RefreshDistance`／`ExploreEntityMaxDist`。`LastMapRefreshOnly` 替换前一同组结果，保留原数量上限、持续时间和180天历史，过期按本地时间清理。附近空位不足时整批回滚费用、随机和记录，显示原 `WORLD_EXPLORE_POS_NOT_ENOUGH` 提示。连续生成可能先占满距离1的格，玩家须清理附近实体或移动后再试；未擅自扩大原生成距离。

实体组54「协力宝箱」7050沿用户确认方案，保留原概率、累计产出上限20次、原奖励和有效期，改为本地玩家独自完成原交互；原客户端的两人要求不再作为本地开启条件。此差异尚待运行流程接入与验证。

固定 APK 客户端、Common 程序集及原探寻／固定奖励表已核对；服务端 `OperPlayerWorldExplore` 执行体未取得，无法证实引导奖励的发放时机。用户明确确认：首次成功命运探寻发放原 `WorldExploreGuideFixedRewardId=5` 的物品56“命运果实·原初”×1，全局只发一次；沿用 `fixed_award:5` 领取记录，跨地图或原剧情入口均不重复。奖励不按既有探寻次数追溯补发。奖励、费用、随机、实体和领取记录一并保存，失败全部回滚。

原奖励表缺少小宝箱8200的711、大宝箱8201的721／722、稀有宝箱8202的732／735、传说宝箱8203的742／745。用户明确确认沿用客户端 `RewardRuleInfoParser.GetRewardDropInfosByRuleList` 跳过空引用的行为，其余原奖励保留；没有编造额外奖励。8200仍执行710／713／714／716／717，715未被现行实体模板、山之国覆盖或探寻配置引用。

探寻实体8364“遗失女神”使用独立的 `WorldTouchDialogLostGoddness`／`StageResultAwardLostGoddess` 领奖流程，不沿用地图失落女神像激活。服务器实现未取得，用户确认：按原 `LostGoddnessAwardWeights` 抽可用类型；人物升原配置1级并保留等级内经验；已拥有且未满阶技能等概率免费升1阶；永久属性按原410／Level1及原属性组权重发放；封顶或无候选的类型排除后重抽。奖励、随机、原完成状态和响应内容一同保存，重复领取不重抽。三类结算、封顶排除和存档恢复已通过隔离规则检查；地图交互现已识别生成的8364并调用本地原子事务，使用官方技能升阶／人物升级／永久属性结果 prefab 显示结算，关闭按钮绑定为模拟 UGUI 可操作路径。

### 已确认的本地接线与边界

Map_Action_PeachExplore_1／2／3沿原map_sfx_01的Wwise Event→Action→Sound取得内嵌DIDX／DATA媒体53374149、990872803、88127107，经vgmstream解码再转换OGG。清单保留原分包凭据、媒体输入哈希、偏移处理和输出哈希。运行时使用显式加载绑定，无网络获取。服务端RPC缺失、用户批准的本地结算裁决与尚未确认的引导礼包触发，分别写入对应功能／地图文档，不冒称官方服务端实现。

## 尚未实现

- 补齐所有原图标附件、奖励粒子与道具详情交互；遗失女神三类结果窗已接入，剩余粒子、泛光、音效及完整道具详情仍待补齐。技能升阶结果现绑定官方 `ItemIconSkill` prefab 的来源／目标技能图标、品质底框及星级；该新增显示尚待实际 Player 画面复验。
- 完成生成实体的地图显示、碰撞／寻路与小地图绑定，以及宝箱、战斗、副本、商店和活动实体的完整交互。部分模型仍出现白片、黑块，原材质／UV绑定须修复；动态物体的剧情标签闭包仍待补齐，现有静态剧情登记校验不再误拒绝冷启动后的探寻实体。
- 恢复原动作、MyPlayable、粒子及音效；复验技能升阶结果的来源／目标图标和三种奖励类型的画面、交互及存档恢复；再构建、运行和发布正式 Windows Player。

## 代码与资源入口

固定 APK 原规则由 `scripts/prepare_unity_world_exploration.py` 重新解码，输出 `Resources/World/Mainline/exploration.json`；包含7种令牌、65条实体组变体、28种完整实体模板、68条递归条件、奖励规则、随机奖励包、宝物选择器、等级映射及每表原字节哈希。来源见 `assets/reference/official-map/world-exploration-source-manifest.json`；规则入口为 `OfficialWorldExplorationCatalog`、`OfflineWorldExplorationService` 及 `OfflineMainlineProgressSession.Exploration`，隔离验证入口为 `Zjcs.Editor.Progression.OfflineWorldExplorationValidation.Run`。`JsonUtility` 对原始 JSON 空对象引用的处理已逐字段校正，避免将原空条件误当成未迁移的条件。

界面来源见 Map11 `UI/source-manifest.json`、`loop-scroll-source-manifest.json`、`popup-effects-source-manifest.json`；既有指针验证覆盖59份原面板、535张Sprite所绑定的探寻流程。地图入口为公共 `OfficialMainlineExplorationModule`，由 `Exploration`、`ExploreUi`、`ExploreDetail`、`ExploreMask` 四份实现分别管理实体、窗口、详情和遮罩；公共 UI 目录由 `OfficialMainlineUiCatalog` 提供。动态实体复用 `OfficialMap10EntityView` 与本地 Map11 Entities 资源；当前恢复116种模型，完整支持范围仍以下述未完成项为准。8364 遗失女神在 `OfficialMainlineExplorationModule.Exploration` 转入 `OfficialMainlineStatuesModule.CollectLostGoddess`，结果显示由 `OfficialLostGoddessResultPopup` 绑定三类官方 prefab。十连指针检查使用隔离档案及可容纳原生成序列的地图位置／随机夹具，实际费用、生成和保存仍由原 HUD 点击触发。

遗失女神的来源准备另已提取原触碰窗、技能升阶／人物升级／永久属性结果窗、属性行和 `ItemIconSkill`，共享 UI 数据扩为65份原面板、945张Sprite及1,055条道具图标记录，其中新增394条原主动／被动技能。保留原35条技能阶位、`MapLostGoddessAwardDialogConfig`、星级品质图、属性行动画、组件引用和三类标题，并补录相关反编译代码哈希。Unity 导入器接受这批层级及图标基础绑定；`OfficialLostGoddessResultPopup` 依据本地事务结果绑定三类官方 prefab、来源／目标技能图标、品质底框与星级、等级和属性行，并绑定原关闭按钮；原粒子／泛光／音效和完整技能结果细节仍待补齐。

该批载荷已通过 Unity 2022.3.57f1c2 导入／编译和 `ProjectTools.Validate`；原三类结果窗的 Button 绑定与离散启用时刻3.183333／2.8／2.416667秒均与原曲线核对，共用原动画按各窗实际目标子集分别保存，避免转换结果互相覆盖。新增六份 prefab、十份被引用动画及技能图标基础资产已入工程，既有运行 prefab 未在批量覆盖。图形 Play Mode 已通过 `OfficialMap11PlayModeAcceptance.RunLostGoddess`：地图屏幕输入选中生成的8364实体，本地事务先保存类型4属性奖励及完成状态，再显示官方结果 prefab；实际截图确认标题、角色图、属性前后值和森之国 HUD，关闭按钮通过模拟 UGUI 点击关闭。隔离 Windows x64 Player 随后通过类型2技能升阶、类型3人物升级和类型4属性奖励三条流程：均由地图屏幕输入触发、以射线命中的屏幕坐标 UGUI 点击关闭，并冷读隔离档案确认对应奖励和完成状态持久化。截图复核发现类型2结果窗未显示技能图标；现已按官方 `LostGoddessResultTipsSkillRankItem` 绑定来源／目标图标、官方品质底框和星级显示，并通过 Unity 编译及 `ProjectTools.Validate`。该代码变更后的 Player 画面尚未复验：构建时 `D:` 卷剩余空间为0，Unity 在写入 Player 资源时断言失败。验证构建未发布到正式目录；完整玩法、剩余演出及画面缺陷仍待完成。

官方输入统一使用 `official-source.json` 选定的当前 APK 与配套云端资源；输入位置与完整性凭据见[官方来源](../../assets/reference/SOURCES.md)。已有转换成果仍按所属来源清单记录真实输入哈希。

共享保底依据为 `Common.StandardRandomGroup`、`RandomObj` 和 `PlayerExploreRecordDataWrap.RecordResult`。命中累计、连续未中计数及保底冲突需按这些实现迁移；组54的20次限制是累计产出次数，不能改成场上实体上限。
