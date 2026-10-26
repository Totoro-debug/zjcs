# 离线开放条件与单机范围

[总索引](../README.md)

状态：未完成

## 功能简介

统一解释本地单机的开放条件。适用于人物突破、养成、地图与功能入口；世界阶段和赛季记录仍独立存在。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

已有 [OfflineOpeningPolicy.Evaluate](../../unity/Assets/Game/Runtime/Offline/State/OfflineOpeningPolicy.cs) 与 [OfflineConditionAdapter.Evaluate](../../unity/Assets/Game/Runtime/Offline/State/OfflineConditionAdapter.cs) 的门槛策略及委托适配。通用官方条件目录、地图和玩法调用方尚未完成接线，未取得端到端验证结论。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

用户已明确取消服务器阶位、服务器／注册天数及地图／功能的日历开放门槛。 对相应原条件返回单机结果，原条件和配置仍保留供追溯。

本地世界阶段用于原内容分档、赛季及历史记录，玩家可独立手动推进；它不再作为人物突破、地图或功能的服务器锁。个人突破仍支付原材料并检查个人资格。谜题活动的本地阶段时长不属于服务器开服日期，仍由各图说明。

真人交易、拍卖、聊天、好友、真人赛事和魔物战争不提供离线替代；公会只保留单人内容，助战使用原 NPC，不生成虚构玩家。

### 暂不开放的联动活动及伙伴

用户于2026-10-21明确决定：暂不开放以下两个联动活动及其全部关联伙伴。

| 活动 | 官方活动标识 | 全部关联伙伴 |
| --- | --- | --- |
| 《关于我转生变成史莱姆这档事》联动 | `ActivitySlime`／活动216 | 利姆鲁（11015）、米莉姆（11016）、朱菜（11017）、红丸（11018） |
| 《孤独摇滚！》联动 | `BocchiTheRock`／活动223 | 后藤独（11019）、伊地知虹夏（11020）、山田凉（11021）、喜多郁代（11022） |

范围包含活动主页、签到、任务、剧情、小游戏（含史莱姆拼图2161）、抽取、兑换与礼包等全部关联子活动及入口；伙伴范围包含名册、结识、详情、送礼／好感、档案、餐桌／其他对话、旅行和全部助战入口。通用取消日历／开服门槛的约定不构成开放这两个活动及其伙伴的授权。

本决定优先于此前针对这些活动签到、拼图的界面／题图复用及常驻开放授权。现有代码、配置、素材和来源记录保留供追溯；已转换、已有底层实现或日后补齐资源均不表示获准开放。相关缺项暂不纳入当前补齐范围，恢复开放须由用户另行明确决定；不能将主动暂停标为已完整还原。

本节记录用户确定的开放范围，不作为运行时全部入口已关闭的验收结论。既有实现与资源缺项分别见[伙伴](partners.md)、[签到](sign-ins.md)和[活动小游戏](activity-minigames.md)。活动标识与伙伴名称依据[官方活动配置](../../assets/official/activities.json)和[官方本地化](../../assets/official/map-localization.json)。

## 尚未实现

- 通用条件、人物突破、地图与玩法入口统一接入已确认的本地开放政策；个人资格、原配置及NPC NeedUnlock否定分支继续校验。
- 恢复官方 Unity 代码、配置、prefab、资源与调用关系，完成本地事务、实际入口和界面接线。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

个人等级、人物阶位、材料、主线目标、探索进度、NPC 原邀请资格和冷却继续读取固定版本原配置。活动持续时间、停止挑战、阶段推进及圣兽／双影的期次刷新继续有效。

条件解释分别经过基础、玩法和地图调用路径；`NpcAdventureUnlockCondition` 的 `NeedUnlock=false` 保留否定分支语义，不把正反条件同时判定为满足。

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

[ServerRankUnlockDaysCondition](../../.tools/source-packages/official-code/common-source/Common/ServerRankUnlockDaysCondition.cs)、[PlayerRankAtServerRankCondition](../../.tools/source-packages/official-code/common-source/Common/PlayerRankAtServerRankCondition.cs)、

[官方活动配置](../../assets/official/activities.json)、[官方本地化](../../assets/official/map-localization.json)、[OfflineOpeningPolicy.Evaluate](../../unity/Assets/Game/Runtime/Offline/State/OfflineOpeningPolicy.cs)、[OfflineConditionAdapter.Evaluate](../../unity/Assets/Game/Runtime/Offline/State/OfflineConditionAdapter.cs)
