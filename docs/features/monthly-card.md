# 月卡与星眷累计奖励

[总索引](../README.md)

状态：未完成。月卡业务尚未接入；星眷已有公共数据和部分离线累计／领奖代码，真实入口、完整界面、表现和 Unity 游玩未验收。

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

尚无本功能完整接入并在 Unity 运行验收的结论。月卡的原 UI 子树虽然包含在 `commerce_main`，但 [`CommerceScreenController`](../../unity/Assets/Game/Runtime/Presentation/Commerce/CommerceScreenController.cs) 未实现其购买、续期和领奖业务。

累计奖励已进入商城公共 [commerce-runtime.json](../../unity/Assets/Game/Resources/Commerce/commerce-runtime.json)：75 档配置和 `commerce_accumulated/commerce_accumulated_item/commerce_core/commerce_core_multi/commerce_price_table/commerce_price_row` 等 UI 树。当前 `CommerceAccumulatedAward` 将主副奖励合为 `rewards`，保留 `isBig`，仍需恢复原主副奖励分区和模板选择。

[`OfflineCommerceOffers.CurrentAccumulatedGroup/CurrentAccumulatedAwards/CanClaimAccumulated/ClaimAccumulated`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceSession.cs) 已写有当前组、门槛、领取次数和奖励处理；[`OfflineCommerceTransaction`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceTransaction.cs) 提供累计值、奖励和保存委托失败时不提交状态的底层。`CommerceScreenController.PopulateAccumulatedItems()` 已写有部分列表、领取按钮和遮罩刷新；原生资源目录资产缺失、真实入口未接以及共享库存覆盖缺陷仍见商城页。

商城公共静态元数据／素材哈希检查及交易核心内存检查的实际范围见[商城已执行检查](commerce.md#已执行检查与边界)。这些检查未验证月卡激活、属性产出、星眷 UI、75 档逐档领奖或实际磁盘恢复，不作为本功能运行通过依据。商城程序集缓存存在不能代替本功能当前 Editor 导入与编译校验；本功能 Play Mode 逻辑、真实输入、画面／动画／音效、存档重开和 Windows Player 均未验收。

## 自定义实现

以下沿用所属页已有的单机处理要求，不代表当前 Unity 已实现或验收。

激活／续期沿商城已确认的本地支付替代，不接真实支付、退款和跨账号服务；有效期按本地时钟推进，回拨不重新领奖。续期保留当天已领状态，最多 179 天；到期不能领，未领日奖励不补发。月度奖励的经验和小推车各 5% 加成须进入实际产出与属性，失效时移除；七日祈愿不增加配置没有的属性。月卡“未领不补发”不能套用到首购，首购仍须按[官方补领规则](commerce.md#官方原始功能与调用依据)实现。

原版服务器累计记录由本地实际确认的购买记录替代，旧免费购买历史不补算；不模拟退款和真实货币流水。所有保留购买类型怎样贡献累计星值须对照官方记录消费与已确认离线方案，不能将未知的服务端归集决策写成已恢复。费用、有效期、属性、购买记录、累计值和领取回执应与主库存同事务保存。

字体、材质、模板、动画与效果仍需恢复官方组件及参数；本地化处理不授权永久使用自绘、静态图或简化轮播代替原表现。

## 尚未实现

### 月卡与七日祈愿

- 将 `periodic_card_pay` 原字段、价格引用和设置接入 Unity 专用目录／加载绑定；当前商城运行目录没有周期卡业务配置和存档模型。恢复两个模板、立即／每日奖励、倍率、折扣、特权和详情。
- 实现激活、续期、当日领奖、已领后的续期、到期重购、179 天上限及失败恢复；保存到期检查点、当日领取回执及相关购买记录，保持当天已领状态，不因续期或时间回拨重复发奖。
- 按原 `NotValid/CanGetPeriodicAward/Renewal` 恢复按钮、价格与代金券方式、剩余天数、临期颜色、倒计时、新标记和可领奖红点，响应购买、领奖、跨日、过期及关闭重开。
- 将月度奖励的两项 5% 加成接入实际经验／小推车产出与人物属性，测试激活后生效、续期不叠加、到期移除、存档恢复；七日祈愿无额外属性。
- 接通商城底栏和资源／奖励详情等真实调用方；规则、支付确认、取消、返回、连续操作与失败提示按官方窗口恢复。

### 星眷累计奖励

- 接通直购顶部 `AccumulatedPayAwardEntry`、可领奖红点和规则／价格表，不以临时六分类导航代替。恢复指定档定位、连续领取后的默认定位、当前组推进及末组锁定项。
- 恢复 `CoreAward/SubAwards/IsBigLevel` 的结构与单／多主奖励模板；当前目录合并后的奖励 map 和手建图标无法还原主副奖励的分区、尺寸、图标详情和轮播顺序，应补齐官方数据与显式资源绑定。
- 恢复每档门槛、累计值／需求文字、进度线、左右排列、锁定／未激活／可领／已领状态、主副奖励与大档表现。当前 `PopulateAccumulatedItems()` 更新 `LevelTxt/ScoreTxt`，原树包含 `ProgressTxt` 等不同绑定，不能视为完整进度显示已完成。
- 接入多主奖励动画事件、轮播、隐藏重开复位、`AccumulatedPayPanel_Show` 及原排序和字体效果；不以静态格子代替原连续表现。
- 验证 15 组 75 档逐档门槛、同档防重、组末转换、最高组、未领取奖励的可达性、累计更新和重新进入后的显示；当前单纯按达到门槛推组的代码需要与原 UI 可达性及已确认单机方案共同验证。
- 核对各保留购买类型对累计记录的贡献及持久化，排除旧免费历史补算、重复确认、支付代金券来源重复累计等异常；需先查官方记录与消费者，缺少服务端决策时明确提出规则问题，不自行猜测。

### 共享资源、存档与验证

- 完成商城公共原生资产、字体／shader、`TMPEffect/TMProWarp`、物品图标、动画、音效、动态模板加载和目录资产安装；跟随[商城待实现清单](commerce.md#尚未实现)修复公共窗口与主库存旧快照覆盖。
- 以隔离存档验证月卡和星眷操作与其他玩法共享库存，覆盖保存失败、损坏数据、旧档兼容、跨角色隔离、退出重开、时间前进／回拨，确保货币、奖励、有效期、加成和回执一致。
- 按[实际运行验证要求](../asset-management.md#实际运行验证方法与要求)完成 Editor 导入／编译、真实商城入口的 Play Mode 逻辑和输入、实际画面、连续动画／音效及存档恢复；不以方法调用、静态通过或旧引擎参考结果代替。只有需要检查导出行为时再验证 Windows Player，不覆盖正式成品。
