# 技能、方案与培养

[总索引](../README.md)

状态：未完成

## 功能简介

人物页进入技能，包含战技、秘法、碎片、推荐、详情、七套方案、共享槽位升级、祝福培养、单技能升阶及批量升阶。

人物页的技能入口已连接七套方案面板。技能档案与角色主档案共同保存，离线规则和人物页数据源已接入；技能窗口、推荐、碎片详情、合成确认、培养页及升阶结果均按官方布局数据构造并连接本地事务。推荐页使用 `skill_recommend`、`skill_recommend_item` 与 `skill_tab` 官方布局，按当前职业前置链和阶位显示方案。官方确认弹窗已绑定，纯祝福一键升级在预演达到 40 次时要求确认。技能页、详情、培养和结果弹窗使用 `skill-ui.json` 的官方动画轨道；结果列表逐项展示、跳过和自动滚动，槽位培养播放官方动画和 `Skill_Upgrade.ogg`。技能页现在使用 `character_skills` 的官方整屏 `CharacterPanelSkillPanel` prefab，包括返回条、方案与技能槽、战技／秘法／碎片列表和底部“技能／馈赠”双页；底部第二页按官方 `Achievement` 状态切换。技能列表使用官方 `skill_list_row` 与 `skill_list_separator`，行列数和间距按 `AdaptiveRowLayoutCalculator` 计算（155 px 项目、两侧各 50 px、最小间距 20 px），合成、收藏和未收藏分组各自起行，收藏组与未收藏组之间按官方条件显示虚线分隔；碎片页按官方 160×160 网格尺寸排布。打开技能时播放官方 `CharacterPanel_Skill` 状态：保留人物预览和背景，按原动画隐藏资料区并调整模型位置；返回时播放 `CharacterPanel_SkillSwitchmain` 并恢复被修改的动画属性。整屏界面与主要技能操作已经通过本页所述 Play Mode 验证；40 级伤害测试及通用窗口表现仍未完成，不能据此将整个技能功能标为完成。

## 原始官方实现

官方技能页按职业、前置链、品质、元素、标签、范围、安装槽、附加属性和升级／突破配置刷新；详情与模型预览使用原控件、数值计算及显示规则。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

复核固定版本 `854_156383` 的 `CharacterPanelSkillPanel`、`SkillPanelSkillDetailDialog`、`SkillRecommendDialog`、`SkillRecommendDialogItem`、`SkillTabGroupItem`，以及客户端 `CharacterPanelSkillContent.BeginPrepareEquip / RefreshEquipedSkill` 和 `SkillPanelSkillDetailDialog.RefreshBtn`。原有实现未给七个空技能槽生成 `ItemIconEmpty`，将“推荐”误接为祈愿，并使用通用 `SkillDetailDialogPanel`；安装时直接追加技能，无法选择位置或替换满槽。

`python scripts/prepare_skill_ui.py` 使用既有 APK 读取器、同版本 CDN 缓存和已校验中文文本分包，转换上述五个原始 prefab、禁用图像状态、遮罩、高亮框、中文文字及脚本引用目标；运行时资源继续纳入 `ui.json` 和显式本地加载清单。`skill_recommend.g` 经原始 `TbSkillRecommendInfo` 构造器完整读取，名称和说明取自原始中文表，输出为 `assets/official/skills.json`。bundle / 对象、APK / CDN 来源、原始配置与输出 SHA256、中文分包凭据及行为来源代码哈希见 [skill-ui/source-manifest.json](../../assets/reference/skill-ui/source-manifest.json)。没有新增自绘图片。

技能页恢复四个战技与四个秘法槽位的原始空槽素材，详情使用技能专用 prefab 和同一套官方遮罩图标；点击安装 / 替换后使用原版暗色遮罩与同类型四槽高亮，选择目标槽位后才提交本地事务，取消不改档。推荐页使用原始方案与说明，仅安装已拥有且职业适用的技能，未获得部分保持空位。新增槽位位置与方案名称兼容旧紧凑数组，并随本地存档保存；原技能等级、升阶费用和战斗公式仍属已有单机适配。

技能列表排序／收藏字段的原 C# 输入哈希见 skill-ui/source-manifest.json 的 list_behavior；全部开关使用原 CommonCheckItem_On／Off。CharacterPanel_Skill 的原 AnchorMin／AnchorMax 曲线从固定 APK AnimationClip 解码，输入对象哈希见 character_animation_inputs；未用手调坐标补偿。收藏值保存到本地存档以替代原网络操作，界面状态和分组仍取原客户端实现。

`prepare_skill_quality_text.py` 从固定 `854_156383` 的 APK／匹配 CDN 读取 `GlobalColors.asset.QualityColorConfig` 的 Normal 调色板，以及 `SkillPromotePanel`、`SkillRankUpNotifyDialog`、`SkillPanelSkillDetailDialog`、`SkillDetailDialog` 中六个品质文字的 `TMPColorGradient`。原四个 RGB 关键点、两个独立 alpha 关键点、Blend 模式和各组件 0°／45° 角度导出到 `unity/Assets/Official/Skills/skill-quality-gradient.json`（SHA256 `a735d8107a91afa88c592a5077b7955c489fafd1d084123ae728470697cb632e`）；没有使用 `GreenRainbow` 四角预设或绘制图片。技能详情、培养和升阶结果的原生 TMP 文本网格由 `OfficialSkillQualityTmpGradient` 接收原关键点。原 bundle、对象、CDN URL、输入字节、调用源码及生成绑定 SHA256 见 [skill-quality-text/source-manifest.json](../../assets/reference/skill-quality-text/source-manifest.json)。

渲染迁移依据恢复后的同 APK `TMPColorGradient.OnPreRenderText`（`0x1121dd44`）、`SetGradientColor` 和 `UpdateMesh`，保留每行未旋转范围与绕原 RectTransform 原点旋转后的顶点归一化、顶点颜色替换和 Color32 的 ties-to-even 舍入。原顶点与取色样本保存在 `scripts/fixtures/skill_quality_native.json`，`SkillTextValidation` 对运行时计算执行 36 个顶点与 10 个颜色样本的逐字节比对；原生镜像、metadata、Unity 引擎和方法字节哈希同存来源清单。

技能详情与角色预览继续使用固定 APK `854_156383`：`prepare_skill_detail_data.py` 解码原 `SkillItemRangeInfos.g.bytes` 的 1,365 条范围记录，恢复 `SkillTagItem`、标签枚举及职业归属／前置链；布局明确绑定 174 张原 UI 精灵。`prepare_skill_native_text.py` 先比对固定 APK 原始对象 SHA-256，再复用对象完全一致的既有原生 FZY4K 字体／材质／Alpha8 图集与 TMP shader 转换，为技能建立独立 GUID 和运行时副本；保留初始图集，避免 TMP 构建处理修改共享资源。`prepare_skill_character_shader.py` 同样核对 APK 中 `Universal Render Pipeline/Spine/SkeletonDye-2D` 原对象与既有转换凭据，保留全部 544 组 GPU 程序，仅调整桌面预览 pass 标签；没有自绘替代角色素材。

依据：`assets/reference/skill-ui/unity-icon-source-manifest.json`、`assets/reference/skill-quality-text/source-manifest.json`。

## 完整还原部分

技能列表附件已按原 `ItemIconAttachmentConfig` 的层级顺序恢复：`CenterHintContent`（点击合成）、`ProfessionMismatch`（职业不符）、`NewMark` 和 `RedDotContent` 均使用原 prefab、字体及组件字段。红点沿 `RedDotMultiItem` 组合原升阶、装配及合成条件；原行项目是否考虑职业、碎片列表不挂这些附件等分支保持客户端配置。装配附件属于技能皮肤页的原附件类型 12，不额外叠加到原列表。新获得标记按原 `ItemNewMarkManager` 的零数量到正数量、正数量到零数量分支更新，点击技能、刷新列表或关闭技能列表时移除上次列表涉及的标记（包括视口外项目），当前玩家会话共享，重新登录清空；不写入主档案。规则测试、带画面 Play Mode 及 675×1463 独立 Windows Player 已通过，模拟合成、点击新技能、全部开关、过滤后返回和滚动显示职业不符项目，并实际查看附件截图。同帧重复的数据投影不会提前消费尚未显示的新技能标记；刷新停止旧列表惯性并恢复首项。

伤害测试结算恢复原 `map/win_01`／`map/injuried_idle` 骨骼动画，不再仅切换空 Animator 状态。复用项目独立原生骨骼、曲线、IK、插槽及绘制顺序求值器，沿原骨骼顺序变换官方附件网格；表情附件来自 `AppearanceConfig` 的 UI 附加皮肤 `performFace`／`map_01`，补齐创建角色专用 bake 未包含的眼睛和嘴部附件。源骨骼、图集及纹理沿用已核验的 `871_156598` 人物资源，UI 动作名称与附加皮肤配置仍取固定 APK `854_156383`，不混改原始哈希。男女角色真实伤害测试的胜利动作与受伤动作的独立网格探针已通过带画面 Play Mode 检查，独立 Windows Player 的真实胜利流程与受伤探针也已通过并查看截图；实际失败分支另已通过 Play Mode 与独立 Windows Player：隔离档案的濒死角色经真实怪物攻击死亡，进入失败结算、受伤动画、统计与技能快照，并检查返回设置和不创建战报目录。已查看实际运行画面。

方案名称已恢复原 `TMP_InputField`（对象 `5229620140741382476`，原始 580 字节 SHA-256 `12c6afe0debddb1e9a5bd164cc495b7f1f0ede5acc182a01872a6bd0728d104c`），保留原文字视口、字体、32 px 字号、颜色、光标与单行配置，不再添加旧 InputField 或透明替代图像。官方 TMP 分支额外序列化的 `m_EncodingType` 根据 APK 元数据恢复，严格检查完整对象字节。名称校验直接迁移 `Common.CJK` 与 `FormulaHandler.CheckSkillPlanNameLegal`：当前区域配置上限 12 计数单位，中日韩文字及原扩展区计 2，按原 StringInfo 文本元素计数；空名称、超长、`#<>`、半角／全角空格分别保留原错误分支，不执行 Trim。保留旧版本已接受的名称，空名称恢复“方案一”至“方案七”。单机没有在线 ContentCustom 禁用标志，原禁用遮罩保持隐藏；其原用途是禁用时重置名称，并非编辑入口。纯规则测试和带画面 Play Mode 均已通过，实际点击输入框、模拟 Ctrl+A 与逐字键盘输入、点击切换方案、检查成功／拒绝提示及硬盘主档案重读，覆盖六个中文字符、七个中文字符、含空格名称及十二个英文字母；另有表情计数和旧名称兼容规则测试。已查看实际运行截图。

固定 APK 的 `GameSettings.SkillPlanMaxCount=7` 已恢复；旧单机档案的三套方案原样保留，并补入原文名称的方案四至方案七，不改变已选方案、装配、幻兽及共享槽位等级。方案下方按原 `Scrollbar` 的 Handle、20% 尺寸、颜色、方向与不可交互状态恢复，步数与 `CharacterPanelSkillPlan.RefreshView` 一样使用七档。`scripts/test_skill_plans.ps1` 已通过旧档案保留、重复规范化、异常数组拒绝及第七方案选择检查；七套方案的模拟点击循环、首尾回绕及进度条位置已通过 Play Mode 与 675×1463 独立 Windows Player 回归，并核对实际画面。

Unity 已新增离线技能档案与规则服务 [OfflineSkillProfile.cs](../../unity/Assets/Game/Runtime/Offline/Skills/OfflineSkillProfile.cs)、[OfflineSkillProgression.cs](../../unity/Assets/Game/Runtime/Offline/Skills/OfflineSkillProgression.cs)。模型保存七套方案、主动／被动槽位、基础等级、按赛季分开的祝福等级、技能阶位与道具数量；服务提供方案切换／改名、技能安装／替换／卸下、单槽及一键培养、单技能及批量升阶。培养和升阶在档案副本核算费用后提交。

固定 APK `854_156383` 的 394 个主动／被动技能、职业链、阶位累计差额、槽位培养、人物等级限制及材料已由 [prepare_unity_skill_runtime.py](../../scripts/prepare_unity_skill_runtime.py) 按三个来源清单校验并整理为 Unity 可解析的服务 DTO。固定 APK `item` 表确实缺少 18 个由 `Formula.GetPieceIdBySkillId` 计算出的碎片条目；另 190 条有效技能／碎片配对具有相同的稳定字段关系：碎片名为技能名加“碎片”，RawIcon 沿用技能 RawIcon，碎片类型为 111、Rank 为 -1、Quality 为 1。运行时 [OfficialCharacterAssetCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs) 仅对缺失且能映射至职业技能条目的碎片 ID 按此官方记录规律生成展示元数据；`OfficialSkillAssetTools.Validate` 校验 18 个目标 ID 的映射。该派生不声称缺失 `item` 原始行已从服务器配置取得。当前 `871_156598` 资源清单含技能图标图集，但未包含 `item` 规则表。[OfficialCharacterAssetCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs) 同时载入技能培养规则和 `skill-ui.json`；[OfficialSkillAssetTools.cs](../../unity/Assets/Game/Editor/Skills/OfficialSkillAssetTools.cs) 提供技能载荷导入绑定与哈希校验入口。官方 UI 层级和 270 张布局引用图像已整理至 `unity/Assets/Official/Skills/`，包括人物技能页与培养布局及原始 RectTransform 参数。

人物主存档中的 `OfflinePlayerProfile.Skills` 由 `OfflineMainlineProgressSession` 校验和保存，`OfflineCharacterProgressionService` 在角色事务内同步技能材料与背包。人物页入口由 `OfficialCharacterPanelView.OpenSkillPanel()` 打开 `CharacterSkillPanel`。该面板已连接方案选择／改名、收藏、技能列表、安装／卸下、拖拽交换、推荐、碎片详情、合成确认、单技能升阶、升阶结果及批量升阶结果。技能页通过 `skill-ui.json` 中的 `character_skills` 完整 prefab 构建，嵌套列表和详情／培养／结果弹窗使用各自的官方模板；技能列表使用官方自适应横排与分类分隔符，碎片列表使用官方 160 px 网格项目。底部“技能／馈赠”页签按 `CharacterPanelSkillPanel` 的 SkillContent／Achievement 状态切换。打开技能面板时保留人物预览与背景，按官方 `CharacterPanel_Skill` 动画切换资料区和模型锚点；关闭时按 `CharacterPanel_SkillSwitchmain` 恢复。UI 字体从官方资源初始化；资源不可用时改用系统中文字体和 Unity 内置字体回退，避免字体查找递归。编辑器校验工具绑定技能载荷、官方图集、官方布局、槽位升级动画和音效，并按 `source-manifest.json` 校验文件哈希。`Zjcs/Validate/Official Skills Assets` 已校验 394 条技能规则、6 组品质渐变、来源哈希、18 个派生碎片映射和 270 个技能 UI 精灵。技能规则载荷与固定 APK 来源重算结果一致（394 条）；静态工程检查通过；Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和整屏 Play Mode 检查通过。Play Mode 用隔离档案和 EventSystem 首次命中校验，模拟人物技能入口、方案、推荐应用、战技／秘法／碎片、全部、收藏、槽位拖动、培养和单次／批量升阶，并检查材料不足回滚、档案重载及返回资料页。头像像素检查和实际截图已执行；合成与部分失败分支仍含服务直接调用，不能代替相应端到端点击检查。Windows Player 已重建并通过实际运行验证，范围见下文。

技能槽使用官方 `item_skill` 和 `item_empty`，不额外添加培养按钮；点击已装备槽位进入官方技能详情，再由详情培养入口操作。图标替换时恢复原颜色，空槽使用原灰色加号精灵，槽位等级使用“级”。九宫格图像恢复导出记录的原 Sprite border。星级按 `skill_rank` 的 Quality／RankAddition 显示，数字星阈值 4 与品质星图映射由原 `SkillStarInfo`、`StarMiniInfo` 组件提取，来源凭据见 `assets/reference/skill-ui/unity-icon-source-manifest.json`；可用 `scripts/prepare_unity_skill_icons.py` 重建。

技能页顶栏复用已恢复的官方 `WorldCanvas/PublicPlayerInfo`、`PublicTopBar` 与 `MoneyUI` 原生 prefab，包含头像、等级、经验、名字、战力与技能升阶材料计数。`TabItemTopBarSwitcher.ShowTypeList=[18]` 对应 `GameSettings.SkillSeniorMaterialItemId=42200`，不显示其他货币；切到馈赠隐藏玩家信息，返回技能恢复。`OfficialCharacterSkillHeader` 保留官方布局与胶囊裁剪网格，按原头像摄影参数、皮肤排除项和 `home/idle_01` 生成头像；D3D 投影矩阵经 GPU 适配。人物预览依原骨骼的 100 UI 单位映射定位，世界几何不继承 Canvas 缩放；无幻兽时按原 `HasAnyPetCondition` 隐藏幻兽占位。布局读取同时修复弧度旋转、内容自适应、透明射线目标与文本溢出。窗口使用原 1080×1920 Expand 缩放；主页入口改为独立人物 Canvas，避免嵌套画布忽略自身 CanvasScaler 而继承主页宽高混合缩放，造成槽位放大、方案和列表下移；该入口的新缩放断言、伤害测试完整流程及运行画面已通过 Play Mode；桌面安全区域按 Windows 全窗口处理，测试另检查与参考图相同的 675×1463 比例，角色外观、技能、等级和材料显示当前档案数据。

Editor 程序集显式引用 `Zjcs.Presentation.Character`，以供技能 Play Mode 验收访问人物技能面板；该引用由 `Zjcs.Editor.asmdef` 声明。

馈赠沿用原 `AchievementPanelWithTypes → AchievementPanelBase → AchievementPanelItem` 的两组数据键：Group 33 为 `CostItem(42200, SkillLevelUp)`，保留 895 条固定奖励；Group 9 为 `SkillScoreCount`，由原 15 条初始记录及 4,500×15 条循环增量记录展开为 67,515 个里程碑。积分按 `PlayerSkillDataExt.GetSkillScoreCountByAllSkill` 汇总持有技能各阶位的 `skill_rank.Score`，列表按原搜索范围、已完成优先排序及五行数量显示。点击领取按原 group、count=-1 行为，在同一本地事务中发放当前组全部可领奖励并保存领取凭据；重复领取不增加道具。培养材料累计值只在实际成功的槽位升级事务中增加，旧存档缺少此历史字段时从 0 开始，不由库存或当前等级反推。原页签动画、状态颜色、箱子、奖励图标和文案已绑定；原 TMP 自动缩放及换行参数从固定 APK 组件恢复。规则检查 `python -B scripts/test_skill_achievements.py` 已通过，完整技能 Play Mode 加入材料馈赠的模拟点击领取、重复领取防护及品质页切换后再次通过。奖励图标已连接原 `ItemDetailDialog`：显示品质、数量、原说明及来源条件，按原规则隐藏不可用的使用／附加属性区域。

人物预览的阴影已恢复原 `AttachmentFollower` 对 `shadow` 骨骼的位置与局部缩放跟随，不再保留 prefab 编辑态的静止位置。Windows 程序通过正常本地启动进入隔离技能档案，使用 EventSystem 首个射线命中、按下／抬起／点击验证入口、方案、推荐、分类和返回，且实看运行截图、重新读取档案核对方案与装配；测试存档与用户存档分开。已发布入口为 `D:/Desktop/杖剑传说/杖剑传说.exe`；新增馈赠流程已在重建后的 Windows Player 中完成两类奖励领取、重复领取防护、页签切换和存档重载验证，未将整体技能功能标为完成。

技能页按官方 `CharacterPanel.mCharacterPanelSkillPanelParent` 挂入 `/CharacterPanel/BgPanel1/Content/TopGo/SkillContent`，恢复人物页、共享 HUD 和技能窗口的 `SafeArea` 参数。返回人物页按原节点字体绑定恢复 FZY4K 中文字体，避免原生 prefab 的缺失字体回落为方块。系统消息从 APK 原 `SystemMsgPanel` 提取层级、镜像背景、Canvas 排序与 Show／Hide 动画，收藏／取消收藏使用原文案。详情、碎片、合成确认、培养及升阶通知按原 `PopupWindow.mMaskType` 选择原生普通或模糊遮罩；模糊复用原 `ScreenBlur` 材质和四级处理，外部点击保留原 0.5 秒防误触条件。新增流程已在带画面的 Play Mode 中通过模拟点击验证：收藏提示可见，模糊背景方向正确，遮罩保持在弹窗下一层，外部点击关闭后技能页继续操作；拖拽、培养、升阶、批量结果、两类馈赠和存档重载一并回归通过。静态元数据检查、Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 已通过；Windows Player 验证待重建后执行。

技能详情已恢复固定 APK 的原生 FZY4K TMP 字体、材质和初始 Alpha8 字体图集，修复弹窗 Canvas 缺少 UV1 通道造成的方块文字；同一来源字体的标签共用一份隔离的字体、材质与动态图集，按使用数量释放；128 标签验证覆盖共享、末次释放和原图集字节不变，技能页独立副本保留原始初始图集，避免 Windows 构建清空共享字体。品质背景、普通品质文字色、星级空星、战技／秘法背景状态、技能等级及祝福显示条件按原组件恢复。`OfficialSkillDetailProperties` 迁移 `PropConfigUtils.CalcSkillViewProps` 与 `SkillPanelPropPanel.Sort` 的主动、被动、状态及召唤属性展示规则；全部 394 个技能在两组等级／阶位条件下的 788 次计算已通过 Play Mode。标签取自原 `SkillTagItem` 和 `ESkillTag`，职业图标沿用 `ProfessionBaseInfoParser.PostParse` 的归属及前置职业链；范围图从 APK `SkillItemRangeInfos.g.bytes` 恢复 1,365 条数据，保留原格子 prefab、五类颜色、间隔和行列顺序，当前可用主动技能均具有有效范围数据。新增标签、范围、属性、收藏、弹窗外部关闭与整屏培养／升阶／馈赠／存档流程已通过模拟点击 Play Mode 回归并查看实际画面； Windows Player 重建验证仍待执行。

人物预览和头像改用经原始对象 SHA-256 核验一致的官方 `Universal Render Pipeline/Spine/SkeletonDye-2D` shader 转换结果，保留 544 组原 GPU 程序、透明度截断、染色及渲染状态；仅适配桌面预览相机的 pass 选择。技能附属 prefab 与脚本字段由 `prepare_skill_extra_ui.py` 提取，18 个 `icons.json` 缺项按 APK 地址补入并显式绑定；来源见 `assets/reference/skill-ui/unity-icon-source-manifest.json`。

## 自定义实现

Windows 竖屏显示适配按用户参考画面保留顶部安全留白：1080 UI 单位宽度对应约 137 UI 单位顶部留白，随窗口宽度缩放；这是桌面显示适配参数。原 `SafeArea` 的边界计算、PaddingTop／PaddingBottom、DisableTopAdapt／DisableBottomAdapt 与技能父级绑定保持官方数据，不修改内部槽位、方案或列表坐标。模糊遮罩恢复 APK 原 RawImage UV，并按 Windows 图形接口的纹理方向适配截图采样；不改变原 ScreenBlur 算法。

用户已确认伤害测试的单机规则：使用已迁移的本地战斗配置计算怪物属性，按原参数页勾选项应用怪物固定伤害 1／无限生命。这部分属于缺少服务器实现后的单机差异；原五组怪物、难度、回合数及勾选参数已连接本地 StageType 43 战斗，保留原 40 级开放条件。

用户已确认幻兽能量的单机时序：战斗开始使用原初始能量；幻兽自己的回合开始恢复原配置能量；主人实际生命损失累计满最大生命的 1% 时恢复原配置能量，护盾吸收不计；达到阈值后在主人行动结束时按原技能配置实体化并扣除阈值。实体技能、属性及持续时间继续使用原配置。APK 中 `FightRolePetTransferEntityComponent`／`FightRolePetEnergyModification` 只消费服务器能量结果，原配置 `NaturalRecoverEnergy=50`、`MasterLoseHpRecoverEnergy=6`、`MasterLoseHpTrigerRecover≈0.01` 已核对；上述时序是用户确认的单机差异，参战接入尚未完成。

伤害测试的公共结算适配已通过规则验证：StageType 43 的怪物伤害在护盾结算前固定为 1，无限生命保留 HP 并累计完整伤害，设置可随战斗检查点恢复；关闭选项或进入普通关卡恢复普通结算。入口为 `Runtime/Offline/Combat/FightDamageTestRules.cs`，验证为 `scripts/test_skill_damage_test.ps1`；普通战斗 97 组公式及规则回归亦已通过。参数页、实际战斗、结算、玩家／技能统计页及返回设置已经在带画面的 Play Mode 中通过模拟点击；战报列标题、头像绑定、原 Image 启用标志、遮罩图像显隐及主页输入遮挡已修正并实际查看画面。头像 UV 的 Unity Rect 字段映射已通过实际绑定器的失败／成功回归及 Play Mode 截图检查。主页 shader 使用显式绑定；`OfficialSkillRuntimeShaderBuild` 在构建依赖收集前登记官方 URP，保留运行时切换所需变体，并在构建后恢复编辑器设置，Player 在首场景前恢复启动管线。存档选择与技能模糊流程在 finally 恢复活动渲染目标。独立 Player 已在 675×1463 客户区通过伤害测试、玩家／技能统计与快照详情的模拟输入，并查看实际非黑帧画面；按最新不保存战报的方案调整后，Play Mode 与独立 Windows Player 的模拟点击复测均已通过，已查看头像和隐藏分享按钮后的实际画面。主页展开／收起入口恢复原控制器默认的 `FunctionEntrysUI_Folded` 零时长动画，避免两个按钮同时命中。验证启动工具补上 Player 窗口识别，在内置屏按命令行客户端尺寸显示窗口并保持不抢前台，避免隐藏黑帧与占位矩形改变竖屏尺寸。验证同时检查 PNG 非空与参考竖屏比例，避免隐藏黑帧或错误窗口尺寸冒充画面通过。

用户最新确认：战报只需在本次战斗完成后查看，退出战报页后无需持久化到硬盘。玩家／技能统计和技能快照详情直接读取本次战斗的内存检查点，关闭窗口释放头像纹理；在线战报分享按钮隐藏，不创建本地历史战报、战报截图或服务器链接。此前保存方案的存储器及主页历史入口接入已移除。验证截图仅写入隔离验证目录，不属于游戏存档；此变更后的 Play Mode 与 675×1463 独立 Windows Player 回归已通过，检查统计／技能快照详情、关闭释放、返回设置、不显示分享按钮及不创建战报目录。

当前存档完整保存每个技能槽位等级及技能投资记录；缺少槽位字段或版本不符时拒绝读取，不执行旧投资转换。装备、费用、随机与进阶通知以本地事务提交，失败不保留部分升级。

服务器开放条件按[离线开放政策](offline-access.md)处理，保留人物、材料与实际原配方条件。

用户明确确认：将官方 Function 138 的一键批量升阶开放等级从 999 级改为与一键升级一致的 12 级；`SkillFastRanklUp`（Function 138）映射到 `SkillFastLevelUp`（Function 23）。费用、升阶上限及按钮优先条件继续使用原规则。

当前可执行版本已发布到 `D:/Desktop/杖剑传说/杖剑传说.exe`，桌面快捷方式指向该入口。构建通过 `ProjectTools.Validate`，独立 Player 的技能入口、七方案、推荐、详情／词条、馈赠及存档重载模拟输入通过；随后从正式发布目录实际启动 675×1463 窗口，完成伤害测试、玩家／技能统计、快照详情、返回设置及不创建战报目录检查，并查看正式程序实际截图。该版本包含官方 TMP 方案输入框、列表附件与新获得标记、结算原生动作和附加表情。正式目录再次启动后，675×1463 的列表合成／新获得／职业提示、键盘名称校验、槽位拖拽、单次升级与进阶、馈赠、存档重载以及伤害测试／统计／快照详情／返回设置均完成模拟输入并通过，已查看正式程序实际画面；本轮培养修复已随该入口增量发布；正式程序另已完成原材料点击、跨品质／品质 7 通知、水平培养列表点击／拖拽、角色等级上限提示及真实胜利／失败伤害测试的模拟输入，统计、快照、返回设置与不创建战报目录均通过，并实看截图。下列缺项仍按各自验证范围记录，不代表整个技能功能已完成。发布使用增量复制，保留用户存档。

## 尚未实现

- 培养页标题、四槽选择、真实材料数量／不足颜色、同品质升星与跨品质通知已按原组件绑定，并通过 Play Mode 模拟点击和实际截图检查；未装备横向列表已通过 Play Mode 模拟拖拽及点击原定位提示；角色等级上限提示与培养组前后切换已通过 Play Mode；上述培养流程另已通过 675×1463 独立 Windows Player 的模拟点击、键盘与拖拽，并查看实际截图。祝福赛季提示内容和槽级显示另已通过 Play Mode 与 Windows Player 验证。

- 馈赠原道具详情已接入并通过 Play Mode；当前奖励涉及的单人公会种植、钓鱼、养殖来源（类型 22／23／24，对应道具 1530／1540／1550）尚未连接 Unity 目的界面，不能视为全部来源已可达。
- 伤害测试参数、真实战斗、胜负结算、仅内存战报、主动与被动技能详情和原技能图标均已通过带画面 Play Mode 与 675×1463 Windows Player 验证；被动触发技能保留 `FightSourceInfo` 对应的物品、品阶和祝福槽级，状态消失后仍能查看本场快照。结算动作、附加表情和失败返回已通过实际运行验证；全队幻兽参战仍需接入。
- 通用弹窗的原遮罩／点击外部关闭和系统消息已通过 Play Mode 与 Windows Player 验证；详情规则说明、技能皮肤替换／视频预览、词条窗口已迁移并通过 Play Mode；怪物／召唤物／幻兽详情链接与馈赠来源跳转仍需补齐。

已从固定 APK 恢复 `MonsterBriefInfoPanel`、`MonsterDetailPropsDialog` prefab 与原模型绑定。怪物链接已接入原弹窗和战斗模型库；宠物实体链接按本地宠物阶段／等级、主人属性及官方 `CalculatePetEntityBaseProps`／`CalcSummonMonsterInheritProp` 组合属性。技能等级优先读取已保存等级；无保存值时按官方 `PetUtils.GetSkillRank`／`SkillRankInfoParser.GetOrigRank`，从技能品质选择原始最低 rank。已核实 `NonPlayerBriefInfoPanel` prefab 位于固定 APK 的 `defaultpackage_assets_prefabs_ui_briefinfo.bundle`（hash `0b2c9e59e6fce00f612f9070d73609c2`），`ItemIconPetSkill` 位于 `defaultpackage_assets_prefabs_ui_item.bundle`（hash `414cf104c6bdea9eddf3a365b4d345e2`），`PetSkillDetailDialog` 位于 `defaultpackage_assets_prefabs_ui_itemdetail.bundle`（hash `ed61998dccf8c8b2451eb16f49b433cb`）；所选 cloud 版本 `202610211330871` 另有实际宠物技能图集与战斗技能图标 bundle 缓存。上述对象尚未转换并绑定到 Unity 窗口；详情尚未实例化 `ItemIconPetSkill`、`SkillWithOuterTagItem`，因此主动／被动技能项、技能详情点击与实际画面交互仍未完成；馈赠来源跳转也未接入。本轮完整技能 Play Mode（升阶、换肤视频、伤害测试战斗／结算／返回）通过；`ProjectTools.Validate` 通过；隔离 Windows Player 构建成功并实际启动到离线存档选择画面，未进入技能流程。宠物链接交互尚无本次运行验证。

### 实现范围

技能文字、原生 TMP 渐变／效果、安装遮罩、取消与槽位提交及当前赛季祝福显示已恢复，伤害测试主动和被动技能使用完整槽级；带画面 Play Mode 与 Windows Player 交互验证通过。

本地祝福存档仍按既有位面阶位键保存；读取官方 `system_level_gap_limit.SeasonId` 前通过 `astrological_season_config` 映射为原赛季编号，两类编号不直接比较。规则验证包含两者编号不同的情形。

培养页恢复 `SkillPromotePanel`、`PlayerUseItemContent`、`CharacterPanelSkillItem`、`SkillPromotePanelItem`、`ItemProgress`、`SkillStarInfo`、`StarMiniInfo` 与 `SkillRankUpNotifyDialog` 的原引用和状态字段，复用已有原生图标，不另套图标层级。顶部槽位只切换选择，升级按钮才扣除官方材料；升级／进阶后复用原窗口并播放 `SkillPromotePanel_Switch`。材料 42200 从固定原道具表显式补入技能载荷并绑定原图标，点击显示原道具详情；碎片材料去掉未绑定的模板数量，并按原 `GlobalColors.LightRed`／`Green` 显示是否足够。原 Light 调色板用 `TypeList` 定位颜色，不能把颜色枚举当数组下标。培养和通知名称使用原道具名，不追加手写阶数。同品质只刷新星级；只有跨品质才打开原进阶通知，通知显示实际前后品质、星级与原 `PlayerPropItemVariant` 属性行。上述分支已通过 Play Mode 与 675×1463 独立 Windows Player 的真实射线命中／模拟点击，并查看实际画面，存档重载保留最终阶数与装配方案。

`PlayerSkillDataExt.CanSkillRankUp`／`TryGetAllSkillPromoteRank` 不限制已拥有技能的职业，单次与批量进阶已修正，装配仍保留职业限制；规则验证同时检查费用扣除。祝福升级按 `PlayerSkillDataExt.CheckSkillBlessLevelCanUpgrade`、`PlayerSkllSlotDataExt.IsBlessUpgradeLimitOtherSkillMax` 与 `SystemLevelGapLimitInfoParser` 恢复全八槽基础等级必须达到精确上限、当前赛季实际最低槽级及阈值的开闭边界；前一赛季祝福不计入当前赛季。原 `system_level_gap_limit` 的技能行按原顺序纳入本地规则，边界与跨赛季规则验证已通过。横向培养列表直接适配官方 `LoopHorizontalScrollRect`，保留原虚拟化和 `ScrollToCellWithinTime`，没有另写近似滚动公式；已通过原定位提示的射线命中与模拟点击、实际横向拖拽，截图可见原列表及选中状态；角色等级上限原提示已通过 Play Mode 与独立 Windows Player 的模拟点击和实际截图检查；祝福／赛季提示仍需专门画面验证。下一赛季限制先于角色等级和祝福差额判断，使用原赛季映射及当前赛季技能基础／祝福上限，边界规则验证已通过。

技能运行时使用本地事务保存七套方案、装配槽位和共享培养数据；方案切换不复制槽位投资，装备和费用校验按固定客户端规则执行。技能详情、碎片详情、合成确认、培养页、单次升阶成功及批量升阶结果由 [CharacterSkillPanel.cs](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.cs) 构建。单技能升阶同品质时按官方 `SkillPromotePanelRankUpContent` 刷新星级与属性；跨品质时按原 `SkillRankUpNotifyDialog` 显示升阶结果。结果列表支持逐项展示、跳过和滚动；批量展示在原 `ShowItems` 动画事件（0.76666665 秒）后启动，并读取原 `ListItemShowSequence` 的 0.1 秒间隔与首次延迟，遮罩准备期间不提前播放结果；培养播放官方槽位升级动画及 `Skill_Upgrade.ogg`。批量升阶按已确认的离线开放规则处理。

固定版本缺少的 18 个技能碎片 `item` 行，按官方 `Formula.GetPieceIdBySkillId` 与其余 190 组稳定字段关系派生显示元数据；相关字段和图标对应由 `OfficialSkillAssetTools.Validate` 检查，不声称这些行来自服务器原始表。

彩虹品质调色板取自原 `LanguageUtils.SetSkillQualityWithColor → GlobalColors.SetTextColorByQuality → MultiLanguageText.SetColor(Gradient)` 数据路径，并由 `prepare_skill_quality_text.py` 导出固定包 Normal 调色板及六个 `TMPColorGradient` 角度。技能运行时将品质 7 的渐变应用于详情、培养两端与升阶结果文字网格；培养与通知的原生 TMP 文本通过 `OnPreRenderText` 更新逐行顶点颜色，其他品质移除该效果。渐变关键点和 0°／45° 配置见 [OfficialSkillQualityGradient.cs](../../unity/Assets/Game/Runtime/Presentation/Skills/OfficialSkillQualityGradient.cs) 与 [skill-quality-gradient.json](../../unity/Assets/Official/Skills/skill-quality-gradient.json)。普通品质与品质 7 的培养／通知已通过 Play Mode 与独立 Windows Player 模拟点击，实际截图可见彩虹文字与原品质图标；运行检查确认文字网格具有多个顶点颜色。

## 代码与资源入口

以下为官方来源、既有规则与回归参考：`OfflineSkillProfile`、`OfflineSkillProgression`、`OfflineCharacterProgressionService`、`CharacterSkillPanel`、`OfficialCharacterPanelView.OpenSkillPanel()`、`OfficialSkillAssetCatalog`、`Zjcs.Editor.Skills.OfficialSkillAssetTools.Validate()`；客户端依据包括 `CharacterPanelSkillContent`、`CharacterPanelSkillListContent`、`SkillPieceDetailDialog`、`SkillMergeConfirmDialog`、`SkillPromotePanel` 与 `SkillMutiPromoteNotifyPanel`。

本轮技能修复隔离工程通过静态元数据检查（77,491 个稳定 GUID）、Unity 导入／编译、`ProjectTools.Validate` 与技能载荷哈希校验；新触发归属与图标交互另通过实际 Player 验证。
