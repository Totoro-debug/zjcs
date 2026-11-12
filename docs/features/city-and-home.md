# 主城、家园与离线产出

[总索引](../README.md)

状态：未完成

## 功能简介

家园属性初始化与人物页、主线地图共用完整成长装配入口，必需配置及属性对照范围见[人物属性的共享计算边界](character.md#属性来源与共享计算边界)。

主城 Canvas 按原 `UIUtils.GetCanvasScaleFactor` 的宽、高比例最小值使用 `Expand`，避免窄长窗口将横向控件放大裁切。主城玩家改用已有 `CharacterCreationNativeModelFactory.CreateWorldModel`，保留存档穿戴／染色及原家园坐标、缩放、排序，原生 Animator 播放 `home/idle_01`；该模型可直接交由公共战斗模块挂载，避免将带预览画布偏移的网格帧再次套入战斗模型父链。

正式角色进入家园，拖动查看设施，通过底栏切换人物、游记、家园、公会和大陆。本页涵盖家园场景、环境表现、设施教学、提示、邮箱、动物／落叶奖励及床铺／小推车离线收益；果树生产、各设施的养成玩法和商店分别见总索引所列页面。

本页同时维护原始官方主城功能目录与本地待实现清单；各玩法的内部公式、养成与奖励规则只在所属页维护，通过下表引用，不另建主城审计或进度报告。

### 官方功能范围与判定依据

后续迁移以 `official-source.json` 当前选择的 `884_156692` 为输入，真实客户端及共享反编译代码位于 `.tools/source-packages/current/884_156692/official-code/`，来源凭据见[客户端代码清单](../../assets/reference/official-client-code/source-manifest.json)。下文的 `871_156598` 是既有家园转换基线，不能据此宣称已经迁移当前版本；既有运行载荷及原生 prefab 保留，待逐项恢复真实脚本、序列化绑定和运行验证。

官方基线为固定版本 `871_156598` 的客户端、共享代码、配置与 prefab。官方脚本来自 `.tools/source-packages/official-code/client-source/` 和 `common-source/Common/`；`assets/official/ui.json` 保存主城、顶部栏、底栏、头像与功能菜单的控件树，`assets/official/home.json` 保存场景层级、设施位置、相机及点击区域。原始联网功能保留在功能目录中，其单机处理单独注明；官方资源或脚本可取得，不代表已在 Unity 中恢复运行。

| 范围 | 原始官方功能与关键调用关系 | 当前实现与剩余工作 |
| --- | --- | --- |
| 主城进入与生命周期 | `MainUIRoot` 管理主面板；`PublicBottomBar.OnTabTypeChanged → MainViewController.SwitchMainView` 切换 UI 和场景；`HomeRoot` 初始化家园作用域服务、激活场景、同步时间与控制音乐 | 已从创角完成页进入 `HomeFeatureRuntime`，打开角色槽和家园存档，退出可返回角色入口；底栏大陆暂转入当前可玩的初始岛地图。主菜单复原、正式主城生命周期与场景音乐仍未完成 |
| 顶部栏与头像 | `HomeUI` 的 `NewPublicTopBarParent/Slot`、`TopBarHandler`、`TopBarManager`／`TopBarItem` 和 `PublicPlayerIconHome` 展示角色及配置指定货币，随窗口借还顶部栏、响应数量变化并提供点击入口 | 正式入口仍使用历史转换的货币 1/2/15、角色名及等级／公会文本；头像打开人物页。所选 884 原生顶栏已独立恢复货币 1/2/57、原借还与数量更新；原生头像、真实角色头部渲染及受控经验环已独立验证。正式 HUD 接线、真实经验奖励流程及入退场动画尚未完成 |
| 底栏五入口 | `PublicBottomBar.TabType` 包含 `Character`、`TravelNote`、`Home`、`Legion`、`Mainland`；原 `TabGroup`、条件、显隐引用、升级标记和场景代理共同控制切页 | 人物页打开／返回、家园选中和大陆到初始岛已接线；游记、公会入口目前提示对应页面未提供，条件／红点／遮挡未完整恢复。玩法范围分别见[人物](character.md)、[单人公会](guild.md)、[世界地图](world-map.md) |
| 主城任务与战斗提示 | `PinnedTaskPanel`、`MainTaskFunctionLimit`、`GameEndNotifyUI`、`GameEndUIHandlerInHome`、`FightDemonBattlefieldAutoPushEndNotifyUI` 负责任务追踪、战斗完成通知和返回战斗入口；床铺／推车有 `HomeHangUpDirectionGuider` | 已接主线任务追踪、本地进度、奖励领取及 Map10 目标跳转；Map11 提示未开放，职业／宝箱任务不展示不可用跳转；非主线页签显示离线未开放。战斗结算提示和床铺／推车指引未完成，魔物战争按单机范围关闭。任务规则见[主线任务](tasks.md) |
| 功能菜单 | 所选 884 的 `FunctionEntrysPanel.OnEntryBtnClick` 打开并初始化 `FunctionEntryDialog`，基类保留折叠／展开动画与外部点击处理；原菜单有任务、商城大按钮，以及图鉴、成就、装潢、设置、拍照、录像等入口 | 正式入口仍沿历史资源树折叠／展开；所选原生功能入口子树、完整原菜单及原动画／音频已在受控宿主独立验证，正式 HUD 与真实玩法页面接线尚未完成。装潢与设置已接既有页面；网络商店、好友、公告、合服与预下载等显示离线关闭提示。图鉴、成就和拍照尚未接对应页面，内部范围见[商城](commerce.md)、[图鉴](codex.md)、[成就](achievements.md)、[设置／拍照／存档](settings-and-save.md) |
| 活动及推荐入口 | `HomeUIActivityEntryController` 和 `FunctionEntryDialog.GetOpenActivityRecommendEntries` 按真实活动记录生成、排序和展示入口／推荐；`EventEntry` 是主城挂载点 | 未接活动记录与推荐入口；不凭资源生成活动实例或奖励。[签到](sign-ins.md)、[活动小游戏](activity-minigames.md)和[离线开放范围](offline-access.md)维护具体范围 |
| 社交及联网服务入口 | 原 prefab 包含队伍小面板、匹配、申请确认、聊天小窗／快捷消息、好友、排行榜、公告、用户、社交、百科、合服和边玩边下载／预下载等控件；功能菜单对应 `FriendBtn`、`TopListBtn`、`AnnounceBtn`、`UserBtn`、`SocialBtn`、`WikiEntry`、`PlanesMergeFunctionEntryBtn`、`PredownloadEntry` | 已对组队、聊天、好友／社交／用户／排行榜、商城、公告、合服和预下载入口给出离线关闭提示，飞艇交易所也拦截；仍缺部分 prefab 控件的统一关闭显隐和实机逐项点击验收，不生成虚构玩家 |
| 场景浏览与输入 | `HomeCameraController`、`HomeDragController → IgnoreUIDragController` 控制视野与区域边界；`ClickableHomeObj` 按实际 Collider／触摸对象和家园／剧情状态判断有效点击 | 已用官方场景坐标、点击多边形、相机边界与 1 像素拖动阈值重建输入；UI／模态时阻断场景命中。真实鼠标拖动、遮挡和穿透尚未完成 Play Mode 验证 |
| 设施通用状态 | `FurnitureBase.GetState/OnValidClick/DoRepair` 区分锁定、维修预告、可维修、正常，读取 `FurnitureInfo` 的条件、费用、关联设施和维修剧情，响应维修奖励与家园变化并播放修复特效 | 已接家具状态求值、条件与费用维修、场景模型四态及果树专用维修剧情；未覆盖所有家具专属对话、修复特效、红点与完整教学，`CanLevelUp` 等仍按官方表处理 |
| 提示与标记 | `HomeTipDotGraphManager`、`CommonTipDotLinker`、各 `TipDot…ConditionNode` 和家具红点组件使用提示图、动态选择器及实际状态决定红／蓝点、升级、新解锁及可领取提示 | 邮箱未读提示与任务／奖励进度已有局部绑定；官方提示图、装备／伙伴／星命／活动等红蓝点及查看清除条件尚未迁移 |
| 昼夜、天气和主题环境 | `HomeEnvironmentManage`、`HomeClock`、`HomeEnvironmentManagerMono`、`HomeLightController`、`HomeMutexLightLayer`、`HomeCloud`、`HomeSnowController` 等驱动天空、太阳、全局光、分层灯光、云、月相、积雪、降雪与夜间特效 | 已接官方时间倍率、昼夜阶段云层切换、按时刻更新家具灯组、月相贴图及 `Night_Common` 动画轨道；夜间切换控制山体夜光、果树夜灯和售货机昼／夜灯。固定版 `HomeTimeline` 的 `Day_Common` 24 秒、20 条官方曲线、9 个 SpriteRenderer、5 个 Light2D 已提取并进入运行时；当前家园入口已启用工程原 URP 2D 管线；这些历史转换层的光照、材质和最终画面仍待实际验证，不能记为画面完成。动态天气、互斥受光层及环境画面仍未验收 |
| 装潢与拍照 | `HomeSkinPanel` 及主题／环境／家具分类、子分类与提供方管理拥有、预览和穿戴；`HomePhotoSystem` 等提供官方拍照表现 | 装潢已接官方分类／子分类、默认与已拥有物品、穿戴标记和本地保存，并按固定场景槽位应用原家具、主题及天气模型；无自由摆放。实际输入、画面和重启恢复待 Play Mode 核验；拍照页面仍未接，本地范围见[设置与拍照](settings-and-save.md) |
| 动物／落叶与家园 NPC | `HomeRandomReward` 按服务器槽位数据实例化物件并请求领取；`HomeNpcBase` 等管理 NPC 挂载、模型和入口，`HomeAwardRetrieve` 打开奖励回收窗口 | 已装配 29 槽、离线刷新规则、当前开放槽位与落叶／动物首帧，点击领取走本地事务；动物动画、皮肤位置覆盖、家园 NPC 骨骼／交互和奖励回收窗口未完成 |
| 设施教学与角色行为 | `RepaireFurniture2`、`GoHomeUnlockHeap`、`RepaireBed`、`BedSpeedUp`、`RepaireBaGuaLu`、`FirstMergeItem` 六张教学图与诺诺九张强制行为图控制条件、分支、对白、镜头、按钮引导、修复、领取与加速回执 | 六张教学图已加载，已接对白、条件、选项、镜头、家具修复、挂机领取／加速、窗口遮罩和教学奖励 ID 7；未支持节点显式暂停为未支持，家园角色 Spine 动作宿主、诺诺九图和中断恢复完整表现仍未完成 |

### 官方14类装置与待实现入口

类型 ID 取自 [`EFurniture`](../../.tools/source-packages/official-code/common-source/Common/EFurniture.cs)；家具物品 ID 与条件来自 `assets/official/rules.json` 的 `furniture`，邮箱按独立 `HomeMail` 调用链处理，不补造不存在的家具物品记录。表内“未接通”均指当前主城流程，其他页面的独立实现不能代替场景入口验收。

| 类型 ID／家具物品 ID | 原装置及官方行为 | 当前实现与待实现范围／所属页 |
| --- | --- | --- |
| 0／4000 | 床铺 `BedFurniture → FurnitureBedDialog`；经验挂机、产量／容量、领取及加速 | 已接收益、床铺窗口、领取、免费／时间结晶加速及经验入角色成长；赐福等级来源、完整奖励演出和真实输入／遮挡验证仍缺。规则见本页及[人物成长](character.md) |
| 1／4001 | 书桌 `DeskFurniture → TreasurePanel`，进入古遗物收藏 | 四态及点击区已装配，点击仍落在占位提示；收藏入口、原页面和维修剧情未接。内部收藏、任务及培养见[古遗物](relics.md) |
| 2／4009 | 餐桌及 `DiningTableNpcPopClick → NpcPanelUtils.Open(Dialogue)`，伙伴交流；维修剧情 `UnlockCommunication` | 四态及点击区已装配，伙伴对话／页面与餐桌维修剧情、NPC角色表现未接；好感、档案等见[伙伴](partners.md) |
| 3／4015 | 炼金炉 `BagualuFurniture → BagualuPanel`，料理、合成、分解、宝石、馈赠 | 炉体点击接入料理窗口；配方、材料检查、学习与制作事务已实现。合成、分解、宝石、馈赠其余页签及炉体完整特效未接；见[料理](cooking.md)、[宝石](gem-forge.md) |
| 4／4016 | 生产果树 `HomeFruitTreeProduceFruit`；解锁、成熟果实领取、产量／容量，维修剧情 `FruitTreeUnlock1` | 果树复合设施状态、维修／解锁、成熟奖励生成与点击领取已接；官方服务端结果改用已确认离线规则。动画、皮肤覆盖和视觉验收仍缺；见[果树](fruit-tree.md) |
| 5／4004 | 女神像 `GachaFurniture → GachaPanel`，技能祈愿及相关对话／演出 | 场景四态点击接本地技能祈愿页；女神专属对话／教学演出及完整 statue 特效未接。祈愿玩法见[技能祈愿](wishes.md) |
| 6／不另造物品 ID | 邮箱 `HomeMail → MailDialog`；原功能52开放条件、邮件列表／正文／附件及有信／无信表现 | 功能 52 条件已检查；本地窗口支持阅读、附件与批量读／领／删，初始收件箱保持为空。猫头鹰动画和本地投递调用方未接 |
| 7／4018 | 储物箱 `StoreBoxFurniture → PlayerBagPanel`，打开行囊 | 四态场景点击打开 `InventoryFeatureRuntime` 并刷新角色行囊；后续物品规则见[行囊](inventory.md) |
| 8／4013 | 售货机 `ShopFurniture → HomeShopPanel`；官方另有访客商店分支，维修剧情 `SenSenHomeStore` | 杂货 9 个随机位与地图解锁固定商品、宝库 12 格按官方分组生成、折扣/刷新和本地馈赠事务已接；访客商店关闭，无独立“特卖”页。`SenSenThanks` 的剧情与 NPC 进度使用角色真实存档；地图 11 交互尚未开放时保持条件未满足。随机结果及真实输入/画面仍待 Play Mode 验收。见[家园商店](home-shop.md) |
| 9／4007 | 时光扭蛋机 `ChestGachaFurniture → ChestGachaPanel`，古遗物宝箱／女神牌抽取 | 场景点击接入宝箱扭蛋页与本地奖池事务；原效果／结果演出和 Windows 画面验证仍待完成。奖池等见[古遗物扭蛋](treasure-gacha.md) |
| 10／4011 | 小推车 `CartFurniture → FurnitureCartDialog`；资源挂机、产出明细、领取及加速 | 已接产物、容量／等级／费用、明细窗口、领取、免费及道具加速；随机装备目前按官方条件选择物品，尚未生成独立装备实例 |
| 11／4017 | 幻兽果树设施 `HomeFruitTreePet.TryCallFunction → PetGachaPanel`；维修剧情 `FruitTreeUnlock2`，原附加维修列表包含物品 4016 | 维修按自身条件判断；附加列表不作为前置条件，按已确认单机方案只维修 4017 并开放结缘。官方角色演出和完整皮肤／提示仍缺。条件见[果树](fruit-tree.md)，抽取及商店见[幻兽结缘](pet-gacha.md) |
| 12／4019 | 命运星象 `AstrologicalDestinyWindowFurniture → AstrologicalDestinyEntryPanel`，按原赛季／位阶配置判断入口 | 四态和点击区、官方页面、Rank→Season 门槛、520 级契约灌注及本地累计属性已接代码；真实输入／画面／存档未验。赛季历史、结算与商店货架没有伪造，见[星命](astrological.md)与[赛季](seasons.md) |
| 13／4020 | 飞艇 `AirshipFurniture → TradePanel`，含交易所、拍卖、交易账簿、特权及规则入口，设施另有激活动画、展示物品与灯光 | 点击被离线策略拦截并提示交易／拍卖关闭；飞艇专用显示和灯光状态尚未恢复，不开放联网业务。范围见[离线开放](offline-access.md) |

## 原始官方实现

官方主场景与家园 prefab 定义场景层级、昼夜天气、设施、NPC、装潢、教学及 HUD；设施状态、产出、领取和升级分别读取原配置。

### 待迁移的规则与表现

- 使用原家园场景、设施坐标、碰撞区域、昼夜关键帧和底栏。拖动超过阈值不触发点击，模态窗口阻断场景输入。
- 设施按原条件显示锁定、维修预告、可维修、正常四态；费用、通知、红点和教学随实际状态变化。女神、餐桌、售货机保留专属对话、遮罩和教学中断恢复。
- 床铺经验和小推车资源各自读取原容量与速率，基础容量36小时；整数产出领取后保留时间余数。升级先结清旧速率，再采用新速率；随机装备生成独立实例。
- 床铺显示数量／容量、圆环和明细；赐福说明预览附近11级，真实产量包含女神、探索、古遗物、月卡、星命及原倍率，不把旧汇总属性伪记为具体来源。
- 加速保留免费次数、时间结晶数量选择、上限确认、原睡眠演出和奖励面板，单种道具单次最多100个。
- 装潢按原分类、子分类、拥有与穿戴保持固定设施位置，不增加自由摆放；普通云、月相、积雪网格／遮罩及降雪渐变按已转换原资源运行。
- 环境接入原太阳、天空、全局光的昼夜曲线，逐排序层的灯光、遮挡轮廓、互斥区域和分批开关灯规则；原特效云、颜色／透明／序列材质、果树灯泡、积雪及光晕网格使用实际材质参数。诺诺和教学角色受家园光照；流星按原夜间时段、间隔、概率和粒子模板播放。
- 炼丹炉制作提示检查最佳已解锁恢复料理：少于10份且材料足够才提示；配方学习提示检查实际持有的未学习配方。
- 分解提示按原职业适配、穿戴槽位、装备评分、锁定及5件阈值计算；宝石提示检查低阶配方、最多1颗已镶嵌主宝石、背包材料与金币费用。回收提示使用真实可回收礼物／钥匙；已确认取消的旧赛季宝石回收不再产生提示。
- 邮箱使用原功能52开放条件、窗口、分页、列表、信纸、正文与附件；未读／未领取优先排序，支持阅读、领取、批量已读／领取／删除、到期过滤及已领取部分附件扣除。原猫头鹰随实际收件箱切换有信／无信动画；存档失败回滚库存与邮件状态。
- 动物及落叶奖励使用原29个槽位、设施解锁限制、奖励组和奖励表；8套地面、女神／售货机换肤位置覆盖、层级、朝向、天气皮肤及动物出现／待机／点击动画接入。点击按实际碰撞区域领取，装潢时隐藏。

- 原提示图沿动态选择器及有序端口接入真实邮箱、背包、装备净化、碎片分解、古遗物、伙伴档案／礼物／对话／成就、星命记录及商店状态；实际活动记录为空时不产生活动提示。碎片须达到原培养上限并满足分组条件才提示，料理和商店的新解锁标记按实际查看清除。
- 补齐 `RepaireFurniture2`、`GoHomeUnlockHeap`、`RepaireBed`、`BedSpeedUp`、`RepaireBaGuaLu`、`FirstMergeItem` 六张原教学图的条件、顺序、分支、对话、奖励、镜头曲线、修复窗口、遮罩和真实按钮引导；领取与加速按实际成功回执推进。诺诺沿原九张强制行为图站位、转向及传送，教学角色使用原起床／训练动作；当前节点、待执行队列和可恢复表现随本地存档恢复。固定场景的 `NpcBehaviour.DecisionGraph` 未挂载，不另加自主巡游。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

主城运行时由 `HomeFeatureRuntime` 接入。官方场景、UI、角色动画、配置和音频来源及显式加载绑定见 [来源总表](../../assets/reference/SOURCES.md) 与同目录代码、规则和音频清单。

| 来源范围 | 实际使用的 APK 资源 |
| --- | --- |
| 家园 | `prefabs_home_dynamic_theme_*`、`prefabs_home_dynamic_furniture_*`、`scenes_main` 的原始对象及依赖图集 |
| UI | `prefabs_ui_home/furniture/public/functionentry/setup/character/playerbagpanel/gachapanel/skill/pet/shop/task/mainlandpanel/top/message/itemdetail/item/common` 的指定原始 prefab |
| 图标 | 原始 `image_itemicon_*` 中按 `item.RawIcon` 定位的 Sprite；缺少的七个商店商品图标没有替代图 |
| 字体 | `defaultpackage_assets_textmesh_pro_rawfonts_fzy4k.bundle` / `FZY4K` TextAsset 原始 TTF |
| 配置 | `defaultpackage_assets_config_luban.bundle` 中原始二进制表；根据 APK `Console.dll` 生成的 Luban 构造函数解码并严格校验消费字节数 |
| 音乐 | `wwisebnk_assets_wwisedata_android_home_bgm_01_pck.rawfile`，bundle hash `91efbabff5375669114cd914b7e6fa6c`，media `946883943` |

逐资源身份和输出 SHA256 见 [来源总表](../../assets/reference/SOURCES.md)，配置证据见 [来源总表](../../assets/reference/SOURCES.md)，程序集证据见 [来源总表](../../assets/reference/SOURCES.md)，音乐见 [来源总表](../../assets/reference/SOURCES.md)，基础包可用性见 [来源总表](../../assets/reference/SOURCES.md)。

家园运行层从既有 `assets/official/home.json`、`home-completion/{scene,ui}.json`、`rules.json`、`home-random-rewards/catalog.json`、`home-mail/ui.json`、`home-tutorial/talk.json` 等已登记输出整理出 `unity/Assets/StreamingAssets/Home/{scene,ui,rules,rewards,mail,actors,environment,tips}.json` 并复制被引用贴图。主城 FirstMergeItem 固定奖励 ID 7 的物品图标同时取自已按 SHA256 登记的固定奖励表，奖励 ID、物品 ID、输入哈希及每个输出哈希记录在 Home `source-manifest.json`。生成器为 [`prepare_unity_home_runtime.py`](../../scripts/prepare_unity_home_runtime.py)；该步骤不重新解析 APK、不新增或替换美术，运行时按显式清单加载这些本地文件。

`.tools/` 保留官方缓存和提取依赖，游戏运行不加载 DLL，不使用 Python / Node / vgmstream。

**尚未完整还原全部功能。

追加提取：原始 `ShopActivityEntryPanel`（含直购、两款月卡）、`PayDirectPayItem`、`ActivityEntryPanel`、`ActivityCommonEntry`、祈愿职业选择、积分奖励及商品详情等 13 个 prefab；`direct_pay`、`periodic_card_pay`、`money_cost`、每日 / 每周礼包、签到奖励及活动入口共 8 张配置表。新增图标仍按原始 RawIcon 定位；`prepare_optional_interfaces.py` 从 `image_ui_chestgacha_chestelementgacha_atlas` 和 `image_ui_treasure` 提取原始 Sprite。当前共 1,317 项运行时资源、1,052 项图标映射、69 个 prefab、75 张完整配置表。每项身份和输出哈希均记录于来源清单，动态图片进入显式加载清单。

完整 `defaultpackage_assets_prefabs_ui_chestgacha.bundle` 及 `image_ui_chestgacha_chestgachabg_*` 背景仍未打包，不能称为已取得完整原界面。家园食材 `item_8051` 至 `item_8057` 分别位于未打包的 `image_itemicon_materials_map12/13/14/17/19/21/23_noatlas.bundle`，保留文字、价格和原始解锁锁头；没有替代图片。两类缺项分别记录于 source-manifest.json 的 `chest_gacha_interface` 和 `shop_ingredient_icons`。活动内容采用本地入口与七日签到布局，月卡目前只实现立即 / 每日奖励及有效期，未宣称额外属性特权生效。

`prepare_bed_ui.py` 从固定 APK 的 `prefabs_ui_furniture.bundle` 转换 `BlessingLevelRuleDialog` 及其内嵌 `RuleFormDataItem`，复用六项原始 Sprite 来源记录，未绘制替代图片。运行时恢复 `BlessingRuleBtn` 的规则弹窗、`FurnitureBedDialog` 两组独立折叠项，并按 `BlessingLevelRuleItem` 读取当前赐福等级附近 11 级的原始基准产量和小推车上限。

床铺圆环按 `FurnitureBedDialog.HangUpTimer` 的累计量 / 容量比例绑定，使用本地同一产量预览的储存上限，打开和累计时刷新、领取后更新，清除空储存的可领取提示。文本更新后重排，避免累计数量变长而裁掉数字。保留原图、原填充方向及颜色。

本地存档只保存汇总产量属性，加成展开项以实际参与计算的比例、固定经验和最终比例显示，不伪造月卡或古遗物等来源分项。规则说明文字、表头换行及 1100 高的滚动区域为桌面适配；弹窗、预览行与素材来源仍为固定 APK。

`prepare_home_ui.py` 从固定 APK 及匹配清单的 CDN 依赖转换原床铺、推车详情／升级／规则、装潢 prefab 与原 ItemIconManager／ItemAwardIconManager 配置，运行数据为 `assets/official/home-completion/ui.json`。40 个原窗口／引用模板、118 个资源的真实 bundle、对象、输入与输出 SHA256、原布局字段及语言来源统一保存在 `assets/reference/home-completion/source-manifest.json`。动态推车背景、升级图标、装备概率预览图标按原 LanguageUtils 路径／原 ItemInfo.RawIcon 取图；复用共同素材仅在 PNG 字节完全一致时进行，无重绘或替代。组件绑定、原 RectTransform、LayoutElementClamper、状态配置与原奖励图标池均保留。

`scripts/prepare_home_scene.py` 从固定 871_156598 APK／校验原 CDN 读取 12 类默认设施，转换全部原状态分支、原碰撞、Spine、粒子与世界文字至 `assets/official/home-completion/scene.json`；来源对象、原始组件 SHA256、语言输入及资源／目录输出哈希见 `home-completion/scene-source-manifest.json`。未绘制替代图片。`prepare_home_ui.py` 另按原引用补入 FurnitureRepairDialog、FurnitureRepairedNotifyDialog、原 FurnitureIconWidget、PlayerPropItem 与 PopupWindow 入场／循环绑定，仍登记在 `home-completion/source-manifest.json`。运行加载绑定来自两份来源清单；独立导入复用一致的共享贴图。

prepare_home_ui.py 继续从固定 871_156598 转换 AwardListPanel、其序列化 normal-path GUID fe14ea0fc3099824683283c4671a9e49 指向的 AwardListPanelItem、原 AnimationClip 事件和 UI ParticleSystem 材质／纹理。UIParticle 的 14 个原 AOT 字段次序及 payload 全量读取由 prepare_map_ui_particles 校验；home_particle_schema.py 的独立 Coffee 类型只解析对象引用，粒子本体仍读取原字节，没有生成替代效果。home_ui_audio.py 沿原 Main_Get_2 Event 3442720740 的 Play／Sound／DIDX 媒体，使用 vgmstream 与 libsndfile 转为本地 Vorbis。源对象、包凭据、原始／输出 SHA256 见 home-completion/source-manifest.json，运行资源在 assets/official/home-completion/。

prepare_home_decoration.py 从固定 871_156598 的 64 行 furniture_skin、19 行 home_theme_skin 和 48 行 home_skin_weather_override_prefab，按原 LanguageUtils 路径定位 131 个实际 prefab 变体；原 HomeEntityManagerMono／HomeSlotEntityData 提供主题及家具槽位，原 HomeEntityCreateData 提供位置偏移。保留全部状态分支、CustomRenderer 云层、原果树果实附件及昼夜动画，骨骼沿既有 Spine 转换工具生成原网格。输入对象、APK／CDN 凭据、916 个资源和输出 SHA256 见 home-decor/source-manifest.json，实际资源在 assets/official/home-decor/。材质属性动画的哈希字段按原材质字段及 AssetRipper CustomCurveResolver 的 Unity 格式解析，没有自拟游戏规则或美术。`prepare_unity_home_skins.py` 从这些转换结果生成固定版运行目录 `unity/Assets/StreamingAssets/Home/home-skins.json`，并按实际引用复制 628 张官方贴图；运行输出和逐张贴图 SHA256 写入家园 `source-manifest.json`。

prepare_home_ui.py 同时补充原 HomeViewController／Slider 控件、HomeSkinItem／ItemIconHomeSkin 字段，沿 ItemIconAttachmentConfig 的 WearingHomeSkin=31 GUID 提取原已装备附件，从原 image_itemicon_home 提取对应皮肤图标；来源继续登记于 home-completion/source-manifest.json。来源登记与尚未迁移的灯光／材质表现范围分别记录，不能将已提取视为已完整还原。




prepare_home_tips.py 遍历 131 个原装潢 prefab 变体中的 TipDotNodeLinker，沿原 PPtr、有序 Childs 端口和嵌套动态选择器取得当前294个实际提示节点，保存条件字段、原优先级和状态覆盖规则。运行时由 official_home_tips／official_fruit_tips／official_home_tip_queries 接入真实系统查询；活动与星神仅在存在对应实际玩家记录时可产生子项，不生成虚构实例。

ItemNewMarkManager 的零到正事件及登录清空、HomeSkinItem 离开列表清标记、HomeSkinMainTabItem／HomeSkinSubTabItem 分类聚合和 AppearanceDataEx 排除默认皮肤均按固定 871_156598 迁移。真实源码 SHA256 保存在 home-completion/source-manifest.json 的 mark_sources，由 prepare_home_ui.py 维护；继续使用原 NewMarkStateSwitcher、原文字和布局，没有新增替代图案。

prepare_home_ui.py 从原 prefabs_ui_itemdetail 的 FurnitureDecorationItemDetailDialog 提取完整布局和 mTryBtn／mFashionTxt／mIsShowFashion，按原 FashionExpUtil 和三类皮肤解析器取得 fashion_exp_group 的真实品质经验。home-completion/source-manifest.json 的 detail_sources、fashion_table、panels、inputs 与 resources 登记原版本代码、配置、对象和输出哈希。没有改用普通物品详情或自制预览按钮。

`assets/official/home-tutorial/` 由 `python scripts/prepare_home_tutorial.py` 生成；来源固定 `871_156598` 的 `prefabs_ui_story/OptionTalkDialog`、`TalkDialog`、`prefabs_ui_portrait/Npc_Portrait_8402` 与 `Npc_Portrait_8411`，及其实际GUID引用主图／表情。26个输出含25张原PNG和一份原层级／字段／动画数据；保留真实SetNativeSize、表情差异偏移、左右翻转与动画事件，无自绘或截图代替。源bundle、对象和输入／输出哈希、语言及调用代码登记于 `home-tutorial/source-manifest.json`。运行时家园教学使用原Common代理对应的OptionTalkDialogPanel，并复用 `map-audio.json` 已登记的原中文语音。独立导入：`python scripts/import_official_subset.py assets/reference/home-tutorial/source-manifest.json`。

家园对话平铺背景：原 OptionTalkDialog 的 BgImg／BgImg2 与选项按压背景为 Image.Type.Tiled，APK 的 FillCenter 与 PixelsPerUnitMultiplier（该批为1）由 prepare_official_city.py 读取，原 Sprite 边框及尺寸保留。 按 Unity 官方 uGUI Image.GenerateTiledSprite／GetAdjustedBorders 迁移平铺、边框缩小、末块UV裁切和65000顶点限制，反转PNG纵轴并保留原Sprite裁切范围；未绘制新图。框架源为 https://raw.githubusercontent.com/Unity-Technologies/uGUI/main/com.unity.ugui/Runtime/UGUI/UI/Core/Image.cs ，取得内容SHA256为74a8d2f63d1ea3783d34f2ba9d778191cc9b27bea5cfa0670d69f1defd9e29dd，缓存于 .tools/source-packages/official-code/ugui-reference/（Image.cs与source.json）。这是Unity官方UI框架来源，与游戏客户端来源分别登记；对话原图及实际输入／输出哈希见home-tutorial/source-manifest.json。重建需恢复该框架来源缓存；已转换对话记录Image原字段，其他尚无tiling字段的旧转换不作参数推定。

`home-environment/source-manifest.json` 登记主场景 HomeEnvironmentManagerMono.HomeMoon 的三个 SpriteRenderer、HomePlayable 的 Night 绑定及 Night_Common 中9条月亮曲线。原 `image_home_dynamic_moon` 分包解码配置会选择的1至15与17号月亮／描边／光晕，共48张PNG；连同 moon.json 共49项输出逐项登记SHA256，保留原Sprite尺寸、裁切、轴心、PPU、世界层级及排序。规则来源为 HomeMoon、HomeEnvironmentManage、HomeTimeDriver、HomePlayable/TimelinePlayable 和原 HomeEnvironmentConfig／HomeSkinConfig：自然日30天一轮、每相2天、08:00刷新、天气4902选择17号；阶段显示小时不改变自然日。转换命令 `python scripts/prepare_home_environment.py`，导入使用 `python scripts/import_official_subset.py assets/reference/home-environment/source-manifest.json`。原3个材质均为 Sprite-Unlit-Default；场景照明、云网格和积雪材质的实际迁移见后续相应来源段落。

`home-environment/solar-timeline-source-manifest.json` 登记固定 APK 主场景 `HomeTimeline`（Timeline path ID 188）及 Day 绑定的 `Day_Common`（AnimationClip path ID 181）。`python scripts/extract_home_sky_timeline.py` 按原 Mecanim 绑定解出20条曲线、21个节点、9个SpriteRenderer、5个Light2D和两种原材质，恢复 Sprite 裁切偏移与逻辑尺寸，所有输入／输出记录 SHA256；`python scripts/prepare_unity_home_environment.py` 校验后复制 JSON 与 PNG 至 Unity `StreamingAssets/Home/environment-visuals/`。曲线包括太阳位置／缩放／显隐、Sprite透明度、Light2D颜色／强度／阴影强度，保留原 Hermite 切线。原材质 shader 为 URP `Sprite-Lit-ColorShift` 和 `Sprite-Lit-Default`。当前家园入口已通过 `OfficialBattleRenderPipelineAssets.Enter` 启用工程原 URP 2D 管线；[`HomeSkyTimelineView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSkyTimelineView.cs) 的这些历史转换层仍需单独核对光照及最终画面。

`home-environment/sky-source-manifest.json` 从固定版背景prefab的真实Material引用提取唯一 Sprite-Lit-ColorBlend 的GLES程序、8种背景材质及对象／分包／原始字节哈希，重建使用 `python scripts/prepare_home_sky.py`。全部实际材质 `_UseMask=0`；ShapeLight纹理及场景照明绑定尚待迁移，运行时仍未启用该输入，不能据此称环境光已还原。

`python scripts/prepare_home_npc.py` 从原主场景 NpcBehaviour 的 HomeActionBehaviourCatalog 转换全部9个动作图及2个原传送Playable；原节点顺序、坐标、朝向、等待、显隐、HomeMid02/10排序、21个粒子发射器参数均保留。HomeNpcModel_8402 使用原0.4管理缩放、0.72子节点缩放、body_8402皮肤与原分离动画 npc_8402/idle_01，通过官方Spine原生扩展持续计算；复用已校验的原版 map_9 分包NPC图集。20项新输出含16张PNG、原粒子网格、Spine二进制／atlas及运行数据；输入、图集复用、真实原MeshRenderer材质／shader与输出哈希登记 `home-npc/source-manifest.json`。MyPlayableCenter／Sequence／Manager 的原程序集反编译代码说明传送使用普通帧时间；NPC Animator 使用 HomeTimeDriver 的 PlayableDeltaTime；行为图Wait使用自然家园时间。原SkeletonEffect-2D编译程序和场景光绑定见“家园灯光与材质迁移”。提取器按原pass的m_BlobIndex区分可执行程序与参数块，不再误读参数块为GLSL。

`python scripts/prepare_home_clouds.py` 从固定版 Background／Background1stAnniversary 及其雪／永夜prefab提取实际挂载的39个CustomRenderer。保留原Sprite.vertices／triangles、轴心、PPU、裁切UV、层级矩阵与颜色／HSL；复用原PNG，不新增图。运行数据为 `home-environment/clouds.json`，来源清单 `home-environment/cloud-source-manifest.json` 登记实际Sprite／Texture／材质、程序集代码、两份原CR-Lit-ColorShift GLES程序及输入／输出哈希。39个云网格的实际昼夜曲线接入shader。固定版这些实体及主场景未挂载HomeCloud，不根据同名未使用类添加遮罩或Shrink规则。场景光纹理及实际挂载的其他特效云材质见“家园灯光与材质迁移”。

`python scripts/prepare_home_snow.py` 读取固定版雪景实际挂载的1575个SpriteRenderer，保留紧密网格、原三角形、Sprite／SpriteAtlas的uvTransform、翻转及原矩阵；复用原数据中的层级、颜色和状态。52份独立原材质、64张完整原Texture2D及原HomeObjectSnow的两份GLES程序登记于 `home-snow/source-manifest.json`，运行数据为 `home-snow/snow.json`。保留原透明扩展、双雪色、细节平铺、Param和LightAtten分支。HomeWeatherSnow按原OnEnable满雪、OnChangeCreate两秒自然家园时间渐积雪及三组原粒子maxParticles曲线接入；无新增绘图或自定渐变。场景光纹理接入见“家园灯光与材质迁移”。

`python scripts/register_home_assets.py` 仅按17份既有独立来源清单核对固定871_156598资源及转换输出后补齐总清单；PNG等验证原bundle清单MD5，音频额外核对APK内Wwise包MD5／SHA256及原media片段SHA256。已有资源或数据哈希冲突直接报错，不覆盖旧期望哈希。云／积雪shader与转换记录同步核对，`--check`只读检查登记及完整加载绑定；保留各模块完整来源凭据。月相／NPC／教学89张图的加载绑定恢复为按需加载，使用方式与其他官方资源一致。

`prepare_home_mail.py` 直接迁移固定版本 `prefabs_ui_mail` 的8个原窗口／条目／分页／信纸预制体，保留布局、原贴图、状态切换、正文、附件和动画绑定。猫头鹰使用原 `HomeMailDefault.NoMailAnim/HaveMailAnim` 两套骨骼动画；原输入对象、UI 布局、语言、代码与输出哈希见 `assets/reference/home-mail/source-manifest.json`。邮箱规则按 `PlayerMail`、`MailUtils`、`MailDialog` 与 `MailContentUiGiveItems` 接入本地存档，不生成运营或社交邮件。

`prepare_home_materials.py` 从固定 `871_156598` APK／匹配 CDN 的实际家园主题、设施和主场景 Sky 读取 Light2D、ShadowCaster2D、HomeLightController、HomeMutexLightLayer、HomeMutex、HomeBloom、RenderExistingMesh，以及 Day_Common／Night_Common。保留原对象矩阵、逐排序层配置、灯光形状／cookie、FalloffLookupTexture、遮挡轮廓、颜色／强度／阴影曲线和灯光时段覆盖。包含原 Sprite-Lit 系列、Sprite-Bulb、Skeleton-Lit-Atten、Skeleton Lit、SkeletonEffect-2D、Spine-Bloom、HomeSpineOnlySnow、Tree-Bulb，云和物体积雪继续使用原独立端口并接入同层光照纹理。25个原 RenderExistingMesh 的引用及材质替换用于积雪／光晕覆盖网格，不自绘替代。

严格字段读取、原字节与输出 SHA256、24种实际着色程序、诺诺原材质、原生 `Spine.Extension.RenderExistingMesh.UpdateMaterials` 0x10967160 的 metadata 对应记录见 `home-materials/source-manifest.json`。辅助核对使用 Unity 官方 Graphics 仓库 2022.3/staging 的 URP Runtime/2D 六份源码，真实下载 URL 与各自 SHA256 在该清单的 urp_sources 中单独登记，不冒充固定客户端源码。

`prepare_home_player.py` 从固定主场景 HomeEnvironmentManagerMono 的实际引用取得 StoryWakeUp／Training 两张行为图、男女角色原分离动画 home/wakeUp_01／100/training_01、角色变换、HomePlayerSpineLightAtten、HomeRenderRuleAsset、主题锚点及 HomeMeteor 的时段／概率／间隔和原粒子模板。角色沿当前真实外观、职业与染色组装。运行素材为 `assets/official/home-player/`，输入输出、图集、生成脚本和原版本信息见 `home-player/source-manifest.json`。

流星主体来自原版871_156598；其一个原版缺包依赖使用经独立校验、同 CAB 的864_156496官方同名包，实际对象、包哈希及来源 URL 在 counterpart_dependencies 中登记。新版资源未混入原缓存，不新增自绘。教学只调用原实际挂载的行为图，不推测未挂载的自主动作。

／ 复用固定 `871_156598` 已完整转换的 `adventure.json` 中 RepaireFurniture2、GoHomeUnlockHeap、RepaireBed、BedSpeedUp、RepaireBaGuaLu、FirstMergeItem 六张图、原故事表和对白；原输入沿 `official-map/story-source-manifest.json` 追溯。原 OptionTalkDialog／TalkDialog 及立绘复用 `home-tutorial/source-manifest.json`，修复／成功通知和遮罩复用既有原控件，未新增替代素材。诺诺九张强制行为图、真实动作及原主场景 NpcBehaviour.DecisionGraph 空引用沿 `home-npc/source-manifest.json`；教学角色的两张实际动作图沿 `home-player/source-manifest.json`，没有添加未挂载的自主行为。

## 完整还原部分

主页进入技能与伤害测试时保持原 URP 2D 管线，并在退出时恢复前一管线；主城输入与消息在人物页或测试战斗期间停止穿透。`HomeRuntimeShaderBindings` 显式绑定主城流星特效、经固定 APK 原对象哈希核对的天空 ColorShift Windows 转换及 URP 引擎 Sprite-Lit shader，防止独立 Player 裁剪仅按名称加载的程序。主城功能入口恢复原 `FunctionEntrys.controller` 默认的 `FunctionEntrysUI_Folded` 零时长姿势，避免展开按钮遮住收起按钮；战报不另接主页历史入口。

当前完成范围是官方载荷恢复和部分本地底层，没有主城整模块已在 Unity 完整接入并验收的结论。

当前热更新 `871_156598` 已重新转换家园原生资源、图层、材质、配置和语言，来源分别记录于 `assets/reference/home-unity/source-manifest.json` 与 `assets/reference/official-map/current-*-source-manifest.json`。`HomeUiRenderer.CreateNode` 使用完整官方 Sprite 边框、pivot 和像素比例；`HomeAssetCatalog.FindSprite(HomeSpriteRef)` 按完整引用缓存，避免先加载纯贴图时丢失九宫格边框。官方 `TabItem` 映射为可选中的 Toggle。官方 Mask 的启用和显示标记已补回载荷，渲染器保留不显示遮罩图片的配置。使用 Unity 模拟输入，没有操作系统鼠标／键盘检查，不代表本页全部目标功能已完成。

`unity/Assets/Official/Home/` 已保存来源清单登记的2,001个输出文件、265个目录及49个跨模块共享脚本绑定；输出包含1张场景、139个 prefab、261个 C# 文件及贴图、动画、材质等。文件哈希和 `.meta` GUID 与[来源总表](../../assets/reference/SOURCES.md)匹配；Unity 重序列化的内置 `Sprite-Lit-Default.asset` 保留原始及导入后哈希。261个本目录 C# 文件全部是带 `Dummy class` 注释的 AssetRipper 空占位类，包含 `HomeRoot`、`HomeUI`、`PublicBottomBar`、`BedFurniture`、`CartFurniture` 和 `HomeRandomReward`，不能作为官方逻辑恢复的证据。官方真实方法体可在反编译目录取得，须迁移到 `Assets/Game/Runtime/Official/` 或对应适配／展示层并重新绑定；不在 `Assets/Official/` 编写手工运行时代码。

[`OfflineHangUpIncome`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs) 实现小时产量、容量、整数领取与单向时间检查点计算。新增 [`OfflineHomeIncomeSession`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs)，从调用方传入家园收益配置及角色属性，打开时按本地 Unix 时间补算并受存储时长上限限制；领取和加速奖励通过候选存档副本校验后原子写入本地 JSON，重复回执不会重复发放。新存档从打开时刻建立检查点，不会从 Unix 纪元错误累计；存档 schema 1 可迁移到 schema 2。加速收益按已确认的单机规则直接进背包，不受家具存储上限限制。

家园打开顺序为先恢复家具／挂机解锁和等级，再由 `configureBeforeAdvance` 装配真实收益与随机奖励目录，最后推进本地时间检查点；不再用零产量占位目录提前消费离线时段。配置失败时尚未推进检查点或写入收益存档。`HomeIncomeRestoreValidation.Run` 已在隔离 Unity 2022.3.57f1c2 工程通过存档恢复、按已存等级补算、36 小时封顶、领取余数、重复回执、同时间重开、时钟回退及配置失败不写盘回归；该范围属于收益规则验证，家园窗口的 Play Mode 模拟点击、画面与 Windows Player 验证仍未完成。

同一存档提供官方随机奖励类型/组/槽位定义、领取回执和随机状态；Unity 已装配 `home_random_reward_type` 刷新区间/上限、29 个槽位及奖励组权重，15 个当前开放槽位可生成动物/落叶，领取按本地事务扣槽并进入背包。官方服务端最终奖励裁决未包含在客户端，因此离线奖励使用已确认本地权重规则。邮箱本地读信、到期过滤、未读优先、单封/批量已读、附件领取和删除均已接存档事务，初始邮箱保持为空。六张官方教学图与步骤／队列已加载，执行器只完成已登记节点；不支持节点会显式暂停，不默认成功。

免费加速按 `fast_hang_up` 官方次数/时长配置和本地日期记录执行；加速窗口使用官方 `fast_item_row` 时间结晶滑杆（数量 1–100/持有数/配置上限取最小值），一次扣除选定数量后批量结算所有已解锁挂机项，并以成本/产出回执防止重复扣领。

收益依据来自官方 `HangUpUtils`、`BedFurniture`、`CartFurniture` 与 `BaseHangUpFurniture`。当前 Unity 已由 `HomeFeatureRuntime` 打开角色槽／家园存档，读取基础与外观属性，按官方挂机表累计／封顶并把领取物桥接到角色背包；床铺经验物品交给 `AwardExperience`，时间结晶数量选择和单次上限已接入。仍缺赐福等级真实来源，以及小推车随机装备独立实例。

### 家园运行层接入

所选 `MainCanvas` 的完整 `HomeUI` 子树已恢复为 `Assets/Official/Home/UI/Hud/HomeHud.prefab`。原 923 个对象中，按已确认的[离线范围](offline-access.md)及[设置范围](settings-and-save.md)移除队伍小面板、申请确认、聊天小窗和快捷聊天四棵子树，保留 651 个对象；裁剪只移除对应子树及其引用，其他原坐标、锚点、组件字段、动画和加载引用保持原值。任务条目继续引用原外部 `TaskListItem`，顶栏挂点、功能入口、任务区、匹配状态、战斗通知与三类场景指引均保留。该 prefab 已通过 Unity 导入、全部脚本引用检查及统一宿主的受控启用／交互验证，尚未接入正式 HUD 的本地业务服务。

`Assets/Official/Home/UI/HudFrame/HomeHudFrame.prefab` 恢复 HomeUI 及公共头像／底栏的原父级，包括两层 `Anim`、CanvasGroup、公共安全区与 `PlayerInfoMask`，共 39 个对象；不以测试位置代替原公共父级。`HomeScreenUI.StencilMask` 直接迁入原复制、材质、排序、显隐及颜色逻辑，只将场景容器实例化映射为本地 Unity 实例化；原 prefab 的 stencil 值为 6、初始隐藏。`HomeHudFrameNativeValidation.Run` 已在隔离完整工程实际 Play Mode 验证激活后的一次复制、`UI/Default` 的 `_StencilComp=8`／`_Stencil=6`／`_StencilOp=2`／`_ColorMask=0`、排序 -1、原父级、重复初始化和显隐恢复，已查看实际运行截图。测试静音、无真实系统输入；隔离完整工程通过 Unity 导入／C# 编译及 `Zjcs.Editor.ProjectTools.Validate`，静态检查通过（150568 个 GUID）。这项测试仅证明公共框架和遮罩；完整 HomeUI 的受控宿主验证见下文，正式业务接线及 Windows Player 尚未完成。

`Runtime/Offline/HomeHudUI/OfflineHomeHud` 负责完整原生层级的实例化及释放，在所有组件启用前显式绑定条件、顶栏、任务、功能窗口、活动、下载、场景指引、匹配、战斗通知、红点、语言和声音服务；动态任务实例沿同一绑定入口创建。`Resources/Home/OfflineHomeHudAssets` 显式引用原 HomeUI、共享框架、语言、声音及字体，沿原 `FontAssetFontService` 初始化 FZY4K 和任务使用的 MPLUS 字体，原五行字体配置保留；其余三种源字体不在本宿主使用范围内。公共头像／底栏宿主可挂到原 `Content/Main/Public/Anim`，当前尚未在正式入口完成装配。

`HomeHudBindingsNativeValidation.Run` 已在隔离完整工程静音 Play Mode 通过完整保留 HomeUI 在原父层启用、原 Show／Hide 动画等待、顶栏类型 1/2/57 借还、原功能按钮和动态任务跳转按钮的首个射线命中与模拟指针，以及隐藏时解除输入、任务、下载、匹配、结果和红点监听。外层条件已接 `OfflineHomeHudConditions`；任务数据及其内部条件、窗口、场景位置、空活动记录和无货币子项的顶栏池仍为受控端点，不能代表正式业务、真实货币／头像／底栏装配或 Windows Player 已完成。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查通过。

`HomeConditionNotifications` 迁入所选 `ConditionHandler` 与 `ConditionInfoParser` 的组合展开、按条件类型有序去重、注册引用计数、排除类型、调用快照、调用期间延后添加／删除及逐回调异常隔离；十二组角色通知保留原条件类型的调用顺序，静态玩家与服务器时钟挂接交由明确外层端口。`OfflineHomeHudConditions` 将原 HUD 条件句柄、所选零参数功能条件与当前角色存档连接，叶节点类型直接核对原条件类，求值复用现有同版条件实现。家园拥有、地图存在与地图聊天状态由外层显式提供，当前条件闭包外的类型仍明确拒绝。正式角色事件转发与完整 HUD 尚未装配。

`OfflineHomeHudInput` 使用所选原 `InputOperationModule`、`GlobalPointerDownOperation` 和通用输入基类，保留回调／操作逆序执行、重复注册诊断、签名种子、延迟清除失效签名、回调异常隔离及层级／结果类型屏蔽令牌。只显式装配 HUD 所需的全局按下操作，其他操作不伪装为已迁入。Windows 采样读取左键按下沿、指针坐标及当前窗口尺寸，保留原屏幕边界包含与夹取；进程内样本可经同一端口验证。完整 HUD 资源宿主在原父层挂载 `OfflineHomeHudInputLoop`，随其启用逐帧轮询，停用父层后停止轮询；隐藏 HomeUI 本体依原 OnDisable 注销监听。适配服务重复释放安全，关闭后的晚到注销不再访问已释放输入。正式家园入口的整体 HUD 替换仍未完成。 静音 Play Mode 使用进程内输入覆盖逆序派发、重复身份、四侧越界／包含边角、叠加屏蔽、调用中注销／注册、异常隔离、逐帧轮询和释放；完整原 HomeUI 内实际展开／折叠动画、窗口遮挡门槛、首个射线命中的按钮点击及任务跳转通过，运行截图已查看。该验证不代表其他业务端口或 Windows Player 已完成。

所选 `TaskSettings.TaskFirstTraceConditionId=5` 为完成 `FirstBattle` 的次数至少 1，或基础等级至少 2。原 `PinnedTaskPanel` 在 prefab 中默认启用；`MainTaskFunctionLimit` 只在满足条件且面板已经停用时激活面板，不会在条件不满足时主动隐藏。本轮保留该默认值；测试中另显式停用面板以覆盖原分支，再分别提交真实剧情次数与等级，经对应原条件通知重新激活，核对下降至不满足条件时仍保留已激活状态。静音 Play Mode 同时验证组合条件监听去重、重复注册引用计数、回调中的增删顺序与异常隔离，原功能／任务按钮模拟指针、Hide 后全部条件监听释放，以及服务重复销毁；已查看新角色默认状态、剧情分支和等级分支的 608×1080 实际画面。上述验证不证明真实任务列表／奖励／导航业务已经接通。

`Runtime/Official/HomeTaskUI/` 迁入所选 884 的 `PinnedTaskPanel`、`TaskListItem`、`TaskGoal`、条件文本、原追踪监听组件、地图限制刷新器与等级监听。固定任务保留原剩余时间升序、自动追踪降序、完成状态降序和配置顺序升序；面板保留固定／显示条件过滤、增改删与每日刷新协程。条目保留领取、跳转、追踪／取消、地图限制、倒计时、条件观察和奖励图标的原调用顺序，规则、事务、导航及资源句柄由显式本地宿主提供；UI 不生成任务、进度或奖励。

完整原生追踪槽（10 个对象）及其实际外部引用的 `TaskListItem`（250 个对象）、两段任务动画、原字体／语言／按钮／声音已恢复至 `Assets/Official/Home/UI/PinnedTasks/`；原父级 `LeftBotGo` 布局和 PreferredSize 适配作为绑定依据，MPLUS 源字体按所选官方配置核验长度及 MD5。`HomeTaskUiNativeValidation.Run` 已在隔离 Unity 2022.3.57f1c2 静音 Play Mode 通过实际首个射线命中与模拟指针、固定／显示条件过滤、限时任务排序、领取一次后移除、目标导航、追踪／取消及状态切换、条件驱动进度更新、取消固定后的销毁、每日刷新／时间偏移调用顺序、启停监听释放和原 PCM 静音播放；已查看可领取和追踪中实际画面。静态元数据检查（150558 个 GUID）、Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 均通过。任务、条件和导航为受控验证数据；真实本地任务／奖励图标／详情窗、限时地域解锁提供方、完整 HomeUI 父层及 Windows Player 尚未接通。

`Runtime/Official/HomeResultUI/` 已迁入所选 884 的战斗结果通知及 `GameUIBase`／`GameCurrentStageText`。保留原战斗初始化和首次解析 Fight 缓存，普通结果按真实记录迭代顺序筛选后台结果、排除原黑名单类型 3，点击按 ShowEndPanel 分支直接请求领取或初始化原类型结果窗口并等完成后请求领取；请求仍核对记录存在且 `invokeByPlayer=false`。魔物后台结果优先，下一层按 EndLayer+1 夹取到 1～原最大层，成功回执后开结果窗，关闭后再刷新剩余结果或恢复普通提示；单机魔物战争仍按离线范围关闭。结果、关卡、窗口与领取由明确服务边界提供，不在 UI 中生成战斗或奖励。

完整原生结果提示子树（134 个对象）、原按钮／语言／红点／样式和声音已恢复至 `Assets/Official/Home/UI/ResultNotify/`。`HomeResultUiNativeValidation.Run` 在隔离 Unity 2022.3.57f1c2 静音 Play Mode 已通过实际首个射线命中与模拟指针、后台／黑名单过滤及迭代顺序、窗口初始化／完成／领奖顺序、无结果隐藏、魔物结果优先及层数上下限、回执后切回普通结果、普通／魔物／裂隙标题分支及启停监听释放；已查看实际普通结果和魔物结果画面。补测已通过等待战斗初始化、原 UIBase 初始化、首次解析 Fight 缓存及销毁退订。静态元数据检查（150505 个 GUID）、Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 均通过。验证结果和窗口为受控宿主，原 HomeUI 父层、正式战斗记录／领奖事务和 Windows Player 尚未接通。

`Runtime/Official/HomeSocialUiControls/` 已迁入所选 884 的 `GameMatchMiniPanel`／`GameMatchMiniSwitchPanel`，保留分类过滤、等待时间的 0.1 秒刷新、确认时隐藏、队长取消成功回调、原四种折叠状态和启停退订。拼写为 `OnDestory` 的原方法按源码保留，不改写为另一条 Unity 生命周期。组件已用同版 Unity C# 编译器和完整隔离工程的实际引用通过编译。完整原生子树（156 个对象）、原布局／语言／状态绑定、五段切换及循环动画已恢复至 `Assets/Official/Home/UI/MatchMiniPanel/`，原旋转组件复用既有 `WorldRotateAnimation`，保留原速度和逐帧旋转分支；`HomeSocialUiNativeValidation.Run` 已在隔离 Unity 2022.3.57f1c2 静音 Play Mode 验证原生按钮首个射线命中与模拟指针、展开／收起的原 Animator 状态、两处计时文字与负时间夹取／小时取分钟分支、目标分类过滤、队长／非队长取消显隐、成功回执后隐藏、确认时隐藏并保留原计时器、启停退订及原点击 PCM 播放；已查看实际收起与展开画面。结果使用受控匹配／时间服务，没有创建本地真人匹配。正式宿主与完整 HomeUI 尚未接线，未作为 Windows Player 发布成果；本模块通过静态元数据检查、Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate`。单机匹配入口仍按[离线范围](offline-access.md)关闭。

`Runtime/Official/HomeHudInfrastructureUI/` 迁入所选 884 的 `HomeUIActivityEntryController` 与 `PlayAsYouDownloadEntry`，保留活动事件、入口去重／排序／关闭、原资源句柄释放，以及下载进度、完成红点、一次奖励检查、检查点订阅和等待启动遮罩关闭后打开全局窗口的顺序。官方右侧入口表从实际 APK 配置解码为 `Resources/Home/HomeRightEntryConfig.json`，共 25 项，其中 11 项按常驻入口创建、14 项按真实活动记录判断；没有活动记录时仍创建常驻入口，由各自组件处理显隐。完整原生下载入口（90 个对象）、原语言／进度遮罩／动画／音频已恢复至 `Assets/Official/Home/UI/DownloadEntry/`。`HomeHudInfrastructureNativeValidation.Run` 已在隔离 Unity 2022.3.57f1c2 静音 Play Mode 验证入口生成／排序／关闭和资源释放、实际首个射线命中与模拟指针、25%／150%／零总量分支、完成红点、奖励后隐藏、检查点等待启动遮罩后打开全局窗口、启停退订、原波纹曲线逐帧推进及超过一段时长后仍循环；已查看实际下载中和完成画面。验证父槽复用原 `LeftUpGo` 的布局与 PreferredSize 适配，活动／窗口／下载服务由受控宿主提供。组件尚未绑定正式本地宿主，不代表完整 HUD 或离线下载业务已接通，未作为 Windows Player 发布成果。本模块通过静态元数据检查、Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate`。

`Runtime/Official/HomeSceneUiControls/` 已迁入所选 884 的 `AUIDirectionGuider`／`AHomeDirectionGuider`、床铺／推车及奖励取回 NPC 指引、`HomeScreenShootActive` 和 `HomeFadeProxy`／`HomeFadeList`。指引保留原视口判断、边缘位置与 75% 按钮尺寸夹取、箭头旋转、相机／当前拖动控制器订阅切换、逐帧聚焦及结束位置强制对齐；只有对应家具且提示级别非负时可显示。拍照显隐保留空 ViewModes 或当前模式匹配的分支，淡出保留原增量插值、Sprite 初始 alpha、Spine alpha 与取消顺序；原 Alpha 变为零时仍保持 Sprite／Spine 对象激活的分支未改写。MessagePipe 取消对象缺少有效托管方法体，已核对所选 AOT 的构造、Token 和 Dispose 指令：Dispose 先 Cancel 再 Dispose，不清空字段或额外防重入。

原生 `Guider` 完整子树（105 个对象、三组按钮、六个粒子系统）及依赖已恢复至 `Assets/Official/Home/UI/SceneGuider/`，只分离原父引用，内部布局、脚本、粒子与声音绑定保持原值。`HomeSceneUiNativeValidation.Run` 已在隔离工程静音 Play Mode 验证实际首个射线命中与模拟指针点击、床铺聚焦和到位隐藏、推车聚焦中断及遮罩释放、家具提示过滤、相机／拖动控制器更换与监听释放、拍照空模式列表／不匹配／匹配分支、Sprite 渐变和连续切换取消，以及原 `Main_Cursor_Normal` 事件 3772241779 到同版 PCM 的静音播放；已查看实际 608×1080 离屏指引与床铺聚焦后画面。家具位置、镜头和父层由验证宿主提供，Spine 淡出目标与实际有记录的 NPC 分支尚未运行验证；官方 NPC 指引的 mMask 原本为空，本轮保留该引用，其点击和真实取回 NPC 加载仍待处理。正式 HomeUI 父层、家具／镜头宿主与拍照流程仍待接线，尚未作为 Windows Player 发布成果。本模块已通过根工作区静态元数据检查（150264 个 GUID）、Unity 2022.3.57f1c2 导入／编译及 `Zjcs.Editor.ProjectTools.Validate`。

`Runtime/Official/HomeFunctionMenuUI/` 已迁入所选 884 的完整 `FunctionEntryDialog`、18 类菜单按钮／样式组件、百科／预下载／时间组件，以及推荐栏、单元格、点击转发、上下文和对象池。按钮保留官方条件注册、任一条件可用分支、显隐、事件订阅和窗口初始化顺序；图鉴保留 None 默认页与可传入数据，设置保留 Normal 样式，社交先清除新功能标记，界域保留禁用分支及错误 53883。推荐栏仍读取实际活动记录和原配置，保留地图筛选、Order 排序、随机起始项、3000ms 自动切换、原 Scroller 缓动和对象池回收顺序；活动记录、配置、资源句柄与窗口由显式服务端口提供，不在 UI 中生成活动或奖励。百科保留原限制、空 URL 和点击分支，原 SDK／下载器由服务端口隔离，正式离线宿主仍须按本页已确认范围接线。

完整原生 `FunctionEntryDialog`（1603 个对象）、独立 `FunctionEntryBannerItem`／`FunctionEntryBannerDotItem` prefab、全部组件和依赖已恢复至 `Assets/Official/Home/UI/FunctionMenu/`；内部坐标、布局、字段、原显示／隐藏动画和声音引用保留，复用已有原 GridLayoutGroupJSZX 与 FancyScrollView／Scroller。原图选择器独立保留关联的 527 个图对象，不代表图求值已接通。`HomeFunctionMenuNativeValidation.Run` 在隔离工程静音 Play Mode，以受控服务端点验证实际菜单按钮的内部模拟指针射线命中、任务／商城／图鉴／成就／设置等窗口调用参数与 Init 回调顺序、装潢／拍照按钮原附加关闭组件、界域禁用／允许分支、预下载等待／下载中／屏蔽／已完成显隐与点击顺序、显示／关闭原动画、原音频到静音 AudioSource、监听器释放及组件无缺失；已查看实际 608×1080 菜单展开与点击后画面。空活动记录隐藏推荐栏已验；有内容的推荐轮播、条件未满足后的解锁、百科可用分支和地区时间定时尚未取得本轮运行验证。正式 HUD 与真实玩法页面宿主未接入此菜单，关闭联网功能的正式策略仍未统一接线，未作为 Windows Player 发布成果。 本模块已通过根工作区静态元数据检查（150222 个 GUID）、Unity 2022.3.57f1c2 导入／编译及 `Zjcs.Editor.ProjectTools.Validate`。

`Runtime/Official/HomeMainUiControls/` 已从所选 884 的完整依赖反编译结果迁入 `FunctionEntrysPanelBase`／`FunctionEntrysPanel`、`MainTaskFunctionLimit`、`ChatQuickMsgEntryController` 和 `TipDotObject`，点击包含判断保留原 UIUtils 的坐标转换与 rect.Contains 顺序。入口按钮沿原分支请求 `FunctionEntryDialog.Init`；基类展开先改变原状态再发事件，只有原动画的 `OnExpandEnd` 才标记展开完成，折叠保留窗口类型与内部点击检查。任务条件只在满足时激活追踪槽，不在变为未满足时反向隐藏；聊天保留功能条件、地图频道检查、首次注册与禁用时注销，原 MainCanvas 的空快捷入口引用仍有效。红点总开关沿原订阅更新 Image／SpriteRenderer。条件、输入、窗口与提示系统由激活前绑定的本地服务端口提供，正式角色宿主尚未接线。

所选 MainCanvas 的完整 `FunctionEntryFunc` 子树（113 个对象）、原控制器及三段动画已恢复到 `Assets/Official/Home/UI/FunctionEntry/`；只分离原父节点，子树坐标、序列化组件、两按钮／点击区域引用及原动画事件完整保留。`FunctionEntry` 红点图选择器按原图名及节点 ID 1 绑定，并独立保留关联的 527 个原图对象，避免与底栏图载荷混用 GUID；此绑定不代表图运行求值已实现。原入口 `mNeedShowData=false`，带计数的数据仍只显示提示图标。`HomeFunctionEntryNativeValidation.Run` 在完整隔离工程静音 Play Mode 验证原生按钮的内部指针射线命中、弹窗请求及 Init 回调、真实动画展开／折叠事件、内外点击和已开窗口分支、重开与销毁释放、任务／聊天条件分支及全局提示显隐；已查看实际入口折叠、展开和提示图标的 608×1080 画面。入口在测试用居中父节点下挂载，条件和窗口为受控端点；该结果不证明最终 HUD 位置、完整 FunctionEntryDialog、真实条件／红点图求值或 Windows Player 发布完成。根工作区静态元数据检查通过（150121 个 GUID），新增组件及原生载荷通过 Unity 2022.3.57f1c2 导入、C# 编译、`Zjcs.Editor.ProjectTools.Validate` 和 Play Mode。

`Runtime/Official/HomeMainPanelUI/` 已迁入原 `HomeUI`、`MainUIPanelBase`、`MainUIPanelWithAnimator`、`ESceneUIGraphicType` 和完整 `AnimatorUtils`；使用原程序集及完整依赖重新反编译异步方法，仅适配命名空间并复用已核对原码的公共 `UIBase`。Show 先激活、设置 `IsShow=true`，等待原 Show 状态且 normalizedTime≥1；Hide 先设置 false，未激活或非 Show 状态立即隐藏，否则等待原 Hide 状态完成后隐藏，保留原取消行为。实际 `MainCanvas.HomeUI.mAnimator` 引用的控制器、八个原控制器子资产和两段完整原生动画已恢复，资源键为 `Home/MainPanel/HomeUI`；Show 为 23/60 秒，Hide 为 7/60 秒，保留原曲线、路径、状态、过渡及末帧 `m_IsActive`，没有按时长重写动画。

`HomeMainPanelNativeValidation.Run` 已在完整隔离工程静音 Play Mode 以原生货币栏作为测试用动画宿主，验证原 Show／Hide 等待、取消、未激活／非 Show 立即返回、反复显示后的订阅数量及隐藏时释放；已查看实际显示和 Hide 半程淡出画面，根 CanvasGroup 的透明度、交互阻断与原曲线一致。测试的运行组件与 Editor 启动器分属程序集；取消结果直接按 UniTask 检查，不依赖 `AsTask()` 的状态转换。根工作区静态元数据检查通过（150087 个 GUID），完整隔离工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。此验证只覆盖原动画控制器和根曲线，不证明完整 HomeUI 子树的全部曲线目标、主城创建、底栏切页／场景切换及正式 HUD 接线完成，也未作为 Windows Player 发布验收。

所选 `MainCanvas` 的屏幕框架已按原节点及组件引用提取为 `HomeScreenFrame`：保留根 Canvas／1080×1920 Expand 缩放器、Content 裁剪层、Main／Public 安全区祖先和 CanvasBorder 左右边框，共 29 个原始对象；功能面板及其处理器明确不在这份部分提取中，未替代完整 `MainUIRoot`。边框 Sprite 沿 APK 实际引用绑定原加载背景及 Windows 纹理，由 `Resources/Home/OfficialHomeScreenAssetCatalog` 显式加载。`Runtime/Official/HomeScreenUI/` 迁入完整原 `CanvasWidthAdjuster`、`SafeArea`、转换工具及屏幕适配服务：宽高比超过 121/160 时居中裁宽，低于 0.45 时居中裁高，与系统安全区交集并为正裁量补原 2 个 UI 单位；上下边框依原流程克隆左边框。保留安全区内边距、禁用分支、尺寸脏标记及边框暂停／恢复顺序。仅替换日志、依赖注入与服务所有权；Windows 沿原非 Android 分支使用 `Screen.safeArea`，默认屏幕指标直接读取 Unity，隔离渲染可通过作用域端口提供同一目标尺寸。此框架尚未接入正式家园 HUD。

`HomeScreenNativeValidation.Run` 已在完整隔离工程静音 Play Mode 验证原生框架、实际资源绑定、宽／高裁剪、不对称安全区、原 2 单位补量、内边距与禁用分支、实际 RectTransform、动态边框复用及注册／暂停／恢复／销毁释放。已查看 800×600、400×1200 和 608×1080 的实际渲染目标画面，原版边框与裁量一致；原 Image 的内部指针射线命中通过。测试未改变系统／编辑器 Screen 尺寸，只验证屏幕框架，不证明完整 HUD 布局、点击流程或 Windows Player 完成。根工作区静态元数据检查通过（150072 个 GUID），完整隔离工程的导入／C# 编译及 `Zjcs.Editor.ProjectTools.Validate` 通过；既有 Burst 扫描的程序集解析诊断未作为 Windows 构建通过依据。

所选 `884_156692` 原生顶部栏使用从 `HomeUI` 精确提取的 `NewPublicTopBarParent/Slot` 子树，保留原 `SafeArea`、`TopBarHandler` 和序列化字段；`TopBarManagerConfig` 指向的 `TopBarManager`、`PublicTopBar`、`MoneyUI` prefab 及三项原图标已恢复为 Windows 原生资产，由 `OfficialHomeTopBarAssetCatalog` 显式引用。原 `mShowTypeList=[0,5,2]` 是货币类型，沿原工厂与当前 `GameSettings` 映射为金币 1、钻石 2、体力 57；不能将枚举值当物品 ID，也不能沿用旧入口的星辉 15。`TopBarHandler`、`TopBarItem`、`MePlayerItemUI` 等原码与当前版本逐字节比对一致，直接复用已有公共迁移；`HomeTopBarManager` 保留原借还、更新顺序和原对象池，`OfflineHomeTopBar` 仅在原组件激活前绑定本地服务、字体和声音资源。

顶部栏原 `SafeArea` 已绑定上述完整屏幕适配代码。`HomeCurrencyPortraitValidation.Run` 在完整隔离工程静音 Play Mode 将原屏幕框架与真实角色存档货币栏组合，608×1080 渲染目标使用原 Expand 缩放（0.5625），验证实际 RectTransform、内部模拟指针点击、原每秒刷新、停止／恢复数量更新、借还时取消订阅、对象池重借及存档重读；已查看实际 200/300 与 402/300 体力画面。测试仅在隔离工程重置 TMP 动态字形缓存，原字体与图集按已登记的官方输入恢复，其他不同资源仍拒绝覆盖。导入、编译与 `Zjcs.Editor.ProjectTools.Validate` 通过；本项尚未替换正式入口 HUD，也未作为完整家园的 Windows Player 或发布验收。

当前版 `PlayerItemDataWrap` 的自增数量读取／设置、速度／上限变化、`AutoIncreaseItemUtils` 分段计算与原时间换算迁入 `Runtime/Official/HomeAutoIncrease/`，配置由 `prepare_home_auto_increase_runtime.py` 从所选实际 bundle 解码为 `Home/item-auto-increase.json`。体力 57 的初始恢复速度为每小时 5 点、自然上限 300；`ItemCountLimitInfo` 的持有上限另为 1200，已超过自然上限的数量不会因挂机被降为 300。设置数量时沿原顺序计算并保留小数余量、更新时间、清零额外秒数；达到自然上限时不积攒小数余量。存档用 `OfflineItemAutoIncreaseProgress` 的 ticks 保存原 `DateTime`，可选参数使用空／单元素列表避免 `JsonUtility` 把空引用写成默认参数；`OfflineInventoryOperations` 的发放／消耗读取实际自增数量并回写同一事务，耗尽后保留零数量自增记录。`InventoryCount` 只读计算，不因界面读取写存档。新建角色在创建事务内初始化，已有角色不补发、不升级。

`Runtime/Official/HomeCurrencyActionsUI/` 直接迁入所选版本 `PlayerItemQuickyShopHandler`、`PlayerDiamondItemUIHandler`、`PlayerDefaultItemUIHandler` 及 HomeUI 使用的原工厂三个分支。金币检查速购记录和原条件；速购记录在构造时缓存，修改 `ItemId` 不重新查表。钻石加号由原 Pay 功能限制决定；体力使用默认处理器，无加号，只有自增物品才按物品 ClassId 读取角色自增参数上限。原物品点击、速购和钻石兑换窗口的初始数量均为 0，默认加号调用仍抛出原未实现异常。播放器、配置、条件和原窗口均通过明确端口提供，没有为离线直接抹掉加号规则。

`HomeCurrencyActionsNativeValidation.Run` 已在隔离完整工程静音 Play Mode 通过原三项货币图标与数量、金币／钻石加号及物品按钮的首个射线命中和模拟指针、速购条件不满足／记录缺失、缓存记录与变更 ItemId 的差异、自增上限实时读取／普通物品返回零、数量更新、对象池重借与释放；已查看编辑器原生 640×480 实际截图，完整顶栏位于右上方。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查通过。条件、数量和窗口回调为受控数据；真实物品详情、速购／兑换窗口与存档事务、正式 HUD 和竖屏 Windows Player 仍未接通。

`Runtime/Official/HomeCountControlsUI/` 迁入所选 884 的 `SetCountSlider`、`SetCountButtonGroup`、`LongTouch` 和原整数 Clamp。滑条保留整数选择、单值／无效区间、交易倍率显示及原 float 转 long；按钮组保留初值不主动钳制、超上限回调和最大值通知规则。长按保留 0.5 秒初始间隔、0.05 秒间隔变化、0.1 秒最小间隔、10 次加速阈值、1.5 次幂、拖动取消与 AutoReset；仅增加可选的测试指针坐标入口，未设置时沿原触摸／鼠标读取。

`Assets/Official/Home/UI/CountControls/` 使用 APK 内 `ShopQuickBuyDialog` 的两个完整控件子树，保留原 RectTransform、滑条填充、按钮、文本、动画和点击音频绑定。独立测试槽位的宽 798、高 162 分别取原父级宽度和 LayoutElement 首选高度；完整父窗口另按下文恢复。`HomeCountControlsNativeValidation.Run` 已在静音 Play Mode 通过真实首个射线命中、模拟点击／拖动／长按、加速、拖动取消、上下限、倍率文字、单值与无效区间、最大值及超限回调，音效检查保持静音并校验原音频事件。已查看原生 640×480 实际截图；Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和静态元数据检查通过。本项独立控件的全局颜色通过受控端口提供；完整窗口及实际颜色另见下文，存档事务、正式 HUD 和竖屏 Windows Player 尚未验证。

`Runtime/Official/HomeCurrencyWindowsUI/` 迁入所选 884 的完整 `QuickBuyDialog`、`PayMoneyChangeDialog`、`PlayerCostMoneyContent` 和 `QuickBuyInfo` 三项共享计算。速购保持原所需数量向上取整、阶梯价格、已购买次数、持有／次数限制、最低购买量、余额变化及日／自增事件的不同刷新顺序；金币记录使用原 30／50／100 价格与 5／5／无限档位。余额不足仍强制选择至少 1 次；多档控件收到货币变化时只重算数量，价格等待原后续刷新事件，保留该原行为。确认时仍只用单次奖励数量检查持有上限，发出购买请求后立即关闭。扣费按钮保留原不足、禁止扣费错误 1659、条件检查、免费、数量颜色和每秒自增刷新分支。`HomeCurrencyBuyLimit` 迁入完整异步 `ShopUIUtils.CheckBuyLimitCountItem`：缺记录拒绝、无限量直接通过、持有量加本次数量不超上限直接通过，超出时等待原确认／取消回执；关闭遮罩自动走取消，仍保留原位或等待条件和计算顺序。

`HomeQuickBuyCalendar` 与 `HomeQuickBuyData` 从所选官方 `TimeUtils`、`PlayerQuickBuyDataExt` 和三类计数 Wrap 迁入，保留日／周／不刷新类型、配置重置时刻、周起始日和服务器时区换算。读取跨周期计数只返回零，不清空或写档；该周期首次新增才清空同类型全部物品，再更新时间和计数。时区为必传参数，正式单机取值仍待用户决定，不使用电脑时区或 UTC+8 作为隐含默认。

`HomePeriodicManager` 迁入所选 `PeriodicManager` 的一秒轮询、角色／时钟可用检查、首次日期初始化、只接受向前时间、先周后日判断及原空跨日消息；已确认移除好友服务，因此不迁入原方法唯一的周／角色变化副作用——联网同步友情币。`OfflineHomePeriodicEvents` 提供本地 UTC 时钟、明确的角色可用回调与可释放跨日订阅，重置日历仍须传入时区。隔离静音 Play Mode 验证首次加载不补发、不可用后重新初始化、一秒节流、精确周一边界、相同／回拨时间和跨多日只通知一次；真实速购存档达到第二档后，打开原窗口并推进本地时钟，原订阅将价格从 50 刷新为 30，期间不改写计数或主存档。模拟取消、服务销毁和重复退订均已验证，已查看实际跨日后 608×1080 画面。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和静态元数据检查通过。本地事件已与上述窗口服务组合验证；正式家园更新循环、其他跨日功能及 Windows Player 尚未接通，本轮 UTC+8 仅为显式测试参数。

兑换保留原零参数滑条／正参数固定数量、最小 1／最大 10000000／倍率 1、零余额选择 0、物品数量变化切换固定模式、余额不足确认后进入直购及原支付奖励拦截。请求、奖励、物品说明、确认和直购窗口均由明确类型端口提供；兑换请求已另接下述真实存档事务，其他端口仍需接通实际流程。`OfflineHomeCurrencyAssets` 显式绑定所选 26 条速购配置、持有上限、等级系数、物品、语言、原 GlobalColors 配色和图标；兑换所需额外顶栏货币按原 `PlayerItemUIGetItemIdFactory` 的 `FlareStar → GameSettings.FlareStarId` 补入运行时目录副本，保持家园三项货币目录的原定义。

`Assets/Official/Home/UI/CurrencyWindows/` 恢复 APK 中两个完整父窗口及默认物品图标，HintTip 附件沿原配置 GUID 查到实际 prefab 后一并恢复；保留全部原层级、RectTransform、布局、字体、图标、材质、入退场动画和声音，不用截图替代窗口。图标、附件对象池及顶部栏均在启用前注入本地资源与生命周期绑定。

`OfflineHomeCurrencyWindows` 提供正式家园可调用的速购／兑换窗口创建、显示和关闭入口，窗口直接挂回原 Canvas，沿原 popup 排序、模糊遮罩准备及入退场动画运行。每个窗口和图标池分别注册资源作用域，字体、屏幕适配、语言、声音、顶部栏与业务端口在启用前绑定；图标和 HintTip 附件沿原对象池借还，默认图标按所选 `ItemIconManager.ResetIconGo`／`UIUtils.CopyRectTransform` 恢复变换。当前默认图标绑定限定物品 1／2／15，26 条配置不代表其余速购物品的专用图标资源全部接通。购买上限与兑换不足确认复用所选 884 已恢复的完整 `ConfirmDialog`，同一宿主绑定原文本对齐协程、排序、模糊遮罩、原声音及动画；不改动原帧末文本刷新时序。

`HomeCurrencyWindowsNativeValidation.Run` 已在完整隔离工程的静音 Play Mode 通过两个完整原窗口的第一射线命中与内部模拟点击、所需数量取整、跨阶梯价格、最大值、余额／禁止扣费／条件分支、周期刷新、取消回调、固定数量／零余额兑换、支付奖励拦截、原入退场动画和图标／事件／奖励监听释放。验证直接调用上述窗口宿主，补测原图标池复用、遮罩准备期间重复关闭、已打开窗口销毁和重复 Dispose 后的监听回收。兑换按钮接入真实新角色背包，验证耀星扣除与晨星发放、原数量监听更新、重开存档恢复、重复请求不重复结算、无效／不足请求不写档，以及文件占用导致写盘失败时主存档及余额不变、窗口保持可操作。已查看 608×1080 实际速购及兑换画面，包含原顶部货币栏。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和静态元数据检查通过。确认弹窗在非批处理、内置屏不激活启动的静音 Play Mode 已通过首个射线命中与模拟指针，验证物品 109 原上限 30 的边界、超限确认／取消、遮罩前 0.5 秒防误触及等待后关闭；兑换不足确认的叠加、取消返回、确认后请求顺序和两层关闭也通过，已查看两张实际确认画面。金币速购与兑换事务已另接实际存档；其余速购、奖励演出、物品详情和直购端口仍为受控数据；速购次数另已使用真实新角色存档验证日／周边界（UTC+8、UTC、UTC−5、UTC+9 均为测试参数）、过期读取不写档、首次新增整组清零、不刷新计数保留、重开恢复、同一库存事务的重复回执与写盘失败回滚。原速购窗口读取实际存档次数后显示 50 晨星的第二档价格，原跨日事件恢复 30 晨星，第一射线命中的模拟取消及 608×1080 实际截图已核对。金币速购再以原购买按钮和真实存档验证所选 10 级基础等级条件、不足条件与禁止扣费端口不写档、扣 50 晨星／发 1500 罗拉／当日累计 6 次、跨 50／100 档位、次日首档、重开恢复、重复回执、无效数量、奖励溢出及写盘失败回滚；失败时仍保留原发请求后立即关闭窗口的行为。已查看购买后持有 1600 罗拉、450 晨星的实际原窗口画面。两个原窗口的玩家端口已再切换为 `OfflineHomeCurrencyWindowPlayer`，复测实际购买／兑换、当前等级与位阶、共享体力恢复读取、原 HasItem 比较、库存数量与体力参数通知；关闭原窗口后转发监听清空，重复销毁适配只解除自身订阅，不销毁共享数量服务。对应实际金币购买与兑换截图已核对。`OfflineHomeCurrencyWindowServices` 已把这两个原生窗口接入同一套配置、真实窗口玩家、金币速购／耀星兑换事务、原持有上限判断与原确认框；外层明确提供跨日消息、物品详情、直购及奖励表现端口。隔离 Play Mode 再验证真实兑换、金币购买、持有上限的取消／确认、存档失败后窗口保留，以及共享数量／体力参数通知和订阅释放；已查看原生窗口显示罗拉 1600、晨星 473 的实际截图。正式家园 HUD 的交易接线、时区选择、这些业务端口的完整往返与 Windows Player 尚未接通或验证。

`OfflineHomeCurrencyActions` 已将原家园三类处理器接到真实角色、所选速购配置与上述原生窗口：金币“＋”沿原基础等级条件开放，晨星“＋”沿原 Pay 功能条件（地图 11 已解锁且基础等级达到 10）开放，体力无“＋”且上限取实际自增参数。隔离静音 Play Mode 从原顶栏第一射线命中的模拟点击完成金币速购和耀星兑换，验证低等级／地图未开放时的显隐、三类说明入口的物品 ID 与零所需数量、真实库存变化及窗口关闭后数量更新；物品说明仍通过明确外层端口提供，本轮未接实际详情窗口。窗口排序改在原 `WindowContainer.OnWindowShowing` 对应时机、首次启用后设置，再附着原模糊遮罩；全部测试窗口验证前景 Canvas 与遮罩层级，组合场景再核对家园三控件保持在遮罩后、速购前景仅有一个晨星控件。已查看家园原栏（罗拉 1600、晨星 473、体力 300/300）与打开速购后的 608×1080 实际画面。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查通过。本项为原生顶栏与真实交易服务的组合验证，正式完整 HUD、详情／直购／奖励表现与 Windows Player 仍未完成。

`PlayerItemUIMainPlayerCountProviderBase` 与重新解析原依赖得到的 `PlayerItemUIPlayerItemCountProvider` 迁入 `Runtime/Official/HomeCurrencyUI/`，保留玩家借还、按物品 ID 过滤、每秒异步刷新及取消顺序。`OfflineHomeCurrencyPlayer` 将实际角色背包和自增参数投影为原数量事件；家园宿主需在提交／重读后调用 `SynchronizeCommittedPlayer`。该共享提供器同时支持家园的 1／2／57 和兑换顶栏的耀星 15；实际兑换验证直接复用此提供器，不另置耀星余额或数量监听。当前 `OutputAdditionSetting.ItemOutputGroupTypeDict` 包含 56／57，因此体力保留原额外参数更新订阅。`OfflineHomeCurrencyWindowPlayer` 为原速购／兑换控制器提供实际角色等级、位阶、同一背包数量和原周期购买次数，并将共享数量／自增参数变化转发为原窗口事件；它不持有第二份余额，物品数据投影与禁止扣费状态由宿主明确传入，适配销毁不取得外部数量服务的所有权。`OfflineHomeCurrencyWindowServices` 负责这两个窗口的本地服务装配，窗口所有者创建后只允许绑定一次；金币事务仅绑定货币 1，其他速购物品不通过该事务。事务返回后先同步共用数量源，兑换仅在成功时执行原窗口的成功回调；失败结果交回外层表现端口，不自造错误窗口。跨日／体力参数订阅随服务释放，角色存档、数量源和事务仍由外层拥有。真实角色存档与原生顶栏的隔离验证入口为 `HomeCurrencyProfileValidation.Run`；正式家园 HUD 的整体替换、物品详情／购买原窗口、属性及产出加成变化时的体力速度／上限应用链仍未接通，不能以初始恢复规则代替这些流程。

`HomeCurrencyProfileValidation.Run` 已在完整隔离工程的静音 Play Mode 使用真实新角色、背包事务与磁盘存档，验证初始 300 点不重复发放、耗尽后保留参数、离线恢复、小数余量、自然／持有上限、普通物品可选参数往返和扣费失败不写档；原 double／向下取整边界保持不变。原生数量显示、实际第一射线命中的内部模拟点击、提交刷新、暂停／恢复、每秒自然恢复、关闭取消、对象池重借及销毁释放全部通过。已查看 640×480 原生 Screen 尺寸的初始及溢出画面，并逐控件检查视口边界，顶栏完整位于右上方。批处理编辑器的竖屏纹理与原安全区尺寸不一致，未将该尝试记作竖屏通过。完整隔离工程的静态元数据检查、导入／C# 编译及 `Zjcs.Editor.ProjectTools.Validate` 已通过。正式 HUD、物品说明窗口及竖屏 Windows Player 尚未完成。

`HomeTopBarNativeValidation.Run` 已在隔离 Play Mode 验证原生三项图标／数量、上限配色、内部指针的真实射线命中、数量事件、暂停／恢复、动态换序、清空、关闭／重开对象复用及订阅释放，运行期间保持静音。两张实际运行截图已查看：按编辑器实际 640×480 渲染尺寸采集，完整顶栏位于右上方且无裁切；该结果不证明竖屏 Windows 窗口适配。工程静态元数据检查已通过（146088 个 GUID）；完整工程的隔离副本已通过本次资产导入、C# 编译和 `Zjcs.Editor.ProjectTools.Validate`；导入中另有既有程序集的 Burst 扫描依赖诊断，不作为 Windows 构建通过的依据。该独立夹具使用测试玩家服务；真实背包／自增参数由上文的角色存档适配验证。正式家园仍保持旧 `HomeUiRenderer` 顶栏，物品详情／快速购买和竖屏 Windows Player 验证尚未接通。

所选版本 `PublicPlayerIconHome` 的完整 36 个原对象、16 个脚本组件及外部依赖已恢复，由 `OfficialHomeAvatarAssetCatalog` 显式绑定。`PlayerIcon`、`PlayerIconMe`、`PlayerIconOther`、`PlayerInfoUI`、`HeadCharacterPortrait` 与当前原码逐字节核对后复用；原 `PlayerUIBase`／`PlayerExpSlider` 分别迁入独立的 `HomeExperiencePlayerUIBase`／`HomePlayerExpSlider`，保留普通经验、储存上限、赐福经验、长整数插值及一秒动画分支，不改动既有底栏共用基类。`OfflineHomeAvatar` 在原组件激活前绑定角色、摄影棚和经验服务；头像读取真实存档外观，由现有原生骨骼摄影棚播放 `home/idle_01` 后生成头部纹理，沿原 `SetNativeSize` 和胶囊网格裁剪显示。原生 RawImage 的 UV 坐标保持 APK 原值，Windows YAML 恢复已核验导出的 `Rect.serializedVersion=2`，避免零 UV 造成尺寸塌缩。

`HomeAvatarNativeValidation.Run` 已在隔离完整工程 Play Mode 通过真实角色槽／外观、头部透明像素、实际 100×120 尺寸、原 UV 与有限裁剪网格、内部指针射线及头像点击回调；另用受控经验表和奖励端点验证普通经验动画、储存上限、赐福分支以及关闭／重开后的五类订阅和奖励回调释放。三张实际运行图已查看，原生头像和经验环在实际 640×480 渲染尺寸中正确显示；测试保持静音。静态元数据检查通过（146111 个 GUID），完整工程隔离副本通过导入、C# 编译和 `Zjcs.Editor.ProjectTools.Validate`；此次按进程禁用无关 Burst 编译，不代表 Windows 构建通过。经验端点尚未绑定真实成长账本与原奖励队列，此结果不证明领取经验升级演出、正式家园 HUD、竖屏 Windows Player 或全场景画面完成。

`HomeExperienceRewardHandler` 来自同版 `PlayerExpRewardHandler`：为原程序集补齐 APK 已提取的 UniTask 等依赖后重新反编译，五个完整异步方法只替换服务／委托类型，跨帧等待、并行经验动画的倒序推进、取消令牌、升级阻塞与演出顺序保持原码，使用工程已有 UniTask。玩家事件、`ConsoleDataManager`、战力提示阻塞和 `LevelUpNotifyTip` 通过类型端口提供；默认升级通知保留原 2.2 秒等待，最后注册的升级演出覆盖默认演出。原双参数 `SafeAction` 单独迁入，保留监听去重和调用期间的监听快照，异常交给 Unity 日志。

`HomeExperienceRewardsValidation.Run` 已在隔离完整工程 Play Mode 使用原生经验环子树，验证原处理器的并行播放顺序、监听去重、升级门槛等待、连续获奖取消与重播、无窗口时保留未演示经验、窗口重开补播、玩家更新取消、默认通知端点的上一级参数／2.2 秒等待和销毁退订。两张实际经验环运行截图已查看，保持原图与尺寸；该验证不包含头像摄影棚、领取按钮或升级通知窗口。静态元数据检查通过，完整工程隔离副本通过导入、C# 编译和 `Zjcs.Editor.ProjectTools.Validate`，按进程禁用无关 Burst 编译。玩家、经验表、通知与战力阻塞使用受控端点；正式本地成长事件、原升级通知 prefab、床铺领取及 Windows Player 仍未接入验证。

所选 `884_156692` 的 `MainCanvas/PublicPlayerInfo` 中，`NameText`、`LevelTxt`、`CRText` 三个完整原生子树（21 个对象、12 个脚本组件）及姓名、等级、战力观察器已迁入 `HomePlayerInfoUI`。保留原坐标、锚点、字号、描边、渐变、语言键与字段绑定，根节点只分离父引用；原父级 RectTransform 链另随资源保留，尚未用来替代完整 MainCanvas。`PlayerNameText` 原样响应姓名变化；`PlayerLevelText` 响应等级、位阶与赛季记录更新，保留字号分支提前返回、经验盈满状态及可选说明按钮分支；`PlayerCombatRatingText` 保留 `NumberWithSizeArg` 的数值／单位分离，原生战力字号为 42。原 `FontAssetFontService` 通过原字体配置和所选 APK 的 FZY4K TTF 补入原动态字体的源字体，保留原字体图集、材质及参数，不以系统字体或新生成字体资产替换。资源与源字体由 `OfficialHomePlayerInfoAssetCatalog` 显式引用。

`HomePlayerInfoNativeValidation.Run` 已在隔离工程的 Play Mode 验证实际文字网格与像素、姓名／等级刷新、战力“万／亿”单位切换、位阶／赛季变化、字号分支提前返回、隐藏后不再收事件，以及重开、重新初始化和销毁时五个观察订阅的数量与释放。已查看 608×1080 的初始和更新画面，中文、等级及数值单位已正确显示。静态元数据检查通过（149951 个 GUID）；整工程隔离副本已通过 Unity 2022.3.57f1c2 导入、C# 编译及 `Zjcs.Editor.ProjectTools.Validate`。测试保持静音，导入过程禁用无关 Burst 编译。画面仅覆盖独立玩家信息文字组，外围 MainCanvas、完整头像／血量／赐福／状态图标、原说明弹窗、真实角色成长事件与正式 HUD 接线尚未由此验证；Windows Player 尚未验证。

所选 `MainCanvas/PublicPlayerInfo` 的完整原生子树已另行恢复：120 个原始对象中包含 22 个 GameObject、56 个脚本组件，连同 `BuffStatusUI`、`DurationBuffStatusUI` 两个原生状态图标 prefab 及动态战力 Sprite，由 `OfficialHomePlayerPanelAssetCatalog` 显式引用。`HomePlayerPanelUI` 迁入原血量条、低血量恢复提示条件、赐福显示、状态区显隐、状态筛选／对象池和多引用显隐控制器；保持血量在无地图时逐帧更新、仅恢复料理数量变化刷新提示、状态来源仅显示 Map／Cooking／Story／MapScene，以及取消、退订和调用顺序。倒计时通过原程序集及完整 UniTask 依赖重新反编译，仍用获得时间加持续秒数计算截止时间，并逐帧取原时钟端口；回收图标时取消协程。原无背景路径分支清空图标的行为也保留。`OfflineHomePlayerPanel` 在激活完整子树前绑定真实头像摄影棚、字体、语言、声音及类型服务，动态生成的图标从其父作用域取得服务，不重新拼装 UI。

`HomePlayerPanelNativeValidation.Run` 已在完整工程隔离副本的 Play Mode 验证真实角色头像、原 UV／胶囊网格、血量事件与无地图恢复轮询、恢复料理提示条件、赐福等级／战力渐变与动态图标、状态来源筛选、倒计时和截止时间数据、对象池复用与回收取消、多个显隐引用，以及重开／重初始化／销毁的订阅释放。空状态区停用时为 18 个订阅，状态区激活后为 19 个；原生头像、赐福战力和状态组按钮均使用内部模拟指针及真实 UI 射线命中。原点击音频事件启动对应本地 AudioSource，测试输出音量保持 0。已查看 608×1080 的赐福／状态及重开后正常画面，字体无方框回退，头像、血条、原图标和布局正确显示。此验证的血量、赐福、状态与经验数据为受控端点；原生子树保留原坐标，诊断 Canvas 不代替完整 MainCanvas 的生命周期。静态元数据检查通过（150043 个 GUID），完整隔离工程通过 Unity 2022.3.57f1c2 导入、C# 编译及 `Zjcs.Editor.ProjectTools.Validate`；验证按进程禁用无关 Burst 编译。正式角色状态／奖励账本、状态详情和赐福说明的原窗口、完整 HUD 整体替换与 Windows Player 发布仍未完成。

当前主城在打开家园时，将所选版本 13 项 `FurnitureInfo` 的条件、费用、原始附加维修 ID、剧情名和可升级标志绑定到场景／维修窗口共用规则实例；当前组合条件只在同版 4496 项条件中解析，不借用历史同号记录。`OfflineHomeConditionPlayer` 每次查询最新角色存档：基础等级沿当前小阶的非赐福等级上限读取，职业阶数遍历实际已解锁职业并查询当前 31 项 `profession_base`，伙伴条件复用原始已结识／可结识判断，剧情、地图可达和女神激活读取实际状态。家园独立拥有记录及果树维修状态仅在条件查询中投影为原家具物品数量，避免向真实背包重复写入家具。挂机表和其他功能表尚未随设施切换；场景原生脚本、维修特效和完整教学仍待恢复。

设施接线验证已在隔离完整工程完成当前 13 项记录、床铺锁定／预告／可维修／已拥有四态、物品拥有优先、背包变化、实际职业记录、可结识伙伴、地图可达、女神与基础等级上限检查，以及维修去重和磁盘重开。隔离窗口 Play Mode 使用官方维修／通知 prefab、实际本地维修服务及模拟指针，确认按钮提交后打开通知、关闭顺序、弹窗动画、遮罩与重开后的拥有状态。

`HomeFurnitureHostValidation.Run` 另在隔离角色槽通过正式 `HomeFeatureRuntime.Begin` 入口：使用真实三级角色，模拟场景指针打开床铺原生维修窗口，模拟维修按钮提交本地事务、显示原修复通知，再关闭／重进家园核对已拥有与正常态。四张完整运行画面已查看；采集仅在测试中将叠加 Canvas 映射到最高排序层的相机画布，不改写正式布局或存档。当前旧 HUD 仍有控件重叠、头像缺图，因此只判定床铺维修接线通过，整体家园画面未通过。完整原生场景、全部家具点击／特效／教学及 Windows Player 尚未完成。

果树特效改用所选 `884_156692` 的完整粒子子树：36 个 GameObject、23 个 ParticleSystem、24 个 ParticleSystemRenderer，保留原模块、曲线、顶点流、祖先变换与一个原始空材质槽。`prepare_home_fruit_tree_particles.py` 按原 CAB／PathID 解析材质、网格、Sprite 与贴图，复用已核验的 Windows 完整 shader／纹理转换缓存；原 Noise_Mask_Pro 的全部 pass 和 520 份 GLES 程序由现有转换器保留，未使用导出工具的 DummyShaderTextExporter 占位实现。网格维持原生文本及空字段表示，避免重新序列化为 `null` 造成原生网格加载断言。`HomeSceneView` 显式加载 `Resources/Home/FruitTree/Particles`，沿既有设施状态路径控制播放；其他果树场景模型、皮肤、教学与状态脚本尚未整体迁入。 `OfficialSparseShaderVariants` 现将此原 shader 的两个 pass 各 260 个程序登记到构建筛选器，排除原来不存在的关键词组合，保留原程序及材质；输入／输出哈希与现有来源清单一致。该登记用于修复完整 Windows 构建中缺少 vert／frag 内核的报错；57 项原稀疏程序域共 52,948 个 GPU 阶段通过登记核验，共享工程导入／编译与 ProjectTools.Validate 通过。完整 Windows 构建仍受[技能页记录的构建阻塞](skills.md#尚未实现)影响，尚未取得有效 Player。

`prepare_home_fruit_tree_particles_validation.py` 在隔离完整工程复用真实家园入口与模拟指针维修流程，核对原粒子／渲染器数量、23 个已绑定材质与一个原空槽、完整 Windows shader 支持，以及原异常区域不再被黑色不透明四边形覆盖。已查看 608×1080 的场景、维修窗口、修复通知与重开截图：家园初始状态的黑色遮挡消失；床铺维修、通知及存档恢复继续通过。静态元数据检查通过（149909 个 GUID）；Unity 2022.3.57f1c2 导入、C# 编译和 `Zjcs.Editor.ProjectTools.Validate` 通过，按进程禁用无关 Burst 编译；测试保持静音。此结果不包含全部果树状态／维修特效、正式 HUD、技能试伤场景或 Windows Player 验证。

所选 `884_156692` 的 25 张家园／入口相关原始配置已按原 Luban 构造器解码，保留 13 项家具、12 项挂机、11 项产出、3024 项升级材料、595 项等级规则、4496 项条件及原单条装潢配置。载荷为 `Assets/Official/Home/home-rules-current.json`，由 `Resources/Home/OfficialCurrentHomeRuleAssetCatalog` 显式引用；此目录独立于旧 `StreamingAssets/Home` 载荷，旧主城运行层尚未整体切换。

`Runtime/Official/HomeConditions/HomeConditionRules` 迁入原条件方法、枚举、组合遍历和比较／阶位转换，复用同版 `PartnerConditionRules` 的物品、基础等级、地图与伙伴条件。原物品类型条件累加该类型全部数量，数量 0 可以满足；伙伴资格包括已结识及当前可结识的非自动伙伴，并排除探索首领；个人阶位由原 `SubRank.ToRank` 计算。保留原 `AlwaysCondition=false`、组合短路／缺失引用行为，以及原比较器单个 `=` 表示相等、未知运算符（含 `==`）默认 `>=` 的分支。单机开放策略通过独立端口注入，规则求值仍需实际角色端点；未将旧存档布尔值或近似地图进度包装为已迁移的官方条件。

`validate_home_conditions.py` 已通过当前 57 个设施／挂机条件与数量累加、64 位数量、状态变化、阶位阈值、伙伴资格、短路和缺失引用检查；`HomeConditionValidation.Run` 已在隔离 Unity 2022.3.57f1c2 通过真实 Resources 绑定、原单条装潢读取、同组规则检查及编辑态 `Zjcs.Editor.ProjectTools.Validate`，`RunPlay` 已另行通过 Play Mode 的绑定与规则求值，静态元数据检查通过。该范围只证明当前配置／条件模块，尚未替换旧主城求值器，也不证明真实设施解锁、HUD、动画、收益窗口及 Windows Player 已完成。

所选 `884_156692` 的 `PublicBottomBar` 完整原生 prefab（661 个对象、264 个脚本组件）及十类原入口组件已恢复到 `Runtime/Official/HomeHudUI/` 和 `Assets/Official/Home/UI/BottomBar/`，由 `OfflineHomeBottomBar` 在激活前注入服务、字体、语言、声音和资源加载边界。原 `TabType` 数值保持人物 0、家园 1、游记 2、公会 3、大陆 4；原 TabGroup、条件监听、等级／公会／场景事件、显隐引用集合、升级标记、男女图标资源代理和 Animator 覆盖控制器均保留。资源入口为 `Resources/Home/OfficialHomeBottomBarAssetCatalog`；界面红点选择器保留原图名称与节点 ID，完整 1438 个原提示图对象另存为带来源的原始数据。地址资产只供原选择器建立键值，不执行条件；全提示图求值及真实角色主城接线仍未完成，不把这些地址当作已实现的提示图。

`HomeBottomBarNativeValidation.Run` 已在隔离 Unity 2022.3.57f1c2、原 URP 2D 管线中通过模拟指针验证五个原入口、游记／公会锁定提示与条件刷新、Tab 单选及原选中动画片段、男女图标切换、升级标记、两个独立隐藏引用及销毁退订；五个原点击事件均取得播放 ID，验证音量为 0。原底栏控制器保留 Default 状态的退出时间门槛及混合时长，等待实际过渡结束后核对 Hide 状态、Y=-100、透明度 0.7、Group 关闭和完整恢复；五张实际运行截图已查看；完整工程另行通过 Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate`，静态元数据检查通过。此验证使用受控状态服务，不证明真实角色条件、全提示图、主城接线或 Windows Player 流程完成。主城当前仍使用旧底栏，后续须整体替换并验证现有页面往返。

所选 `884_156692` 的维修与修复通知窗口已迁移原 `FurnitureRepairDialog`、`FurnitureRepairedNotifyDialog` 和 `FurnitureIconWidget` 方法体，恢复两个完整原生 prefab、原属性行模板及 13 个配置家具图标。入口为 [`HomeHudView.Repair`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Repair.cs)，真实窗口脚本位于 `Runtime/Official/HomeUI/`，本地窗口及服务边界位于 `Runtime/Offline/HomeUI/`；`Resources/Home/OfficialHomeRepairAssetCatalog` 显式绑定资源。家具名称、说明、家具／资源／来访者分类及维修／建造文案来自同版语言与 `HomeSettings`。原 `NeedRecordData` 筛选和表顺序决定通知候选收益，再按本地真实已解锁项过滤；通知沿原 `GetHangUpReward(id, 1.0)` 路径显示截断后的每小时产量。

维修先检查材料，不足时保留窗口并显示原提示；本地维修成功回执先打开原修复通知，再关闭原维修窗口。两窗沿原 `mMaskType=3` 使用同版原生模糊遮罩、材质、排序和点击保护，保留原显示／隐藏／循环动画及按钮／窗口声音事件；教学的 `FurnitureRepairDialog.RepaireBtn` 直接定位原 `RepairBtn` 字段。场景维修特效、全部解锁／剧情分支及教学完整表现仍在本页待实现范围。

`HomeRepairNativeValidation.Run` 已在隔离 Unity 2022.3.57f1c2、工程原 URP 2D 管线中通过模拟指针验证：材料不足、成功回执顺序、原关闭按钮、模糊遮罩点击、床铺维修／推车建造文案、原飞艇骨骼网格和有／无收益条目；按原动画实际时长等待完成，已查看 5 张运行截图。产量条目用受控端点 `321.9` 检查原取整为 `321`，音量为 0 时声音事件仍执行；家园主运行层及 160 个依赖模块已另行通过隔离编译，完整工程也已通过 Unity 导入／编译和 `Zjcs.Editor.ProjectTools.Validate`，静态元数据检查通过。这不证明真实角色维修事务、完整场景输入、存档恢复或 Windows Player 已验收。旧 JSON 家园 HUD 和床铺／推车窗口仍需迁移原生界面，家园功能状态保持未完成。

当前家园通用 Play Mode 验证仍有三项未通过：功能菜单的好友入口回调、类型 7 的背包入口，以及教学固定奖励 7 的规则安装检查。成长工厂装配和伙伴／副本专项的通过范围不能替代这些家园缺项。

- 配置装载：[`prepare_unity_home_runtime.py`](../../scripts/prepare_unity_home_runtime.py) 把官方场景、UI、规则、奖励、角色图谱与昼夜配置整理为 `unity/Assets/StreamingAssets/Home/` 下的运行载荷，其中 `player-runtime.json` 从固定版 `home-player/player.json` 与 `home-npc/npc.json` 生成主角变换、待机及排序参数；共复制 2228 张被引用贴图。输出 [`source-manifest.json`](../../unity/Assets/StreamingAssets/Home/source-manifest.json) 记录输出哈希及主角变换／排序源文件哈希。`OfficialHomeRules.LoadDefault()` 读取同一载荷，保留家具13行、挂机12行、`furniture_level_rule` 595行、`hang_up_reward` 11行、升级材料、皮肤、功能开放与奖励规则。
- 规则适配：[`OfflineHomeProgression`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeProgression.cs) 已接费用、挂机解锁／升级和收益目录；设施记录和条件已通过 `OfflineCurrentHomeFurniture` 绑定所选当前表，`AdditionalRepairList` 保留原物品 ID，已移除错误的家具类型前置检查，也不据此自动维修其他设施。旧 `OfficialHomeRules` 对部分地图条件直接通过、按持有与否判断物品类型数量和保存的伙伴布尔值仍用于尚未切换的旧挂机／功能条件，不能作为这些范围已还原的依据。幻兽树维修不得顺带维修生产树或绕过其条件，已确认方案见[果树](fruit-tree.md)。
- 存档与事务：[`OfflineHomeIncomeSession`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs) 新增家具拥有、挂机等级／解锁、装潢选择与发奖序号字段，`Mutate` 以候选存档方式提交并带回执去重；`AcknowledgeInventory` 在奖励真正进入角色背包后清理暂存，`Reconfigure` 在家具维修、挂机升级后重建收益目录。
- 场景与交互：[`HomeSceneView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs) 用官方 384 个 sprite 四边形、sorting 顺序与点击四边形重建家园；家具按 `/X/Repaired` 与 `/X/Unrepair` 切换四态表现；相机使用官方 `orthographic_size`、`border_center/extents` 与 `drag_sensitivity` 计算夹取，`HomeInputController` 采用 `kDraggingSqrOffsetThread = 1` 的 1 像素拖动阈值，指针在 UI 上或模态窗口打开时不触发设施。
- HUD 与窗口：[`HomeHudView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs) 由官方 `home`、`top_bar`、`money`、`player_icon`、`bottom_bar`、`functions`、家具、邮件、任务、宠物／宝箱扭蛋与果树面板树生成 UGUI；顶栏绑定角色与货币（1/2/15），人物页可返回，底栏可进入初始岛，功能菜单可折叠；设置入口加载官方 `SettingsDialog` 四页并按角色槽保存帧率、音量和数字显示。主线任务追踪、进度与奖励领取已接本地存档，任务跳转只开放已接入的 Map10 目标，其他目标按实际支持状态关闭或提示。床铺／小推车、邮箱、背包、祈愿、宝箱扭蛋、幻兽扭蛋、果树、料理、星命契约与杂货／宝库／馈赠家园商店已有代码入口；书桌／古遗物收藏和餐桌／伙伴交流仍未接，飞艇交易按离线策略拦截。
- 装潢与穿戴：[`HomeSkinScreenController`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSkinScreenController.cs) 从功能菜单打开官方 `decor` 面板，按家具、主题、环境分类及配置子类展示默认项和背包已拥有皮肤；点击按官方逻辑立即穿戴，并将类别、类型、物品 ID 写入家园本地存档。[`HomeSceneView.ApplyHomeSkins`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs) 使用官方家具／主题实体与天气变体按原模型图层和固定槽位覆盖场景；切换时清理旧图层并按果树状态恢复底层模型。真实输入、界面画面及重启恢复仍待 Play Mode 验收。
- 入口与发奖：[`CharacterCreationFlowController`](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 的完成界面新增「进入家园」，调用 `HomeFeatureRuntime.Begin`；[`HomeFeatureRuntime`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs) 打开角色存档与家园存档、每 5 秒按本地时间推进产出、把领取物写入角色背包（经验道具 50 交给 `AwardExperience`），交易失败不改内存状态。`BuildHomePlayer` 创建 [`HomePlayerActorView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomePlayerActorView.cs)，用存档的真实穿戴／染色和 `player-runtime.json` 还原官方 `player_init` 主角待机；坐标、缩放、排序层／顺序与 `home/idle_01` 由固定版 `home-player/player.json`、`home-npc/npc.json` 生成，网格帧复用经哈希校验的原生帧包。
- 环境：[`HomeEnvironmentView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeEnvironmentView.cs) 读取官方 `HomeEnvironmentConfig`（`HomeTimeScale=30`、`DayStageList`、`HomeLightTimeConfigs`、`EffectLightOn/Off`、`MoonConfig`、`MeteorConfig`），以角色档案 `CreatedAtUnixSeconds` 为离线家园时间原点，按配置日长与官方倍率累计游戏日；旧档缺少创建时间时从首次进入时刻起算。灯组按各自配置时刻更新 `DoorLight/LobbyLight/BedRoomLight/StudyLight/DeskLight/DanluLight/DiningTableLight`，`Night_Common` 的 9 条动画轨道驱动 48 张月相贴图。[`HomeMeteorView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeMeteorView.cs) 由 [`HomeFeatureRuntime`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs) 订阅官方流星事件，按固定版 `MeteorPlayable` 的粒子变换、发射／生命周期／速度／颜色／拖尾模块和两套材质实例化 Unity 原生 `ParticleSystem`；材质贴图按来源哈希从本地加载，Windows 使用项目内 `Zjcs/Map10/Official Effect` shader 并应用原材质属性。原规则为进入 20:00–06:00 窗口后等待 600 个家园秒，概率失败时逐帧重试，成功播放后再排下一次；864_156496 的同名材质仅作为单独记录的缺失依赖输入，不改写成原版 871 资源。[`extract_home_sky_timeline.py`](../../scripts/extract_home_sky_timeline.py) 从固定 APK 解码 `HomeTimeline`（Timeline path ID 188，`Day_Common` AnimationClip path ID 181），恢复原有关键帧、节点层级、Sprite trim、材质和相机跟随参数；[`HomeSkyTimelineView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSkyTimelineView.cs) 按本地家园日进度评估全部曲线。该表现要求 URP 2D Light2D 和 `Sprite-Lit-*` shader；当前家园入口已启用工程原 URP 2D，但这组历史转换图层的光照、shader 绑定及流星画面尚未完成 Play Mode 验证。
- 随机奖励：[`HomeRewardView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeRewardView.cs) 读取官方 29 个槽位坐标、18 个落叶 prefab 与 3 组动物骨骼首帧网格，按类型实例化落叶 sprite 或动物静态姿态，点击走 `CollectRandomReward` 本地事务并刷新；动物动画帧（Spine）尚未播放。
- 当前限制：床铺等级按官方 `UseBlessingLevel` 取赐福等级，离线暂以 1 计算；随机奖励已实例化槽位与点击，动物仅首帧静态姿态；尚待 Play Mode 验证；书桌／古遗物收藏、餐桌／伙伴交流、星命的本地赛季历史与货架、家园 NPC 骨骼与动作、动态云雪仍待完成。天空／太阳关键帧已从原版提取并接运行时代码；当前家园入口已启用工程原 URP 2D Renderer，天空／太阳的实际光照和最终画面仍需验证。流星原生运行时已接但未做 Play Mode 画面验收。固定奖励教学节点按官方 `FixedRewardInfo` ID 7 读取罗拉 ×350、水 ×10、青禾 ×5，并原子写入角色背包；该奖励已接领取回执和官方奖励窗口，输入／输出哈希见[来源总表](../../assets/reference/SOURCES.md)。

人物页底栏经 `CharacterFeatureRuntime.OpenProfile(slotId, savesRoot)` 打开本地人物档案。

`Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyExchange` 在每次请求前重读共享角色档案，按所选配置检查数量，以 `OfflineInventoryOperations` 在同一存档克隆中扣除耀星、发放晨星并写入请求回执，成功提交后才通知原窗口关闭；重复回执返回未变化，扣费、发奖或写盘失败均不提交新背包。该实现不维护第二份货币余额，顶部栏通过原数量观察器读取提交后的角色状态。`OfflineQuickBuyProgress` 仅将官方周期字典与 UTC 时间映射为本地序列化字段；`OfflineHomeQuickBuyCounters` 使用原计数运算，在库存事务的同一存档克隆中更新，读取不提交状态，不提供脱离扣费／奖励事务的单独计数提交入口。正式入口接线仍未完成。

`OfflineHomeMoneyQuickBuy` 只处理家园顶栏的金币记录，复用原 `QuickBuyInfo` 与 `QuickBuyUtils` 的阶梯价格、等级／位阶奖励及购买次数；原条件和禁止扣费判断由明确端口传入，真实角色存档在请求前重读。扣除货币、授予金币、更新周期计数和请求回执在同一库存事务提交，重复回执不结算，数量无效、原条件不满足、禁止扣费、余额不足、奖励溢出或写盘失败均不提交。本地事务取代官方网络请求及库存／奖励／次数回执；不扩展到其他 25 条速购物品。条件验证复用所选原 `PartnerConditionRules.LevelCondition` 和实际角色基础等级；禁止扣费端口及正式家园宿主仍需完成绑定，不能记为整项速购玩法完成。

## 自定义实现

### 全模块刷新时刻

用户指定原早上 08:00 的日／周刷新统一提前至 06:00。离线适配共用 `Runtime/Offline/Progression/OfflineRefreshClock.cs`，覆盖商城礼包／周期卡、商店周库存与注册日、速购、伙伴通话／旅行／羁绊、世界探索、幻兽每日计数及周商店、每日挑战与周挑战。已有助战 06:00 方案保持一致；本地时间模块继续按机器本地时间计算，伙伴通话沿用已确认 UTC+8。原始配置与固定日期活动开放表保留来源值。界面规则文字和刷新倒计时使用对应的新时刻。

日期型存档标识保持原日／周语义；每日挑战的旧 08:00 逻辑日边界映射到同日 06:00。周挑战及商店保留已有周标识与领奖回执，只提前当前周的选择边界，避免升级时清空进度或重领。幻兽每日计数先按当前时刻减 6 小时再取日期，修复原先先取日期导致全天日键相同的问题。验证入口为 `Zjcs.Editor.RefreshClockAcceptance.Run`（整工程）与 `RunRules`（隔离规则）。已通过静态检查（152102 GUID）及 Unity 2022.3.57f1c2 隔离导入／编译，规则在 Edit Mode 和静音 Play Mode 均通过：05:59:59／06:00／08:00、幻兽日／周计数、旅行、商城日键、商店周键、每日挑战旧计数保留、周挑战旧期身份、跨期检查点与奖励幂等。该验证不代表各模块界面模拟点击或 Windows Player 已复测。整工程导入／编译因其他未提交伙伴动画验证文件 `OfficialBattleAnimatorPlaybackAcceptance.PartnerMaterials.cs` 中 `OfficialEquipmentJson` 名称未解析而中断，因此 `ProjectTools.Validate`、整工程流程和本次可执行版本发布未完成。现有 `test_home_shop_runtime.py` 的 8 项通过，另 6 项因其旧来源路径 `assets/official/rules.json` 不存在而未执行成功。


耀星兑换沿用[商城](commerce.md)已确定的本地兑换方式，将原 RPC 改为共享角色背包事务；本项使用所选 884 的数量范围、兑换倍率及窗口顺序，防重回执与原子存档为离线适配，不标作已取得的服务器源码。

本次迁移按全新游戏制作；除上述刷新时刻变更的边界兼容外，不增加旧单机存档的升级兼容逻辑。体力等新建状态遵循官方初始化与更新顺序；原存档缺字段时如何补值不再作为本次迁移前置决策。

新角色初始体力确定为 300 点。初始发放数量在现有客户端及共享配置中未取得，由用户明确选择自然恢复上限作为单机初始值；该数量不标为官方服务器规则。恢复速度、自然上限、持有上限及数量更新顺序仍来自官方实现。

离线与运行期间按本地时间累计产出，负时间差按零，时间检查点只前进。所有领取、升级、费用、随机结果和教学进度使用同一本地事务，失败回滚。

家园环境的离线日历以角色档案创建时间为固定起点，使用官方时间倍率持续推进日内时刻与月相天数，避免重启后按公历日期跳相；旧档无创建时间时首次打开写入的角色槽时间作为起点。`OfflineMainlineProgressSession.OpenCharacter` 同步读取并持久化角色槽创建时间，保证主城与家园共用同一起点。

用户补证加速收益直接入背包，超出设施存储上限的加速收益也结算入包；服务端普通收益结算方法体未取得，此语义不标作服务端源码还原。旧自创补给、材料兑换已停用，仅保留兼容记录。

动物／落叶生成的服务端方法体未取得：检索客户端 `HomeRandomReward`、公共数据 `PlayerHomeRandomRewardDataExt`、奖励通知及 RPC 代理，仅能确认刷新区间、容量、槽位、奖励配置和领取后的同步。用户确认本地方案：两类分别按原8～9小时／16～18小时随机区间，每周期在合法空槽生成1个原表奖励，离线补刷至9／6个上限；满额时不积攒刷新欠账，领取后继续下一周期。奖励、计时、随机状态和领取使用原本地存档事务，失败回滚。

邮箱由实际本地系统提供邮件记录，初始收件箱为空。原远程运营、全服推送和玩家来信没有离线投递源，不自动生成邮件、附件或活动奖励；原 RPC 的读／领／删请求改为同一本地存档事务。

真人组队、聊天、好友、真人赛事、魔物战争、交易所／拍卖等按[离线开放范围](offline-access.md)关闭；已接入口显示关闭提示，飞艇设施拦截点击。公会仍只保留单人范围，主城公会页尚未接线。账号、推送、网络预下载移除、Windows 等比竖屏与本地拍照按[设置与存档](settings-and-save.md)执行。暂不开放的联动及关联伙伴继续遵守离线范围页，不通过主城活动、餐桌或快捷入口重新开放。

### 已确认的本地接线与边界

三份 DLL 与 [上游 LICENSE](https://github.com/EsotericSoftware/spine-runtimes/blob/4.2/LICENSE) 原样保留在 assets/official/spine-runtime/，逐项哈希、上游代码参考 URL 与客户端代码哈希在上述来源清单中。该运行库来自 Spine 官方，并非游戏 APK；游戏美术、骨骼、动画及控制配置来自固定原版。用户已明确授权这一运行时扩展例外。

`scripts/prepare_home_random_rewards.py` 从固定 `871_156598` APK／匹配 CDN 读取 `Ground` 等8套地面上的 `HomeRandomReward`、11个动物／落叶预制体、`HomeSkinConfig` 与女神／售货机的 `HomeRandomRewardSlotData`。18套落叶图使用原 prefab／精灵。`home_random_reward_type`、`home_random_reward_group`、`home_random_reward_slot` 依原 C# 构造器解码，运行目录为 `assets/official/home-random-rewards/`，逐输入、输出及脚本 SHA256 见 `assets/reference/home-random-rewards/source-manifest.json`。刷新数量及离线补刷采用用户确认的本地方案，规则区别在家园功能文档维护。

`prepare_weekly_merchant.py` 从固定 APK／匹配原版本 CDN 提取 `HomeNpcManagerConfig`、`npc_home_position`、主场景第0号生成点、`HomeNpcModel_8416`、`NpcModel_UI_8416` 和 `Npc_Portrait_8416`。保留原 ModelSlot 0.4 倍缩放、碰撞框、红点全局缩放、原骨骼／图集／body_8416 皮肤和 `npc_8416/idle_01` 动画，输出至 `assets/official/weekly-merchant/`。复用原 `DemonInavdeNpc` 四句剧情、8402／8416 立绘表情及 `activity_2_18`～`activity_2_21` 已转换语音。输入对象、资源及输出哈希登记于 `weekly-merchant/source-manifest.json`；商店从结算开始开放24小时为用户确认的单机调度，出场位置、首次对话和窗口内容来自原客户端。同一来源清单另登记原 `ItemIconAttachmentConfig` 类型7的 `ProbabilityTip` 预览标签，保留原 prefab 与 `ImgProbability` 贴图。

## 尚未实现
- 家园入口的黑色粒子遮挡已修复；技能试伤场景的主角缺失及其他渲染异常仍需单独复测，相关页面接线不代表主城画面完整。


上方两张表中的未接通项均属于待实现范围。跨模块玩法只维护主城入口与衔接要求，内部规则和进度以所属页为准；关闭项须完成对应入口处理，但不实现原联网业务。已落实部分按上文记录；下列只列仍未实现或未验收的缺项。

- 主流程与 HUD：已接角色进入／退出家园、人物页、顶栏货币、人物页与大陆到初始岛、功能菜单、任务追踪和部分设施入口。原生顶栏、头像／经验环及底栏已有独立验证，仍缺正式 HUD 整体替换、真实经验奖励流程、游记／公会页面、战斗结果提示、活动入口、全量红蓝点、窗口动画／音频及完整模拟输入验收。
- 设施与场景输入：14 类设施状态和官方点击区已装配；床铺、小推车、邮件、宝箱扭蛋、果树、祈愿、储物箱、星命契约、料理和杂货／宝库／馈赠商店已有本地页面入口。书桌／古遗物收藏、餐桌／伙伴交流及料理其他页签未完成；部分设施维修剧情、家具专属对话、维修特效、通知／红点和真实模态遮挡仍缺。星命赛季历史与商店货架由[所属页](astrological.md)说明；飞艇交易点击按离线策略拦截。当前家具表费用均为空，原附加维修物品 ID 保留但不作为前置条件；自动维修范围见[果树](fruit-tree.md)。
- 收益与存档：`OfflineHomeIncomeSession` 已接角色槽、运行期间累计、完整角色属性、家具等级／容量／费用、升级前结算和真实背包（经验走 `AwardExperience`）；时间结晶滑杆／数量选择已接。仍缺赐福等级真实来源（女神／探索／古遗物／月卡／星命）和小推车随机装备独立实例。
- 邮箱、随机奖励及回收：邮箱已接官方列表/正文控件及阅读、领取、批量已读/领取/删除；随机奖励已接29槽、刷新与权重、静态动物首帧/落叶点击领取。剩余动物动画、天气/主题槽位覆盖、领奖回收 `HomeAwardRetrieve` 窗口未接。
- 教学与角色：六张教学图、节点／选项／队列存档和部分执行宿主已接入；支持对白、条件、镜头、遮罩、修复、挂机领取／加速、料理选取与固定奖励 ID 7。固定版官方 `fixed_reward` 表中 ID 7 一次奖励 350 罗拉（物品 1）、10 水（8050）、5 青禾（8101）；本地事务幂等发放，三种官方物品图标均纳入主城显式资源清单。主角按官方 `player_init` 变换与排序、当前存档穿戴／染色显示原生 `home/idle_01` 网格帧；帧包尚无官方 `home/wakeUp_01`／`100/training_01`，教学动作仍暂停。诺诺九张强制行为图、全部节点覆盖和中断后完整恢复尚未完成。
- 环境与装潢：已接官方时间倍率、昼夜阶段云层切换、按时刻更新家具灯组、静态天空双颜色渐变（官方 ColorBlend `_Color1/_Color2` 基色）、原月相贴图及 `Night_Common` 动画轨道；流星调度和固定 prefab 粒子参数已接 Windows 原生运行时。`Day_Common` 官方轨道与资产已提取、接入，家园入口已启用工程原 URP 2D，但尚未验证这些历史转换层的光照、shader 绑定与画面；动态天气及互斥受光层未接，拍照仍未接。
- 资源与平台：按[共用 Android → Windows 迁移流程](../asset-management.md#android-资源迁移到-windows)恢复真实官方脚本、第三方依赖及 shader／材质，替换占位组件并核对序列化字段、GUID 和调用关系；覆盖动态选择的家具／主题／天气／奖励／NPC／UI／音频显式加载，完成随包本地交付。已有来源清单的原始输入已取得，不将未接入或占位代码记为官方内容缺失。



## 代码与资源入口

主流程与交互依据：[MainUIRoot](../../.tools/source-packages/official-code/client-source/MainUIRoot.cs)、[MainViewController](../../.tools/source-packages/official-code/client-source/MainViewController.cs)、[PublicBottomBar](../../.tools/source-packages/official-code/client-source/PublicBottomBar.cs)、[HomeRoot](../../.tools/source-packages/official-code/client-source/HomeRoot.cs)、[HomeUI](../../.tools/source-packages/official-code/client-source/HomeUI.cs)、[TopBarHandler](../../.tools/source-packages/official-code/client-source/TopBarHandler.cs)、[FurnitureBase](../../.tools/source-packages/official-code/client-source/FurnitureBase.cs)、[ClickableHomeObj](../../.tools/source-packages/official-code/client-source/ClickableHomeObj.cs)、[HomeDragController](../../.tools/source-packages/official-code/client-source/HomeDragController.cs)。

功能菜单、特殊装置及环境依据：[FunctionEntryDialog](../../.tools/source-packages/official-code/client-source/FunctionEntryDialog.cs)、[HomeUIActivityEntryController](../../.tools/source-packages/official-code/client-source/HomeUIActivityEntryController.cs)、[HomeMail](../../.tools/source-packages/official-code/client-source/HomeMail.cs)、[HomeFruitTree](../../.tools/source-packages/official-code/client-source/HomeFruitTree.cs)、[HomeRandomReward](../../.tools/source-packages/official-code/client-source/HomeRandomReward.cs)、[HomeAwardRetrieve](../../.tools/source-packages/official-code/client-source/HomeAwardRetrieve.cs)、[HomeTipDotGraphManager](../../.tools/source-packages/official-code/client-source/HomeTipDotGraphManager.cs)、[HomeEnvironmentManage](../../.tools/source-packages/official-code/client-source/HomeEnvironmentManage.cs)、[HomeSkinPanel](../../.tools/source-packages/official-code/client-source/HomeSkinPanel.cs)、[AirshipFurniture](../../.tools/source-packages/official-code/client-source/AirshipFurniture.cs)、[TradePanel](../../.tools/source-packages/official-code/client-source/TradePanel.cs)。

资源迁移脚本 [`prepare_unity_bundle_inputs.py`](../../scripts/prepare_unity_bundle_inputs.py) 从固定APK及已校验原版本CDN准备输入；[`prepare_unity_home_assets.py`](../../scripts/prepare_unity_home_assets.py) 从AssetRipper Unity导出复制场景、家园／邮件UI、家具、主题和随机奖励预制体及GUID依赖闭包，并校验APK内置字体对象。来源输入、转换器版本、原始输出SHA256、Unity重序列化后的Shader哈希、GUID、共享绑定和引用修复登记于[来源总表](../../assets/reference/SOURCES.md)。[`OfficialHomeAssetTools.ValidateMigratedAssets`](../../unity/Assets/Game/Editor/Home/OfficialHomeAssetTools.cs) 为 Editor 资源检查入口，AssetRipper 使用 YAML shader 导出；其资产检查不证明空占位类具有业务逻辑，Windows 渲染、正式运行时绑定和动态资源覆盖仍须验证。

官方收益依据：[HangUpUtils](../../.tools/source-packages/official-code/common-source/Common/HangUpUtils.cs)、[BattleFormulaHandler](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)、[PlayerHangUpDataExt](../../.tools/source-packages/official-code/common-source/Common/PlayerHangUpDataExt.cs)。

资源与规则依据：[来源总表](../../assets/reference/SOURCES.md)、[assets/official/home-completion/ui.json](../../assets/official/home-completion/ui.json)、[OfflineHangUpIncome.cs](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs)。
