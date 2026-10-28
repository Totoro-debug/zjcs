# 商城、代金券与礼包

[总索引](../README.md)

状态：未完成（核心商城已接入，完整表现与 Windows 发布尚未完成）

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

## 原始官方实现

官方商城以原商品目录、分页、礼包、直购、代金券、月卡与奖励条件驱动界面；价格、限购、奖励及原动画／声音由官方配置和组件提供，服务器支付响应不属于客户端源码。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

本节记录首次修复；后续实现已覆盖下文历史记录中尚未接入的礼包组、代金券、续期和月卡属性。

彩色货币为原表 `FlareStarId=15` 的「耀星」，黄色货币为 `DiamonId=2` 的「晨星」，金币为「罗拉」。三种官方图标及库存字段原本已存在；旧顶部默认仅绑定罗拉、晨星，原表又将货币设为 `ShowInBag=false`，造成耀星没有可见余额入口。现主城显示三种主要货币，商城 / 月卡 / 礼包显示晨星、耀星，点击余额使用原物品详情 prefab 显示名称、原始说明及实际库存。调整顶部控件的鼠标顺序，恢复页面上方的实际点击。

`python scripts/prepare_commerce_assets.py` 复用固定 APK 及已校验 `854_156383` CDN 缓存，转换原 `DailyPayGiftDialog / WeeklyPayGiftPanel / PayGiftItem`，并根据 `PayGiftIconConfig` 的 GUID 解码六个原始礼包立绘 prefab。首购 / 再次赠送数值继续读取原表，六档商品名称与货币说明补读官方中文文本分包；每日 / 每周礼包展示完整奖励，补接原表每日免费晨星、每周赠礼、整组购买和限购状态。新增运行目录为 `assets/official/commerce-ui.json`，全部 74 项相关图像资源、来源 bundle / 对象、输出哈希、中文分包及行为代码凭据见 [commerce-ui/source-manifest.json](../../assets/reference/commerce-ui/source-manifest.json)，动态图片均纳入显式加载清单。第 1 组礼包此前缺少的五个奖励图标已从官方资源转换，其余已存在图标重新核验登记。没有新增自绘图片。

每日礼包替换原通用货架卡片，每周礼包恢复原专用卡片、立绘及所有奖励图标。免费入库、本地日 / 七日周期与第 1 组礼包仍为离线适配，未模拟服务器赛季换组或支付 / 代金券服务。

继续从固定 APK 及匹配 `854_156383` 的已校验 CDN 提取原 `ShopActivityEntryPanel`、`PayVoucherItem`、`PayGiftConfirmDialog`、`PayMoneyChangeDialog`、`PayInfoTableDialog / PayInfoTableRowItem`、`AccumulatedPayPanel / AccumulatedPayAwardItem / AccumulatedPayCoreAwardGo / AccumulatedPayCoreMulltiAwardGo`、`FirstPayDialog` 和 `RuleDialog`。与每日 / 每周界面和六个礼包立绘合计 21 个原始 prefab，276 项资源哈希。所有 4 组每日 / 85 组每周礼包、累计奖励及首购奖励共用的 170 个物品图标，以及六档代金券立绘全部核验并接入显式加载清单，没有自绘补充。

追加解析 `daily_pay_gift_group`、`weekly_pay_gift_group`、`pay_voucher`、`accumulated_pay_award`、`first_pay_award`、`rule_tip` 六张原表，补读全部礼包名称、代金券标题、卡片特权与规则中文。原版代金券规则明确「充值不获星值，消费时获得」；本地结算按此实现。月度奖励原表属性 `6004=500 / 1034=500` 分别进入经验最终产量、小推车产出及角色属性显示。

依据：[commerce-ui/source-manifest.json](../../assets/reference/commerce-ui/source-manifest.json)。

用户选择离线提供赛季 / 礼包目录，随时查看和购买全部原版内容；原服务器换组改为手动目录选择，各目录本期限购独立保存，每周采用原周一刷新规则。金额购买是明确标记的本地模拟；代金券购买实际扣除本地库存；线上支付平台、订单、退款及跨账号转移不接入。首购主视觉使用已转换的官方角色网格，武器属性预览复用本地装备规则。

购买选择依据固定 `854_156383` 的 `CommonPayBtn`、`PayGiftConfirmDialog`、`PlayerPeriodicPayGiftDataExt` 及已转换 `commerce-ui.json`。旧购买入口接回足额代金券优先规则，确认弹窗恢复原布局和奖励网格；本项没有新增或重绘图像。原服务器支付订单仍以原报价的本地成功事务适配。原期数选择和所有活动仍未据此完成。

`scripts/prepare_commerce_bindings.py`只读固定APK及已校验同版本缓存，沿现有commerce-ui的21个原prefab取得PopupWindow.mShowClip／mHideClip／mLoopClip、Animation默认播放引用，以及PayPeriodicCardItem、CommonPayBtn、FirstPayDialog／AwardItem／PreviewAppearance的序列化字段。沿ExtraPropItem实际指针取得PayPeriodicCardPropItem／PlayerPropItem原模板，复用已登记官方贴图，严格核对原Sprite解码输出，不新增位图或修改共享素材目录。

原片段、组件对象及相关客户端C#／PopupWindow／RotateAnimation代码的SHA256、原语言分包输入与模板来源内嵌于；21项片段引用包括FirstPayDialog_AniShow两条2793键的原四元数曲线，保留原时间、值和出入切线，仅接受原非加权曲线。生成器`--check`只读重建并比较代码元数据；该文件属于运行代码输入，不包含验收成绩。原图、资源包来源及哈希继续沿用commerce-ui、现有UI／物品素材各来源清单；没有引入新版资源或自绘补充。具体运行边界与剩余未迁移项只记于对应功能／地图文档。

prepare_commerce_bindings.py 从固定 APK／匹配 CDN 的 854_156383 补取全部 38 个商业节点绑定，以及 RuleDialog.mTipInfoPrefab、PayInfoTableDialog.mDescPrefab/mRowItemPrefab 的真实引用模板；GlobalColors.asset 的 White 和 Light 主题 Red 直接读取配置。 保存对象、原始字节及源代码 SHA256；时间格式文本来自相同版本语言表，规则来自 PayPeriodicCardItem、ShowRemainTimeInfo、CommonPayBtn、CultureTimeSpanArg。AccumulatedPayAwardItem 嵌套枚举读取使用原程序集 StateSwitcher<T> 的序列化字段布局，按原类声明顺序拼接，完整对象及托管引用字节仍严格校验。此轮为原有资源与程序集字段解码，没有自绘或新版替换。

prepare_commerce_bindings.py 追加固定854_156383的22个原prefab（既有21个commerce加PayDirectPayItem）完整UI定义、原LayoutElementClamper与AdaptiveHorizontalLayoutGroup字段，保存原组件字节SHA256及原类源码SHA256。92个业务／文本效果／页签／计时节点按原程序集严格解码，规则／价格行沿真实序列化指针取得并重定根路径；模板图片复用已校验的原PNG，不新绘图片。运行时恢复原Grid、字体尺寸、奖励排列与原MoneyCostInfoParser价格列表规则、未修改APK或预期哈希。

`scripts/prepare_commerce_components.py` 按固定 manifest `854_156383` 读取已校验 APK／原版 CDN，复用完全相同的既有 Sprite，新增像素仅写入 `assets/official/commerce-components/`。`commerce-components/source-manifest.json` 记录 26 个 prefab 所用 172 个纹理资源、原对象及包版本／URL／大小／哈希、代码与语言输入；其中 23 个 PNG 位于独立目录，其余复用共享原素材。没有自绘或新版替代。

累计奖励运行时采用 `AccumulatedPayAwardShowConfig` 中原品质背景、锁定图片和缩放、颜色，原 UIMesh 的 MeshRenderer 材质、网格坐标／UV／三角形／顶点色与纹理平移参数；`RotateAnimation` 保留序列化速度；CoreMulti 使用原片段与 SwitchItem 事件。列表方向依据取得的 `LoopScrollRect.Runtime.dll` 反编译代码和该 prefab 的 `reverseDirection=true`、`reverseIndex=true`，不按截图猜测。新增入口原图先转换并登记，接入状态以验收全流程记录为准。

commerce-components/source-manifest.json 进一步覆盖 28 个 prefab、173 个实际纹理资源，包含 ActivityEntryBtn、ActivityEntryItemWrapper、每日／每周入口与 FirstPayIconEntry 的原物件和图片；新增图片仅写入独立目录。原 HomeUIActivityEntryController.mEntryRoot、home_right_entry_ui_activity 原表哈希、ActivityEntryOverview 的 LoopVerticalScrollRect、ShopActivityEntryPanel／PayDirectPay 状态与 RuleBtn.mRuleKey 直接迁移。来源保持固定 APK／原版 CDN manifest 854_156383，没有自绘或新版替代。

prepare_commerce_first_preview.py 只写 assets/official/commerce-components/first-preview；first-preview-source-manifest.json 记录固定854_156383原包／对象输入哈希、原 FirstPayPreviewAppearance／FirstPayDialog 代码、外观与皮肤表、既有职业转换来源哈希及所有输出SHA256。原男女100/show_01、200/show_01四段动作、两种原首购主手皮肤、原Follower矩阵、原骨骼网格／UV／染色，以及原UIParticle／ParticleSystem／材质纹理按现有开发期转换链迁移；37个实际纹理绑定、68条显式运行资源，未绘图、未生成替代角色、未引入UI Spine运行时扩展。

commerce-components/source-manifest.json 扩展至28 prefab、177纹理；四张FirstPay中文图片通过MultiLanguageImage原Key与LanguageImagePathUtils原zh_CN／default路径顺序取得，CustomScaleByLanguageType与语言分组控制器使用原配置。运行时状态、核验和仍未迁移的部分见对应功能／地图文档。

commerce-components/source-manifest.json 补录 TopBarItem、TopBarManager、MePlayerItemUI、PlayerItemUIGetItemIdFactory 原代码输入哈希；沿固定 APK／854_156383 原 PublicTopBar、MoneyUI 及各页面 TopBarHandler 配置复用已有纹理。未添加手绘资源或自行排列货币栏。

commerce-components/source-manifest.json 新增原 CharacterPanelEquipComparePanel 与 ItemDetail/EquipDetailDialog，累计30 prefab、218纹理，按固定APK／854_156383来源解码。补录 FirstPayWeaponPreview 原代码输入哈希；物品样本使用已迁移的原 EquipInfo.GenerateItemParams 规则和零索引／零所有者缓存语义，未绘制或设计替代窗口。

commerce-components/audio-source-manifest.json 登记固定854_156383的minimum／UI声音与BGM原包MD5、SHA256、原Wwise引用／事件对象和ButtonSound／UIClickSound／PopupWindowSound／PopupWindowBgmController／BgmManager原代码输入；运行目录 audio/catalog.json 保存30个原prefab声音链接及16个事件动作，10条原声音经wwiser／vgmstream转换Vorbis，保留原源音量、剪辑、循环。规范Ogg容器序号及校验和仅消除编码器随机元数据，实际输出哈希逐项复建核验。不存在额外设计的商城BGM或替代首购音效。通用原ConfirmDialog加入后，commerce-components/source-manifest.json覆盖32 prefab、218纹理。

commerce-components 来源清单补齐固定854_156383的 Selectable 原 ColorBlock／SpriteState／触发器、Btn_Base_Click／ToggleAnimatorController 状态机与实际覆盖Clip、PopupWindow 三类动作引用、CanvasGroup及 MainCanvas 的 Mask／BlurMask。原 PayDirectPayItem 独立纳入转换，当前33 prefab、222纹理。commerce_animation.py 只接受原单片段状态及 WriteDefaultValues 分支；保留原触发器、转换时长和曲线，不自制替代动作。

沿固定854_156383的 AutoAlignForSingleMultiLineText.AutoAlign 与 ConfirmDialog.OnContentTextRefresh 迁移文字布局完成后的单／多行判定、原514／513对齐值和提示同步；原两类确认 prefab 的字段由 commerce-components/source-manifest.json 登记，补入两份实际C#输入哈希。富文本沿相同布局判定，不用强制居中标签覆盖原多行左对齐。此项不代表 TMPEffect 双描边／阴影已还原；shader 原 GLSL 已从 defaultpackage_unityshaders.bundle 提取为开发输入，受保护原生方法的字段到uniform映射仍未恢复，未自拟百分比换算或阴影公式。

商城组件、首购原show_01开发期转换、原Wwise音频的三份来源清单和运行资源补入37a8f1db943eb3cb素材包；以2adebc529c12e803已提交包为基准，仅追加本会话91个文件，601项相关文件／导入描述对照来源及目录核对，其余原包内容保持逐项一致。全局显式绑定补入53项，合并三组catalog后的302条运行资源均有绑定；完整包与清单哈希见现行lock及全流程文档。素材包的创建不表示TMPEffect参数映射及原越界事件已解决。

Wwise转换输入及十份原音频内容保持不变，运行时改走 official_resources 显式导入资源加载后复制并设置原循环参数；不再依赖发布目录不存在的原始Ogg文件。正式分包中302条商城资源和十份音频均直接挂载、读取／解码通过，未增自绘或替换音频，素材包仍为37a8f1db943eb3cb。

prepare_commerce_components.py 与 commerce_scroll.py 从固定 prefab 转换九处 ScrollRect／LoopVerticalScrollRect／LoopVerticalScrollRectMulti 的原序列化指针及数值；EventSystemManagerConfig → EventSystemProxy 对应 public bundle 142e02f2b39cf67da8e4bb0e4c17167b，EventSystem path_id 5730249452741529571，dragThreshold=5。原输入对象、配置及 EventSystemManager／Config、ClampingScrollRect、已反编译 LoopScrollRectBase／LoopScrollRect 的输入哈希追加在 commerce-components/source-manifest.json；Multi 继承 LoopScrollRectBase 与固定 metadata 对齐。资源像素和302条动态加载路径未新增，素材包只替换该来源清单；lock.json 指向 ca29c4a721b9fc8a，完整SHA256 ca29c4a721b9fc8abf595bc03c911c5161979c3127ad1dc9ce6792cea9acec52，其余基包内容逐项保留。运行验证与字体／动画时序边界仅记对应功能／地图文档。

`commerce_typography.py` 读取固定 manifest 854_156383 的 `defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle`（bf62732f37a5a423ddbac02681e054a4）中 `FZY4K SDF - dynamic.asset`（-1054575886599457342）、材质8668477128756491714、`MultiLanguageFontConfig.json`（-6461299786733695581）。pointSize40、atlasPadding4、GradientScale5均由原对象核验；中文版字体替换调用来自原 `MultiLanguageText.RefreshFont`。原字体仍为已有 `assets/official/FZY4K.ttf`，未写入新字体或替代贴图。

运行时 ／ 依据已恢复的原 `TMProWarp.Refresh` 0x1121e07c、`TMPEffect.DoSetup` 0x1122f780、材质回调0x1123083c及 `ShaderUtilities.UpdateShaderRatios` 0x1126eb34；shader来自固定 `defaultpackage_unityshaders.bundle`（cac97355bb4799c415e3cb1ef38b6dc5）的 `Hidden/JSZX/TextMeshPro`（-4286560471751938018）GLES3程序。原规则／参数不作视觉调参。三份原Warp序列化曲线及字体／材质输入哈希纳入 `assets/reference/commerce-components/source-manifest.json`。

固定 APK arm64-v8a/libunity.so 原条目22360168字节，SHA256 5142b00b7ef6fa53828de13afd2cfd07a6a7763100d145cbb50a22e0114a0340，缓存 .tools/source-packages/official-code/libunity.so。read_official_native.py --engine 根据原 AArch64 icall 注册指令定位 AnimationClip::get_length 0x5755e0、时间范围0xf9c434／0xf9e900及 GetEventsInternal 0xa2bc84；commerce_clip_time.py 按原 Legacy 曲线／事件范围转换。原 AccumulatedPayCoreMulltiAwardGo_Loop 来自 defaultpackage_share_b2696e917c258280310268fe4e4132b1.bundle（MD5 71707dca67f83ef62156a5fe28e0bf81），对象6352346077181223357，Legacy／wrap2／sampleRate60，曲线末尾1.6666666269302368、SwitchItem事件1.8333333730697632；原引擎将事件计入长度。引擎和测试夹具仅作开发输入，不进入运行资源包；来源清单登记真实引擎SHA与方法链。

固定 FZY4K SDF - dynamic.asset 的原序列化值为 m_ItalicStyle=15、FaceInfo.capLine=38.040000915527344、pointSize=40；15为斜切参数，原生成网格指令乘0.01使用，不作角度换算。34个迁移 prefab 中九处非零字体样式（五斜体、四下划线，包含原隐藏模板）来自各自原 TMP 序列化字段，权重均为400。GenerateTextMesh 0x11218a90–0x11218b4c 和原 GlyphMetrics getters 0x11443a8c／0x11443a7c 提供上下边斜切及半差平移公式；运行时对原字体四顶点应用同样计算。

原日礼包 ui_daily_pay_gift_buy_all_discount 使用 <midsize=42>；固定客户端 TMP_Text.ValidateHtmlTag 0x1125a540 的分支0x1125e08c、字号／栈处理0x1125f454、偏移0x1125f4e4和关闭恢复0x1125e64c已取得。基线偏移按 (基础字号−当前字号)×0.5×capLine／pointSize，并沿原正交开关保留缩放分支；GenerateTextMesh 0x11217e70／0x11218134实际读取此偏移。动态格式化保留原标签。以上方法均来自已登记固定 APK 恢复映像，read_official_native.py 读取时另校验该映像SHA256。

辅助阅读使用 Unity 官方 com.unity.textmeshpro 3.0.6 公开源码，下载URL https://download.packages.unity.com/com.unity.textmeshpro/-/com.unity.textmeshpro-3.0.6.tgz，SHA256 1ce172027b906a30be33cefe7b2ee46e1c8d35f729359b8b9785fc120d57b637；只用于定位原生结构，不代表固定客户端版本或其 midsize 自定义方法来源。

`scripts/prepare_unity_commerce_entry.py` 从固定 manifest `854_156383` 已提取的 `assets/official/ui.json` 合并官方 `FunctionEntryDialog`、`FunctionEntryShopItem`、`ActivityCommonEntry`、`ItemDetailDialog` 及商城直购顶部 `AccumulatedPayEntryGo` 子树到 `unity/Assets/Official/Commerce/commerce-ui.json`；所需 32 张图片逐项按 `assets/reference/official-city/source-manifest.json` 的原 bundle／对象／SHA256 复制到 Commerce 载荷，生成缺失的 `.meta` 但未改原图或生成替代图。`scripts/prepare_unity_commerce_catalog.py` 继续从 `commerce-ui.json`、`rules.json`、`settings.json` 与 `item` 表生成 `commerce-runtime.json`，新版本把每棵 prefab 树展平为 `nodes` 并记录 `has_image/has_text/...`，避免 Unity `JsonUtility` 对深层可选成员的默认实例化；同时补齐 `ImgBg_Recharge-993940c86f`、`ImgBg_First-995290a9ab`、`ImgBg_Additional-b0eb862c9a`、`ImgIcon_PeriodicGiftIcon_5-ccd318ec1a` 等原 prefab 实际引用的官方 Sprite。来源仍为固定 APK／匹配原版本 CDN，未使用新版同名包，未自绘图片。

运行时 `OfficialCommerceAssetCatalog.asset` 由 `Zjcs.Editor.Commerce.OfficialCommerceAssetTools.Validate` 生成，现绑定 350 张官方图片、169 个奖励物品、10 个官方音频事件与 27 个 prefab（含官方 `ItemDetailDialog`）。月卡补入的累计奖励品质框／锁图来自 `defaultpackage_assets_image_ui_pay_accumulated.bundle`（固定 manifest `854_156383`，MD5 `e1ed8b9eb1cda15317920de572ee33fb`）；物品详情品质框来自 `defaultpackage_assets_image_ui_itemquality.bundle`（MD5 `74e6afc6b39b88d0e8977d8f12267b52`）；`AccumulatedPayPanel_Show` 原生压缩曲线来自 `defaultpackage_assets_prefabs_ui_pay_accumulatedpay.bundle`（MD5 `0b11bb5781ce56f124f55e08687be9d7`，pathID `-4611456846825482089`），由 Editor 校验并转换为 Legacy AnimationClip。具体 Sprite 哈希及映射记录于 [commerce-ui/source-manifest.json](../../assets/reference/commerce-ui/source-manifest.json)；HUD 首购入口和活动菜单项为官方 `AccumulatedPayEntryGo`／`ActivityCommonEntry` 结构及既有官方图片的重新组合，原 `FirstPayIconEntry` prefab 未在当前 UI 载荷中，因此不声称首购 HUD 图标为原 prefab 一比一复刻；入口位置、标题和红点状态为本地适配。详细实现、验证范围与剩余表现差异只记录在 [商城功能页](commerce.md)。

## 完整还原部分

### 资源与目录

- `OfficialCommerceAssetCatalog.asset` 已生成并随 `Resources/Commerce` 交付，绑定 317 张官方图片、169 个奖励物品、10 个官方音频事件和 26 个 prefab；生成入口为 [`OfficialCommerceAssetTools.Validate`](../../unity/Assets/Game/Editor/Commerce/OfficialCommerceAssetTools.cs)。
- 运行目录由 [`prepare_unity_commerce_catalog.py`](../../scripts/prepare_unity_commerce_catalog.py) 从固定版 `rules.json`、`commerce-ui.json`、`settings.json` 生成；每个 prefab 展平为 `nodes` 并记录 `has_image/has_text/...`，避免 Unity `JsonUtility` 把缺失的可选组件实例化为空对象。加载与重建见 [`OfficialCommerceAssetCatalog.Load`](../../unity/Assets/Game/Runtime/Presentation/Commerce/OfficialCommerceAssetCatalog.cs) 与 [`OfficialCommerceCatalogView.BuildTree`](../../unity/Assets/Game/Runtime/Offline/Commerce/OfficialCommerceCatalogView.cs)。
- [`prepare_unity_commerce_entry.py`](../../scripts/prepare_unity_commerce_entry.py) 把官方 `FunctionEntryDialog`、`FunctionEntryShopItem`、`ActivityCommonEntry`、直购顶部 `AccumulatedPayEntryGo` 子树及所需官方图片合并进商城载荷；来源和哈希记录在 [commerce source-manifest](../../unity/Assets/Official/Commerce/source-manifest.json) 与 [SOURCES.md](../../assets/reference/SOURCES.md)。

### HUD 入口

[`OfficialMainlineMapRuntime.BindWorldControl`](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs) 通过功能入口接入商城：Map11 先打开原 `FunctionEntryDialog`，商城按钮再调用 [`CommerceScreenController.OpenShop`](../../unity/Assets/Game/Runtime/Presentation/Commerce/CommerceScreenController.cs)；Map10 仍从功能入口直接打开商城。Map11 的首购 HUD 已替换为从固定版 APK 提取的原 `FirstPayBtn`，保持原图标、文字、按钮和 LayoutElement，由 WorldCanvas 原纵向布局定位；来源为 `unity/Assets/Official/World/Map/11/UI/source-manifest.json`。首购可见性继续使用现有 `CommerceHudStatus.FirstPayShouldShow`，原支付功能门槛与完整提示图绑定尚未迁移。Map10 仍使用旧 `commerce_first_entry`，其完整还原待后续处理。

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
- 奖励图标使用官方 `commerce_item_detail`（`ItemDetailDialog`）显示图标、名称、品质、数量、基础说明和关闭按钮；奖励图标由 `CreateItemIcon` 统一加点击入口。
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
- Map10 HUD 首购入口仍由 `AccumulatedPayEntryGo` 组合；Map11 已恢复原 `FirstPayBtn` 外观，但原支付功能门槛及完整提示图状态绑定、真实鼠标打开和 Windows Player 尚未验收。
- 活动页四个入口按官方 `ActivityCommonEntry` prefab 生成，但每日／每周／首购／兑换的图标、标题和位置是本地组合，未接入原活动入口表、活动开放条件或动态活动目录。
- 首购预览使用官方 `ImgItem_FirstPay/FirstPay1/FirstPay2` 武器图，未恢复原角色模型预览、武器属性比较和装备详情弹窗。
- 星眷页现展示全部 15 组、75 个档位与分组标题；每档仍用运行时组合行（等级、星值门槛、核心奖励图标、领取按钮），未恢复原完整 LayoutElement、进度线、锁定展示项、主副奖励分区和多奖励轮播。
- 规则／价格表内容使用官方面板和官方文案，但行布局由运行时生成；物品详情只使用官方 `ItemDetailDialog` 图标、名称、品质、数量、基础说明和关闭，未接使用／分解／来源／限时等原交互；活动入口、TMPEffect、TMProWarp、原生动画、粒子和大部分按钮音效未恢复。
- 物品详情顶部白色装饰和中部空白区仍未按原 `ItemDetailDialog` 脚本完全校正，当前是可用的基础版，不声称与官方最终版布局一致。
- 十进制金额购买是离线模拟，不接真实支付；代金券按本地库存扣除；周期卡、月卡属性、赛季换组和原服务器订单不接。

### 已确认的本地接线与边界

commerce-components/source-manifest.json 新增 ConfirmDialog/MarkableConfirmDialog，累计31 prefab、218纹理；补录 WeeklyPayGiftShowContext、DailyPayGiftItem、PayGiftIconUtils、PayVoucherUtils 原代码输入。Toggle 原对象344字节布局严格校验后解析原graphic／transition／isOn，原 UIToggle 与 MarkableConfirmDialog 调用链决定交互；其底层0.1秒graphic渐变与Unity官方[UGUI Toggle实现](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/Toggle.cs)核对，未用新版资源替换原包。现金SDK替换为本地原窗口确认属于用户批准的离线适配，其余确认／兑换控件、文案、图和规则沿固定854_156383原内容。

## 尚未实现

### 后续范围与已知差异

- 月度奖励、七日祈愿和星眷完整页面；周期卡页签当前按锁定功能隐藏。规则和缺项见[月卡与星眷](monthly-card.md#尚未实现)。
- 原活动入口目录、活动开放条件、每日／每周红点 agent、活动页布局和动态活动资源。
- 首购原角色模型／骨骼预览、武器比较、`FirstPayPreviewAppearance` 动画和 `FirstPayDialog` 原生动画。
- 物品详情的原使用／分解按钮、来源列表、限时说明、属性详情和货币余额点击详情；当前奖励图标点击可打开基础物品详情，但不执行物品操作。
- 原 prefab 的 Animator／Animation、PopupWindow 开关动画、UIBloom、TMPEffect／TMProWarp、原生粒子、字体材质和全部按钮音效；仅播放 `Main_Cursor_Normal`。
- 星眷原 15 组完整 LayoutElement、主副奖励分区、多奖励轮播、进度线和末组锁定项；当前已展示全部组与档位，但行布局为运行时组合。
- 价格表行内容由运行时组合，当前页面顶部／底部裁切和滚动定位仍未按原 `PayInfoTableDialog` 脚本校正。
- 活动页入口视觉、礼品图标内数量文字、首购顶部白色信息区、每日整组价格区的最终版式仍需按原 prefab 校正。
- 真实磁盘保存失败、只读目录、断电中断和 Windows Player 运行未验证；当前只验证保存委托失败回滚和正常磁盘重开。
- 新增/变更的 `unity/Assets/Official/Commerce` 入口图片、`commerce-ui.json` 和生成目录尚未纳入完整素材 ZIP 快照；未重建素材包，不声称这些新增载荷已备份。

## 当前验证范围

- `python -B scripts/check_unity_project.py` 通过：27788 个 GUID 的静态元数据检查通过；该检查不代替 Editor 导入、编译或运行。
- 隔离 Unity 工程（`D:/Desktop/zjcs-commerce-acceptance`，复制当前 `Assets/Packages/ProjectSettings`）使用 `Unity 2022.3.57f1c2` 完成 Editor 导入与 C# 编译，`CommercePlayModeAcceptance` 通过：官方主窗口／直购／代金券／每日／每周／首购／星眷／兑换 prefab 实际创建，代金券支付、免费赠礼、单买／整组互斥、首购激活与领取、兑换、保存重开，以及另一玩法新增库存后商城购买不清零。该验收在隔离工程把不相关的 Map10 运行入口替换为桩，商城代码本身未裁剪；共享工程当时被另一模块未完成的 `HomeHudView.TreasureGacha.cs` 编译错误阻塞，不能据此声称共享工程已完成 Editor 验收。
- Play Mode 实际捕获 14 张画面并逐张检查：直购主页、价格表、确认框、物品详情、活动页、星眷全组页、每日礼包、每日规则、每日已购、每周礼包、首购、首购已激活、兑换、重开。画面确认官方角色立绘、标题、六档商品、每日三档、每周六档、首购三天奖励、兑换滑条和官方按钮均实际渲染；同时记录活动入口文字、礼品图标数量、首购顶部信息区和每日整组价格区仍有版式差异，不把差异写成官方最终版。
- 验收的按钮输入通过 `ExecuteEvents` 向真实 Unity UI 控件派发点击并等待界面状态，不是直接调用业务方法；尚未用操作系统鼠标覆盖遮挡、滚动、拖拽和键盘焦点。
- HUD 入口逻辑另由 `CommerceHudPlayModeAcceptance` 在 Play Mode 验证：官方 `WorldCanvas` 的 `FunctionEntryPhoto/EntryBtnFolded` 点击后实际打开商城，首购入口创建成功、标题为「首购」且红点显示。该验收只挂载官方 Map10 UI 与商城运行入口，不进入完整 Map10 地图场景；HUD 在真实地图中的遮挡、层级和最终位置仍未验收。
- 商城专属 Windows Player 交互尚未验收；开场战斗版本包含已提交的商城编译依赖修复，不据此判定商城完整玩法已通过 Windows 验证。

累充面板的 `AccumulatedPayPanel_Show` 已从固定官方 bundle 经 AssetRipper 重建为 Unity 原生 AnimationClip，保留压缩曲线恢复结果，按现有 Animation 播放器设为 Legacy；编辑器优先加载该本地资产，避免直接反序列化 Android bundle 的 `2022.3.57f1`／`2022.3.57f1c2` 版本冲突。来源与输出哈希见 [动画来源清单](../../assets/reference/commerce-components/accumulated-panel-animation-source-manifest.json)。该依赖修复不代表商城其余动画已完成。

开场战斗发布所需的商城数值转换、周期卡接口和原生动画事件接收已纳入提交，并通过所用基线的 Editor 编译、`ProjectTools.Validate` 与 Windows 构建。周期卡业务及属性刷新范围见[月卡与星眷累计奖励](monthly-card.md)；验证流程以开场战斗为主，商城专属流程仍沿用本页所列验证边界。
