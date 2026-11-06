# 12 山之国

[总索引](../README.md)

状态：未完成

## 地图简介

山之国使用官方世界地图、主线、日常副本、战斗碑、固定宝箱及机关配置。地图入口、状态和跨图规则见[主线地图公共模块](../features/mainline-map-modules.md)；战斗及助战规则见[战斗执行](../features/battle-execution.md)。

## 原始官方实现

地图 ID 为 12，目录为坎斯汀大陆／山之国，场景类型为 `map_world`。官方来源固定为 `871_156598`；原配置、代码、场景、UI、地形与音频来源分别记录于 [Map12 来源清单](../../assets/reference/official-map/map12-source-manifest.json) 和 [Map12 Unity 转换清单](../../assets/reference/official-map/map12-unity-source-manifest.json)。

客户端包含升降台、推箱、华容道、固定宝箱、战斗碑及私有日常副本入口。服务端未实现的行为按本页“自定义实现”列出的已确认方案处理；其余缺失规则不作推测。

## 完整还原部分

- `OfficialMainlineMapRuntime` 已接入本图地图状态、原生主体、地形分区及区域环境资源；地图交互仍依赖正式入口、角色移动和完整 HUD 接线。
- 主线实体操作、剧情空间、区域标签、固定宝箱数据、机关目录及本地图部分战斗数据已有运行载荷或本地规则接线。原始配置和绑定入口见上述来源清单。
- 战斗碑已接入本地窗口上下文、目标身份、原奖励预览输入和队伍事件生命周期；正式 Team 页面、助战队伍及完整战斗入口仍未接通。
- 私有副本复用[日常副本](../features/dungeons.md)的路线展示与[战斗执行](../features/battle-execution.md)的公共战斗宿主；本图提供私图路线和房间状态。
- 共享实体探索记录接入[主线地图公共模块](../features/mainline-map-modules.md)。Map12 专用 UI、迷你地图、动画与音频载荷仍需在正式地图流程内接通。

## 自定义实现

- Map12 私图 102 的服务器战斗等级生成逻辑未取得。按用户确认，建筑等级取区域等级加 `Lo.Level`；`Lo.SubRank=0` 继承区域阶位，非零值使用显式阶位；怪物等级按建筑等级、关卡组件和阵型位置偏移计算，保留原怪物属性公式。
- 用户确认的技能选向规则由 `FightBattleTargeting.SelectInSkillRange` 接入：沿用原候选优先级，由范围内首个目标确定方向，再保留同向范围内目标；不代表恢复服务器 NPC 的完整 AI。
- 推箱沿用客户端移动键、体型、目标和可移动范围。用户确认成功推动后角色跟进至箱子原格；四处锁箱机关在指定箱子占满目标格后保持解锁，并执行原状态更新，不自动领取奖励。华容道重置恢复原清单中的箱子／障碍并将角色移至终端旁可站立格。
- 固定宝箱相对等级沿用区域配置；阶位 0 继承所在区域，非零阶位取显式配置。奖励、随机状态、开箱次数和完成标记同笔保存。
- 私有副本路线使用独立进度；按已确认方案，不发副本奖励、不修改公共挑战次数或购买次数。房间、战斗快照与助战信息分别保存在路线档案和角色档案中。
- 原助战候选缺少头像资源时，用户确认保留官方姓名并以文字显示，不制作替代头像。

## 尚未实现

- 完成正式地图入口、连续移动、主线剧情、区域探索、机关、战斗、奖励与地图出口的整条游玩流程。
- 接通完整原生 HUD、迷你地图、战斗碑 Team／NPC 邀请页、私图详情与路线控件、原生动画及全部资源绑定。
- 接通私图选路倒计时、难度和奖励展示、实际助战队伍、战斗胜负与失败回滚；完成退出、冷读恢复和完整存档流程。
- 正式地图画面、模拟输入、遮挡与战斗返回流程尚未完成验收；本图 Windows Player 尚未交付。

## 代码与资源入口

- 地图宿主：[OfficialMainlineMapRuntime.NativeMap12.cs](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeMap12.cs)。
- 公共路线与规则：[日常副本](../features/dungeons.md)、[战斗执行](../features/battle-execution.md)、[主线地图公共模块](../features/mainline-map-modules.md)。
- 配置与资源来源：[Map12 来源清单](../../assets/reference/official-map/map12-source-manifest.json)、[Map12 Unity 转换清单](../../assets/reference/official-map/map12-unity-source-manifest.json)、[剧情载荷来源](../../assets/reference/official-map/map12-runtime-source-manifest.json)。
