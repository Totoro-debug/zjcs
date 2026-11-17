# 装备、强化、继承与星石

[总索引](../README.md)

状态：未完成

## 功能简介

人物装备页和行囊装备分类提供五槽穿戴、候选、详情、强化、继承、星石镶嵌与拆卸。

Unity 已接入装备实例、属性来源、本地存档事务和部分页面交互。当前面板由人物页节点工厂组合，尚未恢复完整官方装备界面。

## 原始官方实现

装备实例、主副属性、随机词条、品质、套装、祝福、强化、继承与星石遵循官方 `EquipInfo`、`EquipAttributeInfoParser`、`PlayerEquipDataExt` 及对应配置。词条互斥、属性计算和战斗被动效果沿用官方规则。

## 完整还原部分

- `OfflineEquipmentProfile` 保存装备实例、部位关系、词条、星石及强化状态；装备操作通过角色存档事务提交。
- `OfficialEquipmentGenerator` 按官方装备配置和随机权重生成装备；装备等级、祝福、品质、词条互斥与套装参与属性计算。
- 角色页已接入装备槽、候选、详情、比较、强化、继承及星石操作。装备规则载荷与局部界面资源入口见[Unity装备界面来源清单](../../unity/Assets/Official/Equipment/UI/source-manifest.json)。
- 装备载荷生成器从 `official-source.json` 读取当前输入，完整解码规则表和等级属性；运行数据不生成来源版本字段，模块来源清单如实记录实际输入。Unity 读取不依赖固定版本号，继续检查显式绑定、载荷大小、SHA256、输入清单和必需表；历史目录恢复工具保留来源哈希、prefab 身份与覆盖保护。不同／缺省来源版本的生成恢复及哈希拒绝测试通过，既有载荷和去除版本字段后的 1,042 项装备选择原代码对照分别通过；Unity 导入编译与 `ProjectTools.Validate` 退出码 0。本项未重新生成既有装备资源或更新其官方输入。
- 共鸣属性按原 `PlayerData.ActivedEquipResonanceLevel` 读取独立的已激活记录，复用原 `EquipResonanceInfoParser.PostParse`／`CalcEquipResonanceProps` 累计各档 `Effect`，不按当前穿戴数量自动激活或仅保留最高档。装备生成、属性和等级限制使用所选 `884_156692` 装备配置；家园去赐福投影沿用同版配置。输入及运行载荷见[装备运行来源清单](../../assets/reference/equipment-runtime-source-manifest.json)。新档与缺字段旧档均为原默认 0。手动激活事务与原生界面仍缺。共鸣记录来源见[共鸣记录接线清单](../../assets/reference/home-resonance-profile-source-manifest.json)。

所选版本装备载荷生成测试、1042 项装备选择原代码对照和战斗词条重放／检查点回归通过；Unity 导入、编译及 `ProjectTools.Validate` 退出码 0。该版本切换后的角色装备界面、模拟输入、存档恢复及 Windows Player 尚未验证，不能视为完整装备功能验收。

`HomeResonanceProfileValidation.Run` 已在隔离 Unity 工程批处理与静音 Play Mode 通过全部 240 级累计效果、装备满足条件但未激活、卸装后保留激活、原越界查询、旧档缺字段默认值、真实存档事务／磁盘重开，以及家园去赐福投影对同一激活记录的限制；两次最终退出码均为 0。装备词条既有 1042 项原代码对照回归通过，静态检查通过（152110 个 GUID）。本项未变更界面，未验证手动激活操作或完整 HUD；最新主工程导入／编译及 `ProjectTools.Validate` 已补跑，退出码 0，仍保留既有战斗 Edit Mode 释放诊断；未构建或发布本项 Windows Player。

## 自定义实现

服务端星石返还参数未在客户端提供。单机拆卸时返还原有星石，不增加费用或额外奖励。

## 尚未实现

- 共鸣手动激活、条件核对、原生按钮与动画，及其他属性消费者的统一。
- 官方装备各面板的完整坐标、锚点、字体、层级、动画、音效、升级粒子与继承奖励表现。
- `IsApplyInBattle=false` 的类型 4 被动词条战外属性转换尚未接入人物属性来源；继承成功后的逐项结果页仍缺失。
- 完整装备操作、存档冷读与 Windows Player 流程尚未完成 Unity Play Mode 验证和发布。

## 代码与资源入口

[OfflineEquipmentProfile.cs](../../unity/Assets/Game/Runtime/Offline/Equipment/OfflineEquipmentProfile.cs)、[OfflineEquipmentOperations.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentOperations.cs)、[OfficialEquipmentGenerator.cs](../../unity/Assets/Game/Runtime/Offline/Equipment/OfficialEquipmentGenerator.cs)、[OfflineEquipmentPropSource.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentPropSource.cs)。

[PlayerEquipDataExt](../../.tools/source-packages/official-code/common-source/Common/PlayerEquipDataExt.cs)、[EquipAttributeInfoParser](../../.tools/source-packages/official-code/common-source/Common/EquipAttributeInfoParser.cs)、[BattleFormulaHandler](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)。

装备词条筛选与替换参数的来源及适配入口见[战斗规则来源](../../assets/reference/battle-rules/equipment-selection-source-manifest.json)和[词条替换来源](../../assets/reference/battle-rules/equipment-term-source-manifest.json)。
