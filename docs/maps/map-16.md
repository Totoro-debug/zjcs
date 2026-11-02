# 16 鲸背岛

[总索引](../README.md)

状态：未完成

## 地图简介

鲸背岛是带独立入口的可交互大地图。迁移范围包含原地面与 HUD、地区与解锁、雕像、网格白雾、探索度、命运探寻、大小地图与标记、体力资源与刷新，以及本图剧情、机关、战斗和本地存档。公共模块见[主线地图公共模块](../features/mainline-map-modules.md)，共享规则见[世界地图](../features/world-map.md)。

## 原始官方实现

当前配置按所选 `884_156692` 的 APK／匹配版本缓存读取，历史 `871_156598` 输入保留独立来源，不据旧文档中的文件名认定资源已存在。原地图 ID／地图组为16，场景为 `map_world`，BGM16，后继地图14。地面地址为 `Map_16/MapGround_16`，独立小地图为 `Map_16/MiniMap/MiniMap_16`，小地图配置 ID4；原 `CanExplore=false`，支持私有地区，公共地区和两种组队开关均关闭。

原 `MapRootEntityLiteSingleInfo` 给出140×120网格、原点 `(-80.5,-70.5,0)`、角色3001、成员3002和35步寻路上限。`MapInfoWrap` 给出43区及各自探索点上限、1021个实体的地区归属、刷新组、公私有支持、初始放置与阶段。默认入口为101，网格 `(20,27)`；不从位置猜测实体所属地区。角色原 `GridMoverComponent` 引用移动动作22021、可通行类型键3／服务器覆盖键1；原 `entity_grid_mover` 表对应 `[0,6]`／`[7]`，移动掩码65按原位操作生成。

地区表保留各区原等级、子位阶、奖励与相邻区。时间条件包含无时间限制和位阶4加14至32天的不同偏移；不得把所有地区统一改成14天。原 `Common.MapRegionInfo` 保留时间条件先于等级的判定、超过指定服务器位阶立即通过、缺少开放日期时关闭等分支；原 `MapUtils.TryGetMapUnlockTimeByServerRank` 在开放日期上 `AddDays(offset)`。本地日期来源的已确认替代见下文。

地面 prefab 的原层级含4224个 Unity 对象，引用区域灯、世界标签、水面及 `SeaRendererReplacement`。其 `Water_map13` 是实际引用的共享水面配置，继续按原 CAB／PPtr 恢复；名称含13不表示改用另一张地图的配置。独立小地图还引用43份 `MinimapRegionTilemap/RegionGround_*` 原 prefab，尚待原生转换与接线。

## 完整还原部分

尚无本图完整 Unity 游玩流程的完成结论。

局部数据入口由 [`prepare_mainline_map_definition.py`](../../scripts/prepare_mainline_map_definition.py) 按地图 ID 提取，输出 `Assets/Official/World/Map/16/map16.json` 及 `Source/` 中的导航、放置、完整 EC、63份递归模板／动作和原配置表。它对比原导航／MapInfoWrap／EC 的身份与实体闭包，核对43区探索上限，验证消费的109份已准备官方源码；不填造默认实体、地图条件或移动规则。来源见[地图定义清单](../../assets/reference/official-map/map16-definition-source-manifest.json)。

公共 `GroundBinding` 接收 `GroundSource` 的地图 ID、原 prefab 地址和 bundle，复用 Map15 的原生依赖恢复路径。Map15 已安装 GUID 保持原值，鲸背岛使用所属地图的 GUID 范围，避免两图复制的载荷互相覆盖。Map16 当前输出位于 `Assets/Official/World/Map/16/NativeCurrent`，资源入口为 `Resources/Map16/Native/current-ground`，含1434个资产／meta输出。脚本只显式绑定已迁移的官方类；原纹理、Windows shader、材质、网格及序列化字段按选定版本和各自哈希恢复。来源见[地面清单](../../assets/reference/official-map/map16-current-ground-source-manifest.json)。

[`prepare_mainline_map15_world_view.py --map-id 16`](../../scripts/prepare_mainline_map15_world_view.py) 读取原3001模型／偏移／排序和原 `WorldCamera` 投影／适配参数，输出 `Resources/Map16/BaseGround/player-view` 与 `Native/region-label-data`。标签按原地区 ID 索引探索上限，保持原表枚举顺序，核对47项实际语言键；公共标签样式、位阶映射和字体仍分别保留历史输入凭据，不声称整个版本等价。来源见[模型、镜头与标签清单](../../assets/reference/official-map/map16-world-view-source-manifest.json)。

公共网格输入已从当前所选版本单独提取：140×120网格、43份区域揭格列表、61份身体模板、1021个实体放置，资源为 `Resources/World/Mainline/Visibility/Map16/visibility` 与 `bodies`。原 `MapRoot_16` 完整雾层 subtree 与依赖为 `Resources/World/Mainline/Fog/Map16`。来源分别见[公共网格输入](../../assets/reference/official-map/mainline-grid-visibility-data-source-manifest.json)和[公共雾层](../../assets/reference/official-map/mainline-grid-fog-source-manifest.json)。这些当前版载荷尚未接入本图正式宿主。

Unity 2022.3.57f1c2 内置屏隔离图形 Play Mode 已通过公共 `MainlineNativeGround` Interface 加载、释放和重新加载 Map15／16 原地面，检查原组件绑定、几何、URP／相机恢复，并实际查看两图各三张画面。鲸背岛以原入口坐标和显式地区夹具验证私有阴影、单区公开后的原 `MapEnvEffect.UnlockRegion`／区域灯演出及揭格回调，重新加载同一地区状态后画面一致。状态刷新本身不替代原区域激活动作；公共女神像路径已接入这项地面调用。该夹具使用测试相机、无角色／HUD、无模拟地图输入，也不验证位阶计时、完整正式宿主、存档或 Windows Player；隔离工程未执行全工程校验；另在正式 Unity 工程执行导入／编译和 `ProjectTools.Validate` 通过，静态元数据检查通过（87,399 GUID）。

## 自定义实现

地区时间门槛采用用户确认的单机规则：角色首次达到位阶4时记录本地时间，14天后满足原 `LimitServerRank=4`／`LimitServerRankUnlockDayOffset=14` 条件；其他地区继续使用各自原配置的15至32天偏移，复用同一位阶4起算时间。角色超过位阶4立即满足这些时间条件。绳索准备剧情、等级及其他原条件继续保留。此方案尚未接入本图正式宿主或完成存档与运行验证。

沿用已确认的独立地图领奖方案：原客户端 `AloneWorldMapEntryItemContentCommonBase.FinshAward` 只发出 `OperPlayerAloneMapFinshAward(16)` 请求；单机按原奖励规则770／771发放钻石200、金币80000，奖励、剧情、随机状态与一次性领取记录同事务写盘，失败回滚，冷读后防重复。表现保留 `SourceOperate.AloneMapAward=222`、Popup 与原 `AwardPanel`。完整本地领取事务和原界面接线仍需实现与验证。

沿用已确认的踩格机关辅助方案：原客户端／Common 的 `StageResultAutoPuzzle／ResetPuzzle`、`MapPuzzleTerminalDataStepGrid／LockBase` 不含服务端执行器；辅助按原菜单扣 `325×1`，只解开 `LockLoEntityIds`。原菜单允许的重置只恢复该组踏板与辅助锁，不清除奖励实体状态或领取记录；完成锁后按原配置生成独立奖励、切换终端状态，不增设重置入口。五组原菜单的实际交互、事务、回滚和冷读仍需实现与验证。

## 尚未实现

原鲸背岛入口卡片、坎斯汀 NPC10801 的准备／提交 `43039×5` 绳索／登岛／离岛剧情、全部任务和战斗、五组踩格机关及奖励770／771的完整原流程仍待接线。对应当前官方配置、故事图和消费路径须逐项核对，不能把历史转换记录当作当前已交付载荷。

本图尚未注册到公共正式宿主。须接通原 HUD、地区开放与本地位阶日期、四类雕像与每日访问、探索度、白雾、资源刷新、独立大小地图及标记，按原 `CanExplore=false` 保留本图探寻差异，并验证场景音画、退出与存档恢复。

完成相关静态检查、Unity 导入／编译、`ProjectTools.Validate`、实际 Play Mode 模拟输入和画面检查后，构建并真实运行 Windows Player，再发布到 `D:/Desktop/杖剑传说/杖剑传说.exe`。当前没有包含完整鲸背岛成果的发布结论。
