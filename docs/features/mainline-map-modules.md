# 主线地图公共模块

[总索引](../README.md)

状态：公共模块抽取已完成；功能迁移未完成（各玩法的原表现与内容缺项仍待接入）

## 功能简介

主线地图通过 `OfficialMainlineMapRuntime.Begin(mapClassId, …)` 使用同一套运行时。目前有完整数据绑定的入口是 Map10、Map11；地图资源、区域条件、剧情、实体和进度仍按地图 ID 隔离。新增地图须先迁移其官方数据与专属流程，再登记 `OfficialMainlineMapProfile`，不能仅替换地图 ID 后宣称地图完成。

## 完整还原部分

公共代码位于 [`Presentation/World/Mainline`](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/)。`OfficialMainlineMapRuntime` 在 `Awake` 为每次入图分别创建 HUD、区域、导航、雕像、体力、探寻、小地图七个模块，负责按原顺序组装、更新和退出；窗口、路线、待交互对象、区域进度、刷新计时、动态实体和渲染目标由所属模块持有。模块实现使用宿主内的私有类型保持封装，不把单张地图或单个角色的可变状态放入全局单例。各图专属剧情、首战引导、机关、音频与实体载荷留在原适配层。

入口组件保留原 MonoScript GUID 和 `MovedFrom`；纯 C# 模块沿用原脚本 `.meta`，原 prefab、层级、坐标、动画、数值、执行次序与存档 DTO 保持兼容。`OfficialMainlineUiCatalog` 按当前 `MapProfile.NativeUiRoot` 缓存原 UI 目录，HUD、雕像、奖励列表、探寻与小地图共用它；通用文本不再由女神像持有或加载。

| 模块 | 当前代码与复用范围 |
| --- | --- |
| 地图配置与载荷 | `OfficialMainlineMapProfile` 集中已迁移地图的资源根、原生 UI 根、阶段／故事数量及必须存在的区域条件。`Data` 分部保留各图官方模板、故事图和数量校验。 |
| HUD、顶部栏、任务与功能入口 | `OfficialMainlineHudModule` 管理 `OfficialWorldUiRenderer`、HUD 控件、顶部栏、任务、功能入口、头像、快速购买与提示的状态和销毁；原生 prefab 路径从地图配置解析。Map10 现有 UI 迁移缺项继续保留在所属地图页。 |
| 区域开放 | `OfficialMainlineRegionsModule` 持有区域进度与雾层，`OfficialMainlineRegionPolicy` 可独立装载官方 `TbMapRegionInfo`／`TbConditionInfo`。保留原有全探索与剧情次数分支、比较运算、错误拒绝及配置检查；实体显隐、交互锁、小地图和任务消费同一份策略。开放条件不等于已实现雾或公开地区状态转换。 |
| 寻路与移动 | `OfficialMainlineNavigationModule` 持有当前位置、路线、到达回调和待交互目标，负责屏幕点击、实体命中、交互距离、路径跟随和逐格存档；继续调用 `OfficialWorldMapSession`、`OfficialMapPathfinder` 与 `OfficialMapGroundRules`。桌面输入提供器与原鼠标输入共用遮挡、命中和寻路流程。 |
| 女神像与失落的女神像 | `OfficialMainlineStatuesModule` 的 `Goddess.cs` 共用探索条件、原生弹窗、按原阶段顺序激活、回执与传送；地区名称按当前地图 ID 查询。当前接入范围为 Map11，珍宝预览等缺项见[森之国](../maps/map-11.md)。 |
| 英雄／魔女访问及魔女阶段 | `OfflineMainlineProgressSession.Statues` 提取女神激活、全图魔女计数、英雄／魔女每日共享额度、奖励、HP 与待战斗效果事务；同一 `OfficialMainlineStatuesModule` 的 `Witch` 实现保留原书页、预览、激活与周期访问，`HeroStatues` 实现已接入四种英雄雕像原弹窗、410 等级属性、首次激活与周期访问；原角色／粒子演出仍待恢复。 |
| 探索进度 | `OfficialMainlineRegionsModule` 与既有 `OfficialMapProgressAdapter`、`OfficialMapExplorationProgress`、`OfficialMapProgressCalculator` 共用官方实体状态和探索点计算。 |
| 地图体力点 | `OfficialMainlineStaminaModule` 与 `OfflineMapStaminaService` 共用金币8011、铁矿8021、时之砂8026和怪物5011的扣费、奖励、战斗回执及本地刷新；原区域／组间隔由 `interactions.json` 加载。 |
| 区域阴影 | `OfficialMainlineRegionsModule` 按 `MapProfile.HasRegionFog` 挂载 `OfficialMainlineRegionFog`，资源根与数量断言从当前地图配置传入，不再内置 Map11 路径；当前载荷恢复 Map11 的73份原 LightRegion 层级、Freeform网格、原衰减贴图及地区公开后的半径渐退；地表按原 Multiply 光照计算。其他地图及日夜场景装饰切换尚待接入。 |
| 命运果实与探寻 | `OfflineFruitTreeService` 共用生产、容量、时间推进和收取事务；`OfflineWorldExplorationService` 共用探寻费用、候选／保底、动态实体与历史事务，`OfficialMainlineExplorationModule` 管理原 HUD 入口、动态实体、确认／详情／令牌窗口、遮罩与释放。果树生产服务继续由家园消费同一角色存档，不在地图重建一套生产规则。完整实体交互与演出仍待完成，详见[命运探寻](world-exploration.md)。 |
| 小地图与图标 | `OfficialMainlineMiniMapModule` 管理相机、渲染纹理、圆形／全屏面板、图标、跨图页签和筛选，共用已迁移的原面板、图标、跨图浏览、缩放与档案筛选存储；各图几何继续按 ID 加载。 |
| 交互、剧情、战斗与存档 | `Interactions`、`Story`、`Dialogue`、`Battle`、`Persistence` 分部保留既有执行顺序；Map10 首战引导独立在 `Map10` 分部。森之国机关、动态怪物与音频仍消费其专属载荷。 |

`Modules.cs` 集中模块组装与各自的 UI／渲染目标释放；开发检查通过 `OfficialMainlineMapInspection` 定位所属模块，只用于 Editor／development Player 或显式启用 `ZJCS_MAINLINE_MODULE_VALIDATION` 的回归构建，正式运行流程不依赖反射。`map-{ID}-save.json`、角色进度、回执前缀和 `minimap-filter.json` 的现有格式保持兼容。`Map10Runtime…` 数据 DTO 名称暂保留，避免把运行时代码抽取混入资源／存档结构变更。

官方依据沿用已提取的 `WorldCanvas`、`WorldPanel`、`FunctionEntryDialog`、`MiniMapMaskedUI`、`MiniMapFullScreenPanel`、`GoddessTouchDialog`、`LostGoddessTouchDialog`、`WorldWitchTouchUI`、`WorldMapProgressCondition`、`StoryCountCondition` 以及现有地图导航／进度实现。各图来源与哈希见 Map10、Map11 所属页和原来源清单；动态怪物、战斗场景和时间轴的来源清单及生成器中的运行时代码绑定同步到公共目录，官方输入与素材哈希保持原值。七类模块与原 Git 实现的对应、官方客户端代码、输入／输出 SHA256、四份原页签动画及 prefab 显式绑定见 [mainline-modules-source-manifest.json](../../assets/reference/official-map/mainline-modules-source-manifest.json)。

验证入口为 `Zjcs.Editor.MainlineMapModuleValidation.Run`，使用 `-verify-mainline-modules <隔离输出目录>`；同一检查也可在 Windows development Player 运行。Unity `2022.3.57f1c2` 已通过编译、`ProjectTools.Validate`、主线／雕像事务检查与带图形的 Play Mode 回归。地图移动通过注入屏幕点击走 UI 遮挡、实体命中与寻路；Map11 小地图开关、跨图页签、筛选和女神激活使用原按钮的指针按下／抬起／点击事件，并核对最上层射线命中。检查覆盖区域探索／剧情门槛、Map10／Map11 位置冷读、筛选 JSON 冷读、女神激活恢复、英雄／魔女每日共享额度及待战斗效果恢复。七类模块拆分后已在隔离工程以 重跑该完整流程，并覆盖探寻概率／历史页签、令牌拖动、单次／十连费用、原引导宝箱地图点击免体力领取及冷读恢复。女神地区完成度及打开弹窗由测试设置，未覆盖自然探索到 100% 或从地图点击雕像的完整路线。

已实际查看 Map10 地图、Map11 HUD、全屏小地图和失落女神弹窗截图；Map10 HUD 当前不可见，Map11 地形／水面／遮挡仍有原有缺项，完整画面和地图验收继续由所属页维护。Python Map11 运行载荷生成与小地图转换三项检查通过。另一次 Map10 旧输入来源检查失败：未纳入当前 Git HEAD 的 `assets/official/adventure.json` 实际哈希为 `a6820d65…`，原清单期望 `84b92c87…`；本次未修改该输入或预期哈希，不能据此声明旧输入来源校验通过。

Windows 普通 Player 已成功构建，在内置屏以 真实运行并通过上述同一套模拟指针、拖动、区域条件、女神／英雄、金币采集／刷新、探寻和冷读检查；已查看小地图、清雾、探寻概率／历史的实际画面。构建入口 `BuildWindowsPlayer` 支持 `-mainline-player-output <exe>` 和 `-mainline-release-probe`；后者仅为本次普通回归构建增加 `ZJCS_MAINLINE_MODULE_VALIDATION`，启动参数仍为 `-verify-mainline-modules <隔离目录>`，并阻止正常启动流程另开存档界面，测试不写入用户档案。沿用本机已安装的 Mono Windows 支持，构建后恢复原脚本后端。回归版本位于 `.tools/mainline-modules-player/杖剑传说.exe`，不带验证参数时仍走正常游戏入口。当前 Unity 日志仍有原生 `PresentFrame／WaitForLastPresentationAndGetTimestamp` 呈现时序告警；development 控制台遮挡的截图不作为画面通过证据，未声明该引擎告警已修复。本次为既有实现的公共模块抽取，完整玩法缺项见下文，未覆盖桌面正式版本。

新增载荷固定 `854_156383`：`scripts/prepare_mainline_interactions.py` 导出英雄参数、地区等级、区域刷新和体力点奖励；`scripts/prepare_unity_world_exploration.py` 解码7种令牌、65条实体组变体、28种模板及68条递归条件，已接入公共探寻事务与原窗口入口。`prepare_unity_map11_ui.py` 扩展至59份官方面板、535张sprite，保留英雄、探寻、奖励列表层级与原动画事件；来源分别见 `mainline-interactions-source-manifest.json`、`world-exploration-source-manifest.json` 及 Map11 UI 清单。探寻令牌列表恢复原LoopScrollRect的9个类，模糊和渐变的原生方法及程序转换分别记录在所属来源清单。

遗失女神领奖 UI 的来源准备将提取数据进一步扩为65份面板、945张Sprite和1,055条图标记录，新增六份原层级及394项主动／被动技能图标；导入器同步接受原结果窗和技能图标基础组件。该批工作保留原阶位／窗口映射／星级图／属性行动画及来源哈希，尚未接通地图入口和结果控制器，完整边界见[命运探寻](world-exploration.md)。各地图未完成页及配套提取／转换工具一并保留，入口见[世界地图](world-map.md)。

本批65份原面板已通过 Unity 导入／编译与 `ProjectTools.Validate`，既有公共流程的内置屏 图形 Play Mode 回归通过；新增结果窗仅验证原基础组件与动画资产，不作为完整交互验收或 Windows 发布结论。

本轮带图形 Play Mode 已通过英雄首次领取、永久生命170／HP恢复、周期访问与每日共享额度、冷读，以及通过地图模拟指针采集金币（扣5体力、入包、点位隐藏、原3秒间隔刷新）。隔离事务检查覆盖体力不足回滚、金币／铁矿／时之砂数量倍率、离线刷新、怪物开始去重、失败重试、胜利历战精华与重复回包。已查看英雄面板和实际金币采集画面；奖励条已恢复原叠加材质、CanvasGroup动画和位置，粒子尚未完成，故未声明本功能完整验收或发布。

Map11区域阴影已通过编译、`ProjectTools.Validate`、图形 Play Mode 和实际截图复查：未公开区域使用原 Light2D 颜色／强度，模拟指针激活女神后播放原 `map_region_active_anim`，半径从0增至30，在2.01666665秒关闭对应阴影，冷读按已公开地区恢复。保留原全局白光、0.7倍光照纹理、HDR格式选择、受光排序层16／17／18和原衰减纹理；GLES程序机械转换为HLSL，网格由固定Unity Graphics的 `LightUtility.GenerateShapeMesh`／UTess／Clipper生成。来源与全部输出哈希分别见 `map11-region-lights-source-manifest.json`、`map11-light-meshes-source-manifest.json`、`map11-light-shaders-source-manifest.json`。这项验证覆盖区域地表阴影；未把日夜专属装饰、其他局部灯光或小地图未探索遮罩算作完成，也未发布新的完整功能版本。

## 自定义实现

沿用已确认的本地地图存档、神像最近可站立格传送和英雄／魔女共享每日访问额度。规则细节分别见[森之国](../maps/map-11.md)、[战斗执行](battle-execution.md)；地图奖励沿用户确认方案：原权重选取、原数量乘等级／阶位倍率，小数按概率取整，再应用原属性29加成并向下取整。普通点位按原配置扣体力57×5；命运探寻来源实体须按官方来源规则免体力。刷新沿用户确认的本地方案：保留点位、区域开放、原组间隔与上限，计入离线时间。

山之国七座战斗碑已按当前原关卡配置接入区域等级／阶位、怪物+7及原属性重算；本地胜利奖励、一次完成和胜场同事务，败北保留次数，支持结算重试与档案到地图的冷启动恢复。原专用窗口、NPC队伍及真实输入／显示／存档验收仍待完成，详见[山之国](../maps/map-12.md)。当前已追加战斗碑四个原窗口／图标依赖、全50种奖品图标、原窗口方法和64位状态绑定，源资产为72份／943张；另迁入原确认窗回调、帧尾自动对齐和计时锁，原生字段／64位锁状态已绑定；新增原生导入及双尺寸指针夹具尚未执行，地图宿主与 NPC 队伍仍待接线。另已恢复四份原循环／结束演出，逐字段核对84节点／64粒子／72渲染器并显式绑定原生目录；原周期状态代码已迁入，地图挂点／事件宿主与实际演出仍待接通验证，自动验收继续暂停。

山之国另已恢复两份原实体的309包转换输入及12条原托管引用，核对四节点层级和挂点 PPtr；两张原碑精灵通过同源对象字节校验并绑定本地精灵目录。两份正式原生实体、七点位目录、四视图及独立原演出池宿主已迁入，显式资源引用／逆向补丁核对、8项独立编译和静态检查通过；正式地图工厂／事件／可见性与运行尚未接通，具体来源与未完成范围仍见[山之国](../maps/map-12.md)。

## 尚未实现

- 英雄雕像原激活动作与三种周期效果的角色、粒子、音效和 MyPlayable 演出；界面与永久属性事务已有实际指针验证，完整表现未完成。
- [命运果实与命运探寻](world-exploration.md)的全部实体交互与演出。果树生产继续复用已有 [果树服务](fruit-tree.md)，探寻费用、候选／保底、动态实体、历史和原窗口已部分验证；遗失女神结算已接入规则层，原领奖界面与地图入口仍待完成。
- 地图体力怪物的实际战斗完整操作和冷读回归、铁矿／时之砂地图点击、奖励粒子及全部原生材质；不能仅以服务事务检查替代运行验证。
- Map11原日夜专属场景装饰、局部灯光和小地图未探索遮罩；区域地表阴影与原渐退已接通，但 `ActiveRegionTilemapDisplayHelper`／`ActiveRegionTileMapDisplayHandler` 及其他地图原载荷仍待接入。
- 小地图的公共地区资源提供器、其余地图实体图标与详情、未完成的官方表现和各图完整路线；共享底层不自动完成单张地图。

共享原演出核心见 `Runtime/Official/StoryPlayables/` 与 `scripts/prepare_unity_story_playable_runtime.py`：原 `MyPlayable` 状态机、双阶段中心、序列、触发器、有序容器与粒子淡入淡出保留；离线显式注入及调用方帧阶段替代原服务注册。隔离 Unity 完整校验、Play Mode 调度／状态／异常恢复及 3 项迁移回归通过，视觉和输入未覆盖；正式宿主、原 prefab、动画／骨骼、Timeline、相机和音频仍待绑定。来源与验证范围见[龙之国](../maps/map-14.md)，不据此开放完整地图。

龙之国 BGM、直接对白与原剧情／实体 Timeline 音频的当前来源、延迟／静音动作和验证范围见[龙之国](../maps/map-14.md)。418 个 PCM 素材已在内置屏 Play Mode 逐采样比较并实际输出，原实体 Timeline 的分段咒文语音及四条气泡已定位；正式事件宿主、总线 DSP／RTPC、气泡界面和完整剧情接线仍待实现，不能据此认定主线模块已完成。

共享原声音及 Timeline 驱动已恢复至 `Runtime/Official/StorySounds/`：保留官方循环、停止、事件目标和手动时钟绑定；内置屏隔离 Play Mode 调度验证通过。原事件 ID 桥接复用官方 SDK Post，完整原声总线 DSP／RTPC、原演出 prefab／气泡与正式剧情入口仍待接通，范围见[龙之国](../maps/map-14.md)。

共享原 Animator 参数执行组件位于 `Runtime/Official/StoryAnimation/`，保留原缓冲、来源分层、暂停和淡出／停止恢复；隔离原生 Animator Play Mode 已验证生命周期及复用。来源和范围见[龙之国](../maps/map-14.md)；该检查尚不覆盖原骨骼／角色画面、控制器资源绑定和正式地图业务。

共享原剧情外部节点注册与 Cinemachine 轨道空绑定逻辑位于 `Runtime/Official/StoryNodes/`，两种异步等待保留官方阶段、令牌与取消行为，职业 UI 注册可共用管理器；隔离 Play Mode 注册／取消／轨道绑定验证通过。原 Timeline 场景和正式地图流程仍未完成，见[龙之国](../maps/map-14.md)。
