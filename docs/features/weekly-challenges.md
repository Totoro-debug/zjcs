# 虚空裂隙与混沌侵袭

[总索引](../README.md)

状态：局部完成。本地关卡战斗与检查点已接入；活动页、周期奖励和地图实体流程仍未完成。

## 功能简介

游记每周活动包含虚空裂隙与混沌侵袭；魔王是混沌战斗阶段之一，不是独立的无限挑战。具体离线开放条件见[离线开放条件](offline-access.md)。

## 原始官方实现

两类活动使用各自的期次、开放与结算时限、阶段顺序、奖励和关卡配置，不沿用日常挑战的逻辑日。混沌奖励、保底、魔王战斗和家园商店按官方客户端与配置处理；服务端活动实体调度未从客户端取得。

所选版本 `DemonInvade.ChallengeVaildCount=1`、`ChallengeAwardRuleId=43000`；原奖励规则为固定 `300 × 41021`，没有额外条件、等级／阶位数量缩放或共享记录索引。`prepare_demon_challenge_award.py` 核对这些分支后生成 `Resources/WeeklyChallenges/demon-challenge-award.json`，由本地首次结算事务读取。输入、输出和处理方法见[挑战奖励来源清单](../../assets/reference/battle-rules/demon-challenge-award-source-manifest.json)。

魔王结算原入口为 `GameEndPanelDemonKing`，由 `GameEndPanelBoss`、`GameEndPlayerSettleGroup`／`Item`、`AwardContent`／`AwardLoopScrollRect` 和原奖励图标工厂组成。统计行沿 `FightUtilsHandler` 汇总本体、召唤物与幻兽的伤害及护盾伤害；期次累计沿下述已确认的实际生命伤害规则。`MonsterIcon_3803` 使用 `TempRTUI` 展示 `MonsterModel_3803_RT`，直接绑定图集材质，没有 `AppearanceRendererLite` 或贴图加载器；其整体材质替换和两份 `CustomRenderExistingMesh` 保留在原层级中。

## 完整还原部分

- `OfflineWeeklyChallengeBattleCatalog` 加载虚空与混沌关卡；`WeeklyChallengeBattleRunner` 接入公共战斗执行器。
- `OfflineWeeklyChallengeSession` 按角色存档保存周期、阶段次数与胜场、魔王伤害和战斗检查点；重新进入可恢复已保存的战斗。
- 魔王 4800 使用原怪物与战场配置；战斗结果从公共战报读取，完成后保存本期胜场和伤害。
- 魔王每期首次结算发放原规则 43000 的固定奖励，数量与资格状态落盘；同一期后续战斗及同一检查点重复结算不重复发放。
- 公共战斗执行和检查点规则见[战斗执行](battle-execution.md)。

## 自定义实现

- 用户确认活动周期改为每周日 08:00 开放，保留原阶段顺序及时长；混沌累计改为本人当期实际魔王伤害，原累计门槛按十分之一使用，奖励不变。
- 魔王 4800 按玩家本体及其召唤物、幻兽的实际生命伤害累计；周期首次入场时固定等级与阶位，查看或领取旧期奖励不补造历史。
- 魔王等级按原 NPC 表的世界阶段最高等级取值：1 至 8 阶为 10、40、70、170、250、400、400、999；更高阶回退到 999。
- 挑战按单人模式运行，隐藏真人组队和联网排名；官方未配置此活动的 NPC 邀请规则，因此不添加 NPC 助战。
- 服务端实体调度缺失时，按用户确认的可用地图点位本地生成、每 5 秒维护并保存目标批次与截止时间。活动比例与延迟沿原配置处理，此行为是单机适配。
- 官方保底计数改为角色级长期保存，不按周清空；虚空层间概率沿原规则，并由本地持久化计时推进。
- 用户确认混沌商店在本期结算开始时开放 24 小时，创建、首次访问和离场操作写入本地存档。家园商人按当前门的绘制顺序显示在门前，保留原变换与行为。
- 用户确认混沌侵袭中途退出正常结算并发相应奖励。原 `DemonKingMainPanelLeaveHandler`／`LegionBossMainPanelLeaveHandler` 的战斗中退出使用 `PartEndGame`；本地 `WeeklyChallengeBattleRunner` 接入公共部分结算，保留已经造成的生命伤害，经正常战报交接后记入当期伤害并清除本场检查点。部分退出不伪造胜利或击杀；首次挑战奖励已接线，其余奖励与页面缺项见下方。
- 用户确认魔王每期首次结算即领取挑战奖励，零伤害主动退出也消耗资格并领取；次数上限沿 `DemonInvadeConfig.ChallengeVaildCount=1`，奖励沿 `ChallengeAwardRuleId=43000`。这是缺失服务端资格裁决的单机决策。`OfflineWeeklyChallengeSession.CompleteBattle` 先准备期次变更，通过 `HomeFeatureRuntime.GrantBattleReward` 将物品与稳定的期次收据一起提交到角色存档，再保存领取次数、伤害与检查点清除；中断重试使用同一收据，不重复加物品。期次写入失败保留当前会话的原检查点；跨期恢复仍结算到入场期次，非魔王战斗不消耗此资格。
- 包内 4800 配置的 `LeaveInPlaying` 为 false；单机按上述已确认退出方案只为该关卡启用战斗中退出，不修改原始配置，也不将此开关标为官方服务端行为。准备阶段仍保留原退出限制。

## 尚未实现

- 游记正式活动页、原生结算面板的战报／奖励宿主接线和完整奖励领取流程。`prepare_demon_result_native.py` 已恢复完整结算 prefab、统计行、奖励容器、默认奖励／物品图标及原文本、字体、材质和动画依赖，并迁入相关官方 UI 控制代码；`OfficialDemonResultAssets` 显式绑定这些资源。`prepare_demon_result_rt.py` 将原魔王结算模型接入共用原生骨骼／Animator 与 Windows 资源路径。此项资源恢复不表示结算流程已经接通。
- 虚空与混沌的活动实体调度、方向指引、跨次保底与自动推进的地图接线。
- 家园商店和商人的完整运行接入。
- 魔王部分退出的公共宿主回归已通过隔离内置屏 Play Mode：原确认框模拟点击、实际技能命中后退出、保留当前步骤、战报先于结果、当前期次伤害持久化及清除检查点；已查看确认和返回画面。该回归未覆盖正式活动入口或原生结算页，且使用隔离数值与原长射程技能保证可观测命中，不证明魔王战斗平衡；身体格选敌验证见[战斗执行](battle-execution.md)。
- 首次挑战奖励的内置屏 Play Mode 回归通过：模拟原开始与退出确认按钮，在零步骤／零伤害下部分结算，角色背包增加 `300 × 41021` 且只有一条期次收据；关闭宿主页并从磁盘重开后再次退出，同期不再发奖，资格与检查点状态正确。期次、旧检查点恢复、跨期归属、无发奖绑定、发奖后中断和冷重试检查通过；Unity 编译、`ProjectTools.Validate` 与静态元数据检查通过。已查看两次原确认框和冷重开画面；返回家园仍有既有人物／头像与贴图缺项，未验证音效输出。该变更未完成 Windows Player 构建、运行或发布；原生魔王结算面板仍未接线。
- 本功能尚未完成 Unity Play Mode 全流程验证和 Windows Player 发布。
- 原生结算资源在内置屏 Play Mode 验证：完整 prefab 无丢失脚本，3803 原 RT 参数、28 个变换节点、原整体材质替换、两份网格复制的引用和启用状态正确；1024×1024 离屏纹理与实际 Game View 均有可见模型，原 Animator 的顶点及像素随时间变化，原 14 份粒子系统正常推进，固定骨骼姿态后关闭粒子会改变 193993 个像素，已查看星空纹理与流动效果；关闭／重开及相机、纹理与注册释放通过。恢复原生 YAML 的 Rect 版本标签，保留原 `(0,0,1,1)` UV，修复二进制字段恢复后的黑屏。共用路径的既有 3706 模型回归通过，原外观绑定、四页图集、20 个跟随节点和 Animator 网格继续工作；Unity 编译、`ProjectTools.Validate` 与 134557 项 GUID 静态检查通过。此验证未激活完整结算面板，未覆盖奖励点击、统计／战报交互、泛光合成或音效，也未构建 Player。

## 代码与资源入口

[OfflineWeeklyChallengeBattleCatalog.cs](../../unity/Assets/Game/Runtime/Presentation/World/OfflineWeeklyChallengeBattleCatalog.cs)、[OfflineWeeklyChallengeSession.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineWeeklyChallengeSession.cs)、[WeeklyChallengeBattleRunner.cs](../../unity/Assets/Game/Runtime/Presentation/Home/WeeklyChallengeBattleRunner.cs)。

结算控制代码位于 `Runtime/Official/BattleResultUI/`；资源入口为 [OfficialDemonResultAssets.cs](../../unity/Assets/Game/Runtime/Presentation/Home/OfficialDemonResultAssets.cs) 与 `Assets/Official/Combat/DemonResult*`。原始对象、重绑定与输出哈希见[结算资源清单](../../assets/reference/demon-result-native-source-manifest.json)、[魔王模型清单](../../assets/reference/battle-animation/demon-result-rt-model-source-manifest.json)和[原模型层级清单](../../assets/reference/battle-animation/demon-result-rt-prefab-source-manifest.json)。

关卡来源见[周常战斗清单](../../assets/reference/weekly-challenges/battle-stages-source-manifest.json)；官方客户端来源见[来源总表](../../assets/reference/SOURCES.md)。
