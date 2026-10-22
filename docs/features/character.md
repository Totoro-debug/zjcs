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

人物程序集 [`Zjcs.Presentation.Character.asmdef`](../../unity/Assets/Game/Runtime/Presentation/Character/Zjcs.Presentation.Character.asmdef) 显式引用 `Zjcs.Offline.Skills`，使 `CharacterSkillPanel` 的技能方案类型可被 Unity 编译器解析。此引用及 Map10 网格 UV 的类型修复后，同版本 Unity Editor 全工程脚本编译成功并加载 `Zjcs.Editor.CharacterCreation`；该结论仅覆盖脚本编译与程序集加载，不代表人物页或开场 Play Mode 验收通过。

Unity 已接入固定版本 `854_156383` 人物成长目录、人物概览与属性文本、限时外观配置、原衣柜规则/物品/UI 数据及其引用图片。生成入口为 [prepare_unity_character_catalog.py](../../scripts/prepare_unity_character_catalog.py)，输出与官方输入哈希登记于 `unity/Assets/Official/Character/source-manifest.json`；`OfficialCharacterAssetCatalog.FindSprite` 按原资源 URI 查找人物与衣柜 Sprite，编辑器绑定及来源核对入口为 [CharacterProgressionValidation](../../unity/Assets/Game/Editor/Character/CharacterProgressionValidation.cs) 的 `BindOfficialCatalogs`。

[OfficialCharacterPanelView](../../unity/Assets/Game/Runtime/Presentation/Character/OfficialCharacterPanelView.cs) 按已恢复的人物、属性和位阶面板层级构建 Unity UI，并绑定等级/经验、位阶材料操作、属性弹窗和官方 `AppearanceBtn`；[CharacterFeatureRuntime.OpenProfile](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterFeatureRuntime.cs) 从人物存档槽打开页面。[CharacterCreationFlowController](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 在新建或选中已有角色后提供“人物”入口，关闭人物页返回当前存档流程。

人物属性管线按固定版 `PlayerPropContext`／`PropChunkFactory` 分块实现：[CharacterPropCalculator](../../unity/Assets/Game/Runtime/Offline/Progression/CharacterPropCalculator.cs) 构建 `BattleBasic`、`Profession`、`Equip`、`Appearance` 分块并按原 `HandleBaseProps`、`HandlePercentProps`、`HandleScaleProps` 顺序处理战斗外属性，用 `CalcCombatRatingCommon` 的 Base／NonBase／BaseRate／BasePercent／Advanced／NonBasePercent 六组、`ScoreBase` 递归和 `CommonCombatRatingRefData` 计算分块战力；[OfficialPropCatalog](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialPropCatalog.cs) 解析 352 条 `prop_cfg`、显示名／说明／图标、分类、单位本地化与相对属性，[OfficialPropFormat](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialPropFormat.cs) 复刻原 `FormatPropString` 的万／亿与百分比格式。生成脚本在 `character-progression.json` 中新增 `prop_units`、`prop_categories`，并从原预制体提取属性弹窗的 `PropCatalogCfgList` 与细分分组绑定。

[CharacterSkillPanel](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.cs) 绑定原 `CharacterPanelSkillContent`：三套方案前后切换与改名（`OfflineSkillProgression.SelectPlan/RenamePlan`）、四个战技槽与四个秘法槽、槽位等级、`CharacterPanelSkillListPanelActiveSkillTabGroupItem`／`PassiveSkillTabGroupItem`／`SkillPieceTabGroupItem` 页签、`ShowAll` 开关、技能列表与原 `SkillScrollList` 滚动区。列表排除当前方案已安装技能，按品质／ID 排序，已拥有技能可点击安装到选中槽（占用时替换），再次点击已选槽卸下；未拥有技能在 `ShowAll` 下以职业不符颜色显示且安装被原规则拒绝。

[CharacterEquipSlots](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterEquipSlots.cs) 绑定原五槽 `CharacterPanelEquipItem`：主武器 10、副武器 14、头盔 11、衣服 12、靴子 13，显示已穿装备图标（品质底图、`item_icons` 或原缺失图标文本）、等级与副武器职业解锁（`profession_base.Rank > 1`）；空槽点击打开原 `CharacterPanelEquipListPanel`，候选按装备评分（原 `EquipItemComparer` 的 Score→Quality→ClassId 降序）列出本部位且职业可穿实例，点击经 `OfflineEquipmentOperations.Wear` 穿戴并同步 `Wardrobe.WornSkins` 的 body／pWeapon／sWeapon 皮肤。已穿槽的原详情／比较面板仍待接线。

[OfflineEquipmentPropSource](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentPropSource.cs) 实现原 `EquipPropChunk`：按 `equip`／`equip_type` 的 `Main`／`Sec`／`Other` 属性类、强化等级（`SlotProgress.BaseLevel`）、祝福等级、位阶差与 `equip_suit` 套装阈值合并装备分块，并以逐件装备属性计算装备分块战力；装备目录缺失时 `CharacterFeatureRuntime` 记录警告并继续运行。装备属性使用 `equipment-runtime-rules.json` 的 `props` 等级表与 `equip_attributes*` 数据。

[CharacterRankPanel](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterRankPanel.cs) 绑定原 `CharacterUpgradePanel`：当前／下一位阶名称（`rank name`＋`sub`）、位阶星级（按 `player_subrank` 的同一大位阶分组，`GetState` 的已亮／下一枚／未亮）、当前等级与基础等级上限、下一位阶等级上限、`PropCatalog.Base` 基础属性前后值、`player_subrank.Items` 材料（原 `MapId`、持有／需求、品质底图与 `item_icons` 图标）、`LimitState` 的无限制／等级不足／满位阶状态和 `UseItemBtn` 突破。突破成功后刷新面板并保持同一实例；原 `UpgradeNotifyDialog` 与 10 级教学仍未接线。

[CharacterPropPanels](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterPropPanels.cs) 绑定人物页概要属性行与综合／细分属性弹窗：概要行读取原 `CharacterPanelPropPanel` 绑定（`PropCatalog=9`、`SlotPropItems` 与原 `row`／`row_fields` 模板），弹窗使用 `character_props` 原层级和 `PropCatalogCfgList`（主要属性 `Main`、附加属性 `Addition`），细分按原 `PropDialogDetailedPropItem` 分组、`mPropCatalogList=[Main,Addition]` 过滤，并支持逐组折叠／展开。[CharacterPanelNodeFactory](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterPanelNodeFactory.cs) 统一还原原布局组、`ContentSizeFitter`、`ScrollRect`、`RectMask2D`、Mask 与按钮；人物页和弹窗使用独立节点索引，关闭弹窗只销毁弹窗本身。

[OfficialWardrobePanelView](../../unity/Assets/Game/Runtime/Presentation/Character/OfficialWardrobePanelView.cs) 按 `wardrobe-ui.json` 的原控件树、`AppendAppearanceFirstTabItem.mTabGroupObj` 与 `AppendAppearanceSecondTabItem.mItemType` 绑定大类和子类；穿戴列表读取已拥有物品并按原可穿规则筛选，称号使用原双列模板，穿脱、称号、风尚经验、规则/属性弹窗及倒计时经人物进度服务处理。已到期物品在衣柜打开期间同步并从人物状态卸下。

[CharacterModelPreview](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterModelPreview.cs) 在官方 `PlayerModelRoot` 播放男性/女性职业 1/10 的 24 fps 原网格帧动画。运行包由 [prepare_character_model_runtime.py](../../scripts/prepare_character_model_runtime.py) 按 profile 使用的四种待机姿态、网格和贴图生成至 `unity/Assets/StreamingAssets/Character/character-models.zip`；包内每个载荷有 SHA256 校验，外部来源清单 `assets/reference/character-appearance/source-manifest.json` 记录包哈希、输入文件与五份 854_156383 关联来源清单。模型按已保存衣柜/装备皮肤解析换装，不依赖 Spine runtime。

当前已完成固定版数据恢复、人物存档入口、衣柜基础交互、人物页 idle 模型播放、属性分块／战力管线以及概要属性和综合／细分属性弹窗；1,563 个可穿戴外观项、1,723 个皮肤定义通过 `CharacterProgressionValidation.BindOfficialCatalogs` 绑定验证，人物属性图标与面板 Sprite 由官方清单导入，基础属性断言为 300/60/60/48，职业 1 的 FeatureProps 使其战斗外生命为 327。`CharacterProgressionValidation.Run` 新增属性管线断言（352 条 prop_cfg、主要属性 4 行、附加属性 96 行、战力为正、人物基础细分 60 攻击、万／百分比格式）。

属性、位阶、装备五槽（含强化／比较／详情）、技能（方案、安装遮罩、收藏、碎片、批量培养）与潮流等级的当前验证范围：`Zjcs.Offline.Skills` 的技能方案／安装／替换／卸下／改名在 Mono 校验程序中通过；穿戴互斥预览、潮流等级跨级与属性/规则弹窗模板由同一目录驱动。Editor `CharacterProgressionValidation.Run` 另校验推荐方案的职业与技能 ID、宝石／强化材料图标、职业名称与图标。Unity 2022.3.57f1c2 Editor 导入/编译通过（`-batchmode -nographics -executeMethod`）：`CharacterProgressionValidation.BindOfficialCatalogs` 成功，日志输出“1563 wardrobe entries, 1723 appearance skins, 1764 official sprites, pinned 854_156383”；随后 `CharacterProgressionValidation.Run` 通过（experience、base properties、wardrobe mutex、timed appearance extension、property pipeline），退出码 0。Play Mode：新增 `Zjcs/Validate/Character Play Mode`（`CharacterPlayModeAcceptance`）逻辑验收。实际运行中修复了三个只会在 Play Mode 暴露的问题：内置 `Arial.ttf` 已失效（改为 OS 中文字体优先、窗口态 `Microsoft YaHei`，batchmode 跳过动态字体）；人物属性摘要行把原始节点当屏幕模板读取导致 `FormatException`（改为 `Read` 兼容原始节点）；角色模型包 `assets/characters/catalog.json` 缺失时不再让整页初始化失败（记录警告并跳过模型）。复查模型包发现 `CharacterModelPackage` 误读 `assets/characters/catalog.json`，实际包内为 `assets/characters/wardrobe/catalog.json`，已修正（`76bc96c`），这是角色外观完全无法渲染的生产错误。窗口态自动运行已加入 `CharacterPlayModeAcceptance`（环境变量 `ZJCS_CHARACTER_AUTORUN=1`，`-disable-assembly-updater` 启动，成功后截图到 `output/character-captures`）；实际启动时被 Unity GUI 的 “Script Updating Consent”／并行编译错误阻断，未取得截图。Home/Wishes 循环依赖已解除（移除 Wishes 对 Home 的过期引用，`39b5ad6`），Play Mode 逻辑验收在技能合成／拖拽／衣柜已读等改动后再次通过（batchmode，退出码 0）：日志 `profile level=10 profession=1 created=True name=人物验收`，真实按钮打开技能面板、切换技能方案并穿戴称号，重新打开存档后方案与称号均持久化，日志“Play Mode logic passed”。batchmode 下未生成 `WeaponEquipItem` 等装备槽图标节点（仅 83 个 Image），已降级为警告；窗口态图标渲染、动画与鼠标命中仍未验证。`python -B scripts/check_unity_project.py` 静态检查通过（27,795 个 GUID）；`Zjcs.Offline.Progression`、`Zjcs.Presentation.Character`、`Zjcs.Editor.Character` 使用 Roslyn 单独编译通过；同一 `Evaluate` 管线在同步的 Mono 校验程序中对 `character-progression.json`／`character-overview.json` 运行通过，并在生成一件 20000 主武器后验证攻击 60→84、战力 786→867、装备明细来源出现。Unity Editor 的 `BindOfficialCatalogs`／`Run` 与 Play Mode 画面验证尚未执行（验证时另一会话正占用该工程实例）。技能、幻兽、古遗物、星石、料理、额外、公会、星命与 NPC 分块仍为 0，因此战力覆盖基础、职业、装备与衣柜来源。人物页称号图标与剩余时间已显示；官方 HSV 染色结果渲染尚未完成。官方衣柜树没有染色控件；原 HSV 编辑入口属于 `ChangeAppearancePanel`，不在本次衣柜界面范围内。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

人物页与衣柜中的角色显示沿用[Spine 角色动画迁移决策](../asset-management.md#spine-角色动画迁移决策)；当前 profile idle 已接通免费原生帧网格路线，衣柜换装从人物状态刷新模型。HSV 染色数据仍保存在进度服务，但材质/遮罩着色和实际页面验证未接通。

用户补证普通经验在当季基础上限后继续提升显示等级，超出部分为当季赐福，赛末回到当季基础上限。服务端经验路由未取得，本地按此处理并保留赛季记录。

限时装扮首次有效期和到期卸下依据原客户端；重复获得的服务端续期未取得。用户已确认按配置 `CanOverlapTime` 叠加 `Duration`，并将剩余有效期限制在 `MaxDuration` 内；不允许叠加时按配置重设时长。此续期规则为已确认的离线实现。

## 尚未实现

- 储存经验说明已接原 `player_exp_max_title/desc1/desc2` 文本并在人物页规则按钮打开说明层；仍缺经验领取成功后的奖励演出与升级延时衔接、位阶教学触发/恢复和返回家园条件，失败与保存结果也只在状态文本体现。
- 已接入装备主／副／额外与套装属性及人物页、位阶、五槽、技能、衣柜的界面；仍缺装备共鸣、星石、特殊词条（`equip_attributes_passive_skill` 的被动技能等价属性）与技能分块、幻兽、古遗物、料理、额外、公会、NPC、星命属性来源。Map10 战斗与家园仍各自拼装基础＋衣柜属性，尚未改为读取 `EvaluateProps` 快照。
- 将人物/衣柜换装后的遮罩与 HSV 染色接入帧网格渲染，并核对原染色入口、互斥提示和称号在人物页的显示；原衣柜底部技能幻彩/家园装潢子页仍需接到所属功能入口。
- 在 Unity Play Mode 中验证人物与衣柜完整流程、不同性别/职业模型、穿脱和读档恢复；全工程编辑器验证还需先修复 `OfficialMap10TerrainView` 与共享战斗网格检查阻塞。

### 人物入口、窗口与共享表现

- [ ] 将现有创角后“人物”入口与正式头像／底栏人物入口统一到当前存档槽，接通 `profession/skills/pet` 跳转的实际接收方，返回时恢复原顶栏、底栏、公共人物资料和主页状态。
- [x] 人物页职业按钮已绑定 `profession_name_{enum}` 本地化名称与 `ProfessionBase.Icon2` 图标（31 个职业有名称、31 个有图标），称号与有效期、各槽位状态已分别接入。
- [ ] 按原 Function 与所属单机开放规则控制入口可见、可点、提示和动态刷新。
- [ ] 在已恢复的布局组、`ContentSizeFitter`、`ScrollRect`、`RectMask2D` 和属性行动态生成基础上，补齐 TMP 字体／材质／描边、状态切换动画、原动画／音效和本地化字段，以及物品图标、材料行和空状态。
- [x] 生命周期已补：`OnDestroy` 停止经验协程、关闭弹窗与子页、销毁衣柜覆盖层；`CharacterFeatureRuntime` 订阅 `Application.lowMemory` 调用原 `OnLowMemory` 回收未显示子页；弹窗使用独立节点索引，`CloseDialog` 只销毁弹窗。待实际画面回归确认。

### 人物模型、外观同步与染色

- [ ] 在现有男女四份 idle 姿态基础上接入原衣柜分类的 `show_*` 过渡和 `appearance` 循环，恢复切换时序、混合参数和退出默认姿态；动态动作纳入显式本地加载清单。
- [ ] 按各职业基础服装、六类武器及主副挂点恢复模型组合；与官方父层变换、尺寸、原比例和位置逐项核验，检查男女及各职业链、空武器、副武器和互斥后外观。
- [ ] 将真实 `Equipment.Equipped` 实例、职业切换和晋升结果同步到 `Wardrobe.WornSkins`／原 `EquipSkinDict` 语义，完成幻装覆盖、卸下后装备／基础皮肤回退与不兼容装备处理。
- [ ] 接入官方遮罩、固定色和 HSV 的材质渲染，恢复正式人物称号及限时显示；从外观编辑／试穿返回后刷新实际形象，临时预览与正式穿戴分别遵循原提交行为。

### 属性、战力、等级与位阶

- [ ] 在已实现的基础／职业／衣柜分块与六组战力评分上，接入装备词条／套装／共鸣／星石、职业累计等级、技能阶位／槽位等级、幻兽及其他有效来源；`ScoreLevelGroupId` 的 `score_level_group` 与 `level_number` 表尚未纳入固定目录（当前只影响元素亲和属性）。
- [x] 属性行详情已接：概要行与细分行可点击打开说明层，显示 `character-properties.json` 中的图标、名称与只读说明；说明层复用原 `CharacterDetailPropsDialog` 框架，原 `EntryDetailDialog` 预制体未提取。
- [ ] 补齐普通经验、储存经验、赐福／满赐福各状态的原数字格式、颜色、说明按钮、上限状态和滑条；将 `AwardExperience` 演出与实际奖励、升级延时及成功写盘后的原时长连接，失败不播放。
- [ ] 位阶页已绑定当前／下一位阶、星级、等级限制、基础属性前后值、材料持有／需求、等级／满位阶状态和突破；成功通知已接：突破后弹出原 `UpgradeNotifyDialog` 语义的提示层（位阶前后名称与基础属性变化），确认关闭；原预制体未提取，提示层复用属性弹窗框架。缺料提示已显示在面板底部（逐项名称与持有/需求）；剩余：首个缺料图标定位、`PlayerSubRankRemainTime` 的位阶／赛季开放倒计时。离线按[离线开放条件](offline-access.md)保留人物等级与材料条件，不等待服务器位阶，因此 `RankUnlock`／倒计时状态当前不出现。`bindings.rank` 已提取全部字段可继续补齐。
- [x] 10 级位阶教学已接：打开位阶页且未领取时弹出教学说明（材料与奖励、只能领取一次），阅读即记 `RankTutorialSeen`，点击领取调用 `ClaimRankTutorial` 发放白垩石片×2与罗拉×2000，重复领取由服务拒绝。原教学预制体未提取，说明层复用属性弹窗框架。
- [ ] 核验经验跨位阶、跨等级和当季赐福边界及实际读档结果；补“返回家园触发/中断恢复”的原入口条件（当前为打开位阶页触发）。

### 人物装备五槽

- [x] 五槽实例、已穿图标、品质底图、等级、副武器职业解锁、空槽／已穿与空列表状态已接线；装备主／副／额外／套装属性与分块战力已进入人物属性。
- [x] 槽位已显示装备等级、祝福等级、幻装标记、宝石数量与“可强化”提示（`QuoteUpgrade`），装备图标已纳入人物目录 `item_icons`（1,223 件装备）。
- [x] 候选列表对评分高于当前装备的项显示“更优”；已穿详情显示最多 6 条主/副属性与词条（普通词条显示属性名与倍率，特殊词条显示“特殊词条”）。
- [x] 空槽显示“未装备”提示；星石小图（当前仅数量，镶嵌详情已可操作）与原红点尚未接；装备图标未覆盖项仍走原 `MissingIconText` 末四字降级。
- [x] 已穿槽点击打开原 `CharacterPanelEquipDetailPanel` 框架的详情层（图标、名称、等级、主／副／额外属性与评分来源），提供“更换／强化／关闭”；更换进入候选列表，选中候选后打开原 `CharacterPanelEquipComparePanel` 框架的双栏比较（当前／更换名称与装备评分），确认后经 `WearEquipment` 替换并刷新。原详情内部件与词条专属控件未提取，词条与星石仍以属性行展示/未展示。
- [x] 详情层“强化”打开原 `EquipPromotePanel` 框架的强化层：当前／下一等级、`LimitKey` 对应条件提示（等级／位阶／赐福／部位差距／上限）、材料名称与持有／需求，确认经 `UpgradeEquipment` 扣材料并刷新槽位与详情。
- [ ] 部位星石影响、强化成功演出与原套装／职业检查提示仍缺。
- [x] 人物详情已接强化、锁定／解锁、分解（二次确认）与卸下，操作经统一存档事务并刷新槽位与背包；均复用同一实例 ID 与 `OfflineEquipmentOperations`。
- [x] 星石已接入详情“星石”层：列出已镶嵌孔位并可卸下，列出背包中可镶嵌宝石并可镶嵌；`InlayGem` 的 `function_limit`／`equip_inlay_site` 条件由 `LevelCondition`、`EquipLevelCondition` 与 `CompositeAndCondition` 求值（未知条件按未开放处理）。
- [x] 已接“同赛季完整继承”：详情“继承”列出同类型装备，确认后由 `Inherit` 模式 0 把来源词条/宝石完整转移到目标并消耗来源，目标保持穿戴；目标随后为锁定态。跨赛季自选词条继承（模式 1、`InheritCost`）仍缺选择界面。
- [x] 词条精炼已接：详情“精炼”层可勾选要保留的词条，按保留数量读取 `equip_purify_cost` 扣除材料，其余词条经 `OfficialEquipmentGenerator.GenerateReplacement` 重掷并保存。
- [ ] 跨赛季自选词条继承与装备碎片合成仍待连通；卸下后的背包展示沿用背包模块。

### 技能安装与附属入口

- [x] `OfflineSkillProfile` 已并入统一人物存档（`OfflinePlayerProgress.Skills`），技能规则由人物目录绑定 `Assets/Official/Skills/skill-runtime-rules.json`；人物技能按钮打开原技能面板，安装／替换／卸下／方案切换与改名经 `ApplyCharacterMutation` 写入同一存档。
- [x] 恢复三套方案切换、方案改名、四战技／四秘法槽、目标槽高亮、列表安装替换与再次点击卸下、页签与 `ShowAll`；方案切换同步出战幻兽仍待幻兽模块。
- [x] 同类技能槽拖拽换位已接：`SkillSlotDrag` 拖起槽位、放到另一同类槽后经 `SwapSkillSlots` 交换并保存，交换后刷新方案。
- [ ] 方案切换动画、占位恢复与操作中的返回限制仍缺；安装遮罩已接：列表点技能后进入安装态（半透明遮罩＋“选择技能槽”提示＋八个槽高亮），点击目标槽安装并退出安装态，空槽也可直接安装。
- [x] 技能列表已排除当前方案已装项、按品质／ID 排序，并对不满足职业链的技能显示灰色与“（职业不符）”；`IsAvailableFor` 与安装校验共用同一可用性规则。
- [x] 技能收藏已接：`OfflineSkillProfile.FavoriteSkills` 进入统一存档（旧档 `Normalize` 补空），列表行右上角星标可收藏／取消，收藏项排在列表前部；未拥有技能不能收藏。
- [x] 碎片页签已列已拥有技能碎片及数量；拥有对应碎片但未获得的技能在列表中标记“（可合成）”，`ShowAll` 关闭时也显示。
- [x] 碎片合成已接：详情层对未拥有的技能按官方 `skill_rank` 首阶 `Piece` 数量显示“合成”，首次点击提示再次确认，确认后扣除碎片、发放技能物品并写入已拥有技能。
- [ ] 新物品标记清除时机及原排序细节仍待补（合成规则见[技能](skills.md)）。
- [x] 已接批量培养：`FastLevelUpBtn` 调用 `UpgradeAllSlots`、`FastPromotionBtn` 调用 `PromoteAllSkills`，操作前把技能碎片与货币从背包同步到技能档案，成功后扣回背包并刷新槽位；每个技能槽右下角“升级”调用 `UpgradeSlot` 单槽培养；面板底部显示结果状态。
- [x] 单次升阶已接：列表行“培养”打开原 `SkillPromotePanel` 框架，显示当前／下一等阶与 `RankCosts` 材料持有/需求，确认经 `PromoteSkill` 扣料并刷新；批量升阶仍由 `FastPromotionBtn` 提供。
- [x] 技能详情层已接：列表行“详情”显示类型、当前/最大等阶、碎片持有与下一等阶材料，并提供安装／培养／收藏（或取消收藏）／关闭；技能描述字段未在固定目录中提取，详情暂不含描述。
- [x] 技能推荐已接：原 `RecommendBtn` 列出当前职业的推荐方案（名称与核心技能），应用时把已拥有且满足职业条件的技能装入对应槽位，未拥有项跳过并统计；推荐数据来自 `Assets/Official/Skills/skills.json`。
- [ ] 连接共享槽祝福（`SlotBlessCosts`）、原结果动画／音效和品质文字；规则与已确认差异沿用[技能](skills.md)。
- [ ] 连接技能成就页签及其任务／领奖状态，切换时同步公共人物资料；接通伤害测试入口、开放判断、怪物／难度／回合／参数选择和实际战斗／结果，战斗规则由[共用战斗](battle-execution.md)维护。

### 幻兽装配与职业联动

- [ ] 恢复主页随行幻兽挂点、原幼年 UI 模型及空／已装状态；连接按原战力比较器排列的选择列表、“无幻兽”、选中状态和加入／退出方案事务。
- [ ] 区分主页随行与技能方案出战索引，连接三套技能方案与对应幻兽展示、衣柜隐藏／返回恢复、选择后属性刷新和跨重启恢复；完整养成、四阵位及原模型／图标缺口按[幻兽](pets.md)落实。
- [ ] 接通职业总览／树／成就、立绘／底色／雷达、总职业等级／累计属性和帮助；职业培养、晋升、普通／自由转职完成后统一整理武器、技能方案、祈愿职业及人物模型，完整规则与单机差异按[职业](professions.md)执行。

### 衣柜、称号、潮流等级与子页

- [x] 衣柜卡片已显示品质名称与颜色、选中／已穿、不可穿（性别／职业／副武器条件不满足）与剩余时间；“卸下”首项与按品质／ID 排序保留。
- [x] 新获得标记已接：`Wardrobe.SeenItems` 记录已查看物品，未查看的外观／称号卡片显示“新”，点击卡片（穿戴或卸下）后清除并持久化；旧档 `Normalize` 补空。
- [ ] 原提示文案、物品数／职业／穿戴状态变化时的自动刷新（当前进入衣柜、穿脱后刷新）仍待补。
- [x] 恢复 `TryGetConflictTip` 语义：`OfflineWardrobeRules.PreviewWear` 对穿戴前后的有效皮肤集合各解析一次，返回会被遮挡的部件或互斥环类型；衣柜列表穿戴时先提示再继续原穿戴请求。部件名称取原 `ui_append_appearance_type_*` 本地化，原冲突提示模板 `ui_append_appearance_conflict_tip` 未在固定目录中提取，当前以部件名拼接提示。
- [x] 人物页称号已接 `HonorTitleItemProvider/Slot` 与 `HonorTitleTimeLimitContent`：显示已佩戴称号图标、剩余天／小时／分钟／秒并在到期时隐藏，`TitlePropContent` 读取 `AppendAppearanceInfo.EffectProp`（当前固定目录中称号 `EffectProp` 均为空，故属性行暂不显示）。
- [ ] 同步称号双列实际渲染、限时倒计时及到期后拥有／穿戴状态。
- [x] 称号详情已接：点击称号卡打开详情层（品质、剩余有效期、称号属性）并可穿戴；原 `HonorTitleItemDetailDialog` 预制体未提取，详情复用衣柜属性弹窗框架。
- [ ] 补齐物品详情、首次收藏经验、原试穿窗口（`PreviewAppearanceDialog`）及任务称号“领取并立即穿戴”选择与奖励事务，任务规则引用[成就](achievements.md)及[任务](tasks.md)。
- [x] 潮流等级升级已按原规则实现：`RefreshWardrobeProgress` 只在经验对应等级低于已存等级时回落（`SyncAppearanceLevelFromExp`），`UpgradeBtn` 使用 `CanUpgradeAppearanceLevel → UpgradeAppearanceLevel` 跨级提升，满级／经验不足给出状态提示；头部显示当前等级、本级经验／所需经验与滑条填充，满级显示上限状态。
- [x] 属性与规则弹窗改用 `wardrobe-ui.json` 的原 `wardrobe_props`／`wardrobe_prop_row`／`wardrobe_rules`／`wardrobe_rule_row`／`wardrobe_rule_value` 模板：属性按 `appearance_prop` 逐级列出至当前级 +20，显示等级、属性名称与数值、已激活状态；规则按 4 组 `fashion_exp_group` 逐组列出品质与收藏经验；跨级升级后弹出新增属性行。原 `AperaanceUpgradePropDialog` 预制体未提取，跨级弹窗复用属性弹窗模板。
- [ ] 原潮流等级「属性」弹窗使用 `AppearancePropDialog` 的循环列表与 `AppearancePropLevelItem`（内含 `PlayerPropItem`），当前以同一模板的静态行与文本实现；待实际画面核对行高、滚动定位（原 `GetCenterIndex`）和已激活渐变。
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
| 人物属性与战力 | `CharacterPanelPropPanel.RefreshProp/OnShowDetailPropBtnClick` → `BattlePropsOutFight`、`CharacterDetailPropsDialog.Init`；`PlayerPropContext` 合并分块并更新战力 | 已接 `CharacterPropCalculator` 分块、`CharacterPropPanels` 概要行与综合／细分弹窗（含页签、来源顺序、相对属性、折叠展开）。剩余：装备／技能／幻兽等分块来源、属性行点击说明浮层、`ScoreLevelGroupId` 等级系数；Map10 战斗尚未改为读取同一 `CharacterPropSnapshot`。 |
| 人物装备 | `CharacterPanelPropPanel.RefreshEquip/UpdateProfessioon` → 五个 `CharacterPanelEquipItem.Refresh/OnBtnClick` → 空槽候选或已穿详情 | 五槽已绑定实例、正式装备图标、等级与副武器解锁；候选按原评分排序，已穿槽替换先经双栏比较确认，穿脱同步身体／武器皮肤。已穿槽完整详情、强化与星石显示尚未接线。 |
| 技能安装 | `CharacterPanel.OnCharacterPanelPropPanelSkillBtnClickEvent` → `CharacterPanelSkillPanel/CharacterPanelSkillContent`；`CharacterPanelSkillPlan` 管理方案 | 技能档案已进入人物主存档，人物按钮直接打开原技能面板；三方案切换／改名、八槽显示、列表安装替换／卸下已接线，战斗读取 `EquippedActiveSkills/PassiveSkills`。剩余：切换动画、安装遮罩与拖拽、详情／推荐／培养与成就页。 |
| 幻兽装配 | `CharacterPanel.RefrshPet` → `PlayerWithPetContent` → `PetPlanContent.OnSelectPetClick` → `PetSelectPanel`；`PetEditPlanDefaultOperation.EquipPet` 加入/退出出战方案 | 此处官方“装备幻兽”指随行/出战幻兽的选择、更换、卸下和模型展示。Unity 未接 `PetSelectContent`、幻兽选择操作、模型或对应存档；人物 `pet` 按钮同样只有无接收方的跳转事件。 |
| 衣柜 | `AppearancePanel`、`AppendAppearanceContent/GridContent/GridItem`、称号及等级内容 | `OfficialWardrobePanelView` 已有类别、已拥有列表、可穿筛选、穿脱、称号和到期事务；原分类动作、互斥反馈、人物称号显示仍不完整。底栏 `skill-appearance/decoration-appearance` 回调未传入；属性弹窗把属性 ID 和数值拼为文本，规则弹窗追加固定字号 Arial 文本，未绑定原属性行/规则行模板及完整说明内容。 |
| 职业与成长说明 | `CharacterPanelPropPanel.OnProfessionBtnClick`、原经验/位阶组件 | 职业按钮已显示官方职业名称与图标，点击仍只有未被消费的 `profession` 事件；位阶页已接主要字段、突破、成功通知与教学，材料说明与首个缺料定位仍缺；储存经验说明已由规则按钮打开。 |

界面构建已改由 `CharacterPanelNodeFactory` 还原原布局组、`ContentSizeFitter`、`ScrollRect`、`RectMask2D`、Mask、按钮与文本样式，人物页与弹窗使用独立节点索引，`CloseDialog` 只销毁弹窗；原 TMP 字体／材质／描边、状态切换动画、按钮音效与 `OnLowMemory` 子页回收仍未接入。属性弹窗的 `PropCatalogCfgList`、细分分组和行模板由 `prepare_character_prop_bindings.py` 从固定 APK 预制体读取，未再凭节点名推断。

验证边界：`python -B scripts/check_unity_project.py` 静态元数据检查通过（27,132 个 GUID）；属性模块以 Roslyn 单独编译 `Zjcs.Offline.Progression`、`Zjcs.Presentation.Character`、`Zjcs.Editor.Character` 通过，并用同步的 Mono 校验程序对属性目录与 `Evaluate` 管线运行断言通过（基础 300/60/60/48、职业 FeatureProps、装备分块、战力、人物基础细分、数字格式）。Unity `2022.3.57f1c2` 的 `BindOfficialCatalogs`／`Run` 与 Play Mode 逻辑／真实输入／画面／存档恢复尚未执行：验证期间另一会话持续占用工程实例，batchmode 无法打开同一项目。

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
