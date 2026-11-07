# 幻想阶梯与历程回响

[总索引](../README.md)

状态：未完成

## 功能简介

月影之森、魔能邃渊、云流漩涡和锁妖塔各自保存通关层数，共 585 层。普通挑战只允许进入当前层；游记入口、楼层界面、战斗、奖励与自动推关均使用本地状态。

家园游记通过原 `OldPlayEntryBtn` 打开。原 `DemonBattleEntry` 默认隐藏，与伙伴冒险共用卡片位置，继续保持隐藏。

## 原始官方实现

官方配置定义四塔入口、逐层关卡、阵型、挑战条件、奖励及自动推关。当前进度按 `PlayerStageDemonBattlefieldUtils.GetLayer(EntryIndex)` 读取指定入口已结算层数；其他入口的胜场或未结算胜利不推进本塔。

普通挑战使用功能 2 的入口条件，预览使用功能 2 的 `key0=-1` 条件；自动推关另用功能 93 条件。普通挑战要求当前层，不额外增加体力或人物等级门槛。

## 完整还原部分

- `OfflineFantasyTowerSession` 按角色槽保存四塔进度、前后台战斗检查点、自动推关状态和待领取结果。
- `OfficialFantasyTowerCatalog` 读取官方关卡、编队、技能和奖励配置。怪物按 `StageMonsterArrayInfoParser.GenerateMonsters` 展开，小数等级沿 `LevelPropParser.GetPropsByFloat` 与 `BattleFormulaHandler.LerpProps` 计算。
- `FantasyTowerBattleRunner` 将当前层或存档检查点接入共用战斗控制器；前台胜负更新层数，奖励按官方权重、数量、等级和阶位倍率结算。
- 自动推关逐层执行实际战斗并累计配置奖励；停推、失败或完成时生成待领取结果，通过本地事务领取一次。打开家园模态界面时保存战斗检查点并转入后台，关闭模态界面后恢复。
- 楼层入口和塔面板使用原 prefab、行布局、图标与战斗结算资源；界面来源见[塔 UI 来源清单](../../assets/reference/tower-push/ui-source-manifest.json)。

## 自定义实现

- 离线开放条件遵循[单机政策](offline-access.md)，不依赖服务器阶位、开服时间或赛季日历；仍检查本地地图和塔进度。
- 自动推关要求所选入口已通关三层。关闭游戏时自动推关暂停，重新打开不按离线时长补算楼层；未完成战斗检查点与未领取结果继续保存。
- 按用户确认，战斗 HUD 的主动退出为放弃当前闯关：本层不推进、不发胜利奖励，清除前台检查点并关闭自动推关。挂机使用独立的后台推关入口，继续执行实际战斗直到失败才停止，已有层数和本批待领取奖励保留；主动退出不会转为挂机。退出按钮与原生确认分支由[公共战斗模块](battle-execution.md)维护，原 `DemonBattlefieldMainPanelLeaveHandler` 对本玩法直接离开，不弹出通用失败确认。
- 客户端没有服务器榜首身份时不播放 `FirstPlayer`／`win_03` 专属表现，普通当前层继续使用原 `CurLevel`／`portal_01` 序列。

## 尚未实现

- 塔选择、楼层和结算界面的原始布局、文本、滚动输入与遮挡仍有缺项；`DemonBattlePassCondition` 前塔／成就条件尚未接入。
- 楼层 `Init`、`Passing`、`Passed` 状态动画及原入离场粒子未接入。角色 `portal_01` 仅覆盖已有动作数据的部分皮肤；其余皮肤暂用自身 idle 网格，不等于原动作变形。
- 公共宿主的主动放弃与后台真实失败回归已通过内置屏 Play Mode：模拟原退出按钮、检查本层不推进／不发胜利奖励，后台执行至失败并冷重开核对停推、清除检查点及单个待领取失败批次。已查看返回画面，仍有既有人物／贴图缺项；该回归未覆盖塔页完整操作与奖励页表现。
- 尚未完成 Unity Play Mode 全流程验证或发布 Windows Player。

## 代码与资源入口

[OfflineFantasyTowerSession.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineFantasyTowerSession.cs)、[FantasyTowerBattleRunner.cs](../../unity/Assets/Game/Runtime/Presentation/Home/FantasyTowerBattleRunner.cs)、[OfficialFantasyTowerCatalog.cs](../../unity/Assets/Game/Runtime/Presentation/World/OfficialFantasyTowerCatalog.cs)、[HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)。

[塔 UI 转换器](../../scripts/prepare_tower_push_ui.py)、[塔规则转换器](../../scripts/prepare_tower_push.py)、[塔 UI 来源清单](../../assets/reference/tower-push/ui-source-manifest.json)、[官方来源总表](../../assets/reference/SOURCES.md)、[PlayerStageDemonBattlefieldUtils](../../.tools/source-packages/official-code/common-source/Common/PlayerStageDemonBattlefieldUtils.cs)。
