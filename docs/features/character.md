# 人物属性、经验、位阶与衣柜

[总索引](../README.md)

状态：未完成

## 功能简介

从头像或底栏人物进入，展示名字、外观、等级、经验、位阶、职业、属性与装备；外观按钮进入衣柜，管理已获得的角色幻装及称号。职业、技能和装备分别在对应文档维护。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

Unity 已接入固定版本 `854_156383` 人物成长目录、人物概览与属性文本、限时外观配置、原衣柜规则/物品/UI 数据及其引用图片。生成入口为 [prepare_unity_character_catalog.py](../../scripts/prepare_unity_character_catalog.py)，输出与官方输入哈希登记于 `unity/Assets/Official/Character/source-manifest.json`；`OfficialCharacterAssetCatalog.FindSprite` 按原资源 URI 查找人物与衣柜 Sprite，编辑器绑定及来源核对入口为 [CharacterProgressionValidation](../../unity/Assets/Game/Editor/Character/CharacterProgressionValidation.cs) 的 `BindOfficialCatalogs`。

[OfflineCharacterExperience](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterExperience.cs) 按绑定配置计算等级、储存经验、赐福等级和进度比例，并计算已确认的限时外观续期；[OfflineCharacterProgressionService](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs) 将经验奖励、位阶材料支付、10级教学奖励、穿戴/卸下、称号、染色、限时到期和衣柜进度写入共享人物进度事务；[OfflineWardrobeRules](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineWardrobeRules.cs) 解析互斥皮肤、绑定部件及装备皮肤回退。属性的基础比率、百分比、转换和倍率处理位于 `OfficialCharacterProperties`。

当前完成的是数据恢复、规则目录绑定和人物存档服务层；尚无人物页或衣柜运行入口，编辑器目录绑定/验证尚未在 Unity 执行，经验领取界面插值、完整来源属性合并与战力、官方人物层级与衣柜交互也未完成。素材恢复和静态 GUID 检查不构成人物功能运行验收。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

人物页与衣柜中的角色显示、换装和染色沿用[Spine 角色动画迁移决策](../asset-management.md#spine-角色动画迁移决策)；共用转换能力与人物显示调用方接通后仍须在本模块验证。

用户补证普通经验在当季基础上限后继续提升显示等级，超出部分为当季赐福，赛末回到当季基础上限。服务端经验路由未取得，本地按此处理并保留赛季记录。

限时装扮首次有效期和到期卸下依据原客户端；重复获得的服务端续期未取得。用户已确认按配置 `CanOverlapTime` 叠加 `Duration`，并将剩余有效期限制在 `MaxDuration` 内；不允许叠加时按配置重设时长。此续期规则为已确认的离线实现。

## 尚未实现

- 接通实际人物入口、人物页和位阶/经验交互；完成经验领取成功后的官方时长动画、储存经验规则说明、教学触发/恢复和返回家园条件，并在 UI 中表现失败与保存结果。
- 迁移 `PlayerPropContext` 分块属性合并与战力公式，接入装备、职业、技能、幻兽、古遗物、公会、星命和衣柜属性来源，使人物页、战斗属性及评分读取同一结果。
- 将已恢复的官方人物页/衣柜 prefab 层级、坐标、控件字段、人物模型/动画及音效接入 Unity；完成类别、称号、筛选、互斥、装备外观回退、染色、时限显示与到期卸下、收藏经验及衣柜属性交互。
- 在 Unity 中执行资源导入、目录绑定、规则核对和运行验收，覆盖正常操作、动画/音效、读档恢复及事务失败回滚；静态检查和既有引擎结果不替代 Unity 验收。

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

、[PlayerExpSlider](../../.tools/source-packages/official-code/client-source/PlayerExpSlider.cs)、[PlayerLevelText](../../.tools/source-packages/official-code/client-source/PlayerLevelText.cs)、[PlayerBlessLevelUtils](../../.tools/source-packages/official-code/common-source/Common/PlayerBlessLevelUtils.cs)、

、[AppearanceDataEx](../../.tools/source-packages/official-code/common-source/Common/AppearanceDataEx.cs)、[PlayerAppearanceHandler](../../.tools/source-packages/official-code/common-source/Common/PlayerAppearanceHandler.cs)

[AppearanceUtils](../../.tools/source-packages/official-code/client-source/AppearanceSystem.Runtime/AppearanceUtils.cs)、[prepare_character_appearance.py](../../scripts/prepare_character_appearance.py)、

、[verify_character_appearance_assets.py](../../scripts/verify_character_appearance_assets.py)、
