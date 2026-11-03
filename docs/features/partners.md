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

`prepare_partner_star_god_native.py` 从原 `NpcPanel` 取完整星神区域，仅解除根父级，并沿实际外部组件引用恢复 2,552 对象的 `StarGodNpcItemGroupItem`；跨 prefab 引用保留确切组件 fileID，不用根 GameObject 替代。保留原坐标、层级、材质、粒子、网格及动画，按当前配置及 CN 地区图片选择提取 10608／10708／10801 的原静态立绘、九种结识信物图标和中文文本。两条原星神开场／解锁 Wwise 事件沿实际 bank／媒体解码为 PCM；普通点击复用已有同事件原 PCM。Windows Mesh 导出中的空二进制字段改用 Unity 自身的空标量格式，修复原 `null` 文本导致的顶点数据读取断言及 UI 几何线程崩溃；原位置、索引、UV、通道及其他字段保留。资源先核对 APK，再取所选云端与已验证 Windows 转换缓存，284 项实际安装文件／目录及 39 份脚本绑定见[星神资源来源](../../assets/reference/partner-star-god-native-source-manifest.json)。`OfficialPartnerStarGodAssetCatalog` 显式绑定实际 Resources 页面、三个星神、图片路径／原 GUID 和文字；本批未纳入素材快照。

`OfflinePartnerStarGodUiServices` 将相遇、置顶及好感事件接到现有本地名册事务；保存成功后才通知原卡片，连续相遇请求在事务完成前合并。已拥有星神保留同一运行对象，先更新等级、经验、时间与置顶字段再通知；等级通知单独保留上次已通知等级，避免同次置顶／等级更新或读状态操作吞掉等级变化。预览记录不写入存档。`PartnerConditionRules.GetMeetNpcItems` 直接迁移原 `NpcHelper` 方法，只枚举复合条件的直接子项并保留原顺序，不用普通伙伴的首个信物进度替代三种材料。`PartnerStarGodPanelView` 在停用父级内实例化原页面，绑定对象池、图像、语言、声音与宿主注入后启用；关闭时先停用，恢复外层图像／语言服务并释放句柄。代码与所依赖原始凭据见[星神本地绑定来源](../../assets/reference/partner-star-god-local-source-manifest.json)。完整 `NpcPanel` 入口尚未接到该宿主。

`prepare_partner_star_god_validation.py` 建立包含原 UI 粒子程序集的隔离工程，以实际安装的 Resources 目录运行 Unity URP Play Mode。原按钮的模拟指针射线验证详情端口、禁用分支、相遇请求、原音频播放、动画延迟及拥有对象身份更新；另验证原分组／居中、置顶优先、链接等级事件、长整型比较溢出、关闭取消提示、重新开启及图片／事件释放。核对 Mesh 原四顶点和六索引，已查看单个、三人分组及解锁后的三帧 450×800 GPU 画面。

`prepare_partner_star_god_local_validation.py` 补齐实际程序集的 `.asmref` 目录及 ItemIcon 资源依赖，在同版本 URP Play Mode 使用正式本地星神服务、原页面和隔离存档。模拟鼠标射线验证连续点击仅结识一次、三份不同信物一起扣除、磁盘保存后发新增事件、按原动画长度乘 0.8 后提示、拥有对象替换预览与详情请求；新建会话恢复结识、材料与好感。外部事务同时更新链接等级和置顶，读状态后仍收到原 1→5 等级通知，并保留卡片对象身份。材料不足拒绝相遇；重新打开后原分组显示另外两位可相遇星神，九种信物均通过原 ItemIcon 的实际图像绑定。已查看信物、结识、等级／置顶和三人分组四帧 450×800 GPU 画面，运行无 Unity 错误。提示图计算、信物详情弹窗、完整正式分页入口、详情窗口内容及 Windows Player 不在该隔离验证范围；不据此记为伙伴功能完成。

本轮全工程静态检查通过（84,865 个 GUID）；主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。原生资源记录 93 项输入、284 项安装输出及 39 份脚本绑定。完整正式宿主仍未完成。

### 当前完整名册窗口资源与原生 Canvas 后端（局部）

`prepare_partner_panel_native.py` 恢复当前 `884_156692` 完整 `NpcPanel` 的 2,962 个对象，不再截取单页；沿原外部引用恢复 200 对象的对话条目，并复用名册卡片与星神分组。保留三个分页、关闭入口、标题、层级、坐标、原材质、粒子和动画。原 `StateAnimationConfig`／`StateAnimationData` 仅适配命名空间与既有状态接口；分页控制器的无动作初始状态和 Entry 选择器对照包内编译常量逐项验证，未改写为简化状态机。三段与已安装名册同来源、同字节的动画显式重映射到既有 GUID，保留共享控制器和元数据。输入、对象身份、映射、输出及显式 Resources 目录见[完整窗口资源来源](../../assets/reference/partner-panel-native-source-manifest.json)；本批未生成素材快照。

窗口依赖的家具修复／拥有、未读通话、伙伴动态链接及宠物升级／进化／潜能／技能／能力提示节点由原代码直接迁移到 `Roster/TipDots/`，只将玩家、解析器、条件订阅及克隆依赖改为显式端口。保留原注册／注销顺序、宠物食物倒序累计及严格大于所需经验的比较，未删除原提示图。完整窗口对应的本地数据与图运行服务仍待绑定，不据此声称提示功能已接通。

`ProfessionNativeUiGraphic` 保留原六个玩家外观原型的序列化字段；这些原型的骨骼资产原本为空，未选择玩家时不生成替代图形。独立 Canvas 后端消费已有原生骨骼姿态及网格批次，使用原图集页、材质、UV、顶点色和索引，按 Canvas 像素比例绘制并接受 UI 遮罩。动画求值由显式姿态来源负责；完整玩家外观动画过渡、额外翻转／布局／物理／分隔参数不在本后端已完成范围。代码及加载绑定见[窗口运行绑定来源](../../assets/reference/partner-panel-runtime-source-manifest.json)。

`prepare_partner_panel_validation.py` 在隔离 Unity URP Play Mode 导入完整窗口依赖，检查六个原外观原型、原材质与动画字段以及全部组件引用。独立绘制使用实际 8401／10412 原生骨骼、图集和贴图，验证三个待机时点的顶点变化、几何／材质通知、冻结后重开、网格边界及重复释放；RectMask2D 额外检查实际 GPU 裁剪边界外无角色像素。已查看八帧 450×800 GPU 画面，运行无 Unity 错误。姿态来源在此验证中显式采用未混合动画，不代表原应用动画图与完整窗口交互已经完成。全工程静态检查通过（84,993 个 GUID），主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。完整窗口共记录 481 项安装路径；窗口宿主与交互验证见下段，正式入口的数据服务与 Windows Player 尚未完成。

### 当前完整名册窗口宿主与分页（局部）

`PartnerPanelView` 实例化完整原 `NpcPanel`，按原层级启用名册、通话和星神页，保留原弹出／收起、分页状态切换、功能限制和返回按钮。窗口生命周期显式绑定中文、字体、图像、池、原生外观裁剪及原控制器的数据服务，关闭时恢复外层服务；新克隆的三类列表行在消费数据前配置服务。`OfflineProfessionNativeSkinClipBindings` 连接实际 `AppearanceCharacter`、`AppearanceUI` 与原生 Canvas 图形；原部件组件可读取实际骨架接口，尚不支持的分隔绘制继续明确拒绝。

`prepare_partner_panel_native.py` 修正原 `AnimatorOverrideController` 的 Unity 引用为 Native 类型 2，保留原对象、GUID、覆盖动画及原状态机；旧安装升级先校验既有来源哈希，仅允许该引用类型变更。`partner_panel_language.py` 从原语言二进制读取完整窗口所需 16 项键，在原 Resources 目录显式绑定。来源与哈希见上文两份窗口来源清单，未新增替代布局或素材。

`prepare_partner_panel_host_validation.py` 在隔离 Unity URP Play Mode 使用完整原窗口、真实名册／星神本地服务和隔离存档，经正式事务自动结识基础等级 1 的 8402；模拟射线点击三页及原返回按钮，验证原动画选中状态、收起完成后释放、指定星神页重开、功能 57／174 限制与默认页回退。已查看名册、通话空状态、星神、重开和限制后五帧 450×800 GPU 画面，运行无 Unity 错误。原窗口同一返回按钮上有两个原 `CloseWindowBtn`，保留两个组件及原调用，不以单组件假设删减。

本夹具只验证新存档没有通话的空状态及原备用行，不代表正式通话存档／播放服务已完成；提示图由明确夹具端点提供。分页和返回音效从原 `UIClickSound.AkEvent` 恢复事件及实际 Wwise 媒体，显式绑定 `Main_Tag`（680474747）和原返回点击事件（3772241779）。`OfflinePartnerUiSoundScope` 保留好感窗口默认解析器，增加完整窗口解析器，并将原 `AkGameObj` 的注册、定位和注销转发给底层声音宿主；窗口关闭恢复宿主，已发声的独立音源按 PCM 时长结束。隔离 Play Mode 已验证模拟分页点击使用实际 PCM 的播放状态、声音对象生命周期无异常及关闭恢复，并复核上述五帧 GPU 画面。全工程静态检查通过（85,050 个 GUID）；主工程新增资源导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。正式 Home 完整窗口入口、全部详情依赖、助战集成及 Windows Player 仍须继续。

### 当前通话配置与原状态推进规则（局部）

`prepare_partner_dialogue_configuration.py` 使用选定 `884_156692` 的原生成构造器解码 `npc_dialogue.g`，取得 349 组、6,707 个节点。运行时 `OfficialPartnerConversationCatalog` 保留原后续节点／通话顺序、发言人、每日限制、解锁／连锁条件及奖励数值；`OfficialPartnerConversationAssetCatalog` 在 Resources 显式绑定本地表。条件通过实际玩家端口注入，未将缺少的条件实现默认放行。原包、对象、载荷、完整消费格式、生成器、运行适配及安装哈希见[通话配置来源](../../assets/reference/partner-dialogue-rules-source-manifest.json)。

`PartnerConversationRecord.Create` 和 `SetCurrentNodeAndUpdateStatus` 直接迁移原共享 `PlayerNpcDialogueDataWrap` 的两个方法，只将数据库身份生成、时钟和玩家／配置访问改为显式本地端口。保留创建时的首节点、历史追加、更新时间、末节点结束、任一后续条件未满足即暂停及最后发出更新通知的顺序。原推进方法在全部后续条件满足时仍保留已有状态，未擅自将既有 `Pause` 改成 `InProgress`。原方法、枚举、模板与输出身份见[通话状态规则来源](../../assets/reference/partner-conversation-rules-source-manifest.json)。

纯 C# 验证对实际全部节点运行终止／继续分支，核对运行时表中的顺序、空值、每日限制和奖励数量，并覆盖身份、历史、分支暂停及通知顺序；全部通过。隔离 Unity 工程复制实际 Progression 及其依赖程序集，在 Play Mode 通过实际 Resources 目录加载完整表；条件采用明确的不可执行验证端口，该夹具不验证玩家条件或通话交互。该阶段主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。实际条件、存档与本地刷新／确认事务见下文，正式界面和原播放窗口尚未接通。

### 当前通话条件闭包（局部）

`prepare_partner_conversation_conditions.py` 从同版共享实现提取复合与／或、逻辑取反、伙伴好感、已结束通话、剧情次数、地图进度和区域女神条件，复用已迁移的无条件、等级及区域激活条件。当前全部通话节点引用 208 个条件 ID，闭包共 11 类。保留原短路顺序、好感反向分支、结束通话计数、比较运算，以及地图进度转百分数的浮点计算和整数截断；缺少引用、循环或未支持类型直接报错。原方法、模板、运行输出及验证源码哈希见[通话条件来源](../../assets/reference/partner-conversation-conditions-source-manifest.json)。

纯 C# 验证执行实际配置中的 6,912 处节点／连锁条件，覆盖 8402 好感 20／40 门槛、前段完成要求、地图进度 9,999／10,000、逻辑取反、女神激活和复合条件短路，全部通过。隔离 Unity 实际依赖程序集编译及完整通话表 Resources 加载 Play Mode 通过。实际玩家与地图存档读取见下两节，本节不声明完整通话功能完成。

### 当前通话存档与玩家读取（局部）

`OfflineNpcConversationProgress` 随角色属性落盘，保留原 `PlayerNpcDialogueData` 的玩家／NPC／通话身份、通话类、当前节点、完整历史、状态和更新时间，另存 NPC 类 ID 的本地映射。时间使用 ticks 与 Kind 字段承接 `JsonUtility`，历史复制隔离且保留原重复节点。旧档没有此字段时仅初始化为空，不编造历史通话；身份无效、重复通话 ID、状态或时间越界、历史末节点不一致会拒绝整笔保存。该适配不生成通话、不决定每日触发，也不发放奖励。

`OfflinePartnerConversationPlayer` 默认每次读取当前已提交角色，接通实际好感、基础等级、库存、地图可进入性、区域激活、剧情次数与结束通话，避免 `Reload` 替换玩家对象后仍使用旧集合。奖励事务通过 `ForCharacterMutation` 显式绑定角色克隆，使后续原条件读取同一事务中的物品／好感变化；世界／剧情仍读取已提交事实。世界服务绑定见下节，更新通知保留必需的事务端口。原字段、规则依赖、适配及验证源码哈希见[通话本地来源](../../assets/reference/partner-conversation-local-source-manifest.json)。

`prepare_partner_conversation_save_validation.py` 在隔离 Unity Play Mode 读取实际 Resources 通话表与真实角色存档，验证好感门槛读取、2021 完整节点推进、结束历史落盘后解锁 2022 连锁条件、原身份／时间恢复、剧情读取、重复历史与旧档兼容。重复 ID 导致整笔角色／库存变更回滚；实际临时写入路径被占用时，文件、内存和事务凭据均保留，释放后使用同一凭据重试成功。现有赠礼／结识／置顶的原条件、事务、落盘及旧档回归通过。该阶段主工程新增代码导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及全工程静态检查（85,063 个 GUID）通过。本地触发／奖励事务见下文，正式入口、原播放窗口、模拟界面输入及 Windows Player 仍未完成。

### 当前通话地图条件与实际存档（局部）

`prepare_partner_conversation_world.py` 从当前官方 MapInfoWrap 与实体扩展配置解码通话实际使用的 11／12／13／14／17／19／21／23 八张地图、831 个区域最大进度，按原 `MapRegionEntityInfoManager` 的实体扩展类型和位置区域恢复两处女神查询。Map11／56 原静态女神身份为 0，Map17／0 为 110；前者即使存在区域激活记录，也保持原女神条件不满足。提取时验证被查询区域中的女神唯一性，避免将不明确的区域记录当作指定实体已经激活。

`OfficialPartnerConversationWorldAssetCatalog` 在 Resources 显式绑定这些本地配置；`OfflinePartnerConversationWorld` 读取当前角色的实际区域进度和激活记录。主线 `RefreshRegionProgress` 已经把原计算器的万分比结果写入 `OfflineRegionProgress.Progress`，此处直接消费该结果，不再次除以区域最大值；原通话条件继续完成百分数转换与截断。当前通话闭包没有整图进度查询，负区域输入明确拒绝。实际包／对象、MessagePack 字段格式、完整消费的实体扩展构造器、生产存档单位、输出与验证源码哈希见[通话地图来源](../../assets/reference/partner-conversation-world-source-manifest.json)。本批未纳入素材快照。

隔离 Unity Play Mode 使用实际 Resources 目录、原通话条件和真实档案适配器执行全部 6,912 处条件，验证未探索状态、9,999／10,000 进度边界、原复合好感条件、ID 0 拒绝区域激活替代、实体 110 激活及读档恢复，全部通过。全工程静态检查通过（85,068 个 GUID），该阶段主工程 `Zjcs.Editor.ProjectTools.Validate` 未执行成功，Unity 报告工程正由另一实例打开；隔离程序集编译通过。正式 Home 入口和原播放窗口尚未接入，本地刷新／奖励事务见下文。

### 当前通讯录与通话存档绑定（局部）

`OfflinePartnerDialogueUiServices` 将实际已提交的通话记录转为原通讯录消费的 NPC／通话视图，保留玩家、NPC、通话身份和 NPC 类映射，不为旧档编造身份。原列表首次收到某 NPC 的新增通话时会扫描当前已发布集合，后续新增则直接追加；本地服务逐条发布新增通知，避免同批两段通话重复显示。已有通话更新时保留原对象引用，无变化读档不重复通知；删除先移出通话集合，再通知原列表重新整理，最后移除不再拥有通话的 NPC 视图。发布前完整校验原节点与身份映射，避免部分错误记录进入界面。

后续节点与条件直接读取实际原配置及本地玩家，回放取得隔离的历史副本；已删除或回收的旧行不能打开其他历史。原实例化、头像初始化、错误提示和窗口展示保留必需的表现端口，不用替代布局或空实现补齐。原客户端／共享消费源码、UI 契约、适配代码和验证输入输出哈希见[通讯录存档绑定来源](../../assets/reference/partner-dialogue-local-ui-source-manifest.json)。

`prepare_partner_dialogue_local_ui_validation.py` 在隔离 Unity Play Mode 编译实际适配器与原契约，使用实际 Resources 通话表和磁盘档案，验证同批首个 NPC 的两段新增不重复、先保存后通知、引用稳定、无变化重读、原节点条件、历史副本隔离、删除顺序及过期行拒绝。实际写盘失败保持文件、内存、界面视图和通知流；身份映射冲突在任何通知前拒绝，全部通过。全工程静态检查通过（85,072 个 GUID）。本批主工程校验因另一 Unity 实例占用而未能运行；本节仅表示状态与通知适配通过；非空原生列表及头像的实际验证见下文，完整播放窗口及 Windows Player 仍未完成。

### 当前原通话每日限制（局部）

`PartnerConversationDayLimitRules` 直接迁移共享 `Player.CheckNpcDialogueHasDayLimit`／`RecordNpcDialogueDayLimit` 和三项 `TimeUtils` 重置方法。仅原首节点标记 `IsDayLimit` 的通话检查并增加对应 NPC 身份的次数；未标记的通话不消耗额度，缺少配置时检查拒绝、记录不变。保留原时钟读取顺序、重置前清零再递增、更新时间及服务器时区转 UTC 的取模分支；当前原设置为每日 08:00 和额度 1，具体时区由调用方显式绑定。2021／2022 原首节点均不受每日限制，不附加“每天最多续接一次”的新规则。

`OfflineNpcConversationDayLimit` 保存原 NPC 身份、次数、上次显示时间，时间继续使用 ticks／Kind；`OfflinePartnerConversationDayLimits` 绑定角色事务的克隆对象，不独立提交。旧通话档案仅补空集合，不推断过去次数；重复 NPC 计数、负次数和非法时间拒绝整笔保存。原方法、字段、生成模板、运行输出及验证源码哈希见[每日通话限制来源](../../assets/reference/partner-conversation-day-limits-source-manifest.json)。此模块提供原检查／记账，创建时机按下文已确认的单机方案接线。

隔离 Unity Play Mode 使用实际完整通话表及实际每日设置，覆盖全部不限制首节点、首次允许／同 NPC 达限／不同 NPC、精确重置秒、正负时区取模、分钟秒保留和 Unix 起点钳制。真实磁盘档案验证计数恢复、重复身份事务拒绝、写盘失败保留原文件／内存／凭据及旧档补空；赠礼、结识、置顶的现有事务回归通过。主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过，全工程静态检查通过（85,074 个 GUID）。本地创建／奖励事务见下文，正式入口与 Windows Player 仍未完成。

### 当前打开页面检查与节点奖励事务（局部）

`OfflinePartnerConversationService.RefreshOnPanelOpen` 承接下文已确认的单机创建时机，读取当前已结识 NPC 的原 `Dialogues` 顺序，检查尚未开始、原 Auto 类型、原解锁／连锁条件及每日额度后，复用原 `Create`。同一 NPC 后续通话复用已存身份，原功能可用性与保存后的表现通知由宿主显式绑定；拒绝无主计数和冲突身份，避免另建 NPC 身份绕过已有额度。暂停联动伙伴继续排除，重复打开不重复创建。

`OfflineLocalActorIdentities` 保存档案内的主角 ID、NPC 配置到实例的映射及单调分配上限，供通话和后续队伍入口共用。旧档尚无映射时，事务性接收已有通话的玩家／NPC ID，并将已有通话和领取凭据中的 ID 纳入分配上限；不改旧历史、奖励或每日次数，也不反推结识时间。仅分配身份不会创建通话。新构造入口使用存档克隆内的分配器，新结识 NPC 的映射、新通话、每日次数与分配上限一起保存；写盘失败不发布或消耗 ID。身份类别冲突、映射不一致及整数耗尽拒绝整笔保存。此处是本地对象标识适配，不是官方服务器 ID 生成规则。

隔离 Unity Play Mode 已使用实际档案验证旧映射迁移、重读不换号、重复初始化不写盘、新结识但尚不满足通话条件的伙伴映射、生产分配器创建原 2021 通话、真实迁移／创建写盘失败与重试、历史删除测试后不复用奖励 ID、旧领取凭据保护、冲突及溢出拒绝。删除历史仅为明确的测试场景，正式界面没有删除入口。完整节点奖励／同日续接／每日额度回归同时通过，主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过，全工程静态检查通过。正式 Home 入口尚未调用这一持久化身份服务，完整播放及 Windows Player 仍未完成。

`Acknowledge` 只允许原当前节点的合法、已解锁后继。该节点的配置物品、好感和原状态／历史更新在同一角色事务落盘，最后再通知外部表现；每个通话／节点保存稳定凭据，重复确认和回看不重复领奖。好感阈值读取当前伙伴配置，等级方法直接复制原 `NpcFriendshipLevelInfoParser.GetFriendshipLevelByExp`，仅适配配置查找、行类型和阶位类型，经过逐词核对；不套用历史配置的阈值。节点状态仍由原方法按原顺序决定。来源、原方法核验、明确的单机边界和验证输入输出哈希见[通话事务来源](../../assets/reference/partner-conversation-transactions-source-manifest.json)。

隔离 Unity Play Mode 使用实际 Resources、完整条件、实际地图与角色档案，验证显式打开才创建 2021、原完整路径推进及配置奖励 110×3、非法后继拒绝、奖励节点真实写盘失败整体回滚、重读后凭据防重、结束后不自动创建及再次打开时同日续接 2022、NPC 身份复用和 8406 每日计数随通话保存。事务内玩家克隆的库存／好感读取与线上视图隔离。当前全部原节点的 `RewardFriendship` 均为 0；非零发奖分支仅使用明确标注并恢复的内存测试值验证，不写入官方配置或交付数据。上述检查通过，八张实际地图、6,912 个条件根及保存单位／女神身份回归通过。本模块已在隔离工程编译运行，并随下文播放窗口迁移通过主工程 `Validate` 和全工程静态检查。具体 Home 身份／功能绑定、完整播放窗口和 Windows Player 仍未完成；非空通讯录头像及原选项组的模拟输入与画面验证范围见下文。

### 当前原通话播放控制器与原生窗口（局部）

`prepare_partner_dialogue_playback_ui.py` 从选定 `884_156692` 迁移原播放窗口、消息行、说话内容、选项组／按钮、奖励行、奖励路由及静态超链接／事件穿透控制器。保留原循环列表、历史复制、逐字显示、快进、自动后继确认、分支选项、延迟刷新和奖励窗口延迟顺序；配置、头像、奖励图标池、导航与确认请求使用显式服务。语言选项键直接提取原方法，不另造命名。原代码与适配输出哈希见[播放控制器来源](../../assets/reference/partner-dialogue-playback-ui-source-manifest.json)。

`prepare_partner_dialogue_playback_native.py` 恢复完整原 `NpcDialoguePanel`、外部消息行及原 Windows 材质／贴图／动画／音效依赖，保留原层级、坐标、字段与引用；同源同字节资源及既有规范动画身份复用。原通话与分支文字按实际全部节点选择，随 `OfficialPartnerDialoguePlaybackAssetCatalog` 显式绑定。来源、实际包／对象、转换、复用与安装哈希见[播放窗口来源](../../assets/reference/partner-dialogue-playback-native-source-manifest.json)。本批素材尚未纳入完整素材快照。

本地通讯录视图补齐原玩家身份和稳定的节点历史集合；`OfflinePartnerDialoguePlaybackServices` 将原确认请求接到上文本地事务，落盘成功后才同步原通知，磁盘写入／权限失败返回错误完成，不发布未提交状态。`PartnerDialoguePlaybackView` 管理原窗口、消息池及文字／图像／声音绑定寿命；具体头像、奖励弹窗、超链接导航与 Home 调用仍须绑定。绑定与验证源码哈希见[本地播放来源](../../assets/reference/partner-dialogue-playback-runtime-source-manifest.json)。

隔离 Unity Play Mode 已导入完整原窗口及外部消息行，核对全部组件引用，使用实际 2021 分支、官方字体／文字及原选项按钮模拟鼠标输入，验证真实写盘失败后不改存档／历史、不发通知，重试合法分支后保存节点 3，读档恢复、稳定视图／历史集合身份、过期另一选项拒绝、原组选项隐藏及奖励处理器启停订阅。三次点击均调用原绑定 PCM 的本地播放，选项前后实际渲染截图已查看。该检查明确直接准备原节点 2 并只初始化原选项组，未验证完整消息列表的自动播放、头像、奖励弹窗或超链接导航。主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过，全工程静态检查通过（86,017 个 GUID）；上文本地通话事务也随本次主工程验证通过。完整窗口交互、Home 调用及 Windows Player 尚未完成。

### 当前原伙伴身份、职业与战力数据（局部）

`OfflinePartnerNpcRuntime` 直接执行选定 `884_156692` 的 `NpcTargetData`、`NpcFightData.CreateByNpcTarget`、`NpcLevelDetailInfoParser` 和 `BattleFormulaHandler`，生成原头像控件需要的身份、姓名键、图标、性别、职业、等级、阶位及战力。当前原表有 148 条 NPC 定义与 97 类有战斗配置的 NPC；无战斗配置的定义不推断职业或战力。保留请求等级／阶位与原详情查询的等级边界、阶位回退、技能生成、属性扣除和战力算法；原通讯录采用的 1 级、Norank 参数与助战承接主角等级／阶位的调用参数分别由所属入口提供，不把 `SubRank.None` 当成 `Norank`。

`OfficialPartnerNpcRuntimeCatalog` 显式绑定 14 张原始 Luban 二进制表及所需程序集，原属性二进制随 Windows 本地文件交付；运行前核对载荷 SHA256 和程序集 MVID。当前 Console／FirstPass 与历史地图模块采用不同程序集名称，避免覆盖历史版本；除 `RawFileUtility.GetRawFilePath` 改为读取已打包的本地绝对路径外，原类型、字段与全部方法指令须通过转换前后指纹核对。LiteDB 使用原程序集要求的 5.0.20 发布者强命名实现并附许可证，不把 IL2CPP 签名占位程序集当作可执行实现。原 `Parsers`、公式和日志服务仅在本次解析／查询作用域内绑定，结束或异常后恢复；释放时关闭原二进制读取器。输入、平台适配指令、发布者包、资源引用及源码哈希见[伙伴运行数据来源](../../assets/reference/partner-npc-runtime-source-manifest.json)，重建入口为 `prepare_partner_shared_identity.ps1`／`prepare_partner_npc_runtime.py`。本批资源未纳入素材快照。

隔离 Unity Play Mode 对照原二进制表的全部 148 条 NPC 身份与 97 类战斗配置，完成 1 级／Norank、20 级／Blackiron3 和 100 级下原参考属性组全部 40 个有效阶位的 4,074 次投影；原 8402 的 1 级／Norank 职业为法师、战力为 1045。原请求身份与等级／阶位保留、空阶位拒绝、未知 NPC 异常后的静态服务恢复、两个配置实例交替查询／释放／重建、历史地图程序集共存及重开均通过。主工程导入／编译和 `Zjcs.Editor.ProjectTools.Validate` 通过，全工程静态检查通过（86,311 个 GUID）。原通讯录头像绑定及实际输入验证见下文；正式 Home、完整通话播放及 Windows Player 尚未完成。

### 当前原通讯录头像与非空列表（局部）

`OfflinePartnerNpcIconBindings` 将上述原战斗资料接到原 `TeamPlayerIcon`／`TeamPlayerIconOther` 和 `PlayerInfoUI`：通讯录保持原 1 级／Norank 参数、NPC 身份、姓名翻译和原在线 NPC 战力分支，职业图标路径直接执行原 `ProfessionBaseInfoParser.GetFirstIconPath`／`GetThirdIconPath`。身份不匹配时拒绝刷新；主角资料、头像、弹窗、助战资格及血条仍由宿主提供明确依赖，NPC 图像目录未命中时回到宿主资源加载器，不覆盖主角图像。原通讯录 prefab 的 `ProfessionIconGo` 引用为空，保留原不更新职业图标的分支；不擅自补引用改变显示。

`prepare_partner_npc_icons.py` 按实际 NPC 战斗表与职业表选择 151 个原图标地址，覆盖开放的 89 类战斗 NPC 及 31 个职业的一阶／三阶图标。先按官方 APK 索引、实际包／CAB／对象及原 Sprite 渲染键定位，缺少包时才使用同版已校验的云端缓存；复用原 Windows 转换缓存，保留 Sprite 网格、索引、图集纹理和采样参数，并在 `Resources/Partners/OfficialPartnerNpcIconImages` 显式绑定。已有同 GUID 资源仅在同载荷时复用。来源、处理与哈希见[伙伴头像来源](../../assets/reference/partner-npc-icons-source-manifest.json)；本批未纳入完整素材快照。

`prepare_partner_npc_icon_validation.py` 在完整原三页 `NpcPanel` 的隔离 Unity Play Mode 中使用真实档案和实际 2021 通话记录，验证全部 151 个原生 Sprite／纹理／网格导入、89 类伙伴与原职业图标取用、头像身份／等级／阶位／战力、姓名及原节点中文预览、错配身份拒绝。模拟鼠标射线点击原联系人按钮展开列表，再点击原通话条目选中实际记录；读档保留记录和稳定视图身份，原标签切换与返回按钮继续通过。原整行按钮覆盖头像区域，按原交互展开列表，不绕过遮挡触发头像弹窗。展开前后实际 450×800 渲染截图已查看；主工程导入／编译、`Zjcs.Editor.ProjectTools.Validate` 与全工程静态检查（87,399 个 GUID）通过。

本节验证只覆盖原通讯录 NPC 分支；主角头像与血条、完整消息播放、奖励弹窗、超链接、Home 身份／功能接线及 Windows Player 未执行。通话预览使用已迁移的原播放文字目录；正式宿主须一并绑定该文字服务与原 `UIUtils.ResetUI` 的变换／自动激活顺序。整体伙伴功能仍未完成。

### 当前资料控件与 NPC 目标的版本接线（局部）

`prepare_partner_player_info.py` 从所选 `884_156692` 客户端重新迁移完整 `PlayerInfoUI`，继续复用原状态切换、外观／头像分支、文字、资源句柄和宿主接口。当前版本的收藏图标直接消费调用方的显示参数；置灰遮罩先检查收藏图片引用、显示参数及目标是否存在，再执行原合击兼容或探索首领助战次数／冷却分支。控件不再自行重复旧版的主角、伙伴收藏和功能限制判断。其他职业头像控件仍保留各自历史版本来源，不将整套职业 UI 标记为当前版本。

NPC 资料投影同时保留实际原 `NpcTargetData` 对象的 `ServerId`，头像目标直接转发这一值。通讯录使用的原构造函数未赋服务器编号，继承 `ActorTargetData` 的 `-1` 默认值，不再写入 `0`。原始源码、程序集、适配依赖和逐文件哈希见[当前伙伴资料控件来源](../../assets/reference/partner-player-info-source-manifest.json)。本项仍属于完整主角资料／消息行接线的局部工作。

完整原三页窗口的隔离 Play Mode 已通过：实际 151 个 NPC／职业图标导入与取用、NPC 默认服务器编号、原收藏图片显隐、联系人展开、通话选择、读档后记录／视图身份、标签切换和返回按钮均完成验证，实际 450×800 收藏显示／隐藏及展开画面已查看。通讯录原 `PinMask` 引用为空，保持原资源；没有补造遮罩。独立资料控件检查另行覆盖调用方显隐、空目标、合击兼容、原助战次数／冷却短路，并回归外观、文字、九类事件、句柄释放与模拟鼠标按钮；其画面只是分支检查夹具，不计为原版页面美术。根工程导入／编译、`ProjectTools.Validate` 及静态检查（88,268 个 GUID）通过。完整主角资料、消息播放、Home 接线与 Windows Player 仍未完成。

### 当前主角资料的本地投影（局部）

`OfflinePartnerPlayerInfoSource` 沿用当前原 `PlayerInfoData(Player)` 的字段顺序，将选中真实存档的稳定角色 ID、原样姓名、等级／阶位、实际配置属性计算器的战力，以及既有外观协议适配接到资料结构。姓名使用原 `CultureTextArg` 的不翻译分支，头像路径接受宿主的可空值；不编造服务器头像初始值。玩家目标继承原 `ServerId=-1`，形象路径与玩家助战资格仍要求宿主显式提供原调用绑定。资料中的 `EngagePetClassId` 读取 `FollowPetIndex` 对应宠物，区别于资料弹窗所用的第一技能方案宠物，不能互相替代。来源与逐文件哈希见[主角资料来源](../../assets/reference/partner-main-player-source-manifest.json)。

本地事务会替换 `Save.Player`，因此宿主在事务成功后调用 `SynchronizeCommittedPlayer`，先构造完整的新一代资料，再通过原管理器 `SafeAction OnPlayerUpdate` 通知控件重新读取。旧代保留原九类事件订阅接口但不逐字段修改；不推测服务器的字段通知顺序。此读取适配不改写游戏存档，跨来源资料拒绝读取，构造失败保留上次资料且不通知。生产宿主须使用已接上装备／技能等来源的实际属性服务，不能用缺项计算器或夹具资料冒充完整战力。

隔离 Unity 编辑器验证已通过真实档案创建、稳定身份、等级及重算战力、男女／职业外观切换、跟随宠物与技能方案宠物不同的分支、通知时完整数据可读、失败不发布／不写盘及真实重新读档。主工程导入／编译、`ProjectTools.Validate` 与静态检查（88,272 个 GUID）通过。该项只验证数据投影，没有把字段断言算作页面交互／画面验证；原主角 UI 外观分支、完整消息行、Home 生产宿主及 Windows Player 仍未接通。

### 当前主角 UI 骨架头像（局部）

`prepare_partner_ui_appearance.py` 从选定 `884_156692` 迁入当前 `AppearanceUI` 与 `PlayerAnimationAppearance`，接到已迁移的同版 `AppearanceSpine`／`AppearanceCharacter`／`PlayerAppearance`。保留先选择动画再加载骨架、遮罩延迟应用皮肤／染色、原材质需求加载及销毁前解绑／释放配置的顺序。`PlayerInfoUI.PlayerAppearance` 使用原共同基类 `PlayerAppearanceInfo`，使当前与历史原派生类都能保持原序列化引用和 `RefreshAppearance` 调用；不改变自己／他人或 UI／场景分支。

`OfflineProfessionUiAppearanceBindings` 预热当前男女原生骨架，按原 GUID 加载皮肤及 High 纹理配置，并绑定实际 Canvas 端点。`OfflineProfessionUiAnimationBindings` 保留未初始化时只设置起始动画的原分支；不会将 prefab 遗留的 `home/idle_M_01` 冒充有效动画。原规则在初始化之前选择 `map/idle_01`。`ProfessionNativeUiCharacterPose` 保留轨道、循环及重播归零；默认姿态生产已包含下节的原生约束与物理计算，也可显式传入完整姿态回调。当前原生发丝裁切端口接到同版外观类。

`prepare_partner_ui_high_textures.py` 恢复头像原字段指定的 `7747b1622b6dcd941ac31d869760ad8d` High 加载器，保留全部 702 项占位图映射、977 个实际纹理 GUID、原卸载／清晰度字段及纹理色彩空间、采样器和全部 mip；原 ASTC 转为 Windows RGBA32。占位图仅在原对象与编码图像字节一致时复用当前场景资产，实际纹理沿用 High 地址，不以场景 Low 加载器替代。显式入口为 `Partners/appearance-resources`，来源见[High 纹理来源](../../assets/reference/partner-ui-high-textures-source-manifest.json)。

`prepare_partner_ui_appearance_assets.py` 恢复原 `AppearanceAssetManagerConfig` 两个字段，显式绑定当前 APK 中已转换的 UI 染色 shader；它与历史版本的原始字节不同。默认骨架材质仅在完整原对象载荷一致时复用，分别保留来源。资源地址为 `Partners/appearance-asset-manager-config`；代码与资源输入输出哈希见[当前 UI 外观代码](../../assets/reference/partner-ui-appearance-source-manifest.json)和[材质配置来源](../../assets/reference/partner-ui-appearance-assets-source-manifest.json)。本批未生成完整素材快照。

`prepare_partner_main_avatar_validation.py` 在隔离工程保留原 `NpcPanel` 层级、字段、遮罩和资源，仅将六组外观脚本引用绑定到当前原类。Play Mode 使用其中真实头像子树及原玩家／自己状态配置，验证真实组合皮肤／染色、切换性别后切回的初始 GPU 像素完全一致，以及销毁后纹理引用归零且无运行错误。连续动画、跨循环与完整骨架数值对照范围见下节；完整资料控件刷新、鼠标交互和整页运行尚未验证。正式 `NpcPanel` 的当前外观脚本绑定、Home 宿主与 Windows Player 仍未接通。

头像代码与全部 High 纹理的主工程 Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 已通过；连续物理播放的接线与当前验证范围见下节。

### 当前主角原生物理计算（局部）

`ProfessionNativeSpringConstraint` 以独立平面弹簧／仿射数学恢复当前玩家实际使用的 X／Y 位移与旋转通道。按原参数保留惯性位移限制、固定步长余量、先衰减速度再施加弹簧／风力／重力、质量倒数、混合和变换顺序。平移先完成并更新枢轴，再计算旋转惯性；风力转矩按骨长与参考尺寸换算。零混合时保留状态和时钟；初始未激活约束的首帧绑定与时钟由宿主负责，不把动画重播时间当作骨骼物理时钟。

`scripts/test_partner_player_physics.py` 仅编译实际独立实现与数值回放夹具，不加载、编译或分发 Spine SDK。已通过 34 组、4,080 帧既录数值，覆盖组合位移／旋转、部分混合、零混合再激活、风力／重力、阻尼、质量、位移限制、不同步长／零时间步、不同骨长及连续旋转；大坐标按单精度相邻数的间隔检查，最大绝对差为 `3.05175781×10⁻⁵`。

可选 `capture_partner_player_physics_reference.py --reference-dll <现有二进制> --verify-player-assets` 仅在隔离目录通过公开 API 取得数值参照；固定回放数据已验证可逐字节重建。真实资产检查读取当前男女角色的原骨骼和 `map/idle_01` 通道，分别对 1,543／1,807 个独立物理约束的 120 帧输入矩阵进行对照，共 402,000 次，最大差为 `7.62939453×10⁻⁶`。该检查由参照提供各约束输入矩阵，不算独立层级求解、完整动画、原生 UI 或实际换装链已通过。人工大幅旋转／位移的全部皮肤同时激活压力输入仍有极少量更大浮点偏差，不能扩大为任意动画与任意状态完全一致。

来源、数据包、数值参照二进制、取样工具及实现／回放输入输出哈希见[主角物理依据](../../assets/reference/partner-player-physics-source-manifest.json)。当前原始玩家物理数据没有 ScaleX／ShearX 通道；尚未将这两类扩展视作支持。约束顺序、皮肤激活、初始绑定、原动画采样和物理时钟已接到下节的连续头像播放器。主工程 Unity 导入／编译与 `Zjcs.Editor.ProjectTools.Validate`、全工程静态检查（89,779 个 GUID）通过；正式头像连续播放、画面／输入、存档与 Windows Player 尚未完成。

### 当前主角头像连续动画（局部）

`ProfessionNativePlayerPhysicsPose` 使用当前男女原骨架的局部坐标、骨长、参考尺寸和皮肤约束集合，先求原 IK／变换约束，再按原顺序施加位移／旋转物理并更新后代矩阵。骨架物理时钟与动画轨道分开；重复播放只重置动画时间，皮肤暂时移除约束不清除其历史，更换骨架则重建状态。没有轨道时用显式原设置姿态求解，不选择其他动画代替；未支持的约束类型、交错物理顺序及 ScaleX／ShearX 仍明确拒绝。

原头像播放器启用独立构造的十段 Bezier 折线求值，并传递到骨骼、颜色、约束、物理和形变通道；该取样方式通过既有二进制公开 API 的数值输入确认，不复制 SDK 实现。对照输入将原二进制投影省略的变换约束零混合值显式补为零，避免 JSON 读取器的非零默认值改变参照；原数据包字节未改变。代码、原输入、既录数值和可选参照工具的哈希见[连续头像来源](../../assets/reference/partner-player-pose-source-manifest.json)，维护入口为 `record_partner_player_pose.py`。

`scripts/test_partner_player_bezier.py` 不加载 SDK，已通过当前原身体曲线及三组几何控制点的 244 个既录数值，最大差 `9.06×10⁻⁶`。隔离 URP Play Mode 使用原头像、遮罩、组合皮肤、High 纹理与染色材质，男女 × 两种职业均通过实际 `LateUpdate` 连续播放并跨过原 `2.666667` 秒循环边界；已查看四组连续播放 GPU 画面。重播保留物理时钟、更换性别后重建时钟、切回后初始 GPU 像素一致、释放后纹理引用归零及无运行错误均通过。每组另对 241 帧完整活动骨架作公开调用数值对照，合计 109,896 个姿态；矩阵系数最大差约 `3.04×10⁻⁶`，原坐标位置最大差约 `0.00055`。

上述结果只覆盖当前原默认头像组合及 `map/idle_01`，不扩大为任意外观、其他动画、正式页面输入、完整资料刷新、存档或 Windows Player 已验证。正式伙伴入口及详情中的统一外观生命周期仍待接线。主工程 Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate`、全工程静态检查（89,840 个 GUID）通过。

### 当前主角头像组件运行绑定（局部）

`OfflineProfessionUiPlayerBindings` 将当前原 `PlayerAnimationAppearance`、`AppearanceUI`、`AppearanceCharacter`、`AppearanceUIPartHandler` 与原生图形绑定为每个窗口独立的组件图。绑定在原窗口激活及骨架初始化前执行；从原 `AppearanceUI` 序列化字段读取 skin／High 加载器代理，逐项核对选定资源目录身份后保留代理。玩家外观、部件加载器及材质管理器由所属存档宿主提供，不生成另一套外观状态，也不改原部件排除、遮罩、初始动画、freeze 或 prefab 布局。

重复遍历已绑定树保留其动画轨道；绑定新窗口或回收行时先移除已销毁 Unity 对象。默认姿态生产者使用已迁移的连续物理求解，显式特殊姿态生产者仍可注入。原组件继续负责销毁时的图形、材质和纹理请求释放；材质／纹理服务寿命由宿主管理。代码及依赖凭据见[组件运行绑定来源](../../assets/reference/partner-ui-player-bindings-source-manifest.json)，记录入口为 `record_partner_ui_player_bindings.py`。

隔离 URP Play Mode 已以实际原头像、遮罩及 High 纹理验证男女 × 两种职业：重复绑定不替换轨道，实际连续播放跨过原循环，切换性别再切回恢复原初始像素；销毁后通过默认运行姿态生产者重新绑定，四组 GPU 像素均与原初始画面完全一致，释放后纹理引用归零且无运行错误。已查看四组连续播放画面。验证工程关闭 Editor 异步 shader 编译，避免将临时未染色画面作为基准，并检查实际原默认眼睛染色；完整冷导入与缓存运行均通过。主工程导入／编译、`Zjcs.Editor.ProjectTools.Validate` 及静态检查（89,841 个 GUID）通过。

本节只记录组件图接线；正式伙伴页完整资料、通话、入口及宿主服务生命周期仍待接通，不能据此计作完整窗口或 Windows Player 已完成。

### 原整页打开与通话事务接线（局部）

`OfflinePartnerConversationUiSession` 组合既有原条件／每日限制、本地事务、稳定通讯录视图与原播放服务，绑定 `PartnerPanelContext.PrepareOpen`。`PartnerPanelView.Open` 在激活控件前运行准备，已打开的窗口拒绝重复打开；构造服务不会创建新通话或注册时钟／状态变化钩子。读取已有本地角色身份，通话检查只由打开入口调用，事务成功后再同步 UI 视图；来源与接线哈希见[窗口通话接线来源](../../assets/reference/partner-conversation-window-source-manifest.json)。

隔离 Unity URP Play Mode 使用完整原三页窗口、正式本地组合与真实存档身份；将已自动结识的 8402 好感条件置为 40 后，构造组合仍无通话记录，实际打开窗口才按原顺序创建 2021 通话。三次真实打开只提交一次创建；已打开窗口的重复请求被拒绝，不额外运行准备。经原 EventSystem 射线和模拟输入验证带红点的通讯录折叠／展开、真实预览文本与选择回调、三页切换、原返回动画、关闭后重开和功能限制；运行截图已查看，无 Unity 错误。静态检查（90,066 个 GUID）、主工程导入／编译和 `Zjcs.Editor.ProjectTools.Validate` 通过。通话内容弹窗／节点交互未在本批渲染；正式 Home 入口尚未接入这个组合，不计作完整页面或 Windows Player 完成。

### 原提示图配置与资源目录（局部）

`OfficialPartnerTipDotAssetCatalog` 保留所选 `884_156692` 的全部 376 条 `tip_dot` 配置及原顺序，显式绑定当前伙伴窗口实际引用的 `DiningTable`、`NpcDetailItem`、`NpcProfileItem` 三张原图。复用已验证的 APK 表提取缓存，按所选官方 Luban 构造器解码；表载荷、官方构造器、原图和输出哈希见[提示图目录来源](../../assets/reference/partner-tipdot-catalog-source-manifest.json)，入口为 `prepare_partner_tipdot_catalog.py`。

原客户端的链接器／管理器读取配置表的图类型及作用域；两张动态子图资源仍序列化了静态类型，迁移保留两种原输入，不改原图来迁就配置。三张原图均通过具体编号节点作为入口，没有标记零号根节点，仍沿用原节点选择器。其他功能的配置条目保留，但尚未提供对应原图时明确报未绑定，不伪装为无提示。原图、节点和 prefab 没有复制、裁剪或修改；本地状态条件及正式 Home 宿主仍待接线。

隔离 Unity URP Play Mode 已实际加载三张原图的 36 个原节点，检查全部节点及管理引用类型、原图归属、编号、双向端口引用及表／资源的独立类型。运行后继续通过原执行器检查及原三页窗口、通话事务和模拟点击回归，运行截图已查看。静态检查（90,193 个 GUID）、主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过；未把目录导入视作原提示图已接入本地状态或正式入口。

### 原提示图执行器（局部）

从所选 `884_156692` 官方客户端直接迁入 `TipDotNodeManager`、`TipDotNodeDataRepository`、`TipDotGraphHandler`、`TipDotSystemController`、`TipDotSystem` 及屏蔽策略管理器，复用已迁移的原节点工厂、条件节点和序列化图。DI、玩家／配置／资源查询与事件订阅改为明确的宿主端口；动态连接的逆序处理、沿父节点检查环路并撤回连接、图实例身份、监听时立即回传状态、初始化／清理事件顺序保留。原异步状态机恢复为 `async UniTaskVoid`，仍以 `DelayFrame(1, (PlayerLoopTiming)8)` 更新；先复制并清空脏集合，再逐个处理，处理中再次变脏的节点留到后续帧。宿主销毁额外结束该宿主的更新循环。

源码输入、适配后输出、既有图／节点依赖与工具哈希见[提示图执行器来源](../../assets/reference/partner-tipdot-runtime-source-manifest.json)，入口为 `prepare_partner_tipdot_runtime.py`。本批未修改原美术、prefab 或素材快照。执行器的配置／本地状态端口尚未接入正式 Home 窗口，不能据此认定正式名册、通话、星神页的提示图已全部运行。

隔离 Unity Play Mode 使用验证图覆盖真正的原工厂／执行器：等值 NPC 身份查找、两位 NPC 的实例隔离及分别销毁、未显示时延后脏条件、UI 监听激活子图、立即回传与后续聚合通知、重复变脏合并、处理中再次变脏跨帧保留、环路撤回、双向断连／重连、父节点排序、资源缓存、屏蔽策略短路、作用域切换、重复显示设置通知及换角色时的节点／订阅释放。验证图只提供测试拓扑和条件，不代替原正式图或玩法配置。同场回归原三页窗口的模拟点击、实际通话打开事务、原动画及关闭／重开，运行截图已查看；验证工程补齐赠礼模板通过资源引用的共享窗口脚本依赖和 Tilemap 包，重跑通过且无模板缺脚本警告。静态检查（90,148 个 GUID）、主工程导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过；整页本地提示图绑定和 Windows Player 尚未完成。

### 当前主角资料与原通话头像整合（局部）

`OfflinePartnerPlayerInfoBindings` 将 `PlayerIconMe`／`PlayerInfoUI` 的主角、管理器更新通知和 `PlayerInfoData` 接到已提交的本地资料源；名称参数保留原翻译标记，战力参数使用原 `CultureNumberArg.Compact`。图片、职业／阶位配置、伙伴模型、队伍规则和详情弹窗继续由原宿主服务负责，不在资料适配器中生成替代规则或界面。

原通话模板中的 `PlayerIconMe`、`PlayerInfoData` 已分别与所选 `884_156692` 原码核对为逐文件字节相同，复用其既有迁移，不将整个历史 UI 目录混标为当前版本。当前 `PlayerInfoUI`、原血条、本地主角状态和原生头像依赖及本批输出哈希见[实际主角控件来源](../../assets/reference/partner-local-player-widgets-source-manifest.json)，入口为 `record_partner_local_player_widgets.py`。

隔离 Unity Play Mode 已实际实例化原通话头像整棵控件，绑定真实隔离角色和存档；通过原 `EventSystem` 射线与模拟按下／抬起／点击验证资料回调及原公共点击事件 `3772241779` 的当前伙伴 PCM 播放。改名不作为翻译键、男性转女性／职业变更、关闭期间取消订阅、重新启用刷新、真实存档重开与 37.5% 血条恢复、销毁后 High 纹理释放均通过，运行头像截图已查看，两种性别的原遮罩与染色显示正常。此处资料弹窗仅验证原回调参数，没有渲染完整资料弹窗；该模板未绑定的等级／战力文字不计作可见展示。正式整页和 Windows Player 验证仍未完成。

本批静态检查通过（90,064 个 GUID），主工程 Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。共享工作区检查期间出现的战报命名空间、小地图验证程序集引用编译问题已在最新共享文件中修复，重跑成功。

### 当前主角头像摄影棚与原生拍摄（局部）

`prepare_partner_player_portraits.py` 从所选 `884_156692` 的实际头像 bundle 恢复原 `CharacterPortraitStudio` 的 24 个对象，保留五个 GameObject 的层级、变换、原摄影机、Animator、SortingGroup 及组件引用。原 `HeadCharacterPortraitBuilder`、外观排除复制和空 `PreviewTool` 迁入工程；显式依赖替代注入，拍摄仍由原 `CharacterPortraitPhotographer` 执行。摄影棚中的动画与绘制组件接到既有免费原生角色后端，不读取或复制动画 SDK 实现。

当前原 `PlayerPortraitSettings` 与 `AppearanceConfig` 从实际原对象字段恢复，保留全部列表、枚举、哈希权重、裁切、尺寸和排除顺序，避免转换器将枚举列表写成标量。三张默认／加载／禁用图片保留原 Sprite 网格和 Windows 纹理依赖，已有依赖仅在同 GUID、同载荷时复用。原棚 `zSpacing=-0.0002` 由原生网格按源绘制顺序接入，现有零间距入口保持其配置。显式资源入口为 `Resources/Partners/OfficialPartnerPlayerPortraitResources`，代码、原包／CAB／对象、配置字段、绑定与逐文件哈希见[主角头像来源](../../assets/reference/partner-player-portraits-source-manifest.json)。本批未纳入完整素材快照。

`OfflineProfessionPortraitStudio` 接收实际玩家外观、原材质管理器、部件池和部件配置服务，在原棚激活前完成绑定；发型遮罩持有该角色的实例端口。拍摄使用原消息行／通讯录的 `home/idle_01`、当前男女角色数据、原 Low／染色纹理、原摄影参数与拍后释放路径。本入口只取同步零时刻照片，不声明持续动画、物理运动、全衣柜特效或独立部件已完整支持。头像管理器的排队和缓存接入见下文，正式 Home／消息行绑定仍待接通。

已在实际 Play Mode 中用原摄影机完成男女角色、职业 1／10 的八次拍摄：四组头像均为原 `100×120`、有透明边缘与角色有效像素，查看实际成图确认发型、脸部和服装正常。逐源槽位核对非零深度不改变 XY、裁切三角形、UV、PMA 颜色及图集页；外观排除不修改请求方数据；释放后重拍像素完全一致，经过实际帧与回收后原 Low 纹理持有数归零。四种原头像尺寸及参数已加载核对，实际成图仅覆盖 Head；根工程导入／编译、ProjectTools.Validate 与静态检查（87,656 个 GUID）均通过。本验证没有模拟正式页面点击、缓存恢复或 Windows Player，不将这些范围记为完成。

`OfflineProfessionPortraitSession` 将原 `CharacterPortraitManager` 的队列、原皮肤哈希表、原始像素文件持久化和三日过期检查接到实际摄影棚，按本地实例身份隔离缓存目录。独立元数据保留原版本号 `3`，不写入或清理玩法存档。离线衣柜没有持久服务器 `ModifyTimeStamp`，头像端口以全部外观字段、性别和职业的规范内容摘要提供稳定缓存版本；不改请求方协议对象，后续皮肤哈希异或仍走原方法。离线会话关闭先按原路径取消任务，再释放自有头像纹理、空闲纹理池与摄影棚根对象；原 `Dispose` 方法保留。

实际 Play Mode 已用男女角色、职业 1／10 四组当前外观验证原队列拍摄、加载图过渡、内存缓存复用、原始像素文件保存；重新构建外观对象后，虽瞬时时间戳变化，原管理器仍在首次 `Update` 前从磁盘恢复与直接拍摄逐字节一致的像素。染色与装备外观变化会改变离线版本，取消队列不拍摄、不写文件；关闭会话后拍摄／恢复纹理均实际销毁。根工程导入／编译、`ProjectTools.Validate` 及静态检查（87,661 个 GUID）通过。正式 Home／消息行仍未绑定这一会话，未将本验证计为页面点击或 Windows Player 验证。

### 当前主角血量与头像血条（局部）

`PlayerHealthRules` 迁移当前 `884_156692` 的 `Player.HpPercent`、`CanAutoAddHpInFight`、`FormulaHandler.GetHangUpHpAddSpeed` 与 `TimeManager.CalcElapsedSecond`：地图中或任一当前战斗的原 `UsePlayerHp` 为真时读取保存值；其他情况按非战斗属性的原定点换算、恢复速度及非负经过秒数恢复，最多为 1。`OfflinePlayerHealth` 读取角色属性流水线，保存原血量比例及 UTC 更新时间；设置值与时间一起落盘后才发布事件，写盘失败不发布。`OfflinePartnerPlayerHpBindings` 将同一状态提供给原 `TeamPlayerIconMeHpSliderNormal`。

新角色及无血量字段的旧存档按下文确认方案初始化；已有零血量不补满。实际地图数量、当前战斗配置与使用玩家血量的战斗进出同步仍须由正式宿主绑定，不能用历次解锁／探索的地图集合代替当前地图。来源见[主角血量来源](../../assets/reference/partner-player-health-source-manifest.json)。

隔离 Unity Play Mode 已验证新档／缺字段旧档满血、零血量保留、原定点恢复与真实角色非战斗属性、地图／混合战斗分支、时间回退／上限、刷新时间落盘，以及真实写盘失败不改文件／不发通知、重试和无效值拒绝。原 `NpcPanel` 的 `HpGo` 默认隐藏，生产资源保留该标志；独立控件画面检查仅在测试中启用原区域，保留原坐标和字段，核对保存值、地图内事件更新、地图外逐帧恢复及启停生命周期。已查看原血条 37% 和 80% 的实际 GPU 截图。本检查没有初始化完整头像或伙伴窗口；正式入口、战斗同步与 Windows Player 仍未完成。根工程导入／编译及 `ProjectTools.Validate`、全工程静态检查通过。

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

### 原天赋提示条件（局部）

`PartnerTalentRules` 从所选 `884_156692` 游戏自有代码迁入天赋图节点／双向连线构造、邻接普通节点的桥点遍历、节点条件与物品消耗检查、原四种节点状态，以及礼物经验转点计算。解析复用现有伙伴配置目录的三位星神、252 个节点及对应条件、物品品质和设置；先建全部节点，再按原顺序建边。每位星神原图为 84 个节点、总成本 132 点。

原条件分别检查好感等级达到 50，以及达到该伙伴原配置的最高好感等级；转换采用品质经验表及原 `9600` 比例，叠加该 NPC 的已有天赋经验，保留商／余数、数量 `long` 转 `int` 和负经验归零分支。树的剩余需求按未激活节点成本减去现有点数，最低为零；全部满足后停止显示转换提示。普通伙伴没有天赋配置时沿用原功能受限分支。此处不使用当前阶位的好感上限替代原最高等级。

`OfflinePartnerTalentPlayer` 将玩家、库存、好感及既有 `NpcTalentTrees` 的激活节点和经验绑定到当前 `Save.Player`；事务／读档替换对象后每次查询继续读取最新状态，不缓存另一份库存或天赋数据。未创建的树读为零经验、空节点，读取不写存档；返回的节点列表与存档分离，查询方不能借其修改存档。礼物列表只取原 `ItemType.NpcGift=328`，排除信物和天赋点物品，沿用原转换入口的分类。本批不新增存档格式、激活／转换事务或替换原窗口；整页提示服务与正式宿主接线仍待完成。计算和只读接线分别见[天赋计算来源](../../assets/reference/partner-talent-source-manifest.json)、[本地玩家读取来源](../../assets/reference/partner-talent-player-source-manifest.json)。

隔离 Unity Play Mode 已验证全部 252 个节点的原图连线、相邻节点解锁、精确点数及少一点的消耗门槛；三位星神的 49／50 与 99／100 好感边界、未结识限制、礼物各品质的精确兑换门槛、NPC 独立经验余数、数量强转溢出及全部节点完成分支通过。当前表没有桥点，另以隔离图状态验证原桥点遍历与显示分支，未改生产资源。原整页三页模拟点击、关闭重开、通话展开／收藏回归通过，已查看八张实际运行截图；三张原提示图导入及执行器回归通过。静态检查（90,264 个 GUID）、主工程 Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。本批没有验证正式 Home 的天赋／提示图接线或 Windows Player。验证临时产物的清理命令被执行策略拒绝，仍保留在本会话的 `output/validation` 目录，未绕过拒绝或清理其他会话。

实际 Play Mode 另以隔离角色存档验证天赋只读接线：空树读取不改存档，真实事务替换对象后读取新好感、点数、经验和节点；重新读档及新建会话恢复一致，后续事务与另一会话显式读档后不残留旧值。返回节点数组的修改不影响存档，三类物品按原类型正确分开，原公式实际消费这些保存字段。原整页模拟点击与提示图／252 节点回归通过，已查看本轮八张实际运行截图；静态检查（90,324 个 GUID）、主工程导入／编译及 `ProjectTools.Validate` 通过。正式 Home 提示接线与 Windows Player 仍未验证；本会话清理限制同上。

### 原伙伴档案与领取事务（局部）

`PartnerProfileRules` 迁入所选 `884_156692` 的 `NpcProfileInfoParser.PostParse/GetNpcProfileInfos`、原好感条件、反向判断与解锁说明键。保留全部 385 条配置及输入章节顺序；原 UI 使用三章／五章选择器、优先首个可领取章节、长列表延迟一帧滚动，以及锁定／可领取／正文、长短封面和首项奖励图标状态。`NpcDetailPanelProfileContent`、章节标签与游戏自有 `ScrollRectUtil` 从原代码迁入，只替换配置、当前玩家、图标及领取请求端口。

`OfflinePartnerProfileService` 按下文确认方案，在点击时重读当前角色存档，核对角色创建、伙伴已结识、原章节条件与领取记录；奖励物品通过现有库存事务加入，章节记录同笔原子保存。写盘或访问失败返回失败，不通知成功、不显示正文；重复请求不写盘、不再次发奖。好感与章节记录始终读取最新 `Save.Player`，返回的章节数组不能修改存档。旧存档缺字段时按原 `PlayerNpcReceivedProfileRewardsWrap` 的空集合默认值初始化，保存验证拒绝重复、非正章节号。暂不开放联动仍拒绝领取。

原档案子树仅解除父引用，保留原层级、坐标、图像、字体、材质、动画和按钮。开放伙伴范围的 369 条原文字与 121 个配置奖励图像，经所选 APK／实际云端包、对象及哈希校验后恢复为 Windows 原生资源；`Resources/Partners/OfficialPartnerProfileAssetCatalog` 显式绑定原区域、既有原 `ItemIconDefault`、字体／附加标记目录及奖励图像。章节标题 `{0}` 沿用原语言服务返回未收录键的分支，不编造词条。代码与事务见[档案接线凭据](../../assets/reference/partner-profile-source-manifest.json)，原生子树见[恢复凭据](../../assets/reference/partner-profile-native-source-manifest.json)，文字／奖励图像见[资源凭据](../../assets/reference/partner-profile-assets-source-manifest.json)；未生成完整素材快照。

隔离 Unity Play Mode 已通过全部 385 条章节的顺序、原好感边界与说明键，以及实际奖励／章节同笔保存、未结识拒绝、重复请求不写盘、文件占用失败重试、读档恢复、库存溢出整体回滚与旧存档空记录默认值。原生档案区域通过 8 次模拟指针点击及实际滚动，覆盖三章／五章选择器、锁定／可领取／正文、失败后重试、重开与读档；六幅运行画面已查看，原 `GotObj` 遮罩与领取勾选标记、正文、奖励及末章滚动显示正确，图标回收无残留，运行没有 Unity 错误。主工程导入／编译及 `ProjectTools.Validate` 已通过；完整正式详情宿主、生产提示系统与 Windows Player 仍待接入和验证。临时产物沿用本会话清理限制。

### 提示节点的当前玩家读取（局部）

`OfflinePartnerTipDotPlayer` 将已迁入原提示节点的玩家端口绑定当前 `Save.Player`：读取阶位、实际库存、已结识伙伴、好感与档案领取集合，天赋节点复用既有只读接线。管理器始终跟随事务替换及读档后的对象，返回的伙伴、章节集合和节点数组不能修改存档；旧存档的有效好感经验沿用赠礼适配的既有恢复规则。

窗口拥有者在本地事务完成或显式读档后调用同步；保留原新增／移除伙伴的类别与自动／手动相遇类型、物品类型及变化前数量、好感旧／新等级、领取伙伴与章节参数。通知前已发布完整当前状态；相同状态、重复领取与写盘失败不重复发出成功通知。初次读取伙伴的历史章节随新增状态出现，不伪造领取回执。原物品枚举通过所属程序集显式引用，物品类型由拥有者的实际解析器读取，覆盖静态表之外的既有技能碎片定义。来源、保存读取与通知边界见[提示玩家来源](../../assets/reference/partner-tip-player-source-manifest.json)。这部分没有新增玩法规则或存档字段，生产配置服务、条件订阅与正式窗口的提示生命周期仍未接通。

隔离 Unity Play Mode 已通过真实事务替换、原通知参数、重入同步不重复、初始历史不重复通知、领取与奖励同笔保存后的通知、文件占用失败／重试、另一会话保存后显式读档、移除与重新打开存档，以及实际生成技能碎片的库存通知。原生档案的 8 次模拟点击／滚动及原整页、提示图执行器、385 章节与 252 天赋节点回归通过，已查看本轮实际运行画面；静态检查（90,687 个 GUID）、主工程导入／编译及 `ProjectTools.Validate` 通过。正式窗口提示接线和 Windows Player 仍未验证。临时产物沿用本会话清理限制。

### 原条件订阅与存档事件（局部）

`PartnerConditionHandler` 迁入所选 `Common.ConditionHandler` 的订阅引用计数、排除类型、回调快照及回调期间的增删暂存；`ConditionInfoParser.Expand/Distinct` 保留复合条件递归展开、按原 `ConditionType` 稳定去重，以及执行结束后先添加再移除的顺序。单个回调异常通过显式日志端口报告后继续执行后续回调。条件值仍由既有相遇、档案和天赋规则计算，订阅描述不另算条件。

`PartnerConditionSubscriptionCatalog` 按所选原条件类的 `Type` 声明映射当前名册、385 个档案章节、252 个天赋节点及相关功能条件的订阅结构，共 12 种类型、80 个引用条件；天赋树解锁、赠礼转换及节点的整数条件 ID 先查原条件表再解析。`OfflinePartnerTipDotConditions` 在每个拥有者生命期内订阅已落盘的物品、伙伴新增与好感通知，调用原事件方法，关闭时先清除回调再解除订阅。好感通知只刷新原方法列出的好感等级／等级数量类型，没有额外编造满级或前置天赋通知。其他等级、阶位、地图事件方法已迁入，其正式事件来源仍待所属窗口接入；当前伙伴条件闭包不含服务器小时条件，未增加本地计时器。

重建入口为 `scripts/prepare_partner_condition_handler.py`，原方法、条件类型和本地适配哈希见[条件订阅来源](../../assets/reference/partner-condition-handler-source-manifest.json)。实际 Unity Play Mode 已验证重复订阅／逐次解除、复合去重、回调期间增删与自我重新订阅、异常后的后续回调、真实存档提交后的完整事件顺序、档案奖励保存失败／重试、相同读档无通知，以及关闭／重新创建拥有者后的订阅次数。原生整页、档案的 8 次模拟点击／滚动、385 章节、252 天赋节点和提示图执行器回归通过，已查看实际 GPU 画面；静态检查（90,770 个 GUID）通过，最终条件闭包的主工程导入／编译及 `ProjectTools.Validate` 通过。正式提示配置服务、完整窗口拥有者和 Windows Player 仍未完成；本节不作为伙伴功能完成结论，临时产物沿用前述清理限制。

### 提示节点的配置服务（局部）

`OfflinePartnerTipDotServices` 将原提示节点消费的配置、相遇状态和条件订阅接到当前存档与已迁入规则。档案复用原章节顺序与好感条件，重复查询返回稳定的章节列表／对象，保留原提示列表按引用去重的行为；正式可见查询遵守已确认的关闭伙伴范围。NPC 查询保留原偏好数据；原 `GetNpcs(ignoreType)` 实际排除地图 Boss 而包含星间之神，配置服务保留此筛选及输入顺序。

天赋配置复用原三棵树与 252 个节点，原节点条件携带各自 NPC／节点上下文，节点费用、已激活状态、桥接点、树解锁及礼物转换仍由 `PartnerTalentRules` 计算；好感阶位上限、经验换算及礼物偏好加成继续调用原解析器。相遇状态复用当前伙伴规则的本地成长服务；条件订阅复用上节原调度。配置对象保持稳定，玩家记录随事务或读档替换时读取新状态。服务拥有者最后建立订阅并在释放时解除，不新增存档字段或通话触发时机。

输入、代码边界及验证入口见[提示配置服务来源](../../assets/reference/partner-tip-services-source-manifest.json)，记录入口为 `scripts/record_partner_tip_services.py`。成就键查询保留 NPC ID／普通或星神类型参数；Home 伙伴名册现由 `OfflinePartnerTipDotRuntime` 提供原图执行器、上下文及本地成就读取／刷新。档案页领取完成后的提示刷新尚未接入这个 Home 生命周期。隔离工程实际 Play Mode 已核对全部 252 个天赋节点的原上下文／费用／状态，并加载原 `NpcProfileItem` 提示图，经原 `TipDotSystemController` 界面监听激活，验证好感达标刷新、原功能 65 限制、领取写失败／重试／奖励与凭据同存、读档重建不重复提示、监听与条件订阅释放。原完整伙伴三标签窗口及档案页 8 次模拟指针点击／滚动回归通过，实际查看本轮运行截图；静态检查通过（90778 GUID），主工程 Unity 2022.3.57f1c2 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。本节仍为局部接线，不作为伙伴功能或发布完成结论；临时产物沿用前述清理限制。

### 当前主页伙伴提示图绑定（局部）

主页拥有者在角色页面生命周期内创建已迁移的 `TipDotNodeManager`、`TipDotSystem` 与 `TipDotSystemController`，按 Resources 目录绑定原伙伴图。官方 `NpcPanelNpcItem` 交给本地伙伴 ID 后，Linker 使用原 `NpcNodeInstanceInfo` 和 prefab 节点选择器注册图实例；状态由原聚合节点和原 `TipDotNodeLinker` 更新。普通／星神好感成就键映射到各自原配置组，节点读取本地成就、档案及天赋条件服务。关闭 Home 时先释放原节点，再解除条件和玩家事件订阅。

打开名册时先重读存档并刷新原好感成就可领索引，再生成原生名册项；名册期间本地相遇、赠礼库存或好感提交事件会同步刷新图条件，不模拟服务器推送。档案奖励提交后的提示刷新依赖正式档案页宿主接线。伙伴页面图中的原面板节点现已注入本地端口：通话红点复用原 `PartnerDialogueListRules.HasRedDot` 和本地解锁条件，NPC 角色类别读取所选原配置；尚未迁移的家具／宠物状态显式为空，不报告可领取状态。该端口不承接其他职业提示规则。入口为 `OfflinePartnerTipDotRuntime`、`PartnerPanelView`、`OfflinePartnerConversationUiSession`、`HomeFeatureRuntime` 与 `HomeHudView.Partners`。隔离 Unity Play Mode 已检查实际官方原生卡片截图，红点显示在绯乐头像右上角且使用原 `ImgRedDot`；并以模拟指针完整跑过伙伴、通话、星神页切换、关闭、重开和功能限制流程，错误计数为零。正式 Home 对话宿主接入后的红点更新、档案奖励后提示刷新及 Home 销毁释放仍待验证；本节不能作为完整提示功能或 Windows Player 已完成的依据。

### 伙伴馈赠成就与本地领取（局部）

`PartnerFriendshipAchievementRules` 从所选原代码迁入普通／星神好感任务、键相等与哈希、组／索引编码、领取记录默认值、可领索引更新、顺序领取、数量截断及原返回码／事件顺序。当前两类共 99 组、1980 条独立配置，按原始行号排序后使用原零起始索引；领取凭据是原编码 ID，不能使用原配置行号代替。星间之神同时保留普通伙伴组与独立星神组。配置参数复用工程已有 Newtonsoft 依赖，不改奖励、门槛或原任务先检查好感再检查领取记录的顺序。

`OfflinePartnerAchievementService` 提供按下文确认方案执行的刷新／领取事务端口。每次先重读当前角色存档，使用原任务刷新可领索引；点击领取按原顺序计算配置奖励，普通请求处理指定组，同类型请求只处理已结识、已开放且属于请求原类型的组。奖励、可领索引和领取索引在同一角色克隆中保存，成功后才按原顺序发布通知；写盘失败、库存溢出、未结识、关闭联动或没有可领项不会发奖。旧档缺字段时保留空记录，不推测历史领取；记录的组／索引编码边界、重复组及所选配置范围分别由存档和服务核对，读取的玩家记录不能反向修改存档。

原方法、配置、适配与工具哈希见[馈赠规则与事务来源](../../assets/reference/partner-achievement-rules-source-manifest.json)，重建与验证入口为 `scripts/prepare_partner_achievements.py`、`scripts/prepare_partner_achievement_validation.py`。实际隔离 Unity Play Mode 已通过全部 1980 项门槛／奖励、99 组键与原编码、顺序及数量分支、真实文件占用失败／重试、原奖励库存溢出整体回滚、普通与星神组隔离、同类型请求、实际读档及旧档空字段。原整页、档案 8 次模拟指针点击／滚动、385 章节、252 天赋节点及提示执行器回归通过，已查看本轮实际 GPU 截图；静态检查通过（90785 GUID）。主工程 Unity 2022.3.57f1c2 导入／编译及 `ProjectTools.Validate` 通过，正式界面的刷新触发、馈赠领取／提示绑定及 Windows Player 仍未完成；本节不作为伙伴功能或发布完成结论，临时产物沿用前述清理限制。

### 原生馈赠页与领取交互（局部）

`prepare_partner_achievement_ui.py` 从当前原 `AchievementPanel`、`AchievementPanelBase`、`AchievementPanelItem`、`AchievementUtils` 和好感内容处理器迁入伙伴专用控制器，保留原键切换、索引检索范围、已领取／进行中筛选、完成项优先排序、五行绑定、进度参数顺序、领取整组请求及成功回调。普通伙伴内容使用原 NPC 名称参数，星神使用原通用内容键；两类好感任务均不使用进度黑板键。现有职业专用控件未改动，不再将伙伴请求误接到职业组。

`prepare_partner_achievement_native.py` 通过原 `NpcDetailPanel.mAchievementPanel` 的实际引用选择完整子树，只分离根节点父引用，校验内部引用闭合；原层级、锚点、尺寸、标题、五个奖励槽、按钮、状态切换、动画、音效、字体、遮罩和材质继续使用官方输入。`prepare_partner_achievement_assets.py` 恢复两类配置涉及的八个原奖励图像、中文文本、显式 Resources 目录及 `OfficialPartnerAchievementAssetCatalog`；先核对 APK，再使用所选版本官方缓存和已有转换缓存。共享按钮控制器的原空名称序列化差异已登记并保留既有文件，不覆盖共享资源。输入版本、对象、绑定、原始与输出哈希见[原生馈赠页来源](../../assets/reference/partner-achievement-ui-source-manifest.json)。

`OfflinePartnerAchievementUiServices` 将原控件接到前节本地成就服务，保留原显示数量及同类型显示配置；功能限制、图标与消息由窗口宿主显式提供。实际领取仍重读存档，奖励与索引成功保存后才执行原成功回调，失败保持可领取状态。普通与星神键分别选择自己的组；星神伙伴同时拥有的普通组不随星神组领取而被消费。

`prepare_partner_achievement_ui_validation.py` 在已有隔离原窗口工程内完成 Play Mode：原生五行、原名称／进度／奖励图像、可领排序、三次实际模拟鼠标点击、文件占用失败后重试、整组两档领取、读档防重、功能限制分支、普通／星神独立领取和停用回收均通过。已查看未达标、待领取、领取后真实 GPU 截图；原整页与档案八次点击／滚动及规则、事务、提示回归通过。静态检查通过（90989 GUID）；主工程 Unity 2022.3.57f1c2 导入／编译及 `ProjectTools.Validate` 通过。Home 伙伴服务初始化（读档后）、打开伙伴名册、伙伴结识／好感变化及赠礼库存变化都会触发本地成就索引刷新；这只接通可领取状态刷新，不代表详情页内的成就页签、正式提示节点显示和领取交互已经接入。正式详情宿主、助战邀请及 Windows Player 仍未完成，本节不作整体完成或发布结论。临时产物沿用前述清理限制。

### 详情原生对白与尺寸控制（局部）

`prepare_partner_detail_talk.py` 从所选原 `NpcTalkUI`、`NpcCustomSizeController` 迁入完整控制器。对白复用已迁入的原 `TMP_TypeWriterEffect`，保留设置文本、显示状态、入场状态、逐字播放、正常结束后等待配置时长、结束状态及停用时取消等待的执行顺序；替换对白重新开始打字，不触发被中断对白的延迟关闭。尺寸控制沿用原 NPC 字典查询、默认配置、偏移与缩放及空目标分支。这里只迁入客户端表现，不增加触摸好感或修改赠礼回调。

原 `NpcDetailPanel.mDetailContent.mNpcTalkUI` 对应完整子树仅分离根父引用；气泡图像、字体、布局、状态引用、Legacy Animation 与三段入场／循环／退场曲线保留原数据。原 Legacy clip 直接保存未压缩路径，转换为 YAML 空值的根路径逐项核对 APK 实际空字符串后恢复，不套用 Animator 编译路径哈希。`OfficialPartnerDetailTalkAssetCatalog` 显式绑定 prefab 与当前 `npc_talk` 表的 62 条原文，键形式来自原详情控制器。来源、复用依赖、转换工具和输入输出哈希见[详情对白来源](../../assets/reference/partner-detail-talk-source-manifest.json)；本批不生成完整素材快照。

`prepare_partner_detail_talk_validation.py` 在隔离原窗口 Play Mode 中加载原子树，验证三段 Legacy 动画绑定、逐字文本推进、替换对白、配置延迟关闭、停用取消与重开，并检查原尺寸字典的配置／默认／空目标分支；实际 GPU 截图核对入场文字、完整文字和退场空画面。原成就页领取、档案及提示服务窗口回归通过，Unity 项目静态检查为 91,019 GUID，主工程 `ProjectTools.Validate` 返回码 0。该验证用 API 调用对白组件，未验证角色触摸按钮或完整详情路由；正式详情控制器、立绘／角色模型仍待迁移，Windows Player 未发布。

## 自定义实现

用户已确认：自定义伙伴称谓通过原字库与长度校验后直接在本地保存，不等待线上文本审核；保留原伙伴资格及好感等级门槛。自动伙伴在创建角色及读档后检查原相遇条件，满足且尚未拥有时自动结识，初始好感使用原配置；此触发时机是确认的单机方案，不声称取得服务器实现。

服务端通话创建／最终确认方法未取得。用户补证已完成前段并满足后续条件时可当天续接，超出好感最高值经原浪费确认后仍累计；本地按此保存，不反推旧结识日期。

用户进一步确认：仅在打开伙伴界面时，按原 NPC 通话顺序检查尚未开始且满足原解锁／连锁条件及每日限制的通话；建角、读档、好感或其他条件变化时不额外创建。每日计数按原重置规则和用户确认的 UTC+8 检查。确认合法后继节点时，将该节点的配置物品／好感奖励和历史合并为同一保存事务，回看不重复领奖。`OfflinePartnerConversationOpenRefresher` 从原伙伴条件、通话和地图配置创建本地事务；Home 打开伙伴入口时会调用，失败时提示并保留名册访问。真实存档 Play Mode 覆盖首次创建、重复打开、原始完整通话、奖励回滚／重试和每日限制。正式通话地址簿／播放窗口及完整 `NpcPanel` 宿主仍未接线。

用户已确认：档案点击解锁时重读存档，按已结识及原章节好感条件核对，配置奖励与章节领取记录同笔保存；失败不解锁，重试与回看不重复领奖。未取得服务器领取实现，该本地事务是确认的单机方案。

送礼扣库存、好感、档案／通话奖励与历史共用本地事务，失败回滚；相遇仅由当前格式的伙伴记录与实际相遇流程决定。

头像缓存的本地版本元数据、稳定外观版本及会话资源释放属于离线运行适配，规则与范围见上文“当前主角头像摄影棚与原生拍摄”；不改变玩法存档、好感或伙伴资格。

用户已确认：增加本地玩家血量及更新时间；新角色及缺字段的旧存档以满血初始化，其后沿用原回血公式、地图／战斗分支，并与使用玩家血量的本地战斗同步。该初始状态是单机方案，不声称取得服务器初始化实现。当前接线及验证范围见上文“当前主角血量与头像血条”。

用户已确认：伙伴普通／星神馈赠在读档及结识／好感变化后按原条件刷新可领索引；领取时重读存档，保留原顺序和界面请求范围，将配置奖励与领取索引同笔保存，失败不发奖、重试不重复。原共享代码提供领取计算，但未取得可领索引更新的服务器调用方及领取服务，此处的触发与落盘属于确认的单机方案。

## 尚未实现

- 完整 `NpcPanel` 宿主的对话／星神页、伙伴档案与餐桌通话；当前 Home 已接通原生名册卡片、本地相遇／好感／置顶、详情属性区、赠礼控制器和好感说明弹窗。
- 正式窗口中的立绘／小模型、完整详情简报、伙伴位置导航、赠礼物品详情及声音仍未全部接通。
- 每日挑战与每日副本已使用官方助战邀请弹窗：按原 `FightAssistNpcClassIds` 名册生成伙伴候选，结合本地结识／好感、原助战条件、战斗模型可用性和次数上限过滤；选择及最大人数写入 `OfflineNpcAssistanceProgress`，确认后进入对应战斗。入口位于 `HomeHudView.DailyChallenges`，弹窗为 `OfficialBattleAssistSelectionView`。目前没有证据表明主线等其他战斗入口已接入相同邀请流程，也未完成主角加伙伴队伍在所有相关战斗中的运行验证。
- 主工程 Play Mode 已通过名册卡片和赠礼按钮的模拟射线点击、赠送落盘及读档恢复，原生区域在 450×800 下显示，初始化遮罩、礼物空图标和属性键名问题已修复。完整页面交互、手动结识的正式宿主流程、声音与 Windows Player 仍未完成。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。
- 史莱姆、孤独摇滚的八位伙伴及全部相关入口按[离线政策](offline-access.md#暂不开放的联动活动及伙伴)暂停，不列为自动开放目标。

## 代码与资源入口

[NpcDialoguePanel](../../.tools/source-packages/official-code/client-source/NpcDialoguePanel.cs)、

[`NpcPanelNpcItem.Refresh`](../../.tools/source-packages/official-code/client-source/NpcPanelNpcItem.cs)、[`NpcDetailPanelDetailContent.Refresh`](../../.tools/source-packages/official-code/client-source/NpcDetailPanelDetailContent.cs)

[`NpcAdventureModelItem.InitContent`](../../.tools/source-packages/official-code/client-source/NpcAdventureModelItem.cs)、[伙伴图像来源清单](../../assets/reference/partner-ui/source-manifest.json)、[档案来源清单](../../assets/reference/npc-cultivation/source-manifest.json)、[对话来源清单](../../assets/reference/partner-interactions/source-manifest.json)

[羁绊助战模型来源清单](../../assets/reference/npc-adventure-models/source-manifest.json)、
