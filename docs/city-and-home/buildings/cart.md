# 小推车

[主城与建筑索引](../city-and-home.md) · [总索引](../../README.md)

状态：部分完成

## 原始功能描述

官方小推车（`EFurniture.Cart = 10`、`4011`）为无解锁条件、无修复成本的户外默认挂机建筑，位于主城户外挂机位 `(-8.92, -4.51, 0)`，随昼夜与天气皮肤联动切换外观。所选官方中文表未提供该家具的物品描述，不补写替代文案。

小推车管理 8 项挂机资源产出（`HangUpId` `2~9`）：`3/4/5`（Lv1 解锁）、`6/8`（Lv3 解锁）、`2`（Lv15 解锁）、`7`（Lv24 解锁）、`9`（Lv45 解锁），解锁即赠送 `initialExtraRewardSeconds` 初始挂机时长。窗口提供各产出项解锁、单次升级与 `Map 12` 解锁后的多级提升（`Function 175` 一次最多 10 级，上限受赐福等级 `FurnitureLevelUpLimit` 限制）、产量与加成详情（`HangUpCartItemDetailDialog`、`PopTipOutputAddition`）、赐福等级超链接规则（`<link=1000>`）、小推车规则与装备掉率预览（`CartRule`、`rewardRules 11100`）、批量一键领取（`AwardPanel`）及 30 级开启的场景一键收取开关（`Function 164`）。

## 完成概要

已完整迁移 `903_156948` 官方原生小推车场景表现、6 套皮肤/天气预制体（`Cart`/`CartSnow`/`CartSpring`/`CartSpringSnow`/`CartMaidCafe`/`CartMaidCafeSnow`、`4206/4231/4266` 与天气 `4900/4901`）、全套 11 个官方 UI 预制体与 18 个 UI 控制器（`HomeCartWindowsUI`、`OfficialHomeCartWindowAssetCatalog`）、3 张红点图（`HomeBed`/`FurnitureCart`/`HangUpItem`）及 6 段原生音效。

已在独立 Unity 验证工程中完成端到端全流程验证与视觉截图核验：户外定位与 `HomeDayNight` 昼夜切换、6 套皮肤×天气切换与装潢界面 `4266` 选用、`FurnitureCart`/`HangUpItem` 红点联动、解锁产出项与 `3600s` 初始挂机时长、单项详情弹窗与空白处点击关闭、1x/10x 升级扣料与 `Map 12` `Function 175` 门禁、`OutputAddition` 加成弹窗、`<link=1000>` 赐福规则弹窗、规则与装备概率预览、`AwardPanel` 领奖展示、`Function 164` 一键收取开关切换与场景气泡直接收取、以及冷重启存档持久化；并同步回归通过 `Bed`、`DiningTable` 与控制器套件。

尚未完成最终 Windows Player 构建（`Zjcs.exe`）与打包产物整套真机验收，因此状态暂记为部分完成。

## 单机适配实现

- **运行时扩展注册**：保持只读 `StreamingAssets/Home/rules.json` 与表格载荷不变，在 `HomeFeatureRuntime.Cart` 启动时通过 `OfficialHomeRules.Cart` 动态注册 `4266` 女仆咖啡皮肤及道具记录，并由 `HomeSceneView` 让户外 `EFurniture.Cart` 始终保持原生表现。
- **共用挂机与红点底座**：扩展 `OfflineHomeProgression`（`initialExtraRewardSeconds` 解锁种子与最多 10 级批量升级）、`OfflineHomeBedServices`/`OfflineHomeBedAwards`（条件轮询、`HomeConditionNode` 求值、装备盒默认图标与统一领奖流）、`OfflineHomeHangUpTipSource`（支持默认解锁家具判定）及 `WorldMultiLanguageText`（`<midsize>` 富文本标签转换）。
- **独立窗口栈与音频宿主**：`OfflineHomeCartServices`/`OfflineHomeCartWindows` 接入 `HomeHudView` 栈式弹窗与返回关闭，切换窗口时安全接管并还原 `OfficialWwiseEvent.Host`（含 `IProfessionSoundObjectHost`）；共用产出与装潢机制分别维护在[离线产出](../functions/offline-income.md)与[建筑管理](management.md)。
