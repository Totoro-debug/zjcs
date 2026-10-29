# NPC伙伴、好感与餐桌通话

[总索引](../README.md)

状态：未完成

## 功能简介

铃铛桌子或功能菜单进入NPC列表与详情，餐桌进入通话；羁绊、旅行和助战各有独立说明。

史莱姆与孤独摇滚两个联动及其八位伙伴（11015–11022）由用户明确暂不开放；活动归属、姓名和全部关联入口范围统一见[暂不开放约定](offline-access.md#暂不开放的联动活动及伙伴)。

## 原始官方实现

原 NPC 名册、相遇、置顶、详情、关系称谓、好感等级／馈赠、档案及餐桌对话由 NPC 配置和对应原窗口驱动。

### 待迁移的规则与表现

原三列列表、卡片、置顶、好感／结识排序、全部显示条件、相遇按钮、信物、立绘、触摸、送礼、档案、称谓、属性和馈赠须接入。

好感按 `npc_friendship_level` 累计和个人阶位计算；专属礼物不送错伙伴，偏爱倍率及四舍六入五成双取整使用原算法。展示优先偏爱／品质／好感，自动选择采用原升序和物品号规则，补足下一等级；触摸不凭空增加好感。

称谓用原字库和中日韩长度校验。相遇按原个人条件／信物，自动与手动分别处理；相遇顺序和新相遇的本地时间均由当前存档字段记录。

餐桌使用原349段、6707节点中文对白，选项、条件暂停、历史和配置奖励保存，回看不重复领奖；馈赠显示原五项未领取组，领取须实现项。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

后续更新（2026-10-13）：伙伴交互与送礼已替换下述历史资料布局，当前来源与范围见本文末尾「伙伴相遇、互动与送礼」及 `partner-ui/source-manifest.json`。

同一固定 APK 追加完整读取 `home_visit_shop`、`home_visit_shop_goods`、`npc` 三张表，构造器及字节哈希见 rules-manifest.json。按 NPC 配置中的 `Icon` / `FigureImage` 定位并提取实际打包的图标和立绘，新增 37 项运行时资源、39 项图标映射。当前共 1,471 项资源、1,248 项图标映射、80 个 prefab、80 张完整配置表，未添加外部或生成图像。

`HomeShopPanel` 原始两个 `ShopPanelTabItem.myShopId` 为 1 / 2，分别对应杂货、宝库；特卖和馈赠分别绑定 `HomeVisitShopContent`、`AchievementPanel`。商店中的嵌入商品和馈赠行直接复用已提取的 prefab 节点；物品内部图像按原遮罩留白和等比显示，槽位缩放作用于整个 prefab。

`HomeNpcChatEntry.OnClick` 与 `DiningTableNpcPopClick.OnValidClick` 证实铃铛桌子 / 气泡为 NPC 面板入口，修正原本误接的烹饪路由。`prefabs_ui_npc.bundle` 未打包，当前为已有 NPC 原图与本地图鉴布局；完整对话和养成仍待实现。

再次核对胡椒等 7 个食材图标的地图 bundle 哈希与 APK ZIP 条目，并检索 216 个已打包图像包的容器：无对应图像。缺项仍保留于 source-manifest.json，不把锁图像当作对应食材图标已经补齐。

`prepare_partner_ui.py` 校验固定 APK，并读取同版本 `854_156383`、具有 MD5 / SHA256 下载凭据的官方资源缓存，转换 `NpcPanel`、`NpcPanelNpcItem`、`NpcDetailPanel`、`ItemIconNpcGift`、`ReduceDot` 五个原始 prefab；恢复原始三列伙伴卡片、角色背景、送礼区、偏爱标记和减少按钮。通过原资源路径及祖级 atlas bundle 定位补齐伙伴立绘、头像及礼物图像，不使用模板图替代。每项原资源的 APK / 官方缓存来源、bundle 身份、对象和输出哈希见 [partner-ui/source-manifest.json](../../assets/reference/partner-ui/source-manifest.json)。

完整消费并追加 `npc_gift`、`npc_friendship_level`、`npc_friendship_tag`、`npc_talk`、`npc_unlock_item` 五张 APK 配置表。相遇、专属礼物限制、偏好倍率、累计阈值、位阶限制和自动选量对照原客户端 / 公共代码实现；礼物扣除及好感增加通过本地同一存档事务保存。原始回应使用 `npc_talk`，静态角色触摸时的小幅缩放为本地反馈适配。

固定安装包没有完整剧情 / 档案语言文件。逻辑配置路径 `Config/Binary/level_prop.g.bytes` 不位于主配置包目录，但固定 APK 的 WwiseBnk 中包含对应原始表 `assets/yoo/WwiseBnk/11e11b3ef06eaece6061f07f8f399542.rawfile`；已按 APK SHA256 `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9` 提取并用于原属性数据恢复，不属于资源缺失。未编写替代剧情或虚构属性加成。另有八位后续联动伙伴（11015–11022）的立绘、头像、背景和专属礼物图像不在当前资源中，共 32 项路径，逐项记录为待补齐；运行时可用伙伴清单保存在 `assets/official/partners.json`，这些伙伴暂不开放，避免空白卡片和缺失形象的互动。该范围不能计作完整原作剧情、档案、羁绊冒险或战斗养成已经实现。

全量资源校验仍受共享地图工作中已有的 `adventure.json` 哈希记录不一致影响；伙伴资源、四个相关数据目录和显式加载覆盖单独验证，没有改写地图预期哈希。

`scripts/prepare_partner_detail.py` 从固定 APK／已校验原版本 CDN（854_156383）读取原 `NpcDetailPanel`、`NpcDetailPanelDetailContent`、`NpcDetailLiveItem` 及 23 个专用 Live prefab，覆盖当前本地支持的 76 位伙伴（52 位动态立绘、24 位静态立绘）。背景按 `Npc.BigBgImage` 选择原 `NpcBgFavor` Sprite；大立绘按 `FullBodyLiveImage`／`FullBodyImage`、`LiveOffset`／`LiveScale` 接入，右侧模型使用 `FigureModelUI` 与原 `SkeletonGraphicProxy` 指向的主图形。静态立绘遵循原 `ImageLocator` 的尺寸、pivot 和阴影透明度。没有自绘、替代背景或使用列表头像填充大立绘。

`assets/official/partner-detail/` 保存原 prefab 字段、中文文本、动态模型采样网格与配置目录；共享 PNG 位于 `assets/official/sprites/`。原 High texture-loader 的 GUID、颜色页／染色遮罩按原配置解析；原加法与乘法材质的混合参数已登记。原 `AppearanceAnimUtils` 在指定动画不存在时保留 prefab 当前动画，此分支也按原逻辑保留。

加成说明使用原 `NpcFriendshipDialog`／`NpcFriendshipPreviewPropItem`，当前已提供属性继续来自实际好感等级与已生效的珍宝加成；预览使用原等级变化及位阶可见条件。重建：`python scripts/prepare_partner_detail.py`；来源与输出核验：`python scripts/prepare_partner_detail.py --verify`。转换需持有 `partner-detail` 工作区锁；`--reuse-meshes` 仅用于保留既有已校验网格、刷新原 prefab 绑定。

依据：[伙伴详情来源](../../assets/reference/partner-detail/source-manifest.json)。

scripts/prepare_official_partner_interactions.py 复用固定 NpcPanel／NpcPanelNpcItem／NpcDetailPanel 和礼物模板，沿 prefabs_ui_npc 的 NpcDialoguePanel、NpcDialoguePanelItem、NpcDialogueOptionBtn、NpcDialogueEmojiOptionBtn、NpcPanelDialogueItem 及 prefabs_ui_item 的 GotGrayObj 读取原层级、锚点、尺寸、LayoutGroup、TMP 排版、109 项组件字段、StateSwitcher、原 AnimationClip 和 Animator 触发映射。原 npc_dialogue.g.bytes 全表和两份官方中文语言分包提供真实对白、分支及界面文字；源码 SHA256、表／语言输入、36 项 Sprite 的原对象与 APK／同版本 CDN 来源、输出哈希在 assets/reference/partner-interactions/source-manifest.json。仅 5 张贴图为新增解码，其余复用已转换原素材；没有新版替换、自绘图或自编对白。

固定 manifest 854_156383 的 NpcPanel、NpcPanelNpcItem、NpcFriendshipLevelUpNotifyDialog／Item、NpcDetailPanel 中的 AchievementPanel，以及原 InputDialog 通过 prepare_official_partner_interactions.py 读取真实层级、RectTransform、TMP 排版、StateSwitcher／TabObjectTransition／Animator 参数和完整组件字段后迁移。关系背景通过固定 APK manifest 的真实地址解析 Level_1～4 对应 Sprite，不按文件名猜测包。完整来源见 partner-interactions/source-manifest.json：49 份原码哈希、4,454 个对象输入、213 条组件绑定与 100 项图片／音频输出，多数为既有资源复用；来源清单中的 resources／inputs／outputs 保存真实 bundle、对象及输入输出哈希。

NpcPanelNpcItem.MeetSound 指向 NpcAffinity_Get（事件 723930271），经固定版 UI Wwise 包的 Event→Action→Sound／媒体 368085030 转成 OGG；输入媒体、原分包和输出均有独立哈希。中文对白继续使用同目录 dialogues.json 和原语言包文本。用户经验补证及运行边界记在对应功能／地图文档，不记为取得服务端实现。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

服务端通话创建／最终确认方法未取得。用户补证已完成前段并满足后续条件时可当天续接，超出好感最高值经原浪费确认后仍累计；本地按此保存，不反推旧结识日期。

送礼扣库存、好感、档案／通话奖励与历史共用本地事务，失败回滚；相遇仅由当前格式的伙伴记录与实际相遇流程决定。

## 尚未实现

- NPC名册、相遇／结识、详情、送礼、好感、档案与餐桌通话；活动开放范围以离线政策为准。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。
- 史莱姆、孤独摇滚的八位伙伴及全部相关入口按[离线政策](offline-access.md#暂不开放的联动活动及伙伴)暂停，不列为自动开放目标。

## 代码与资源入口

[NpcDialoguePanel](../../.tools/source-packages/official-code/client-source/NpcDialoguePanel.cs)、

[`NpcPanelNpcItem.Refresh`](../../.tools/source-packages/official-code/client-source/NpcPanelNpcItem.cs)、[`NpcDetailPanelDetailContent.Refresh`](../../.tools/source-packages/official-code/client-source/NpcDetailPanelDetailContent.cs)

[`NpcAdventureModelItem.InitContent`](../../.tools/source-packages/official-code/client-source/NpcAdventureModelItem.cs)、[伙伴图像来源清单](../../assets/reference/partner-ui/source-manifest.json)、[档案来源清单](../../assets/reference/npc-cultivation/source-manifest.json)、[对话来源清单](../../assets/reference/partner-interactions/source-manifest.json)

[羁绊助战模型来源清单](../../assets/reference/npc-adventure-models/source-manifest.json)、
