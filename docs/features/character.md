# 人物属性、经验、位阶与衣柜

[总索引](../README.md)

状态：未完成

## 功能简介

从头像或底栏人物进入，展示名字、外观、等级、经验、位阶、职业、属性与装备；外观按钮进入衣柜，管理已获得的角色幻装及称号。职业、技能和装备分别在对应文档维护。

人物六项界面和离线状态已有代码接线，属性来源也已扩展到离线状态；界面还没有验收通过。窗口 Editor 的 `r43` 可运行到交互检查点，但用户实测反馈页面整体与原版不符，打开幻兽层后没有可见关闭控件，因此这轮画面与真实输入验收失败。官方 `CharacterPanelPropPanel.OnPetBtnClick` 打开独立 `PetPanel`，当前人物页接入的随行候选弹层并不能替代该路由；真实输入、视觉还原、存档重启及 Windows Player 均未通过。本页保持未完成。

### 属性来源与共享计算边界

`CharacterPropCalculateIntent.SortPowerCategorys` 的明细顺序为：人物基础、职业、NPC、装备、技能、古遗物、星石、料理、额外、幻兽、公会、衣柜、星命。装备明细合并 `Equip + EquipResonance`，古遗物合并 `Treasure + TreasureSuit`，NPC 合并 `NpcFriendshipLevel + NpcTalentTree`，星命合并 `Astrological + EquipCabinet`；各来源分别经过 `HandleSingleSystemDisplayProp`，关联属性随主属性展示，零值和无内容来源按原规则隐藏。`PropDetailCategory.Apperance=12` 是明细分类编号，实际计算分块为 `EPropHandleType.Appearance=17`，二者不能混用。

总属性与战力恢复须覆盖 `PlayerPropContext` 和原 `PropChunckFactory` 的实际分块：`BattleBasic`、`NoBattleBasic`、`Skill`、`Equip`、`EquipResonance`、`Treasure`、`TreasureSuit`、`Gem`、`Cooking`、`Extra`、`NpcFriendshipLevel`、`Pet`、`LostGoddess`、`PeriodIcCar`、`Legion`、`EquipCabinet`、`Astrological`、`Appearance`、`NpcTalentTree`。`NoBattleBasic` 已从 `PlayerLevelPropId`／`PlayerSubRankPropId` 生成，只合入非战斗属性，不参与战力或人物明细；`CharacterProgressionValidation.ValidatePropertyPipeline` 校验其与固定目录一致。其余来源按所属功能已确认的单机保留／关闭范围处理输入和有效条件；`GmProp=100` 为原调试分块，单独标识。计算与展示保留原合并顺序、百分比、转换、倍率、独立截断及评分引用数据，人物属性变化须使主页、明细、装备比较、技能详情和实战共同更新。

## 原始官方实现

原人物面板汇总角色经验、等级／阶位、属性、战力、装备、技能、职业与衣柜；数值取官方属性来源和计算链，显示遵循原面板字段、格式与原模型配置。

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

### 待迁移的规则与表现

原人物页、综合／细分属性、位阶卡和属性说明按原控件绑定。来源分组独立展开与滚动，属性说明只读，不修改存档。

经验完整接入原 `PlayerLevelText`、`PlayerExpSlider` 和 `Slider` 绑定：普通经验显示当前级进度；满阶显示橙色储存经验及下一阶整段需求，原说明按钮打开储存规则；赛季赐福显示实际赐福等级、当前级经验，满赐福时显示下一赛季限制。领取经验成功保存后按原组件时长播放等级与进度插值，失败交易不播放，重新读档保持实际经验。

基础属性读取 `PlayerLevelBattlePropId`、`PlayerSubRankBattlePropId`、`PlayerFightRolePropGroupId`。1级生命／攻击／防御／速度为300／60／60／48；职业、装备、技能、幻兽、古遗物、公会和星命按原顺序合并，倍率、百分比及转换逐步截断。

战力采用原 `PlayerPropContext` 分块公式，装备词条、套装、技能槽、幻兽、遗物等分别计分；属性与评分供实际战斗读取。细分来源依原独立截断，不用虚构差额补足总值。

10级返回家园触发原位阶教学，阅读材料说明后一次领取白垩石片2个和罗拉2000；突破仍支付原费用，教学可恢复。人物突破不再等待服务器阶位，参见[离线开放条件](offline-access.md)。

装备穿戴、卸下及职业变化同步原 `AppearanceData.EquipSkinDict`；主副武器先取实际装备皮肤，衣柜装扮覆盖对应部件，卸下后恢复装备或基础皮肤；服装按原职业基础皮肤和衣柜选择显示。男女、职业条件、穿戴时间、双向／单向互斥、黑白名单、绑定皮肤与原 HSV 染色按原配置解析；旧外观迁入 `SkinMap`／`DyeMap`。状态经本地事务与形象共同保存，写盘失败整体回滚。

衣柜采用原 `AppearancePanel`、分类页签、装扮图标与选中状态、称号双列控件、属性和规则弹窗。支持主副武器、服装、面饰、背饰、发型、头饰、脸纹及称号的穿戴／卸下和可穿筛选，已佩戴称号显示于人物页与称号页。收藏经验按原品质、类别、限时排除与限时称号记录计算，衣柜等级属性及拥有称号属性纳入原属性来源12和战力。限时装扮按原规则向上取整显示剩余天／小时，称号显示剩余天／小时／分钟／秒；到期清除拥有状态并自动卸下。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`python -B scripts/prepare_unity_character_catalog.py --catalog-only` 从固定 APK `854_156383` 的 Luban 输入生成 Unity 人物成长目录。该目录包含 166 条 `legion_map_skill`、498 条 `legion_map_skill_study_level`、12 条 `legion_map_building`、468 条 `legion_map_building_level`、75 条 `legion_map_building_level_hall` 和 10 条 `legion_map_hangup`；输入 SHA256 与官方 `Tb*` 构造器来源见[人物来源清单](../../unity/Assets/Official/Character/source-manifest.json)。`--catalog-only` 只更新人物配置并保留现有 Sprite 输出。

追加转换固定 APK 中的 `CharacterPanelEquipComparePanel`、`CharacterPanelEquipListItem`、`ItemIconSkill`、`ItemIconSkillPiece`、`ItemIconEquip`、`ItemIconEmpty` 六个 prefab。重复同名的比较栏和按钮容器保留为独立路径，避免覆盖；按 `LanguageUtils.GetItemDetailQualitySmallBg` 提取七档 `Img{Quality}_PopUps_Small` 原图。新增 16 项运行时图片，当前共 1,345 项资源、1,059 项图标映射、75 个 prefab、77 张完整配置表。所有图片均为 APK 原始 Sprite 解码与裁切，清单和显式加载同步更新。

头像使用原始 `ImgBg_MainHp` / `ImgLoad_MainHp`：原图为 152×133 的圆弧裁切，圆心在 (76,76)，底部缺口属于原图。修正 uGUI Image 的径向填充字段读取，使用实际升级进度。技能和装备图标按原始 Mask 的 Sprite alpha 裁切后代图像，即使 showMaskGraphic 关闭也保留遮罩作用。人物面板按 SkeletonGraphic 的原始骨骼单位与底部轴心显示，保留 prefab 的缩放，避免按布局矩形再次缩小。

核对 `CharacterPanelPropPanel` / `CharacterPanelEquipItem` 客户端代码与 prefab 后，移除此前本地额外添加的人物页行囊、祝福、启程补给按钮；这不构成对线上最新版本 UI 的确认。装备槽接入原始按部位筛选、单栏穿戴及双栏比较弹窗，详情属性与操作继续绑定现有离线规则，未实现的原始装备系统不以示例字段显示。

`python scripts/prepare_character_props.py` 校验固定 APK，提取 `854_156383` 的 `Public/PlayerPropItem1`、`CharacterPanel/PropDialogDetailedPropItem`、`Tips/PopTip/PopTipEntry` 和 `Item/EntryDetailItem`，接入现有 `Character/CharacterDetailPropsDialog`。属性名称、来源分类和说明来自同版本官方中文语言分包及 `prop_cfg / entry` 原表；新增图像均为原始 Sprite 解码，没有自行绘制。

综合页使用原始 Main / Addition 分类、零值隐藏、关联数值和点击说明；细分页使用原始来源排序、独立折叠卡片和箭头动画。属性及来源汇总使用本地战斗规则；旧档累计加成无来源台账时显示为“其它加成”，尚无本地原版分母的同等级估算行不显示虚构比例。公会加成单独归入公会分组。此变更不代表完整原版战斗公式已经移植。

依据：`assets/reference/character-properties/source-manifest.json`。

同日背景复核：重新读取原始 `Character/CharacterDetailPropsDialog`，面板背景 `Image` 和两层 `CanvasGroup` 的 alpha 均为 1，`ImgBg_PopUps` 的浅灰主体像素 alpha 为 255，透明通道用于外缘。`BgDialog1/BgImg/BgTitle` 的 `MirrorImage` 组件原本禁用，此次停止绘制该装饰。接回 `PopupWindow` 的 `BlurMask / Dialog / close_by_mask` 设置及 `UIDialog_Show / UIDialog_Hide` 原曲线；共核对五个 prefab、50 项 Sprite 记录。

背景蒙层来自已校验同版本 CDN 的 `defaultpackage_assets_prefabs_ui_main.bundle`（MD5 `5ce6cf884ed57ba5d3aeaf1222681726`），对象 `/MainCanvas/Content/Popup/BlurMask` 的原始 `RawImage` 颜色为 `(124/255,124/255,124/255,1)`。蒙层下方保留原始人物页并隔离指针；它使用原始四级缩小层数和颜色，未逐像素移植 Unity 的多遍滤波与 1/13 渲染目标。

`prepare_fidelity_skill_rules.py` 新增原人物等级、位阶、战斗角色组和非战斗属性类，总计 108 类。、 按原属性处理与分块评分接入；

`prepare_fidelity_skill_ui.py` 从固定 854_156383 的 promotepanel、skill、itemdetail、item、character、commonnotify、public 包转换 17 个原 prefab／模板，严格读取原序列化对象字段，含技能碎片详情、合成确认、数量进度、普通技能详情及批量升阶通知模板；两张原技能槽位费用表共 1,270 行，原 SkillItemRangeInfos.g.bytes 严格按 MessagePack 索引读取 1,365 份作用范围。输入版本、CAB／对象、原始数据、源码和输出哈希见 [skill-cultivation/source-manifest.json](../../assets/reference/skill-cultivation/source-manifest.json)。固定 `item` 表漏掉的 18 个碎片 ID 由 `Formula.GetPieceIdBySkillId` 反推对应技能；同一配置中其余 190 条技能碎片行的名称后缀、RawIcon、ItemType、Rank、Quality 及所有展示相关字段取值一致，运行时只对这类技能 ID 使用该一致映射生成缺失元数据，不将其记录为原始缺失行或服务器返回值。槽位升级音效沿原 CharacterPanelSkillItem.LevelUpSound 的 WwiseObjectReference、Event、Play、Sound 与媒体链转换，输出 `assets/official/skill-audio/473101969.ogg` SHA256 `421c23910fd77aaf8a184f3e2fc439b80df147e195eb3ec41c798d36b658773e`；Unity 播放载荷为 `unity/Assets/Official/Skills/Audio/Skill_Upgrade.ogg`，引用于角色资源目录。原 GlobalColors 品质及进度颜色配置另存于运行数据；炫彩渐变的边界在技能功能文档维护；ArrowLoop 的两条空绑定已核对原 prefab 全部 Transform，原层级本就没有对应目标，没有新增自绘素材。

技能整屏图标绑定：`scripts/prepare_unity_skill_icons.py` 核对 APK 与已下载官方缓存后，从固定 `854_156383` 的 `defaultpackage_assets_prefabs_ui_item.bundle` 提取 `item_empty`、`SkillStarInfo` 和 `StarMiniInfo` 序列化字段。保留原灰色加号精灵、数字星阈值 4 和七组品质星图映射；新增图像为 `ImgItemEmpty-725d8512ac.png`，技能界面目录共 140 张精灵。原始组件与 bundle 哈希、输入 UI／精灵和输出哈希见 [unity-icon-source-manifest.json](../../assets/reference/skill-ui/unity-icon-source-manifest.json)。`prepare_unity_skill_runtime.py` 将原 `skill_rank` 的 Rank／Quality／RankAddition／Score 接入展示，完整页恢复 `CharacterPanelSkillPanel` prefab 与人物页 `CharacterPanel_Skill`／`CharacterPanel_SkillSwitchmain` 动画，九宫格按原 Sprite border 绑定。实际窗口与发布验证范围见[技能](skills.md)。 顶栏另复用官方共享 HUD 原生 prefab，补入 42200 货币及头像／战力图像，并保留原自定义布局和胶囊裁剪；同一生成脚本核对 Map11 UI 载荷哈希及其来源清单，再读取固定 APK 的 `TabItemTopBarSwitcher`、头像 RawImage UV、AspectRatioFitter、裁剪组件和 CharacterPanel 状态绑定，写入 `shared_header` 与来源凭据。人物预览使用原骨骼坐标到 UI 的映射，不按服装包围盒缩放。 恢复 `AttachmentFollower` 的 `shadow` 骨骼绑定、原 TMP 自动缩放／换行参数、两类馈赠组件与原奖励图标；文案从固定 APK 内的 `assets/yoo/WwiseBnk/9a0b6ab8c5136050fd005ead8f567863.rawfile` 直接读取并核对固定来源哈希，避免误用已下载缓存中的 871 语言文件。馈赠配置保留 Group 33、Group 9 的完整原记录及循环参数；进度与领取规则入口为 `OfficialSkillAchievementRules.cs`、`OfflineCharacterProgressionService.ClaimSkillAchievements` 和 `OfficialSkillGiftPanel.cs`。 同一固定 APK 新增 `SystemMsgPanel` 原 prefab、镜像图像 `ImgBg_Banner`、系统消息原文案和动画轨道，提取入口为 `prepare_skill_system_messages.py`；按原对象 path ID 与 bundle 哈希匹配图像，未混用同名精灵。同步提取人物页、HUD 与窗口 SafeArea 参数、人物 TMP 字体绑定、WorldCanvas 原 WindowMask／WindowBlurMask 引用、RawImage 原 UV（[0,0,1,1]）、输入字节哈希和 PopupWindow 遮罩字段。Windows 顶部参考留白是桌面适配，范围与验证记录写回技能功能页。

复用固定 APK manifest `854_156383` 已转换的 `PlayerUpgrade` 剧情图、两组五句对白、原角色／突破面板、`TalkDialog`、固定奖励 24 和本地教学语音。不会把位阶教学视为职业转职。现有 [剧情来源](../../assets/reference/official-map/story-source-manifest.json)、[地图 UI 来源](../../assets/reference/official-map/ui-source-manifest.json)、[音频来源](../../assets/reference/official-map/audio-source-manifest.json) 与角色面板来源清单保留其真实输入与对象，未新增图像或语音文件。

`python scripts/prepare_map_localization.py --task-growth` 按已登记且校验 SHA256 的原版中文 rawfile 增量提取 `condition_SubRank_desc`、`jump_to_home_tip0` 和 `Common.SubRank` 枚举名称，追加到 `assets/official/map-localization.json`；输入 URL／版本／哈希与新增键、输出哈希见 [official-map/localization-source-manifest.json](../../assets/reference/official-map/localization-source-manifest.json)，枚举源文件 SHA256 另行登记。位阶目标沿用原译名；累计祈愿目标的中文句式及实时计数由本地控件生成，不冒充原本地化字符串。

固定版本854_156383的职业和技能迁移复用原ProfessionSettings、CharacterPanelProfessionPanel、ProfessionPanel、ProfessionPromotionPanel、ProfessionChangePanel、ProfessionChangeNotifyDialog、ProfessionSkillUnlockNotifyDialog及其独立属性／条件行模板。真实引用、输入哈希、中文语言和TMP／ClampingScrollRect／LoopScrollGridLayoutGroup字段见 profession-ui/source-manifest.json；技能自适应行、分隔行与碎片单元直接沿序列化PPtr提取，见 skill-ui/source-manifest.json。技能培养增强转换保留这些绑定。所有新增PNG均为原Texture2D／Sprite解码，没有绘制替代。

导师跳转读取原TutorEntityTag；NativeDialog职业动作与OpenProfessionPromotionWinNode改为等待真实窗口，不再打开即执行转职。晋升使用原前置职业、分离的总职业等级／职业等级条件及导师区域Public限制。通知使用原属性变化和技能说明组件；技能方案名称按原CJK.GetByteCount、CN上限和非法字符规则校验。以上C#输入哈希分别保存在两份模块来源清单；本地事务替代网络请求。职业服装与主副武器模型、地图晋升Playable不属于本条界面转换的完成范围，继续单独追踪。

`scripts/prepare_character_overview.py` 从固定 APK `854_156383` 的 CharacterPanel 读取 8 类原组件字段（含原 Slider 绑定），沿 PropPrefab PPtr 转换 CharacterPanelProp 行；原 Rank／SubRankSubLevel 语言、经验说明、赛季名称、player_subrank 图标及 EnumExt 分组生成 `assets/official/character-overview.json`。15 个实际 Sprite 和所有输入／输出 SHA256 登记于 `assets/reference/character-overview-source-manifest.json`；图标由原 Texture2D／Sprite 裁切，没有绘制替代。

Unity 人物运行目录由 `scripts/prepare_unity_character_catalog.py` 从上述人物/属性目录、固定版官方规则表、`role_prop_group.g.bytes`、`item_time_limit.g.bytes` 及衣柜资源清单生成。人物概览、属性、等级/位阶/赐福表、限时装扮表和原衣柜/属性图标 Sprite 保留固定版输入哈希；Unity 输出及每张图片的输入 SHA256 记录于 `unity/Assets/Official/Character/source-manifest.json`。角色外观、称号和衣柜 UI 数据仍由 `assets/characters/wardrobe/` 与 `Assets/Official/CharacterCreation/` 的来源清单分别追溯，Unity 载荷须随本模块资源快照恢复。

`prepare_character_appearance.py` 从固定 APK／匹配原 CDN（854_156383）迁移1723条 spine_skin、1567条 append_appearance、男女职业集合、品质经验、衣柜属性及原 UI；运行数据位于 `assets/characters/wardrobe/`。`source-manifest.json`、`rules-source-manifest.json`、`ui-source-manifest.json`、`effects-source-manifest.json` 分别保存部件／图集、配置／代码、原控件及原粒子的真实输入输出哈希，均位于 `assets/reference/character-appearance/`。`ui-source-manifest.json` 另记录 `HonorTitleItem_39602` 的 `huoyan (3)` 原生 MeshRenderer 未绑定 Mesh；

`prepare_character_skill_appearance_ui.py` 按固定 APK manifest `854_156383` 转换技能幻彩页使用的 `AppearanceSkillContent.prefab`、`SkillSkinGridItem.prefab`、`ItemIconSkill.prefab` 及其原 Sprite，来源分别为 `defaultpackage_assets_prefabs_ui_appendappearance.bundle`、`defaultpackage_assets_prefabs_ui_skill.bundle`、`defaultpackage_assets_prefabs_ui_item.bundle`。附件配置 `ItemIconAttachmentConfig` 的 Type 12／4 原挂件一并解析。导出 36 条技能幻彩选项和 30 张原图至 `unity/Assets/Game/Resources/Character/skill-appearance-ui.json`；原对象、配置、输入输出哈希见 [skill-appearance-ui.json](../../assets/reference/character-appearance/skill-appearance-ui.json)，Unity 运行时由 `OfficialSkillAppearancePanelView` 显式读取。

原男女骨骼按24 fps转换男13种／女14种 UI／地图姿态及独立压缩部件，完整分辨率 atlas、colormap、绑定皮肤、主副武器／背饰 follower 和正背面切换来自原包。32种独立装扮特效使用原 PlayerEffect/UI 与 Renderer prefab、UIParticle 缩放、ParticleSystem 参数及骨骼／插槽／附件跟随；4种有 HasEffect 标记的主武器在集成 prefab 内使用 Spine，检查无独立 ParticleSystem。没有绘制替代素材。

人物页 profile idle 运行载荷由 `scripts/prepare_character_model_runtime.py` 按男性/女性职业1/10所需的四种原始24 fps待机姿态、网格和贴图构建至 `unity/Assets/StreamingAssets/Character/character-models.zip`。包内 catalog、姿态、网格和贴图运行数据共 7,566 个文件；ZIP SHA256、生成器哈希、逐项输入哈希及 URI 同哈希副本映射记于 `assets/reference/character-appearance/source-manifest.json` 的 `runtime_pack`；五份关联人物来源清单也逐份登记哈希。Unity 运行时校验包内 SHA256，不直接加载 Android AssetBundle。

## 完整还原部分

人物程序集 [`Zjcs.Presentation.Character.asmdef`](../../unity/Assets/Game/Runtime/Presentation/Character/Zjcs.Presentation.Character.asmdef) 显式引用 `Zjcs.Offline.Skills`。Unity `2022.3.57f1c2` 窗口 Editor 的 `r36` 编译与脚本 Play Mode 通过人物属性来源交易、幻兽和人物模型、官方页面节点、技能／装备／衣柜服务流程与存档断言。`r37` 导入最近的预览、占位图及 TMP 字体修正后触发程序集重载，日志停在重新构建人物页；这轮没有新的通过记录。`scripts/check_unity_project.py` 静态稳定 GUID 检查通过（39,579 个 GUID），`Zjcs.Editor.ProjectTools.Validate` 已通过。面板占位底图现按原无绘制行为透明，最新可见检查仍需覆盖模型比例、完整排版与遮挡；真实鼠标输入、存档重启及 Windows Player 尚未验证。

Unity 已接入固定版本 `854_156383` 人物成长目录、人物概览与属性文本、限时外观配置、原衣柜规则/物品/UI 数据及其引用图片。生成入口为 [prepare_unity_character_catalog.py](../../scripts/prepare_unity_character_catalog.py)，输出与官方输入哈希登记于 `unity/Assets/Official/Character/source-manifest.json`；`OfficialCharacterAssetCatalog.FindSprite` 按原资源 URI 查找人物与衣柜 Sprite，编辑器绑定及来源核对入口为 [CharacterProgressionValidation](../../unity/Assets/Game/Editor/Character/CharacterProgressionValidation.cs) 的 `BindOfficialCatalogs`。

[OfficialCharacterPanelView](../../unity/Assets/Game/Runtime/Presentation/Character/OfficialCharacterPanelView.cs) 按已恢复的人物、属性和位阶面板层级构建 Unity UI，并绑定等级/经验、位阶材料操作、属性弹窗和官方 `AppearanceBtn`；[CharacterFeatureRuntime.OpenProfile](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterFeatureRuntime.cs) 从人物存档槽打开页面。[CharacterCreationFlowController](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 在新建或选中已有角色后提供“人物”入口，关闭人物页返回当前存档流程。

人物属性管线按固定版 `PlayerPropContext`／`PropChunkFactory` 的来源块实现：[CharacterPropCalculator](../../unity/Assets/Game/Runtime/Offline/Progression/CharacterPropCalculator.cs) 构建战斗基础、非战斗基础、职业、装备、装备共鸣、宝石、技能、幻兽、古遗物及配置驱动来源，并按原 `HandleBaseProps`、`HandlePercentProps`、`HandleScaleProps` 顺序处理战斗外属性；非战斗基础由 `OfficialCharacterAssetCatalog.CalcCharacterNoBattleProperties` 读取固定版 `PlayerLevelPropId` 和 `PlayerSubRankPropId`，只进入 `NonBattleProps`。[OfflineCharacterProgressionService](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs) 从人物存档读取料理、额外、NPC 好感／天赋、失落女神、周期卡、公会、装备图鉴和星命等来源；NPC 天赋保存 `TalentExp`，按官方品质换算礼物经验／点数，并对最大好感条件、节点图前置、桥接点和点数消费执行本地事务。固定 APK 的 11 条离线开放主线均没有 `NpcFriendshipReward`；2,884 条带固定好感数值的委托记录属于关闭内容。NPC 礼物本地事务按 `NpcGiftInfo.Friendship × NPC 偏好系数` 计算并消费物品；目前仅可给已会面 NPC 送礼，人物验收覆盖了事务，但首次会面规则和官方 NPC 礼物交互入口仍待接入。[OfficialMainlineTaskCatalog](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialMainlineTaskCatalog.cs) 携带固定版 10,000 条 NPC 好感等级门槛。商城提交由 [OfflineCommerceSession.PersistState](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceSession.cs) 在同一人物存档事务中按周期卡到期日同步 `CharacterProperties.PeriodicCards`；人物属性源继续依据官方 `periodic_card_pay.ExtraProps` 与有效期计算。装备、共鸣、宝石分别注册为 `Equip`、`EquipResonance`、`Gem`，避免共鸣／宝石重复计入装备块。战力使用 `CalcCombatRatingCommon` 的六组、`ScoreBase` 递归和 `CommonCombatRatingRefData`；[OfficialPropCatalog](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialPropCatalog.cs) 解析 352 条 `prop_cfg`、显示名／说明／图标、分类、单位本地化与相对属性，[OfficialPropFormat](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialPropFormat.cs) 复刻原 `FormatPropString` 的万／亿与百分比格式。生成脚本在 `character-progression.json` 中新增 `prop_units`、`prop_categories`、NPC 关系／条件／礼物表和从固定 APK 提取的地图女神／NPC 天赋表，并在主线任务目录加入好感门槛表，还从原预制体提取属性弹窗的 `PropCatalogCfgList` 与细分分组绑定。

[CharacterSkillPanel](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.cs) 绑定原 `CharacterPanelSkillContent`：三套方案前后切换与改名（`OfflineSkillProgression.SelectPlan/RenamePlan`）、四个战技槽与四个秘法槽、槽位等级、原页签、`ShowAll` 开关、技能列表与滚动区。列表排除当前方案已安装技能，按品质／ID 排序；已拥有技能可安装、替换、卸下，同类槽位可用 [SkillSlotDrag](../../unity/Assets/Game/Runtime/Presentation/Character/SkillSlotDrag.cs) 鼠标拖拽换位，拖影跟随指针并在结束时校验目标槽类型。

[CharacterPetEquipmentView](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterPetEquipmentView.cs) 将原人物技能方案的幻兽选择绑定到本地成长存档：保留主页随行索引和每套技能方案索引，候选包含“无幻兽”，按本地战力、品质、类别和实例序号排序，并展示等级／成长属性、3 个技能位与官方原生幻兽模型。一次选择可将同一只幻兽分配给多个方案，这是已确认的离线规则。

[CharacterEquipSlots](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterEquipSlots.cs) 绑定原五槽 `CharacterPanelEquipItem`：主武器 10、副武器 14、头盔 11、衣服 12、靴子 13，显示已穿装备图标（品质底图、`item_icons` 或原缺失图标文本）、等级与副武器职业解锁（`profession_base.Rank > 1`）；空槽点击打开原 `CharacterPanelEquipListPanel`，候选按装备评分（原 `EquipItemComparer` 的 Score→Quality→ClassId 降序）列出本部位且职业可穿实例，点击经 `OfflineEquipmentOperations.Wear` 穿戴并同步 `Wardrobe.WornSkins` 的 body／pWeapon／sWeapon 皮肤。已穿槽的详情／比较／强化等操作已有接线，原控件与完整表现缺口见下文。

[OfflineEquipmentPropSource](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentPropSource.cs) 实现原 `EquipPropChunk`：按 `equip`／`equip_type` 的 `Main`／`Sec`／`Other` 属性类、强化等级（`SlotProgress.BaseLevel`）、祝福等级、位阶差与 `equip_suit` 套装阈值合并装备分块，并以逐件装备属性及套装属性计算装备分块战力；`OfflineEquipmentResonancePropSource` 单独实现原 `EquipResonance` 块，宝石由 `OfflineGemPropSource` 单独实现 `Gem` 块。装备属性使用 `equipment-runtime-rules.json` 的 `props` 等级表与 `equip_attributes*` 数据。

[CharacterRankPanel](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterRankPanel.cs) 绑定原 `CharacterUpgradePanel`：当前／下一位阶名称（`rank name`＋`sub`）、位阶星级（按 `player_subrank` 的同一大位阶分组，`GetState` 的已亮／下一枚／未亮）、当前等级与基础等级上限、下一位阶等级上限、`PropCatalog.Base` 基础属性前后值、`player_subrank.Items` 材料（原 `MapId`、持有／需求、品质底图与 `item_icons` 图标）、`LimitState` 的无限制／等级不足／满位阶状态和 `UseItemBtn` 突破。突破成功后刷新面板并保持同一实例；成功通知与 10 级教学已有复用属性弹窗框架的实现，原预制体和原触发条件仍未完整恢复。

[CharacterPropPanels](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterPropPanels.cs) 绑定人物页概要属性行与综合／细分属性弹窗：概要行读取原 `CharacterPanelPropPanel` 绑定（`PropCatalog=9`、`SlotPropItems` 与原 `row`／`row_fields` 模板），弹窗使用 `character_props` 原层级和 `PropCatalogCfgList`（主要属性 `Main`、附加属性 `Addition`），细分按原 `PropDialogDetailedPropItem` 分组、`mPropCatalogList=[Main,Addition]` 过滤，并支持逐组折叠／展开。[CharacterPanelNodeFactory](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterPanelNodeFactory.cs) 统一还原原布局组、`ContentSizeFitter`、`ScrollRect`、`RectMask2D`、Mask 与按钮；人物页和弹窗使用独立节点索引，关闭弹窗只销毁弹窗本身。

[OfficialWardrobePanelView](../../unity/Assets/Game/Runtime/Presentation/Character/OfficialWardrobePanelView.cs) 按 `wardrobe-ui.json` 的原控件树、原分类绑定和拥有类别规则筛选衣柜底栏；穿脱、称号、风尚经验、规则／属性弹窗及倒计时经人物进度服务处理。底栏技能幻彩页由 [OfficialSkillAppearancePanelView](../../unity/Assets/Game/Runtime/Presentation/Character/OfficialSkillAppearancePanelView.cs) 使用固定 APK 的原选项列表、品质框、图标和视频控件绑定技能幻彩；家园装潢沿原模块入口切页。衣柜子类选择从原 prefab 读取 `AppendAppearanceContent.mAnimExitTime` 与 `mAnimMixDuration`，调用人物原生 Animator 的 `show_*` 过渡、`appearance` 循环和离开复位。到期物品在衣柜打开期间同步并从人物状态卸下。

[CharacterModelPreview](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterModelPreview.cs) 在人物存档槽路径使用 [CharacterCreationNativeModelFactory](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterCreationNativeModelFactory.cs) 创建官方原生模型与 Animator，按官方 26 个已转换动作驱动待机、衣柜类别过渡及循环；无人物槽的预览回退才使用网格帧包。运行包由 [prepare_character_model_runtime.py](../../scripts/prepare_character_model_runtime.py) 按四种待机姿态、网格和贴图生成至 `unity/Assets/StreamingAssets/Character/character-models.zip`；包内每个载荷有 SHA256 校验，外部来源清单记录包与输入哈希。模型按已保存衣柜／装备皮肤解析换装，不依赖 Spine runtime。

既有 `CharacterProgressionValidation.BindOfficialCatalogs`／`Run` 记录验证过 1,563 个可穿戴外观项、1,723 个皮肤定义、352 条 prop_cfg、属性分组行数、基础 300/60/60/48、职业 1 的战斗外生命 327、人物基础细分 60 攻击及万／百分比格式。人物属性源和装备分块随后又有变更，受全工程编译阻断，以上断言尚未重跑，不能视为当前版本验证通过。

人物六项目前均已有代码接线和本地状态：原生 Animator／分类动作、官方分块人物属性、五槽装备、技能安装与拖拽、随行／方案幻兽装备、衣柜与技能幻彩页。衣柜模型动作参数来自固定版 prefab。窗口 Editor 的 `r43` 曾运行至 `interactive-ready`，静态源码日志显示 154 个 Image 绑定、缺失 0；但用户实际操作后确认整体排版与原版不符，幻兽弹层也没有可见关闭控件。该截图和绑定数量不能作为视觉通过证据；官方独立 PetPanel 路由、真实输入、动画材质、六项完整流程、共享调用方数值及存档重启恢复均待修正和复验。

## 自定义实现

人物页与衣柜中的角色显示沿用[Spine 角色动画迁移决策](../asset-management.md#spine-角色动画迁移决策)；当前 profile idle 已接通免费原生帧网格路线，衣柜换装从人物状态刷新模型。HSV 数据、遮罩与材质已有接线，实际染色效果仍未完成画面验证。

用户补证普通经验在当季基础上限后继续提升显示等级，超出部分为当季赐福，赛末回到当季基础上限。服务端经验路由未取得，本地按此处理并保留赛季记录。

限时装扮首次有效期和到期卸下依据原客户端；重复获得的服务端续期未取得。用户已确认按配置 `CanOverlapTime` 叠加 `Duration`，并将剩余有效期限制在 `MaxDuration` 内；不允许叠加时按配置重设时长。此续期规则为已确认的离线实现。

### 已确认的本地接线与边界

原战斗 Spine 包保留全部迁移皮肤与所需 follower，`prepare_battle_spine.py` 生成每种皮肤依赖页面；运行时仅载入当前组合使用的图集页。所有动态纹理、JSON 及 mesh.gz 已纳入显式资源绑定和 Windows 分包清单。限时装扮重复获得的续期按用户确认的配置叠加与上限处理，差异在[人物与衣柜](character.md)记录。

## 尚未实现

- 储存经验说明已接原 `player_exp_max_title/desc1/desc2` 文本并在人物页规则按钮打开说明层；仍缺经验领取成功后的奖励演出与升级延时衔接、位阶教学触发/恢复和返回家园条件，失败与保存结果也只在状态文本体现。
- `OfflineCharacterPropertyProgress` 保存料理、遗物、NPC、周期卡、公会、装备图鉴和星命等输入；料理消耗已按实际数量累计官方 `UseTimes`，NPC 礼物事务按官方偏好公式扣除物品并更新关系，会面事务按官方条件扣门槛道具并初始化好感。新档保存全部 12 座建筑等级，Hall／Library 为 1 级，其余为 0 级；Hall rank、建筑 BuildCondition、等级条件、材料及 HangUp 进度已接入 `OfflineLegionBuildingProgressionService`。建筑和人物隔离存档断言已加入，尚未由 Unity Editor 执行。默认建筑 5/6/7 在 Hall 1 级时可施工。新宝具按用户确认初始化为有效、未入槽、0 星，并按官方效果类型设初始命中数；宝具槽培养入口仍未实现。装备图鉴现按用户确认在本季穿戴和卸下时自动同步评分快照，使用官方装备／星石评分表及分数门槛；Map10 与家园读取同一人物属性计算器，Editor 运行验证未完成。
- 核对帧网格回退模型的遮罩／HSV 表现、原染色入口、互斥提示及称号挂点；衣柜底栏技能幻彩已接固定版原 UI，家园装潢切页已调用所属模块入口，但两者仍需实际运行验证。
- 官方人物幻兽按钮应打开 `PetPanel`；当前为使人物页随行装配可访问而打开官方随行候选弹层，完整 PetPanel 路由及其宠物列表／编队／成就页仍未迁移，见[幻兽](pets.md)。
- 修正人物页对原版的视觉差异，恢复官方幻兽管理页入口和选择弹层的可见关闭交互；在 Unity Play Mode 中重新验证人物与衣柜完整流程、不同性别／职业模型、穿脱、真实拖拽和读档恢复。`r43` 用户实测未通过，不能据此认定页面或幻兽弹层已验收。

### 人物入口、窗口与共享表现

- [x] 创角后“人物”入口（`CharacterCreationFlowController`）与主城头像入口（`HomeHudView`）均以当前存档槽打开 `CharacterFeatureRuntime.OpenProfile`；主城使用自身 `SavesRoot` 重载。
- [ ] `profession` 跳转事件仍无接收方；幻兽按钮代码已打开人物随行候选选择器，真实输入待验，官方独立 `PetPanel` 路由仍未接入；技能按钮已直接打开技能页。
- [x] 人物页职业按钮已绑定 `profession_name_{enum}` 本地化名称与 `ProfessionBase.Icon2` 图标（31 个职业有名称、31 个有图标），称号与有效期、各槽位状态已分别接入。
- [ ] 按原 Function 与所属单机开放规则控制入口可见、可点、提示和动态刷新。
- [x] 人物 UI 绑定工程内官方中文字体 `Assets/Official/Commerce/Art/fonts/FZY4K.ttf`（目录 `uiFont` 字段），不再依赖已失效的内置字体；字体应用失败时记录一次警告并跳过文本，避免整页崩溃。
- [ ] 在已恢复的布局组、`ContentSizeFitter`、`ScrollRect`、`RectMask2D` 和属性行动态生成基础上，补齐 TMP 文本组件、材质、字体图集、状态动画与多语言排版，并逐项对照原预制体。
- [x] 生命周期已补：`OnDestroy` 停止经验协程、关闭弹窗与子页、销毁衣柜覆盖层；`CharacterFeatureRuntime` 订阅 `Application.lowMemory` 调用原 `OnLowMemory` 回收未显示子页；弹窗使用独立节点索引，`CloseDialog` 只销毁弹窗。待实际画面回归确认。

### 人物模型、外观同步与染色

- [x] 有人物存档槽时从官方 native model prefab 创建 Animator，衣柜分类从原 `AppendAppearanceContent` 绑定读取 `show_*` 输入、退出时机和 mix，过渡后进入 `appearance` 循环并在离开时恢复默认姿态；实际模型比例和多职业组合待 Play Mode 核验。
- [ ] 按各职业基础服装、六类武器及主副挂点恢复模型组合；与官方父层变换、尺寸、原比例和位置逐项核验，检查男女及各职业链、空武器、副武器和互斥后外观。
- [x] 装备穿戴／卸下会把 `Equipment.Equipped` 的 `AppendAppearanceItemId` 同步到 `Wardrobe.WornSkins`，卸下时移除该类型并回退基础皮肤；幻装仍由 `WornItems` 覆盖。
- [ ] 职业切换与晋升结果的皮肤同步待职业模块；不兼容装备提示仍缺。
- [x] 人物模型已按官方 `CharacterCreationMeshFrameDye` 着色器与 `masks` 遮罩接入染色：按 `character_skin_dye`／`character_skin_color`／`Wardrobe.Dyes` 计算各皮肤类型的通道 HSV 偏移。
- [x] 从衣柜/外观编辑返回时会重刷模型、属性摘要、五槽、称号与职业显示（`CloseWardrobe`）。
- [x] 原 `PreviewAppearanceDialog` 已从固定包恢复并加入人物面板资源；衣柜卡片“试穿”按钮用临时 `WardrobeResolution` 打开官方预览框架与人物模型，batchmode Play Mode 已验证打开/关闭不写存档。
- [ ] 窗口态核验试穿模型、原动画/音频及取消/确认的真实鼠标交互；`PreviewAppearanceDialog` 的骨骼动画与礼包奖励绑定仍不完整。

### 属性、战力、等级与位阶

- [ ] 逐项验证 18 个官方属性分块、分块截断、评分与 Map10／家园实际读取；装备图鉴穿戴／卸下快照、公会技能逐级事务、建筑初始等级／门槛／HangUp 和 NPC 首次结识事务已有隔离存档断言，仍需 Editor 运行验证。人物 NPC 关系入口和 Hall／Library 建筑操作界面尚待接通；宝具槽位培养入口仍未实现。
- [x] 属性行详情已接：概要行与细分行可点击打开说明层，显示 `character-properties.json` 中的图标、名称与只读说明；说明层复用原 `CharacterDetailPropsDialog` 框架，原 `EntryDetailDialog` 预制体未提取。
- [x] 经验区已按原规则呈现：普通／储存经验使用 `CultureNumberArg.Compact` 等价的万／亿压缩格式，储存经验为 `#e17126` 橙色；满赐福显示赛季上限文案；说明按钮打开规则说明，滑条使用原 `mExpSliderPlayTime` 时长；`AwardExperience` 仅在写盘成功后播放升级动画。
- [ ] 位阶页已绑定当前／下一位阶、星级、等级限制、基础属性前后值、材料持有／需求、等级／满位阶状态和突破；成功通知已接：突破后弹出原 `UpgradeNotifyDialog` 语义的提示层（位阶前后名称与基础属性变化），确认关闭；原预制体未提取，提示层复用属性弹窗框架。缺料提示已显示在面板底部（逐项名称与持有/需求）；剩余：首个缺料图标定位、`PlayerSubRankRemainTime` 的位阶／赛季开放倒计时。离线按[离线开放条件](offline-access.md)保留人物等级与材料条件，不等待服务器位阶，因此 `RankUnlock`／倒计时状态当前不出现。`bindings.rank` 已提取全部字段可继续补齐。
- [x] 10 级位阶教学已接：打开位阶页且未领取时弹出教学说明（材料与奖励、只能领取一次），阅读即记 `RankTutorialSeen`，点击领取调用 `ClaimRankTutorial` 发放白垩石片×2与罗拉×2000，重复领取由服务拒绝。原教学预制体未提取，说明层复用属性弹窗框架。
- [ ] 核验经验跨位阶、跨等级和当季赐福边界及实际读档结果（Editor 校验已新增基于真实目录的“位阶上限存储经验/赐福等级”断言，待 Unity 运行）；补“返回家园触发/中断恢复”的原入口条件（当前为打开位阶页触发）。

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
- [x] 已接“同赛季完整继承”：详情“继承”列出同类型装备，确认后由 `Inherit` 模式 0 把来源词条/宝石完整转移到目标并消耗来源，目标保持穿戴；目标随后为锁定态。跨赛季自选词条继承的现有接线见本小节对应条目。
- [x] 词条精炼已接：详情“精炼”层可勾选要保留的词条，按保留数量读取 `equip_purify_cost` 扣除材料，其余词条经 `OfficialEquipmentGenerator.GenerateReplacement` 重掷并保存。
- [x] 跨赛季自选词条继承已接：继承层每个来源提供“完整／选词条”两种入口，选词条时按勾选数量读取 `equip_inherit_cost` 扣料，仅复制勾选词条、其余用 `GenerateReplacement` 重掷。
- [x] 装备碎片合成已接：详情“碎片合成”列出持有量足够的碎片，选择目标装备后按 `equip_piece_merge.PieceMergeCount` 扣碎片并经 `OfficialEquipmentGenerator` 生成实例加入装备列表。
- [ ] 卸下后的背包展示沿用背包模块。

### 技能安装与附属入口

- [x] `OfflineSkillProfile` 已并入统一人物存档（`OfflinePlayerProgress.Skills`），技能规则由人物目录绑定 `Assets/Official/Skills/skill-runtime-rules.json`；人物技能按钮打开原技能面板，安装／替换／卸下／方案切换与改名经 `ApplyCharacterMutation` 写入同一存档。
- [x] 恢复三套方案切换、方案改名、四战技／四秘法槽、目标槽高亮、列表安装替换与再次点击卸下、页签与 `ShowAll`；方案切换时同步显示对应方案的幻兽。
- [x] `SkillSlotDrag` 实现 `IBeginDragHandler/IDragHandler/IEndDragHandler`，从技能图标起拖、显示跟随指针的原图拖影、结束时恢复图标并按同类型槽位校验后调用换位服务；真实鼠标拖拽尚未 Play Mode 验收。
- [x] 安装遮罩按 Esc 会取消安装态并清除高亮，而不是关闭面板。
- [ ] 方案切换动画、占位恢复与其余操作中的返回限制仍缺；安装遮罩已接：列表点技能后进入安装态（半透明遮罩＋“选择技能槽”提示＋八个槽高亮），点击目标槽安装并退出安装态，空槽也可直接安装。
- [x] 技能列表已排除当前方案已装项、按品质／ID 排序，并对不满足职业链的技能显示灰色与“（职业不符）”；`IsAvailableFor` 与安装校验共用同一可用性规则。
- [x] 技能收藏已接：`OfflineSkillProfile.FavoriteSkills` 进入统一存档（旧档 `Normalize` 补空），列表行右上角星标可收藏／取消，收藏项排在列表前部；未拥有技能不能收藏。
- [x] 碎片页签已列已拥有技能碎片及数量；拥有对应碎片但未获得的技能在列表中标记“（可合成）”，`ShowAll` 关闭时也显示。
- [x] 碎片合成已接：详情层对未拥有的技能按官方 `skill_rank` 首阶 `Piece` 数量显示“合成”，首次点击提示再次确认，确认后扣除碎片、发放技能物品并写入已拥有技能。
- [ ] 新物品标记清除时机及原排序细节仍待补（合成规则见[技能](skills.md)）。
- [x] 已接批量培养：`FastLevelUpBtn` 调用 `UpgradeAllSlots`、`FastPromotionBtn` 调用 `PromoteAllSkills`，操作前把技能碎片与货币从背包同步到技能档案，成功后扣回背包并刷新槽位；每个技能槽右下角“升级”调用 `UpgradeSlot` 单槽培养；面板底部显示结果状态。
- [x] 单次升阶已接：列表行“培养”打开原 `SkillPromotePanel` 框架，显示当前／下一等阶与 `RankCosts` 材料持有/需求，确认经 `PromoteSkill` 扣料并刷新；批量升阶仍由 `FastPromotionBtn` 提供。
- [x] 技能详情层已接：列表行“详情”显示类型、当前/最大等阶、碎片持有与下一等阶材料，并提供安装／培养／收藏（或取消收藏）／关闭；技能描述字段未在固定目录中提取，详情暂不含描述。
- [x] 技能推荐已接：原 `RecommendBtn` 列出当前职业的推荐方案（名称与核心技能），应用时把已拥有且满足职业条件的技能装入对应槽位，未拥有项跳过并统计；推荐数据来自 `Assets/Official/Skills/skills.json`。
- [x] 共享槽祝福已接：`SkillContext.CurrentSeasonId` 沿用档案的位面阶位键，`SeasonRank` 保留位面阶位；`CharacterProperties.SeasonId` 另用 `astrological_season_config` 的赛季编号（阶位 4 对应赛季 1），不再在重载时将两者混写。培养上下文使用基础等级，排除总等级中的祝福部分。基础槽全部达到上限且 `PlayerRank == CurrentSeasonRank` 后，批量／单槽培养按 `SlotBlessCosts` 写入 `BlessRecords`；完整交互、战斗槽级与验证范围见[技能](skills.md)。
- [ ] 原结果动画／音效和品质文字仍缺；规则与已确认差异沿用[技能](skills.md)。
- [ ] 连接技能成就页签及其任务／领奖状态，切换时同步公共人物资料；接通伤害测试入口、开放判断、怪物／难度／回合／参数选择和实际战斗／结果，战斗规则由[共用战斗](battle-execution.md)维护。

### 幻兽装配与职业联动

- [x] 人物页随行区与技能方案出战区使用 `CharacterPetEquipmentView`，候选有卸下项并按本地幻兽战力、品质、类别和实例序号排列；选择和取消写入当前人物存档，显示已装备／空状态、等级、成长属性及官方模型。
- [x] 随行索引与三套方案索引分开保存；切换技能方案刷新对应幻兽，衣柜打开时隐藏随行模型并在返回时恢复。单只幻兽可同时出现在多个方案；Play Mode 的输入和存档恢复仍待验证。完整养成、四阵位按[幻兽](pets.md)落实。
- [ ] 接通职业总览／树／成就、立绘／底色／雷达、总职业等级／累计属性和帮助；职业培养、晋升、普通／自由转职完成后统一整理武器、技能方案、祈愿职业及人物模型，完整规则与单机差异按[职业](professions.md)执行。

### 衣柜、称号、潮流等级与子页

- [x] 衣柜卡片已显示品质名称与颜色、选中／已穿、不可穿（性别／职业／副武器条件不满足）与剩余时间；“卸下”首项与按品质／ID 排序保留。
- [x] 新获得标记已接：`Wardrobe.SeenItems` 记录已查看物品，未查看的外观／称号卡片显示“新”，点击卡片（穿戴或卸下）后清除并持久化；旧档 `Normalize` 补空。
- [x] 衣柜每秒检查物品数量与职业签名，变化时自动重建列表并刷新模型；穿脱与到期仍即时刷新。
- [ ] 原提示文案与原红点仍待补。
- [x] 恢复 `TryGetConflictTip` 语义：`OfflineWardrobeRules.PreviewWear` 对穿戴前后的有效皮肤集合各解析一次，返回会被遮挡的部件或互斥环类型；衣柜列表穿戴时先提示再继续原穿戴请求。部件名称取原 `ui_append_appearance_type_*` 本地化，原冲突提示模板 `ui_append_appearance_conflict_tip` 未在固定目录中提取，当前以部件名拼接提示。
- [x] 人物页称号已接 `HonorTitleItemProvider/Slot` 与 `HonorTitleTimeLimitContent`：显示已佩戴称号图标、剩余天／小时／分钟／秒并在到期时隐藏，`TitlePropContent` 读取 `AppendAppearanceInfo.EffectProp`（当前固定目录中称号 `EffectProp` 均为空，故属性行暂不显示）。
- [x] 称号列表按双列渲染，卡片与人物页称号图标均显示剩余时间；`SynchronizeExpiredItems` 到期移除限时物品、清空已穿称号并刷新拥有/穿戴状态。
- [x] 称号详情已接：点击称号卡打开详情层（品质、剩余有效期、称号属性）并可穿戴；原 `HonorTitleItemDetailDialog` 预制体未提取，详情复用衣柜属性弹窗框架。
- [x] 首次收藏经验已接：外观/称号按官方品质经验累加；限时称号首次获得写入 `LimitedTitleCollection`，到期后仍计入收藏经验。
- [ ] 补齐物品详情及任务称号“领取并立即穿戴”选择与奖励事务，任务规则引用[成就](achievements.md)及[任务](tasks.md)。
- [x] 潮流等级升级已按原规则实现：`RefreshWardrobeProgress` 只在经验对应等级低于已存等级时回落（`SyncAppearanceLevelFromExp`），`UpgradeBtn` 使用 `CanUpgradeAppearanceLevel → UpgradeAppearanceLevel` 跨级提升，满级／经验不足给出状态提示；头部显示当前等级、本级经验／所需经验与滑条填充，满级显示上限状态。
- [x] 属性与规则弹窗改用 `wardrobe-ui.json` 的原 `wardrobe_props`／`wardrobe_prop_row`／`wardrobe_rules`／`wardrobe_rule_row`／`wardrobe_rule_value` 模板：属性按 `appearance_prop` 逐级列出至当前级 +20，显示等级、属性名称与数值、已激活状态；规则按 4 组 `fashion_exp_group` 逐组列出品质与收藏经验；跨级升级后弹出新增属性行。原 `AperaanceUpgradePropDialog` 预制体未提取，跨级弹窗复用属性弹窗模板。
- [ ] 原潮流等级「属性」弹窗使用 `AppearancePropDialog` 的循环列表与 `AppearancePropLevelItem`（内含 `PlayerPropItem`），当前以同一模板的静态行与文本实现；待实际画面核对行高、滚动定位（原 `GetCenterIndex`）和已激活渐变。
- [x] 衣柜底栏读取原 `AppendAppearanceBottomTab`／`AppearanceBottomTab.mItemTypes` 并按已拥有类别控制显隐；技能幻彩使用原 prefab 控件、36 条官方选项、30 张原图、视频预览及拥有技能校验，穿戴／卸下写入人物存档。家园装潢沿所属入口切页，具体分类、事务与预览按[主城与家园](city-and-home.md)维护。
- [x] 角色幻装、称号、技能幻彩和家园装潢的收藏经验、限时排除、首次限时称号记录和等级属性由衣柜进度写入统一存档；各原生动画和属性行仍待画面验收。

### 保存、资源与完成验证

人物验收 `CharacterPlayModeAcceptance.Run` 通过 `CharacterAcceptancePreparation.EnsureModels` 准备角色与幻兽：首次完整烘焙并验证后写入 Library 内容凭据；后续输入、转换代码、元数据、输出及编辑器／目标平台不变时，校验绑定和26个必要人物动作后跳过重复烘焙。角色与幻兽独立失效；内容变化、缺失输出、校验失败或显式强制重建均不能复用旧凭据。菜单 `Zjcs/Validate/Check Prepared Character Models` 仅检查当前绑定，不建立缓存、也不代替实际玩法验收。缓存回归入口为 `scripts/test_unity_acceptance_cache.ps1`，覆盖同时间戳内容变更、增删文件、输出损坏、失败、强制重建及准备期间并发修改；缓存不会跳过本页要求的 Play Mode、真实输入、画面或存档检查。

缓存核心的上述回归已在独立 .NET 进程和隔离的同版本 Unity Editor 中通过；人物验收程序集使用实际 Unity 引用编译通过。主工程批处理在开场特效 `OpeningSpecialEffectRuntime.cs` 的 `PlayGroundEffect`／`log` 未定义错误处停止，尚未执行新绑定检查入口，亦未测得整轮验收或主工程缓存命中耗时；这些检查不构成人物玩法、输入或画面通过结论。

- [ ] 动态模型、动作、称号、字体／材质、图标、预览视频、家园预览、特效与音频均从随游戏交付的本地清单加载；按[素材管理](../asset-management.md)补齐官方来源、必要转换、依赖、哈希及显式绑定。
- [ ] 让穿脱、技能方案、幻兽方案、潮流升级、经验／位阶、职业变化和奖励使用统一事务；覆盖重复操作、取消、失败回滚、到期同步、旧状态校验、角色槽隔离及重新启动恢复。
- [ ] 静态 GUID 检查已通过（39,822 个 GUID）。窗口 Editor `r43` 进入 `interactive-ready` 后，用户实际点击幻兽入口确认弹层打开，但未发现可见关闭控件，并反馈整页与原版差异明显；因此真实输入与视觉验收未通过。完整返回流程、模型比例／遮挡／文字排版、滚轮／拖拽、连续动画、原音效和读档恢复仍待验；`Bootstrap.unity` 仍为空启动框架，不能证明 Windows Player 可进入人物页。
- [ ] 验证结果分别写明静态检查、Editor 导入／编译、Play Mode 逻辑、真实输入、画面／动画／音频及存档范围；本页全部流程和相关共享调用方实际验证完成后，才将人物界面标记为完成。

### 人物界面与官方调用关系的差异

人物界面尚未完整迁移。以下按固定版客户端源码、当前 Unity 运行时代码和实际载荷核对；“已接线”不代表已通过实际输入、画面或存档恢复验收。装备、技能和幻兽的完整规则范围分别见[装备](equipment.md)、[技能](skills.md)与[幻兽](pets.md)。

| 范围 | 官方调用关系 | 当前实现与缺口 |
| --- | --- | --- |
| 人物模型 | `CharacterPanel.RefreshAppearance`、`SwitchAnimCallBack`、`AppendAppearanceContent.PlayAnim/GetSwitchAnimData/GetLoopAnimData` | **模型和衣柜动作已接线，完整画面待验收。** 人物存档槽使用官方 native model prefab、Animator 和已转换 26 个动作；衣柜类别从原 binding 读取 `show_*` 输入、退出时间与 mix，接 `appearance` 循环及离开复位。装备／衣柜皮肤解析、HSV／遮罩与临时试穿已接入。预览挂在固定版 `MyPlayerModel_UI`（207×337、scale 1.2），刷新时先播放官方待机再按 renderer bounds 取景。不同性别／职业组合、装备挂点、实际换装效果及人物页遮挡仍需完整 Play Mode 画面验证；四姿态网格帧 ZIP 只用于无槽预览回退。 |
| 人物属性与战力 | `CharacterPanelPropPanel.RefreshProp/OnShowDetailPropBtnClick` → `BattlePropsOutFight`、`CharacterDetailPropsDialog.Init`；`PlayerPropContext`／`PropChunckFactory` 合并分块并评分 | **计算源已接线，交互与运行待验收。** 18 个属性分块接入统一本地计算器；NPC 送礼／会面、天赋、料理实际消耗、周期卡商城同步、失落女神地图状态、公会技能逐级升级、公会建筑 HangUp 和装备柜评分快照已有本地状态来源，穿戴／卸下会同步本季分数。11 条开放主线没有固定 NPC 好感奖励；会面按官方条件扣门槛道具并创建关系。新档建筑等级和施工条件按用户确认及官方表接入。NPC 关系与公会建筑操作入口、属性公式、评分、真实人物输入／画面与读档仍待验证。 |
| 人物装备 | `CharacterPanelPropPanel.RefreshEquip/UpdateProfessioon` → 五个 `CharacterPanelEquipItem.Refresh/OnBtnClick` → 候选、详情、比较、强化 | **操作已接线，原控件表现待验收。** 五槽、副武器解锁、候选排序、详情／比较、穿脱、强化、锁定、分解、继承、精炼、星石镶卸及碎片合成均调用本地事务，穿脱同步身体／武器皮肤；装备、共鸣、宝石分别计入对应属性源。星石数量／镶嵌操作已接，槽内原图标和红点、部分原词条专属控件及演出／音效、真实点击和画面尚未验证。 |
| 技能安装 | `CharacterPanel.OnCharacterPanelPropPanelSkillBtnClickEvent` → `CharacterPanelSkillPanel/CharacterPanelSkillContent`；`CharacterPanelSkillPlan` 管理方案，`SetPlan` 联动幻兽 | **安装与换位已接线，未验收。** 主存档中的三方案、四战技／四秘法槽、切换／改名、指定槽安装／替换／卸下、收藏、合成、推荐、培养、同类型鼠标拖拽均已接线；技能属性源随 `AttachSkillRules` 注册。技能成就／伤害测试属于技能页完整范围；原方案动画、描述文字、结果演出／音效和真实输入仍需验收。 |
| 幻兽装配 | `CharacterPanel.RefrshPet` → `PlayerWithPetContent` → `PetPlanContent.OnSelectPetClick` → `PetSelectPanel`；`PetEditPlanDefaultOperation.EquipPet` 加入／退出方案 | **本地装配已接线，未验收。** 页面区分人物随行幻兽与三套技能方案索引，候选包含卸下项并按本地成长战力排序，展示等级、成长属性、技能位和原生模型；选择写入人物存档，按用户确认允许同一实例存在于多个方案。模型挂点、筛选排序和重启恢复仍待 Play Mode 核验。 |
| 衣柜 | `AppearancePanel`、`AppendAppearanceContent/GridContent/GridItem`、称号及等级内容 | **衣柜与技能幻彩页已接线，未验收。** 原分类拥有条件控制底栏，衣柜穿脱／互斥／新获得／限时、称号和潮流等级已接入；技能幻彩使用原选项格、图标、品质框及视频控件，家园装潢沿原模块入口切页。衣柜模型按原 `show_*` 动画参数过渡。任务称号领取与立即穿戴、原提示／红点、循环列表定位、动画材质、真实输入和画面仍需验收。 |
| 职业与成长说明 | `CharacterPanelPropPanel.OnProfessionBtnClick`、原经验/位阶组件 | 职业按钮已显示官方职业名称与图标，事件会转发到家园；当前家园处理器只接装潢，职业页接收端仍缺。位阶页已接主要字段、突破、成功通知与教学，材料说明与首个缺料定位仍缺；储存经验说明已由规则按钮打开。 |

界面构建已改由 `CharacterPanelNodeFactory` 还原原布局组、`ContentSizeFitter`、`ScrollRect`、`RectMask2D`、Mask、按钮与文本样式，人物页与弹窗使用独立节点索引，`CloseDialog` 只销毁弹窗；`OnLowMemory` 已接未显示子页回收。没有官方 Image 的按钮按原 `NoDrawingRayCast` 脚本创建无绘制命中组件，其余无图按钮使用透明 Image 命中代理；幻兽候选 prefab 的 TMP 文本绑定官方 FZY4K 动态字体。原 TMP 材质／描边、状态动画与按钮音效仍未完整恢复。属性弹窗的 `PropCatalogCfgList`、细分分组和行模板由 `prepare_character_prop_bindings.py` 从固定 APK 预制体读取。

验证边界： `scripts/check_unity_project.py` 已通过（39,822 个 GUID）；窗口 Unity `r43` 进入 `interactive-ready`，用户实测打开幻兽弹层后未找到可见关闭控件，并确认人物页整体与原版不符，因此本页 Play Mode 画面与真实输入验收失败。`git diff --check`（按 CRLF 文件规则）通过。`scripts/prepare_character_skill_appearance_ui.py --check` 仍因 Unity 已生成的 `ImgSkillListBg-d5c6db9805.png.meta` 与固定生成器字节不同而失败；保留现有 GUID，未覆盖该元数据。完整人物流程、弹层关闭、存档退出重开、不同模型组合、动画／音效和 Windows Player 尚未验证；`Bootstrap.unity` 当前仍为空启动框架，不能证明 Player 可进入人物流程。

## 代码与资源入口

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
