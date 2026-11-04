# 16 鲸背岛

[总索引](../README.md)

状态：未完成

## 地图简介

鲸背岛是带独立入口的可交互大地图。迁移范围包含原地面与 HUD、地区与解锁、雕像、网格白雾、探索度、命运探寻、大小地图与标记、体力资源与刷新，以及本图剧情、机关、战斗和本地存档。公共模块见[主线地图公共模块](../features/mainline-map-modules.md)，共享规则见[世界地图](../features/world-map.md)。

## 原始官方实现

当前配置按所选 `884_156692` 的 APK／匹配版本缓存读取，历史 `871_156598` 输入保留独立来源，不据旧文档中的文件名认定资源已存在。原地图 ID／地图组为16，场景为 `map_world`，BGM16，后继地图14。地面地址为 `Map_16/MapGround_16`，独立小地图为 `Map_16/MiniMap/MiniMap_16`，小地图配置 ID4；原 `CanExplore=false`，支持私有地区，公共地区和两种组队开关均关闭。

原 `MapRootEntityLiteSingleInfo` 给出140×120网格、原点 `(-80.5,-70.5,0)`、角色3001、成员3002和35步寻路上限。`MapInfoWrap` 给出43区及各自探索点上限、1021个实体的地区归属、刷新组、公私有支持、初始放置与阶段。默认入口为101，网格 `(20,27)`；不从位置猜测实体所属地区。角色原 `GridMoverComponent` 引用移动动作22021、可通行类型键3／服务器覆盖键1；原 `entity_grid_mover` 表对应 `[0,6]`／`[7]`，移动掩码65按原位操作生成。

地区表保留各区原等级、子位阶、奖励与相邻区。时间条件包含无时间限制和位阶4加14至32天的不同偏移；不得把所有地区统一改成14天。原 `Common.MapRegionInfo` 保留时间条件先于等级的判定、超过指定服务器位阶立即通过、缺少开放日期时关闭等分支；原 `MapUtils.TryGetMapUnlockTimeByServerRank` 在开放日期上 `AddDays(offset)`。本地日期来源的已确认替代见下文。

地面 prefab 的原层级含4224个 Unity 对象，引用区域灯、世界标签、水面及 `SeaRendererReplacement`。其 `Water_map13` 是实际引用的共享水面配置，继续按原 CAB／PPtr 恢复；名称含13不表示改用另一张地图的配置。独立小地图引用43份 `MinimapRegionTilemap/RegionGround_*` 原 prefab；区域地面及完整小地图根已原生转换，正式地图接线仍待完成。

## 完整还原部分

鲸背岛已加入共享 `OfficialMainlineMapProfile` 作为数据档案，配置同版网格／身体／白雾资源、43区、默认入口实体101、单本地分线、交互资源及 `884_156692` 战斗／剧情来源；`IsRuntimeEntryReady=false`，正式地图入口仍关闭。网格资源、白雾 prefab 和交互配置尚未接入本图正式地图宿主，剧情、战斗及实体视觉仍有独立缺项。

43个官方地区的 `AccessibleConditionId` 均为0，因此公共档案的 `RequiredAccessConditions` 为空；整图解锁条件116仍由独立入口配置保留，不混作地区条件。`ProjectTools.Validate` 从真实Map16定义构造 `OfficialMainlineRegionPolicy` 并检查全部43区，通过当前无地区剧情门槛的分支；这不绕过等级、位阶日期、登岛准备和剧情条件，也不开放正式入口。

完整原小地图 prefab 已接入公共 `MainlineMiniMap` 实例工厂，保留原根315个序列化对象文档及全部原外部说明／接缝线引用；`MapAreaTile` 保留原继承关系、区域编号和完整Tile字段。隔离 GUI Play Mode 已检查本图完整 prefab 的脚本、托管类型及对象引用；使用本图140×120网格、43地区和配置4，通过与泽之国相同工厂的加载、原离线请求／等待隐藏和订阅释放。此项尚未提供本图正式存档响应、地区白雾／实体图标实播或HUD模拟交互；不视为本图完整小地图完成。来源见[当前完整小地图根](../../assets/reference/official-map/map16-current-minimap-root-source-manifest.json)，公共生命周期与验证边界见[公共模块](../features/mainline-map-modules.md)。

尚无本图完整 Unity 游玩流程的完成结论。

小地图区域地面使用公共 `MainlineMiniMapRegions`，由 `MainlineMiniMapRegionAssets` 提供本图原 Ground subtree、43个地区插槽及原区域 Tilemap，状态、角色和可见范围通过 `IMainlineMiniMapRegionState` 注入。资源入口为 `Resources/Map16/Native/mini-map-regions`，原生载荷在 `NativeMiniMapCurrent`，共120个资产／meta输出。转换器核对原 YooAsset GUID 与所选版本资源索引后绑定本地 prefab，不把转换缓存的 Unity GUID 当作原 YooAsset GUID。原 `MapMeCharacterUIBase`／`MiniMapPrivateRegionMaskManager` 与已迁移源码逐字节核对，保留初始化、地区开放事件、公私有 Z、倒序可见性遍历、缓存复用、十秒回收和释放顺序。可见范围由公共模块订阅原 `IMapViewRangeHandler` 事件，普通刷新不重复发布；替换处理器统一退订／订阅并发布一次，停用／销毁时释放订阅，不释放外部拥有的处理器。此段范围仅为区域地面；完整根工厂见上文，正式宿主的白雾、标记、相机、输入／存档仍待接线。资源与代码来源见[本图区域地面](../../assets/reference/official-map/map16-current-minimap-regions-source-manifest.json)及[公共区域模块](../../assets/reference/official-map/mainline-minimap-regions-source-manifest.json)。

四份原 Unity Tile 的字段由当前程序集严格解码恢复，包含 Sprite、颜色、矩阵、实例对象、标志及碰撞类型；不使用只含头部的导出缓存作为完整 Tile。原 Sprite 引用按 CAB／PathID 精确对应已绑定资源，矩阵只转换 Unity YAML 字段名。空运行标记复用已有官方 `WorldPreviewTool`，原 `PreviewTool` 源码已按当前版本核对。

公共区域地面在隔离 Unity Play Mode 中以本图全部43区验证：原 Tilemap 加载、仅目标地区的开放 Z 变化、原范围事件驱动的隐藏、期限前实例复用、范围处理器替换／旧事件隔离、持续普通刷新仍按十秒回收、重新加载、释放与带已有公开状态重进均通过，无运行错误。使用原渲染器索引1及原材质，已查看初始／重进实际运行截图；镜头范围为本次局部检查设置，不代表正式小地图镜头已迁移。此验证不包含白雾、标记、UI／模拟输入、正式地图存档或 Windows Player。

局部数据入口由 [`prepare_mainline_map_definition.py`](../../scripts/prepare_mainline_map_definition.py) 按地图 ID 提取，输出 `Assets/Official/World/Map/16/map16.json` 及 `Source/` 中的导航、放置、完整 EC、63份递归模板／动作和原配置表。它对比原导航／MapInfoWrap／EC 的身份与实体闭包，核对43区探索上限，验证消费的109份已准备官方源码；不填造默认实体、地图条件或移动规则。来源见[地图定义清单](../../assets/reference/official-map/map16-definition-source-manifest.json)。

[`prepare_mainline_entity_prefab_roots.py --map-id 16`](../../scripts/prepare_mainline_entity_prefab_roots.py) 按当前地图放置记录、角色／成员类别、原 EC `index.g.txt` 与当前 `TbEntityMapDisplayInfo` 解析真实 prefab 地址，不使用 `PenPath` 猜资源名。鲸背岛的1021个放置引用62种角色／实体类别，解析出164个 GameObject prefab，命中18个所选 `884_156692` APK／官方 CDN bundle，877份序列化文件的CAB依赖闭包完整；来源身份、对象哈希、源码及工具哈希见[实体 prefab 输入清单](../../assets/reference/official-map/map16-entity-prefab-roots-source-manifest.json)。这些仍是 Android 原始转换输入，Windows prefab、脚本、材质、动画和正式实体宿主尚未接入。

[`verify_mainline_entity_native_exports.py --map-id 16`](../../scripts/verify_mainline_entity_native_exports.py) 将上述164个根地址与同版 AssetRipper 完整导出逐一配对，并校验每个 prefab 与 `.meta` 对应的输出 SHA256／长度；逐项结果见[原生转换输入收据](../../assets/reference/official-map/map16-native-entity-prefab-exports-source-manifest.json)。该收据锁定后续 Windows 转换所用的准确 YAML 输入，不表示原 Assembly-CSharp 脚本、材质、shader、动画或正式实体宿主已经恢复。

[`audit_mainline_entity_script_bindings.py --map-id 16`](../../scripts/audit_mainline_entity_script_bindings.py) 从164个已校验 prefab 中识别103个原 `Assembly-CSharp` 脚本 ID，并按884版反编译源码的 MonoScript local file ID 规则全部还原到官方类型。当前已提交 Unity 运行时代码没有完整类型名／命名空间匹配项，但有39个同名迁入类型可供 Adapter 逐项核对；其中20类的自定义字段名存在候选匹配，14类进一步匹配反编译源码与迁入类中的字段类型写法。另7类有类型差异，包括 `SpriteRendererList.mSkeletonRenderer`（`SkeletonRenderer`→`NativeActorFadeRenderer`）、`MyMapDisplay.mMyEntity`（`MyEntity`→`ECViewEntityBinding`）、`EntityAwardItemHook.mPriority`、`PlayerIconMe` 和角色外观字段。收据记录候选脚本路径、`.meta` GUID、字段及类型差异，不将静态字段匹配视为可直接绑定；Unity 反序列化、继承关系和运行行为仍待逐类验证，原生 prefab 暂不能接入正式宿主。来源与引用 prefab 见[实体脚本绑定审计](../../assets/reference/official-map/map16-entity-script-bindings-source-manifest.json)。

其中 `SlicedFilledRender` 用于本图56份实体视图 prefab。884版该类及 `SlicedFilledRenderService` 的反编译源码字节与 Map13 现有迁入前的官方输入完全一致；公共地图宿主现驱动 `SlicedFilledRenderService.Current.LateUpdate()`，Map13 的原组件验收也改由该宿主推进。Map16 的 `Img_RoundLoad` 原 Sprite YAML 已按原 GUID 安装，保留 8 顶点／18 索引网格、矩形、UV变换与 `World_Atlas` 引用；同版 AssetRipper 输出的1024×2048完整图集以原 Texture2D GUID 安装，并将其导入设置改为 Windows 可用的2D未压缩纹理。安装器及输入／输出哈希见[实体进度图资源清单](../../assets/reference/official-map/map16-entity-rendering-assets-source-manifest.json)。Unity 工程编译及 `ProjectTools.Validate` 已通过，但本图 Sprite 尚未在运行时显式加载／渲染、GPU像素未核对，56份原 prefab 也未渲染，因此仍不能将这些 Android prefab 接入正式宿主。Map13 原组件及验证范围见[泽之国](map-13.md)和[公共模块](../features/mainline-map-modules.md)。

[`prepare_mainline_map_interactions.py --map-id 16`](../../scripts/prepare_mainline_map_interactions.py) 校验本图既有定义输入，从同版原表补充状态效果与周期随机奖励，并复用公共英雄结果索引转换器。英雄模板列表按本图原周期奖励组件提取，`Resources/Map16/interactions` 包含6570–6575六种英雄雕像、621项实体扩展参数、本图43区及43个区域的刷新组／间隔；原组件索引、状态 ID、5／25／70奖励权重、费用、地区顺序和空 `DropItems` 保留。原生数据输入另存 `Source/interaction-tables.json`，来源见[交互配置清单](../../assets/reference/official-map/map16-interactions-source-manifest.json)。此为配置投影，尚未登记正式宿主，不代表雕像、资源交互或存档已接通。

[`prepare_map16_alone_configuration.py`](../../scripts/prepare_map16_alone_configuration.py) 直接解码所选 `884_156692` 的 `TbMapWorldAloneInfo`、`TbConditionInfo` 与 `TbStoryInfo`，输出 `Resources/Map16/world-alone-configuration`。原准备条件为 Silver、14天及300秒，条件116为地图16解锁，保留770／771奖励规则、入口 prefab 地址及七条鲸背岛剧情的原路径／次数／自动运行字段；测试核对字段差异、故事缺项拒绝。该配置尚未绑定入口 UI、故事资源、存档事务或公共地图宿主，不代表准备／登岛／离岛剧情已可运行。来源见[独立地图配置清单](../../assets/reference/official-map/map16-alone-configuration-source-manifest.json)。

公共 `GroundBinding` 接收 `GroundSource` 的地图 ID、原 prefab 地址和 bundle，复用 Map15 的原生依赖恢复路径。Map15 已安装 GUID 保持原值，鲸背岛使用所属地图的 GUID 范围，避免两图复制的载荷互相覆盖。Map16 当前输出位于 `Assets/Official/World/Map/16/NativeCurrent`，资源入口为 `Resources/Map16/Native/current-ground`，含1434个资产／meta输出。脚本只显式绑定已迁移的官方类；原纹理、Windows shader、材质、网格及序列化字段按选定版本和各自哈希恢复。来源见[地面清单](../../assets/reference/official-map/map16-current-ground-source-manifest.json)。

[`prepare_mainline_map15_world_view.py --map-id 16`](../../scripts/prepare_mainline_map15_world_view.py) 读取原3001模型／偏移／排序和原 `WorldCamera` 投影／适配参数，输出 `Resources/Map16/BaseGround/player-view` 与 `Native/region-label-data`。标签按原地区 ID 索引探索上限，保持原表枚举顺序，核对61项实际语言键，包含原倒计时格式所需的14项语言键；公共标签样式、位阶映射和字体仍分别保留历史输入凭据，不声称整个版本等价。来源见[模型、镜头与标签清单](../../assets/reference/official-map/map16-world-view-source-manifest.json)。

公共网格输入已从当前所选版本单独提取：140×120网格、43份区域揭格列表、61份身体模板、1021个实体放置，资源为 `Resources/World/Mainline/Visibility/Map16/visibility` 与 `bodies`。原 `MapRoot_16` 完整雾层 subtree 与依赖为 `Resources/World/Mainline/Fog/Map16`。来源分别见[公共网格输入](../../assets/reference/official-map/mainline-grid-visibility-data-source-manifest.json)和[公共雾层](../../assets/reference/official-map/mainline-grid-fog-source-manifest.json)。这些当前版载荷尚未接入本图正式宿主。

原 `MapRegionLimitStatusRefreshForRankUnlockTime` 的 UniTask 状态机已迁移，按原顺序取得地区开放时间、等待并刷新状态。公共标签通过每实例 `IMapRegionTime` 注入日期和位阶，保留原 `CultureTimeSpanArg` 的格式计算；只增加每实例语言服务注入，避免修改其他地图的全局语言。区域行的时间回调属于标签实例，不写回共享资源。当前版原倒计时格式源码与历史已迁移的该文件逐字节一致，外围程序集并不相同；来源及适配哈希见[地区时间清单](../../assets/reference/official-map/mainline-region-time-source-manifest.json)。

完整原小地图工厂也已消费同一时间适配，保留本图43区的原时间字段。隔离 GUI Play Mode 已通过工厂创建本图实例，核对全部偏移、位阶高低与准确到期边界；另正常初始化本图当前原地区说明 prefab，实际查看原“鲸背岛一”名称与倒计时，等待原刷新任务解除时间锁，并核对重进、独立实例和释放。小地图完整数据响应仍未接通，此项为原标签和公共工厂的组合检查，详细边界见[公共模块](../features/mainline-map-modules.md)。

Unity 2022.3.57f1c2 内置屏隔离图形 Play Mode 已通过公共 `MainlineNativeGround` Interface 加载、释放和重新加载 Map15／16 原地面，检查原组件绑定、几何、URP／相机恢复，并实际查看两图各三张画面。鲸背岛以原入口坐标和显式地区夹具验证私有阴影、单区公开后的原 `MapEnvEffect.UnlockRegion`／区域灯演出及揭格回调，重新加载保留同一区域灯光状态。状态刷新本身不替代原区域激活动作；公共女神像路径已接入这项地面调用。另核对原标签倒计时文字网格，实际查看位阶4倒计时、到期自动解除时间锁、另一未到期地区及其位阶5取消时间锁的四张运行画面。到期检查只推进注入时钟并等待原 UniTask 刷新，不直接调用业务回调或视图刷新。隔离工程按依赖闭包携带原字体、材质、TMP Settings 及其 shader include，文字不以非空字符串代替渲染验证。

同一 Play Mode 使用本图实际43行地区配置检查各自14至32天偏移、边界前一秒／恰好到期、时间条件先于等级、较低／较高位阶和缺失日期分支。隔离角色档案通过现有 `SetPlayerProgress` 记录首次位阶4日期、重复赋值保留日期，并重新打开档案冷读核对。该夹具使用测试相机与显式角色／地区状态，无角色／HUD或模拟地图输入；不覆盖自然晋阶、正式地图入口、整图进度存档或 Windows Player。隔离工程不执行全工程校验；另在正式 Unity 工程执行导入／编译和 `ProjectTools.Validate` 通过，静态元数据检查已通过。

本批主工程 Unity 导入／编译、`ProjectTools.Validate` 和静态元数据检查通过；公共转换器回归核对 Map15 的469项、Map16 的1434项既有地面输出逐字节不变，地面与原时间标签 Play Mode 回归及画面检查通过。

本图交互配置已通过重复提取一致性、地区顺序、空奖励列表、区域刷新表与所有英雄结果引用检查；共享转换器七项规则测试覆盖六种原英雄、历史输出不变及不支持的周期组拒绝。静态元数据检查、主工程 Unity 导入／编译、`ProjectTools.Validate` 及实际配置加载通过。`ProjectTools.Validate` 通过共享档案 `Load(16)` 实际加载并核对六种原英雄、43个地区和同版来源身份；隔离工程另核对 Map10–15 历史配置，以及错误版本／地图身份／地区数量的拒绝；不包含地图画面或模拟交互。尚未运行本图正式雕像／资源流程，不能据此宣称其 Play Mode 或 Player 验证完成。

## 自定义实现

地区时间门槛采用用户确认的单机规则：角色首次达到位阶4时记录本地时间，14天后满足原 `LimitServerRank=4`／`LimitServerRankUnlockDayOffset=14` 条件；其他地区继续使用各自原配置的15至32天偏移，复用同一位阶4起算时间。角色超过位阶4立即满足这些时间条件。绳索准备剧情、等级及其他原条件继续保留。`OfflineCharacterRankRegionTime` 消费现有角色 `RankUnlockTimes`，不应用服务器合服加速；现有晋阶事务保留已有首次日期，正式地图宿主仍待接入。

独立地图准备的官方 `Silver/14/300秒` 配置由 [`prepare_map16_alone_configuration.py`](../../scripts/prepare_map16_alone_configuration.py) 解码并输出至 `Resources/Map16/world-alone-configuration.json`。用户确认单机准备时间为：准备开始后至少300秒；角色恰为位阶4时，还须距首次达到位阶4满14天，采用两者较晚时间；角色高于位阶4时免除14天等待，但仍须完成300秒。原绳索剧情、位阶、等级和其他条件继续保留。`OfflineMainlineProgressSession` 已实现准备状态、开始、到时解锁和提交时刷新。配置提取测试、全量静态检查、Unity 导入／编译、包含位阶4／14天边界与300秒最短时长场景的 `OfflineMainlineProgressValidation`，以及 `ProjectTools.Validate` 均通过。正式入口卡片与鲸背岛宿主尚未接线，未覆盖本图实际界面、Play Mode 流程或 Windows Player。

用户确认鲸背岛的6条手动剧情按登岛、NPC交互和离岛映射；本地适配具体采用：首次启程流程播放 `LookForWhaleIsland`；独立地图准备完成播放配置指定的 `DiscoverWhaleIsland`；实际进入Map16播放 `WhaleIslandArrive`；NPC10801交互播放 `SubmitRopes`（原图含交物品3及 Map16 登岛节点）；鲸岛神龛NPC8443交互播放 `WhaleIslandFinishLoop`（该NPC由自动完成图 `WhaleIslandFinish` 在神龛处生成）；独立地图离开播放配置指定的 `WhaleIslandLeave`。`WhaleIslandFinish` 保持原自动条件启动，各图仍按原 `MaxCount` 限制次数。NPC8443绑定依据是原完成图的生成节点，属于本地触发适配；公共宿主调用和完整剧情流程仍待接通。

沿用已确认的独立地图领奖方案：原客户端 `AloneWorldMapEntryItemContentCommonBase.FinshAward` 只发出 `OperPlayerAloneMapFinshAward(16)` 请求；单机按原奖励规则770／771发放钻石200、金币80000，奖励、剧情、随机状态与一次性领取记录同事务写盘，失败回滚，冷读后防重复。表现保留 `SourceOperate.AloneMapAward=222`、Popup 与原 `AwardPanel`。完整本地领取事务和原界面接线仍需实现与验证。

沿用已确认的踩格机关辅助方案：原客户端／Common 的 `StageResultAutoPuzzle／ResetPuzzle`、`MapPuzzleTerminalDataStepGrid／LockBase` 不含服务端执行器；辅助按原菜单扣 `325×1`，只解开 `LockLoEntityIds`。原菜单允许的重置只恢复该组踏板与辅助锁，不清除奖励实体状态或领取记录；完成锁后按原配置生成独立奖励、切换终端状态，不增设重置入口。五组原菜单的实际交互、事务、回滚和冷读仍需实现与验证。

## 尚未实现

公共剧情转换已修复 `CanHandInItemNode.HandInItemId` 与 `HandInItemNode.HandleItemId` 的字段差异：`SubmitRopes` 查询编号从0恢复为3，与绳索 `43039×5`、一次交付规则相连；Map16载荷只改变此字段。5项图归一化回归、运行时载荷检查及4项来源检查通过。正式宿主和交付 UI 尚未接通，未验证实际NPC交付、Play Mode或Player。

原鲸背岛入口卡片、坎斯汀 NPC10801 的准备／提交 `43039×5` 绳索／登岛／离岛剧情、全部任务和战斗、五组踩格机关及奖励770／771的完整原流程仍待接线。对应当前官方配置、故事图和消费路径须逐项核对，不能把历史转换记录当作当前已交付载荷。

本图尚未注册到公共正式宿主。鲸背岛7张原剧情图、94个节点、45条对白、6个选项、51个中文文本键、7份配对服务端记录、2个实际引用对白角色、3条Map16地图逻辑标签、1条绳索交付规则和0条固定奖励配置已从同版 `884_156692` APK 按实际所属的 `config_luban`、`config_luban_dialog`、`config_luban_map` bundle 提取至 `Assets/Official/World/Map/16/Source/story-source.json`；来源收据记录每张表的bundle、APK条目、对象／payload哈希，测试锁定版本与角色／地图标签表的bundle身份。`prepare_map16_story_source.py` 复用官方构造器，拒绝缺少图引用的角色、交付物或固定奖励配置；`test_prepare_map16_story_source.py` 的4项检查通过。`prepare_unity_map16_story_runtime.py` 使用公共 `normalize_story` 生成 `Resources/Map16/stories.json`，保留原图、节点、端口、对白、角色、选项、地图标签和交付物规则；输出为6张主图、1张子图、17个对白组，并由 `test_prepare_unity_map16_story_runtime.py` 校验收据、哈希和闭包。`test_prepare_map16_story_normalization.py` 另验证入口、唯一子图、节点原文、端口连接、完整对白及已确认触发映射的官方锚点，4项通过。共享 `Map13OfflinePlayerConditionEvaluator` 现允许 Map16 剧情节点使用官方 `MapLogicEntityEndByTag` 条件，并以当前本地完成标签状态判断；`ProjectTools.Validate` 增加该条件的匹配、反向条件及不匹配检查。两段官方演出引用的GUID仍未进入本图运行时绑定；触发映射已由用户确认，正式宿主仍未消费该载荷或注册故事数据。

`prepare_unity_map16_battle_data.py` 从同版 `884_156692` 提取70个阶段、9个战斗场景、46个怪物编组表和237个实体阶段绑定，输出 `Assets/Official/World/Map/16/map16-battles-source.json`；来源测试覆盖哈希、阶段／场景／编组闭包及剧情无战斗启动节点，3项通过。`prepare_unity_map16_battle_runtime.py` 复用公共怪物阵容转换及官方等级属性解码器，生成 `Resources/Map16/battle-runtime.json`，包含70个阶段与阵容、9个场景、38个实际引用怪物组、540个阵位和237个实体阶段绑定。逐阵位怪物索引与 `884_156692` 官方 `StageMonsterArrayInfoParser.GenerateMonsters` 一致；超出原怪物列表的编组阵位依官方异常处理不生成额外单位，原阶段数据仍保留。`test_prepare_unity_map16_battle_runtime.py` 校验同版哈希、阶段／阵容集合、场景和绑定数、输出收据。7张原剧情图均无 `StoryStartStageNode`。战斗载荷的生成不代表玩法已接通：Map16 已登记公共数据档案但 `IsRuntimeEntryReady=false`；官方怪物 Windows 视觉绑定、故事演出绑定、NPC／登岛／离岛触发和地图 HUD／正式宿主仍待迁移，不得用已生成战斗与剧情JSON宣称地图可玩。接下来须完成剩余配置及接线，再接通原HUD、地区开放与本地位阶日期、四类雕像与每日访问、探索度、白雾、资源刷新、独立大小地图及标记，按原 `CanExplore=false` 保留本图探寻差异，并验证完整场景交互、退出与存档恢复。

小地图使用公共模块确认的单本地分线配置，原分线数 `≤1` 时隐藏按钮，地区私有／公开状态与玩法保持原配置；规则及公共适配范围见[主线地图公共模块](../features/mainline-map-modules.md)。此配置尚未接入本图正式宿主。

完成相关静态检查、Unity 导入／编译、`ProjectTools.Validate`、实际 Play Mode 模拟输入和画面检查后，构建并真实运行 Windows Player，再发布到 `D:/Desktop/杖剑传说/杖剑传说.exe`。当前没有包含完整鲸背岛成果的发布结论。
