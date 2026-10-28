# 成长试炼与活动小游戏

[总索引](../README.md)

状态：未完成

## 功能简介

各活动使用自己的官方入口、关卡、计分、道具、奖励与界面；节奏、三消、拼图、伐木、宾果、刮卡、烧烤及其他小游戏不能共用一套近似规则。

## 原始官方实现

各活动使用自己的官方入口、关卡、计分、道具、奖励与界面；节奏、三消、拼图、伐木、宾果、刮卡、烧烤及其他小游戏不能共用一套近似规则。

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

`prepare_jigsaw_game.py` 从固定854_156383 APK及匹配 CDN 转换七套已有主题界面及其共用行、卡片、星星、格子和六个实际入口，共37个 prefab；有配置和界面的六主题使用52幅原题图。图片保留 `JigsawGameContent.GenerateImage` 的原 Texture2D、`JigsawSuffixSettings` 中文标识图、归一化TRS及颜色，转换为本地PNG；没有自行绘制。状态、严格解析的RawImage、原EventSystem拖动阈值和GUID星星图保留原配置。其他主题未提供的模糊图请求对应默认隐藏且未被卡片状态启用的节点，逐项检查后不生成替代图。快速完成复用已转换的原 `ConfirmDialog/CostItemConfirmDialog.prefab`（`gacha-purchase.json`），补读原TMP换行／边距、自动对齐、默认语言键和数量／颜色配置。对象来源与输入／输出哈希见 [jigsaw-game/source-manifest.json](../../assets/reference/jigsaw-game/source-manifest.json)。`prepare_activity_game_audio.py --game jigsaw` 从三个原Wwise分包转换六个按钮／动画事件及六条音频，来源见 [jigsaw-game/audio/source-manifest.json](../../assets/reference/jigsaw-game/audio/source-manifest.json)。

史莱姆2161专用界面、Slime01～09题图与Money_3034在固定APK五包、新版864清单及原APK／已校验CDN相关容器内均未找到。27档奖励包含用于原兑换商品的4,950枚魔素结晶；按用户“若影响玩法与奖励则允许复用”的明确意。复用既有200（SimpleSunny）官方界面、入口外观和九张题图。缺失货币图使用既有原 `ItemIconDefault.FakeIcon` 文字占位，显示原物品名“魔素结晶”；没有绘制或伪造史莱姆素材。映射与缺项在  单独登记，2161的原玩法参数与奖励保持独立，相关源码及原配置仍按真实固定版本登记。

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

尚无完整 Unity 流程的完成结论。

## 自定义实现

开放与本地保存遵循[共同离线政策](offline-access.md)，具体本地接线和未确认规则见下文。

### 已确认的本地接线与边界

`prepare_activity_game_audio.py --game tree` 按原 `ButtonSound`、`AnimationSoundPlayer`、动画事件和 `TreeFellingGameConfigSO.PlayerHitSound` 取得七个事件，从两个固定版 Wwise 分包转换七条音频，保留原容器、增益、循环及停止动作。完整来源与哈希见 [tree-felling/audio/bindings-source-manifest.json](../../assets/reference/tree-felling/audio/bindings-source-manifest.json)、[tree-felling/audio/source-manifest.json](../../assets/reference/tree-felling/audio/source-manifest.json)。未使用新版替代包或自绘素材；服务端每局轮数和等待时长写入器未取得；用户已明确同意单机按五组原参数各玩一轮，每轮11秒，提示按原动画播完继续。

`prepare_activity_card_audio.py` 沿原 `ButtonSound`、`AnimationSoundPlayer` 及动画事件转换宾果三个事件／三条音频、刮卡四个事件／四条音频，保留 Wwise 容器与增益。两模块的 `audio/bindings-source-manifest.json`、`audio/source-manifest.json` 登记原引用与输入输出哈希。未使用新版替代包或自绘素材；服务端图案排列未取得，已确认本地分布与三档预览的具体差异见[活动小游戏](activity-minigames.md)。

`prepare_activity_game_audio.py --game bbq` 依据原按钮、动画事件、`BBQMainGameContent` 及SDK `AkEvent` 生命周期配置，从5个固定版Wwise分包转换18个事件、19条音轨，保留容器随机分支、增益及停止动作。`LuoXiaoHei_Fail_Play` 沿原事件引用取得 `Battle_ME_00` 内嵌媒体；`prepare_battle_audio.py.convert` 补齐实际TXTP所引用的跨声库媒体依赖，不改事件选择与音频内容。共享纪录的本地达标领奖规则属于用户确认的单机自定义，具体差异见[活动小游戏](activity-minigames.md)。

依据：[bbq-game/audio/bindings-source-manifest.json](../../assets/reference/bbq-game/audio/bindings-source-manifest.json)、[bbq-game/audio/source-manifest.json](../../assets/reference/bbq-game/audio/source-manifest.json)。

## 尚未实现

接通官方代码、原生界面／资源、本地状态与存档，完成实际入口、模拟输入、画面及失败恢复验证，并发布对应 Windows 版本。
