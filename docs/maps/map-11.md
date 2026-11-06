# 11 森之国

[总索引](../README.md)

状态：未完成

## 地图简介

11 森之国，范围按下列原配置与本地决策维护。

## 原始官方实现

原目录：坎斯汀大陆/森之国。场景类型：`map_world`；原区域配置77项，BGM配置11。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_unity_map11_base_ground.py` 补取固定版 `defaultpackage_assets_partition_mapground_11_mapground_11_bare.bundle` 的原 `Ground` Tilemap：17,752 格、55 张原 Sprite、55 批多边形，保留原 Tile 矩阵、锚点、颜色及 Unity 坐标；`Resources/Map11/BaseGround/geometry.bytes` SHA256 为 `f7e948cec6dce80d3fbe252715bfcdf8ed2b7b984621cb37d6f7e105889d0cba`。同一转换器读取原世界角色 `3001.prefab` 的模型偏移、缩放和渲染槽位，以及原 `WorldCamera.prefab` 的视野适配、裁剪面。当前地面沿用既有材质适配，原 URP 光照、水面与遮挡排序未完整恢复；不能据此声称场景完整还原。

依据：`unity/Assets/Official/World/Map/11/base-ground-source-manifest.json`。

Map11 女巫原界面沿固定版本 `871_156598` 的 `defaultpackage_assets_prefabs_ui_worldmap.bundle` 提取 `WorldWitchTouchDialog`（PathID `-3082449800091456431`）、`WorldStageInteractionPreviewDialogWitch`（PathID `8230908895915710719`）和 `WorldTouchDialogWitchPeriodicityInteraction`（PathID `640011520031964919`）。该 bundle 原始 SHA256 `b89571df3f7eca7a6d9bf9f7d6f03af49b38191d4f3324fbd40559fe70f9916a`，大小 1,186,915 字节。逐对象来源、原始引用、可选动画目标核对及输出哈希统一保存在 `unity/Assets/Official/World/Map/11/UI/source-manifest.json`；当前行为与验证边界见地图所属页。

Unity 森之国载荷固定使用 `871_156598`。`prepare_unity_map11_runtime_data.py` 将基础实体模板与地图 `Lo` 覆盖分别保留，生成 110 类实体组件、44 个故事图（666 节点）与四个子图（64 节点）、50 条地图对话、61 条选项、8 条固定奖励、24 条主线及 129 个战斗阶段；输入、官方客户端／共享程序集来源与输出哈希见 `official-map/map11-runtime-source-manifest.json`。`prepare_unity_map10_battle_data.py --map-id 11` 保留 57 个怪物编队数组、17 个场景和 591 个实体阶段绑定，来源见 `unity/Assets/Official/World/Map/11/battle-source-manifest.json`。

森之国 `5110` 动态怪物显示：`prepare_unity_map11_world_monsters.py` 沿固定版 `ECStageBehStageView.GetMonsterId`、`Formula.GetMainMonster`、`MapMonsterLoader` 和 `MapMonsterLoadHandler` 核验六个阶段绑定，以及 3212、3215、3216、3217、3218 的官方 `MonsterModel` prefab。原 `5110.prefab` 的 `ModelSlot`、`Shadow` 挂点及偏移均记录在来源清单；`MonsterShadow` prefab 跟随 `shadow` 骨骼，其 Sprite 从 APK 原 bundle 提取为 `unity/Assets/Official/World/Map/11/Resources/Map11/Textures/MonsterShadow.png`。官方 CDN／APK 来源 URL、bundle 哈希、CAB 名、path ID、对象与纹理哈希、缩放和复用的战斗骨骼映射见 `official-map/map11-world-monsters-source-manifest.json`；运行时显式从 `Resources/Map11/Textures/MonsterShadow` 加载。

原语音以 Windows 原生流式 AudioClip 绑定 `Resources/Map11/Audio/voices`；`Voice_StopAll` 保留原两项立即停止动作的顺序。音源静音，试听与真实输入尚未验证；来源凭据见[来源总表](../../assets/reference/SOURCES.md)。

`prepare_unity_map11_story_spatial.py` 从 APK 实际 DefaultPackage 清单定位 EC 索引及 113 类 Map11／本地玩家 prefab，严格读取原 `MapEntityTag`、`StoryNodeRegister`、玩家活动范围及距离设置，保留 bundle、对象和组件来源哈希；结果绑定 `Resources/Map11/story-spatial.json`，清单为 `official-map/map11-story-spatial-source-manifest.json`。原 `StoryUtils` 会根据动态 AttachAgent 骨骼可见性筛掉地图 NPC，这部分自制地图视图尚未复原，不能将空间查询记作完整 NPC 可见性迁移。

Map11 实体转换保留 105 类模型中的 24 类状态模型、64 份状态几何和空状态原始字节。补充纹理 `building_naka1`、`building_luzi` 从原 `6330_11_view` 的真实 SpriteRenderer 引用定位至 `defaultpackage_assets_image_mapentity_entity11_building_grave.bundle`（SHA256 `6b24112c0669a6a45628ee51d92a568a8eecedea97ab91eb12ba877a2ec263ae`）；重新提取 PNG 与既有文件逐字节一致。161 份几何、27 个 Spine 实例、203 张纹理及额外对象来源保存在 `unity/Assets/Official/World/Map/11/entity-source-manifest.json`，未修改原 APK／CDN 预期哈希。

Map11 地面、水面和放置实体按原材质、Renderer 与地形层级恢复；运行资源分别由地形、实体和水面来源清单记录。水面沿用原高度图与遮罩，Windows 资源通过本地目录加载。

`prepare_map11_lighting.py` 从固定 APK／校验 CDN 转换 MapGround_11 的原全局 Light2D、85 个地图可用显示 prefab 中的89盏局部灯和 FalloffLookupTexture 衰减；43个活跃 Water2 渲染器均核对为排序层16。`assets/official/map11-lighting.json`、衰减贴图及所有输入／输出哈希见 `official-map/lighting-11-source-manifest.json`；其余原版本缺包记录保持不变。

6530原两种idle为空，仅0→1的原一秒Start播放8个原粒子发射器；声音由原 `Map_Entity_MiniGame_Bonus` Wwise事件解析到media163983865，其1.5秒尾音按原无StopSoundEvent规则播放。真实PCK来源、bank／event／action／sound／media身份、输入与OGG输出哈希见 `official-map/audio-11-state-source-manifest.json`；保留冻结OGG，不因重新编码的容器序号不同替换现有音频哈希。

`prepare_map11_stone_states.py` 从原6503 prefab、Animator、Spine及附着轨道重建close／middle／open与各自loop；按原中断规则和normalized entry mix采样64组12fps本地网格动画。输出及原对象来源见 `official-map/stone-11-source-manifest.json`。`prepare_map11_logic_views.py` 核对五包清单、原实体显示表与模板，确认6533的413／3210／6105是原定义的逻辑节点，没有对应显示Prefab；`official-map/logic-11-source-manifest.json` 记录检索范围与原对象，不为这些节点绘制假素材。

`prepare_battle_stele.py` 将 APK 固定版本 `871_156598` 的 `stage_battle_stele`、`game_settings` 转换为 `assets/official/battle-stele.json`：65 条关卡记录，每条保留 4 档战力阈值，推荐索引为 2。该文件与其他官方配置转换结果集中存放；迁移仅变更路径，9,390 字节及 SHA256 `f3d6f8bc7ea42f115e07a98c87ab4ebac01c618c02b7bbac81dd267dfff0c239` 保持不变。官方表输入哈希、bundle／对象身份和输出路径见 `official-map/battle-stele-source-manifest.json`；地图 11／12／13 的来源清单同步引用新路径，运行时载荷和加载绑定不变。隔离验证按来源哈希读取 APK 原语言，保留现有地图载荷的后续增量。

`prepare_map11_dialogs.py` 从原 `stage_battle_stele` 表选择900／901／902／925／926／927，并按XXHash64读取原两份中文语言二进制的17个名称／说明／界面键，输出 `assets/official/map11-dialogs.json`。原关卡表、语言来源及哈希见 `official-map/dialogs-11-source-manifest.json`；界面复用原BattleSteleWorldStageDialog和已转换官方敌人模型。

Map11 原生资源通过显式本地清单加载；输入版本、对象身份及输出哈希见各所属来源清单。语言增量和跨模块共用资源由对应来源页维护。

不同阶数的历战精华匣／箱子按原Item.RawIcon共用图片，运行时对缺少独立ID绑定的9200／9217等解析到完整同名路径。白箱`ItemIcon/Other/itembox_white_5`与蓝箱`ItemIcon/Other/itembox_blue_5`复用已转换的`assets/official/sprites/itembox_white_5-9cc50ec3de.png`、`itembox_blue_5-fd88304460.png`；没有新增或重新绘制图像，没有修改图标目录或图片字节。

Map11 的家园资源图标按原 Sprite 对象绑定，来源与显式加载路径见[来源总表](../../assets/reference/SOURCES.md)。

存档选择界面：`scripts/prepare_official_save_selection.py` 复用启动页提取工具，从固定854官方 APK 恢复 `PlanesListPanel`、列表内嵌 `PlanesRoleItem` 与原启动模糊遮罩，共131个对象、37张 Sprite、16个原动画片段；保留原层级、锚点、TMP字体／描边、Sprite几何、RawImage UV、页签 Animator 与窗口显示曲线。角色头像复用原外观网格、摄影参数及待机姿势；本地槽位替代在线位面提供者。APK、bundle／CAB／对象、官方代码及载荷哈希见 [source-manifest.json](../../assets/reference/save-selection/source-manifest.json)，Unity原生转换、资源目录和显式绑定哈希见 [native-source-manifest.json](../../assets/reference/save-selection/native-source-manifest.json)。实际验证范围见[设置、拍照与本地存档](../features/settings-and-save.md)。

主线地图七类公共模块的原实现 Git 来源、官方客户端代码、模块输出及探寻页签原动画绑定见 [mainline-modules-source-manifest.json](../../assets/reference/official-map/mainline-modules-source-manifest.json)。沿用854固定版与既有官方载荷；仅拆分状态／生命周期和恢复四份原页签动画的名称及 prefab 引用，没有新增绘制素材。本地地图保持在活动场景，以保留原 FollowSceneActiveSelfController 的全局灯光控制，不改原灯光参数。

## 完整还原部分

正式入口的原世界加载、原玩家与放置实体状态同步及退出释放统一使用[主线地图公共模块](../features/mainline-map-modules.md)。该公共接线不代表本图完整表现与交互已完成。未验证跨图快照窗口或 Windows Player，整图状态仍为未完成。

本图完整小地图根按所选884版数据加载，包含531个原序列化对象、原接缝线与地区说明引用；本图数据采用240×180导航及77个地区。共享生命周期和状态适配见[主线地图公共模块](../features/mainline-map-modules.md)。正式离线响应、白雾／标记、HUD输入和地图存档仍待接通。来源见[当前完整小地图根](../../assets/reference/official-map/map11-current-minimap-root-source-manifest.json)。

Map11 地区小地图使用原 Grid、77 个地区插槽和 Tilemap，通过共享 `MainlineMiniMapRegions` 读取公开状态。正式小地图根、标记、白雾、HUD 输入和存档接线仍未完成；来源见[地区小地图清单](../../assets/reference/official-map/map11-current-minimap-regions-source-manifest.json)，共享行为见[主线地图公共模块](../features/mainline-map-modules.md)。

共用原生 UI 导入器补回 `6010_11_DialogIcon` 的 `GoddessDialogIcon` 原状态清单，未激活／已激活图标按原对象组互斥显示。不代表本图完整游玩或存档验证。

地图公共能力由 `OfficialMainlineMapRuntime` 组装 HUD、区域／雾、四类雕像、导航、体力、探寻、小地图七个独立状态模块；本图资源、剧情和进度按地图 ID 注入。模块边界、共享规则与未接入项见[主线地图公共模块](../features/mainline-map-modules.md)。按地图 ID 装载本图资源，沿用本图存档与回执。

Map11 UI 图片以原 Sprite 元数据导入；`OfficialMap11UiTools.RestoreSpriteMetadata` 可恢复 Texture2D、Single Sprite、无 mipmap／NPOT 缩放、Clamp、Bilinear 与无压缩 Alpha 设置。来源凭据见[来源总表](../../assets/reference/SOURCES.md)。

本图 UI 已从固定版本官方 prefab、程序集和资源绑定恢复为 37 份原生 panel、325 张 Sprite。`WorldCanvas` 保持官方根 Canvas、CanvasScaler、SafeArea、原始坐标和层级，修复 HUD 缩放与布局；头像使用官方角色模型、相机参数和排除皮肤列表生成，生命、等级、战力、货币／体力和固定任务内容接到本地状态。人物对话恢复原底板、左右立绘、说话人位置、明暗切换、选项、逐字显示及动画半程事件；首次点击补全当前句，之后才推进。右侧立绘的原 `RectMask2D` 为禁用状态，现按序列化状态还原，避免人物被错误裁掉。对话与选项期间按原流程隐藏地图 HUD。功能入口使用官方 `FunctionEntryDialog`，迁移 `GridLayoutGroupJSZX` 的间距、排列和自定义行数算法，恢复原按钮排列；没有本地服务器时钟区域或提示记录时使用官方关闭状态。功能入口和快捷消息恢复原 Animator 默认片段的初始状态，修复未收起的内嵌面板残留。首购 HUD 改为原 `FirstPayBtn` prefab，由原纵向布局定位，不再借用累计星值图标或手写坐标；仍沿用现有首购可见性业务查询。

小地图恢复原圆形遮罩材质、人物标记、地区名／坐标／探索度、全屏面板、缩放、拖拽边界与分类筛选。37 份原生标记 prefab 按官方实体／地图／区域／标签优先级绑定到 1,707 个放置点；标记偏移使用官方身体格中心，缩放与完成状态按原配置处理。`MiniMapWorldStageSwitcherSupport` 直接消费原世界阶段状态，传送点、战场、副本与圣兽标记按各自序列化状态切换，避免未激活／已激活图标叠加。筛选保存至所属档案的 `minimap-filter.json`，关闭、清空内存后重开可冷读恢复。跨地图页签沿 `MiniMapFullScreenPanel`／`MiniMapSwitchTabItem`，按官方 `MapInfo` 顺序筛选同组、已有访问记录且已迁移的 Map10／Map11；使用原 `ImgMapSwitch_0`／`_1`、标题、选中态及各图缩放参数。浏览其他地图不创建访问记录或移动角色，隐藏当前图角色和标记；关闭后恢复当前图。其他地图的离线实体标记、标记点击／详情、公共地区资源点提供器仍未完整接入，不能据此认定完整小地图功能已完成。

森之国 UI 宿主复用官方 HUD、全屏小地图、筛选、功能菜单、对话和选项 prefab；代码入口位于 `OfficialMainlineMapRuntime` 的 Hud、TopBar、Tasks、MiniMap、MiniMapTabs、MiniMapFilter、MiniMapMarks、Portrait 与 Functions 分部。地形／角色遮挡排序、水面缺块和原 URP 材质表现仍待恢复。原始对象与代码来源见 `UI/source-manifest.json`。

基础地面补回原 `MapGround_11_bare/GridGround/Ground` 的 17,752 个 Tile，以 55 张原 Sprite 的多边形、三角形、UV、颜色、Tile 矩阵及锚点生成 55 批原生网格，不改变原世界坐标。角色模型挂回原 `3001_view/Model/Offset/Player_Model_Map` 的 `(0.3, 0.3, 1)` 缩放、`(0, -0.5, -0.5)` 偏移，并以排序组适配原单 Renderer 的世界槽位，恢复角色显示。地图相机从原 `WorldCamera` 读取 SafeArea、适配优先级及裁剪面，沿 `OrthographicCameraResolutionAdaptor.AdaptOrthographicSize` 恢复视野和窗口比例，剧情结束恢复同一基准。转换器为 `prepare_unity_map11_base_ground.py`，显式加载 `Resources/Map11/BaseGround`；对象、bundle、原 shader、源码与输出哈希见 `base-ground-source-manifest.json`。该补载尚未恢复原 URP 光照、水面与完整排序，不代表整图渲染完成。

Map11 已接入地图加载、实体显示、踏板、战斗碑、故事路由、怪物和女神像的部分本地流程。已迁移的规则包含本地准入、回滚、持久化、到期、额度竞争、遗失女神属性及每日08:00边界。奖励结算窗、正常入口、完整主线、地形／排序、音频和存档恢复仍未完整接通。

已转换并放入 `unity/Assets/Official/World/Map/11/` 的官方载荷：地图定义含 77 区、81 面积配置、1,803 个放置实体及其一一对应的实体组件、导航和 8 条区域开放条件；绑定 110 条实体进度参数、247 条实体显示、128 条实体 UI、5 条阶段动作，以及地图、区域、逻辑实体配置。Map 11 有 17 个普通女神像（`EntityClassId=6010`），其所在区域各配置一组 `DropItems`；另有 49 个失落女神像（`EntityClassId=6011`），其所在区域各配置一组 `GoddessActivePropAwardDict`。1045～1160 范围含 24 条主线记录。地形几何为 14,427 批次，引用的 475 张官方纹理已复制至 `Resources/Map11/Textures/`；小地图为 85 批次并绑定 2 张官方纹理。输入版本固定为 `871_156598`，来源和输出哈希见该目录的 `*-source-manifest.json`。转换器为 `scripts/prepare_unity_map11_rendering.py`、`scripts/prepare_unity_map10_minimap.py --map-id 11` 和 `scripts/prepare_unity_map11_definition.py`。地形视图已支持原始 Map11 几何头格式，保留 Map10 默认格式。

实体载荷含 1,803 个放置记录、105 个模型绑定、161 份官方几何（97 份基础几何和 24 类模型的 64 份状态几何）、27 个 Spine 实例和 203 张实体贴图。状态 `-1` 保留原 `StateGameObjectConfig` 关闭所有切换对象后的常驻内容，空几何也保留来源文件；女神像激活切换原 0／1 状态。补入的 `building_naka1` 和 `building_luzi` 均从固定版本官方 `worldbuilding/6330_11_view` 的 Sprite 引用重新提取，与已有 PNG 逐字节一致；来源见 `entity-source-manifest.json`。`8210–8213` 世界奖励实体按官方 `Map/entity_map_display` 绑定到公共单物品模板；展示表有常规地面与浅水两个版本，当前载荷保留常规地面版本。原 9 个无静态模型实例属于 `5110` 和 `6533`：`6533` 的官方逻辑视图明确无显示实体；`5110` 由 `ECStageBehStageView` 按当前阶段编队取得主怪，再由 `MapMonsterLoader` 动态装载怪物模型。六个 `5110` 放置点现按各自阶段编队绑定 3212、3215、3216、3217、3218 五种官方骨骼，挂在原实体格位并播放官方 `idle_01`；这些地图模型 prefab 的根缩放均为 `(0.3, 0.3, 1)`。来源核验见 `assets/reference/official-map/map11-world-monsters-source-manifest.json`，转换器为 `scripts/prepare_unity_map11_world_monsters.py`。

战斗载荷包含 129 个放置阶段及魔女实体组 2000 的召唤阶段 20001，共 130 个阶段／展开编队，保留 57 个怪物编队数组、17 个战斗场景和 591 个实体阶段绑定，以及玩家等级／位阶动态阶段及官方等级属性缺行语义。`prepare_unity_map11_runtime_data.py` 已将 110 类基础实体组件、44 个故事图（666 个节点）、四个子图（64 个节点）、50 条地图对话、35 个逻辑标签、17 个对话角色、10 个可演出资源及 24 条主线写入本地 `Resources/Map11`。原放置组件是 `Lo` 覆盖，不能代替基础实体模板；基础组件与嵌套阶段列表保持官方顺序。

固定版主线目录包含 24 项森之国目标（1045～1160），覆盖地区探索、女神激活、“月影之森”实体及 1070 恶魔战场第 1 入口通关第 2 层。条件读取本地地图与幻想阶梯状态，奖励由任务领取事务提交，进度按官方层数加一规则显示。

森之国入口要求先完成并领取 Map10 主线任务1040。固定版任务要求成功开启10次 `chestId=6000` 普通祈愿宝箱；本地成功祈愿事务原子记录数量、奖励与消耗，重复回执不重复计数。地图内 `EntityClassId=6000` 放置实体不计入祈愿次数，已有 Map11 地区记录也不会绕过入口条件。完整前序任务路线和新档入口流程仍未完成。

`prepare_unity_map11_ui.py`、`map11_support_ui.py` 与 `map11_minimap_ui.py` 恢复原 UI、动态布局、TMP 字形和小地图标记，并显式加载到 `Resources/Map11/UI`。

森之国机关配置包含116个放置记录、九组条件锁和七座终端；本地会话保留原状态更新顺序、关联通知、奖励实体生成与存档恢复规则。机关动画和终端原界面仍未完整接入。




镜头位置查询沿用原 `GetEntityNode.GetEntityType` 分支：0为主角、4为放置ID、5为逻辑标签，并保留原位置偏移。周围对话中心按 `StorySettings.PlayerArroundRangeSize` 的2×3格查询地图及故事NPC；空范围或战斗中回退主角，中心距离超过5格也回退。NPC骨骼显隐筛选仍不完整；其他 `WorldCameraNode` 行为和NPC动画待恢复。

森之国默认 BGM 使用原媒体750073388，按 `Map_11_Play`、Stop、Pause、Resume事件循环，并保留Sine淡变参数1,000／250／250／500毫秒。音量读取现有音乐设置；区域／面积音乐表没有本图覆盖项。战斗音乐切换及其他音效仍待接入。

森之国44张故事图及四个子图引用的207个剧情语音事件已绑定本地流式音频。沿用原 TalkDialog／OptionTalkDialogPanel 规则：仅 Voice 行播放，换句先停止旧语音；纯文字、普通关闭和故事结束时停止，选项面板仍显示时保留当前语音，过期异步加载不覆盖当前行。音量读取现有语音设置；整段剧情演出及真实输入仍待接通。


共享地图定义、寻路／探索和演出代码已接入。战斗怪物头像覆盖Map10／Map11共38个原IconId；对白切换等待原动画半程事件，关闭或替换对白后旧回调不会覆盖新内容。森之国当前战斗状态和结算流程已接入公共运行时；地图HUD叠加、角色与镜头比例及场景排序仍未完整恢复。


森之国73份 `LightRegion` 原生阴影已绑定 `OfficialMainlineRegionFog`，保留原形状／falloff网格、衰减贴图、Multiply光照及女神解锁时2.01666665秒的消雾曲线。原资源不含地区21／28／57／62；日夜装饰、局部灯光和小地图未探索遮罩仍待接入。来源见[主线地图公共模块](../features/mainline-map-modules.md)。

### 森之国地图状态

以下为局部代码／资源接入，完整流程仍受本页缺项限制。





森之国已接入公共网格和原白雾。地图配置提供资源地址；载入时按地形地区及实体状态创建可见性，并将原压缩状态写入地图存档 `gridVisibility`。女神启动区域阴影渐退后清除原区域揭格列表，顺序对应 `MapEnvEffect.UnlockRegion`。新档使用 `TbMapWorldInfo.DefEntryId=101` 的(167,133)，已有坐标保留。



## 自定义实现

原线上组队按已确认原剧情 NPC 资格、等级、位阶和技能适配，不生成真人玩家。公共刷新使用本地时钟；普通路线保留真实败北、产出和等待，后期资格回访不能记作普通角色自然成长。

用户确认 49 座失落女神像（`EntityClassId=6011`）均须在对应地区探索度达到 100% 后激活；首次激活自动增加床的家园经验收益 1% 和小推车的罗拉（金币）收益 1%。两项分别使用 `HangUpExpAddPercent`（1001）和 `HangUpMoneyAddPercent`（1005），每座各增加定点值 `100`（1%）。`HangUpCartAllPercent`（1034）增加小推车全部产出，不能用于代替罗拉专用加成。49 座像分别对应 49 个不同地区，激活按地图／地区持久化，并以事件回执防止重复结算。

普通女神像需地区探索度达到 100% 后按原结果 204 与实体状态 209 激活；地区 `DropItems` 仅用于珍宝预览，不作为激活奖励。祝福等级沿用 `BaseBlessingLevel + GoddessCount * GoddessToBlessingLevel`。失落女神的独立探寻与领奖规则见[命运果实探寻](../features/world-exploration.md)。

此奖励是已确认的单机差异：固定版本官方 `map_region.GoddessActivePropAwardDict` 为全部 49 座配置经验 +1%，另一项按地区分为不同资源，仅 8 座配置罗拉 +1%。`OfflineGoddessPropSource` 在原属性流水线中替换 `LostGoddess` 来源，对森之国失落女神统一汇总 1001／1005 各 100，保留原表及哈希，避免与原表资源加成叠加；普通女神地区没有该字典，不计入家具奖励。家园实际收益与界面仍需运行核验。

女神像传送采用用户确认的单机落点：优先神像所在格；该格不可站立时取最近可站立格，等距按行、列确定，落点写入本地图位置存档。官方 `PlayerWorldTransferRequest` 只提交目标实体 ID，包内没有服务器选落点实现，因此此项是单机规则。森之国官方出口目标为 Map12；用户已确认不迁移 Map12，后续按主线地图逐步扩展。

机关终端结果 254／258 的服务器类为空，单机按用户确认依关联锁配置满足解锁状态，重置恢复机关初始状态和踏板计数。锁 413 的官方结果仍保留对 423 设置状态 1 的原始数据；重新反编译并核对原始 EC、MapInfoWrap、客户端实体定位与生成调用后，423 无放置定义或生成入口。用户确认仅跳过这条无目标更新，继续执行生成奖励实体 401 等其余结果；其他未知目标仍报错，不套用此例外。

用户确认一键解谜成功时消耗一张物品 325，重置免费，保留原固定奖励 45 首次赠送的一张。原剧情称其为“黄金甲虫”，物品表中的正式物品名为“破局之匙”，两处原文分别保留。购买券时晨星不足会打开兑换；因原 `ShopBuyIntent` 关闭确认框后没有通知 `ItemQuikBuyNode`，用户确认单机在兑换／商店关闭后走原故事“购买失败”分支，返回机关选项，避免剧情一直等待。

`SelectorNode`／`SwitchNode` 的客户端只消费服务器返回的 `SelectedIndex`，包内没有选择实现。用户确认单机按原配置顺序取首个为真的条件或首个与输入相等的结果，均未匹配时走原 `Exit`；结果比较保留 `SerializableObj.Compare` 的原始类型转换。规则用于世界树、援助和魔女剧情；分支路由接入不代表魔女激活状态和整段剧情已经完成。

`GetMapWitchExploreInfoNode` 原本读取服务端 `ActiveCount`；单机沿用户确认规则，使用 `OfflineMainlineProgressSession.ActivatedWitches` 记录的跨地图去重总数。Map11 两处 6620（放置 ID 2801、6901）接入原书页、结果预览、阶段成功结算和周期访问；预览不发奖，成功后按激活前全图计数选择原 `WitchInfo` 并处理奖励、HP、状态或实体组。完整剧情与召唤战斗循环仍缺。

按用户确认，英雄／魔女共用每日一次访问额度，访问记录、道具和待战斗效果在同一本地事务中写入。状态退出游戏后保留，进入下一场任意战斗生效，战斗结束后清除；HP 减少按入场最大生命百分比结算，`-100%` 至少保留 1 HP。首次剧情激活与周期访问沿原阶段图分别处理。普通女神／失落女神的探索激活不占该每日额度；Map11 没有英雄雕像放置点，英雄界面与随机增益入口仍待所属地图迁移。

### 已确认的本地接线与边界

机关规则沿用原 EC／Lo 配置中的九组锁与七座终端；锁 413 对缺失实体 423 的结果保留为单机跳过例外，不生成替代实体或修改官方数据。

终端物品 325 的 `quick_buy` 行从同版 APK 的 `config_luban` 提取，保留 50 晨星、每次一张、无购买次数上限及物品 4013 持有条件。剧情分支清单记录 `SelectorNode`、`SwitchNode`、`StoryGraph`、布尔条件及 `SerializableObj` 原代码哈希；按用户确认补全服务器缺失的首项匹配规则，未改动故事图的端口、顺序或默认出口。

剧情来源保留 `GetEntityBaseNode`／`GetEntityPosNode` 以及 `GetMapWitchExploreInfoNode`／`WorldWitchTouchUI` 的调用关系；魔女激活总数按单机账本计算，魔女界面与完整主线仍未完成。

`prepare_unity_map11_runtime_data.py` 沿固定版 `871_156598` 的实体 6620 EC 组件、`WitchInfo`、`map_periodicity_random_award_interaction` 与 APK 内 `reward_rule.g.bytes`，保留 14 条全图激活计数规则、七条周期奖励权重，以及原结果组件类型／索引；实体组 2000 解析到官方召唤阶段 20001，战斗载荷由 129 个放置阶段补为 130 个。输入对象、版本、奖励原始载荷及输出哈希见 `official-map/map11-runtime-source-manifest.json` 和 `unity/Assets/Official/World/Map/11/battle-source-manifest.json`，显式加载为 `Resources/Map11/stories` 与 `Resources/Map11/battle-runtime`。缺失服务端的结果结算、共享每日额度、下一场战斗状态及 HP 至少保留 1 点均按用户确认处理，不能标作服务器源码还原。

`prepare_unity_mainline_tasks.py` 生成 33 项主线目录与 10,000 条好感门槛，由 `OfficialMainlineTaskCatalog` 加载。Map11 入口沿用户确认接入任务 1040 的领取门槛，祈愿宝箱计数随奖励／消耗事务保存；来源见 `mainline-tasks-source-manifest.json`。

## 尚未实现

- Map11 完整入口、场景渲染、HUD、常规移动／点击、退出返回和存档冷读仍未接通；当前共享运行时及局部玩法接线不代表整图可玩。
- 森之国战斗场景的 HUD 遮挡、角色与镜头比例、场景缺块和排序、完整战斗演出仍待恢复。
- 九组踏板、六块战斗碑、森林老人／世界树／狮鹫剧情，以及 1045～1160 主线的完整领奖与中断恢复仍待接入。
- 入口任务 1040 的前置条件、新档祈愿计数与进入流程尚未完整连接；出口 6001 的个人资格、全区激活及公共化条件仍待落实。
- 探索里程碑的阈值、奖励和原 UI 对象未在当前官方来源中取得；命运果实的领取与探索事件也需单独接通，不推定奖励规则。
- 完整小地图标记、跨图详情、公共资源点及除关闭外的功能菜单仍待接入。

当前范围为共享地图数据、位置／状态存档以及已接入的局部交互；尚无完整 Map11 游玩流程或对应 Windows Player 发布。

## 代码与资源入口

官方转换数据：[regions/11.json](../../assets/official/regions/11.json)、[adventure.json](../../assets/official/adventure.json)、[map11-rendering.json](../../assets/official/map11-rendering.json)、[map11-lighting.json](../../assets/official/map11-lighting.json)、[map11-stone-animations.json](../../assets/official/map11-stone-animations.json)、[map11-logic-views.json](../../assets/official/map11-logic-views.json)、[map11-dialogs.json](../../assets/official/map11-dialogs.json)。Unity 载荷：[map11.json](../../unity/Assets/Official/World/Map/11/map11.json)、[OfficialMap11Tools](../../unity/Assets/Game/Editor/OfficialMap11Tools.cs)。

Unity 共享地图接入：[OfficialWorldMapAssetCatalog](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapAssetCatalog.cs)、[OfficialWorldMapSession](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapSession.cs)。主线公共运行时：[OfficialMainlineMapRuntime](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs)。离线激活与奖励属性：[OfflineMainlineProgressSession](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs)、[OfflineCharacterConfiguredPropSource](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterConfiguredPropSource.cs)。官方地图逻辑与特效参考：[MyPlayableManager](../../.tools/source-packages/official-code/playable-manager-source/GameFramework.MyPlayableManager.decompiled.cs)。
