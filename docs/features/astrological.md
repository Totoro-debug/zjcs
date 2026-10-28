# 星命契约与累计属性

[总索引](../README.md)

状态：未完成

## 功能简介

家园命运星象设施（家具类型 12、物品 ID 4019）进入星命契约。官方位阶／赛季门槛、520 级灌注、原初之星消耗及契约累计属性已接入本地运行时；赛季历史、结算、奖励与星命商店数据仍属于[赛季](seasons.md)及商店服务器数据缺口，当前没有捏造记录或货架。

本功能的规则核心已移植，但共享 Unity Editor 正被其他验收会话使用，没有执行 Editor 导入／编译和 Play Mode 实际输入、画面或存档恢复验证，因此功能仍标为未完成。

## 原始官方实现

命运星象按官方星象配置、节点连线、解锁条件、消耗与属性计算；原星象界面负责节点选择及详情。

## 完整还原部分

- [`prepare_unity_home_runtime.py`](../../scripts/prepare_unity_home_runtime.py) 将官方 `AstrologicalDestinyEntryPanel`、契约、属性、规则及 S1～S6 窗口纳入主城 UI 载荷，并按官方 `ContractTagSetting` 恢复 13 种属性标签的图标和底色；贴图与 `.meta` 一并显式生成。载荷中的面板结构由 [`HomeUiRenderer`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeUiRenderer.cs) 按原父子路径还原。
- [`HomeFeatureRuntime`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs) 为家具 4019 接入入口。[`OfflineAstrologicalService`](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineAstrologicalService.cs) 从固定版配置读取人物 `Rank` 到赛季映射、开放下限、费用和加速阶段；`TreasurePlaneRank` 只表示世界内容分档，不用于季别查询。
- 固定版 `astrological_level` 表的 520 个连续等级和累计门槛由服务校验，费用总量为 221080 个物品 ID 61（原初之星）。契约列表按官方倒序显示等级、属性、费用、已达进度、锁级预览及材料图标；列表采用虚拟行，避免同时创建 520 个完整 UI 树，并按官方限制只开放当前本地投入之后最多 10 个未解锁等级供滚动查看。
- 按住灌注按官方 0.05 秒间隔、1.5 秒平滑启动及配置加速阶段步进；松开或关闭面板时，以本地库存事务消费四舍五入后的材料数并增加 `CharacterProperties.AstrologicalCost`。达到材料或总等级上限时保留当前预览量，释放后提交。
- 属性详情从同一累计费用计算全部已解锁属性，使用官方属性行、名称、数值格式及 13 种标签样式；角色属性汇总读取同一 `AstrologicalCost`。规则弹窗使用官方窗口资源。
- 赛季阶梯在没有本地赛季记录时显示空状态；商店页签因官方入口只在收到玩家货架数据后显示，在本地无货架时隐藏。

## 自定义实现

星命费用与属性在角色存档中保存。灌注结束通过角色背包事务扣除物品 61，并更新累计投入；事务失败不更改已保存的材料和角色属性。费用、属性和开放门槛仍取固定官方客户端配置，不以服务器历史数据补默认值。

赛季记录只允许由[赛季功能](seasons.md)按实际战斗、地图和手动推进事务建立；既有离线约定不允许扫描当前状态后虚构过去的角色／幻兽快照。官方滚动预览奖励由赛季记录中的祝福评分发放；本地赛季评分接入前，不显示未确认的预估奖励，只依据已保存投入和当前按住灌注量计算 10 级预览范围。官方阶梯、结算、奖励和商店依赖玩家赛季记录或服务器货架，等待相应本地功能接入。

## 尚未实现

- 将本地实际玩法记录接入赛季列表、六季详情、赛季结算、赐福评分、装备柜与奖励领取；不得根据当前人物状态补造已结束赛季历史。
- 星命商店的本地货架与购买规则。已取得客户端只显示服务端货架的调用关系，没有可据以重建货品及刷新状态的独立客户端初始数据。
- Unity Editor 导入／编译、契约及属性弹窗画面、真实鼠标按住／松开／移出、材料和属性存档回读验证。

## 代码与资源入口

官方入口和门槛：[AstrologicalDestinyWindowFurniture](../../.tools/source-packages/official-code/client-source/AstrologicalDestinyWindowFurniture.cs)、[AstrologicalSeasonConfigParser](../../.tools/source-packages/official-code/common-source/Common/AstrologicalSeasonConfigParser.cs)。灌注交互和公式：[AstrologicalContractChargeButton](../../.tools/source-packages/official-code/client-source/AstrologicalContractChargeButton.cs)、[AstrologicalContractContent](../../.tools/source-packages/official-code/client-source/AstrologicalContractContent.cs)、[AstrologicalContractTag](../../.tools/source-packages/official-code/client-source/AstrologicalContractTag.cs)。赛季记录及单机保存边界见 [赛季功能页](seasons.md)。

素材与代码入口：[`prepare_unity_home_runtime.py`](../../scripts/prepare_unity_home_runtime.py)、[`HomeAssetCatalog`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeAssetCatalog.cs)、[`HomeHudView.Astrological`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Astrological.cs)、[`OfflineAstrologicalService`](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineAstrologicalService.cs)。

已执行验证：`python -B scripts/test_prepare_unity_home_runtime.py`（6 项通过）；`python -B scripts/check_unity_project.py`（静态元数据检查通过，29799 个 GUID）。Editor 与 Play Mode 未运行；当前 Unity PID 63328 仍由其他验收会话使用。
