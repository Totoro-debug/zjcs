# 商城、代金券与礼包

[总索引](../README.md)

状态：未完成（核心商城已接入，完整表现与 Windows 发布尚未完成）

## 功能简介

范围为从当前角色 HUD 进入商城，恢复商城底栏、耀星直购、代金券、每日礼包、每周礼包、首购、耀星兑换、公共购买确认、规则与价格表、奖励图标、提示与存档。月度奖励、七日祈愿及星眷累计奖励的完整规则和缺项统一维护在[月卡与星眷](monthly-card.md)，本页只记录商城中的入口、共享依赖和已实现的星眷入口／当前组领取。家园商店等独立商店见所属功能页。

固定官方版本以 `official-source.json` 为准；原始客户端及 Common 代码见 [client-source](../../.tools/source-packages/official-code/client-source) 与 [common-source](../../.tools/source-packages/official-code/common-source)，商城 UI 载荷见 [commerce-ui.json](../../unity/Assets/Official/Commerce/commerce-ui.json)，配置与其余来源凭据见[来源总表](../../assets/reference/SOURCES.md)。当前接入与缺项见下文。

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

## 原始官方实现

官方商城以原商品目录、分页、礼包、直购、代金券、月卡与奖励条件驱动界面；价格、限购、奖励及原动画／声音由官方配置和组件提供，服务器支付响应不属于客户端源码。

### 官方代码与资源依据

原始客户端、Common、配置、prefab、音频及本地资源的来源凭据集中见[来源总表](../../assets/reference/SOURCES.md)。商城现状与单机支付边界见下文。

## 完整还原部分

### 资源与目录

- `OfficialCommerceAssetCatalog.asset` 已生成并随 `Resources/Commerce` 交付，绑定 317 张官方图片、169 个奖励物品、10 个官方音频事件和 26 个 prefab；生成入口为 [`OfficialCommerceAssetTools.Validate`](../../unity/Assets/Game/Editor/Commerce/OfficialCommerceAssetTools.cs)。
- 运行目录由 [`prepare_unity_commerce_catalog.py`](../../scripts/prepare_unity_commerce_catalog.py) 从固定版 `rules.json`、`commerce-ui.json`、`settings.json` 生成；每个 prefab 展平为 `nodes` 并记录 `has_image/has_text/...`，避免 Unity `JsonUtility` 把缺失的可选组件实例化为空对象。加载与重建见 [`OfficialCommerceAssetCatalog.Load`](../../unity/Assets/Game/Runtime/Presentation/Commerce/OfficialCommerceAssetCatalog.cs) 与 [`OfficialCommerceCatalogView.BuildTree`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfficialCommerceCatalogView.cs)。
- [`prepare_unity_commerce_entry.py`](../../scripts/prepare_unity_commerce_entry.py) 把官方 `FunctionEntryDialog`、`FunctionEntryShopItem`、`ActivityCommonEntry`、直购顶部 `AccumulatedPayEntryGo` 子树及所需官方图片合并进商城载荷；来源和哈希记录在 [commerce source-manifest](../../unity/Assets/Official/Commerce/source-manifest.json) 与 [SOURCES.md](../../assets/reference/SOURCES.md)。Map10 仍从功能入口直接打开商城。Map11 的首购 HUD 已替换为从固定版 APK 提取的原 `FirstPayBtn`，保持原图标、文字、按钮和 LayoutElement，由 WorldCanvas 原纵向布局定位；来源为 `unity/Assets/Official/World/Map/11/UI/source-manifest.json`。首购可见性继续使用现有 `CommerceHudStatus.FirstPayShouldShow`，原支付功能门槛与完整提示图绑定尚未迁移。Map10 仍使用旧 `commerce_first_entry`，其完整还原待后续处理。

### 商城界面与业务

- 主城显示罗拉、晨星和耀星余额；商城、月卡与礼包显示晨星和耀星。点击余额打开原物品详情并读取实际库存。
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
- 奖励图标使用官方 `commerce_item_detail`（`ItemDetailDialog`）显示图标、名称、品质、数量、基础说明和关闭按钮；奖励图标由 `CreateItemIcon` 统一加点击入口。
- 物品图标由 [`CommerceItemIcons`](../../unity/Assets/Game/Runtime/Presentation/Commerce/CommerceItemIcons.cs) 按官方 `ItemIcon` 组成（品质底、图标、数量条、可选底部名称），奖励列表、首购槽、每日奖励、每周礼物与星眷档位共用。

### 存档与交易

[`OfflineCommerceSession`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceSession.cs) 以主存档库存为权威：

- 每次打开时从主存档读取库存，并同步商城状态，不以商城镜像改写主库存。
- 每次提交重新读取最新主存档，只写入商城前后库存的差额，保留其他玩法在商城打开期间新增／消耗的物品。
- 日／周周期在打开期间每秒检查；跨日推进、刷新列表并保存，且不会重复领取。
- 首购不因普通直购自动激活；激活只来自首购确认。激活日记录后第 1/2/3 天奖励永久可补领。

## 自定义实现

本模块原 08:00 刷新按用户指定统一提前至 06:00；共享时钟、时区及旧存档兼容规则见[全模块刷新时刻](city-and-home.md#全模块刷新时刻)。


- HUD 不复制原 `FunctionEntryDialog` 中间层，直接把官方 `FunctionEntryPhoto` 点击绑定到商城；这是已实现的本地入口简化，官方 `commerce_functions/commerce_shop_entry` prefab 已进入目录但当前未作为必须中间页。
- Map10 HUD 首购入口仍由 `AccumulatedPayEntryGo` 组合；Map11 已恢复原 `FirstPayBtn` 外观，但原支付功能门槛及完整提示图状态绑定、真实鼠标打开和 Windows Player 尚未验收。
- 活动页四个入口按官方 `ActivityCommonEntry` prefab 生成，但每日／每周／首购／兑换的图标、标题和位置是本地组合，未接入原活动入口表、活动开放条件或动态活动目录。
- 首购预览使用官方 `ImgItem_FirstPay/FirstPay1/FirstPay2` 武器图，未恢复原角色模型预览、武器属性比较和装备详情弹窗。
- 星眷页现展示全部 15 组、75 个档位与分组标题；每档仍用运行时组合行（等级、星值门槛、核心奖励图标、领取按钮），未恢复原完整 LayoutElement、进度线、锁定展示项、主副奖励分区和多奖励轮播。
- 规则／价格表内容使用官方面板和官方文案，但行布局由运行时生成；物品详情只使用官方 `ItemDetailDialog` 图标、名称、品质、数量、基础说明和关闭，未接使用／分解／来源／限时等原交互；活动入口、TMPEffect、TMProWarp、原生动画、粒子和大部分按钮音效未恢复。
- 物品详情顶部白色装饰和中部空白区仍未按原 `ItemDetailDialog` 脚本完全校正，当前是可用的基础版，不声称与官方最终版布局一致。
- 离线目录提供用户选择的赛季／礼包内容；目录切换替代服务器换组，各目录限购状态独立保存，每周按原周一规则刷新。
- 十进制金额购买是离线模拟，不接真实支付；代金券按本地库存扣除；周期卡、月卡属性、赛季换组和原服务器订单不接。

### 已确认的本地接线与边界

确认弹窗、周期礼包控件及 Toggle 的原始字段和资源绑定见 [来源总表](../../assets/reference/SOURCES.md)；离线确认窗口沿用本地 UI 流程，不接真实支付 SDK。

## 尚未实现

### 后续范围与已知差异

- 月度奖励、七日祈愿和星眷完整页面；周期卡页签当前按锁定功能隐藏。规则和缺项见[月卡与星眷](monthly-card.md#尚未实现)。
- 原活动入口目录、活动开放条件、每日／每周红点 agent、活动页布局和动态活动资源。
- 首购原角色模型／骨骼预览、武器比较、`FirstPayPreviewAppearance` 动画和 `FirstPayDialog` 原生动画。
- 物品详情的原使用／分解按钮、来源列表、限时说明、属性详情和货币余额点击详情；当前奖励图标点击可打开基础物品详情，但不执行物品操作。
- 除已接入的累计充值显示曲线外，原 prefab 的 Animator／Animation、弹窗开关动画、UIBloom、文字特效、原生粒子、字体材质和按钮音效仍有缺项。
- 星眷原 15 组完整 LayoutElement、主副奖励分区、多奖励轮播、进度线和末组锁定项；当前已展示全部组与档位，但行布局为运行时组合。
- 价格表行内容由运行时组合，当前页面顶部／底部裁切和滚动定位仍未按原 `PayInfoTableDialog` 脚本校正。
- 活动页入口视觉、礼品图标内数量文字、首购顶部白色信息区、每日整组价格区的最终版式仍需按原 prefab 校正。
- 商城完整入口、交互和 Windows Player 发布仍未完成。

累计充值面板的显示曲线已转换为 Unity 原生 `AnimationClip` 并由本地资源加载；来源与输出见[动画来源清单](../../assets/reference/commerce-components/accumulated-panel-animation-source-manifest.json)。
