# 主题签到与长期活动入口

[总索引](../README.md)

状态：未完成

## 功能简介

本页覆盖当前开放的29套官方主题签到、成长试炼、宾果、刮刮乐、赛季七日任务100～105，以及自定义的签到表2入口。活动216与223及其全部关联内容按用户决定暂不开放，见[暂不开放约定](offline-access.md#暂不开放的联动活动及伙伴)。主题活动的关联小游戏范围见[活动小游戏](activity-minigames.md)；签到可领不代表关联玩法完成。

## 原始官方实现

各主题签到及长期活动分别读取官方签到天数、物品、状态、活动入口与原动态展示；活动开放和本地领取回执属于单机接线。

### 待迁移的规则与表现

官方主题签到使用原活动卡片、签到面板、奖励格、签到状态、领奖反馈与返回流程。每日签到一次；中断后继续领取下一档，不要求连续签到，不会在第七档后自动开启新轮。各活动奖励、个人条件、性别／职业限制、7处换装预览和动态展示按原配置与已转换资源运行。原窗口生命周期、遮罩点击保护、分格演出、跨日刷新和奖励弹窗状态由本地界面接续。

赛季100～105使用固定版本的六套 `SevenDayTaskPanelSeason1`～`Season5`、`SevenDayTaskPanelS6` 原面板，以及对应的任务行和积分奖励格。任务页签按官方 `TaskGroupType` 展示地图探索、赛季养成、赛季玩法三组；每组按完成条件类型分组、按 `ClassId` 排序，显示须实现任务和当前任务。任务行展示本地可确定的当前／目标进度、赛季积分与配置奖励；积分条、阈值奖励、职业奖励、已领状态和领取去重使用原活动及奖励配置。

赛季配置的1255条任务、16种完成条件均接入本地任务状态与进度计算。领取仍经过任务归属、开放条件、完成条件、奖励和收据校验，不直接补写完成状态或跳过原奖励规则。

规则与实现定位：（`seasonal_growth`、`seasonal_task_ids`、`seasonal_task_list`、`seasonal_score_rewards`）、（`task_state`、`task_progress`）、（`execute`）。配置表为 `activity_sign_days`、`seven_day_task_activity`、`seven_day_task`、`seven_day_task_group`、`seven_day_task_phase`、`seven_day_task_score_reward`；赛季活动 ID 为100～105。

对应官方代码为 `ActivityEntrySevenDayTaskHandler.OnActivityOpen`、`SevenDayTaskPanel`、`SevenDayTaskSubPanelTaskWithTaskGroup`、`SevenDayTaskGroup.GetDisplayTask`、`TaskListLiteItemForProgressTask.RefreshProgress`、`TaskListLiteItemForSevenDayTask.RefreshAward` 和 `SevenDayTaskScoreRewardItem`，源文件位于固定版本 `.tools/source-packages/official-code/client-source/`。提取与输出登记见[活动 UI 来源清单](../../assets/reference/activity-ui/source-manifest.json)及[素材来源总览](../../assets/reference/SOURCES.md)。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_activity_ui.py` 从固定 `854_156383` APK / 匹配版本官方 CDN 缓存解码 `ActivityEntryPanel` 所引用的图片卡片与签到、SevenDayTask、BingoActivity、ScratchCardActivity 界面。此次登记 109 个原始 prefab、757 项运行资源、13 张完整消费的活动配置表，以及同版本中文语言表；运行目录为 `assets/official/activities.json`，动态贴图进入显式加载清单。各项原始包名、哈希、对象路径及输出 SHA256 见 [活动来源清单](../../assets/reference/activity-ui/source-manifest.json)。没有生成、截图裁切或重绘素材。

当次转换有 32 个入口（29 个主题签到 + 成长试炼 / 宾果 / 刮刮乐）；主题活动只开放已接入的签到模块，其余节日小游戏仍待迁移。取消活动开始 / 结束排期、倒计时及开服等待；签到每日一次，宾果 / 刮刮乐原始 30 套配置分别保存进度。刮卡的本地预抽结果和三个匹配图案结算属于离线规则适配，不声称已取得服务器实现。

后续补入赛季七日任务100～105的六套原面板、六套任务行及六套积分格，来自固定版本的 `prefabs_ui_seasonactivity_season1`～`season5` 与 `prefabs_ui_activity_s6`。当前 [活动 UI 来源清单](../../assets/reference/activity-ui/source-manifest.json)登记127个 prefab、801项运行资源；`ActivityEntrySevenDayTaskHandler.OnActivityOpen` 按 `DisplayCommonEntry` 过滤通用入口，赛季目录卡片为本地导航，面板与配置仍为原版。签到表2仅在 `activity_sign_days` 存在 ID 2 奖励配置，没有对应原导航或专用面板；本地入口复用已登记的官方通用签到面板，独立进度按自定义单机规则保存。

检索固定版本完整资源清单和本地已缓存容器后，`ActivitySlime/Sign/ActivitySlimeSignDialog` 未定位到资源包；物品 `1478`、`1487`、`35968`、`35986`、`35987` 的 RawIcon 未定位到 Sprite，均在来源清单 `missing` 中保留。孤独摇滚签到的背景和 NPC Sprite 实际为 16px 白色占位，真实美术未取得，因此不放入活动目录。不使用其他图片冒充。部分原始动态角色预览仍未转换，以上 prefab / 图集已提取不代表该动画已迁移。历史“签到专有面板缺失、活动文字入口”的说明保留为此前状态，以本节和运行时清单为当前状态。

`scripts/prepare_activity_completion.py` 独立输出 `assets/official/activity-completion/`，来源清单为 `activity-completion/source-manifest.json`，Unity 显式贴图绑定尚待接入。输入均为固定 APK 与经过原版本清单校验的 CDN 依赖；保留原 prefab、组件 path_id、原骨骼／图集／初始动画、Skin、材质、RectTransform 和转换输出 SHA256。7 个静态角色／宠物 SkeletonGraphic 使用原初始动画逐帧转换，不自绘；火锅签到嵌入面板的 ImgStove 来自原 RawImage 指向的 Texture2D，保留原面板的初始可见状态。

中秋原 prefab 存在重复同名对象，改按原对象身份建立唯一内部路径；节点层级、顺序、坐标及奖励不变。官方通用 ActivitySignDialog1／ActivitySignDialogItem1 用于用户明确授权的两处缺失处理：史莱姆 216 的专有签到／入口 prefab 不在完整固定版清单；孤独摇滚 223 的专有签到 prefab 存在，但背景和角色引用为白色占位。通用面板隐藏无关 IP 的人物、标志和介绍，使用真实活动标题及原奖励；14 天奖励在原槽位范围内滚动。该处理属于授权替代，不称为专有面板还原。

1478、1487、35968、35986、35987 的精确图像路径不在固定版完整资源清单，已解析礼物／外观 Sprite 容器中也无相符对象；现存 185 个新版同名缺包记录不提供这些图标。保留原品质框、数量和真实名称，不画或借用其他物品图片。7 处玩家奖励换装预览仍未完成迁移，与这五项图像缺失分开记录；未将“尚未接入”登记为官方资源不存在。

`prepare_activity_sign.py` 从固定854_156383签到 prefab 提取 `PopupWindow`、签到物品、状态切换、`ListItemShowSequence`、`PlayerAnimationAppearance` 和提示文字组件；复用既有原图片，另转换两张原品质框至 `assets/official/activity-sign/`。原开窗／循环／关窗曲线、`StartShow` 动画事件和职业皮肤组的输入哈希见 [activity-sign/source-manifest.json](../../assets/reference/activity-sign/source-manifest.json)。

`prepare_sign_appearance.py` 转换27种签到外观及绑定皮肤，7个原角色预览窗口使用男女原骨骼、完整分辨率图集、染色遮罩和24帧原待机动作，武器沿原 prefab 的骨骼／插槽／附件跟随。运行资源位于 `assets/characters/sign-preview/`；原脚本、组件、模型来源、复用输入及输出哈希见 [sign-preview/source-manifest.json](../../assets/reference/sign-preview/source-manifest.json)。未使用新版替代包或自绘素材。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

原客户端通过 `DisplayCommonEntry` 控制七日任务是否进入通用活动列表，赛季100～105不显示普通活动卡片；本地目录增加了便于访问的入口卡片，面板、任务行、积分格、奖励和条件配置仍使用固定版官方内容。赛季服务端的 `TaskManager` 处理中状态不可离线取得；本地按配置顺序显示已完成任务和首个未完成任务，并使用已有本地玩法状态或事件记录计算进度。

用户确认移除服务器活动起止日期和开服等待，开放中的主题签到长期可领且不过期，保留原个人条件与每日一次；此排期与领取期限属于单机适配。

固定版提供 `activity_sign_days` 签到表2（ID 2）的七日奖励，但没有对应导航和专用签到面板。按本地离线入口实现，目录新增“签到表2”，复用官方通用七日签到面板与格子，奖励直接读取 ID 2 配置，独立保存天数和每日领取时间；该入口及表2导航属于自定义实现，不作为官方入口还原。

官方 `LegionFightJoinCountNewRecordCondition` 只读取服务端任务记录。按用户确认的离线规则，每次通过公会战攻击校验并实际发起攻击计1次；报名、无效目标、已摧毁据点和次数耗尽均不计数。计数接入（`combat_action`）与（`emit`、`amount`），属于已确认的单机适配。

## 尚未实现

- 已授权开放主题的签到、原奖励格、换装预览、赛季100～105七日任务与积分领奖；暂停联动的入口继续关闭。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。

## 代码与资源入口

[活动 UI 来源清单](../../assets/reference/activity-ui/source-manifest.json)
