# 游记入口与日常副本

[总索引](../README.md)

状态：未完成

## 功能简介

游记入口汇集日常副本、地图、竞技及每日／每周挑战；各玩法的具体规则由所属页面维护。

日常副本与竞技已有本地规则、存档及战斗控制器接线。官方入口与详情界面仍有缺项，完整输入、结算和存档恢复流程尚未完成。

## 原始官方实现

日常副本由 `DailyDungeonEntryPanel`、`DailyDungeonDetailContent`、路线与结果面板及 `DailyDungeonBattleInfo`、`DailyDungeonMapGroupInfo` 配置驱动。难度、等级、区域进度、评级、房间、战斗和奖励规则使用各自原配置；Type 22 是按累计成功领奖次数发放奖励的成就页，不增加每日领奖次数。

竞技场入口使用 `ArenaEntry`、守卫、名次、匹配和奖励配置。原客户端将榜单、对手和战斗结果请求交给服务器，未提供完整的服务端结算规则。

日常副本规则见[运行来源清单](../../assets/reference/dungeons-runtime-source-manifest.json)，界面绑定见[副本界面来源清单](../../assets/reference/dungeon-ui/prefab-binding-source-manifest.json)。竞技场来源版本与项目范围见[官方来源总表](../../assets/reference/SOURCES.md)。

## 完整还原部分

### 日常副本

`HomeFeatureRuntime` 使用公共 `DailyDungeonModule` 加载目录、路线和存档；旧 `OfflineDailyDungeonModule` 仅作为兼容适配器保留。世界之树、机神山、海之宫遗迹及目录中的其他公开关卡，以及 Map12 私有路线，都通过同一个模块打开。模块用 `DailyDungeonEntryPageModel`、`DailyDungeonRouteHudModel`、`DailyDungeonSettlementHudModel` 和 `DailyDungeonGiftModel` 聚合入口页、关卡/HUD、结算预览和 Type 22 馈赠状态；调用方只提供区域可见性、玩家条件、经济和奖励发放适配器。`DailyDungeonBattleRunner` 使用原战斗配置接入战斗控制器。已接入区域开放、难度与入场条件、房间推进、战斗检查点、评级、奖励预览及待领取结算。原 `RandomReward` 装备占位项按配置权重生成装备实例。

公共模块还统一处理难度解锁、评分与评级行、各难度奖励概率、评级奖励翻倍、每日领奖次数与购买、个人排行榜记录、Type 22 累计领奖馈赠和 Type 57 排行榜馈赠；路线、关卡、榜单、翻倍概率和经济差异分别通过 `IDailyDungeonRouteAdapter`、`IDailyDungeonLevelAdapter`、`IDailyDungeonLeaderboardAdapter`、`IDailyDungeonRewardDoubleChanceAdapter` 与 `IDailyDungeonEconomyAdapter` 接入。关卡只选择目录行、参数和适配器，不复制副本规则。

日常副本、Map12 私有副本和每日挑战的入场一致性由 `DailyDungeonBattleEntryModule` 统一处理：恢复时核对关卡、StageType、助战身份与 checkpoint 快照；新战斗按“提交助战 → 挂载战斗 → 首次保存 checkpoint”顺序执行，挂载或保存失败只回滚本次提交。三条入口仍各自保留玩法条件、存档适配器、挑战结算及空助战／已结束恢复分支。

`DailyDungeonModuleRuleAcceptance` 验证公共模块的目录页/难度状态、首档解锁条件、每日次数跨日结转和购买重置、路线 HUD、结算预览、额外领奖次数适配及领奖事务，并覆盖入场提交、挂载、保存顺序和失败回滚；完整副本功能仍按本页“尚未实现”项继续验证。

公共副本保存个人最佳评级，不创建虚构玩家榜单。路线、清理房间、战斗结果、领奖和背包更新写入本地存档事务，领奖回执防止重放。目录、路线、结算和馈赠页面均从 `DailyDungeonModule` 的聚合模型读取，关卡差异通过官方目录行以及 route/level/leaderboard/reward adapters 提供，不在页面复制关卡规则。

公共模块规则、Unity 编译与 `ProjectTools.Validate` 已通过，Play Mode 的模拟点击已验证累计馈赠页与副本页往返、原生 `ItemIcon` 奖励绑定、路线保存恢复及领奖次数回读。排行榜改用当前官方原生 `DailyDungeonTopPanel`、`TopRankTeamItem` 与原 `LoopVerticalScrollRect`、页签及布局组件，恢复原始图片、中文和对象状态；实际截图已确认空榜与标题显示，射线点击已验证榜单入口、原页签和关闭按钮。已修复本地同步查询早于原视口布局导致的空列表；Play Mode 已通过有记录列表与通关时长显示检查并查看实际截图。队伍头像、首通展示和个人记录区域仍未完成；该通关记录由隔离存档夹具生成，不代表完整战斗流程验证。结算截图仍有洋红材质与文字方块。

公共 `DailyDungeonLevelDefinition.LeaderboardGrades` 独立读取所选 `884_156692` 的 `DailyDungeonBattleInfo.TopRecordGradeTypeDict`，不从路线全部评级行推导上榜页签。原表由 APK 优先的 `config_luban` 原始字节及当前官方 Luban 构造函数重新解码，136 个副本／难度组合均只开放 S 评级榜；私有副本不开放。规则验证覆盖世界之树 S 评级榜和机神山私有路线隔离，来源与哈希见[排行榜来源](../../assets/reference/dungeon-ui/daily-dungeon-leaderboard-source-manifest.json)。

`DailyDungeonModule.BuildLeaderboardGiftModel` 与 `ClaimLeaderboardGift` 读取当前官方 Type 57 的 409 条固定奖励门槛，按官方 Group／Index 生成成就标识。`FinishAndPersist` 在公开路线成功完成的同一存档事务内累计指定副本、难度、准确评级的次数；未通关退出、私有路线及重复完成不累计。领奖将固定回执交给主档奖励事务，再保存已领取状态。规则验证覆盖四次世界之树 S 通关、难度隔离、领奖次数独立、失败发奖、写入失败后的同回执重试和冷打开去重；来源与配置见[排行榜馈赠来源](../../assets/reference/dungeon-ui/daily-dungeon-top-achievements-source-manifest.json)。官方表有 137 个奖励组，其中 44／Hell 无上榜配置且缺对应标题文本，保留原行与缺项记录，不开放其入口。原生馈赠页恢复原对象／颜色状态、`NoDrawingRayCast` 点击区域、进度条，以及经输入哈希核实同源的 Windows `UI/Gray` 材质；Play Mode 的射线模拟点击已通过页签切换、四次通关夹具达标、领取后背包增量和冷打开已领取检查，已查看未达标／达标／已领取实际画面。该夹具不代表完整战斗验证，可领取宝箱特效仍未恢复。

`DailyDungeonRewardAssetCatalog` 为所有副本奖励预览、随机装备池和两类馈赠共用的图标目录，1,164 个道具标识显式绑定到 586 张原 Sprite；`DailyDungeonResultItemIconAdapter` 从该目录读取原道具品质、类型和图标，不再依赖只覆盖家园物品的图标子集。贴图按原逻辑矩形恢复留白，并保留原 Sprite 打包旋转、紧密网格遮罩、pivot、border 和 PPU。资源、配置及处理哈希见[奖励图标来源](../../assets/reference/dungeon-ui/daily-dungeon-reward-icons-source-manifest.json)。

奖励结算由 `OfflineDailyDungeonSettlementModule.Claim`／`SettleClearedRooms` 统一承担，保留“预留领取次数 → 主档奖励与明细同时入库 → 确认副本次数与领取状态”的顺序。展示读取已提交的实际物品及装备实例标识，不再重复抽取或扫描装备序号；零掉落也保存回执。主档已提交而副本确认失败时，重新打开后读取回执补齐确认，不再生成奖励。

结算规则验证覆盖预留、入库、确认写入失败后的冷打开、重复提交、非法次数及装备实例关联；Unity 导入、编译和 `ProjectTools.Validate` 通过。Play Mode 与最终 Windows Player 的模拟点击已验证保存路线、退出清理房间奖励、最终领奖、次数及重新读取存档，竞技场守卫胜利与保存回读亦通过。该流程同时修正了结果页领奖按钮相对官方 prefab 漏掉 `Content` 的绑定路径。实际截图仍有既存材质洋红与字体显示问题，不能据此宣称结果界面还原完成。

### 竞技场

`OfflineArenaSession` 与 `ArenaBattleRunner` 使用原守卫、名次、匹配区间和战斗场景。地图进度达到原入口条件后开放；离线对手取自本地守卫模板，支持刷新、购买次数、挑战、战斗或直接胜利结算。

### 私有副本

Map12 的 121／50 机神山路线共用日常副本运行时，其奖励、次数隔离和结算边界见[山之国地图](../maps/map-12.md)。

## 自定义实现

- 每日领奖按官方存档的总剩余次数结转：跨日取前一日剩余次数与每日免费次数的较大值，购买次数、价格档位和购买回执每日重置。单机仅在成功领取最终奖励时扣总次数，进场、失败和未领奖均不扣；界面不把总次数拆成无法由官方存档证明的免费／购买余额。未接入福利时单次批量领取上限为 1，接入 `DailyDungeonExtraClaim` 后按适配器提高；Type 22 只累计成功领奖次数。
- 首个公开难度也读取其自身 `unlockConditionId`；入口页显示锁定状态，公共模块在条件满足前拒绝进入，条件适配器只由调用方提供。
- 离线排行榜只保留官方评级和通关时长两个排序字段：评级降序，同评级按通关时长升序；不加入职业、伤害或客户端虚构的贡献分。
- 排行榜馈赠 Type 57 使用已确认的单机方案：按本档案在指定副本、难度、评级下的累计通关次数判断，保留官方奖励与数量阈值。旧存档没有历次通关总数，新计数字段从 0 开始累计；保留已有最佳评级、排行榜与奖励回执。
- 评级使用官方地图评级行，不增加客户端未使用的职业贡献系数。`ServerRankUnlockDaysCondition` 按档案创建后经过的完整 24 小时计算。
- 奖励按官方条件、`Percent` 和候选权重结算；评级双倍概率为官方基础值 5%，未接入的本地加成按 0% 处理。单次结算领奖次数由 `IDailyDungeonAwardTimesAdapter` 提供，默认是官方基础值 1；接入官方有效 `DailyDungeonExtraClaim` 福利时使用“1 + ExtraClaimCount”，不改变每日领奖池。服务器没有提供的行为不推断为官方规则。
- 已确认的旧回执兼容：只保存入库标记而没有实际明细的历史奖励继续去重并补齐副本确认，提示“历史奖励已入库，明细不可恢复”，不重新抽取或推测历史装备。
- 竞技场用本地守卫和个人名次代替在线玩家及服务器赛季同步；胜利后使用原胜负奖励与对手名次结算。守卫附加外观按模板读取，性别在可用选项中均衡随机并保存。

## 尚未实现

- 游记入口、日常副本与竞技的完整官方界面还原。游记页仍缺部分原始图像素材；详情存在未绑定内容、默认外语文本和遮罩问题。
- 排行榜队伍头像、首通队伍展示与个人记录区域尚未完成；Type 57 可领取状态的宝箱特效，以及副本奖励图标的原详情弹窗和附属交互尚未恢复。
- 匹配倒计时、在线目标更新和服务器榜单不属于已迁移流程；完整的原生界面输入、战斗结算、关闭重开及冷启动存档恢复仍需接通并实际验证。
- 本功能尚未完成 Unity Play Mode／Windows Player 的全部功能与视觉还原验证；上述结算专项通过不代表完整副本功能完成。
- 本地档案尚未接入官方 `PlayerBenefitManager` 的有效期与 `DailyDungeonExtraClaim` 数据源；公共模块已保留 `IDailyDungeonAwardTimesAdapter`，未接入时按基础 1 次结算。奖励找回数据及服务器批量领奖上限配置仍有缺项。

## 代码与资源入口

[DailyDungeonModule.cs](../../unity/Assets/Game/Runtime/Offline/World/DailyDungeonModule.cs)、[OfflineDailyDungeonModule.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineDailyDungeonModule.cs)、[OfflineDailyDungeonSession.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineDailyDungeonSession.cs)、[DailyDungeonBattleEntryModule.cs](../../unity/Assets/Game/Runtime/Offline/World/DailyDungeonBattleEntryModule.cs)、[OfflineArenaSession.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineArenaSession.cs)、[DailyDungeonBattleRunner.cs](../../unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs)。

[日常副本规则与资源](../../assets/reference/dungeons-runtime-source-manifest.json)、[竞技场来源版本与范围](../../assets/reference/SOURCES.md)、[副本界面绑定](../../assets/reference/dungeon-ui/prefab-binding-source-manifest.json)。
