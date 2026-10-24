# 主线任务进度

[总索引](../README.md)

状态：未完成

## 功能简介

本页记录 Unity 主线任务目录、本地进度事务及主城任务入口。委托刷新、每日／每周及活动任务缺少服务端决策的部分明确显示为未开放；成就独立维护。

## 完整还原部分

[`OfficialMainlineTaskCatalog`](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialMainlineTaskCatalog.cs) 读取固定版本 `854_156383` 的主线目录与来源清单，资源位于 `Resources/Official/Progression/mainline-tasks*.json`。当前目录只包含本地状态有依据且奖励可本地处理的 11 个主线目标（1000、1005～1050）及 15 条奖励记录；目录生成器为 [`prepare_unity_mainline_tasks.py`](../../scripts/prepare_unity_mainline_tasks.py)，其哈希和官方输入记录见 `assets/reference/task/source-manifest.json`。

`OfflineMainlineProgressSession` 在本地角色槽中维护玩家成长、物品、地图与区域进度、战斗次数、实体标签、女神区域、宝箱、事件去重和已领取主线任务，并提供条件检查与领奖事务。`TaskSubmitCondition` 依据已领取 ID 判定；`TaskCompleteCondition` 对已领取 ID 计完成，未领取任务仅在接取、展示（若有）和完成条件均由本地状态满足时计完成；不支持的条件会继续返回 `Unsupported`。保存由 `ProfileSlotCatalog` 和 `LocalJsonSaveFile` 提供。

主城 UI 恢复固定版官方 `TaskPanel` 和 `TaskPanelListItemProgress` 的层级与 sprites：`StreamingAssets/Home/Tasks/task-panels.json` 及其 `sprites/`。来源、prefab path ID、转换和哈希记录见 `assets/reference/task/source-manifest.json`；资源转换脚本为 [`prepare_unity_home_tasks.py`](../../scripts/prepare_unity_home_tasks.py)。[`HomeHudView.Tasks.cs`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Tasks.cs) 提供 `OpenTaskPanel(progress, navigate)`、`BuildPinnedTask(progress, navigate)` 与 `RefreshTasks()`；导航回调接收完整任务定义，仅对地图目标显示跳转，地图 10 目标进入已接入大陆，地图 11 明确提示未开放；职业与宝箱条件不显示离线端无法执行的跳转。主线目标按 session 当前可用状态追踪，领奖回写现有本地事务。没有服务端刷新／实例状态的委托、日常、周常和成就页仅显示未开放提示，不生成任务目标。

编辑器校验入口为 `Zjcs.Editor.Progression.OfflineMainlineProgressValidation.Run`。主线 bundle 已按 `prepare_unity_bundle_inputs.py` 核对固定版 CAB 闭包：Unity `2022.3.57f1`、Android 目标、26 个外部 CAB 依赖、86 个序列化文件。`python scripts/check_unity_project.py` 通过（28,310 个 GUID）；这只验证项目元数据和本地引用，不验证 C# 编译或 Windows 渲染。任务入口和主城 HUD 接线已落地；本轮未运行 Unity Editor，因此 C# 导入／编译、Play Mode 输入命中、奖励状态恢复和实际渲染仍待验证，详见[工程页](unity-project.md)。

## 自定义实现

任务数据和领奖在本地提交，无运行时服务器依赖。目录中的官方任务条件只有在本地会话明确支持时才可完成；不支持的条件不会被猜测为满足。

以下为已确认的离线任务要求；当前 Unity 主线目录和进度事务不代表委托、日常／每周任务及活动事件已迁移。

- 服务端委托生成未取得：按原组权重抽组，再从满足接取条件的组内任务等概率抽取；重要委托占普通名额，不启用语义未确认的首次／两批保底。
- 实例、刷新、随机、奖励、日额及领取记录须同一本地事务保存，失败回滚；已移除真人玩法不生成可领目标。
- 长期玩法保留永久累计，活动／新纪录另记活动开始后的真实行为；首次打开对应活动页面建立本地起点，重开不清零，不补造无法恢复的历史。签到仅在实际领取当日奖励时每日记一次，同日多个主题不重复，登录不计数，有限期表领完不自行循环。
- 小游戏保留真实个人游玩、分数及参与任务，停用超过对手、第一名与梦境舞会名次目标。音乐206／221分开累计；伐木209须完成已确认五轮整局后才计一次参与，中退、重复或保存失败不计。
- 三消按原 ActivityId、StageId、Count 更新已接取实例，不追补接取前进度；万圣节40601～40614仍归 BattlePassSeason。拼图按 Id、Level、Order、Count 统计首次实际通关，练习、未完成及失败提交不计，不追补接取前领奖。

既有任务事件规则及小游戏条件见[活动小游戏](activity-minigames.md)和[签到与活动任务](sign-ins.md)；这些要求须在 Unity 调用链中逐项接线验证。

## 尚未实现

- `Zjcs.Editor.Progression.OfflineMainlineProgressValidation.Run` 尚未在本轮 Unity Editor 执行；静态 GUID 检查不验证 Resources 装载与 C# 运行。
- 主线当前只覆盖目录中 11 个有本地规则依据的目标；地图 10 的区域进度、战斗回执、剧情次数／状态和首次手动 NPC 交互有角色存档写入，其他地图与未接入事件不得推定完成。
- 地图 11 任务目标、职业目标跳转、宝箱条件从主城发起跳转仍未接；当前导航不再把所有任务错误地送到地图 10。
- 完整主线路线、任务奖励表现、失败恢复及冷读验证；任务面板的实际输入、画面与存档恢复尚未 Play Mode 实测。
- 非主线任务面板、委托、每日／每周任务与成就系统。
