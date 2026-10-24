# 装备、强化、继承与星石

[总索引](../README.md)

状态：未完成

## 功能简介

人物五个装备槽和行囊装备分类进入候选、详情、比较、强化、继承、星石及碎片合成。

Unity 已接入角色五槽及行囊装备操作、实例存档事务、装备属性来源和官方装备 UI 数据载荷。运行界面目前由角色页节点工厂组合面板，尚未恢复装备 prefab 的完整原始坐标、动画音效、强化粒子及继承奖励表现；尚未完成 Unity 编辑器编译和 Play Mode 交互验收，因此模块仍未完成。

## 完整还原部分

`scripts/prepare_unity_equipment_catalog.py` 校验固定 `854_156383` 规则来源并恢复运行所需表。`OfficialEquipmentCatalog` / `OfficialEquipmentAssetCatalog.Load` 校验版本、输出长度、SHA256 与关键表；编辑器入口为 `Zjcs.Editor.Equipment.OfficialEquipmentValidation.Validate`。

`OfflineEquipmentProfile` 为五部位保留独立实例 ID、实例词条、星石、锁定状态、穿戴关系和各部位基础/赛季祝福强化状态。`OfflineMainlineProgressSession.ApplyCharacterMutation` 对装备数据与玩家背包使用同一存档副本及原子写盘，旧存档缺少装备字段时初始化为空装备档案。

`OfflineEquipmentOperations` 已接入穿戴职业检查、部位强化报价和材料事务、星石候选/镶卸、碎片职业过滤与部位排序、碎片实例生成回调、装备分解限制与原奖励/星石返还、以及继承属性事务。强化和祝福限制读取原 `system_level_limit`、`system_level_gap_limit` 与材料表；星石条件由调用方按原条件对象求值；继承重掷由调用方传入官方生成回调，以便单机存档事务整体回滚。

角色页的 `CharacterEquipSlots` 从五个原角色槽与背包调用穿戴、卸下、锁定、分解、强化、星石、继承、碎片合成操作；详情、候选排序、比较与操作状态绑定角色页。`OfflineEquipmentPropSource` 接入主副属性、普通/特殊词条、星石普通与特殊属性、祝福强化、套装阈值及装备共鸣；普通星石按原 `GemPartInfoParser.GetGemProp` 使用物品阶位和属性类型查 `level_prop`，特殊星石按 `EquipAttributesPropsInfo.GetGemSpecialProp` 取 `Props[0]`。有效词条按 `ExclusiveGroup`/`SelfExclusive` 比较战力后择优；`CalculateCombatRating` 已计算装备基础属性、套装评分、普通/特殊词条品质与祝福缩放。被动特殊词条的技能实体属性转换尚未合入战斗角色属性。角色属性页和战斗属性读取同一来源，分解/继承/镶卸/强化仍由 progression 存档事务提交。

`OfficialEquipmentGenerator` 按固定版 `EquipInfo.GenerateItemParams` 与 `EquipAttributeInfoParser` 的已还原数据迁移掉落等级、祝福等级、主副属性类型权重、随机词条类型/互斥、品质与阶位过滤、被动品质及套装抽取。代码依据为 `.tools/source-packages/official-code/common-source/Common/PlayerEquipDataExt.cs`、`EquipInfo.cs`、`EquipAttributeInfoParser.cs`、`EquipUpgradeMatInfoParser.cs` 和客户端 `EquipPromotePanelUpgradePanel.cs`、`EquipInheritSelectContent.cs`、`EquipSelectPanel.cs`。

Unity 官方载荷中有 39 个装备、强化、继承、星石、详情与碎片 prefab 层级目录和 132 张对应原图；由 `prepare_unity_equipment_catalog.py` 从已转换官方目录恢复，`Assets/Official/Equipment/UI/source-manifest.json` 记录面板、来源身份、输入/输出哈希与图片路径。编辑器校验会检查目录结构及原图导入/哈希。此处目前只完成原始 UI 数据和图片载荷，不表示已生成可操作 Unity 界面。

## 自定义实现

副本最终评级奖励读取 `dungeons-runtime.json` 的 `gradeRewards`、`rewardRules`：按官方条件选中当前评级行，并遵照客户端 `RewardRuleInfoParser` 的 `Percent × Weight / WeightTotal` 逐候选概率和数量结算；房间惊喜奖励读取房间配置引用的同一套规则。完整装备奖励的 `ItemType=110` 是官方随机装备占位项，`randomEquipmentPools` 由 `random_reward.IdList` / `WeightOfIdList` 导出为带权实际装备 ID 池；装备实例通过 `OfficialEquipmentGenerator.Generate` 按所选装备和官方生成逻辑创建。实例在一次性 `ApplyInventoryTransaction` 中写入装备档案；等级和祝福等级应用对应副本地图组、难度的官方区间，掉落来源使用 `SourceOperate.DailyDungeonReward = 61`。装备碎片等普通道具按库存事务发放。服务端领奖响应中的 `EquipItemData` 未下发生成参数，本地使用同一官方装备配置生成实例；提交后的重复领奖由 receipt 去重，不重掷。

星石卸下按现有 Godot 离线适配返还同一颗已拥有星石，不添加费用、升级或替代奖励。赛季装备柜的记录和只读详情见[赛季](seasons.md)；远古砥石的词条替换、自动锁定、分解与期限处理见[行囊](inventory.md)。

## 尚未实现

- 被动技能词条的品质、索引、`EquivalentProps` 战力估值及祝福倍率已接入；实际战斗效果仍缺。官方 `BattleFormulaHandler.CalcEquipTermExchangeProps` 按 `EquipAttributesPassiveSkillInfo.SkillId` 查技能实体，以 `PropsFactors`/品质计算技能属性，用 `SkillPropMapFactorIds` 做祝福缩放，再应用对应 `FightStatusPropComponent.TransitionPropCfgs` 将属性转换到玩家战斗属性。当前战斗组装只有角色自带被动技能，尚未把装备被动特殊词条转换加到玩家属性。
- 继承事务已执行来源/目标实例更新并保存，但成功后界面只关闭弹窗并刷新装备槽；原继承结果页逐条显示接收属性、替换属性和首项奖励装备实例的表现尚未接入。
- 当前交互面板使用已恢复节点模板组合，没有直接恢复装备原 prefab 完整坐标/锚点、字体和层级；尚缺星石长按原详情、部分重复合成确认及原材料不足/取消状态表现。
- 恢复 `Effect_ui_Upgrade` 六个粒子系统、目标词条顺序、六类原奖励挂件与原按钮/动画音效；检查 Android prefab/bundle 与编译 shader 的 Windows 兼容性。
- 当前 `assets/official/ui.json` 汇总 SHA256 与历史 `equipment-completion` 输出清单记录值不一致；逐面板 prefab 身份和图片来源哈希已核验，仍需独立从原始 bundle 重建 UI 投影并核对实际汇总哈希。
- 在 Unity 编辑器执行装备及 UI 载荷校验、编译并实际运行界面；覆盖正常操作、存档恢复、失败回滚、原资源绑定、动画/音频及不同窗口尺寸下的鼠标与键盘交互。
