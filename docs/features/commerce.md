# 商城、代金券与礼包

[总索引](../README.md)

状态：未完成。官方目录、资源载荷和部分离线交易代码已具备；真实 HUD 入口、资源目录资产、完整界面与交互尚未接通，商城 Play Mode 游玩未验收。

## 功能简介

范围为游戏 HUD／功能菜单进入商城，商城分类导航、耀星直购、代金券、每日礼包、每周礼包、首购、耀星兑换、公共购买确认、规则与价格表、奖励详情、提示与存档。月度奖励、七日祈愿及星眷累计奖励的完整规则和缺项统一维护在[月卡与星眷](monthly-card.md)，本页记录商城中的入口与共享依赖。家园商店等独立商店见所属功能页，不因商城目录存在而视为已完成。

固定官方来源为 manifest `854_156383`：客户端反编译结果在 [client-source](../../.tools/source-packages/official-code/client-source)，共享规则在 [common-source](../../.tools/source-packages/official-code/common-source)，配置在 [rules.json](../../assets/official/rules.json)、[commerce-ui.json](../../unity/Assets/Official/Commerce/commerce-ui.json) 与 [settings.json](../../.tools/source-packages/official-data/settings.json)，原 UI 层级在 [ui.json](../../assets/official/ui.json)。以下“官方功能”是恢复目标，不能据此认定 Unity 已实现。

### 官方原始功能与调用依据

| 范围 | 官方功能、规则与交互 | 官方定位 |
| --- | --- | --- |
| HUD 与功能菜单 | `ShopEntryBtn` 检查 `FunctionType.ShopActivity` 开放条件，未开放时禁用入口并显示空位，开放后恢复；点击弹出 `ShopActivityEntryPanel`。首购另有 HUD 图标，支付功能未开放时隐藏，激活且全部领奖后隐藏 | [`ShopEntryBtn`](../../.tools/source-packages/official-code/client-source/ShopEntryBtn.cs)；[`FirstPayIconEntry`](../../.tools/source-packages/official-code/client-source/FirstPayIconEntry.cs) |
| 商城框架与导航 | 原底栏切换活动、周期卡、直购，切换时更新标题与内容；`InitPayPanel(PayType)` 支持从资源不足等调用方直接进入指定支付页；关闭恢复调用方窗口 | [`ShopActivityEntryPanel.Init/InitPayPanel/OnTabItemChanged`](../../.tools/source-packages/official-code/client-source/ShopActivityEntryPanel.cs)；[`PayMoneyChangeDialog.OnConfirmBtnClick`](../../.tools/source-packages/official-code/client-source/PayMoneyChangeDialog.cs) |
| 活动分类入口 | 活动区域使用官方活动入口目录与各活动开放状态；每日／每周礼包入口及红点由对应 agent 驱动。具体活动玩法、开放或关闭范围继续以所属功能页为准 | [`ActivityEntryOverview`](../../.tools/source-packages/official-code/client-source/ActivityEntryOverview.cs)；[`ActivityEntryDailyPayGiftRedDotAgent`](../../.tools/source-packages/official-code/client-source/ActivityEntryDailyPayGiftRedDotAgent.cs)；[`ActivityEntryWeeklyPayGiftRedDotAgent`](../../.tools/source-packages/official-code/client-source/ActivityEntryWeeklyPayGiftRedDotAgent.cs) |
| 直购／代金券页签 | 按功能开放状态决定可见页签；未指定支付类型时，在代金券功能可用时默认选择代金券，否则选择直购。切换刷新商品列表、顶部说明和标题；提供代金券规则价格表 | [`PayDirectPay.OnEnable/SwitchTab/OnRuleBtnClick`](../../.tools/source-packages/official-code/client-source/PayDirectPay.cs) |
| 耀星直购 | 6 档商品按官方排序显示标题、图标、价格、固定奖励和额外奖励；每档独立记录首次购买，首购后切换复购奖励和超值比例，并响应购买记录更新 | [`PayDirectPayItem.InitShow/RefreshGreatValueAndExtraAward`](../../.tools/source-packages/official-code/client-source/PayDirectPayItem.cs)；`direct_pay`、`money_cost` 表 |
| 代金券购买 | 6 档商品显示官方标题、图标、`MoneyCostInfo.VoucherValue` 数量和价格；普通目录没有额外奖励，第三方支付上下文与外部产品价格刷新属原联网能力 | [`PayVoucherItem`](../../.tools/source-packages/official-code/client-source/PayVoucherItem.cs)；[`NormalPayVoucherItemContext`](../../.tools/source-packages/official-code/client-source/NormalPayVoucherItemContext.cs)；`pay_voucher` 表 |
| 共享支付与确认 | 按商品上下文和可用代金券决定货币／物品支付方式；确认框显示商品标题、礼包图、奖励列表、物品支付图标与数量或货币价格。奖励显示数量、底部名称并支持点击详情；购买记录更新后关闭确认框 | [`CommonPayBtn`](../../.tools/source-packages/official-code/client-source/CommonPayBtn.cs)；[`PayGiftItem`](../../.tools/source-packages/official-code/client-source/PayGiftItem.cs)；[`PayGiftConfirmDialog.Refresh/ShowAwards/RefreshPayPrices`](../../.tools/source-packages/official-code/client-source/PayGiftConfirmDialog.cs) |
| 每日礼包 | 4 组共 12 条商品，每组按 ID 排序显示 3 档；免费赠礼每日一次。单买、整组购买及已购状态互斥：任一单品已购后不能整组购买，整组已购后不能再单买；显示原价、整组价格、超值比例、剩余时间和规则 | [`DailyPayGiftDialog`](../../.tools/source-packages/official-code/client-source/DailyPayGiftDialog.cs)；[`DailyPayGiftItem`](../../.tools/source-packages/official-code/client-source/DailyPayGiftItem.cs)；[`PlayerPeriodicPayGiftDataExt.DailyPayGiftCanBuy/DailyPayGiftCanBuyAll`](../../.tools/source-packages/official-code/common-source/Common/PlayerPeriodicPayGiftDataExt.cs) |
| 每周礼包 | 85 组共 595 条目录；按当前组及条件显示商品，免费赠礼使用顶部独立入口；付费商品按售罄状态和 ID 排序，显示剩余次数、售罄遮罩、超值比例、礼包图、奖励、新标记和剩余时间。当前固定版所有商品 `Condition` 均为 `NoneCondition`，不得误写为缺少条件资源 | [`WeeklyPayGiftPanel.RefreshGifts/ResetListenConditions`](../../.tools/source-packages/official-code/client-source/WeeklyPayGiftPanel.cs)；[`WeeklyPayGiftShowContext`](../../.tools/source-packages/official-code/client-source/WeeklyPayGiftShowContext.cs)；[`PayGiftItem`](../../.tools/source-packages/official-code/client-source/PayGiftItem.cs) |
| 日／周组与周期更新 | 客户端消费服务器发来的当前组、截止时间和更新消息，刷新倒计时、商品和购买状态；官方目录中的全部组不等于可以同时购买。组选择不能从 APK 目录条数直接推定 | [`PeriodicPayGiftManager.TryGetCurrentDailyGiftInfo/TryGetCurrentWeeklyGiftInfo`](../../.tools/source-packages/official-code/common-source/PeriodicPayGiftManager.cs)；`daily_pay_gift_group`、`weekly_pay_gift_group` 表 |
| 首购激活与领奖 | 独立 `FirstPayGift` 支付入口；激活后第 1／2／3 日逐步解锁奖励，每档只领一次。`FirstPayTodayCanGetAwardIndex` 达到最大档后保持最大档，第三天后仍可补领已解锁且未领的奖励，不存在激活满三天自动失效分支 | [`FirstPayDialog.OnPayBtnClick/RefreshState`](../../.tools/source-packages/official-code/client-source/FirstPayDialog.cs)；[`PlayerFirstPayExt`](../../.tools/source-packages/official-code/common-source/Common/PlayerFirstPayExt.cs)；[`FirstPayAwardItem`](../../.tools/source-packages/official-code/client-source/FirstPayAwardItem.cs) |
| 首购奖励与预览 | 首日按职业匹配武器，其他日取相应配置奖励，再加入公共奖励；标题带价格、装备等级和品质；显示角色外观、剑／杖按钮，支持对应武器比较或详情预览，并随职业变化刷新 | [`FirstPayDialog.InitShowWeapon/GetShowAward/RefreshPreviewAppearance/DoPreviewWeapon`](../../.tools/source-packages/official-code/client-source/FirstPayDialog.cs)；[`FirstPayPreviewAppearance`](../../.tools/source-packages/official-code/client-source/FirstPayPreviewAppearance.cs)；[`FirstPayWeaponPreview`](../../.tools/source-packages/official-code/client-source/FirstPayWeaponPreview.cs) |
| 星眷入口与红点 | 直购顶部进入累计奖励，随累计值及领奖事件更新可领奖红点；15 组 75 档奖励、进度与价格表的完整目标见[月卡与星眷](monthly-card.md#官方原始功能与配置) | [`AccumulatedPayAwardEntry`](../../.tools/source-packages/official-code/client-source/AccumulatedPayAwardEntry.cs)；[`AccumulatedPayAwardHasAwardRedDotItem`](../../.tools/source-packages/official-code/client-source/AccumulatedPayAwardHasAwardRedDotItem.cs) |
| 耀星兑换 | 耀星物品 15 → 晨星物品 2（官方代码名 `DiamonId`），比例 1:1，范围 1～10,000,000，实际上限还受持有量约束。支持加减、最大值、滑条及指定数量模式，显示两侧名称与数量；不足时确认跳转直购，成功后展示奖励并关闭兑换框 | [`PayMoneyChangeDialog`](../../.tools/source-packages/official-code/client-source/PayMoneyChangeDialog.cs)；`Pay.DiamonChangeMin/Max/Scale` |
| 规则、物品详情与提示 | 每日礼包规则、代金券／累计星值价格表、品质框、物品数量与名称、点击详情、新标记、免费／可领奖红点、售罄和不可操作状态；支付、购买、背包变化应同步刷新关联界面 | [`PayInfoTableUtils`](../../.tools/source-packages/official-code/client-source/PayInfoTableUtils.cs)；[`ItemIcon`](../../.tools/source-packages/official-code/client-source/ItemIcon.cs)；[`FirstPayHasAwardRedDotItem`](../../.tools/source-packages/official-code/client-source/FirstPayHasAwardRedDotItem.cs) |
| 表现与窗口交互 | 原 prefab 层级、坐标、锚点、尺寸、滚动与布局、遮罩、排序、字体材质、开关动画、粒子和按钮音效；公共顶部栏与弹窗关闭、遮挡、返回行为按官方绑定恢复 | [商城组件来源清单](../../assets/reference/commerce-components/source-manifest.json)；[商城 UI 来源清单](../../assets/reference/commerce-ui/source-manifest.json) |

## 完整还原部分

本节仅记录已落实的数据、载荷和代码范围，尚无商城完整流程已在 Unity 运行验收通过的结论。

### 官方目录与资源载荷

[`prepare_unity_commerce_catalog.py`](../../scripts/prepare_unity_commerce_catalog.py) 已生成 [commerce-runtime.json](../../unity/Assets/Game/Resources/Commerce/commerce-runtime.json)：43 条价格配置、6 档直购、6 档代金券、4 组每日礼包／12 条商品、85 组每周礼包／595 条商品、首购 3 档、累计奖励 75 档、169 个物品、709 条文本、22 份 UI 树及 10 个音频事件记录。数量仅代表目录覆盖，不代表页面或商品流程已可用。累计奖励在本页使用公共载荷，业务说明仍以月卡页为准。

直购、代金券和礼包文本沿用官方配置及 [`LanguageUtils`](../../.tools/source-packages/official-code/common-source/Common/LanguageUtils.cs)。UI 树来自原 UI 数据，`PayDirectPayItem` 另从固定 APK 同名 prefab 恢复。已提取原 `pay`、`dailypaygift`、`weeklypaygift`、`firstpay`、`accumulatedpay` bundle；首购 `FirstPayDialog_Show/AniShow/Hide`、累计奖励 `AccumulatedPayPanel_Show` 等素材有来源记录，尚未转换接入不等于官方资源缺失。

运行素材位于 [Assets/Official/Commerce](../../unity/Assets/Official/Commerce)，来源、输入输出哈希及处理记录见 [source-manifest.json](../../unity/Assets/Official/Commerce/source-manifest.json)、[商城 UI 清单](../../assets/reference/commerce-ui/source-manifest.json)和[组件清单](../../assets/reference/commerce-components/source-manifest.json)。所核查运行目录 JSON 哈希与来源清单一致，157 个运行素材文件哈希均与记录一致；未据此认定 Unity 原生 prefab、动画、shader 和绑定已恢复。

### 当前 Unity／离线代码

- [`OfficialCommercePrefabRenderer`](../../unity/Assets/Game/Runtime/Presentation/Commerce/OfficialCommercePrefabRenderer.cs) 从 JSON 创建部分 UGUI 图片、文本、按钮、布局、滚动和滑条；尚未恢复全部官方脚本组件与状态行为。
- [`CommerceScreenController`](../../unity/Assets/Game/Runtime/Presentation/Commerce/CommerceScreenController.cs) 已写有选择已有角色、构造六类页面、商品列表、购买确认、免费领取、首购与累计领奖、兑换的代码。`Install()` 未接实际启动调用方，不得描述为已在 Bootstrap 自动加载或玩家已能进入商城。
- [`CommerceFeatureRuntime`](../../unity/Assets/Game/Runtime/Presentation/Commerce/CommerceFeatureRuntime.cs) 已写有已有角色检查、打开商城会话、报价和业务调用；无可用档位时不创建角色或默认主线档。
- [`OfflineCommerceOffers`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceSession.cs) 已写有直购首购／复购报价、代金券数量、每日整组与单品互斥、日／周计数、免费赠礼、首购职业奖励、累计档位和兑换计算。首购补领、日期刷新与主库存同步仍有下述缺陷。
- [`OfflineCommerceTransaction`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceTransaction.cs) 已实现候选状态、购买请求复核、扣券与奖励、购买计数、重复领奖拦截和保存失败时不提交内存状态；[`OfflineCommerceSession`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceSession.cs) 将商城状态嵌入角色主存档。代码存在不能代替真实写盘和跨模块存档验证。

### 已执行检查与边界

| 层次 | 实际结论 |
| --- | --- |
| 静态工程与素材 | `python -B scripts/check_unity_project.py` 通过；商城目录与上述 157 个运行素材的记录哈希核对通过。静态检查不验证编译、加载、画面或输入 |
| 交易核心内存检查 | 直接编译当前 `OfflineCommerceTransaction.cs`，验证扣券发奖与累计值、过期购买请求拒绝、重复领奖拒绝、模拟保存返回失败后内存余额／计数不变；未验证磁盘失败或 Unity 窗口 |
| 主库存同步方法检查 | 对当前 `SyncInventory` 方法做隔离检查，确认旧商城快照会覆盖主库存：数量 150 被旧值 100 覆盖，快照缺失物品由 1 清为 0。此为方法行为复现，未执行完整游戏存档流程 |
| Editor 导入／编译 | 缓存中已有商城程序集，但没有本功能当前目录资产及素材导入校验通过的结论。旧角色模块编译报错未经当前商城检查复现，不能继续列为当前编译失败依据 |
| Play Mode | 商城逻辑集成、实际鼠标／键盘／拖拽与遮挡、画面、动画、音效、退出重开及真实保存失败恢复均未验收；其他模块的运行结果不计入商城通过 |
| Windows Player | 未执行商城专项验证；未构建或发布商城成品 |

## 自定义实现

### 已确认的离线范围

外部 SDK 支付、服务器订单、退款和跨账号服务不在离线版本范围。可用代金券时保留物品支付，其余购买经本地确认后按官方配置结算，不产生真实支付。首购独立确认仅激活首购并开放其分日奖励，不在该激活操作中额外发普通直购奖励。已确认差异不授权修改首购补领、商品奖励或限购规则。

商城依附已有角色档位，货币、奖励、购买／领取计数和商城状态应在主存档同一事务保存；禁止为商城创建默认角色、覆盖其他玩法最新库存或在写盘失败后保留一半交易。

日／周商品保留官方目录、条件和限购，周期沿用已有文档确认的本地重置点及单向检查点，不新增目录或赛季选择器；旧免费购买历史不反算累计星值。当前代码在 `Open()` 以本地时间推进 `Day`，默认 `DailyGroup=1`、`WeeklyGroup=85`；这些实现值不能冒充官方服务器组选择规则，也不能证明长期打开期间的更新已实现。原版当前组和截止时间属于服务器输入；后续若需超出已确认范围选择或轮换商品组，应先依据共享规则和配置核实，仍无依据时明确询问用户，不自行补全。

### 当前临时实现

控制器手建档位选择面板、六分类导航栏和余额状态文字，并以 JSON 重建页面。临时壳层、简化奖励图标及统一确认方式不属于已完成的官方界面还原，也不构成获得了永久降级同意。应接入完整游戏的当前角色与官方导航，按实际 prefab／脚本恢复；表现替代方案仍须用户明确同意。

## 尚未实现

### HUD、真实入口与资源安装

- 接通 `Bootstrap → 当前角色游戏 HUD／功能菜单 → ShopActivityEntryPanel`，复用当前角色会话；按官方 `ShopEntryBtn` 恢复开放条件、锁定状态和点击行为。现有 `CommerceScreenController.Install()` 没有调用方，场景也没有商城组件。
- 接通首购 HUD 图标、商城／活动入口红点、免费赠礼和累计奖励红点，随开放条件、激活、领奖、购买及跨日变化刷新；全部首购奖励领完后隐藏首购入口。
- 完成商城原生资源与字体的按需导入，生成并验证 `Assets/Game/Resources/Commerce/OfficialCommerceAssetCatalog.asset`、字体、图片、音频和动态资源显式绑定。该目录资产当前不存在，而 `OfficialCommerceAssetCatalog.LoadDefault()` 必须加载它。
- 按[Android → Windows 流程](../asset-management.md#android-资源迁移到-windows)恢复所需原 prefab、脚本、材质／shader 和依赖；恢复已有 bundle，不将未接入写成素材缺失，不要求日常功能重建全量 ZIP 或正式成品。

### 商城框架、导航与公共交互

- 恢复原活动／周期卡／直购底栏、标题、选中态和父页面状态。当前 `commerce_main` 的 `DirectPay` 父节点默认关闭、`PeriodicCard` 默认开启；`SwitchDirectPayTab()` 只切换商品与顶部区，没有切换这些父页面。
- 原底栏分类、规则按钮、直购顶部累计星值入口和月卡操作尚无对应业务绑定；`BindPageButton()` 对这些按钮主要只绑定音效。六分类临时导航不能替代官方入口。
- 商城关闭须返回调用它的游戏窗口；子页返回、取消、Esc、遮罩关闭、弹窗阻挡及再次进入须按官方公共窗口组件核对。当前页面关闭统一 `ShowPage(0)`，确认框主要依赖 Esc，不能算已完成原版关闭流程。
- 恢复活动区域和日／周礼包导航，具体活动仍按所属页已确认单机范围接入；恢复月卡和星眷入口，其业务缺项见[月卡页](monthly-card.md#尚未实现)。
- 完成官方滚动、网格／自定义布局、顶部栏、文本对齐、材质效果、遮罩及按比例缩放，核对竖屏基准和窗口缩放后的真实输入命中。

### 商品、支付与奖励详情

- 耀星商品按每档购买记录切换首次／复购额外奖励、首次标记和超值百分比。当前 `PopulateDirectItems()` 始终显示 `firstExtraAward`，与后端已使用复购报价不一致；支付价格与代金券数量状态也须对应实际方式刷新。
- 代金券商品恢复正确价格、数量、无额外赠送状态和页签默认选择／可见条件；核对切换后的商品尺寸、布局与输入。
- 购买确认恢复商品标题、礼包图、奖励、支付物品图标／数量、货币价格与累计值，覆盖确认、取消、余额变化及售罄后关闭。当前通用标题没有填充完整报价，代码查找的 `BgDialog1/Content/AwardContent` 在当前确认框树中不存在，奖励填充被跳过。
- 使用官方 `ItemIcon` 及附件恢复品质框、名称、数量、计数布局、点击详情和奖励弹窗。当前 `PopulateRewardIcons()` 手建 92×92 图片及数字，缺少完整组件和详情交互；原 TMP 内联图标与字体效果同样需要恢复绑定。
- 确认时复核玩家看到的报价、支付方式、次数和周期；当前控制器确认后调用 `Offers.Purchase()` 重新构造报价，原确认报价的完整快照未贯穿请求，不能仅凭底层存在 `StaleQuote` 判定整个确认流程已覆盖。

### 每日／每周礼包与时间更新

- 每日礼包恢复免费／已领状态、3 档完整奖励与支付显示、整组可买／不可买／已买状态、整组原价／折扣和规则入口。当前有购买与领取代码，但整组按钮、代金券文案与完整表现未同步恢复。
- 每周礼包恢复顶部免费入口、付费商品剩余次数、售罄排序、超值比例、礼包图、新标记与动态状态。现有列表有免费／付费分离和部分售罄控制，但次数显示主要为配置限购总数，不能代表剩余次数。
- 接通剩余时间、当前组与购买记录变化、跨日／跨周即时刷新和重新进入刷新。日期当前只在打开存档时更新，`Update()` 只处理 Esc，长期停留不会推进领取／限购周期。
- 验证单买与整组互斥、同周期重复领取／购买拒绝、换周期恢复、时间回拨不重复领取，以及弹窗打开期间跨周期的旧报价处理。当前固定组值与组切换方案的关系必须明确，不能把全部组目录视为已开放。

### 首购

- 修复 `ClaimFirstAward()` 与 `CanClaimFirst()` 的 `FirstPurchaseDay + 3` 截止条件，恢复官方第 1／2／3 日逐步解锁且第三日后可补领的规则；“三日奖励”不能解释为“三日有效期”。
- 恢复首购标题、未激活／已激活、各日未解锁／可领／已领状态，职业变化后的奖励与预览刷新；确认激活、普通直购与首购状态变化之间的关系，不能把客户端未说明的服务器激活决策当成官方已证实行为。
- 恢复角色外观、男女模型素材、剑／杖按钮、武器比较与详情预览；首日职业匹配与第 2／3 日固定奖励须保证显示和实际发放一致。
- 接入 `FirstPayDialog_Show/AniShow/Hide`、预览动画、粒子与音效，验证关闭重开、职业变化和领完后 HUD 图标／红点消失。

### 星眷、月卡与兑换

- 星眷真实入口、红点、规则价格表、档位进度／连线、主副奖励、末组锁定项、多奖励轮播及所有未验收分支统一见[月卡页待实现清单](monthly-card.md#尚未实现)，已有累计值与领取底层不代表该页面完成。
- 月度奖励、七日祈愿激活／续期／领奖／到期／属性加成与原窗口表现统一在月卡页维护；当前商城中的周期卡 UI 子树不能充当业务已接通证明。
- 兑换恢复原数量绑定。`SetExchangeAmount()` 更新 `CountTxt`，实际树使用 `SelectedCountTxt`，两侧物品名称和兑换前后数量也未完整刷新；滑条、加减与最大值必须驱动同一显示状态。
- 恢复零余额、最小／最大／余额不足、指定数量模式、不足时跳转直购、取消返回、成功奖励与关闭。当前零余额仍将选择量夹到最小值，且成功后停留兑换页，不能认定与原行为一致。

### 存档、表现与最终验证

- 修复 `OfflineCommerceSession.Open/Persist/SyncInventory` 的旧库存覆盖：读取嵌入商城状态后未按最新主库存合并，`SyncInventory` 又全量回写并将未出现在商城快照中的主库存置零。以主库存为权威或按事务差量结算，验证其他玩法获得／消耗后再购物不会清零、回滚或重复发放，保留原物品实例记录。
- 验证已有档位读取、无档位不创建、切换角色隔离、旧商城档迁入、损坏档处理、保存失败回滚、退出重开，以及同一档位跨模块更新；模拟保存委托失败不等于真实磁盘写入失败已验收。
- 恢复原 prefab Animator／Animation、开关与布局动画、首购预览、粒子、字体材质和 `TMPEffect/TMProWarp`，按官方参数运行；目前 JSON 重建器没有完整恢复这些行为，部分按钮音效也尚未实际听验。
- 按[实际运行验证要求](../asset-management.md#实际运行验证方法与要求)，完成商城专项 Editor 导入／编译、从真实 HUD 进入的 Play Mode 正常与失败分支、真实鼠标／键盘／滚动／拖拽与遮挡、实际画面／动画／音效、隔离存档保存重开；必要时验证 Windows Player。全部完成前保持“未完成”，不以静态检查、资源哈希或其他模块运行代替商城验收。
