# 14 龙之国

[总索引](../README.md)

状态：未完成。当前官方配置、原生地形、裸场景与机关规则已恢复并分项验证；完整分区、实体、正式玩法、界面、存档与 Windows 发布尚未完成。

## 地图简介

地图 ID 为 14，原目录为坎斯汀大陆／龙之国，场景类型为 `map_world`。原配置包含 111 个地区、2,961 个放置实体，导航为 280×280，原点为 `(-134.5, -134.5, 0)`。BGM 配置为 14，后继地图为 15，默认入口为 101，地形入口为 `Assets/Partition/MapGround_14/MapGround_14_bare.prefab`。

使用 `official-source.json` 选择的 `871_156598`；APK 基础清单为 `854_156383`。先核对 APK 清单、实际包及内部对象，再读取所选索引下已校验的云端缓存，来源及哈希分别记录，不使用旧版数据补缺。

## 完整还原部分

- 当前官方程序集及客户端／共享反编译代码已经核验，按原 formatter、构造器与 schema 解码本图导航、放置、169 个模板／动作、93 张关联表、85 份客户端剧情图及对应服务端配置记录。对白为当前 Luban 表的 64 组，保留原顺序、中文键、角色、表情与配音键。配置记录不视为服务端执行器。原脚本中误指向地图 13 的输入已纠正；本图区域和实体分别读取 `mapinfowrap_14.g.bytes` 与 `map_14.g.bytes`。
- 五个原地图 bundle 及 CAB 闭包共 113 份输入已提取、解码并核对。5,400 个原组件按元数据顺序严格消费字段，29,773 个对象在补齐序列化描述后逐字节核对。`SkeletonAnimation` 仅恢复原序列化 schema，包括私有 `_animationName`，与独立骨骼组件解析器逐字段比较；此步骤不实现或替代 Spine 动画运行代码。
- 原生地形闭包包含 2,386 份资产、240 个区块、1,046 个 Tile、1,045 个 Sprite 及 54 张贴图。裸场景另新增 270 份资产并复用地形 102 份依赖，新增 10 张纹理的 47 个 mip 全部保留。保留原层级、坐标、网格、UV、轴心、逐格矩阵、Tile 指针及材质引用。ASTC 解码为原生 RGBA32，保留线性／sRGB、行序、过滤、重复与各向异性，不增加有损压缩、透明颜色扩展或降采样。
- 本图新增的原 `Universal Render Pipeline/2D/Movable-Sprite` GLES 程序已机械转换为 Windows shader，保留原属性、算式与渲染状态，并通过 DX12 实际材质 pass 编译。其余依赖复用已迁移且核对同一原对象的水面、地形和场景 shader。显式目录为 `Resources/Map14/Native/native-terrain-catalog`（241 路径）与 `native-scene-catalog`（242 路径）；裸场景没有丢失脚本。
- Unity 2022.3.57f1c2 内置屏 DX12 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。Play Mode 在隔离场景逐一运行全部 240 个完整区块的原 `PartitionLoader` 加载、激活、清除、重载、卸载及未完成异步移除；核对 2,094 个含偏移的 Tilemap 记录及原矩阵。本图没有局部区块，未以本图覆盖该分支。10 张裸场景纹理的 47 个 mip GPU 回读字节一致。已查看两处实际地形画面；该夹具只加载单区块，未加载邻区块、水面与环境分区，不能视为完整地图画面验收。
- 十二终端的完整依赖闭包为 109 个实体，包含 27 个去重火炮目标、60 个走格子踏板、五把锁及五个独立奖励。原目标列表中的重复项保留于载荷，仅在执行清障时去重。踏板保留原偏移、访问次数、来向限制、公／私图启用及解锁后停用规则；`1704`、`4206`、`4208`、`4508`、`4511` 需要两次访问，八个踏板限制来向。完成锁按原顺序执行生成奖励、更新终端和自增状态。
- 当前 EC 索引、全部放置类及完整显示状态选择表解析出 416 个实际 prefab、33 个根 bundle；补齐 CAB 和原动态贴图加载依赖后为 1,544 份输入、3,228 条动态贴图绑定。25,419 个原组件严格恢复序列化字段，2,643 个托管引用注册表以可逆临时描述交给转换器；重新编码、严格读取并重开后核对原字段，117,858 个内部对象保留其余原载荷字节。`CinemachineComposer` 的 15 个序列化字段、类型、继承、属性与 APK 元数据核对后补齐描述；没有实现替代相机逻辑。当前成果为转换输入及字段来源，尚未完成实体原生引用、行为、骨骼和正式场景接入。
- 四个踏板原活动结果为传送：`3604→3609`、`4201→4205`、`4504→4506`、`4509→4512`，原 ActionId 为 `22081`。目标按原 `StageResultTransportComponent.TryGetTargetPos` 读取放置坐标再加 Offset，结果通过显式宿主回调交付；尚未接入正式角色移动和动作播放。没有传送宿主时明确报错，不忽略原结果。
- `Map14/Native/puzzle-catalog` 显式绑定原规则与哈希、十二终端的 `325×1` 原费用。内置屏 DX12 Play Mode 通过七组火炮清障／重置范围、重复目标、状态与坐标保留，五组走格子原次数／来向、超次数不完成、手动条件、辅助、独立奖励状态保留、组间隔离、传送目标／动作参数、JSON 冷读及坏哈希拒绝；地图 11、12 的共享机关回归、全局校验与静态检查通过。该验证直接执行规则，没有模拟鼠标、正式地图路线、扣费、实际领奖或磁盘存档。

## 自定义实现

以下为用户确认并已接入独立规则层的单机处理；正式地图宿主、菜单、背包交易和存档尚未接通。

火炮终端共 7 个：`1512`、`2914`、`5015`、`6912`、`8011`、`8516`、`9413`，原类型为 `MapWorldPuzzleType.Cannon=4`。自动解谜使用原实体类 `6502` 的道具费用 `325×1`，仅清除该终端 `NeedRemoveLoEntityIds` 中的障碍，重复目标去重。不模拟炮弹发射、成功演出或失败状态，不生成额外奖励。重置只清除这些障碍的 `destroyed` 标记，不改变终端状态、玩家位置、其他机关、费用或奖励。

五组走格子终端为 `909`、`1715`、`3612`、`4214`、`4505`，放置配置将 `PuzzleType` 覆盖为 6；分别指定辅助锁 `903`、`1708`、`3613`、`4215`、`4517`。用户确认沿用[鲸背岛](map-16.md)方案：辅助消耗 `325×1`，仅解开配置指定的锁；重置只恢复本组踏板和辅助锁，保留奖励及领取记录；完成后按原顺序执行奖励生成链。当前 Common／客户端只有自动完成和重置标记，没有服务端执行器，不能将此适配写成官方服务器实现。

## 尚未实现

- 恢复全部环境分区、地区标签、实体 prefab、骨骼、粒子、动画、MiniMap_14 迷雾／标记、BGM 与剧情音频，并接入官方 HUD、对话和交互界面。
- 接通 111 区、2,961 个放置实体、8 块战斗碑、3 处遗迹、7 组火炮、5 组走格子及地图 15 原出口的正式入口、原条件、剧情、交互、战斗与奖励。不得以配置提取或地形夹具代替完整玩法。
- 接通背包交易、本地状态和存档，完成正常游玩、失败回滚、退出与冷读恢复；上述资源验证未模拟鼠标，也未覆盖正式业务或存档。
- 完成编辑器与 Windows Player 的受影响流程、模拟输入、完整画面及存档验证，提交并发布包含本图成果的 Windows 可执行版本。当前没有本图可游玩版本交付结论。

### 官方来源与入口

- [配置生成器](../../scripts/prepare_unity_map14_sources.py)、[配置来源](../../assets/reference/official-map/map14-source-manifest.json)；载荷为 `Assets/Official/World/Map/14/Source/`。
- [原生输入恢复](../../scripts/prepare_unity_map14_native.py)、[输入及组件来源](../../assets/reference/official-map/map14-native-input-source-manifest.json)。
- [实体输入恢复](../../scripts/prepare_unity_map14_entity_inputs.py)、[原组件序列化描述](../../scripts/map14_entity_native_schemas.py)及[实体／显示／动态贴图来源](../../assets/reference/official-map/map14-native-entity-input-source-manifest.json)；完整原字段载荷为 `Source/native-entity-components.json`，临时转换输入保存在 `.tools/map14-entity-native-cache-871_156598/`，不进入运行时。
- [机关投影](../../scripts/prepare_unity_map14_puzzles.py)、[官方代码／规则来源](../../assets/reference/official-map/map14-puzzles-source-manifest.json)、[本地绑定](../../assets/reference/official-map/map14-puzzles-binding-source-manifest.json)及[Play Mode 规则检查](../../unity/Assets/Game/Editor/Map14/OfficialMap14PuzzleAcceptance.cs)。运行入口为 `Map14PuzzleAssets` 与共享 `OfflineMapPuzzleSession`；`AuxiliaryUnlockOnly` 仅对已确认的本图五终端启用，不改地图 11／12 的原辅助行为。
- [地形转换](../../scripts/prepare_unity_map14_native_terrain.py)、[地形来源](../../assets/reference/official-map/map14-native-terrain-source-manifest.json)及[Unity 绑定](../../assets/reference/official-map/map14-native-terrain-binding-source-manifest.json)。
- [裸场景转换](../../scripts/prepare_unity_map14_native_scene.py)、[裸场景来源](../../assets/reference/official-map/map14-native-scene-source-manifest.json)及[Unity 绑定](../../assets/reference/official-map/map14-native-scene-binding-source-manifest.json)。
- [原 shader 转换](../../scripts/prepare_unity_map14_scene_shaders.py)、[shader 来源](../../assets/reference/official-map/map14-scene-shaders-source-manifest.json)、[导入检查](../../unity/Assets/Game/Editor/Map14/OfficialMap14NativeSceneTools.cs)及[区块运行检查](../../unity/Assets/Game/Editor/Map14/OfficialMap14PartitionAcceptance.cs)。共享原地形与环境代码位于 `Runtime/Official/World/NativeTerrain/`、`NativeEnvironment/`，本地加载边界位于 `Runtime/Offline/World/NativePartition/`。

新增原生素材独立保存在 `Assets/Official/World/Map/14/`，尚未生成完整素材快照，不能声称已进入 `asset-pack` 备份。
