# 主线任务进度

[总索引](../README.md)

状态：未完成

## 功能简介

本页记录 Unity 主线任务目录、本地进度事务及主城任务入口。委托刷新、每日／每周及活动任务缺少服务端决策的部分明确显示为未开放；成就独立维护。

## 原始官方实现

官方主线／委托任务读取目标目录、阶段、条件、奖励与地图背景，原任务条目和追踪窗口按对应进度刷新。

所选 `PlayerTaskDataUtils.IsCompletedState` 只读取 `TaskState.Completed`；`TaskCompleteCondition.GetValue` 分别统计已提交记录与仍在角色任务表中的 Completed 实例，`TaskSubmitCondition.GetValue` 仅统计已提交记录。`PinnedTaskPanel` 按 `DisplayCondition` 决定是否展示已固定任务。客户端／共享代码中未取得服务器生成 Completed 状态的方法，单机状态生成采用下述已确认方案。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

本地成长目标不再使用通用文字卡片，改用已提取 HomeShopPanel 内的 AchievementPanel / AchievementPanelItem 原始背景、宝箱图像、进度、领奖状态和 RewardSlot 物品图标。没有新增位图素材，资源数量不变。

独立 defaultpackage_assets_prefabs_ui_achievement.bundle 的清单哈希为 95045fde8488e6c55418ef3a9131ede9，APK ZIP 中没有对应文件；本地成长目标名称、目标筛选和组合位置不能据此视为已取得完整独立原版布局。具体状态记录在 source-manifest.json 的 growth_goals_interface。

删除  的 nav 生成入口及所有额外跨页快捷栏，删除挑战页上方本地新增的技能 / 装备 / 幻兽快捷按钮。核对 TaskPanel.mTabGroupGo / mTabToggleGroup 与已提取 prefab 的 BottomGo/TabGroup_11，原始底部任务分类保留；未将名称相同的原始分类误删为额外导航。

`scripts/prepare_task_bindings.py --convert-art` 从固定 manifest `871_156598` 的 `defaultpackage_assets_image_ui_itemnotes.bundle` 读取原始 Sprite，并复用既有 Unity 解码、裁切规则，补入 12 张地图阶段背景；没有重绘或改变布局。卡片使用 `Map/map_ui_display.MapBgPath` 及当前真实来源地图选择背景；发布者和怪物图片复用既有官方转换，原始 UI 字段和语言文件哈希内嵌于 。

依据：`assets/reference/task/source-manifest.json`。

## 完整还原部分

[`OfficialMainlineTaskCatalog`](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialMainlineTaskCatalog.cs) 读取主线目录与来源清单，资源位于 `Resources/Official/Progression/mainline-tasks*.json`。目录包含1000～1270的55项任务，其中山之国1165～1270为22项。事务载荷保留自身标记的 `854_156383` 来源，并登记山之国 `871_156598` 原表对照输入；不将该载荷改标为新版本。目录生成器为 [`prepare_unity_mainline_tasks.py`](../../scripts/prepare_unity_mainline_tasks.py)，目录与对照输入的哈希见 `Resources/Official/Progression/mainline-tasks-source-manifest.json`；主城任务素材来源仍见 `assets/reference/task/source-manifest.json`。目录覆盖不代表山之国完整主线路线已经验证。

[`prepare_home_mainline_task_data.py`](../../scripts/prepare_home_mainline_task_data.py) 另从所选 `884_156692` 官方 `task` 全表的5,809行中提取这55项完整 `TaskInfo`，逐字段核对现有事务目录消费的条件、顺序、触发、固定奖励、好感奖励及随机规则；仅空好感奖励的缺省表示统一为空数组，其他差异立即拒绝。补充目录保留完整原条件（含可空 `Jumpable`）、前后任务、追踪开关、45项进度条开关、次数、时限、奖励及其他序列化字段，同时提取七种奖励物品定义和原 `TaskSettings`。`TaskInfoParser.PostParse` 把完成条件描述键设为 `task_<ID>_progress`，标题沿原 `task_<ID>_title`；162条任务、区域及物品原文直接读取经版本／哈希校验的官方 `WwiseBnk` 语言载荷。数据位于 `Resources/Home/home-mainline-task-data*.json`，来源见[同版任务补充目录](../../assets/reference/home-mainline-task-data-source-manifest.json)。[`OfflineHomeMainlineTaskData`](../../unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeMainlineTaskData.cs) 校验补充数据及被精确对照的事务载荷哈希，提供原始字段的隔离副本和动态文本。隔离完整工程静音 Play Mode 已验证55项加载、原始字段／可空值、45项进度条配置、调用方副本隔离及动态原文格式化，并通过原生 HUD 模拟指针回归；已查看实际运行截图。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查通过。该基础数据测试使用受控任务条目；真实角色与原生条目的组合验证见下文。

主线奖励图标已补齐七种物品（1／2／50／110／120／41300／42200）的同版原生 Sprite 与 Windows 纹理依赖，显式目录为 `Resources/Home/OfflineHomeTaskRewardImages.asset`，来源见[奖励图标清单](../../assets/reference/home-task-reward-images-source-manifest.json)。所选 `ItemIconManagerConfig` 的专用 ID 列表不含这些物品，类型列表也不含其对应物品类型，均沿原默认 prefab；[`OfflineHomeTaskRewardIcons`](../../unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskRewardIcons.cs) 复用原 `ItemIcon`、品质框、数量文字、附件管理器和对象池，并将任务 UI 的窄接口转发到原组件。创建时在激活前绑定字体、资源、声音和详情宿主，归还时沿原组件取消物品通知；默认 RectTransform 的记录与复用恢复保持原 `ResetIconGo`／`CopyRectTransform` 次序。隔离完整工程静音 Play Mode 已验证七种原 Sprite 网格与品质／数量显示、七个无遮挡模拟指针点击到详情宿主回调、原对象池复用及订阅释放，并通过既有货币窗口、实际购买／兑换事务、存档恢复、失败回滚与新周期通知回归；已查看实际运行截图。测试宿主仅排列原图标，不作为游戏布局，详情弹窗仍为显式回调边界。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查通过。原任务条目与该图标池的真实角色组合验证见下文；正式物品详情窗口仍未接入。

[`OfflineHomeTaskConditionReader`](../../unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskConditionReader.cs) 将这55项原完成条件和展示条件接到真实角色槽的只读投影，保留所选原 `GetProgress`／`GetDescComponent` 分支：区域已存为万分比，按 `Formula.Int2Float` 的 float 除法、乘100和整数截断转换成原任务条目的百分比；未访问区域读取0且不建立地图记录。战斗／宝箱次数和客户端加1后的塔层进度保留原始值，描述参数另行夹到目标；实体标签与女神区域读取既有真实进度，职业目标按原布尔0/1显示。原任务描述键、区域 `CultureTextArg`、`{c}` 条件颜色替换及空颜色原文处理保持原顺序；代码和方法凭据见[条件读取来源](../../assets/reference/home-task-condition-reader-source-manifest.json)。本地事务同时支持未访问区域的0进度，以及最高职业阶级0时的未完成状态。塔进度和默认祈愿 ID 通过显式宿主读取。隔离完整工程静音 Play Mode 已验证55项完成条件的投影、真实新角色的未访问区域／战斗／宝箱／职业条件、百分比截断、描述夹值、展示条件、只读查询和重开存档；塔层测试使用显式受控宿主，未证明实际塔入口接线。原生奖励图标及既有货币窗口回归通过。主线规则校验、Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和静态元数据检查通过。正式 HUD 中的任务装配与跳转仍未组合。

[`OfflineHomeTaskConditions`](../../unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskConditions.cs) 将原任务 UI 条件接口连接到同版 `HomeConditionNotifications`：任务读取得到的条件保留原条件类型身份，并校验所属玩家作用域。原 `ConditionHandler` 补充宝箱、任务新增／更新／移除、完成记录、塔层、黑板及可激活实体通知，保留各组原条件顺序；女神分支先通知女神条件，再通知区域进度，其他实体仅通知区域进度。重复回调按原引用计数管理、执行一次；异常由明确宿主报告，继续后续通知。所有通知须由角色宿主在对应事务成功提交后转发，释放时清空原注册表。来源与适配入口见[任务条件通知来源](../../assets/reference/home-task-condition-notifications-source-manifest.json)。隔离完整工程静音 Play Mode 已验证七类真实任务条件的事件范围和调用顺序、女神分支、提交后读取替换的角色状态、重复订阅引用计数、退订、异常隔离、跨作用域拒绝及重复释放；原生奖励图标和全部既有货币窗口回归通过。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查通过。正式 HUD 中的任务装配及地图／成长事件自动转发仍未完成。

`OfflineMainlineProgressSession` 在本地角色槽中维护玩家成长、物品、地图与区域进度、战斗次数、实体标签、女神区域、宝箱、事件去重和已领取主线任务，并提供条件检查与领奖事务。成功祈愿抽取会将实际开启数量与奖励、消耗写入同一存档事务；重复回执不会重复增加宝箱计数；`TaskSubmitCondition` 依据已领取 ID 判定；`TaskCompleteCondition` 分别统计已提交 ID 和尚未领奖的 Completed 状态，Received 不再作为活跃完成实例重复计数；不支持的条件会继续返回 `Unsupported`。保存由 `ProfileSlotCatalog` 和 `LocalJsonSaveFile` 提供。

所选 `884_156692` 的 `MapGameTotalCountCondition.GetValue/GetProgress` 从玩家黑板读取计数，`Player.GetBlackboardDataValue<long>` 在缺少记录时返回 0，不要求地图或区域已经初始化。本地首项任务 1000 因而在新角色进入地图前显示目标进度 0/2，状态为未完成；提前领奖不产生奖励或修改存档。条件进度保留实际次数，超过目标时不截断；原条件的描述格式化另行将显示参数夹到目标值。其余地图条件的既有支持边界保持独立。`OfflineMainlineProgressValidation.Run` 已验证新档零计数、超过目标的 3/2 原始进度、既有领奖及存档恢复；完整隔离工程静音 Play Mode 使用实际创建角色复核未进入地图的读取及提前领奖无状态变化，并通过原生 HUD 模拟指针回归。已查看运行截图；其中原任务条目仍用受控数据，不能作为真实任务栏装配的画面证明。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和静态元数据检查通过。

任务目录还携带固定版 `npc_friendship_level` 的 10,000 条等级门槛。领取带 `NpcFriendshipReward` 的主线任务时，在领奖存档副本内累加 NPC 好感经验，并按当前 `SubRankLimit` 更新等级；门槛目录、奖励扣发和任务领取同由本地事务提交，失败不会留下部分奖励。

主城 UI 恢复固定版官方 `TaskPanel` 和 `TaskPanelListItemProgress` 的层级与 sprites：`StreamingAssets/Home/Tasks/task-panels.json` 及其 `sprites/`。来源、prefab path ID、转换和哈希记录见 `assets/reference/task/source-manifest.json`；资源转换脚本为 [`prepare_unity_home_tasks.py`](../../scripts/prepare_unity_home_tasks.py)。[`HomeHudView.Tasks.cs`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Tasks.cs) 提供 `OpenTaskPanel(progress, navigate)`、`BuildPinnedTask(progress, navigate)` 与 `RefreshTasks()`；导航回调接收完整任务定义，仅对地图目标显示跳转，地图 10 目标进入已接入大陆，地图 11 在领取 Map10 主线任务 1040 前明确提示未开放；职业与宝箱条件不显示离线端无法执行的跳转。主线目标按 session 当前可用状态追踪，领奖回写现有本地事务。没有服务端刷新／实例状态的委托、日常、周常和成就页仅显示未开放提示，不生成任务目标。

森之国主线目标使用现有地图进度、女神激活、实体标签和塔进度存档；任务 1070 的 `DemonBattleLayerPassCondition` 按官方共享条件检查第 1 入口已通关层数，UI 按官方客户端将当前层及目标层各加 1 显示，并跳转到幻想阶梯入口。其他地图目标的跳转仍受对应地图正式入口接线限制。

[`OfflineMainlineTaskStates`](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineTaskStates.cs) 生成主线的 Unavailable／Processing／Completed／Received 只读状态投影，后三项保留原枚举值；按接取链读取真实已保存进度，单次求值复用任务结果并拒绝循环引用，不写第二份状态账本或发奖。任务接取条件现在可读取未提交的 Completed 前置；同版 `OfflineHomeTaskConditionReader.Acquisition` 转发该原条件进度，任务展示仍按提交条件单独判断。领奖仅要求可提交的 Completed 状态，保持原固定奖励与已提交记录的原子存储；显示条件不再作为领奖事务门槛。原码、确认的本地生产规则、验证入口和输出哈希见[任务状态来源](../../assets/reference/home-mainline-task-states-source-manifest.json)。主线规则校验已覆盖新档55项接取链、未领奖的两个 Completed、后继 Processing、TaskComplete／TaskSubmit 不同计数、查询无写盘／无发奖、展示前的独立提交、Received 单次计数、重复领奖和重开存档；既有地图、女神、奖励与提交物品回归通过。完整隔离工程静音 Play Mode 使用实际新角色验证未提交的 Completed、原接取／展示差异，并通过条件通知、原生奖励图标及货币窗口回归。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和静态元数据检查通过。正式 HUD 中的任务组合仍未完成。

[`OfflineHomeTaskPlayer`](../../unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskPlayer.cs) 将所选55项单次主线映射为原任务 UI 的玩家／实例接口，自动置顶读取原 `AutoPinTaskTypes`／`CanBePinnedTaskTypes`，完成、可领奖、持续时间、顺序和追踪标志按原字段分支读取。玩家事件复用原 `SafeAction` 的去重、调用快照、列表池与单个监听异常隔离。领奖前重读共享角色槽，保留其他页面刚写入的物品；原提交事务成功后，先通知提交记录，再按原消息消费顺序通知任务移除／更新，随后转发实际物品变化。后继任务展示条件发生变化时，即使仍为 Processing，也发布实例更新供原 `PinnedTaskPanel` 重新检查展示。来源见[任务玩家与奖励预览](../../assets/reference/home-task-player-source-manifest.json)。

[`OfflineHomeTaskRewardPresenter`](../../unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskRewardPresenter.cs) 保留原固定奖励预览的过滤和排序：经验优先、货币其次，再按品质降序和物品 ID 升序；使用原图标数量缩放、点击详情及概率附件设置。原 `SetAttachment(ProbabilityTip, false)` 仍会取得附件，因此另恢复原 `GotObj`／`ProbabilityGo` prefab、语言尺寸控制组件、原语言分组和“概率”文字，沿原对象池显隐、回收和语言通知；显式加载入口为 `Resources/Home/OfflineHomeTaskRewardAttachments.asset` 与 `home-task-attachment-language.json`，资源凭据见[奖励附件来源](../../assets/reference/home-task-reward-attachments-source-manifest.json)。这些新增素材尚未进入完整快照。

隔离完整工程静音 Play Mode 已使用实际新角色验证接取／置顶和原字段、隐藏后继在领奖后显现、完成／领奖事件顺序、原事件去重与异常隔离、共享角色槽重读、原子奖励和提交记录、重放不重复发事件及重开存档。原生奖励预览已验证经验／货币／其他物品排序与数量、三个无遮挡模拟指针点击到详情宿主、概率／已获得附件显隐和全部组件解析、中文文字及语言切换时原1.0／1.3缩放；已查看实际运行截图。既有条件通知、七种奖励图标和全部货币窗口事务回归通过；Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 与静态元数据检查通过。此处领奖测试直接调用任务模型；原任务条目真实领奖按钮的验证见下文。完整地图导航／追踪、正式详情弹窗和正式 HUD 装配仍未完成。

隔离完整工程进一步将实际新角色、`OfflineHomeTaskPlayer`、条件通知和奖励图标池装配到原 `HomePinnedTasks.prefab`、`PinnedTaskPanel`、`TaskListItem` 与 `TaskGoal`。静音 Play Mode 已验证原条目按真实战斗回执从0/2刷新为1/2、2/2并切换领奖状态，模拟指针实际点击原领奖按钮后获得30货币并展示任务1005；真实区域进度随后按原文字显示25/100，完成后第二次原按钮点击再获得30货币并展示1010。重开角色槽保留60货币及两项提交记录，原任务栏关闭／重开／销毁后订阅正确释放。测试检查原声音事件、按钮无遮挡和文字位于画面内，并已查看三种状态的实际截图；未调整原 prefab／条目布局，独立测试挂载容器不作为游戏布局。该宿主已绑定下述真实地图限制／推荐数据，仍明确控制战力／阶级、导航／追踪、日历和提示点显示，不代表正式 HUD 或完整任务路线完成。验证入口及输入凭据见[原生任务条目组合](../../assets/reference/home-task-row-profile-source-manifest.json)。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 与静态元数据检查通过。

[`OfflineHomeTaskMapData`](../../unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskMapData.cs) 读取所选884的45个主线探索目标区域完整 `MapRegionInfo`、原推荐战力索引、抑制解锁阶级、86项原 `SubRank→Rank` 映射和原 `CombatRatingConfig` 主题／档位颜色；原战力分档与阶级抑制方法迁入 `HomeTaskMapFormula`，保留阈值相等分支与低阶不抑制分支。区域载荷保留空战力列表、个人等级、服务器阶位和日期原字段；按已确认的[离线开放政策](offline-access.md)取消后两类门槛，个人等级继续按原 `< LimitLevel` 判断。图标色值直接读取原配置，不重绘。显式入口为 `Resources/Home/home-task-map-data*.json`，输入版本、CAB／PathID、逐文件哈希和迁移方法见[任务地图数据来源](../../assets/reference/home-task-map-data-source-manifest.json)。原生任务条目组合宿主已使用该数据，正式 HUD 接线仍未完成。

隔离工程规则校验已验证全部45个区域的等级边界、原推荐索引／空值、战力阈值相等、7396组原阶级映射组合和每个原主题／档位 RGBA，修改载荷会被哈希校验拒绝。原生条目真实领奖与存档回归、Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查通过；画面仍仅证明已列明的真实初始岛任务组合，不证明后续地图完整路线、导航与追踪。

[`OfflineHomeTaskNavigation`](../../unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskNavigation.cs) 为所选55项主线接入原完成条件导航。原目录的 `NavigateKey` 全部为空，因此保持原 `NavigateUtils` 的条件跳转分支；出现新导航键会要求重新迁移。`ClientConditionRedirector` 中的战斗次数、探索进度、实体标签、女神、宝箱和幻想阶梯六个原客户端条件，其 `CanJump`／`Jump` 与探索／实体标签的 `CanTrace` 方法已迁入 `HomeTaskJumpConditions`。原分支分别保留同图／地图组判断、全图战斗条件的已访问区域检查、地图资格与时间提示、快速清理／星系入口优先处理、原确认文字和取消语义、实体任务先登记待追踪再请求附近入口、宝箱返家确认和幻想阶梯跳转。原职业阶级条件没有客户端跳转实现，不补造入口；原 `Jumpable == true` 语义保留。导航完成回调仍先转发调用方，再交给显式默认处理宿主。

确认窗口复用已恢复的原 `WorldConfirmDialog` prefab、字体、遮罩、音频及动画，八条原跳转／追踪文字和三条通用确认文字来自同版官方语言载荷；显式入口为 `Resources/Home/home-task-navigation-texts.json`。原码、重定向核对、适配输出与输入哈希见[任务导航来源](../../assets/reference/home-task-navigation-source-manifest.json)。隔离完整工程静音 Play Mode 已通过实际新角色原任务条目的跳转按钮、原确认框取消／确认两组模拟指针，确认前不请求进入、确认后只转发原目标地图10／区域0；原领奖及存档恢复回归通过。另验证原同图／地图组／无容器／跨图可追踪判断、地图分组时提示区域与实际请求目标的区别、快速清理和时间提示、全图战斗及跳转开关、实体待追踪顺序、宝箱返家与幻想阶梯回调顺序；已查看实际运行截图，确认框标题、动作及正文均为原中文。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 与静态元数据检查通过。

上述地图、实体标签位置、快速清理／星系、日历和追踪实体查询仍由显式受控验证宿主提供；进入、离开、幻想阶梯及默认导航结果处理只证明原请求顺序，尚未接到正式地图／页面，不代表真实地图已经进入或追踪标记已经出现。正式地图宿主、实际追踪实体生命周期、任务详情窗口与正式 HUD 装配仍未完成。

原任务跳转的区域落点选择已进一步迁入 [`HomeTaskMapEntryNavigation`](../../unity/Assets/Game/Runtime/Official/HomeTaskUI/HomeTaskMapEntryNavigation.cs)：直接提取所选客户端 `MapGoToUtil.GotoMapRegion`、共享 `EntityHandler.FindRegionNearEntry/IsEntryUsable` 和 `MapPeriodicityInteractionInfoParser.CheckCanInteraction`，保留地图组改向、同组当前地图提示、最后入口区域／区域0回退、按邻接列表及入口列表顺序的广度搜索和地图默认入口回退。普通入口须在实际激活列表中；已激活入口／传送点须处于公共区域；周期交互传送点还须沿原返回码2155判断。周期计数低于上限不会被擅自解释为入口可用。

[`OfflineHomeTaskMapEntryData`](../../unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskMapEntryData.cs) 从同版原 `MapEntryInfoPack`、周期实体映射、`map_world`、`map_region` 和周期配置提供所选55项主线涉及的地图10／11／12、181个区域及原入口顺序；返回值隔离可变存储，输入按版本及SHA256核对。原地图组提示文字也已加入既有任务导航原文目录。配置、原始方法、枚举数值核对及适配凭据见[任务地图入口来源](../../assets/reference/home-task-map-entry-source-manifest.json)，显式配置入口为 `Resources/Home/home-task-map-entry*.json`。

隔离工程规则校验已验证这181个区域的无可用入口回退、全部实际入口的类型判定及已激活入口选择、循环图的搜索终止和邻接／入口顺序、周期门槛及否定分支的原错误码、空玩家／缺失地图、地图组改向和最后区域选择、返回副本隔离及篡改拒绝。完整隔离工程静音 Play Mode 已通过实际新角色原任务条目和原确认框的取消／确认模拟指针，取消无入口请求、确认后沿原搜索只请求一次地图10的默认入口101；原领奖、存档重开、货币窗口及导航回归通过，并已查看实际运行截图。Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 与静态元数据检查通过。此处激活列表、公共／私有状态、地图组与周期计数由显式验证宿主提供；最终进入地图仍是请求边界，尚未证明实际场景进入、任务追踪标记或正式HUD装配。

所选原 `TaskHandler`、无参数 `SafeAction` 和 `TaskMapTraceHandler.OnActivate` 已迁入 [`HomeTaskTraceHandler`](../../unity/Assets/Game/Runtime/Official/HomeTaskUI/HomeTaskTraceHandler.cs)、`HomeTaskTraceEvent`、`HomeTaskMapTraceHandler`。开始追踪先取消旧目标；取消先释放目标、清空引用，再按原开关显示停止文字并发布取消事件；指定停止只释放匹配任务，不发布取消事件。目标结束回调仅发布结束事件，随后由原 `TraceTaskComponent → TaskListItem.OnTraceEnd` 停止并释放目标。区域变化时原条目以 `showTip:false` 重新追踪；条目销毁先停止自己的追踪，再移除三类事件订阅。无参数事件保留原去重、快照、列表池和异常隔离，异常通过必需宿主报告。

入图追踪保留原等待规则：重复登记提示并替换等待ID；地图未初始化或玩家缺失时保留等待；非正ID不消费；固定任务与等待目标不符时只报告，仍启动所登记目标，随后清空等待。`OfflineHomeTaskNavigation` 按原两类条件的创建／初始化顺序调用必需的地图作用域工厂：区域目标使用原地图ID／区域，实体标签目标使用原标签解析得到的地图ID／LogicId；缺少实际工厂时明确拒绝开始。原码、适配与验证输入输出凭据见[任务追踪生命周期来源](../../assets/reference/home-task-trace-runtime-source-manifest.json)。

完整隔离工程静音 Play Mode 已用真实新角色任务1005和原生按钮模拟指针，验证追踪／取消状态、原提示键、区域变化无提示重建、目标结束后的原条目回收、真实领奖移除时停止追踪、所有受控目标恰好释放一次及三类事件全部退订；独立追踪拥有者同时验证替换顺序、错ID停止、结束事件保留目标、无效任务、入图等待各分支和原事件去重／快照／异常隔离。已查看追踪中和结束后的实际运行截图。原任务领奖、存档重开、原生跳转确认与货币窗口回归通过，Unity 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和静态元数据检查通过。此处地图初始化、当前固定任务及实体／区域追踪实例由明确验证端口提供；实际地图实体观察、区域目标选择、方向指引图像及正式HUD装配仍未迁入，不能据此视为可游玩的完整地图追踪。

固定版任务配置与本地奖励、条件、跨地图魔女累计规则已接入；主城 HUD 输入、完整任务路线、画面和存档连续流程仍待完成。资源来源与工程检查范围见[工程页](unity-project.md)。

## 自定义实现

任务数据和领奖在本地提交，无运行时服务器依赖。

用户确认：主线满足接取条件且完成条件达成时生成本地 Completed；DisplayCondition 仅控制展示；成功领奖后写入已提交记录。所选主线使用已有存档的累计进度恢复状态，不补造服务器任务数据。

职业主线 `MaxProfessionRankCondition` 的服务器附加参数使用角色本地最高已解锁职业阶级；解锁后读取实际存档中的最新阶级。该差异已由用户确认，保留原条件比较与布尔进度。目录中的官方任务条件只有在本地会话明确支持时才可完成；不支持的条件不会被猜测为满足。

以下为已确认的离线任务要求；当前 Unity 主线目录和进度事务不代表委托、日常／每周任务及活动事件已迁移。

- 服务端委托生成未取得：按原组权重抽组，再从满足接取条件的组内任务等概率抽取；重要委托占普通名额，不启用语义未确认的首次／两批保底。
- 实例、刷新、随机、奖励、日额及领取记录须同一本地事务保存，失败回滚；已移除真人玩法不生成可领目标。
- 长期玩法保留永久累计，活动／新纪录另记活动开始后的真实行为；首次打开对应活动页面建立本地起点，重开不清零，不补造无法恢复的历史。签到仅在实际领取当日奖励时每日记一次，同日多个主题不重复，登录不计数，有限期表领完不自行循环。
- 小游戏保留真实个人游玩、分数及参与任务，停用超过对手、第一名与梦境舞会名次目标。音乐206／221分开累计；伐木209须完成已确认五轮整局后才计一次参与，中退、重复或保存失败不计。
- 三消按原 ActivityId、StageId、Count 更新已接取实例，不追补接取前进度；万圣节40601～40614仍归 BattlePassSeason。拼图按 Id、Level、Order、Count 统计首次实际通关，练习、未完成及失败提交不计，不追补接取前领奖。

既有任务事件规则及小游戏条件见[活动小游戏](activity-minigames.md)和[签到与活动任务](sign-ins.md)；这些要求须在 Unity 调用链中逐项接线验证。

## 尚未实现

- 正式家园原生菜单已派发到既有任务页，并通过遮罩点击返回菜单；该页主体仍沿历史 JSON 树，实际 608×1080 截图的每日重置文字仍露出 `{DailyResetTime}{TimeZone}` 占位符。任务页原 prefab／脚本及本页已迁移原生条目的正式接线仍缺；菜单测试不证明完整任务页表现完成。
- 当前目录含1000～1270共55项主线任务；山之国新增22项已接入目录和跨图任务文字，正式入口、完整任务路线、条件和领奖仍须按[山之国地图页](../maps/map-12.md)验证。地图10的区域进度、战斗回执、剧情次数／状态和首次手动NPC交互有角色存档写入，地图11只接入文档列明的地区、女神、实体标签、塔通关条件及固定奖励，未接入事件不得推定完成。
- 地图 11 主线数据与本地条件、奖励事务已接线；地图 11 目标从主城跳转仍待正式大陆入口接通。职业目标跳转、宝箱条件及完整任务路线仍未验收。
- 森之国正式入口要求先完成并领取 Map10 主线任务 1040；固定版该任务要求成功开启 10 次 `chestId=6000` 普通祈愿宝箱。计数已接入本地祈愿成功事务，地图内的 `EntityClassId=6000` 放置实体不是该计数来源。完整前序任务路线、新档累计 10 次祈愿的实际操作及领奖后的进入流程仍未完成 Play Mode 验收；运行时会继续检查任务实际领取记录。
- 完整主线路线与任务奖励表现仍未验收；尚未 Play Mode 实测。
- 非主线任务面板、委托、每日／每周任务与成就系统。
