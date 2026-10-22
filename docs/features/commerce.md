# 商城、代金券与礼包

[总索引](../README.md)

状态：未完成。核心商城流程已按官方 prefab 和规则接通，并在隔离 Unity 工程完成 Editor 编译、Play Mode 逻辑与实际画面验证；月度奖励／七日祈愿、星眷完整页面、首购角色与武器预览、活动目录、原生动画／粒子／全部音效、奖励详情和 Windows Player 仍未完成。

## 功能简介

范围为从当前角色 HUD 进入商城，恢复商城底栏、耀星直购、代金券、每日礼包、每周礼包、首购、耀星兑换、公共购买确认、规则与价格表、奖励图标、提示与存档。月度奖励、七日祈愿及星眷累计奖励的完整规则和缺项统一维护在[月卡与星眷](monthly-card.md)，本页只记录商城中的入口、共享依赖和已实现的星眷入口／当前组领取。家园商店等独立商店见所属功能页。

固定官方来源为 manifest `854_156383`：客户端反编译结果在 [client-source](../../.tools/source-packages/official-code/client-source)，共享规则在 [common-source](../../.tools/source-packages/official-code/common-source)，配置在 [rules.json](../../assets/official/rules.json)、[commerce-ui.json](../../unity/Assets/Official/Commerce/commerce-ui.json) 与 [settings.json](../../.tools/source-packages/official-data/settings.json)，原 UI 层级在 [ui.json](../../assets/official/ui.json)。以下“官方功能”是恢复依据；本页“完整还原部分”只写已实际接通并验证的内容。

### 官方原始功能与调用依据

| 范围 | 官方功能、规则与交互 | 官方定位 |
| --- | --- | --- |
| HUD 与功能菜单 | `ShopEntryBtn` 检查 `FunctionType.ShopActivity` 开放条件，未开放时禁用入口并显示空位；点击打开 `ShopActivityEntryPanel`。首购另有 HUD 图标，支付未开放时隐藏，激活且全部领奖后隐藏 | [`ShopEntryBtn`](../../.tools/source-packages/official-code/client-source/ShopEntryBtn.cs)；[`FirstPayIconEntry`](../../.tools/source-packages/official-code/client-source/FirstPayIconEntry.cs) |
| 商城框架与导航 | 底栏切换活动、周期卡、直购；切换刷新标题与内容；`InitPayPanel(PayType)` 支持从资源不足等调用方直接进入指定支付页；关闭恢复调用方窗口 | [`ShopActivityEntryPanel.Init/InitPayPanel/OnTabItemChanged`](../../.tools/source-packages/official-code/client-source/ShopActivityEntryPanel.cs)；[`PayMoneyChangeDialog.OnConfirmBtnClick`](../../.tools/source-packages/official-code/client-source/PayMoneyChangeDialog.cs) |
| 直购／代金券页签 | 按功能开放状态决定可见页签；未指定支付类型时，代金券功能可用则默认代金券，否则直购。切换刷新商品列表、顶部说明、标题和价格表 | [`PayDirectPay.OnEnable/SwitchTab/OnRuleBtnClick`](../../.tools/source-packages/official-code/client-source/PayDirectPay.cs) |
| 耀星直购 | 6 档商品按官方排序显示标题、图标、价格、固定奖励和额外奖励；每档独立首购，首购后切换复购奖励与超值比例，并响应购买记录 | [`PayDirectPayItem.InitShow/RefreshGreatValueAndExtraAward`](../../.tools/source-packages/official-code/client-source/PayDirectPayItem.cs)；`direct_pay`、`money_cost` 表 |
| 代金券购买 | 6 档商品显示官方标题、图标、`MoneyCostInfo.VoucherValue` 和价格；普通目录没有额外奖励；第三方支付上下文与外部产品价格刷新属原联网能力 | [`PayVoucherItem`](../../.tools/source-packages/official-code/client-source/PayVoucherItem.cs)；[`NormalPayVoucherItemContext`](../../.tools/source-packages/official-code/client-source/NormalPayVoucherItemContext.cs)；`pay_voucher` 表 |
| 共享支付与确认 | 按商品上下文和可用代金券决定货币／物品支付方式；确认框显示标题、礼包图、奖励列表、物品支付图标与数量或货币价格；购买记录更新后关闭确认框 | [`CommonPayBtn`](../../.tools/source-packages/official-code/client-source/CommonPayBtn.cs)；[`PayGiftItem`](../../.tools/source-packages/official-code/client-source/PayGiftItem.cs)；[`PayGiftConfirmDialog.Refresh/ShowAwards/RefreshPayPrices`](../../.tools/source-packages/official-code/client-source/PayGiftConfirmDialog.cs) |
| 每日礼包 | 4 组共 12 条，每组 3 档；免费赠礼每日一次。单买、整组互斥：任一单品已购后不能整组，整组已购后不能再单买；显示原价、整组价格、超值比例、剩余时间和规则 | [`DailyPayGiftDialog`](../../.tools/source-packages/official-code/client-source/DailyPayGiftDialog.cs)；[`DailyPayGiftItem`](../../.tools/source-packages/official-code/client-source/DailyPayGiftItem.cs)；[`PlayerPeriodicPayGiftDataExt.DailyPayGiftCanBuy/DailyPayGiftCanBuyAll`](../../.tools/source-packages/official-code/common-source/Common/PlayerPeriodicPayGiftDataExt.cs) |
| 每周礼包 | 85 组共 595 条；按当前组和条件显示，免费赠礼使用顶部独立入口；付费商品按售罄状态和 ID 排序，显示剩余次数、售罄遮罩、超值比例、礼包图、奖励、新标记和剩余时间。固定版所有商品为 `NoneCondition` | [`WeeklyPayGiftPanel.RefreshGifts`](../../.tools/source-packages/official-code/client-source/WeeklyPayGiftPanel.cs)；[`WeeklyPayGiftShowContext`](../../.tools/source-packages/official-code/client-source/WeeklyPayGiftShowContext.cs)；`weekly_pay_gift_group` 表 |
| 首购 | 独立确认激活首购；奖励按激活日计算第 1/2/3 天，不因超过第三天失效；每天奖励只能领一次，第三天以后仍可补领；全部领取后隐藏 HUD 图标 | [`FirstPayDialog`](../../.tools/source-packages/official-code/client-source/FirstPayDialog.cs)；[`FirstPayAwardItem`](../../.tools/source-packages/official-code/client-source/FirstPayAwardItem.cs)；[`PlayerFirstPayExt`](../../.tools/source-packages/official-code/common-source/Common/PlayerFirstPayExt.cs) |
| 累计星值与兑换 | 直购顶部星眷入口显示累计星值、当前组奖励和价格表；兑换窗口按原滑条、加减、最大值、余额不足跳转直购；成功领奖后关闭 | [`AccumulatedPayPanel`](../../.tools/source-packages/official-code/client-source/AccumulatedPayPanel.cs)；[`PayMoneyChangeDialog`](../../.tools/source-packages/official-code/client-source/PayMoneyChangeDialog.cs) |
| 规则、价格表、物品详情 | 代金券、每日礼包、累计星值读取 `rule_tip`；价格表按 `money_cost` 顺序显示代金券与折扣价格；奖励图标点击打开原物品详情 | [`PayInfoTableUtils`](../../.tools/source-packages/official-code/client-source/PayInfoTableUtils.cs)；[`RuleDialog`](../../.tools/source-packages/official-code/client-source/RuleDialog.cs)；[`ItemIcon`](../../.tools/source-packages/official-code/client-source/ItemIcon.cs) |

## 完整还原部分

### 资源与目录

- `OfficialCommerceAssetCatalog.asset` 已生成并随 `Resources/Commerce` 交付，绑定 317 张官方图片、169 个奖励物品、10 个官方音频事件和 26 个 prefab；生成入口为 [`OfficialCommerceAssetTools.Validate`](../../unity/Assets/Game/Editor/Commerce/OfficialCommerceAssetTools.cs)。
- 运行目录由 [`prepare_unity_commerce_catalog.py`](../../scripts/prepare_unity_commerce_catalog.py) 从固定版 `rules.json`、`commerce-ui.json`、`settings.json` 生成；每个 prefab 展平为 `nodes` 并记录 `has_image/has_text/...`，避免 Unity `JsonUtility` 把缺失的可选组件实例化为空对象。加载与重建见 [`OfficialCommerceAssetCatalog.Load`](../../unity/Assets/Game/Runtime/Presentation/Commerce/OfficialCommerceAssetCatalog.cs) 与 [`OfficialCommerceCatalogView.BuildTree`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfficialCommerceCatalogView.cs)。
- [`prepare_unity_commerce_entry.py`](../../scripts/prepare_unity_commerce_entry.py) 把官方 `FunctionEntryDialog`、`FunctionEntryShopItem`、`ActivityCommonEntry`、直购顶部 `AccumulatedPayEntryGo` 子树及所需官方图片合并进商城载荷；来源和哈希记录在 [commerce source-manifest](../../unity/Assets/Official/Commerce/source-manifest.json) 与 [SOURCES.md](../../assets/reference/SOURCES.md)。

### HUD 入口

[`OfficialMap10GameRuntime.BindWorldControl`](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap10GameRuntime.cs) 把官方 `FunctionEntryPhoto/EntryBtnFolded`、`EntryBtnExpanded` 绑定到商城；[`CommerceScreenController.OpenShop`](../../unity/Assets/Game/Runtime/Presentation/Commerce/CommerceScreenController.cs) 以当前存档打开独立 1080×1920 商城 Canvas。HUD 首购入口使用商城直购顶部官方 `AccumulatedPayEntryGo` 节点作为入口结构，显示官方首购标题、红点，并按 `CommerceHudStatus.FirstPayShouldShow` 在全部领奖后隐藏；它由 `commerce_first_entry` 生成并位于 `EventAndPurchaseEntrys`。

### 商城界面与业务

- 主窗口使用官方 `commerce_main`：底栏活动／直购切换，直购内再按官方规则默认代金券页签（代金券可用），可切回耀星直购。周期卡页签因月卡业务未完成而按锁定功能隐藏，入口和缺项只在月卡页记录。
- 活动页使用官方 `ActivityCommonEntry` 生成每日、每周、首购、耀星兑换四个入口；入口标题、红点和图标槽按本地状态刷新。
- 直购 6 档、代金券 6 档由官方 `commerce_direct_item`、`commerce_voucher` 生成；显示首购／复购额外奖励、超值比例、价格、代金券数量，并优先使用足额代金券。
- 每日礼包使用官方 `commerce_daily`：3 个官方商品槽、免费赠礼、整组购买、原价／整组价、规则和刷新时间；单买／整组按 `DailyPayGiftCanBuy`、`DailyPayGiftCanBuyAll` 互斥。
- 每周礼包使用官方 `commerce_weekly` 和 `commerce_gift`：按售罄状态、ID 排序；显示剩余可购买次数、售罄遮罩、超值比例、官方礼包图标和奖励；顶部独立免费礼包。
- 首购使用官方 `commerce_first`：未激活时显示支付按钮和累计星值，激活后显示已购买；三天奖励按 `FirstPayTodayCanGetAwardIndex` 计算且不设过期，逐日领取；武器槽按职业匹配第 1 天武器，第 2/3 天使用原配置首项。
- 星眷使用官方 `commerce_accumulated`、`commerce_accumulated_item`、`commerce_core`：显示当前组档位、门槛、主／副奖励、领取状态和规则价格表；`CurrentAccumulatedGroup/CanClaimAccumulated/ClaimAccumulated` 已接通。完整月卡／星眷表现由月卡页继续维护。
- 兑换使用官方 `commerce_exchange`：滑条、加减、最大值、兑换前后物品与数量；零余额时选择量为 0，余额不足弹出前往直购确认，成功领取后关闭兑换。
- 确认使用官方 `commerce_confirm`：标题、礼包图、奖励图标、代金券或货币价格；购买时以确认时的完整报价（期数、已购数、价格、奖励集合）复核，状态变化返回 `StaleQuote` 并回滚。
- 规则与价格表使用官方 `commerce_rule`、`commerce_price_table` 面板；代金券／星眷规则读取原 `rule_tip` 文案，价格表按 `money_cost` 的 `StartValue` 排序显示。
- 物品图标由 [`CommerceItemIcons`](../../unity/Assets/Game/Runtime/Presentation/Commerce/CommerceItemIcons.cs) 按官方 `ItemIcon` 组成（品质底、图标、数量条、可选底部名称），奖励列表、首购槽、每日奖励、每周礼物与星眷档位共用。

### 存档与交易

[`OfflineCommerceSession`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceSession.cs) 以主存档库存为权威：

- 打开时重新读取主存档并把库存镜像到商城状态；首次迁移不会用旧商城快照增删主库存。
- 每次提交重新读取最新主存档，只写入商城前后库存的差额，保留其他玩法在商城打开期间新增／消耗的物品。
- 日／周周期在打开期间每秒检查；跨日推进、刷新列表并保存，且不会重复领取。
- 首购不因普通直购自动激活；激活只来自首购确认。激活日记录后第 1/2/3 天奖励永久可补领。
- 每日单买／整组互斥、每周免费礼包、限购次数、累计星值、兑换边界、确认快照和保存失败回滚均由 [`OfflineCommerceTransaction`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceTransaction.cs) 统一处理。

## 自定义实现

- HUD 不复制原 `FunctionEntryDialog` 中间层，直接把官方 `FunctionEntryPhoto` 点击绑定到商城；这是已实现的本地入口简化，官方 `commerce_functions/commerce_shop_entry` prefab 已进入目录但当前未作为必须中间页。
- HUD 首购入口由官方 `AccumulatedPayEntryGo` 结构、标题和红点组成，不是原 `FirstPayIconEntry` prefab 的一比一复刻；原 `FirstPayIconEntry` 不在当前已提取 UI 载荷中。
- 活动页四个入口按官方 `ActivityCommonEntry` prefab 生成，但每日／每周／首购／兑换的图标、标题和位置是本地组合，未接入原活动入口表、活动开放条件或动态活动目录。
- 首购预览使用官方 `ImgItem_FirstPay/FirstPay1/FirstPay2` 武器图，未恢复原角色模型预览、武器属性比较和装备详情弹窗。
- 星眷档位行按官方核心奖励 prefab 与物品图标组合，未恢复原 15 组完整 LayoutElement、进度线、锁定展示项、主副奖励分区和多奖励轮播。
- 规则／价格表内容使用官方面板和官方文案，但行布局由运行时生成；活动入口、物品详情、TMPEffect、TMProWarp、原生动画、粒子和大部分按钮音效未恢复。
- 十进制金额购买是离线模拟，不接真实支付；代金券按本地库存扣除；周期卡、月卡属性、赛季换组和原服务器订单不接。

## 尚未实现

- 月度奖励、七日祈愿和星眷完整页面；周期卡页签当前按锁定功能隐藏。规则和缺项见[月卡与星眷](monthly-card.md#尚未实现)。
- 原活动入口目录、活动开放条件、每日／每周红点 agent、活动页布局和动态活动资源。
- 首购原角色模型／骨骼预览、武器比较、`FirstPayPreviewAppearance` 动画和 `FirstPayDialog` 原生动画。
- 物品详情弹窗、奖励图标点击、货币余额点击详情；当前奖励图标只展示数量与名称。
- 原 prefab 的 Animator／Animation、PopupWindow 开关动画、UIBloom、TMPEffect／TMProWarp、原生粒子、字体材质和全部按钮音效；仅播放 `Main_Cursor_Normal`。
- 星眷 15 组完整布局、主副奖励分区、多奖励轮播、进度线和末组锁定项。
- 活动页入口视觉、礼品图标内数量文字、首购顶部白色信息区、每日整组价格区的最终版式仍需按原 prefab 校正。
- 真实磁盘保存失败、只读目录、断电中断和 Windows Player 运行未验证；当前只验证保存委托失败回滚和正常磁盘重开。
- 新增/变更的 `unity/Assets/Official/Commerce` 入口图片、`commerce-ui.json` 和生成目录尚未纳入完整素材 ZIP 快照；本轮未重建素材包，不声称这些新增载荷已备份。

## 已执行检查与边界

- `python -B scripts/check_unity_project.py` 通过：27534 个 GUID 的静态元数据检查通过；该检查不代替 Editor 导入、编译或运行。
- 隔离 Unity 工程（`D:/Desktop/zjcs-commerce-acceptance`，复制当前 `Assets/Packages/ProjectSettings`）使用 `Unity 2022.3.57f1c2` 完成 Editor 导入与 C# 编译，`CommercePlayModeAcceptance` 通过：官方主窗口／直购／代金券／每日／每周／首购／星眷／兑换 prefab 实际创建，代金券支付、免费赠礼、单买／整组互斥、首购激活与领取、兑换、保存重开，以及另一玩法新增库存后商城购买不清零。该验收在隔离工程把不相关的 Map10 运行入口替换为桩，商城代码本身未裁剪；共享工程当时被另一模块未完成的 `HomeHudView.TreasureGacha.cs` 编译错误阻塞，不能据此声称共享工程已完成 Editor 验收。
- Play Mode 实际捕获 10 张画面并逐张检查：直购主页、确认框、活动页、每日礼包、每日已购、每周礼包、首购、首购已激活、兑换、重开。画面确认官方角色立绘、标题、六档商品、每日三档、每周六档、首购三天奖励、兑换滑条和官方按钮均实际渲染；同时记录活动入口文字、礼品图标数量、首购顶部信息区和每日整组价格区仍有版式差异，未把差异写成已完成。
- 验收的按钮输入通过 `ExecuteEvents` 向真实 Unity UI 控件派发点击并等待界面状态，不是直接调用业务方法；尚未用操作系统鼠标覆盖遮挡、滚动、拖拽和键盘焦点。
- 未执行 Windows Player 构建或运行；完整游戏验收前不发布或覆盖 `D:/Desktop/杖剑传说/`。
