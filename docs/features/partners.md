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

`scripts/prepare_partner_card_native.py` 闭合当前 `NpcPanelNpcItem` 的资源引用：保留 72 个原层级节点和 306 个对象，绑定实际迁移脚本、原动画、31 个 Image、8 个 TMP 文本、字体图集、粒子材质和精灵网格。精灵贴图按原 SpriteAtlas 的 RenderDataKey 解析，不以文件名建立资源身份；纹理及 shader 按原 CAB／PathID、对象字节哈希复用同版本 Windows 转换结果。当前 281 个载荷及其元数据、115 份目录元数据已安装到 `unity/Assets/Official/Partners/UI/Card/`；包含原卡片、静态依赖、两条音效，以及下述名册伙伴的动态图片与字体输入。凭据及输入输出哈希见[卡片资源来源](../../assets/reference/partner-card-binding-source-manifest.json)。正式伙伴页面宿主仍未绑定。

`prepare_current_partner_native.UnityYamlLoader` 保留 Unity 未加引号的十六进制字节字段，避免 YAML 1.1 将纯数字索引当成整数／八进制，损坏原三角形。卡片及动态依赖的 117 个精灵逐项核对原顶点数及完整索引字节，保留原网格；没有改用矩形网格规避渲染失败。

`scripts/prepare_partner_roster_scope.py` 按原 `NpcInfoParser.GetNpcs(ignoreType)` 的排除语义恢复 88 位候选：排除 `NpcType.MapExploreBoss`（2）、`NpcRoleType.StarGod`（2）及已确认暂停的 11015–11022。商人和职业导师也按原相遇／显示条件参与名册；不将参数误作普通伙伴筛选。按原 `ResourceLoaderExtensions.LoadSprite` 的 `Assets/Image/` 前缀、当前资源表和原容器定位 FigureImage／SmallBgImage，共 108 个去重图片地址。原 Sprite 的 RenderDataKey 与资源表 GUID、转换 Sprite 的键对应后，再闭合原纹理与图集，形成 `partner-card-images.json` 显式加载表；每个地址保留伙伴／字段消费者。优先读取 APK，包内未提供的内容读取同版本已校验云端缓存；不混用历史版本。

同一目录保存名册名称与原卡片标记的 89 项显示值。88 个候选中，10611 的原 NameKey 为 `ui_npc_name_10609`；选定版本的原启动／游戏中文二进制语言表均无此键，沿原 `LocalizationBinaryTextService.TryGetOriginalText → GetText → LocalizationTextService.GetMissingKeyText` 返回键名，不编造角色名称。该 NPC 的原相遇条件为 `Alwaysfalse`，正常名册流程不会显示它。字体从原 `MultiLanguageFontConfig` 的 CN／zh_CN 配置选择 `FZY4K SDF - dynamic`，原 CN 配置没有备用字体；按 `FontAssetFontConfig` 恢复原始 TTF，并核对原长度与 MD5。验证宿主按原 `FontAssetFontService.SetupFont` 恢复 sourceFontFile，字体变化后沿原顺序重设文字效果，不用临时生成的字体替代原字体资产。

原 `NpcAffinity_Get` 和 `Main_Cursor_Normal` 从卡片字段指向的 Wwise Event 追踪到实际 bank／Action／Sound／媒体，用官方开源工具 wwiser 与 vgmstream 解码。分别保留 0 dB／−6 dB 事件增益和 48 kHz 双声道 PCM；不以名称相近的音频替代。原事件对象、bank、媒体号、路由文本、工具和输出哈希均在同一凭据中记录。

`Runtime/Official/PartnersUI/Roster/TipDots/` 迁移原节点工厂、外接／动态连接、伙伴与档案实例身份，以及赠礼、相遇、档案、馈赠和天赋提示节点。原条件分支、事件订阅、释放和克隆顺序保留，玩家／配置／条件处理由实例服务端口提供；复用已有官方提示聚合与成就核心。节点工厂仍先克隆非禁用节点、设置实例键与预设，再恢复图关系并逐个注入服务。赠礼提示仅统计现有偏好礼物，并要求能在当前个人阶位内提升好感等级。来源、转换方式及代码哈希见[提示节点来源](../../assets/reference/partner-tipdot-source-manifest.json)，重建入口为 `scripts/prepare_partner_tipdot_nodes.py`。服务端口尚未接入完整本地提示管理器，不能据此认定名册红点流程已完成。

`prepare_partner_card_validation.py` 与 `PartnerCardBindingValidation.Run` 在 Unity `2022.3.57f1c2` 验证实际 prefab 和 25 个原提示图资产导入、九种运行节点反序列化／独立克隆、原图名称、端口归属、脚本／托管引用、字体图集、图片贴图、两条音频及 Windows 材质支持；原节点工厂对两张图实际恢复非禁用节点、连接、预设与实例键。指针逐处写入完整引用，避免 YAML 别名使 Unity 丢失端口归属。

`PartnerRosterUiValidation.RunNativeCard` 使用实际原卡片、原动画／图像／字体和隔离本地存档，以模拟鼠标射线验证信物不足、可结识、连续点击仅扣一次、700 毫秒解锁、拒绝／回收、好感比例／预览／满级、置顶与存档恢复；实际查看未结识、可结识、解锁中、已结识和外部好感更新的 GPU 画面。全部名册候选的图片地址、字段消费者、原图网格及显示文字字形纳入验证；实际卡片回归、提示图绑定、静态检查、主工程新增资源导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 均通过，导入后资源哈希保持一致。红点管理器和通知／信物／详情窗口使用明确验证端点，这些端点不进入游戏，不代表完整页面、红点或弹窗交互已经完成；Windows Player 尚未发布。

本轮新增 `OfficialPartnerRosterAssetCatalog` 与 `PartnerRosterPanelView`：显式绑定已安装的原 `NpcRosterContent` 子树、108 个原卡片 Sprite 及两份原中文文本；Home 功能菜单的伙伴入口会实例化该官方层级，并将 `OfflinePartnerRosterUiServices` 注入原列表／对象池，卡片相遇、好感、置顶和礼物信物路由继续写入本地存档。生成与输入输出哈希见 [名册运行时绑定来源](../../assets/reference/partner-roster-runtime-source-manifest.json)。当前只接通名册入口和卡片层，完整 `NpcPanel` 宿主、详情／送礼页面和 Windows Player 仍需继续接入并验证。

名册范围修正的原方法与输入输出哈希见[名册范围来源](../../assets/reference/partner-roster-scope-source-manifest.json)。相遇条件中的 `HasManualMeetNpc` 同样使用“排除探索首领”的原枚举范围，已结识商人／导师可满足原置顶解锁条件。重建卡片还逐对象恢复原 `MultiLanguageText.myKey` 空字符串，修正旧转换将其写成 `null` 的差异；除此字段及动态加载表外，原卡片与既有依赖字节保持不变。

`PartnerRosterTransactionValidation` 已通过商人 8411、导师 8404 的名册／置顶条件与探索首领 3601 排除回归，并复核地图／区域、信物不足、相遇扣物品、置顶和存档恢复。原生名册在隔离 URP Play Mode 对这两类实际卡片执行模拟射线点击，连同全部显示、排序、反向、遮罩关闭、滚动和池释放检查通过，已查看五帧 GPU 画面。原生卡片验证逐项核对 88 个候选的 176 个图片消费者、108 个地址、89 项显示值、字形和网格；相遇／好感／置顶交互及五帧运行画面已复核，资源、图节点和音效实际引用检查通过。静态检查（81,651 个 GUID）、主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过，安装资源哈希保持一致。完整页面、正式弹窗与 Player 仍未完成。

### 当前原生名册区域（局部）

`scripts/prepare_partner_roster_native.py` 从选定 `884_156692` 的原 `NpcPanel.mNpcListGo` 实际指针提取完整名册子树（1,159 个对象），只断开根的父引用；三列循环列表、卡片源、全部显示、好感／结识排序、反向、排序面板及开合动画的原层级、坐标与字段保留。根仍按原配置默认关闭，由窗口页签宿主激活；当前不包含完整 `NpcPanel` 的其他页签、对话与详情宿主。

资源入口为 `unity/Assets/Official/Partners/UI/Roster/`，新增 19 项载荷，按原 GUID 和完整载荷字节复用已有 10 项依赖。卡片另按 CAB／PathID、原 GUID、安装凭据和完整字节核对后复用。原动画曲线的 MonoScript 引用使用程序集 GUID 与脚本 fileID 联合识别，保留不同组件共享同一程序集 GUID 的情况；排序控制器逐项核对状态、过渡、动作、条件和整数参数默认值。`TabObjectTransition`、`StateAnimatorConfig`、`StateAnimatorData` 从同版官方源码迁入，仅适配命名空间、既有状态端口和反编译枚举语法；原显隐顺序、参数分支、Trigger 重置和即时更新不变。原对象、字体／贴图／精灵／shader 依赖及源码、转换与安装哈希见[原生名册来源](../../assets/reference/partner-roster-native-source-manifest.json)。

生成器将原名册的 `MultiLanguageText` 显式绑定到伙伴 UI 使用的 `WorldMultiLanguageText`，不按同名类自动选择另一个 `NativeLabelUI` 实现。重新生成的 prefab 与已安装资源 SHA-256 一致；隔离 Play Mode 再次通过 21 张本地伙伴记录、全部显示、排序／倒序、遮罩关闭、卡片点击、滚动和池租约释放，并已查看真实 GPU 画面。

三份原开合动画中六处根曲线路径按 APK 的路径哈希 0、属性 CRC、组件类型和脚本指针联合核对，恢复转换缓存误写为 `null` 的空路径；否则透明遮罩虽随面板隐藏仍会截获卡片点击。排序标题、原筛选标签与实际开放的两种排序文字从同版语言分包绑定，未为隐藏的通用选项编造缺失翻译。

`prepare_partner_roster_native_validation.py` 在隔离 URP Play Mode 使用实际原区域、卡片及本地名册服务／存档，模拟射线点击全部显示、结识排序、反向、遮罩关闭与卡片路由，核对初始／打开／关闭时遮罩真实启停、实际滚轮位移、重开后的原状态和全部图片资源租约释放，均通过。验证工程须一并复制原 `CombatAppearance` 程序集依赖，供赠礼和名册控制器共同使用的角色外观接口完成编译。已查看默认、全部显示、排序展开、关闭和滚动五帧 GPU 画面；50 项输入、72 项安装资源／目录、6 项迁移代码及验证源码哈希一致，静态检查、主工程导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 通过。此夹具仍使用既有历史名册规则目录，当前版条件接线、正式完整窗口／详情入口、提示管理、实际声音播放和 Windows Player 尚未完成；弹窗路由、提示及 Wwise 事件由明确测试端点承担，不进入产品。

### 当前赠礼图标与好感属性组件（局部）

`Runtime/Official/PartnersUI/Gifts/` 从当前 `884_156692` 完整迁移 `ItemIconNpcGift`、`NpcFriendshipPropItem`、`NpcFriendshipPreviewPropItem`、`NpcFriendshipPropItemGroup` 和原 CanvasGroup 显隐扩展，复用既有官方 `ItemIcon`、`ProfessionPlayerPropItem` 与属性格式化。赠礼图标沿用偏好／选中遮罩和减少按钮订阅；重复显示同一附件不增加监听，清理先执行原物品清理、隐藏偏好标记并移除减少监听，保留原选中状态和回调字段。

属性行保留普通／旅行分支、正数增量标记和背景开关。预览首级读取完整属性、后续读取相邻等级差，按原顺序只显示正数变化；差值超过两项时记录配置错误并提前返回。旅行分支沿用原语言参数与旅行功能开关。属性组按行号取增量，未使用槽位同时关闭透明度、交互和射线。配置、功能门槛和属性表经显式服务接口提供，当前本地提供者及原生好感窗口的接线见下文，原生赠礼／正式详情页最终绑定尚未完成。

重建入口为 `scripts/prepare_partner_gift_ui.py`，版本比对、原源码及输出哈希见[赠礼组件来源](../../assets/reference/partner-gift-ui-source-manifest.json)。`prepare_partner_gift_ui_validation.py` 在隔离 Unity Play Mode 验证实际控制器的模拟鼠标射线点击、重复绑定只触发一次、清理／复用／空回调、普通属性实际文本、正／零／负增量、旅行文本、组内按行号取增量、空槽位射线关闭、预览基准／差值／负值过滤／旅行门槛，以及超过两项差值的原诊断提前返回。已查看修正测试标签重叠后的 GPU 截图；测试控件和语言端点不进入正式游戏，不代表完整官方赠礼界面已还原。静态检查（81,115 个 GUID）、主工程编译与 `Zjcs.Editor.ProjectTools.Validate` 通过；15 项输出哈希核对通过，原生窗口绑定与 Windows Player 尚未完成。

### 当前原生好感属性模板（局部）

`scripts/prepare_partner_gift_native.py` 从当前 `884_156692` 的 `NpcFriendshipDialog.mPropItemPrefab` 实际指针提取内嵌 `RuleFormDataItem`，保留 62 个原对象、890×93 根尺寸、全部子层级、普通／旅行属性文字与两组状态绑定；仅将提取根的父指针断开，以便独立实例化。独立的同名 `NpcFriendshipPreviewPropItem.prefab` 在原资源中没有旅行文字引用，不能代替窗口实际消费的模板。原 `NpcFriendshipPropItemGroup` 的两槽属性、背景、增量文字和粒子层级一并绑定。

资源安装到 `unity/Assets/Official/Partners/UI/Friendship/`，新增 19 项载荷及其元数据，按 GUID 与完整载荷字节复用卡片目录已有的 11 项依赖。字体、精灵网格、纹理、shader、效果材质与原 `Main_Upgrade` 事件引用均追溯到实际官方对象；`AkEvent.triggerList` 从原对象解码，修复转换器写成字符串的整型列表。`TMPColorGradient` 使用已迁移完整原类，逐项复核当前所选 APK AOT 的八个方法及字节哈希。原始输入、消费指针、提取变换和安装输出哈希见[原生好感属性来源](../../assets/reference/partner-gift-native-source-manifest.json)。

`prepare_partner_gift_native_validation.py` 建立隔离工程，使用实际原模板与迁移控制器检查普通属性、等级差值、旅行开关、空槽位模拟指针射线、粒子发射和声音对象注册／销毁，全部通过。隔离宿主采用实际同版本 URP 包建立 2D 管线，以运行原 `UniversalPipeline／Universal2D` shader；已查看普通、旅行、旅行关闭及原升级光带／星光四帧 GPU 画面，原 890 宽模板完整显示。数据、配置格式化及声音主机使用明确测试端点，这两份原属性模板没有按钮／详情入口，不增加测试按钮冒充交互。74 项安装资源／目录哈希核对、静态检查、主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 均通过。后续完整好感窗口与当前本地属性提供者见下文，实际音频播放尚未接通；当前模块不代表完整伙伴页或 Windows Player 已完成。

### 当前原生好感详情窗口（局部）

当前主城名册卡片已可打开恢复的官方 `NpcDetailPanelPropContent` 属性子树。`HomeFeatureRuntime` 显式加载 `OfficialPartnerDetailPropertyAssetCatalog`、伙伴属性配置和中文文本，`PartnerDetailPanelView` 复用官方 prefab、`NpcDetailPanelPropContent.Configure/Init`、官方 `PartnerDetailUiUtils` 对象创建和属性行布局；属性值由 `OfflinePartnerDetailPropertySource` 读取当前存档的好感等级／天赋节点并执行原 `PartnerNpcPropertyFormula`。收藏按钮通过 `OfflinePartnerDetailPropertyServices.SetCollected` 写入当前角色存档，窗口关闭时恢复全局对象池和本地化绑定。详情中的等级简报和导航由 Home 本地端点承接：等级使用主角当前等级／阶位，导航在尚未有离线位置规则时保持关闭；赠礼和好感说明弹窗现已由原控制器回调打开原生 prefab。

`Runtime/Official/PartnersUI/FriendshipDialog/` 从当前 `884_156692` 完整迁入 `NpcFriendshipDialog` 控制器，保留原偏好礼物排序／图标回收、连续规则编号、助战好感条件、普通／星神说明、旅行开关和阶位属性预览。仅以显式接口替换原配置、玩家、属性和图标管理器注入；池化预览先配置服务再执行原初始化，不改数值与分支。

`scripts/prepare_partner_friendship_dialog_native.py` 恢复原窗口全部 311 个对象及原坐标／层级，绑定完整原 `PopupWindow`、开合 AnimationClip、`PopupWindowSound`、布局限制、镜像图片、Bloom 标记及预览工具。资源位于 `unity/Assets/Official/Partners/UI/FriendshipDialog/`；19 项载荷中 8 项按原 GUID 与完整字节复用既有伙伴依赖。中文文字从所选官方语言分包提取，原 Wwise 事件引用保留；输入、源码／组件绑定、依赖和安装哈希见[原生详情窗口来源](../../assets/reference/partner-friendship-dialog-source-manifest.json)。

`prepare_partner_friendship_dialog_validation.py` 的隔离 URP Play Mode 使用实际原窗口，验证开合动画及事件顺序、助战条件显示／隐藏、原阶位加一过滤、跳过一级、18 行属性池化复用、普通／星神说明、旅行列隐藏与重排，以及实际射线路径的模拟滚轮和遮罩鼠标点击关闭。已查看打开、滚动及旅行受限三帧 GPU 画面；动态字体／图集在内存副本中生成字形，38 项测试资源与原暂存载荷字节一致。静态检查（81,228 个 GUID）、主工程导入／编译、`Zjcs.Editor.ProjectTools.Validate` 和导入后 59 项资源／代码／目录哈希核对通过。属性、语言、窗口管理和声音端点为明确测试宿主，遮罩由测试宿主提供，不写入原 prefab。该夹具覆盖空礼物偏好，非空图标的原生集成验证见下节；当前本地配置／属性提供者验证见下节；实际声音播放、页面入口、存档联动及 Windows Player 尚未验证，本节不表示完整伙伴系统已接通。

### 当前好感属性与详情窗口本地提供者（局部）

`Offline/PartnersUI/OfflinePartnerFriendshipProperties` 将原属性组件接到当前 `OfficialPartnerCatalog` 的完整等级属性与相邻等级差值，不另写属性公式；委派属性类型读取原 `NpcAdventureSettings.TravelNpcPropTypeList`（6010、6011、6012），旅行功能限制由当前宿主实时提供。`OfflinePartnerFriendshipDialogServices` 投影当前伙伴、好感等级表及实时个人阶位，原窗口继续执行跳过一级和 `SubRank + 1` 的可见等级筛选。助战提示严格读取原窗口使用的 `CanMeetCondition` 好感条件分支，不改用 `AssistCondition`；当前配置没有符合该分支的伙伴时隐藏提示。

`prepare_partner_property_ui_configuration.py` 完整读取当前 Luban 的 891 条 `entry`，与既有 352 条 `prop_cfg` 组合，核对全部 49 个属性词条引用，并从所选官方中文语言包提取 215 项伙伴名、相关属性及词条文本。配置位于 `Assets/Official/Partners/partner-property-ui.json` 和 `partner-property-texts.json`。格式化复用 `OfflineProfessionPropertyBindings` 与原属性格式函数；偏好图标排序迁移原 `PlayerTaskDataUtils.sRewardItemComparer` 实际调用的 `RewardPreviewItemComparer.RewardItemComparer`，保留指定经验／货币优先、品质降序和物品号升序及原比较执行顺序。来源见[属性界面配置](../../assets/reference/partner-property-ui-source-manifest.json)、[奖励排序](../../assets/reference/partner-reward-comparer-source-manifest.json)和[本地服务绑定](../../assets/reference/partner-friendship-services-source-manifest.json)。

`Resources/Partners/OfficialPartnerFriendshipAssetCatalog.asset` 显式引用原生好感详情 prefab、原窗口语言、属性配置和属性语言，提供本地运行时加载入口。`prepare_partner_friendship_current_validation.py` 在隔离 Unity Play Mode 实际从该 Resources 目录加载，使用当前本地配置／属性提供者和原生窗口、字体、礼物图标，通过模拟鼠标射线验证七项真实偏好、物品入口参数、滚轮位移、遮罩关闭、原开合动画／事件、阶位变化下 59 行／9 行预览及对象池复用、旅行列可见性和原奖励排序。已查看打开、滚动、旅行受限三帧 GPU 画面。静态检查通过（84,309 个 GUID），主工程导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 通过；属性配置、排序、本地服务及既有赠礼绑定输出哈希一致。窗口管理、声音及物品详情内容仍由明确测试端点承担；正式伙伴详情页路由、实际声音、存档联动和 Windows Player 尚未完成。

### 当前原生礼物偏好图标（局部）

`scripts/prepare_partner_gift_preferences_native.py` 核对当前 `884_156692` 原 `ItemIconManagerConfig` 的实际字段：ID 替代列表为空，伙伴礼物与信物类型 328／433 均不在类型替代列表，故偏好列表使用原 `DefPrefab` 指向的 `ItemIconDefault`；赠礼选择格另使用原 `ItemIconNpcGift`。原图标的偏好／选中遮罩、品质底图、数量文字、按钮及子层级全部保留。`ItemIcon.Clear` 即使隐藏中心提示也会取 `HintTip`，因此同时恢复实际附件配置指向的 HintTip 与 ReduceTip，沿已迁移的原附件管理器和池创建／回收，不另造减号按钮。

全部开放范围的 107 种偏好图标按原 `ItemInfo.GetIconPath`、`ResourceLoaderExtensions` 地址前缀及资源表定位，再核对原 Sprite／转换 Sprite 的 RenderDataKey，闭合实际纹理与图集依赖。已确认暂停的八位联动伙伴仍按[既定约定](offline-access.md#暂不开放的联动活动及伙伴)处理，其八种专属图像不进入本次加载范围；原配置表未删行。原八档品质底图、区域字体／TTF、数量格式与 ReduceDot 的 `Main_Cursor_Normal` 事件音频一并恢复；音频从原 Wwise 事件单叶图解码，保留增益和 PCM 参数。资源入口为 `unity/Assets/Official/Partners/UI/Gifts/`，`partner-gift-images.json` 保存显式图像／根对象／配置引用，来源见[礼物图标凭据](../../assets/reference/partner-gift-preferences-source-manifest.json)。

`prepare_partner_gift_preferences_validation.py` 的隔离 URP Play Mode 使用完整原窗口与原生物品格，验证七项真实偏好按品质／ID 排列、图标遮挡射线与模拟鼠标点击、物品详情入口原参数、窗口关闭后的图标回收／订阅释放、107 种原图轮换显示、数量变化、偏好／选中遮罩、原 ReduceTip 附件创建／回收复用，以及复用后减少按钮仅触发一次。已查看非空偏好、滚动、空偏好／旅行受限及选中赠礼格四帧 GPU 画面。窗口、属性、调色和物品详情内容仍由明确隔离端点提供，测试池只承担图标宿主边界，附件使用实际已迁移的官方管理器与池。

静态检查（81,471 个 GUID）、主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过；Play Mode 前后 454 项暂存载荷／元数据、导入后 474 项安装资源／目录及 22 项实际使用的脚本绑定哈希一致。提取工具扩展以原 311 对象窗口逐对象回归，默认 NPC 路径保持一致。当前本地配置／属性提供者接线见前文；物品详情弹窗内容、实际音频播放、完整赠礼页和 Windows Player 尚未接通；当前资源接入不表示伙伴系统已经完整完成。

### 当前原生赠礼区域（局部）

Home 运行时现在为 `OfflinePartnerGiftPlayer` 提供 `OfflinePartnerItemInfo`，直接投影选定伙伴物品表的 `ClassId`、`Icon`、`IconEffect`、品质和堆叠字段，并把官方伙伴图像目录注册到 `ProfessionImageResourceBridge`。赠礼玩家因此可以从当前本地库存生成原 `IProfessionItemData`，`NpcDetailPanelGiftContent` 已沿官方偏好排序、图标和数量刷新路径消费这些数据；物品详情弹窗仍未接入。

当前 Home 详情路由已接入官方 `NpcDetailPanelGiftContent` 与 `NpcFriendshipLevelContent` 子树。`PartnerGiftPanelView` 在官方 prefab 激活前绑定 `OfflinePartnerGiftUiServices`、本地库存／物品图标上下文、好感配置和图像附件池；赠礼成功调用 `OfflinePartnerGiftPlayer.GiveGifts`，按官方控制器的偏好倍率、自动选量、阶位上限、满级和溢出确认分支提交本地事务。伙伴卡片的专属礼物入口对已结识伙伴打开赠礼页。称谓输入和赠礼溢出确认已接入可交互的本地 Unity 弹窗，提交仍先执行原字符集／长度校验，再通过 `OfflinePartnerFriendshipTags` 保存；官方 `NpcFriendshipDialog` 已由本地等级、属性差值、偏好礼物与阶位服务驱动。物品详情弹窗仍未接入。静态元数据检查通过；主工程 Unity 初始化当前被工作区另一会话的 `FightStatusTriggerRuntime.cs` 编译错误阻断，主工程 Play Mode 实际赠礼点击及 Windows Player 发布仍待完成。

`Runtime/Official/PartnersUI/GiftController/` 从当前 `884_156692` 完整迁入 `NpcDetailPanelGiftContent`、`NpcGiftBlueDotItem` 和 `NpcFriendshipLevelContent`，保留原礼物排序、选量／减少／重置、自动补量、好感预览、精确升满与溢出确认、阶位／满级限制、称谓校验及响应后的刷新顺序。原配置、玩家、资源与窗口服务改为显式接口；原属性组和礼物格在初始化前绑定服务，原资源袋保留释放顺序。

`prepare_partner_gift_controller_native.py` 从原 `NpcDetailPanel` 的 3,242 个对象中按实际控制器字段提取完整 GiftContent 子树（325 个对象），只断开提取根的父引用；原坐标、尺寸、层级和内部绑定不变。恢复原 Upgrade 附件及 `Btn_Base_Click` 控制器，逐项核对编译状态机的五个状态、四个转换、参数、条件、时间与五份原动画引用。资源安装到 `unity/Assets/Official/Partners/UI/GiftController/`，其余图标、属性与字体按原 GUID 和载荷复用。转换器对原空 `myKey` 误写字面值 null 的问题已修正；两份既有物品格及原属性组逐字段核对原 prefab 后恢复空键，保留原静态数量分隔符。此次凭据明确记录被替换的旧字节、原字段编号与新哈希，覆盖早期清单中的对应输出值。输入、代码、原生依赖与安装哈希见[赠礼区域来源](../../assets/reference/partner-gift-controller-source-manifest.json)。

`prepare_partner_gift_controller_validation.py` 使用完整原子树及原物品格，在隔离 URP Play Mode 通过实际射线和模拟鼠标检查选择、减少、自动选量、重置、赠送异步响应、恰好升满、溢出确认、阶位／满级拒绝、称谓校验与回调顺序、库存蓝点、功能门槛和关闭订阅释放。选量使用所选版本真实礼物经验和偏好；等级、库存、属性、弹窗及声音仍由明确测试端点提供。已查看初始、选中、二级阶位限制与三级满级四帧实际 GPU 画面；静态检查（81,537 个 GUID）、主工程导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 通过，572 项安装文件及 23 项目录元数据哈希一致。正式完整详情宿主、真实本地配置／事务／存档提供者、原输入／确认弹窗、实际声音和 Windows Player 尚未接通，本节不表示完整伙伴功能已完成。

### 当前版伙伴配置输入（局部）

`scripts/prepare_current_partner_rules.py` 从选定的 `884_156692` 主 Luban 包与独立 `NpcTalentTree` 包读取 16 张完整表，使用同版本反编译构造器、条件分派和元数据解码，并逐表检查全部字节已消费。`unity/Assets/Official/Partners/partner-rules.json` 保留 148 条 NPC、115 条礼物、100 条信物、10,000 条好感阈值、385 条档案、20 条称谓、62 条互动、3 条天赋物品、两组各 252 条天赋节点／属性，以及依赖的条件、功能门槛、物品和成就表。原表没有删行或改写版本；输入包／对象、全部实际使用的构造器、输出及元数据哈希见[配置来源](../../assets/reference/partner-rules-source-manifest.json)。

与现有历史总表逐字段核对：礼物经验、信物限制、好感阈值、天赋节点和伙伴全局参数一致；NPC 表仅 `10114.LiveOffset` 不同。当前条件表另有更新，因此不能把整套历史配置当作当前版使用。这份当前版载荷尚未接入正式页面宿主及完整本地提示服务，也未替换其他功能的数据源。

配置交叉检查覆盖 859 个条件引用、好感复合键唯一性、礼物／信物物品和档案 NPC 引用。原好感表含当前名册之外的 `8403`、`8419`；原 NPC 的 `8402→1301`、`8408→1305`、`8409→1306` 专属信物引用在原物品表中没有对应定义。上述原始数据均保留，并在凭据中列明；卡片仍按原 `HasItemDefinition` 分支判断，不生成替代物品或名册条目。提取、数据／哈希校验、Unity 导入、静态检查及主工程 `Zjcs.Editor.ProjectTools.Validate` 已通过；完整交互与 Windows Player 验证仍待页面接通。

### 当前好感属性与本地配置目录（局部）

`scripts/prepare_current_partner_properties.py` 先核对 APK 中的实际文件，再按所选 `884_156692` 的 WwiseBnk 索引读取并校验原 `level_prop.g.bytes`；沿原 `LevelPropBinaryReader` 的区段／类别／偏移和有符号 64 位变长数读取 18 类伙伴好感属性及三类当前天赋属性，共 21 类、1,803 行。天赋类别来自当前 `npc_talent_tree_npc_map_prop.PropId`，保留其 `PropLevel` 和属性筛选。当前 73 条默认属性和 352 条属性定义从同版本 Luban 构造器解码，全部原数值保留。来源、原始二进制哈希、源码／构造器、NPC 类别映射对照及输出见[当前好感属性来源](../../assets/reference/partner-properties-source-manifest.json)。

`OfficialPartnerAssetCatalog` 在 `Resources/Partners/OfficialPartnerAssetCatalog.asset` 显式绑定当前 `partner-rules.json` 与 `partner-properties.json`；`OfficialPartnerCatalog` 检查两个输入均为 `884_156692`，提供当前表和属性，沿原解析器补默认值、保留等级缺项、最高等级越界取最高行，以及原好感差值枚举顺序。当前 NPC 的 148 项属性类别映射与历史名册逐项一致，角色总表加载时将该目录提供给现有好感属性计算；已结识 8401 的二级好感实际加入生命 174、经验每小时产量 300，按原宝物增益顺序继续计算。

`prepare_partner_property_validation.py` 在隔离 Unity Play Mode 运行实际本地目录与好感属性块，并以仅改变命名空间的官方原 `LevelPropBinaryReader` 和 `PropType` 为独立参照，逐项通过 1,803 行、1,782 个相邻等级差值、低于首级／越过最高级、148 项 NPC 属性身份及实际加成检查。混用版本拒绝检查通过。等级与库存完整 UI、真实存档恢复和 Windows Player 尚未在这一步验证；伙伴名册／赠礼的正式页面提供者仍待接通。当前工程检查范围见下文详情属性区。

### 当前赠礼、阶位与称谓配置提供者（局部）

`Offline/PartnersUI/OfflinePartnerGiftConfiguration` 实现原赠礼控制器的 `IPartnerGiftLevels`、`IPartnerGiftExperience` 配置端口，并提供当前 NPC、礼物、关系背景及称谓校验。`prepare_partner_gift_configuration.py` 从选定 `884_156692` 的 `NpcFriendshipLevelInfoParser`、`NpcGiftInfoParser`、`NpcFriendshipTagInfoParser`、`NpcFriendshipTagInfo.InRangeLevel`、`FormulaHandler` 与 `FontCharacterValidChecker` 提取原方法体；只投影所需字段、绑定本地目录并移除缺配置分支的诊断日志，保留返回值、算术和枚举顺序。来源和输出见[配置提供者凭据](../../assets/reference/partner-gift-configuration-source-manifest.json)。

10,000 条累计阈值保留原顺序，包含缺少 NPC 定义的 8403／8419 两组共 200 行；这些组仍可查询阈值，阶位上限按原码返回 0。不可培养伙伴也返回 0；其余伙伴只取不超过当前阶位的最高配置组，再取该组最大等级。经验换算按累计经验与阶位同时过滤。115 条礼物配置保留不能赠送的信物排除、未知物品使整批结果归零、每种礼物先乘数量／偏好并按 `Math.Round` 五成双取整再累加的原顺序；不把信物类型当作普通礼物类型。

原 `CustomContentSettings` 的 `zh_CN` 入口指向原 `FZY4K_Characters.txt`，已按原 GUID 解析实际 TextAsset 并原样打包，`Resources/Partners/OfficialPartnerGiftAssetCatalog.asset` 显式绑定当前配置目录和该字符文件。称谓逐个 UTF-16 字符核对原白名单，长度复用与当前原码一致的 `Official/SkillRules/CJK` 文本元素算法；空称谓返回 1513，超过原 12 长度上限返回 1514，原自定义称谓好感门槛为 100 级。没有增加另一套正则过滤或字符替换规则。

`prepare_partner_gift_configuration_validation.py` 在实际迁移程序集及显式绑定资产的隔离 Unity Play Mode 中通过 10,000 条阈值各三点、1,176 项阶位上限、115 种礼物各 16 组数量／倍率、两组缺 NPC 定义的数据、关系背景区间及完整原字库／CJK 边界检查。静态检查（81,561 个 GUID）、主工程导入／编译和 `Zjcs.Editor.ProjectTools.Validate` 通过，输入／输出与验证源码哈希核对一致。本提供者尚未安装到正式伙伴窗口；原窗口的玩家提供者安装、称谓保存、原输入／确认窗口、完整页面模拟点击和 Windows Player 均待接通。

### 当前库存与赠礼事务桥接（局部）

`Offline/PartnersUI/OfflinePartnerGiftPlayer` 将原 `IPartnerGiftPlayer` 端口接到实际 `OfflineMainlineProgressSession`，不复制一份玩家背包供界面修改。伙伴和礼物视图按本地物品／伙伴号保持身份，取值始终读取最新 `Save.Player`，因此事务替换玩家对象或重读存档后仍能看到新数量、等级和好感。物品显示配置与功能门槛由宿主显式绑定；默认称谓读写使用下述实际本地存档提供者，既有显式宿主端口继续可用。

`OfflinePartnerGiftInventoryRules` 按当前版 `NpcInfoParser.ProcessNpcExclusiveGift` 和 `NpcHelper.CanSendGiftToNpc` 建立完整专属礼物归属，保留原背包顺序、礼物／信物类型过滤、不可赠送信物和星神非偏好礼物的排除。赠礼命令只消费当前 `884_156692` 的配置提供者，不改用历史 NPC 阈值或物品表。校验后复用本地 `ApplyCharacterMutation` 和 `OfflineInventoryOperations.Consume`，将礼物数量、单位记录、累计好感、好感等级及重复请求凭据共同落盘；旧存档仅有等级但经验为零时，界面和事务统一沿用已有单机接口的原等级阈值恢复。保留现有算术越界拒绝，以及超过全局最高经验须确认、确认后保留超出经验的已确认方案。

通知在保存成功后发出，物品通知携带原 `preCount`，好感通知先携带旧／新等级，再通知好感变化。外部存档更新按实际差异刷新；写入失败不发布本次赠礼通知、不改变内存数量／好感或主存档，释放文件占用后可用同一请求重试。代码、原方法、配置依赖及验证源码哈希见[玩家桥接凭据](../../assets/reference/partner-gift-player-source-manifest.json)，记录入口为 `scripts/prepare_partner_gift_player.py`。

`OfflinePartnerFriendshipTags` 将自定义称谓保存到实际伙伴记录的 `CustomFriendshipTag`，旧存档缺字段时读取为空。命令重新读档，依次检查原字库、原长度、当前伙伴资格和原 100 级好感门槛，再与请求凭据原子落盘；写盘成功后才执行原窗口所需的成功回调。默认玩家视图随存档对象替换读取最新称谓；原响应回调再次设置同值时不重复写盘。文本审核的单机差异见「自定义实现」。

`prepare_partner_gift_player_validation.py` 在隔离 Unity Play Mode 使用实际玩家／事务代码与隔离存档，通过实时视图、偏好赠礼、保存后通知参数与顺序、单位记录扣除、重复请求、库存不足、专属礼物拒绝、外部存档刷新、文件占用写入失败／同请求重试、旧等级经验恢复、恰好满级、溢出取消／确认及阶位限制；并检查全部 17,020 组伙伴／礼物归属组合。称谓覆盖原校验错误码、伙伴／好感资格、旧存档空值、写入失败时无成功回调、同请求重试、重复请求及读档恢复。静态检查（84,294 个 GUID）、主工程导入／编译和 `Zjcs.Editor.ProjectTools.Validate` 通过。这仍是状态提供者的 Play Mode 验证，尚未安装到正式窗口；完整原 UI 模拟点击、原输入弹窗、实际 Windows Player 与发布尚未完成。

### 原面板页签控制器（局部）

`scripts/prepare_partner_panel_controller.py` 迁入当前版 `NpcPanel`、`NpcPanelTabType`，从原 `NpcPanelUtils` 与 `FunctionType` 提取伙伴／通话／星神对应的 `65／57／174` 功能门槛。字段名称、门槛遍历、默认页回退、标题、页签关闭回调及关闭窗口重置均保留；复用已有官方 `PanelTitle`、`MonoDictionary`、`WorldTabGroup`、`WorldTabItem`、`WorldStateSwitcher`。本地宿主通过 `IPartnerPanelServices` 提供功能判断与通话列表初始化，不再访问全局在线玩家。代码入口为 `Runtime/Official/PartnersUI/Roster/NpcPanel.cs`；选定官方源码及迁移输出哈希见[面板控制器来源](../../assets/reference/partner-panel-controller-source-manifest.json)。

隔离 Unity Play Mode 使用模拟鼠标射线验证三次无遮挡页签点击，以及全开、单开、全关、指定默认页被限制、重新启用、标题更新和销毁解绑。原通话页关闭回调仍先切换状态并初始化通话列表，再处理新页签；没有改写这一顺序。实际查看 GPU 截图，静态检查、主工程导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 通过。验证控件为隔离夹具，不能代表原面板视觉或完整通话功能；完整原 prefab、页面宿主、子页面和 Windows Player 尚未接通。

### 已迁移的名册与相遇状态（局部）

`Runtime/Official/Partners/NpcRosterRules.cs` 与 `OfflineCharacterProgressionService.Partners.cs` 按当前 `884_156692` 的 `NpcHelper`、`NpcPanelNpcList`、`NpcComparer` 恢复五种结识状态、名册显示条件及好感／相遇时间排序。判断依次为已结识、可结识、可显示后的等待／未结识；可结识条件优先于可显示条件。默认显示已结识及可结识伙伴；勾选全部后增加等待结识伙伴，以及拥有首个专属信物的未结识伙伴。名册排除原 `MapExploreBoss` 类别、星神和已确认暂停的八位联动伙伴。

排序固定优先结识状态、置顶、置顶时间升序及伙伴类型，再比较选中的好感或相遇时间，最后按伙伴号降序；反向只改变选中的排序键。置顶功能按 `function_limit` 判断，其中 `HasManualMeetNpcCondition` 按原码同时检查“已拥有手动结识伙伴”和“当前存在满足结识条件的手动伙伴”，不直接使用历史存档的布尔标记。地图可达读取 `UnlockedMapClassIds`；区域激活读取 `ActivatedGoddessRegions`，探索完成本身不等于区域激活。

相遇事务提交前重新读取存档，信物扣除、初始好感与实际相遇时间共同保存；重复事件不再次相遇或扣除。置顶及其时间单独保存，重复置顶保持原顺序。旧存档未记录的相遇时间保持零，不从注册时间或当前时间反推。来源函数、当前原码及配置哈希见 [名册规则来源](../../assets/reference/partner-roster-rules-source-manifest.json)。

`scripts/test_partner_gifts.ps1` 的隔离 Unity 编译／存档验证同时覆盖地图锁定、区域未激活／探索完成／激活后的状态变化、部分信物显示、信物不足拒绝、可结识优先、真实结识扣除、重复相遇、置顶解锁／顺序、存档恢复及旧日期兼容；排序检查覆盖反向与固定优先级、最终伙伴号比较。卡片的模拟点击与主工程验证范围见上文「已接通的名册卡片控制器」；官方名册 prefab 和实际入口尚未接通。

### 当前名册条件与相遇事务接线（局部）

`scripts/prepare_partner_conditions.py` 从选定 `884_156692` 迁移 `DataHub`、九类条件及相遇／信物／比较辅助方法，共 19 份原方法或代码块；读取全部 148 条 NPC 的相遇、显示、等待条件及置顶功能 1011，闭合 72 个条件号。保留原条件枚举、字段默认值、分支顺序与短路；未知比较符沿原默认分支按 `>=`，空 AND 为真、空 OR 为假，运行时缺失子条件保留空项而不跳过。`GetHasItemCanMeetCondition` 只找直接子条件中的首个信物条件，不递归展开。源码、当前配置、闭包、适配和输出哈希见[当前条件来源](../../assets/reference/partner-condition-source-manifest.json)。

`OfficialCharacterCatalog.Partners` 为名册提供当前版本的 NPC、条件、功能门槛、物品、好感阈值与初始等级；相遇状态、置顶、卡片元数据及信物进度消费此目录。其他功能继续通过各自原有数据入口读取配置。玩家端口读取实际背包、已结识记录、可达地图和已激活女神区域；基础等级复用现有单机技能条件的投影，即角色等级与原阶位基础等级上限的较小值。原 8408／8409 的手动相遇条件是基础等级至少 38，满足后即参与原手动伙伴判断，不额外要求信物。

相遇继续使用已确认的本地单事务方案，提交前重读存档并复核当前条件；信物通过 `OfflineInventoryOperations.Consume` 同步扣除数量和逐件记录，空栈一并移除。初始好感、结识时间与事件凭据共同保存，成功后才由界面桥接发通知；写盘失败可用同一事件重试。11015–11022 的暂停约定同时应用于相遇命令。

`PartnerConditionValidation.Run` 在隔离 Unity 工程通过九类条件、比较默认分支、基础等级数据优先级、物品数据后备、复合短路／空列表／缺失子项、直接信物提取与当前相遇事务断言，包含实际文件锁写盘失败、回滚、同事件重试、逐件记录扣除、商人／导师和读档恢复。原生名册及卡片均切换为当前配置，在 URP Play Mode 通过模拟鼠标射线、显示全部、排序／反向／关闭遮罩、商人／导师卡片、滚动、信物不足、连续相遇只扣一次、700 毫秒解锁、置顶及存档恢复；已查看名册五帧和卡片五帧实际 GPU 画面。静态检查（84,287 个 GUID）、主工程导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 通过。本节仍为名册与事务局部验证，完整页面宿主、正式弹窗及 Windows Player 尚未完成。

当前名册唯一自动伙伴 8402 的原 `IsAutoMeet` 为真、相遇条件为基础等级至少 1。`MsgPlayerNpc.OnMessage` 只消费服务器新增数据并触发通知，`NPCHandler.MainPlayer_OnAddNewNpc` 只处理自动结识广播；尚未取得服务器自动触发实现。本地触发方案已确认，见下文「自定义实现」。

`OfflineCharacterProgressionService.MeetAutomaticNpcs` 检查当前原条件，仅补入尚未拥有的自动伙伴，初始等级与经验分别读取原 `NpcDefaultFriendshipLevel` 和对应好感阈值，结识时间为本地实际发生时间。创角服务将初始化放入创角事务；选档进入已有角色时重新检查，浏览存档与头像检查不触发。重复进入不重建记录，不覆盖已有好感、称谓或结识时间。伙伴条件在尚无阶位上限行时使用已保存等级；已有上限行时保持原阶位上限投影。结识初始化不重新推导或改写玩家等级。来源见 [自动结识凭据](../../assets/reference/partner-automatic-meeting-source-manifest.json)。隔离规则验证通过未创角拒绝、原初始好感、写盘失败回滚与重试、重复加载不写盘及已有好感／称谓／时间保留；现有单机创角初始等级为 0，不满足该原条件时不会提前结识；本接线不改创角等级。隔离 Play Mode 将未创角存档设在原条件阈值（等级 1），通过实际命名提交、职业选择和存档卡片模拟点击，验证创角事务内保存原初始好感、进入旧档补建、浏览列表不写盘、再次进入保留等级／经验／称谓／结识时间及逐字节存档不变。已查看存档选择、命名页、角色预览、创角完成和重复读档画面。静态检查通过（84,304 个 GUID），主工程导入／编译与 Zjcs.Editor.ProjectTools.Validate 通过。该验证覆盖自动结识接线；正式伙伴详情页与 Windows Player 交付仍待完成。

### 已迁移的送礼规则与存档接口（局部）

`Runtime/Official/Partners/NpcGiftRules.cs` 从当前 `884_156692` 官方源码迁移礼物展示／自动选择排序、单件和整批好感取整及自动补量。展示依次按偏好、品质、原始好感降序；自动选择按偏好降序、品质和原始好感升序、物品号降序，保留已有选择并按单件取整值向上补量。批量预览仍按同类总量乘偏好后进行四舍六入五成双取整；两种顺序不能互换。来源函数与源码哈希见 [送礼规则来源](../../assets/reference/partner-gift-rules-source-manifest.json)。

`OfflineCharacterProgressionService.NpcGiftCandidates`、`AutoSelectNpcGifts` 和 `GiveNpcGifts` 接入上述规则，按 `NpcHelper.CanSendGiftToNpc` 拦截不能赠送的信物、属于其他伙伴的专属礼物及星神非偏好礼物。已选礼物可使好感恰好达到当前位阶上限；当前好感已经达到该上限时限制继续赠送。超过全局最高经验时返回待确认，明确确认后保留超出经验；礼物扣除和好感更新共同落盘，重复事件不重复扣除。提交前重新读取存档，避免长驻伙伴服务覆盖其他功能已保存的库存。

验证：`scripts/test_partner_gifts.ps1` 编译实际 C# 规则，并使用隔离 Unity `2022.3.57f1c2` 工程编译实际养成依赖、加载工程现有官方配置，验证恰好升满、溢出取消／确认、重复提交、库存不足、存档重新读取、自动选量及 90 项其他伙伴专属礼物的排除。纯规则测试另覆盖偏好排序、已有选量、库存耗尽、两种取整差异及数值溢出。送礼仍为 Editor 中的接口与存档验证，尚未验证官方送礼界面、对应模拟输入、Play Mode 画面或 Windows Player。

### 当前原生赠礼页与本地事务接线（局部）

`OfflinePartnerGiftUiServices` 将实际迁移的 `NpcDetailPanelGiftContent` 与 `NpcFriendshipLevelContent` 接到当前配置、实际 `OfflinePartnerGiftPlayer`、好感属性提供者和称谓存档。窗口端继续执行原偏好、阶位、最高等级、溢出确认和自动选量逻辑；服务复制本次选量后在后续主线程继续执行本地事务，先保存并发布库存／好感通知，再执行原成功回调。`DoGiveGift` 已经过原溢出确认后才提交允许溢出的事务；取消确认不触发事务。写盘失败仅报告失败，不执行成功回调，不清空选择、不改变库存和好感，用户可再次点击重试。称谓沿已确认的本地保存方案，保留原字库、长度、伙伴资格与好感门槛。

`prepare_partner_gift_presentation_assets.py` 按所选 `884_156692` 的 `npc_friendship_tag.TagBg`、原资源索引和 Sprite RenderDataKey 恢复八张普通／星神关系背景，闭合原图集与同版本 Windows 贴图，提取二十条原称谓文本。图片加载保留官方 `Assets/Image/` 前缀。`Resources/Partners/OfficialPartnerGiftPageAssetCatalog` 显式绑定原赠礼区域、文字、107 种当前开放范围的偏好图像、八张称谓背景、三个原附件入口、原配置和区域字体／TTF；已确认暂停的联动伙伴范围不变。来源见[赠礼资源绑定](../../assets/reference/partner-gift-presentation-assets-source-manifest.json)与[本地赠礼服务](../../assets/reference/partner-gift-services-source-manifest.json)。

`prepare_partner_gift_current_validation.py` 在隔离 Unity URP Play Mode 加载上述实际 Resources 目录与原赠礼区域，使用模拟鼠标射线验证选择、减少、重置、自动选择、赠送、阶位拒绝和最高等级溢出确认；确认前不扣库存，确认后保留原超出经验。实际本地事务覆盖先保存后回调、逐件库存扣除、重读恢复、存档文件占用时不执行成功回调且保留选量、释放后重试，以及原称谓字库拒绝／成功保存。八张背景均验证可加载且具有贴图；已查看打开、选中、赠送后、阶位上限与满级五帧 GPU 画面。验证宿主仍提供弹窗端口及测试调色，不代表完整正式窗口。

主工程 Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过；最新全工程检查范围见下方详情属性区。

正式伙伴窗口路由已接入，物品详情弹窗、独立好感弹窗、原赠礼弹窗的完整官方资源调色／声音和 Windows Player 验证仍待完成；当前接线不表示伙伴系统已完成。

Home 的名册宿主先初始化原滚动列表对象池，在停用池中创建并绑定本地服务后才启用卡片，保留新增伙伴事件订阅时机。赠礼物品按官方 `ItemInfo.GetIconPath` 的无 `PartInfo` 分支读取 `RawIcon`，图标从实际赠礼目录加载；停用节点的祖先查找同样保留绑定。原 `LanguageManager`、`CultureNumberArg.Compact` 和 `WorldItemProgress` 数量格式化器已接线；详情与赠礼区域同时读取既有原属性中文表，避免显示属性键名。

主工程 `-homeAcceptance -partnersAcceptance` 使用隔离存档和唯一 450×800 Game View，跳过并行的常规启动流程。Play Mode 经真实 UI 射线检查卡片显示后点击详情，以及礼物选择、重置、重新选择和赠送；核对实际礼物 Sprite、原偏好经验、扣除一件库存及重新读档后的数值。已查看名册、详情属性、赠礼前后和六种偏好礼物的实际画面。全工程静态检查通过（84,680 个 GUID），主工程 Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。此验证覆盖现有原生区域的正式服务接线，仍未覆盖完整官方窗口壳、档案／通话、立绘、声音和 Windows Player。

### 当前官方称谓输入窗口（局部）

`prepare_partner_input_dialog.py` 从选定 `884_156692` 的原 `InputDialog` 恢复完整窗口，并按原 `MainCanvas` 默认 `WindowContainer.MaskList` 的 `WindowMaskType.Mask` 绑定确切遮罩子树。仅解除遮罩根父节点，保留原对象、层级、矩形、颜色、材质、按钮和动画；`Resources/Partners/OfficialPartnerPromptAssetCatalog` 显式绑定窗口、遮罩及原中文文本。已核验相同 GUID／字节的依赖复用现有伙伴资源，不覆盖其他窗口素材。来源、原 CAB／PathID、处理步骤与输出哈希见[称谓输入来源](../../assets/reference/partner-input-dialog-source-manifest.json)。本批未生成素材快照。

`WorldInputDialog` 与 `WorldContentModerationClickMask` 移植原初始化、确定、取消、重置、提示与屏蔽检查顺序；窗口关闭与内容自定义状态由宿主投影。称谓仍经原赠礼控制器的字库、长度、伙伴资格和好感门槛校验，按下文已确认方案本地保存。原输入框字符上限保留，不使用临时窗口的 32 字符限制。保存成功后刷新原称谓文本并播放原退出动画；取消与返回只关闭输入窗口，保留赠礼页。遮罩使用原窗口排序间隔与点击保护时长。

原转换器的空字符串与空动画根路径分别依据原对象字段和编译曲线绑定恢复；空语言键及输入框空值保持原空字符串，不写成字面值 `null`。原缺少的 `ui_null` 继续沿官方 `GetMissingKeyText` 返回键名，不自造翻译。点击音和返回音追踪原 Wwise Event／bank／媒体，复用已核验的本地 PCM；播放器使用 Unity 对象空值检查再补建 `AudioSource`。

主工程隔离 Play Mode 已通过原按钮鼠标射线、取消后保留赠礼页、空称谓拒绝、中文键盘事件及 TMP 正常文字刷新、成功保存并刷新称谓、重新读档，以及点击原遮罩仅关闭输入窗口。已查看打开、输入“挚友”和保存后三帧 450×800 实际 GPU 画面；名册、详情属性、赠礼选择／重置／提交及磁盘恢复、好感说明回归通过并查看五帧实际画面。赠礼规则测试通过舍入、顺序、已有选择、库存及溢出；全工程静态检查（84,707 个 GUID）、主工程 Unity 导入／编译和 `Zjcs.Editor.ProjectTools.Validate` 通过。完整伙伴窗口、赠礼溢出确认弹窗及 Windows Player 仍未完成。

### 当前官方赠礼溢出确认窗口（局部）

`scripts/prepare_partner_confirm_dialog.py` 从选定 `884_156692` 的原 `ConfirmDialog` 恢复完整 222 个对象，按原脚本身份、CAB 与资源引用绑定 Windows 原生 prefab。复用既有 `WorldConfirmDialog`、文本对齐、计时遮罩等官方移植，补齐原 `AutoAlignForSingleMultiLineTextContent` 和原超链接组件的序列化引用；空字符串、空集合及动画根路径按原字段恢复。来源、原码、脚本绑定和输入输出哈希见[确认窗口凭据](../../assets/reference/partner-confirm-dialog-source-manifest.json)。

Home 赠礼溢出回调现打开原窗口，保留原标题、说明、按钮、布局、开合动画与声音事件，沿用原普通窗口遮罩。确认调用既有本地赠礼事务；取消保留待赠选择，两个动作均只关闭上层确认窗口。满级后溢出的累计好感沿用本页已确认方案保存，不截断为满级阈值；好感等级仍按原配置计算。

隔离完整工程 Play Mode 已通过原礼物格、赠送按钮和确认／取消按钮的模拟鼠标射线，验证取消不扣物、不改好感且保留选量，确认仅扣一份、升至原满级并保留溢出经验，重新读盘结果一致。已查看 450×800 的原确认框和满级赠礼画面。共享输入窗口回归通过原称谓按钮、空值拒绝、中文输入／显示、确认保存、读盘恢复与遮罩关闭，并查看三帧实际画面。全工程静态检查通过（84,719 个 GUID）；主工程导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 通过。此模块不代表完整伙伴窗口、立绘、助战或 Windows Player 已完成。

### 当前好感说明窗口与所属页面（局部）

好感说明窗口宿主恢复原 `NpcFriendshipLevelContent.OnClickTipBtn`／`NpcDetailPanelPropContent.OnClickTipBtn` 的所属窗口弹出关系：说明覆盖赠礼／属性页，遮罩和返回只关闭说明，底层选量及服务保留。`prepare_partner_friendship_window.py` 按当前原 `NpcFriendshipDialog.mMaskType=3` 恢复默认容器的确切模糊遮罩，迁移原 `WindowBlurMask`、窗口状态及遮罩挂接顺序。Windows 屏幕采集适配保留原 1/13 分辨率及四层下采样／上采样、七点 shader 表达式；当前 APK 的 ScreenBlur 程序与历史转换不同，单独生成当前 Windows shader，不改写旧版素材。原礼物点击事件复用当前伙伴卡片中已核验的同事件 PCM，以页面作用域安装及恢复声音宿主。窗口、遮罩、当前 shader、复用依赖与运行绑定见[好感窗口来源](../../assets/reference/partner-friendship-window-source-manifest.json)。

主工程 Play Mode 已通过实际原礼物格、赠礼／属性说明按钮与原模糊遮罩的模拟鼠标射线，验证非空偏好、属性预览、遮罩排序、保留待赠选择、重复打开／返回、关闭所属页时清理窗口及恢复池／语言／声音服务。已查看赠礼与属性入口的两帧 450×800 GPU 画面；称谓输入和赠礼溢出确认回归均通过，并查看五帧画面及核对磁盘恢复。全工程静态检查通过（84,732 个 GUID），主工程导入／编译和 `Zjcs.Editor.ProjectTools.Validate` 通过；11 项安装载荷／目录、24 项运行代码及元数据、46 项输入与 27 份脚本绑定哈希一致。完整伙伴宿主、立绘、助战及 Windows Player 仍未完成。

### 当前对话名册控制器与原生条目（局部）

`prepare_partner_dialogue_list_ui.py` 迁移当前 `NpcPanelDialogueList`、`NpcPanelDialogueListNpcItem` 与 `NpcPanelDialogueListDialogueItem`，保留 NPC 按最近通话时间倒序／类 ID 升序、通话按状态升序／时间倒序／通话 ID 升序、原折叠展开、备用对象数量、逐项淡入、更新／删除刷新和生命周期订阅顺序。原 `ItemComparerBase` 的未检查长整型减法与符号转换保留，原下一节点任一条件满足即显示红点及 `<PN>` 名称替换直接迁移；玩家状态、图标初始化、窗口、条件与实例注入由显式宿主端口承接。代码与 12 份输出及元数据见[对话名册源码凭据](../../assets/reference/partner-dialogue-list-ui-source-manifest.json)。这些端口尚未接入正式通话存档与完整列表宿主。

`prepare_partner_dialogue_entry_native.py` 从当前主窗口真实的 `mItemPrefab` 外部引用找到 `NpcPanelDialogueItem`，再按其 `mDialogueItemPrefab` 提取完整 66 对象条目，保留状态、原图像、字体、矩形、淡入动画及点击音；不使用主窗口内演示副本。恢复空字段及动画根路径，复用 9 项同 GUID、同字节素材，以 `OfficialPartnerDialogueAssetCatalog` 显式绑定本地 prefab；40 项实际安装文件／目录、资源和脚本绑定凭据见[原生对话条目来源](../../assets/reference/partner-dialogue-entry-native-source-manifest.json)。本批资源未纳入素材快照。

隔离 Unity Play Mode 已通过原按钮的实际模拟射线、原 PCM 播放、原开场文字、进行中／暂停／结束状态及红点、重新启用不重复监听、序列显示／隐藏及最终释放，并核对通话排序边界与名称占位符。验证视口使用当前原 1080×1920 缩放和原父级布局参数，已查看进行中、结束两帧 450×800 GPU 画面；这是独立条目验证，完整分页、折叠列表、正式通话窗口和 Windows Player 仍未验证。已从实际安装的 Resources 目录引用重跑并通过；全工程静态检查通过（84,753 个 GUID），40 项安装输出、26 项资源输入、10 份脚本绑定及 12 份控制器输入／输出哈希一致。主工程本轮验证未完成，Unity 报告工程被另一实例打开；隔离工程编译及本条目 Play Mode 通过。

### 当前星神分页与原生分组模板（局部）

`prepare_partner_star_god_ui.py` 迁移选定 `884_156692` 的 `NpcPanelStarGodContent`、`StarGodNpcItemGroupItem`、`StarGodNpcItemContent` 与 `StarGodNpcItem`，保留地图探索 Boss 排除、星神身份、未相遇隐藏、原置顶／好感排序、向上取整分组、单个首组居中及多组顶部锚点。卡片按原顺序更新相遇状态、名字、地点、链接等级、立绘 pivot、信物与置顶图；复用现有原 `ImageLocator`。原禁用错误 53897、可相遇请求、已相遇详情顺序、解锁动画及其长度乘 0.8 后的等级提示保持；对象池停用／重新绑定取消旧延迟并释放图片句柄。12 份原输入与 12 份代码／元数据输出见[星神控制器来源](../../assets/reference/partner-star-god-ui-source-manifest.json)。玩家、库存、窗口与提示图仍通过显式服务端口承接，尚未接入完整正式宿主。

`prepare_partner_star_god_native.py` 从原 `NpcPanel` 取完整星神区域，仅解除根父级，并沿实际外部组件引用恢复 2,552 对象的 `StarGodNpcItemGroupItem`；跨 prefab 引用保留确切组件 fileID，不用根 GameObject 替代。保留原坐标、层级、材质、粒子、网格及动画，按当前配置及 CN 地区图片选择提取 10608／10708／10801 的原静态立绘和中文文本。两条原星神开场／解锁 Wwise 事件沿实际 bank／媒体解码为 PCM；普通点击复用已有同事件原 PCM。Windows Mesh 导出中的空二进制字段改用 Unity 自身的空标量格式，修复原 `null` 文本导致的顶点数据读取断言及 UI 几何线程崩溃；原位置、索引、UV、通道及其他字段保留。资源先核对 APK，再取所选云端与已验证 Windows 转换缓存，245 项实际安装文件／目录及 39 份脚本绑定见[星神资源来源](../../assets/reference/partner-star-god-native-source-manifest.json)。`OfficialPartnerStarGodAssetCatalog` 显式绑定实际 Resources 页面、三个星神、图片和文字；本批未纳入素材快照。

`prepare_partner_star_god_validation.py` 建立包含原 UI 粒子程序集的隔离工程，以实际安装的 Resources 目录运行 Unity URP Play Mode。原按钮的模拟指针射线验证详情端口、禁用分支、相遇请求、原音频播放、动画延迟及拥有对象身份更新；另验证原分组／居中、置顶优先、链接等级事件、长整型比较溢出、关闭取消提示、重新开启及图片／事件释放。核对 Mesh 原四顶点和六索引，已查看单个、三人分组及解锁后的三帧 450×800 GPU 画面。库存／信物图标、实际相遇事务与存档、详情窗口内容、完整分页入口及 Windows Player 不在此隔离验证范围；不据此记为伙伴功能完成。

全工程静态检查通过（84,841 个 GUID）；主工程最终导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。12 份源码输入／输出、83 项原生资源输入、245 项安装输出及 39 份脚本绑定哈希一致。正式星神库存、相遇存档和完整宿主仍未完成。

### 当前原生详情属性区与天赋合并（局部）

`prepare_partner_detail_properties_ui.py` 迁移选定 `884_156692` 的 `NpcDetailPanelPropContent`，保留属性排序、旅行门槛、每组两项、交替背景、原珍宝百分比及转为整数的截断顺序。`PartnerNpcPropertyFormula` 提取原 `BattleFormulaHandler` 的好感／天赋计算及属性合并方法，读取实际存档的好感等级和已激活天赋节点；按当前 `PropId`、`PropLevel`、`PropTypes` 取值并合并，保留原零值、分支和执行顺序。`OfflinePartnerDetailPropertySource` 使用上文当前属性目录，补齐三类天赋属性，不采用历史总表的缺省值替代。

`OfflinePartnerDetailPropertyServices` 将置顶接到现有本地事务，保存成功后才回复原控制器；伙伴视图始终读取最新存档，支持连续置顶与取消。珍宝是否激活由宿主显式绑定，品质倍率取当前原配置。好感说明、伙伴战斗属性简报及地图跳转保留显式窗口／导航端口；原 `LiteInfoCallbackBase` 的调用、异常处理、复用与销毁释放语义保留，仅适配类型与日志。尚未声称这些端口的完整正式页面已接通。

`prepare_partner_detail_properties_native.py` 从当前原 `NpcDetailPanel` 截取完整属性区域，仅解除根父节点，保留原层级、坐标、布局、材质、图像、按钮和 Animator。共享属性行与既有同 GUID、同字节资产复用，原点击控制器由编译状态机数据恢复；`UIUtils.Instantiate` 的对象创建、注入、复位及激活顺序继续保留。`prepare_partner_detail_properties_assets.py` 提取八条原中文文字，并在 `Resources/Partners/OfficialPartnerDetailPropertyAssetCatalog` 显式绑定原生区域及既有原字体／图像目录。代码、转换、实际包／对象和输入输出哈希见[详情属性来源](../../assets/reference/partner-detail-properties-native-source-manifest.json)及[显式资源绑定](../../assets/reference/partner-detail-properties-assets-source-manifest.json)。未新增替代美术或布局。

`prepare_partner_detail_properties_validation.py` 在隔离 Unity URP Play Mode 使用实际 Resources 原区域、当前配置和隔离存档，通过三位伙伴全部已激活天赋节点的属性合并、未拥有伙伴不贡献属性、实际好感等级、原双列组数、无遮挡指针置顶／取消及两次磁盘恢复、10110 实际关联珍宝的附加值、好感／简报／导航按钮端口调用顺序、跳转隐藏和回调销毁释放。已查看打开、置顶和珍宝生效三帧实际 GPU 画面。简报数值与导航结果由验证宿主提供，未记为正式简报／地图流程已经完成。当前属性目录另通过官方原二进制读取器的 21 类、1,803 行比对。

全工程静态检查通过（84,374 个 GUID），主工程 Unity 导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 通过。正式完整详情、立绘／小模型、弹窗内容、声音与 Windows Player 尚未在本区域完成。

### 当前原版动态立绘与原生贴图（局部）

`prepare_partner_native_portraits.py` 按当前 `884_156692` 的 NPC 可培养配置与 `FullBodyLiveImage` 地址，先核对固定 APK，再解析所选云端 bundle 的实际容器、CAB 和对象依赖，提取开放范围内 54 份动态立绘。原始骨骼二进制使用既有免费方案允许的 MIT `SkelToJson 1.0.1` 格式读取器解码，关闭浮点舍入并验证完整消费；保留原骨骼、网格、动画、图集和 SkeletonDataAsset 字段，不复用 Spine SDK 运行时代码。八位暂停开放伙伴沿用本页既定范围。

`prepare_partner_portrait_assets.py` 校验当前原对象字节及 Windows 原生贴图转换缓存，安装 54 份带版本、伙伴身份、压缩块长度和 SHA256 校验的数据包，以及全部原生 Texture2D 载荷、原配置字段和逐伙伴 Resources 引用，共 489 个文件。`OfficialPartnerPortraitAsset.Load` 按伙伴按需加载，保留原 LiveOffset、LiveScale、defaultMix、图集页与贴图绑定；不将所有立绘贴图集中预载。原生 Texture2D 保留原尺寸、单级 mip、色彩空间和采样字段。输入、处理工具、转换缓存、输出及绑定哈希集中记录于[立绘来源](../../assets/reference/partner-portrait-assets-source-manifest.json)，本批素材未纳入素材快照。

独立原生几何层增加原负权重外插、反射目标偏移、约束顺序中已求解父骨骼保留、独立透明度通道和多关键帧稀疏网格形变。`PartnerNativePortraitIk` 独立构造父坐标系内的单骨骼朝向与长度变换，支持原 Stretch／Compress／Uniform、部分权重、反射及非均匀缩放父级，并消费前序约束的实际仿射结果；解析几何用例覆盖端点、角度、长度、剪切保持、连续约束与零权重。双骨骼入口在当前父级正向等比仿射坐标内消费前序约束后的真实父／子矩阵并保留已求解祖先；支持子骨骼 X 轴长度、原 Softness、Stretch 与 Uniform 分支，解析用例覆盖最大可达距离、软约束过渡、非均匀／均匀拉伸，以及反射且非均匀缩放的祖先、两种弯曲方向、末端对齐和零权重。softness 与拉伸分支用隔离验证工程中的原 Spine 4.2 实际运行时构造骨架作为黑盒数值参照；生产实现不依赖该运行时。Compress 在原实现中只用于单骨骼 IK，双骨骼分支不使用。IK Bezier 混合按原控制点保留大于 1 的超调值。双骨骼首骨骼反射／非均匀缩放／剪切仍明确拒绝；部分已约束局部变换恢复、继承与凹多边形裁剪也未完成，不能当作完整动画还原。

`prepare_partner_native_portraits_validation.py` 在隔离 Unity Play Mode 读取全部 54 份实际 Resources 绑定，验证版本、身份、篡改拒绝、图集页及原 mip 数，并采样全部 118 段动画的五个时点。目前 110 段可生成有限且有效的原网格；剩余 8 个样本涉及双骨骼首骨骼反射／非均匀缩放／剪切、单骨骼选择性继承和需验证凹分解的剪裁多边形，仍明确拒绝。另通过负权重、反射与非零偏移、交错约束保留、原透明度关键帧以及原多帧线性／Bezier 稀疏形变检查。四种新增双骨骼模式断言与原运行时数值样本通过。

`prepare_partner_portrait_graphics_validation.py` 核对当前原 `NpcDetailLiveItem`、SkeletonGraphic 材质与原编译 shader，复用同版已校验的 Windows shader 转换缓存。隔离 URP Play Mode 使用实际 Resources 绑定的 Windows 原生 Texture2D 渲染并查看 8401、10412、10702、10503、10601 待机与点击各三个时点（共三十帧），并查看 10501 待机与点击动作各三个时点；原骨骼网格与贴图可见，姿态按原动画变化。原尺寸、mip、Bilinear 和 Repeat 采样字段检查通过。此项验证只覆盖独立绘制，未覆盖正式详情窗口、原应用控制器驱动的动作切换、过渡混合及 Windows Player。全部伙伴立绘仍须补齐上述未绑定动画分支并接通原应用控制器。本轮样本验证通过；全工程静态检查报告既有文件 `Assets/Game/Runtime/Official/Combat/FightBattlePetEnergyRuntime.cs` 缺少稳定 `.meta`；主工程 Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 未完成，Unity 报告工程已由另一实例打开。

## 自定义实现

用户已确认：自定义伙伴称谓通过原字库与长度校验后直接在本地保存，不等待线上文本审核；保留原伙伴资格及好感等级门槛。自动伙伴在创建角色及读档后检查原相遇条件，满足且尚未拥有时自动结识，初始好感使用原配置；此触发时机是确认的单机方案，不声称取得服务器实现。

服务端通话创建／最终确认方法未取得。用户补证已完成前段并满足后续条件时可当天续接，超出好感最高值经原浪费确认后仍累计；本地按此保存，不反推旧结识日期。

送礼扣库存、好感、档案／通话奖励与历史共用本地事务，失败回滚；相遇仅由当前格式的伙伴记录与实际相遇流程决定。

## 尚未实现

- 完整 `NpcPanel` 宿主的对话／星神页、伙伴档案与餐桌通话；当前 Home 已接通原生名册卡片、本地相遇／好感／置顶、详情属性区、赠礼控制器和好感说明弹窗。
- 正式窗口中的立绘／小模型、完整详情简报、伙伴位置导航、赠礼物品详情及声音仍未全部接通。
- 主工程 Play Mode 已通过名册卡片和赠礼按钮的模拟射线点击、赠送落盘及读档恢复，原生区域在 450×800 下显示，初始化遮罩、礼物空图标和属性键名问题已修复。完整页面交互、手动结识的正式宿主流程、声音与 Windows Player 仍未完成。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。
- 史莱姆、孤独摇滚的八位伙伴及全部相关入口按[离线政策](offline-access.md#暂不开放的联动活动及伙伴)暂停，不列为自动开放目标。

## 代码与资源入口

[NpcDialoguePanel](../../.tools/source-packages/official-code/client-source/NpcDialoguePanel.cs)、

[`NpcPanelNpcItem.Refresh`](../../.tools/source-packages/official-code/client-source/NpcPanelNpcItem.cs)、[`NpcDetailPanelDetailContent.Refresh`](../../.tools/source-packages/official-code/client-source/NpcDetailPanelDetailContent.cs)

[`NpcAdventureModelItem.InitContent`](../../.tools/source-packages/official-code/client-source/NpcAdventureModelItem.cs)、[伙伴图像来源清单](../../assets/reference/partner-ui/source-manifest.json)、[档案来源清单](../../assets/reference/npc-cultivation/source-manifest.json)、[对话来源清单](../../assets/reference/partner-interactions/source-manifest.json)

[羁绊助战模型来源清单](../../assets/reference/npc-adventure-models/source-manifest.json)、
