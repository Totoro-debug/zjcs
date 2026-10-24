# 幻想阶梯与历程回响

[总索引](../README.md)

状态：未完成

## 功能简介

月影之森、魔能邃渊、云流漩涡、锁妖塔各自保存层数，合计585个原可挑战层；通关原功能93要求的楼层后，可在战斗中开启自动推关并切出后台。

入口由家园游记的 `DemonBattleEntry` 打开。四个入口分别保存最高通关层；普通挑战只能启动当前层。四塔合计585层，战斗、奖励、自动推关和后台结果均在本地保存。

## 完整还原部分

已接通入口、逐层挑战、共用官方战斗执行器、官方奖励配置、本地存档、自动推关、后台检查点与结果领取。尚未通过 Unity Play Mode 输入、画面及存档恢复验收，不能据此标记模块完成。

## 自定义实现

以下单机处理已接入；尚未验证 Unity Play Mode 全流程。

原逐层战斗和奖励由共用本地执行器与保存事务连接，入口、层数与原Layer编号保持一致；世界阶段的服务器开放限制按[单机政策](offline-access.md)处理。

`Zjcs.Offline.World.OfflineFantasyTowerSession` 已由 `HomeFeatureRuntime.Open` 按角色槽位初始化；它保存四个入口进度、前后台战斗检查点和待领取后台结果。`FantasyTowerBattleRunner` 已接入 `OfficialBattleRuntimeController`，从当前层或检查点构造战斗并保存每个行动，胜利后按官方层奖励规则和稳定交易 ID 写入本地库存，失败保留已通关层。官方 `GameEndPanelDemonBattlefield` 已被复用作前台胜负面板，显示当前层与配置奖励；胜利后自动推关按5秒间隔继续，自动按钮使用所选入口的第3层条件。家园中打开任意模态页会把自动推关战斗切至后台检查点并隐藏战场；返回无模态界面时恢复同一战斗。停推、失败、完塔和重开存档都会生成最早优先的待领取结果，结果通知页通过本地库存事务领取一次。

`scripts/prepare_tower_push.py` 按固定来源 `854_156383` 解码原始 `stage_demon_battlefield`、`stage`、`stage_monster_array` 和 `skill` Luban 表，并复用官方 `StageMonsterArrayInfoParser.GenerateMonsters` 分组顺序、`LevelPropParser.GetPropsByFloat` 属性插值与 Map10 怪物公式，导出四入口 Layer 1–585 的战斗网格、编队、怪物属性和技能。同时导出42条层奖励规则及倍率表；`OfficialFantasyTowerCatalog.RollRewards` 按官方百分比、权重、数量和玩家等级/阶位倍率抽取。`scripts/prepare_tower_push_ui.py` 从已核验的 `defaultpackage_assets_prefabs_ui_demonbattlefield.bundle` 转换 `main`、`end`、`result`、`notice`、官方入口、入口项、逐层页和四种塔层行 prefab，写入 `TowerPush/ui.json` 及 UI 来源清单。入口页绑定四塔进度，逐层页按官方层行高度、拖动惯性与回弹设置虚拟化显示，只有当前层可点击开战；正式语言的动态标题与已通关层数由本地进度绑定。

`OfflineFantasyTowerSession` 在打开存档时关闭遗留自动推关状态并保留检查点；开启自动推关要求当前入口已通关至少3层。前台结算由 runner 调用 `CompleteForegroundBattle`，以 `fantasy-tower:stage:{entry}:{stage}` 稳定交易 ID 写入角色库存。后台战斗逐层调用 `CompleteBackgroundLayer` 累积奖励，切回前台、停推、失败或完塔时封存结果；`ShowFantasyTowerPendingResult` 按最早结果展示，确认领取后以本地库存事务发放一次。打开其他家园模态页时将同一战斗检查点切至后台，关闭模态页后恢复。

官方 `function_limit` 类型93的全局开放条件为入口1–4任一入口通过第3层（条件413/423/433/443）；开启所选入口的自动推关则检查该入口自身已通关3层。普通挑战不依赖自动推关条件。已确认的离线开放政策取消服务器阶位、开服时间与地图日历门槛，入口仍使用本地四塔进度。

原后台推关使用服务器持续执行，本地改由游戏生命周期节点调用同一战斗执行器，行动调度沿现有本地前台节奏，胜负、下一层和奖励仍使用实际战斗与原配置。后台战斗和奖励使用独立随机检查点；并行前台战斗的结算基础同步真实层数及已领取奖励，避免覆盖进度。

用户明确确认：“游戏关闭推关自然就暂停了，重新打开游戏也不需要自动续推”。因此重新进入游戏先关闭自动状态、暂停保留的前台战斗，不按关机时长补算楼层；已完成层数、未领取结果及未完成战斗检查点继续保存，需要手动开启才会继续。

用户确认：离线版不触发依赖服务器榜首玩家 ID／楼层的 `FirstPlayer`／`win_03` 专属表现；离线存档不包含该榜单身份，普通当前层继续使用原 `CurLevel`／`portal_01` 序列。

## 尚未实现

- 入口页与逐层页已连接当前层、本地进度、战斗启动与胜负/奖励面板；仍需在 Unity 验证四个入口列表的实测布局、滚轮/拖拽/惯性命中、翻层索引和存档恢复。
- 前后台切换已绑定家园模态页面状态，但尚未通过 Play Mode 验证切出切入期间的战斗队列、随机检查点、连续胜负与领取事务一致性。
- 塔层行的 `Init`、`Passing`、`Passed` 状态动画和原入离场粒子仍未从官方动画依赖恢复接入；当前只按官方层状态切换对应节点显隐。
- 男、女原生角色动画载荷已加入 `map/portal_01`，但仅覆盖资源动作表中对应的91个男性和90个女性皮肤 ID；缺动作的职业外观目前冻结显示其本身官方 idle 网格，不等同于官方 portal 变形。`win_03` 不接入，符合已确认的无榜单身份处理。
- Home 与 Editor 程序集编译通过。`HomeHudView.Dungeons` 的官方内容节点类型及行高读取已修正；塔玩法仍缺 Play Mode 的真实输入、画面、连续结算及存档恢复验证。

### 待迁移的规则与表现

以下表现目标尚未完整迁移；已实现的规则见上文。

入口及四塔楼层列表使用 Unity `ScrollRect` 和官方 viewport、行高与层索引；原 `LoopScrollRectBase` 的速度采样、滚轮灵敏度及0.5秒 `ScrollToCellWithinTime` 对齐行为仍待复原和实测。需要验证入口和楼层按钮命中、拖动不误触、滚动惯性及边缘回弹。

楼层保留原已通关、当前、自动推关和下一层状态，以及四种塔原 UI 动画、入场／离场粒子和玩家 Spine 序列。推关时旧当前层播放原 `Passing` 的 `idle_01` 一次并播放 UI 离场片段；新当前层先播原 UI 入场片段，再播放 `portal_01` 一次并循环 `idle_01`，入场粒子在 UI 片段结束后出现。初次打开按原 instant 状态采样稳定姿势；下一层使用原 `Init` UI 片段，已通关层播放原 `Passed` 片段并隐藏玩家。入／离场 Wwise 事件 `Note_FantasyLadder_Flash`、`Note_FantasyLadder_Elevator` 使用固定原版本转换的本地音频。

男、女动作资源按原 `SplitAnimationSO` 转换 `map/portal_01`；动作缺失时的职业外观回退需替换成可正确变形的原始分体动作。`win_03` 按用户确认不触发。

开放检查原地图、阶位和前塔通关条件，必须挑战当前层；敌人等级为推荐，不添加体力或等级硬锁。奖励取原关卡，未结算战斗先结算，不增加自创经验或祈愿材料。

进度遵循 `PlayerStageDemonBattlefieldUtils.GetLayer(EntryIndex)` 的指定入口最高已结算层语义；其他入口胜场和未结算胜利不计，旧无累计字段的有效记录仍识别。

自动按钮、开关／锁定状态、楼层“推关中”及“切入”按钮采用原 `FightDemonBattlefieldMainPanel`、`StageDemonBattlefieldPanelItem` 和组件绑定。开启时保留当前前台战斗；切出、切入保留实际角色、敌人、行动队列、技能状态和随机检查点，不重新判胜。前台胜负使用原 `GameEndPanelDemonBattlefield`，胜利后按 `DemonBattlefield.AutoPushCountdown=5` 秒继续下一配置层；失败或完成该塔后停止。

后台战胜按原关卡推进层数并累积原奖励，不直接发入背包；切回前台或停止时保留每段推关的起止层、耗时、胜负与奖励。当前已接结果通知、最早结果选择和本地一次性领取；官方 `StageDemonBattlefieldAutoPushResultPanel` 的原奖励格、空奖励及完成状态表现仍需核对。

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

[PlayerStageDemonBattlefieldUtils](../../.tools/source-packages/official-code/common-source/Common/PlayerStageDemonBattlefieldUtils.cs)、[LoopScrollRectBase](../../.tools/source-packages/official-code/loop-scroll-source/UnityEngine.UI.LoopScrollRectBase.decompiled.cs)、[StageDemonBattlefieldPanelItem](../../.tools/source-packages/official-code/client-source/StageDemonBattlefieldPanelItem.cs)、[StageDemonBattlefieldPanelItemPlayerPlayable](../../.tools/source-packages/official-code/client-source/StageDemonBattlefieldPanelItemPlayerPlayable.cs)、[SkeletonGraphicAnimSequence](../../.tools/source-packages/official-code/client-source/AppearanceSystem/SkeletonGraphicAnimSequence.cs)

[AppearanceAnimData](../../.tools/source-packages/official-code/client-source/AppearanceSystem/AppearanceAnimData.cs)、[PlayerStageDemonBattlefieldExt](../../.tools/source-packages/official-code/common-source/Common/PlayerStageDemonBattlefieldExt.cs)、[FightDemonBattlefieldMainPanel](../../.tools/source-packages/official-code/client-source/FightDemonBattlefieldMainPanel.cs)、[GameEndPanelDemonBattlefield](../../.tools/source-packages/official-code/client-source/GameEndPanelDemonBattlefield.cs)、[FightDemonBattlefieldAutoPushEndNotifyUI](../../.tools/source-packages/official-code/client-source/FightDemonBattlefieldAutoPushEndNotifyUI.cs)

[StageDemonBattlefieldAutoPushResultPanel](../../.tools/source-packages/official-code/client-source/StageDemonBattlefieldAutoPushResultPanel.cs)、

[prepare_map_player_actions.py](../../scripts/prepare_map_player_actions.py)、[prepare_female_character.py](../../scripts/prepare_female_character.py)、[player-actions-source-manifest.json](../../assets/reference/official-map/player-actions-source-manifest.json)、[character-genders/source-manifest.json](../../assets/reference/character-genders/source-manifest.json)、[prepare_battle_audio.py](../../scripts/prepare_battle_audio.py)

[battle-audio/source-manifest.json](../../assets/reference/battle-audio/source-manifest.json)、

[prepare_tower_push.py](../../scripts/prepare_tower_push.py)、[prepare_tower_push_ui.py](../../scripts/prepare_tower_push_ui.py)、[prepare_tower_push_audio.py](../../scripts/prepare_tower_push_audio.py)、[界面来源清单](../../assets/reference/tower-push/source-manifest.json)、[UI 输出清单](../../assets/reference/tower-push/ui-source-manifest.json)、[塔层战斗来源清单](../../assets/reference/tower-push/tower-battle-source-manifest.json)、[音频来源清单](../../assets/reference/battle-audio/source-manifest.json)、[原生角色动作清单](../../assets/reference/character-creation/native-mesh-source-manifest.json)


