# 装备、强化、继承与星石

[总索引](../README.md)

状态：未完成

## 功能简介

人物装备页和行囊装备分类提供五槽穿戴、候选、详情、强化、继承、星石镶嵌与拆卸。

Unity 已接入装备实例、属性来源、本地存档事务和部分页面交互。当前面板由人物页节点工厂组合，尚未恢复完整官方装备界面。

## 原始官方实现

装备实例、主副属性、随机词条、品质、套装、祝福、强化、继承与星石遵循固定版 `EquipInfo`、`EquipAttributeInfoParser`、`PlayerEquipDataExt` 及对应配置。词条互斥、属性计算和战斗被动效果沿用官方规则。

## 完整还原部分

- `OfflineEquipmentProfile` 保存装备实例、部位关系、词条、星石及强化状态；装备操作通过角色存档事务提交。
- `OfficialEquipmentGenerator` 按官方装备配置和随机权重生成装备；装备等级、祝福、品质、词条互斥与套装参与属性计算。
- 角色页已接入装备槽、候选、详情、比较、强化、继承及星石操作。装备规则载荷与局部界面资源入口见[Unity装备界面来源清单](../../unity/Assets/Official/Equipment/UI/source-manifest.json)。

## 自定义实现

服务端星石返还参数未在客户端提供。单机拆卸时返还原有星石，不增加费用或额外奖励。

## 尚未实现

- 官方装备各面板的完整坐标、锚点、字体、层级、动画、音效、升级粒子与继承奖励表现。
- `IsApplyInBattle=false` 的类型 4 被动词条战外属性转换尚未接入人物属性来源；继承成功后的逐项结果页仍缺失。
- 完整装备操作、存档冷读与 Windows Player 流程尚未完成 Unity Play Mode 验证和发布。

## 代码与资源入口

[OfflineEquipmentProfile.cs](../../unity/Assets/Game/Runtime/Offline/Equipment/OfflineEquipmentProfile.cs)、[OfflineEquipmentOperations.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentOperations.cs)、[OfficialEquipmentGenerator.cs](../../unity/Assets/Game/Runtime/Offline/Equipment/OfficialEquipmentGenerator.cs)、[OfflineEquipmentPropSource.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentPropSource.cs)。

[PlayerEquipDataExt](../../.tools/source-packages/official-code/common-source/Common/PlayerEquipDataExt.cs)、[EquipAttributeInfoParser](../../.tools/source-packages/official-code/common-source/Common/EquipAttributeInfoParser.cs)、[BattleFormulaHandler](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)。

装备词条筛选与替换参数的来源及适配入口见[战斗规则来源](../../assets/reference/battle-rules/equipment-selection-source-manifest.json)和[词条替换来源](../../assets/reference/battle-rules/equipment-term-source-manifest.json)。
