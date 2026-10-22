# 技能祈愿、材料购买与兑换

[总索引](../README.md)

状态：未完成

## 功能简介

女神像或技能页进入常驻／高阶祈愿，范围包含职业选择与自选、技能池、概率与保底、积分、馈赠、兑换、材料购买、详情、历史，以及对应 UI、动画、特效和音频。幻兽及古遗物奖池分别见[幻兽召唤](pet-gacha.md)和[古遗物寻宝](treasure-gacha.md)。

本页同时记录固定 manifest `854_156383` 的原始官方功能和 Unity / C# 待迁移内容。下列官方行为是还原依据，不代表当前游戏已经实现；祈愿仍未形成可运行的 Unity 完整流程。友情祈愿属于原官方总页，保留其功能记录，但离线开放范围另见“自定义实现”。

客户端源码根目录为 `.tools/source-packages/official-code/client-source/`，共享源码根目录为 `.tools/source-packages/official-code/common-source/Common/`；下文类名均相对此入口。表数据见[原规则配置](../../assets/official/rules.json)，页面布局见[原 UI 转换数据](../../assets/official/ui.json)。

### 原始官方功能：总页与抽取入口

| 编号 | 原始官方行为 | 源码／配置定位 |
| --- | --- | --- |
| W01 | 总页包含“祈愿／兑换／馈赠”，祈愿及馈赠区分常驻、高阶、友情。检查各箱子的 `OpenChest` 功能条件，隐藏未开放页签；默认常驻，按开放数量控制横向滚动与拖拽，动态加载对应页面，维护红点、顶部货币、选中页和关闭清理 | `GachaPanel`、`GachaLoopView`、`AchievementPanelWithTypes`、原 `GachaPanel.prefab` |
| W02 | 常驻 `GachaType.Skill=0`，箱子 `6000`，每抽消耗 `110×1`、获得积分 `251×1`；支持 `1/10/100` 次。读取已解锁的各分支最高阶职业，默认当前职业；切换后更新奖池、规则提示、自选入口及职业消息 | `SkillGachaPanel`、`GachaPanelBase`，`gacha/chest` |
| W03 | 高阶 `GachaType.SkillSenior=2`，箱子 `6002`，每抽消耗 `118×1`、获得积分 `253×1`；支持 `1/10/100` 次。职业列表取全部已解锁职业，当前职业优先，其余按阶级降序及原职业顺序；对应职业名、颜色、提示和女神像皮肤同步变化 | `SkillSeniorGachaPanel`、`FurnitureUISkinProfessionExtension` |
| W04 | 两种技能祈愿按持有材料刷新单抽及十／百抽按钮；能支付百抽时显示百抽模式，不擅自增加配置外的任意次数。抽取前校验每日限额、自选完成状态、非当前职业确认、功能禁用及是否已存在结果窗口；发送前再核对限额 | `GachaPanelBase`、`PlayerOpenSkillChestBtn`、`SkillGachaPanel.OnSkillGacha/GachaSend` |
| W05 | 百抽先弹确认，可勾选“今日不再提示”；按祈愿类型保存忽略时间，同日有效，取消不抽取。三种女神祈愿均为 `GachaClassType.GoddessGacha=0`，共享类别每日上限 `10000`，不是每个页签各有一万次 | `GachaPanelUtils.HundredGachePreRemind`、`GachaBaseSetting.LastIgnoreRemindHundredTickDict`、`GachaTodayCountLimitUI`、`DailyGachaMaxCountDict` |

### 原始官方功能：职业自选、技能池与概率

| 编号 | 原始官方行为 | 源码／配置定位 |
| --- | --- | --- |
| W06 | 常驻达到职业阶级 `8` 后开启前置职业自选，需选 `4` 个职业。候选为所选职业的前置链，排除自身，按阶级降序排列；可选／取消，满额阻止继续添加。按职业分支恢复既有选择，去重、截断、排序；弹窗草稿与已提交状态分离，未满额确认有提示，提交中禁用按钮，成功关闭、失败恢复操作；未完成时抽取返回原提示 `61903` | `SkillGachaCustomProfessionSelectionEntry/Panel/Group`、`PlayerSkillGachaSetCustomSelectionReq`、`SkillGachaCustomSelectionSettings` |
| W07 | 常驻按所选职业及可用前置职业筛选可祈愿技能；`isGacha:true` 使用职业 `Skills`，不能把仅在 `FightUsedSkills` 的技能加入。启用自选后取当前职业及各选中职业的可抽技能并集。高阶严格筛选 `SkillInfo.UnlockProfession == 所选职业`；技能碎片先还原技能 ID 再判断 | `ItemFilterForProfessionSkill`、`ItemFilterForProfessionUnlockedSkill`、职业与技能表 |
| W08 | 奖励规则递归展开随机包、移除无效项，按等级／突破阶缩放数量，职业过滤后重新归一化；展示时合并相同物品和数量并按品质／类型分组。抽取池不能替换为所有技能均匀随机，概率页也不能显示未过滤原包 | `RewardRuleInfoParser`、`RandomRewardInfoParser`、`GachaProbabilityContent/Item` |
| W09 | 品质保底、连续未中记录、职业阶级整技保底及碎片保底分别维护；面板剩余次数、详情规则和结果后刷新使用对应记录。十抽／百抽仍需逐抽更新记录，两个技能池各用自己的共享记录 ID | `PlayerRandomRewardPreviewerUI`、`PlayerRewardRecordPreviewer`、`SkillGachaLowRankRecord`、`SkillGachaLowRankPieceRecord`、`SkillGachaSettings` |

常驻规则 `20000`、高阶规则 `21000` 均依次指向 `50302/50301/50300/50252`，权重 `100/700/2200/7000`，对应传说整技、史诗整技、稀有整技、普通技能碎片；基础类别概率为 `1%/7%/22%/70%`，碎片包每次 `3` 片。具体技能继续按随机包权重及职业筛选，不能据类别概率推断池内每个技能都等概率。

两条规则分别配置 `IndexMininum=[70,30,10,0]`、`RecordSharedId=20000/21000`。默认 `EnsureHightRankConfig` 对品质 `4/3/2` 均为 `3`，`EnsureHighRankPieceCount=3`；职业阶级 `1～4` 的覆盖配置为 `3/7/7`、碎片 `7`，其他阶级走原配置选择函数。常驻详情的职业阶级保底文案对配置次数加 `1`，面板品质剩余次数则沿 `IndexMininum - GetKeepMissScore`；二者不能混用。高阶不使用常驻的职业自选及其自定义规则提供器。

`gacha_pity_probability` 对两个技能池配置 `241/790/2400/6569`，`Formula.Long2Double` 除以 `10000`，对应保底综合展示概率 `2.41%/7.90%/24%/65.69%`。`GachaProbabilityItem.SetPityProbability` 遵循 prefab 的地区隐藏字段；是否显示须恢复实际序列化配置。这组展示值不等于逐抽权重，也不能据此拟合未知动态概率。

### 原始官方功能：积分与馈赠

`W10` 积分条由 `ScoreRewardUI` 读取下一未领奖档位，进度相对前一档门槛计算；显示当前奖励、首档附加提示、可领取／未达成／已领取状态。奖励图标可打开物品信息，积分详情通过 `ScoreRewardDialog` 打开原成就列表和规则文案。`W11` 馈赠是累计抽取次数奖励，与积分领奖分别维护，通过总页切换对应祈愿类别，恢复原成就列表、完成状态、红点及领奖反馈。

原 `achievement` 表的全部相关模板如下。`物品ID×数量` 沿原配置，循环段由 `AchievementInfo/CycleTemplateInfo` 展开；`CycleCount` 是原模板参数，不应擅自解释为无限循环或任意周期。

| 范围／类型 | 分组及原模板 ID | 初始门槛与奖励（按列出顺序对应） | 后续模板与循环参数 |
| --- | --- | --- | --- |
| 常驻积分 `20` | Group `6`，`6001～6010` | `200/250/300/350/400`；首档 `6800×1`，其余 `118×5` | `6005.CycleCount=2800`；`6006～6010` 每档增 `50`、奖励 `118×5` |
| 高阶积分 `60` | Group `8`，`8001～8010` | `50/100/150/200/250`；均 `4×1` | `8005.CycleCount=1600`；后五档每档增 `50`、奖励不变 |
| 常驻馈赠 `7` | Group `5`，`5001～5014` | `10/20/30/40/50/60/70`；`100×3/101×3/102×3/301×2/301×2/302×2/303×2` | `5007.CycleCount=10000`；后七档每档增 `10`，奖励 `301/301/302/301/301/302/303` 各 `×2` |
| 高阶馈赠 `61` | Group `7`，`7001～7008` | `10/20/30/40`；依次为 `(301×2,302×2)`、`(301×2,303×2)`、`(301×2,302×2)`、`(301×2,304×1)` | `7004.CycleCount=10000`；后四档每档增 `10`，奖励重复上述四组 |
| 友情积分 `59` | Group `19`，`19001～19008` | `30/60/90/120`；`1100/1110/1120/1131` 各 `×1` | `19004.CycleCount=100`；后四档每档增 `30`，重复上述奖励 |
| 友情馈赠 `62` | Group `18`，`18001～18010` | `10/20/30/40/50`；均 `301×2` | `18005.CycleCount=200`；后五档每档增 `10`、奖励不变 |

领奖沿 `OperPlayerGetAchievementReward(group,1,receiveSameType:false)` 等原调用与 `AchievementPanelBase` 行为；通用显示数配置 `AchievementPanelShowCount=5`，不能据模板数量自行添加“一键领完”。馈赠奖励消息按 `SourceOperate.Achievement=18`、`AwardStyle.Popup=2` 打开 `AwardPanel`；技能商店商品另按自己的奖励窗口类型处理。

### 原始官方功能：详情与历史

`W12` `GachaDetailDialog` 提供概率／历史标签，按 `RecordHistory` 控制历史入口，显示对应标题、规则、类别和每日次数说明。常驻 `GachaDetailInfoSkillGachaProvider` 按 `max(职业阶级,1)` 选配置，自选开启后附加原规则；高阶不套用该常驻自定义设置。概率分组展示类别总概率、品质和物品图标；`GachaProbabilityItem` 仅为友情祈愿显示逐物品百分比，基础概率汇总按原精度处理，物品点击沿原详情。

`W13` `GachaHistoryContent/Item` 按祈愿类别加载分页，打开从第 `1` 页开始，使用 prefab 的每页数量；换页取消旧异步任务，按返回总页数更新翻页控件。每行显示物品名、类型、本地日期时间和品质颜色，不能用抽取按钮次数伪造历史行。`PlayerGachaHistoryManager` 分类缓存上限 `1000` 条，配置记录期 `180` 天；服务端分页返回契约需要由本地记录读取适配，不能把缓存上限直接当作永久总记录上限。

### 原始官方功能：结果、动画、音效与重复转化

| 编号 | 原始官方行为 | 源码／资源定位 |
| --- | --- | --- |
| W14 | 结果面板消费当前祈愿 `SourceOperate` 的真实奖励消息（常驻 `75`、高阶 `87`、友情 `86`），从展示列表滤除积分物品但仍结算入账；按实际展示奖励数选择单抽、十抽、百抽表现。空奖励走原异常分支，不能播放假成功；顶部货币、继续抽取、关闭和每日次数同步结果 | `GachaResultPanel`、`GachaPanelBase` |
| W15 | 单抽区分整技／碎片，显示品质背景、名称、职业图标、技能描述／标签与详情链接，新技能提示，渐显、星光、音效及转化延迟；技能碎片详情在结果中隐藏底部合成操作 | `GachaOneRewardGo`、`GachaResultPanel`、`ItemAwardIconGodGacha` |
| W16 | 十抽按星光动画事件填槽与播放品质音效，结束补齐未显示槽位，再按原顺序执行转化；百抽沿原行模板、滚动列表、播放缓存、自动滚动和转化时序；跳过结束演出并补全最终结果，不重抽、不变更数量 | `GachaResultAnimPlayer`、`GachaTenRewardGo`、`GachaHundredContent`、`GachaHundredRewardGo` |
| W17 | 新整技、重复技能转碎片、回收为代币、碎片与代币同时出现分别有图标、数量、过渡效果与声音；以奖励数据的 `Agiotage/RecycleAwards/GetDetailAward` 为依据，不由表现层重新决定奖励 | `AwardData`、`ItemAwardIconGodGacha` 的四种状态、`GachaTenRewardGo` |
| W18 | 演出结束恢复按钮组、刷新保底提示及每日次数，处理额外奖励处理器；关闭／重开时取消主分支异步任务、解绑事件、回收槽位并清理粒子。原额外奖励表当前为空，不能自行添加额外奖励 | `GachaResultPanel`、`GachaAddtionalAwardHandler`（原拼写）、原动画事件、`gacha_additional_awards` |

物品表另提供重复与回收依据：`CanRepeat/AgioItemId/AgioAmount/AgioRuleIdList/RecycleReward/RecycleGroupCount`。例如技能 `10000` 转 `12000×10`，`10001/10002` 分别转对应碎片 `×30`，`10003` 转对应碎片 `×90`；碎片 `12000` 的回收组数量 `1`、奖励 `{20:1}`。这些配置与客户端展示分支不能单独证明服务端对已满阶技能、部分升级及溢出碎片的全部裁决顺序；接入时须与[技能养成](skills.md)及[背包](inventory.md)共用已确定规则，未确定的服务端分支继续查证。

### 原始官方功能：兑换商店

`W19` `SkillExchageShopParent`（官方类名如此拼写）将总页兑换接到商店 `4/5`，按功能条件切子页，初始化／打开对应商店、设置真实语言标题并清除新标。顶部使用原 `PublicTopBar/MoneyUI`；这里不是付费代金券商城，不能用 prefab 中未刷新的静态文字替代运行时标题。

| 编号／商店 | 原始官方规则与交互 | 源码／配置定位 |
| --- | --- | --- |
| W20／商店 `4` | 固定商品 `376` 项（`shop_goods` 的 `2500～2875`），按原商品逐行读取数量、价格、库存、购买条件和限购；无随机组、无手动刷新次数、无周期刷新，随职业重建。列表按已解锁职业筛选并按职业与物品类型分组，支持原品质／元素／类型／职业过滤，默认采用当前职业前置链，不混入不可用技能 | `ShopSubPanelForSkillPiece`、`ShopInfo.GenerateFixedGoods`、`shop/shop_goods`；例如 `2500` 为 `12000×30`、消耗物品 `4×1`，不能据此把所有商品价格写死 |
| W21／碎片详情与购买 | 打开原物品／技能详情并隐藏来源入口；未拥有显示合成需求，已拥有且未满阶显示升阶需求，满阶按原状态隐藏。支持原数量滑条、可负担上限、货币总价与不足颜色、确认／取消；数量乘商品每份数量，按共享购买请求生成器处理库存、限购及奖品窗口 | `SkillPieceShopDetailDialog`、`ShopUIUtils`、`PlayerBuyFromShopRequestList` |
| W22／商店 `5` | 原组 `54001/53001/52001` 各生成 `1/2/6` 格，共九格，按职业过滤；组内递归权重继续解析 `shop_goods_group`，不能按商品列表均匀抽样。日刷新货架、独立格库存、售罄和购买反馈；切页或重开不得任意重掷 | `ShopSubPanel`、`ShopInfo.GenerateRandomGoods`、`ShopPanelGoodsItem`、`ShopGoodsItemIconSkillPiece` |
| W23／刷新 | 每日最多手动 `10` 次，依次消耗物品 `2` 的 `50/100/100/150/150/200/200/200/200/200`；显示剩余次数、下一刷新倒计时、可刷新／不可刷新／次数用完状态。原消耗物品 `2` 需确认，取消不刷新；不足走提示与失败音效，成功才刷新并播放原音效；底部区域按刷新能力决定显隐 | `ShopPanel`、`ShopInfo.GetRefreshCost/GetNextRefreshTime`、`OperPlayerShopManualRefresh`、`ShopRefreshCoinNeedConfirm` |

原随机组 `52001` 含子组 `50001:600、50501:300`，`53001` 再含 `51001:100`，`54001` 再含 `51501:50`；仍需按子组、条件与职业过滤递归展开。固定生成条件与购买时条件分别沿原 `Condition/BuyCondition` 校验。周期计算沿原 `ShopRefreshCycleType/TimeUtils`，离线时用本地时间与已确认时钟策略，不另造刷新周期。

### 原始官方功能：材料来源与快捷购买

`W24` `PlayerOpenSkillChestBtn` 在不足时计算差额，沿 `QuickBuyUtils.TryGetQuickBuyInfo` 检查原购买配置、条件及价格；存在可用快捷购买才进入对应确认流程，否则提示材料不足／原物品来源。`GachaPanelBase.PopupCurrencyPurchaseDialog` 显示缺少数量、购买所得、货币与总价；`ShopBuyIntent.PopupConfirmDialog` 再检查货币，成功才执行购买和成功回调，取消不扣款。购买与抽取为各自步骤，不能自行增加“补足后自动抽取”。

`W25` 原 `QuickBuyDialog` 支持数量增减、滑条、允许时的最大按钮、持有量、每份／合计成本、可负担上限以及限购价格档位；价格按已购买数量及 `PriceBuyCount` 等原字段计算。`ClientItemSourceQuickBuyHandler` 与 `QuickBuyNavigateHandler` 负责物品来源及顶部货币加号导航，需检查购买资格、刷新持有量和监听生命周期，不能把所有货币加号都接到同一商店。

已提取的三条配置：`110` 每份 `1`、消耗 `2×100`，条件为拥有女神像物品 `4004`；共享配置 `301` 消耗 `2×8`、条件为 `4007`，`107` 消耗 `2×160`、等级至少 `41`，后二者属于相邻召唤模块，不应改成高阶祈愿材料。原 `quick_buy` 共 `26` 行，已核实没有 `118/302/303/304/311` 的快捷购买条目；材料不足不授权擅自给这些物品定价出售。

### 原始官方功能：友情祈愿与引导

`W26` `FriendGachaPanel` 无职业选择，`GachaType.Friend=1`、箱子 `6001`、每抽消耗友情币 `3×25`、获得积分 `252×1`，只支持 `1/10` 次。奖励规则 `20080` 是原货币、材料与随机包组合，不使用技能祈愿的 `1/7/22/70%`。显示本周已获得友情币与上限，订阅刷新事件；同样有详情、历史、积分、馈赠及结果流程，材料不足分支没有技能材料快捷购买。

官方友情币配置包含每周上限 `1000`，助战、组队、队长奖励各 `1`，助战日上限 `3`，互动分享奖励 `5`，周未领取邮件 `1300`。这些只记录原始功能，不能据此在单机新增真人好友、模拟玩家或联网领币。

`W27` 女神像维修／自动维修资格、首次进入、首次抽取及教程路由需连同原家园条件和技能获得引导恢复。配置包含 `SkillGachaGuideFixedRewardId=8`、`SkillGachaNewbieTimes=20`、`SkillGachaOnceSupportTimes=[1,10]`；教程输入与来源见[家园祈愿引导](../../assets/reference/home-wish-tutorial/source-manifest.json)。这些字段不足以独立确定完整新人服务端奖励顺序，不能擅自宣布“前二十抽固定奖励”或自造新人保底。

### 原始官方 UI、资源与加载绑定

`W28` 页面需按实际 prefab 恢复层级、RectTransform、锚点、尺寸、布局组、字体、本地化、职业皮肤、按钮／图标输入、页签、遮罩、滚动、红点及关闭路径。动态职业页、技能／碎片图标、品质背景、弹窗及音效均应列入显式本地加载目录。

| 资源组 | 原始内容与定位 | 当前边界 |
| --- | --- | --- |
| 祈愿主体 | `defaultpackage_assets_prefabs_ui_gachapanel.bundle`：`GachaPanel`、`SkillGachaPanel`、`SkilSeniorlGachaPanel`（原拼写）、`GachaDetailDialog`、`GachaHistoryContent`、`ScoreRewardDialog`、`GachaResultPanel` 等原 prefab；友情页、自选及列表项继续沿原引用绑定 | 七个关键 prefab 已直接读取核实；不是官方内容缺失，也未接成 Unity 祈愿流程 |
| 结果演出 | [结果演出来源](../../assets/reference/wish-performance/source-manifest.json)、[转换数据](../../assets/official/wish-performance/performance.json)：单抽、十抽、百抽行、奖励图标、AnimationClip／事件、粒子、品质效果、Wwise 事件到媒体映射 | 旧转换为迁移输入，需恢复 Unity 原生播放、材质与目标平台效果 |
| 视觉与后处理 | [视觉来源](../../assets/reference/wish-visuals/source-manifest.json)、[转换数据](../../assets/official/wish_visuals.json)、[后处理参数](../../assets/official/battle-post/data.json)、原 [Bloom.cs](../../.tools/source-packages/official-code/urp-reference/Bloom.cs) | 来源明确含旧 Godot 粒子投影、Noise／Bloom 近似；不能把旧 `unconverted_modules=[]` 视为 Unity 迁移完成 |
| 兑换／购买共用组件 | [商店组件来源](../../assets/reference/shop-components/source-manifest.json)、[价格按钮来源](../../assets/reference/wish-button-costs/source-audit.json)、[材料来源入口](../../assets/reference/gacha-item-sources/source-manifest.json)、[快捷购买来源](../../assets/reference/gacha-purchase/source-manifest.json) | 沿原列表、详情、确认、批量设置、价格组件、顶部货币及奖励弹窗绑定；共享模块恢复不等于祈愿调用方已接入 |

## 完整还原部分

尚无本功能在 Unity 完整接入并运行验收的部分。已取得的官方代码、配置、旧引擎转换素材与测试只作为迁移输入。

### 当前实际入口、状态与资源

- `unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs` 将 `StatueOfGod` 映射为家具类型 `5`；`HomeFeatureRuntime.OpenFurniture` 尚未将女神像接入祈愿内部页，而是进入 `HomeHudView.ShowFurniturePlaceholder`。家具模型、贴图及可点击不能代替祈愿玩法接入。
- 当前 `unity/Assets/Game/Runtime/` 未发现接通上述常驻／高阶、积分／馈赠、职业自选、概率／历史及抽取演出的祈愿控制器和服务。`Presentation/Skills/OfficialSkillAssetCatalog.cs` 是技能素材与养成目录，没有祈愿路由。
- `OfflineMainlineSave/OfflinePlayerProgress` 有库存及技能养成数据，尚无完整祈愿保底、自选、领奖、分类历史和抽取随机状态事务。其他模块的库存随机数或开箱计数不能代替该状态。
- `assets/official/ui.json`、`rules.json`、演出及视觉数据未通过祈愿专用原生 prefab、动画／材质和加载目录接通 Unity 玩法。`Assets/Official/Character/Art/wish/` 的特效贴图及家园女神像贴图不构成结果演出接入。

### 已核实的来源与验证范围

固定 APK 经现有 `Apk` 读取器核验；原 Luban 构造器重新解码 `gacha/chest/reward_rule/random_reward/profession_base/skill/item/achievement/game_settings/gacha_pity_probability`，十张表与 `assets/official/rules.json` 一致。上述七个关键 prefab 已直接读取；`wish-performance` 的 `90` 个资源及 `17` 份客户端代码、`wish-visuals` 的 `29` 个资源及 `4` 份行为代码、两份主输出的哈希与现有清单一致。此核对不等于逐项复核全部输入对象或 Unity 运行通过。兑换与购买另按其原表、调用方和既有来源清单核对，未混入新版或自绘替代。

本页记录源码、配置、资源与接线核查范围。祈愿未完成 Editor 导入／编译验收，未进行接通流程的 Play Mode 逻辑、真实输入、画面、连续动画、音频、存档恢复及 Windows Player 验证，不能标为通过。文档补全只检查文档结构、引用与差异，不以工程其他模块的检查或旧引擎测试代替祈愿验收。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

- 用户明确按原配置权重与已知保底离线裁决，不猜测额外动态概率；服务器随机序列未取得，材料、积分、保底、历史、货架和随机状态采用本地原子事务。
- 隐藏抽取／结果／刷新价格黑底，改用深色价格字；原 `CommonBtn_CostItem` 确有黑底，这是已确认的 UI 差异。仍需保留数量、货币图标及可用／不足状态和正确输入命中。
- 服务端免费修复回调缺失时，已有女神像或满足原免费自动维修条件即可只读通过购买资格，不伪写家具所有权。同批重复商品按用户补证允许，各格库存独立，不能擅自按商品 ID 合并库存。
- 友情祈愿的真人好友、真人组队及分享等来源受[离线开放范围](offline-access.md)限制，不提供虚构玩家替代。仅记录原页与规则不构成其单机开放决定；是否保留既有友情币的消费入口，实施前须确认，不能自行补送友情币或开放联网。

### 服务器边界与仍须查证的规则

`SkillGachaPanel.GachaSend` 调用 `OperPlayerGacha`，结果由奖励消息返回；APK 共享代码 `Common.Player.SkillGacha` 只校验配置、记录次数并返回空奖励列表，未包含完整服务端抽取裁决。因此已知配置、客户端显示、服务器返回结构与完整决策逻辑必须分别记录。

尚不能由现有片段独立确定的内容包括：新人固定奖励及其与常规池的完整切换顺序、未知动态概率、满阶／溢出转化的服务端全部分支，以及多个保底同时触发时未被现有规则明确覆盖的优先级。前述“原权重＋已知保底”只适用于已确认部分；实施遇到这些具体分支时继续核对共享实现、配置和调用方，仍无依据则请求规则决定，不能以展示概率、空返回或猜测填补。友情币消费入口的单机范围也不视为已经确认。

## 尚未实现

以下均为当前 Unity 待完成项，编号对应上文原始官方功能；未勾选即未达到完整接入与运行验收条件。

### 入口、抽取与本地状态

- [ ] `W01～W05`：女神像／技能页实际路由、总页三类底部功能、已确认开放的祈愿标签、顶部货币和红点；常驻／高阶单抽、十抽、百抽按钮与材料刷新；每日共享限额、非当前职业及百抽确认、今日免提示、重复点击与窗口保护。
- [ ] `W06～W07`：两种不同职业列表、默认项、切换／关闭恢复与皮肤；自选入口、候选列表、四个槽位、选满／未满提示、提交／取消／失败及按分支保存；所选职业真实抽取池与预览同步。
- [ ] `W08～W09`：递归随机包、过滤与归一化，基础概率、已知保底、整技／碎片职业阶级记录、逐抽计数、提示更新；抽取随机状态与规则版本一同保存，切页、重开、读档不得重新抽取已提交奖励。
- [ ] `W14/W17`：抽取材料、技能／碎片／回收奖励、积分、馈赠累计、保底、每日次数和历史一次提交；失败无部分扣款／发奖，重复事件不重复入账；与技能／背包共用已确定的转换规则，补查上文未确定服务端分支后再实施相应裁决。

### 奖励、兑换、购买与查询

- [ ] `W10～W11`：积分条、首档提示、全部原始及循环模板、积分详情、馈赠分类页、奖励图标／详情、领取状态、红点、领奖防重与原奖励窗口；进度和已领取索引跨重开保存。
- [ ] `W12～W13`：原详情概率／历史标签、职业规则及地区显隐、筛选后类别概率、友情逐物品概率（若获准开放）；分类历史写入、时间／品质显示、分页及任务取消、记录期／缓存语义、重进恢复，不能只放静态概率表。
- [ ] `W19～W23`：兑换商店 `4/5` 子页、固定商品全表、职业与元素／品质／类型筛选、分组列表及碎片详情；合成／升阶提示、数量滑条、原价和库存／限购；九格随机货架、独立库存、重复商品、日自动刷新、十次递增价格手动刷新、确认／不足／售罄与倒计时；货架、刷新次数及购买状态持久化。
- [ ] `W24～W25`：材料不足差额、原快捷购买条件及价格档、数量选择／最大值、确认／取消、货币不足、来源入口和顶部加号；原女神像购买资格及只读免费维修兼容；没有原快捷购买配置的材料继续走原不足／来源流程。
- [ ] `W26`：友情祈愿、积分／馈赠、详情／历史和周币提示尚无 Unity 接入；先确认单机消费入口范围，再接获准部分；真人好友／组队／分享来源按既定离线政策处理。
- [ ] `W27`：女神像维修与首次进入、原教程及技能获得引导的入口／完成状态；补查新人奖励切换的真实依据，避免引导重复发奖或覆盖正常存档。

### UI、演出与实际运行验证

- [ ] `W14～W18/W28`：恢复主体、职业／自选、详情／历史、积分／馈赠、兑换／购买及结果页全部原组件和动态绑定；按原层级、坐标、锚点、布局、字体与本地化恢复竖屏和比例缩放，并落实获准的价格黑底／文字差异。
- [ ] `W15～W18`：单／十／百抽整技与碎片、新技能与重复转化、回收代币、品质星光、槽位入场、渐显、百抽滚动、跳过、继续抽取、延迟／额外奖励处理及清理；原动画曲线／事件、UIParticle、shader／材质、后处理、音效及语音按真实绑定迁移为 Windows 可用原生资源，不能以静态结果页或旧近似效果代替。
- [ ] 规则验证覆盖职业过滤、基础权重与明确保底边界、逐抽记录、循环领奖、重复转化、材料购买、库存／价格档、日切刷新、原子事务、防重和存档恢复；未知裁决先确定依据，不把测试期望当成官方规则。
- [ ] 按影响运行静态检查及 Editor 导入／编译，再在有画面的 Unity Play Mode 从真实入口用鼠标／键盘／拖拽操作；检查页签、滑条、翻页、物品图标、遮罩与弹窗输入、缩放、连续演出／音频、跳过／再次抽取、材料不足／取消、关闭重进与重新加载存档。只调用回调不构成输入验证，截图不构成连续动画验证。
- [ ] 按 [Android → Windows 资源流程](../asset-management.md#android-资源迁移到-windows) 核对原包、递归 CAB 依赖和显式加载目录；按[实际运行验证要求](../asset-management.md#实际运行验证方法与要求)记录真实范围。仅在涉及导出链或验证成品行为时进行 Windows Player 验证；完整接通并验证前保持本页“未完成”。

原表及总页来源另见[祈愿原始来源](../../assets/reference/official-city/wish-source-audit.json)和[素材来源总表](../../assets/reference/SOURCES.md)。此清单维护于本功能页，不另建审计或验收报告。
