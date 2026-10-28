# 14 龙之国

[总索引](../README.md)

状态：未完成。当前官方配置、原生地形、全部 486 个环境／地区标签 prefab、两类蝴蝶动画与机关规则已恢复并分项验证；实体、正式玩法、HUD、存档与 Windows 发布尚未完成。

## 地图简介

地图 ID 为 14，原目录为坎斯汀大陆／龙之国，场景类型为 `map_world`。原配置包含 111 个地区、2,961 个放置实体，导航为 280×280，原点为 `(-134.5, -134.5, 0)`。BGM 配置为 14，后继地图为 15，默认入口为 101，地形入口为 `Assets/Partition/MapGround_14/MapGround_14_bare.prefab`。

使用 `official-source.json` 选择的 `871_156598`；APK 基础清单为 `854_156383`。先核对 APK 清单、实际包及内部对象，再读取所选索引下已校验的云端缓存，来源及哈希分别记录，不使用旧版数据补缺。

## 完整还原部分

- 当前版本的 169 个原地图实体模板／1,483 个组件已按原顺序接入剧情运行载荷；加入公共剧情跟随实体后为 171 个模板／1,505 个组件；女巫 14 个激活档位、22 个结果索引及 7 项探访权重均保留，召唤组按 Map14 选择原关卡 `20004`／实体 `5100`。四种英雄雕像效果、621 项实体参数、111 区及 115 个 Area 使用本图当前配置，共享主线 getter 已验证选择本图来源。修复共享预览窗口固定加载 `_11` 图标的问题，按当前 UI 地图 ID 加载 `_14`。完整工程校验及内置屏 Play Mode 在 450×800、720×1280 各通过 33 个原结果预览、按钮遮挡射线与空白处关闭点击，并查看 66 张实际画面。5 项新来源／地图选择检查与原森之国构建回归通过。本轮使用隔离预览状态，尚未验证正式实体激活、结果应用、每日次数、主线进度或存档；HUD 未初始化完整数据。

- 龙之国官方通用 UI 已按当前 `871_156598` 从原 prefab／组件字段恢复为 68 个面板、3,657 个节点、4,235 条组件记录及 956 张原精灵；保留原 Transform／RectTransform、层级顺序、锚点、四元数、Image 启用位与遮罩。另恢复对话图标等子 prefab、本地图小地图标记与原字体绑定。原生导入、编译、全项目校验及所有面板的层级／锚点／脚本绑定检查已通过；内置屏 Play Mode 在 450×800 与 720×1280 两种尺寸完成女神像／失落女神像锁定、激活、传送、图标切换、原动画关闭及遮挡射线检查，14 张实际画面已查看。修复共享夹具把尺寸写入已废弃 WebPlayer 组的问题，按当前编辑器实际 Standalone 组设置并断言尺寸。测试使用隔离 UI 状态，HUD 的玩家数值、头像、小地图和提示点未初始化；本轮不包含正式进度、存档或 Windows Player。雕像、命运探寻、HUD 和剧情正式业务尚未完整接通，粒子、音频与其余组件行为继续迁移，不据此认定全部界面已完成。

- 原地形、场景、水体／环境及区域标签已接入 `OfficialMap14NativeWorldView` 统一宿主；修复官方原点 X／Y／Z 三坐标被误判为二维的问题。完整工程导入、编译、`ProjectTools.Validate` 和 Play Mode 通过；实际查看原入口（放置 101）、区域状态刷新、另一位置及卸载后重新挂载的运行截图。已核对 111 区状态查询、分区加载、管线与新增相机组件清理。此处状态由隔离 fixture 提供，尚未绑定正式地图进度、实体点击、存档或 Windows Player。

- 辉／残影战斗实体 4651／4652 已追溯到 APK 内 `npc_middle_model`，源骨骼 2,315 根、171 个皮肤、834 段动画已完整解码；修复 MIT 二进制读取器遗漏的皮肤骨骼／约束成员引用，保留辉 54 条、残影 58 条物理约束。原 `SkeletonMecanim`、组合皮肤、HSV、Animator／Controller 字段已写入可复建源缓存，来源见 `map14-fightother-source-manifest.json`。6 项读取器测试及全部原二进制消费检查通过；尚未接入原生模型播放、战斗画面或 Windows Player，未烘焙或忽略这些约束。

- 龙之国的当前版战斗数据闭包已恢复为 217 个关卡、217 份波次名单、20 个战斗场景、90 套阵型和 909 组实体关卡绑定；包括魔女召唤关卡 `20004` 和剧情图中的三处 `StoryStartStageNode`（关卡 `52`／`53`）。关卡、怪物组、技能物品、阵型、场景和属性二进制均读取 `871_156598` 所选官方来源，九份共享公式源码与当前程序集反编译结果逐字节相同。运行载荷为 `Resources/Map14/battle-runtime`，尚未接通正式地图战斗入口。
- Unity Editor 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和战斗数据 Play Mode 夹具通过；实际消费 1,649 个怪物实例，其中 87 个按测试玩家等级动态计算，逐项比较 C# 官方属性公式与提取名单，并执行实际被动属性、单位建立和主动／被动技能组装。共享单位构造已保留计算后的等级，避免动态关卡技能误用占位等级 1。此夹具不覆盖完整战斗画面、鼠标输入、胜负奖励与存档；剧情对手 `4651`／`4652` 的原 `FightOther` prefab、`npc_middle_model` 骨骼及辉／残影皮肤已定位，原生模型、武器、动画和表现绑定仍待恢复。

- 当前官方程序集及客户端／共享反编译代码已经核验，按原 formatter、构造器与 schema 解码本图导航、放置、169 个模板／动作、93 张关联表、85 份客户端剧情图及对应服务端配置记录。对白为当前 Luban 表的 64 组，保留原顺序、中文键、角色、表情与配音键。配置记录不视为服务端执行器。原脚本中误指向地图 13 的输入已纠正；本图区域和实体分别读取 `mapinfowrap_14.g.bytes` 与 `map_14.g.bytes`。
- 已从当前官方收据生成统一地图会话格式的 `map14.json`：包含原导航、2,961 个放置、2,961 组实体组件、111 区进度及原配置表；默认出生入口为 101。它尚未进入 `OfficialWorldMapAssetCatalog`，也没有实体显示资源／战斗载荷，不能单独打开为可玩地图。
- 五个原地图 bundle 及 CAB 闭包共 113 份输入已提取、解码并核对。5,400 个原组件按元数据顺序严格消费字段，29,773 个对象在补齐序列化描述后逐字节核对。`SkeletonAnimation` 仅恢复原序列化 schema，包括私有 `_animationName`，与独立骨骼组件解析器逐字段比较；此步骤不实现或替代 Spine 动画运行代码。
- 原生地形闭包包含 2,386 份资产、240 个区块、1,046 个 Tile、1,045 个 Sprite 及 54 张贴图。裸场景另新增 270 份资产并复用地形 102 份依赖，新增 10 张纹理的 47 个 mip 全部保留。保留原层级、坐标、网格、UV、轴心、逐格矩阵、Tile 指针及材质引用。ASTC 解码为原生 RGBA32，保留线性／sRGB、行序、过滤、重复与各向异性，不增加有损压缩、透明颜色扩展或降采样。
- 本图新增的原 `Universal Render Pipeline/2D/Movable-Sprite` GLES 程序已机械转换为 Windows shader，保留原属性、算式与渲染状态，并通过 实际材质 pass 编译。其余依赖复用已迁移且核对同一原对象的水面、地形和场景 shader。显式目录为 `Resources/Map14/Native/native-terrain-catalog`（241 路径）与 `native-scene-catalog`（242 路径）；裸场景没有丢失脚本。
- Unity 2022.3.57f1c2 内置屏 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。Play Mode 在隔离场景逐一运行全部 240 个完整区块的原 `PartitionLoader` 加载、激活、清除、重载、卸载及未完成异步移除；核对 2,094 个含偏移的 Tilemap 记录及原矩阵。本图没有局部区块，未以本图覆盖该分支。10 张裸场景纹理的 47 个 mip GPU 回读字节一致。已查看两处实际地形画面；该夹具只加载单区块，未加载邻区块、水面与环境分区，不能视为完整地图画面验收。
- 十二终端的完整依赖闭包为 109 个实体，包含 27 个去重火炮目标、60 个走格子踏板、五把锁及五个独立奖励。原目标列表中的重复项保留于载荷，仅在执行清障时去重。踏板保留原偏移、访问次数、来向限制、公／私图启用及解锁后停用规则；`1704`、`4206`、`4208`、`4508`、`4511` 需要两次访问，八个踏板限制来向。完成锁按原顺序执行生成奖励、更新终端和自增状态。
- 当前 EC 索引、全部放置类及完整显示状态选择表解析出 416 个实际 prefab、33 个根 bundle；补齐 CAB 和原动态贴图加载依赖后为 1,544 份输入、3,228 条动态贴图绑定。25,419 个原组件严格恢复序列化字段，2,643 个托管引用注册表以可逆临时描述交给转换器；重新编码、严格读取并重开后核对原字段，117,858 个内部对象保留其余原载荷字节。`CinemachineComposer` 的 15 个序列化字段、类型、继承、属性与 APK 元数据核对后补齐描述；没有实现替代相机逻辑。当前成果为转换输入及字段来源，尚未完成实体原生引用、行为、骨骼和正式场景接入。
- 完整实体闭包已用私有 AssetRipper 1.3.14 转为 Unity 原生缓存，1,822 份含托管注册表的导出载荷已恢复原引用顺序、RID、类型及字段，并剥离 2,643 组临时来源标记。逐项核对 193,131 个值、10,376 条引用记录、16,576 个实际非空指针及 1,001 个原类型打包数组；43 个原空注册表也保留。缓存中的 Unity YAML 还没有绑定迁移后的游戏脚本、材质与动画，不能视为 Windows 可运行实体。
- 四个踏板原活动结果为传送：`3604→3609`、`4201→4205`、`4504→4506`、`4509→4512`，原 ActionId 为 `22081`。目标按原 `StageResultTransportComponent.TryGetTargetPos` 读取放置坐标再加 Offset，结果通过显式宿主回调交付；尚未接入正式角色移动和动作播放。没有传送宿主时明确报错，不忽略原结果。
- `Map14/Native/puzzle-catalog` 显式绑定原规则与哈希、十二终端的 `325×1` 原费用。内置屏 Play Mode 通过七组火炮清障／重置范围、重复目标、状态与坐标保留，五组走格子原次数／来向、超次数不完成、手动条件、辅助、独立奖励状态保留、组间隔离、传送目标／动作参数、JSON 冷读及坏哈希拒绝；地图 11、12 的共享机关回归、全局校验与静态检查通过。该验证直接执行规则，没有模拟鼠标、正式地图路线、扣费、实际领奖或磁盘存档。

- 全部 389 个非标签环境 prefab 已恢复原生层级与引用：新增 766 份资产、复用 22 份核验依赖，新增 59 张纹理。原全局环境配置、渲染管线及共享实现按当前 APK 对象与 113 份代码／资产载荷核对。`native-environment-scene-catalog` 与原加载器显式绑定全部路径，没有缺失脚本或未解析对象引用。
- 139 个环境 prefab 中的 238 个蝴蝶实例保留原 Transform、材质、初始皮肤、动画与更新模式；两种原模型分别为 9／7 根骨骼、3／1 套皮肤、9／2 段动画。按已确认的免费原生方案，读取原骨骼、非加权网格／四边形、图集裁切和旋转、链接网格、仿射继承及绝对时间三次曲线，独立计算原生 Mesh；没有复制、翻译或运行 Spine runtime。两套原 GLES shader 保留全部程序及渲染状态，全部变体的顶点／片元阶段通过 Windows D3D 编译；实际 Play Mode 使用 。
- 完整环境隔离夹具在内置屏 Play Mode 通过公／私图原分区与环境回调，分别加载 135／238 个蝴蝶实例、注册 108 个地区光照；全部 240 个地形区块及 389 个环境路径经过加载、场景停用／恢复及清除／注销。71 张纹理的 432 个 mip GPU 回读与源字节一致。独立双精度原始数据参考与 C# 原生求值器核对 3,274 个姿态、68,618 个顶点，覆盖全部皮肤、动画、关键帧及曲线内部；这不是与 Spine runtime 的逐帧对照。已验证两种可见实例的真实 Update 时钟并查看近景及公／私图实际画面。夹具明确省略 97 个地区标签，使用隔离环境状态，不包含正式角色、实体交互或存档。

- 完整实体闭包中的 2,040 个原材质已严格解码并逐个 JSON 往返编码，要求与原二进制逐字节相同。5,747 个 shader／纹理指针按原 CAB 与 PathID 解析，无悬空引用，涉及 36 个原 shader 对象／族与 1,650 个原纹理对象；完整对象清单仍为 117,858。另将 AssetRipper 导出的 2,034 个材质模板逐字段匹配回原 APK 对象，再按原 CAB／PathID 替换 shader 与纹理指针，生成 2,034 个 Unity 原生材质并建立本地材质目录；四个没有独立导出模板的 Unity 默认资源材质留给引擎，六个原始零尺寸动态字体 atlas 保留为空运行时槽位，等待原字体初始化链。Unity 主工程导入及 `Zjcs.Editor.ProjectTools.Validate`、静态检查通过； Play Mode 对 1,644 张原生纹理的 8,068 个 mip 完成精确 GPU 字节回读，并核验原格式、色彩空间与采样设置。34 个原 shader 对象的 2,208 个 pass 变体与 4,416 个 D3D 阶段在主工程通过编译； GPU 检查通过四类骨骼颜色／变换、16,384 个 UI 裁剪像素及三个 dithering 覆盖状态。材质模板仍来自核对后的 AssetRipper Unity YAML；正式实体 prefab 重绑定、字体初始化、实体渲染、鼠标交互和存档未完成。最近一次重新导入被其他工程文件中的 `DailyDungeonBattleRunner.cs:318` 编译错误阻断，之前的成功导入及项目校验日志仍有效。

- 97 个原地区标签 prefab 新增 130 份原生资产、复用 78 份已核验依赖，保留 440 条原状态切换托管引用记录。原 `MapRegionDescSupportUI`、`MapRegionDescItemUI`、`MapRegionStrengthDisplayUI`、`UIColorByLevel` 等代码按本图可达分支迁移，保留层级、坐标、原字体／材质／图集、初始化顺序、进度公式、公图特例、等级限制、战力颜色线性转换及事件释放。111 区配置没有时间／服务器段位开放限制；世界标签原调用传入空 MiniMap，未把小地图专用分支视为已实现。所有地区名称及横排文本从当前 APK 语言表解析，不以其他地图名称替代。
- 完整环境与标签夹具在内置屏 Play Mode 通过公／私图、110 个原地区组件绑定、660 处本地化文本、等级 1→150→1、段位与战力颜色、进度满值／恢复、禁用重启及公／私详情切换。原 TMP 文字实际生成字形；加载全部 486 个场景路径和 240 个地形区块，注册 111 处原地区光照，停用／恢复、完整清除和标签订阅释放通过。84 张纹理的 496 个 mip GPU 回读字节一致，已查看六处公／私图实际画面。动态字形写入使用原字体及图集的本地实例，未修改源字体。主工程原生导入／编译、`Zjcs.Editor.ProjectTools.Validate` 与静态检查通过。该夹具仍使用隔离状态，不包含正式角色、实体交互、鼠标输入或磁盘存档。

- Map14 官方 `HandInItemNode` 客户端只发送提交请求，扣物品规则由服务端执行。已按用户确认接入单机原子事务：`hand_in_item[4]` 提交 `2208×1`、最多 1 次；成功后增加次数，失败回滚，相同凭证回放不重复扣除，旧存档补齐独立提交次数列表。`CanHandInItemNode` 保留官方只检查次数的查询。正式地图剧情入口尚未接通；原窗口恢复与独立运行覆盖见下文。
- 提交物品窗口从当前 `ItemDetail/DialogHandInItemDialog` 与原 APK 默认 `ItemIconDefault` 严格恢复，保留 39＋19 个原节点、94 份组件字段记录、18 张原切片贴图、三个原 Animator 配置、两段 60 Hz 弹出／关闭曲线及原五态按钮 controller。controller 的五个参数、五个状态、四条转场、条件、时间和剪辑曲线与官方二进制解析值逐项核对。`AwardPanelLayoutElement`、`ItemProgress`、原状态切换器、全局缩放补偿、标题／红点／bloom 元数据、按钮音及 `PopupWindowSound` 绑定已恢复；`PreviewTool` 按原空组件保留。官方 `UI/Gray` 四个 GLES 变体机械转换为 Windows shader，保留原禁用材质参数，TMP 灰阶复用已迁移的官方 `TMPEffect`。本图 catalog 使用 `871_156598`，共享 APK 载荷保留实际基础包来源；两段 UI 音频复用前核对当前五份 Wwise 输入包与既有转换输入完全相同。
- 本次提交窗口在主工程 Unity 2022.3.57f1c2 完成原生 prefab 重建、导入／编译、`Zjcs.Editor.ProjectTools.Validate` 与 Play Mode 实测，并通过静态元数据检查。以 1080×1920 原弹窗基准在内置屏竖屏 Game View 显示，已查看物品不足与持有物品两种实际画面；模拟鼠标经过 EventSystem 射线及输入模块，核验禁用确认、取消、确认、原窗口关闭事件中断、销毁中断、原 UI 音实际播放与释放、原子扣除、次数上限、凭证回放、多物品缺项回滚和磁盘冷读。夹具使用独立存档，未进入 Map14 正式主线；图标点击详情、全局窗口遮罩与正式宿主仍待接通，不据此认定整个窗口功能或地图已完成。

- 已恢复地图选择之外的 9 张公共剧情图：`CombatDoorActive`、`EntityVineLock`、`LevelDoorActive`、`ProfessionTutor4_public`、`PuzzleEnd`、`PuzzleSolve`、`RankDoorActive`、`TowerActive3`、`WitchStory2`，共 155 个节点。统一载荷现有 94 张图、1,488 个节点、207 组／442 条对白、16 个角色、79 条 MapTalk 入口、18 项入口条件、9 份固定奖励及 1 份快捷购买配置。保留原图端口、节点字段和跟随实体组件顺序；5 类跟随实体中补入 `8402`、`8454` 两份缺少的原模板。4 项公共剧情检查、5 项雕像来源检查及 9 项 Map13 剧情回归通过；静态检查通过（65,521 个 GUID）。隔离 Unity 完整校验与两种竖屏共 66 次雕像预览模拟点击复跑通过，已查看实际截图；正式剧情执行与演出仍未完成。

- 原剧情演出已恢复为 16 个 prefab（其中 4 个本图专用 Timeline）、4 个预定义键、621 层对象及 2,190 份对象／组件记录；所选 260 个 CAB 共 10,281 个对象、3,996 份原脚本字段已严格解码并生成缓存转换输入。按原层级与组件顺序逐项绑定，验证 621 个 GameObject、621 个 Transform、194 个脚本组件、2,449 条引用和 11,462 项字段／数组／float 位值。保留原 YAML 的 `-0` 词法值，避免按整数读取时丢失浮点符号；4 项提取选择、源篡改及浮点宽度回归通过。粒子／Renderer 全字段与 Timeline 外部轨道仍待验证；缓存转换不等于原生资产接入或可播放。

- 原演出核心的 22 份 C# 源码已迁移，包含 `MyPlayable`、序列、双阶段调度、延迟增删、互斥、引用计数、触发器、回调、原始有序容器、源对象应用与粒子淡入淡出，保留原数值和状态顺序。隔离工程的 Unity 编译、完整工程校验及 Play Mode 逻辑验证通过：速度／初始帧、淡入淡出／回调顺序、互斥延时、引用计数、帧内增删、暂停／隐藏／恢复、序列及错误停止。3 项迁移回归防止日志适配吞掉原编号异常分支与后续方法。当前只证明调度状态逻辑；原生视觉、Timeline、相机／音频、正式地图输入和存档仍未完成。

- 龙之国音频已从所选官方 APK／871 云端包恢复为 418 个无损 PCM 素材：400 个直接对白、17 个演出独立素材及地图 BGM。读取实际 EventBankMapping、HIRC、DIDX／DATA 与流式 WEM，保留来源哈希、原总线／FX／RTPC 字段和对象原始字节；不使用旧 Ogg 二次有损转换。BGM 保留原四类事件、Sine 渐变时长与 −1dB MakeUpGain 配置，实际总线 DSP 尚待实现。
- `chapter_5_main_36_17` 并非缺少官方语音：原 `CallDragon` 设置“龙国圣兽地台”状态后，由 `SuiMo_StoryShow02Timeline` 在 5.5166667／7.7333333 秒触发 `_17_1`／`_17_2`，并显示 `bubble_dialog_16_0`／`16_1`。已恢复全部 5 份实体 Timeline、原轨道／片段字段和 4 条气泡配置／中文；不把两段语音拼接为直接对白，也不以静音代替。六项实体 Timeline 音频连同原演出共 17 项引用、24 个顺序动作，保留 400／1000／1700／5000 毫秒延迟及对象静音／取消静音动作；流式预取片段与完整 WEM 分别记录。
- 音频来源解析 5 项回归、根工程静态检查（68,630 个 GUID）、隔离 Unity 导入／编译及 `ProjectTools.Validate` 通过。内置屏带画面 Play Mode 对全部 418 个素材逐采样比较原 PCM、核对导入参数并执行实际 AudioSource／监听器输出，全部通过。本次只验证素材与设备播放；正式对白／Timeline 调用、原总线 DSP／RTPC、气泡窗和实体绑定、地图输入／存档及 Windows Player 尚未完成。恢复入口为 `scripts/prepare_unity_map14_audio.py`、`prepare_unity_map14_dialogue_audio.py`、`prepare_unity_map14_playable_audio.py`、`prepare_unity_map14_entity_timelines.py`；来源为 Map14 audio／dialogue-audio／playable-audio／entity-timelines 四份清单。

- 原 `MyPlayableSound`、`MyPlayableTimeLine` 及三份 Wwise Timeline 代码已迁入 `Runtime/Official/StorySounds/`，复用已核对官方 SDK 简单 Post 的事件 ID／Playing ID 桥接。保留 `Time - deltaTime` 循环边界、跨大帧只补一次、目标为空回退自身、停止／重用顺序；Timeline 源绑定优先于目标绑定、手动时钟使用缩放 delta，仅 None 外推采用有限时长，原音频片段暂停／结束不额外发送停止事件。内置屏隔离 Unity 导入／编译、`ProjectTools.Validate` 及 Play Mode 的原组件绑定、循环／暂停／重用和两段咒文实际 Timeline 调度通过。来源为 `map14-story-sound-runtime-source-manifest.json`，恢复器为 `scripts/prepare_unity_story_sound_runtime.py`；本轮记录原事件调度，未接通音频总线 DSP／RTPC、完整原生 Timeline／气泡／地图流程或存档，也未发布完整功能。

- 原 `AnimationController`、`StateMergeList<T>`、`MyPlayableAnimator` 已迁入 `Runtime/Official/StoryAnimation/`，保留初始化前缓冲、同一来源覆盖／取消、来源分层恢复、嵌套暂停、Animator 替换通知、允许状态限制，以及淡出／停止恢复和池复用。保留原层权重使用各自快照、布尔／浮点／整数使用来源栈的差异，未改写为另一种合并规则。仅将反编译 C# 13 自动属性适配为等价 C# 9，并替换诊断日志。内置屏隔离 Unity 导入／编译、`ProjectTools.Validate` 和原生 Animator Play Mode 的参数／状态调度验证通过；夹具使用隔离控制器，不代表原角色／原控制器、骨骼／换装／物理约束或实际画面已经恢复。来源 `map14-story-animator-runtime-source-manifest.json`，恢复器 `scripts/prepare_unity_story_animator_runtime.py`；原模型／动画资源绑定、正式地图输入／存档和 Windows 发布继续迁移。

- 原外部剧情节点注册接口、管理器、启停注册组件及 `CinemachineTrackStoryBinder` 已迁入 `Runtime/Official/StoryNodes/`，保留同键覆盖后通知、缺项注销仍通知、Unity 已销毁对象语义、匹配列表注册／全部键注销，以及相机轨道仅补空绑定。两份异步方法按原 MoveNext 调用链恢复：可选 `UniTask.WaitUntil`、原 PlayerLoopTiming 8、原令牌与非立即取消，等待后再次读取原节点；不等待分支沿原实现不使用令牌。显式注入替代 DI，职业界面注册可复用同一宿主。内置屏隔离 Unity 导入／编译、`ProjectTools.Validate` 与 Play Mode 的注册、覆盖、启停、类型查找、延迟注册、取消、已销毁对象及原生 Cinemachine Timeline 绑定通过。来源 `map14-story-nodes-runtime-source-manifest.json`，恢复器 `scripts/prepare_unity_story_nodes_runtime.py`；原完整 Timeline／虚拟相机／角色／气泡、实际地图画面、输入和存档仍待接通。

## 自定义实现

原演出核心以显式中心注入及调用方提供两个原帧阶段替代 DI／R3 注册；日志接入 Unity，移除只用于 Inspector／MessagePack 的属性。调度、数值、状态和回调顺序保留，正式宿主的生命周期接入仍待验证。

以下为用户确认并已接入独立规则层的单机处理；正式地图宿主、菜单、背包交易和存档尚未接通。

官方 `story[EntityVineLock].ResPath` 为 `Map10Story/ChickChaos/Entity/EntityVineLock`，当前资源清单实际路径为 `Map10Story/ChickChaos/EntityVineLock.asset`。离线恢复按唯一同名原 StoryGraph、实际 GUID `499633388cadee742a44e8e976dd624c` 与根对象类型验证并显式绑定；原图和条件保留。配置路径与实际路径分别记录在[公共剧情来源清单](../../assets/reference/official-map/map14-story-dependencies-source-manifest.json)，不将修复后的路径说成官方客户端原路径。

剧情 `HelpHuiAfterBattle` 的 `HandInItemNode`（节点 15）按 `hand_in_item[4]` 提交 `2208×1`，最大次数为 1。用户确认离线规则为在单次本地事务中按表原子扣除配置物品，并且只在成功后增加提交次数；失败回滚，回放相同凭证不重复扣除。官方 `CanHandInItemNode` 只查询次数上限；窗口按是否持有配置物品启用确认。确认、取消和中断分别沿原 `ComfirmExit`、`CancelExit`、`InterruptExit` 分支；Map14 正式剧情宿主尚未接通，不能据此认定该任务可游玩。

火炮终端共 7 个：`1512`、`2914`、`5015`、`6912`、`8011`、`8516`、`9413`，原类型为 `MapWorldPuzzleType.Cannon=4`。自动解谜使用原实体类 `6502` 的道具费用 `325×1`，仅清除该终端 `NeedRemoveLoEntityIds` 中的障碍，重复目标去重。不模拟炮弹发射、成功演出或失败状态，不生成额外奖励。重置只清除这些障碍的 `destroyed` 标记，不改变终端状态、玩家位置、其他机关、费用或奖励。

五组走格子终端为 `909`、`1715`、`3612`、`4214`、`4505`，放置配置将 `PuzzleType` 覆盖为 6；分别指定辅助锁 `903`、`1708`、`3613`、`4215`、`4517`。用户确认沿用[鲸背岛](map-16.md)方案：辅助消耗 `325×1`，仅解开配置指定的锁；重置只恢复本组踏板和辅助锁，保留奖励及领取记录；完成后按原顺序执行奖励生成链。当前 Common／客户端只有自动完成和重置标记，没有服务端执行器，不能将此适配写成官方服务器实现。

## 尚未实现

- 恢复实体 prefab、角色／实体骨骼、粒子、动画、MiniMap_14 迷雾／标记、BGM 与剧情音频，并接入官方 HUD、对话和交互界面；将已恢复的地区标签绑定正式本地地图状态。
- 接通 111 区、2,961 个放置实体、8 块战斗碑、3 处遗迹、7 组火炮、5 组走格子及地图 15 原出口的正式入口、原条件、剧情、交互、战斗与奖励。不得以配置提取或地形夹具代替完整玩法。
- 接通正式地图背包交易、本地状态和存档，完成正常游玩、失败回滚、退出与冷读恢复。提交窗口的隔离夹具已覆盖模拟鼠标与存档事务；其他资源夹具不代表正式地图流程。
- 完成编辑器与 Windows Player 的受影响流程、模拟输入、完整画面及存档验证，提交并发布包含本图成果的 Windows 可执行版本。当前没有本图可游玩版本交付结论。

### 官方来源与入口

- [当前实体模板／雕像配置转换](../../scripts/prepare_unity_map14_interactions.py)、[来源与运行绑定](../../assets/reference/official-map/map14-interactions-source-manifest.json)、[来源与地图选择回归](../../scripts/test_prepare_unity_map14_interactions.py)及[原结果预览／模拟关闭检查](../../unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Map14Statues.cs)。目录为 `Map14/interactions` 与 `Map14/stories`；运行检查入口 `Zjcs.Editor.OfficialMap12PlayModeAcceptance.RunUiMap14Statues`。

- [通用 UI 提取](../../scripts/prepare_unity_map14_ui.py)、[共享原生导入器](../../unity/Assets/Game/Editor/OfficialMap11UiTools.cs)、[UI 显式绑定与哈希](../../assets/reference/official-map/map14-ui-binding-source-manifest.json)及[原生层级／模拟点击检查](../../unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Map14Ui.cs)。原始投影在 `Assets/Official/World/Map/14/UI/`，本地原生 prefab 入口为 `Resources/Map14/UI/`；提交窗口仍使用独立 `HandIn/` 及其原来源收据。

- [当前战斗数据提取](../../scripts/prepare_unity_map14_battle_data.py)、[原对象／构造器／formatter／公式来源](../../assets/reference/official-map/map14-battles-source-manifest.json)、[运行载荷生成](../../scripts/prepare_unity_map14_battle_runtime.py)、[运行绑定来源](../../assets/reference/official-map/map14-battle-runtime-source-manifest.json)及[数据 Play Mode 校验](../../unity/Assets/Game/Editor/OfficialMap14BattleDataAcceptance.cs)；共享怪物构造位于 `OfficialMainlineMapRuntime.BattleMonsters.cs`。

- [提交窗口字段／素材生成器](../../scripts/prepare_unity_map14_handin_ui.py)、[官方 UI 严格解码](../../scripts/map14_handin_ui_source.py)、[原对象／代码／输入输出来源清单](../../assets/reference/official-map/map14-handin-ui-source-manifest.json)、[原生绑定来源清单](../../assets/reference/official-map/map14-handin-ui-bindings-source-manifest.json)；原始投影位于 `Assets/Official/World/Map/14/UI/HandIn/`，运行目录为 `Resources/Map14/UI/HandIn/`，Unity 绑定入口为 `Zjcs.Editor.OfficialMap11UiTools.ImportMap14HandIn`，故事回调为 `OfficialMainlineMapRuntime.HandIn.cs`。

- [配置生成器](../../scripts/prepare_unity_map14_sources.py)、[配置来源](../../assets/reference/official-map/map14-source-manifest.json)；载荷为 `Assets/Official/World/Map/14/Source/`。
- [统一地图定义生成器](../../scripts/prepare_unity_map14_definition.py)；输出 `Assets/Official/World/Map/14/map14.json` 并对照本图来源收据核验导航、区域、位置及默认入口。
- [原生输入恢复](../../scripts/prepare_unity_map14_native.py)、[输入及组件来源](../../assets/reference/official-map/map14-native-input-source-manifest.json)。
- [实体输入恢复](../../scripts/prepare_unity_map14_entity_inputs.py)、[原组件序列化描述](../../scripts/map14_entity_native_schemas.py)及[实体／显示／动态贴图来源](../../assets/reference/official-map/map14-native-entity-input-source-manifest.json)；完整原字段载荷为 `Source/native-entity-components.json`，临时转换输入保存在 `.tools/map14-entity-native-cache-871_156598/`，不进入运行时。
- [实体原生导出](../../scripts/prepare_unity_map14_entity_export.py)与[逐字段／指针核对来源](../../assets/reference/official-map/map14-native-entity-export-source-manifest.json)；使用 `--convert` 执行私有转换器，原顺序恢复结果在同一缓存的 `restored/` 中。
- [实体材质来源恢复](../../scripts/prepare_unity_map14_entity_materials.py)及[逐字节／指针来源](../../assets/reference/official-map/map14-native-entity-materials-source-manifest.json)；原字段载荷为 `Source/native-entity-materials.json`。
- [材质原生绑定生成器](../../scripts/prepare_unity_map14_entity_materials_native.py)、[材质／shader／原纹理指针来源](../../assets/reference/official-map/map14-native-entity-materials-native-source-manifest.json)、[材质 Unity 绑定收据](../../assets/reference/official-map/map14-native-entity-materials-binding-source-manifest.json)及[Unity 材质导入与显式目录](../../unity/Assets/Game/Editor/Map14/EntityTextures/OfficialMap14EntityMaterialTools.cs)；资源目录为 `Map14/Native/native-entity-materials-catalog`，按 `CAB:Material:PathID` 查找。
- [实体原纹理恢复](../../scripts/prepare_unity_map14_entity_textures.py)、[纹理来源](../../assets/reference/official-map/map14-native-entity-textures-source-manifest.json)、[纹理 Unity 绑定收据](../../assets/reference/official-map/map14-native-entity-textures-binding-source-manifest.json)、[ mip 回读](../../unity/Assets/Game/Editor/Map14/EntityTextures/OfficialMap14EntityTextureAcceptance.cs)与[shader 原生转换](../../scripts/prepare_unity_map14_entity_shaders.py)、[shader 来源](../../assets/reference/official-map/map14-entity-shaders-source-manifest.json)、[shader Unity 绑定收据](../../assets/reference/official-map/map14-entity-shaders-binding-source-manifest.json)、[绑定及变体验收](../../unity/Assets/Game/Editor/Map14/EntityShaders/OfficialMap14EntityShaderTools.cs)。
- [机关投影](../../scripts/prepare_unity_map14_puzzles.py)、[官方代码／规则来源](../../assets/reference/official-map/map14-puzzles-source-manifest.json)、[本地绑定](../../assets/reference/official-map/map14-puzzles-binding-source-manifest.json)及[Play Mode 规则检查](../../unity/Assets/Game/Editor/Map14/OfficialMap14PuzzleAcceptance.cs)。运行入口为 `Map14PuzzleAssets` 与共享 `OfflineMapPuzzleSession`；`AuxiliaryUnlockOnly` 仅对已确认的本图五终端启用，不改地图 11／12 的原辅助行为。
- [地形转换](../../scripts/prepare_unity_map14_native_terrain.py)、[地形来源](../../assets/reference/official-map/map14-native-terrain-source-manifest.json)及[Unity 绑定](../../assets/reference/official-map/map14-native-terrain-binding-source-manifest.json)。
- [裸场景转换](../../scripts/prepare_unity_map14_native_scene.py)、[裸场景来源](../../assets/reference/official-map/map14-native-scene-source-manifest.json)及[Unity 绑定](../../assets/reference/official-map/map14-native-scene-binding-source-manifest.json)。
- [原 shader 转换](../../scripts/prepare_unity_map14_scene_shaders.py)、[shader 来源](../../assets/reference/official-map/map14-scene-shaders-source-manifest.json)、[导入检查](../../unity/Assets/Game/Editor/Map14/OfficialMap14NativeSceneTools.cs)及[区块运行检查](../../unity/Assets/Game/Editor/Map14/OfficialMap14PartitionAcceptance.cs)。共享原地形与环境代码位于 `Runtime/Official/World/NativeTerrain/`、`NativeEnvironment/`，本地加载边界位于 `Runtime/Offline/World/NativePartition/`。

- [蝴蝶原生动画与 shader 转换](../../scripts/prepare_unity_map14_butterflies.py)、[来源](../../assets/reference/official-map/map14-native-butterflies-source-manifest.json)、[显式绑定](../../assets/reference/official-map/map14-native-butterflies-binding-source-manifest.json)及[独立原始数据参考](../../scripts/map14_butterfly_reference.py)；运行边界为 `Runtime/Offline/World/NativeMap14/`。
- [环境分区恢复](../../scripts/prepare_unity_map14_environment_scene.py)、[原生来源](../../assets/reference/official-map/map14-native-environment-scene-source-manifest.json)、[绑定](../../assets/reference/official-map/map14-native-environment-scene-binding-source-manifest.json)、[共享环境来源核对](../../assets/reference/official-map/map14-shared-environment-source-manifest.json)及[实际运行检查](../../unity/Assets/Game/Editor/Map14/OfficialMap14EnvironmentSceneAcceptance.cs)。
- [地区标签代码投影](../../scripts/prepare_unity_map14_label_components.py)、[代码／语言／配置来源](../../assets/reference/official-map/map14-label-components-code-source-manifest.json)、[原生转换](../../scripts/prepare_unity_map14_region_labels.py)、[素材来源](../../assets/reference/official-map/map14-native-region-labels-source-manifest.json)、[显式绑定](../../assets/reference/official-map/map14-native-region-labels-binding-source-manifest.json)及[完整分区运行检查](../../unity/Assets/Game/Editor/Map14/Labels/OfficialMap14LabelAcceptance.cs)。运行边界位于 `Runtime/Official/World/NativeMap14Labels/` 与 `Runtime/Offline/World/NativeMap14Labels/`；显式目录为 `Map14/Native/native-region-labels-catalog`。

新增原生素材独立保存在 `Assets/Official/World/Map/14/`，尚未生成完整素材快照，不能声称已进入 `asset-pack` 备份。
