# 技能祈愿、材料购买与兑换

[总索引](../README.md)

状态：未完成

## 功能简介

女神像或技能页进入常驻／高阶祈愿，保留兑换、馈赠、概率、记录和材料购买。幻兽及古遗物奖池分别维护。

以上为 Unity / C# 的目标范围。对照固定 `854_156383` 官方客户端、共享代码、原始配置和当前工作区，祈愿尚未完整迁移；当前 Unity 没有接通祈愿玩法、对应界面与演出。

## 完整还原部分

尚无本功能在 Unity 完整接入并运行验收的部分。已取得的官方代码、配置、旧引擎转换素材与测试只作为迁移输入。

### 当前入口与资源接入

- `unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs` 将 `StatueOfGod` 映射为家具类型 `5`；`HomeFeatureRuntime.OpenFurniture` 仅为床、推车和邮箱打开内部页面，女神像进入 `HomeHudView.ShowFurniturePlaceholder`，显示“内部页面由所属功能模块提供”。家具模型、贴图及可点击并不代表祈愿入口已接通。
- 当前 `unity/Assets/Game/Runtime/` 未发现常驻／高阶祈愿、积分／馈赠、详情／历史、抽取或结果演出的控制器及服务。`Presentation/Skills/OfficialSkillAssetCatalog.cs` 是技能素材与养成规则目录，没有祈愿路由。
- 当前本地角色状态 `OfflineMainlineSave/OfflinePlayerProgress` 有库存及技能养成数据，尚无祈愿保底、职业自选、积分／馈赠领奖、分类历史与抽取随机状态的完整事务。其他模块的库存随机数或开箱计数不能代替祈愿状态。
- `assets/official/ui.json`、`rules.json`、`wish-performance/performance.json` 和 `wish_visuals.json` 保存原布局、规则与演出转换数据；当前 Unity 没有祈愿专用原生 prefab、动画／材质和加载目录将它们接入玩法。`Assets/Official/Character/Art/wish/` 中复用的特效贴图及家园女神像贴图不构成祈愿演出接入。

### 官方行为与迁移差距

| 范围 | 官方依据与应有行为 | 当前 Unity |
| --- | --- | --- |
| 常驻祈愿 | `SkillGachaPanel`、`GachaPanelBase`：`GachaType.Skill=0`，箱子 `6000`，每抽消耗 `110×1`、奖励积分物品 `251×1`；支持配置中的 `1/10/100` 次，百抽按钮随材料量显示；职业选择、非当前职业确认、功能与每日次数限制、材料不足购买 | 未接通入口、抽取、职业选择及消耗／奖励 |
| 高阶祈愿 | `SkillSeniorGachaPanel`：`GachaType.SkillSenior=2`，箱子 `6002`，每抽消耗 `118×1`、奖励积分物品 `253×1`；职业列表取已解锁职业，当前职业优先、阶级降序；提示与女神像皮肤随职业变化 | 未接通；不能复用常驻职业池代替 |
| 祈愿积分 | `ScoreRewardUI`、`ScoreRewardDialog`：按 `ScoreAchievementType=20/60` 领取，普通首档 `200` 积分奖励 `6800×1`，高阶首档 `50` 积分奖励 `4×1`；显示进度、奖励、已领取与循环档位 | 未接通计分、领奖、详情和循环状态 |
| 馈赠 | `GachaPanel` 的 `mGachaOrAchievementToggleGroup`、`AchievementPanelWithTypes`：与积分奖励分开，按累计抽取成就类型 `7/61` 显示常驻／高阶奖励、切页与领取；循环档位按 `AchievementInfo/CycleTemplateInfo` 展开 | 未接通馈赠页、累计进度及重复领取保护 |
| 技能池 | `ItemFilterForProfessionSkill`：常驻按职业可祈愿技能及可用前置职业筛选；达到职业阶级 `8` 后涉及 `4` 个职业自选，未选完时原客户端阻止抽取。`ItemFilterForProfessionUnlockedSkill`：高阶仅允许 `SkillInfo.UnlockProfession == 所选职业`；碎片先映射回技能 ID | 未实现抽取池筛选、自选和对应 UI |
| 概率与保底 | `RewardRuleInfoParser`、`GachaProbabilityContent/Item`、`SkillGachaSettings`：按规则与随机奖励包展开、职业过滤、重新归一化，按类型／品质分组并合并同物品同数量；抽取保底和高阶级技能／碎片记录另行维护，见下文 | 未实现本地抽取与保底，也未接通概率页 |
| 祈愿详情 | `GachaDetailDialog`、`GachaDetailInfoSkillGachaProvider`：概率／历史两个标签，规则文字随常驻职业阶级、自选条件变化；高阶不使用常驻自定义规则；每日女神祈愿上限配置为 `10000` | 未接通弹窗、标签、规则文字与滚动列表 |
| 历史记录 | `GachaHistoryContent/Item`、`PlayerGachaHistoryDataWrap/Manager`：按祈愿类别分页，显示物品名称、类型、本地时间和品质颜色；配置记录期 `180` 天，分类缓存 `1000` 条。分页通过请求／响应返回，本地适配须保留相同消费语义 | 未接通记录写入、分页、重进恢复及 UI |
| UI 与演出 | 原 `GachaPanel`、`SkillGachaPanel`、`SkilSeniorlGachaPanel`、`GachaDetailDialog`、`GachaHistoryContent`、`ScoreRewardDialog`、`GachaResultPanel` prefab；`GachaResultAnimPlayer` 以原动画事件驱动星光粒子和结果槽，单抽／十抽／百抽、品质、碎片／整技、跳过、再次抽取、音效各有分支 | 尚未恢复为接通玩法的 Unity 原生页面与演出；未取得运行表现通过结论 |

### 概率系统的依据与边界

常驻奖励规则 `20000`、高阶规则 `21000` 均依次指向 `50302/50301/50300/50252`，权重 `100/700/2200/7000`，对应传说整技、史诗整技、稀有整技、普通技能碎片；基础类别概率为 `1%/7%/22%/70%`，碎片包每次 `3` 片。职业池内的具体技能来自 `RandomRewardInfoParser` 展开与职业筛选，不能直接在全部技能中均匀抽取。

两条奖励规则分别记录 `IndexMininum=[70,30,10,0]`、`RecordSharedId=20000/21000`。常驻还涉及 `SkillGachaLowRankRecord`、`SkillGachaLowRankPieceRecord` 与按职业阶级选择的 `EnsureHightRankConfig/EnsureHighRankPieceCount`；`GachaDetailInfoSkillGachaProvider` 展示次数时加 `1`，不能混同品质保底与技能职业阶级保底，也不能遗漏逐抽状态更新。

`gacha_pity_probability` 为两种祈愿配置了传说整技 `241`、史诗整技 `790`、稀有整技 `2400`、普通碎片 `6569`；`Formula.Long2Double` 除以 `10000`，即详情中的保底综合概率 `2.41%/7.90%/24%/65.69%`。`GachaProbabilityItem.SetPityProbability` 还遵循 prefab 的地区隐藏配置。这是展示数据，不能作为每抽的权重或据此推算未取得的动态加权公式。

官方 `SkillGachaPanel.GachaSend` 调用 `OperPlayerGacha`，结果页消费奖励消息；APK 共享代码 `Common.Player.SkillGacha` 只校验配置、记录次数并返回空奖励列表，未包含完整服务端抽取裁决。应按已确认的本地权重与已知保底方案适配，不能将该函数直接迁入后声称恢复了官方服务器随机实现。

### 已核实的来源与验证范围

- 通过现有 `Apk` 读取器校验固定 APK，并用原 Luban 构造器重新解码 `gacha/chest/reward_rule/random_reward/profession_base/skill/item/achievement/game_settings/gacha_pity_probability`，10 张表与 `assets/official/rules.json` 一致。直接读取了原祈愿包中的上述七个关键 prefab，包含常驻、高阶、详情、历史、积分与结果页面；这些官方内容可取得，不能标为源内容缺失。
- `wish-performance` 的 `90` 个资源及 `17` 份客户端代码哈希、`wish-visuals` 的 `29` 个资源与 `4` 份行为代码哈希、两份主输出哈希均与现有来源清单一致。此核对不等于逐项复核全部输入对象或 Unity 运行通过。
- `wish-visuals/source-manifest.json` 明确记录旧 Godot 的粒子投影、Noise 与 Bloom 近似处理；旧转换的 `unconverted_modules=[]` 不代表 Unity 原生组件、shader、动画或效果已经迁移完成。
- 工程静态检查 `python -B scripts/check_unity_project.py` 未通过：当前并行工作区的 `Presentation/Commerce/CommerceAccountPages.cs`、`CommerceShopPages.cs` 缺少 `.meta`。这是全局元数据检查结果，不能据此判断祈愿已运行或官方素材缺失；本次仅修改功能文档与索引，未改动这些商城文件。
- 本次完成源码、原始配置、资源哈希与当前接线检查。祈愿未进行 Editor 导入／编译验收、Play Mode 逻辑／真实输入／画面／连续动画／音频／存档恢复验证，也未进行 Windows Player 验证；缺少已接通的祈愿流程，不能标为通过。现有编辑器实例用于家园验证，未中断或改用该实例执行祈愿。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

用户明确按原配置权重与已知保底进行离线裁决，不猜测额外动态概率；服务器随机序列未取得，材料、积分、保底、历史、货架和随机使用本地原子事务。

用户要求隐藏抽取／结果／刷新价格黑底并改深色价格字；原 `CommonBtn_CostItem` 确实有黑底，这是一项明确界面差异。

服务端免费修复回调缺失时，已有女神像或满足原免费自动维修条件即可只读通过购买资格，不伪写家具所有权。同批重复商品按用户补证允许，各格库存独立。

## 尚未实现

- 女神像／技能页的常驻与高阶祈愿、原奖池、保底、演出、兑换、馈赠、概率、历史与材料购买。
- 恢复官方 Unity 代码、配置、prefab、资源与调用关系，完成本地事务、实际入口和界面接线。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 依据与代码定位

客户端代码根目录为 `.tools/source-packages/official-code/client-source/`，共享代码根目录为 `.tools/source-packages/official-code/common-source/Common/`；上文给出具体类／方法。材料不足与百抽确认另参照 `GachaPanelUtils`、`MarkableConfirmDialog`、`QuickBuyDialog`。

原始来源与转换依据：[祈愿表及 UI 来源](../../assets/reference/official-city/wish-source-audit.json)、[结果演出来源](../../assets/reference/wish-performance/source-manifest.json)、[旧视觉转换与适配边界](../../assets/reference/wish-visuals/source-manifest.json)、[材料来源入口](../../assets/reference/gacha-item-sources/source-manifest.json)、[快捷购买来源](../../assets/reference/gacha-purchase/source-manifest.json)。后处理另参照 [battle-post/data.json](../../assets/official/battle-post/data.json) 与 [Bloom.cs](../../.tools/source-packages/official-code/urp-reference/Bloom.cs)。

后续实现须先按 [Android → Windows 资源流程](../asset-management.md#android-资源迁移到-windows) 准备原包与 CAB 依赖，再恢复原生组件与加载绑定；按 [实际运行验证](../asset-management.md#实际运行验证方法与要求) 验证真实入口、输入、连续演出和持久化。
