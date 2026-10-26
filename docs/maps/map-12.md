# 12 山之国

[总索引](../README.md)

状态：未完成

## 地图简介

地图 ID：12。原目录：坎斯汀大陆/山之国。场景类型：`map_world`；原区域配置99项，BGM配置12。

当前验收对象为 Unity / C#。

## 完整还原部分

已按 `official-source.json` 选择的 `871_156598` 程序集解码山之国：280×210 导航、99区／105 area、2591个放置实体、148份所需 EC 模板、92张配置表及65份原剧情图。当前云端与旧提取的对应地图、配置和有效剧情端口逐项比对，全部一致才复用旧转换；保留端口执行顺序及原始节点，不能用相似文件替代。129个实际美术 bundle 完成索引长度／MD5／SHA-256校验；更新过的 shader bundle 中7个被引用对象另核对原始对象 SHA-256。

Unity 入口 `Assets/Official/World/Map/12/` 已绑定地形、小地图和实体 prefab：18683个原地形批次、原 Ground Tilemap 的26725块瓦片／56批次、139种实体显示／249个运行静态批次／42份 Spine 帧数据，以及123个原小地图批次。原生地图目录登记12，运行仍未开放山之国完整游玩入口。资源导出不是本图完整还原结论。

剧情载荷保留49个入口与17个递归子图、22项1165～1270主线任务、11处终端的完整原始参数；战斗载荷保留155关／858项实体绑定／17个场景。放置202的未消费 `LoStageBehStageComponent.StageIds=[2000]` 保留在源数据中；其原 EC 模板没有对应行为消费者，故不把缺失的2000伪造为可执行战斗。

官方 UI 从当前选定来源重新解析65份 prefab 层级、928张 Sprite、序列化引用、JSZX 布局、原动画曲线和中文文本，绑定到 `Resources/Map12/UI/`；不使用截图或手写布局。`prepare_unity_map12_ui.py` 负责原对象提取，`OfficialMap11UiTools.ImportForMap(12)` 复用原生导入步骤，保留各 Sprite 的尺寸、边框、枢轴和 PPU。女神图标按原 `GoddessDialogIcon` 的状态对象清单切换；当前源数据另保留 Image／Mask 启用值，防止把遮罩禁用后直接显示黑色遮罩贴图。原 UIParticle 引用已记录，完整粒子模拟和全部特效尚未接入。当前仅为界面素材与组件绑定，地图运行状态、费用、奖励和存档仍须整体验证。

`OfficialMap12PlayModeAcceptance.RunUiImported` 已在内置屏以 Direct3D12 实际运行，分别设置真实 GameView 分辨率并等待 Screen 尺寸同步，查看450×800和720×1280下的 HUD、女神及失落女神三种状态画面，核对原遮罩启用值、图标对象组、按钮尺寸与射线遮挡；模拟鼠标按下／抬起／点击，验证未满足条件、激活关闭及传送关闭分支。运行夹具使用独立界面数据与计数回调，未调用实际地图的发奖、扣费或存档，不代表正式玩家流程已经接通。

对话立绘从当前 APK／匹配云端重新提取8份原 prefab 与28张 Sprite，按原 `PortraitFaceData` 的左右图和区域代理选择资源；`RegionCodeEnum.CN=0`，8421使用原中国区域主图。8402的剧情表情 `roast` 未出现在原表情清单中，沿用 `PortraitFace.ChangeFace` 找不到名称时保留当前表情的行为，不绘制替代图。绑定入口为 `Map12/dialogue-portraits`，来源见[对话立绘清单](../../assets/reference/official-map/map12-dialogue-source-manifest.json)。

区域光载荷保留原98组 `RegionShadow`、共111个 Light2D（含13个 Day 节点）、原形状、falloff、颜色、强度、排序及原开雾动画；使用固定版本 Unity 原 `LightUtility`／UTess／Clipper 生成111份原生网格。`RegionLight2D` 和 `RegionSpriteLit` 负责本地显式绑定。入口画面已实际查看；连续开雾、昼夜分支、水面与全部场景材质仍须整体验证，不能以网格导出代替完整表现。

`prepare_unity_map12.py` 可重新提取并转换以上载荷；`OfficialMap12Tools.ImportAndValidate` 已完成 Unity 2022.3.57f1c2 导入／编译。来源等价、剧情倒计时、未消费战斗引用、终端原清单及原生坐标及默认生成标记共7项专项测试通过；Map11战斗提取2项回归通过。Map11实体旧测试期望161批次而 HEAD 本身生成171，属于既有断言差异。Map10立绘素材测试的1项在共享 `adventure.json` 输入哈希处失败（实际 `a6820d65…`，原收据 `84b92c87…`）；该测试不调用本次生成器，本次未改该输入或原收据；原边界、表情和对话样式测试通过。Play Mode 使用 Direct3D12 实际加载18683+56地形批次与2591实体，已查看入口101、华容道746和建筑202三处450×800运行截图。逐个核对当前官方 prefab 后移除56份会被 Spine 组件覆盖的重复预览网格，恢复旧导出向下的Y／V坐标；骨骼本身Y保持原值。贴图按原图尺寸、过滤、wrap和颜色空间导入，不再默认缩放非方形图集。建筑／箱子的白块已修复，瀑布等原特效 shader 仍显示白片，水／熔岩／光照和完整动画尚未还原。这次检查只覆盖场景载荷，未覆盖玩家输入、官方界面、机关结算和存档，不代表本图可玩或完成。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

历史验证中的九组机关开发求解只在复制状态寻找合法路径；缺有限MovableRect时用原区域约束开发搜索范围，生产碰撞、推动规则和奖励不变。九组机关原生点击在450×800通过584项、在720×1280对关键8702终端通过85项；这些历史点击结果不代表当前 Unity 验收通过。公共怪物／食材刷新以本地时钟及保存代次替代服务器。

固定 APK 客户端与 Common 程序集提供 `MapPuzzleTerminalDataHuarongDao`／`PushBox`、`ResetPuzzle`／`AutoPuzzle` 调用与原费用，但未取得服务端执行体。按用户明确确认，华容道重置恢复原 `NeedRemoveLoEntityIds` 与 `BoxLoEntityIds` 清单的箱子／障碍，将角色移回终端旁可站立格；自动解谜消耗原物品325×1，华容道只移除 `NeedRemoveLoEntityIds`，推箱只解除 `LockLoEntityIds`。奖励仍须实际交互或战斗领取，原领取记录保留，不能重复发放。六处终端的原对话、费用、逐项作用范围、重置与自动解谜冷读已接入 Unity 并验证；支付中断恢复与实际领奖仍待验证。

推动成功后角色跟进到箱子原格，按本次用户确认迁移。客户端 `MyStageResultEntityPushed.CanMoveToNextPos` 及共享 `StageResultEntityPushedComponent.GetTargetPosNextPosAndDir` 提供目标格、方向、可站立格与 `MovableRect` 约束；服务端推动执行体未取得，跟进位置属于已确认的单机差异，不能标作官方服务端代码。

1401、4602、6305、8702四处 `EntityLockPushBox` 按用户确认，以全部 `LoOverlapDetectorEntityIds` 目标格被 `LoBoxEntityIds` 中指定箱子占据作为完成条件；完成后保持解锁，执行原 `EntityUnlockWorldStateCompleteComponent` 对应的生成实体／更新状态结果，奖励仍需实际领取。`OfflineMapPuzzleSession` 已保存独立的箱子坐标、解锁和生成状态，旧存档没有坐标时使用原放置坐标；返回的保存快照与会话状态相互独立。机关初始生成另按原 `LoEntityComponentSpawnInfo.BlockDefaultSpawnInMap(Private)` 判断，不能把 `WorldActiveable.Active` 当作“已生成”；四个推箱奖励宝箱初始隐藏，解锁后才执行原234生成结果。已重新运行58次规则推动与六终端模拟输入／冷读，核对解锁前后生成状态；奖励尚未直接发放。推动沿用官方 GroundType 位掩码、实体占位、自身原占位白名单与包含端点的 `MovableRect`，6511箱子使用当前配置的移动键2／掩码1。

`OfficialMap12PuzzleValidation` 从四组原始摆放执行13／14／22／9次、共58次合法推动，逐次核对走到箱子旁、角色跟进和JSON冷读；四组解锁及原结果生成、锁存、六处重置、华容道两份不同作用清单与保存快照隔离通过。验证路径只在复制的网格状态中搜索，不进入游戏规则。Map11九组机关回归及 `ProjectTools.Validate` 通过。上述为规则层验证；运行层检查另见下段，扣券和完整领奖仍待验证，不构成本图可游玩完成结论。

运行适配已通过原入口101加载角色，使用与鼠标点击相同的地图指针入口，对四组机关完成58次推动并核对方向、角色跟进、四锁完成状态及退出后的冷读恢复。夹具逐步把角色放到箱子旁，不代表从入口连续行走的流程通过。原华容道746／5220终端已通过模拟地图点击、原对话“下一句”和原重置选项，核对角色回到终端旁可站立格；夹具提供前置区域完成度，尚未验证新角色路线。透明按钮须在首次实际绘制后再发出模拟输入，单独强制 Canvas 布局不能替代绘制；原按钮代码保持不变。四组推箱最后一步分别拒绝地图写入，核对箱子／角色／锁回滚且角色档案的区域进度不提前写入；派生区域进度在地图提交后写入，写入失败保留已提交机关并重试。`OfficialMap12PlayModeAcceptance.RunRuntimeTerminals` 已在内置屏以 DX12 通过746／5220／1416／4631／6326／8721六处终端：每处先经模拟地图指针合法推动箱子，再按原文字选择“重置谜题”，核对原箱子／障碍清单恢复、角色回到终端旁和冷读；随后经原“帮我解谜”与“提交”两层对话，核对325恰扣1个、华容道只移除指定障碍、推箱只解指定锁，以及退出后不二次扣券。检查仅比较机关管理的实体；普通实体按正式区域条件重建显示，不能与区域前置夹具的可见状态混为一谈。支付中断恢复与实际领奖仍待验证。 宿主 Canvas 绑定原缩放模式、参考分辨率／PPU 并在关闭时恢复；已查看修复后的450×800终端画面，Map11原立绘与透明按钮输入回归通过。角色档案临时拒绝写入后，已提交的地图不回滚，解除限制后区域进度重试通过。

## 尚未实现

恢复99区、57个Water2渲染器、七战斗碑、踏板／推箱／华容道、凤凰祭坛、命运探寻、1165～1270主线、小地图和剧情开雾；普通角色连续路线与高资格回访分别验证。

按原地图配置恢复场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口；原 prefab、骨骼、材质、粒子、动画及音频需在 Unity / Windows 逐项恢复并绑定。

- 完成本图独立的本地资源绑定、玩家入口、正常游玩、战斗／交互结算、失败回滚、退出与冷读恢复。
- 在 Unity 编辑器实际运行，并验证 Windows 目标表现；

### 依据与代码定位

- [当前程序集、配置、剧情与模板来源](../../assets/reference/official-map/map12-source-manifest.json)、[资源转换与哈希](../../assets/reference/official-map/map12-unity-source-manifest.json)。
- [原坐标、网格组件与贴图导入适配](../../assets/reference/official-map/map12-coordinate-source-manifest.json)、[官方界面原对象与转换](../../unity/Assets/Official/World/Map/12/UI/source-manifest.json)、[原生界面加载绑定及哈希](../../assets/reference/official-map/map12-ui-native-source-manifest.json)。
- [剧情／任务／机关载荷来源](../../assets/reference/official-map/map12-runtime-source-manifest.json)、[战斗来源](../../assets/reference/official-map/map12-battles-source-manifest.json)。
- [原区域](../../assets/official/regions/12.json)、[原配置](../../assets/official/adventure.json)、[Unity 地图定义](../../unity/Assets/Official/World/Map/12/map12.json)。
- [转换入口](../../scripts/prepare_unity_map12.py)、[当前来源解码](../../scripts/prepare_unity_map12_sources.py)、[Unity 导入](../../unity/Assets/Game/Editor/OfficialMap12Tools.cs)、[场景渲染检查](../../unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.cs)。
- [机关状态](../../unity/Assets/Game/Runtime/Offline/World/OfflineMapPuzzleSession.cs)、[推动适配](../../unity/Assets/Game/Runtime/Offline/World/OfflineMapPuzzleMovement.cs)、[原摆放推动验证](../../unity/Assets/Game/Editor/OfficialMap12PuzzleValidation.cs)。
