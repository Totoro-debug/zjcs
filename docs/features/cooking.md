# 炼金炉料理

[总索引](../README.md)

状态：部分完成，未完成项见下文。

## 功能简介

家园炼金炉中的料理页支持查看已解锁料理、查看配方纸、学习配方、选择制作数量并制作料理。范围固定为官方版本 `854_156383` 的110种料理及其305种相关产出、材料和配方物品。合成、分解、宝石和馈赠页不属于当前 Cooking 模块实现。

主城家具类型 3 已在 `HomeFeatureRuntime.OpenCooking` 调用 `CookingFeatureRuntime.Open(profile, canvasParent, maxHpProvider, onClosed)`；家具解锁、摆放和交互由主城功能维护。

## 原始官方实现

炼金炉读取官方料理配方、制作条件、消耗、数量、效果与机械师保底配置，使用原设施、制作界面和演出。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

核对原始 `BagualuFurniture.OnClick → BagualuPanel`，锅应打开料理、合成、装备分解、宝石合成及馈赠，原本本地“石块 50 + 罗拉 500”的固定兑换不属于此原始逻辑，已移除。`prepare_kitchen_ui.py` 从固定 APK 及同版本 `854_156383`、具下载哈希凭据的官方缓存转换八个原始 prefab，保留完整背景、底部页签、料理五列网格、材料状态位置、数量控件及配方学习 / 合成目标弹窗。

追加完整消费的 `item_merge`（38 行）、`gem`（360 行）、`gem_merge`（14 行）三张表。料理使用原 `Materials / Count / Condition`，学习原配方物品后记录 `UsedItemCondition`；各页签读取原功能解锁条件，馈赠使用原内嵌成就卡片及分解 / 宝石 / 料理三个分类。466 项图像 / prefab 资源全部有输出哈希与显式加载记录，所需图标缺失 0 项；原对象身份、bundle、处理方式见 [kitchen-ui/source-manifest.json](../../assets/reference/kitchen-ui/source-manifest.json)。没有重绘或生成图片。

原炉的 RenderTexture / UIMesh 及完整制作特效尚未转换，在同一原始图像槽使用原 `UI/Cooking/ImgStove` 静态图片。本地宝石按同 ID 堆叠、自动选择同类材料；独立宝石实例、混合材料、镶嵌、高阶 NPC 熔炉和赛季赐福进度未接入，一键合成维持原赛季条件锁定。装备分解保留既有本地强化材料返还；原作不同料理的食用效果仍待补齐。上述范围不记作完整战斗 / 赛季逻辑已经实现。

运行数据 `assets/official/cooking-effects.json` 来自固定 APK manifest `854_156383`，转换工具为 `scripts/prepare_cooking_effects.py`。从原 `CookingUseDetailDialog.mEffectContentUI.mEffectItemConfig` 的引用提取 Recovery／Buff／Prop 三种真实模板、各自属性行及适用范围行，同时提取 `CookingBuffReplaceConfirmDialog`、`CookingPropEffectFullDialog`、盈满行、原规则提示行、确认窗专用窄列属性行及 `PopTipEntry`／`EntryDetailItem`，共14个窗口／引用模板；保留 RectTransform、原布局、字段引用、颜色及状态配置。嵌入模板的字段路径仅按原父级重定位，不自行重画或重排。适用范围说明读取同版本 `entry` 表及语言，原条目均无图标；位置与最大高度按 `UIUtils.SetRectPopPosInScreen`、`LoopVerticalScrollWithMaxHeight`，标题编号按 `RuleTipNumer` 的序列化数字。

依据：[料理效果来源](../../assets/reference/cooking-effects/source-manifest.json)。

永久属性使用原 `cooking_prop_effect_full` 的 284 条阈值和原 `level_prop` 的 71 类数值；前者逐字节核对 APK，后者沿已登记原版本 WwiseBnk 来源。临时战斗效果复用既有 `map-status.json` 的 `StatusEffectInfo` 与 `battle-rules.json`／`skill-rules.json` 的原 FightStatus 实体及属性。新增适用范围背景 `ImgInfoBg_CookBuff` 为原 Sprite，已登记到全局来源和显式资源绑定；没有新增自绘图。中文数字显示依据原 `NumberCultureFormatter_CN`／`NumberFormatHelper`，颜色取原 `GlobalColors.Red`／`Orange`。

生命上限变化的代码直接迁移自 `.tools/source-packages/official-code/common-source/Common/FightRoleFightComponent.cs` 的 `UpdateHpData`、`PropComponent_OnPropUpdated`，原文件 SHA256 为 `3b842762196f9f5701339f34be02179e9af3f352db79c770d83f0f637994c048`。`BattleFormulaHandler.MultAndCeiling` 对乘积向上取整；初始化使用原 float 生命比例，属性变化按原 double 比例同步当前生命，死亡时不补血。运行迁移不产生新素材；该公共组件已经包含实际方法，不再将本分支登记为服务端代码缺失。

布局交叉轴的可用空间约束迁移自 [Unity 官方 uGUI 的 HorizontalOrVerticalLayoutGroup.SetChildrenAlongAxis](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/Layout/HorizontalOrVerticalLayoutGroup.cs)：即使未强制扩展，也按可用空间、最小尺寸和首选尺寸夹取。该引擎算法已核对官方公开源码并登记链接，不将它冒充固定 APK 内反编译代码；游戏窗口自身的尺寸、锚点和布局参数仍取固定 APK。

固定 APK／原版本已校验 CDN 的 `prefabs_ui_bagualupanel`、`prefabs_ui_mechanicalfurnace`、`prefabs_ui_confirmdialog` 与 `prefabs_ui_tips`，经 `scripts/prepare_kitchen_completion.py` 转换为 `assets/official/kitchen-completion.json`、同名目录中的原 Spine 网格与 Wwise 音频。使用原 RectTransform、状态切换、中心宝石 Animator、三套 Timeline 的 Control／Spine／AkEvent／Signal 轨道、147 个机械师发射器及料理炉的 3 个发射器。RawImage 保留原贴图，UIMesh 保留网格、UV 与材质参数，流动扭曲及结果扫光按 APK 中编译的 `UI/EffectDistort`／`UI/EffectAdditive` GLES 程序迁移；shader、源对象和输出 SHA256 见 `assets/reference/kitchen-completion/source-manifest.json`。未绘制替代素材。

四项原粒子依赖使用已登记的新版同名包中匹配 CAB 的官方对象，经 `CounterpartAssets` 独立读取，版本、对象、实际 URL 和输入输出哈希均保留于该清单；原版本 185 个 404 缺项仍保留，不混入或改名覆盖原缓存。Spine Timeline 混合遵循 EsotericSoftware `spine-runtimes` 4.2 的官方 `SpineAnimationStateMixerBehaviour.cs`，来源 URL 与取得文件 SHA256 同样登记；

家园提示来源清单 assets/reference/home-tips/source-manifest.json 追加固定854_156383 FunctionEntryDialog 的原 HomeSkinBtn／TipDotNodeLinker 绑定，以及 HomeSkinNewMark 的2个原图节点。graphs.json合计232节点，保留原对象路径、状态配置、托管条件、有序连接、PresetTipLevel及HomeSkinBtn／HomeSkinUtils源码SHA256；没有新增或自制图片。来源转换脚本prepare_home_tips.py。

prepare_kitchen_completion.py --story-only 从固定 manifest 854_156383 的 prefabs_ui_story 和 prefabs_ui_portrait 迁移 TalkDialog、OptionTalkDialog、Npc_Portrait_8422 的原 RectTransform、PopupWindow／角色动画和托管字段。PortraitFace.LoadMain 的真实 GUID a0a47b1eef578c948abe2f44204ceee4 对应 npc_portrait_8422，按原 SetNativeSize 及 Main 偏移显示；三个表情保持独立原图，剧情空表情时隐藏 Face 层，不使用 NPC 列表半身图替代。chapter_3_side_5_01–12 从原中文 chapter_03 Wwise 分包的 Event→Play→Sound→media 调用提取；原包 MD5、实际来源、媒体偏移、输入与 Vorbis 输出 SHA256 均登记于 kitchen-completion/source-manifest.json。当前资源记录204项、数据输出25项。运行时按原设置的 StoryList 播放前三次补偿剧情，原20003仍保留为配置内容；本地退款范围的用户补证与原子保存适配见对应功能／地图文档。

新增 home-fast/source-manifest.json，prepare_home_fast.py 将固定854_156383的 FastHangUpProgressBarDialog、ConfirmDialog、男女home/sleep_02分离动作、Sleep_Bubble、AppearanceConfig覆盖0（男10206／女20206、33201、34000）及对应原图集和原染色遮罩转换到 assets/official/home-fast/。14个运行输出、真实bundle／对象、原CanvasScaler像素比例、HomeEnvironmentConfig的FastCycleCurve和两个动画事件、原客户端调用代码及spine_skin表均保留来源与SHA256；没有自绘素材或自定演出时长。原动作转换器复用 prepare_pet_gacha_performance.py 的player_actions，额外皮肤页从原角色图集包解析，不借用不匹配图集。

## 完整还原部分

- `scripts/prepare_cooking_module.py` 从官方 `cooking.g.bytes`、`item.g.bytes`、`game_settings.g.bytes` 解码规则，并将固定版 `CookingPanel`、`CookingPanelItem` 的变换、文字、图片、按钮与层级转换为本地载荷。面板原始节点245个、料理行节点4个；34张引用 Sprite 保持官方图像内容、边框与 pivot。来源与输出 SHA-256、包 ID、Prefab path ID 记录在 [source-manifest.json](../../assets/reference/cooking-module/source-manifest.json)。
- [cooking.json](../../unity/Assets/StreamingAssets/Cooking/cooking.json) 含官方110条配方和305个相关物品；材料与产出、条件、料理类型、效果字段、单次上限 `9999`、恢复料理红点库存阈值 `10` 由固定配置生成。数据加载会检查版本号、行数、唯一 ID 和所有物品引用。
- [ui.json](../../unity/Assets/StreamingAssets/Cooking/ui.json) 与 [Cooking/Sprites](../../unity/Assets/Game/Resources/Cooking/Sprites) 载入本地 Cooking 页。面板复用官方尺寸和层级，制作数量滑条、加减/最大数量、关闭、滚动列表、配方行选择和材料/成品库存数字可操作；Escape 可关闭面板。物品图标复用行囊 `OfficialInventoryAssetCatalog`。
- [OfflineCookingService.cs](../../unity/Assets/Game/Runtime/Offline/Cooking/OfflineCookingService.cs) 按官方条件过滤料理：`NoneCondition`、`MapUnlockCondition`、`UsedItemCondition`、`AlwaysCondition`；配方纸为物品类型322，96种 `UsedItemCondition` 的来源操作为89。配方纸进入背包后可学习，学习时本地原子扣除一张配方纸并记录该解锁条件。
- 制作上限遵循官方 `CookingInfo.CalcCanMakeCount`，即每种材料库存/单份用量的最小值，并受 `MaxCookingCountAtATime` 限制；每次制作按配方消耗材料并按配方 `Count × 制作数量` 发放成品。扣料、发放和事件 ID 通过 `OfflineMainlineProgressSession.ApplyInventoryTransaction` 一次保存，失败不提交部分结果。
- 恢复料理排序使用注入的 `Func<float> maxHpProvider`，按官方 `CurePercent + CureValue / MaxHp` 经 `Formula.Float2Int` 的评分与比较次序选默认料理。教学接口为 `IsSelect`、`SelectedItemId`、`ItemSelectedForTutorial(int itemId)` 和 `CookingCompletedForTutorial(int itemId, long outputCount)`；`GetDefaultRecoveryItem()` 返回官方最优恢复料理的实际列表行。

## 自定义实现

- 官方 `OperPlayerCooking` 只有客户端请求/响应调用，未包含服务器结算实现。单机制作仅按已核实的配方费用和产出执行原子背包事务，不推演服务器额外奖励、状态或异常分支。
- 官方配方纸使用物品详情弹窗触发物品使用；此面板没有迁移 `ItemDetailDialog`，因此在选中配方纸后用主操作按钮执行同一官方 `OperPlayerUseCookingFormula` 对应的本地消耗和解锁动作。
- `LoopVerticalScrollRect` / `LoopScrollGridLayoutGroup` 的原运行时代码未包含在可用客户端源码中。运行时采用 Unity `ScrollRect` 与原配方行100×100尺寸构建滚动网格；列表虚拟化及原网格私有参数没有声称与 Android 执行细节一致。

### 已确认的本地接线与边界

服务端 `OperPlayerUseCooking` 方法仍未取得；连续食用同种料理刷新效果由用户原作经验确认，处理意见和实现核对写在对应功能／地图文档中，不将该补证标为服务端代码来源。

规则依据为固定版本 `GemMergeManager`、`GemMergePanel`、`HighRankGemMergePage`、`GemMergePanelListPanelItem`、`GemPartInfoParser`、`PlayerGemDataExt`、`gem_merge`、`player_subrank`、原成就生成器及剧情 20000–20003。前三次保底剧情 `CompensateGems` 由未取得的服务端返回；失败材料的返还范围现已由用户原作经验补证，具体确认与实现核对记录于对应功能／地图文档，不标作已取得的服务端方法体。

## 尚未实现

- 主城家具类型 3 已接入 `CookingFeatureRuntime.Open`；尚未在 Unity Play Mode 验证实际鼠标／键盘命中、画面、制作状态与存档重载。
- `OperPlayerCooking` 的服务端结算未在客户端源码中提供，当前不能确认超出配方表的奖励或服务器失败语义。
- 料理恢复消费、增益状态、71种料理的永久属性、盈满阶段、生命上限变化后的战斗刷新及相同料理刷新行为不由本模块落地；这些须接到物品使用/战斗系统。已经确认的离线语义不得当作本模块已验证。
- 官方效果说明依赖 `LanguageUtils.SetCookingFormulaDesc`、本地化表和效果解析链；面板目前只显示已确认的恢复值及原始效果 ID，尚未恢复完整效果说明、可得来源跳转和物品详情弹窗。
- 原 Animator、炉图 UIMesh、材质、粒子与奖励逐项出现时序未迁移。合成、分解、宝石、馈赠等其他炉页签尚未实现。

### 官方依据与验证

- 固定 APK：`.tools/source-packages/zjcs_M509085.apk`，SHA-256 `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9`。
- Cooking Prefab 包：`defaultpackage_assets_prefabs_ui_cooking.bundle`，manifest `854_156383`，MD5 `5b2b455726b8c4f6c699ce328f9bdfc1`，SHA-256 `3a4b0c6790370ad29f026b954172b32f6cbe3ba894b555a5b434e04c8faf7802`；面板和行 prefab path ID 分别为 `6989734908194755216` 与 `7013167902997705936`。
- 规则来源见 `assets/reference/cooking-module/source-manifest.json`：`cooking.g.bytes` SHA-256 `99cfe4b96d2b46897f0ceb3125029732e14b16a5d97b0c164540616f8e64275f`，`item.g.bytes` SHA-256 `a317a104405d25b63d1ec8c6f63397001f8cc6f2329a04034c2a49be4a18934d`，`game_settings.g.bytes` SHA-256 `8832aa02061ada1c2dad9cc190fbe6fe54a25be1e30596513b9f8c1a1ef22b27`。
- 相关实现：`.tools/source-packages/official-code/client-source/CookingPanel.cs`、`CookingPanelItem.cs`、`ItemUseEffectCookingFormula.cs`；`.tools/source-packages/official-code/common-source/Common/CookingInfo.cs`、`CookingInfoParser.cs`、`CookingExtraInfoRecovery.cs`、`UsedItemCondition.cs`、`MapUnlockCondition.cs`、`CookingMakeItemComparer.cs`。
- 转换器直接校验固定版 APK 原始 `cooking`、`item`、`game_settings` 表级哈希与 854 CDN Bundle／图片回执，不依赖可变的汇总 `assets/official/rules.json`。固定表／载荷 parity 与输出哈希检查：`python -B scripts/test_cooking_module.py`。工程静态检查：`python -B scripts/check_unity_project.py`。Unity Editor 导入、编译与 Play Mode 尚未由本模块单独执行。
