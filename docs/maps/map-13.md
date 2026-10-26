# 13 泽之国

[总索引](../README.md)

状态：未完成。官方配置与水路规则已恢复，完整地图尚未接通、运行验证或发布。

## 地图简介

地图 ID 为 13，原目录为坎斯汀大陆／泽之国，场景类型为 `map_world`。官方配置包含 106 个地区、2,690 个放置实体，BGM 配置为 13，后继地图为 16。地形入口为 `Assets/Partition/MapGround_13/MapGround_13_bare.prefab`。

迁移使用 `official-source.json` 选择的 `871_156598`。APK 基础清单为 `854_156383`；按所选索引先核对 APK 实际文件，再读取校验通过的云端缓存，保留各自版本与哈希。

## 完整还原部分

尚无本图在 Unity 完整运行并验证的结论。以下为已完成的来源恢复和规则模块，不能代替实际可游玩的地图：

- 复用已核验的当前官方 `Console.dll`、`Assembly-CSharp.dll` 及反编译结果，按原 formatter、构造器和序列化 schema 解码导航、放置实体、163 个模板／动作、93 张相关表、88 份客户端剧情图及对应服务端配置记录。组件、条件、结果和剧情连接保留原顺序，服务端配置记录不视为服务端执行代码。
- 对白记录按当前 `DialogInfoParser` 使用的 Luban `dialog` 表读取，保留原对白顺序、角色、表情与配音键；中文文本按 `LanguageUtils` 的原键规则，从所选版本的中文 rawfile 读取并校验，不使用旧版 CSV 补缺。
- 五组水路共 126 个相关实体、10 个浮台的原交互范围、刚体、地面覆盖、状态、状态更新顺序、触发关系与路径已接入独立 C# 规则模块。保留 5328 配置中重复的 5324 更新及 `UseLoEntDefaultState` 语义。
- 原生场景的 104 份来源文件及完整 CAB 依赖已核验；恢复 4,314 个组件的序列化描述，严格消费全部字段，并逐字节核对转换前后 23,818 个对象数据。涵盖 200 份地形分块、630 个原生文字组件、173 个自定义灯光、265 个浸没组件及 46 个骨骼实例。APK 内置资源整体保留，不改写其内容。
- 导出工具不支持 `SerializeReference`，已核对本闭包唯一的 `StateGameObjectConfig` 类型，为转换输入补充等价结构描述；保留原引用 ID、类型、字段和配置顺序，并与原解码值全量比较。重导出的分块和区域标签引用已恢复；这只证明转换输入和中间 prefab 数据，不代表 Windows 场景已可用。
- 从所选版本的官方 `CustomWater.dll` 反编译并直接迁移 `WaterController`、`PartialSubmersion`、`SeaRendererReplacement`、`FordingSpriteRenderer` 和 `WaterMaskRenderer`。仅增加命名空间，保留原数值、字段、回调与执行顺序；尚未完成场景脚本／贴图绑定。
- 从原始 GPU 程序机械转换五类水面 shader 的全部 16 个 GLES 变体，保留原算式顺序、材质属性默认值与属性标记、顶点输入／插值数据、pass 标签，以及混合、深度、裁剪和颜色写入状态；原 GLES 文本随来源载荷保留。Windows D3D 的 16 组顶点／片元程序均已在 DX12 Unity 编辑器通过编译检查，尚无原场景中的渲染对照结论。
- 从 APK 原生 IL2CPP 方法注册与 ARM64 指令恢复 `Light2D` 的四个衰减字段、材质键第 7 位、仅 Freeform 启用的 `USE_RECESSION` 关键字，以及绘制前按原顺序设置的中心／半径／边缘参数。使用本机 Unity 附带的 URP 14.0.11 作为可编译依赖基线；它不是完整游戏渲染管线的反编译替身。嵌入包保留运行时、编辑器、shader、纹理、许可证和元数据，只去除文档／示例／包测试。
- 原版形状灯光 shader 的全部 32 个 GLES 变体已机械转换并通过 Windows D3D 顶点／片元编译，含原 `USE_RECESSION` 算式；矩阵按原列向量语义恢复。两套 `Renderer2DData` 和 `UniversalRenderPipelineAsset` 已按 APK 字段顺序严格解码，保留默认 Renderer 索引 0 和完整原参数；尚未将这些配置绑定为原生管线资产，也未切换项目渲染设置。
- 从当前官方代码迁移 11 个地形／分区数据类型，保留字段、枚举、坐标公式和分块顺序。`MyTile.GetTileData` 的反编译虚调用按程序集 IL 修复为原 `base.GetTileData`；只适配命名空间、C# 构造器和错误日志，去掉未使用的网络序列化属性。
- 原生地形依赖闭包 1,759 份资产已导入 Windows Unity，包含 200 个区块、768 个 Tile、764 个原生 Sprite 与 26 张贴图。保留原 Sprite 网格／UV／轴心和 Tile 引用；从原 ASTC 数据按原线性／sRGB 设置解码，关闭额外有损压缩、4096 图集降采样与透明颜色扩展，保持过滤／重复／各向异性／mip 参数。使用 Unity 实际内置 Tile 脚本 13312，未另造 Tile 替身。
- `Resources/Map13/Native/native-terrain-catalog` 已显式绑定 200 个原区块路径及 `partition_data`。DX12 编辑器逐项验证资产输入哈希、GUID、原生引用、Sprite 网格、贴图尺寸和采样，以及全部 201 条目录加载；转换输入元数据和 Unity 规范化后的元数据分别记哈希。尚未实例化正式地形场景。
- 官方 `PartitionLoader`、`PartitionController`、`PartitionAsset`／`Tile`／`Scene`／`Extension` 和三种状态枚举已迁移。保留原九宫格顺序、区块／Tilemap 引用计数、逐格矩阵、完整及局部区块填充／清除、900 帧闲置和超过 5 帧的卸载间隔。资源句柄、实例绑定、相机与世界状态通过明确的本地接口接入；低内存通知使用 Unity 原事件。原 UniTask 延续按反编译状态机恢复为协程，保留 5 秒实时夜层移除及预递增、最多 120 次的就绪等待；GM 专用版本提示尚未接入。
- 使用与主工程源文件／资产逐字节一致的隔离 Unity 工程，在内置屏 DX12 Play Mode 实际运行全部 200 个原区块：延迟资源完成、未激活时预载、启用／禁用、清除／重载、移除和待加载时移除均通过；2,444 组含偏移的 Tilemap 项逐格验证原 Tile 与矩阵。本图 200 份来源均为完整区块，局部区块分支已迁移但未由本图覆盖。测试使用专门的 Tilemap 逻辑夹具，不是正式地形场景，没有覆盖地图画面、模拟鼠标、区域世界状态、界面或存档。

- 官方 `MapGroundRoot`、`MapEnvEffect`、`MapEffect`、`MapLight`、`MapRegionLight`、`RegionLightHub`、环境数据、场景跟随与相机剔除等 12 个类型已迁移。保留原灯光异步状态机及 Update 调度、取消、原强度字典、严格大于时长的结束条件；依赖注入改为明确的本地服务接口。全部 117 个剔除组件的原骨骼列表均为空，列表接口允许免费原生动画的 Behaviour 适配，原启停循环保持不变。
- 原 `MapEnvData` 通过所选索引先核对 APK 后从校验云端包读取：消失前延迟 0.5 秒、消失 1 秒、熄灯 2 秒。已生成并显式绑定 `Resources/Map13/Native/map-env-data`。主工程及同源隔离工程的 Play Mode 通过 106 个区域槽位、原注册／昼夜切换、两秒渐变、取消、衰减中心、场景启停；使用测试夹具，尚未覆盖正式世界状态服务。
- 地形／熔岩 shader 的 4 个原 pass、全部 20 个 GLES 变体已转换并逐顶点／片元通过 Windows D3D 编译。地形保留 `Universal2D`、法线和前向 pass；独立保留各 pass 的原程序、材质属性与渲染状态。
- 原 `MapGround_13_bare.prefab` 的依赖闭包已绑定：228 份新增原生资产、93 份复用地形资产、实际官方脚本、8 份原材质及 shader。7 张额外贴图保留全部 54 个原 mip，按原线性／sRGB 配置解码成原生 RGBA32 Texture2D；保留原底部起始行顺序、过滤和重复等参数，未重新生成 mip 或额外压缩。纹理和同名 Sprite 使用独立资产路径，原生纹理引用使用 Unity 的正确类型标记。
- 内置屏 DX12 编辑器验证全部新增资产及引用、完整 mip 参数、地形主体无缺失脚本，并生成含 202 条原路径的 `Resources/Map13/Native/native-scene-catalog`。同源隔离 Play Mode 实际加载该目录／prefab，并从 GPU 回读 7 张贴图的全部 54 个 mip，每字节哈希均与官方 ASTC 解码数据一致，核对颜色及行顺序。尚未实例化完整官方地图、渲染对照、模拟鼠标或正式存档。

`scripts/test_map13_water.ps1` 已实际通过：10 个浮台完整多段往返、载人路径、阻挡后状态回滚、五终端开放／重置范围、已开放道路保留、原水位状态切换、JSON 冷读和越界存档拒绝。它直接调用规则模块，不覆盖鼠标输入、场景动画、背包扣费或正式存档接线。

工程静态检查及 Unity `2022.3.57f1c2` 的 DX12 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 已通过。全局校验中的地图资产检查针对 Map10，不代表 Map13 已运行或画面正确。

## 自定义实现

官方 `WorldConveyanceComponent.FireTrigger()` 没有服务器移动实现，水位执行同样缺少服务端逻辑；已核对其客户端载人实现、`LoWorldConveyanceComponent.OffsetPath`、`StageResultElevatorStateUpdateComponent` 和原 `StageDatas`。用户已明确允许以下单机规则，规则模块已实现：每次触发沿原配置顺序前进一段，到末端后沿原路径逐段反向返回；载人保留原偏移，路径被阻挡则本次不移动；水位按原 `StageDatas` 顺序更新，不发奖励。

五组终端为 2634／3337／5334／7439／9338。已确认：重置只恢复 `NeedResetLoEntityIds` 的原始状态与位置；自动解谜消耗原道具并开放 `ExtendRoadLoEntityIds`；奖励仍须实际领取且不重复发放。规则模块只执行道路开放与重置，原扣费交易、终端状态、独立奖励领取及中断恢复尚待接入。

## 尚未实现

- 地形主体 prefab、原材质、水面／地形／熔岩 shader 和昼夜组件已绑定或迁移；仍须恢复原渲染管线资产、遮罩层／排序层、动态分区、地区标签、粒子、骨骼、导演和音频，完成完整 Windows 场景表现。`WaterMaskRenderer` 的运行代码已迁移，其他特效 shader 与剩余组件／资源仍待接入；原生主体资产导入不代表完整地图已可游玩。
- 接通泽之国入口、正常移动、海之宫剧情、探索／任务、战斗、交互、条件、奖励、小地图和地图出口，以及五组手动水路和终端原界面。
- 将水路位置、方向、水位、终端扣费意图和奖励凭据接入正式本地存档，验证取消、条件不足、重复操作、中断和重新启动。
- 在内置屏使用 DX12 实际运行 Play Mode，经模拟鼠标命中完成受影响流程，查看运行画面；构建并真实启动 Windows Player，验证后发布至 `D:/Desktop/杖剑传说/杖剑传说.exe`。目前没有包含本图完成成果的发布版本。

来源与入口：

- [来源解码工具](../../scripts/prepare_unity_map13_sources.py)、[水路投影工具](../../scripts/prepare_unity_map13_water.py)、[规则测试](../../scripts/test_map13_water.ps1)。
- [水路规则模块](../../unity/Assets/Game/Runtime/Offline/World/OfflineMap13WaterSession.cs)、[序列化定义](../../unity/Assets/Game/Runtime/Offline/World/Map13WaterDefinition.cs)；原始配置输出为 `unity/Assets/Official/World/Map/13/Source/`，水路显式资源路径为 `Resources/Map13/water-rules.json`，尚无地图运行时加载绑定。
- [当前配置来源清单](../../assets/reference/official-map/map13-source-manifest.json)、[当前水路来源清单](../../assets/reference/official-map/map13-water-source-manifest.json)记录输入／输出与代码哈希。历史 `854_156383` 的 `materials-13`、`dialogs-13`、`minimap-13`、`lighting-13` 来源清单独立保留，不作为当前 Unity 原生资产已经恢复的证据。
- [原生转换输入工具](../../scripts/prepare_unity_map13_native.py)、[序列化描述](../../scripts/map13_native_schemas.py)及[输入来源清单](../../assets/reference/official-map/map13-native-input-source-manifest.json)记录官方文件、程序集、APK 元数据、参考程序集和对象校验；组件字段保存于 `Source/native-components.json`。[官方水面代码迁移工具](../../scripts/prepare_unity_map13_water_components.py)及[代码来源清单](../../assets/reference/official-map/map13-native-water-code-source-manifest.json)记录解码程序集、ILSpy 工具、原反编译文件与 C# 输出哈希，入口为 `Runtime/Official/World/NativeWater/`。本次新增载荷未进入完整素材快照。
- [水面 shader 转换工具](../../scripts/prepare_unity_map13_water_shaders.py)、[来源清单](../../assets/reference/official-map/map13-water-shaders-source-manifest.json)和[逐变体编译检查](../../unity/Assets/Game/Editor/OfficialMap13WaterShaderTools.cs)；原 GLES 程序位于 `Source/Shaders/`，译文位于 `Runtime/Official/World/NativeWater/Shaders/`。这些源程序和译文均尚未进入完整素材快照。
- [地形代码迁移工具](../../scripts/prepare_unity_map13_terrain_components.py)及[代码来源清单](../../assets/reference/official-map/map13-native-terrain-code-source-manifest.json)记录原程序集、反编译文件、修复 IL 和输出；代码入口为 `Runtime/Official/World/NativeTerrain/`。[原生地形转换工具](../../scripts/prepare_unity_map13_native_terrain.py)、[素材来源清单](../../assets/reference/official-map/map13-native-terrain-source-manifest.json)、[Unity 绑定清单](../../assets/reference/official-map/map13-native-terrain-binding-source-manifest.json)记录 ASTC 输入／解码像素、ARM 解码器、素材及导入元数据哈希；载荷位于 `Assets/Official/World/Map/13/NativeTerrain/`，[导入与绑定检查](../../unity/Assets/Game/Editor/OfficialMap13TerrainTools.cs)生成本地目录。该批素材未进入完整快照。
- [分区执行迁移工具](../../scripts/prepare_unity_map13_partition.py)及[代码来源清单](../../assets/reference/official-map/map13-partition-code-source-manifest.json)记录所选程序集、原控制器／状态机、适配和输出哈希。本地边界位于 `Runtime/Offline/World/NativePartition/`，目录资源句柄与实例绑定位于 `Runtime/Offline/World/NativeMap13/`；[Play Mode 分区检查](../../unity/Assets/Game/Editor/Map13/OfficialMap13PartitionAcceptance.cs)只覆盖上述原区块生命周期。

- [渲染依赖恢复工具](../../scripts/prepare_unity_map13_render_pipeline.py)及[来源清单](../../assets/reference/official-map/map13-render-pipeline-source-manifest.json)记录 Unity URP 基线每文件输入／输出、APK 原生方法地址／元数据索引／哈希及适配；[原灯光 shader 转换](../../scripts/prepare_unity_map13_light_shader.py)、[来源清单](../../assets/reference/official-map/map13-light-shader-source-manifest.json)及[逐变体检查](../../unity/Assets/Game/Editor/Map13/OfficialMap13LightShaderTools.cs)记录全部 32 个程序；[管线配置解码](../../scripts/prepare_unity_map13_pipeline_config.py)与[配置来源清单](../../assets/reference/official-map/map13-render-config-source-manifest.json)记录原资产及 schema 参考程序集。新增灯光源程序与配置未进入完整素材快照。

- [异步依赖安装](../../scripts/prepare_unity_map13_async_dependency.py)及[MIT 上游包来源](../../assets/reference/official-map/map13-async-dependency-source-manifest.json)记录 UniTask 2.5.10 ZIP、许可和逐文件哈希；此为兼容依赖基线，不声称恢复了游戏所用的确切包版本。[环境代码迁移](../../scripts/prepare_unity_map13_environment.py)、[代码来源](../../assets/reference/official-map/map13-environment-code-source-manifest.json)、[配置绑定](../../assets/reference/official-map/map13-environment-binding-source-manifest.json)和[Play Mode 检查](../../unity/Assets/Game/Editor/Map13/OfficialMap13EnvironmentAcceptance.cs)记录原状态机、配置、接口与验证范围。
- [地形／熔岩 shader 转换](../../scripts/prepare_unity_map13_scene_shaders.py)及[来源](../../assets/reference/official-map/map13-scene-shaders-source-manifest.json)记录各原 pass 程序；[原生主体绑定工具](../../scripts/prepare_unity_map13_native_scene.py)、[来源](../../assets/reference/official-map/map13-native-scene-source-manifest.json)及[Unity 绑定清单](../../assets/reference/official-map/map13-native-scene-binding-source-manifest.json)记录主体闭包、完整 ASTC mip、原生 Texture2D 序列化、GUID 与实际导入哈希，[编辑器工具](../../unity/Assets/Game/Editor/Map13/OfficialMap13NativeSceneTools.cs)验证绑定。新增地形主体和贴图未进入完整素材快照。
