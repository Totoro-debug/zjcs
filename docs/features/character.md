# 人物属性、经验、位阶与衣柜

[总索引](../README.md)

状态：未完成

## 功能简介

从头像或底栏人物进入，展示名字、外观、等级、经验、位阶、职业、属性与装备；外观按钮进入衣柜，管理已获得的角色幻装及称号。职业、技能和装备分别在对应文档维护。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

### 原始官方功能范围

本页记录固定 APK `854_156383` 人物主页及其直接进入的属性、位阶、装备、技能、幻兽、职业和衣柜流程。下表是原始客户端功能要求；实际迁移状态见“完整还原部分”和“尚未实现”。装备、技能、幻兽、职业及共享成就的系统规则分别由[装备](equipment.md)、[技能](skills.md)、[幻兽](pets.md)、[职业](professions.md)和[成就](achievements.md)维护，本页负责人物入口、跨页状态和展示绑定。

| 范围 | 原始官方功能与交互 | 官方类／函数 |
| --- | --- | --- |
| 人物主页与返回 | 展示人物身份、职业图标／名称、模型、随行幻兽、称号、等级／经验、位阶、概要属性及五个装备槽；技能／衣柜子页控制公共底栏和顶栏状态，返回恢复主页；重新显示时刷新外观与幻兽，关闭／销毁时解除订阅并释放载荷，低内存时回收未显示的子页。 | `CharacterPanel.OnEnable/SetState/ResetClickReturnBtnData/OnLowMemory`、`CharacterPanelProfessionIcon.Refresh` |
| 功能开放与提示 | 职业、幻兽、衣柜入口读取对应 Function 限制；等级、职业、拥有物品及实际部件限制分别控制按钮、可用状态或原提示。离线开放差异按[离线开放条件](offline-access.md)执行。 | `CharacterPanelPropPanel.RefreshAppearanceBtn/RefreshPetBtn/OnProfessionBtnClick/OnPetBtnClick` |
| 人物模型与换装 | 从性别、职业及 `AppearanceData` 组合基础服装、主副武器、幻装与染色；装备／职业／晋升变化更新外观。衣柜分类切换按 `show_*` 过渡接 `appearance` 循环，保留 mix、退出时机和返回默认姿态；称号及限时信息通过原挂点显示，衣柜状态隐藏随行幻兽。 | `CharacterPanel.RefreshAppearance/SwitchAnimCallBack/RefrshPet`、`PlayerAppearanceHandler`、`AppendAppearanceContent.GetSwitchAnimData/GetLoopAnimData/LeaveWindow` |
| 等级、经验与赐福 | 显示当前等级／上限及本级经验进度；达到位阶上限显示储存经验、橙色数值及原说明；赐福开放时显示赐福等级和该级经验，达到赐福上限显示下一赛季限制。经验奖励、等级变化、赐福及赛季记录变化更新数值，奖励演出按原注册的延时、动画时长和执行顺序完成。 | `PlayerLevelText.Refresh`、`PlayerExpSlider.SetExp/RefreshExpText/DoPlayerExp`、`PlayerExpRewardHandler` |
| 位阶与突破 | 展示当前／下一位阶、等级上限、星级、基础属性前后值、原材料图标与持有／所需量；区分等级不足、材料不足、货币不足、位阶未开放、开放倒计时及最高位阶；材料／等级／位阶开放变化刷新窗口，成功后刷新并打开原 `UpgradeNotifyDialog`。 | `CharacterUpgradePanel.RefreshDetailPanel/OnSubRankUpBtnClick`、`CharacterUpgradePanelStarItem` |
| 综合属性与细分来源 | 主页读取战斗外属性；详情支持综合／细分页签，按原分类、扩展属性、关联百分比、隐藏空值规则、单位与说明生成行。细分来源按原顺序排列，可独立折叠／展开并在列表滚动查看，显示各来源经原公式处理后的贡献。 | `CharacterDetailPropsDialog`、`CharacterDetailPropsDialogOverallContent.Init/AddPropItem`、`CharacterDetailPropsDialogDetailedContent.RefreshUI`、`CharacterDetailPropDialogDetailPropItem` |
| 装备五槽与候选 | 主武器10、头盔11、衣服12、靴子13、副武器14；副武器按职业阶位解锁。槽位显示实际装备、强化等级、职业、星石、幻装标记与原提示点；空槽打开本部位可穿候选，已穿槽打开详情。候选按原比较器排序，支持空列表提示和新获得标记清除。 | `CharacterPanelPropPanel.RefreshEquip/UpdateProfessioon`、`CharacterPanelEquipItem.Refresh/OnBtnClick`、`CharacterPanelEquipListPanel.RefreshEquipList` |
| 装备详情与替换 | 详情进入更换或强化；无旧装备显示单件穿戴，有旧装备显示双栏比较、评分升降与原红点，比较保留当前部位星石的影响；确认按原职业／套装检查处理，成功刷新人物状态与外观。强化、继承、分解、星石、碎片、词条及套装详见[装备](equipment.md)。 | `CharacterPanelEquipDetailPanel`、`CharacterPanelEquipComparePanel.Init/CompareEquipScore/OnConfirmBtnClick` |
| 技能方案与安装 | 三套方案各四个战技／四个秘法槽，方案切换、改名与原切换动画；列表选择技能进入安装遮罩，指定槽位安装或替换，已装技能可卸下，同类槽位可拖拽交换。操作期间控制高亮、占位、遮罩、返回限制；更换方案同步幻兽方案展示。 | `CharacterPanelSkillPlan`、`CharacterPanelSkillContent.BeginPrepareEquip/EquipSkill/TakeOffSkill/CanExchangeSkill/SetPlan`、`CharacterPanelSkillItemDragContent` |
| 技能列表、详情与培养 | 战技／秘法／碎片页签、职业可用性及其他职业筛选；排除当前方案已安装项，展示可合成技能、收藏分组、原排序、职业不符标记及新物品／可升级提示。详情支持收藏／取消收藏并更新状态与提示，进入安装、卸下、培养、碎片合成与推荐；共享槽升级、赐福、单次／批量升阶及结果表现见[技能](skills.md)。 | `CharacterPanelSkillListContent.BuildSkillLoopEntries/SelectSkillPieceItem`、`SkillPanelSkillDetailDialog.OnCollectionBtnClick`、`CharacterPanelSkillContent.OpenSkillDetailDialog/OnClickRecommendBtn/GetFastBtnState` |
| 技能附属入口 | 技能页还含技能成就页签；切换成就页隐藏公共人物资料，返回技能页恢复。伤害测试按钮按 `FunctionType.StageDamageTest` 开放，进入怪物、位阶对应难度、回合和怪物参数选择后启动测试。 | `CharacterPanelSkillPanel.OnAchievementTabChanged/OnSkillContentTabChanged`、`CharacterPanelSkillContent.OnClickStageDamageTestBtn`、`StageDamageTestPanel` |
| 幻兽装配与养成入口 | 主页显示随行幻兽；技能方案页按方案显示出战幻兽。点击模型区／选择按钮进入原选择列表，按原战力优先比较器排序并含“无幻兽”项，选择后加入／退出对应方案，更新模型与选择标记。人物幻兽按钮另进入完整幻兽管理页，其四阵位、技能、喂养、潜力、进化、能力树及改名见[幻兽](pets.md)。 | `PlayerWithPetContent`、`PetPlanContent`、`PetSelectPanel.InitPetList/OnTabItemChanged`、`PetUIUtils.PetEditPlanDefaultOperation` |
| 职业入口与反馈 | 职业总览含总览／职业树／成就页签、当前职业图标／名称／立绘／底色、雷达、历次职业总等级和累计等级属性，规则按钮打开 `ProfessionLevelInfo`。培养、晋升及转职完成后同步职业图标、模型、副武器可用性、装备和技能适用状态。完整系统见[职业](professions.md)。 | `ProfessionOverviewPanel`、`CharacterPanelProfessionIcon`、`PlayerAppearanceHandler` |
| 衣柜分类与穿脱 | 幻装一级／二级分类管理主副武器、服装、面饰、背饰、发型、头饰、脸纹；列表取已拥有且未到期物品，“全部”切换是否纳入当前不可穿项，按原品质／ID比较器排序并在首项保留卸下。显示品质、名称、选中／已穿／新获得／性别／职业限制、倒计时；穿脱分别处理重复操作、功能限制及副武器缺装／职业不符提示。 | `AppendAppearanceContent`、`AppendAppearanceGridContent.RefreshContent`、`AppendAppearanceGridItem`、`AppendAppearanceUtils.IsUnlockSecondWeapon` |
| 互斥、称号与详情 | 穿戴前比较原皮肤解析的前后有效集合，提示被遮挡部件或互斥环后继续原穿戴请求。称号双列列表含卸下、品质／已穿／新获得状态及剩余时间；详情展示称号模型、首次收藏经验、有效期及属性，任务称号窗口支持领取并勾选立即穿戴，物品详情可进入原试穿窗口。 | `AppendAppearanceGridItem.TryGetConflictTip`、`AppendAppearanceHonorContent/AppendAppearanceHonorItem`、`HonorTitleItemDetailDialog`、`AppendAppearanceHonorTitleDialog` |
| 潮流等级与属性 | 收藏幻装、称号、技能幻彩与家园装潢按原配置累计风尚经验；等级条显示本级经验／所需量、跨级动画和满级状态；升级按钮验证可升级后更新等级并展示跨级新增属性。属性窗按原等级表展示已激活及后续行并定位当前级；规则窗逐组展示类别／品质经验规则。 | `AppendAppearanceLevelContent`、`AppearanceDataEx.CanUpgradeAppearanceLevel/TryUpgradeAppearanceLevel`、`FashionExpUtil`、`AppearancePropDialog`、`AppearanceLevelDoubleDescRuleDialog` |
| 技能幻彩与家园装潢 | 衣柜底栏按已拥有类别及原开放条件显示幻装／技能幻彩／家园装潢，多个有效页签时显示底栏。技能幻彩按类别展示、选择和播放原视频预览，检查是否持有原技能后穿戴，卸下按原技能 ID 处理；家园装潢按原数据提供器展示一／二级分类、已拥有项、选择状态及 `HomePhotoSystem` 的实际预览。 | `AppearancePanel.RefreshTabGroup`、`AppearanceBottomTab.IsTabItemActive`、`AppendAppearanceSkillContent`、`AppearanceHomeDecorationContent` |
| 染色、详情试穿与外观变更 | 人物／衣柜模型消费保存的固定色与 HSV，外观编辑入口属于 `ChangeAppearancePanel`；物品详情进入 `PreviewAppearanceDialog` 试穿并按原性别条件提示。编辑界面和创角规则见[角色创建](character-creation.md)，本页负责返回后的正式人物、换装和读档一致性。 | `ChangeAppearancePanel`、`AppearanceItemDetailDialog.OnTryDressClick`、`HonorTitleItemDetailDialog.OnClickTryDressBtn`、`PreviewAppearanceDialog` |

### 属性来源与共享计算边界

`CharacterPropCalculateIntent.SortPowerCategorys` 的明细顺序为：人物基础、职业、NPC、装备、技能、古遗物、星石、料理、额外、幻兽、公会、衣柜、星命。装备明细合并 `Equip + EquipResonance`，古遗物合并 `Treasure + TreasureSuit`，NPC 合并 `NpcFriendshipLevel + NpcTalentTree`，星命合并 `Astrological + EquipCabinet`；各来源分别经过 `HandleSingleSystemDisplayProp`，关联属性随主属性展示，零值和无内容来源按原规则隐藏。`PropDetailCategory.Apperance=12` 是明细分类编号，实际计算分块为 `EPropHandleType.Appearance=17`，二者不能混用。

总属性与战力恢复须覆盖 `PlayerPropContext` 和原 `PropChunckFactory` 的实际分块：`BattleBasic`、`NoBattleBasic`、`Skill`、`Equip`、`EquipResonance`、`Treasure`、`TreasureSuit`、`Gem`、`Cooking`、`Extra`、`NpcFriendshipLevel`、`Pet`、`LostGoddess`、`PeriodIcCar`、`Legion`、`EquipCabinet`、`Astrological`、`Appearance`、`NpcTalentTree`。按所属功能已确认的单机保留／关闭范围处理其输入和有效条件；`GmProp=100` 为原调试分块，单独标识。计算与展示保留原合并顺序、百分比、转换、倍率、独立截断及评分引用数据，人物属性变化须使主页、明细、装备比较、技能详情和实战共同更新。

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

### 人物入口、窗口与共享表现

- [ ] 将现有创角后“人物”入口与正式头像／底栏人物入口统一到当前存档槽，接通 `profession/skills/pet` 跳转的实际接收方，返回时恢复原顶栏、底栏、公共人物资料和主页状态。
- [ ] 按原 Function 与所属单机开放规则控制入口可见、可点、提示和动态刷新；绑定职业名称／图标、人物称号／有效期及各槽位状态。
- [ ] 恢复人物、属性、位阶及附属弹窗的布局组、`ContentSizeFitter`、滚动／裁切、状态切换、TMP 字体／材质／描边、原动画／音效和本地化字段；补齐动态生成的属性行、物品图标、材料行及空状态。
- [ ] 修复 `CloseDialog` 重复构建主页、弹窗与主页按钮索引混用及关闭目标选择，保证顶层返回、遮罩阻挡、Escape、反复开关及同一页只有一份有效层级；按原生命周期解除事件、取消协程并回收模型／贴图／子页资源。

### 人物模型、外观同步与染色

- [ ] 在现有男女四份 idle 姿态基础上接入原衣柜分类的 `show_*` 过渡和 `appearance` 循环，恢复切换时序、混合参数和退出默认姿态；动态动作纳入显式本地加载清单。
- [ ] 按各职业基础服装、六类武器及主副挂点恢复模型组合；与官方父层变换、尺寸、原比例和位置逐项核验，检查男女及各职业链、空武器、副武器和互斥后外观。
- [ ] 将真实 `Equipment.Equipped` 实例、职业切换和晋升结果同步到 `Wardrobe.WornSkins`／原 `EquipSkinDict` 语义，完成幻装覆盖、卸下后装备／基础皮肤回退与不兼容装备处理。
- [ ] 接入官方遮罩、固定色和 HSV 的材质渲染，恢复正式人物称号及限时显示；从外观编辑／试穿返回后刷新实际形象，临时预览与正式穿戴分别遵循原提交行为。

### 属性、战力、等级与位阶

- [ ] 恢复上文全部有效属性分块与战力公式，将现有基础属性、衣柜接口及各所属系统接入同一计算结果；覆盖装备词条／套装／共鸣／星石、职业累计等级、实际技能阶位／槽位等级、幻兽及其他有效来源。
- [ ] 生成人物主页概要行、综合／细分页签、原顺序来源组、关联百分比、属性名称／单位／说明和展开／折叠；属性说明只读，明细按各来源原截断处理。
- [ ] 补齐普通经验、储存经验、赐福／满赐福各状态的原数字格式、颜色、说明按钮、上限状态和滑条；将 `AwardExperience` 演出与实际奖励、升级延时及成功写盘后的原时长连接，失败不播放。
- [ ] 为位阶页绑定当前／下一位阶、星级、等级限制、基础属性前后值、材料持有／需求、首个缺料定位及不足提示，接通突破成功通知与动态刷新。原位阶／赛季开放倒计时的单机处理沿用[离线开放条件](offline-access.md)，保留人物等级与材料条件。
- [ ] 接通10级返回家园教学、材料说明、一次领取白垩石片2个和罗拉2000、教学中断恢复及防重复领取；核验经验跨位阶、跨等级和当季赐福边界及实际读档结果。

### 人物装备五槽

- [ ] 绑定五槽实例、空槽／已穿图标、职业限制、副武器解锁、强化／祝福等级、星石小图、幻装标记及原缺装／更优／可强化等提示点。
- [ ] 连接空槽候选、已穿详情、更换、单双栏比较、原排序、空列表和新获得标记；比较读取原评分及部位星石影响，确认前执行原职业／套装检查，穿脱后同步模型、属性和战力。
- [ ] 将人物入口与[装备](equipment.md)中的强化、继承、星石、词条、锁定、分解、碎片合成及奖励流程连通，复用同一实例 ID、背包、部位强化档案及原子存档事务。

### 技能安装与附属入口

- [ ] 将 `OfficialSkillAssetCatalog` 的导入／绑定、`OfflineSkillProfile` 及服务接入统一人物档案；背包、职业、赛季投资与技能槽共用事务，按[技能](skills.md)处理版本、不完整档案及18条缺少元数据的碎片。
- [ ] 恢复三套方案、四战技／四秘法槽、方案切换动画与改名、安装遮罩、目标槽高亮、替换／卸下、同类拖拽换位、占位恢复及操作中的返回限制，方案切换同步出战幻兽。
- [ ] 恢复战技／秘法／碎片页签、当前职业／其他职业筛选、可合成项、收藏／普通分组和原排序、收藏／取消收藏事务及提示、职业不符标记、可升级／可升阶／可合成提示及新物品标记的清除时机。
- [ ] 连接详情、推荐、合成确认及嵌套返回、共享槽培养／祝福、单次／批量升阶、原结果动画／音效和品质文字；批量开放等级等已确认差异沿用[技能](skills.md)，不另改费用或按钮优先级。
- [ ] 连接技能成就页签及其任务／领奖状态，切换时同步公共人物资料；接通伤害测试入口、开放判断、怪物／难度／回合／参数选择和实际战斗／结果，战斗规则由[共用战斗](battle-execution.md)维护。

### 幻兽装配与职业联动

- [ ] 恢复主页随行幻兽挂点、原幼年 UI 模型及空／已装状态；连接按原战力比较器排列的选择列表、“无幻兽”、选中状态和加入／退出方案事务。
- [ ] 区分主页随行与技能方案出战索引，连接三套技能方案与对应幻兽展示、衣柜隐藏／返回恢复、选择后属性刷新和跨重启恢复；完整养成、四阵位及原模型／图标缺口按[幻兽](pets.md)落实。
- [ ] 接通职业总览／树／成就、立绘／底色／雷达、总职业等级／累计属性和帮助；职业培养、晋升、普通／自由转职完成后统一整理武器、技能方案、祈愿职业及人物模型，完整规则与单机差异按[职业](professions.md)执行。

### 衣柜、称号、潮流等级与子页

- [ ] 对已接入的一级／二级分类、穿脱和筛选补齐原所有选中／已穿／品质／新获得／性别／职业标记及原提示文案，保留原排序、卸下首项和副武器实际装备检查；物品数／职业／穿戴状态变化时自动刷新。
- [ ] 恢复 `TryGetConflictTip` 的前后有效集合、部件名称与互斥环提示；提示后继续原穿戴流程。同步称号双列实际渲染、主页称号、限时倒计时及到期后拥有／穿戴状态。
- [ ] 连接物品／称号详情、首次收藏经验、品质／有效期／属性、原试穿窗口，接通任务称号“领取并立即穿戴”选择与奖励事务，任务规则引用[成就](achievements.md)及[任务](tasks.md)。
- [ ] 恢复原潮流等级升级按钮、可升级／经验不足／满级状态、本级经验需求、滑条与跨级新增属性弹窗。当前 `RefreshWardrobeProgress` 直接按总经验重算等级，`UpgradeBtn` 仅显示自动累计提示，须恢复 `CanUpgradeAppearanceLevel → TryUpgradeAppearanceLevel` 的原升级行为；该自动升级差异未记录为用户确认的单机改动。原 `SyncAppearanceLevelFromExp` 只在经验对应等级低于已存等级时回落。
- [ ] 用原 `AppearancePropLevelItem` 逐级展示已激活和后续属性、当前级定位与滚动；按 `FashionExpGroupInfo` 恢复类别／品质收藏经验规则行，替换当前属性 ID 拼接与固定文本追加。
- [ ] 按原已有类别和开放条件控制底栏；接通技能幻彩的列表、选择、原视频预览、原技能持有校验、穿戴／卸下及战斗表现；接通家园装潢数据提供器、一／二级分类、选择事务及本地实际预览，家园规则由[主城与家园](city-and-home.md)维护。
- [ ] 将角色幻装、称号、技能幻彩和家园装潢的收藏经验、限时排除、首次限时称号记录和等级属性共同接入统一存档；既有续期规则按“自定义实现”执行。

### 保存、资源与完成验证

- [ ] 动态模型、动作、称号、字体／材质、图标、预览视频、家园预览、特效与音频均从随游戏交付的本地清单加载；按[素材管理](../asset-management.md)补齐官方来源、必要转换、依赖、哈希及显式绑定。
- [ ] 让穿脱、技能方案、幻兽方案、潮流升级、经验／位阶、职业变化和奖励使用统一事务；覆盖重复操作、取消、失败回滚、到期同步、旧状态校验、角色槽隔离及重新启动恢复。
- [ ] 修复进入 Play Mode 所需的实际编译阻塞并执行相关 Editor 校验；从 `Bootstrap.unity` 的玩家可见入口验证上述页面与返回链，在450×800及其他竖屏尺寸实际检查点击、滚轮／拖拽、遮挡、字体／布局、连续动画、原音效和读档恢复。
- [ ] 验证结果分别写明静态检查、Editor 导入／编译、Play Mode 逻辑、真实输入、画面／动画／音频及存档范围；本页全部流程和相关共享调用方实际验证完成后，才将人物界面标记为完成。

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

| 范围 | 官方依据或 Unity 定位 |
| --- | --- |
| 属性分块／技能收藏补充 | [PropChunckFactory](../../.tools/source-packages/official-code/common-source/PropChunckFactory.cs)、[技能详情与收藏操作](../../.tools/source-packages/official-code/client-source/SkillPanelSkillDetailDialog.cs) |
| 人物主页／入口 | [CharacterPanel](../../.tools/source-packages/official-code/client-source/CharacterPanel.cs)、[CharacterPanelPropPanel](../../.tools/source-packages/official-code/client-source/CharacterPanelPropPanel.cs)、[CharacterPanelProfessionIcon](../../.tools/source-packages/official-code/client-source/CharacterPanelProfessionIcon.cs)；[CharacterFeatureRuntime](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterFeatureRuntime.cs)、[OfficialCharacterPanelView](../../unity/Assets/Game/Runtime/Presentation/Character/OfficialCharacterPanelView.cs) |
| 属性／战力 | [PlayerPropContext](../../.tools/source-packages/official-code/common-source/Common/PlayerPropContext.cs)、[EPropHandleType](../../.tools/source-packages/official-code/common-source/Common/EPropHandleType.cs)、[CharacterPropCalculateIntent](../../.tools/source-packages/official-code/client-source/CharacterPropCalculateIntent.cs)、[PropDetailCategory](../../.tools/source-packages/official-code/client-source/PropDetailCategory.cs)、[属性综合行](../../.tools/source-packages/official-code/client-source/CharacterDetailPropsDialogOverallContent.cs)、[细分来源](../../.tools/source-packages/official-code/client-source/CharacterDetailPropsDialogDetailedContent.cs)、[折叠来源行](../../.tools/source-packages/official-code/client-source/CharacterDetailPropDialogDetailPropItem.cs) |
| 成长／位阶 | [LevelPropParser](../../.tools/source-packages/official-code/common-source/Common/LevelPropParser.cs)、[BattleFormulaHandler](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)、[PlayerLevelText](../../.tools/source-packages/official-code/client-source/PlayerLevelText.cs)、[PlayerExpSlider](../../.tools/source-packages/official-code/client-source/PlayerExpSlider.cs)、[PlayerBlessLevelUtils](../../.tools/source-packages/official-code/common-source/Common/PlayerBlessLevelUtils.cs)、[PlayerExpRewardHandler](../../.tools/source-packages/official-code/client-source/PlayerExpRewardHandler.cs)、[CharacterUpgradePanel](../../.tools/source-packages/official-code/client-source/CharacterUpgradePanel.cs) |
| 装备入口 | [五槽图标与点击](../../.tools/source-packages/official-code/client-source/CharacterPanelEquipItem.cs)、[候选列表](../../.tools/source-packages/official-code/client-source/CharacterPanelEquipListPanel.cs)、[装备详情](../../.tools/source-packages/official-code/client-source/CharacterPanelEquipDetailPanel.cs)、[装备比较](../../.tools/source-packages/official-code/client-source/CharacterPanelEquipComparePanel.cs)；规则与资源见[装备](equipment.md) |
| 技能入口／方案 | [技能与成就页签](../../.tools/source-packages/official-code/client-source/CharacterPanelSkillPanel.cs)、[安装与培养控制](../../.tools/source-packages/official-code/client-source/CharacterPanelSkillContent.cs)、[列表／收藏／筛选](../../.tools/source-packages/official-code/client-source/CharacterPanelSkillListContent.cs)、[三套方案](../../.tools/source-packages/official-code/client-source/CharacterPanelSkillPlan.cs)、[拖拽](../../.tools/source-packages/official-code/client-source/CharacterPanelSkillItemDragContent.cs)、[伤害测试](../../.tools/source-packages/official-code/client-source/StageDamageTestPanel.cs) |
| 幻兽／职业 | [PlayerWithPetContent](../../.tools/source-packages/official-code/client-source/PlayerWithPetContent.cs)、[PetPlanContent](../../.tools/source-packages/official-code/client-source/PetPlanContent.cs)、[PetSelectPanel](../../.tools/source-packages/official-code/client-source/PetSelectPanel.cs)、[PetUIUtils](../../.tools/source-packages/official-code/client-source/PetUIUtils.cs)、[ProfessionOverviewPanel](../../.tools/source-packages/official-code/client-source/ProfessionOverviewPanel.cs) |
| 衣柜／互斥／称号 | [AppearancePanel](../../.tools/source-packages/official-code/client-source/AppearancePanel.cs)、[分类与展示动作](../../.tools/source-packages/official-code/client-source/AppendAppearanceContent.cs)、[列表](../../.tools/source-packages/official-code/client-source/AppendAppearanceGridContent.cs)、[穿脱与冲突提示](../../.tools/source-packages/official-code/client-source/AppendAppearanceGridItem.cs)、[称号双列](../../.tools/source-packages/official-code/client-source/AppendAppearanceHonorContent.cs)、[称号状态](../../.tools/source-packages/official-code/client-source/AppendAppearanceHonorItem.cs)、[称号领取](../../.tools/source-packages/official-code/client-source/AppendAppearanceHonorTitleDialog.cs)、[称号详情／试穿](../../.tools/source-packages/official-code/client-source/HonorTitleItemDetailDialog.cs) |
| 潮流等级／收藏 | [AppendAppearanceLevelContent](../../.tools/source-packages/official-code/client-source/AppendAppearanceLevelContent.cs)、[AppearanceDataEx](../../.tools/source-packages/official-code/common-source/Common/AppearanceDataEx.cs)、[FashionExpUtil](../../.tools/source-packages/official-code/common-source/Common/FashionExpUtil.cs)、[等级属性窗口](../../.tools/source-packages/official-code/client-source/AppearancePropDialog.cs)、[等级属性行](../../.tools/source-packages/official-code/client-source/AppearancePropLevelItem.cs)、[收藏规则窗口](../../.tools/source-packages/official-code/client-source/AppearanceLevelDoubleDescRuleDialog.cs) |
| 技能幻彩／家园装潢 | [底栏可见性](../../.tools/source-packages/official-code/client-source/AppearanceBottomTab.cs)、[AppendAppearanceSkillContent](../../.tools/source-packages/official-code/client-source/AppendAppearanceSkillContent.cs)、[AppearanceHomeDecorationContent](../../.tools/source-packages/official-code/client-source/AppearanceHomeDecorationContent.cs) |
| 模型／染色／外观变更 | [PlayerAppearanceHandler](../../.tools/source-packages/official-code/common-source/Common/PlayerAppearanceHandler.cs)、[AppearanceUtils](../../.tools/source-packages/official-code/client-source/AppearanceSystem.Runtime/AppearanceUtils.cs)、[ChangeAppearancePanel](../../.tools/source-packages/official-code/client-source/ChangeAppearancePanel.cs)、[PreviewAppearanceDialog](../../.tools/source-packages/official-code/client-source/PreviewAppearanceDialog.cs)、[CharacterModelPreview](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterModelPreview.cs) |
| 规则／资产／校验 | [人物原表来源及哈希](../../unity/Assets/Official/Character/source-manifest.json)、[模型来源](../../assets/reference/character-appearance/source-manifest.json)、[人物进度服务](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs)、[人物目录](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs)、[衣柜界面](../../unity/Assets/Game/Runtime/Presentation/Character/OfficialWardrobePanelView.cs)、[目录绑定与规则校验](../../unity/Assets/Game/Editor/Character/CharacterProgressionValidation.cs)、[人物目录生成](../../scripts/prepare_unity_character_catalog.py)、[模型包生成](../../scripts/prepare_character_model_runtime.py)、[人物面板生成](../../scripts/prepare_unity_character_panels.py)、[衣柜界面提取](../../scripts/prepare_appearance_ui.py) |
