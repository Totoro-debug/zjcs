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

`prepare_partner_ui.py` 校验固定 APK，并读取同版本 `871_156598`、具有 MD5 / SHA256 下载凭据的官方资源缓存，转换 `NpcPanel`、`NpcPanelNpcItem`、`NpcDetailPanel`、`ItemIconNpcGift`、`ReduceDot` 五个原始 prefab；恢复原始三列伙伴卡片、角色背景、送礼区、偏爱标记和减少按钮。通过原资源路径及祖级 atlas bundle 定位补齐伙伴立绘、头像及礼物图像，不使用模板图替代。每项原资源的 APK / 官方缓存来源、bundle 身份、对象和输出哈希见 [partner-ui/source-manifest.json](../../assets/reference/partner-ui/source-manifest.json)。

完整消费并追加 `npc_gift`、`npc_friendship_level`、`npc_friendship_tag`、`npc_talk`、`npc_unlock_item` 五张 APK 配置表。相遇、专属礼物限制、偏好倍率、累计阈值、位阶限制和自动选量对照原客户端 / 公共代码实现；礼物扣除及好感增加通过本地同一存档事务保存。原始回应使用 `npc_talk`，静态角色触摸时的小幅缩放为本地反馈适配。

固定安装包没有完整剧情 / 档案语言文件。逻辑配置路径 `Config/Binary/level_prop.g.bytes` 不位于主配置包目录，但固定 APK 的 WwiseBnk 中包含对应原始表 `assets/yoo/WwiseBnk/11e11b3ef06eaece6061f07f8f399542.rawfile`；已按 APK SHA256 `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9` 提取并用于原属性数据恢复，不属于资源缺失。未编写替代剧情或虚构属性加成。另有八位后续联动伙伴（11015–11022）的立绘、头像、背景和专属礼物图像不在当前资源中，共 32 项路径，逐项记录为待补齐；运行时可用伙伴清单保存在 `assets/official/partners.json`，这些伙伴暂不开放，避免空白卡片和缺失形象的互动。该范围不能计作完整原作剧情、档案、羁绊冒险或战斗养成已经实现。

全量资源校验仍受共享地图工作中已有的 `adventure.json` 哈希记录不一致影响；伙伴资源、四个相关数据目录和显式加载覆盖单独验证，没有改写地图预期哈希。

`scripts/prepare_partner_detail.py` 从固定 APK／已校验原版本 CDN（871_156598）读取原 `NpcDetailPanel`、`NpcDetailPanelDetailContent`、`NpcDetailLiveItem` 及 23 个专用 Live prefab，覆盖当前本地支持的 76 位伙伴（52 位动态立绘、24 位静态立绘）。背景按 `Npc.BigBgImage` 选择原 `NpcBgFavor` Sprite；大立绘按 `FullBodyLiveImage`／`FullBodyImage`、`LiveOffset`／`LiveScale` 接入，右侧模型使用 `FigureModelUI` 与原 `SkeletonGraphicProxy` 指向的主图形。静态立绘遵循原 `ImageLocator` 的尺寸、pivot 和阴影透明度。没有自绘、替代背景或使用列表头像填充大立绘。

`assets/official/partner-detail/` 保存原 prefab 字段、中文文本、动态模型采样网格与配置目录；共享 PNG 位于 `assets/official/sprites/`。原 High texture-loader 的 GUID、颜色页／染色遮罩按原配置解析；原加法与乘法材质的混合参数已登记。原 `AppearanceAnimUtils` 在指定动画不存在时保留 prefab 当前动画，此分支也按原逻辑保留。

加成说明使用原 `NpcFriendshipDialog`／`NpcFriendshipPreviewPropItem`，当前已提供属性继续来自实际好感等级与已生效的珍宝加成；预览使用原等级变化及位阶可见条件。重建：`python scripts/prepare_partner_detail.py`；来源与输出核验：`python scripts/prepare_partner_detail.py --verify`。转换需持有 `partner-detail` 工作区锁；`--reuse-meshes` 仅用于保留既有已校验网格、刷新原 prefab 绑定。

依据：[伙伴详情来源](../../assets/reference/partner-detail/source-manifest.json)。

scripts/prepare_official_partner_interactions.py 复用固定 NpcPanel／NpcPanelNpcItem／NpcDetailPanel 和礼物模板，沿 prefabs_ui_npc 的 NpcDialoguePanel、NpcDialoguePanelItem、NpcDialogueOptionBtn、NpcDialogueEmojiOptionBtn、NpcPanelDialogueItem 及 prefabs_ui_item 的 GotGrayObj 读取原层级、锚点、尺寸、LayoutGroup、TMP 排版、109 项组件字段、StateSwitcher、原 AnimationClip 和 Animator 触发映射。原 npc_dialogue.g.bytes 全表和两份官方中文语言分包提供真实对白、分支及界面文字；源码 SHA256、表／语言输入、36 项 Sprite 的原对象与 APK／同版本 CDN 来源、输出哈希在 assets/reference/partner-interactions/source-manifest.json。仅 5 张贴图为新增解码，其余复用已转换原素材；没有新版替换、自绘图或自编对白。

固定 manifest 871_156598 的 NpcPanel、NpcPanelNpcItem、NpcFriendshipLevelUpNotifyDialog／Item、NpcDetailPanel 中的 AchievementPanel，以及原 InputDialog 通过 prepare_official_partner_interactions.py 读取真实层级、RectTransform、TMP 排版、StateSwitcher／TabObjectTransition／Animator 参数和完整组件字段后迁移。关系背景通过固定 APK manifest 的真实地址解析 Level_1～4 对应 Sprite，不按文件名猜测包。完整来源见 partner-interactions/source-manifest.json：49 份原码哈希、4,454 个对象输入、213 条组件绑定与 100 项图片／音频输出，多数为既有资源复用；来源清单中的 resources／inputs／outputs 保存真实 bundle、对象及输入输出哈希。

NpcPanelNpcItem.MeetSound 指向 NpcAffinity_Get（事件 723930271），经固定版 UI Wwise 包的 Event→Action→Sound／媒体 368085030 转成 OGG；输入媒体、原分包和输出均有独立哈希。中文对白继续使用同目录 dialogues.json 和原语言包文本。用户经验补证及运行边界记在对应功能／地图文档，不记为取得服务端实现。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

### 已迁移的官方界面公共组件（局部）

`Runtime/Official/PartnersUI/` 从当前 `884_156692` 原码迁移伙伴页使用的排序、筛选、选项切换、多文本控制、信物进度和卡片显示队列。`FilterSortPanel` 保留原缓存、默认选择、重置及关闭逻辑；排序切换保留旧选项关闭、新选项开启的通知顺序及反向标记重置。筛选结果仍按原集合增删，缓存恢复保留原有结果后追加，不将传入的 `isOn` 改作另一套规则。显示队列保留首次延迟、重复卡片移至队尾、暂停保留队列及停用清空的原分支。

迁移只隔离命名空间，复用现有 `WorldUI` 的标签页、文本和状态切换器，移除编辑器专用枚举检索属性。筛选项复制保留 `UIUtils.ResetUI(All)` 的位置归零、旋转归一、缩放归一和激活操作；这些筛选组件不依赖原在线注入服务。原枚举数值和语言键生成函数保持不变。源码、处理方式与输出哈希见[界面公共组件来源](../../assets/reference/partner-ui-helpers-source-manifest.json)，重建入口为 `scripts/prepare_partner_ui_helpers.py`。

`prepare_partner_ui_validation.py` 使用实际迁移代码及实际共享 UI 源码建立隔离工程；`PartnerUiHelperValidation.Run` 在 Unity `2022.3.57f1c2` Play Mode 中以指针坐标、UI 射线命中和 pointer down／up／click 验证排序／反向、筛选重复点击、缓存、默认值、重置与开关，并验证队列延迟、去重、停止和清空。组件测试通过，已查看相机实际渲染的测试控件画面；该测试布局仅属于验证工程，不进入游戏，也不代表官方伙伴界面已还原。静态检查、主工程 Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 均通过；完整伙伴页的入口、资源绑定、Play Mode 流程及 Windows Player 仍待完成。

### 已接通的名册卡片控制器（局部）

`Runtime/Official/PartnersUI/Roster/` 从当前 `884_156692` 迁移 `NpcPanelNpcList`、`NpcPanelNpcItem`、`NpcFriendshipLevelShow` 及原滚动位置扩展。名册控制器保留显示全部、排序通知、滚动位置恢复和原显示队列；卡片保留五种相遇状态、普通／特殊名字、官方立绘／背景地址和信物进度。等待／未结识卡片打开首个专属信物，已结识卡片按当前名册过滤后的顺序打开详情。好感条按当前等级的累计经验差计算，预览叠加本次好感；满级按原全局等级上限判断。

`Offline/PartnersUI/OfflinePartnerRosterUiServices` 将控制器消费的玩家、配置、图片和窗口调用接到实际本地服务。相遇先完成信物扣除与伙伴记录的同一落盘事务，再发出原新增通知；卡片进入解锁状态、播放相遇音效，等待原 700 毫秒后刷新并请求好感提示。拒绝相遇会恢复点击状态；重复点击受请求中状态限制。卡片停用或重新绑定时取消旧的延迟提示并释放图片句柄，避免对象池中的旧伙伴影响新卡片。置顶和外部存档变化分别转发原置顶／移除／好感通知。原码、适配边界及输入输出哈希见[名册界面来源](../../assets/reference/partner-roster-ui-source-manifest.json)，重建入口为 `scripts/prepare_partner_roster_ui.py`。

`prepare_partner_roster_ui_validation.py` 复制实际运行时程序集依赖，并使用隔离存档在 Unity Play Mode 验证指针坐标与射线命中后的按钮点击、信物不足、连续点击只扣一次、先保存后通知、700 毫秒延迟、信物／详情路由、置顶落盘恢复、好感比例／预览／满级、外部存档刷新及卡片回收后的订阅和句柄释放。控制器测试通过，已查看实际相机渲染的测试夹具；夹具仅在验证工程中使用，不是官方 UI 还原成果。静态检查、主工程编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。官方 prefab、音效／红点／窗口宿主的最终绑定、完整界面流程及 Windows Player 仍未完成。

### 当前原生 prefab 字段恢复（局部）

`scripts/prepare_current_partner_native.py` 从当前 APK／已校验的配套云端资源读取官方 NPC bundle，对 `NpcPanel`、`NpcPanelNpcItem`、`NpcDetailPanel`、`NpcFriendshipDialog`、`NpcFriendshipPreviewPropItem` 和 `NpcFriendshipPropItemGroup` 逐节点核对原层级、名称、子节点和组件顺序，并以原程序集脚本身份校验转换器的 MonoScript 引用。共核对 7,004 个对象，补回 106 个被转换器省略的组件字段；原字段缓存和对象原始字节分别校验哈希。跨 prefab 引用保留目标 prefab 自身 GUID／根对象，未按相似名称匹配。

这些字段恢复中间文件保留官方原脚本身份，只在验证目录生成，不作为可玩 prefab 导入；对话动画、相遇 Wwise 音效和红点节点保留原外部对象身份，不将尚未绑定当作资源缺失。卡片本身的依赖闭合见下节，其余窗口仍待显式绑定。来源版本、原 bundle／对象、转换输入与中间输出哈希见[当前原生字段来源](../../assets/reference/partner-native-recovery-source-manifest.json)；使用已设置的独立验证目录运行上述脚本，`--record` 更新来源凭据。

### 当前原生卡片资源与提示图（局部）

`scripts/prepare_partner_card_native.py` 闭合当前 `NpcPanelNpcItem` 的资源引用：保留 72 个原层级节点和 306 个对象，绑定实际迁移脚本、原动画、31 个 Image、8 个 TMP 文本、字体图集、粒子材质和精灵网格。精灵贴图按原 SpriteAtlas 的 RenderDataKey 解析，不以文件名建立资源身份；纹理及 shader 按原 CAB／PathID、对象字节哈希复用同版本 Windows 转换结果。当前 261 个载荷及其元数据、105 份目录元数据已安装到 `unity/Assets/Official/Partners/UI/Card/`；包含原卡片、静态依赖、两条音效，以及下述名册伙伴的动态图片与字体输入。凭据及输入输出哈希见[卡片资源来源](../../assets/reference/partner-card-binding-source-manifest.json)。正式伙伴页面宿主仍未绑定。

`prepare_current_partner_native.UnityYamlLoader` 保留 Unity 未加引号的十六进制字节字段，避免 YAML 1.1 将纯数字索引当成整数／八进制，损坏原三角形。卡片及动态依赖的 117 个精灵逐项核对原顶点数及完整索引字节，保留原网格；没有改用矩形网格规避渲染失败。

`--npc-ids` 传入当前原名册范围的 77 位伙伴：`NpcType == MapExploreBoss`、排除 `NpcRoleType.StarGod` 及已确认暂停的 11015–11022。按原 `ResourceLoaderExtensions.LoadSprite` 的 `Assets/Image/` 前缀、当前资源表和原容器定位 FigureImage／SmallBgImage，共 98 个去重图片地址。原 Sprite 的 RenderDataKey 与资源表 GUID、转换 Sprite 的键对应后，再闭合原纹理与图集，形成 `partner-card-images.json` 显式加载表；每个地址保留伙伴／字段消费者。优先读取 APK，包内未提供的内容读取同版本已校验云端缓存；不混用历史版本。

同一目录保存 77 位伙伴姓名及原卡片标记所需的 78 项当前中文文本。字体从原 `MultiLanguageFontConfig` 的 CN／zh_CN 配置选择 `FZY4K SDF - dynamic`，原 CN 配置没有备用字体；按 `FontAssetFontConfig` 恢复原始 TTF，并核对原长度与 MD5。验证宿主按原 `FontAssetFontService.SetupFont` 恢复 sourceFontFile，字体变化后沿原顺序重设文字效果，不用临时生成的字体替代原字体资产。

原 `NpcAffinity_Get` 和 `Main_Cursor_Normal` 从卡片字段指向的 Wwise Event 追踪到实际 bank／Action／Sound／媒体，用官方开源工具 wwiser 与 vgmstream 解码。分别保留 0 dB／−6 dB 事件增益和 48 kHz 双声道 PCM；不以名称相近的音频替代。原事件对象、bank、媒体号、路由文本、工具和输出哈希均在同一凭据中记录。

`Runtime/Official/PartnersUI/Roster/TipDots/` 迁移原节点工厂、外接／动态连接、伙伴与档案实例身份，以及赠礼、相遇、档案、馈赠和天赋提示节点。原条件分支、事件订阅、释放和克隆顺序保留，玩家／配置／条件处理由实例服务端口提供；复用已有官方提示聚合与成就核心。节点工厂仍先克隆非禁用节点、设置实例键与预设，再恢复图关系并逐个注入服务。赠礼提示仅统计现有偏好礼物，并要求能在当前个人阶位内提升好感等级。来源、转换方式及代码哈希见[提示节点来源](../../assets/reference/partner-tipdot-source-manifest.json)，重建入口为 `scripts/prepare_partner_tipdot_nodes.py`。服务端口尚未接入完整本地提示管理器，不能据此认定名册红点流程已完成。

`prepare_partner_card_validation.py` 与 `PartnerCardBindingValidation.Run` 在 Unity `2022.3.57f1c2` 验证实际 prefab 和 25 个原提示图资产导入、九种运行节点反序列化／独立克隆、原图名称、端口归属、脚本／托管引用、字体图集、图片贴图、两条音频及 Windows 材质支持；原节点工厂对两张图实际恢复非禁用节点、连接、预设与实例键。指针逐处写入完整引用，避免 YAML 别名使 Unity 丢失端口归属。

`PartnerRosterUiValidation.RunNativeCard` 使用实际原卡片、原动画／图像／字体和隔离本地存档，以模拟鼠标射线验证信物不足、可结识、连续点击仅扣一次、700 毫秒解锁、拒绝／回收、好感比例／预览／满级、置顶与存档恢复；实际查看未结识、可结识、解锁中、已结识和外部好感更新的 GPU 画面。全部 77 位名册伙伴的图片地址、字段消费者、原图网格及全部姓名字形比对通过；实际卡片回归、提示图绑定、静态检查、主工程新增资源导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 均通过，导入后资源哈希保持一致。红点管理器和通知／信物／详情窗口使用明确验证端点，这些端点不进入游戏，不代表完整页面、红点或弹窗交互已经完成；Windows Player 尚未发布。

### 当前赠礼图标与好感属性组件（局部）

`Runtime/Official/PartnersUI/Gifts/` 从当前 `884_156692` 完整迁移 `ItemIconNpcGift`、`NpcFriendshipPropItem`、`NpcFriendshipPreviewPropItem`、`NpcFriendshipPropItemGroup` 和原 CanvasGroup 显隐扩展，复用既有官方 `ItemIcon`、`ProfessionPlayerPropItem` 与属性格式化。赠礼图标沿用偏好／选中遮罩和减少按钮订阅；重复显示同一附件不增加监听，清理先执行原物品清理、隐藏偏好标记并移除减少监听，保留原选中状态和回调字段。

属性行保留普通／旅行分支、正数增量标记和背景开关。预览首级读取完整属性、后续读取相邻等级差，按原顺序只显示正数变化；差值超过两项时记录配置错误并提前返回。旅行分支沿用原语言参数与旅行功能开关。属性组按行号取增量，未使用槽位同时关闭透明度、交互和射线。配置、功能门槛和属性表经显式服务接口提供，本地提供者与原生赠礼／详情窗口的最终绑定尚未完成。

重建入口为 `scripts/prepare_partner_gift_ui.py`，版本比对、原源码及输出哈希见[赠礼组件来源](../../assets/reference/partner-gift-ui-source-manifest.json)。`prepare_partner_gift_ui_validation.py` 在隔离 Unity Play Mode 验证实际控制器的模拟鼠标射线点击、重复绑定只触发一次、清理／复用／空回调、普通属性实际文本、正／零／负增量、旅行文本、组内按行号取增量、空槽位射线关闭、预览基准／差值／负值过滤／旅行门槛，以及超过两项差值的原诊断提前返回。已查看修正测试标签重叠后的 GPU 截图；测试控件和语言端点不进入正式游戏，不代表完整官方赠礼界面已还原。静态检查（81,115 个 GUID）、主工程编译与 `Zjcs.Editor.ProjectTools.Validate` 通过；15 项输出哈希核对通过，原生窗口绑定与 Windows Player 尚未完成。

### 当前原生好感属性模板（局部）

`scripts/prepare_partner_gift_native.py` 从当前 `884_156692` 的 `NpcFriendshipDialog.mPropItemPrefab` 实际指针提取内嵌 `RuleFormDataItem`，保留 62 个原对象、890×93 根尺寸、全部子层级、普通／旅行属性文字与两组状态绑定；仅将提取根的父指针断开，以便独立实例化。独立的同名 `NpcFriendshipPreviewPropItem.prefab` 在原资源中没有旅行文字引用，不能代替窗口实际消费的模板。原 `NpcFriendshipPropItemGroup` 的两槽属性、背景、增量文字和粒子层级一并绑定。

资源安装到 `unity/Assets/Official/Partners/UI/Friendship/`，新增 19 项载荷及其元数据，按 GUID 与完整载荷字节复用卡片目录已有的 11 项依赖。字体、精灵网格、纹理、shader、效果材质与原 `Main_Upgrade` 事件引用均追溯到实际官方对象；`AkEvent.triggerList` 从原对象解码，修复转换器写成字符串的整型列表。`TMPColorGradient` 使用已迁移完整原类，逐项复核当前所选 APK AOT 的八个方法及字节哈希。原始输入、消费指针、提取变换和安装输出哈希见[原生好感属性来源](../../assets/reference/partner-gift-native-source-manifest.json)。

`prepare_partner_gift_native_validation.py` 建立隔离工程，使用实际原模板与迁移控制器检查普通属性、等级差值、旅行开关、空槽位模拟指针射线、粒子发射和声音对象注册／销毁，全部通过。隔离宿主采用实际同版本 URP 包建立 2D 管线，以运行原 `UniversalPipeline／Universal2D` shader；已查看普通、旅行、旅行关闭及原升级光带／星光四帧 GPU 画面，原 890 宽模板完整显示。数据、配置格式化及声音主机使用明确测试端点，这两份原属性模板没有按钮／详情入口，不增加测试按钮冒充交互。74 项安装资源／目录哈希核对、静态检查、主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 均通过。完整好感窗口、正式本地属性提供者与实际音频播放尚未接通；当前模块不代表完整伙伴页或 Windows Player 已完成。

### 当前版伙伴配置输入（局部）

`scripts/prepare_current_partner_rules.py` 从选定的 `884_156692` 主 Luban 包与独立 `NpcTalentTree` 包读取 16 张完整表，使用同版本反编译构造器、条件分派和元数据解码，并逐表检查全部字节已消费。`unity/Assets/Official/Partners/partner-rules.json` 保留 148 条 NPC、115 条礼物、100 条信物、10,000 条好感阈值、385 条档案、20 条称谓、62 条互动、3 条天赋物品、两组各 252 条天赋节点／属性，以及依赖的条件、功能门槛、物品和成就表。原表没有删行或改写版本；输入包／对象、全部实际使用的构造器、输出及元数据哈希见[配置来源](../../assets/reference/partner-rules-source-manifest.json)。

与现有历史总表逐字段核对：礼物经验、信物限制、好感阈值、天赋节点和伙伴全局参数一致；NPC 表仅 `10114.LiveOffset` 不同。当前条件表另有更新，因此不能把整套历史配置当作当前版使用。这份当前版载荷尚未接入正式页面宿主及完整本地提示服务，也未替换其他功能的数据源。

配置交叉检查覆盖 859 个条件引用、好感复合键唯一性、礼物／信物物品和档案 NPC 引用。原好感表含当前名册之外的 `8403`、`8419`；原 NPC 的 `8402→1301`、`8408→1305`、`8409→1306` 专属信物引用在原物品表中没有对应定义。上述原始数据均保留，并在凭据中列明；卡片仍按原 `HasItemDefinition` 分支判断，不生成替代物品或名册条目。提取、数据／哈希校验、Unity 导入、静态检查及主工程 `Zjcs.Editor.ProjectTools.Validate` 已通过；完整交互与 Windows Player 验证仍待页面接通。

### 原面板页签控制器（局部）

`scripts/prepare_partner_panel_controller.py` 迁入当前版 `NpcPanel`、`NpcPanelTabType`，从原 `NpcPanelUtils` 与 `FunctionType` 提取伙伴／通话／星神对应的 `65／57／174` 功能门槛。字段名称、门槛遍历、默认页回退、标题、页签关闭回调及关闭窗口重置均保留；复用已有官方 `PanelTitle`、`MonoDictionary`、`WorldTabGroup`、`WorldTabItem`、`WorldStateSwitcher`。本地宿主通过 `IPartnerPanelServices` 提供功能判断与通话列表初始化，不再访问全局在线玩家。代码入口为 `Runtime/Official/PartnersUI/Roster/NpcPanel.cs`；选定官方源码及迁移输出哈希见[面板控制器来源](../../assets/reference/partner-panel-controller-source-manifest.json)。

隔离 Unity Play Mode 使用模拟鼠标射线验证三次无遮挡页签点击，以及全开、单开、全关、指定默认页被限制、重新启用、标题更新和销毁解绑。原通话页关闭回调仍先切换状态并初始化通话列表，再处理新页签；没有改写这一顺序。实际查看 GPU 截图，静态检查、主工程导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 通过。验证控件为隔离夹具，不能代表原面板视觉或完整通话功能；完整原 prefab、页面宿主、子页面和 Windows Player 尚未接通。

### 已迁移的名册与相遇状态（局部）

`Runtime/Official/Partners/NpcRosterRules.cs` 与 `OfflineCharacterProgressionService.Partners.cs` 按当前 `884_156692` 的 `NpcHelper`、`NpcPanelNpcList`、`NpcComparer` 恢复五种结识状态、名册显示条件及好感／相遇时间排序。判断依次为已结识、可结识、可显示后的等待／未结识；可结识条件优先于可显示条件。默认显示已结识及可结识伙伴；勾选全部后增加等待结识伙伴，以及拥有首个专属信物的未结识伙伴。名册限定原 `MapExploreBoss` 类别并排除星神和已确认暂停的八位联动伙伴。

排序固定优先结识状态、置顶、置顶时间升序及伙伴类型，再比较选中的好感或相遇时间，最后按伙伴号降序；反向只改变选中的排序键。置顶功能按 `function_limit` 判断，其中 `HasManualMeetNpcCondition` 按原码同时检查“已拥有手动结识伙伴”和“当前存在满足结识条件的手动伙伴”，不直接使用历史存档的布尔标记。地图可达读取 `UnlockedMapClassIds`；区域激活读取 `ActivatedGoddessRegions`，探索完成本身不等于区域激活。

相遇事务提交前重新读取存档，信物扣除、初始好感与实际相遇时间共同保存；重复事件不再次相遇或扣除。置顶及其时间单独保存，重复置顶保持原顺序。旧存档未记录的相遇时间保持零，不从注册时间或当前时间反推。来源函数、当前原码及配置哈希见 [名册规则来源](../../assets/reference/partner-roster-rules-source-manifest.json)。

`scripts/test_partner_gifts.ps1` 的隔离 Unity 编译／存档验证同时覆盖地图锁定、区域未激活／探索完成／激活后的状态变化、部分信物显示、信物不足拒绝、可结识优先、真实结识扣除、重复相遇、置顶解锁／顺序、存档恢复及旧日期兼容；排序检查覆盖反向与固定优先级、最终伙伴号比较。卡片的模拟点击与主工程验证范围见上文「已接通的名册卡片控制器」；官方名册 prefab 和实际入口尚未接通。

### 已迁移的送礼规则与存档接口（局部）

`Runtime/Official/Partners/NpcGiftRules.cs` 从当前 `884_156692` 官方源码迁移礼物展示／自动选择排序、单件和整批好感取整及自动补量。展示依次按偏好、品质、原始好感降序；自动选择按偏好降序、品质和原始好感升序、物品号降序，保留已有选择并按单件取整值向上补量。批量预览仍按同类总量乘偏好后进行四舍六入五成双取整；两种顺序不能互换。来源函数与源码哈希见 [送礼规则来源](../../assets/reference/partner-gift-rules-source-manifest.json)。

`OfflineCharacterProgressionService.NpcGiftCandidates`、`AutoSelectNpcGifts` 和 `GiveNpcGifts` 接入上述规则，按 `NpcHelper.CanSendGiftToNpc` 拦截不能赠送的信物、属于其他伙伴的专属礼物及星神非偏好礼物。已选礼物可使好感恰好达到当前位阶上限；当前好感已经达到该上限时限制继续赠送。超过全局最高经验时返回待确认，明确确认后保留超出经验；礼物扣除和好感更新共同落盘，重复事件不重复扣除。提交前重新读取存档，避免长驻伙伴服务覆盖其他功能已保存的库存。

验证：`scripts/test_partner_gifts.ps1` 编译实际 C# 规则，并使用隔离 Unity `2022.3.57f1c2` 工程编译实际养成依赖、加载工程现有官方配置，验证恰好升满、溢出取消／确认、重复提交、库存不足、存档重新读取、自动选量及 90 项其他伙伴专属礼物的排除。纯规则测试另覆盖偏好排序、已有选量、库存耗尽、两种取整差异及数值溢出。送礼仍为 Editor 中的接口与存档验证，尚未验证官方送礼界面、对应模拟输入、Play Mode 画面或 Windows Player。

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
