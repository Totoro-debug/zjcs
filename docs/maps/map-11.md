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

Map11 女巫原界面沿固定版本 `854_156383` 的 `defaultpackage_assets_prefabs_ui_worldmap.bundle` 提取 `WorldWitchTouchDialog`（PathID `-3082449800091456431`）、`WorldStageInteractionPreviewDialogWitch`（PathID `8230908895915710719`）和 `WorldTouchDialogWitchPeriodicityInteraction`（PathID `640011520031964919`）。该 bundle 原始 SHA256 `b89571df3f7eca7a6d9bf9f7d6f03af49b38191d4f3324fbd40559fe70f9916a`，大小 1,186,915 字节。逐对象来源、原始引用、可选动画目标核对及输出哈希统一保存在 `unity/Assets/Official/World/Map/11/UI/source-manifest.json`；当前行为与验证边界见地图所属页。

Unity 森之国载荷固定使用 `854_156383`。`prepare_unity_map11_runtime_data.py` 将基础实体模板与地图 `Lo` 覆盖分别保留，生成 110 类实体组件、44 个故事图（666 节点）与四个子图（64 节点）、50 条地图对话、61 条选项、8 条固定奖励、24 条主线及 129 个战斗阶段；输入、官方客户端／共享程序集来源与输出哈希见 `official-map/map11-runtime-source-manifest.json`。`prepare_unity_map10_battle_data.py --map-id 11` 保留 57 个怪物编队数组、17 个场景和 591 个实体阶段绑定，来源见 `unity/Assets/Official/World/Map/11/battle-source-manifest.json`。

森之国 `5110` 动态怪物显示：`prepare_unity_map11_world_monsters.py` 沿固定版 `ECStageBehStageView.GetMonsterId`、`Formula.GetMainMonster`、`MapMonsterLoader` 和 `MapMonsterLoadHandler` 核验六个阶段绑定，以及 3212、3215、3216、3217、3218 的官方 `MonsterModel` prefab。原 `5110.prefab` 的 `ModelSlot`、`Shadow` 挂点及偏移均记录在来源清单；`MonsterShadow` prefab 跟随 `shadow` 骨骼，其 Sprite 从 APK 原 bundle 提取为 `unity/Assets/Official/World/Map/11/Resources/Map11/Textures/MonsterShadow.png`。官方 CDN／APK 来源 URL、bundle 哈希、CAB 名、path ID、对象与纹理哈希、缩放和复用的战斗骨骼映射见 `official-map/map11-world-monsters-source-manifest.json`；运行时显式从 `Resources/Map11/Textures/MonsterShadow` 加载。

`prepare_unity_map11_dialogue_audio.py` 同样先查 APK 实际内容，再核对固定版云端语音包，验证 207 个剧情事件的 Play 动作、原媒体切片和已转换 Vorbis 哈希；`Voice_StopAll` 的两个立即停止动作保留原调用顺序。原语音以 Windows 原生流式 AudioClip 绑定 `Resources/Map11/Audio/voices`，来源、代码及输入输出哈希见 `official-map/map11-dialogue-audio-source-manifest.json`。带图形的后台 Play Mode 检查了全部导入、实际主图／子图换句与关闭、过期加载取消及语音音量，音源静音，未进行试听或真实输入检查。

`prepare_unity_map11_story_spatial.py` 从 APK 实际 DefaultPackage 清单定位 EC 索引及 113 类 Map11／本地玩家 prefab，严格读取原 `MapEntityTag`、`StoryNodeRegister`、玩家活动范围及距离设置，保留 bundle、对象和组件来源哈希；结果绑定 `Resources/Map11/story-spatial.json`，清单为 `official-map/map11-story-spatial-source-manifest.json`。Play Mode 核对了 6101 世界树注册和主角回退位置。原 `StoryUtils` 会根据动态 AttachAgent 骨骼可见性筛掉地图 NPC，这部分自制地图视图尚未复原，不能将空间查询记作完整 NPC 可见性迁移。

Map11 实体转换保留 105 类模型中的 24 类状态模型、64 份状态几何和空状态原始字节。补充纹理 `building_naka1`、`building_luzi` 从原 `6330_11_view` 的真实 SpriteRenderer 引用定位至 `defaultpackage_assets_image_mapentity_entity11_building_grave.bundle`（SHA256 `6b24112c0669a6a45628ee51d92a568a8eecedea97ab91eb12ba877a2ec263ae`）；重新提取 PNG 与既有文件逐字节一致。161 份几何、27 个 Spine 实例、203 张纹理及额外对象来源保存在 `unity/Assets/Official/World/Map/11/entity-source-manifest.json`，未修改原 APK／CDN 预期哈希。

地图11继续使用固定 manifest `854_156383` 的 APK／匹配 CDN。`prepare_map11_rendering.py` 直接读取 `MapGround_11_bare.prefab` 的活跃 Renderer、原材质和原输入对象，不再依赖临时检查输出。转换1188个地形Sprite、67510个Tile、192个Mesh与1803个放置实体；Ground1按原高度图保留水面上下两遍，Water2的原Mask按64采样／世界单位转换为本地纹理。`map11-rendering.json`、两份11区域文件、Mask及复用纹理的对象／输入输出哈希见 `official-map/materials-11-source-manifest.json`，历史区域／水面来源保留。

`prepare_map11_lighting.py` 从固定 APK／校验 CDN 转换 MapGround_11 的原全局 Light2D、85 个地图可用显示 prefab 中的89盏局部灯和 FalloffLookupTexture 衰减；43个活跃 Water2 渲染器均核对为排序层16。`assets/official/map11-lighting.json`、衰减贴图及所有输入／输出哈希见 `official-map/lighting-11-source-manifest.json`；其余原版本缺包记录保持不变。

6530原两种idle为空，仅0→1的原一秒Start播放8个原粒子发射器；声音由原 `Map_Entity_MiniGame_Bonus` Wwise事件解析到media163983865，其1.5秒尾音按原无StopSoundEvent规则播放。真实PCK来源、bank／event／action／sound／media身份、输入与OGG输出哈希见 `official-map/audio-11-state-source-manifest.json`；保留冻结OGG，不因重新编码的容器序号不同替换现有音频哈希。

`prepare_map11_stone_states.py` 从原6503 prefab、Animator、Spine及附着轨道重建close／middle／open与各自loop；按原中断规则和normalized entry mix采样64组12fps本地网格动画。输出及原对象来源见 `official-map/stone-11-source-manifest.json`。`prepare_map11_logic_views.py` 核对五包清单、原实体显示表与模板，确认6533的413／3210／6105是原定义的逻辑节点，没有对应显示Prefab；`official-map/logic-11-source-manifest.json` 记录检索范围与原对象，不为这些节点绘制假素材。

`prepare_battle_stele.py` 将 APK 固定版本 `854_156383` 的 `stage_battle_stele`、`game_settings` 转换为 `assets/official/battle-stele.json`：65 条关卡记录，每条保留 4 档战力阈值，推荐索引为 2。该文件与其他官方配置转换结果集中存放；迁移仅变更路径，9,390 字节及 SHA256 `f3d6f8bc7ea42f115e07a98c87ab4ebac01c618c02b7bbac81dd267dfff0c239` 保持不变。官方表输入哈希、bundle／对象身份和输出路径见 `official-map/battle-stele-source-manifest.json`；地图 11／12／13 的来源清单同步引用新路径，运行时载荷和加载绑定不变。隔离验证按来源哈希读取 APK 原语言，保留现有地图载荷的后续增量。

`prepare_map11_dialogs.py` 从原 `stage_battle_stele` 表选择900／901／902／925／926／927，并按XXHash64读取原两份中文语言二进制的17个名称／说明／界面键，输出 `assets/official/map11-dialogs.json`。原关卡表、语言来源及哈希见 `official-map/dialogs-11-source-manifest.json`；界面复用原BattleSteleWorldStageDialog和已转换官方敌人模型。

`register_map_assets.py --map11` 仅登记上述独立来源收据验证的输出，保留其余模块绑定与历史来源；不以当前文件哈希覆盖未核对的预期值。既有位阶教学中文增量另由 `--localization` 逐键核对两份原语言输入后登记，中央来源保留旧摘要和原增量链。资源进入显式本地加载清单及匹配素材包，旧APK／CDN预期哈希和原版本185个缺项保留。

登记发现既有 `season-lifecycle.json`／`season-lifecycle-ui.json` 的中央摘要停留在较早转换。`--season-lifecycle` 重新核对固定 APK 中的原表、完整构造器、中文二进制、客户端代码及序列化输入，再登记独立 `season-lifecycle/source-manifest.json` 已记录的现有输出；原摘要由该模块 `history/` 中真实旧来源证明并保留在中央 `registration_sources.previous_data`。没有重新绘制、替换现有素材或改写原包哈希。

不同阶数的历战精华匣／箱子按原Item.RawIcon共用图片，运行时对缺少独立ID绑定的9200／9217等解析到完整同名路径。白箱`ItemIcon/Other/itembox_white_5`与蓝箱`ItemIcon/Other/itembox_blue_5`复用已转换的`assets/official/sprites/itembox_white_5-9cc50ec3de.png`、`itembox_blue_5-fd88304460.png`；没有新增或重新绘制图像，没有修改图标目录或图片字节。

两图来自固定854_156383的校验CDN包`defaultpackage_assets_image_itemicon_other_homeresource_noatlas.bundle`，MD5 `88c8ec63019259f1e41216df57277da1`；原Sprite对象分别为1938551758088215332与-661215029578351316。原URL、bundle SHA256、Sprite解码／atlas裁切及输出SHA256均沿用[原城市素材来源](../../assets/reference/official-city/source-manifest.json)的resources条目；显式加载绑定和既有素材包已包含两图。此项仅补运行时物品与已有原图片的对应关系，原来源和历史记录保留。

存档选择界面：`scripts/prepare_official_save_selection.py` 复用启动页提取工具，从固定854官方 APK 恢复 `PlanesListPanel`、列表内嵌 `PlanesRoleItem` 与原启动模糊遮罩，共131个对象、37张 Sprite、16个原动画片段；保留原层级、锚点、TMP字体／描边、Sprite几何、RawImage UV、页签 Animator 与窗口显示曲线。角色头像复用原外观网格、摄影参数及待机姿势；本地槽位替代在线位面提供者。APK、bundle／CAB／对象、官方代码及载荷哈希见 [source-manifest.json](../../assets/reference/save-selection/source-manifest.json)，Unity原生转换、资源目录和显式绑定哈希见 [native-source-manifest.json](../../assets/reference/save-selection/native-source-manifest.json)。实际验证范围见[设置、拍照与本地存档](../features/settings-and-save.md)。

主线地图七类公共模块的原实现 Git 来源、官方客户端代码、模块输出及探寻页签原动画绑定见 [mainline-modules-source-manifest.json](../../assets/reference/official-map/mainline-modules-source-manifest.json)。沿用854固定版与既有官方载荷；仅拆分状态／生命周期和恢复四份原页签动画的名称及 prefab 引用，没有新增绘制素材。

原102女神已通过正式地图指针／原按钮激活及冷读，实际画面恢复明亮地形；本地地图保持在活动场景，以保留原 FollowSceneActiveSelfController 的全局灯光控制，不改原灯光参数。

## 完整还原部分

共用原生 UI 导入器补回 `6010_11_DialogIcon` 的 `GoddessDialogIcon` 原状态清单，未激活／已激活图标按原对象组互斥显示。两种竖屏尺寸的弹窗按钮模拟点击及图标切换回归通过；该回归使用独立界面夹具，不代表本图完整游玩或存档验证。

地图公共能力由 `OfficialMainlineMapRuntime` 组装 HUD、区域／雾、四类雕像、导航、体力、探寻、小地图七个独立状态模块；本图资源、剧情和进度按地图 ID 注入。模块边界、共享规则与未接入项见[主线地图公共模块](../features/mainline-map-modules.md)。按地图 ID 装载本图资源，沿用本图存档与回执。

UI素材目录的385张Sprite已核对PNG的SHA256、稳定GUID、原尺寸、PPU、枢轴和边框，其中18份仅含GUID的 `.meta` 已补全。`OfficialMap11UiTools.RestoreSpriteMetadata` 复用原Sprite转换步骤，可独立修复导入设置；明确使用Texture2D、Single Sprite、无mipmap／NPOT缩放、Clamp、Bilinear和无压缩Alpha，避免不完整元数据将图片识别为Cube纹理。Unity `2022.3.57f1c2` 实际导入及Sprite对象／尺寸校验通过，现有PNG、prefab、目录绑定和GUID均保留。输入清单、转换工具及18份元数据的前后哈希见 `assets/reference/official-map/map11-sprite-metadata-source-manifest.json`；该项验证范围为元数据与Editor导入。

本图 UI 已从固定版本官方 prefab、程序集和资源绑定恢复为 37 份原生 panel、325 张 Sprite。`WorldCanvas` 保持官方根 Canvas、CanvasScaler、SafeArea、原始坐标和层级，修复 HUD 缩放与布局；头像使用官方角色模型、相机参数和排除皮肤列表生成，生命、等级、战力、货币／体力和固定任务内容接到本地状态。人物对话恢复原底板、左右立绘、说话人位置、明暗切换、选项、逐字显示及动画半程事件；首次点击补全当前句，之后才推进。右侧立绘的原 `RectMask2D` 为禁用状态，现按序列化状态还原，避免人物被错误裁掉。对话与选项期间按原流程隐藏地图 HUD。功能入口使用官方 `FunctionEntryDialog`，迁移 `GridLayoutGroupJSZX` 的间距、排列和自定义行数算法，恢复原按钮排列；没有本地服务器时钟区域或提示记录时使用官方关闭状态。功能入口和快捷消息恢复原 Animator 默认片段的初始状态，修复未收起的内嵌面板残留。首购 HUD 改为原 `FirstPayBtn` prefab，由原纵向布局定位，不再借用累计星值图标或手写坐标；仍沿用现有首购可见性业务查询。

小地图恢复原圆形遮罩材质、人物标记、地区名／坐标／探索度、全屏面板、缩放、拖拽边界与分类筛选。37 份原生标记 prefab 按官方实体／地图／区域／标签优先级绑定到 1,707 个放置点；标记偏移使用官方身体格中心，缩放与完成状态按原配置处理。`MiniMapWorldStageSwitcherSupport` 直接消费原世界阶段状态，传送点、战场、副本与圣兽标记按各自序列化状态切换，避免未激活／已激活图标叠加。筛选保存至所属档案的 `minimap-filter.json`，关闭、清空内存后重开可冷读恢复。跨地图页签沿 `MiniMapFullScreenPanel`／`MiniMapSwitchTabItem`，按官方 `MapInfo` 顺序筛选同组、已有访问记录且已迁移的 Map10／Map11；使用原 `ImgMapSwitch_0`／`_1`、标题、选中态及各图缩放参数。浏览其他地图不创建访问记录或移动角色，隐藏当前图角色和标记；关闭后恢复当前图。其他地图的离线实体标记、标记点击／详情、公共地区资源点提供器仍未完整接入，不能据此认定完整小地图功能已完成。

Unity `2022.3.57f1c2` 已执行带图形的后台 Play Mode，实际查看 HUD、全屏小地图、筛选、功能菜单、单人及双人对话和选项画面，检查世界阶段标记 0／1／2 三种状态的互斥图标分支及跨图页签的未访问过滤、Map10／Map11 切换、位置与访问记录不变及关闭恢复，并检查筛选切换／冷读恢复、逐字显示／首击补全文本、动画半程换句、HUD 遮挡和右侧立绘遮罩状态。该项调用脚本回调，未执行真实鼠标／键盘／拖拽，Windows Player 尚未构建验证。地形／角色遮挡排序、水面缺块和原 URP 材质表现仍须修复后再核对整体画面。代码入口为 `OfficialMainlineMapRuntime` 的 Hud／TopBar／Tasks／MiniMap／MiniMapTabs／MiniMapFilter／MiniMapMarks／Portrait／Functions 分部、`OfficialMap10DialoguePortraitView` 和 `Runtime/Official/WorldUI`；原始对象与代码哈希见 `UI/source-manifest.json`。

基础地面补回原 `MapGround_11_bare/GridGround/Ground` 的 17,752 个 Tile，以 55 张原 Sprite 的多边形、三角形、UV、颜色、Tile 矩阵及锚点生成 55 批原生网格，不改变原世界坐标。角色模型挂回原 `3001_view/Model/Offset/Player_Model_Map` 的 `(0.3, 0.3, 1)` 缩放、`(0, -0.5, -0.5)` 偏移，并以排序组适配原单 Renderer 的世界槽位，恢复角色显示。地图相机从原 `WorldCamera` 读取 SafeArea、适配优先级及裁剪面，沿 `OrthographicCameraResolutionAdaptor.AdaptOrthographicSize` 恢复视野和窗口比例，剧情结束恢复同一基准。转换器为 `prepare_unity_map11_base_ground.py`，显式加载 `Resources/Map11/BaseGround`；对象、bundle、原 shader、源码与输出哈希见 `base-ground-source-manifest.json`。带图形 Play Mode 已实际查看基础地面和角色恢复后的 HUD／对话画面；该补载尚未恢复原 URP 光照、水面与完整排序，不代表整图渲染完成。

已在带图形的 Unity Play Mode 运行地图加载、实体显示、踏板、战斗碑、故事路由、怪物和女神像流程：加载 77 区、1,803 个实体、31 个 Spine 实例、14,427 批地形及 85 批小地图；验证九组踏板、七个终端、连接转换、计数、生成、重置和持久化；运行 5110 怪物与主线分支检查；女神像覆盖预览、访问额度、持久效果和进入战斗后的最低 HP。另以独立规则校验覆盖本地准入、回滚、存档冷读、到期、额度竞争、遗失女神等级／技能／属性及每日 08:00 边界。以上使用脚本回调或静音音源，不能替代完整用户流程、真实音频试听及 Windows Player 验证；整图仍未完成验收。

已转换并放入 `unity/Assets/Official/World/Map/11/` 的官方载荷：地图定义含 77 区、81 面积配置、1,803 个放置实体及其一一对应的实体组件、导航和 8 条区域开放条件；绑定 110 条实体进度参数、247 条实体显示、128 条实体 UI、5 条阶段动作，以及地图、区域、逻辑实体配置。Map 11 有 17 个普通女神像（`EntityClassId=6010`），其所在区域各配置一组 `DropItems`；另有 49 个失落女神像（`EntityClassId=6011`），其所在区域各配置一组 `GoddessActivePropAwardDict`。1045～1160 范围含 24 条主线记录。地形几何为 14,427 批次，引用的 475 张官方纹理已复制至 `Resources/Map11/Textures/`；小地图为 85 批次并绑定 2 张官方纹理。输入版本固定为 `854_156383`，来源和输出哈希见该目录的 `*-source-manifest.json`。转换器为 `scripts/prepare_unity_map11_rendering.py`、`scripts/prepare_unity_map10_minimap.py --map-id 11` 和 `scripts/prepare_unity_map11_definition.py`。地形视图已支持原始 Map11 几何头格式，保留 Map10 默认格式。

实体载荷含 1,803 个放置记录、105 个模型绑定、161 份官方几何（97 份基础几何和 24 类模型的 64 份状态几何）、27 个 Spine 实例和 203 张实体贴图。状态 `-1` 保留原 `StateGameObjectConfig` 关闭所有切换对象后的常驻内容，空几何也保留来源文件；女神像激活切换原 0／1 状态。补入的 `building_naka1` 和 `building_luzi` 均从固定版本官方 `worldbuilding/6330_11_view` 的 Sprite 引用重新提取，与已有 PNG 逐字节一致；来源见 `entity-source-manifest.json`。`8210–8213` 世界奖励实体按官方 `Map/entity_map_display` 绑定到公共单物品模板；展示表有常规地面与浅水两个版本，当前载荷保留常规地面版本。原 9 个无静态模型实例属于 `5110` 和 `6533`：`6533` 的官方逻辑视图明确无显示实体；`5110` 由 `ECStageBehStageView` 按当前阶段编队取得主怪，再由 `MapMonsterLoader` 动态装载怪物模型。六个 `5110` 放置点现按各自阶段编队绑定 3212、3215、3216、3217、3218 五种官方骨骼，挂在原实体格位并播放官方 `idle_01`；这些地图模型 prefab 的根缩放均为 `(0.3, 0.3, 1)`。来源核验见 `assets/reference/official-map/map11-world-monsters-source-manifest.json`，转换器为 `scripts/prepare_unity_map11_world_monsters.py`。

战斗载荷包含 129 个放置阶段及魔女实体组 2000 的召唤阶段 20001，共 130 个阶段／展开编队，保留 57 个怪物编队数组、17 个战斗场景和 591 个实体阶段绑定，以及玩家等级／位阶动态阶段及官方等级属性缺行语义。`prepare_unity_map11_runtime_data.py` 已将 110 类基础实体组件、44 个故事图（666 个节点）、四个子图（64 个节点）、50 条地图对话、35 个逻辑标签、17 个对话角色、10 个可演出资源及 24 条主线写入本地 `Resources/Map11`。原放置组件是 `Lo` 覆盖，不能代替基础实体模板；基础组件与嵌套阶段列表保持官方顺序。转换测试已通过，来源及输出哈希分别见 `battle-source-manifest.json` 与 `assets/reference/official-map/map11-runtime-source-manifest.json`。

固定版主线目录已扩展到 1045～1160 的 24 项森之国目标，包含地区探索、女神激活、“月影之森”实体完成及 1070 恶魔战场第 1 入口通关第 2 层。条件按 `OfflineMainlineProgressSession` 的本地地图状态与独立幻想阶梯存档判定，原固定物品奖励通过任务领取事务提交；1070 的进度显示采用官方客户端的层数加一规则，并可跳转到幻想阶梯入口。`OfflineMainlineProgressValidation.Run` 已实际通过，覆盖主线固定奖励、条件和存档冷读；目录生成器为 `scripts/prepare_unity_mainline_tasks.py`。目录接入和逻辑校验不代表完整剧情、主城地图跳转或实机主线流程已验收。

森之国正式入口按用户确认要求先完成并领取 Map10 主线任务 1040。`ResolveHomeReturnMap` 和 Map11 初始化都会检查已领取任务 ID；固定版 1040 要求开启 10 次 `chestId=6000` 普通祈愿宝箱，本地成功祈愿事务会原子记录实际开启数量、奖励与消耗，重复回执不会重复计数。`OfficialWishAcceptance.Run` 已通过十连抽实际增加 10、重复及无效请求不增加、1040 达到 10/10 与冷读恢复检查。地图内的 `EntityClassId=6000` 放置实体不计入祈愿次数。已有 Map11 区域记录不会绕过入口门槛；完整前序路线和新档实际输入尚未完成 Play Mode 验收。

`OfficialMap11Tools.ImportAndValidate` 已在 Unity `2022.3.57f1c2` 实际执行并正常退出，生成地形、小地图、实体 prefab，绑定 ClassId 11，并检查 77 区、1,803 实体、14,427 地形批次、85 小地图批次和相邻格寻路。`prepare_unity_map11_ui.py` 与 `map11_support_ui.py`／`map11_minimap_ui.py` 恢复原 UI、动态布局、TMP 字形和小地图标记；显式加载绑定为 `Resources/Map11/UI`。Common 的七个 TMP 图标字形使用原图集、指标和字符表，并注册原序列化哈希及当前名称哈希。上述导入不能代替整图玩法验收。

机关配置已补齐 116 个相关放置记录、九组条件锁和七座终端，保留自状态、通知、生成、其他实体状态更新与自动进入下一状态的原执行顺序。`OfflineMapPuzzleSession` 已在 Editor 检查九组解锁、七终端解谜／重置、踏板计数、关联状态通知、奖励实体生成及存档冷读；同时检查 413 只跳过已确认的缺失目标 423，其他未知目标仍拒绝执行。窗口化 Play Mode 已用真实鼠标点击石碑 2302，核对其与 2303／2304／2305 的联动并重新进入地图恢复状态；机关动画和终端原界面尚待完整接入，不能据此认定九组玩法完整完成。

窗口化 Play Mode 同时核对了女神像未达成／可激活／已激活三态、激活回执不重复、1001／1005 各增加 100、最近可站立格传送与位置存档恢复，并检查原 HUD 的初始状态开关。验证使用隔离档案，地区完成度由测试设置，尚未验证自然探索到 100% 的循环。静态检查、资源导入／编译与 `ProjectTools.Validate` 已通过；Windows Player 尚未构建验证，正式入口及剩余表现继续实施。

带图形的后台 Play Mode 验证了六个 `5110` 放置点均取得当前阶段编队、官方骨骼绑定和循环 `idle_01`，并核对 `ModelSlot` 零偏移、父级 `Model` 的 `(0, -0.5, -0.5)` 偏移、`(0.3, 0.3, 1)` 模型缩放，以及跟随 `shadow` 骨骼的原阴影 Sprite、层级和排序。相机对准 3828 的实际运行帧确认模型及 atlas 纹理参与渲染；Unity 另验证其材质 shader 受支持且纹理有效。未执行真实桌面输入检查。

终端购买链已在带图形渲染的后台 Play Mode 检查：首次赠券、免费重置、成功解谜扣一张、购买取消不扣款、支付 50 晨星购一张后继续解谜，以及零晨星时展示固定数量兑换并在关闭后回到原选项。已查看实际运行截图，修正 TMP 图标绑定、官方按钮动态布局、兑换标题对齐和数量栏激活。该检查调用了回调，没有模拟真实鼠标／键盘；新增购买与兑换界面的实际输入检查仍待桌面空闲。另以隔离存档冷读验证未扣款的中断取消、已扣款的中断补完，以及再次冷读不重复扣券。

世界树与援助的实际故事图已检查计数 0／1／2 时的首项优先和无匹配分支，魔女 Switch 已检查原结果匹配、类型转换及默认出口，剧情状态条件已检查 None／Playing／End，并冷读核对计数与路由。后台 Play Mode 已验证无连接的 `Exit` 按原 `StoryGraph.AsyncRun` 正常结束并只记录一次完成；魔女实际查询节点已核对跨地图总数、同一像不同回执不重复计数及旧档默认值，Editor 冷读检查已通过。该项使用回调和隔离激活记录，未覆盖魔女原界面及完整主线。

镜头位置查询已按原 `GetEntityBaseNode.GetEntityType` 修正：`0` 为主角，`4` 为放置 ID，`5` 为逻辑标签，位置保留原偏移。后台 Play Mode 已检查资格门、战斗门、方尖碑和圣兽故事中的实际主角查询，不再误读未使用的 `LoEntityId` 字段。新增转换器按固定版本官方 EC 索引读取 111 类地图／故事实体与两类本地玩家 prefab 的 `MapEntityTag` 和 `StoryNodeRegister`；世界树注册核对为实体 6101，方尖碑为 6002，均指向原 prefab 根节点。周围对话中心按 `StorySettings.PlayerArroundRangeSize` 的 2×3 格范围，合并地图与故事 NPC 的 `StoryTalk` 实体世界位置边界；空范围或战斗中回退主角，中心距离超过 5 格也回退，并应用原 `GetEntityPosOffset`。后台 Play Mode 已补世界树注册位置和无标签回退检查。移动相机的其他 `WorldCameraNode` 行为、NPC 动画和可见性仍须恢复；原子 prefab 的动态骨骼显隐目前没有在自制实体视图中模拟，因此对话中心的 NPC 可见性筛选仍不完整。

默认 BGM 11 已以原媒体 `750073388` 接入本地 Unity AudioClip，使用 `Map_11_Play`／`Stop`／`Pause`／`Resume` 的无限循环与 Sine 淡变参数：1,000／250／250／500 毫秒。原区域／面积音乐表没有本图覆盖项。音量读取现有音乐设置，离图按原停止事件释放。带图形与原生音频的后台 Play Mode 已检查循环、暂停采样位置保持、恢复继续播放、音量变化和停止释放；音源保持静音，尚未试听或验证战斗音乐切换及其他音效。转换器为 `prepare_unity_map11_audio.py`，绑定为 `Resources/Map11/Audio`，来源见 `assets/reference/official-map/map11-audio-source-manifest.json`。

44 个故事图与四个子图引用的 207 个原剧情语音事件已绑定本地流式 AudioClip。按原 `TalkDialog`／`OptionTalkDialogPanel`，仅 Voice 行播放，换句先执行 `Voice_StopAll`；纯文字行、普通对话关闭及故事结束立即停止，选项面板仍显示时保留当前语音，异步加载完成后核对当前行。音量读取现有语音设置。后台 Play Mode 已检查全部 207 份 Windows 导入结果、实际主图／子图对话播放、连续换句、纯文字停止、过期加载取消、关闭停止及音量；音源静音，未试听或验证真实输入。转换器为 `prepare_unity_map11_dialogue_audio.py`，加载绑定为 `Resources/Map11/Audio/voices`，来源与输入输出哈希见 `assets/reference/official-map/map11-dialogue-audio-source-manifest.json`；整段剧情的镜头、演出与主线仍须继续恢复。

地图表现程序集与编辑器检查程序集已显式引用现有 `spine-csharp`／`spine-unity`，解决 `OfficialMainlineMapRuntime.WorldMonsters` 与地图后台检查的 CS0246 编译错误。主工程 Editor 与 Windows Player 脚本编译已通过；未据此扩大本图运行验收范围。

共享地图定义、阶段结构、寻路／探索计算、表现适配及本图复用的演出／音频代码随地图纳入版本控制；原生 UI catalog 的静态 GUID 依赖沿现有官方绑定补入。战斗怪物头像由固定版本原 Sprite 扩展至 Map10／Map11 共 38 个 IconId，修复森之国进入战斗时缺少头像的异常；来源见 `assets/reference/battle-hud/monster-portraits-source-manifest.json`。对白切换继续等待原动画半程事件，已替换或关闭对白的旧回调不再覆盖新内容和语音。

带图形后台 Play Mode 已通过魔女原书页、首次结果预览、成功后计数、周期访问及其原 ActionTime、英雄／魔女每日额度互斥、未用效果冷读恢复和结果文本绑定检查；实际进入战斗确认 `-100%` 保留 1 HP、绑定官方战斗状态，并等待战斗结束核对效果清除且每日额度保留。已查看首次预览与战斗运行帧。该检查调用脚本回调，音源静音，没有真实鼠标输入；战斗画面仍有地图 HUD 叠加，不能作为整体战斗界面还原结论。资源转换、38 种怪物头像来源／PNG／Sprite 绑定检查与工程静态检查通过；没有据此发布 Windows Player。

四类英雄雕像6570～6573的官方触摸／首次奖励／周期访问面板已接入公共 `OfficialMainlineStatuesModule` 的 `HeroStatues.cs` 实现，首次激活按地区等级读取410生命属性；实际指针已验证永久生命170与生命恢复、周期访问和跨雕像每日共享额度。普通金币点位115已通过地图模拟点击验证扣5体力、奖励入包、隐藏和原3秒间隔刷新。铁矿、时之砂与历战精华奖励、怪物回执去重和离线刷新已有隔离事务检查；完整地图刷怪操作、英雄原角色／粒子演出、探寻动态实体及雾层尚未完成。规则和来源统一见[主线地图公共模块](../features/mainline-map-modules.md)。

本图73份 `LightRegion` 原生阴影已绑定 `OfficialMainlineRegionFog`，恢复原形状／falloff网格、衰减贴图、Multiply光照和女神解锁时2.01666665秒的消雾曲线。原73份资源不包含地区21／28／57／62，未补造阴影。图形 Play Mode 已用模拟指针激活女神，检查渐退结束、公开地区冷读并查看前后实际画面；`ProjectTools.Validate` 与原地图回归通过。日夜专属装饰、局部灯光及小地图未探索遮罩仍未完成，完整边界与来源见[公共模块](../features/mainline-map-modules.md)。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

`prepare_unity_map11_audio.py` 先核对 APK 的 WwiseBnk 清单及实际包内文件，再读取同版云端包，验证 `map_bgm_01` 的包与原 Wwise 媒体哈希。原 `750073388` Vorbis 转换载荷复制为原生 AudioClip；四个事件的动作、无限循环、Sine 曲线和 1,000／250／250／500 毫秒参数从原 bank 解出。代码、输入、处理方式及输出哈希见 `official-map/map11-audio-source-manifest.json`，显式加载绑定为 `Resources/Map11/Audio`；试听、战斗音乐和语音不属于已完成验证。

`prepare_unity_map11_ui.py`、`map11_support_ui.py` 与 `map11_minimap_ui.py` 从 APK 实际文件及固定版本补充资源恢复 37 份原生 panel、325 张 Sprite、37 份原生小地图标记和 1,707 条实体标记绑定。包含 WorldCanvas、TalkDialog／OptionTalkDialogPanel、圆形／全屏小地图及筛选、FunctionEntryDialog、原 FirstPayBtn、任务项、物品及女神／魔女窗口。跨图标签提取同组 Map10／Map11 的原 `MapInfo`、`MiniMapSettings` 和 `ImgMapSwitch_0`／`_1`，原 parser 顺序、图标及逐图缩放配置均进入同一载荷；运行时只显示已访问地图。原 `MiniMapWorldStageSwitcherSupport` 的对象状态引用及源码哈希一并导出，由本地实体阶段驱动标记图标互斥切换。功能入口／快捷消息的默认 Animator 状态、默认片段与 Controller 来源同样保留在该载荷中。原 MonoBehaviour 引用、严格读取的 UI／Framework 字段、CanvasScaler／遮罩启用状态、中文、GlobalColors、PopupWindow／Animation 曲线和半程事件、UGUIJszx 横纵布局及自定义网格、MirrorImage、战力推荐和数字格式均记录输入代码、对象及资源哈希；输出 `map11-ui.json` SHA256 为 `f30126a7a7a619aaec8f5a12da04c083e0d71bcb47c5c24e0b1b5fc01b3be5ae`。`map11_tmp_sprites.py` 提取 Common 七个原 TMP 字形、字符及 400×200 图集，恢复 Texture2D／材质及原序列化哈希。显式加载为 `Resources/Map11/UI`。真实输入、跨图离线标记及点击、完整场景渲染和 Windows Player 仍未完成验证。失落女神经验／罗拉各 +1%、最近可站立格传送及兑换关闭后的剧情返回属于所属页记录的单机差异，未改动官方输入或预期哈希。

`scripts/prepare_mainline_fog.py` 读取固定854官方 `partition_mapground_11_extensions` 的73份 `LightRegion` 原层级、Light2D字段和 `map_region_active_anim`，从 APK builtin 取原 `2D Renderer Data` 与2048×128 `FalloffLookupTexture`；全局白光来自已核实的同版本 bare 地形依赖。APK／缓存实际对象、输入与输出哈希见 [区域阴影来源](../../assets/reference/official-map/map11-region-lights-source-manifest.json)。`prepare_mainline_fog_meshes.py` 在隔离 Unity 2022.3.57f1c2 工程中调用固定原 Unity Graphics `LightUtility.GenerateShapeMesh`、UTess／Clipper；固定Git blob、Mathematics包和73份网格哈希见 [网格来源](../../assets/reference/official-map/map11-light-meshes-source-manifest.json)。`prepare_mainline_fog_shaders.py` 机械转换 APK 原 Light2D 与 Sprite-Lit GLES 程序，保持原表达式、深度／剔除和 Multiply 分支，见 [程序来源](../../assets/reference/official-map/map11-light-shaders-source-manifest.json)。显式加载 `Resources/Map11/Fog` 与 `World/Mainline/RegionLight2D`／`RegionSpriteLit`，运行范围和仍缺的日夜装饰／小地图遮罩见 [公共模块](../features/mainline-map-modules.md)。

## 自定义实现

原线上组队按已确认原剧情 NPC 资格、等级、位阶和技能适配，不生成真人玩家。公共刷新使用本地时钟；普通路线保留真实败北、产出和等待，后期资格回访不能记作普通角色自然成长。

用户确认 49 座失落女神像（`EntityClassId=6011`）均须在对应地区探索度达到 100% 后激活；首次激活自动增加床的家园经验收益 1% 和小推车的罗拉（金币）收益 1%。两项分别使用 `HangUpExpAddPercent`（1001）和 `HangUpMoneyAddPercent`（1005），每座各增加定点值 `100`（1%）。`HangUpCartAllPercent`（1034）增加小推车全部产出，不能用于代替罗拉专用加成。49 座像分别对应 49 个不同地区，激活按地图／地区持久化，并以事件回执防止重复结算。

此奖励是已确认的单机差异：固定版本官方 `map_region.GoddessActivePropAwardDict` 为全部 49 座配置经验 +1%，另一项按地区分为不同资源，仅 8 座配置罗拉 +1%。`OfflineGoddessPropSource` 在原属性流水线中替换 `LostGoddess` 来源，对森之国失落女神统一汇总 1001／1005 各 100，保留原表及哈希，避免与原表资源加成叠加；普通女神地区没有该字典，不计入家具奖励。家园实际收益与界面仍需运行核验。

女神像传送采用用户确认的单机落点：优先神像所在格；该格不可站立时取最近可站立格，等距按行、列确定，落点写入本地图位置存档。官方 `PlayerWorldTransferRequest` 只提交目标实体 ID，包内没有服务器选落点实现，因此此项是单机规则。森之国官方出口目标为 Map12；用户已确认不迁移 Map12，后续按主线地图逐步扩展。

机关终端结果 254／258 的服务器类为空，单机按用户确认依关联锁配置满足解锁状态，重置恢复机关初始状态和踏板计数。锁 413 的官方结果仍保留对 423 设置状态 1 的原始数据；重新反编译并核对原始 EC、MapInfoWrap、客户端实体定位与生成调用后，423 无放置定义或生成入口。用户确认仅跳过这条无目标更新，继续执行生成奖励实体 401 等其余结果；其他未知目标仍报错，不套用此例外。

用户确认一键解谜成功时消耗一张物品 325，重置免费，保留原固定奖励 45 首次赠送的一张。原剧情称其为“黄金甲虫”，物品表中的正式物品名为“破局之匙”，两处原文分别保留。购买券时晨星不足会打开兑换；因原 `ShopBuyIntent` 关闭确认框后没有通知 `ItemQuikBuyNode`，用户确认单机在兑换／商店关闭后走原故事“购买失败”分支，返回机关选项，避免剧情一直等待。

`SelectorNode`／`SwitchNode` 的客户端只消费服务器返回的 `SelectedIndex`，包内没有选择实现。用户确认单机按原配置顺序取首个为真的条件或首个与输入相等的结果，均未匹配时走原 `Exit`；结果比较保留 `SerializableObj.Compare` 的原始类型转换。规则用于世界树、援助和魔女剧情；分支路由接入不代表魔女激活状态和整段剧情已经完成。

`GetMapWitchExploreInfoNode` 只读取服务器返回的 `ActiveCount`，缺少服务端计数实现；用户确认沿用 `WorldWitchTouchUI` 的所有地图已激活魔女总数。`OfflineMainlineProgressSession.ActivatedWitches` 账本按地图／放置实体持久化，重复激活不重复计数，缺少新字段的旧档从零开始；原查询节点及 Switch 输入已接入该总数，并通过跨地图累计、重复回执和存档冷读校验。Map11 两处魔女实体 6620（放置 ID 2801、6901）已接入原书页、结果预览、世界阶段成功结算和周期访问；预览不发奖，阶段成功后按激活前全图计数选择原 `WitchInfo`，应用奖励、HP、状态或实体组生成结果。实现入口为 `OfficialMainlineMapRuntime.Witch.cs`、`OfficialMap11WitchWindow.cs`，运行载荷保留 14 条激活规则和七条原权重周期奖励。完整剧情与召唤战斗循环仍待验证。

按用户确认，英雄／魔女共用每日一次访问额度，访问记录、道具和待战斗效果在同一本地事务中写入。状态退出游戏后保留，进入下一场任意战斗生效，战斗结束后清除；HP 减少按入场最大生命百分比结算，`-100%` 至少保留 1 HP。首次剧情激活与周期访问沿原阶段图分别处理。普通女神／失落女神的探索激活不占该每日额度；Map11 没有英雄雕像放置点，英雄界面与随机增益入口仍待所属地图迁移。

### 已确认的本地接线与边界

机关规则从原 EC／Lo 配置恢复 116 个相关记录、九组锁与七座终端。锁 413 对缺失实体 423 的原结果保留，独立记录用户确认的单机跳过例外；已重新反编译原程序集并核对原始地图数据，未创建替代实体或修改官方源配置。

终端物品 325 的 `quick_buy` 行从同版 APK 的 `config_luban` 提取，保留 50 晨星、每次一张、无购买次数上限及物品 4013 持有条件。剧情分支清单记录 `SelectorNode`、`SwitchNode`、`StoryGraph`、布尔条件及 `SerializableObj` 原代码哈希；按用户确认补全服务器缺失的首项匹配规则，未改动故事图的端口、顺序或默认出口。

剧情来源清单同时记录 `GetEntityBaseNode`／`GetEntityPosNode` 的枚举和偏移代码，以及 `GetMapWitchExploreInfoNode`／`WorldWitchTouchUI` 的查询与跨地图计数调用。单机沿用户确认使用所有地图魔女激活账本，按地图／放置实体去重，计数与主角定位均已用实际故事图检查；魔女界面和完整主线仍见地图页缺项。

`prepare_unity_map11_runtime_data.py` 沿固定版 `854_156383` 的实体 6620 EC 组件、`WitchInfo`、`map_periodicity_random_award_interaction` 与 APK 内 `reward_rule.g.bytes`，保留 14 条全图激活计数规则、七条周期奖励权重，以及原结果组件类型／索引；实体组 2000 解析到官方召唤阶段 20001，战斗载荷由 129 个放置阶段补为 130 个。输入对象、版本、奖励原始载荷及输出哈希见 `official-map/map11-runtime-source-manifest.json` 和 `unity/Assets/Official/World/Map/11/battle-source-manifest.json`，显式加载为 `Resources/Map11/stories` 与 `Resources/Map11/battle-runtime`。缺失服务端的结果结算、共享每日额度、下一场战斗状态及 HP 至少保留 1 点均按用户确认处理，不能标作服务器源码还原。

`prepare_unity_mainline_tasks.py` 复用已核验官方 `task`／`npc_friendship_level` 及奖励表，生成 33 项本地主线目录（含 1045～1160 的 24 项森之国目标）与 10,000 条好感门槛。来源与输出哈希随 `Resources/Official/Progression/mainline-tasks-source-manifest.json` 交付，由 `OfficialMainlineTaskCatalog` 加载；正式入口采用用户确认的 Map10 任务 1040 领取门槛，祈愿宝箱计数在原奖励／消耗事务中写入。整图与正式入口仍按地图页所列范围验证。

## 尚未实现

完整小地图仍需其他地图的离线实体标记、标记点击／详情与公共资源点提供器；功能菜单仍需接通商城、关闭之外的功能窗口。真实鼠标／键盘／拖拽和 Windows Player 验证尚未执行，不能用后台脚本回调代替。

森之国进入战斗后，地图 HUD 与战斗场景仍有叠加，玩家和战斗镜头比例、场景缺块／排序及完整演出需要继续恢复；魔女召唤阶段 20001 的完整循环尚未 Play Mode 验证。

继续验证 77 区、九组踏板、六块战斗碑、森林老人／世界树／狮鹫完整剧情与 1045～1160 主线领奖路线；任务数据和条件接入不代表任务流程已在主城及地图中完整跑通。还须验证个人资格、公共化、全区激活与 6001 出口条件，不能仅按任务 1160 完成推断可以离图。

正式入口已按 Map10 主线任务 1040 加入运行时门槛；普通祈愿成功开启 6000 宝箱现已原子更新任务计数，但前序任务条件、新档累计 10 次祈愿的实际操作及领奖后进入森之国仍未完成 Play Mode 验收。仍须接通区域、放置实体、条件、交互、战斗、奖励和出口前置状态。共享运行时已支持按地图 ID 加载资源、区域开放条件、位置与状态存档，但 Map10 的正常出口与家园返回入口尚未接通本图。原地图 NPC、建筑、世界奖励、交互物、怪物及小地图 bundle 已提取校验；`map11-lighting.json`、`map11-stone-animations.json`、`map11-logic-views.json`、`map11-dialogs.json` 的完整动态行为、特效和音频仍待绑定。地形与小地图原生 prefab 已生成，Windows 实际画面、输入和完整流程仍待验证。

普通女神像与失落女神像是不同机制。共享运行时的 Map11 女神处理器已按基础阶段先检查地区 100%，再依次执行结果 204（激活并持久化回执）与 209（自身状态变为 1）；已激活神像按官方输入分支直接打开弹窗。两种基础模板均没有 `StageResultAwardComponent`，地区 `DropItems` 在 `GoddessTouchDialog.RefreshAward` 中仅生成珍宝预览，不在激活时自动发放。珍宝预览及详情操作限制、普通女神祝福等级派生和提示、失落女神奖励提示动画尚待接通。`WorldTouchDialogLostGoddness` 的 `PlayerGetLostGoddessAwardRequest` 是独立的“找回遗失女神奖励”流程，不能作为像的激活奖励。探索里程碑仍须按 `MapGoddessExpProcessItem` 手动领取；命运果实独立核对日常领取与探索事件。

### 完成方案

以下为拟实施方案，不代表已接入或验证。主要缺口是 Map 11 的实际游玩运行时；当前没有经核实必须由用户补充资料才能继续的外部阻塞。

地图加载、HUD、区域策略、移动、点击、交互、战斗调用与存档已提取为[公共模块](../features/mainline-map-modules.md)，并保持 Map10／Map11 的资源和存档隔离。森之国踏板、战斗碑、剧情与出口仍须按本页缺项继续完成；公共抽取不代表本图完整迁移完成。

| 部分 | 尚需完成的范围 | 实施要求 |
| --- | --- | --- |
| 地图入口与存档 | 正常进入森之国、地图切换、返回家园、位置恢复 | 以地图 ID 加载资源并接入角色现有存档，消除 Map 10 专用资源路径、存档名和数量断言对本图的限制 |
| 场景与素材 | Unity 导入、场景对象、灯光、水面、原生角色动画、动态动画、特效和音频 | 按官方层级、坐标、材质和动画参数恢复，显式绑定动态资源，核查 Windows shader 与材质兼容性 |
| 区域与实体 | 77 区开放条件、探索度、个人／公共实体状态、九组踏板、六块战斗碑 | 使用官方配置和调用关系驱动交互，持久化解谜、实体完成与地区进度，接入本地任务和剧情条件 |
| 剧情与主线 | 1045～1160 范围的 24 条主线记录、森林老人、世界树、狮鹫剧情 | 恢复条件、对话、镜头、动画、任务推进和中断后的恢复 |
| 战斗与奖励 | 129 个官方阶段、57 个编队数组、NPC 参战、胜负与重复领取保护 | 接现有战斗核心，保留官方等级、位阶、技能、编队和资格条件，完成败北、离场、重进与奖励结算 |
| 女神与探索玩法 | 17 座普通女神、49 座失落女神、探索里程碑、命运果实 | 分别接通入口、状态、界面、动画和结算；失落女神采用已确认的单机奖励，探索里程碑保持手动领取，命运果实单独核对领取、探索事件与奖励链 |
| 地图出口 | 个人资格、公共化、全区激活及 `6001` 出口条件 | 逐条落实官方条件并验证组合分支，不能仅凭任务 1160 完成允许离图 |

### 实施顺序与完成条件

1. **正常进入并操作森之国。** 完成所需资源导入与编译、ClassId 11 目录绑定、正式入口、移动、点击、小地图和位置保存；在 Unity Play Mode 查看真实运行画面并操作输入。
2. **跑通一个地区的完整循环。** 完成打怪、交互和探索度累计，达到 100% 后激活失落女神；核对床的经验收益与小推车罗拉收益各增加 1%，其他产出不被该罗拉加成改变；冷读存档后状态仍在，重复点击不重复结算。此阶段通过后继续其余范围。
3. **扩展到全部地区和主线。** 完成所有区域、踏板、战斗碑、剧情、普通女神、探索奖励、命运果实及出口条件，逐类处理失败和中断恢复。
4. **完成整图验收并发布。** 使用实际鼠标、键盘和涉及的拖拽检查输入、遮挡、渲染、胜败、退出、重进、重复操作、剧情中断及冷读存档；验证出口前置状态并保留 Map12 未迁移边界，回归受影响的 Map10。通过相关检查并提交代码、资源及本页后，构建 Windows Player，实际启动核验受影响流程，再发布到 `D:/Desktop/杖剑传说/杖剑传说.exe`，保护用户原存档并核对桌面快捷方式。

完成结论须分别说明静态检查、Editor 导入／编译、Play Mode 逻辑／输入／画面及 Windows Player 的实际范围。逐功能／地图发布是完成条件；转换数据或静态检查通过均不能替代完整可游玩成果。全部要求实现并验证后才将本页改为“已完成”并移除剩余缺项。

### 待核查技术项与规则决策边界

- `EntityClassId=5110` 已按官方 `ECStageBehStageView`、`MapMonsterLoader` 和 `MonsterShadow` 绑定动态加载模型与阴影；桌面实际输入检查仍待桌面可用后完成。
- 地图个人资格、公共化和实体刷新：继续核对官方条件、请求／响应消费与客户端状态变化，落实已确认的本地时钟及 NPC 方案。
- 大量场景对象的 Unity 加载与运行表现：核查 14,427 批地形和 1,803 个实体的实际开销、加载绑定和状态刷新，必要时调整加载与批处理实现；视觉与玩法保持官方要求，不自行降级。
- 编译及编辑器运行：取得当前编译结果并处理真实错误，再执行 `OfficialMap11Tools.ImportAndValidate` 和实际游玩验证；旧的编辑器状态不能代替当前问题定位。

上述均为可继续查找和实现的技术工作，现阶段不需要用户决定基础架构或重复确认既定规则。若客户端实现、配置、调用方和既有单机约定核查后，仍无法确定具体公式、概率、条件或状态变化，再提供相关类／函数、配置项、资源名称、已查范围和行为缺口，请用户决定；等待时继续不依赖该答案的部分。

## 代码与资源入口

官方转换数据：[regions/11.json](../../assets/official/regions/11.json)、[adventure.json](../../assets/official/adventure.json)、[map11-rendering.json](../../assets/official/map11-rendering.json)、[map11-lighting.json](../../assets/official/map11-lighting.json)、[map11-stone-animations.json](../../assets/official/map11-stone-animations.json)、[map11-logic-views.json](../../assets/official/map11-logic-views.json)、[map11-dialogs.json](../../assets/official/map11-dialogs.json)。Unity 载荷：[map11.json](../../unity/Assets/Official/World/Map/11/map11.json)、[OfficialMap11Tools](../../unity/Assets/Game/Editor/OfficialMap11Tools.cs)。

Unity 共享地图接入：[OfficialWorldMapAssetCatalog](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapAssetCatalog.cs)、[OfficialWorldMapSession](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapSession.cs)。主线公共运行时：[OfficialMainlineMapRuntime](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs)。离线激活与奖励属性：[OfflineMainlineProgressSession](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs)、[OfflineCharacterConfiguredPropSource](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterConfiguredPropSource.cs)。官方地图逻辑与特效参考：[MyPlayableManager](../../.tools/source-packages/official-code/playable-manager-source/GameFramework.MyPlayableManager.decompiled.cs)。
