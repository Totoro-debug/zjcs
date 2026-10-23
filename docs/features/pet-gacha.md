# 幻兽结缘与兑换

[总索引](../README.md)

状态：未完成

## 功能简介

结缘使用物品107抽取固定版幻兽、碎片和能力食材；页面包含结缘、馈赠、商店、详情、历史、抽取演出及新幻兽介绍。家园宠物设施入口见[果树生产与领取](fruit-tree.md)。活动结缘在离线版本显示未开放，不创建虚构活动实例。

## 完整还原部分

### 抽取与本地状态

`HomeFeatureRuntime.OnFurnitureClicked → HomeHudView.ShowPetGacha` 接通宠物设施入口；`HomeFeatureRuntime` 从角色存档建立 `OfflinePetGachaService`。`Draw` 支持1／10／100抽，使用箱子6003与抽取类型3；前10次累计抽取使用20401，之后使用20400，两条规则共用保底记录20400。百抽按原确认流程二次确认，当前持有107不足时可在41级以上按晨星2每个160补齐。

一次抽取事务保存材料、随机状态、全部奖励、幻兽库存、保底、总／每日次数、积分和逐抽历史。每抽发奖后立即更新本事务内持有状态，同批后续抽取会按更新后的状态判重。材料不足、每日上限、确认取消或存档失败不会先行扣款。每日上限10000次，按本地08:00日界线重置；历史按180天及1000条配置裁剪。百抽“今日不再提示”按本地日状态保存。

### 奖池与概率

以下是固定版规则20400／20401的基础类别权重，合计10000；包内数量为每次命中后给出的数量。保底数据来自 `IndexMininum`，不等于实际每抽概率。

| 奖励包／物品 | 类别权重 | 包内候选及候选权重 | 数量 | 常规保底门槛 |
| --- | ---: | --- | ---: | ---: |
| 53102 橙色幻兽整件 | 80 | 1603／1604／1605／1607／1608，各100 | 1 | 80 |
| 53100 紫色幻兽整件 | 500 | 1600／1601／1602，各100 | 1 | 30；前十抽为10 |
| 53201 橙色幻兽碎片 | 1500 | 1703／1704／1705／1707／1708，各100 | 3 | 0 |
| 53200 紫色幻兽碎片 | 900 | 1700／1701／1702，各100 | 3 | 0 |
| 53323 蓝色能力食材 | 5000 | 1530／1540／1550，各100 | 3 | 0 |
| 53324 紫色能力食材 | 1600 | 1531／1541／1551，各100 | 1 | 0 |
| 物品25 | 420 | 固定物品 | 1 | 10 |

结缘详情还单独展示合并了保底表的概率：橙／紫整件1.96%／6.37%，橙／紫碎片13.27%／7.96%，蓝／紫能力食材44.24%／14.16%，其他12.04%。详情展示值不作为抽样权重。

### 积分、馈赠与商店

每抽增加积分254。成就组16累计阈值与循环奖励、组15的结缘次数馈赠分别按固定版 `achievement` 模板展开，支持进度、可领状态、领奖和本地领取记录；两组状态互不混用。结缘详情展示规则及奖池概率，历史分页展示本地逐抽结果。

商店7使用官方商品、三列货架、价格、库存和周刷新时间；蓝色幻兽从配置候选中按权重生成，职业变化按 `RefreshByProfession` 更新。购买扣款、库存及物品到账在本地事务内处理，确认弹窗和活动未开放提示均接入。

### 演出与技能说明

`HomeHudView.PetGacha.cs` 接入抽取、结果、详情、历史、积分／馈赠、商店与新幻兽面板。`PetGachaResultPerformancePlayer` 消费已提交的奖励；角色动作、击打、掉落、收集、跳过、保护时间和关闭清理使用固定版演出载荷，不在表现阶段重抽。

新幻兽介绍使用官方模型、品质、类型图标及幼年支援技能。技能图标可切换当前说明；`PetGachaDescriptionLinkHandler` 将 TMP `<link>` 命中序号映射到官方词条，弹出固定版 `EntryDetailDialog`／`EntryDetailItem`。弹窗依点击坐标定位并按屏幕边界夹取，标题分隔沿用原始面板状态；文本和图标来自图鉴链接载荷。`scripts/prepare_pet_gacha.py` 将相关面板、词条文本、技能链接和图标加入 `PetGacha` 运行载荷及来源哈希。

### 规则与代码

固定规则见 [rules.json](../../assets/official/rules.json) 的 `gacha`、`chest`、`reward_rule`、`random_reward`、`gacha_pity_probability`、`achievement`、`game_settings`、`skill` 与 `pet_evolution`。核心代码为 [OfflinePetGachaService.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflinePetGachaService.cs)、[OfficialPetGachaCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialPetGachaCatalog.cs)、[HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)、[HomeHudView.PetGacha.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.PetGacha.cs) 和 [PetGachaDescriptionLinkHandler.cs](../../unity/Assets/Game/Runtime/Presentation/Home/PetGachaDescriptionLinkHandler.cs)；角色进度由 `OfflineMainlineProgressSession.ApplyInventoryTransaction` 持久化。

固定版客户端依据包括 `PetGachaPanel`、`PetGachaSubPanel`、`PetGachaResultPerformer`、`PetGetNewPanel`、`SkillHyperLinkDataProvider`、`GachaProbabilityContent`、`GachaHistoryContent`、`ScoreRewardUI` 和 `AchievementRawInfoParser`。RPC40797仅取得客户端请求与响应类型，未取得服务端结缘方法体。

载荷由 [prepare_pet_gacha.py](../../scripts/prepare_pet_gacha.py) 生成；固定版来源记录见[结缘来源清单](../../assets/reference/pet-gacha/source-manifest.json)、[演出清单](../../assets/reference/pet-gacha-performance/source-manifest.json)和[图鉴链接清单](../../assets/reference/codex-links/source-manifest.json)。

## 自定义实现

固定版客户端没有RPC40797服务端方法体或 `IRandomItemWeightAgent` 在结缘中的绑定。用户确认按已有类别／候选权重，并参考详情概率描述模拟离线抽样：包内候选按配置权重比例选择，53102的五个候选当前各100，因此各占该包候选概率的20%；`WeightScaleFactor=2`、`WeightScaleCountLimit=0`不据此臆造未证实的公式。已知固定版共享保底配置用于本地逐抽更新；离线结果不声称与服务端随机序列或动态权重公式逐项一致。

用户确认幻兽重复转换数量：紫色（史诗）转30个对应碎片，橙色（传说）转60个对应碎片。服务端没有提供发奖决策；此数量是用户指定的离线规则，不标作官方服务器实现。

用户确认逐抽即时判重：同一批次中较早抽到的幻兽会加入持有状态，后续抽到相同不可重复幻兽时立即按上述品质规则转换碎片。

## 尚未实现

- 结缘入口、输入、动画画面及存档恢复尚未在 Unity Play Mode 完成真实操作验证；Editor导入／编译和Windows Player验证也未通过本次验证。静态工程检查及资源生成不能代替运行验收。
