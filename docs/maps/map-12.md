# 12 山之国

[总索引](../README.md)

状态：未完成

## 地图简介

地图 ID：12。原目录：坎斯汀大陆/山之国。场景类型：`map_world`；原区域配置99项，BGM配置12。

当前验收对象为 Unity / C#。

## 完整还原部分

已按 `official-source.json` 选择的 `871_156598` 程序集解码山之国：280×210 导航、99区／105 area、2591个放置实体、148份所需 EC 模板、92张配置表及65份原剧情图。当前云端与旧提取的对应地图、配置和有效剧情端口逐项比对，全部一致才复用旧转换；保留端口执行顺序及原始节点，不能用相似文件替代。129个实际美术 bundle 完成索引长度／MD5／SHA-256校验；更新过的 shader bundle 中7个被引用对象另核对原始对象 SHA-256。

Unity 入口 `Assets/Official/World/Map/12/` 已绑定地形、小地图和实体 prefab：18683个原地形批次、原 Ground Tilemap 的26725块瓦片／56批次、140种实体显示／43份 Spine 帧数据，以及123个原小地图批次。原生地图目录登记12，运行仍未开放山之国完整游玩入口。资源导出不是本图完整还原结论。

剧情载荷保留49个入口与17个递归子图、22项1165～1270主线任务、11处终端的完整原始参数；战斗载荷保留155关／858项实体绑定／17个场景。放置202的未消费 `LoStageBehStageComponent.StageIds=[2000]` 保留在源数据中；其原 EC 模板没有对应行为消费者，故不把缺失的2000伪造为可执行战斗。

官方 UI 从当前选定来源重新解析68份 prefab 层级、934张 Sprite、序列化引用、JSZX 布局、原动画曲线和中文文本，绑定到 `Resources/Map12/UI/`；不使用截图或手写布局。`prepare_unity_map12_ui.py` 负责原对象提取，`OfficialMap11UiTools.ImportForMap(12)` 复用原生导入步骤，保留各 Sprite 的尺寸、边框、枢轴和 PPU。女神图标按原 `GoddessDialogIcon` 的状态对象清单切换；当前源数据另保留 Image／Mask 启用值，防止把遮罩禁用后直接显示黑色遮罩贴图。原 UIParticle 引用已记录，完整粒子模拟和全部特效尚未接入。当前仅为界面素材与组件绑定，地图运行状态、费用、奖励和存档仍须整体验证。

`OfficialMap12PlayModeAcceptance.RunUiImported` 已在内置屏以 实际运行，分别设置真实 GameView 分辨率并等待 Screen 尺寸同步，查看450×800和720×1280下的 HUD、女神及失落女神三种状态画面，核对原遮罩启用值、图标对象组、按钮尺寸与射线遮挡；模拟鼠标按下／抬起／点击，验证未满足条件、激活关闭及传送关闭分支。运行夹具使用独立界面数据与计数回调，未调用实际地图的发奖、扣费或存档，不代表正式玩家流程已经接通。

对话立绘从当前 APK／匹配云端重新提取8份原 prefab 与28张 Sprite，按原 `PortraitFaceData` 的左右图和区域代理选择资源；`RegionCodeEnum.CN=0`，8421使用原中国区域主图。8402的剧情表情 `roast` 未出现在原表情清单中，沿用 `PortraitFace.ChangeFace` 找不到名称时保留当前表情的行为，不绘制替代图。绑定入口为 `Map12/dialogue-portraits`，来源见[对话立绘清单](../../assets/reference/official-map/map12-dialogue-source-manifest.json)。

区域光载荷保留原98组 `RegionShadow`、共111个 Light2D（含13个 Day 节点）、原形状、falloff、颜色、强度、排序及原开雾动画；使用固定版本 Unity 原 `LightUtility`／UTess／Clipper 生成111份原生网格。`RegionLight2D` 和 `RegionSpriteLit` 负责本地显式绑定。入口画面已实际查看；昼夜与区域载入已通过，连续开雾、Water2实际画面和全部场景材质仍须整体验证，不能以网格导出代替完整表现。

Water2 已按固定来源的57个原 `MeshRenderer` 名称绑定，运行时沿用场景中的 MeshFilter、Transform、UV和索引缓冲，只替换为原 WaterSurface shader，并复用原 `WaterMaskRenderer` 将这些 renderer 纳入官方遮罩绘制；四张动态贴图、原材质属性与地图水面遮罩复制到本地 Resources，所有输入输出哈希见[Map12 Water2 来源清单](../../assets/reference/official-map/map12-water-runtime-source-manifest.json)。34个原分块网格在官方渲染载荷中没有重复UV数组，按其原 MeshFilter 保留原UV；有载荷UV时验收逐点核对。原 Mesh 按不可读模式导入， 运行校验使用子网格索引计数，不强制读取／改写原网格。生成脚本为 `scripts/prepare_unity_map12_water.py`，运行入口为 `OfficialMap12WaterView`。升降台真实地图 Play Mode 已通过57个 renderer 的 shader／索引拓扑绑定；水面可见画面、动画和遮罩动态仍待单独实测。

剧情空间从当前官方138份 EC prefab 恢复原 `MapEntityTag`、`StoryNodeRegister` 与 `StorySettings`。`DungeonActive` 原 Selector 的输入／输出逐对证明六个其他地图子图不可在本图选中，原图仍完整保留；山之国使用 `MountLift`／`IronMount` 注册点。`RunRuntimeSpatial` 在 Play Mode 直接执行原剧情图夹具，核对升降台1.5格偏移、机神山本图分支、原6秒／9秒等待、镜头恢复及剧情状态／次数冷读，并查看两处实际截图。该夹具直接启动剧情，只证明图执行与画面，不证明建筑输入；两图 `MaxCount=-1`，不添加单次播放限制。8项来源专项测试、六终端回归（含746从范围外走近）、58次指针推动及Map11原立绘／透明按钮输入回归通过。原建筑点击应经显示 prefab 中的 `ECWorldStageView` 交互链：升降台先打开 `WorldFantasyLadderDialog`，窗口操作才启动预览；机神山先判断4102／4103等原条件。当前已接通两处未满足前置条件时的原剧情分支；就绪后的窗口、关卡结算与完整建筑状态链仍未接通，不能把点击建筑直接播放预览当作官方行为。

机神山6103与魔能邃渊6302的显示 prefab 已从当前官方 `ECViewDriver` 严格解码，保留7种状态、5项条件和原64位 managed-reference ID；运行投影采用平铺节点表，避免 Unity JsonUtility 的递归深度截断。`WorldStageInteractionOperation` 迁移条件、同步顺序、激活检查、剧情重入保护、完成回调和首个匹配窗口点击分支；32种条件组合夹具通过。顺序操作中的剧情异步启动后仍立即执行后续点击，不改成等待剧情结束。来源和代码见[建筑操作链](../../assets/reference/official-map/map12-building-operations-source-manifest.json)。正式地图已按相同原操作图判断未满足前置条件的建筑分支。`RunBuildingPrelude` 在内置屏 以实际地图指针打开魔能邃渊指引，并模拟原 NextBtn 和机神山两项原对话选项，核对原剧情完成次数／状态、NPC移除及冷读。4103～4105的状态条件逐一验证0／1／2再回到1，修正以状态值作为永久事务凭据造成重复状态无法保存的问题。机神山引用的公共8402模型、原贴图／骨骼／网格已从官方来源补齐，模型闭包专项测试通过；NPC精确朝向、选位及完整 Playable 表现仍待迁移。建筑对话中的中文文本已绑定原语言表。魔能邃渊实际地图指针→原窗口→关卡102准备流程已运行，并检查全部七个怪物的50级／6阶位及原属性公式。`RunBuildingBattleRetry` 在内置屏 以真实地图指针和原建筑窗口启动 Stage102 战斗，验证实际胜利、档案锁定导致奖励提交回滚、解除锁后重试、2490金币和物品110×1精确到账、完成标志与推箱状态冷读及重复结算防护。另一次 `RunBuildingBattle` Play Mode 实际从入口101经地图指针、升降台原窗口及准备按钮完成Stage102战斗，检查原怪物等级／阶位／属性、档案锁定后的奖励回滚与重试，并确认2490金币、物品110×1、完成标志和任务1215“山壁电梯”标签在冷读后恰好保留一次。两次均使用已确认的90级／12阶位隔离验证角色与区域／剧情前置；方向型技能 AI 尚未定案，不能代表普通角色完整路线。

原 `WorldFantasyLadderDialog`、6103／6302图标 prefab、`StageDialog`、`StageTouchUI`、`WorldCommonStageUI` 和两份原窗口点击组件已恢复序列化引用，保留原空 Touch 按钮与首个窗口处理器绑定。6302的三个原 SkeletonGraphic 起始动画均为空，按原骨骼初始姿态、区域裁剪／旋转及权重独立转换144个顶点为 Unity 原生网格，保留完整贴图 mip、材质参数及6份原 shader 程序；未使用 Spine runtime 或历史采样帧。`NativeMap12BuildingGraphic` 按实际网格边界执行原遮罩裁剪，避免100×100 RectTransform 将延伸网格整体裁掉。`ImportAndRunBuildingUi` 在内置屏 通过450×800／720×1280原窗口、三份网格实际可见像素、模拟射线点击及“开始关卡→关闭→窗口点击”顺序，并查看两幅实际运行画面。奖励槽现已使用原 ItemIcon 与当前原奖励570／571，迁移原浮点曲线乘算、整数截断、聚合和排序；显式等级50／阶位6的公式夹具显示金币2490与物品110×1，低／高阶位边界及双尺寸模拟输入通过。另从当前官方APK补取山之国全部44种实际战斗头像，按原纹理色彩空间、Sprite枢轴和采样参数显式绑定到Map12关卡图鉴；转换／输入／输出哈希见[战斗头像来源](../../assets/reference/official-map/map12-battle-portraits-source-manifest.json)。6103原 `DailyDungeonDialogPrivateMap` 已加入当前官方 APK／云端 prefab 绑定快照，保留192张原 Image、76条 TMP、坐标／锚点／尺寸／颜色和 Sprite 哈希；运行时在服务端状态未下发时按原4103／4104／4105状态条件选择私有窗口操作，并把原 `BattleBtn` 接到副本121／难度50离线路线。按官方房间类型，12101为`Bonfire`且无敌人波次；路线从篝火可选择12102或12103，分别沿12104或12105汇入12106，再经12107到无敌人终点12108。此前 实际输入到达机神山原窗口并调用121/50路线创建；检查发现路线层级和房间射线目标需修正，现已按官方 prefab 绑定表调整窗口排序、房间原名绑定顺序、奖励图标及多语言加载。`Room_12101`根节点没有射线Graphic，点击目标使用原`IconImg`，未增加画面元素。12108仅写入本地路线完成标志，不发副本奖励、不改变次数或角色档案；失败重试、两条分支、完成状态与冷读夹具已加入，尚未由 Unity 执行。修复后的路线可见画面、真实房间点击及12102实际战斗胜负冷读仍待 验收。按用户确认，副本胜负在本地结算并允许失败重试；不修改奖励、费用或次数。

Map12主线任务1165～1270沿用官方顺序、前置和奖励；其中1215要求逻辑标签“山壁电梯”。该标签在当前官方地图逻辑配置中绑定实体3501，升降台胜利事务现与原奖励、内部完成标志原子写入该官方标签；加载旧的内部完成标志时也会补齐主线标签。 实际升降台胜利验收确认任务1215从0/1变为1/1，重开角色档案后标签、任务进度和奖励仍恰好各写入一次。

原生场景转换输入由 `prepare_unity_map12_native.py` 先查 APK，再按当前871索引读取缓存并补齐93个包的 CAB 依赖。复用已验证的原生 schema 恢复器，恢复4160个组件并逐对象核对21416个原始载荷字节不变，另独立重读全部字段；保留原 Unity 版本、Android目标、CAB身份及引用。转换缓存用于后续原生地形／场景／Water资产恢复，正式运行绑定与实际验证范围见下文。

`NativeTerrain/` 已恢复186个原 chunk、747个 Tile、739个 Sprite、11张原 ASTC 贴图及原 PartitionData，共1684个原生资产；按原线性／sRGB编码解压，保持尺寸、网格、UV、枢轴、采样与引用。`OfficialMap12TerrainTools` 验证实际 MonoScript、逐槽 Tile GUID／对象 ID、Sprite 网格和贴图导入参数，显式绑定 `Map12/Native/native-terrain-catalog`。`-6_0` 的第63个 Tile 槽在原始对象中即为空，精确保留，不补图；其他槽须绑定到对应原资产。`OfficialMap12PartitionAcceptance` 在 Play Mode 对全部186个 chunk 验证本地异步句柄、激活／隐藏、清除／重载／移除和加载未完成时移除，核对2060处带偏移的 Tilemap 数据及实际矩阵。复用同一871来源的官方 Partition 实现和本地资源适配，现有类型名中的 Map13 不代表使用泽之国配置。该夹具只验证分块生命周期与瓦片状态，正式玩家场景接入及其范围另见下文。

`NativeScene/` 已恢复原 `MapGround_12_bare` 主体及152个原生资产，核对哈希后复用109个地形资产；`NativeEnvironmentScene/` 保留187份环境 prefab／358个原生资产，`NativeRegionLabels/` 保留其余80份区域文字 prefab／124个原生资产。全部原层级、坐标、对象 ID、动画、材质、骨骼引用和纹理 mip 保留。原 shader 以原始对象 SHA-256 核对后迁移至 Windows ShaderImporter，保留显式脚本、shader、纹理与 prefab 加载绑定；熔岩边缘程序通过 Windows D3D 双阶段编译。`map12-shared-environment-source-manifest.json` 核对同一 APK 的5个全局配置对象及113项共享资源／代码载荷，不使用 map-13 的地图、导航或玩法配置。

区域文字迁移当前官方 `MapRegionDescSupportUI`、`MapRegionDescItemUI`、`MapRegionStrengthDisplayUI`、等级限制刷新、`UIColorByLevel` 及原 UI 初始化顺序，保留80份 prefab 中100组区域描述、600个文字组件和400条 `StateGameObjectConfig`。99区中16区配置等级限制；原表的服务器阶位限制／开放时间均为0，原世界标签调用明确传入空 MiniMap／regionPosList，因此本组件适配不代替小地图专用布局或定时开放提供者。显式保留原空战力列表分支、已激活区域探索度特殊分支和事件订阅／退订顺序；两个横排等级／战力文字键从 APK 原语言二进制补提。字体、字形初表、图集、材质和文字特效按原始对象哈希复用官方迁移资产；运行时复制动态字库与图集，并在 TMP Awake 前绑定副本，防止写回源资产。基础 TMP 与完整官方定制引擎的执行等价性仍未全部验证。

`OfficialMap12LabelAcceptance.Run` 在内置屏 、原 Linear／URP 配置下通过公开／私有区域夹具：完整原 PartitionData 未删资源，186个地形 chunk 和267份场景分区按原 PartitionController／Loader 回调加载，99处原区域光照全部注册，含文字 prefab 内的区域34。100组区域文字绑定、等级条件变化、探索度刷新与重启用、境界压制、战力文字线性颜色、昼夜显隐、完整清理与退订通过；87张地形主体／环境／文字分区纹理的604层 mip GPU 回读与原解码 SHA-256 一致。已查看包含区域文字的实际昼夜截图；这里使用配置及玩家状态夹具，尚未接入正式本地玩家／存档、普通角色路线及 Windows Player。

本地音频已接入正式 `OfficialMainlineMapRuntime`：BGM12使用原 Wwise 媒体664663381，播放／停止／暂停／恢复保留原 Sine 曲线与1000／250／250／500ms时间；236个主图／子图语音事件逐一核对当前871包、原媒体区段和已提取 Vorbis 字节，作为本地流式 AudioClip 打包。`chapter_3_main_42_03` 的原 MakeUpGain 为+2dB，其余235条为0；`OfficialWorldVoiceGain` 以原分贝对应的线性幅度处理 PCM，避免正增益被 AudioSource 的音量上限截断。共用播放器保留先 `Voice_StopAll`、文本行停声、异步版本取消、对白关闭停声和当前音量设置，不将音乐或语音请求发到网络。原剧情转换头仍为854，但236事件来自已用当前871逐端口证明一致的山之国剧情闭包；收据同时保留两种来源，不改写版本标识。

`RunRuntimeAudio` 已以 启动实际 map-12／独立测试档，验证 BGM 循环、四种淡入淡出、暂停／恢复采样位置和当前设置，236份语音均可在 Windows 本地流式导入；直接开启原对白组后，通过原“下一句”按钮的模拟指针输入验证换声，文本对白替换取消旧异步加载，并核对+2dB处理的 PCM 数值。已查看本次入口运行画面。森之国共用播放器的原 BGM 与207份语音回归通过；音源在自动验证中静音，未进行听音检查，直接对白夹具不证明剧情触发或主线连续路线。来源与原事件／媒体区段见 [BGM](../../assets/reference/official-map/map12-audio-source-manifest.json)／[语音](../../assets/reference/official-map/map12-dialogue-audio-source-manifest.json)，237份实际 AudioClip、Resources 路径、导入设置与适配代码哈希见 [音频绑定](../../assets/reference/official-map/map12-audio-binding-source-manifest.json)。Unity 导入／编译、`ProjectTools.Validate` 和静态检查通过。

`prepare_unity_map12.py` 可重新提取并转换以上载荷；`OfficialMap12Tools.ImportAndValidate` 已完成 Unity 2022.3.57f1c2 导入／编译。来源等价、剧情倒计时、未消费战斗引用、终端原清单及原生坐标及默认生成标记共7项专项测试通过；Map11战斗提取2项回归通过。Map11实体旧测试期望161批次而 HEAD 本身生成171，属于既有断言差异。Map10立绘素材测试的1项在共享 `adventure.json` 输入哈希处失败（实际 `a6820d65…`，原收据 `84b92c87…`）；该测试不调用本次生成器，本次未改该输入或原收据；原边界、表情和对话样式测试通过。Play Mode 使用 实际加载18683+56地形批次与2591实体，已查看入口101、华容道746和建筑202三处450×800运行截图。逐个核对当前官方 prefab 后移除56份会被 Spine 组件覆盖的重复预览网格，恢复旧导出向下的Y／V坐标；骨骼本身Y保持原值。贴图按原图尺寸、过滤、wrap和颜色空间导入，不再默认缩放非方形图集。建筑／箱子的白块已修复，瀑布等原特效 shader 仍显示白片，水／熔岩／光照和完整动画尚未还原。这次检查只覆盖场景载荷，未覆盖玩家输入、官方界面、机关结算和存档，不代表本图可玩或完成。

## 自定义实现

魔能邃渊102的服务端生成战斗等级执行体未取得；按用户确认，建筑相对等级为原区域等级加 Lo.Level，Lo.SubRank=0继承区域阶位，非0使用显式配置；怪物等级使用建筑实体等级加原 StageBehStageComponent、Lo 组件、关卡及阵型位置偏移，阶位使用建筑实体阶位，保留原怪物属性公式。此处基准为50／6，不使用关卡表静态1／1。

山之国关卡102的方向型技能施法朝向也未能由服务端 AI 确定；相关命中格旋转由客户端源码证明，目标选择后的施法朝向仍待用户确认。等级／属性验证不代表战斗胜负 AI 已还原。

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

历史验证中的九组机关开发求解只在复制状态寻找合法路径；缺有限MovableRect时用原区域约束开发搜索范围，生产碰撞、推动规则和奖励不变。九组机关原生点击在450×800通过584项、在720×1280对关键8702终端通过85项；这些历史点击结果不代表当前 Unity 验收通过。公共怪物／食材刷新以本地时钟及保存代次替代服务器。

固定 APK 客户端与 Common 程序集提供 `MapPuzzleTerminalDataHuarongDao`／`PushBox`、`ResetPuzzle`／`AutoPuzzle` 调用与原费用，但未取得服务端执行体。按用户明确确认，华容道重置恢复原 `NeedRemoveLoEntityIds` 与 `BoxLoEntityIds` 清单的箱子／障碍，将角色移回终端旁可站立格；自动解谜消耗原物品325×1，华容道只移除 `NeedRemoveLoEntityIds`，推箱只解除 `LockLoEntityIds`。奖励仍须实际交互或战斗领取，原领取记录保留，不能重复发放。六处终端的原对话、费用、逐项作用范围、重置与自动解谜冷读已接入 Unity 并验证；支付中断恢复与实际领奖仍待验证。

推动成功后角色跟进到箱子原格，按本次用户确认迁移。客户端 `MyStageResultEntityPushed.CanMoveToNextPos` 及共享 `StageResultEntityPushedComponent.GetTargetPosNextPosAndDir` 提供目标格、方向、可站立格与 `MovableRect` 约束；服务端推动执行体未取得，跟进位置属于已确认的单机差异，不能标作官方服务端代码。

1401、4602、6305、8702四处 `EntityLockPushBox` 按用户确认，以全部 `LoOverlapDetectorEntityIds` 目标格被 `LoBoxEntityIds` 中指定箱子占据作为完成条件；完成后保持解锁，执行原 `EntityUnlockWorldStateCompleteComponent` 对应的生成实体／更新状态结果，奖励仍需实际领取。`OfflineMapPuzzleSession` 已保存独立的箱子坐标、解锁和生成状态，旧存档没有坐标时使用原放置坐标；返回的保存快照与会话状态相互独立。机关初始生成另按原 `LoEntityComponentSpawnInfo.BlockDefaultSpawnInMap(Private)` 判断，不能把 `WorldActiveable.Active` 当作“已生成”；四个推箱奖励宝箱初始隐藏，解锁后才执行原234生成结果。已重新运行58次规则推动与六终端模拟输入／冷读，核对解锁前后生成状态；奖励尚未直接发放。推动沿用官方 GroundType 位掩码、实体占位、自身原占位白名单与包含端点的 `MovableRect`，6511箱子使用当前配置的移动键2／掩码1。

`OfficialMap12PuzzleValidation` 从四组原始摆放执行13／14／22／9次、共58次合法推动，逐次核对走到箱子旁、角色跟进和JSON冷读；四组解锁及原结果生成、锁存、六处重置、华容道两份不同作用清单与保存快照隔离通过。验证路径只在复制的网格状态中搜索，不进入游戏规则。Map11九组机关回归及 `ProjectTools.Validate` 通过。上述为规则层验证；运行层检查另见下段，扣券和完整领奖仍待验证，不构成本图可游玩完成结论。

运行适配已通过原入口101加载角色，使用与鼠标点击相同的地图指针入口，对四组机关完成58次推动并核对方向、角色跟进、四锁完成状态及退出后的冷读恢复。夹具逐步把角色放到箱子旁，不代表从入口连续行走的流程通过。原华容道746／5220终端已通过模拟地图点击、原对话“下一句”和原重置选项，核对角色回到终端旁可站立格；夹具提供前置区域完成度，尚未验证新角色路线。透明按钮须在首次实际绘制后再发出模拟输入，单独强制 Canvas 布局不能替代绘制；原按钮代码保持不变。四组推箱最后一步分别拒绝地图写入，核对箱子／角色／锁回滚且角色档案的区域进度不提前写入；派生区域进度在地图提交后写入，写入失败保留已提交机关并重试。`OfficialMap12PlayModeAcceptance.RunRuntimeTerminals` 已在内置屏以 通过746／5220／1416／4631／6326／8721六处终端：每处先经模拟地图指针合法推动箱子，再按原文字选择“重置谜题”，核对原箱子／障碍清单恢复、角色回到终端旁和冷读；随后经原“帮我解谜”与“提交”两层对话，核对325恰扣1个、华容道只移除指定障碍、推箱只解指定锁，以及退出后不二次扣券。检查仅比较机关管理的实体；普通实体按正式区域条件重建显示，不能与区域前置夹具的可见状态混为一谈。支付中断恢复与实际领奖仍待验证。 宿主 Canvas 绑定原缩放模式、参考分辨率／PPU 并在关闭时恢复；已查看修复后的450×800终端画面，Map11原立绘与透明按钮输入回归通过。角色档案临时拒绝写入后，已提交的地图不回滚，解除限制后区域进度重试通过。

固定宝箱按当前871的83处放置组件、原 WorldStage 手动行为2／结果200、原奖励索引与 Lo 追加／替换清单接入本地奖励服务。82处相对等级取原区域等级，8110保留显式等级70／阶位7；`WorldEntityLevelComponent` 的私有区域使用实体等级、公共区域使用角色等级分支保持不变。按用户确认，Lo 阶位0继承宝箱所在区域阶位，非0使用显式配置；空奖励引用沿用已确认方案。6401追加原规则782，原物品61925的图标、品质、图标模板与文字从当前官方资源补齐，界面目录现为934张 Sprite。奖励选择、等级倍率、包展开、原随机状态、开箱计数和完成标记在同一角色档案事务中提交；次级地图文件／区域进度失败后重试，冷读先恢复完成标记，已领取宝箱不重新占位或发奖。

`RunRuntimeChests` 在内置屏 通过全部83处奖励事务和重复领取检查，以及301／3104／2201／8110／8801五处正式地图指针领奖、四类宝箱、角色档案拒绝写入和“奖励已提交但地图未写入”后的冷读恢复。五处测试提供前置区域状态及初始站位；三处验证走入原交互格，两处写入拒绝夹具先站在交互格，不代表普通角色连续路线。已查看原奖励列表运行截图；6401的追加规则仅由规则夹具覆盖，四推箱奖励的连续解锁后实际领奖、原22025／22044交互动画、完整地图与 Windows Player 仍待验证。来源和代码／数据哈希见[固定宝箱来源](../../assets/reference/official-map/map12-chests-source-manifest.json)，界面绑定更新在原[原生界面清单](../../assets/reference/official-map/map12-ui-native-source-manifest.json)。

正式 `OfficialMainlineMapRuntime` 已通过 `Map12/Native/world-assets` 接入原主体、186份地形和267份完整场景分区，使用未过滤的原 PartitionData、原相机邻域加载与区域回调。等级、阶位、战力、原始探索点数及女神激活状态由本地玩家／地图状态提供；动态字体在 Awake 前复制，退出时恢复原管线并仅释放已注册的分区句柄。APK 的 PlayerSettings 明确为 Linear，工程同步恢复；实体与 Spine 显示恢复原独立 Sorting Layer／Order，玩家外层排序组读取原3001的 Object层／顺序0。共享 `Effect/Noise_Mask_Pro` 保留原关键字作用域，防止 URP 全局灯光误启局部变体；原动态 `_ColorMask` 绑定已恢复，未改 shader 公式或绘制替代素材。来源、实际资源目录与代码哈希见[正式原生场景绑定](../../assets/reference/official-map/map12-native-world-source-manifest.json)。

`RestoreAndRunNativeWorld` 在内置屏 使用正式地图相机逐区加载99处指定站位，实际观察到全部99处区域光照注册，核对原地形、文字生命周期和运行错误；原局部关键字修复后通过。已查看实际运行画面中的角色、实体、地形和区域文字。另在原102女神提供区域满进度前置，通过正式地图指针和原弹窗按钮激活，检查原5秒夜间分区移除、全局灯光所属活动场景及角色档案冷读；已查看解锁后与冷读后的实际明亮地形画面。正式地图保留在活动场景，避免原 `FollowSceneActiveSelfController` 因持久化场景不匹配而关闭全局灯光，未修改官方灯光数值或控制器分支。83处宝箱事务／五处指针领奖与写入失败冷读、Map10开场／地图存档及Map11原对话透明按钮输入回归通过，8项来源专项测试与静态元数据检查通过；Unity实际导入／编译和 `ProjectTools.Validate` 已执行。此处人为提供站位以覆盖分区，不代表普通角色连续路线；完整 HUD、头像、主线推进及 Windows Player 仍未完成。

## 尚未实现

6103原状态3通过 `DailyDungeonDialog` 打开普通难度面板，状态1在条件满足后打开专用 `DailyDungeonDialogPrivateMap`；原 StageBehaviour 指定副本121／难度50，路线从12101分成12102→12104与12103→12105两支，在12106汇合后经12107到12108终点。专用官方 prefab 与当前源 Sprite／TMP 绑定已接入；运行时接入两支选路、无敌人房间推进、官方战斗入口和不发奖励／不改次数的本地终点标记。用户确认的离线结算边界为：胜负写入本地路线存档，失败可重试；不发副本奖励、不修改公共次数或购买次数。用户也确认可按本地其他官方规则推导敌人编队；当前依据同一来源版本的房间类型／关卡波次配置，12101和12106不生成敌人。夹具覆盖两条完整路线及冷读，但还未由 Unity 执行；地图指针到窗口和路线 UI 的 连续输入，以及实际战斗胜负、冷读恢复仍待验收。

恢复99区剩余交互、七战斗碑、踏板／推箱／华容道、凤凰祭坛、命运探寻、1165～1270主线、小地图和剧情开雾；普通角色连续路线与高资格回访分别验证。Water2 的57个 renderer 已通过正式地图 运行绑定及网格计数检查，水面可见画面、动画和遮罩表现仍待单独验收。

按原地图配置恢复场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口；原 prefab、骨骼、材质、粒子、动画及音频需在 Unity / Windows 逐项恢复并绑定。

恢复显示 prefab 的 `ECWorldStageView` 条件／顺序／窗口／剧情操作链及其原生功能窗口。导航按官方 `WorldStageComponent.InInteractiveRange` 检查原偏移，并从四种山之国原模板（5013／5014／5015／6660）读取 `StageResultOverrideInteractiveRangeComponent`，随实体状态覆盖或恢复基础交互范围；`OfficialMap12PuzzleValidation` 已加入状态逐项验证，运行输入仍待实际 Play Mode。当前135种实体模板的 `InteractionFirst` 均为 false，故保留原值而无启用优先交互分支。正常角色连续路线与各建筑入口仍未完整接通。

- 完成本图独立的本地资源绑定、玩家入口、正常游玩、战斗／交互结算、失败回滚、退出与冷读恢复。
- 在 Unity 编辑器实际运行，并验证 Windows 目标表现；

### 依据与代码定位

- [当前程序集、配置、剧情与模板来源](../../assets/reference/official-map/map12-source-manifest.json)、[资源转换与哈希](../../assets/reference/official-map/map12-unity-source-manifest.json)。
- [原坐标、网格组件与贴图导入适配](../../assets/reference/official-map/map12-coordinate-source-manifest.json)、[官方界面原对象与转换](../../unity/Assets/Official/World/Map/12/UI/source-manifest.json)、[原生界面加载绑定及哈希](../../assets/reference/official-map/map12-ui-native-source-manifest.json)。
- [建筑操作与原窗口图形](../../assets/reference/official-map/map12-building-operations-source-manifest.json)、[建筑原生预览](../../assets/reference/official-map/map12-building-preview-source-manifest.json)、[山之国战斗头像](../../assets/reference/official-map/map12-battle-portraits-source-manifest.json)。
- [剧情／任务／机关载荷来源](../../assets/reference/official-map/map12-runtime-source-manifest.json)、[战斗来源](../../assets/reference/official-map/map12-battles-source-manifest.json)。
- [原剧情空间绑定](../../assets/reference/official-map/map12-story-spatial-source-manifest.json)、[原生场景转换输入](../../assets/reference/official-map/map12-native-input-source-manifest.json)。
- [原生地形转换](../../assets/reference/official-map/map12-native-terrain-source-manifest.json)、[Unity 导入与本地目录绑定](../../assets/reference/official-map/map12-native-terrain-binding-source-manifest.json)。
- [原生区域文字代码及范围](../../assets/reference/official-map/map12-label-components-code-source-manifest.json)、[原 prefab 与依赖转换](../../assets/reference/official-map/map12-native-region-labels-source-manifest.json)、[Unity 本地目录绑定](../../assets/reference/official-map/map12-native-region-labels-binding-source-manifest.json)。
- [原区域](../../assets/official/regions/12.json)、[原配置](../../assets/official/adventure.json)、[Unity 地图定义](../../unity/Assets/Official/World/Map/12/map12.json)。
- [转换入口](../../scripts/prepare_unity_map12.py)、[当前来源解码](../../scripts/prepare_unity_map12_sources.py)、[Unity 导入](../../unity/Assets/Game/Editor/OfficialMap12Tools.cs)、[场景渲染检查](../../unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.cs)。
- [机关状态](../../unity/Assets/Game/Runtime/Offline/World/OfflineMapPuzzleSession.cs)、[推动适配](../../unity/Assets/Game/Runtime/Offline/World/OfflineMapPuzzleMovement.cs)、[原摆放推动验证](../../unity/Assets/Game/Editor/OfficialMap12PuzzleValidation.cs)。
