# 主线地图公共模块

[总索引](../README.md)

状态：未完成（已有主线运行时已提取；下列未迁移玩法仍待接入）

## 功能简介

主线地图通过 `OfficialMainlineMapRuntime.Begin(mapClassId, …)` 使用同一套运行时。目前有完整数据绑定的入口是 Map10、Map11；地图资源、区域条件、剧情、实体和进度仍按地图 ID 隔离。新增地图须先迁移其官方数据与专属流程，再登记 `OfficialMainlineMapProfile`，不能仅替换地图 ID 后宣称地图完成。

## 完整还原部分

已从原 `OfficialMap10GameRuntime` 提取现有实现，公共代码位于 [`Presentation/World/Mainline`](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/)。入口组件保留原 MonoScript GUID，使用 `MovedFrom` 记录类型迁移；角色创建流程与现有编辑器检查统一调用公共类型。

| 模块 | 当前代码与复用范围 |
| --- | --- |
| 地图配置与载荷 | `OfficialMainlineMapProfile` 集中已迁移地图的资源根、原生 UI 根、阶段／故事数量及必须存在的区域条件。`Data` 分部保留各图官方模板、故事图和数量校验。 |
| HUD、顶部栏、任务与功能入口 | `OfficialWorldUiRenderer` 与 `Hud`、`HudControls`、`TopBar`、`Tasks`、`Functions` 分部共用本地状态绑定；Map11 原生 prefab 路径从地图配置解析。Map10 现有 UI 迁移缺项继续保留在所属地图页。 |
| 区域开放 | `OfficialMainlineRegionPolicy` 可独立装载官方 `TbMapRegionInfo`／`TbConditionInfo`。保留原有全探索与剧情次数分支、比较运算、错误拒绝及配置检查；实体显隐、交互锁、小地图和任务消费同一份策略。开放条件不等于已实现雾或公开地区状态转换。 |
| 寻路与移动 | `Navigation` 分部负责屏幕点击、实体命中、交互距离、路径跟随和逐格存档；继续调用 `OfficialWorldMapSession`、`OfficialMapPathfinder` 与 `OfficialMapGroundRules`。桌面输入提供器与原鼠标输入共用遮挡、命中和寻路流程。 |
| 女神像与失落的女神像 | `Goddess` 分部共用探索条件、原生弹窗、按原阶段顺序激活、回执与传送；地区名称按当前地图 ID 查询。当前接入范围为 Map11，珍宝预览等缺项见[森之国](../maps/map-11.md)。 |
| 英雄／魔女访问及魔女阶段 | `OfflineMainlineProgressSession.Statues` 提取女神激活、全图魔女计数、英雄／魔女每日共享额度、奖励、HP 与待战斗效果事务；`Witch` 分部保留 Map11 原书页、预览、激活与周期访问。英雄实际界面尚未迁移。 |
| 探索进度 | `Regions` 分部与既有 `OfficialMapProgressAdapter`、`OfficialMapExplorationProgress`、`OfficialMapProgressCalculator` 共用官方实体状态和探索点计算。 |
| 命运果实生产 | 既有 `OfflineFruitTreeService` 已是与地图无关的公共服务，沿用其生产、容量、时间推进和收取事务；家园入口继续复用，不再为各地图复制一份。命运探寻的地图入口仍未接通。 |
| 小地图与图标 | `MiniMap`、`MiniMapTabs`、`MiniMapMarks`、`MiniMapFilter` 共用已迁移的原面板、图标、跨图浏览、缩放与档案筛选存储；各图几何继续按 ID 加载。 |
| 交互、剧情、战斗与存档 | `Interactions`、`Story`、`Dialogue`、`Battle`、`Persistence` 分部保留既有执行顺序；Map10 首战引导独立在 `Map10` 分部。森之国机关、动态怪物与音频仍消费其专属载荷。 |

`map-{ID}-save.json`、角色进度、回执前缀和 `minimap-filter.json` 的现有格式保持兼容。`Map10Runtime…` 数据 DTO 名称暂保留，避免把运行时代码抽取混入资源／存档结构变更。

官方依据沿用已提取的 `WorldCanvas`、`WorldPanel`、`FunctionEntryDialog`、`MiniMapMaskedUI`、`MiniMapFullScreenPanel`、`GoddessTouchDialog`、`LostGoddessTouchDialog`、`WorldWitchTouchUI`、`WorldMapProgressCondition`、`StoryCountCondition` 以及现有地图导航／进度实现。各图来源与哈希见 Map10、Map11 所属页和原来源清单；动态怪物、战斗场景和时间轴的来源清单及生成器中的运行时代码绑定同步到公共目录，官方输入与素材哈希保持原值。

验证入口为 `Zjcs.Editor.MainlineMapModuleValidation.Run`，使用 `-verify-mainline-modules <隔离输出目录>`；同一检查也可在 Windows development Player 运行。Unity `2022.3.57f1c2` 已通过编译、`ProjectTools.Validate`、主线／雕像事务检查与带图形的 Play Mode 回归。地图移动通过注入屏幕点击走 UI 遮挡、实体命中与寻路；Map11 小地图开关、跨图页签、筛选和女神激活使用原按钮的指针按下／抬起／点击事件，并核对最上层射线命中。检查覆盖区域探索／剧情门槛、Map10／Map11 位置冷读、筛选 JSON 冷读、女神激活恢复、英雄／魔女每日共享额度及待战斗效果恢复。女神地区完成度及打开弹窗由测试设置，未覆盖自然探索到 100% 或从地图点击雕像的完整路线。

已实际查看 Map10 地图、Map11 HUD、全屏小地图和失落女神弹窗截图；Map10 HUD 当前不可见，Map11 地形／水面／遮挡仍有原有缺项，完整画面和地图验收继续由所属页维护。Python Map11 运行载荷生成与小地图转换三项检查通过。另一次 Map10 旧输入来源检查失败：未纳入当前 Git HEAD 的 `assets/official/adventure.json` 实际哈希为 `a6820d65…`，原清单期望 `84b92c87…`；本次未修改该输入或预期哈希，不能据此声明旧输入来源校验通过。

Windows development Player 已成功构建并真实启动，以隔离档案重复通过上述指针交互、区域条件与冷读检查，并查看四张实际渲染画面；隐藏窗口产生的无效截图未计入通过结论。回归构建位于 `.tools/mainline-refactor-player/杖剑传说.exe`，入口参数与 Editor 相同，测试不会写入用户档案。本次交付是既有实现的公共代码抽取，下列功能及地图仍未完成，尚未作为新增完整功能发布到桌面正式目录。

## 自定义实现

沿用已确认的本地地图存档、神像最近可站立格传送和英雄／魔女共享每日访问额度。规则细节分别见[森之国](../maps/map-11.md)、[战斗执行](battle-execution.md)；本次抽取没有新增单机奖励或改变官方条件。

## 尚未实现

- 英雄雕像的所属地图、官方原生界面与随机效果入口；现有共用事务不能替代实际交互。
- [命运果实与命运探寻](world-exploration.md)的完整地图交互。果树生产继续复用已有 [果树服务](fruit-tree.md)，探寻费用、候选／保底、动态实体、历史与原演出仍须完成 Unity 接入。
- 主线地图内消耗体力采集金币、铁矿、时之砂和打怪取得历战精华的原阶段与奖励流程。已有[素材秘境](material-gathering.md)属于独立功能，不能用它替代地图采集／战斗规则。
- 未探索区域原雾层、按区域状态显示及探索后的清除动画；官方 `ActiveRegionTilemapDisplayHelper`／`ActiveRegionTileMapDisplayHandler` 已有反编译结果，尚未接入本地地区状态与原资源绑定。
- 小地图的公共地区资源提供器、其余地图实体图标与详情、未完成的官方表现和各图完整路线；共享底层不自动完成单张地图。
