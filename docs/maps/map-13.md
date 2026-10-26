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
- 从所选版本的官方 `CustomWater.dll` 反编译并直接迁移 `WaterController`、`PartialSubmersion`、`SeaRendererReplacement`、`FordingSpriteRenderer`。仅增加命名空间，保留原数值、字段、回调与执行顺序；尚未完成场景脚本／贴图绑定。

`scripts/test_map13_water.ps1` 已实际通过：10 个浮台完整多段往返、载人路径、阻挡后状态回滚、五终端开放／重置范围、已开放道路保留、原水位状态切换、JSON 冷读和越界存档拒绝。它直接调用规则模块，不覆盖鼠标输入、场景动画、背包扣费或正式存档接线。

工程静态检查及 Unity `2022.3.57f1c2` 的 DX12 导入／编译、`Zjcs.Editor.ProjectTools.Validate` 已通过。全局校验中的地图资产检查针对 Map10，不代表 Map13 已运行或画面正确。

## 自定义实现

官方 `WorldConveyanceComponent.FireTrigger()` 没有服务器移动实现，水位执行同样缺少服务端逻辑；已核对其客户端载人实现、`LoWorldConveyanceComponent.OffsetPath`、`StageResultElevatorStateUpdateComponent` 和原 `StageDatas`。用户已明确允许以下单机规则，规则模块已实现：每次触发沿原配置顺序前进一段，到末端后沿原路径逐段反向返回；载人保留原偏移，路径被阻挡则本次不移动；水位按原 `StageDatas` 顺序更新，不发奖励。

五组终端为 2634／3337／5334／7439／9338。已确认：重置只恢复 `NeedResetLoEntityIds` 的原始状态与位置；自动解谜消耗原道具并开放 `ExtendRoadLoEntityIds`；奖励仍须实际领取且不重复发放。规则模块只执行道路开放与重置，原扣费交易、终端状态、独立奖励领取及中断恢复尚待接入。

## 尚未实现

- 恢复原生地形与分区加载、昼夜灯光、水面／浅水、粒子、骨骼、导演和音频，并完成 Windows 资源绑定。组件字段及区域标签托管引用的中间导出已恢复；导出仍含 dummy shader，`WaterMaskRenderer` 还依赖原渲染管线，其他原生组件运行代码和显式绑定尚未全部接入，不能将中间导出当作最终资产。
- 接通泽之国入口、正常移动、海之宫剧情、探索／任务、战斗、交互、条件、奖励、小地图和地图出口，以及五组手动水路和终端原界面。
- 将水路位置、方向、水位、终端扣费意图和奖励凭据接入正式本地存档，验证取消、条件不足、重复操作、中断和重新启动。
- 在内置屏使用 DX12 实际运行 Play Mode，经模拟鼠标命中完成受影响流程，查看运行画面；构建并真实启动 Windows Player，验证后发布至 `D:/Desktop/杖剑传说/杖剑传说.exe`。目前没有包含本图完成成果的发布版本。

来源与入口：

- [来源解码工具](../../scripts/prepare_unity_map13_sources.py)、[水路投影工具](../../scripts/prepare_unity_map13_water.py)、[规则测试](../../scripts/test_map13_water.ps1)。
- [水路规则模块](../../unity/Assets/Game/Runtime/Offline/World/OfflineMap13WaterSession.cs)、[序列化定义](../../unity/Assets/Game/Runtime/Offline/World/Map13WaterDefinition.cs)；原始配置输出为 `unity/Assets/Official/World/Map/13/Source/`，水路显式资源路径为 `Resources/Map13/water-rules.json`，尚无地图运行时加载绑定。
- [当前配置来源清单](../../assets/reference/official-map/map13-source-manifest.json)、[当前水路来源清单](../../assets/reference/official-map/map13-water-source-manifest.json)记录输入／输出与代码哈希。历史 `854_156383` 的 `materials-13`、`dialogs-13`、`minimap-13`、`lighting-13` 来源清单独立保留，不作为当前 Unity 原生资产已经恢复的证据。
- [原生转换输入工具](../../scripts/prepare_unity_map13_native.py)、[序列化描述](../../scripts/map13_native_schemas.py)及[输入来源清单](../../assets/reference/official-map/map13-native-input-source-manifest.json)记录官方文件、程序集、APK 元数据、参考程序集和对象校验；组件字段保存于 `Source/native-components.json`。[官方水面代码迁移工具](../../scripts/prepare_unity_map13_water_components.py)及[代码来源清单](../../assets/reference/official-map/map13-native-water-code-source-manifest.json)记录解码程序集、ILSpy 工具、原反编译文件与 C# 输出哈希，入口为 `Runtime/Official/World/NativeWater/`。本次新增载荷未进入完整素材快照。
