# 命运果实探寻

[总索引](../README.md)

状态：未完成

## 功能简介

修复生产果树并满足原个人条件后，在允许探寻的公开区域使用命运果实115；可查看地区、次数、概率和历史。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

尚无本功能在 Unity 完整接入并运行验收的结论。原代码、配置、已转换素材和旧引擎测试仅是迁移依据，不作为本功能已还原的证据。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

未取得 `OperPlayerWorldExplore` 服务端体，用户允许本地结算：候选取当前Map／Area的原地区 `DropItems` 与原宝物选择器交集；多保底同时命中按 `ExplorePriorities` 及原配置次序选择。

实体落在同Area最近的可通行、允许地表、未占用格，范围使用原 `RefreshDistance`／`ExploreEntityMaxDist`。`LastMapRefreshOnly` 替换前一同组结果，保留原数量上限、持续时间和180天历史，过期按本地时间清理。

固定 APK 客户端、Common 程序集及原探寻／固定奖励表已核对；服务端 `OperPlayerWorldExplore` 执行体未取得，无法证实引导奖励的发放时机。用户明确确认：首次成功命运探寻发放原 `WorldExploreGuideFixedRewardId=5` 的物品56“命运果实·原初”×1，全局只发一次；沿用 `fixed_award:5` 领取记录，跨地图或原剧情入口均不重复。奖励不按既有探寻次数追溯补发。奖励、费用、随机、实体和领取记录一并保存，失败全部回滚。

小宝箱8200的原 `StageResultAwardComponent.AwardInfos` 引用奖励规则711，固定版本 `reward_rule` 无该编号，内容无法确定。用户明确确认跳过711，其余原奖励规则710／713／714／716／717照常执行；没有编造额外奖励。715未被现行实体模板、山之国覆盖或探寻配置引用。跨地图一次领取、奖励不按既有探寻次数追溯发放、失败回滚及小宝箱规则保留；相关流程仍须接入 Unity 并验证。

## 尚未实现

- 命运果实115的原个人条件、区域候选、概率／保底、实体落点、历史、原奖励与跨地图一次性引导奖励。
- 恢复官方 Unity 代码、配置、prefab、资源与调用关系，完成本地事务、实际入口和界面接线。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

保留原单次／多次按钮，单批最多10次，令牌功能开放后可选择真实持有令牌。消耗、每日上限、生命、区域、剧情／副本和待领奖状态均校验。

首次使用原引导实体组51，之后按原探寻组、地区、权重、保底、数量和额外数据生成实体；奖励经过原地图交互、战斗和领奖取得，不直接将预览入包。

原 `WorldExploreAction`、`AppearAction`、角色动作、MyPlayable轨道、粒子和音效须接入。动态实体、每日次数、保底、历史、果实／令牌和随机在同一事务保存，失败回滚，重开不重抽。

### 依据与代码定位

固定 `854_156383` APK 的 `explore_token_group`、`explore_entity_group`、`map_entity_group`、`map_explore_limit`、`conditions` 和 `game_settings` 已由 `scripts/prepare_unity_world_exploration.py` 重新解码，输出 `Resources/World/Mainline/exploration.json`。7种令牌、65条实体组变体、17条递归条件及每表原字节哈希见 `assets/reference/official-map/world-exploration-source-manifest.json`。官方8份探寻 prefab 及令牌／实体组图标已纳入 Map11 原生 UI 载荷；尚未绑定可操作探寻入口。

共享保底依据为 `Common.StandardRandomGroup`、`RandomObj` 和 `PlayerExploreRecordDataWrap.RecordResult`。命中累计、连续未中计数及保底冲突需按这些实现迁移；组54的20次限制是累计产出次数，不能改成场上实体上限。


