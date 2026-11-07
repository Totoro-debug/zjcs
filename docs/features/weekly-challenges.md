# 虚空裂隙与混沌侵袭

[总索引](../README.md)

状态：局部完成。本地关卡战斗、检查点及原生魔王结算已接入；活动页、周期奖励和地图实体流程仍未完成。

## 功能简介

游记每周活动包含虚空裂隙与混沌侵袭；魔王是混沌战斗阶段之一，不是独立的无限挑战。具体离线开放条件见[离线开放条件](offline-access.md)。

## 原始官方实现

两类活动使用各自的期次、开放与结算时限、阶段顺序、奖励和关卡配置，不沿用日常挑战的逻辑日。混沌奖励、保底、魔王战斗和家园商店按官方客户端与配置处理；服务端活动实体调度未从客户端取得。

所选版本 `DemonInvade.ChallengeVaildCount=1`、`ChallengeAwardRuleId=43000`；原奖励规则为固定 `300 × 41021`，没有额外条件、等级／阶位数量缩放或共享记录索引。`prepare_demon_challenge_award.py` 核对这些分支后生成 `Resources/WeeklyChallenges/demon-challenge-award.json`，由本地首次结算事务读取。输入、输出和处理方法见[挑战奖励来源清单](../../assets/reference/battle-rules/demon-challenge-award-source-manifest.json)。

魔王结算原入口为 `GameEndPanelDemonKing`，由 `GameEndPanelBoss`、`GameEndPlayerSettleGroup`／`Item`、`AwardContent`／`AwardLoopScrollRect` 和原奖励图标工厂组成。统计行沿 `FightUtilsHandler` 汇总本体、召唤物与幻兽的伤害及护盾伤害；期次累计沿下述已确认的实际生命伤害规则。`MonsterIcon_3803` 使用 `TempRTUI` 展示 `MonsterModel_3803_RT`，直接绑定图集材质，没有 `AppearanceRendererLite` 或贴图加载器；其整体材质替换和两份 `CustomRenderExistingMesh` 保留在原层级中。

挑战奖励 41021 沿 `ItemDetailDialogFactory` 的普通物品分支打开原 `ItemDetailDialog`：可堆叠、显示背包数量，不可使用，没有数量上限或产出加成组。原物品来源代理不为它生成来源入口；地图来源另从 APK 的 `config_luban_map` 解码 `item_map_location` 后核对。`ItemIcon` 即使没有中心提示也会创建 `HintTip`，`ItemAwardIcon` 即使不显示额外掉落也会创建 `ExtraGetTip`；两者沿原 `ItemIconAttachmentConfig` 的 GUID 加载原 prefab，不能跳过附件池。物品详情的空白关闭使用 `MainCanvas` 默认窗口容器内的原模糊遮罩，不启用隐藏的分享按钮代替关闭。

`GameEndUIHandler` 在最后演出与结果均已到达后显示结算，普通结算沿 `GameEndDelayTime=0.8` 秒，回合限制沿 `GameEndOverRoundDelayTime=2` 秒。此时保留原战场；`GameEndPanelDemonKing.OnLeaveGameBtnClicked` 在存在战斗根时调用离开战斗，之后才返回地图。

## 完整还原部分

- `OfflineWeeklyChallengeBattleCatalog` 加载虚空与混沌关卡；`WeeklyChallengeBattleRunner` 接入公共战斗执行器。
- `OfflineWeeklyChallengeSession` 按角色存档保存周期、阶段次数与胜场、魔王伤害和战斗检查点；重新进入可恢复已保存的战斗。
- 魔王 4800 使用原怪物与战场配置；战斗结果从公共战报读取，完成后保存本期胜场和伤害。
- 魔王每期首次结算发放原规则 43000 的固定奖励，数量与资格状态落盘；同一期后续战斗及同一检查点重复结算不重复发放。
- 魔王完成回调打开原完整结算面板，保留结束时的战场与原 0.8／2 秒等待，显示原 3803 模型、本人统计和已入账奖励；原按钮打开公共战报与普通物品详情，原离开按钮完成战后交接。
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
- Windows 中文字体沿原 CN 区域配置选择字体与 TTF。`OfflineUiFontInstance` 为窗口创建字体、材质和图集副本，保留原参数和初始像素；运行时字形缓存写入自有副本，关闭窗口时释放这些副本。
- `DemonResultWindow` 接收实际完成的检查点、公共战报和已入账挑战奖励，使用原完整 prefab 及默认窗口容器的模糊遮罩。结算期间保留战场，原离开按钮关闭后释放战斗宿主并恢复家园；物品详情使用独立的原遮罩。文字按实例绑定字体副本，避免窗口释放影响家园文字；战报沿[公共战报生命周期](battle-execution.md)读取最终快照。
- `OfflineDemonResultItemBindings` 只读取已经持久化的挑战奖励与当前背包；点击图标或查看详情不再次发奖。该适配限定原 41021 普通物品分支；可使用、过期、分解、产出加成及 GM 管理分支尚未迁完，遇到不支持的配置会明确报错。模糊遮罩复用同一 884 版本的伙伴弹窗资源与 `PartnerWindowBlurCapture`，按原十三分之一尺寸和 shader 模糊流程适配 Windows Overlay Canvas 截屏。

## 尚未实现

- 游记正式活动页和完整周期奖励领取流程；原泛光合成与回合限制结算提示尚未接通。
- 虚空与混沌的活动实体调度、方向指引、跨次保底与自动推进的地图接线。
- 家园商店和商人的完整运行接入。
- 本功能全流程与 Windows Player 构建、实际运行和发布尚未完成。

## 当前验证范围

- 本地期次、旧检查点恢复、跨期归属、无发奖绑定、发奖后中断与冷重试检查通过；首次奖励使用原固定 `300 × 41021`，角色存档保留唯一的期次收据。
- 实际周常完成回调的内置屏 Play Mode 回归通过：模拟原开始、退出及确认按钮，零步骤／零伤害结算领取首次奖励，关闭宿主页并从磁盘重开后再次退出不重复发奖。两次结算均使用生产窗口，检查原模型与中文、最终快照、两次打开真实战报、原奖励详情、原模糊遮罩关闭及原离开按钮；查看详情或战报不改背包、资格或已结束检查点，离开后才释放战场并恢复家园。
- 原 10011／4800 身体边缘近战与冷恢复后的部分结算通过：受控生命／攻击夹具沿原公式产生实际伤害，结算和公共战报对应同一最终记录，原技能页及只读详情可模拟点击，反复关闭／重开保留最终快照。已查看接近、命中、结算、战报、技能与物品详情和返回画面；此夹具不证明魔王战斗平衡，完整身体格规则与覆盖范围见[战斗执行](battle-execution.md)。
- 原结算 prefab 无丢失脚本，3803 的 28 个变换节点、整体材质替换、两份网格复制与原 `(0,0,1,1)` UV 已核对；1024×1024 离屏纹理、Animator 网格和 14 份粒子系统均有实际运行画面。固定骨骼后关闭粒子可改变输出像素；关闭／重开与相机、纹理、注册释放通过，共用路径的原 3706 模型回归通过。结算源资源 330 文件哈希与来源清单一致，动态字形缓存未改写源图集。
- Unity 导入／编译、`ProjectTools.Validate` 与静态元数据检查通过（108416 项 GUID）；上述 Play Mode 使用隔离工程和存档，Windows Player 尚未构建／运行或发布。
- 已查看实际截图：结算与详情文字、奖励品质框／图标／数量和窗口层级正常，战报使用窗口自己的字体，避免宿主页全局字体覆盖。返回家园仍有既有头像、提示箭头与贴图缺项；原五个音频事件已绑定 PCM 与事件 ID，未验证实际音效输出。此范围不包含正式活动页入口、原泛光、超时提示或 Windows Player。

## 代码与资源入口

[OfflineWeeklyChallengeBattleCatalog.cs](../../unity/Assets/Game/Runtime/Presentation/World/OfflineWeeklyChallengeBattleCatalog.cs)、[OfflineWeeklyChallengeSession.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineWeeklyChallengeSession.cs)、[WeeklyChallengeBattleRunner.cs](../../unity/Assets/Game/Runtime/Presentation/Home/WeeklyChallengeBattleRunner.cs)。

实际交接由 [DemonResultWindow.cs](../../unity/Assets/Game/Runtime/Presentation/Home/DemonResultWindow.cs) 管理；结算控制代码位于 `Runtime/Official/BattleResultUI/`，普通物品详情代码位于 `Runtime/Official/ItemDetailUI/`，本地背包和原图标附件池由 [OfflineDemonResultItemBindings.cs](../../unity/Assets/Game/Runtime/Presentation/Home/OfflineDemonResultItemBindings.cs) 绑定；资源入口为 [OfficialDemonResultAssets.cs](../../unity/Assets/Game/Runtime/Presentation/Home/OfficialDemonResultAssets.cs) 与 `Assets/Official/Combat/DemonResult*`，字体缓存隔离位于 [OfflineUiFontInstance.cs](../../unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineUiFontInstance.cs)。原始对象、重绑定与输出哈希见[结算资源清单](../../assets/reference/demon-result-native-source-manifest.json)、[魔王模型清单](../../assets/reference/battle-animation/demon-result-rt-model-source-manifest.json)和[原模型层级清单](../../assets/reference/battle-animation/demon-result-rt-prefab-source-manifest.json)。

关卡来源见[周常战斗清单](../../assets/reference/weekly-challenges/battle-stages-source-manifest.json)；官方客户端来源见[来源总表](../../assets/reference/SOURCES.md)。
