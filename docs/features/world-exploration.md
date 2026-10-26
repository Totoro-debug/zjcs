# 命运果实探寻

[总索引](../README.md)

状态：未完成

## 功能简介

修复生产果树并满足原个人条件后，在允许探寻的公开区域使用命运果实115；可查看地区、次数、概率和历史。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

已接入 Unity 本地规则层：原个人与区域条件、7种令牌、65条实体组变体、原权重与保底记录、首次组51、生成实体的等级／阶位与额外奖励数据、原费用、每日上限、累计产出限制、持续时间和180天历史。动态实体、随机、费用、记录与首次固定奖励以同一存档事务保存；重复请求不重抽，整批失败全部回滚。

原触碰奖励实体按配置读取奖励规则、条件、等级与阶位倍率，以及随机奖励包。探寻来源免体力；奖励只在领取时入包，宝箱开启计数与领取记录同时保存。协力宝箱的本地单人奖励结算保留原完成状态及 `DestroyOnEnd=false`，开启后直至原有效期届满才移除。该结算层已经接通，原交互动画和实际鼠标开启流程仍待验证。

隔离 Unity 编辑器验证已覆盖个人条件、首次令牌限制、首次引导与全局一次奖励、重复请求、批量失败回滚、冷启动恢复、原实体等级、同时满足多个保底、协力宝箱累计上限20、五分钟实体过期、180天历史清理、旧剧情奖励记录兼容，以及奖励失败完整回滚、宝箱重复领取和协力宝箱开启状态。`python -B scripts/check_unity_project.py` 与隔离 Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 已通过。尚无完整界面、Play Mode 交互或 Windows Player 发布结论。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

未取得 `OperPlayerWorldExplore` 服务端体，用户允许本地结算：候选取当前Map／Area的原地区 `DropItems` 与原宝物选择器交集；多保底同时命中按 `ExplorePriorities` 及原配置次序选择。

实体落在同Area最近的可通行、允许地表、未占用格，范围使用原 `RefreshDistance`／`ExploreEntityMaxDist`。`LastMapRefreshOnly` 替换前一同组结果，保留原数量上限、持续时间和180天历史，过期按本地时间清理。

实体组54「协力宝箱」7050沿用户确认方案，保留原概率、累计产出上限20次、原奖励和有效期，改为本地玩家独自完成原交互；原客户端的两人要求不再作为本地开启条件。此差异尚待运行流程接入与验证。

固定 APK 客户端、Common 程序集及原探寻／固定奖励表已核对；服务端 `OperPlayerWorldExplore` 执行体未取得，无法证实引导奖励的发放时机。用户明确确认：首次成功命运探寻发放原 `WorldExploreGuideFixedRewardId=5` 的物品56“命运果实·原初”×1，全局只发一次；沿用 `fixed_award:5` 领取记录，跨地图或原剧情入口均不重复。奖励不按既有探寻次数追溯补发。奖励、费用、随机、实体和领取记录一并保存，失败全部回滚。

原奖励表缺少小宝箱8200的711、大宝箱8201的721／722、稀有宝箱8202的732／735、传说宝箱8203的742／745。用户明确确认沿用客户端 `RewardRuleInfoParser.GetRewardDropInfosByRuleList` 跳过空引用的行为，其余原奖励保留；没有编造额外奖励。8200仍执行710／713／714／716／717，715未被现行实体模板、山之国覆盖或探寻配置引用。

## 尚未实现

- 完成原确认框、令牌选择、概率、详情、历史分页与 HUD 的实际入口、资源绑定及点击验证。
- 完成生成实体的地图显示、碰撞／寻路与小地图绑定，以及宝箱、战斗、副本、商店和活动实体的完整交互。
- 恢复原动作、MyPlayable、粒子及音效，完成 Play Mode 正常操作、画面与存档恢复，并构建、运行和发布 Windows Player。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

保留原单次／多次按钮，单批最多10次，令牌功能开放后可选择真实持有令牌。消耗、每日上限、生命、区域、剧情／副本和待领奖状态均校验。

首次使用原引导实体组51，之后按原探寻组、地区、权重、保底、数量和额外数据生成实体；奖励经过原地图交互、战斗和领奖取得，不直接将预览入包。

原 `WorldExploreAction`、`AppearAction`、角色动作、MyPlayable轨道、粒子和音效须接入。动态实体、每日次数、保底、历史、果实／令牌和随机在同一事务保存，失败回滚，重开不重抽。

### 依据与代码定位

固定 APK 原规则由 `scripts/prepare_unity_world_exploration.py` 重新解码，输出 `Resources/World/Mainline/exploration.json`；包含7种令牌、65条实体组变体、28种完整实体模板、68条递归条件、奖励规则、随机奖励包、宝物选择器、等级映射及每表原字节哈希。来源见 `assets/reference/official-map/world-exploration-source-manifest.json`；规则入口为 `OfficialWorldExplorationCatalog`、`OfflineWorldExplorationService` 及 `OfflineMainlineProgressSession.Exploration`，隔离验证入口为 `Zjcs.Editor.Progression.OfflineWorldExplorationValidation.Run`。`JsonUtility` 对原始 JSON 空对象引用的处理已逐字段校正，避免将原空条件误当成未迁移的条件。

原始 APK 保留，旧版云端目录按用户要求清理；如需重建，显式启用 `Apk(reuse_current_cache=True)`，直接读取当前871缓存中大小／MD5符合旧清单且SHA256符合真实下载凭据的同哈希文件，保留真实来源版本和URL。此选项不会恢复旧缓存，也不会以新版同名变化资源填充旧输入。

共享保底依据为 `Common.StandardRandomGroup`、`RandomObj` 和 `PlayerExploreRecordDataWrap.RecordResult`。命中累计、连续未中计数及保底冲突需按这些实现迁移；组54的20次限制是累计产出次数，不能改成场上实体上限。
