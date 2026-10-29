# 羁绊冒险

[总索引](../README.md)

状态：未完成

## 功能简介

从游记原羁绊入口进入NPC冒险；原配置880节点，当前按用户意见开放720节点，至第17国第8区第20节点15860。

## 原始官方实现

羁绊冒险读取原 NPC、地图、关卡、邀请资格、站位、战前演出与奖励配置，使用官方对应界面和队伍流程。

### 待迁移的规则与表现

原MapSlot、GeographySlot、旋转、节点状态／头像／动画、挑战按钮、三人选择、刷新和基础奖励说明须接入。每天最多原十次，节点属性按连续实际通关推进。

羁绊战斗阵容读取助战类型6的三名NPC（不是旅行派遣队伍），按原NpcInfo的FigureModelWithWeaponUI／FigureModelUI路径显示原模型，并播放原RunAnimName／IdleAnimName或职业run_03／idle_01。主角播放原100/run_03或200/run_03，角色与地图使用原AnimTimeScale／RotateSpeeds。挑战时停止地图旋转、切换主角和伙伴待机，播放NpcAdventurePanel_CombatTrigger；原动画0.5667秒结束后才提交本地战斗动作。

原奖励说明展示当前区域全部未通关节点；区域全清后，直到下一地区首节点完成再切图。已取得原玩家模型放原槽位，第一节点通过后进入伙伴旅行，不跳到幻兽派遣。

地图、区域、当前节点、下一挑战、旅行候选与新派遣的节点属性共用实际开放范围。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`scripts/prepare_npc_adventure_models.py` 根据固定版 `npc`、`npc_level_detail` 与 `FightEntityId` 确定 95 名可配置羁绊助战候选；固定版 AssetTable 中有 91 名候选的 `FigureModelUI` 与 `FigureModelWithWeaponUI` 原 prefab 路由，并据此转换皮肤合成、SkeletonGraphic、Idle／Run 动画及引用贴图。输入对象、原客户端源码、转换器与输出哈希见 [npc-adventure-models/source-manifest.json](../../assets/reference/npc-adventure-models/source-manifest.json)。运行时按原 `NpcAdventureModelItem` 规则优先选择 WithWeaponUI，缺失时回退 UI 模型，并读取该 NPC 的原 IdleAnimName／RunAnimName。

固定版 AssetTable 的 UI prefab 路由仅覆盖其中 91 名；11015–11018 的 FigureModelUI 与 FigureModelWithWeaponUI 均无路径记录。已取得的固定版 NPC 骨骼包不含这些 prefab；新版 `864_156496` manifest（`.tools/source-packages/cloud-research/current-manifest.v3.bytes`，SHA256 `30d8debb4b9541668321f57c5e116e916d38a8bcb7a78f3b2f3161a902acfbb7`）也无四名 NPC 的对应 UI prefab 路径。因此来源清单分别记录全部候选、91 名可转换候选及四组缺失路径，不重建替代 prefab。

羁绊主角复用固定版男女角色资源；`scripts/prepare_profession_appearance.py` 同时输出原 `100/run_03`、`200/run_03` 职业动作，再由 `scripts/prepare_character_appearance.py --art` 转换到实际使用的男女外观皮肤和武器。战前曲线来自 `assets/official/ui.json` 的 `NpcAdventurePanel_CombatTrigger`（0.5666666627秒）；原 `NpcAdventurePanelMainContent.cs` 先停地图旋转并切换待机，再由 `NpcAdventurePanelMainContentAnimController.cs` 在动画结束后调用挑战。适配沿用项目离线战斗执行，不改原角色、伙伴、动画或战斗配置，不绘制替代素材。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

第19国160节点配置和旧通过记录保留，因原两版本完整清单尚未取得对应专用地图，用户明确暂不开放，取得真实地图后再恢复；不绘制替代地图。已有旅行仍按原派遣快照领取。

节点、次数、奖励和属性由本地事务保存，不以直接写通关取代实际战斗。

## 尚未实现

- 原羁绊入口、NPC模型与主角外观、地图路线、战前触发及真实节点战斗；当前授权只开放720节点至15860。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。
- 第19国真实地图未取得，按用户决定不开放；NPC11015～11022所属联动继续暂停，不以替代模型或已取得资源开放。

## 代码与资源入口

`scripts/prepare_npc_adventure_models.py`（来源工具标识）、`scripts/prepare_profession_appearance.py`（来源工具标识）、`scripts/prepare_character_appearance.py`（来源工具标识）、[NpcAdventureModelItem.cs](../../.tools/source-packages/official-code/client-source/NpcAdventureModelItem.cs)

[NpcAdventurePanelMainContent.cs](../../.tools/source-packages/official-code/client-source/NpcAdventurePanelMainContent.cs)、[NpcAdventurePanelMainContentAnimController.cs](../../.tools/source-packages/official-code/client-source/NpcAdventurePanelMainContentAnimController.cs)、[ui.json](../../assets/official/ui.json)、
