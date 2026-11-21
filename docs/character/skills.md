# 技能目录

[总索引](../README.md)

状态：已完成

本次迁移范围：一阶初始职业、二阶首次转职及四条路线的三阶、四阶职业技能；当前已完成 194 项（原 104 项、40 项三阶技能及 50 项四阶技能）。

技能记录仅包含技能类型、技能 ID、技能名、官方描述、特殊词条解释、是否完成与实现要点。

| 职业技能 | 是否完成 |
| --- | --- |
| [战士（一阶）](skills-warrior.md) | 已完成 |
| [法师（一阶）](skills-mage.md) | 已完成 |
| [骑士路线（二至四阶）](skills-knight.md) | 已完成 |
| [斗士路线（二至四阶）](skills-fighter.md) | 已完成 |
| [术士路线（二至四阶）](skills-warlock.md) | 已完成 |
| [贤者路线（二至四阶）](skills-sage.md) | 已完成 |

<a id="自定义实现"></a>
<a id="尚未实现"></a>

## 实现要点

按官方描述迁移主动战技、被动秘法、被动与触发效果、子技能、增益与减益、动画及人物动作；已确认的单机边界只在对应技能的实现要点中简述。

合成、装配、升级、进阶与存档使用公共培养及界面流程，技能差异由官方配置驱动。

装配仅使用已持有且职业可用的技能；空战技方案允许进入战斗，正常结算回合与被动，不自动补战技。

连锁攻击遵循已确认的[单机目标选择规则](../combat/battle-execution.md#自定义实现)。

三阶、四阶技能继续使用官方数据驱动的共享链路：`FightOfficialCombatCatalog` 负责技能与数值配置，`FightBattleHitCollector` 负责命中图和子技能展开，`FightDamageFormula` 负责伤害计算，`FightStatusTriggerRuntime` 与 `FightSkillLogicRuntime` 负责通用状态、增益/减益、治疗、护盾、召唤和触发效果，`OfficialBattleSkillGraphPlayback` 负责原生技能图演出。完整 40 项三阶职业技能由 `scripts/verify_third_profession_skills.py` 校验，完整 50 项四阶职业技能由 `scripts/verify_fourth_profession_skills.py` 校验官方来源载荷、职业绑定、引用实体图及原生演出绑定；`scripts/test_fourth_profession_skills.ps1` 进一步通过同一战斗执行器验证四阶主动及内置战技的选取、施放和结算。

五阶及以上职业仅保留既有技能说明，不计入本次完成范围。
