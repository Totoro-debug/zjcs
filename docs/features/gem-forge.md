# 宝石合成与机械师熔炉

[总索引](../README.md)

状态：未完成

## 功能简介

炼金炉提供普通宝石合成，高阶宝石按原导航进入山之国机械师熔炉；镶嵌和卸下见[装备](equipment.md)。本页维护宝石配方、炉能与失败结果、入口及合成表现。

## 原始官方实现

宝石合成和机械师熔炉分别按原 gem、gem_merge、消耗／产物、保底与选择规则运行，不混同星石镶卸。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

固定 854_156383 的 ItemIconAttachmentManager → ItemIconAttachmentConfig → GUID 对应 GotObj、Mask、RedDotContent 三个原 prefab，由 prepare_kitchen_completion.py --selection-only 按原 RectTransform、Image 色值及 ItemIconAttachmentCell 层级 7／0／20 转换。ImgTick、ImgQuality_Mask、ImgRedDot 复用已有官方贴图；资源清单当前共 181 项、数据输出 25 项，配置、挂件对象、客户端代码及语言输入／输出 SHA256 保留于 kitchen-completion/source-manifest.json。MergePanel.GemMerge_FullLevel 从固定版原语言包提取，未手写替代提示。完整重建亦包含这三项挂件；--language-only 仅重新提取已有界面使用的原文。

`scripts/prepare_unity_gem_forge_rules.py` 从固定 manifest `854_156383` 的 `assets/official/rules.json` 读取 `gem`、`gem_merge` 和 `GemMergeStorySetting.StoryList`，写入 `unity/Assets/Game/Resources/Equipment/gem-forge-rules.json`。输入由 `assets/reference/official-city/source-manifest.json` 校验；Unity 载荷与输出 SHA256 记录在同目录 `gem-forge-source-manifest.json`，运行时由 `OfflineGemForgeService.LoadDefault` 校验。当前提取 360 条宝石类型和 14 条合成规则。规则原值不改写；高阶炉的随机结算状态随本地角色装备存档保存。

## 完整还原部分

`scripts/prepare_unity_gem_forge_rules.py` 校验固定版 `854_156383` 的 `rules.json` 来源哈希，提取 `gem_merge` 表及 `GemMergeStorySetting.StoryList` 前三项至 `unity/Assets/Game/Resources/Equipment/gem-forge-rules.json`；配套来源哈希记录由 `OfflineGemForgeService.LoadDefault` 校验。

`OfflineGemForgeService` 使用 `OfficialEquipmentCatalog` 的 `gem`、`item` 表读取宝石类型、等级与 ID。普通配方按 `RequireCount + 1` 计总颗数；主石与材料等级相同则主石计入总数，镶嵌主石也适用。玩家可选择背包堆叠数量或其他装备孔位中的同类宝石作为材料；合成事务按所选孔位扣除材料，成功时原主石孔位升级。高阶配方按 `BaseRate + StepRate[min(PercentMininum, missCount)]` 计算成功概率；保底故事播放完后，达到 `PercentMininum` 次失败时必成。失败类型使用官方 `FailedResult`。公式依据官方 `PlayerGemDataExt.GetSuccessRateByGemRank`、`GetGemEnergyStorageValue`、`NeedTriggerStory` 与 `NeedTriggerDownExplodeStory`。

熔炉记录、炉能失败次数、故事序号、随机状态、材料和货币在 `OfflineMainlineProgressSession.ApplyInventoryTransaction` 的同一存档事务中提交。重复 receipt 不重复执行。失败保留主石并消耗材料；按用户确认，仅前三次保底失败返还当次材料，不返还此前累计材料。新版赛季宝石不开放旧回收分支。

角色装备星石窗口提供“宝石合成”入口。普通配方读取官方 `BagualuPanel/GemPanel` 树，高阶配方读取官方 `MechanicalFurnacePanel` 树；两种配方都先选择主石，再逐件选择背包堆叠数量或已装备孔位中的同类材料。普通面板将主石、已选材料和目标预览绑定到官方中心槽；高阶面板在列表首行标明主石、将已选材料写回机械熔炉材料槽，并在列表显示目标、材料数和金币，另显示成功率与炉能。两类合成均使用官方确认窗和结果窗的状态/图标。结算后镶嵌主石按装备实例和孔位替换，不再按相同物品 ID 任意查找。官方 UI 树与精灵由 `scripts/prepare_unity_gem_forge_ui.py` 从固定版 bundle 解码并按来源哈希校验，运行时绑定在 `CharacterPanelNodeFactory` 与 `CharacterEquipSlots`。

## 自定义实现

服务端最终判定改为使用官方 `gem_merge` 配置在本地结算。结果随机值由存档内状态推进；游戏运行不请求网络。前三次保底故事补偿依用户确认只返还失败材料；新版赛季宝石不回收。合成保留主石的原装备孔位身份，背包材料按数量扣除，其他镶嵌材料按装备实例及孔位扣除；这些材料变化与炉能、故事序号、随机状态在同一存档事务提交。

## 尚未实现

- 按 `DialogOpenFurnaceHandle`、`OpenFurnacePanelNode` 和 `MechanicalFurnacePanel` 恢复山之国地图类 12 的世界交互入口及 NPC 类 8422 导航。固定版 `assets/official/regions/12-entities.json` 的放置记录 `1803` 将 `8422`（梅克娜_日常）放在 `(182, 99)`；对应 `defaultpackage_assets_prefabs_ecentity_mapworld_worldnpc_map_12_npc.bundle` 已在原版 CDN 缓存中。当前运行地图为地图 10，地图 12 实体、场景和切图流程仍未转换接入。
- 主石列表仍未恢复官方循环列表逐件排序、红点/选中状态和低阶提醒；当前用材料数及可合成状态排序，并按配置显示当前等级上限。
- 接入高阶成功、爆炸、降阶爆炸三套原 Timeline、Spine、粒子和音效；补取并绑定对白 20000–20002、梅克娜立绘和中文语音。当前结果窗只有官方静态层与文字反馈，尚无合成演出。
- 验证普通/高阶成功与失败、满炉能必成、材料和货币变化、前三次补偿、receipt 去重、存档恢复、鼠标输入与实际画面。此前 `python -B scripts/check_unity_project.py` 通过（38,668 GUID）；再次运行时没有取得脚本退出回传，不能记作通过。Unity 日志确认完整 Tundra 脚本编译通过（411 个评估项，Character 与 Offline Progression 程序集成功）；独立 Editor 启动因另一会话的批处理 Editor 已占用同一工程而失败，未运行 Play Mode。高阶材料来源选择和对应机械熔炉中心槽是在该次脚本编译之后接入的，尚未单独编译和实机运行验证。

固定版 `854_156383` 的 `defaultpackage_assets_prefabs_ui_mechanicalfurnace.bundle` 原始 SHA-256 为 `7360ff7fbf99e81e823cce4fef22a7233db66c801b772dbbdc10e37ffce34093`；UnityPy 读取确认含 `MechanicalFurnacePanel`、`HighRankMergePage`、`GemMerge` 官方根对象。`scripts/prepare_unity_gem_forge_ui.py` 已转换机械熔炉、普通合成、结果、确认、列表行、规则、NPC 对话节点共 9 棵 UI 树，并按对象哈希恢复 77 张官方精灵到 `unity/Assets/Game/Resources/Equipment/GemForgeSprites/`。演出 bundle `defaultpackage_assets_animation_ui_gemmergepanel.bundle` 存在于固定版提取视图，Spine/粒子/Timeline 依赖尚未转换绑定。地图 12 NPC bundle 同样已在固定版原 CDN 缓存中，入口位置由 `regions/12-entities.json` 确认；当前工程尚无地图 12 运行场景与跨图切换。

## 代码与资源入口

- 官方公共代码：`.tools/source-packages/official-code/common-source/Common/PlayerGemDataExt.cs`、`GemMergeInfo.cs`、`GemMergeStorySetting.cs`。
- 官方客户端：`.tools/source-packages/official-code/client-source/GemMergeManager.cs`、`GemMergePanel.cs`、`HighRankGemMergePage.cs`、`MechanicalFurnacePanel.cs`、`DialogOpenFurnaceHandle.cs`、`OpenFurnacePanelNode.cs`。
- 固定版配置：`assets/official/rules.json` 的 `gem`、`gem_merge`、`game_settings.GemMergeStorySetting`，提取工具 `scripts/prepare_unity_gem_forge_rules.py`。
- Unity 静态入口：`python -B scripts/check_unity_project.py`（此前通过，38,668 个 GUID）。Unity 批处理日志有一轮完整脚本编译通过：Tundra 411 项成功，包含 `CharacterEquipSlots.cs` 与 `OfflineGemForgeService.cs` 所属程序集；后续高阶材料中心槽接线未包含在该次编译。Unity Play Mode 未运行，不能声称已完成输入或画面验收。
