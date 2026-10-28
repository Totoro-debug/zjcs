# 伙伴旅行、品质与保底

[总索引](../README.md)

状态：未完成（旅行配置、主页面、详情、伙伴选择和本地事务已接通；状态窗原生模型演出、缺失伙伴头像和 Unity 实际运行验收仍缺）

## 功能简介

羁绊第一节点后开放四格伙伴旅行。选择已结识伙伴派遣，计时完成后领取一次奖励；召回遵循用户确认的离线规则：不发奖励，移除旅行，退还仍属于派遣日的次数并恢复原位置委托。

入口沿用主城 `TravelNote` 页签中的官方 `NpcAdventureEntry`，羁绊第一节点为基础前置；实际开放还要求从已通关主线进度解析出有效官方旅行节点。联网战斗入口按离线范围关闭。

## 原始官方实现

原伙伴旅行按 NPC、品质、奖励／保底、消耗和次数配置执行，使用 Npc 旅行原窗口及结果显示。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

固定 `854_156383` 的 7 个原旅行 prefab、44 份组件绑定与 3,489 条中文使用独立 `npc-travel-ui/source-manifest.json` 记录；运行目录为 `assets/official/npc-travel-ui/`，包含 `panels.json` 与 72 张原贴图。共享接入按来源 SHA 验证全部 73 条运行路径并合并中央来源及显式清单，保留既有预期哈希。

第二批补入原 `MarkableConfirmDialog` 及其 5 张依赖图像，现为 8 个 prefab、59 份组件绑定、3,493 条中文和 78 条运行路径。前批 72 张 PNG 的来源、实际文件和中央绑定 SHA 均未改变；`panels.json` 按新增原确认框重新转换并登记新 SHA，旧 73 条路径哈希、原来源指纹和 72 张图像完整凭据保存在 `npc-travel-ui/registration-history/registered-v1.json` 及相邻历史文件。首批未另存完整来源文件原字节，该限制如实记录；第二批保存完整来源和增量快照。

旅行功能冻结版补入原 `ui_common_confirm_dialog_no_tip` 一词，现为 3,494 条中文，最终 `panels.json` SHA 为 `7ce474936a2b863bf3ed816f9c7f33b8efcc68f613762f0a7ee13270ebf602c9`。77 张 PNG 逐项对照前版来源、实际文件及中央绑定，全部同字节；没有新增 PNG。完整冻结版来源和前版 JSON 哈希保存在 `npc-travel-ui/registration-history/source-v3.json`、`registered-v3.json`。

旅行状态演出依据固定 `854_156383`：`inspect_npc_travel_ui_followup.py` 提取原出发／领奖／伙伴详情／条件提示 prefab、动画剪辑和事件；`prepare_npc_travel_followup.py` 与 `prepare_npc_travel_brief_extension.py` 转换四条状态时间线和原羁绊规则窗。`NpcAdventureModelItem.cs`、`NpcAdventureTravelStartStatusDialog.cs`、`NpcAdventureTravelEndStatusDialog.cs`、`NpcAdventureTravelEndStatusAnimController.cs`、`INpcAdventureTravelStatusShow.cs` 与 `AppearanceSystem.Runtime/AppearanceAnimUtils.cs` 的调用及 SHA 随模块清单登记。`prepare_npc_travel_model_extension.py` 按原 `NpcAdventureSettings`／伙伴配置，为76位可派遣伙伴转换 `FigureModelUI` 与 `FigureModelWithWeaponUI` 共152个原 prefab；NPC 11011 的两个原模型缺少 `100/idle_01` 与 `100/run_03`；固定客户端 `AppearanceAnimUtils.PlayAnimation` 在查找失败时不改当前轨道，运行时沿用该回退，四条具体绑定列在模块清单。装配、来源哈希、运行资源和显式加载由 `assemble_npc_travel_extension.py`、`publish_npc_travel_assets.py` 校验并登记；正式资源为 `assets/official/npc-travel-ui/travel-models.json`，完整原始对象与输出凭据位于 `npc-travel-ui/source-manifest.json`。出发剪辑的 `CanvasGroup.m_Alpha` 路径哈希 `588897702` 在固定弹窗和官方动态插入的152个模型层级中均无对应目标，来源清单保留 `target_absent`；运行时不将该无目标曲线套用到其他对象。

## 完整还原部分

旅行配置从官方 `npc_adventure_travel`、`npc_adventure`、`NpcAdventureSettings` 和 `npc` 表导出到 `unity/Assets/StreamingAssets/Home/travel-catalog.json`，并包含 NPC 配置的 `FigureImage`、`Icon`、`FigureModelUI`、`FigureModelWithWeaponUI`、`IdleAnimName` 和 `RunAnimName`。官方主面板、详情、选择页及36张地图树载入 `unity/Assets/StreamingAssets/Home/Travel/ui.json`；运行时校验固定版本 `854_156383`。官方 `NpcAdventureTravelSelectNpcItem.InitContent` 优先用 `NpcInfo.FigureImage` 调用 `LoadSprite`；资源缺失时尝试同一 NPC 配置的 `Icon`。97个可派遣 NPC 中已取得并绑定87个 `FigureImage`、2个配置 `Icon`，8个头像缺项列在生成回执中。头像缺失不再禁用伙伴选择；缺图伙伴仍按旅行中及每日次数规则处理。18个条件图标按来源绑定并记录哈希，映射按 `NpcAdventureSettings.MapGroupDatas`、`ProfessionDatas`、`PreferenceDatas` 的官方顺序对应 `NpcAdventure_1..18`。

主城入口复用原 `NpcAdventureEntry`。位置由 `HomeHudView.Travel.ResolveNpcTravelNodeFromMainline` 按用户确认规则推导：离线主线 `MapClassId` 匹配旅行节点 `MapGroupId`，且同区域 `Progress > 0`；有多个区域时按官方节点 `StageId` 取最高的已推进位置。地图位置按当前委托切换节点并显示奖励品质、概率和倒计时。详情显示官方文案、时长、基础奖励、额外奖励和职业／国度／偏好条件；伙伴选择遵循官方状态顺序，显示已结识且可派遣伙伴，旅行中或当日次数耗尽的伙伴保留在列表但不可选。快速选择沿官方贪心步骤选人。人数不足和条件未满足分别确认后派遣。每日刷新、手动刷新道具检查与确认、领取、召回和本地计时均已接线。

服务入口为 `unity/Assets/Game/Runtime/Offline/Progression/OfflineNpcTravelService.cs`，存档模型位于同目录 `OfflineNpcTravelProgress.cs`，目录和抽样位于 `OfficialNpcTravelCatalog.cs`。属性入口 `OfflineNpcTravelPropertyCalculator.Calculate` 汇总伙伴好感等级和天赋节点属性，并按官方旅行属性类型筛选；`HomeFeatureRuntime.NpcTravelNpcProperties` 按需加载。载荷生成脚本为 `scripts/prepare_unity_npc_travel_catalog.py` 和 `scripts/prepare_unity_npc_travel_ui.py`；后者导出官方 UI 索引中的旅行面板、地图树、动态头像及条件图标。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

Unity 离线旅行 UI 载荷由 `scripts/prepare_unity_npc_travel_ui.py` 从固定版 `assets/official/ui.json`、旅行 prefab 树及 NPC 配置头像生成，写入 `unity/Assets/StreamingAssets/Home/Travel/`；当前含50个面板树及243张运行贴图，伙伴头像按 `FigureImage` 优先、同一 NPC 的 `Icon` 回退，其中87个使用 FigureImage、2个使用 Icon，另有8个两种图像均缺失。派生输入／输出与稳定 `.meta` GUID 哈希记录在运行目录的 `source-manifest.json`，由 `HomeFeatureRuntime` 显式加载。NPC 11015–11022 的 `FigureImage` 和 `Icon` 在固定版、当前热更新及已核对新版同名资源包中均无可用图像；各自配置模型路由与缺项见该回执的 `missing_npc_avatars`。

## 自定义实现

候选按当前位置、地域和品质过滤，使用官方配置权重在本地抽样；用户已确认这作为服务端生成的离线近似。抽样种子与保底、委托结果放在同一档案事务内提交。首轮自动／手动刷新使用官方固定列表；多品质保底同时触发时低品质先行。稀有5、史诗30、传说100 的保底计数跨每日刷新保留，仅生成对应品质时重置。

每日按固定版 `GameSettings.DailyResetTime`（08:00）对应的本地时间切日并刷新四位置，未领取旅行保留。同日主线推进到新地图区域时，空闲位置按新位置重抽，进行中的委托及其快照保留，当日派遣／伙伴计数不重置。每天最多启动4次，每名伙伴每日1次；旅行时长和伙伴人数读取品质配置。基础奖励、额外奖励种类和额外物品在派遣事务中按本地配置抽样并保存快照，进行中属性变化不重算。额外物品按官方 `RewardRule` Percent/Weight/Count 本地抽样；单条规则内重复物品 ID 按官方 `RewardRuleInfoParser.GetRewardDropInfosByRuleList` 合并（权重相加、数量取最大值），跨规则则累加。此抽样和客户端概率／倍率公式作为用户确认的离线近似，不声称复现不可见的服务端随机源。派遣、伙伴计数和奖励快照通过 `OfflineMainlineProgressSession.ApplyInventoryTransaction` 原子提交；时钟回拨不会提前完成旅行。

召回不发奖励并恢复原委托。派遣日未切换时退还每日派遣次数和所选伙伴的当日次数；跨日后只移除旧旅行，不扣新一天的计数。该差异由用户确认。

## 尚未实现

头像实现已按官方配置改为 `FigureImage` 优先、同 NPC 的 `Icon` 回退；头像资源缺失不再锁定选择。97 名候选中 87 名有 FigureImage、2 名有 Icon，11015–11022 的两条配置路径均无可用图像。回执 `unity/Assets/StreamingAssets/Home/Travel/source-manifest.json` 记录8人的缺失配置与模型路由。

11015–11022 配置均含 `FigureModelUI`／`FigureModelWithWeaponUI`。复查固定 APK `854_156383` 的完整 AssetTable 路径、当前热更新 `871_156598` 的缓存提取物和已核对的新版同名资源记录：11015–11018 有 Spine 骨骼、图集及控制器对象，但没有对应 `NpcModel_UI` prefab；11019–11022 有模型采样对象。可取得的八人 `npc_<id>.png`、`Low.png` 与 `placeholder.png` 均为 108 字节且 SHA256 相同（`0b92327864244a52…`），不是可显示角色贴图。`FigureImage` 与 `Icon` 路由都没有可用图像。八人仍开放选择，但没有可见头像；状态窗按配置选择模型及 Idle/Run 动画仍未接入。没有用相似伙伴或占位纹理冒充官方头像。

- 官方 `NpcAdventureTravelStartStatusDialog`、`NpcAdventureTravelEndStatusDialog` 的 Spine Idle/Run 模型和换装已经按免费迁移路线采样为 Unity 原生网格数据，76 名伙伴的 UI/WithWeaponUI 共152个模型及原始贴图位于 `assets/official/npc-travel-ui/travel-models/`，回执登记在旅行来源清单。尚未将采样数据打包进 Unity StreamingAssets，或接入 UI 原生网格播放、状态窗及出发／返回演出。
- Unity Editor 编译和实际运行验收未完成。Editor 当前显示 Safe Mode 提示，尚未重新导入并确认编译结果。旅行入口、鼠标／键盘输入、奖励发放、08:00刷新、召回、时钟回拨、存档恢复和失败回滚尚未在 Play Mode 验证。

### 尚未接入的表现

官方开始／结束状态窗使用旅行 UI 面板、原生模型 Idle/Run 动画、换装贴图、计时动画和额外奖励演出。现阶段没有旅行动画运行时包，不以静态图代替。

原出发剪辑中 `CanvasGroup.m_Alpha` 路径哈希 `588897702` 在状态窗层级和官方 `NpcAdventureModelItem.InitContent` 动态插入的全部152种模型层级中都没有对应目标；固定版来源清单保留 `target_absent` 绑定。运行时不向其他控件套用这条无目标曲线，与原层级下无对象接收该绑定一致。

## 代码与资源入口

官方客户端依据：`.tools/source-packages/official-code/client-source/NpcAdventureEntry.cs`、`NpcAdventurePanelMainContent.cs`、`NpcAdventureTravelDialog.cs`、`NpcAdventureTravelConditionItem.cs`、`NpcAdventureTravelStartStatusDialog.cs`、`NpcAdventureTravelEndStatusDialog.cs`；共享公式依据：`.tools/source-packages/official-code/common-source/Common/NpcAdventureUtils.cs`、`NpcAdventureTravelInfoParser.cs`、`NpcAdventureSettings.cs`。旅行素材来源、模型对象和转换状态见[旅行原始来源与模型转换回执](../../assets/reference/npc-travel-ui/source-manifest.json)。
