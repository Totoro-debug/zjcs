# 主城、家园与离线产出

[总索引](../README.md)

状态：未完成

## 功能简介

正式角色进入家园，拖动查看设施，通过底栏切换人物、游记、家园、公会和大陆。本页涵盖家园场景、环境表现、设施教学、提示、邮箱、动物／落叶奖励及床铺／小推车离线收益；果树生产、各设施的养成玩法和商店分别见总索引所列页面。

本页同时维护原始官方主城功能目录与本地待实现清单；各玩法的内部公式、养成与奖励规则只在所属页维护，通过下表引用，不另建主城审计或进度报告。

### 官方功能范围与判定依据

官方基线为固定版本 `871_156598` 的客户端、共享代码、配置与 prefab。官方脚本来自 `.tools/source-packages/official-code/client-source/` 和 `common-source/Common/`；`assets/official/ui.json` 保存主城、顶部栏、底栏、头像与功能菜单的控件树，`assets/official/home.json` 保存场景层级、设施位置、相机及点击区域。原始联网功能保留在功能目录中，其单机处理单独注明；官方资源或脚本可取得，不代表已在 Unity 中恢复运行。

| 范围 | 原始官方功能与关键调用关系 | 当前实现与剩余工作 |
| --- | --- | --- |
| 主城进入与生命周期 | `MainUIRoot` 管理主面板；`PublicBottomBar.OnTabTypeChanged → MainViewController.SwitchMainView` 切换 UI 和场景；`HomeRoot` 初始化家园作用域服务、激活场景、同步时间与控制音乐 | 已从创角完成页进入 `HomeFeatureRuntime`，打开角色槽和家园存档，退出可返回角色入口；底栏大陆暂转入当前可玩的初始岛地图。主菜单复原、正式主城生命周期与场景音乐仍未完成 |
| 顶部栏与头像 | `HomeUI` 的 `NewPublicTopBarParent/Slot`、`TopBarHandler`、`TopBarManager`／`TopBarItem` 和 `PublicPlayerIconHome` 展示角色及配置指定货币，随窗口借还顶部栏、响应数量变化并提供点击入口 | 已绑定角色名、等级／公会文本、配置货币 1/2/15 与实际背包数量；头像打开人物页，余额说明可查看、充值入口关闭。原入退场动画、外观头像和经验环尚未迁移 |
| 底栏五入口 | `PublicBottomBar.TabType` 包含 `Character`、`TravelNote`、`Home`、`Legion`、`Mainland`；原 `TabGroup`、条件、显隐引用、升级标记和场景代理共同控制切页 | 人物页打开／返回、家园选中和大陆到初始岛已接线；游记、公会入口目前提示对应页面未提供，条件／红点／遮挡未完整恢复。玩法范围分别见[人物](character.md)、[单人公会](guild.md)、[世界地图](world-map.md) |
| 主城任务与战斗提示 | `PinnedTaskPanel`、`MainTaskFunctionLimit`、`GameEndNotifyUI`、`GameEndUIHandlerInHome`、`FightDemonBattlefieldAutoPushEndNotifyUI` 负责任务追踪、战斗完成通知和返回战斗入口；床铺／推车有 `HomeHangUpDirectionGuider` | 已接主线任务追踪、本地进度、奖励领取及 Map10 目标跳转；Map11 提示未开放，职业／宝箱任务不展示不可用跳转；非主线页签显示离线未开放。战斗结算提示和床铺／推车指引未完成，魔物战争按单机范围关闭。任务规则见[主线任务](tasks.md) |
| 功能菜单 | `FunctionEntrysPanel` 管理折叠／展开；原 `FunctionEntryDialog` 有任务、商城大按钮，以及图鉴、成就、装潢、设置、拍照、录像等入口 | 原资源树已折叠／展开；装潢已接菜单并进入官方分类页；设置已接固定版官方四页和本地选项；网络商店、好友、公告、合服与预下载等显示离线关闭提示。图鉴、成就和拍照尚未接对应页面，内部范围见[商城](commerce.md)、[图鉴](codex.md)、[成就](achievements.md)、[设置／拍照／存档](settings-and-save.md) |
| 活动及推荐入口 | `HomeUIActivityEntryController` 和 `FunctionEntryDialog.GetOpenActivityRecommendEntries` 按真实活动记录生成、排序和展示入口／推荐；`EventEntry` 是主城挂载点 | 未接活动记录与推荐入口；不凭资源生成活动实例或奖励。[签到](sign-ins.md)、[活动小游戏](activity-minigames.md)和[离线开放范围](offline-access.md)维护具体范围 |
| 社交及联网服务入口 | 原 prefab 包含队伍小面板、匹配、申请确认、聊天小窗／快捷消息、好友、排行榜、公告、用户、社交、百科、合服和边玩边下载／预下载等控件；功能菜单对应 `FriendBtn`、`TopListBtn`、`AnnounceBtn`、`UserBtn`、`SocialBtn`、`WikiEntry`、`PlanesMergeFunctionEntryBtn`、`PredownloadEntry` | 已对组队、聊天、好友／社交／用户／排行榜、商城、公告、合服和预下载入口给出离线关闭提示，飞艇交易所也拦截；仍缺部分 prefab 控件的统一关闭显隐和实机逐项点击验收，不生成虚构玩家 |
| 场景浏览与输入 | `HomeCameraController`、`HomeDragController → IgnoreUIDragController` 控制视野与区域边界；`ClickableHomeObj` 按实际 Collider／触摸对象和家园／剧情状态判断有效点击 | 已用官方场景坐标、点击多边形、相机边界与 1 像素拖动阈值重建输入；UI／模态时阻断场景命中。自动输入逻辑已测，真实鼠标拖动、遮挡和穿透仍待 Play Mode 核验 |
| 设施通用状态 | `FurnitureBase.GetState/OnValidClick/DoRepair` 区分锁定、维修预告、可维修、正常，读取 `FurnitureInfo` 的条件、费用、关联设施和维修剧情，响应维修奖励与家园变化并播放修复特效 | 已接家具状态求值、条件与费用维修、场景模型四态及果树专用维修剧情；未覆盖所有家具专属对话、修复特效、红点与完整教学，`CanLevelUp` 等仍按官方表处理 |
| 提示与标记 | `HomeTipDotGraphManager`、`CommonTipDotLinker`、各 `TipDot…ConditionNode` 和家具红点组件使用提示图、动态选择器及实际状态决定红／蓝点、升级、新解锁及可领取提示 | 邮箱未读提示与任务／奖励进度已有局部绑定；官方提示图、装备／伙伴／星命／活动等红蓝点及查看清除条件尚未迁移 |
| 昼夜、天气和主题环境 | `HomeEnvironmentManage`、`HomeClock`、`HomeEnvironmentManagerMono`、`HomeLightController`、`HomeMutexLightLayer`、`HomeCloud`、`HomeSnowController` 等驱动天空、太阳、全局光、分层灯光、云、月相、积雪、降雪与夜间特效 | 已接官方时间倍率、昼夜阶段云层切换、按时刻更新家具灯组、月相贴图及 `Night_Common` 动画轨道；夜间切换控制山体夜光、果树夜灯和售货机昼／夜灯。固定版 `HomeTimeline` 的 `Day_Common` 24 秒、20 条官方曲线、9 个 SpriteRenderer、5 个 Light2D 已提取并进入运行时；当前工程是 Built-in 管线且没有 URP 2D，前置检测会禁用官方昼间图层并报告缺失依赖，不能记为画面完成。动态天气、互斥受光层及环境画面仍未验收 |
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
| 11／4017 | 幻兽果树设施 `HomeFruitTreePet.TryCallFunction → PetGachaPanel`；依赖生产设施，维修剧情 `FruitTreeUnlock2` | 与生产树共用状态／维修剧情，依赖条件满足后点击接幻兽结缘页；官方角色演出和完整皮肤／提示仍缺。条件见[果树](fruit-tree.md)，抽取及商店见[幻兽结缘](pet-gacha.md) |
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
- 装潢按原分类、子分类、拥有与穿戴保持固定设施位置，不增加自由摆放；代码已接入，需在 Play Mode 验证完整分类点击、场景变体、画面及重启恢复。普通云、月相、积雪网格／遮罩及降雪渐变按已转换原资源运行。
- 环境接入原太阳、天空、全局光的昼夜曲线，逐排序层的灯光、遮挡轮廓、互斥区域和分批开关灯规则；原特效云、颜色／透明／序列材质、果树灯泡、积雪及光晕网格使用实际材质参数。诺诺和教学角色受家园光照；流星按原夜间时段、间隔、概率和粒子模板播放。
- 炼丹炉制作提示检查最佳已解锁恢复料理：少于10份且材料足够才提示；配方学习提示检查实际持有的未学习配方。
- 分解提示按原职业适配、穿戴槽位、装备评分、锁定及5件阈值计算；宝石提示检查低阶配方、最多1颗已镶嵌主宝石、背包材料与金币费用。回收提示使用真实可回收礼物／钥匙；已确认取消的旧赛季宝石回收不再产生提示。
- 邮箱使用原功能52开放条件、窗口、分页、列表、信纸、正文与附件；未读／未领取优先排序，支持阅读、领取、批量已读／领取／删除、到期过滤及已领取部分附件扣除。原猫头鹰随实际收件箱切换有信／无信动画；存档失败回滚库存与邮件状态。
- 动物及落叶奖励使用原29个槽位、设施解锁限制、奖励组和奖励表；8套地面、女神／售货机换肤位置覆盖、层级、朝向、天气皮肤及动物出现／待机／点击动画接入。点击按实际碰撞区域领取，装潢时隐藏。

- 原提示图沿动态选择器及有序端口接入真实邮箱、背包、装备净化、碎片分解、古遗物、伙伴档案／礼物／对话／成就、星命记录及商店状态；实际活动记录为空时不产生活动提示。碎片须达到原培养上限并满足分组条件才提示，料理和商店的新解锁标记按实际查看清除。
- 补齐 `RepaireFurniture2`、`GoHomeUnlockHeap`、`RepaireBed`、`BedSpeedUp`、`RepaireBaGuaLu`、`FirstMergeItem` 六张原教学图的条件、顺序、分支、对话、奖励、镜头曲线、修复窗口、遮罩和真实按钮引导；领取与加速按实际成功回执推进。诺诺沿原九张强制行为图站位、转向及传送，教学角色使用原起床／训练动作；当前节点、待执行队列和可恢复表现随本地存档恢复。固定场景的 `NpcBehaviour.DecisionGraph` 未挂载，不另加自主巡游。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

主城从 挂载 。首轮迁移实际提取 542 个 Sprite、26 张家园骨骼 atlas、原始 FZY4K 字体和家园音乐，当时共 570 项显式加载资源；转换 384 个场景图层、24 组骨骼、34 个 UI prefab 及 42 张完整 Luban 配置表。图像仅进行原始 Sprite / atlas 解码、裁切、旋转还原和格式转换；音乐从原始 Wwise 包解码为 PCM WAV，没有合成替换。

| 来源范围 | 实际使用的 APK 资源 |
| --- | --- |
| 家园 | `prefabs_home_dynamic_theme_*`、`prefabs_home_dynamic_furniture_*`、`scenes_main` 的原始对象及依赖图集 |
| UI | `prefabs_ui_home/furniture/public/functionentry/setup/character/playerbagpanel/gachapanel/skill/pet/shop/task/mainlandpanel/top/message/itemdetail/item/common` 的指定原始 prefab |
| 图标 | 原始 `image_itemicon_*` 中按 `item.RawIcon` 定位的 Sprite；缺少的七个商店商品图标没有替代图 |
| 字体 | `defaultpackage_assets_textmesh_pro_rawfonts_fzy4k.bundle` / `FZY4K` TextAsset 原始 TTF |
| 配置 | `defaultpackage_assets_config_luban.bundle` 中原始二进制表；根据 APK `Console.dll` 生成的 Luban 构造函数解码并严格校验消费字节数 |
| 音乐 | `wwisebnk_assets_wwisedata_android_home_bgm_01_pck.rawfile`，bundle hash `91efbabff5375669114cd914b7e6fa6c`，media `946883943` |

逐资源身份和输出 SHA256 见 [official-city/source-manifest.json](../../assets/reference/official-city/source-manifest.json)，配置证据见 [rules-manifest.json](../../assets/reference/official-city/rules-manifest.json)，程序集证据见 [code-manifest.json](../../assets/reference/official-city/code-manifest.json)，音乐见 [audio-manifest.json](../../assets/reference/official-city/audio-manifest.json)，基础包可用性见 [bundle-availability.json](../../assets/reference/official-city/bundle-availability.json)。

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

prepare_battle_spine.py 从固定 871_156598 已核验 APK／原版 CDN 的骨骼、atlas 和 SplitBinarySO 输入，重新组装原格式二进制供官方运行库直接读取；使用官方 @esotericsoftware/spine-core 4.2.110 解码定位字节边界，已开放皮肤及其链接依赖、骨骼／约束／槽／绘制顺序／事件轨道保持原字节，不烘焙姿态；未开放皮肤只保留原表索引并删除相关附件及变形／sequence 轨。图集只改页路径，贴图复用已有原转换或按需解码原 Texture2D，未绘制替代。739 份原骨骼及男女 227／237 动画在 assets/official/battle-spine/，显式目录由  读取；完整输入输出哈希及复用来源凭据见 battle-spine/source-manifest.json。

`prepare_battle_spine_pages.py` 从固定 APK 按已核验 Texture2D 对象恢复 Spine atlas 页并绑定生成材质；除开场创角录像所需的 monster 3803／3281／3282／3283／3481 与 NPC 8401／8402 外，为 Map10 已映射的 monster 3204／3206／3207／3254／3255／3281／3282／3283／3401／3411 提取并核验纹理，合计16页，路径、页尺寸、APK bundle/path ID 与输入／输出哈希见 `battle-spine/pages-source-manifest.json`。Map10 的 monster 3603 不在当前模型 identity 表中，因此未列入 Spine 贴图恢复；其时间轴头像仍由独立 portrait catalog 提供。

prepare_battle_animation.py 读取原 SkeletonRenderer／SkeletonMecanim／SkeletonAnimation 的完整序列化字段，按实际模型路径登记独立 prefab 绑定。客户端控制参照 PlayerAnimatorAppearance、AppearanceAddressUtils、AppearanceUtils、AppearanceSkeletonPart／MultiSidesPart、AppearancePartSideSwitcher、BaseSkeletonFollower 和原职业配置；来源与源文件 SHA256 分别登记于 battle-animation/source-manifest.json 及 battle-spine/source-manifest.json；这是引擎适配，未将 Unity 原生 Animator 内部实现冒称为已取得的游戏脚本。原过渡全部为 InterruptionSource=None，其混合中仅 AnyState 可中断的语义另核对 [Unity 官方过渡文档](https://docs.unity3d.com/2022.3/Documentation/Manual/class-Transition.html)，此引擎依据与 APK 游戏代码分别登记。

prepare_home_tips.py 遍历 131 个原装潢 prefab 变体中的 TipDotNodeLinker，沿原 PPtr、有序 Childs 端口和嵌套动态选择器取得当前294个实际提示节点，保存条件字段、原优先级和状态覆盖规则。运行时由 official_home_tips／official_fruit_tips／official_home_tip_queries 接入真实系统查询；活动与星神仅在存在对应实际玩家记录时可产生子项，不生成虚构实例。

ItemNewMarkManager 的零到正事件及登录清空、HomeSkinItem 离开列表清标记、HomeSkinMainTabItem／HomeSkinSubTabItem 分类聚合和 AppearanceDataEx 排除默认皮肤均按固定 871_156598 迁移。真实源码 SHA256 保存在 home-completion/source-manifest.json 的 mark_sources，由 prepare_home_ui.py 维护；继续使用原 NewMarkStateSwitcher、原文字和布局，没有新增替代图案。

prepare_home_ui.py 从原 prefabs_ui_itemdetail 的 FurnitureDecorationItemDetailDialog 提取完整布局和 mTryBtn／mFashionTxt／mIsShowFashion，按原 FashionExpUtil 和三类皮肤解析器取得 fashion_exp_group 的真实品质经验。home-completion/source-manifest.json 的 detail_sources、fashion_table、panels、inputs 与 resources 登记原版本代码、配置、对象和输出哈希。没有改用普通物品详情或自制预览按钮。

`assets/official/home-tutorial/` 由 `python scripts/prepare_home_tutorial.py` 生成；来源固定 `871_156598` 的 `prefabs_ui_story/OptionTalkDialog`、`TalkDialog`、`prefabs_ui_portrait/Npc_Portrait_8402` 与 `Npc_Portrait_8411`，及其实际GUID引用主图／表情。26个输出含25张原PNG和一份原层级／字段／动画数据；保留真实SetNativeSize、表情差异偏移、左右翻转与动画事件，无自绘或截图代替。源bundle、对象和输入／输出哈希、语言及调用代码登记于 `home-tutorial/source-manifest.json`。运行时家园教学使用原Common代理对应的OptionTalkDialogPanel，并复用 `map-audio.json` 已登记的原中文语音。独立导入：`python scripts/import_official_subset.py assets/reference/home-tutorial/source-manifest.json`。

家园对话平铺背景：原 OptionTalkDialog 的 BgImg／BgImg2 与选项按压背景为 Image.Type.Tiled，APK 的 FillCenter 与 PixelsPerUnitMultiplier（该批为1）由 prepare_official_city.py 读取，原 Sprite 边框及尺寸保留。 按 Unity 官方 uGUI Image.GenerateTiledSprite／GetAdjustedBorders 迁移平铺、边框缩小、末块UV裁切和65000顶点限制，反转PNG纵轴并保留原Sprite裁切范围；未绘制新图。框架源为 https://raw.githubusercontent.com/Unity-Technologies/uGUI/main/com.unity.ugui/Runtime/UGUI/UI/Core/Image.cs ，取得内容SHA256为74a8d2f63d1ea3783d34f2ba9d778191cc9b27bea5cfa0670d69f1defd9e29dd，缓存于 .tools/source-packages/official-code/ugui-reference/（Image.cs与source.json）。这是Unity官方UI框架来源，与游戏客户端来源分别登记；对话原图及实际输入／输出哈希见home-tutorial/source-manifest.json。重建需恢复该框架来源缓存；已转换对话记录Image原字段，其他尚无tiling字段的旧转换不作参数推定。

`home-environment/source-manifest.json` 登记主场景 HomeEnvironmentManagerMono.HomeMoon 的三个 SpriteRenderer、HomePlayable 的 Night 绑定及 Night_Common 中9条月亮曲线。原 `image_home_dynamic_moon` 分包解码配置会选择的1至15与17号月亮／描边／光晕，共48张PNG；连同 moon.json 共49项输出逐项登记SHA256，保留原Sprite尺寸、裁切、轴心、PPU、世界层级及排序。规则来源为 HomeMoon、HomeEnvironmentManage、HomeTimeDriver、HomePlayable/TimelinePlayable 和原 HomeEnvironmentConfig／HomeSkinConfig：自然日30天一轮、每相2天、08:00刷新、天气4902选择17号；阶段显示小时不改变自然日。转换命令 `python scripts/prepare_home_environment.py`，导入使用 `python scripts/import_official_subset.py assets/reference/home-environment/source-manifest.json`。原3个材质均为 Sprite-Unlit-Default；场景照明、云网格和积雪材质的实际迁移见后续相应来源段落。

`home-environment/solar-timeline-source-manifest.json` 登记固定 APK 主场景 `HomeTimeline`（Timeline path ID 188）及 Day 绑定的 `Day_Common`（AnimationClip path ID 181）。`python scripts/extract_home_sky_timeline.py` 按原 Mecanim 绑定解出20条曲线、21个节点、9个SpriteRenderer、5个Light2D和两种原材质，恢复 Sprite 裁切偏移与逻辑尺寸，所有输入／输出记录 SHA256；`python scripts/prepare_unity_home_environment.py` 校验后复制 JSON 与 PNG 至 Unity `StreamingAssets/Home/environment-visuals/`。曲线包括太阳位置／缩放／显隐、Sprite透明度、Light2D颜色／强度／阴影强度，保留原 Hermite 切线。原材质 shader 为 URP `Sprite-Lit-ColorShift` 和 `Sprite-Lit-Default`。当前项目使用 Built-in 管线且未安装／启用 URP 2D，因此 [`HomeSkyTimelineView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSkyTimelineView.cs) 会安全禁用该图层并报告依赖；恢复 URP 2D Renderer 后仍须执行 Editor 导入、编译及 Play Mode 画面验证。

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

主页进入技能与伤害测试时保持原 URP 2D 管线，并在退出时恢复前一管线；主城输入与消息在人物页或测试战斗期间停止穿透。`HomeRuntimeShaderBindings` 显式绑定主城流星特效、经固定 APK 原对象哈希核对的天空 ColorShift Windows 转换及 URP 引擎 Sprite-Lit shader，防止独立 Player 裁剪仅按名称加载的程序。主页真实技能入口、测试结算与本次战斗统计已通过 Play Mode 模拟点击及独立 Windows Player 运行；最新按用户决定移除战报持久化后的流程亦已通过 Play Mode 和独立 Windows Player 复测，详见[技能](skills.md)。主城功能入口恢复原 `FunctionEntrys.controller` 默认的 `FunctionEntrysUI_Folded` 零时长姿势，避免展开按钮遮住收起按钮；战报不另接主页历史入口。

当前完成范围是官方载荷恢复和部分本地底层，没有主城整模块已在 Unity 完整接入并验收的结论。

当前热更新 `871_156598` 已重新转换家园原生资源、图层、材质、配置和语言，来源分别记录于 `assets/reference/home-unity/source-manifest.json` 与 `assets/reference/official-map/current-*-source-manifest.json`。`HomeUiRenderer.CreateNode` 使用完整官方 Sprite 边框、pivot 和像素比例；`HomeAssetCatalog.FindSprite(HomeSpriteRef)` 按完整引用缓存，避免先加载纯贴图时丢失九宫格边框。官方 `TabItem` 映射为可选中的 Toggle。官方 Mask 的启用和显示标记已补回载荷，渲染器保留不显示遮罩图片的配置。`HomeSpriteBindingValidation.RunAndRender` 已在图形设备验证床铺完整背景子树的边框、pivot、九宫格角部和遮罩渲染。 `HomeAcceptanceLauncher.Run` 已通过目录加载、维修、收益、加速、邮件、环境、奖励、菜单／页签事件、商店货架与购买、仓库场景输入路由及存档恢复，并查看真实运行截图；使用 Unity 模拟输入，没有操作系统鼠标／键盘检查，不代表本页全部目标功能已完成。

`unity/Assets/Official/Home/` 已保存来源清单登记的2,001个输出文件、265个目录及49个跨模块共享脚本绑定；输出包含1张场景、139个 prefab、261个 C# 文件及贴图、动画、材质等。文件哈希和 `.meta` GUID 与[Home Unity来源清单](../../assets/reference/home-unity/source-manifest.json)匹配；Unity 重序列化的内置 `Sprite-Lit-Default.asset` 保留原始及导入后哈希。261个本目录 C# 文件全部是带 `Dummy class` 注释的 AssetRipper 空占位类，包含 `HomeRoot`、`HomeUI`、`PublicBottomBar`、`BedFurniture`、`CartFurniture` 和 `HomeRandomReward`，不能作为官方逻辑恢复的证据。官方真实方法体可在反编译目录取得，须迁移到 `Assets/Game/Runtime/Official/` 或对应适配／展示层并重新绑定；不在 `Assets/Official/` 编写手工运行时代码。

[`OfflineHangUpIncome`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs) 实现小时产量、容量、整数领取与单向时间检查点计算。新增 [`OfflineHomeIncomeSession`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs)，从调用方传入家园收益配置及角色属性，打开时按本地 Unix 时间补算并受存储时长上限限制；领取和加速奖励通过候选存档副本校验后原子写入本地 JSON，重复回执不会重复发放。新存档从打开时刻建立检查点，不会从 Unix 纪元错误累计；存档 schema 1 可迁移到 schema 2。加速收益按已确认的单机规则直接进背包，不受家具存储上限限制。

同一存档提供官方随机奖励类型/组/槽位定义、领取回执和随机状态；Unity 已装配 `home_random_reward_type` 刷新区间/上限、29 个槽位及奖励组权重，15 个当前开放槽位可生成动物/落叶，领取按本地事务扣槽并进入背包。官方服务端最终奖励裁决未包含在客户端，因此离线奖励使用已确认本地权重规则。邮箱本地读信、到期过滤、未读优先、单封/批量已读、附件领取和删除均已接存档事务，初始邮箱保持为空。六张官方教学图与步骤／队列已加载，执行器只完成已登记节点；不支持节点会显式暂停，不默认成功。

免费加速按 `fast_hang_up` 官方次数/时长配置和本地日期记录执行；加速窗口使用官方 `fast_item_row` 时间结晶滑杆（数量 1–100/持有数/配置上限取最小值），一次扣除选定数量后批量结算所有已解锁挂机项，并以成本/产出回执防止重复扣领。

收益依据来自官方 `HangUpUtils`、`BedFurniture`、`CartFurniture` 与 `BaseHangUpFurniture`。当前 Unity 已由 `HomeFeatureRuntime` 打开角色槽／家园存档，读取基础与外观属性，按官方挂机表累计／封顶并把领取物桥接到角色背包；床铺经验物品交给 `AwardExperience`，时间结晶数量选择和单次上限已接入。仍缺赐福等级真实来源，以及小推车随机装备独立实例。

### 家园运行层接入（）

以下为按官方源码接入的实际代码。此前隔离副本的 Play Mode 验收覆盖了家园基础场景、维修、收益、邮件、随机奖励和人物页；共享工作区新增任务、料理、商店与教程接线后，尚未重复 Unity 导入／编译和可视运行验收。

- 配置装载：[`prepare_unity_home_runtime.py`](../../scripts/prepare_unity_home_runtime.py) 把官方场景、UI、规则、奖励、角色图谱与昼夜配置整理为 `unity/Assets/StreamingAssets/Home/` 下的运行载荷，其中 `player-runtime.json` 从固定版 `home-player/player.json` 与 `home-npc/npc.json` 生成主角变换、待机及排序参数；共复制 2228 张被引用贴图。输出 [`source-manifest.json`](../../unity/Assets/StreamingAssets/Home/source-manifest.json) 记录输出哈希及主角变换／排序源文件哈希。`OfficialHomeRules.LoadDefault()` 读取同一载荷，保留家具13行、挂机12行、`furniture_level_rule` 595行、`hang_up_reward` 11行、升级材料、皮肤、功能开放与奖励规则。
- 规则移植：[`OfflineHomeProgression`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeProgression.cs) 复刻 `FurnitureBase.GetState`（已拥有→可维修→锁定→维修预告的判断顺序）、`DoRepair` 的费用与 `AdditionalRepairList` 前置校验、`UnlockHangUp`、`HangUpLevelUpPanel` 的等级上限（`home_level.FurnitureLevelUpLimit`）与 `hang_up_upgrade_material` 材料，并按 `HangUpUtils` 公式（`HourlyOutput * LevelNum * 加成 + 固定值`，容量 = 36小时产出）生成挂机定义。条件求值覆盖 `Level/Rank/SubRank/MaxProfessionRank/CompositeAnd/Or/HasItem/StoryCount/StoryStatus/MapUnlock/LogicalNot` 等类型，未识别的条件类型记录在 `Unresolved`，不默认通过。
- 存档与事务：[`OfflineHomeIncomeSession`](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs) 新增家具拥有、挂机等级／解锁、装潢选择与发奖序号字段，`Mutate` 以候选存档方式提交并带回执去重；`AcknowledgeInventory` 在奖励真正进入角色背包后清理暂存，`Reconfigure` 在家具维修、挂机升级后重建收益目录。
- 场景与交互：[`HomeSceneView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs) 用官方 384 个 sprite 四边形、sorting 顺序与点击四边形重建家园；家具按 `/X/Repaired` 与 `/X/Unrepair` 切换四态表现；相机使用官方 `orthographic_size`、`border_center/extents` 与 `drag_sensitivity` 计算夹取，`HomeInputController` 采用 `kDraggingSqrOffsetThread = 1` 的 1 像素拖动阈值，指针在 UI 上或模态窗口打开时不触发设施。
- HUD 与窗口：[`HomeHudView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs) 由官方 `home`、`top_bar`、`money`、`player_icon`、`bottom_bar`、`functions`、家具、邮件、任务、宠物／宝箱扭蛋与果树面板树生成 UGUI；顶栏绑定角色与货币（1/2/15），人物页可返回，底栏可进入初始岛，功能菜单可折叠；设置入口加载官方 `SettingsDialog` 四页并按角色槽保存帧率、音量和数字显示。主线任务追踪、进度与奖励领取已接本地存档，任务跳转只开放已接入的 Map10 目标，其他目标按实际支持状态关闭或提示。床铺／小推车、邮箱、背包、祈愿、宝箱扭蛋、幻兽扭蛋、果树、料理、星命契约与杂货／宝库／馈赠家园商店已有代码入口；书桌／古遗物收藏和餐桌／伙伴交流仍未接，飞艇交易按离线策略拦截。
- 装潢与穿戴：[`HomeSkinScreenController`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSkinScreenController.cs) 从功能菜单打开官方 `decor` 面板，按家具、主题、环境分类及配置子类展示默认项和背包已拥有皮肤；点击按官方逻辑立即穿戴，并将类别、类型、物品 ID 写入家园本地存档。[`HomeSceneView.ApplyHomeSkins`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs) 使用 131 个官方家具／主题实体与天气变体按原模型图层和固定槽位覆盖场景；切换时清理旧图层并按果树状态恢复底层模型。`prepare_unity_home_skins.py` 与对应测试准备 628 张原贴图和实体数据；真实输入、界面画面及重启恢复仍待 Play Mode 验收。
- 入口与发奖：[`CharacterCreationFlowController`](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 的完成界面新增「进入家园」，调用 `HomeFeatureRuntime.Begin`；[`HomeFeatureRuntime`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs) 打开角色存档与家园存档、每 5 秒按本地时间推进产出、把领取物写入角色背包（经验道具 50 交给 `AwardExperience`），交易失败不改内存状态。`BuildHomePlayer` 创建 [`HomePlayerActorView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomePlayerActorView.cs)，用存档的真实穿戴／染色和 `player-runtime.json` 还原官方 `player_init` 主角待机；坐标、缩放、排序层／顺序与 `home/idle_01` 由固定版 `home-player/player.json`、`home-npc/npc.json` 生成，网格帧复用经哈希校验的原生帧包。
- 环境：[`HomeEnvironmentView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeEnvironmentView.cs) 读取官方 `HomeEnvironmentConfig`（`HomeTimeScale=30`、`DayStageList`、`HomeLightTimeConfigs`、`EffectLightOn/Off`、`MoonConfig`、`MeteorConfig`），以角色档案 `CreatedAtUnixSeconds` 为离线家园时间原点，按配置日长与官方倍率累计游戏日；旧档缺少创建时间时从首次进入时刻起算。灯组按各自配置时刻更新 `DoorLight/LobbyLight/BedRoomLight/StudyLight/DeskLight/DanluLight/DiningTableLight`，`Night_Common` 的 9 条动画轨道驱动 48 张月相贴图。[`HomeMeteorView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeMeteorView.cs) 由 [`HomeFeatureRuntime`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs) 订阅官方流星事件，按固定版 `MeteorPlayable` 的粒子变换、发射／生命周期／速度／颜色／拖尾模块和两套材质实例化 Unity 原生 `ParticleSystem`；材质贴图按来源哈希从本地加载，Windows 使用项目内 `Zjcs/Map10/Official Effect` shader 并应用原材质属性。原规则为进入 20:00–06:00 窗口后等待 600 个家园秒，概率失败时逐帧重试，成功播放后再排下一次；864_156496 的同名材质仅作为单独记录的缺失依赖输入，不改写成原版 871 资源。[`extract_home_sky_timeline.py`](../../scripts/extract_home_sky_timeline.py) 从固定 APK 解码 `HomeTimeline`（Timeline path ID 188，`Day_Common` AnimationClip path ID 181），恢复原有关键帧、节点层级、Sprite trim、材质和相机跟随参数；[`HomeSkyTimelineView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSkyTimelineView.cs) 按本地家园日进度评估全部曲线。该表现要求 URP 2D Light2D 和 `Sprite-Lit-*` shader；当前项目未安装／启用 URP，因此运行时安全禁用这组图层并输出缺失依赖日志，需恢复 URP 2D Renderer 后再做画面验证。动态天气及互斥受光层仍未接，流星画面尚未做 Play Mode 验证。
- 随机奖励：[`HomeRewardView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeRewardView.cs) 读取官方 29 个槽位坐标、18 个落叶 prefab 与 3 组动物骨骼首帧网格，按类型实例化落叶 sprite 或动物静态姿态，点击走 `CollectRandomReward` 本地事务并刷新；Play Mode 实测 15 个在位奖励生成 14 个可见对象（领取 1 个后计数）并可领取清除槽位，动物贴图页与空模型已被过滤；动物动画帧（Spine）尚未播放。
- 当前限制：床铺等级按官方 `UseBlessingLevel` 取赐福等级，离线暂以 1 计算；随机奖励已实例化槽位与点击，动物仅首帧静态姿态；装潢本地穿戴和场景模型已接，尚待 Play Mode 验证；书桌／古遗物收藏、餐桌／伙伴交流、星命的本地赛季历史与货架、家园 NPC 骨骼与动作、动态云雪仍待完成。天空／太阳关键帧已从原版提取并接运行时代码，但工程尚无 URP 2D Renderer，当前会安全禁用，必须恢复管线后验证。流星原生运行时已接但未做 Play Mode 画面验收。固定奖励教学节点按官方 `FixedRewardInfo` ID 7 读取罗拉 ×350、水 ×10、青禾 ×5，并原子写入角色背包；该奖励已接领取回执和官方奖励窗口，输入／输出哈希见[祈愿来源清单](../../assets/reference/wishes/source-manifest.json)。

人物页底栏已通过 `CharacterFeatureRuntime.OpenProfile(slotId, savesRoot)` 打开。家园真实状态接线读取角色剧情次数／状态、地图 10 真实 NPC 手动交互标记，并按官方物品表从背包推导物品类别 305；地图 10 剧情状态变化会持久化到角色进度。商店 `SenSenThanks` 条件在地图 11 的真实剧情／NPC 流程尚未接入前保持未满足，不自动解锁。人物、行囊、家园商店、料理和其他跨模块页面的内部限制由各所属页维护。

验证边界：此前冻结副本的静态检查、Editor 编译和 13 项 Play Mode 检查覆盖旧版家园子集，包括 384 场景层、68 面板、12694 物品、维修、收益、邮件、加速、昼夜、随机奖励、点击区与人物页。它不覆盖新增的任务 HUD、固定奖励弹窗、料理、商店、星命及流星运行时。当前主城载荷包含 81 个面板；静态元数据检查通过（29800 个 GUID）， 8 项家园资源生成测试通过；共享工作区最新 C# 尚未在 Editor 导入／编译，Play Mode 可视运行、真实鼠标拖拽、模态遮挡与 Windows Player 也未验收。

当前已完成场景与基础 HUD、离线收益／邮件／随机奖励、角色入口与主角官方待机显示、果树、扭蛋、料理、星命契约本地核心、杂货／宝库／馈赠商店和主线追踪的部分运行接线；尚未达到主城模块完成条件。书桌／餐桌页面、赛季历史／商店货架、真实鼠标拖动与窗口遮挡、环境动画／粒子、教学图完整执行和起床／训练动作，以及最新代码的 Unity 验收仍缺。

开场战斗发布所用的已提交基线已补齐 `ProfileSlotCatalog.GetHomePath`、主角 `Transform` 入口、家园对商城程序集的引用、家园方法签名及行囊 `OpenProfile(slotId, savesRoot)` 连接，并通过 Editor 编译、`ProjectTools.Validate` 与 Windows 构建。该编译范围包含开场发布所需的既有家园连接；家园完整交互与工作区未提交接线仍按本页缺项验证。

## 自定义实现

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

上方两张表中的未接通项均属于待实现范围。跨模块玩法只维护主城入口与衔接要求，内部规则和进度以所属页为准；关闭项须完成对应入口处理，但不实现原联网业务。已落实部分按上文记录；下列只列仍未实现或未验收的缺项。

- 主流程与 HUD：已接角色进入／退出家园、人物页、顶栏货币、人物页与大陆到初始岛、功能菜单、任务追踪和部分设施入口。仍缺游记／公会页面、头像完整美术／经验环、战斗结果提示、活动入口、全量红蓝点、窗口动画／音频及真实鼠标输入验收。
- 设施与场景输入：14 类设施状态和官方点击区已装配；床铺、小推车、邮件、宝箱扭蛋、果树、祈愿、储物箱、星命契约、料理和杂货／宝库／馈赠商店已有本地页面入口。书桌／古遗物收藏、餐桌／伙伴交流及料理其他页签未完成；部分设施维修剧情、家具专属对话、维修特效、通知／红点和真实模态遮挡仍缺。星命赛季历史与商店货架由[所属页](astrological.md)说明；飞艇交易点击按离线策略拦截；自动维修与非空费用依配置逐行保留。
- 收益与存档：`OfflineHomeIncomeSession` 已接角色槽、运行期间累计、完整角色属性、家具等级／容量／费用、升级前结算和真实背包（经验走 `AwardExperience`）；时间结晶滑杆／数量选择已接。仍缺赐福等级真实来源（女神／探索／古遗物／月卡／星命）和小推车随机装备独立实例。
- 邮箱、随机奖励及回收：邮箱已接官方列表/正文控件及阅读、领取、批量已读/领取/删除；随机奖励已接29槽、刷新与权重、静态动物首帧/落叶点击领取。剩余动物动画、天气/主题槽位覆盖、领奖回收 `HomeAwardRetrieve` 窗口未接。
- 教学与角色：六张教学图、节点／选项／队列存档和部分执行宿主已接入；支持对白、条件、镜头、遮罩、修复、挂机领取／加速、料理选取与固定奖励 ID 7。固定版官方 `fixed_reward` 表中 ID 7 一次奖励 350 罗拉（物品 1）、10 水（8050）、5 青禾（8101）；本地事务幂等发放，三种官方物品图标均纳入主城显式资源清单。主角按官方 `player_init` 变换与排序、当前存档穿戴／染色显示原生 `home/idle_01` 网格帧；帧包尚无官方 `home/wakeUp_01`／`100/training_01`，教学动作仍暂停。诺诺九张强制行为图、全部节点覆盖和中断后完整恢复尚未完成。
- 环境与装潢：已接官方时间倍率、昼夜阶段云层切换、按时刻更新家具灯组、静态天空双颜色渐变（官方 ColorBlend `_Color1/_Color2` 基色）、原月相贴图及 `Night_Common` 动画轨道；流星调度和固定 prefab 粒子参数已接 Windows 原生运行时。`Day_Common` 官方轨道与资产已提取、接入，但当前 Built-in 管线缺 URP 2D Light2D／shader，安全禁用且未验证画面；动态天气及互斥受光层未接，流星画面和装潢输入／画面／存档恢复仍待 Play Mode 验证；拍照仍未接。
- 资源与平台：按[共用 Android → Windows 迁移流程](../asset-management.md#android-资源迁移到-windows)恢复真实官方脚本、第三方依赖及 shader／材质，替换占位组件并核对序列化字段、GUID 和调用关系；覆盖动态选择的家具／主题／天气／奖励／NPC／UI／音频显式加载，完成随包本地交付。已有来源清单的原始输入已取得，不将未接入或占位代码记为官方内容缺失。
- 验证：完成下文所列规则／事务、Editor、Play Mode 逻辑／真实输入／画面与存档恢复验证。静态检查、文件哈希、占位类可编译、自动回调或旧引擎测试均不能代替实际玩法验收。

### 实现完成后的验证范围

- 规则与事务：覆盖原条件的正反分支、家具四态、收益容量／小数余量／升级切换、免费与道具加速限额、真实奖励与背包、邮件部分／批量领取、随机槽生成与满额、存档失败回滚及回执去重；本地时间与随机规则按已确认的单机方案验证。
- Editor：运行 `Zjcs.Editor.ProjectTools.Validate` 和 `Zjcs.Editor.Home.OfficialHomeAssetTools.ValidateMigratedAssets`；核对原场景／prefab层级与引用、实际脚本及序列化字段、shader／材质、动态资源和本地配置。没有 Missing Script 仍不等于恢复了原逻辑，须检查实际方法与调用方。
- Play Mode 逻辑：使用隔离角色存档走通进入家园、设施解锁／修复、打开／关闭各保留页面、领取／加速、邮件／随机奖励、教学中断恢复与切档／重启；确认费用、实际库存／经验、随机状态、回执、时间检查点和角色槽一致，关闭项无法跳入原联网流程。
- Play Mode 输入与画面：实际使用鼠标／键盘、拖拽和滚动检查底栏、菜单、场景及弹窗命中、遮挡与返回；查看真实运行画面或截图，核对原布局、比例缩放、昼夜／天气、换肤、角色／家具动画、粒子／灯光和音频。自动按钮回调不代替真实输入，画面检查不使用 `-nographics`。
- Windows Player：平台 shader、依赖、资源交付或构建／分包链受影响时按需验证离线本地加载与渲染；日常纯文档或功能检查不额外发布成品。完成标准遵循[实际运行验证方法与要求](../asset-management.md#实际运行验证方法与要求)，未完成的层次继续保留未完成状态。

## 代码与资源入口

主流程与交互依据：[MainUIRoot](../../.tools/source-packages/official-code/client-source/MainUIRoot.cs)、[MainViewController](../../.tools/source-packages/official-code/client-source/MainViewController.cs)、[PublicBottomBar](../../.tools/source-packages/official-code/client-source/PublicBottomBar.cs)、[HomeRoot](../../.tools/source-packages/official-code/client-source/HomeRoot.cs)、[HomeUI](../../.tools/source-packages/official-code/client-source/HomeUI.cs)、[TopBarHandler](../../.tools/source-packages/official-code/client-source/TopBarHandler.cs)、[FurnitureBase](../../.tools/source-packages/official-code/client-source/FurnitureBase.cs)、[ClickableHomeObj](../../.tools/source-packages/official-code/client-source/ClickableHomeObj.cs)、[HomeDragController](../../.tools/source-packages/official-code/client-source/HomeDragController.cs)。

功能菜单、特殊装置及环境依据：[FunctionEntryDialog](../../.tools/source-packages/official-code/client-source/FunctionEntryDialog.cs)、[HomeUIActivityEntryController](../../.tools/source-packages/official-code/client-source/HomeUIActivityEntryController.cs)、[HomeMail](../../.tools/source-packages/official-code/client-source/HomeMail.cs)、[HomeFruitTree](../../.tools/source-packages/official-code/client-source/HomeFruitTree.cs)、[HomeRandomReward](../../.tools/source-packages/official-code/client-source/HomeRandomReward.cs)、[HomeAwardRetrieve](../../.tools/source-packages/official-code/client-source/HomeAwardRetrieve.cs)、[HomeTipDotGraphManager](../../.tools/source-packages/official-code/client-source/HomeTipDotGraphManager.cs)、[HomeEnvironmentManage](../../.tools/source-packages/official-code/client-source/HomeEnvironmentManage.cs)、[HomeSkinPanel](../../.tools/source-packages/official-code/client-source/HomeSkinPanel.cs)、[AirshipFurniture](../../.tools/source-packages/official-code/client-source/AirshipFurniture.cs)、[TradePanel](../../.tools/source-packages/official-code/client-source/TradePanel.cs)。

资源迁移脚本 [`prepare_unity_bundle_inputs.py`](../../scripts/prepare_unity_bundle_inputs.py) 从固定APK及已校验原版本CDN准备输入；[`prepare_unity_home_assets.py`](../../scripts/prepare_unity_home_assets.py) 从AssetRipper Unity导出复制场景、家园／邮件UI、家具、主题和随机奖励预制体及GUID依赖闭包，并校验APK内置字体对象。来源输入、转换器版本、原始输出SHA256、Unity重序列化后的Shader哈希、GUID、共享绑定和引用修复登记于[Home Unity来源清单](../../assets/reference/home-unity/source-manifest.json)。[`OfficialHomeAssetTools.ValidateMigratedAssets`](../../unity/Assets/Game/Editor/Home/OfficialHomeAssetTools.cs) 为 Editor 资源检查入口，AssetRipper 使用 YAML shader 导出；其资产检查不证明空占位类具有业务逻辑，Windows 渲染、正式运行时绑定和动态资源覆盖仍须验证。

官方收益依据：[HangUpUtils](../../.tools/source-packages/official-code/common-source/Common/HangUpUtils.cs)、[BattleFormulaHandler](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)、[PlayerHangUpDataExt](../../.tools/source-packages/official-code/common-source/Common/PlayerHangUpDataExt.cs)。

资源与规则依据：[home-materials/source-manifest.json](../../assets/reference/home-materials/source-manifest.json)、[home-player/source-manifest.json](../../assets/reference/home-player/source-manifest.json)、[home-completion/source-manifest.json](../../assets/reference/home-completion/source-manifest.json)、[home-completion/scene-source-manifest.json](../../assets/reference/home-completion/scene-source-manifest.json)、[Home Unity来源清单](../../assets/reference/home-unity/source-manifest.json)、[assets/official/home-completion/ui.json](../../assets/official/home-completion/ui.json)、[OfflineHangUpIncome.cs](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs)。
