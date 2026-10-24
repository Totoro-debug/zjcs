# 技能、方案与培养

[总索引](../README.md)

状态：未完成

## 功能简介

人物页进入技能，包含战技、秘法、碎片、推荐、详情、三套方案、共享槽位升级、祝福培养、单技能升阶及批量升阶。

人物页的技能入口已连接三套方案面板。技能档案与角色主档案共同保存，离线规则和人物页数据源已接入；官方窗口、碎片详情、合成确认、培养页及升阶结果现已从官方布局数据构造并连接本地事务。官方确认弹窗已绑定，纯祝福一键升级在预演达到 40 次时要求确认。技能页、详情、培养和结果弹窗使用 `skill-ui.json` 的官方动画轨道；结果列表逐项展示、跳过和自动滚动，槽位培养播放官方动画和 `Skill_Upgrade.ogg`。Unity Play Mode 已通过真实鼠标切换方案，并核验技能合成、装备及档案重载；其余弹窗、拖拽、培养／升阶、动画音效和异常回滚流程尚未完成 Play Mode 验收。

## 完整还原部分

Unity 已新增离线技能档案与规则服务 [OfflineSkillProfile.cs](../../unity/Assets/Game/Runtime/Offline/Skills/OfflineSkillProfile.cs)、[OfflineSkillProgression.cs](../../unity/Assets/Game/Runtime/Offline/Skills/OfflineSkillProgression.cs)。模型保存三套方案、主动／被动槽位、基础等级、按赛季分开的祝福等级、技能阶位与道具数量；服务提供方案切换／改名、技能安装／替换／卸下、单槽及一键培养、单技能及批量升阶。培养和升阶在档案副本核算费用后提交。

固定 APK `854_156383` 的 394 个主动／被动技能、职业链、阶位累计差额、槽位培养、人物等级限制及材料已由 [prepare_unity_skill_runtime.py](../../scripts/prepare_unity_skill_runtime.py) 按三个来源清单校验并整理为 Unity 可解析的服务 DTO。固定 APK `item` 表确实缺少 18 个由 `Formula.GetPieceIdBySkillId` 计算出的碎片条目；另 190 条有效技能／碎片配对具有相同的稳定字段关系：碎片名为技能名加“碎片”，RawIcon 沿用技能 RawIcon，碎片类型为 111、Rank 为 -1、Quality 为 1。运行时 [OfficialCharacterAssetCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs) 仅对缺失且能映射至职业技能条目的碎片 ID 按此官方记录规律生成展示元数据；`OfficialSkillAssetTools.Validate` 校验 18 个目标 ID 的映射。该派生不声称缺失 `item` 原始行已从服务器配置取得。当前 `871_156598` 资源清单含技能图标图集，但未包含 `item` 规则表。[OfficialCharacterAssetCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs) 同时载入技能培养规则和 `skill-ui.json`；[OfficialSkillAssetTools.cs](../../unity/Assets/Game/Editor/Skills/OfficialSkillAssetTools.cs) 提供技能载荷导入绑定与哈希校验入口。官方 UI 层级和 119 张布局引用图像已整理至 `unity/Assets/Official/Skills/`，包括人物技能页与培养布局及原始 RectTransform 参数。

人物主存档中的 `OfflinePlayerProfile.Skills` 由 `OfflineMainlineProgressSession` 校验和保存，`OfflineCharacterProgressionService` 在角色事务内同步技能材料与背包。人物页入口由 `OfficialCharacterPanelView.OpenSkillPanel()` 打开 `CharacterSkillPanel`。该面板已连接方案选择／改名、收藏、技能列表、安装／卸下、拖拽交换、推荐、碎片详情、合成确认、单技能升阶、升阶结果及批量升阶结果。技能详情、碎片详情、合成确认、培养及结果层级使用 `skill-ui.json` 的官方布局树；人物技能列表按职业可用性、收藏、可升阶状态和品质信息排列。UI 字体从官方资源初始化；资源不可用时改用系统中文字体和 Unity 内置字体回退，避免字体查找递归。编辑器校验工具绑定技能载荷、官方图集、官方布局、槽位升级动画和音效，并按 `source-manifest.json` 校验文件哈希。`Zjcs/Validate/Official Skills Assets` 已在 Unity Editor 通过：394 条技能规则、6 组品质渐变、来源哈希、18 个派生碎片映射和 119 个技能 UI 精灵均已校验。静态工程检查与隔离 Unity Editor 编译通过；[SkillPlayModeAcceptance.cs](../../unity/Assets/Game/Editor/Skills/SkillPlayModeAcceptance.cs) 的真实鼠标方案切换、技能合成、装配和档案重载验收通过。

Editor 程序集显式引用 `Zjcs.Presentation.Character`，以供技能 Play Mode 验收访问人物技能面板；该引用由 `Zjcs.Editor.asmdef` 声明。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

当前存档完整保存每个技能槽位等级及技能投资记录；缺少槽位字段或版本不符时拒绝读取，不执行旧投资转换。装备、费用、随机与进阶通知以本地事务提交，失败不保留部分升级。

服务器开放条件按[离线开放政策](offline-access.md)处理，保留人物、材料与实际原配方条件。

用户明确确认：将官方 Function 138 的一键批量升阶开放等级从 999 级改为与一键升级一致的 12 级；`SkillFastRanklUp`（Function 138）映射到 `SkillFastLevelUp`（Function 23）。费用、升阶上限及按钮优先条件继续使用原规则。

## 尚未实现

- 尚未在 Play Mode 覆盖技能列表分类／筛选、详情与推荐弹窗、拖拽换槽、培养确认与结果、升阶与批量升阶、动画／音效、材料不足及事务失败回滚。已验证的方案切换、一次合成／装配和主档案重载不替代这些剩余场景的输入、渲染和状态验收。

### 实现范围

技能运行时使用本地事务保存三套方案、装配槽位和共享培养数据；方案切换不复制槽位投资，装备和费用校验按固定客户端规则执行。技能详情、碎片详情、合成确认、培养页、单次升阶成功及批量升阶结果由 [CharacterSkillPanel.cs](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.cs) 构建。结果列表支持逐项展示、跳过和滚动；培养播放官方槽位升级动画及 `Skill_Upgrade.ogg`。批量升阶按已确认的离线开放规则处理。

固定版本缺少的 18 个技能碎片 `item` 行，按官方 `Formula.GetPieceIdBySkillId` 与其余 190 组稳定字段关系派生显示元数据；相关字段和图标对应由 `OfficialSkillAssetTools.Validate` 检查，不声称这些行来自服务器原始表。

彩虹品质调色板取自原 `LanguageUtils.SetSkillQualityWithColor → GlobalColors.SetTextColorByQuality → MultiLanguageText.SetColor(Gradient)` 数据路径，并由 `prepare_skill_quality_text.py` 导出固定包 Normal 调色板及六个 `TMPColorGradient` 角度。技能运行时将品质 7 的渐变应用于详情、培养两端与升阶结果文字网格；其他品质移除该效果。渐变关键点和 0°／45° 配置见 [OfficialSkillQualityGradient.cs](../../unity/Assets/Game/Runtime/Presentation/Skills/OfficialSkillQualityGradient.cs) 与 [skill-quality-gradient.json](../../unity/Assets/Official/Skills/skill-quality-gradient.json)。UGUI 网格迁移与实际效果仍须通过 Unity Editor／Play Mode 验证。

### 依据与代码定位

以下为官方来源、既有规则与回归参考：`OfflineSkillProfile`、`OfflineSkillProgression`、`OfflineCharacterProgressionService`、`CharacterSkillPanel`、`OfficialCharacterPanelView.OpenSkillPanel()`、`OfficialSkillAssetCatalog`、`Zjcs.Editor.Skills.OfficialSkillAssetTools.Validate()`；客户端依据包括 `CharacterPanelSkillContent`、`CharacterPanelSkillListContent`、`SkillPieceDetailDialog`、`SkillMergeConfirmDialog`、`SkillPromotePanel` 与 `SkillMutiPromoteNotifyPanel`。
