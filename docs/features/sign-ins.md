# 主题签到与长期活动入口

[总索引](../README.md)

状态：未完成

## 功能简介

各主题签到及长期活动分别读取官方签到天数、物品、状态、活动入口与原动态展示；活动开放和本地领取回执属于单机接线。

## 原始官方实现

各主题签到及长期活动分别读取官方签到天数、物品、状态、活动入口与原动态展示；活动开放和本地领取回执属于单机接线。

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

尚无完整 Unity 流程的完成结论。

## 自定义实现

开放与本地保存遵循[共同离线政策](offline-access.md)，具体本地接线和未确认规则见下文。

## 尚未实现

接通官方代码、原生界面／资源、本地状态与存档，完成实际入口、模拟输入、画面及失败恢复验证，并发布对应 Windows 版本。
