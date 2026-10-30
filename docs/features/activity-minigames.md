# 成长试炼与活动小游戏

[总索引](../README.md)

状态：未完成

## 功能简介

活动列表进入成长试炼、宾果抽抽乐、幸运刮刮乐、两套音乐节奏配置（206、221）、四套三消主题（204、208、214、226）、星霜派对伐木（209）及七个拼图主题（200、2076、213、2151、2161、222、2242，其中2161复用界面与题图）。地图中的机关、厨房、躲猫猫和音乐节场景按对应地图维护。

## 原始官方实现

各活动使用自己的官方入口、关卡、计分、道具、奖励与界面；节奏、三消、拼图、伐木、宾果、刮卡、烧烤及其他小游戏不能共用一套近似规则。

### 待迁移的规则与表现

成长试炼使用原任务、积分档、职业奖励和领奖去重。宾果使用原券、未中格候选、连线和轮次奖励；单次与批量抽取共同结算格子和连线，完成轮次与手动领取轮奖分别记录。刮刮乐使用原三品质、权重、19图案与三同结算。

宾果／刮卡各保留30套已提取配置及页首切换，进度独立；未完卡片、抽定奖励和刮开位置保存。活动材料任务仅登记原目录中的真实完成事件，仍检查原功能／等级、每日条件和去重。

宾果接入原 `BingoActivityPanelContent` 的16格与10条连线、原布局和奖励格。单抽／全部抽取保留原数量、费用及交互保护，依次执行格子 `GetAnimTrigger`、非阻塞奖励列表、连线 `LightAnimTrigger`、连线奖励和原 `AwardPanel`；关闭奖励后在原窗口切换下一轮，轮奖仍须点击原轮次按钮手动领取。已领取轮奖显示原 `GotTip`。原规则、任务列表、进度详情、批量领取和地图跳转确认／取消须接入，关闭详情回到原列表，再关闭列表回到原棋盘。两种活动的任务行按原 `TryCheckPreviewConditionLimit` 显示结缘、扭蛋、日常副本和素材秘境的解锁提示，沿 `BaseTaskListLiteItem.CanBeClick` 拦截未解锁任务的详情／单项领取／跳转，原 `PlayerTaskComparer` 把未解锁任务排在可用任务之后；原文及语言输入哈希由 保存。

刮卡接入原 `ScratchCardActivityPanel`、16格、卡片堆叠及三档奖品预览。首次卡片、按原背包时间顺序自动使用下一张、品质状态、点击／75像素拖动阈值和5秒提示沿原脚本／配置迁移。原刮擦溶解、提示、奖级完成、奖励图案闪光和切卡曲线使用原网格、贴图及材质程序；第三个相同图案的刮擦动画播完再显示奖励，关闭奖励后由原 `SwitchCardEvent` 在窗口内切换下一张。未刮完退出后仍保留同一张卡及刮开位置，不再次扣卡。原规则、任务列表与详情接入，通用详情单次提交、任务行批量提交，领奖后保留原列表。

音乐节奏的左右／双向接取直接迁移 `Common/PickGame`：原种子随机序列、9个预备物件、禁止奖励物件相邻、先补尾再弹出、奖励物件转换前6个位置、60秒计时、连击加分、狂热门槛／时长／倍率、失误清除连击及保留狂热等级下限。暂停冻结游戏与狂热时钟；结束后不能继续得分。

原 `ActivityPickGameMainPanel`、队列、左右按钮、暂停与结果弹窗接入；Unity 同名兄弟物件保留各自引用。开始时机读取原准备动画的 `OnStartReadyAnimEnd` 事件（约4.53秒），双向物件读取原2秒输入间隔。原奖励按钮打开 `ActivityFestivalMusicTaskPanel`，任务卡使用 `TaskListLiteItemForActivityTask` 的原状态及领取按钮，206／221各自的参与、分数、连击和累计分数在真实结算时更新，不接受直接传入分数的操作。

音乐界面的五档动态评级、原规则弹窗、游戏内返回按钮隐藏、任务排序和动态文字布局须接入。原 NPC 的组合皮肤、材质、挂件跟随及分离动画按狂热等级切换，保留左右角色的五档演出；队列按原位置／缩放曲线与0.1秒过渡移动，失误先播放失败动画。原 UIParticle、按钮正误效果、倒数／狂热动画的音频事件及场景开局／结算音频由原 prefab、动画事件和 Wwise 容器驱动，22个事件、26条音频保留随机候选、循环、增益和停止动作。

伐木单轮场景须接入原 `ActivityTreeFellingGamePanel`、树干、障碍、背景适配与基座定位；按钮按下及 A／D、左右键调用真实砍伐。男女当前自定义角色采用原 `PlayerAnimCtrlAnimator` 的待机、三段连击、眩晕与返回待机状态及原转换条件；树干飞出、障碍掉落、0.1秒下落曲线和命中特效读取原对象。原个人头像位置／遮罩、`FlickerCounter` 延迟及逐步数字、0.05秒刷新倒计时和原 Wwise 砍伐音频须接入；七个原事件及七条音频由两个原分包转换。原圣诞活动列表图标、活动主页、盛典任务和整局完成界面须接入，保留原按钮、动态名称、状态切换、任务类型文字、进度条、滚动及奖励图；任务按原圣诞面板的自定义优先级排序。

三消底层规则须迁移原 `Common/Match3Game`：60套关卡、原权重随机、初始消除重抽、无解棋盘重建、三连／四连／五连／交叉判定、特殊棋子生成位置、血量链、特殊技能命中顺序、重力补位和连锁。原 `StageScoreHandler` 的普通／技能分数、连锁倍率和中点取偶舍入保留；分数关按成功交换次数扣步并折算剩余步数，限时关按原倒数总时长结束。

三消的本地开局／结算事务须接入原关卡前置、通关集合及一次性原表奖励，通关关卡不能再次进入；限时模式单独保存最高分。参与、分数及连锁只取实际棋盘结果，未结束不能提交，退出使用独立停止操作，重复提交不计数。`Match3GamePassLevelCondition` 按原活动号、关卡号和次数计算已接取实例的真实通关；万圣节40601～40614保留 `BattlePassSeason` 任务归属，不移入小游戏奖励表。

三消的菜狗、旅程庆典、春日出游及万圣节原入口、主页、十四关列表、限时挑战和结算界面须接入。关卡列表保留原交错锚点、显示间隔、默认入场／循环动画、倒计时解锁、奖励逆序、锁定灰度及领取附件；原物品表绑定奖励图。棋盘按各主题的原格宽／高／间距布局，拖动阈值、方向、无效交换回弹、移动／加速下落、血量图片链、特殊棋子特效、浮动分数和连锁显示直接读取原组件参数。限时挑战保留3秒准备及90秒游玩，退局确认期间时钟继续；结束先播放原2秒结束演出，再提交真实结果。

冰淇淋的顾客生成与食材顺序须迁移：按原候选顺序排除仍在队列中的 NPC，候选耗尽时回退全部 NPC，再按原配方权重生成杯底、球体、装饰及分数；保留原随机调用次序。食材添加按原杯底／球体／顶饰边界和顺序逐项检查，最大球数从允许配方计算；关卡按原开放小时与上一普通订单通关状态解锁。

烧烤的原15关、72份配方及7种食材须接入底层计算：NPC占用排除且候选耗尽不回退、NPC先于配方的随机调用、分配／提交生成／回滚分离、精确食材顺序、生串零分／熟串全分／焦串原倍率及中点取偶、准备状态与挂点容量限制、逐帧单精度烹饪和顾客耐心。烤熟帧不把超出的时间带入焦化计时，拖动暂停烹饪，游戏暂停同时冻结烹饪和顾客耐心；顾客入场演出完成后才倒数，超时只触发一次。

烧烤整局控制接入原准备区、三个烤架及顾客槽的占用和释放。首位顾客延迟2秒、随后间隔3秒读取实际 `BBQGameContent` prefab，而非类默认值；初始入场期间交单不会提前补客，后续仍按原引用顺序寻找空槽。入场完成才接受交单；生串、错误配方仍按原控制器完成一次零分交单，等待退场演出结束后补客并判断普通关结束。无尽使用原150秒时钟，暂停不消耗时间。烧烤入口、操作界面和历史纪录页须接入。

知识问答规则使用原题池筛选、权重抽题、选项洗牌、排除已答题、阶段解锁及奖励；当前入口与本地阶段结算见下文 。

拼图的183份原关卡须接入交换与条件计算：保留任意两块交换、有效交换计步、查看参考时禁止操作、重置恢复原排列与计步、逐块归位判定。原解析器按活动／关卡／难度排序生成前置链，逐项检查完成记录和原小时开放时间；快速完成跳过已领奖难度，并在第一个不允许或未到时间的难度停止。实际逐块还原后的首次通关按原 `AwardIds` 发奖并记录领奖集合，已通关可练习但不重复发奖；`JigsawPassLevelCondition` 只累计已接取实例对应的活动、关卡及难度。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_map_anniversary.py` 从固定版本官方 `defaultpackage_assets_config_luban.bundle` 完整解码 `anniversary_map_build`、`anniversary_map_build_item`、`activity_festival_shop` 三表，保留逐表输入／构造器哈希和消费字节数，生成 `assets/official/map-anniversary.json`。原建筑与 NPC Tag 对应八组修复交互；活动 217 对应原商店 514 的十九件商品。二十九个材料、奖励和商品图标从原 APK／已校验同版本 CDN 的原 Sprite 提取，没有自绘补充；来源、实际 URL、bundle、对象、输入／输出哈希见 [anniversary-source-manifest.json](../../assets/reference/official-map/anniversary-source-manifest.json)。

运行界面使用原 AnniversaryMapBuildDetailDialog、AnniversaryMapBuildSelectDialog、材料 Item 与 ActivityFestivalAnniversaryShopPanel／ShopItem，显示原建筑模型。每日总提交二次、每建筑限额、物资消耗、修复进度与奖励、完成奖励防重、原货币与库存均本地执行。

联网共同建设采用离线适配：首次进入开始本地计时，阶段长度使用原 168／168／167 小时；庆典阶段自动完成共享修复并保留个人贡献。该阶段自动完成属于本项目离线设计，不能据此声称已验证原服务器的自动完成规则。

固定版 `854_156383` 的 `ActivityPickGameMainPanel`、`ActivityPickGameContentSceneEffect`、`AnimationSoundPlayer`、`PickButtonEffect` 和 `ButtonSound` 按原对象引用接入。`prepare_activity_game_assets.py` 转换三套原 NPC 骨骼、组合皮肤、材质、挂件及12个所需动画数据集和五张评级图；原 UI 层级、曲线及粒子复用已转换的 map-ui。

依据：[activity-games/source-manifest.json](../../assets/reference/activity-games/source-manifest.json)。

`prepare_activity_game_audio.py` 从原 prefab 中解析 Wwise 引用，按7个原版音频分包转换22个事件、26条音频，保留原容器随机、循环、增益与停止动作；完整对象依据、音频包来源和转换哈希分别见 [activity-games/audio/bindings-source-manifest.json](../../assets/reference/activity-games/audio/bindings-source-manifest.json) 与 [activity-games/audio/source-manifest.json](../../assets/reference/activity-games/audio/source-manifest.json)。未使用新版同名包或自绘素材。

`prepare_match3_game.py` 从固定版 `854_156383` 提取菜狗、旅程庆典、春日出游及万圣节的原三消界面、关卡卡片、棋子、浮动分数、奖励已领取附件和对应活动内入口图，保留22套 prefab 的层级、布局、状态引用、动画曲线、UIParticle 和原中文规则；动态棋子及彩色棋子绑定原表的16条图片路径，八种奖励按原 `item` 表的 `RawIcon` 绑定图片。基础及万圣节布局复用同版本已转换的 map-ui，其余主题直接从原包转换；自适应奖励布局、原物品图标附件／文字效果及 `GlobalColors` 深色主题参数从原组件读取。输入对象、资源及输出哈希见 [match3-game/source-manifest.json](../../assets/reference/match3-game/source-manifest.json)，运行资源位于 `assets/official/match3-game/`。

`prepare_activity_game_audio.py --game match3` 从这些原 prefab 的 `ButtonSound`、`AnimationSoundPlayer` 和原动画事件读取13个事件，按5个固定版音频分包转换13条音频；保留原容器、增益、循环和停止动作。完整对象和音频来源、输入输出哈希见 [match3-game/audio/bindings-source-manifest.json](../../assets/reference/match3-game/audio/bindings-source-manifest.json)、[match3-game/audio/source-manifest.json](../../assets/reference/match3-game/audio/source-manifest.json)。结果奖励复用原 `AwardContent` 及既有原奖励卡片资源。未使用新版替代包或自绘素材。

拼图现有七套主题界面、37个 prefab、六个入口和52幅题图保留 `JigsawGameContent.GenerateImage` 的原 Texture2D、`JigsawSuffixSettings` 中文标识图、归一化 TRS、颜色、原布局与拖动行为；不生成替代图。871 对象级复核覆盖26个输入 bundle、3,065个主输入对象及全部布局／粒子引用；234个 PNG 均从871 Sprite 重新解码并逐个匹配输出 SHA256。九张 SimpleSunny 题图和题库来源已切换并通过回归（183阶段、2,196组状态用例、解局轨迹不变）。三个不同哈希对象中，`JigsawPanel` 用官方程序集类型树解出1,028字节字段流及 PPtr 绑定，两个配置表按183阶段的41个奖励ID、15个物品ID及557条既有规则提示验证投影不变；原始流不含 `ManagedReferencesRegistry` 数据，两个 `StateSwitcher` 的动态配置仍未恢复。变化的 `ParticleSystemRenderer` 已从871重新读取类型树并写入目录与来源清单。目录中另有120处来源指针没有原始对象哈希，尚不能证明其与871对象内容相同，暂未改写来源标记。三份拼图 Wwise 音频分包哈希一致。来源与投影核验见 [拼图来源清单](../../assets/reference/jigsaw-game/source-manifest.json)、[音频绑定来源清单](../../assets/reference/jigsaw-game/audio/bindings-source-manifest.json)、[音频来源清单](../../assets/reference/jigsaw-game/audio/source-manifest.json) 和[拼图来源重核脚本](../../scripts/reconcile_jigsaw_871_sources.py)。原始流的动态状态配置、目录来源指针核验及 Unity 导入／运行验证仍未完成。快速完成继续复用原 `ConfirmDialog/CostItemConfirmDialog.prefab`（`gacha-purchase.json`），保留原TMP换行／边距、自动对齐、默认语言键和数量／颜色配置。

2161 专用资源与当前停用状态见下方来源核验；配置、奖励与既有迁移边界分开记录，不据此开放联动活动。

`prepare_tree_felling.py` 从固定854_156383 APK及匹配 CDN 迁移 `FestivalMinigamePartyActivity` 的六个 UI prefab 及原圣诞活动入口、列表图标、任务面板和任务条目，共十个 UI prefab、`TreeFellingGameContent`、五个 `TreeSegment` 和 `Obstacles1`。保留原层级、坐标、缩放、深度、SpriteRenderer、背景适配、基座定位、动画曲线、Animator 条件和 UIParticle；138张原图片位于 `assets/official/tree-felling/sprites/`。男女角色从同版本原骨骼、图集及分离动画采样 `home/pugilism_idle`、三段 `home/pugilism_lianxu_*` 和 `map/failure_01`，原控制器驱动当前自定义身体与外观。原 UI 组件、表、语言、输入对象及输出哈希见 [tree-felling/source-manifest.json](../../assets/reference/tree-felling/source-manifest.json)。

`prepare_bingo_activity.py` 和 `prepare_scratch_activity.py` 从固定854_156383转换原宾果六个、刮卡七个面板／单格／任务／详情／规则预制体，保留坐标、布局组、动画曲线与事件、状态切换和原奖励引用。原 `UIMesh` 几何、材质参数与 GLES 程序哈希分别登记；刮卡原19个图案同时保留 UI 裁切与网格所用完整透明纹理，按原 `ScratchCardGridItem.SetSprite` 使用。资源位于 `assets/official/bingo-activity/` 和 `assets/official/scratch-activity/`；原对象、脚本、配置／语言及输出哈希见 [bingo-activity/source-manifest.json](../../assets/reference/bingo-activity/source-manifest.json)、[scratch-activity/source-manifest.json](../../assets/reference/scratch-activity/source-manifest.json)。

`prepare_bbq_game.py` 从固定854_156383 APK及匹配CDN转换15个原烧烤prefab、2个通用历史纪录prefab、原已领取／红点附件及劳动节烧烤入口，共20个布局；200张原图片保留裁切、坐标、挂点、食材生熟焦状态、奖励和纪录背景。29名顾客沿原 `FigureModelUI`，其中23个模型核验后复用既有 `partner-detail/catalog.json`，6个模型及垃圾桶的原骨骼、皮肤、材质、分离动画在开发时转换；玩家模型／头像空槽保留原运行时外观绑定。运行资源位于 `assets/official/bbq-game/`；原组件、动画事件、语言、AOT AkEvent入口、动态角色槽、复用输入及输出哈希见 [bbq-game/source-manifest.json](../../assets/reference/bbq-game/source-manifest.json)。未使用新版替代包或自绘素材。

缺项小游戏使用固定 `854_156383` 的已校验 APK／CDN 内容。官方逻辑来源为 `client-source/FootballCollisionPanel.cs`、`client-source/FootballGameWindowManager.cs`、`common-source/EntityComponent/FCGameStateComponent.cs`、`client-source/ActivityDreamDance.cs`、`client-source/ActivityDreamDanceQuizUI.cs`、`common-source/DreamDanceNetService.cs`、`client-source/ActivityJumpGamePanel.cs` 和 `client-source/KnowledgeQuizQuestionPanel.cs`；连线与能量回路分别来自 `client-source/ElementLinkGame.cs` 与 `client-source/EnergyCircuitGridPlayable.cs`。

已核验并提取的原资源包包括 `defaultpackage_assets_prefabs_ui_activityfootballfestival.bundle`、`defaultpackage_assets_config_luban_activitydreamdance.bundle`、`defaultpackage_assets_config_luban_activityminigame.bundle`、`defaultpackage_assets_scenes_doodlejumpgame.bundle`、`defaultpackage_assets_prefabs_ui_activity_jumpgamecommon.bundle`、`defaultpackage_assets_image_ui_elementlink.bundle` 和 `defaultpackage_assets_image_ui_energycircuit.bundle`。知识问答题目／阶段表另以 `scripts/fixtures/knowledge-quiz-reference.json` 保存固定版 `ActivityKnowledgeQuizQuestionInfo`／阶段表的核对结果。原 `activityjumpgame` ScriptableConfig 分包未在已校验原版本缓存中取得，因此跳跃使用已取得的场景、计分配置和官方反编译逻辑，不冒充该缺包已下载。

`prepare_football_assets.py` 从已校验原版 `defaultpackage_assets_prefabs_ui_activityfootballfestival.bundle` 的 `FootballCollisionPanel.prefab` 转换完整静态控件树到 `assets/official/activity-games/football-panel.json`，并解码其55项原 Sprite 引用，含 `ImgFootballCollision_Ground`； 使用该清单显式加载运行时图片。prefab、Sprite、来源代码及转换输出 SHA256 见 [activity-games/source-manifest.json](../../assets/reference/activity-games/source-manifest.json) 的 `football_offline`。

上述迁移保留官方参数、题目筛选、选项显示位、输入状态、阶段奖励和原子存档；足球房间同步、舞蹈网络请求、知识问答在线助战／排行，以及连线／能量回路服务端题盘生成仍按[活动小游戏](activity-minigames.md)登记为单机边界内的自定义部分。

`scripts/prepare_linkage_activities.py` 从固定 `854_156383` APK／匹配原版本 CDN 转换音乐节 `ActivityMusicFestival/GachaPanel/LuoTianYiGachaPanel`、春节 `ActivityFestivalSpring/Entry/ActivityFestivalSpringEntryPanel`、周年庆 `ActivityFestivalAnniversary/Entry/ActivityFestivalAnniversaryEntryPanel` 和万圣节 `Activity/Halloween/Entry/ActivityHalloweenEntryPanel` 原 prefab。运行界面目录为 `assets/official/linkage-activities/catalog.json`；`assets/reference/linkage-activities/source-manifest.json` 登记原 bundle／对象／URL／哈希、prefab 窗口、表格输入与转换输出。

活动时间、持续时长、CN/OpenTest 区服、UTC+8 偏移、入口条件和音乐节扭蛋配置来自固定版 `activity`、`region_release_time`、`local_time_offset`、`conditions`、`function_limit`、`activity_special_gacha`、`special_gacha`、`special_gacha_award_group`、`reward_rule` 与 `navigate`。客户端逻辑依据 `ActivityEntryLinkageActivityHandler.Refresh`、`ActivityEntryPanelCommon.OnEnableAndInited`、`ActivityInfo.IsOutOfDate`、`LuoTianYiGachaContent`、`WheelGachaContentBase.DoGahcaByCount`、`StandardRandomGroup` 和 `SpecialGachaUtils` 迁移；单机开服时间映射及本地抽奖差异见[行囊、物品与宝箱](inventory.md)。没有推断活动年度复开，没有使用新版替代包或自绘素材。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

知识问答客户端原通过 `KnowledgeQuizQuestionPanelAnswerContent.Init`／`SelectQuestion` 请求服务器返回初题、`NextQuestionId`、`AnsweredCount` 和 `IsCleared`；Common 的 `ActivityKnowledgeQuizQuestionInfoParser.DrawQuestions`／`BuildShuffledOptionMap`、原题表及 `KnowledgeQuizSetting` 则提供本地可用的题池、权重、选项洗牌、每阶段3个正确答案和0.5秒反馈间隔。为满足项目离线运行，本地用这套官方抽题／洗牌逻辑和配置替代服务器下发的题号与状态，并在本地保存正确进度、阶段奖励和领取记录；随机种子与线上服务端不同，因此不声称每局题序与线上一致。聊天助战及排行榜仍依赖在线服务，本地页不提供助战，相关排名目标按用户确认停用。

史莱姆、孤独摇滚联动的全部关联小游戏现按用户最新决定暂不开放，包含史莱姆拼图2161，范围见[暂不开放约定](offline-access.md#暂不开放的联动活动及伙伴)。下述常驻入口及史莱姆题图／界面复用的旧授权不再作为开放这些联动小游戏的依据；既有迁移说明仅记录实现现状。

固定版客户端、Common 及序列化代码只有 `ItemParamScratchCard.RandomIcons`／`RewardRecord` 的读取和同步，未取得服务端生成各奖级图案与16格分布的实现。用户明确确认保留既有本地结算：先按原奖励表和权重抽定奖励，三个相同图案中奖，其余图案各不相同；另外两档预览各使用原19图案中不进入本卡格子的一个图案。图案、奖品、权重和刮擦动画取原内容，此处分布为已确认的本地补充。活动取消运营开放日历，保留原个人条件、消耗、阶段与领取语义。

本地任务成员登记替代服务器下发，不直接把任务标为完成；所有材料、随机、卡片与奖励共同保存。

宾果／刮卡循环任务的服务端提交后进度写回未取得；用户明确确认按本地真实累计进度批量提交，每次扣除相应任务进度、保留余量，奖励与次数上限使用原表。宾果11601从本地活动激活后记录实际消耗，刮卡11900使用独立活动扫荡事件。此处为经确认的单机结算，不能标为原服务端实现。

音乐节奏的服务端开局种子与结果提交由本地真实操作和原子存档替代；原关卡规则不变。未提交的局在退出后放弃，已结算最高分与任务进度保留；重复提交不重复计数，保存失败回滚。活动列表复用原音乐节小游戏卡片提供两套常驻入口；键盘左右／A、D等价于原左右按钮。

音乐恢复官方 Wwise 音频的本地播放；打开小游戏时暂停本地家园背景音乐，关闭后按当前音量设置恢复，避免两个播放链叠加。活动剩余时间因常驻入口而隐藏。

伐木的固定版客户端、Common、程序集、五条轮次配置和 prefab 仅有 `ActivityGame.Rounds`、`TreeFellingStatusCountdownComponent.Time` 的读取，未取得服务端每局轮数和提示等待写入器。参数0／1相同、2／3相同、4的障碍最密；原游戏及房间面板未发现难度选择控件，`RoundNo` 直接读取当前轮次，不能证明原作每局必玩五轮。用户已确认单机按原表0～4依次各玩一轮，每轮11秒，开场／轮次／结束提示按原动画播完继续。五轮汇总真实砍伐分数并保存个人最高分、参与次数和已接取任务进度；未取得原服务端名次换算，不发放排名游戏币。退出放弃未提交局，重复提交、失败写盘不重复计数，原圣诞主页开放伐木和已支持任务，其余子活动保持原图但禁用入口；不代表整个圣诞活动已经迁移。

三消使用同一常驻活动适配，首次打开／实际开局记录本地活动起点；仍按原 `UnlockTime` 小时数解锁，且须通关前一关。服务端种子／局号和结果写入改为本地真实引擎及原子存档，奖励、任务、最高分共同提交；万圣节战令目标仅支持真实已接取实例的计算，战令发放入口不由三消额外创建。

小游戏历史纪录的原 `HistoricalSegmentRecordScopeType` 只有位面／全局范围，客户端及 Common 未提供服务端纪录写入器。用户明确确认将这类共享纪录改为本地真实挑战首次达到原分数档后开放该档原奖励，每档只领一次；保存本地角色标识0与首次提交时间，同一次活动领奖发放所有已达成未领档并保存累计索引。普通关、未提交局和退出局不写入挑战纪录。此处为单机自定义，原分数档和奖励不变。

烧烤采用同一常驻活动起点及本地事务适配。服务端局号和结果写入由本地实际对局与原子存档代替；开局保存失败时回滚新建的内存对局并保留旧局，结算或退出保存失败时保留原实际对局供重试，原奖励及计数规则不变。

拼图沿用常驻活动起点与本地事务适配，原小时解锁和逐难度前置保持；服务端领奖请求由本地实际完成的棋盘提交。中途退出不领奖，练习不重复发奖或补记任务；保存失败保留可重试的实际完成局，同时回滚物品、随机状态、领奖记录和任务。

871 所选包清单中存在 `activityslime` 与 `tipdotgraphassets_activity_activityslime` 配置 bundle；按 bundle 名检索未发现专用 `ActivitySlime/Jigasaw` prefab 或 `UI/Slime/JigsawActivity` 图片 bundle。现有九张 SimpleSunny 主题图的五个输入 bundle 已按 871 包清单、实际 APK 条目或 871 下载收据核验；转换 PNG 的 SHA256 与登记值一致，来源写在 `jigsaw-game/source-manifest.json` 的 `source_migrations.jigsaw_simple_sunny_images`。2161 当前仍按[暂不开放约定](offline-access.md#暂不开放的联动活动及伙伴)停用，不为其制作替代史莱姆素材；货币图 `Money_3034` 的 871 对象级查找尚未完成。

拼图一键完成的原客户端 `JigsawGameContent.OnConfirmFastComplete` 只发送请求，未取得服务端结算写入器。用户已确认单机按原 `JigsawFastCompleteSetting` 一次消耗1张道具325，完成当前题图所有符合原 `JigsawUIUtils.GetFastCompletableOrders` 的未领奖难度，逐档发放原奖励并累计对应已接取任务。成本、奖励、领奖记录和任务共同保存；重复请求、已完成题图及保存失败均不会重复扣费或计数，保存失败后保留可重试状态。

用户确认保留个人游玩、分数和参与任务，停用依赖其他玩家的名次／胜负目标。`ActivityGameOutpaceRivalCountCondition`、`ActivityGameFirstPlaceCountCondition`、`ActivityDreamDanceRankCondition` 不生成可领奖目标，音乐节奏和三消的排行榜入口隐藏。

官方 `FCGameStateComponent` 的房间状态、对手操作和胜负提交依赖在线服务。按用户确认，足球在官方 prefab 布局和球场图上绘制本地物理快照、提供拖动蓄力输入，并增加确定性对手：最近球员朝球射门，蓄力距离为官方曲线归一化长度的72%；每方每局各射一次，进球或全队倒下判胜，双方均未得分则该局平局。总局数和所需胜局数读取官方 `MaxStages`／`WinStages`，本地只记录个人胜／负／平，不创建在线任务、奖励或排名。Unity 实现须管理本地对手及赛制，并按官方固定步长和碰撞处理顺序迁移物理；应优先复用原 `Framework.FixedPoint`，不沿用跨语言浮点近似。物理依据见 [FootballCollisionWorld](../../.tools/source-packages/official-code/common-source/Common/FootballCollisionWorld.cs)、[LWPhysicsWorld](../../.tools/source-packages/official-code/framework-firstpass-source/Framework.Physics.LightweightPhysics/LWPhysicsWorld.cs)、[ContactSolver](../../.tools/source-packages/official-code/framework-firstpass-source/Framework.Physics.LightweightPhysics/ContactSolver.cs)、[FP64](../../.tools/source-packages/official-code/framework-firstpass-source/Framework.FixedPoint/FP64.cs)；界面、蓄力和官方 prefab 来源见 [FootballCollisionPanel](../../.tools/source-packages/official-code/client-source/FootballCollisionPanel.cs)、[FootballChargeController](../../.tools/source-packages/official-code/client-source/Common.FootballPhysics.UI/FootballChargeController.cs)、[FootballPitchView](../../.tools/source-packages/official-code/client-source/Common.FootballPhysics.UI/FootballPitchView.cs)、[football-panel.json](../../assets/official/activity-games/football-panel.json) 及 `scripts/prepare_football_assets.py`（来源工具标识）。来源文件与55项图片绑定哈希见 `activity-games/source-manifest.json` 的 `football_offline`。

固定版 `ActivityFestivalStageDanceComponent.DanceInfo` 未提供个人入口到 `QuizId` 的映射，`DreamDanceNetService` 的答题提交和评分依赖服务端。按用户确认，舞蹈题列固定按 `activity_dream_dance_quiz` 的 `Id` 顺序播放168条；本地通过数和最长连击仅作个人记录，不伪造服务端分数、任务或排行。舞蹈专用 prefab 尚未定位，须继续从官方内容恢复；当前没有可验收的 Unity 舞台入口。

聊天助战、房间邀请和依赖其他玩家的排名目标均不开放；它们属于在线服务，没有离线等价。地图元素连线／能量回路由对应地图关卡规则提供，不属于本功能缺项。

### 已确认的本地接线与边界

`prepare_activity_game_audio.py --game tree` 按原 `ButtonSound`、`AnimationSoundPlayer`、动画事件和 `TreeFellingGameConfigSO.PlayerHitSound` 取得七个事件，从两个固定版 Wwise 分包转换七条音频，保留原容器、增益、循环及停止动作。完整来源与哈希见 [tree-felling/audio/bindings-source-manifest.json](../../assets/reference/tree-felling/audio/bindings-source-manifest.json)、[tree-felling/audio/source-manifest.json](../../assets/reference/tree-felling/audio/source-manifest.json)。未使用新版替代包或自绘素材；服务端每局轮数和等待时长写入器未取得；用户已明确同意单机按五组原参数各玩一轮，每轮11秒，提示按原动画播完继续。

`prepare_activity_card_audio.py` 沿原 `ButtonSound`、`AnimationSoundPlayer` 及动画事件转换宾果三个事件／三条音频、刮卡四个事件／四条音频，保留 Wwise 容器与增益。两模块的 `audio/bindings-source-manifest.json`、`audio/source-manifest.json` 登记原引用与输入输出哈希。未使用新版替代包或自绘素材；服务端图案排列未取得，已确认本地分布与三档预览的具体差异见[活动小游戏](activity-minigames.md)。

`prepare_activity_game_audio.py --game bbq` 依据原按钮、动画事件、`BBQMainGameContent` 及SDK `AkEvent` 生命周期配置，从5个固定版Wwise分包转换18个事件、19条音轨，保留容器随机分支、增益及停止动作。`LuoXiaoHei_Fail_Play` 沿原事件引用取得 `Battle_ME_00` 内嵌媒体；`prepare_battle_audio.py.convert` 补齐实际TXTP所引用的跨声库媒体依赖，不改事件选择与音频内容。共享纪录的本地达标领奖规则属于用户确认的单机自定义，具体差异见[活动小游戏](activity-minigames.md)。

依据：[bbq-game/audio/bindings-source-manifest.json](../../assets/reference/bbq-game/audio/bindings-source-manifest.json)、[bbq-game/audio/source-manifest.json](../../assets/reference/bbq-game/audio/source-manifest.json)。

## 尚未实现

- 成长试炼、宾果、刮卡、音乐节奏、三消、伐木、拼图、知识问答、足球和舞蹈；逐项恢复官方入口、操作、动画、音效、结算与存档。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。
- 舞蹈专用舞台 prefab 尚未定位；史莱姆2161及孤独摇滚相关小游戏暂停开放，旧外观复用授权不覆盖后续暂停决定。

## 代码与资源入口

`scripts/prepare_bingo_activity.py`（来源工具标识）、`scripts/prepare_scratch_activity.py`（来源工具标识）、`scripts/prepare_activity_card_audio.py`（来源工具标识）、[bingo-activity/source-manifest.json](../../assets/reference/bingo-activity/source-manifest.json)

[scratch-activity/source-manifest.json](../../assets/reference/scratch-activity/source-manifest.json)、

[BingoActivityPanelContent](../../.tools/source-packages/official-code/client-source/BingoActivityPanelContent.cs)、[PlayerBingoActivityDataExt](../../.tools/source-packages/official-code/common-source/Common/PlayerBingoActivityDataExt.cs)、[TaskDataUtils.GetCanSubmitTimes](../../.tools/source-packages/official-code/client-source/TaskDataUtils.cs)

[PlayerTaskDataUtils](../../.tools/source-packages/official-code/common-source/Common/PlayerTaskDataUtils.cs)、[TaskDetailDialog.OnClaim](../../.tools/source-packages/official-code/client-source/TaskDetailDialog.cs)、

[test_jigsaw_reference.ps1](../../scripts/test_jigsaw_reference.ps1)、[引擎／配置适配夹具](../../scripts/fixtures/jigsaw_reference_harness.cs)、[期望值及来源哈希](../../scripts/fixtures/jigsaw-reference.json)、

[test_ice_cream_reference.ps1](../../scripts/test_ice_cream_reference.ps1)、[引擎／配置适配夹具](../../scripts/fixtures/ice_cream_reference_harness.cs)、[原配置、期望值及输入哈希](../../scripts/fixtures/ice-cream-reference.json)

[generate_bbq_reference.py](../../scripts/generate_bbq_reference.py)、[引擎／配置适配夹具](../../scripts/fixtures/bbq_reference_harness.cs)、[test_bbq_reference.ps1](../../scripts/test_bbq_reference.ps1)、[原配置、输入哈希与期望值](../../scripts/fixtures/bbq-reference.json)

[test_knowledge_quiz_reference.ps1](../../scripts/test_knowledge_quiz_reference.ps1)、[引擎／配置适配夹具](../../scripts/fixtures/knowledge_quiz_reference_harness.cs)、[原表、期望结果及输入哈希](../../scripts/fixtures/knowledge-quiz-reference.json)、

`scripts/prepare_activity_games.py`（来源工具标识）、

`scripts/prepare_tree_felling.py`（来源工具标识）、[tree-felling/source-manifest.json](../../assets/reference/tree-felling/source-manifest.json)

[tree-felling/audio/source-manifest.json](../../assets/reference/tree-felling/audio/source-manifest.json)、

`scripts/test_match3_reference.ps1`（来源工具标识）、[运行适配夹具](../../scripts/fixtures/match3_reference_harness.cs)、[原代码测试输入及源文件哈希](../../scripts/fixtures/match3-reference.json)、

`scripts/prepare_match3_game.py`（来源工具标识）、

[match3-game/source-manifest.json](../../assets/reference/match3-game/source-manifest.json)、[match3-game/audio/source-manifest.json](../../assets/reference/match3-game/audio/source-manifest.json)、`scripts/prepare_activity_game_assets.py`（来源工具标识）

`scripts/prepare_activity_game_audio.py`（来源工具标识）、[activity-games/source-manifest.json](../../assets/reference/activity-games/source-manifest.json)、[activity-games/audio/source-manifest.json](../../assets/reference/activity-games/audio/source-manifest.json)、`scripts/prepare_jigsaw_game.py`（来源工具标识）、

[jigsaw-game/source-manifest.json](../../assets/reference/jigsaw-game/source-manifest.json)、[jigsaw-game/audio/source-manifest.json](../../assets/reference/jigsaw-game/audio/source-manifest.json)、

[test_jump_score_reference.ps1](../../scripts/test_jump_score_reference.ps1)、[ActivityKnowledgeQuizQuestionInfoParser.DrawQuestions／BuildShuffledOptionMap](../../.tools/source-packages/official-code/common-source/Common/ActivityKnowledgeQuizQuestionInfoParser.cs)

[KnowledgeQuizQuestionPanelAnswerContent](../../.tools/source-packages/official-code/client-source/KnowledgeQuizQuestionPanelAnswerContent.cs)、[KnowledgeQuizSetting](../../.tools/source-packages/official-code/common-source/Common/KnowledgeQuizSetting.cs)

[FootballCollisionWorld](../../.tools/source-packages/official-code/common-source/Common/FootballCollisionWorld.cs)、[LWPhysicsWorld](../../.tools/source-packages/official-code/framework-firstpass-source/Framework.Physics.LightweightPhysics/LWPhysicsWorld.cs)

[ContactSolver](../../.tools/source-packages/official-code/framework-firstpass-source/Framework.Physics.LightweightPhysics/ContactSolver.cs)、[FP64](../../.tools/source-packages/official-code/framework-firstpass-source/Framework.FixedPoint/FP64.cs)、[FootballCollisionPanel](../../.tools/source-packages/official-code/client-source/FootballCollisionPanel.cs)、[FootballChargeController](../../.tools/source-packages/official-code/client-source/Common.FootballPhysics.UI/FootballChargeController.cs)、[FootballPitchView](../../.tools/source-packages/official-code/client-source/Common.FootballPhysics.UI/FootballPitchView.cs)

[football-panel.json](../../assets/official/activity-games/football-panel.json)、`scripts/prepare_football_assets.py`（来源工具标识）
