# 炼金炉料理

[总索引](../README.md)

状态：部分完成，未完成项见下文。

## 功能简介

家园炼金炉中的料理页支持查看已解锁料理、查看配方纸、学习配方、选择制作数量并制作料理。范围固定为官方版本 `854_156383` 的110种料理及其305种相关产出、材料和配方物品。合成、分解、宝石和馈赠页不属于当前 Cooking 模块实现。

主城家具类型 3 已在 `HomeFeatureRuntime.OpenCooking` 调用 `CookingFeatureRuntime.Open(profile, canvasParent, maxHpProvider, onClosed)`；家具解锁、摆放和交互由主城功能维护。

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
