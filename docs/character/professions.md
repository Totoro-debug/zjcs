# 职业、晋升与自由转职

[总索引](../README.md)

状态：未完成

## 功能简介

维护职业、晋升及自由转职；技能详见[技能目录](skills.md)。

职业信息、角色技能与战斗技能栏共用项目固定来源的技能图标，保留原始 Sprite 网格、UV、图集采样和 mip 数据；技能品质取自固定来源配置，品质框共用当前原生 Sprite 和完整图集，保留官方浮点裁剪范围及留白。职业信息的装备类型图标沿同一来源迁移。见[职业图标来源](../../assets/reference/profession-ui/skill-icons-source-manifest.json)及[战斗与角色绑定](../../assets/reference/battle-animation/skill-icons-source-manifest.json)和[品质框来源](../../assets/reference/battle-hud/skill-quality-source-manifest.json)。

## 自定义实现

自由转职生成三组方案后取消不消耗转职石，最终确认才扣：免费方案零个、非免费一个。免费间隔沿原 168 小时，生成免费方案时推进，放弃不重置；费用、方案、职业和奖励同笔保存。

职业动画沿[免费原生迁移方案](character-creation.md#spine-迁移决策)。
