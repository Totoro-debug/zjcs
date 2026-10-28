# 职业、晋升与自由转职

[总索引](../README.md)

状态：未完成

## 功能简介

人物页进入职业总览、职业树、详情、升级和成就；原导师入口在地图寻路并打开实际职业窗口。自由转职按原 Function 135 的人物 100 级及职业分支条件开放。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

已核对官方职业总览、职业树、详情、成就和相关弹窗的源码、prefab 及配置。职业包 8357 个、规则弹窗包 241 个组件，奖励图标包 1900 个、说明弹窗包 100 个组件，以及字体、外观和提示图依赖共 16035 个组件的字段已经严格解码，完成转换输入写入、重开和逐字段比对。原生导出已恢复 1525 个资产中的 1853 个托管引用注册表、3377 条记录、12734 个对象引用和 1665 组打包数组，保留 CAB／对象身份、RID、顺序、浮点位值与层级身份。转换缓存尚未接通全部官方运行时类型和显式资源绑定。

主页面立绘按原 `ProfessionSettings` 的 GUID 从官方 Sprite 提取，38 个职业的男女映射复用 12 张原图片，保留 alpha、PPU、pivot、border；原 `ImageLocator` 的原生尺寸及锚点修正代码已迁移。职业总览、雷达、职业树的五种状态与条件分组，以及馈赠的原面板控制代码已迁入 `Runtime/Official/ProfessionUI/`。关闭、倒计时、导师入口和剧情节点登记通过服务接口连接本地运行环境；服务和原生 prefab 接线仍未完成。不能把旧节点投影界面算作原生 prefab 迁移完成。

两份原始字体由官方 `FontAssetFontConfig` 的路径、长度和 MD5 取回。原 `MPLUSRounded1c-Medium Atlas` 是 0×0、无像素载荷的动态 Alpha8 图集，已恢复并验证原生导入。当前含动态物品／配置的主面板依赖检查覆盖 881 个资产、87 种脚本、8 种托管类型和 7 个 shader，断开 GUID 为 0；尚有 18 种运行组件及相关服务未接入，主要是外观预览、离屏角色渲染及顶部信息栏，不能视为原生职业界面已经可运行。

馈赠使用的 `ItemIconDefault`、总职业等级说明使用的 `RuleDialog` 及四种奖励 Sprite 已从 APK 的实际对象取回，并记录调用、配置、CAB、对象 ID 和原始哈希。当前版本的成就、物品及规则提示三张 Luban 表已完全解码；组 10 的十条成就配置、四种奖励物品定义及 `ProfessionLevelInfo` 的标题／三条说明与现有打包配置相符。原图标管理器的 ID 覆盖、类型覆盖、默认图标选择顺序已核对，这四种奖励均使用原 `ItemIconDefault`，没有改用手写图标控件。

转换工具对单张奖励 Sprite 的裁剪 pivot 与原 Sprite 不同，现按原逻辑矩形、pivot、PPU、border、网格／索引字节、UV 变换及纹理引用恢复 4 个原生 Sprite；原 ASTC 按既有 UNORM8 解码流程转成 4 个原生 RGBA32 Texture2D，保留 mip、颜色空间和采样参数，Windows 不再二次压缩。`ProfessionRewardSpriteValidation.Run` 在隔离 Unity 中核对全部原始顶点、三角形顺序、UV、pivot、尺寸、PPU、border、纹理绑定、采样与格式；四张纹理上传 GPU 后读回的 SHA256 与解码 RGBA 完全一致。此验证没有覆盖职业馈赠列表的实际渲染和点击。

馈赠图标的原 `ImageProxy`、`ImageLoaderProxy`、`LoaderProxyBase`、`PreviewableProxyBase`、`AssetGuidProxy` 已按原源码迁入，保留嵌套序列化字段、Image 属性委托、路径前缀、同步／GUID／异步加载、当前请求身份检查和清理／销毁顺序。资源操作替换为显式本地接口；原 R3 资源集合的按当前数量正序释放、末尾清空及销毁标记顺序由原 APK 原生指令核实。`ProfessionImageLoaderValidation.Run` 在隔离 Unity 中以原奖励 Sprite 验证乱序完成、失败结果、清理后迟到结果、已完成请求的即时订阅，以及销毁后的请求拒绝和资源释放。原 `ItemIcon` 与本地 Sprite 提供器的接入和验证见下文；此加载器验证没有覆盖馈赠列表。代码及原生证据见[图标加载来源](../../assets/reference/profession-ui/image-resource-source-manifest.json)。


`UIBase`、物品附件／自定义附件、提示附件、品质与附件配置等 12 个原始类型已迁入，保留初始化前后启停回调、异常边界、重置顺序、附件缓存、排序及回收。原附件配置的 `HintTip=13` 按实际 AssetTable GUID 指向 `CenterHintContent.prefab`；其 6 个原生资产（含原 atlas Sprite、贴图、字体和材质）已安装到 `Assets/Official/ProfessionUI/Native/`，沿用已核实的 Windows 字体 shader。隔离 Unity 的实际 Play Mode 验证原层级／引用、延迟创建、缓存不自动激活、显式显示隐藏及回收重借，并通过 GPU 渲染检查，运行截图已查看。测试数字仅用于附件控件验证，未覆盖完整馈赠列表或鼠标输入。来源见[附件代码](../../assets/reference/profession-ui/item-components-source-manifest.json)与[原生附件资源](../../assets/reference/profession-ui/item-resources-source-manifest.json)。

原 `ItemIconAttachmentManager`、`ItemIconAttachmentPool`、`GameObjectPool`、`GameObjectPoolBase`、销毁监听和非泛型 `GAction` 六类已迁入。保留配置首次匹配、GUID 资源句柄、先入先出队列、借出时设置父节点／显隐后重置、归还时先清理再隐藏／入队、反向缩减及低内存释放顺序；场景注入通过显式实例化边界接入，未绑定时拒绝创建。隔离 Unity 的实际 Play Mode 验证这些顺序、原附件同实例重借、低内存释放后原对象销毁并重新借出、管理器释放时取消事件订阅，以及原字体／附件 GPU 渲染，全部通过，画面已查看。完整职业窗口、应用级内存策略及详情弹窗服务仍待接通。来源见[原管理器和对象池](../../assets/reference/profession-ui/item-pools-source-manifest.json)。

原 `ItemIcon` 的字段、初始化／清理顺序、品质与效果分支、计数、进度、附件及点击行为已迁入，依赖通过原代码实际消费的接口连接。原 `ItemIconConfig` 的 8 个有序品质引用（含重复白色）、附件配置的 42 个 GUID，以及四种当前奖励的图标路径／品质已核对；`ChestInfo` 和 `AppendAppearanceInfo` 两种图标覆盖表均不包含这些物品。33 个原生资产安装到 `Assets/Official/ProfessionUI/Native/`，本地资源目录保持原逻辑地址和 AssetTable GUID，不生成界面层级。来源见[原控件](../../assets/reference/profession-ui/item-icon-source-manifest.json)、[配置](../../assets/reference/profession-ui/item-configs-source-manifest.json)和[本地绑定](../../assets/reference/profession-ui/item-catalog-source-manifest.json)。

公共图集、品质图集、遮罩和字体图集恢复为原生 Texture2D，保留全部 mip、颜色空间、可读性和采样参数；原 ASTC 解码成 RGBA32，原 Alpha8／RGBA32 保持像素字节，不经过 PNG importer 再次压缩。替换保留原 GUID／fileID，并将 Texture2D 外部引用类型从导入资产 3 改为原生资产 2。隔离 Unity GPU 读回四张纹理的每个 mip，字节数与 SHA256 均与源转换结果一致；动态图集检查在生成新字形之前进行。来源见[原生纹理](../../assets/reference/profession-ui/native-textures-source-manifest.json)。

原 `FontAssetFontService` 已迁入，保留延迟加载配置、源字体已绑定判断、句柄保留、反射绑定、按名称释放及销毁顺序；两个原始 TTF 以 Windows 动态 Font 交付，原 JSON 字典在打包时解析为等值本地配置，运行时核对原 JSON 后交回原服务。未迁移 Android 流式缓存路径，也未声称完成多语言字体服务。隔离 Unity 的实际 Play Mode 使用正式本地图标／字体目录渲染四种奖励，逐项核对原品质背景、文字和字体归属，模拟鼠标按下／抬起进入原 `ItemIcon` 详情调用入口，清理后核对监听解除和控件状态；四项通过，实际运行画面已查看。附件提示也使用原字体重跑。测试中的统一数字 300 只验证控件，正式馈赠数量仍由成就配置决定。此处没有验证详情弹窗内容、完整馈赠列表或职业主页面。字体来源见[字体服务](../../assets/reference/profession-ui/font-service-source-manifest.json)。


`RuleDialog` 的全部设置重载、原列表复用、行激活及 `UIUtils` 的变换复位顺序和 `TipInfo` 的图文设置代码已迁入；原规则解析器结果通过本地接口提供，实际说明弹窗仍待接线。已核对并复用原 `BgDialogElement`、`ItemProgress` 的现有迁移组件，以及从原生代码恢复的 `TMPEffect`，未用空脚本消除原 prefab 引用。包含上述动态资源及物品配置的闭包为 881 个资产、87 种脚本，断开 GUID 为 0，仍有 18 种运行脚本待接入。


音效触发字段已按 APK 原始组件恢复。转换工具曾把 8 个 `AkEvent.triggerList` 的原始值 `-320808462` 写成无效的十六进制字符串；现以原始整数的小端字节重新写入，并逐个保留原层级、事件引用及其余字段。原生 `AkTriggerHandler.OnEnable` 的指令核实该值为启用触发。`ButtonSound`、`UIClickSound`、`PopupWindowSound` 及实际使用的 `AkEvent` 启用／Start、事件引用和单参数 `Post` 路径已迁入；动作、回调等本页未使用分支显式保留为未接入范围。隔离 Unity 导入验证 8 组触发列表、原生事件 ID／名称／GUID、自身发声对象及原序列化播放 ID，核对无效引用返回零和有效请求传递／保存返回 ID 的顺序。该探针只证明原生字段与请求契约，该字段探针没有覆盖声音播放或界面输入；Windows 播放适配另见下述验证。来源见[音效字段与代码](../../assets/reference/profession-ui/audio-source-manifest.json)。

五个原始职业声音事件 `Main_Tag`、`Main_Upgrade`、`Main_Cursor_Fail`、`Main_Cursor_Back`、`Main_Cursor_Normal` 已从原生 SDK 引用追踪到当前版本的 `EventBankMapping`，核实原事件／动作／声音／媒体 ID；`minimum.pck` 与 `ui_sfx_01.pck` 使用 APK 中哈希相符的实际载荷，映射使用当前云端已下载的哈希校验载荷。原 Wwise Vorbis 经 wwiser／vgmstream 转成五份本地 PCM WAV，禁用自动音量提升，分别保留原 −6／−8／−16 dB 增益、声道、采样率和时长；Windows AudioImporter 使用 PCM，不归一化、不重采样、不再次有损压缩。资源及显式事件目录位于 `Assets/Official/ProfessionAudio/`，本地播放桥位于 `Runtime/Offline/ProfessionUI/`，没有替换原 UI 控件。

`ProfessionAudioPlaybackValidation.Run` 已在隔离 Unity 的实际 Play Mode 核对五份 AudioClip 的全部 PCM 采样、采样率／声道／帧数及显式绑定，再通过原事件包装向 Windows 播放桥发出五个声音请求，逐项观察真实 AudioSource 的样本推进与 AudioListener 非零输出。验证在正式进入 Play Mode 并等待实际游戏帧后开始，五项播放全部通过。此范围只完成声音载荷和平台播放，正式职业窗口生命周期及本地设置混音仍未连接，也不替代职业界面鼠标操作验收。处理与哈希见[Windows 音频来源](../../assets/reference/profession-ui/windows-audio-source-manifest.json)；未生成完整素材快照。


转换输入工具允许显式选择隔离 Unity 工程的实际 TMP／UGUI／URP 编译程序集作为类型元数据输入，逐项记录来源与 SHA256；默认仍使用主工程。本次刷新后 357 个 bundle 的转换输入字节与上次完全一致，复用既有导出，再按来源恢复事件数组，没有绕过哈希校验。


红点系统按原 `TipDotNodeBase`、`TipDotConditionNodeBase` 和 `SafeAction` 迁移聚合、激活、优先级／等级选择、全部子节点门槛、忽略等级、反转、屏蔽和脏刷新，保留去重及可重入回调。职业、晋升、自由转职及总等级成就的 5 种条件节点通过只读解析器／玩家事件接口接入本地；接口实现仍待连接。原图资产的读取与连接遍历代码、UI linker 和计数显示控制代码已恢复；图编辑器及故事／服务器节点执行 API 不属于该界面的运行范围。隔离 Unity 真实导入官方 2 张图、16 个节点，核对 6 种托管类型、节点字段、条件数据、预设、端口顺序及每条连接，导入和核心行为验证通过。

主面板所需的字体、灰化、粒子及 Spine UI 共 7 个 shader，按原 CAB、对象 ID 和原始载荷 SHA256 复用工程中已从官方 GLES 转换的结果，保留原材质引用的映射；`Hidden/JSZX/TextMeshPro` 的双描边、底色、辉光、灰化与裁剪分支均在原转换范围内。隔离 Unity 验证 7 个原生 GUID、源码哈希、加载及支持状态，662 组原始程序的 1324 个 Windows D3D 顶点／片元阶段全部编译通过；尚未据此声称实际职业界面渲染通过。

成就 C# 规则已按官方 `AchievementRawInfoParser`、`CycleTemplateInfo` 和 `AchievementInfo` 生成 10005 个档位，保留已领取／处理中条目的过滤和排序。点击任一可领取行时，按原 `AchievementPanelItem` 的组 10／`count=-1` 请求及共享 `Player.TryRecordAlreadyGotAchievementReward`，领取该组所有已达成、未领取档位；本地奖励和记录使用一次事务。原配置边界、五行显示、循环尾档、批量领取和重复领取保护的独立 C# 测试通过。

隔离 Unity 工程已完成迁入控制组件的导入、编译及 `Zjcs.Editor.ProjectTools.Validate`。`ProfessionProgressionValidation.Run` 使用独立存档验证总等级 5 时一次取得 1200 个原奖励物品、四条记录，重复请求不写盘，冷读恢复，总等级 6 时只领取循环起始奖励；真实文件锁导致的写盘失败和物品数量溢出均保持原存档与内存状态。职业预览的服装／武器处理按原 `ProfessionUtils.SetProfessionAppearance` 克隆玩家外观，不改写装备和存档。尚未完成职业原生 prefab 的 Play Mode 点击、画面与保存恢复验证，以上不代表功能完整还原。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

自由转职服务端扣除方法未取得。用户确认生成三组方案后取消不消耗转职石，最终确认才消耗；本地费用适配器遵循免费 0 个／非免费 1 个转职石，另存当次报价支持恢复。免费间隔继续按原 168 小时计算，生成免费方案即推进时间，放弃不重置该时间。服务端请求改为本地原子存档事务，写盘失败回滚费用、方案、职业及奖励。

## 尚未实现

- 红点图的本地玩家事件、图生命周期／controller 和 UI linker 实际接线，以及红点显隐和数量的交互验证。
- 职业树卡片的官方角色 Spine／小人外观和动画；主职业页立绘、雷达及属性列表与游戏内画面的实际对照验收。
- 职业详情中的官方技能和属性、升级消耗与徽章领取差额；树中未解锁条件、导师寻路与晋升条件。
- 成就五行列表的领取状态与官方奖励图标／数量、批量领取、保存恢复和重复领取保护的实际运行验收。
- 职业转换与晋升；导师自由转职的比较、三组方案、确认、原奖励、撤回及免费间隔。
- 全部原生组件与资源接线后的 Unity 编译、Play Mode 模拟点击和运行画面检查，Windows Player 构建／启动／存档验证和发布。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

总览、分支连线、详情、升级、普通转职及晋升使用原面板。升级消耗原徽章且不按人物等级额外截断；晋升核对前置职业等级、总职业等级和导师区域公开状态。历次职业等级属性持续累计。

进入职业详情或升级时按已拥有技能的阶位领取原徽章，只领取未领差额；重开与冷读不重复发放。职业成就使用组10固定档和循环奖励。

切换职业卸下不兼容武器、清除方案中不适用技能并重置祈愿职业选择，装备和已获技能不删除。男女预览使用原默认服装、六类武器、prefab挂点和完整父变换；晋升原时间线驱动换装、动作、粒子及音频。

自由转职从地图十处原导师剧情入口打开，按原节点顺序继续剧情。选择窗口使用原三职业立绘、多边形点击区和选中动画；比较窗口展示职业、武器、属性与费用，三套方案支持页签、战技／秘法列表、未获得标记和技能详情。

生成方案、放弃、最终确认、通知与奖励、八小时内撤回、反向技能预览、提前结束撤回期、期限届满提示均接入本地事务。生成与放弃确认使用原 prefab 的倒计时按钮锁定；方案、选择、免费间隔、当次费用和撤回期限在重启后恢复。技能、碎片、徽章及历次职业等级按原映射双向交换；切换后统一整理职业技能方案、武器和祈愿职业选择。

### 依据与代码定位

以下官方代码与规则用于核对接线：`ProfessionOverviewPanel`、`ProfessionTreePanel`、`ProfessionTreePanelItem`、`ProfessionPanel`、`ProfessionUpgradePanel`、`ProfessionPropRadarDrawer` 和 `Common/AchievementRawInfoParser`，均位于 `.tools/source-packages/official-code/`。职业图片输入、bundle 身份和输出哈希见[职业立绘来源清单](../../assets/reference/profession-ui/portraits-source-manifest.json)；Unity 面板包由 `scripts/prepare_unity_character_panels.py` 生成。迁移组件的方法范围、官方代码和 C# 输出哈希见[运行时代码来源](../../assets/reference/profession-ui/runtime-source-manifest.json)；属性行当前只迁移总览和雷达使用的字符串显示路径，完整彩色预览和祝福交互仍待对应调用方迁移。动态加载凭据与 Sprite／纹理处理见[动态资源来源](../../assets/reference/profession-ui/dynamic-resources-source-manifest.json)，转换入口为 `prepare_unity_profession_dynamic_sources.py`、`prepare_unity_profession_reward_sprites.py`；隔离导入由 `prepare_unity_profession_reward_probe.py --project <隔离 Unity 工程>` 准备。原生组件来源与转换分别由 `prepare_unity_profession_native_inputs.py`、`prepare_unity_profession_native_export.py` 恢复，凭据见[原生输入](../../assets/reference/profession-ui/native-input-source-manifest.json)及[原生导出](../../assets/reference/profession-ui/native-export-source-manifest.json)。成就独立测试入口为 `python -B scripts/test_profession_achievements.py`；相关界面仍未通过 Play Mode 和 Player 运行验证。


