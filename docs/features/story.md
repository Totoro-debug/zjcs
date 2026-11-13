# 主线剧情公共模块

状态：未完成（公共图执行已接入；章节完整剧情、演出、引导及发布未完成）。

[返回总索引](../README.md) · [主线地图公共模块](world-map.md#主线地图公共模块)

## 功能简介

所有主线故事图共用 `Zjcs.Offline.Story.StoryModule<TGraph, TNode>`。章节提供原图、入口与节点查找，以及执行节点的适配器；公共模块管理顺序推进、嵌套子图、异步等待、正常结束、取消、异常与同步循环保护。章节不得再复制推进循环或子图栈。

目标范围包括动画播放、主线专用副本及其剧情（世界之树、机神山等）、主线圣兽解锁及其剧情（飓风狮鹫、烈焰凤凰等）、玩法系统解锁与剧情引导。公共执行接线不代表上述完整流程或素材已经迁移完成。

## 原始官方实现

复用已提取、解密并反编译的 `.tools/source-packages/official-code/client-source/StoryGraph.cs`、`SubgraphRunNode.cs` 及各节点实现。`StoryGraph.AsyncRun` 依次等待节点结果、消费 `NextNode`，空出口正常结束；取消／异常进入清理。`SubgraphRunNode.InternalRun` 等待子图运行后返回原父图出口。公共模块将这些客户端执行顺序适配为本地主线程状态机，不实现在线 RPC。

节点配置、图端口、对白、条件、固定奖励、原 MyPlayable 和原生 UI 继续由现有地图目录加载。分支所需的已确认单机规则仍维护在[森之国](../maps/map-11.md)；专用副本的实际路线与收益边界见所属地图及[副本页](dungeons.md)，不把公开日常副本视为主线专用副本已经完成。

## 完整还原部分

此节列出当前已迁移的局部能力，整项功能仍未完成。

- 公共模块不依赖 Unity、地图 ID、章节名或资源路径；调用接口为 `Play(graph, completed)`、`Cancel()` 和只读运行状态。节点适配器返回下一节点、等待、子图调用或结束；0 号出口结束当前图，子图结束返回父图保存的出口。
- `OfficialMainlineMapRuntime.StartStoryGraph` 统一接入该模块，原地图推进循环与 `Map10RuntimeStoryFrame` 栈已移除。原图与子图不改写；所有使用该入口的主线章节共用同一执行实现。
- 序章 `OpeningStoryPlayer` 和原生 NPC 对白图也通过同一模块推进，不再各自维护节点循环。序章适配器保留 `SafeEnd` 的跳过模式和 `ForceEnd` 的结束语义；原生对白适配器保留原节点类型、职业变更、领奖回执、NPC 演出和嵌套故事调用。序章每个节点仍由原协程播放，NPC 对白保留原 64 次同步节点保护。
- `OfficialMainlineMapRuntime.StoryAdapter.cs` 保存地图节点动作：对白／选项、原生播放、关卡／专用副本入口、地图激活、独立世界出现、跟随者、镜头、交物和窗口引导。章节差异仍由原节点参数、地图目录和现有适配代码处理；尚未迁移的动作保留缺项，不能通过空出口声称完成。
- 正常完成继续按根图执行完成状态、播放次数、保存和清理回调。取消与异常不增加播放次数；地图销毁使当前续播失效。动画、战斗、对白、选项、等待和购买回调捕获对应节点的续播，重复或过期回调不能推进后续节点或新一轮同名剧情。
- `StoryUiModule` 集中处理窗口开关、按钮／列表点击、原注册注销事件、点击次数、对象 `activeSelf`、页签值与底栏状态等待。地图适配器只传原字段和原生对象；移除私有副本引导中自动开战的旧分支，点击必须来自原控件事件。取消或等待异常会释放监听，异常中止当前图，不留悬空等待。需要遮罩的窗口打开节点保留原 10 秒超时，按原 `NeedResetMask`／`NeedMask` 管理遮罩。
- `NativeStoryUiBindings` 从所选 884 原副本、队伍与邀请 prefab 恢复 `StoryNodeRegister` 的键、引用顺序、空引用、启用状态及精确层级地址。机神山私有详情在激活前接入注册与原射线检测字段；团队窗口有注册数据，完整队伍剧情接线仍未完成。来源和输入输出哈希见[剧情 UI 注册清单](../../assets/reference/official-map/story-ui-registration-source-manifest.json)。
- `ShowFullMaskNode`／`HideFullMaskNode`／`PlayMaskNode` 和点击引导共用迁入的原 `StoryMaskManager`、`StoryMask`、`StoryMaskGraphic`。APK 内 `StoryMask.prefab`／`Mask.prefab` 的完整层级、半径 23、扇形 10 段、原按钮 `targetGraphic`、边框与提示排版保留；公共 UI 模块管理等待注册时的透明全屏遮罩、激活后镂空和结束清理。原图的整数布尔字段显式转换，取消／地图停用释放目标克隆与整份遮罩实例。私有副本弹窗层级仅按原窗口列表递增，不再把全局剧情遮罩算成上一层窗口。窗口元数据恢复原 `mWindowType`，宿主按实际窗口层级排除 Tip；尚未接入的窗口不据此认定完成。来源见[遮罩代码](../../assets/reference/official-map/story-mask-runtime-source-manifest.json)和[原生素材](../../assets/reference/official-map/story-mask-native-source-manifest.json)。
- 旧 Map10 载荷缺少逐节点 `sourceJson` 时，从同一载荷保留的完整 `graphJson` 恢复参数，并核对节点 ID 与类型；不猜测页签状态、窗口或按钮参数。生成器同时保留原节点 JSON，后续转换不再丢失字段。
- 机神山 `FirstMeetMecna` 的 `PlayStoryTimelineNode` 已接入公共 `Zjcs.Official.StoryTimeline.StoryTimelineModule`：按官方 `PlayableDirector` 停止事件、`duration+5` 超时、`OptionTime`、`NeedMask`、Cinemachine 输出绑定和取消清理执行；`Earthquake1` 原生 prefab、三段镜头、玩家跟随、`ZSettings` 相机 Z 值与噪声配置均从当前 884_156692 包恢复。章节节点 16 的字段、端口和当前演出资源在来源清单中逐项核对；其他章节 Timeline 仍待接入。

规则验证入口为 `scripts/test_story_module.ps1`，直接编译生产模块并从同一调用接口验证动画等待、子图战斗、解锁与引导顺序、空出口返回、同步回调、取消后重开、重复回调、并发拒绝、完成后启动下一段、异常和递归保护。这些是执行契约回归，不是章节实际游玩验收。

当前规则回归、整工程 Unity 编译通过；最新 `ProjectTools.Validate` 在 `FightCombatValidation.RunOfficialCatalogBattleTest` 的现有 12860／24078 胜负断言处失败，尚未通过整工程验证。本轮未修改战斗规则或该断言。`Zjcs.Editor.StoryUI.StoryUiAcceptance.Run` 已在后台静音 Play Mode 使用实际打包的原生副本 prefab，通过原注册、真实 Button 指针事件、两次点击、开关窗口、取消清理、注销结束、对象显隐、原 TabItem 与底栏等待的契约检查；该检查不代表完整章节验收。

该 Play Mode 检查同时从旧 Map10 完整原图恢复全部节点参数，使用机神山原状态绑定显示进入按钮，通过真实射线检测核对按钮没有被遮挡，并由相机离屏渲染 720×1280 画面；截图已实际查看。测试没有填充完整队伍、掉落与奖励数据，仍可见模板占位值，不据此认定私图详情完整还原。追加的遮罩契约检查使用原 prefab 和实际 `EventSystem.RaycastAll`：全屏阻挡、镂空内真实按钮指针穿透、区域外拦截、两次点击与取消后遮罩／监听清理通过，720×1280 遮罩运行截图已查看。batchmode 不执行 `WaitForEndOfFrame`，该边界仅显式推进原迭代器验证，尚未验证带窗口自动帧末调度；也未宣称完成 bloom 效果。验证入口使用既有隔离场景启动开关，防止正常启动界面的独立射线层混入。全程不创建／激活 Game View、不输入系统鼠标键盘，不触碰用户存档。

`python -B scripts/check_unity_project.py` 通过（151,426 个 GUID）。Map11 后台回归入口复用已有 Game View 的无焦点输入准备；本次 batchmode 没有已存在的 Game View，输入准备停止，未进入实际地图流程。没有通过新建／激活窗口规避此限制。

`test_prepare_unity_map13_story_runtime.py` 的 10 项检查中 7 项通过，3 项因 `prepare_unity_map13_story_dependencies.py`、`map13_native_schemas.py`、`official_managed_reference_conversion.py` 的现有哈希与来源清单不一致而失败；三者内容与本轮开始的 HEAD 一致，本轮未改写这些工具、素材或预期哈希。该素材来源回归尚未通过。

## 自定义实现

公共模块、节点续播去重和失效保护为离线适配。遮罩的资源加载、UI 相机与窗口服务通过显式宿主提供；原全局窗口管理器和 bloom 合成尚未完整迁入。同步节点保护沿用迁移前的 256 次限制；没有新设奖励、解锁条件、圣兽结果或副本规则。

## 尚未实现

- 世界之树／机神山主线专用副本及前后剧情的整条流程，仍须在公共模块上补齐所属章节的原节点、原演出、战斗和存档恢复。各地图已有局部实现不等于完整迁移。
- 飓风狮鹫／烈焰凤凰等圣兽解锁剧情及对应玩法开放的连续交互、奖励与冷恢复尚未完整验证。
- 其他章节的 `PlayStoryTimelineNode` 仍有按时长等待的适配；机神山节点已改为公共原生 Timeline 播放。遮罩的自动帧末调度与原 bloom 合成、完整窗口注册覆盖、列表 `StoryTarget` 绑定、职业窗口及选项节点仍有缺项。机神山 Play Mode 已进入原生 prefab 和导演播放检查；当前 Cinemachine 噪声资源在该包版本运行时仍出现脚本解析警告，已如实保留为待修项。窗口／按钮／页签／底栏等待已接入公共实现，仍须随实际章节验证；不能把等待或直接前进作为完整演出／交互验收。
- 三种图格式的实际章节回归仍需覆盖序章、NPC 对白和地图故事图；统一执行不代替这些章节的画面和输入验证。
- 受影响章节的实际 Play Mode、模拟输入、渲染、存档中断恢复及 Windows Player 验证和发布尚未完成。
