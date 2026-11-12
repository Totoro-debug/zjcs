# 主线任务进度

[总索引](../README.md)

状态：未完成

## 功能简介

本页记录 Unity 主线任务目录、本地进度事务及主城任务入口。委托刷新、每日／每周及活动任务缺少服务端决策的部分明确显示为未开放；成就独立维护。

## 原始官方实现

官方主线／委托任务读取目标目录、阶段、条件、奖励与地图背景，原任务条目和追踪窗口按对应进度刷新。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

本地成长目标不再使用通用文字卡片，改用已提取 HomeShopPanel 内的 AchievementPanel / AchievementPanelItem 原始背景、宝箱图像、进度、领奖状态和 RewardSlot 物品图标。没有新增位图素材，资源数量不变。

独立 defaultpackage_assets_prefabs_ui_achievement.bundle 的清单哈希为 95045fde8488e6c55418ef3a9131ede9，APK ZIP 中没有对应文件；本地成长目标名称、目标筛选和组合位置不能据此视为已取得完整独立原版布局。具体状态记录在 source-manifest.json 的 growth_goals_interface。

删除  的 nav 生成入口及所有额外跨页快捷栏，删除挑战页上方本地新增的技能 / 装备 / 幻兽快捷按钮。核对 TaskPanel.mTabGroupGo / mTabToggleGroup 与已提取 prefab 的 BottomGo/TabGroup_11，原始底部任务分类保留；未将名称相同的原始分类误删为额外导航。

`scripts/prepare_task_bindings.py --convert-art` 从固定 manifest `871_156598` 的 `defaultpackage_assets_image_ui_itemnotes.bundle` 读取原始 Sprite，并复用既有 Unity 解码、裁切规则，补入 12 张地图阶段背景；没有重绘或改变布局。卡片使用 `Map/map_ui_display.MapBgPath` 及当前真实来源地图选择背景；发布者和怪物图片复用既有官方转换，原始 UI 字段和语言文件哈希内嵌于 。

依据：`assets/reference/task/source-manifest.json`。

## 完整还原部分

[`OfficialMainlineTaskCatalog`](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialMainlineTaskCatalog.cs) 读取主线目录与来源清单，资源位于 `Resources/Official/Progression/mainline-tasks*.json`。目录包含1000～1270的55项任务，其中山之国1165～1270为22项。事务载荷保留自身标记的 `854_156383` 来源，并登记山之国 `871_156598` 原表对照输入；不将该载荷改标为新版本。目录生成器为 [`prepare_unity_mainline_tasks.py`](../../scripts/prepare_unity_mainline_tasks.py)，目录与对照输入的哈希见 `Resources/Official/Progression/mainline-tasks-source-manifest.json`；主城任务素材来源仍见 `assets/reference/task/source-manifest.json`。目录覆盖不代表山之国完整主线路线已经验证。

[`prepare_home_mainline_task_data.py`](../../scripts/prepare_home_mainline_task_data.py) 另从所选 `884_156692` 官方 `task` 全表的5,809行中提取这55项完整 `TaskInfo`，逐字段核对现有事务目录消费的条件、顺序、触发、固定奖励、好感奖励及随机规则；仅空好感奖励的缺省表示统一为空数组，其他差异立即拒绝。补充目录保留完整原条件（含可空 `Jumpable`）、前后任务、追踪开关、45项进度条开关、次数、时限、奖励及其他序列化字段，同时提取七种奖励物品定义和原 `TaskSettings`。`TaskInfoParser.PostParse` 把完成条件描述键设为 `task_<ID>_progress`，标题沿原 `task_<ID>_title`；162条任务、区域及物品原文直接读取经版本／哈希校验的官方 `WwiseBnk` 语言载荷。数据位于 `Resources/Home/home-mainline-task-data*.json`，来源见[同版任务补充目录](../../assets/reference/home-mainline-task-data-source-manifest.json)。[`OfflineHomeMainlineTaskData`](../../unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeMainlineTaskData.cs) 校验补充数据及被精确对照的事务载荷哈希，提供原始字段的隔离副本和动态文本。隔离完整工程静音 Play Mode 已验证55项加载、原始字段／可空值、45项进度条配置、调用方副本隔离及动态原文格式化，并通过原生 HUD 模拟指针回归；已查看实际运行截图。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查通过。原任务条目仍使用受控数据，尚未完成真实业务及奖励图标绑定。

主线奖励图标已补齐七种物品（1／2／50／110／120／41300／42200）的同版原生 Sprite 与 Windows 纹理依赖，显式目录为 `Resources/Home/OfflineHomeTaskRewardImages.asset`，来源见[奖励图标清单](../../assets/reference/home-task-reward-images-source-manifest.json)。所选 `ItemIconManagerConfig` 的专用 ID 列表不含这些物品，类型列表也不含其对应物品类型，均沿原默认 prefab；[`OfflineHomeTaskRewardIcons`](../../unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskRewardIcons.cs) 复用原 `ItemIcon`、品质框、数量文字、附件管理器和对象池，并将任务 UI 的窄接口转发到原组件。创建时在激活前绑定字体、资源、声音和详情宿主，归还时沿原组件取消物品通知；默认 RectTransform 的记录与复用恢复保持原 `ResetIconGo`／`CopyRectTransform` 次序。隔离完整工程静音 Play Mode 已验证七种原 Sprite 网格与品质／数量显示、七个无遮挡模拟指针点击到详情宿主回调、原对象池复用及订阅释放，并通过既有货币窗口、实际购买／兑换事务、存档恢复、失败回滚与新周期通知回归；已查看实际运行截图。测试宿主仅排列原图标，不作为游戏布局，详情弹窗仍为显式回调边界。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查通过。主线原任务条目、真实领奖事务和正式物品详情窗口仍未接入该图标池。

`OfflineMainlineProgressSession` 在本地角色槽中维护玩家成长、物品、地图与区域进度、战斗次数、实体标签、女神区域、宝箱、事件去重和已领取主线任务，并提供条件检查与领奖事务。成功祈愿抽取会将实际开启数量与奖励、消耗写入同一存档事务；重复回执不会重复增加宝箱计数；`TaskSubmitCondition` 依据已领取 ID 判定；`TaskCompleteCondition` 对已领取 ID 计完成，未领取任务仅在接取、展示（若有）和完成条件均由本地状态满足时计完成；不支持的条件会继续返回 `Unsupported`。保存由 `ProfileSlotCatalog` 和 `LocalJsonSaveFile` 提供。

所选 `884_156692` 的 `MapGameTotalCountCondition.GetValue/GetProgress` 从玩家黑板读取计数，`Player.GetBlackboardDataValue<long>` 在缺少记录时返回 0，不要求地图或区域已经初始化。本地首项任务 1000 因而在新角色进入地图前显示目标进度 0/2，状态为未完成；提前领奖不产生奖励或修改存档。条件进度保留实际次数，超过目标时不截断；原条件的描述格式化另行将显示参数夹到目标值。其余地图条件的既有支持边界保持独立。`OfflineMainlineProgressValidation.Run` 已验证新档零计数、超过目标的 3/2 原始进度、既有领奖及存档恢复；完整隔离工程静音 Play Mode 使用实际创建角色复核未进入地图的读取及提前领奖无状态变化，并通过原生 HUD 模拟指针回归。已查看运行截图；其中原任务条目仍用受控数据，不能作为真实任务栏装配的画面证明。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和静态元数据检查通过。

任务目录还携带固定版 `npc_friendship_level` 的 10,000 条等级门槛。领取带 `NpcFriendshipReward` 的主线任务时，在领奖存档副本内累加 NPC 好感经验，并按当前 `SubRankLimit` 更新等级；门槛目录、奖励扣发和任务领取同由本地事务提交，失败不会留下部分奖励。

主城 UI 恢复固定版官方 `TaskPanel` 和 `TaskPanelListItemProgress` 的层级与 sprites：`StreamingAssets/Home/Tasks/task-panels.json` 及其 `sprites/`。来源、prefab path ID、转换和哈希记录见 `assets/reference/task/source-manifest.json`；资源转换脚本为 [`prepare_unity_home_tasks.py`](../../scripts/prepare_unity_home_tasks.py)。[`HomeHudView.Tasks.cs`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Tasks.cs) 提供 `OpenTaskPanel(progress, navigate)`、`BuildPinnedTask(progress, navigate)` 与 `RefreshTasks()`；导航回调接收完整任务定义，仅对地图目标显示跳转，地图 10 目标进入已接入大陆，地图 11 在领取 Map10 主线任务 1040 前明确提示未开放；职业与宝箱条件不显示离线端无法执行的跳转。主线目标按 session 当前可用状态追踪，领奖回写现有本地事务。没有服务端刷新／实例状态的委托、日常、周常和成就页仅显示未开放提示，不生成任务目标。

森之国主线目标使用现有地图进度、女神激活、实体标签和塔进度存档；任务 1070 的 `DemonBattleLayerPassCondition` 按官方共享条件检查第 1 入口已通关层数，UI 按官方客户端将当前层及目标层各加 1 显示，并跳转到幻想阶梯入口。其他地图目标的跳转仍受对应地图正式入口接线限制。

固定版任务配置与本地奖励、条件、跨地图魔女累计规则已接入；主城 HUD 输入、完整任务路线、画面和存档连续流程仍待完成。资源来源与工程检查范围见[工程页](unity-project.md)。

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

- 当前目录含1000～1270共55项主线任务；山之国新增22项已接入目录和跨图任务文字，正式入口、完整任务路线、条件和领奖仍须按[山之国地图页](../maps/map-12.md)验证。地图10的区域进度、战斗回执、剧情次数／状态和首次手动NPC交互有角色存档写入，地图11只接入文档列明的地区、女神、实体标签、塔通关条件及固定奖励，未接入事件不得推定完成。
- 地图 11 主线数据与本地条件、奖励事务已接线；地图 11 目标从主城跳转仍待正式大陆入口接通。职业目标跳转、宝箱条件及完整任务路线仍未验收。
- 森之国正式入口要求先完成并领取 Map10 主线任务 1040；固定版该任务要求成功开启 10 次 `chestId=6000` 普通祈愿宝箱。计数已接入本地祈愿成功事务，地图内的 `EntityClassId=6000` 放置实体不是该计数来源。完整前序任务路线、新档累计 10 次祈愿的实际操作及领奖后的进入流程仍未完成 Play Mode 验收；运行时会继续检查任务实际领取记录。
- 完整主线路线与任务奖励表现仍未验收；尚未 Play Mode 实测。
- 非主线任务面板、委托、每日／每周任务与成就系统。
