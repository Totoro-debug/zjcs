# 主城售货机与馈赠

[总索引](../README.md)

状态：已完成（离线实现与 Play Mode 商店流程已验证）

## 功能简介

主城家具类型 8 打开官方 `HomeShopPanel`。离线页面提供杂货、宝库和馈赠三个页签；访问页签按单机范围关闭。官方面板没有独立“特卖”页签，第二个商店页签在本地化界面中显示为“宝库”。祈愿商店 4／5／7 见[祈愿与兑换](wishes.md)。

## 完整还原部分

- 当前 `871_156598` 共用代码 `Common/ShopInfo.cs::GenerateRandomGoods` 会过滤无符合条件商品的组；`OfflineHomeShopService.GenerateGroceryStock` 沿用这一行为，避免 10601 组在 SubRank 3 无候选时导致整个货架事务失败。按现有逐组离线方案，此阶段为 8 个随机格，SubRank 5 起该组有候选，才达到配置上限 9 格；固定格仍由地图解锁条件决定。此修复不声称复现服务器的复杂组展开和随机序列。

- [prepare_home_shop_runtime.py](../../scripts/prepare_home_shop_runtime.py) 从固定 APK 规则、UI 与图标索引生成 `unity/Assets/StreamingAssets/HomeShop/`。UI 来源是 `defaultpackage_assets_prefabs_ui_shop.bundle`，hash 为 `a38c91d0d1720027b377b778e38863f5`。运行时复用官方 `HomeShopPanel`、`ShopGoodsItem`、商品详情层级及五个预置 `AchievementPanelItem`。
- [OfflineHomeShopService.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineHomeShopService.cs) 从本地配置和角色进度生成并保存货架。购买、刷新和领奖通过 `ApplyInventoryTransaction` 原子扣款／发奖并记录幂等事件；不足、售罄、条件不符或存档拒绝时不提交。
- [HomeShopScreenController.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeShopScreenController.cs) 接入家具入口、官方商店页签、刷新、商品详情、价格／折扣、库存／余额禁用态、数量操作、馈赠行、关闭和键盘导航。官方 `TabItem` 还原为 UGUI `Toggle`，三个商店页签均通过 `onValueChanged` 切页；购买后刷新 HUD 货币，馈赠进度和领奖状态随本地存档恢复。
- 官方 shop 1 的 `GroupIdList=[12001,12002,12003,10301,10401,10501,10601,14001]`、`GroupRandomCounts=[1,1,1,1,1,1,1,2]` 是逐组抽取次数，总计 9 格；`Unique=true`。官方共享函数 `ShopInfo.GenerateRandomGoods(groupDict,count,...)` 按组权重及商品权重抽取，但没有客户端调用点来确定服务器如何把复杂组展开。本地离线实现按配置逐组抽取指定次数，`ComplexGroupWeight` 用于递归选择子组，商品按 `Weight` 抽取，并按官方条件过滤；货架在本地保存，随机种子不声称复现服务器。候选商品包含木材、石块、粗炼石、时之砂、稀有时之砂、历战精华、普通冻干、星之愿、稀有女神牌和结缘绳；各商品另按 `SubRankBetweenCondition` 过滤。SubRank 3 可出现木材、石块、粗炼石、时之砂、历战精华、星之愿和稀有女神牌；普通冻干、稀有时之砂、结缘绳从 SubRank 5 起开放。每条随机商品库存 1、不能单项多买，累计购买上限 `-1`（不限跨刷新累计次数）；折扣倍率从该商品对应 SubRank 的 `PriceOff` 数组随机选取。用户实机看到的类别属于可能结果，不是官方完整候选范围；货币为金币物品 1。
- shop 1 的 `FixedGoodsList` 是 10701–10708 共 8 项，并非固定 5 项。物品 8050–8057（水、胡椒、糖、盐、木果粉、乐园牛奶、奶油、黄玉奶酪）分别是官方料理配方 7500–7507 的材料；配方地图 ID 与商店解锁 ID 一致，依次为 0、12、13、14、17、19、21、23。解锁到地图 17 时前 5 项可见，加上随机 9 格正好为 14；继续推进会解锁另外三项。八项均金币购买、不限库存／购买次数、无折扣。配置按地图 ID 开放，未直接记录“五国”名称。
- 杂货手动刷新为 10 次，金币成本依次为 4000、8000、12000、20000、30000、48000、80000、120000、160000、240000。官方 `RefreshCycleType=None`，没有自动倒计时重置；本地次数保存在角色档案。
- shop 2 按 `GroupIdList`／`GroupRandomCounts` 生成 12 格：8 个阶段确定组分别是 30 体力（原价 144、`PriceOff=0`，免费）、命运果实 1 个（60 晨星，50% 价）、精锻石（100／120／150／180／200 个，阶段条件选档）、史诗女神牌 5 个（75 晨星）、委托卷轴 1 个（120 晨星）、黄金甲虫（50 晨星）、幻形魔镜（100 晨星）和改名卡（500 晨星，每周刷新）；当前进度显示 180 个精锻石与官方 `goods_id=20404` 对应。另 4 格是随机组：第 2 格从星之愿 2 个、稀有女神牌 20 个、结缘绳 1 个中选 1；第 4、5 格各从四种采矿工具（每种 5 个、原价 100 晨星）抽取；第 8 格从 17 种伙伴礼物／素材中抽取。`Unique=true` 保证同架不重复。早期注册天数和位阶条件会过滤候选。折扣逐商品读取 `shop_goods_price_off`：宝库各商品折扣并不相同，甲虫、魔镜、改名卡无折扣；不能把所有商品都概括成随机折扣。改名卡按官方每周周期刷新，本地周起始和 08:00 重置时间取固定配置。
- 馈赠使用官方成就类型 12、分组 4：39 个一次性档位、20 万积分循环锚点、每轮 40 个模板且每档递增 5000，奖励物品与数量从固定表读取。积分、已领取 ID 和奖励都写本地存档；服务器玩家进度不参与。
- `ShopBuyBatchContent` 仅由 `FunctionType.Shop, shopId=3` 的通用商店启用；家园杂货与宝库使用 `shop_id=1/2`，只按各自 `SupportBuyMultiCount` 配置提供商品数量选择，不显示该批量勾选面板。shop 1/2 固定与随机商品的 `PopConfirmDialog` 均为 `false`；`ShopRefreshCoinNeedConfirm=[2]`，杂货刷新货币为物品 1，因此家园购买和刷新没有确认弹窗。
- 源表对照测试：`python -B scripts/prepare_home_shop_runtime.py` 后运行 `python -B scripts/test_home_shop_runtime.py`。测试核对商品 ID、物品、价格、条件、分组权重、折扣、馈赠节点、官方 UI 树及图标哈希。

## 自定义实现

- 用户确认以当前配置驱动的离线货架为本功能完成标准：首次打开或手动刷新时使用官方逐组抽取次数、嵌套权重、商品权重、条件与折扣表抽取，并将随机状态和货架存档；重新打开不重抽。宝库受 `RegisteredDaysCondition` 控制，固定商品按 `UnlockedMapClassIds` 显示。固定 APK 中商店 1 与商店 2 货币分别使用官方物品 ID，不硬编码为同一货币。该本地随机序列不冒称复现服务端。
- 服务器生成的初始 `PlayerShopData`、RPC 回包和服务器随机种子不属于离线游戏；Visit 关闭。宝库第二页沿用官方随机商品配置，不增加“特卖”映射。
- 验证：`python -B scripts/test_home_shop_runtime.py` 的 14 项官方源表对照通过；`python -B scripts/check_unity_project.py` 静态检查通过（38,719 个 GUID）。Unity 2022.3.57f1c2 Editor 在 r28 成功编译（Tundra 更新 6 项、评估 411 项）。r28 Play Mode 中杂货购买／刷新成本／货架重载、成熟注册天数开放 12 格宝库、宝库购买／库存重载、馈赠领奖／领取状态重载均通过，并实际生成 450×800 的杂货、宝库、馈赠画面。r22 原始 Play Mode 报告另有 `PASS real-input`：实际鼠标打开宝库、PageDown 打开馈赠、Escape 关闭商店。r28 的真实输入等待未收到输入，故该轮总报告为失败；其余失败为家园月相基线、HUD 构造事件、行囊输入和未安装的教学固定奖励规则，不属于商店。r28 未作为全套验收通过报告。

### 依据与代码定位

- [shop UI source manifest](../../assets/reference/shop-ui/source-manifest.json)、[规则 manifest](../../assets/reference/official-city/rules-manifest.json)、[HomeShop/ui.json](../../unity/Assets/StreamingAssets/HomeShop/ui.json)、[HomeShop/rules.json](../../unity/Assets/StreamingAssets/HomeShop/rules.json)。
- 官方代码：[ShopInfo.cs](../../.tools/source-packages/official-code/common-source/Common/ShopInfo.cs)、[ShopGoodsGroupInfoParser.cs](../../.tools/source-packages/official-code/common-source/Common/ShopGoodsGroupInfoParser.cs)、[ShopDataManager.cs](../../.tools/source-packages/official-code/client-source/ShopDataManager.cs)、[ShopPanel.cs](../../.tools/source-packages/official-code/client-source/ShopPanel.cs)、[AchievementPanelItem.cs](../../.tools/source-packages/official-code/client-source/AchievementPanelItem.cs)。
