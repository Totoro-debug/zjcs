# 虚空裂隙与混沌侵袭

[总索引](../README.md)

状态：未完成

## 功能简介

游记每周活动进入虚空与混沌。魔王是混沌战斗阶段的一部分，不是独立无限挑战。

## 原始官方实现

虚空裂缝、魔物侵袭等每周挑战分别使用原期次、开放／结束、首领、奖励与原界面配置，不能套用每日挑战的重置规则。

### 待迁移的规则与表现

原活动阶段、停止挑战边界、奖励条件和关卡配置保留，已入场战斗允许结算。虚空挑战24小时；混沌战斗14小时后奖励结算10小时。

混沌奖励沿原 `DemonInvadeUtils`、`DemonInvadeStepAwardView`、`DataHub.Supply` 读取等级／阶位及嵌套随机。魔王4800已接真实 `fight:demon_invade:4800` 事务，使用原怪物／场景与100混沌之息准入。

原51档个人累计奖励按配置保留奖励内容、领取去重和手动领取，未领取可恢复。

游记入口使用原 `VoidFissurePanel`、`DemonInvadePanel`，绑定原活动状态切换、倒计时、奖励次数、最高伤害与横向奖励进度条。规则按钮打开各自原窗口；混沌51档节点使用原节点尺寸、布局及领取状态。领取走真实事务，保存失败回滚，重复点击不重复发奖。前往按钮按 `Player.LastActiveRegionByGoddess` 和 `EntityHandler.FindRegionNearEntry` 寻找最高已激活女神区域及邻接区域可用入口。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

用户确认虚空／混沌由原14天周期改为每周日08:00开放，原阶段顺序和时长不改；服务器开放条件另按[共同政策](offline-access.md)处理。

混沌公共累计改为本人当期实际魔王伤害，原门槛统一除以10，首档2500万变250万，奖励不变。每期首次实际进入普通侵袭或魔王时固定个人等级／阶位快照，查看、预览和旧期领取不补造历史。

魔王等级生产公式未取得，用户确认采用原NPC表各世界阶段最高值：1～8阶为10、40、70、170、250、400、400、999；更高阶按原最大配置回退999，不外推。已保存战斗保留入场参数。

用户确认虚空／混沌保留单人挑战并隐藏真人组队入口。原 `RobotInvitePlayType` 和 `player_robot_invite` 没有这两项的 NPC 邀请配置，不扩展 NPC 规则。混沌页隐藏联网排名页签、名次及排名结算区，累计伤害标题、规则说明按已确认的本人累计口径显示。

客户端／公共程序集没有服务端活动实体调度。用户确认在已解锁区域按原可用点位本地生成并随存档保存，5秒检查补充并清理旧期实体。用户另确认按 `DemonAppearProportions` 推定每批比例、按 `DemonAppearDelay` 区间随机并以小时解释延迟；本地按地图／周期／活动保存目标批次与下次截止时间。此分批时序是获准的离线推定实现，不代表已取得服务端调度代码或确认原服务端单位。

官方保底记录以 `teamId` 索引；用户确认离线存档按角色长期累计。实现保留官方计数与阈值算法，存档键以本地角色存档替代服务器 `teamId`，不按周清空。

用户确认保留原层间概率且实现自动推进。服务端下一层调度由本地5秒计时与持久化截止时间替代，关闭再打开后继续，不重新抽取已保存的层数结果。

服务端创建混沌商店的时刻未取得，用户确认从每期混沌进入结算阶段时开放，持续原 `shop[102].Duration=86400` 秒。商店创建、首次访问与请离请求改为本地存档事务；货架、购买规则、家园出场位置与首次剧情仍采用原配置和客户端调用顺序。

商人第0号原生成点与默认家园门重叠，原模型层级6／5低于门的6／10。已核对 `HomeNpcManager`、`HomeNpcBase`、`HomeNpcModel`、`AppearanceRendererLite`、主场景及地形 prefab，尚未找到原运行时避让或层级修正。用户确认将商人绘制在门前： 的 `install` 按当前 `Mid_HomeGate` 实际绘制键调整商人顺序，家园重建后重新应用；保留原位置、大小、动画、碰撞及红点。检查入口 覆盖初次加载、重建后的门前顺序和原模型变换。

## 尚未实现

- 虚空、混沌、阶段实体、地图方向、单人实战、跨次保底、自动推进、本人伤害累计和家园商人。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。

## 代码与资源入口

`scripts/prepare_weekly_metadata.py`（来源工具标识）、[VoidFissureGuiderManager.cs](../../.tools/source-packages/official-code/client-source/VoidFissureGuiderManager.cs)

[MapDemonInvadeDirectionGuiderItemProvider.cs](../../.tools/source-packages/official-code/client-source/MapDemonInvadeDirectionGuiderItemProvider.cs)、[DirectionGuiderDisplayManager.cs](../../.tools/source-packages/official-code/client-source/DirectionGuiderDisplayManager.cs)、[ShowMapRequireDemonInvade.cs](../../.tools/source-packages/official-code/client-source/ShowMapRequireDemonInvade.cs)、`scripts/prepare_challenge_results.py`（来源工具标识）

[challenge-results/source-manifest.json](../../assets/reference/challenge-results/source-manifest.json)、

`scripts/prepare_weekly_merchant.py`（来源工具标识）、[weekly-merchant/source-manifest.json](../../assets/reference/weekly-merchant/source-manifest.json)、
