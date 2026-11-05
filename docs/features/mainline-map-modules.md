# 主线地图公共模块

[总索引](../README.md)

状态：未完成（已提取共享宿主、原世界配置、部分地图网格、正式存档快照读取及离线小地图数据源组合入口，客户端依赖、全新角色初始化、七图原客户端实体启动与混合地区身份已验证；各玩法的原表现与正式宿主接线仍待完成）

## 功能简介

`MainlineSharedWorld` 统一持有原程序集解析、配置管理器、地图数据和原容器根，调用者只传入程序集目录、地图编号与容器身份。加载时在每次让出帧前完成同步解析任务，全部数据就绪后，地形调用自动启动原根，带地区查询的新存档调用先完成实体准备、由宿主绑定上下文后显式启动；加载异常、枚举器取消及地图退出按根、数据、配置、程序集的顺序释放。重复加载和关闭后恢复加载会被拒绝，原全局解析器、文件加载器与 formatter 作用域恢复给原所有者。龙之国正式地形入口已使用此模块及当前884地图数据目录，原生地形和原111区配置继续保持各自来源。七图原根验证现均走此公共生命周期，另核对缺失地图配置、加载取消、重复启动和关闭后恢复。龙之国实际地形宿主的加载、私有／公开地区刷新、分区移动、重入及中断清理通过带图形 Play Mode，入口与重挂画面已查看；公共小地图全屏、筛选、HUD、白雾和区域时间回归及主工程编译／`ProjectTools.Validate` 通过。正式宿主的实体创建／新存档状态恢复、激活检查和正式图标接线仍未完成。

七图原始共享数据现在由同一加载器的地图编号、资源目录和容器表参数选择，既有调用的默认龙之国绑定保留。`NativeMap14SharedData`／`NativeMap14SharedRoot` 已可读取初始岛、森之国、山之国、泽之国、龙之国、通天塔和鲸背岛的原 EC、导航、MapInfoWrap、根组件及公共实体／动作模板。新资源保留所选884原字节与独立来源；共享 Console IL、formatter和 GameSettings 仍保留历史871身份，不声明整个程序集跨版本等价。隔离图形 Play Mode 已逐图启动原根，核对259,150个原始网格、11,174份放置模板的组件顺序／新实例／共享配置和对应默认实体模板查询。逐组件编码检查在原 `Info.Init` 前进行，避免把原 `AreaInfo.PosSet` 初始化缓存误判为字段变化；按所选官方 `SubStageStateInfoFormatter.Deserialize` 保留其读取但不赋回 `Select` 的行为，其余所选组件载荷重新编码与输入逐字节一致。主工程编译、`ProjectTools.Validate` 与原全屏／筛选／单分线HUD／白雾／区域时间回归通过，实际画面已查看。原 EC 数据加载本身不提供服务器初始状态或刷新执行器；全新存档的公共实体初始化按下述已确认方案接入，正式宿主恢复、完整标记和 Windows Player 接线仍未完成。来源与显式资源地址见[七图共享数据凭据](../../assets/reference/official-map/mainline-shared-world-data-source-manifest.json)。

小地图资源点宿主显式引用区域说明和原标签接口程序集，保证组合数据源所用类型可在正式世界程序集编译。

公共共享世界现于创建时加载所选884的三份原始 Luban 表：`EC/entity_grid_mover`、`Map/map_logic_entity_tag` 和 `map_entity_condition_show`。`MainlineSharedWorldConfiguration` 将它们注册到同一原 `Parsers` 作用域，保留原阶段解析、`PostParse` 标签索引和原 `GridMoverComponentInfo.MoveMask`／`Walkable` 的地形与服务器覆盖分支；未向掩码缓存写入替代值。各图无需增加表绑定参数。输入载荷、原源码、历史871共享IL依赖与显式资源地址分别记录于[公共世界配置来源](../../assets/reference/official-map/mainline-shared-world-configuration-source-manifest.json)。`OfficialMainlineWorldStartupAcceptance.RunConfiguration` 的内置屏 D3D11 Play Mode 已覆盖七图×私有／公开状态、12,789个准备实体（含各次根实体）及79个移动实体，对照官方地形表核对掩码和全部地形的覆盖分支，逐项核对原标签表的索引。该入口仅验证配置和启动前实体，不验证 NPC 显隐条件执行或完整客户端启动；完整客户端启动改由下述 `LoadClient` 和显式原玩家／地图快照验证。 同次回归通过七图原根自动启动；主工程 Unity 导入／编译、`ProjectTools.Validate` 与静态元数据检查（127,833个GUID）通过。本次未改动界面或美术，未执行交互画面与 Windows Player 验证。

`MainlineSharedWorld.LoadClient(regionStatus)` 在同一公共世界内加载角色配置和七张原地图客户端表，恢复原 EC 加载器后，再读取完整模板并执行原实体扩展 `PostParse`。地图信息查询复用同一已验证的原 `MapInfoWrap`，原 `MapInfoManager`／`MapRegionEntityInfoManager` 使用独立缓存，退出时恢复此前所有者的字段引用。18份额外官方EC记录仅补齐原 `PostParse` 的模板依赖，实体出生仍由本图放置和原出生条件决定。宿主随后绑定真实原玩家及 `PlayerMap`，再显式 `Start`。角色与地图表的次序、原查询作用域和取消清理均由此入口管理。

历史871的 `MapWorldInfo` 比当前884少两个尾字段、`EntityExtendParameterInfo` 少一个尾字段。所选读取器通过派生原 DTO 保留 `SupportFastClear`、`EnterNavigateKey`、`IgnoreFastClear`，复用当前官方表读取循环和原解析器／`PostParse`；生成时直接反编译所交付IL，逐构造函数核对继承前缀。当前官方与所交付 `Luban.ByteBuf.ReadString` 逐方法一致，零长度仍返回 `null`。原共享程序集与官方表字节均保持原身份，不声明整份程序集跨版本等价。原生源码位于 `Runtime/Official/World/SelectedMapConfiguration/`，以明确编译符号单独生成对真实共享ABI的程序集，并按资源地址／哈希加载；来源、编译输入和绑定见[地图客户端配置来源](../../assets/reference/official-map/mainline-shared-map-configuration-source-manifest.json)。

`OfficialMainlineWorldStartupAcceptance.RunClientMaps`（默认 `Run` 同一路径）的内置屏 D3D11 Play Mode 已通过七图×私有／公开的14组状态、12,789个实际原实体的 `Awake`／`Start` 与退出，包含458次营地怪物列表的独立快照和14,574项当前新增字段对照；原错误日志计数为零。加载中途取消后，原属性文件读取器已释放，地图元数据未改变；逐图退出均恢复原查询作用域。该入口的玩家及区域状态为显式均一夹具，不覆盖正式存档投影、实体美术、鼠标交互、HUD或 Windows Player；混合地区验证见下段。本批主工程 Unity 导入／编译、`ProjectTools.Validate` 与静态元数据检查（127,985个GUID）通过；完整公共模块仍未完成。

`MainlineSharedWorld.LoadClient(regionStatus, publicContainerId)` 另提供完整本地客户端作用域；调用者在加载后将投影的原 `Player` 交给 `StartClient(player)`，模块负责原依赖注入、公式绑定、`Player.ClientInit`、`PlayerMap`、原容器事件与退出时的 `ClientDestory`／作用域恢复。区域查询按官方连续索引只读取一次，实体创建与原玩家使用同一份快照。依据原 `MsgMapStart` 的合并容器消费方式，主容器身份用于私有地图（仅公开地图时用于公开身份），混合地图的公开实体使用独立 `publicContainerId`；原双向状态登记、运行时ID和LoID查询保留这一区别，不能将两种状态登记成同一个ID。公共分线沿已确认方案只登记一条本地分线。生成工具直接反编译交付IL，核对八个 `PlayerMap` 身份／查询方法及原进入消息的身份分支与所选源码一致。

`OfficialMainlineWorldStartupAcceptance.RunClientContext` 已在内置屏 D3D11 Play Mode 通过七图、6,971个实体的原启动／退出及6,964个放置实体的直接和地区LoID查询；森／山／泽／龙四图使用私有／公开交错地区，其余图按原配置的默认状态初始化。逐图核对原双向容器身份、混合地图判断、单分线序号、原区域快照只读一次、角色／地图元数据／应用作用域恢复；加载中途取消后，公式、注入作用域和原属性读取器均已恢复或释放。本批主工程 Unity 导入／编译、`ProjectTools.Validate`、全量静态元数据检查（127,987个GUID）及原14组客户端状态回归通过。此项仍使用显式新角色与地区夹具，正式存档投影、正式表现宿主与交互组合尚未完成。

正式宿主的放置实体输入由 `scripts/mainline_entity_catalog.py` 公共实现转换官方 EC 的阶段、激活、锁定、奖励、换图、战斗绑定与周期组，泽之国、龙之国和鲸背岛的生成入口均调用此实现。来源清单路径可由调用方传入，保留各图独立版本和哈希校验。读取前核对地图身份、定义／组件来源哈希及完整放置集合，转换不修改借用的组件字典。龙之国旧目录统一阶段0和空锁定／绑定的输入已替换为原配置；来源、具体数据与运行验证范围见[龙之国](../maps/map-14.md)和[鲸背岛](../maps/map-16.md)。这仍是放置配置，不定义服务器实体快照、出生或刷新算法。

主线地图通过 `OfficialMainlineMapRuntime.Begin(mapClassId, …)` 使用同一套运行时。`OfficialMainlineMapProfile` 登记 Map10–16；各图数据、表现和游玩完成范围以所属地图页为准，登记或可进入不表示整图完成。鲸背岛 Map16 已登记原版本、网格／身体／白雾、交互和战斗／剧情来源配置，但 `IsRuntimeEntryReady=false`，不能打开正式地图入口。地图资源、区域条件、剧情、实体和进度仍按地图 ID 隔离。新增地图须先迁移其官方数据与专属流程，再登记配置，不能仅替换地图 ID 后宣称地图完成。

实体资源寻址已有通用官方输入解析器 [`prepare_mainline_entity_prefab_roots.py`](../../scripts/prepare_mainline_entity_prefab_roots.py)：它从原地图放置、导航角色类别、EC `index.g.txt` 和展示表导出真实 prefab 地址，按选定 APK／CDN bundle 核验 GameObject 身份并闭合CAB依赖；不把显示用的 `PenPath` 当资源路径，也不声明 Android 输入可直接作为 Windows 运行资源。后续 AssetRipper 输出由 [`verify_mainline_entity_native_exports.py`](../../scripts/verify_mainline_entity_native_exports.py) 按根清单逐项核对 prefab 与 `.meta` 输出哈希；此步骤只锁定转换输入，不替代 Windows 脚本／资源绑定。Map16 已通过两步输入解析，实体视觉的 Windows 转换和宿主接线仍未完成，见[鲸背岛](../maps/map-16.md)。

原生实体 prefab 实例化使用 `IMainlineEntityPrefabProvider` 与 `MainlineEntityPrefabVisualFactory` 公共契约；地图 profile 配置 provider 资源地址和来源 manifest，宿主统一校验地图 ID／来源后按实体类创建视觉。Map14 已通过该接口接入现有主线宿主，目录序列化与官方路径查找保持原样。其他地图仍需按各自原生资源与 prefab 来源实现 provider，并完成显式资源绑定和运行验证；该接口不代表这些地图实体已迁移。

该接口与 profile 配置所属的 `Zjcs.Offline.World.NativeMap14EntityAssets`、`Zjcs.Presentation.World` 程序集已在正式 Unity 工程编译通过，`ProjectTools.Validate` 已通过。Map14 provider 的 Play Mode 尚未覆盖此次 factory/profile 重构；此前旧版 provider 的运行验证不作为该重构的交互证明。

小地图区域地面由公共 `MainlineMiniMapRegions` 管理原 prefab 实例、原管理器的状态／范围事件及资源生命周期。各图提供 `MainlineMiniMapRegionAssets` 与 `IMainlineMiniMapRegionState`；模块直接订阅原可见范围事件，替换处理器时切换订阅，停用／销毁时退订；普通状态刷新不会重置原十秒回收期限。所选 `884_156692` 的 Map10／11／12／13／14／16 已分别转换5／77／99／106／111／43份原区域 Tilemap，复用同一实现；Map15 原配置没有独立小地图 prefab，不生成空目录。当前原生资源与各图历史来源独立记录，不混接不同版本的区域身份；完整原小地图根已提供公共实例绑定；白雾、标记及正式宿主的完整状态接线仍未完成。各图验证范围见[初始岛](../maps/map-10.md)、[森之国](../maps/map-11.md)、[山之国](../maps/map-12.md)、[泽之国](../maps/map-13.md)、[龙之国](../maps/map-14.md)和[鲸背岛](../maps/map-16.md)，来源见[公共小地图区域模块](../../assets/reference/official-map/mainline-minimap-regions-source-manifest.json)。

原生小地图的地形、标记管理器与标记数据工厂共用 `MainlineMiniMapWorldData` 的地图／区域／坐标／实体查询；461条官方地区标签、192条剧情实体标签及9条传送目标从所选 `884_156692` 原配置读取，11,148个默认实体位置逐项与各图安装数据核对。标签沿用原 `MapLogicEntityTagInfoParser` 的按地图和逻辑ID查询，不能用实体出生组件的 `Tag` 字段替代；默认位置返回独立DTO，供本地状态投影而不污染其他实例。实时实体、角色、阶段与操作仍由地图宿主提供，正式入口完整接线尚未完成。

实时实体通过必需的 `MainlineMiniMapBindings.Entities` 注入一个 `IMainlineMiniMapEntities`。公共根的区域、占位格、生命周期事件及标记管理器的实体ID查询均使用同一实例，不能分别接入身份不同的数据源。所选884原 `MiniMapMarkManager.AddListener`／`RemoveListener` 分别监听 `MeManager.Instance.MapData` 的容器事件和 `MeManager.MainPlayer.OnMapStatusUpdate`，公共绑定保持这两个来源。标记管理器使用的三类容器事件委托直接保留此实体源的身份；玩家区域状态变化则从 `State.OnMapStatusUpdate` 交给每个根各自的事件作用域，避免浏览多个小地图时在共用实体源上重复广播。工厂创建失败及模块释放都会退订本根的玩家监听；隔离图形Play Mode已同时创建两个完整原根，验证区域前后状态只送达各根一次、共用实体源不重复广播、独立释放及重复释放保留宿主监听，并验证绑定后缺失必需引用的工厂失败路径恢复原订阅数量。独立离线宿主可用 `MainlineMiniMapEntityIndex` 维护实体身体、区域与占位索引；该索引先更新自身状态再通知消费者，隔离 GUI Play Mode 已核对六图全部11,148个放置身份及显式双格身体、重叠、跨区移动、替换和重复移除。这不替代原 Common 容器的 `Awake`／`Start`／`End` 与区域代理时序。

借用原 Common 容器的宿主使用 `MainlineMiniMapSharedWorld`，要求容器已启动且原区域代理与网格已安装。它直接读取原 `AreaAgentComponent`／`GridGroundComponent`，转发原添加、移除、替换事件，并在原销毁事件后释放自己的实体适配器；不再维护第二份区域／占位索引。原添加通知发生在实体 `Awake`／`Start` 前，移除通知发生在 `End` 清除占位前。原区域代理排除网格移动组件，静态实体移动不自动更新区域成员，替换事件才重新登记；这些行为均由原对象保留。原 `ReplaceEntity` 原地修改源实体及ID，因此适配器、阶段对象与现有标记监听保持身份；只有原对象被替换才创建新适配器。释放此小地图作用域仅退订自己的四个容器监听及阶段监听，不结束地图或销毁借用视图。正式宿主的创建／恢复与原始标记接线仍待完成。隔离图形 Play Mode 已用64格、两个区域的显式事件夹具实际执行原容器启动、添加、移动、原地替换、重复移除及销毁，核对原区域排除移动组件、通知前后占位差异、旧ID移除／新ID查询、阶段事件继续送达及借用视图生命周期。夹具移动掩码仅为绕开无关全局表的缓存输入，不覆盖实际地图寻路或行走规则；未将这个小型夹具作为整图渲染证明。

原始实体通过 `Runtime/Offline/World/MainlineMiniMapEntities/MainlineMiniMapSharedEntity` 提供小地图所需的身体、坐标换算、阶段、传送目标、世界入口、传送点、移动及归属读取。适配器借用已有 `NativeMap14SharedViewEntity`；标签查询每次通过宿主注入的原运行时ID→放置ID映射，沿用 `MapEntityExtra.GetEntityTag` 的先映射后查表顺序；缺失映射保留 `MapLoEntityIdManager.GetLoEntIdByEntId` 的 `-1`，不使用运行时ID补查。公共容器将同一映射传给其所有实体适配器，不维护第二份映射。阶段可交互直接调用原 `WorldStageComponent.IsActive()`，保留有效状态与已注册激活检查，不能以显示激活或阶段编号代替。原阶段事件保留前状态和参数身份；释放适配器仅退订自己的监听，不销毁宿主实体或视图。没有对应原组件时不构造替代组件；原地替换保留适配器身份。本模块仍复用分别记录的 `871_156598` 原始 Console IL 与四种英雄雕像客户端 prefab；已直接反编译所交付 IL，逐方法核对 `StateEqual`、三参数 `UpdateState`、`GetCurStateInfo` 和 `IsActive` 与所选884源码一致；交付 IL 的 `MapLoEntityIdManager` 反编译全文也与所选884源码一致，不声明整份程序集跨版本等价。原容器生命周期适配已有实现；正式地图的实体创建／恢复、激活检查注册、宿主组合接线及实际图标交互仍未完成。

该实体适配在隔离 GUI Play Mode 中使用四种原英雄 prefab 与各自真实放置身份，使用与放置ID不同的运行时ID及原 `MapLoEntityIdManager`，核对每次查表的真实地图／放置ID、未映射ID撞上有效标签放置ID时仍返回空标签、绑定变化即时生效和清空映射；另验证双格身体、原标记中心点、无效阶段、完成状态与可交互条件的区别、原激活检查、阶段事件前状态／参数身份、占位索引及重复释放。传送目标、世界入口、传送点和移动使用显式添加的原 Common 组件夹具，标签冲突使用额外的真实带标签放置数据，不据此宣称这些组件或标签属于英雄雕像原配置；归属保留原空值、类型和64位身份。原窗口／筛选／HUD／白雾／时间回归通过，已查看运行截图。既有英雄 prefab JSON 恢复凭据记录的 LF 字节，并通过 `.gitattributes` 保留；原哈希未改变。六图完整小地图根与共享HUD／渲染器／窗口／标记已用当前转换器在隔离目录重建，全部输出哈希与原凭据一致，来源哈希检查三项、全工程静态检查、主工程编译与 `ProjectTools.Validate` 通过。此验证不包含正式实体图标点击或 Windows Player。

共享查询已在隔离 Play Mode 核对六图258,400格、441个区域与全部默认实体，并通过原传送说明参数、实例数据隔离及原管理器／数据工厂绑定检查。原全屏、缩放、筛选、单分线 HUD、实时白雾与鲸背岛时间标签回归使用模拟鼠标；截图入口禁用编辑器异步着色器编译，并拒绝大面积青色待编译覆盖，已查看正确渲染的窗口与筛选截图。本次静态元数据、来源哈希校验、主工程编译、`ProjectTools.Validate`、隔离工程编译和图形Play Mode通过。窗口回归在原720×1280安全区尺寸下运行，模拟鼠标验证全屏、缩放、筛选、关闭及重开；全屏、筛选与单分线HUD实际截图已查看。正式共享宿主的模拟鼠标回归覆盖移动、女神与英雄激活／每日访问、资源消耗／刷新、命运探寻和存档冷读。正式原生全屏入口与 Windows Player 的完整组合仍待验证。

`MainlineMapMiniMapSnapshots.CreateOfflineDataSource` 将当前角色槽位的只读地图快照与原离线响应消费接口组合，避免调用方自行实现跨图实体筛选或状态恢复。已访问地图只提供本地探索过的实体及公开地区实体；隐藏／激活和图标状态从最新本地存档投影，读取不装载地图场景、不推进存档。地图外小地图调用与 Map13 正式地图回归现走同一组合入口；`OfficialMainlineMapRuntime` 在原小地图根激活前注入该数据源，保留原协调器当前图不请求、跨图请求的分支。完整白雾、标记状态、HUD输入与Player验证仍待完成。

`OfficialMainlineMapProfile.LocalChannelCount` 是单机分线配置；当前已迁移地图均固定为一条本地分线。公共 HUD 按原 `PublicMapBranchLineBtn.IsShowBtn` 的数量阈值隐藏分线按钮，同时不改变地图的私有／公开地区状态或其他地图玩法。未接入原生小地图 HUD 的地图仍须在正式宿主接线时消费同一配置。

原 `SlicedFilledRenderService` 的脏网格刷新由 `OfficialMainlineMapRuntime.LateUpdate` 驱动，地图实体 prefab 可共用同一进度组件生命周期；泽之国的独立实体宿主仍保留自身刷新入口。该公共刷新只负责推进已迁移组件，不表示各图原 Sprite、材质或实体 prefab 已完成 Windows 绑定。

鲸背岛56份原实体视图使用的旋转进度 Sprite／World Atlas 已按所选884版原 GUID、Sprite网格／UV和图集坐标安装；PNG导入设置固定为2D未压缩。实际 Unity 导入和原 prefab GPU 画面仍未验证，见[鲸背岛实体进度图资源清单](../maps/map-16.md)。

## 原始官方实现

当前 `884_156692` 的完整原 `MiniMap` 根代码已迁入内部 `MainlineMiniMapRoot`，保留角色／全图视野处理器的缓存和订阅切换、原全屏显隐调用顺序、相机定位／边界裁剪、标记查找及关闭事件；配置、状态与资源从既有本地端口接入。公共 `MainlineMiniMap.Create` 通过具体脚本 `MainlineNativeMiniMapRoot` 绑定完整原 prefab，在激活前注入状态、角色、离线响应、资源、标记、点击和白雾坐标端口；实例统一管理显隐、全屏、关闭事件、纹理和销毁。初始岛、森之国、山之国、泽之国、龙之国与鲸背岛使用同一入口及各自配置，正式地图宿主／HUD接线仍未完成。离线标记初始化另按当前官方源码补入缺实体字典、容器实体及地区实体列表的分支；原64组条件、64次回调、24组归属条件及新增缺数据路径已在 Play Mode 通过。历史871源码凭据保留，只有 `OfflineMiniMapMarkManager.Init` 由当前源码覆盖，见[完整根与当前初始化来源](../../assets/reference/official-map/mainline-minimap-root-source-manifest.json)。

原 `OfflineMiniMap` 协调组件已复用同一公共根程序集：非当前地图且地图ID大于零时，先隐藏旧数据并请求本地数据端口；空响应／错误码保留隐藏状态，成功时按原顺序显示内容、初始化离线白雾、初始化离线标记。重新启用当前地图不请求离线数据，重新启用非当前地图重新请求。所选884的 `StateSwitcher` 与历史原输入哈希一致，继续复用已迁移实现；原仅供编辑器使用的 `RequireComponent(MiniMap)` 因公共根为抽象类而不迁入，完整原生实例绑定须在激活前校验根组件。`prepare_mainline_minimap_offline_validation.py` 准备隔离工程；Play Mode 使用已安装的泽之国原图层、57,600格导航、106地区、原可见数据编解码及隔离保存文件，验证成功／空响应／错误响应、切图重新启用、先白雾后标记、12格动态解锁、1,326格公开地区填充和四种雾效shader变体；已查看实时与离线两张实际渲染截图。此处验证的是协调组件与图层，正式地图存档到完整响应的投影及HUD／输入／Player仍未接通；完整原根实例验证见下文。

完整原 prefab 由 `prepare_mainline_minimap_native_root.py` 恢复，保留各图原序列化对象文档及原接缝线、主／支地区说明 prefab 引用。原 Tile 的 Sprite、颜色、矩阵、实例对象、标志和碰撞字段从实际对象恢复，`MapAreaTile.Area` 与原完整类型声明一并迁移。主根的16种应用脚本逐文件核对当前原输入与已有迁移凭据，仅离线标记 Init 的覆盖范围按当前根凭据另记；不同来源版本不作整版等价处理。完整根及各图已安装验证导航分开核对，参数如下：

| 地图 | 原根对象文档 | 验证导航 | 地区 | 原配置 | 来源 |
| --- | ---: | --- | ---: | ---: | --- |
| 初始岛 | 87 | 60×60 | 5 | 1 | [完整根](../../assets/reference/official-map/map10-current-minimap-root-source-manifest.json) |
| 森之国 | 531 | 240×180 | 77 | 2 | [完整根](../../assets/reference/official-map/map11-current-minimap-root-source-manifest.json) |
| 山之国 | 672 | 280×210 | 99 | 2 | [完整根](../../assets/reference/official-map/map12-current-minimap-root-source-manifest.json) |
| 泽之国 | 747 | 240×240 | 106 | 2 | [完整根](../../assets/reference/official-map/map13-current-minimap-root-source-manifest.json) |
| 龙之国 | 732 | 280×280 | 111 | 2 | [完整根](../../assets/reference/official-map/map14-current-minimap-root-source-manifest.json) |
| 鲸背岛 | 315 | 140×120 | 43 | 4 | [完整根](../../assets/reference/official-map/map16-current-minimap-root-source-manifest.json) |

隔离 GUI Play Mode 已验证六张完整原 prefab 的全部 MonoScript、托管引用类型和序列化对象引用。泽之国使用原57,600格／106地区夹具，通过延迟离线响应、原白雾及标记初始化、全屏视野切换与缓存恢复、关闭事件、重新启用、订阅释放；已查看温泉乡原层级实际渲染截图。原尺寸的小地图纹理复用同一实例，公共工厂销毁实例时先停用再释放纹理，重复销毁不重复处理。初始岛、森之国、山之国、龙之国与鲸背岛分别使用本图已安装的导航、地区和原配置，通过同一工厂验证原根加载、地区插槽数量、离线请求保持隐藏和销毁释放；未给它们套用泽之国离线响应。上述原根夹具的实体查询仍由显式夹具提供，资源上下文已改为当前资源目录；关闭事件直接调用原入口，HUD和全屏窗口另按下文验证。真实实体图标、采集资源交互、正式地图存档响应投影或Windows Player仍未完成。主工程 Unity 导入／编译、`ProjectTools.Validate` 与静态元数据检查通过。

公共小地图 HUD 另迁入当前原 `MiniMapMaskedUI`、`AreaInfoUI`、`PublicMapBranchLineBtn` 完整路径及 `MultiLanguageTMPControler`／语言组配置，原局部坐标、区域名称、原探索度公式、延迟弹窗回调取当前地图、公开地区与频道数显隐、战斗错误4131及状态配色分支均保留。`MainlineMiniMap.AttachHud` 在原父插槽内实例化完整79对象的 `MiniMapMaskedUI.prefab`，激活前绑定地图／角色／语言／字体／窗口／声音；与主实例共同显隐并先销毁 HUD，再释放纹理。原按钮声音 `Main_Cursor_Normal` 从实际 Wwise 事件、bank和媒体解码，不用替代声音。资源、脚本与逐文件原输入等价凭据见[原 HUD 代码](../../assets/reference/official-map/mainline-minimap-hud-code-source-manifest.json)和[原生 HUD](../../assets/reference/official-map/mainline-current-minimap-hud-source-manifest.json)。

隔离 GUI Play Mode 使用泽之国当前地图实例，通过原角色渲染器将真实已探索地块绘入原尺寸 RenderTexture，再由原圆形 HUD 显示；已查看实际截图。模拟鼠标经原 GraphicRaycaster／Button 验证打开全屏页的请求及延迟初始化地图ID、战斗中禁止切线和非战斗时请求分线窗口；多分线检查显式使用原按钮的兼容性夹具，窗口内容不接入单机入口。真实导航地区查询、坐标事件、探索度更新、单频道隐藏／多频道显示、当前状态配色、模块显隐／销毁和全部订阅释放通过。单机分线决定集中在[世界地图](world-map.md#自定义实现)；女神头像地区通知、正式 WorldCanvas 插槽、全屏窗口正式接线、正式存档状态及 Player 接线仍待完成。

主线地图共享原 WorldCanvas／WorldStage 界面、地区条件、导航、交互、剧情、机关、雕像、小地图和进度规则；单图放置及差异由地图页维护。

全屏小地图另迁入所选884的完整 `MiniMapFullScreenPanel`、页签、主角／道具方向指引、原几何计算和关闭按钮，保留原已访问地图／切换组枚举、半秒页签冷却、异步请求版本丢弃、当前图复用、相机与滑条同步、筛选刷新及禁用恢复顺序。`MainlineMiniMap.OpenFullScreen` 在激活前绑定原窗口依赖，实例关闭、停用和销毁统一释放窗口；状态、头像和浏览其他地图仍由所属宿主适配。原窗口294个对象、两个筛选条目的15／31个对象及完整依赖由 `prepare_mainline_minimap_native_panel.py` 显式恢复；原按钮控制器的三个压缩常量缩放通道按路径哈希、浮点字节和循环参数逐项核对导出曲线。373条地图和201条标志记录按原构造器解码，只投影当前窗口消费字段；97个动态图标覆盖当前六图切换组及原有筛选条件，不为原表未选中的废弃记录制造替代素材。来源见[全屏原代码](../../assets/reference/official-map/mainline-minimap-panel-code-source-manifest.json)与[全屏原生资源](../../assets/reference/official-map/mainline-current-minimap-panel-source-manifest.json)。原 WorldCanvas 的 CanvasScaler 从当前实际对象提取，保留1080×1920参考尺寸及原缩放模式。正式 WorldCanvas／存档／跨图实体／头像与 Windows Player 接线仍未完成。

原 `MapRootSwitchMapSupport.CreateMiniMap` 还将当前图绑定到地图插槽共用的 `MiniMapNonMaskableManager`；此引用由公共工厂绑定，完整16对象的当前原渲染器由 `prepare_mainline_minimap_native_renderer.py` 恢复，来源见[共享渲染器](../../assets/reference/official-map/mainline-current-minimap-renderer-source-manifest.json)。原 Quad、内置材质／shader选择器、相机、排序、网格和材质引用保留；内置资源按GUID与fileID组合记录，不能按GUID合并成单一对象。窗口通过原 `PopupWindow.Show`／即时隐藏生命周期开启和释放。

隔离 GUI Play Mode 使用720×1280实际 Game View、当前原1080×1920缩放、原URP叠加相机和界面画布，通过公共模块打开完整窗口。原全屏相机实际绘出已探索地块、地区边线和说明，帧末截图已查看；原三列筛选条目完整显示。模拟鼠标经原射线／按钮验证相机滑条、打开筛选、全选写入端口、关闭恢复圆形HUD和重开；窗口、筛选钩子、网格位置订阅与纹理释放通过。资源板加载使用既有871原目录及本地适配器，保持其来源独立；当前夹具只登记泽之国为已访问，不决定跨图实体快照策略。筛选写入、主角资料／头像和世界HUD显隐由显式夹具提供，该窗口夹具未接通正式地图快照、实际头像、道具指引或完整存档宿主。

公共小地图工厂可通过 `MainlineMiniMapBindings.Visibility` 直接借用世界正在运行的原 `GridVisibilityAgentComponent`。工厂先核对当前地图、源实体地图ID和导航尺寸，再于原根激活前创建事件适配；关闭／销毁先禁用原遮罩，再释放订阅，创建失败也释放订阅。源组件及探索数据继续由世界拥有，存档回滚更换组件实例时须重建小地图，避免继续读取旧实例或另一张地图。

隔离 GUI Play Mode 已使用现有 `OfflineMapGridVisibility`、871原地图／身体配置和同一公共工厂，验证角色移动揭格、温泉乡区域解锁经实时事件移除原 `MiniMapMaskManager` 遮罩格；写入隔离文件后冷读得到相同原RLE字节，独立实例互不串用，小地图释放后世界可继续移动。圆形HUD、全屏窗口及筛选模拟点击回归通过，实际帧末画面已查看。此项仍使用显式地图宿主夹具，正式入口的状态／存档组合接线未完成。原资源输出不变时，`prepare_mainline_minimap_native_root.py --refresh-integration --include-shared` 校验原输入、原输出、官方脚本与MonoScript元数据后，仅更新本地适配源码及绑定哈希；不放宽原资源校验。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

遗失女神8364的原领奖层级另由同一提取入口读取 `defaultpackage_assets_prefabs_ui_map_upgraderesult.bundle`，包括触碰窗、三类结果窗及属性行；原 `ItemIconManagerConfig` 绑定 `ItemIconSkill`，新增394条主动／被动技能图标。完整来源数据现为65份原面板、945张Sprite和1,055条图标记录，保留35条原技能阶位、`MapLostGoddessAwardDialogConfig`、星级品质图、三类原标题及属性行动画。新增客户端代码、对象／bundle真实来源和提取输出哈希见 `unity/Assets/Official/World/Map/11/UI/source-manifest.json`；原生 prefab／动画、图标元数据和转换器的换行归一化哈希见同目录 `native-source-manifest.json`。Unity 导入器已同步层级与图标基础绑定；结果控制器、原关闭交互、地图入口、粒子／泛光／音效仍未完成，不能将提取或导入通过等同于领奖流程已迁移。

原结果窗的 Button 类型、离散启用曲线及各窗动画目标子集已在 Unity 中核对；共享原动画的转换结果按窗口分别保存，不覆盖其他结果窗。新增六份 prefab、十份被引用动画及其稳定元数据随源数据提交，验证范围见所属功能页。

固定 `871_156598` APK 优先提取；英雄、探寻和奖励 UI 的原面板／对象、sprite、动画及返回对象池事件由 `prepare_unity_map11_ui.py` 保留至59份原面板、535张sprite。`map11_support_ui.py` 按原 `ItemIconManagerConfig` 的ID／类型／默认优先级绑定661条本图奖励道具记录，使用原普通、装备、宝具模板；宝具素材取Map11原地区 `DropItems` 并集。名称、品质框、奖励高品质光效、GraphicRaycaster和原数字标题均显式绑定。完整输入、各对象来源、客户端／共享代码哈希及输出见 Map11 `UI/source-manifest.json`。奖励列表 `UIImgAdd` 的 `UI/EffectAdditive` shader 来自 APK `defaultpackage_unityshaders.bundle`（解码包 SHA256 `f6cc2ad49028110a87744a116bc2fd193af9d8e2f2e7e452b7d90c0bc934dabe`），使用既有 GLES→HLSL 转换并保留四种裁剪变体及原材质参数；输出和源码见 Map11 UI 来源清单。

`prepare_mainline_loop_scroll.py` 从固定官方 `LoopScrollRect.Runtime.dll` 完整反编译并恢复9个类，保留原拖动、惯性、网格和回收分支，仅修复C#反编译基类调用及工程命名空间；程序集、逐文件源码与输出哈希见 [循环列表来源](../../assets/reference/official-map/loop-scroll-source-manifest.json)。`prepare_mainline_popup_effects.py` 核对 APK 恢复的ARM64实现，迁移 `TMPColorGradient` 的原顶点计算以及 `ScreenBlur` 原程序；原窗口模糊保留4级下采样、3级上采样、七个权重、screen/13快照、原遮罩颜色及半秒关闭门槛。元数据／原生映像、方法地址／字节哈希、原shader／material及显式加载绑定见 [弹窗表现来源](../../assets/reference/official-map/popup-effects-source-manifest.json)。这些恢复只覆盖当前原窗口使用的分支，原动作、奖励粒子、图标附件和完整实体交互尚未完成；未生成素材快照。

## 完整还原部分

公共剧情交付窗口使用所选 `884_156692` 的 `DialogHandInItemDialog`，资源入口为 `World/Mainline/HandIn`。官方43个弹窗节点、19个道具图标节点、101份组件记录、19张切片、原按钮 controller／五态动画、弹出／关闭动画、灰色材质与原中文文本均显式恢复；新增概率说明区域保留原默认隐藏状态。两条按钮／关闭声音从当前 Wwise 事件链按原增益解码，与已安装官方 PCM 逐字节一致后复用。公共宿主按节点 `HandleItemId`、`NeedHandInWindow` 和原交付配置选择事务及分支，不再限制 Map14。旧版 Map14 资源和单独恢复入口保留独立来源，不将其版本身份改为当前版。转换与绑定见[公共交付窗口来源](../../assets/reference/official-map/mainline-handin-ui-source-manifest.json)和[原生绑定](../../assets/reference/official-map/mainline-handin-ui-bindings-source-manifest.json)。

内置屏带图形 Play Mode 已通过四组已安装配置的原窗口模拟鼠标验证：缺物品禁用、取消、弹窗关闭／销毁中断、原音效播放与释放、按配置数量原子扣除、次数上限、回执幂等、失败回滚和存档冷读；已查看缺物品和绳索足量的实际渲染画面。该夹具直接实例化公共窗口并调用同一存档事务，未覆盖正式 NPC 剧情入口、完整图运行或 Windows Player。官方成功系统提示、图标详情及全局剧情遮罩仍需接线，不能将公共窗口验证视为整图交付完成。

公共区域小地图已在 Unity 2022.3.57f1c2 图形 Play Mode 通过六图441份原 Tilemap 的公共 Interface 检查：地区公开状态、原 Z、范围事件替换／旧事件隔离、缓存复用、原游戏时间十秒回收、释放与重挂；12张区域小地图实际画面已查看。夹具按原 UniTask 的游戏时间计时并等待销毁帧，不以墙钟时间替代；地区／角色状态由夹具提供，未覆盖正式根、白雾／标记、HUD 输入、地图存档或 Windows Player。静态元数据检查与主工程 `ProjectTools.Validate` 均通过。

`Resources/Official/Progression/mainline-tasks.json` 的 `output_sha256` 对照 `TextAsset.text`，Git 固定其工作树换行为 LF，确保 Windows `core.autocrlf=true` 检出仍与清单一致。

公共代码位于 [`Presentation/World/Mainline`](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/)。`OfficialMainlineMapRuntime` 在 `Awake` 为每次入图分别创建 HUD、区域、导航、雕像、体力、探寻、小地图七个模块，负责按原顺序组装、更新和退出；窗口、路线、待交互对象、区域进度、刷新计时、动态实体和渲染目标由所属模块持有。模块实现使用宿主内的私有类型保持封装，不把单张地图或单个角色的可变状态放入全局单例。各图专属剧情、首战引导、机关、音频与实体载荷留在原适配层。

原生地形的公共绑定在独立程序集 `Zjcs.Presentation.World.MainlineNativeGround`：`MainlineWorldLabels` 将原区域描述／战力／多语言标签绑定到地图数据和 `IMainlineWorldLabelState`；`MainlineWorldEnvironment` 对原灯光、环境和分区组件提供同一份区域、进度、位置与揭格服务。状态与进度数组按 Region ID 索引，保留原配置的枚举顺序；不能把 Map14 原表中先76后75的顺序当成状态下标。Map14 分区视图复用这两项服务；`MainlineNativeGround` 为 Map15 整图 prefab 提供非激活实例化、依赖绑定、原 URP 管线切换和退出恢复。角色／镜头资源地址独立于 HUD 是否迁移，避免无 HUD 的地图跳过原模型缩放和镜头参数。此项仍不等于通天塔完整 HUD、楼层与交互已经迁移。来源见[原生地形公共绑定清单](../../assets/reference/official-map/mainline-native-ground-source-manifest.json)。

`MainlineMapLabelContext` 复用 `IMainlineWorldLabelState`，集中提供原 `IMap13LabelServices` 路径的地区状态／探索度事件、原二进制语言／字体绑定和生命周期；泽之国地形宿主已用它替代地图专属标签包装。调用者提供所属版本的原地图数据、颜色／字体、语言文件与本地状态，在原 prefab 激活前绑定，退出时先释放视图再释放模块。模块复制地区行、网格和进度上限，按地区 ID 寻址状态，避免两个地图实例互改配置或时间回调；有时间条件的地图必须注入对应 `IMapRegionTime`。现有 Map14 标签端口保持独立，完整原小地图的正式状态组合仍待接通，不能将此抽取视为各图 HUD 已完成。

该模块的同一公共 Interface 已在隔离 Play Mode 核对泽之国／鲸背岛真实地区数据、乱序行的地区 ID 索引、等级／战力／公开／进度通知顺序、无变化刷新、跨图状态隔离、配置复制、两个实例的独立时钟、准确到期边界、高位阶放行和重复释放。时间状态由夹具提供，此检查不证明鲸背岛原标签文字／画面或正式小地图已接通；原场景渲染范围另见泽之国所属页。

公共宿主通过 `OfficialMainlineMapRuntime.NativeGround.cs` 的 `MainlineNativeGroundState` Adapter 提供角色位置、等级／战力、地区状态、探索点及公共揭格；整图资源地址由地图档案传入。`NativeMap14.cs` 仅保留龙之国分区视图的创建、刷新与释放，消费同一状态 Interface。骨骼地面载荷通过 `MainlineGroundAnimation` 复用项目独立原生绘制端；原素材与具体运行验证范围见[通天塔入口](../maps/map-15.md)。

公共原生地面转换器 `GroundBinding` 以 `GroundSource` 显式接收地图 ID、原 prefab 地址和 bundle；Map15 的原 GUID 保持不变，Map16 采用独立范围。地图定义和角色／镜头／标签数据提取同样按地图 ID 消费实际官方输入，尚未迁移的地图不自动注册。`MainlineNativeGround.UnlockRegion` 保留原环境激活动作与揭格回调顺序，区别于普通状态刷新；女神像的公共地面调用使用该 Interface。两图已通过隔离图形 Play Mode 的同一宿主生命周期夹具，具体范围及缺口见[鲸背岛](../maps/map-16.md)。所用 CustomWater.dll 已与当前所选版本逐字节核对一致，仅此程序集可复用原水面代码凭据，不能据此认定其他历史输入等价。

原地区倒计时通过 `IMapRegionTime` 注入地区开放日期、当前时间和角色位阶，标签消费原刷新状态机及原时间格式；日期规则不散落在地形或 HUD 中。`OfflineCharacterRankRegionTime` 使用现有角色进度存档，标签只绑定自身地区副本和语言服务。已确认的本地日期策略、43区边界／存档检查与原倒计时画面范围集中见[鲸背岛](../maps/map-16.md)，正式地图宿主的接入尚未完成。代码与来源见[地区时间清单](../../assets/reference/official-map/mainline-region-time-source-manifest.json)。

完整原小地图工厂另通过 `MainlineMiniMapBindings.RegionTime` 消费同一地区计时模块，并核对时钟、地区数据与原根地图ID。每个实例复制原地区行和区域列表，地区说明与强度标签读取同一副本；原配置资源不保存窗口回调。原 `MapRegionLimitStatusRefreshHelper` 的时间分支及 `MapRegionLimitStatusRefreshForRankUnlockTime` 状态机已恢复到共享小地图标签路径，保持时间先于等级、到期刷新和取消顺序。原 `ShowRemainTimeInfo` 使用该实例的时钟和文本模块，保留原倒计时计算与格式；正式存档宿主组合仍未接通。

隔离图形 Play Mode 已通过同一工厂核对鲸背岛全部原时间偏移、超过位阶4立即通过、较低位阶继续受限、准确到期边界及两个窗口的时钟／配置隔离。完整当前原地区说明 prefab 正常初始化，实际画面中的原名称与倒计时已查看；推进注入时钟后由原 UniTask 自动解除时间限制，重进会重建计时。混合条件夹具组合本图原时间字段与泽之国一项原等级门槛，验证时间优先、随后切换等级提示及原等级事件解除，不写回地图配置。标签关闭后保留父小地图仍需的边线订阅，整个模块销毁后全部订阅释放；原 HUD／全屏页／筛选模拟点击与实时白雾回归通过。此项使用显式宿主与隔离时钟，未覆盖正式入口、完整鲸背岛小地图或 Windows Player。

入口组件保留原 MonoScript GUID 和 `MovedFrom`；纯 C# 模块沿用原脚本 `.meta`，原 prefab、层级、坐标、动画、数值、执行次序与存档 DTO 保持兼容。`OfficialMainlineUiCatalog` 按当前 `MapProfile.NativeUiRoot` 缓存原 UI 目录，HUD、雕像、奖励列表、探寻与小地图共用它；通用文本不再由女神像持有或加载。

公共英雄配置通过 `heroTemplateIds` 声明本图原模板列表，`OfficialMainlineInteractionCatalog` 校验模板身份、唯一性、顺序、原周期权重及地图／版本；历史四种模板的既有载荷保持兼容。共享转换器接收该列表，拒绝尚未迁移的周期组规则，英雄效果与每日访问仍由同一个雕像模块消费。当前鲸背岛已提取六种原模板，正式地图接线与验证范围见[鲸背岛](../maps/map-16.md)。

| 模块 | 当前代码与复用范围 |
| --- | --- |
| 地图配置与载荷 | `OfficialMainlineMapProfile` 集中已迁移地图的资源根、原生 UI 根、阶段／故事数量及必须存在的区域条件。`Data` 分部保留各图官方模板、故事图和数量校验。 |
| HUD、顶部栏、任务与功能入口 | `OfficialMainlineHudModule` 管理 `OfficialWorldUiRenderer`、HUD 控件、顶部栏、任务、功能入口、头像、快速购买与提示的状态和销毁；原生 prefab 路径从地图配置解析。Map10 现有 UI 迁移缺项继续保留在所属地图页。 |
| 区域开放 | `OfficialMainlineRegionsModule` 持有区域进度与雾层，`OfficialMainlineRegionPolicy` 可独立装载官方 `TbMapRegionInfo`／`TbConditionInfo`。保留原有全探索与剧情次数分支、比较运算、错误拒绝及配置检查；实体显隐、交互锁、小地图和任务消费同一份策略。开放条件不等于已实现雾或公开地区状态转换。 |
| 寻路与移动 | `OfficialMainlineNavigationModule` 持有当前位置、路线、到达回调和待交互目标，负责屏幕点击、实体命中、交互距离、路径跟随和逐格存档；继续调用 `OfficialWorldMapSession`、`OfficialMapPathfinder` 与 `OfficialMapGroundRules`。桌面输入提供器与原鼠标输入共用遮挡、命中和寻路流程。 |
| 女神像与失落的女神像 | `OfficialMainlineStatuesModule` 的 `Goddess.cs` 共用探索条件、原生弹窗、按原阶段顺序激活、回执与传送；地区名称按当前地图 ID 查询。当前接入范围为 Map11，珍宝预览等缺项见[森之国](../maps/map-11.md)。 |
| 英雄／魔女访问及魔女阶段 | `OfflineMainlineProgressSession.Statues` 提取女神激活、全图魔女计数、英雄／魔女每日共享额度、奖励、HP 与待战斗效果事务；同一 `OfficialMainlineStatuesModule` 的 `Witch` 实现保留原书页、预览、激活与周期访问，`HeroStatues` 实现已接入四种英雄雕像原弹窗、410 等级属性、首次激活与周期访问；原角色／粒子演出仍待恢复。 |
| 探索进度 | `OfficialMainlineRegionsModule` 与既有 `OfficialMapProgressAdapter`、`OfficialMapExplorationProgress`、`OfficialMapProgressCalculator` 共用官方实体状态和探索点计算。 |
| 网格可见性与存档 | `OfflineMapGridVisibility` 集中原角色视野、实体身体／营地揭格、区域激活、事件生命周期和 RLE 存档；地图 ID、矩形尺寸、角色模板、视野半径与解锁格通过配置传入。Map13 正式入口及其小地图检查改用这一公共类，原 `map13GridVisibility` JSON 字段和原数据断言保留在 Map13 适配层。其他地图仍须接入各自已核实的官方载荷；该类不承担白雾绘制。 |
| 地图体力点 | `OfficialMainlineStaminaModule` 与 `OfflineMapStaminaService` 共用金币8011、铁矿8021、时之砂8026和怪物5011的扣费、奖励、战斗回执及本地刷新；原区域／组间隔由 `interactions.json` 加载。 |
| 区域阴影 | `OfficialMainlineRegionsModule` 按 `MapProfile.HasRegionFog` 挂载 `OfficialMainlineRegionFog`，资源根与数量断言从当前地图配置传入，不再内置 Map11 路径；当前载荷恢复 Map11 的73份原 LightRegion 层级、Freeform网格、原衰减贴图及地区公开后的半径渐退；地表按原 Multiply 光照计算。其他地图及日夜场景装饰切换尚待接入。 |
| 命运果实与探寻 | `OfflineFruitTreeService` 共用生产、容量、时间推进和收取事务；`OfflineWorldExplorationService` 共用探寻费用、候选／保底、动态实体与历史事务，`OfficialMainlineExplorationModule` 管理原 HUD 入口、动态实体、确认／详情／令牌窗口、遮罩与释放。果树生产服务继续由家园消费同一角色存档，不在地图重建一套生产规则。完整实体交互与演出仍待完成，详见[命运探寻](world-exploration.md)。 |
| 小地图与图标 | `OfficialMainlineMiniMapModule` 管理相机、渲染纹理、圆形／全屏面板、图标、跨图页签和筛选，共用已迁移的原面板、图标、跨图浏览、缩放与档案筛选存储；各图几何继续按 ID 加载。 |
| 交互、剧情、战斗与存档 | `Interactions`、`Story`、`Dialogue`、`Battle`、`Persistence` 分部保留既有执行顺序；Map10 首战引导独立在 `Map10` 分部。森之国机关、动态怪物与音频仍消费其专属载荷。 |

`Modules.cs` 集中模块组装与各自的 UI／渲染目标释放；开发检查通过 `OfficialMainlineMapInspection` 定位所属模块，只用于 Editor／development Player 或显式启用 `ZJCS_MAINLINE_MODULE_VALIDATION` 的回归构建，正式运行流程不依赖反射。`map-{ID}-save.json`、角色进度、回执前缀和 `minimap-filter.json` 的现有格式保持兼容。`Map10Runtime…` 数据 DTO 名称暂保留，避免把运行时代码抽取混入资源／存档结构变更。

官方依据沿用已提取的 `WorldCanvas`、`WorldPanel`、`FunctionEntryDialog`、`MiniMapMaskedUI`、`MiniMapFullScreenPanel`、`GoddessTouchDialog`、`LostGoddessTouchDialog`、`WorldWitchTouchUI`、`WorldMapProgressCondition`、`StoryCountCondition` 以及现有地图导航／进度实现。各图来源与哈希见 Map10、Map11 所属页和原来源清单；动态怪物、战斗场景和时间轴的来源清单及生成器中的运行时代码绑定同步到公共目录，官方输入与素材哈希保持原值。七类模块与原 Git 实现的对应、官方客户端代码、输入／输出 SHA256、四份原页签动画及 prefab 显式绑定见 [mainline-modules-source-manifest.json](../../assets/reference/official-map/mainline-modules-source-manifest.json)。

验证入口为 `Zjcs.Editor.MainlineMapModuleValidation.Run`，使用 `-verify-mainline-modules <隔离输出目录>`；同一检查也可在 Windows development Player 运行。Unity `2022.3.57f1c2` 已通过编译、`ProjectTools.Validate`、主线／雕像事务检查与带图形的 Play Mode 回归。地图移动通过注入屏幕点击走 UI 遮挡、实体命中与寻路；Map11 小地图开关、跨图页签、筛选和女神激活使用原按钮的指针按下／抬起／点击事件，并核对最上层射线命中。检查覆盖区域探索／剧情门槛、Map10／Map11 位置冷读、筛选 JSON 冷读、女神激活恢复、英雄／魔女每日共享额度及待战斗效果恢复。七类模块拆分后已在隔离工程以  重跑该完整流程，并覆盖探寻概率／历史页签、令牌拖动、单次／十连费用、原引导宝箱地图点击免体力领取及冷读恢复。女神地区完成度及打开弹窗由测试设置，未覆盖自然探索到 100% 或从地图点击雕像的完整路线。

已实际查看 Map10 地图、Map11 HUD、全屏小地图和失落女神弹窗截图；Map10 HUD 当前不可见，Map11 地形／水面／遮挡仍有原有缺项，完整画面和地图验收继续由所属页维护。Python Map11 运行载荷生成与小地图转换三项检查通过。另一次 Map10 旧输入来源检查失败：未纳入当前 Git HEAD 的 `assets/official/adventure.json` 实际哈希为 `a6820d65…`，原清单期望 `84b92c87…`；未修改该输入或预期哈希，不能据此声明旧输入来源校验通过。

Windows 普通 Player 已成功构建，在内置屏以  真实运行并通过上述同一套模拟指针、拖动、区域条件、女神／英雄、金币采集／刷新、探寻和冷读检查；已查看小地图、清雾、探寻概率／历史的实际画面。构建入口 `BuildWindowsPlayer` 支持 `-mainline-player-output <exe>` 和 `-mainline-release-probe`；后者仅为普通回归构建增加 `ZJCS_MAINLINE_MODULE_VALIDATION`，启动参数仍为 `-verify-mainline-modules <隔离目录>`，并阻止正常启动流程另开存档界面，测试不写入用户档案。沿用本机已安装的 Mono Windows 支持，构建后恢复原脚本后端。回归版本位于 `.tools/mainline-modules-player/杖剑传说.exe`，不带验证参数时仍走正常游戏入口。当前 Unity  日志仍有原生 `PresentFrame／WaitForLastPresentationAndGetTimestamp` 呈现时序告警；development 控制台遮挡的截图不作为画面通过证据，未声明该引擎告警已修复。为既有实现的公共模块抽取，完整玩法缺项见下文，未覆盖桌面正式版本。

新增载荷固定 `871_156598`：`scripts/prepare_mainline_interactions.py` 导出英雄参数、地区等级、区域刷新和体力点奖励；`scripts/prepare_unity_world_exploration.py` 解码7种令牌、65条实体组变体、28种模板及68条递归条件，已接入公共探寻事务与原窗口入口。`prepare_unity_map11_ui.py` 扩展至59份官方面板、535张sprite，保留英雄、探寻、奖励列表层级与原动画事件；来源分别见 `mainline-interactions-source-manifest.json`、`world-exploration-source-manifest.json` 及 Map11 UI 清单。探寻令牌列表恢复原LoopScrollRect的9个类，模糊和渐变的原生方法及程序转换分别记录在所属来源清单。

遗失女神领奖 UI 的来源准备将提取数据进一步扩为65份面板、945张Sprite和1,055条图标记录，新增六份原层级及394项主动／被动技能图标；导入器同步接受原结果窗和技能图标基础组件。该批工作保留原阶位／窗口映射／星级图／属性行动画及来源哈希，尚未接通地图入口和结果控制器，完整边界见[命运探寻](world-exploration.md)。各地图未完成页及配套提取／转换工具一并保留，入口见[世界地图](world-map.md)。

65份原面板已通过 Unity 导入／编译与 `ProjectTools.Validate`，既有公共流程的内置屏  图形 Play Mode 回归通过；新增结果窗仅验证原基础组件与动画资产，不作为完整交互验收或 Windows 发布结论。

带图形 Play Mode 已通过英雄首次领取、永久生命170／HP恢复、周期访问与每日共享额度、冷读，以及通过地图模拟指针采集金币（扣5体力、入包、点位隐藏、原3秒间隔刷新）。隔离事务检查覆盖体力不足回滚、金币／铁矿／时之砂数量倍率、离线刷新、怪物开始去重、失败重试、胜利历战精华与重复回包。已查看英雄面板和实际金币采集画面；奖励条已恢复原叠加材质、CanvasGroup动画和位置，粒子尚未完成，故未声明本功能完整验收或发布。

Map11区域阴影已通过编译、`ProjectTools.Validate`、图形 Play Mode 和实际截图复查：未公开区域使用原 Light2D 颜色／强度，模拟指针激活女神后播放原 `map_region_active_anim`，半径从0增至30，在2.01666665秒关闭对应阴影，冷读按已公开地区恢复。保留原全局白光、0.7倍光照纹理、HDR格式选择、受光排序层16／17／18和原衰减纹理；GLES程序机械转换为HLSL，网格由固定Unity Graphics的 `LightUtility.GenerateShapeMesh`／UTess／Clipper生成。来源与全部输出哈希分别见 `map11-region-lights-source-manifest.json`、`map11-light-meshes-source-manifest.json`、`map11-light-shaders-source-manifest.json`。这项验证覆盖区域地表阴影；未把日夜专属装饰、其他局部灯光或小地图未探索遮罩算作完成，也未发布新的完整功能版本。

地图 12 的专属实现、决策和缺口见[山之国](../maps/map-12.md)。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

`assets/reference/official-map/mainline-interactions-source-manifest.json` 记录620类实体扩展参数、英雄410等级属性、Map10／11地区等级、刷新间隔与体力点奖励的官方输入；`world-exploration-source-manifest.json` 记录7种令牌、65条实体组变体、28份完整实体模板、68条递归条件，以及奖励／随机包／宝物选择器／等级映射的原配置。运行时显式加载 `Resources/World/Mainline/interactions.json`、英雄属性及 `exploration.json`。探寻本地生成、保底、费用、奖励和存档事务，以及原窗口鼠标操作、令牌拖动、十连、历史分页与动态实体冷启动已通过隔离 Unity 编辑器验证；完整生成实体交互、模型材质、动画与 Player 尚未完成，范围见[命运探寻](world-exploration.md)和[公共模块](mainline-map-modules.md)。旧云端缓存不再保留，历史重建显式复用当前871缓存的相同哈希字节，核对原清单MD5／大小及真实凭据SHA256，来源保持实际版本及URL，不重建旧目录或替换变化资源。

公共网格 Module 位于 `Runtime/Offline/World/MapGridVisibility/`。调用方提供官方可见性／身体配置、当前坐标、已公开地区、地区查询及实体活动状态，使用同一个实例完成移动揭格、区域解锁、实体通知和存档快照。复用原 `GridVisibilityAgentComponent`／`GridVisibilityAgentHandle`，没有另写一套视野算法；旧原实现仍在 `Runtime/Official/World/Map13GridVisibility/`，目录名仅反映最初的迁移来源。尺寸和地图身份沿配置传入内部容器、范围与地区查询，不再限制为 13 或 240×240。来源对应与归一化文本 SHA256 见 [公共网格来源](../../assets/reference/official-map/mainline-grid-visibility-source-manifest.json)。

`SharedMapGridVisibilityAcceptance.Run` 已通过 Unity 2022.3.57f1c2 编译与规则执行：原 Map13 的 148 份身体模板、2,690 份放置、106 地区、57,600 格和 RLE 冷读字节保持一致；四组 17×9 接口夹具验证不同地图身份、角色模板、半径、实例隔离、移动与公开地区、存档及损坏游程拒绝。夹具不代表相应地图已迁移。静态元数据检查与 `ProjectTools.Validate` 通过。图形 Play Mode 中的实际地图入口、移动与冷读范围见[泽之国](../maps/map-13.md)，不据规则测试声明白雾画面或其他地图完成。

公共白雾 Module 位于 `Presentation/World/Mainline/OfficialMainlineGridFog.cs`，以原根 prefab、相机、地形坐标、公共可见性实例和玩家／传送坐标为 Interface，内部完成原 prefab 实例化、独立材质、原 `CloudController2` 初始化与结束清理。`LocalGridVisibilityAgent` 从完整官方 `MyGridVisibilityAgent` 迁移，只将实体容器、DI 生命周期与角色查询换成本地 Adapter；原单格／多格事件、即时移除、按距离扩散、暂缓揭雾、源坐标覆盖及目标集合均保留。现有 `MapRootVisibility` 的容器生命周期 Adapter 继续保留供原实体宿主使用；公共地图宿主不依赖该动态容器。Map11／12／13 的正式宿主已通过公共载入层接入原白雾；Map15 复用可见性且保留原无白雾差异。当前验证范围在各地图页维护。

当前版 Map11／12／15／16 的真实网格与身体输入已按原生成格式解码，使用 `World/Mainline/Visibility/Map<ID>/` 的独立 Resources 地址；Map11／12／15 已接入正式 Adapter，Map16 的配置已在 profile 登记但地图宿主仍待接线。Map11／12／13／16 的原雾层 prefab 已分别恢复；Map15 原根的 `CloudController` 显式为空，该差异以配置保持。资源规格及接入／验证范围只在各地图页维护；共同来源见 `mainline-grid-visibility-data-source-manifest.json` 与 `mainline-grid-fog-source-manifest.json`。

静态元数据检查（85,050 GUID）与 `ProjectTools.Validate` 通过。`SharedMapGridVisibilityAcceptance.RunSourceMaps` 已在 Unity 2022.3.57f1c2 中通过：保留原 Map13 与四组小尺寸 Interface 夹具回归，再使用 Map11／12／15／16 的真实原版输入执行移动揭格、全部区域列表、独立实例及逐格／逐字节冷读。后四图使用显式规则夹具（实体全部活动、地区查询返回0、不检查通行），未代替各图真实地区 Adapter、导航或地图交互验证。

公共可见性的加载、坐标／实体来源核对、区域查询、快照、白雾绑定和清理集中至 `OfficialMainlineMapRuntime.GridVisibility.cs`。Map13 保留旧 JSON 字段，其他地图使用 `gridVisibility`；事务回滚更换可见性实例时同时重新绑定雾层事件，避免继续监听已释放实例。当前接入与验证范围见各地图页。

地图档案显式配置网格／身体／白雾地址、是否要求独立小地图 prefab、交互配置版本及战斗／剧情数据版本，公共载入层不再按地图ID选择来源版本。当前 Map10–13、15 的战斗数据使用 `854_156383`，Map14 的战斗数据使用 `871_156598`，Map16 使用 `884_156692`；Map10–12 的剧情数据使用 `854_156383`，Map13–15 使用 `871_156598`，Map16 使用 `884_156692`。Map12／13 的地区、奖励预览、区域刷新和英雄结果索引从各自已校验的 `871_156598` 原表转换，避免误用仅含 Map10／11 的早期共享地区数组。空奖励列表按原表保留；`ProjectTools.Validate` 经档案公共加载入口核验 Map10–16 交互配置和鲸背岛六种英雄／43区；来源与绑定哈希见[各图交互配置](../../assets/reference/official-map/mainline-interaction-maps-source-manifest.json)。

公共小地图资源点已扩展到当前 `884_156692` 的六个原地图目录。`LocalMiniMapResourcePointContext` 直接从原地区配置取得地图身份，同一构造入口消费各图资源目录、地区状态与可选图标解析器，不再固定为泽之国13；旧目录没有新增存档字段，原泽之国目录继续按原地区行识别。原 `MiniMapResourcePointManager`／`Board`／`TapItem` 等六类当前源码与已迁移版本字节一致，复用同一实现。Map10／11／12／13／14／16 的原资源板数量分别为0／18／23／23／30／0，空列表保留；其余四图共94块原资源板与94条动态图标路径，物品定位106条、刷新组定位212条均由当前原 Luban 构造器读取，地区分别为5／77／99／106／111／43条。

原资源管理器从完整原根提取三对象子树，只断开父引用，完整保留原字典；资源板按原 GUID 显式绑定，Sprite 恢复原逻辑矩形、原网格、有效图集 UV、像素单位与 pivot，纹理沿当前 Windows 原生转换链。当前载荷位于 `Assets/Official/World/Map/{ID}/NativeMiniMapResourcesCurrent`，目录入口为 `Map{ID}/Native/current-resource-point-assets`；历史871目录保持独立。生成器为 `prepare_mainline_minimap_resource_points.py`，来源分别见 `assets/reference/official-map/map{ID}-current-minimap-resources-source-manifest.json`；隔离图形验证入口为 `OfficialMap13MiniMapResourcePointsAcceptance.RunCurrent`，工程由 `prepare_mainline_minimap_resource_validation.py` 准备。当前资源目录已由公共原生小地图工厂消费；正式地图宿主的资源层接入范围见下文，完整原根HUD与Windows Player仍未完成。

正式地图中已有原生 UI 的森之国、山之国与泽之国，将原资源筛选状态交给同一个 `MiniMapResourcePointManager`，载入本图当前884版原资源板、刷新分组与图标目录；地区状态由 `MainlineMapLabelContext` 读取正式玩家进度并发送原事件。未公开地区的资源板区域保持隐藏，地区公开后沿原事件刷新；切换浏览其他地图时隐藏当前地图资源层。筛选状态在圆形HUD初始化时恢复，不再等到首次打开全屏窗口；地图退出先停用原视图，再释放本地事件与字体实例。实体标记的公开显示读取共享原扩展配置，资源分布交给原资源板，避免同时显示公开地区的私有资源标记。完整公共原根和跨图浏览的资源层接线仍未完成。

静态元数据检查（120,461个GUID）、主工程导入／编译、`ProjectTools.Validate`、主线进度／体力／世界探索规则校验和内置屏图形 Play Mode 回归通过。新增森之国模拟鼠标检查覆盖资源全选／取消／恢复、私有地区隐藏、地区公开后原图标显示，以及进入地图时恢复筛选、原 `Start` 初始化后恢复公开区域；采集消耗／奖励／刷新、英雄雕像、命运探寻和通天塔移动／冷读回归通过。已查看资源分布、采集奖励、英雄标记和通天塔冷读的实际画面；森之国全屏底图及部分世界表现仍有缺项。山之国与泽之国的同一接线尚未逐图运行验证，本模块未发布新的 Windows Player。

公共动态标记目录使用当前 `884_156692` 的放置数据、原实体扩展／展示表和刷新分组全部候选，保留区域／实体标签的标记备选，具体优先级继续由原运行时选择。六图共用71份完整原标记 prefab 和105份原生 Sprite；Map10／11／12／13／14／16 的依赖名称分别为10／37／45／42／51／16种。目录位于 `Assets/Official/World/Shared/NativeMiniMapMarkersCurrent`，`Resources` 入口为 `MainlineMiniMap/current-marker-assets`，沿既有 `NativeMap13AssetCatalog.Load<T>(officialPath)` 按原完整地址查找；该目录类型不限制地图ID，历史泽之国871目录独立保留。生成器为 `prepare_mainline_minimap_markers.py`，来源为 `mainline-current-minimap-markers-source-manifest.json`。原标记组件逐文件核对当前源码与已迁移来源哈希，资源点和动态标记共同使用 `mainline_minimap_native_sprite.py` 恢复逻辑矩形、网格、轴心和图集UV。正式宿主的状态与标记创建仍待接入。

`MainlineMiniMapWorldData` 将已安装的官方地图定义一次转换为原小地图导航、地区与行走数据；`MainlineMiniMap.Create` 必须接收同一数据实例，并核对原地图与小地图配置身份。完整原根直接消费该实例的网格、坐标、区域归属、地区行、行走集合及村落显示配置，不再要求状态 Adapter 重复提供这些静态数据。各实例保留独立的可变原 DTO，倒计时继续使用每实例地区行。六图的全部258,400格、441地区和坐标往返已在隔离 Play Mode 核对；行走保留原普通集合与服务端覆盖集合的并集（当前 `None(0)`／`Shallow(6)`／`Around(7)`），不按地形值正负推断。所选884的完整导航、地图小地图身份与地区归属已逐项对比既有854／871／884地图定义；仅这些字段证明一致，不认定不同版本整版等价。四条村落偏移、名称格式和高亮配置从所选版本原表提取。来源见[共享世界数据凭据](../../assets/reference/official-map/mainline-minimap-world-data-source-manifest.json)。正式地图宿主的动态实体状态、完整HUD与该原根的组合仍未完成。 本次隔离图形工程编译及完整原根回归通过，覆盖模拟鼠标HUD／全屏／筛选、实时白雾、计时及释放，已查看实际圆形HUD、全屏、地区高亮和倒计时截图；静态元数据及5项来源检查通过。主工程已补齐验证程序集的 `Zjcs.Presentation.Combat` 引用并通过编译与 `ProjectTools.Validate`；本模块未发布新的Windows Player。

共享标记配置查询由 `MainlineMiniMapMarkerCatalog` 复用原 `EntityMapUIDisplayInfoParser`，读取 `Resources/MainlineMiniMap/current-marker-data` 并按官方完整地址取得同一原生 prefab。`prepare_mainline_minimap_marker_catalog.py` 从所选884版原 Luban 表保留六图放置与刷新候选涉及的287类实体扩展字段及311条完整UI显示行；当前解析器的查询与初始化正文分别核对原 `Get`／`PostParse`，初始化仅改为显式输入配置。查找保留地图组、地图、地区标签及实体标签的逐层默认分支，显示行图标为空时依原 `MapUtils` 回退到实体扩展图标；不展开成逐放置的固定绑定。每实例解析配置并只加一次原路径前缀，不修改共享JSON，实例不拥有原生资源。当前选取不证明剧情动态生成实体的完整覆盖；正式宿主实体状态、标记管理器与HUD组合仍待接入。来源见 [共享标记配置凭据](../../assets/reference/official-map/mainline-minimap-marker-catalog-source-manifest.json)。

共享配置查询与标记的带图形 Play Mode 经 `OfficialMap13MiniMapMarkSupportsAcceptance.RunCurrent` 通过：真实配置验证六图查询、同类怪物跨图图标覆盖、地区默认行、空图标回退、未知实体、两实例前缀与共享输入隔离，并按查询结果加载同一原生 prefab；71份原 prefab 的155个 SpriteRenderer、105份原Sprite逻辑字段／网格／图集UV、7次状态切换、40组每日次数与归属条件、4次NPC显隐、红点开关／数量上限、释放／复用和销毁退订通过。已查看两张实际渲染画面；静态项目检查、来源／输出／绑定哈希检查、正式工程 Unity 编译与 `ProjectTools.Validate` 通过。标记状态仍由明确的测试实体直接驱动，未代替正式地图输入、存档或 Windows Player 验证，本批未发布新可执行版本。

`MainlineMiniMap.Create` 的资源目录入口在原根激活前创建本地资源适配器，并与绑定副本一起交给既有完整根工厂；调用方原绑定保持不变。工厂统一持有并释放适配器，创建失败也退订，不销毁外部目录拥有的原生资产；直接提供外部资源上下文的既有入口继续由调用方持有，两个入口不能同时提供上下文与目录。额外引导图标可通过原适配器已有的精灵解析依赖注入。回归入口已将泽之国HUD、鲸背岛地区计时及其余五图原根改为读取当前资源目录；图形Play Mode通过同一工厂验证目录地图身份、原图层顺序、调用方绑定不变、创建失败退订及销毁后的全部订阅释放。正式宿主与完整地图路线仍未切换。

完整原生小地图转换链为官方 Rectf 补齐 Unity YAML 的版本标记，保持原坐标、宽高与UV数值；缺少版本会使原RawImage的UV宽高在导入后变为0。图形回归同时检查原UV矩形与圆形HUD最终画面中的地形像素，后台纹理有像素不再单独作为HUD显示通过条件。修复后原HUD、全屏页、筛选、单分线隐藏及鲸背岛计时回归通过；七张实际画面已查看，圆框中的地形、角色点与原遮罩正常显示。

六图资源目录的隔离图形 Play Mode 已通过：94块原资源板、620个标记点（森之国121、山之国160、泽之国178、龙之国161），全部 Sprite 逻辑字段、网格／图集UV与来源凭据一致；覆盖原资源层顺序、物品／怪物定位过滤、地区映射、公开／私有／未知状态事件、显式选择与引导取消、缓存复用及全部租约释放。已实际查看四张资源标记渲染截图；共用Sprite恢复代码后的六图回归同样通过。测试直接驱动原管理器，不代表正式地图鼠标操作或存档流程。三项来源／绑定检查、全量静态元数据检查（119,305个GUID）、正式工程Unity导入／编译与 `ProjectTools.Validate` 通过。当前没有发布新的Windows Player。

共享标记目录按原 `MiniMapWindowAttribute` 地址读取三个完整原生标记弹窗，`LoadDialogPrefab(Type)` 核对原控制器与 `PopupWindow` 类型，`LoadDialogSprite` 复用17份原 Sprite；入口为 `Resources/MainlineMiniMap/mark-dialog-assets`。该目录另按实际GUID绑定已独立转换的当前884版原 `ScreenBlur` 材质，保留其原始shader对象凭据；历史871原生弹窗和当前884标记配置、模糊资源分别记录来源，不认定整版等价，见[共享弹窗资源凭据](../../assets/reference/official-map/mainline-minimap-mark-dialog-catalog-source-manifest.json)。

`prepare_mainline_minimap_window_masks.py` 从当前884版原 `WorldCanvas/Content/Popup/BlurMask` 恢复全部8个原生对象及7个组件，作为 `Resources/MainlineMiniMap/BlurMask` 供同一目录加载。迁移原 `WindowMaskBase`、`WindowBlurMask`、遮罩效果与原 `IBlurManager`，保留原截图下一帧、重新开窗截图复用、全屏UV、排序、暂停恢复及窗口释放行为；依赖注入改为显式绑定，修复反编译的异步正文和 `Dictionary.Remove` 输出参数。原截图通道源码与已有桌面转换的输入逐字节核对，当前shader另核对当前APK对象哈希，不以历史shader代替。输入、原对象与输出记录见[共享遮罩来源](../../assets/reference/official-map/mainline-minimap-window-masks-source-manifest.json)。正式地图弹窗宿主与实体状态 Adapter 的组合仍未接通。

共享弹窗的正式工程图形 Play Mode 通过 Resources 加载三个原弹窗、原模糊遮罩和全部17份 Sprite，验证403个层级对象、1,495个组件的托管与对象引用、原中文标签与背景、7份动画、42次窗口状态变化、两次模拟鼠标传送按钮及一次原遮罩关闭、原音频播放、显示／隐藏事件顺序、暂停恢复、半秒遮罩点击保护、重入／取消、三次原模糊截图及全部纹理与背景租约释放。已查看三个含原模糊遮罩的实际运行画面；这些来自原窗口测试宿主，尚未覆盖正式地图实体详情，本批未发布新 Windows Player。来源／输出／绑定的92项哈希检查通过。全量静态元数据检查（125,350个GUID）、正式工程导入／编译与 `ProjectTools.Validate` 通过。

## 自定义实现

原玩家客户端的注入依赖已恢复原 `Framework.Core` 中四个已核对类型：属性标记 `InjectAttribute`、`IInstanceInjector`、静态 `ServiceProviderExtensions.Inject` 和 `DryIocInstanceInjector`。依据所选884 APK的已恢复AOT映像、原元数据及方法指令，将原 `GetService<IInstanceInjector>().Inject(对象)` 调用、原实例注入器构造及其 `ContainerTools.InjectPropertiesAndFields<object>` 调用迁为托管IL；仅交付该局部程序集，不使用DummyDll空方法体，也不改写历史871的Console／Framework.FirstPass IL。原 `Launch.IVersionManager` 完整接口从所选官方Launch程序集与反编译源码复用，核对六个原方法签名后只规范换行。资源、原生调用证据、版本、哈希、MVID和显式目录绑定见[共享注入来源](../../assets/reference/official-map/mainline-shared-injection-source-manifest.json)。

原实例注入检查覆盖原接口身份、同一目标／空目标传递、异常身份、缺失／错误服务、空 provider 与空 resolver，以及原 MeManager 的 GameData／MapData／RoomData 构造。原属性选择器保留私有／基类标记属性、未标记属性过滤、服务身份、类型缓存、默认可缺失及显式 Throw 分支。正式注入入口现由 `MainlineSharedClientServices` 创建真实 DryIoc 容器，使用原选择器、原 `DryIocInstanceInjector` 和原 `ApplicationScope.Current`，再取得原 MeManager 单例；公式服务按原 `UtilsHandlerBinder.BindAll` 的顺序绑定原 `BattleFormulaHandler` 与 `FormulaHandler`。退出时恢复仍由本作用域拥有的应用与公式绑定，保留外部后装的绑定。此入口不设置玩家、替换客户端初始化或启动网络连接；调用者须持有原配置作用域，正式世界宿主与玩家／地图状态的完整组合仍未接通。

原 Framework.FirstPass 所需 `DryIoc` 5.4.3.0（dfbf2bd50fcf7768）使用发布者 NuGet 5.4.3 netstandard2.1；原 Console 的 PlayerDataWrap／IMongoObj 所需 `LiteDB` 5.0.20.0（4ee40123013c9f27）使用 NuGet 5.0.20 netstandard2.0；原玩法状态观察器所需 `R3` 1.3.0.0（df4c250b14d82627）使用 NuGet 1.3.0 netstandard2.1。包 SHA-512 均核对 NuGet 官方元数据，DLL／许可 SHA-256、MVID 与原强命名引用分别固定在[客户端依赖来源](../../assets/reference/official-map/mainline-shared-client-dependencies-source-manifest.json)。R3 四项依赖沿用已有固定的 TimeProvider 8.0.0、Annotations 5.0.0、Unsafe 6.0.0 和 Channels 8.0.0。原 Console／Framework IL 未改写；加载继续核对哈希和 MVID。共享资源目录显式引用全部21份发布者许可文本；R3 许可取其包内仓库提交的完整 MIT 文本。

`MainlineSharedClientConfiguration.Load` 为公共原配置作用域绑定所选884的13张角色初始化表，统一核对原字节哈希，先注册、解析全部表，再执行原 `PostParse`，保留职业对技能表的交叉索引。属性二进制复用已经安装且核验来源的 `StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes`，不复制第二份载荷；共享资源目录明确记录该本地路径与哈希。单机只将原 YooAsset 路径发现改为本地文件：仍调用原 `ParserBase.Initialize`、原 `LevelPropBinaryReader(string)` 和1000项原LRU，再登记至同一 `ConfigManager`；属性读取、默认值合并、等级限制、战力公式和两个原属性上下文均保留原实现。配置退出现在调用原管理器的 `Dispose`，关闭原读取器后销毁所属对象。此加载入口不创建角色、不改角色模板默认值，重复登记被拒绝；加载失败时由调用者释放所属配置作用域。来源、所选884源码、独立固定的历史871 IL、输入／输出与显式加载路径见[角色配置来源](../../assets/reference/official-map/mainline-shared-client-configuration-source-manifest.json)。正式存档投影和地图宿主组合仍待完成。

`OfficialMainlineWorldStartupAcceptance.RunClientServices` 的内置屏 D3D11 Play Mode 通过真实容器与注入器身份、原 MeManager 单例、两次全新 Player／PlayerDataWrap 构造、LiteDB 强命名身份、原 R3 玩法观察器初始化／空状态枚举／释放，以及嵌套应用作用域和两类公式绑定的恢复。完整玩家入口 `RunClientPlayer` 已在同类图形 Play Mode 通过：使用正式公共配置模块及原本地文件读取器，沿原 MeManager setter 实际执行两次完整 `Player.ClientInit`、两个原属性上下文、属性重算、账号初始化及 `ClientDestory`；两个未改模板的全新角色原战力均为644，重算稳定。重复配置登记被拒绝，配置退出后原二进制读取器已释放，原生命周期无错误日志。当前客户端、角色配置、公共世界配置与地图配置四份清单的299项输入／载荷／绑定哈希核验通过。这仍不包含正式存档到原玩家的状态投影、正式地图表现宿主的完整组合、界面交互或 Windows Player；共享模块保持未完成。七图配置生命周期回归通过14组私有／公开状态、12,789个准备实体、79个移动实体及七图默认原根启动。本次主工程 Unity 导入／编译、`ProjectTools.Validate` 及全量静态元数据检查（127,947个GUID）通过。

`MainlineSharedWorld` 在解析配置前安装 `NativeMap14SharedLogging`，通过原 `Framework.Logging.Log.Initialize` 向已交付原 `ILogger`／`ILoggerFactory` ABI 提供本地输出，不启动在线应用入口。日志保留原泛型状态格式化器、等级、类别及异常对象；正式输出写入 Unity 日志，原事件委托和 EC 组件仍按原实现捕获并报告异常。已有完整日志绑定时借用，本模块拥有的绑定在最后一个重叠地图作用域退出后恢复，且不覆盖外部后装的绑定。ZLogger 所需的16份传递依赖按其官方 NuGet 清单固定版本、包SHA-512、DLL哈希和MVID，原 Console／Framework IL 保持不变；载荷、许可与本地目录见[日志依赖来源](../../assets/reference/official-map/mainline-shared-logging-source-manifest.json)。带图形的隔离 Play Mode 已核对泛型值状态、格式化器、异常对象身份、类别复用、原泛型日志包装、原事件委托捕获异常、嵌套／交错释放、外部替换及构造失败恢复；七图259,150格与33,522次全新放置初始化回归通过（12,775次出生、20,747次被原出生条件拦截）。主工程编译、`ProjectTools.Validate` 和静态元数据检查通过；此次是原运行时日志与状态检查，实体完整客户端启动、正式交互和 Windows Player 仍未验证。

公共原 EC 实体按已确认方案从全新存档初始化，无旧存档迁移。`MainlineSharedWorld.Load(地区状态查询)` 统一处理七图的默认出生，查询接收本图地区编号，沿原 `MapRegionStatus` 的0／1／2返回未知／私有／公开；只使用地形／根的既有调用仍可省略该查询。内部 `MainlineSharedEntityCreation` 查本图原 `MapInfoWrap` 与原 Lo 模板，调用原 `RegionStatusSupported`／`BlockDefaultSpawnInMap` 决定是否出生，不符合条件不创建。非法状态使加载失败并释放所属作用域，重复加载仍由原公共生命周期拒绝。

原 `EntityHandler.InstantiateEntity` 只克隆组件配置，缺少服务端初始化执行器；单机在加入容器前，将原 `LoGridTransform` 的位置／方向／翻转、`LoWorldStage` 的阶段和 `LoWorldActiveableEntity` 的激活配置写入对应已有运行时组件，营地的原 `LoEntityCampComponentInfo.CampMonsterEntities` 复制为独立运行时列表；其余值保留原模板构造默认值，不补造组件、不改变共享Info。编号仍调用原 `EcContainer.NewEntityIndex`，Lo ID 对应仍由原 `MapLoEntityIdManager` 保存。全部初始实体先注册到尚未启动的容器。带地区查询的 `Load` 完成后 `IsLoaded=true`、`IsReady=false`，宿主安装真实玩家、地图和条件上下文后再调用 `Start`，沿原容器先全体 Awake、再全体 Start及 `newEntity=false` 的事件派发顺序；未完成加载或重复启动被拒绝。原 NPC 启动依赖 `MeManager.MainPlayer` 的跟随者事件，以及 `PlayerMap` 的逻辑ID映射和条件查询，配置与真实上下文的启动已通过上述显式夹具，正式存档和宿主适配仍待接入。地图退出通过原容器移除实体后清空该映射；尚未启动的实体按原行为只移除登记，不调用 End／Destroy。刷新沿所属功能已确认规则，新的存档状态写回、重新进入恢复及正式地图宿主的状态接线尚待接通。

全新实体准备已在主工程内置屏带图形 Play Mode 通过七图×未知／私有／公开的33,522次放置检查：12,775个实体实际克隆和登记，20,747次按原条件排除。核对已有组件的坐标／方向／翻转、阶段、激活值、原编号／Lo映射、启动前无 Awake／Start、显式释放，以及非法地区状态和准备阶段取消的清理。Map11–16 对照独立导出的组件JSON；Map10旧导出缺少组件JSON，坐标／方向对照地图定义，翻转／阶段／激活对照同一原Lo配置。既有七图默认加载路径也通过同一入口回归，覆盖259,150格、11,174份模板和原组件载荷；未知地区无放置实体时，显式启动原根及重复启动拒绝通过。静态元数据检查（125,958个GUID）、主工程导入／编译与 `ProjectTools.Validate` 通过，验证入口为 `OfficialMainlineMiniMapRootAcceptance.Run -verify-fresh-entities-only`。本项验证快照准备及原根生命周期，未验证原客户端完整实体启动、正式实体美术、交互、存档流程或 Windows Player。

鲸背岛数据由公共 `OfficialWorldMapAssetCatalog.LoadMap(16)` 统一读取和校验来源，图形 Play Mode 已检查本图数据身份与未开放入口状态。公共夹具的 `-verify-map15-only` 分支另通过通天塔模拟移动、冷读及每日访问效果冷读；后者比较访问前已有记录及新增的一条精确效果，不依赖被跳过的英雄雕像步骤。整图与资源接线边界见[鲸背岛](../maps/map-16.md)。

`OfficialMainlineMapProfile.RequiredAccessConditions` 只固定地区 `AccessibleConditionId` 的依赖，不包含整图入口条件。鲸背岛按原43区的零条件配置登记空清单，整图条件116留在独立入口配置；`ProjectTools.Validate` 以本图真实定义构造公共区域策略并检查全部地区，防止把整图条件误作缺失的地区条件。正式鲸背岛入口仍未开放，详见[鲸背岛](../maps/map-16.md)。

公共剧情归一化保留官方查询节点 `HandInItemId` 和执行节点 `HandleItemId` 两种字段名，统一投影为运行器消费的 `handInItemId`；Map13／16 的生成载荷同步修复。Map14 转换已有同义处理，本次保留既有载荷；其历史输入 `stories.json` 与来源收据不一致，重建被拒绝，未修改预期哈希。各图实际交付流程和验证边界见所属地图页。 Unity 导入／编译、`ProjectTools.Validate` 和内置屏公共地图 Play Mode 回归通过：覆盖初始岛／森之国模拟点击、女神／英雄雕像、资源消耗／刷新、探寻和冷读，以及通天塔移动／冷读；交付次数、限额与冷读由既有存档规则检查覆盖，尚未通过Map13／16实际交付UI验证。已查看运行截图，森之国仍存在地面和部分实体显示缺项，不能将交互断言通过等同于完整视觉还原；未发布新的 Windows Player。

公共原生小地图的单机差异沿[世界地图已确认规则](world-map.md#自定义实现)。`LocalMiniMapDataSource.Request(mapClassId)` 读取一份地图快照，校验到访身份、原RLE尺寸及唯一实体ID，选择实体后复制白雾、公开地区和最新类型／状态；延迟完成保持原协调器先订阅、再显示内容／初始化白雾／标记的次序。`UseLatestLocalEntityStates` 仅用于本地响应，让原标记初始化按最新类型判断完成隐藏，并略过公开地区的默认完成状态补建；未设置该字段的原消费分支保留。`SingleChannelMiniMapHud` 由 `AttachHud` 统一绑定，原按钮自行按 prefab 条件隐藏；地区、战斗、窗口和公开容器身份继续读取原宿主，HUD 文本复用所属实例的地区计时绑定。正式地图文件的快照读取已由下述公共入口提供；原生全屏页签与正式宿主的组合接线仍未完成，不据此声明整图或 Windows Player 完成。

`OfflineMapGridVisibility.ExploredEntityIds` 维护每个实例曾发现的实体放置ID：沿现有世界可见性判断，实际启用的世界对象所在坐标格已揭开时记录，移动、停用或完成后不删除。加载时使用当前实体位置，官方放置默认坐标仍保留用于原营地计算；移动、地区解锁、世界实体启用刷新及读取快照时更新记录，返回独立、有序副本；已揭格内新出现的实体在下一次交互前即被记住。正式共享宿主保存到各图原存档的 `exploredEntityPlacements`，沿原RLE和位置一起恢复；山之国机关与泽之国水面机关移动同步原可见性身体位置，存档失败先恢复机关状态，再重建可见性与探索记录。重复、负数或不属于本图的记录拒绝载入。旧档没有该字段时保留原探索格，从当前仍可见的活动实体补建；无法追溯旧档未记录的历史移动或已移除实体，不伪造历史。

正式宿主的世界实体与小地图标记共用 `MainlineMapEntityPresentation.Result`：世界刷新将最新逻辑状态交给小地图，标记不再重复判断机关、女神与完成回执，也不再从仍在播放移除动画的世界对象反推活动状态。英雄雕像私有激活状态1按原完成隐藏，公开地区状态2保留每日访问标记；每次刷新同时同步标记坐标，释放地图时清空实例状态。这是既有正式标记宿主的状态接入，完整原标记管理器与公共原根仍待组合。静态元数据检查（120,456个GUID）、主工程编译与 `ProjectTools.Validate`、主线进度／体力／世界探索规则校验及内置屏 Play Mode 回归通过。新增检查验证同一个实际英雄标记在未激活、私有激活、公开地区和存档冷读时的显隐；原激活与每日访问按钮使用模拟鼠标，已查看公开标记和每日访问画面。既有回归同时覆盖地图移动、全屏页签／筛选、女神揭雾、资源消耗／刷新、探寻和通天塔冷读；未验证 Windows Player。

`MainlineMapMiniMapSnapshots.Read(mapClassId)` 从所选档案读取已到访地图的正式文件，每次读取最新角色进度，复用当前地图的存档校验／备份读取、实体目录、机关依赖闭包、山之国完成回执恢复、原地区探索点计算及实体状态投影。世界刷新与跨图读取共用女神／英雄状态、剧情销毁、地区可进入条件和体力点公开／私有刷新判断；另恢复森之国魔女与泽之国水面实体状态。命运探寻生成实体沿已有模板与本地有效期读取，不执行探寻或领奖。读取不实例化地图、不创建地图记录、不修复损坏主文件、不写回奖励、进度或备份；缺少正式地图文件或探索白雾记录、未到访或主备份均损坏时拒绝读取。白雾保留原压缩字节，已完成／未激活实体从当前实体集合排除，由已有本地响应选择曾探索或公开地区实体。此入口尚待接入正式原生全屏浏览。当前正式揭格记录来自初始岛、森之国、山之国、泽之国、龙之国与通天塔；Map16 的共享宿主尚未接通。

`MainlineMapMiniMapSnapshots.ReadRegionPoints(mapClassId)` 复用同一冷读与实体恢复，返回原 `Common.Player.WorldMapRegionProgressDict` 所需的原始实体探索点；本地角色页的 `OfflineRegionProgress.Progress` 为万分比，不能直接填入该原字段。此读取不要求小地图白雾已保存，仍校验地图文件及已有白雾数据，每次返回独立字典，不创建场景对象或修改文件。正式工程带图形 Play Mode 通过公共入口核对七图442个地区：全部完成的实体逐区等于官方最大值，即使角色页万分比均为10000也不混用单位；空白雾下原始点数可读、小地图快照拒绝读取，重复读取互不污染且全部文件保持不变。静态项目检查、Unity 编译与 `ProjectTools.Validate` 通过。该入口为原玩家状态接入提供数据，尚未代替正式玩家完整投影。

正式存档快照已在森之国、山之国、泽之国与通天塔的隔离图形运行中读取，核对实体显隐与地图实际活动实体一致、探索RLE保持原值，读取不创建场景对象且不修改存档文件。森之国与山之国另验证了最新本地实体状态、刷新时刻、备份回退及损坏／缺少白雾记录时拒绝读取。共享模块 GUI 回归覆盖主线地图 HUD、地区条件、地图移动与冷读、女神／英雄雕像、体力采集、命运探寻交互和探索奖励；规则验证覆盖主线进度、体力事务、世界探索。验证入口未覆盖正式原生全屏跨图窗口与 Windows Player。

实体探索记录已通过两组独立地图身份的17×9接口夹具：隐藏／未激活排除、当前坐标、实体移动与地区揭格、实例隔离、防御副本、磁盘冷读和非法记录拒绝。原 Map13 全部可见格／身体与原RLE回归，以及 Map11／12／15／16 真实输入规则检查通过。正式森之国、山之国、泽之国和通天塔入口层另通过带图形 Play Mode 的模拟地图点击，检查原可见数据、位置和实体探索记录保存／冷读一致；森之国与山之国女神揭雾回归通过，已查看四图实际运行画面，女神验证的前置地区完成度由夹具设置。静态元数据检查、隔离工程编译及 `ProjectTools.Validate` 通过；跨图窗口或 Windows Player 尚未覆盖。

隔离 GUI Play Mode 通过同一 `Request` 接口，冷读泽之国240×240和鲸背岛140×120配置的测试快照，验证探索／公开筛选、最新实体类型和状态、完成隐藏、默认公开标志不重复补建、每次浏览重读磁盘及错图／未到访／损坏白雾拒绝。实体状态组合是明确的测试数据，不作为官方实体配置。原 HUD 的单分线隐藏、公开地区保留、外部频道列表不改变单机频道数、无多余频道事件订阅、模拟鼠标打开／关闭全屏和筛选、地区倒计时及生命周期回归通过；已查看单分线圆形HUD、全屏地图、筛选和鲸背岛原倒计时画面。标记检查覆盖原管理器的异步 prefab 创建决定，未将其声称为所有实体图标／详情的实际渲染完成。

本批静态元数据检查、正式工程 Unity 导入／编译与 `ProjectTools.Validate` 通过；原标记管理器64组离线条件、24组归属条件及实时状态／地区事件在带图形 Play Mode 回归通过。原生来源、输出、脚本及接线哈希经重建和校验通过；验证使用隔离快照，不写用户档案，尚无正式 Windows Player 接线结论。

沿用已确认的本地地图存档、神像最近可站立格传送和英雄／魔女共享每日访问额度。规则细节分别见[森之国](../maps/map-11.md)、[战斗执行](battle-execution.md)；地图奖励沿用户确认方案：原权重选取、原数量乘等级／阶位倍率，小数按概率取整，再应用原属性29加成并向下取整。普通点位按原配置扣体力57×5；命运探寻来源实体须按官方来源规则免体力。刷新沿用户确认的本地方案：保留点位、区域开放、原组间隔与上限，计入离线时间。

初始岛与龙之国通过 profile 的可见性／身体／白雾资源地址接入同一 `OfflineMapGridVisibility` 与 `OfficialMainlineGridFog`，不增加地图专用揭格实现。输入分别含60×60格／82个放置／5区、280×280格／2,961个放置／111区；当前884输入逐项核对已安装历史地图定义的尺寸、实体编号、类别和格坐标，不据此声称不同版本整体等价。两图原 CloudController2 子树在同一宿主生命周期挂载和释放，揭格原RLE及探索实体ID写入各图既有 `gridVisibility`／`exploredEntityPlacements`。旧档缺少记录时只从当前角色位置和已有公开地区重建；出生点配置独立于是否启用揭格，保留初始岛既有位置流程，森之国与泽之国仍使用原入口101／11001。来源见[揭格数据](../../assets/reference/official-map/mainline-grid-visibility-data-source-manifest.json)和[原白雾](../../assets/reference/official-map/mainline-grid-fog-source-manifest.json)。

初始岛／龙之国正式工程回归通过静态元数据检查（120,483个GUID）、Unity导入／编译、`ProjectTools.Validate` 和主线进度／体力／世界探索规则检查；六张官方输入的全部地区揭格与全部格子的原RLE冷读一致。内置屏图形 Play Mode 经 `-verify-visibility-maps` 验证初始岛旧档补建、龙之国新档原入口、两图模拟地图点击后新增揭格、一次且仅一次原白雾实例、位置／原RLE／已发现实体的磁盘冷读，以及释放场景后两图快照仍可只读取得；已查看移动和冷读的实际白雾画面。正式工程补齐龙之国416项实体 prefab 目录，另将旧战斗验证夹具的十处约束采样委托改为显式四参调用，兼容新增的可选本地回调，采样参数不变。龙之国原实体 prefab 的缺失脚本警告仍存在，实际角色／实体动画与HUD未完整呈现；本次数据与白雾断言通过不代表完整视觉通过。两图女神激活UI、完整原生小地图窗口与Windows Player未在本批验证，地图和公共模块均保持未完成。

## 尚未实现

当前主工程编译、`ProjectTools.Validate`、经 profile 的 Map10–16 交互加载校验和全量静态元数据检查通过（117,636个GUID）。公共交付窗口的隔离 Play Mode 已通过；本次未覆盖正式NPC剧情地图流程或 Windows Player。

- 英雄雕像原激活动作与三种周期效果的角色、粒子、音效和 MyPlayable 演出；界面与永久属性事务已有实际指针验证，完整表现未完成。
- [命运果实与命运探寻](world-exploration.md)的全部实体交互与演出。果树生产继续复用已有 [果树服务](fruit-tree.md)，探寻费用、候选／保底、动态实体、历史和原窗口已部分验证；遗失女神结算已接入规则层，原领奖界面与地图入口仍待完成。
- 地图体力怪物的实际战斗完整操作和冷读回归、铁矿／时之砂地图点击、奖励粒子及全部原生材质；不能仅以服务事务检查替代运行验证。
- Map11原日夜专属场景装饰、局部灯光和小地图未探索遮罩；区域地表阴影与原渐退已接通，但 `ActiveRegionTilemapDisplayHelper`／`ActiveRegionTileMapDisplayHandler` 及其他地图原载荷仍待接入。
- 正式宿主尚需以公共原生工厂接通小地图 HUD、全屏页签、正式快照读取、跨图公共地区资源层及各图实体图标与详情；当前森之国全屏小地图底图仍缺失，实际画面仅见标记等UI。未完成的官方表现和各图完整路线继续按所属地图页维护，共享底层不自动完成单张地图。

共享原演出核心见 `Runtime/Official/StoryPlayables/` 与 `scripts/prepare_unity_story_playable_runtime.py`：原 `MyPlayable` 状态机、双阶段中心、序列、触发器、有序容器与粒子淡入淡出保留；离线显式注入及调用方帧阶段替代原服务注册。隔离 Unity 完整校验、Play Mode 调度／状态／异常恢复及 3 项迁移回归通过，视觉和输入未覆盖；正式宿主、原 prefab、动画／骨骼、Timeline、相机和音频仍待绑定。来源与验证范围见[龙之国](../maps/map-14.md)，不据此开放完整地图。

`Runtime/Offline/World/MainlineStoryPlayables/` 是各图原演出资源边界：`MainlineStoryPlayableAssets.Load(mapClassId, expectedManifest, loadCatalog)` 按地图配置和固定来源清单核对 GUID／节点字段并创建原 `PlayPlayableNode`；各图通过 `loadCatalog` 适配自己的原生资源目录；`MainlineStoryPlayableScope` 为原 `MyPlayableManager`／`MyPlayableCenter`／FIFO池提供地图生命周期作用域。Map13／16 已配各自资源清单，不改原请求 flags、时长、资源顺序或端口。已绑定鲸背岛两份演出 prefab。此次相关 C# 程序集在主工程重新编译成功，全量静态元数据检查覆盖127,774个GUID通过；内置屏 D3D11 Unity 回归实际执行并通过 Map13 全部29个请求、55路演员输入、20次等待、2次演出前停止及清理，运行画面已查看；同次验证加载并创建了Map16的2个请求节点，Map16正式演员流程仍待正式入口接通，详见[泽之国](../maps/map-13.md)和[鲸背岛](../maps/map-16.md)。

地图 14 的专属实现、决策和缺口见[龙之国](../maps/map-14.md)。

共享原声音及 Timeline 驱动已恢复至 `Runtime/Official/StorySounds/`：保留官方循环、停止、事件目标和手动时钟绑定；内置屏隔离 Play Mode 调度验证通过。原事件 ID 桥接复用官方 SDK Post，完整原声总线 DSP／RTPC、原演出 prefab／气泡与正式剧情入口仍待接通，范围见[龙之国](../maps/map-14.md)。

共享原 Animator 参数执行组件位于 `Runtime/Official/StoryAnimation/`，保留原缓冲、来源分层、暂停和淡出／停止恢复；隔离原生 Animator Play Mode 已验证生命周期及复用。来源和范围见[龙之国](../maps/map-14.md)；该检查尚不覆盖原骨骼／角色画面、控制器资源绑定和正式地图业务。

共享原剧情外部节点注册与 Cinemachine 轨道空绑定逻辑位于 `Runtime/Official/StoryNodes/`，两种异步等待保留官方阶段、令牌与取消行为，职业 UI 注册可共用管理器；隔离 Play Mode 注册／取消／轨道绑定验证通过。原 Timeline 场景和正式地图流程仍未完成，见[龙之国](../maps/map-14.md)。
