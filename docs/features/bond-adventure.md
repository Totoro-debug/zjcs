# 羁绊冒险

[总索引](../README.md)

状态：未完成

## 功能简介

羁绊冒险读取原 NPC、地图、关卡、邀请资格、站位、战前演出与奖励配置，使用官方对应界面和队伍流程。

## 原始官方实现

羁绊冒险读取原 NPC、地图、关卡、邀请资格、站位、战前演出与奖励配置，使用官方对应界面和队伍流程。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`scripts/prepare_npc_adventure_models.py` 根据固定版 `npc`、`npc_level_detail` 与 `FightEntityId` 确定 95 名可配置羁绊助战候选；固定版 AssetTable 中有 91 名候选的 `FigureModelUI` 与 `FigureModelWithWeaponUI` 原 prefab 路由，并据此转换皮肤合成、SkeletonGraphic、Idle／Run 动画及引用贴图。输入对象、原客户端源码、转换器与输出哈希见 [npc-adventure-models/source-manifest.json](../../assets/reference/npc-adventure-models/source-manifest.json)。运行时按原 `NpcAdventureModelItem` 规则优先选择 WithWeaponUI，缺失时回退 UI 模型，并读取该 NPC 的原 IdleAnimName／RunAnimName。

固定版 AssetTable 的 UI prefab 路由仅覆盖其中 91 名；11015–11018 的 FigureModelUI 与 FigureModelWithWeaponUI 均无路径记录。已取得的固定版 NPC 骨骼包不含这些 prefab；新版 `864_156496` manifest（`.tools/source-packages/cloud-research/current-manifest.v3.bytes`，SHA256 `30d8debb4b9541668321f57c5e116e916d38a8bcb7a78f3b2f3161a902acfbb7`）也无四名 NPC 的对应 UI prefab 路径。因此来源清单分别记录全部候选、91 名可转换候选及四组缺失路径，不重建替代 prefab。

羁绊主角复用固定版男女角色资源；`scripts/prepare_profession_appearance.py` 同时输出原 `100/run_03`、`200/run_03` 职业动作，再由 `scripts/prepare_character_appearance.py --art` 转换到实际使用的男女外观皮肤和武器。战前曲线来自 `assets/official/ui.json` 的 `NpcAdventurePanel_CombatTrigger`（0.5666666627秒）；原 `NpcAdventurePanelMainContent.cs` 先停地图旋转并切换待机，再由 `NpcAdventurePanelMainContentAnimController.cs` 在动画结束后调用挑战。适配沿用项目离线战斗执行，不改原角色、伙伴、动画或战斗配置，不绘制替代素材。

## 完整还原部分

尚无完整 Unity 流程的完成结论。

## 自定义实现

开放与本地保存遵循[共同离线政策](offline-access.md)，具体本地接线和未确认规则见下文。

## 尚未实现

接通官方代码、原生界面／资源、本地状态与存档，完成实际入口、模拟输入、画面及失败恢复验证，并发布对应 Windows 版本。
