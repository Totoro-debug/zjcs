# 技能、方案与培养

[总索引](../README.md)

状态：未完成

## 功能简介

人物页进入技能，包含战技、秘法、碎片、推荐、详情、三套方案、共享槽位升级、祝福培养、单技能升阶及批量升阶。

人物页的技能入口已连接三套方案面板。技能档案与角色主档案共同保存，离线规则和人物页数据源已接入；技能窗口、推荐、碎片详情、合成确认、培养页及升阶结果均按官方布局数据构造并连接本地事务。推荐页使用 `skill_recommend`、`skill_recommend_item` 与 `skill_tab` 官方布局，按当前职业前置链和阶位显示方案。官方确认弹窗已绑定，纯祝福一键升级在预演达到 40 次时要求确认。技能页、详情、培养和结果弹窗使用 `skill-ui.json` 的官方动画轨道；结果列表逐项展示、跳过和自动滚动，槽位培养播放官方动画和 `Skill_Upgrade.ogg`。技能页现在使用 `character_skills` 的官方整屏 `CharacterPanelSkillPanel` prefab，包括返回条、方案与技能槽、战技／秘法／碎片列表和底部“技能／馈赠”双页；底部第二页按官方 `Achievement` 状态切换。技能列表使用官方 `skill_list_row` 与 `skill_list_separator`，行列数和间距按 `AdaptiveRowLayoutCalculator` 计算（155 px 项目、两侧各 50 px、最小间距 20 px），合成、收藏和未收藏分组各自起行，收藏组与未收藏组之间按官方条件显示虚线分隔；碎片页按官方 160×160 网格尺寸排布。打开技能时播放官方 `CharacterPanel_Skill` 状态：保留人物预览和背景，按原动画隐藏资料区并调整模型位置；返回时播放 `CharacterPanel_SkillSwitchmain` 并恢复被修改的动画属性。整屏界面与主要技能操作已经通过本页所述 Play Mode 验证；40 级伤害测试及通用窗口表现仍未完成，不能据此将整个技能功能标为完成。

## 完整还原部分

Unity 已新增离线技能档案与规则服务 [OfflineSkillProfile.cs](../../unity/Assets/Game/Runtime/Offline/Skills/OfflineSkillProfile.cs)、[OfflineSkillProgression.cs](../../unity/Assets/Game/Runtime/Offline/Skills/OfflineSkillProgression.cs)。模型保存三套方案、主动／被动槽位、基础等级、按赛季分开的祝福等级、技能阶位与道具数量；服务提供方案切换／改名、技能安装／替换／卸下、单槽及一键培养、单技能及批量升阶。培养和升阶在档案副本核算费用后提交。

固定 APK `854_156383` 的 394 个主动／被动技能、职业链、阶位累计差额、槽位培养、人物等级限制及材料已由 [prepare_unity_skill_runtime.py](../../scripts/prepare_unity_skill_runtime.py) 按三个来源清单校验并整理为 Unity 可解析的服务 DTO。固定 APK `item` 表确实缺少 18 个由 `Formula.GetPieceIdBySkillId` 计算出的碎片条目；另 190 条有效技能／碎片配对具有相同的稳定字段关系：碎片名为技能名加“碎片”，RawIcon 沿用技能 RawIcon，碎片类型为 111、Rank 为 -1、Quality 为 1。运行时 [OfficialCharacterAssetCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs) 仅对缺失且能映射至职业技能条目的碎片 ID 按此官方记录规律生成展示元数据；`OfficialSkillAssetTools.Validate` 校验 18 个目标 ID 的映射。该派生不声称缺失 `item` 原始行已从服务器配置取得。当前 `871_156598` 资源清单含技能图标图集，但未包含 `item` 规则表。[OfficialCharacterAssetCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs) 同时载入技能培养规则和 `skill-ui.json`；[OfficialSkillAssetTools.cs](../../unity/Assets/Game/Editor/Skills/OfficialSkillAssetTools.cs) 提供技能载荷导入绑定与哈希校验入口。官方 UI 层级和 174 张布局引用图像已整理至 `unity/Assets/Official/Skills/`，包括人物技能页与培养布局及原始 RectTransform 参数。

人物主存档中的 `OfflinePlayerProfile.Skills` 由 `OfflineMainlineProgressSession` 校验和保存，`OfflineCharacterProgressionService` 在角色事务内同步技能材料与背包。人物页入口由 `OfficialCharacterPanelView.OpenSkillPanel()` 打开 `CharacterSkillPanel`。该面板已连接方案选择／改名、收藏、技能列表、安装／卸下、拖拽交换、推荐、碎片详情、合成确认、单技能升阶、升阶结果及批量升阶结果。技能页通过 `skill-ui.json` 中的 `character_skills` 完整 prefab 构建，嵌套列表和详情／培养／结果弹窗使用各自的官方模板；技能列表使用官方自适应横排与分类分隔符，碎片列表使用官方 160 px 网格项目。底部“技能／馈赠”页签按 `CharacterPanelSkillPanel` 的 SkillContent／Achievement 状态切换。打开技能面板时保留人物预览与背景，按官方 `CharacterPanel_Skill` 动画切换资料区和模型锚点；关闭时按 `CharacterPanel_SkillSwitchmain` 恢复。UI 字体从官方资源初始化；资源不可用时改用系统中文字体和 Unity 内置字体回退，避免字体查找递归。编辑器校验工具绑定技能载荷、官方图集、官方布局、槽位升级动画和音效，并按 `source-manifest.json` 校验文件哈希。`Zjcs/Validate/Official Skills Assets` 已校验 394 条技能规则、6 组品质渐变、来源哈希、18 个派生碎片映射和 139 个技能 UI 精灵。技能规则载荷与固定 APK 来源重算结果一致（394 条）；静态工程检查通过（64,163 GUID）；Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和整屏 Play Mode 检查通过。Play Mode 用隔离档案和 EventSystem 首次命中校验，模拟人物技能入口、方案、推荐应用、战技／秘法／碎片、全部、收藏、槽位拖动、培养和单次／批量升阶，并检查材料不足回滚、档案重载及返回资料页。头像像素检查和实际截图已执行；合成与部分失败分支仍含服务直接调用，不能代替相应端到端点击检查。Windows Player 已重建并通过实际运行验证，范围见下文。

技能槽使用官方 `item_skill` 和 `item_empty`，不额外添加培养按钮；点击已装备槽位进入官方技能详情，再由详情培养入口操作。图标替换时恢复原颜色，空槽使用原灰色加号精灵，槽位等级使用“级”。九宫格图像恢复导出记录的原 Sprite border。星级按 `skill_rank` 的 Quality／RankAddition 显示，数字星阈值 4 与品质星图映射由原 `SkillStarInfo`、`StarMiniInfo` 组件提取，来源凭据见 `assets/reference/skill-ui/unity-icon-source-manifest.json`；可用 `scripts/prepare_unity_skill_icons.py` 重建。

技能页顶栏复用已恢复的官方 `WorldCanvas/PublicPlayerInfo`、`PublicTopBar` 与 `MoneyUI` 原生 prefab，包含头像、等级、经验、名字、战力与技能升阶材料计数。`TabItemTopBarSwitcher.ShowTypeList=[18]` 对应 `GameSettings.SkillSeniorMaterialItemId=42200`，不显示其他货币；切到馈赠隐藏玩家信息，返回技能恢复。`OfficialCharacterSkillHeader` 保留官方布局与胶囊裁剪网格，按原头像摄影参数、皮肤排除项和 `home/idle_01` 生成头像；D3D 投影矩阵经 GPU 适配。人物预览依原骨骼的 100 UI 单位映射定位，世界几何不继承 Canvas 缩放；无幻兽时按原 `HasAnyPetCondition` 隐藏幻兽占位。布局读取同时修复弧度旋转、内容自适应、透明射线目标与文本溢出。窗口使用原 1080×1920 Expand 缩放；桌面安全区域按 Windows 全窗口处理，测试另检查与参考图相同的 675×1463 比例，角色外观、技能、等级和材料显示当前档案数据。

Editor 程序集显式引用 `Zjcs.Presentation.Character`，以供技能 Play Mode 验收访问人物技能面板；该引用由 `Zjcs.Editor.asmdef` 声明。

馈赠沿用原 `AchievementPanelWithTypes → AchievementPanelBase → AchievementPanelItem` 的两组数据键：Group 33 为 `CostItem(42200, SkillLevelUp)`，保留 895 条固定奖励；Group 9 为 `SkillScoreCount`，由原 15 条初始记录及 4,500×15 条循环增量记录展开为 67,515 个里程碑。积分按 `PlayerSkillDataExt.GetSkillScoreCountByAllSkill` 汇总持有技能各阶位的 `skill_rank.Score`，列表按原搜索范围、已完成优先排序及五行数量显示。点击领取按原 group、count=-1 行为，在同一本地事务中发放当前组全部可领奖励并保存领取凭据；重复领取不增加道具。培养材料累计值只在实际成功的槽位升级事务中增加，旧存档缺少此历史字段时从 0 开始，不由库存或当前等级反推。原页签动画、状态颜色、箱子、奖励图标和文案已绑定；原 TMP 自动缩放及换行参数从固定 APK 组件恢复。规则检查 `python -B scripts/test_skill_achievements.py` 已通过，完整技能 Play Mode 加入材料馈赠的模拟点击领取、重复领取防护及品质页切换后再次通过。奖励图标点开道具详情仍是缺项。

人物预览的阴影已恢复原 `AttachmentFollower` 对 `shadow` 骨骼的位置与局部缩放跟随，不再保留 prefab 编辑态的静止位置。Windows 程序通过正常本地启动进入隔离技能档案，使用 EventSystem 首个射线命中、按下／抬起／点击验证入口、方案、推荐、分类和返回，且实看运行截图、重新读取档案核对方案与装配；测试存档与用户存档分开。已发布入口为 `D:/Desktop/杖剑传说/杖剑传说.exe`；新增馈赠流程已在重建后的 Windows Player 中完成两类奖励领取、重复领取防护、页签切换和存档重载验证，未将整体技能功能标为完成。

技能页按官方 `CharacterPanel.mCharacterPanelSkillPanelParent` 挂入 `/CharacterPanel/BgPanel1/Content/TopGo/SkillContent`，恢复人物页、共享 HUD 和技能窗口的 `SafeArea` 参数。返回人物页按原节点字体绑定恢复 FZY4K 中文字体，避免原生 prefab 的缺失字体回落为方块。系统消息从 APK 原 `SystemMsgPanel` 提取层级、镜像背景、Canvas 排序与 Show／Hide 动画，收藏／取消收藏使用原文案。详情、碎片、合成确认、培养及升阶通知按原 `PopupWindow.mMaskType` 选择原生普通或模糊遮罩；模糊复用原 `ScreenBlur` 材质和四级处理，外部点击保留原 0.5 秒防误触条件。新增流程已在带画面的 Play Mode 中通过模拟点击验证：收藏提示可见，模糊背景方向正确，遮罩保持在弹窗下一层，外部点击关闭后技能页继续操作；拖拽、培养、升阶、批量结果、两类馈赠和存档重载一并回归通过。本次静态元数据检查、Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 已通过；Windows Player 验证待本次重建后执行。

技能详情已恢复固定 APK 的原生 FZY4K TMP 字体、材质和初始 Alpha8 字体图集，修复弹窗 Canvas 缺少 UV1 通道造成的方块文字；字体、材质与动态图集按窗口克隆，技能页独立副本保留原始初始图集，避免 Windows 构建清空共享字体。品质背景、普通品质文字色、星级空星、战技／秘法背景状态、技能等级及祝福显示条件按原组件恢复。`OfficialSkillDetailProperties` 迁移 `PropConfigUtils.CalcSkillViewProps` 与 `SkillPanelPropPanel.Sort` 的主动、被动、状态及召唤属性展示规则；全部 394 个技能在两组等级／阶位条件下的 788 次计算已通过 Play Mode。标签取自原 `SkillTagItem` 和 `ESkillTag`，职业图标沿用 `ProfessionBaseInfoParser.PostParse` 的归属及前置职业链；范围图从 APK `SkillItemRangeInfos.g.bytes` 恢复 1,365 条数据，保留原格子 prefab、五类颜色、间隔和行列顺序，当前可用主动技能均具有有效范围数据。新增标签、范围、属性、收藏、弹窗外部关闭与整屏培养／升阶／馈赠／存档流程已通过模拟点击 Play Mode 回归并查看实际画面；本批 Windows Player 重建验证仍待执行。

人物预览和头像改用经原始对象 SHA-256 核验一致的官方 `Universal Render Pipeline/Spine/SkeletonDye-2D` shader 转换结果，保留 544 组原 GPU 程序、透明度截断、染色及渲染状态；仅适配桌面预览相机的 pass 选择。运行截图确认面部／头发的白色三角残片已消失。技能模块目前显式绑定 174 张官方 UI 精灵；转换与原对象哈希、字体隔离方式见 `assets/reference/skill-ui/unity-icon-source-manifest.json`，重建入口增加 `prepare_skill_native_text.py`、`prepare_skill_character_shader.py` 和 `prepare_skill_detail_data.py`。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

Windows 竖屏显示适配按用户参考画面保留顶部安全留白：1080 UI 单位宽度对应约 137 UI 单位顶部留白，随窗口宽度缩放；这是桌面显示适配参数。原 `SafeArea` 的边界计算、PaddingTop／PaddingBottom、DisableTopAdapt／DisableBottomAdapt 与技能父级绑定保持官方数据，不修改内部槽位、方案或列表坐标。模糊遮罩恢复 APK 原 RawImage UV，并按 Windows 图形接口的纹理方向适配截图采样；不改变原 ScreenBlur 算法。

用户已确认伤害测试的单机规则：使用已迁移的本地战斗配置计算怪物属性，按原参数页勾选项应用怪物固定伤害 1／无限生命。这部分属于缺少服务器实现后的单机差异，参数页和战斗接入尚待完成。

当前存档完整保存每个技能槽位等级及技能投资记录；缺少槽位字段或版本不符时拒绝读取，不执行旧投资转换。装备、费用、随机与进阶通知以本地事务提交，失败不保留部分升级。

服务器开放条件按[离线开放政策](offline-access.md)处理，保留人物、材料与实际原配方条件。

用户明确确认：将官方 Function 138 的一键批量升阶开放等级从 999 级改为与一键升级一致的 12 级；`SkillFastRanklUp`（Function 138）映射到 `SkillFastLevelUp`（Function 23）。费用、升阶上限及按钮优先条件继续使用原规则。

## 尚未实现

- 馈赠奖励图标的原道具详情弹窗仍待接入；列表、两类进度、领取事务和 Windows Player 模拟输入验证已完成。
- 伤害测试原开放条件为人物 40 级（Function 148），当前已按原条件隐藏未开放入口；开放后的怪物／难度／回合参数页及测试战斗尚未接入。
- 通用弹窗的原遮罩／点击外部关闭和系统消息已通过 Play Mode、待完成本次 Windows Player 验证；详情附属规则／皮肤入口、道具来源跳转及培养对比属性仍需迁移；已移除手写后备详情和培养布局及技能底部自定义状态文字，不能视为这些公共表现已补齐。

### 实现范围

技能运行时使用本地事务保存三套方案、装配槽位和共享培养数据；方案切换不复制槽位投资，装备和费用校验按固定客户端规则执行。技能详情、碎片详情、合成确认、培养页、单次升阶成功及批量升阶结果由 [CharacterSkillPanel.cs](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.cs) 构建。结果列表支持逐项展示、跳过和滚动；批量展示在原 `ShowItems` 动画事件（0.76666665 秒）后启动，并读取原 `ListItemShowSequence` 的 0.1 秒间隔与首次延迟，遮罩准备期间不提前播放结果；培养播放官方槽位升级动画及 `Skill_Upgrade.ogg`。批量升阶按已确认的离线开放规则处理。

固定版本缺少的 18 个技能碎片 `item` 行，按官方 `Formula.GetPieceIdBySkillId` 与其余 190 组稳定字段关系派生显示元数据；相关字段和图标对应由 `OfficialSkillAssetTools.Validate` 检查，不声称这些行来自服务器原始表。

彩虹品质调色板取自原 `LanguageUtils.SetSkillQualityWithColor → GlobalColors.SetTextColorByQuality → MultiLanguageText.SetColor(Gradient)` 数据路径，并由 `prepare_skill_quality_text.py` 导出固定包 Normal 调色板及六个 `TMPColorGradient` 角度。技能运行时将品质 7 的渐变应用于详情、培养两端与升阶结果文字网格；其他品质移除该效果。渐变关键点和 0°／45° 配置见 [OfficialSkillQualityGradient.cs](../../unity/Assets/Game/Runtime/Presentation/Skills/OfficialSkillQualityGradient.cs) 与 [skill-quality-gradient.json](../../unity/Assets/Official/Skills/skill-quality-gradient.json)。UGUI 网格迁移与实际效果仍须通过 Unity Editor／Play Mode 验证。

### 依据与代码定位

以下为官方来源、既有规则与回归参考：`OfflineSkillProfile`、`OfflineSkillProgression`、`OfflineCharacterProgressionService`、`CharacterSkillPanel`、`OfficialCharacterPanelView.OpenSkillPanel()`、`OfficialSkillAssetCatalog`、`Zjcs.Editor.Skills.OfficialSkillAssetTools.Validate()`；客户端依据包括 `CharacterPanelSkillContent`、`CharacterPanelSkillListContent`、`SkillPieceDetailDialog`、`SkillMergeConfirmDialog`、`SkillPromotePanel` 与 `SkillMutiPromoteNotifyPanel`。
