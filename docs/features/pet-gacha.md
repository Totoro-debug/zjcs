# 幻兽结缘与兑换

[总索引](../README.md)

状态：未完成

## 功能简介

结缘使用物品107抽取固定版幻兽、碎片和能力食材；页面包含结缘、馈赠、商店、详情、历史、抽取演出及新幻兽介绍。家园宠物设施入口见[果树生产与领取](fruit-tree.md)。活动结缘在离线版本显示未开放，不创建虚构活动实例。

## 原始官方实现

原结缘面板、抽取结果与兑换入口使用官方幻兽池、概率／保底、消耗、重复转化和演出配置。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

prepare_pet_gacha_performance.py 从固定 871_156598 的 APK／匹配清单 CDN 读取 PetGachaSettings、六个 Pet UI prefab、原 MyPlayable 轨迹／粒子／拖尾／Wwise 与男女角色 SplitBinarySO。pet-gacha-performance/source-manifest.json 登记源对象的 bundle、对象、来源及 SHA256、转换输出及客户端源码 SHA256。资源位于 assets/official/pet-gacha-performance/；六段角色动作通过 repack_battle_spine.mjs 保留原二进制动画，不使用插帧或重绘，图集仍引用已有官方原贴图。MainCanvas／UICamera 决定画布单位，原 PolygonCollider2D 决定百抽掉落范围；各 prefab 的 UIBloomGroup、UIBloomPlayerElement、UIBloomGraphicElement 归属路径与原组件来源随性能数据登记。

在原结缘演出转换基础上，补取固定 manifest `871_156598` 的完整 `PetGachaPanel` 外壳、`AwardFloatingSettings`、积分和保底刷新绑定及原语言文字。当前模块使用同一份 `assets/reference/pet-gacha-performance/source-manifest.json`，累计 1,955 个输入对象、128 个输出；记录 APK／原版本 CDN 来源、bundle／对象、客户端与补充反编译代码、两份原语言输入及输出 SHA256。来源顺序未变，未引入自绘或混用新版包。

`prepare_pet_gacha.py` 从固定版 `rules.json` 读取幼年支援技能的 `HyperLinkTypes`／`HyperLinkDatas`，将官方 EntryId 与技能说明一起写入结缘运行目录。词条名称、说明、图标及 `EntryDetailDialog`／`EntryDetailItem` 原始层级来自 [codex-links载荷](../../assets/official/codex-links/links.json)；该输入的来源对象及输出图标哈希见 [codex-links来源清单](../../assets/reference/codex-links/source-manifest.json)。脚本把实际需要的六个词条及原图标复制进 `PetGacha` 动态资源目录，并在 `pet-gacha/source-manifest.json` 登记输入、输出和SHA256。客户端行为依据固定版 `PetGetNewPanel`、`SkillHyperLinkDataProvider` 与 `HyperLinkClickHandlerEntry`；没有自绘技能说明或词条图片。

prepare_home_decoration.py 从固定 871_156598 的 131 个实际家具／主题／天气 prefab 原 Transform 追加 local_trs（原 m_LocalPosition、m_LocalRotation、m_LocalScale）。--transforms-only 核对既有实体 name 和完整源对象凭据，独立重读原对象，更新 assets/reference/home-decor/source-manifest.json 的输入与实体 SHA256；既有 AnimationClip 原曲线、插槽及创建偏移不变。运行时  迁移原父子局部矩阵，未添加自绘素材或自定关键帧。

`prepare_pet_gacha_performance.py --panel-clips-only` 重新转换固定 APK 的 `PetGachaSubPanel_Default／Show／Hide`，包括 `CanvasGroup.m_BlocksRaycasts`，同步独立与嵌入主页面两份动画；原完整 Transform 层级核对了共享 Hide 的两条无目标绑定，保留原未绑定行为。原文 `ui_pet_gacha_is_times_hundred`、`ui_no_prompts_today` 来自同版本中文分包；确认窗直接复用原 `MarkableConfirmDialog`，界面来源仍由 commerce-components 清单登记，结缘动画、原文与 `GachaPanelUtils`／`MarkableConfirmDialog` 代码哈希在 `pet-gacha-performance/source-manifest.json`。

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

### 已确认的本地接线与边界

`prepare_pet_gacha_performance.py` 从 APK 清单解析外壳真实 bundle，保留默认 Animation 片段及扩展粒子材质。`Fx_XingKon_0002` 没有显式 `_MainTex`，使用已解析的原 shader 默认白纹理，以常量资源表达官方默认行为；不是替代图片。10 个官方 NoiseModule 的参数和曲线直接进入运行模块；Unity 引擎原生采样算法和 quality 实现未在当前官方客户端源码中取得，因此不声明逐轴或逐帧一致；原 `UIBloomManager.SetActive` 仅在 Bloom 全局开关开启且需求计数大于零时激活，需求计数由 `UIBloomRequirer.OnEnable/OnDisable` 管理；结缘使用的 prefab 共登记 6 个 `UIBloomGroup`、`UIBloomPlayerElement`、`UIBloomGraphicElement`，没有 Requirer。结缘页不自行注册需求或强制启用 Bloom；结缘页隐藏／离树时， 按 `PetGachaResultPerformer.OnDisable` 清空队列与计时、释放工作中的奖励与特效、停止音效并关闭操作遮罩，恢复显示时应用官方默认动画；`OnWindowClosing` 的停止计时行为一并迁移。结缘场景、结果与新幻兽介绍由原 UI 层级、RectTransform、动画和调用时序迁移，运行入口为该控制器及其 reward／effect／popup 脚本。价格黑底隐藏沿用用户明确确认的界面调整，不标为原版表现。校验命令为 `python scripts/prepare_pet_gacha_performance.py --check`；素材包和 Windows 发布状态见对应功能／地图文档。

## 尚未实现

- 结缘入口、输入、动画画面及存档恢复尚未在 Unity Play Mode 完成真实操作验证；Editor导入／编译和Windows Player验证也未通过验证。静态工程检查及资源生成不能代替运行验收。
