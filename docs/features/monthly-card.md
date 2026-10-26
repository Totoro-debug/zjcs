# 月卡与星眷累计奖励

[总索引](../README.md)

状态：未完成。月卡、累计星值交易与领奖、官方品质框／锁图、周期卡倒计时格式及累计页开场动画已接入代码和资源目录；Unity Editor 当前未导入这些变更，编译及 Play Mode 鼠标输入、画面、音效和存档重开均尚未验收。

## 功能简介

商城原底栏的周期卡页显示月度奖励和七日祈愿；耀星直购顶部的星眷入口显示累计星值奖励、进度、规则与价格表。商城公共 HUD、导航、支付确认、物品详情和存档缺陷见[商城页](commerce.md)，本页是月卡与累计奖励规则及待实现项的唯一维护页。

### 官方原始功能与配置

固定 manifest `854_156383` 的 [rules.json](../../assets/official/rules.json) 中 `periodic_card_pay` 表、[commerce-ui.json](../../unity/Assets/Official/Commerce/commerce-ui.json) 中 `accumulated_pay_award` 表及 [settings.json](../../.tools/source-packages/official-data/settings.json) 是数值依据；下表是原版恢复目标，并非当前 Unity 已完成状态。

| 范围 | 原始官方功能与规则 | 官方定位 |
| --- | --- | --- |
| 两类周期卡 | 按 ID 排序显示独立卡片；月度奖励 ID 1、`MoneyCostId=3002`，有效 30 天，立即奖励耀星 15×300，每日晨星 2×200，`AwardMultiple=20`、`Discount=2100`；七日祈愿 ID 2、`MoneyCostId=6801`，有效 7 天，立即耀星 15×680，每日物品 110×10，`AwardMultiple=11`、`Discount=1100`。货币价格仍读 `money_cost`，折扣字段按原文案格式显示，不能把字段直接当价格 | [`PeriodicCardPayInfo`](../../.tools/source-packages/official-code/common-source/Common/PeriodicCardPayInfo.cs)；[`PayPeriodicCard.RefreshUI`](../../.tools/source-packages/official-code/client-source/PayPeriodicCard.cs)；`periodic_card_pay` 表 |
| 购买、续期和领奖 | `NotValid`、`CanGetPeriodicAward`、`Renewal` 三种状态控制购买、每日领取与续期按钮；领取成功切换续期态。显示立即／每日奖励、特权、倍率和折扣；随购买记录、剩余时间及进入新一天更新 | [`PayPeriodicCardItem.OnOperationBtn/Refresh/RefreshButtonState`](../../.tools/source-packages/official-code/client-source/PayPeriodicCardItem.cs)；[`PayPeriodicCardItemState`](../../.tools/source-packages/official-code/client-source/PayPeriodicCardItemState.cs) |
| 有效期与提醒 | 显示未激活有效天数、有效状态剩余天数及倒计时；剩余不超过 3 天采用提醒颜色，`Pay.PeriodicCardMaxDurationDay=179` 为续期时长上限配置。有效与当日可领状态分别依赖购买记录及奖励记录，不能只按按钮显示发奖 | [`PayPeriodicCardItem.RefreshDayText`](../../.tools/source-packages/official-code/client-source/PayPeriodicCardItem.cs)；`Pay.PeriodicCardRemindRemainDay/PeriodicCardMaxDurationDay` |
| 额外属性 | 月度奖励配置 `ExtraProps` 为 6004、1034 各 500，对应既有迁移要求中的经验和小推车各加 5%；七日祈愿 `ExtraProps=null`。既显示特权，又需接入真实属性与产出，不能只绘制说明 | [`PayPeriodicCardItem.RefreshExtraProp`](../../.tools/source-packages/official-code/client-source/PayPeriodicCardItem.cs)；`periodic_card_pay.ExtraProps` |
| 红点与新标记 | 有效且每日奖励可领时显示红点；新标记由记录与关闭页面时的读取行为控制，不能永远显示或仅在首次建树时计算 | [`PayPeriodicCardDailyAwardRedDotItem`](../../.tools/source-packages/official-code/client-source/PayPeriodicCardDailyAwardRedDotItem.cs)；[`PayPeriodicCardNewMarkManager`](../../.tools/source-packages/official-code/client-source/PayPeriodicCardNewMarkManager.cs)；[`PayPeriodicCard.OnDisableAndInited`](../../.tools/source-packages/official-code/client-source/PayPeriodicCard.cs) |
| 星眷入口与累计来源 | 直购顶部入口打开 `AccumulatedPayPanel`，购买累计和领奖事件刷新红点；原版区分累计支付与退款记录，并按价格表和货币类型计算累计星值 | [`AccumulatedPayAwardEntry`](../../.tools/source-packages/official-code/client-source/AccumulatedPayAwardEntry.cs)；[`PlayerAccumulatedPayDataExt`](../../.tools/source-packages/official-code/common-source/Common/PlayerAccumulatedPayDataExt.cs) |
| 15 组 75 档奖励 | 按原 `Level/Group/StartValueRequire/CoreAward/SubAwards/IsBigLevel` 配置展示；以达到的最高档确定当前组，达到组末档后显示下一组，末组追加锁定展示项；打开时定位指定档或连续已确认奖励后的档位 | [`AccumulatedPayPanel.InitItemList/RefreshLoopScroll/GetShowLevel`](../../.tools/source-packages/official-code/client-source/AccumulatedPayPanel.cs)；`accumulated_pay_award` 表 |
| 档位状态与表现 | `CanTakeAward/TakenAward/NotActive/Lock` 状态；显示累计值与需求、主副奖励、品质／名称／数量、进度线、遮罩、大档标志和领取按钮；每档只领一次，支持物品详情 | [`AccumulatedPayAwardItem`](../../.tools/source-packages/official-code/client-source/AccumulatedPayAwardItem.cs)；[`PlayerAccumulatedPayAwardExt`](../../.tools/source-packages/official-code/common-source/Common/PlayerAccumulatedPayAwardExt.cs) |
| 主奖励与轮播 | 单主奖励和多主奖励使用不同模板，副奖励独立显示；多奖励使用包含动画事件的约 1.833333 秒片段轮播，隐藏重开从首奖励开始。原 UIBloom 为遮挡排序组件，不能直接视作需要后处理 Bloom | [`AccumulatedPayAwardCoreAwardItem`](../../.tools/source-packages/official-code/client-source/AccumulatedPayAwardCoreAwardItem.cs)；[`AccumulatedPayAwardCoreMultiAwardItem`](../../.tools/source-packages/official-code/client-source/AccumulatedPayAwardCoreMultiAwardItem.cs)；[组件来源清单](../../assets/reference/commerce-components/source-manifest.json) |
| 规则与价格表 | 星眷规则入口显示 `AccumulatedPayRule` 及价格／累计星值两列，按官方价格排序；周期卡特权、原规则和公共支付说明按官方窗口接入 | [`PayInfoTableUtils.PopupAccumulatedPayRuleTable`](../../.tools/source-packages/official-code/client-source/PayInfoTableUtils.cs)；[`PayPeriodicCardItem`](../../.tools/source-packages/official-code/client-source/PayPeriodicCardItem.cs) |
| UI、动画和输入 | 独立月卡模板、原导航与按钮状态、字体、标题 `TMProWarp` 弯曲、`TMPEffect` 描边／双描边／阴影、显示／关闭动画、累计页开场动画、音效、滚动、遮罩与真实命中 | [商城 UI 清单](../../assets/reference/commerce-ui/source-manifest.json)；[组件清单](../../assets/reference/commerce-components/source-manifest.json) |

## 完整还原部分

`scripts/prepare_unity_commerce_catalog.py` 从固定 manifest `854_156383` 的 `rules.json`、官方商城 UI 及 SHA-256 已核对的简中本地化 rawfile 生成 [commerce-runtime.json](../../unity/Assets/Game/Resources/Commerce/commerce-runtime.json)，保留周期属性名称、累计档位描述／进度格式、主副奖励和多主奖励原动画事件。

[`CommerceScreenController`](../../unity/Assets/Game/Runtime/Presentation/Commerce/CommerceScreenController.cs) 在官方周期卡树上绑定即时／每日奖励、官方属性说明、有效期倒计时、临期提醒、领取红点、新标记和购买／领取／续期状态。`OfflineCommerceOffers` 以档案事务保存周期卡时长和每日领取；`OfflineCommerceSession.ActivePeriodicProperties()` 将有效 `ExtraProps` 投影到角色属性，主页挂机收益读取 6004（挂机领取经验）和 1034（小推车产出）加成。离开商城后主页运行时尚无明确的属性重载通知，实时刷新待补。

星眷页面由 `CommerceAccountPages.PopulateAccumulatedItems()` 创建官方档位与 `commerce_core`／`commerce_core_multi` 奖励模板，按配置分开展示主副奖励、大档辉光、详情点击、领取遮罩、左右交错及官方进度线。`prepare_unity_commerce_catalog.py` 从固定累计奖励图集提取品质 3–7 的大／小框与末组锁图；`AccumulatedPayAwardShowConfig` 序列化映射为 3 紫、4 橙、5 金、6 红、7 彩虹，并按档位大小选择对应框。多主奖励播放固定 bundle 中的 `AccumulatedPayCoreMulltiAwardGo_Loop` 并按原 `SwitchItem` 事件轮播。档位描述、进度及未达成文案从官方简中本地化读取。打开页面时，Editor 从固定 prefab bundle 导入 `AccumulatedPayPanel_Show` 原生压缩曲线、转为 Legacy clip 并播放；资源 bundle、对象 ID 与输出路径记录在 Commerce 来源清单。当前组、末组锁定项和首个未连续领取档的定位已按官方 `AccumulatedPayPanel` 流程实现，尚无逐档运行验收。

奖励详情卡片的品质边框使用固定 `ItemQuality` 图集；`CommerceItemIcons.QualitySprite()` 按官方 `Common.Quality` 1–7 选择白、蓝、紫、橙、金、红、彩虹边框。

[`OfflineCommerceOffers.CurrentAccumulatedGroup/CanClaimAccumulated/ClaimAccumulated`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceSession.cs) 按官方门槛／分组处理档位；`OfflineCommerceTransaction` 在库存、奖励和 profile 存档事务成功后提交本地累计值及回执。累计来源采用用户确认的“所有已确认的本地付费订单”，覆盖首购、直购、代金券消费、每日特惠与周期卡；不计代金券购入，不补旧免费订单历史，不模拟退款。资源加载及库存并发保存缺陷见[商城页](commerce.md)。

周期卡剩余时间现在按官方重置点倒计时，并以 `ShowRemainTimeInfo` 的 Brief 分支格式显示；本地化格式和 `PeriodicCardRemindRemainDay=3` 已进入生成目录。月卡变更后主页已存在收益会话的属性重载通知仍待接通。

Play Mode 逻辑、真实鼠标／键盘输入、渲染画面、动画／音效、存档退出重开和 Windows Player 尚未验证。全工程静态元数据检查通过（38,252 个 GUID）；Commerce 的 207 项素材清单逐项通过文件、字节数和 SHA-256 核对，17 个新增或复用品质框／锁图绑定均通过校验，生成目录包含 75 档累计奖励和 2 类周期卡。静态检查不覆盖 Editor 导入、编译和运行。Home 验收 Editor 仍停留在 Safe Mode；本轮新增资源没有被该进程导入，最新 Editor 编译和 Play Mode 状态尚未复核。

开场战斗发布所需的既有周期卡接口已纳入提交：`OfflineCommerceSession` 的状态更新、有效属性投影、到期记录同步、购买及每日领取接口与已提交的展示层保持一致。所用基线已通过 Editor 编译与 Windows 构建；本页的激活、续期、到期和属性重载运行检查仍待完成。

## 自定义实现

以下为已确认的离线差异。

激活／续期沿商城已确认的本地支付替代，不接真实支付、退款和跨账号服务；有效期按本地时钟推进，回拨不重新领奖。续期保留当天已领状态，最多 179 天；到期不能领，未领日奖励不补发。月度奖励的经验和小推车各 5% 加成须进入实际产出与属性，失效时移除；七日祈愿不增加配置没有的属性。月卡“未领不补发”不能套用到首购，首购仍须按[官方补领规则](commerce.md#官方原始功能与调用依据)实现。

原版服务器累计记录由用户确认的本地实际付费订单替代，旧免费购买历史不补算；不模拟退款和跨账号服务。费用、有效期、属性、购买记录、累计值和领取回执与主库存同事务保存。

周期卡及累计模板沿用官方商城 prefab，品质框、锁图、累计页开场及多奖励轮播使用固定 manifest 的官方资源。官方差异仅限已确认的离线交易与本地存档规则。

## 尚未实现

### 月卡与七日祈愿

- 补齐周期卡离开后主页收入会话的属性重载通知，并在 Play Mode 验证激活、续期不叠加、到期移除及存档恢复。

### 星眷累计奖励

- 通过 Play Mode 验证 15 组 75 档逐档门槛、同档防重、组末转换、累计增加后的入口红点／页面刷新及连续领取定位。

### 共享资源、存档与验证

- 在 Editor 成功导入／编译后按[实际运行验证要求](../asset-management.md#实际运行验证方法与要求)完成真实入口 Play Mode 输入、画面、倒计时／动画／音效及隔离档案重开；当前编译与运行验收未完成。
