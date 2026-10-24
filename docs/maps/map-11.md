# 11 森之国

[总索引](../README.md)

状态：未完成

## 地图简介

地图 ID：11。原目录：坎斯汀大陆/森之国。场景类型：`map_world`；原区域配置77项，BGM配置11。

当前验收对象为 Unity / C#。

## 完整还原部分

尚无本图在 Unity 完整运行并验收的结论。

已转换并放入 `unity/Assets/Official/World/Map/11/` 的官方载荷：地图定义含 77 区、81 面积配置、1,803 个放置实体及其一一对应的实体组件、导航和 8 条区域开放条件；绑定 110 条实体进度参数、247 条实体显示、128 条实体 UI、5 条阶段动作，以及地图、区域、逻辑实体配置。Map 11 有 17 个普通女神像（`EntityClassId=6010`），其所在区域各配置一组 `DropItems`；另有 49 个失落女神像（`EntityClassId=6011`），其所在区域各配置一组 `GoddessActivePropAwardDict`。1045～1160 范围含 24 条主线记录。地形几何为 14,427 批次，引用的 475 张官方纹理已复制至 `Resources/Map11/Textures/`；小地图为 85 批次并绑定 2 张官方纹理。输入版本固定为 `854_156383`，来源和输出哈希见该目录的 `*-source-manifest.json`。转换器为 `scripts/prepare_unity_map11_rendering.py`、`scripts/prepare_unity_map10_minimap.py --map-id 11` 和 `scripts/prepare_unity_map11_definition.py`。地形视图已支持原始 Map11 几何头格式，保留 Map10 默认格式。

实体载荷另含 1,803 个放置记录、105 个模型绑定、97 份官方几何、27 个 Spine 实例和 201 张实体贴图。`8210–8213` 世界奖励实体按官方 `Map/entity_map_display` 绑定到公共单物品模板；展示表有常规地面与浅水两个版本，当前转换载荷保留常规地面版本。剩余 9 个无模型实例属于 `5110` 和 `6533`：`6533` 的官方逻辑视图记录明确标记为无显示实体；`5110` 目前未找到实体显示表绑定或模型，需要继续核对其正式交互表现。战斗载荷已转换为 124 个官方阶段、54 个怪物编队数组和对应战斗场景，保留玩家等级／位阶动态阶段及官方等级属性缺行语义；转换器为 `scripts/prepare_unity_map10_battle_data.py --map-id 11`，输出哈希见 `battle-source-manifest.json`。`OfficialMap11Tools` 已扩展为生成地形、小地图和实体 prefab、绑定 ClassId 11 目录并验证定义、几何及寻路，但尚未在 Unity 执行；主资源目录尚未实际写入 ClassId 11。当前 Unity 编辑器处于 Safe Mode，尚无导入、编译或 Play Mode 证据。静态来源哈希、数据转换和寻路检查不代表本图完整玩法已接入。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

原线上组队按已确认原剧情 NPC 资格、等级、位阶和技能适配，不生成真人玩家。公共刷新使用本地时钟；普通路线保留真实败北、产出和等待，后期资格回访不能记作普通角色自然成长。普通女神像和失落女神像均在首次激活时自动结算各自区域配置奖励，并将激活和奖励回执一起持久化，避免重复领取；这是用户确认的离线处理要求，当前代码尚未接通。

## 尚未实现

恢复 77 区、九组踏板、六块战斗碑、森林老人／世界树／狮鹫剧情与 1045～1160 主线；验证个人资格、公共化、全区激活与 6001 出口条件，不能仅按任务 1160 完成推断可以离图。

按原地图配置恢复并接入场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口。Map11 的区域开放条件已从固定 APK 中提取并写入定义，但尚未与本地任务条件状态联动。原版本中地图 NPC、建筑、世界奖励、交互物、怪物及小地图 prefab bundle 已从固定 APK 提取并校验，保存在 `.tools/source-packages/history/854_156383/extracted/`；尚未转换为 Unity 原生资源或完成组件及实体绑定。map10 的 2,683 行专用运行时包含地图专属剧情和战斗假设，不能直接代替 map11。`map11-rendering.json`、`map11-lighting.json`、`map11-stone-animations.json`、`map11-logic-views.json`、`map11-dialogs.json` 仍未接入运行时；场景对象、动态动画／特效和音频还需逐项恢复绑定。原 `MapGround_11` 和 `MiniMap_11` prefab 尚未恢复为 Unity prefab，Windows 画面及输入也未验证。

普通女神像与失落女神像是不同机制。`WorldGoddessInput` 打开 `GoddessTouchDialog`；区域进度满足 `StageConRegionProgressFullComponent` 后，`StageTouchUI` 通过地图阶段交互启动激活阶段。客户端 `GoddessActiveableManager.OnMapStatusUpdate` 在收到区域状态变化后弹出祝福等级变化提示；`ActiveGoddessHandler` 监听激活实体更新并显示女神像地图通知。失落女神打开 `LostGoddessTouchDialog`，`LostGoddessFurniturePropComponent.GetPropDict()` 从本区域 `GoddessActivePropAwardDict` 读取奖励；激活区域状态更新后客户端显示奖励提示。官方客户端只展示服务端已结算的状态和奖励，没有本地发放逻辑。按用户确认，单机版在两类像各自首次激活时自动结算配置奖励，持久化激活与结算状态并防止重复领取；该规则已确认但尚未接入。`WorldTouchDialogLostGoddness` 的 `PlayerGetLostGoddessAwardRequest` 是另一个“找回遗失女神奖励”流程，不是失落女神像激活奖励。地图探索进度里程碑奖励通过 `MapGoddessExpProcessItem` 手动领取；命运果实则是独立的日常领取与探索入口。

- 完成本图独立的本地资源绑定、玩家入口、正常游玩、战斗／交互结算、失败回滚、退出与冷读恢复。
- 在 Unity 编辑器实际运行，并验证 Windows 目标表现；

### 依据与代码定位

官方转换数据：[regions/11.json](../../assets/official/regions/11.json)、[adventure.json](../../assets/official/adventure.json)、[map11-rendering.json](../../assets/official/map11-rendering.json)、[map11-lighting.json](../../assets/official/map11-lighting.json)、[map11-stone-animations.json](../../assets/official/map11-stone-animations.json)、[map11-logic-views.json](../../assets/official/map11-logic-views.json)、[map11-dialogs.json](../../assets/official/map11-dialogs.json)。Unity 载荷：[map11.json](../../unity/Assets/Official/World/Map/11/map11.json)、[OfficialMap11Tools](../../unity/Assets/Game/Editor/OfficialMap11Tools.cs)。

Unity 共享地图接入：[OfficialWorldMapAssetCatalog](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapAssetCatalog.cs)、[OfficialWorldMapSession](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapSession.cs)。map10 运行时仅作架构参考：[OfficialMap10GameRuntime](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap10GameRuntime.cs)。官方地图逻辑与特效参考：[MyPlayableManager](../../.tools/source-packages/official-code/playable-manager-source/GameFramework.MyPlayableManager.decompiled.cs)、[official_particle_codec.py](../../scripts/official_particle_codec.py)。
