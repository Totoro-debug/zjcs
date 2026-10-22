# 主城、家园与离线产出

[总索引](../README.md)

状态：未完成

## 功能简介

正式角色进入家园，拖动查看设施，通过底栏切换人物、游记、家园、公会和大陆。本页涵盖家园场景、环境表现、设施教学、提示、邮箱、动物／落叶奖励及床铺／小推车离线收益；果树生产、各设施的养成玩法和商店分别见总索引所列页面。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。本页同时维护原始官方主城功能目录与本地待实现清单；各玩法的内部公式、养成与奖励规则只在所属页维护，通过下表引用，不另建主城审计或进度报告。

### 官方功能范围与判定依据

官方基线为固定版本 `854_156383` 的客户端、共享代码、配置与 prefab。官方脚本来自 `.tools/source-packages/official-code/client-source/` 和 `common-source/Common/`；`assets/official/ui.json` 保存主城、顶部栏、底栏、头像与功能菜单的控件树，`assets/official/home.json` 保存场景层级、设施位置、相机及点击区域。原始联网功能保留在功能目录中，其单机处理单独注明；官方资源或脚本可取得，不代表已在 Unity 中恢复运行。

| 范围 | 原始官方功能与关键调用关系 | 当前实现与剩余工作 |
| --- | --- | --- |
| 主城进入与生命周期 | `MainUIRoot` 管理主面板；`PublicBottomBar.OnTabTypeChanged → MainViewController.SwitchMainView` 切换 UI 和场景；`HomeRoot` 初始化家园作用域服务、激活场景、同步时间与控制音乐 | 未接通。`CharacterCreationFlowController.ShowComplete` 当前提供人物、初始岛和返回存档入口；须接通正式角色进入／返回家园、加载与卸载、会话恢复及与创角后剧情的衔接，不能将独立功能入口视为主城入口 |
| 顶部栏与头像 | `HomeUI` 的 `NewPublicTopBarParent/Slot`、`TopBarHandler`、`TopBarManager`／`TopBarItem` 和 `PublicPlayerIconHome` 展示角色及配置指定货币，随窗口借还顶部栏、响应数量变化并提供点击入口 | 未接通主城数据绑定。须读取当前角色名称、外观、等级及实际余额，恢复原数值格式、货币说明／跳转、头像打开人物页、窗口入退场与刷新 |
| 底栏五入口 | `PublicBottomBar.TabType` 包含 `Character`、`TravelNote`、`Home`、`Legion`、`Mainland`；原 `TabGroup`、条件、显隐引用、升级标记和场景代理共同控制切页 | 未接通。恢复五入口的选中／锁定／提示、面板及场景切换、返回状态和窗口遮挡；人物、游记、公会与大陆玩法分别见[人物](character.md)、[日常副本](dungeons.md)、[单人公会](guild.md)、[世界地图](world-map.md) |
| 主城任务与战斗提示 | `PinnedTaskPanel`、`MainTaskFunctionLimit`、`GameEndNotifyUI`、`GameEndUIHandlerInHome`、`FightDemonBattlefieldAutoPushEndNotifyUI` 负责任务追踪、战斗完成通知和返回战斗入口；床铺／推车有 `HomeHangUpDirectionGuider` | 未接通主城 HUD。连接实际任务、可领取状态、允许保留的战斗结果和引导目标；原魔物战争按单机范围关闭，不伪造完成提示。任务规则见[主线任务](tasks.md) |
| 功能菜单 | `FunctionEntrysPanel` 管理折叠／展开；原 `FunctionEntryDialog` 有任务、商城大按钮，以及图鉴、成就、装潢、设置、拍照、录像等入口 | 未接通主城菜单。按原控件树恢复折叠、展开、遮罩、排序、开放条件、页面跳转与关闭返回；分别引用[商城](commerce.md)、[图鉴](codex.md)、[成就](achievements.md)、[设置／拍照／存档](settings-and-save.md)，装潢见本页 |
| 活动及推荐入口 | `HomeUIActivityEntryController` 和 `FunctionEntryDialog.GetOpenActivityRecommendEntries` 按真实活动记录生成、排序和展示入口／推荐；`EventEntry` 是主城挂载点 | 未接通。只展示已确认保留且实际开放的本地活动，不因资源存在生成活动实例或奖励；[签到](sign-ins.md)、[活动小游戏](activity-minigames.md)和[离线开放范围](offline-access.md)维护具体范围 |
| 社交及联网服务入口 | 原 prefab 包含队伍小面板、匹配、申请确认、聊天小窗／快捷消息、好友、排行榜、公告、用户、社交、百科、合服和边玩边下载／预下载等控件；功能菜单对应 `FriendBtn`、`TopListBtn`、`AnnounceBtn`、`UserBtn`、`SocialBtn`、`WikiEntry`、`PlanesMergeFunctionEntryBtn`、`PredownloadEntry` | 未完成入口处理。真人组队、聊天、好友、真人赛事、交易／拍卖和联网账号／预下载按既定范围关闭；不生成虚构玩家。其他入口只记录官方功能，未有明确本地处理依据的分支须在接入时核实，不能自行造排行榜、公告、远端服务或开放联动 |
| 场景浏览与输入 | `HomeCameraController`、`HomeDragController → IgnoreUIDragController` 控制视野与区域边界；`ClickableHomeObj` 按实际 Collider／触摸对象和家园／剧情状态判断有效点击 | 未恢复完整输入链。恢复官方坐标、镜头、拖动范围、移动阈值、点击区域、层级和模态窗口拦截；验证拖动不误点、关闭窗口不穿透、遮挡时不触发设施 |
| 设施通用状态 | `FurnitureBase.GetState/OnValidClick/DoRepair` 区分锁定、维修预告、可维修、正常，读取 `FurnitureInfo` 的条件、费用、关联设施和维修剧情，响应维修奖励与家园变化并播放修复特效 | 未接通。按原 `furniture`、条件目录及实际家具所有权更新模型、通知、费用、红点和教学；`CanLevelUp`、空费用与自动维修条件逐项取原表，不给所有家具统一添加升级／收费规则 |
| 提示与标记 | `HomeTipDotGraphManager`、`CommonTipDotLinker`、各 `TipDot…ConditionNode` 和家具红点组件使用提示图、动态选择器及实际状态决定红／蓝点、升级、新解锁及可领取提示 | 未接通实际玩家状态。需覆盖邮箱、背包、装备净化／分解／宝石、古遗物、伙伴、星命、商店、料理与真实活动，并按查看／领取结果更新和清除 |
| 昼夜、天气和主题环境 | `HomeEnvironmentManage`、`HomeClock`、`HomeEnvironmentManagerMono`、`HomeLightController`、`HomeMutexLightLayer`、`HomeCloud`、`HomeSnowController` 等驱动天空、太阳、全局光、分层灯光、云、月相、积雪、降雪与夜间特效 | 资源已恢复，运行驱动未接通。须恢复原关键帧、材质参数、遮挡／互斥层、分批开关灯、网格／遮罩、流星和角色受光，完成 Windows 实际画面验证 |
| 装潢与拍照 | `HomeSkinPanel` 及主题／环境／家具分类、子分类与提供方管理拥有、预览和穿戴；`HomePhotoSystem` 等提供官方拍照表现 | 未接通。装潢沿用固定设施布局和原镜头控制，不新增自由摆放；本地拍照及允许保留的范围见[设置与拍照](settings-and-save.md)，不把原相册／滤镜等扩展自动标为完成 |
| 动物／落叶与家园 NPC | `HomeRandomReward` 按服务器槽位数据实例化物件并请求领取；`HomeNpcBase` 等管理 NPC 挂载、模型和入口，`HomeAwardRetrieve` 打开奖励回收窗口 | 只有随机奖励槽和本地事务底层。须装配原29槽位、奖励组、家具限制、位置／皮肤覆盖及动画，连接点击领取；NPC、回收入口与提示只连接实际保留玩法，不编造奖励源 |
| 设施教学与角色行为 | `RepaireFurniture2`、`GoHomeUnlockHeap`、`RepaireBed`、`BedSpeedUp`、`RepaireBaGuaLu`、`FirstMergeItem` 六张教学图与诺诺九张强制行为图控制条件、分支、对白、镜头、按钮引导、修复、领取与加速回执 | 仅有教学进度和队列存储；执行器、场景角色动作、窗口／遮罩、成功回执及中断恢复均未接通。固定场景 `NpcBehaviour.DecisionGraph` 未挂载，不增设自主巡游 |

### 官方14类装置与待实现入口

类型 ID 取自 [`EFurniture`](../../.tools/source-packages/official-code/common-source/Common/EFurniture.cs)；家具物品 ID 与条件来自 `assets/official/rules.json` 的 `furniture`，邮箱按独立 `HomeMail` 调用链处理，不补造不存在的家具物品记录。表内“未接通”均指当前主城流程，其他页面的独立实现不能代替场景入口验收。

| 类型 ID／家具物品 ID | 原装置及官方行为 | 当前实现与待实现范围／所属页 |
| --- | --- | --- |
| 0／4000 | 床铺 `BedFurniture → FurnitureBedDialog`；经验挂机、产量／容量、领取及加速 | 收益计算和会话底层已写；原界面、真实经验路由、等级／容量／费用、属性来源、升级前结算、睡眠演出和奖励反馈未接通；规则见本页及[人物成长](character.md) |
| 1／4001 | 书桌 `DeskFurniture → TreasurePanel`，进入古遗物收藏 | 场景四态、点击与收藏页面衔接未接通；内部收藏、任务及培养见[古遗物](relics.md) |
| 2／4009 | 餐桌及 `DiningTableNpcPopClick → NpcPanelUtils.Open(Dialogue)`，伙伴交流；维修剧情 `UnlockCommunication` | 解锁／维修剧情、伙伴场景角色、点击、专属对话、遮罩和教学恢复未接通；好感、档案等见[伙伴](partners.md) |
| 3／4015 | 炼金炉 `BagualuFurniture → BagualuPanel`，料理、合成、分解、宝石、馈赠 | 场景入口、原五页签、费用与背包事务、提示及炉体材质／粒子未接通；见[料理／合成／分解](cooking.md)、[宝石](gem-forge.md) |
| 4／4016 | 生产果树 `HomeFruitTreeProduceFruit`；解锁、成熟果实领取、产量／容量，维修剧情 `FruitTreeUnlock1` | 实际生产、条件／加成配置、果实实例和领取未接通；见[果树](fruit-tree.md)，果实消费另见[命运果实探寻](world-exploration.md) |
| 5／4004 | 女神像 `GachaFurniture → GachaPanel`，技能祈愿及相关对话／演出 | 原场景入口、四态、专属对话和原奖池页面未接通；抽取、保底、兑换、记录、馈赠及材料购买见[技能祈愿](wishes.md) |
| 6／不另造物品 ID | 邮箱 `HomeMail → MailDialog`；原功能52开放条件、邮件列表／正文／附件及有信／无信表现 | 有本地邮件读领删及附件事务；开放条件、猫头鹰、官方窗口／分页／附件和真实本地投递调用方未接通 |
| 7／4018 | 储物箱 `StoreBoxFurniture → PlayerBagPanel`，打开行囊 | 有独立 `InventoryFeatureRuntime`；储物箱点击、场景条件、实际背包页面与物品操作衔接未接通；见[行囊](inventory.md) |
| 8／4013 | 售货机 `ShopFurniture → HomeShopPanel`；官方另有访客商店分支，维修剧情 `SenSenHomeStore` | 主人商店入口、四态、剧情及购买／刷新未接通；单机不保留访客／分享库存，杂货、宝库、特卖、馈赠和批量购买见[家园商店](home-shop.md) |
| 9／4007 | 时光扭蛋机 `ChestGachaFurniture → ChestGachaPanel`，古遗物宝箱／女神牌抽取 | 维修资格、场景模型切换、原页面与抽取演出未接通；奖池、次数、保底、记录和实际奖励见[古遗物扭蛋](treasure-gacha.md) |
| 10／4011 | 小推车 `CartFurniture → FurnitureCartDialog`；资源挂机、产出明细、领取及加速 | 收益底层已写；产物配置、随机装备实例、容量／等级／费用、明细界面、背包和领取／加速反馈未接通 |
| 11／4017 | 幻兽果树设施 `HomeFruitTreePet.TryCallFunction → PetGachaPanel`；依赖生产设施，维修剧情 `FruitTreeUnlock2` | 原果树复合状态、解锁／维修、点击分发和结缘入口未接通；条件见[果树](fruit-tree.md)，抽取及商店见[幻兽结缘](pet-gacha.md) |
| 12／4019 | 命运星象 `AstrologicalDestinyWindowFurniture → AstrologicalDestinyEntryPanel`，按原赛季／位阶配置判断入口 | 场景入口、预告／开放判断、记录提示与契约页面未接通；内部阶梯、灌注、费用和累计属性见[星命](astrological.md) |
| 13／4020 | 飞艇 `AirshipFurniture → TradePanel`，含交易所、拍卖、交易账簿、特权及规则入口，设施另有激活动画、展示物品与灯光 | 官方功能已定位；真人交易／拍卖按[离线范围](offline-access.md)关闭，不作为待开放单机玩法。待实现原场景对象的显示／关闭状态与点击处理，禁止保留可误入联网流程的入口 |

## 完整还原部分

当前完成范围是官方载荷恢复和部分本地底层，没有主城整模块已在 Unity 完整接入并验收的结论。

`unity/Assets/Official/Home/` 已保存来源清单登记的2,001个输出文件、265个目录及49个跨模块共享脚本绑定；输出包含1张场景、139个 prefab、261个 C# 文件及贴图、动画、材质等。文件哈希和 `.meta` GUID 与[Home Unity来源清单](../../assets/reference/home-unity/source-manifest.json)匹配；Unity 重序列化的内置 `Sprite-Lit-Default.asset` 保留原始及导入后哈希。261个本目录 C# 文件全部是带 `Dummy class` 注释的 AssetRipper 空占位类，包含 `HomeRoot`、`HomeUI`、`PublicBottomBar`、`BedFurniture`、`CartFurniture` 和 `HomeRandomReward`，不能作为官方逻辑恢复的证据。官方真实方法体可在反编译目录取得，须迁移到 `Assets/Game/Runtime/Official/` 或对应适配／展示层并重新绑定；不在 `Assets/Official/` 编写手工运行时代码。

[`OfflineHangUpIncome`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs) 实现小时产量、容量、整数领取与单向时间检查点计算。新增 [`OfflineHomeIncomeSession`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs)，从调用方传入家园收益配置及角色属性，打开时按本地 Unix 时间补算并受存储时长上限限制；领取和加速奖励通过候选存档副本校验后原子写入本地 JSON，重复回执不会重复发放。新存档从打开时刻建立检查点，不会从 Unix 纪元错误累计；存档 schema 1 可迁移到 schema 2。加速收益按已确认的单机规则直接进背包，不受家具存储上限限制。

同一存档现提供离线随机奖励槽、领取回执和确定性随机状态；按调用方传入的奖励定义及分类上限填充空槽，满额时不继续生成，领取后从当前检查点重新计时。已解析官方 `home_random_reward_type` 的刷新区间／上限、29 个槽位、组别及家具解锁限制，以及 `home_random_reward_group` 到 `reward_rule` 的映射。官方 `reward_rule` 的产物是 `ItemType=302` 的家园奖励令牌（6015、6017、6013）；`HomeRandomReward` 只实例化服务器下发的槽位物件并请求服务器领取，最终发奖逻辑未包含在客户端代码中。奖励槽表、解锁条件和服务端奖励结算尚未装配为离线 Unity 配置。邮箱记录提供本地邮件写入入口、到期过滤、未读/待读优先排序、单封与批量已读、按附件索引领取和删除；部分领取保留未领附件，附件和背包变化在同一候选存档事务中提交。初始邮箱仍为空，不生成服务端邮件。教学当前节点、步骤和待执行队列也纳入同一存档格式。

免费加速按官方 `PlayerHangUpDataExt.CanFreeFastHangUp` 的配置时长和次数上限执行，次数按本地日期分开记录；道具加速扣除时间结晶、计算收益及写入领取回执在同一事务中完成，单次数量不超过100并受调用方传入的官方最大数量限制。具体免费次数、时长、道具小时数和奖励配置尚未由官方表加载器接入。

计算依据为官方 [`HangUpUtils`](../../.tools/source-packages/official-code/common-source/Common/HangUpUtils.cs) 的收益量、结算量和容量合并逻辑；客户端 `BedFurniture`、`CartFurniture` 与 `BaseHangUpFurniture` 确认床铺和小推车的家具入口与领取流程。`ProfileSlotCatalog.GetHomePath` 与 `OfflineHomeIncomeSession.Open(ProfileSlotCatalog, ...)` 已将家园存档定位到当前角色槽目录；Unity 尚无家园收益配置加载器、角色属性提供方、启动会话入口或连接实际玩家背包的调用方，因此会话层仍未接入实际游戏流程，不能视为游戏内功能已完成。

### 家园运行层接入（本轮）

以下为按官方源码接入的实际代码，尚未完成 Play Mode 画面／真实输入验收。

- 配置装载：[`prepare_unity_home_runtime.py`](../../scripts/prepare_unity_home_runtime.py) 把 `assets/official/home.json`、`home-completion/scene.json`、`ui.json`、`rules.json`、`home-random-rewards/catalog.json` 等整理为 `unity/Assets/StreamingAssets/Home/{scene,ui,rules,rewards}.json` 并复制 647 个被引用贴图，输出 [`source-manifest.json`](../../unity/Assets/StreamingAssets/Home/source-manifest.json) 记录哈希。`OfficialHomeRules.LoadDefault()` 读取同一载荷，保留家具13行、挂机12行、`furniture_level_rule` 595行、`hang_up_reward` 11行、升级材料、皮肤、功能开放与奖励规则。
- 规则移植：[`OfflineHomeProgression`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeProgression.cs) 复刻 `FurnitureBase.GetState`（已拥有→可维修→锁定→维修预告的判断顺序）、`DoRepair` 的费用与 `AdditionalRepairList` 前置校验、`UnlockHangUp`、`HangUpLevelUpPanel` 的等级上限（`home_level.FurnitureLevelUpLimit`）与 `hang_up_upgrade_material` 材料，并按 `HangUpUtils` 公式（`HourlyOutput * LevelNum * 加成 + 固定值`，容量 = 36小时产出）生成挂机定义。条件求值覆盖 `Level/Rank/SubRank/MaxProfessionRank/CompositeAnd/Or/HasItem/StoryCount/StoryStatus/MapUnlock/LogicalNot` 等类型，未识别的条件类型记录在 `Unresolved`，不默认通过。
- 存档与事务：[`OfflineHomeIncomeSession`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs) 新增家具拥有、挂机等级／解锁、装潢选择与发奖序号字段，`Mutate` 以候选存档方式提交并带回执去重；`AcknowledgeInventory` 在奖励真正进入角色背包后清理暂存，`Reconfigure` 在家具维修、挂机升级后重建收益目录。
- 场景与交互：[`HomeSceneView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs) 用官方 384 个 sprite 四边形、sorting 顺序与点击四边形重建家园；家具按 `/X/Repaired` 与 `/X/Unrepair` 切换四态表现；相机使用官方 `orthographic_size`、`border_center/extents` 与 `drag_sensitivity` 计算夹取，`HomeInputController` 采用 `kDraggingSqrOffsetThread = 1` 的 1 像素拖动阈值，指针在 UI 上或模态窗口打开时不触发设施。
- HUD 与窗口：[`HomeHudView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs) 由官方 `home`、`top_bar`、`money`、`player_icon`、`bottom_bar`、`functions`、`repair`、`bed`、`cart`、`mail_*`、`fast_idle` 面板树生成 UGUI；顶栏显示实际背包货币（1/2/15），底栏五入口可选中并提示所属模块，功能菜单可展开收起，床铺显示存量／容量并领取或免费加速，小推车按 `hang_up` 行列出解锁／升级／领取，邮箱列出本地邮件并支持阅读、全部已读与领取附件。
- 入口与发奖：[`CharacterCreationFlowController`](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 的完成界面新增「进入家园」，调用 `HomeFeatureRuntime.Begin`；[`HomeFeatureRuntime`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs) 打开角色存档与家园存档、每 5 秒按本地时间推进产出、把领取物写入角色背包（经验道具 50 交给 `AwardExperience`），交易失败不改内存状态。
- 环境：[`HomeEnvironmentView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeEnvironmentView.cs) 读取官方 `HomeEnvironmentConfig`（`HomeTimeScale=30`、`DayStageList`、`HomeLightTimeConfigs`、`EffectLightOn/Off`、`MoonConfig`、`MeteorConfig`），按官方时间倍率推进昼夜、切换 `DoorLight/LobbyLight/BedRoomLight/StudyLight/DeskLight/DiningTableLight` 灯光组、计算月相与流星窗口。原天空／太阳颜色曲线与流星粒子模板在 `HomeEnvironmentConfig` 的动画剪辑与粒子资产中，尚未导出，故未自造颜色关键帧。
- 随机奖励：[`HomeRewardView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeRewardView.cs) 读取官方 29 个槽位坐标、18 个落叶 prefab 与 3 组动物骨骼首帧网格，按类型实例化落叶 sprite 或动物静态姿态，点击走 `CollectRandomReward` 本地事务并刷新；Play Mode 实测 15 个在位奖励生成 14 个可见对象（领取 1 个后计数）并可领取清除槽位，动物贴图页与空模型已被过滤；动物动画帧（Spine）尚未播放。
- 当前限制：床铺等级按官方 `UseBlessingLevel` 取赐福等级，离线暂以 1 计算，女神像／探索等属性来源未接入；随机奖励已实例化槽位与点击，动物仅首帧静态姿态；装潢与教学图、牛车以外的设施页面仍是待办；场景骨骼（43 个 Spine 实例）与月相／流星贴图未绘制。

人物／衣柜、行囊和商城已有各自的部分运行时实现，但家园场景、底栏／头像／储物箱／菜单尚未连接这些模块。人物页当前由 `CharacterCreationFlowController.OpenCharacterProfile` 独立打开；其实际完成与验证范围见[人物页](character.md)，不在本页重复认定完成。

验证边界：`python -B scripts/check_unity_project.py` 的静态元数据检查通过；家园清单输出的文件存在性、SHA256 和 GUID 核对通过。`OfficialHomeAssetTools.ValidateMigratedAssets` 提供 Editor 资产与场景检查入口，但严格脚本／Shader 检查未取得通过结论；此处不沿用其他模块早期编译报错作为当前阻塞判定。主城 Editor 导入／编译、Play Mode 逻辑／真实输入／画面／保存恢复以及 Windows Player 表现均未取得完整验收结论。

本轮补充证据与缺口：家园离线源码（`Offline/Home/*.cs`）与表现层源码（`Presentation/Home/*.cs`）用 Unity 同版本 Roslyn 单独编译无错误；运行的 Unity 编辑器已在 `Library/ScriptAssemblies` 生成 `Zjcs.Presentation.Home.dll` 与包含「进入家园」的 `Zjcs.Presentation.CharacterCreation.dll`，说明编辑器编译通过；`check_unity_project.py` 本轮仅报告 `StreamingAssets/Opening/voices` 等其他会话的元数据缺项，家园新增文件无报错。已加入 Play Mode 验收驱动 [`HomeAcceptanceLauncher`](../../unity/Assets/Game/Editor/Home/HomeAcceptanceLauncher.cs) 与 [`HomeAcceptanceDriver`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.cs)：用隔离存档进入家园、维修家具、领取与升级挂机、打开床铺／小推车／邮箱／功能菜单、渲染截图并写 `output/home-acceptance/report.json`。2026-10-23 02:08 Play Mode 验收在冻结副本工程中全部通过（`success:true`）：官方载荷 384 场景层/57 面板/12694 物品加载、床铺/小推车/储物箱维修、床铺 3 小时收益预览 150 并成功领取、小推车挂机解锁、昼夜阶段与月相、床铺/小推车/邮箱/功能菜单四个窗口构树与截图、家园存档写盘均通过。截图见 `output/home-*.png`（不入 Git）。期间修复：`ui.json` 顶层面板漏读（57 面板）、`JsonUtility` 为缺失子对象生成默认实例造成的空引用、`initAutoUnlock` 缺失导致床铺不产、额外挂机行（100-102）被误列为独立产出、主城画布排序与创建界面遮挡、缺失贴图渲染成不透明白块。本轮又修复：天空改为按官方 `Home_Back_Common_ColorBlend` 材质 `_Color1/_Color2` 渲染两色渐变（不再是被当成纯白的 Square 背景），顶栏货币图标改由物品表 `icon` 文件名映射官方图集。集成状态（2026-10-23 05:00）：在冻结副本中合并当前工作区（含并行会话接入的宝箱扭蛋／宠物扭蛋／祈愿／果树页面）跑通 11 项验收；宠物扭蛋目录因数据键名为 `PetShop` 而代码读 `Shop`，已加兼容别名；宝箱／宠物扭蛋目录缺失时降级为入口提示，不再阻断家园进入。`output/home-acceptance/report.json` 为每次运行结果（不入 Git）。输入与相机：`HomeSceneView.HitTest` 对床铺/小推车点击四边形命中（0/10）、空白区域不命中，`MoveCamera` 拖动后按官方 `border_center/extents` 夹取（5.52 → -15.52）并在验收后复位镜头，均在 Play Mode 通过；随机奖励 15 槽生成/领取亦通过。真实鼠标拖拽/遮挡仍待人工或输入设备级验证。仍存表现缺口：床铺面板的赐福/女神像/古遗物等属性行数值与标题图标未绑定（当前为空图块）、底栏文字与任务提示仍为 prefab 原值、房间内家具在默认镜头外需拖动查看。2026-10-23 00:40 的批次运行被并行会话未完成的 `CommerceScreenController` 编译错误阻断（`OpenDaily/OpenWeekly/OpenFirstPay/OpenAccumulated/OpenExchange/AddRewardIcons/OpenPriceTable` 不存在），家园代码本身未报错；因此尚未取得 Play Mode 画面／鼠标输入／存档恢复结论，也没有 Windows Player 构建或截图。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

离线与运行期间按本地时间累计产出，负时间差按零，时间检查点只前进。所有领取、升级、费用、随机结果和教学进度使用同一本地事务，失败回滚。

用户补证加速收益直接入背包，超出设施存储上限的加速收益也结算入包；服务端普通收益结算方法体未取得，此语义不标作服务端源码还原。旧自创补给、材料兑换已停用，仅保留兼容记录。

动物／落叶生成的服务端方法体未取得：检索客户端 `HomeRandomReward`、公共数据 `PlayerHomeRandomRewardDataExt`、奖励通知及 RPC 代理，仅能确认刷新区间、容量、槽位、奖励配置和领取后的同步。用户于2026-10-19确认本地方案：两类分别按原8～9小时／16～18小时随机区间，每周期在合法空槽生成1个原表奖励，离线补刷至9／6个上限；满额时不积攒刷新欠账，领取后继续下一周期。奖励、计时、随机状态和领取使用原本地存档事务，失败回滚。

邮箱由实际本地系统提供邮件记录，初始收件箱为空。原远程运营、全服推送和玩家来信没有离线投递源，不自动生成邮件、附件或活动奖励；原 RPC 的读／领／删请求改为同一本地存档事务。

真人组队、聊天、好友、真人赛事、魔物战争、交易所／拍卖等按[离线开放范围](offline-access.md)关闭；公会只保留单人范围，助战使用原 NPC。主城原控件与飞艇交易功能仍列入官方目录，待落实关闭／显隐及输入处理，不将原联网业务算作待开放玩法，也不将“政策已确认”写成“入口关闭已验收”。账号、社交、推送、振动和网络预下载的移除、Windows 等比竖屏与本地拍照按[设置与存档](settings-and-save.md)执行。两项暂不开放的联动及关联伙伴继续遵守离线范围页，不通过主城活动、餐桌或快捷入口重新开放。

## 尚未实现

上方两张表中的未接通项均属于待实现范围。跨模块玩法只维护主城入口与衔接要求，内部规则和进度以所属页为准；关闭项须完成对应入口处理，但不实现原联网业务。下列第一条已由本轮代码部分落实，剩余缺口按原样保留。

- 主流程与 HUD：已接通「正式角色→进入家园」、官方场景与四态家具、顶栏货币、底栏五入口、功能菜单折叠、床铺／小推车／邮箱窗口与存档事务。仍缺：底栏其余四页的真实页面与返回状态、头像美术图与经验环、任务追踪／战斗结果提示、活动挂载、红蓝点与新解锁标记、窗口动画与音频，以及 Play Mode 真实输入验收。
- 设施与场景输入：已按四种状态与点击四边形接通床铺、小推车、邮箱、维修对话框；桌面／餐桌／炼丹炉／女神像／果树／储物箱／售货机／扭蛋机／星命／飞艇仅按四态切换模型并提示所属模块，未接入各自页面。原 `AutoRepairCondition`、非空维修费用与 `CanLevelUp` 行仍按表逐条核对，当前 13 行费用为空、`CanLevelUp=false`。
- 收益与存档：已将 `OfflineHomeIncomeSession` 接入角色启动、退出、运行中累计、完整角色属性（基础＋外观）、家具等级／容量／费用、升级先结算与真实背包（经验走 `AwardExperience`）。仍缺：赐福等级真实来源（女神／探索／古遗物／月卡／星命）、原 `hang_up` 全部免费／道具加速配置的界面选择（当前免费加速按 `fast_hang_up.freeHours` 执行，时间结晶选择未做）、随机装备生成独立实例（当前按权重选物品并叠加数量）。
- 邮箱、随机奖励及回收：邮箱已接入原面板树、未读优先排序、阅读、全部已读与附件领取；随机奖励已解析 29 槽位／类型间隔／组与奖励权重并写入存档，但场景中的动物／落叶对象、模型动画与点击领取尚未实例化。`HomeAwardRetrieve` 回收窗口未接入。
- 教学与角色：接通六张教学图及诺诺九张强制行为图、执行队列、场景站位／动作、对白、按钮引导、镜头／遮罩和实际操作成功回执；保存并恢复当前节点、待执行队列与必要表现，失败不推进教学。
- 环境与装潢：接通原昼夜、受光、灯光分层／互斥、天气、云／月相／积雪／降雪／流星、换肤和固定位置装潢；连接原动画、粒子、材质和音频调用，拍照入口按所属页接线。
- 资源与平台：按[共用 Android → Windows 迁移流程](../asset-management.md#android-资源迁移到-windows)恢复真实官方脚本、第三方依赖及 shader／材质，替换占位组件并核对序列化字段、GUID 和调用关系；覆盖动态选择的家具／主题／天气／奖励／NPC／UI／音频显式加载，完成随包本地交付。已有来源清单的原始输入已取得，不将未接入或占位代码记为官方内容缺失。
- 验证：完成下文所列规则／事务、Editor、Play Mode 逻辑／真实输入／画面与存档恢复验证。静态检查、文件哈希、占位类可编译、自动回调或旧引擎测试均不能代替实际玩法验收。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

- 使用原家园场景、设施坐标、碰撞区域、昼夜关键帧和底栏。拖动超过阈值不触发点击，模态窗口阻断场景输入。
- 设施按原条件显示锁定、维修预告、可维修、正常四态；费用、通知、红点和教学随实际状态变化。女神、餐桌、售货机保留专属对话、遮罩和教学中断恢复。
- 床铺经验和小推车资源各自读取原容量与速率，基础容量36小时；整数产出领取后保留时间余数。升级先结清旧速率，再采用新速率；随机装备生成独立实例。
- 床铺显示数量／容量、圆环和明细；赐福说明预览附近11级，真实产量包含女神、探索、古遗物、月卡、星命及原倍率，不把旧汇总属性伪记为具体来源。
- 加速保留免费次数、时间结晶数量选择、上限确认、原睡眠演出和奖励面板，单种道具单次最多100个。
- 装潢保留原分类、子分类、拥有与预览、固定设施位置及镜头滑条，不增加自由摆放。普通云、月相、积雪网格／遮罩及降雪渐变按已转换原资源运行。
- 环境接入原太阳、天空、全局光的昼夜曲线，逐排序层的灯光、遮挡轮廓、互斥区域和分批开关灯规则；原特效云、颜色／透明／序列材质、果树灯泡、积雪及光晕网格使用实际材质参数。诺诺和教学角色受家园光照；流星按原夜间时段、间隔、概率和粒子模板播放。
- 炼丹炉制作提示检查最佳已解锁恢复料理：少于10份且材料足够才提示；配方学习提示检查实际持有的未学习配方。
- 分解提示按原职业适配、穿戴槽位、装备评分、锁定及5件阈值计算；宝石提示检查低阶配方、最多1颗已镶嵌主宝石、背包材料与金币费用。回收提示使用真实可回收礼物／钥匙；已确认取消的旧赛季宝石回收不再产生提示。
- 邮箱使用原功能52开放条件、窗口、分页、列表、信纸、正文与附件；未读／未领取优先排序，支持阅读、领取、批量已读／领取／删除、到期过滤及已领取部分附件扣除。原猫头鹰随实际收件箱切换有信／无信动画；存档失败回滚库存与邮件状态。
- 动物及落叶奖励使用原29个槽位、设施解锁限制、奖励组和奖励表；8套地面、女神／售货机换肤位置覆盖、层级、朝向、天气皮肤及动物出现／待机／点击动画接入。点击按实际碰撞区域领取，装潢时隐藏。

- 原提示图沿动态选择器及有序端口接入真实邮箱、背包、装备净化、碎片分解、古遗物、伙伴档案／礼物／对话／成就、星命记录及商店状态；实际活动记录为空时不产生活动提示。碎片须达到原培养上限并满足分组条件才提示，料理和商店的新解锁标记按实际查看清除。
- 补齐 `RepaireFurniture2`、`GoHomeUnlockHeap`、`RepaireBed`、`BedSpeedUp`、`RepaireBaGuaLu`、`FirstMergeItem` 六张原教学图的条件、顺序、分支、对话、奖励、镜头曲线、修复窗口、遮罩和真实按钮引导；领取与加速按实际成功回执推进。诺诺沿原九张强制行为图站位、转向及传送，教学角色使用原起床／训练动作；当前节点、待执行队列和可恢复表现随本地存档恢复。固定场景的 `NpcBehaviour.DecisionGraph` 未挂载，不另加自主巡游。

### 依据与代码定位

主流程与交互依据：[MainUIRoot](../../.tools/source-packages/official-code/client-source/MainUIRoot.cs)、[MainViewController](../../.tools/source-packages/official-code/client-source/MainViewController.cs)、[PublicBottomBar](../../.tools/source-packages/official-code/client-source/PublicBottomBar.cs)、[HomeRoot](../../.tools/source-packages/official-code/client-source/HomeRoot.cs)、[HomeUI](../../.tools/source-packages/official-code/client-source/HomeUI.cs)、[TopBarHandler](../../.tools/source-packages/official-code/client-source/TopBarHandler.cs)、[FurnitureBase](../../.tools/source-packages/official-code/client-source/FurnitureBase.cs)、[ClickableHomeObj](../../.tools/source-packages/official-code/client-source/ClickableHomeObj.cs)、[HomeDragController](../../.tools/source-packages/official-code/client-source/HomeDragController.cs)。

功能菜单、特殊装置及环境依据：[FunctionEntryDialog](../../.tools/source-packages/official-code/client-source/FunctionEntryDialog.cs)、[HomeUIActivityEntryController](../../.tools/source-packages/official-code/client-source/HomeUIActivityEntryController.cs)、[HomeMail](../../.tools/source-packages/official-code/client-source/HomeMail.cs)、[HomeFruitTree](../../.tools/source-packages/official-code/client-source/HomeFruitTree.cs)、[HomeRandomReward](../../.tools/source-packages/official-code/client-source/HomeRandomReward.cs)、[HomeAwardRetrieve](../../.tools/source-packages/official-code/client-source/HomeAwardRetrieve.cs)、[HomeTipDotGraphManager](../../.tools/source-packages/official-code/client-source/HomeTipDotGraphManager.cs)、[HomeEnvironmentManage](../../.tools/source-packages/official-code/client-source/HomeEnvironmentManage.cs)、[HomeSkinPanel](../../.tools/source-packages/official-code/client-source/HomeSkinPanel.cs)、[AirshipFurniture](../../.tools/source-packages/official-code/client-source/AirshipFurniture.cs)、[TradePanel](../../.tools/source-packages/official-code/client-source/TradePanel.cs)。

资源迁移脚本 [`prepare_unity_bundle_inputs.py`](../../scripts/prepare_unity_bundle_inputs.py) 从固定APK及已校验原版本CDN准备输入；[`prepare_unity_home_assets.py`](../../scripts/prepare_unity_home_assets.py) 从AssetRipper Unity导出复制场景、家园／邮件UI、家具、主题和随机奖励预制体及GUID依赖闭包，并校验APK内置字体对象。来源输入、转换器版本、原始输出SHA256、Unity重序列化后的Shader哈希、GUID、共享绑定和引用修复登记于[Home Unity来源清单](../../assets/reference/home-unity/source-manifest.json)。[`OfficialHomeAssetTools.ValidateMigratedAssets`](../../unity/Assets/Game/Editor/Home/OfficialHomeAssetTools.cs) 为 Editor 资源检查入口，AssetRipper 使用 YAML shader 导出；其资产检查不证明空占位类具有业务逻辑，Windows 渲染、正式运行时绑定和动态资源覆盖仍须验证。

官方收益依据：[HangUpUtils](../../.tools/source-packages/official-code/common-source/Common/HangUpUtils.cs)、[BattleFormulaHandler](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)、[PlayerHangUpDataExt](../../.tools/source-packages/official-code/common-source/Common/PlayerHangUpDataExt.cs)。

资源与规则依据：[home-materials/source-manifest.json](../../assets/reference/home-materials/source-manifest.json)、[home-player/source-manifest.json](../../assets/reference/home-player/source-manifest.json)、[home-completion/source-manifest.json](../../assets/reference/home-completion/source-manifest.json)、[home-completion/scene-source-manifest.json](../../assets/reference/home-completion/scene-source-manifest.json)、[Home Unity来源清单](../../assets/reference/home-unity/source-manifest.json)、[assets/official/home-completion/ui.json](../../assets/official/home-completion/ui.json)、[OfflineHangUpIncome.cs](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs)。

### 实现完成后的验证范围

- 规则与事务：覆盖原条件的正反分支、家具四态、收益容量／小数余量／升级切换、免费与道具加速限额、真实奖励与背包、邮件部分／批量领取、随机槽生成与满额、存档失败回滚及回执去重；本地时间与随机规则按已确认的单机方案验证。
- Editor：运行 `Zjcs.Editor.ProjectTools.Validate` 和 `Zjcs.Editor.Home.OfficialHomeAssetTools.ValidateMigratedAssets`；核对原场景／prefab层级与引用、实际脚本及序列化字段、shader／材质、动态资源和本地配置。没有 Missing Script 仍不等于恢复了原逻辑，须检查实际方法与调用方。
- Play Mode 逻辑：使用隔离角色存档走通进入家园、设施解锁／修复、打开／关闭各保留页面、领取／加速、邮件／随机奖励、教学中断恢复与切档／重启；确认费用、实际库存／经验、随机状态、回执、时间检查点和角色槽一致，关闭项无法跳入原联网流程。
- Play Mode 输入与画面：实际使用鼠标／键盘、拖拽和滚动检查底栏、菜单、场景及弹窗命中、遮挡与返回；查看真实运行画面或截图，核对原布局、比例缩放、昼夜／天气、换肤、角色／家具动画、粒子／灯光和音频。自动按钮回调不代替真实输入，画面检查不使用 `-nographics`。
- Windows Player：平台 shader、依赖、资源交付或构建／分包链受影响时按需验证离线本地加载与渲染；日常纯文档或功能检查不额外发布成品。完成标准遵循[实际运行验证方法与要求](../asset-management.md#实际运行验证方法与要求)，未完成的层次继续保留未完成状态。
