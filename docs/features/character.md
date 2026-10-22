# 人物属性、经验、位阶与衣柜

[总索引](../README.md)

状态：未完成

## 功能简介

从头像或底栏人物进入，展示名字、外观、等级、经验、位阶、职业、属性与装备；外观按钮进入衣柜，管理已获得的角色幻装及称号。职业、技能和装备分别在对应文档维护。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

Unity 已接入固定版本 `854_156383` 人物成长目录、人物概览与属性文本、限时外观配置、原衣柜规则/物品/UI 数据及其引用图片。生成入口为 [prepare_unity_character_catalog.py](../../scripts/prepare_unity_character_catalog.py)，输出与官方输入哈希登记于 `unity/Assets/Official/Character/source-manifest.json`；`OfficialCharacterAssetCatalog.FindSprite` 按原资源 URI 查找人物与衣柜 Sprite，编辑器绑定及来源核对入口为 [CharacterProgressionValidation](../../unity/Assets/Game/Editor/Character/CharacterProgressionValidation.cs) 的 `BindOfficialCatalogs`。

[OfficialCharacterPanelView](../../unity/Assets/Game/Runtime/Presentation/Character/OfficialCharacterPanelView.cs) 按已恢复的人物、属性和位阶面板层级构建基础 Unity UI，并绑定等级/经验、位阶材料操作及属性弹窗；[CharacterFeatureRuntime.OpenProfile](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterFeatureRuntime.cs) 从角色存档槽创建页面运行时，并初始化基础衣柜皮肤/染色、同步已到期外观。[OfflineCharacterExperience](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterExperience.cs) 按绑定配置计算等级、储存经验、赐福等级和进度比例，并计算已确认的限时外观续期；[OfflineCharacterProgressionService](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs) 将经验奖励、位阶材料支付、10级教学奖励、穿戴/卸下、称号、染色、限时到期和衣柜进度写入共享人物进度事务，并提供 `BaseBattleProperties` 读取入口；[OfflineWardrobeRules](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineWardrobeRules.cs) 解析互斥皮肤、绑定部件及装备皮肤回退。人物目录从原始 `level_prop.g.bytes` 解码等级、位阶及职业组属性，`OfficialCharacterAssetCatalog.CalcCharacterBaseBattleProperties` 按官方 `BattleFormulaHandler.CalcCharacterBattlePropsWithoutProfession` 的等级、位阶、职业组顺序合并。属性的基础比率、百分比、转换和倍率处理位于 `OfficialCharacterProperties`。

当前完成的是数据恢复、规则目录绑定、基础战斗属性原表解码/合并、人物进度事务和可由代码调用的基础人物 UI；基础属性尚未接入人物页数值、综合属性管线及实战读取。`OfficialCharacterAssetCatalog` 从官方 `wardrobe-rules.json` 读取默认皮肤映射，将 4 条 `ItemType=339` 且 `SkinDict=null` 的称号记录排除出皮肤穿戴目录，由独立称号逻辑处理；其余 1,563 个可穿戴外观项、1,723 个皮肤定义通过 `CharacterProgressionValidation.BindOfficialCatalogs` 的 Unity 编辑器绑定验证，基础属性断言为 300/60/60/48，537 张人物 Sprite 已导入并绑定。此入口只验证目录和资源绑定，不代表游戏内人物流程验收通过。游戏内可达的人物入口和人物创建后正式身份资料接入尚未完成；角色模型/动画、衣柜运行界面及完整交互、属性来源明细、经验领取结果动画、属性合并与战力计算仍未完成。静态 GUID 检查不构成人物功能运行验收。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

人物页与衣柜中的角色显示、换装和染色沿用[Spine 角色动画迁移决策](../asset-management.md#spine-角色动画迁移决策)；共用转换能力与人物显示调用方接通后仍须在本模块验证。

用户补证普通经验在当季基础上限后继续提升显示等级，超出部分为当季赐福，赛末回到当季基础上限。服务端经验路由未取得，本地按此处理并保留赛季记录。

限时装扮首次有效期和到期卸下依据原客户端；重复获得的服务端续期未取得。用户已确认按配置 `CanOverlapTime` 叠加 `Duration`，并将剩余有效期限制在 `MaxDuration` 内；不允许叠加时按配置重设时长。此续期规则为已确认的离线实现。

## 尚未实现

- 将 `CharacterFeatureRuntime.OpenProfile` 接入可达的人物入口，补齐角色正式身份资料；完成经验领取成功后的官方时长动画、储存经验规则说明、教学触发/恢复和返回家园条件，并在 UI 中表现失败与保存结果。
- 在已恢复的等级、位阶和职业组基础属性上，迁移 `PlayerPropContext` 其余分块属性合并与战力公式；接入装备、职业、技能、幻兽、古遗物、公会、星命和衣柜属性来源，使人物页、战斗属性及评分读取同一结果。
- 将已恢复的官方人物页/衣柜 prefab 层级、坐标、控件字段、人物模型/动画及音效接入 Unity；完成类别、称号、筛选、互斥、装备外观回退、染色、时限显示与到期卸下、收藏经验及衣柜属性交互。
- 在 Unity 中执行资源导入、目录绑定、规则核对和运行验收，覆盖正常操作、动画/音效、读档恢复及事务失败回滚；静态检查和既有引擎结果不替代 Unity 验收。

### 人物界面与官方调用关系的差异

人物界面尚未完整迁移。以下按固定版客户端源码、当前 Unity 运行时代码和实际载荷核对；“已接线”不代表已通过实际输入、画面或存档恢复验收。装备、技能和幻兽的完整规则范围分别见[装备](equipment.md)、[技能](skills.md)与[幻兽](pets.md)。

| 范围 | 官方调用关系 | 当前实现与缺口 |
| --- | --- | --- |
| 人物模型 | `CharacterPanel.RefreshAppearance`、`SwitchAnimCallBack`、`AppendAppearanceContent.PlayAnim/GetSwitchAnimData/GetLoopAnimData` | `CharacterModelPreview` 已接衣柜解析和网格播放；运行包仅含男女 `100/idle_01`、`200/idle_01` 四份姿态。目录中职业 0–30 的 profile 记录不能代表职业切换流程或全部动作已接通；衣柜分类 `show_*` 切换和 `appearance` 循环、HSV 材质/遮罩、人物页称号及实际装备驱动的外观同步仍缺失。模型还采用按包围盒缩放居中，尚未完成与官方画面的比例、位置核验。 |
| 人物属性与战力 | `CharacterPanelPropPanel.RefreshProp/OnShowDetailPropBtnClick` → `BattlePropsOutFight`、`CharacterDetailPropsDialog.Init`；`PlayerPropContext` 合并分块并更新战力 | `OfficialCharacterPanelView.Refresh` 只更新等级、经验、位阶；属性按钮只构建弹窗模板，未绑定综合数值、细分来源、页签或说明。`BaseBattleProperties` 与 `AppearanceProperties` 已有数据接口，Map10 战斗调用基础值并累加衣柜值，但尚无人物页/战斗共用的完整分块属性与评分管线。 |
| 人物装备 | `CharacterPanelPropPanel.RefreshEquip/UpdateProfessioon` → 五个 `CharacterPanelEquipItem.Refresh/OnBtnClick` → 空槽候选或已穿详情 | 官方五槽为主武器、副武器、头盔、衣服、靴子。当前人物 UI 未绑定五槽实例、图标、强化/星石状态、副武器职业解锁或点击入口；`OfflineEquipmentOperations` 有穿脱等规则，仍未由人物页调用。穿脱只改 `Equipment.Equipped`，未同步官方 `PlayerAppearanceHandler.Player_OnEquipUpdate` 对应的武器外观；衣柜解析读取的 `Wardrobe.WornSkins` 尚未与实际装备连接。 |
| 技能安装 | `CharacterPanel.OnCharacterPanelPropPanelSkillBtnClickEvent` → `CharacterPanelSkillPanel/CharacterPanelSkillContent`；`CharacterPanelSkillPlan` 管理方案 | 人物按钮仅发出 `OfficialModuleRequested("skills")`，`CharacterFeatureRuntime` 转发 `NavigationRequested`，创建流程没有订阅该事件。`OfflineSkillProgression` 已有安装/替换/卸下与培养规则，但 `OfflineSkillProfile` 未进入统一人物主存档，技能运行面板、槽位交互、方案选择和战斗读取未接通。 |
| 幻兽装配 | `CharacterPanel.RefrshPet` → `PlayerWithPetContent` → `PetPlanContent.OnSelectPetClick` → `PetSelectPanel`；`PetEditPlanDefaultOperation.EquipPet` 加入/退出出战方案 | 此处官方“装备幻兽”指随行/出战幻兽的选择、更换、卸下和模型展示。Unity 未接 `PetSelectContent`、幻兽选择操作、模型或对应存档；人物 `pet` 按钮同样只有无接收方的跳转事件。 |
| 衣柜 | `AppearancePanel`、`AppendAppearanceContent/GridContent/GridItem`、称号及等级内容 | `OfficialWardrobePanelView` 已有类别、已拥有列表、可穿筛选、穿脱、称号和到期事务；原分类动作、互斥反馈、人物称号显示仍不完整。底栏 `skill-appearance/decoration-appearance` 回调未传入；属性弹窗把属性 ID 和数值拼为文本，规则弹窗追加固定字号 Arial 文本，未绑定原属性行/规则行模板及完整说明内容。 |
| 职业与成长说明 | `CharacterPanelPropPanel.OnProfessionBtnClick`、原经验/位阶组件 | 职业按钮只有未被消费的 `profession` 事件；储存经验规则按钮的 `StoredExperienceHelpRequested` 亦无接收方。成长操作已有服务，不能据此认定职业、位阶教学、材料说明和领取表现全部还原。 |

共享界面还存在两项直接影响完整性的代码缺口：`OfficialCharacterPanelView.BuildNode` 没有恢复目录中的布局组、`ContentSizeFitter`、`ScrollRect`、原 TMP 字体/材质、状态切换器或按钮音效，仅创建基础 Image/Text/Button/Mask/LayoutElement/Slider；`CloseDialog` 清空索引后重新 `BuildNode(characterRoot, transform)`，却不销毁既有主页，反复关闭属性/位阶弹窗会累积主页层级。上述为源码可确认的问题，尚未在实际画面中复现。

验证边界：`python -B scripts/check_unity_project.py` 静态元数据检查通过（22,050 个 GUID）；`character-models.zip` 内清单登记的 7,566 个载荷 SHA256 全部一致。Unity `2022.3.57f1c2` 在尝试执行 `Zjcs.Editor.ProjectTools.Validate` 前编译失败：`OfficialMap10GameRuntime` 缺少 `PlayMapTalkDialog` 且 `PlayStory` 参数不匹配，`CommerceFeatureRuntime` 两处 `CommerceTransactionResult` 构造参数不匹配且缺少 `ActivateFirstPurchase`。因此本次没有执行该 Editor 校验方法、Play Mode 逻辑/真实输入/画面/存档恢复或 Windows Player 验证；本节是代码与载荷检查结论。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

原人物页、综合／细分属性、位阶卡和属性说明按原控件绑定。来源分组独立展开与滚动，属性说明只读，不修改存档。

经验完整接入原 `PlayerLevelText`、`PlayerExpSlider` 和 `Slider` 绑定：普通经验显示当前级进度；满阶显示橙色储存经验及下一阶整段需求，原说明按钮打开储存规则；赛季赐福显示实际赐福等级、当前级经验，满赐福时显示下一赛季限制。领取经验成功保存后按原组件时长播放等级与进度插值，失败交易不播放，重新读档保持实际经验。

基础属性读取 `PlayerLevelBattlePropId`、`PlayerSubRankBattlePropId`、`PlayerFightRolePropGroupId`。1级生命／攻击／防御／速度为300／60／60／48；职业、装备、技能、幻兽、古遗物、公会和星命按原顺序合并，倍率、百分比及转换逐步截断。

战力采用原 `PlayerPropContext` 分块公式，装备词条、套装、技能槽、幻兽、遗物等分别计分；属性与评分供实际战斗读取。细分来源依原独立截断，不用虚构差额补足总值。

10级返回家园触发原位阶教学，阅读材料说明后一次领取白垩石片2个和罗拉2000；突破仍支付原费用，教学可恢复。人物突破不再等待服务器阶位，参见[离线开放条件](offline-access.md)。

装备穿戴、卸下及职业变化同步原 `AppearanceData.EquipSkinDict`；主副武器先取实际装备皮肤，衣柜装扮覆盖对应部件，卸下后恢复装备或基础皮肤；服装按原职业基础皮肤和衣柜选择显示。男女、职业条件、穿戴时间、双向／单向互斥、黑白名单、绑定皮肤与原 HSV 染色按原配置解析；旧外观迁入 `SkinMap`／`DyeMap`。状态经本地事务与形象共同保存，写盘失败整体回滚。

衣柜采用原 `AppearancePanel`、分类页签、装扮图标与选中状态、称号双列控件、属性和规则弹窗。支持主副武器、服装、面饰、背饰、发型、头饰、脸纹及称号的穿戴／卸下和可穿筛选，已佩戴称号显示于人物页与称号页。收藏经验按原品质、类别、限时排除与限时称号记录计算，衣柜等级属性及拥有称号属性纳入原属性来源12和战力。限时装扮按原规则向上取整显示剩余天／小时，称号显示剩余天／小时／分钟／秒；到期清除拥有状态并自动卸下。

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

[PlayerPropContext](../../.tools/source-packages/official-code/common-source/Common/PlayerPropContext.cs)、

[LevelPropParser.GetProps](../../.tools/source-packages/official-code/common-source/Common/LevelPropParser.cs)、[BattleFormulaHandler.CalcCharacterBattlePropsWithoutProfession](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)、[角色属性原表来源与输出哈希](../../unity/Assets/Official/Character/source-manifest.json)、

、[PlayerExpSlider](../../.tools/source-packages/official-code/client-source/PlayerExpSlider.cs)、[PlayerLevelText](../../.tools/source-packages/official-code/client-source/PlayerLevelText.cs)、[PlayerBlessLevelUtils](../../.tools/source-packages/official-code/common-source/Common/PlayerBlessLevelUtils.cs)、

、[AppearanceDataEx](../../.tools/source-packages/official-code/common-source/Common/AppearanceDataEx.cs)、[PlayerAppearanceHandler](../../.tools/source-packages/official-code/common-source/Common/PlayerAppearanceHandler.cs)

[AppearanceUtils](../../.tools/source-packages/official-code/client-source/AppearanceSystem.Runtime/AppearanceUtils.cs)、[prepare_character_appearance.py](../../scripts/prepare_character_appearance.py)、

、[verify_character_appearance_assets.py](../../scripts/verify_character_appearance_assets.py)、
