# 官方来源与提取入口

[文档索引](../../docs/README.md) · [素材管理](../../docs/asset-management.md)

本页只记录官方输入与来源凭据。各模块的原始实现、转换／绑定细节、当前实现和验证边界见所属功能／地图页；逐文件记录以模块来源清单为准。

完整骨骼怪物输入取所选 `884_156692` 的18份原实体 prefab、完整骨骼二进制、AppearanceConfig／Low加载器、图集、材质及控制器闭包；按APK优先、已下载同版云缓存次序解析。原对象／包、固定MIT格式读取器、原生转换输入、控制器逐字段核对、19张原贴图全部ASTC mip至Unity RGBA32的转换与输出哈希见[完整骨骼怪物来源](battle-animation/live-monsters-source-manifest.json)。提取与绑定入口为 `scripts/prepare_battle_live_monsters.py`／`scripts/prepare_battle_live_monster_assets.py`；Unity仅提供原生纹理序列化模板，保留原图像、mip、色彩空间和采样参数。未生成完整素材快照；实现及验证边界见[战斗执行](../../docs/features/battle-execution.md)。

武器约束与物理使用完整High来源中已核验同版原始对象一致的 `primaryWeapon` 数据，保留历史原生载荷的 `871_156598` 标记；输入包含399个原皮肤、1项IK、1项变换、222项物理约束及6段原动作，未读取或复制动画SDK实现。原始约束字段、同版应用代码身份、既有独立数值依据和当前代码绑定见[武器物理来源](battle-animation/weapon-physics-source-manifest.json)，转换入口为 `scripts/prepare_battle_weapon_physics.py`；功能与验证边界在[战斗执行](../../docs/features/battle-execution.md)维护。

完整武器High载荷取所选 `884_156692`，按APK优先及同版已下载缓存解析全部398项原加载映射、图集／材质／配置依赖与实际High贴图；仅按原始对象及编码图像哈希复用既有占位图，全部原mip转换为Windows原生纹理，保持原GUID和显式本地请求绑定。当前shader由已核验同版Windows转换及fallback依赖恢复。输入／输出哈希、解码器、历史复用依据和绑定见[完整武器High来源](battle-animation/weapon-high-textures-source-manifest.json)，入口为 `scripts/prepare_battle_weapon_high_textures.py`。本批未生成完整素材快照，功能与验证边界见[战斗执行](../../docs/features/battle-execution.md)。

人物动作过渡物理使用当前 `884_156692` 的男女完整骨骼载荷，核对原层级的 `MixNext` 配置并参照公开 4.2 行为文档；原输入、全部物理轨道、独立关键帧／区间预期、原生实现及验证入口哈希见[人物过渡物理来源](battle-animation/player-mixed-physics-source-manifest.json)，生成入口为 `scripts/prepare_battle_player_mixed_physics.py`。不读取或复制动画 SDK 实现，不修改美术载荷；功能与验证边界见[战斗执行](../../docs/features/battle-execution.md)。

人物 Game prefab 与地面／武器裁剪取所选 `884_156692` 的原 `PlayerModel_Game` 三节点对象、已核验原生导出及已核验官方程序集／三份应用源码；原载荷、完整字段、默认空 Animator、脚本映射、转换输出及加载绑定的 SHA256 见[人物 prefab 与裁剪来源](battle-animation/player-model-prefab-source-manifest.json)，入口为 `scripts/prepare_battle_player_model_prefab.py`。复用当前男女原骨骼、Low／High 纹理和既有独立原生绘制端点，无新增自绘素材或完整素材快照；实现与验证边界见[技能](../../docs/features/skills.md)。

战斗技能演出相机来自所选 `884_156692` 原生 FightStage 导出中的231个战场 prefab；逐文件SHA256与原生导出凭据核对，保留原层级、变换、激活标记、完整镜头／过渡／优先级及 CameraOffset 字段，并核实原管线没有 body／aim／noise、Follow／LookAt 或 framing／recomposer 代理。原 prefab、`GameCameraController`／`FightCamera`／演出节点／输入封锁源码及生成 JSON／meta 哈希见[演出相机来源](battle-scenes/performance-camera-source-manifest.json)，提取入口为 `scripts/prepare_battle_performance_camera.py`。原普通相机仍保留既有历史来源，未重标为当前版本；本批没有新增美术或完整素材快照，行为与验证范围见[战斗执行](../../docs/features/battle-execution.md)。

公共技能实体视图配置来自所选 `884_156692` 原生导出缓存中 1,884 个 `MyFightSkill` 的原序列化字段；逐 prefab 哈希与导出凭据核对，原 `PreferredPosToPlayable`／`ShowSkillReplay`、代码输入及输出 JSON／meta 哈希见[技能实体视图凭据](battle-animation/skill-entity-views-source-manifest.json)，入口为 `scripts/prepare_battle_skill_entity_views.py`。本批没有新增美术或完整素材快照；使用与验证范围见[战斗执行](../../docs/features/battle-execution.md)。

头顶状态项的所选 `884_156692` 原生 `BattleStatusItem` prefab、官方显示／计数源码及转换输出哈希见[状态项来源](battle-hud/status-item-source-manifest.json)，入口为 `scripts/prepare_battle_status_item.py`。复用哈希匹配的原生导出缓存、原 SpriteRenderer／TMP／描边参数和相同官方 FZY4K TTF 的既有 Windows 字体绑定；历史854状态栏网格另按原凭据保留；回合调用计数的当前共享／客户端源码与历史854规则／显示标记分别核对并登记。本批未生成完整素材快照，行为与验证范围见[战斗执行](../../docs/features/battle-execution.md)。

状态表现基础运行代码取所选 `884_156692` 的已核验 Assembly-CSharp.dll 与客户端反编译源文件；原程序集、三个原文件、11个方法体指纹、脚本fileID、命名空间／显式注入适配、输出C#／meta及既有依赖凭据哈希见[状态表现代码来源](battle-animation/status-playable-runtime-source-manifest.json)，入口为 `scripts/prepare_battle_status_playable_runtime.py`。独立图形夹具复用既有原生水波粒子来源，无新增美术或完整素材快照；迁移与验证范围见[战斗执行](../../docs/features/battle-execution.md)。

状态实体表现代码取所选 `884_156692` 已核验的 Assembly-CSharp.dll、Framework.FirstPass.Runtime.dll 与七份应用／框架源码；共享层数和可叠层元数据另登记五份原始边界证据。原文件、22个核心方法的显式实体访问替换核对、原脚本fileID、诊断／绑定适配、输出C#／meta及依赖哈希见[状态实体表现代码来源](battle-animation/status-view-runtime-source-manifest.json)，入口为 `scripts/prepare_battle_status_view_runtime.py`。本批复用既有原生水波粒子夹具，无新增美术或完整素材快照；行为与验证范围见[战斗执行](../../docs/features/battle-execution.md)。

原生自定义目标绑定取所选 `884_156692` 的 `GameFramework.Runtime.dll` 与 `ApplyByCustom.cs`，三个方法体不变，保留原CustomApplyObj和无目标类型约束时的标记字段；复用依赖八个方法的当前源码核对、七个原生施法图使用点的已核验导出哈希、脚本fileID和输出哈希见[自定义目标绑定来源](battle-animation/status-custom-runtime-source-manifest.json)。入口为 `scripts/prepare_battle_status_custom_runtime.py`，未据此转换或删减七个完整施法图。

原生全局冰冻图取所选 `884_156692` 的已核验客户端程序集、`PlayableMaterialBoundingBoxProvider` 与两个原生 `playable_Frozen_1/2` 依赖 prefab；代码适配和脚本fileID见[冰冻代码来源](battle-animation/status-frozen-runtime-source-manifest.json)。原生导出 meta 的 bundle 名定位到 APK 中两个共享依赖包，逐包核对当前索引、原始 GameObject 名称／对象哈希和 CAB 来源；完整40项资产、15张全mip纹理、17份材质、5份网格、2个完整prefab及1份完整Windows shader的输入／输出哈希、依赖与显式资源目录见[冰冻图来源](battle-animation/status-frozen-assets-source-manifest.json)。转换入口为 `scripts/prepare_battle_status_frozen_runtime.py` 与 `scripts/prepare_battle_status_frozen_assets.py`，复用既有完整图转换器；独立共享依赖对象不伪装为地址索引条目。

原生状态动画暂停输入取所选 `884_156692` 已核验程序集、`MyPlayableAnimationControllerPause` 与 `AnimationController`；五个播放节点方法、三个控制器方法的当前源码核对、脚本fileID、诊断适配及输出哈希见[暂停代码来源](battle-animation/status-pause-runtime-source-manifest.json)。原24390完整播放图及其原APK对象、Windows原生转换缓存、所有mip／Shader和显式加载绑定见[暂停资源来源](battle-animation/status-pause-assets-source-manifest.json)；重建入口为 `scripts/prepare_battle_status_pause_runtime.py`／`scripts/prepare_battle_status_pause_assets.py`。本批复用完整图转换入口，未生成完整素材快照；实现和验证范围见[战斗执行](../../docs/features/battle-execution.md)。

原生状态材质链取所选 `884_156692` 的 Assembly-CSharp.dll、`MeshMaterialProperty`／`MyPlayableMaterialProperty` 及两个属性提供者，复用已迁入的完整／轻量人物外观与部件收集边界；四类源文件、41个方法体指纹、显式C#语法／诊断适配、脚本fileID与输出哈希见[材质代码来源](battle-animation/status-material-runtime-source-manifest.json)。原24059完整prefab、13张全mip纹理、6个材质及全量Windows Shader的原APK对象和转换输入输出见[材质资源来源](battle-animation/status-material-assets-source-manifest.json)。重建入口为 `scripts/prepare_battle_status_material_runtime.py`／`scripts/prepare_battle_status_material_assets.py`，后者复用已核验的完整播放图转换入口；未生成完整素材快照，行为及验证边界见[战斗执行](../../docs/features/battle-execution.md)。

原生状态环绕输入取所选 `884_156692` Assembly-CSharp.dll、`MyPlayableOrbitMovement`、三组原prefab及其已核验原生导出／Windows转换缓存；原APK对象、每个纹理mip、全量Shader转换、脚本fileID、十九个方法适配指纹、34个资源及显式绑定哈希见[环绕代码来源](battle-animation/status-orbit-runtime-source-manifest.json)和[环绕资源来源](battle-animation/status-orbit-assets-source-manifest.json)。重建入口为 `scripts/prepare_battle_status_orbit_runtime.py`／`scripts/prepare_battle_status_orbit_assets.py`，对象池适配及验证范围见[战斗执行](../../docs/features/battle-execution.md)。本批未生成完整素材快照。

原装备被动词条选择输入：`record_battle_equipment_selection_sources.py` 校验 `884_156692` 的战斗词条过滤／互斥比较、套装阈值、被动属性、技能品阶与 `854_156383` 装备／战斗数据；源代码、配置及运行适配 SHA256 见[装备被动选择来源](battle-rules/equipment-selection-source-manifest.json)，行为与验证见[装备功能](../../docs/features/equipment.md)。

原场景部件应用代码输入：`prepare_battle_renderer_part_sources.py` 核验 `884_156692` 解码程序集与8份官方应用源码（含 `CharacterPartEntryProvider`／`CharacterPartPoolEntry`），记录加载／池化／分层／动作通知代码及显式适配端口；输入、依赖与迁移输出 SHA256 见 [场景部件凭据](battle-animation/renderer-parts-source-manifest.json)，当前实现及验证范围见[战斗执行](../../docs/features/battle-execution.md)。

原核心人物战斗动作输入：`prepare_battle_core_pose_fixtures.py` 从哈希匹配的 `871_156598` 无损人物载荷提取全部职业战斗动作边界，复用已有原角色资源、外观配置／表、加载与染色链的来源凭据；输入、依赖、实现与夹具 SHA256 见 [核心人物动作凭据](battle-animation/core-pose-source-manifest.json)，使用和验证范围见[战斗执行](../../docs/features/battle-execution.md)。

原生人物物理参数输入：`prepare_battle_physics_channel_fixtures.py` 核对原 `871_156598` 无损人物载荷及固定 MIT 格式读取器的字段投影，保留全部物理约束、皮肤成员和 mix 通道；输入／依赖／运行代码与夹具 SHA256 见 [物理参数凭据](battle-animation/physics-channels-source-manifest.json)，使用和验证范围见[战斗执行](../../docs/features/battle-execution.md)。

原局部服装图集输入：`prepare_battle_local_clothing_textures.py` 复用 `871_156598` 高分辨率加载器及历史整包／对象凭据，核对当前 APK／已下载云缓存中的相同原主图、染色图字节；另取回并严格核对旧占位 bundle，不用已变化的新包替换。四项本地绑定和6张原纹理的输入输出 SHA256 见 [局部服装图集凭据](battle-animation/local-clothing-textures-source-manifest.json)；使用和验证范围见[战斗执行](../../docs/features/battle-execution.md)。

原生约束姿势输入：`prepare_battle_constraint_pose.py` 核对同一份 `871_156598` 无损人物载荷及原 IK／transform 顺序，记录既有独立几何与多帧通道的绑定代码哈希；输入与前置凭据见 [约束姿势来源](battle-animation/constraint-pose-source-manifest.json)，使用和验证范围见[战斗执行](../../docs/features/battle-execution.md)。

原生人物附件变形输入：`prepare_battle_deform_fixtures.py` 复用逐文件哈希匹配的 `871_156598` 无损人物载荷，提取男女8条单关键帧附件变形及原裁剪附件；输入、既有凭据、独立运行代码与夹具 SHA256 见 [附件变形凭据](battle-animation/deform-source-manifest.json)，使用和验证范围见[战斗执行](../../docs/features/battle-execution.md)。

原生战斗约束通道输入：`prepare_battle_constraint_channel_fixtures.py` 复用逐文件哈希匹配的 `871_156598` 无损人物载荷，记录男女全部896条动画及其原 IK／transform 字段；原载荷／既有凭据、独立实现与夹具 SHA256 见 [约束通道凭据](battle-animation/constraint-channels-source-manifest.json)，使用范围见[战斗执行](../../docs/features/battle-execution.md)。

原版战斗 Game 控制器：`prepare_battle_game_animators.py` 使用所选 `884_156692` 的原生转换凭据、APK／云缓存中的原控制器和依赖对象，保留四份职业／性别控制器及33个片段。原对象／包、转换输入、消费者源码及输出 SHA256 见 [Game 控制器凭据](battle-animation/game-animators-source-manifest.json)；正式人物绑定和验证范围见[战斗执行](../../docs/features/battle-execution.md)。

人物死亡序列输入：`prepare_battle_character_death_assets.py` 核对所选 `884_156692` 的 3100／3102／3991 原实体驱动器托管引用，沿 APK 内两个共享 bundle 转换完整正常／入场死亡图，保留原空引用。原 bundle、对象、组件字段、反编译来源、迁移方法、资源与显式绑定 SHA256 见[人物死亡图来源](battle-animation/character-death-assets-source-manifest.json)；使用和验证范围见[技能](../../docs/features/skills.md)。本批未生成完整素材快照。

战斗 Animator 补充代码：`prepare_battle_animator_runtime.py` 从当前 `884_156692` 已校验程序集／反编译结果迁入 `MyPlayableOverrideAnimator`；当前 `AnimationController`、`StateMergeList`、`MyPlayableAnimator` 原源码与既有历史迁移凭据逐字节相同。原始输入、程序集、兼容核对及输出 SHA256 见 [Animator 代码凭据](battle-rules/animator-runtime-source-manifest.json)；已有技能 cue 与 clip 的来源身份不变。

游记布局补充输入：`prepare_travel_entry_layout.py` 读取当前 APK 中 `defaultpackage_assets_prefabs_ui_home.bundle`，严格核对其与历史 `871_156598` 清单的字节身份相同（353,152 字节，SHA256 `78b8a1151fcd575ce7dd6d98511b788853ba71730dbd620bf8e5917eabebd9c2`），再解码原组件字段。原 prefab PathID、逐组件原字节哈希、解码程序集、原脚本和输出哈希集中记录于 [游记布局凭据](dungeon-ui/prefab-binding-source-manifest.json) 的 `travel_entry_layout`；历史输入凭据保持原值，当前 APK 仅记录为相同包的新位置。

## 输入身份与版本

世界人物附件补充输入：复用已保存且逐文件哈希匹配的 `ProfessionUI/NativeSkeletons/{male,female}.bytes`（871_156598），验证内部骨骼、图集与创角几何相同，补出全部原 region／mesh 皮肤附件；未重新使用当前不同版本 APK。原包和压缩块哈希、基础几何来源及派生输出见[世界人物附件凭据](character-creation/world-append-geometry-source-manifest.json)。

战斗相机输入：`prepare_battle_camera_bindings.py` 沿既有场景布局清单的固定 bundle 身份提取229个场景、256个 GameId 的原 `GameCameraIniter`、虚拟相机、群组及 FramingTransposer。当前 APK 中 FightSettings 对象的原始 SHA256 为 `0a5fb09f4a312602cd0c6e4d798c15f9017923b8393b164f2bfb39361b7c45ec`，与既有历史对象相同；各 bundle、对象、程序集及输出哈希见 [相机来源清单](battle-scenes/camera-source-manifest.json)。

| 输入 | 固定身份／位置 |
| --- | --- |
| 当前官方 APK | `.tools/source-packages/zjcs_M509085.apk`；包内五包 `881_156673`；1,036,023,846 字节 |
| APK SHA256 | `f8f54a38ec74b6ea09f39109282e23671fd91bf704e7802063cfcdba21c5c431` |
| APK 校验凭据 | `.tools/source-packages/source.json`；MD5 `dede2bf63d1542b7fd02da1f2be59363` 与官方 HTTP ETag 一致，完整 ZIP CRC 通过 |
| 官方下载 | [zjcs_M509085.apk](https://zhangjcsdlf.leiting.com/full/zjcs_M509085.apk)，入口来自[官网](https://zjcs.leiting.com/)配置接口 `https://zjcs.leiting.com/client/gameV2/home` |
| 资源版本选择 | 根目录 `official-source.json`；当前五包 `884_156692`，官方更新配置为 APK build `156673` 选择资源目录 `202610291150884` |
| 当前云端输入 | `.tools/source-packages/cloud/runtime/202610291150884/`；五包清单共 21,030 个文件、3,271,984,657 字节，全量从该版本官方 CDN 下载 |
| 云端完整性凭据 | 同目录 `inventory.json`、`receipts.jsonl`、`integrity.json`、`verified-cab-files.json`；21,030 个文件已通过清单大小／MD5、收据 SHA256／版本／精确 URL 复验，缺失／错误为 0；UnityFS 目录解码共 29,771 个唯一 CAB，WwiseBnk 原视频容器边界检查通过；不代表 Windows 转换成果或工程完整 |
| 官方更新入口 | [110001_Main.dis](https://zhangjcsupdate.leiting.com/zhangjcs/version/release/110001_Main.dis) |
| 官方程序集／反编译 | 当前版本独立目录 `.tools/source-packages/current/884_156692/official-code/`；通用合并目录 `.tools/source-packages/official-code/managed/decoded/`、`client-source/`、`common-source/` 等；原程序集与 metadata 哈希见版本目录 `code-manifest.json`，通用合并凭据为 `.tools/source-packages/prepared-code-source-manifest.json` |
| 完整托管客户端 C# 归档 | [来源清单](official-client-code/source-manifest.json)；版本目录 `client-decompilation-manifest.json` 记录各项目全部输出文件 SHA256，覆盖所选 HybridCLR 清单的全部 10 个程序集；静态反编译，不执行游戏程序集，AOT 类型描述桩不计入 C# 方法实现 |
| 当前配置与批量转换输入 | 当前版本目录 `official-data/`、`native-conversion/source-manifest.json`；后者保留五包 APK／云端资产路径目录、bundle 对应关系与逐文件原始／解码 SHA256 |
| 当前反编译／配置／转换缓存凭据 | [准备来源清单](official-map/current-resource-preparation-source-manifest.json)：程序集、448 份原表、原生资源、WAV／媒体、组件字段、转换输入与 CAB 闭包清单的 SHA256；旧模块原始来源身份保留 |
| 当前全量原生资源、Windows shader 与精确 mip | [结果清单](official-map/current-resource-windows-source-manifest.json)：全量原生输出、组件字段、全部纹理原生像素及原 mip、全部 shader 程序与 Windows 引擎对应载荷的清单 SHA256、源 CAB／PathID 和转换器身份；原输入、较早输出与未对应旧工程文件实际保留 |
| APK 原生 IL2CPP 输入 | 版本代码目录 `native/arm64-v8a/libil2cpp.so`（347,966,368 字节）及 `native/armeabi-v7a/libil2cpp.so`（287,148,316 字节）；ZIP 条目、大小与 SHA256 记录在版本 `code-manifest.json` 的 `native_libraries`，提取不代表 AOT 反编译完成 |
| APK AOT 类型资料 | 当前版本 `official-code/aot-metadata/source-manifest.json`：ARM64 原库、global-metadata、恢复镜像、ELF 分析视图、57,638 个类型及 148 份描述 DLL 的输入输出 SHA256 |
| 分包 | Boot、Launch、HybridCLR、DefaultPackage、WwiseBnk |

只维护当前官方 APK 与其配套云端输入。APK 包内版本与配套热更新版本分别记录；转换产物的来源与哈希仍按实际输入记录，不把已有转换结果改标为当前版本。本地输入不等于仓库外备份。

## 凭据位置

| 内容 | 入口 |
| --- | --- |
| 原始包与逐文件下载来源 | 当前缓存中的 `inventory.json`、`receipts.jsonl` 及 APK 同目录 `source.json` |
| 当前来源切换与转换链 | `assets/reference/official-map/current-*-source-manifest.json`、`assets/reference/character-creation/current-*-source-manifest.json` |
| 客户端／共享代码来源 | [官方程序集与反编译目录](../../.tools/source-packages/official-code/) 与各模块源码来源清单 |
| 模块资源来源 | 本目录各模块的 `source-manifest.json`、`*-source-manifest.json` 及所属功能／地图页中的入口 |
| 当前伙伴界面公共组件、名册控制器与原生字段输入 | [公共组件源码](partner-ui-helpers-source-manifest.json)、[名册控制器源码](partner-roster-ui-source-manifest.json)、[原生 prefab 字段](partner-native-recovery-source-manifest.json)：`884_156692` 选定反编译输入、NPC bundle／对象、原字段缓存及转换输入输出哈希；运行接线与验证范围见[伙伴功能页](../../docs/features/partners.md) |
| 当前伙伴卡片依赖与提示图输入 | [卡片资源凭据](partner-card-binding-source-manifest.json)、[提示节点源码凭据](partner-tipdot-source-manifest.json)：`884_156692` 原 CAB／PathID、SpriteAtlas／原索引字节、动态图片地址、CN 字体配置／TTF、Windows 纹理／shader 转换、Wwise 路由与 PCM、选定反编译源码及元数据输入输出哈希；处理入口为 `prepare_partner_card_native.py`／`prepare_partner_tipdot_nodes.py`，接线与验证范围见[伙伴功能页](../../docs/features/partners.md) |
| 当前伙伴赠礼与好感属性组件输入 | [赠礼组件源码凭据](partner-gift-ui-source-manifest.json)：`884_156692` 选定客户端／共享／框架源码、生成工具与输出 SHA256；处理入口为 `prepare_partner_gift_ui.py`，接线与验证范围见[伙伴功能页](../../docs/features/partners.md) |
| 当前原生好感属性模板输入 | [原生好感属性凭据](partner-gift-native-source-manifest.json)：`884_156692` NPC bundle、窗口实际模板指针、完整字段缓存、Windows 原生资源及所选 APK AOT 八方法哈希；提取工具为 `prepare_partner_gift_native.py`，运行范围见[伙伴功能页](../../docs/features/partners.md) |
| 当前原生好感详情窗口输入 | [原生详情窗口凭据](partner-friendship-dialog-source-manifest.json)：`884_156692` 完整 `NpcFriendshipDialog`、原弹窗／布局源码、开合动画、语言分包与依赖对象／输出哈希；提取工具为 `prepare_partner_friendship_dialog_native.py`，接线范围见[伙伴功能页](../../docs/features/partners.md) |
| 当前原生伙伴礼物图标输入 | [礼物图标凭据](partner-gift-preferences-source-manifest.json)：`884_156692` 原物品／附件管理配置、默认与赠礼 prefab、HintTip／ReduceTip、偏好 Sprite 与品质底图、原事件音频及输入输出哈希；提取工具为 `prepare_partner_gift_preferences_native.py`，范围见[伙伴功能页](../../docs/features/partners.md) |
| 当前伙伴配置输入 | [配置来源](partner-rules-source-manifest.json)：`884_156692` 主 Luban／NpcTalentTree 包、16 张原表、实际使用的同版本反编译构造器／元数据，以及完整配置载荷与 Unity 元数据哈希；提取入口为 `prepare_current_partner_rules.py`，使用范围见[伙伴功能页](../../docs/features/partners.md) |
| 当前伙伴面板控制器输入 | [代码来源](partner-panel-controller-source-manifest.json)：`884_156692` 的 `NpcPanel`、页签枚举、原映射与功能枚举，以及三项迁移源码／Unity 元数据哈希；处理入口为 `prepare_partner_panel_controller.py`，交互与验证范围见[伙伴功能页](../../docs/features/partners.md) |
| 当前伙伴战斗模型输入 | [来源凭据](partner-battle-fighters-source-manifest.json)：`884_156692` 的 NPC 战斗实体、原 Spine binary／Atlas／贴图对象、Animator、组合皮肤配置与实体根至骨骼节点的原 Transform 链，转换器及输出 SHA256；处理入口为 `prepare_opening_fighters.py` 的 `ZJCS_PARTNER_FIGHTERS=1` 模式及 Unity `OfficialPartnerBattleFighterTools.Import`，支持范围与验证边界见[NPC助战页](../../docs/features/npc-assistance.md) |
| 当前伙伴完整骨骼战斗数据输入 | [来源凭据](partner-native-battle-source-manifest.json)：`884_156692` 的完整 Spine binary、独立背饰、Atlas、原材质／MeshRenderer、AppearanceConfig／TextureLoader 实际纹理映射、Animator 状态和原 Transform／骨骼跟随字段；记录所选 APK／匹配云端对象、固定 MIT 解码器身份、当前原 shader 对象对应的 Windows 转换缓存、独立几何／运行检查输入与安装载荷／元数据 SHA256。处理入口为 `prepare_partner_native_battle.py`；运行范围见[NPC助战页](../../docs/features/npc-assistance.md) |
| 当前伙伴完整骨骼战斗 Animator 输入 | [来源凭据](partner-native-battle-animators-source-manifest.json)：`884_156692` 的 34 个原 AnimatorController、532 个片段及完整依赖；核对固定原生转换回执、原编译状态机与片段对象、输入输出／GUID 重绑定哈希，保留原一维混合树和片段时长差异。处理入口为 `prepare_partner_native_battle_animators.py`；运行范围见[NPC助战页](../../docs/features/npc-assistance.md) |
| 当前伙伴完整骨骼战斗本地绑定 | [来源凭据](partner-native-battle-assets-source-manifest.json)：校验完整骨骼／Animator 输入哈希，显式绑定35个骨骼包、35份材质字段、28份原运行时贴图、34个控制器及当前原 shader 对应的 Windows 转换载荷；记录本地目录、转换与元数据哈希。处理入口为 `prepare_partner_native_battle_assets.py`；运行范围见[NPC助战页](../../docs/features/npc-assistance.md) |
| 当前伙伴技能 Animator 节点输入 | [来源凭据](partner-native-playables-source-manifest.json)：选定884 APK的3张生成配置表、34名伙伴的等级／阶位与技能选择、75个技能的93个默认／方向分支及95个 Animator 节点；记录原组件字段／节点顺序、其他节点来源、组件模式、生成构造器、应用源码及安装数据／元数据哈希。处理入口为 `prepare_partner_native_battle_playables.py`；运行范围见[NPC助战页](../../docs/features/npc-assistance.md) |
| 当前伙伴战斗挂点输入 | [来源凭据](partner-native-attachments-source-manifest.json)：选定884 APK的34份实体、1,085个 Transform／RectTransform、原 AttachAgent 注册表及应用层 AttachmentFollower 配置；保留组件引用、原对象哈希、组件模式、应用源码及本地加载／元数据哈希。处理入口为 `prepare_partner_native_battle_attachments.py`；运行范围见[NPC助战页](../../docs/features/npc-assistance.md) |
| 森之国女神珍宝详情原 prefab 与 Unity 转换 | [转换清单](official-map/map11-treasure-detail-source-manifest.json)：APK `871_156598` bundle／PathID／输入哈希、转换工具哈希、Unity prefab 输出哈希 |
| Windows Player 官方应用图标 | [来源清单](application-icon-source-manifest.json)：APK 内 `res/mipmap-xxxhdpi/app_icon.png` 原始 PNG 与 SHA256，绑定至 Windows Standalone PlayerSettings |
| 通天塔 871 原生地形闭包 | [来源清单](official-map/map15-native-terrain-source-manifest.json)：`871_156598` 的14个官方地形根、CAB输入收据、原始shader GLES程序、MonoScript／Windows shader绑定及 Unity YAML 输出哈希 |
| 龙之国原根雾层与原生输入 | `official-map/map14-native-root-input-source-manifest.json`、`official-map/map14-root-fog-source-manifest.json` | 原根19依赖／1,614对象／23组托管引用，10对象雾层子树、原Sprite／三纹理／三材质／三GLES程序及完整CloudController2；原Console坐标／可见性消费，Windows渲染分项验证，正式地图未完成 |
| 龙之国原地图根组件 | [来源清单](official-map/map14-root-components-source-manifest.json)：八个原薄客户端根组件、泛型地表基类及真实共享组件／信息绑定；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原实体缓冲事件 | [来源清单](official-map/map14-buffered-events-source-manifest.json)：原缓冲模块、两类分发器、原生阶段／交互者／容器事件与动态启动依赖；验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国稳定客户端视图接线 | [接线来源清单](official-map/map14-client-view-bindings-source-manifest.json)：同一原生实体上的普通组件、可见性、动作、奖励、显示状态与原绑定前／移除消费；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原可见性处理器 | [处理器来源清单](official-map/map14-visibility-handlers-source-manifest.json)：原 FIFO 工厂、等待动作／奖励／隐藏处理器、原 MyAction 应用层实现及原生组件／事件绑定；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原地图显示数据 | [地图显示数据来源清单](official-map/map14-native-display-data-source-manifest.json)：三张原表、原解析器／标签函数、原逻辑 ID 映射、区域／地表组件及本地显示状态绑定；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原共享可见性绑定 | [共享可见性来源清单](official-map/map14-native-visibility-source-manifest.json)：原组件／原接收器接口、稳定消费投影、字段与事件顺序、视图订阅释放及输入输出哈希；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国四类雕像原实体根 | [雕像实体根来源清单](official-map/map14-native-statue-entities-source-manifest.json)：原实体 prefab／17 脚本组件、官方代码来源哈希、状态类型与费用雷达绑定及输入输出哈希；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原雕像费用雷达 prefab | [费用雷达 prefab 来源清单](official-map/map14-native-radar-tips-source-manifest.json)：完整原层级／组件字段与字体、精灵、材质、动画绑定及输入输出哈希；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原快速战斗雷达提示 | [快速战斗提示来源清单](official-map/map14-radar-fast-kill-tip-source-manifest.json)：完整原提示、原营地查询与快速战斗规则 IL 调用及输入输出哈希；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原雷达费用提示 | [费用提示来源清单](official-map/map14-radar-cost-tip-source-manifest.json)：原提示完整实现、原客户端费用查询及 Console 行为／来源过滤调用与输入输出哈希；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原雷达组合提示 | [组合提示来源清单](official-map/map14-radar-combined-tip-source-manifest.json)：完整原组件、原异步状态机恢复及 UniTask 外部依赖哈希；实际验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原实体雷达核心 | [雷达核心来源清单](official-map/map14-entity-radar-source-manifest.json)：原扫描／提示生命周期、网格与移除事件、APK 实例命名及输入输出哈希；验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原实体奖励截留 | [奖励截留来源清单](official-map/map14-entity-award-hook-source-manifest.json)：完整原组件、原奖励管理器 IL、原消息身份与历史处理／延迟展示顺序及来源哈希；验证范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原世界实体触摸输入 | [输入来源清单](official-map/map14-world-input-source-manifest.json)：原输入分支与调用顺序、原共享实体／玩家交互／网格查询、重定向身份及来源哈希；实际验证边界见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原实体根客户端组件 | [组件来源清单](official-map/map14-client-components-source-manifest.json)：八个原客户端组件、原共享组件／配置身份与查询、奖励原对象／列表引用及来源哈希；四类雕像模板绑定范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原客户端实体索引 | [索引来源清单](official-map/map14-entity-index-source-manifest.json)：固定 871 原索引处理器、3860 目录／9604 编号的原文本、原 JSON 解码入口及 LiteDB 5.0.20 平台依赖哈希／许可；运行范围见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原共享容器事件与实体状态边界 | [共享状态来源清单](official-map/map14-client-entity-state-source-manifest.json)：固定 871 原容器／实体／组件调用、四路原事件分发、稳定投影身份与输入输出哈希；运行检查及后续接入见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原客户端容器与剧情 NPC 处理器 | [容器来源清单](official-map/map14-entity-containers-source-manifest.json)：固定 871 官方客户端容器、两类客户端实体处理器、四组原事件、复用生命周期及输入输出哈希；原共享状态消费边界与运行检查见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原实体加载器与 TTL 对象池 | [实体加载来源清单](official-map/map14-entity-loading-source-manifest.json)：固定 871 原客户端／Framework 源码与程序集、复用的生命周期／基础池、显式平台消费边界及输入输出哈希；运行与验证边界见[龙之国](../../docs/maps/map-14.md) |
| 龙之国统一原生宿主 | [宿主来源清单](official-map/map14-native-host-source-manifest.json)：固定 871 原地形／场景／环境／标签目录及原共享配置、序列化、容器根实体的显式加载绑定，记录官方排序层依赖与输入输出哈希；运行与验证边界见[龙之国](../../docs/maps/map-14.md) |
| 龙之国原容器与根实体 | [根实体来源清单](official-map/map14-shared-root-source-manifest.json)：固定 871 原 Container／EntityHandler／事件调用代码、原 EC/container 字节与 bundle 对象、未改写的 Console／Luban 及输入输出 SHA256；接线与验证边界见[龙之国](../../docs/maps/map-14.md) |
| 龙之国共享序列化与原地图数据 | [共享序列化来源清单](official-map/map14-shared-serialization-source-manifest.json)：固定 871 AOT 方法、未改写的 Console 格式器与 35 份原载荷、固定 MIT MessagePack 发布包／源码、转换依赖及输入输出 SHA256；适配与验证边界见[龙之国](../../docs/maps/map-14.md) |
| 龙之国共享配置与根实体查询 | [共享配置来源清单](official-map/map14-shared-config-source-manifest.json)：固定 871 Config AOT 方法／元数据、原 Console／Launch、Luban 表 bundle／对象及输入输出 SHA256；实现、单机绑定和验证边界见[龙之国](../../docs/maps/map-14.md) |
| 当前职业外观表与解析 | [七张原表](profession-ui/appearance-tables-source-manifest.json)、[原应用解析与显式加载](profession-ui/appearance-resolver-source-manifest.json)；`871_156598`，包含输入对象、完整字节消费、输出与处理工具 SHA256；运行与接线边界见[职业功能页](../../docs/features/professions.md) |
| 职业音频对象 | [原 APK 方法、三个实例与 Windows 绑定](profession-ui/sound-objects-source-manifest.json)；保留注册／位置／销毁次序，限定原无 Collider／默认监听器配置；PCM 输入与输出见[音效来源](profession-ui/windows-audio-source-manifest.json) |
| 职业本地外观绑定 | [原应用方法、当前配置与本地存档投影](profession-ui/local-appearance-source-manifest.json)；原职业预览使用独立深副本，当前物品类型与角色会话显式注入；未完成边界见[职业功能页](../../docs/features/professions.md) |
| 职业原生骨骼数据 | [固定原数据、无损容器与显式加载](profession-ui/skeleton-data-source-manifest.json)；男女骨骼、图集区域、全部动画／事件／约束／附件及逐字节输入输出 SHA256；未使用 Spine runtime 或姿态烘焙，运行边界见[职业功能页](../../docs/features/professions.md) |
| 职业骨骼继承与原曲线 | [独立复数参考、真实皮肤和运行时绑定](profession-ui/world-inheritance-source-manifest.json)；男女待机原曲线与约束前变换，保留原数据；物理、混合与角色显示边界见[职业功能页](../../docs/features/professions.md) |
| 职业待机变换约束 | [原配置／动画默认值与独立几何](profession-ui/idle-transform-source-manifest.json)；男女待机 IK 后的绝对世界变换和主武器挂点，输入输出哈希及未完成边界见[职业功能页](../../docs/features/professions.md) |
| 职业角色核心图集 | [原占位映射、AssetTable GUID 与 GPU mip](profession-ui/character-textures-source-manifest.json)；原版完整默认外观链所需页面及保留的诊断页，共 15 组页绑定、35 张原贴图；范围见[职业功能页](../../docs/features/professions.md) |
| 职业实际动画接线 | [实际打包插槽／顺序／IK 与独立参考](profession-ui/runtime-timelines-source-manifest.json)；原数据直接供给运行时，保留边界、空附件、有符号偏移和 IK 默认值；边界见[职业功能页](../../docs/features/professions.md) |
| 职业原生外观对象 | [原应用皮肤合成、实际骨骼／附件及 GPU 诊断](profession-ui/native-appearance-graph-source-manifest.json)；原版默认脸部／头发／肤色与染色接线、96 组独立世界姿态参考及四张原贴图角色诊断；边界见[职业功能页](../../docs/features/professions.md) |
| 职业原角色资源 | [原路径／GUID、模型元数据及分离动画 SO](profession-ui/character-resources-source-manifest.json)；四个本地原资源入口、原字符串池／动画查找与真实角色资源加载；无对应数据的原初始动作及边界见[职业功能页](../../docs/features/professions.md) |
| 职业实际原生挂点 | [原跟随组件与实际骨骼／插槽视图](profession-ui/native-followers-source-manifest.json)；原数据缩放、附件／顺序身份、UI 与场景挂点及零深度分支；验证与缺项见[职业功能页](../../docs/features/professions.md) |
| 职业原默认武器皮肤 | [原初始武器／职业默认皮肤与配置](profession-ui/skin-defaults-source-manifest.json)；完整解码当前配置、原皮肤验证／移除／事件顺序及本地只读快照缺字段恢复；验证与缺项见[职业功能页](../../docs/features/professions.md) |
| 职业原剑杖资源 | [原默认剑／杖资源与可执行 shader](profession-ui/weapon-resources-source-manifest.json)；四个原 UI 层级、完整共享模型与图集、实际高分辨率纹理、原高度裁剪程序及精确曲线采样；验证与缺项见[职业功能页](../../docs/features/professions.md) |
| 职业原轻量外观与材质管理 | 当前 871_156598 官方应用程序集及反编译类 | 原 AppearanceUILite 生命周期、完整 AppearanceAssetManager 与配置／共享材质记录；本地资源／Graphic 边界，实际剑杖贴图生命周期 Play Mode 和 GPU 验证；正式 prefab／整页尚未接通 | [来源与哈希](profession-ui/appearance-ui-lite-source-manifest.json) |
| 职业原武器加载器与图集 | 当前 871_156598 原 APK／已缓存官方资源 | 完整 398 项占位／主图／染色映射与原图集材质、原管理配置和 shader 回退依赖；400 个纹理 GPU 哈希、真实本地请求与原外观生命周期验证；完整武器 prefab／整页尚未接通 | [来源与哈希](profession-ui/weapon-texture-loader-source-manifest.json) |
| 职业原详情窗口控制器 | 当前 `871_156598` 官方应用反编译代码 `ProfessionPanel`、`ProfessionSkillBadgeRewarder`、`ProfessionChangeJumpBtn` | `assets/reference/profession-ui/detail-controllers-source-manifest.json`；保留原初始化／页签／状态、技能阶位差额与排序、导师后继／回溯及失败分支，仅通过显式接口接入本地服务；完整 prefab 与子面板仍待绑定 |
| 职业原信息与技能图标 | 当前 `871_156598` 官方应用反编译代码、五张原 Luban 表、六个原 prefab 闭包与原 Sprite／完整纹理；APK 优先，所选云端输入逐项核验 | `assets/reference/profession-ui/info-panels-source-manifest.json`、`skill-resources-source-manifest.json`、`skill-icons-source-manifest.json`；原方法／阶位排序／托管引用、400 个 Sprite 网格与 77 张纹理 GPU mip 核对；完整信息页与正式玩家／弹窗服务仍待绑定 |
| 职业原升级与详情资源 | 当前 `871_156598` 官方应用／框架反编译代码、原详情／升级 prefab、原 CN 字体配置及完整中文二进制；APK 优先，所选缓存云端资源逐项核验 | `assets/reference/profession-ui/upgrade-panel-source-manifest.json`、`detail-resources-source-manifest.json`；七类全部方法、89 资产闭包、原托管引用与三类 shader 显式绑定；原升级子面板模拟输入／效果／红点／回收检查通过，完整详情与本地状态仍待绑定 |
| 职业原类型化属性行 | 当前 `871_156598` 官方属性行／复合值／祝福按钮及数值格式源码、完整原属性与词条表；APK 优先、所选云端缓存哈希核验 | `assets/reference/profession-ui/property-rows-source-manifest.json`、`property-resources-source-manifest.json`；352 条属性、891 条词条，40 个原 Sprite 与两张完整纹理；原中文格式、全部 GPU mip、资源生命周期和模拟原按钮派发通过，完整弹窗与页面调用仍待接通 |
| 历史输入与非运行参考 | [LEGACY_SOURCES.md](LEGACY_SOURCES.md)；历史版本证据保留原身份 |
| 转换素材备份 | `asset-pack/layout.json`、`asset-pack/lock.json`；实际覆盖以已生成并校验的归档为准 |

技能附属窗口由固定 APK `871_156598` 的原 prefab、组件字段、字体与动画提取，运行时显式绑定 270 张官方 UI 精灵；窗口、原代码适配、字体／shader、工具及输入输出 SHA256 见 `skill-ui/unity-icon-source-manifest.json`。列表附件恢复原配置的类型 4／8／10／13／14、原 prefab 和脚本类绑定；培养窗口恢复原状态字段、材料计数、星级与跨品质通知，培养配色／原代码与横向虚拟滚动组件的输入输出 SHA256 记录在 auxiliary_inputs.cultivation_code、cultivation_colors、cultivation_scroll，来源、输入及输出哈希同见该清单；结算附加皮肤来自原 `AppearanceConfig`，网格、骨骼与图集见 `character-creation/native-geometry-source-manifest.json`，纹理显式绑定 `ProfessionUI/character-textures`。技能包内缺失的 18 个原技能图标按 `Assets/Image/ItemIcon/Skill/skill_<id>` 解析同版本 Sprite，地址、bundle、对象哈希及转换后字节哈希记录在 `auxiliary_sprites`。七套方案取原 `GameSettings.SkillPlanMaxCount` 与中文键 `skill_plan_default_name_1..7`；原方案 Scrollbar 对象 `-4354697964205878964` 的 SHA256 为 `53972465f66e438fa93ab2217ff461a62d6b579c6ffbcea23b42e78b90e219fc`。状态与运行验证见[技能功能页](../../docs/features/skills.md)，新增载荷未声明进入完整素材快照。技能方案输入框恢复 `TMP_InputField` 的完整 580 字节及 APK 分支字段 `m_EncodingType`；名称规则直接迁移官方 `Common.CJK`，公式、错误码、客户端调用与元数据哈希记录在技能载荷的 `auxiliary_inputs.plan_name_rules`、`plan_input`、`plan_input_schema`。全部技能模板与 SystemMsgPanel 的原 TMP 文字、顶点渐变预设及 TMPEffect 原组件字节哈希纳入 text_style_inputs；安装遮罩、取消按钮、四槽高亮和祝福文字从原组件字段绑定，不另画遮罩或提示。

技能载荷字节校验：四项字体／材质／规则文件恢复清单中既定 LF 字节，并固定 Git 换行策略；输入及预期哈希保持不变。既有提交 `562bf7f62` 在 `skill-ui.json` 仅增加 `auxiliary_inputs.pet_entity_formula` 的历史来源指纹，原模块清单尚未同步；现逐项比较 JSON，证明移除此项来源元数据后与原已校验载荷完全一致，在 `unity/Assets/Official/Skills/source-manifest.json` 保留旧输出哈希、原提交和新增字段并记录当前输出，不改 UI、公式或美术。 NPC 援助规则载荷同样恢复既定 LF 字节并固定换行策略，原871输入和预期哈希不变。既有同步工具补齐28个已交付技能 Sprite 的显式引用，共297项；没有新增图片。目录、meta、同步工具的输出字节与 SHA256 记录于同一清单的 `runtime_binding`。

## 提取与登记

1. 先核对 APK 的包内清单、实际文件／内部对象、prefab 引用、绑定和 CAB 依赖；确认包内没有时，再查所选版本已下载官方云端资源及哈希。加载或解码失败不等于资源缺失。
2. 使用 `scripts/official_apk.py`、`fetch_official_assets.py` 和现有缓存。当前输入读取用 `Apk.current()` 或 `Apk(manifest=None)`；旧 `Apk()` 的历史语义不能冒充当前入口。
3. 缺少相关官方程序集／反编译结果时运行 `python scripts/prepare_official_sources.py --decompile-client`，按本机环境指定 `--dotnet`／`--ilspy`，核对提取范围与依赖。已有结果先核验来源再复用。
4. 模块清单保存版本、实际 URL、bundle 名／对象名或 CAB／PathID、原始输入 SHA256、大小／MD5、工具及处理方式、输出路径／SHA256和显式加载绑定。原始包哈希与转换输出哈希分开登记，未变化文件沿真实转换链复用。
5. 两处均核实缺失时，列出具体资源／bundle、查找范围、方法与影响，请用户决定；不自行绘制、近似替换或降低表现。服务器规则缺口写入所属功能／地图页。

```powershell
python -B scripts/official_manifest.py --verify-packaged
python -B scripts/fetch_official_runtime_assets.py --verify-cache 202610211330871
python -B scripts/fetch_official_assets.py --extract '<bundle 名称正则>'
```

原始包、工具及缓存不进入 Windows 运行目录。Android → Windows 的恢复、脚本／shader／插件适配和目标平台检查见[素材管理](../../docs/asset-management.md#android-资源迁移到-windows)。

Map13 四份 NPC 出现／消失原生演出复用：固定 871_156598，独立使用 Map13 已核验的原 prefab 转换结果，核对 CAB／根对象、完整层级、262 个对象载荷哈希，并核验原生粒子／渲染器／Animator 的 902 个指针、94,949 个值和 176 项 lightmap 默认值；所有引用按 Map13 原 CAB／pathID 重新对应共享载荷，不依赖其他地图未提交的提取文件；共享原材质、shader、贴图和原应用代码的 76 项交付文件全部校验，新增 Map13 原 GUID 目录，零替代美术／Spine runtime。`map13-native-npc-appearance-bindings-source-manifest.json` 记录全部输入、共享载荷、输出及内置屏 Unity 2022.3.57f1c2 D3D11 实播来源：53 层 GPU mip 字节一致，四份演出各两次、粒子／跟随／淡出／显隐／事件顺序及 16 个已查看实际帧。原 NPC 动画与实际音频、完整剧情／地图输入存档及 Windows Player 仍待完成，未进入完整素材快照。原剧情转换器按 LF 保存，引用和完整层级核对重新执行，来源工具哈希与 Git 字节一致。

Map13 剧情跟随者资源：固定 `871_156598`，五张原 NPC Atlas 原生 RGBA32 与 GUID 由历史已核验载荷恢复，输入 bundle／对象、ASTC profile、全部 mip 与输出哈希见[图集来源](official-map/map13-npc-follower-textures-source-manifest.json)。[骨骼共用凭据](official-map/map13-follower-skeleton-resources-source-manifest.json) 逐项核对 Map13 与 Map12 的原 CAB／path ID、原 Atlas 字节和骨骼／皮肤／约束／动画 JSON 内容一致，复用两份现有原生数据包；记录四个原 NPC prefab 的模型变换，并按原 PPtr 生成五项专用材质目录。核对工具仅使用 Python 标准库，不加载动画 runtime 或烘焙姿势；素材未进入完整快照。

Map13 原文字特效 AOT 输入：固定 `871_156598` 的原始映像／metadata 哈希和 30 项方法证据保存在 [文字效果代码来源](official-map/map13-text-effects-code-source-manifest.json)。原 `.tools/source-packages/commerce-native/libil2cpp-restored.bin` 当前缺失，通用 `official-code/global-metadata.dat` 及两项提取工具已由后续准备流程更新；历史输入／工具哈希保留，重提取须恢复匹配版本。当前模板、交付代码和验收配置另按本次实际内容记录；提取工具在写出前检查原输入身份，不能用新版 metadata 重新标记为旧版产物。

Map14 实体 prefab：固定云端选择 `871_156598`，输入选择、官方 bundle 身份、对象路径及原始字段核对见 `official-map/map14-native-entity-input-source-manifest.json` 与 `official-map/map14-native-entity-export-source-manifest.json`。Unity 包含 416 个被放置实体引用的原 prefab 根及其依赖，原路径和 EntityClassId 由 `Map14/Native/entity-prefab-catalog` 显式绑定；`scripts/test_map14_entity_prefab_export.py` 对照导出回执逐根验证恢复与打包文件 SHA256。运行状态及未完成交互范围见[龙之国](../../docs/maps/map-14.md)。

主线国度商店表：`prepare_unity_nation_shop_catalog.py` 从所选 `871_156598` 官方 `map_nation_shop`、`shop_level_group`、`shop`、`shop_goods` 表生成本地运行目录；固定商品、等级阈值、原价、货币、库存、购买限制和刷新周期逐表导出。`mainline-nation-shop-source-manifest.json` 记录官方表输入与 `nation-shops.json` 输出哈希。离线经验按用户确认规则以消耗专属货币 1:1 累计，运行、存档及未完成项见[泽之国](../../docs/maps/map-13.md)。

山之国战斗碑来源：[原始规则与物品](official-map/map12-steles-source-manifest.json) · [原窗口转换](official-map/map12-stele-ui-source-manifest.json) · [Windows 原生绑定](official-map/map12-ui-native-source-manifest.json)。

山之国原队伍来源：[邀请动作](official-map/map12-team-invitation-source-manifest.json) · [原权限控件](official-map/map12-team-toggle-source-manifest.json) · [原序列化字体](official-map/map12-team-font-source-manifest.json) · [原中文字体](official-map/map12-team-chinese-font-source-manifest.json) · [原头像与队员栏](official-map/map12-team-icons-source-manifest.json) · [原目标选择代码](official-map/map12-team-targets-source-manifest.json) · [原目标选择器](official-map/map12-team-target-prefabs-source-manifest.json) · [原确认音效](official-map/map12-team-target-audio-source-manifest.json) · [原精确 Sprite 网格](official-map/map12-team-target-sprites-source-manifest.json) · [原完整队伍窗口代码](official-map/map12-team-panel-source-manifest.json) · [原完整队伍窗口与完整图集](official-map/map12-team-panel-prefabs-source-manifest.json) · [原邀请名单与排序](official-map/map12-team-invite-rules-source-manifest.json) · [原邀请行与显示控件](official-map/map12-team-invite-widgets-source-manifest.json) · [原邀请模式与共享控件](official-map/map12-team-invite-panels-source-manifest.json)；[原 NPC 骨骼与拆分动画](official-map/map12-team-npc-skeletons-source-manifest.json)及[独立原生数据绑定](official-map/map12-team-npc-data-source-manifest.json)；[原生 NPC 动画轨道](official-map/map12-team-npc-channels-source-manifest.json)；[原 NPC 模板](official-map/map12-team-npc-prefabs-source-manifest.json)、[NPC 原生转换输入](official-map/map12-team-npc-native-input-source-manifest.json)及[原生字段核对](official-map/map12-team-npc-native-source-manifest.json)；[上下文邀请组件](official-map/map12-context-inviters-source-manifest.json)、[完整邀请动画转换源](official-map/map12-team-invite-animation-source-manifest.json)及[Windows 原动画载荷](official-map/map12-team-invite-windows-animation-source-manifest.json) · [原邀请依赖提取](official-map/map12-team-invite-extra-source-manifest.json) · [原邀请依赖原生字段](official-map/map12-team-invite-extra-native-source-manifest.json)。

Map13 原剧情演出请求边界：`map13-story-playable-requests-source-manifest.json` 固定原 `PlayPlayableNode.cs`、`GetEntityBaseNode.cs`、`XNode/Node.cs`／`NodePort.cs`、原图及 12 份原生演出来源。直接使用迁移原请求、原管理器／中心／FIFO 池；保留 `-1` 原时长、完整 GUID／标志和 XNode 精确分组端口语义，未把 55 个编号端口合并成原图未连接的 58 个分组端口。内置屏 Unity 2022.3.57f1c2 D3D11 实播 29 请求、55 输入、20 等待、两处前置停止并查看原地形上的多演员特效画面，来源记录日志／画面哈希、声音事件 ID 和清理结果。演员为隔离参数／挂点对象，正式 EC／NPC 原动画、实际音频、完整剧情与 Windows Player 仍未完成；无新增替代美术／Spine runtime，未生成完整素材快照。

泽之国视野本地边界补齐原 148 份身体模板、2,690 份放置变换、108 份营地成员列表与 478 份所属营地记录；原坐标旋转、身体范围缓存及共享组件执行顺序保持，离线适配只提供本地状态、事件与存档输入。`scripts/prepare_unity_map13_grid_visibility.py` 记录全部原代码／配置及输出哈希，`OfflineMap13GridVisibility` 在内置屏 D3D11 编辑器核对 106 地区、57,600 格恢复和原压缩字节，见[视野来源](official-map/map13-grid-visibility-source-manifest.json)。该项不代表完整地图实体、输入或 Windows 发布完成，无新增图像素材。

Map13 正式原生入口与本地藤蔓：`prepare_unity_map13_tree_world.py` 复核已交付的两份完整原树 prefab 与当前放置／模板／显示选择，显式绑定唯一根节点、三处本地状态和原数量奖励查询；正式宿主接入原生地图、默认入口、视野字节存档及官方 −250 相机深度。原序列、EC 视图和实时几何沿用已核验模块，无 Spine runtime 迁移或替代素材。内置屏 D3D11 Play Mode 覆盖模拟地图点击、4／5／6 原奖励与果实数、实际视锥／淡入、消散移除及磁盘冷读，运行画面已查看；其余实体／剧情、Windows Player 与发布尚未完成。输入／输出及执行哈希见 `official-map/map13-native-tree-world-source-manifest.json`；未生成完整素材快照。 Unity 导入／编译、ProjectTools.Validate 及根工程 74,281 GUID 静态检查通过。

泽之国原小地图：`scripts/prepare_unity_map13_minimap_inputs.py` 严格恢复所选 `871_156598` 的完整 40 包原始依赖，区分 APK／云端来源并校验官方程序集；`prepare_unity_map13_minimap_sources.py` 对 12 处托管引用作逆转换及逐字段核对，保存完整原 prefab 来源文本和组件字段。原始输入、9,052 个对象核对、Windows 导出工具与输出哈希见 [小地图来源](official-map/map13-native-minimap-source-manifest.json)。`prepare_unity_map13_minimap_mask.py` 复用原遮罩类及共享格子数据；全部 256 种邻格配置的原生 Tilemap Play Mode、零运行错误、Unity 编译和 ProjectTools 检查见 [遮罩代码来源](official-map/map13-minimap-mask-code-source-manifest.json)。这些结果覆盖来源与遮罩核心，正式完整小地图、UI／地图输入存档和 Windows Player 尚未完成；未新增替代素材，未生成完整素材快照。

泽之国原小地图视野：`prepare_unity_map13_minimap_view.py` 逐类复用四份原视野处理器，调用已迁移的完整原 `MyGridTransform`，仅替换命名空间及消费接口。原输入／输出哈希、483 格有序视野、25 组边界、角色切换／释放 Play Mode 与编译检查见 [视野来源](official-map/map13-minimap-view-code-source-manifest.json)。正式小地图、输入存档及 Player 尚未完成；无新增图像素材或完整快照。

泽之国原小地图地区：`prepare_unity_map13_minimap_regions.py` 从已核验的官方 40 包闭包恢复 106 个地区 Tilemap、7,577 条原记录、12 种 Tile 和两张原 ASTC 纹理；原编译遮罩程序转换为 4 个 Windows 变体，保留原透明／遮罩分支，替换导出器的非原实现占位。输入输出／逐 mip 哈希、显式原生绑定、Play Mode 与 8 个 Windows 着色阶段及已查看的公开／未公开画面见 [地区来源](official-map/map13-native-minimap-regions-source-manifest.json)。正式完整小地图与 Player 尚未完成，未生成完整素材快照。

泽之国小地图原地区管理：`prepare_unity_map13_minimap_region_manager.py` 逐类迁移原玩家 UI 初始化和地区加载／遮罩管理器，保留反序遍历、原边界／GUID、10 秒回收与取消时序，复用原 UIBase、AssetGuidProxy 和 MapRegionStatus；完整输入输出哈希及实际等待、角色生命周期和资源释放 Play Mode 见 [地区管理来源](official-map/map13-minimap-region-manager-code-source-manifest.json)。正式根组件、本地状态接线及 Player 尚未完成，无新增素材或完整快照。

泽之国原小地图相机与配置：`prepare_unity_map13_minimap_camera.py` 迁移完整原相机、配置查找和虚线网格代码；Windows UGUI 滚轮／指针入口复用原缩放公式和已迁移拖拽物理。`prepare_unity_map13_minimap_settings.py --cache <本会话目录> --prepare-inputs --convert` 可从已核验 MapCommon 的 139 包闭包恢复完整原配置及所有资源引用，保留原字段、71 个 Tile 与 Sprite、虚线 prefab／材质和原 ASTC 纹理。共享输入工具参数化后已重新水合两份输入并逐包比较全部转换输入哈希，字节完全相同才复用转换缓存；小地图原来源同时重验。完整输入输出哈希、2930 次查找／原缓存与回退、原 URP 相机实播和已查看画面见 [相机代码](official-map/map13-minimap-camera-code-source-manifest.json) 与 [原配置资源](official-map/map13-native-minimap-settings-source-manifest.json)。正式地图鼠标／存档及 Player 尚未完成；素材未进入完整快照。

泽之国原小地图无遮挡层：`prepare_unity_map13_minimap_nonmaskable.py --cache <已核验小地图导出目录>` 复用 APK／所选官方包的 40 包转换闭包，完整恢复原无遮挡层 prefab 的所有节点、组件与资源。逐包重核输入哈希，绑定原完整迁移组件和实际嵌入的 URP 相机脚本，原 Square ASTC 逐 mip 解码至原生 RGBA32，不重绘／采样／替代。代码、资源、纹理输入输出哈希与 Play Mode 显隐／缩放／跟随／原 URP 实际画面验证见 [来源清单](official-map/map13-minimap-nonmaskable-source-manifest.json)。正式地图鼠标／存档与 Player 尚未完成；素材未进入完整快照。

泽之国原小地图资源点：`prepare_unity_map13_minimap_resource_points.py` 迁移原六个完整类并复用原 UIBase／MapUIBase／MonoDictionary／AssetGuidProxy；`prepare_unity_map13_minimap_resource_boards.py --cache <已核验小地图导出目录>` 恢复 23 个原面板及其完整依赖，从原完整小地图提取管理器三对象子树并保留全部原字段，只断开父引用以支持独立加载。全部 23 个动态图标依次在 APK 及所选官方资源中定位，保留原 Sprite 几何与精确 ASTC 解码，背景 Sprite 解析原 Atlas 有效渲染数据，恢复原逻辑矩形与 UV。单机原生资源目录和本地配置／玩家／资源服务适配位于 `Runtime/Offline/World/NativeMap13MiniMapResources`，不新增服务器规则。输入输出、脚本绑定、原图标／背景网格及纹理哈希、106 项物品与 149 项怪物查询、显隐与资源生命周期、原 URP 实际画面验证见 [代码清单](official-map/map13-minimap-resource-points-code-source-manifest.json) 和 [原资源清单](official-map/map13-native-minimap-resource-points-source-manifest.json)。正式地图鼠标／存档、原根绑定和 Player 尚未完成；素材未进入完整快照。

当前主线小地图资源点：`scripts/prepare_mainline_minimap_resource_points.py --map-id <10|11|12|13|14|16>` 先解析 APK，再按所选 `884_156692` 清单与实际 CAB 对象恢复各图资源管理器子树、原资源板、动态图标及图集背景；地区、实体组、物品定位和刷新组定位均由同版 Luban 构造器解码。转换复用当前原生 prefab／Windows 纹理绑定链，保留原逻辑 Sprite 矩形、网格、图集 UV、像素单位、图层与字段；六个官方资源点类逐文件核对当前字节身份后复用既有迁移。当前载荷独立保存于 `Assets/Official/World/Map/{ID}/NativeMiniMapResourcesCurrent`，历史871载荷保持独立；原包身份、配置输入、代码等价凭据、原对象、全部输出 SHA256 及显式 Resources 绑定分别见 `official-map/map{ID}-current-minimap-resources-source-manifest.json`。未生成完整素材快照；使用与验证范围见[主线地图公共模块](../../docs/features/mainline-map-modules.md)。

泽之国原小地图尺寸保持：`prepare_unity_map13_minimap_sizing.py` 完整迁移原三个尺寸保持类，直接复用原 GAction 和 UIBase，保留阈值、统一缩放、原默认值恢复与全部订阅生命周期，只适配命名空间、根依赖端口、日志和资源释放后端。代码／依赖哈希、Play Mode 的 20 个公式边界、28 个缩放保护条件、初始化／重新启用及 61 次事件，连同 Unity 验证结果见 [原代码清单](official-map/map13-minimap-sizing-code-source-manifest.json)。未改变正式 prefab 或素材，原小地图根和正式画面／输入／存档及 Player 尚未接通。

泽之国原小地图地区边界：`prepare_unity_map13_minimap_borders.py` 迁入两个完整原几何类，直接复用原 Pos2d／Direction，仅适配命名空间和日志。原代码／依赖哈希和实际 Play Mode 全图边线、村庄轮廓、凹角偏移、取整边界结果见 [原代码清单](official-map/map13-minimap-borders-code-source-manifest.json)。正式界面／根接入和 Player 仍未完成。

泽之国原小地图地区说明：`prepare_unity_map13_minimap_descriptions.py` 迁入四个完整原类、原地区类型及完整 `MapUtils.GetMapRegionPosList`，复用原文本／战力／UIBase／可见性，仅适配本地服务后端。`prepare_unity_map13_minimap_description_assets.py` 复原两套原说明与村庄线框／高亮，使用已逐字段验证的原 RefIds 注册表，保留所有原层级、对象 ID、字段、5 个 Sprite 网格／UV／矩形与 3 张 ASTC 贴图原 mip；复用所选官方字体及其独立运行时字形实例。106 地区中心、全图解锁条件、状态／进度、全屏高亮、原虚线池、真实渲染和资源释放结果见 [代码清单](official-map/map13-minimap-descriptions-code-source-manifest.json)、[素材清单](official-map/map13-native-minimap-descriptions-source-manifest.json)。正式全图鼠标、存档、根绑定与 Player 尚未完成。

泽之国原小地图实时／离线迷雾：`prepare_unity_map13_minimap_masks.py` 复制完整原遮罩管理器、离线响应 DTO 和原网格坐标转换，只替换网络注解／单例依赖与 Unity 嵌套枚举限定；原数组长度、遍历顺序、可见边界、逐帧批处理与激活地区填充不变。`prepare_unity_map13_minimap_mask_assets.py` 恢复原 11 Tile、9 Sprite、2 纹理、材质和完整 Tilemap／Renderer，严格保留原网格／Atlas UV／mip，四种原 GLES 程序与输出哈希均记录。完整 57,600 格、隔离压缩数据恢复、原默认数组零坐标清空、GPU 渲染与订阅释放已实测，两张画面已查看；正式地图根／鼠标／存档／Player 尚未完成，详见 [代码清单](official-map/map13-minimap-masks-code-source-manifest.json)、[素材清单](official-map/map13-native-minimap-masks-source-manifest.json)。

泽之国原小地图玩家标记：`prepare_unity_map13_minimap_character.py` 保留完整原管理器、相机绘制、位置与状态分支，只以显式只读端口提供原地图／角色数据，修复反编译 out／cast；`prepare_unity_map13_minimap_character_assets.py` 严格核对原转换输入与恢复后的根，提取全部 19 个原对象、原循环动画和两张精确 Sprite，复用已验证原 Atlas。16 组位置、8 条原曲线、实际 GPU 画面、全屏／MainCamera 停绘、角色更换、销毁／释放已验证；正式全图输入、后端与 Player 未完成，详见 [代码清单](official-map/map13-minimap-character-code-source-manifest.json)、[素材清单](official-map/map13-native-minimap-character-source-manifest.json)。

泽之国原小地图标记／弹窗：三个 prepare_unity_map13_minimap_mark_core、minimap_window、minimap_mark_dialog_assets 脚本分别迁入完整原标记／UI／弹窗逻辑、完整原窗口与队列状态机、三份恢复托管引用后的原 prefab 及精确 Sprite/ASTC/RGBA32 mip/动画/音效引用。R3 SerialDisposable 与 CancellationDisposable 固定上游提交 a8d5fb3c2d0afd124223084e94c9e0ee73497510，许可随代码交付；仅做命名空间与 Unity C# 语法适配。三份实播原界面、42 组窗口状态、两次实际射线命中／模拟按钮、音效与释放通过；正式地图实体标记、容器模糊、输入／存档和 Player 仍待完成。详见 [标记清单](official-map/map13-minimap-mark-core-code-source-manifest.json)、[窗口清单](official-map/map13-minimap-window-code-source-manifest.json)、[资源清单](official-map/map13-native-minimap-mark-dialogs-source-manifest.json) 及 [泽之国](../../docs/maps/map-13.md)。未生成完整素材快照。

泽之国原小地图标记数据：prepare_unity_map13_minimap_mark_data.py 从所选 871_156598 客户端六类标记和共享 MapUtils／LanguageUtils、实体描述参数提供器与 WayPointComponentImplement 迁入完整实现；仅将全局配置／实体／会话服务替换为显式类型端口，保持原分支、数值、执行顺序及真实实体身份。复用原 GridTransformComponentInfo 与 EntityMapUIDisplayInfo。Play Mode 的 148 种体型、2,690 个位置、5,380 次中心及工厂／状态／描述／传送验证通过，正式地图绑定与 Player 待完成，详见 [来源清单](official-map/map13-minimap-mark-data-code-source-manifest.json) 与 [泽之国](../../docs/maps/map-13.md)。

泽之国原小地图标记生成与池：prepare_unity_map13_minimap_mark_spawn.py 完整迁入官方客户端的生成管理器、池、查询接口与提示等级，按反编译状态机恢复延迟清理；显式加载／实例化／实体／格子／性能接口只消费本地实际状态。R3 DisposableBag 固定 a8d5fb3c2d0afd124223084e94c9e0ee73497510，源码哈希锁定、保留 MIT 许可；仅做命名空间及 Unity C# 适配。规则 Play Mode、两级静态检查、Unity 编译与 Validate 通过；正式标记美术、整图交互／存档及 Player 未完成。详见 [清单](official-map/map13-minimap-mark-spawn-code-source-manifest.json) 与 [泽之国](../../docs/maps/map-13.md)。

泽之国原小地图区域与离线标记：prepare_unity_map13_minimap_mark_managers.py 从所选 871_156598 完整迁入四管理器、四组实体事件接口／委托、MapPartInfoBase 的原支持判断、MapMyEntityUtils 的原归属判断及完整 EntityOwnershipData 字段／操作码；原区域配置的消费投影只暴露两个支持字段。复用已迁移的完整事件分派、GAction、归属枚举和离线响应，原配置标记选择的覆盖与回退逻辑也完整保留。Play Mode 条件表、生命周期、事件／离线注册和相关 5,380 次中心回归通过，正式标记美术／整图存档及 Player 未完成。详见 [清单](official-map/map13-minimap-mark-managers-code-source-manifest.json) 与 [泽之国](../../docs/maps/map-13.md)。

泽之国原小地图标记：prepare_unity_map13_minimap_mark_assets.py 由实际 Map13 放置／UI 选择和原扩展表推导 41 种标记，核对 APK／所选云端 18 个依赖包、175 个原托管引用注册表（4,557 个值及 660 个原生指针），恢复 117 个原生资产并显式绑定原脚本、TMP 和所选官方字体。prepare_unity_map13_minimap_mark_supports.py 完整迁入六类支持组件及实体提示节点、身份／键和原次数／归属／提示图选择分支。41 个原 prefab 的 Play Mode 状态、事件生命周期及 URP 画面检查通过，正式地图输入／存档、原提示图控制器和 Windows Player 尚待接通。见 [代码清单](official-map/map13-minimap-mark-supports-code-source-manifest.json)、[资源清单](official-map/map13-native-minimap-marks-source-manifest.json) 与 [泽之国](../../docs/maps/map-13.md)。

泽之国原小地图点击与当前相机框：prepare_unity_map13_minimap_mark_click.py 完整迁入原点击组件和坐标解析分支，以明确本地输入／相机／发布接口替换注入；保留源查询顺序、射线／viewport、错误与视野框行为。prepare_unity_map13_minimap_click_frame_assets.py 从已核验 MiniMap_13 原生转换结果提取原三对象视野框及切片精灵，复用同一原图集并保持原排序、层、隐藏状态和变换。8 次模拟鼠标输入及原生画面、Unity 编译／Validate／静态检查通过；正式面板／整图／存档／Player 尚待接通。见 [代码](official-map/map13-minimap-mark-click-code-source-manifest.json)、[资源](official-map/map13-native-minimap-click-frame-source-manifest.json) 与 [泽之国](../../docs/maps/map-13.md)。
| 通天塔入口运行时定义 | `assets/official/regions/15.json`、`15-entities.json` 与 `unity/Assets/Game/Resources/Map15/Group/maps/15.json`，由 `scripts/prepare_unity_map15_runtime_data.py` 生成 `Assets/Official/World/Map/15/map15.json`、来源清单和 `Map15/Entities/catalog.json`；保留 25×30 导航、26 个实体与 871 组件闭合 |

伙伴原生赠礼区域输入：当前 `884_156692` 的 NPC／物品／通用资源分包、原 `NpcDetailPanel` 字段缓存、原 Btn_Base_Click 编译常量及所选中文语言分包；APK 来源保持原 `881_156673` 标识。原对象身份、恢复字段、代码／资源输入哈希及转换方法见 [来源清单](partner-gift-controller-source-manifest.json)。

伙伴好感属性输入：所选 `884_156692` 的 WwiseBnk `wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile` 与同版本 Luban `level_prop_default_value`／`prop_cfg`；APK 优先核对、原索引、二进制、构造器及转换哈希见 [来源清单](partner-properties-source-manifest.json)。

- 伙伴称谓中文字符输入：当前 `884_156692` 的 `CustomContentSettings` 按 `zh_CN = 0` 指向 GUID `366b7fb6f251a70459f751d0fc324b65`，原 `FZY4K_Characters.txt` 为 65,628 字节，SHA256 `aa014998f49525ac47e63286168545af758912920bc6fcad0634c7575b233462`。`prepare_partner_gift_configuration.py` 通过原资源索引定位设置与 TextAsset，按 APK 优先／同版本已校验云端回退读取原对象并打包原字符载荷；输入包、对象、原码、输出及绑定哈希见 `partner-gift-configuration-source-manifest.json`。

场景武器输入与应用代码：当前 `884_156692` 索引下 APK 优先解析的剑／法杖 Integrated prefab、原场景材质与 shader，按完整原对象字节身份复用已有模型／图集／高分辨率加载器，当前 Windows shader 转换保持独立身份；原四节点字段、程序集、应用源码、转换工具、输出哈希与显式绑定见 [场景武器](battle-animation/scene-weapon-source-manifest.json) 和 [轻量渲染器](battle-animation/scene-renderer-runtime-source-manifest.json)。

伙伴当前原生名册区域：输入采用选定 `884_156692` 的官方 APK／已校验云端包及同版本原生转换缓存；`prepare_partner_roster_native.py` 按 `NpcPanel.mNpcListGo` 原指针提取并绑定名册层级，原卡片按 CAB／PathID、GUID 和已有凭据复用。动画曲线脚本以程序集 GUID＋脚本 fileID 核对，状态／过渡及参数默认值按原编译数据校验，Windows 精灵、贴图、字体与 shader 延用所选版本转换链。全部原对象、提取方式、源码／资源输入与安装输出哈希见 [原生名册来源](partner-roster-native-source-manifest.json)；使用方式见 [伙伴功能](../../docs/features/partners.md)。本次未生成完整素材快照。

当前场景人物输入：所选 `884_156692` 的 APK 优先 `PlayerModel_Game` 原 prefab、公共 SkinAsset／Low 加载器、男女核心骨骼与全部分离动作、完整原图集；实际 APK 身份保持 `881_156673`。使用固定哈希的独立 MIT 格式读取器进行完整二进制消费和无损字段投影，当前与历史871分别保存。原字段、GUID、原包／对象、转换工具、完整输入输出哈希及显式本地绑定见 [场景人物来源](battle-animation/scene-core-source-manifest.json)；素材未生成完整快照。

当前场景外观应用代码：从 `884_156692` 已核验的 `Assembly-CSharp.dll` 和反编译应用源码迁移 `AppearanceSpine`／`AppearanceRenderer`／`AppearanceMecanim`，与历史871 UI 基类分别保留；共用 SkinTextureAsset／配置和外观接口另核对当前源码。框架资源、渲染器、Mecanim 与缓存操作改为显式端口，输入程序集、原应用源码、共享依赖、转换脚本、程序集绑定和迁移输出哈希见 [场景外观运行时来源](battle-animation/scene-appearance-runtime-source-manifest.json)。未复制动画 SDK 实现。

当前动态场景分段代码：从同版已核验 `Assembly-CSharp.dll` 的 `DynamicSkeletonRenderSeparator` 应用源码迁移池化、排序与可见性分支，SDK 基类调用替换为显式端口，Unity 网格赋值采用独立原生后端。原程序集／应用源码、转换与迁移输出哈希见 [动态场景分段来源](battle-animation/scene-separator-source-manifest.json)；验证复用既有当前来源剑、杖原资源，没有新增美术载荷，也未复制 SDK 渲染实现。

当前原生场景绘制端：复用上述 `884_156692` 人物层级、完整图集／Low 目录和分段应用来源，独立编写 Unity 网格赋值及原字段绑定；没有读取或复制动画 SDK 实现，没有新增或重绘美术载荷。输入及独立实现输出哈希、Resources 地址和原外观／跟随接口绑定由 `prepare_battle_scene_native_renderer.py` 记录于 [原生场景绘制来源](battle-animation/scene-native-renderer-source-manifest.json)。其中当前 `BaseSkeletonFollower`／`SkeletonRendererFollower` 应用源码逐字节哈希与历史迁移凭据一致，复用既有应用迁移；清单同时保留当前输入、历史源身份及迁移输出，未读取动画 SDK 实现。独立剑／杖继续使用已提取的当前四节点层级、原点槽／材质／跟随字段和已核验字节身份的原载荷；新原生装配、同场景 Low／High 路由、官方部件池端口及验证入口哈希也写入该清单，先前提取凭据保留其原输入及运行时身份，不改标版本。

当前人物外观调用与配置：`prepare_battle_scene_character_appearance.py` 从所选 `884_156692` 应用反编译结果迁移 `AppearanceSystem.Runtime/AppearanceCharacter.cs`；先核对实际APK内 `defaultpackage_assets_scriptableconfig_modules_appearance.bundle` 的 `AppearanceConfig` 对象，恢复当前完整字段并与历史871配置分别保存。10份共享配置类型核对当前／历史源码字节身份后复用，配置对象不改标历史版本。原程序集、对象原始载荷与包身份、配置全部字段、共享类型身份、转换及独立绑定输出哈希（含共享原生组合皮肤类型）、Resources地址依赖见[人物外观来源](battle-animation/scene-character-appearance-source-manifest.json)。不读取或复制动画SDK实现。

当前玩家外观与默认资料输入：`prepare_battle_scene_player_appearance.py` 逐字节核对当前应用与共享迁移来源，从实际APK的 Luban 分包完整解码所选 `884_156692` 的9张表，保留外观消费字段、枚举和集合顺序；全局设置按当前生成构造器读取，包括独立 Luban 向量结构，严格消费全部载荷。`PlayerAppearance` 直接迁自当前应用源码，动画名称组合规则恢复原 ScriptableObject 字段；当前表、默认剑／杖设置和规则使用独立 `Combat/SceneAppearance` 地址。历史共享解析器仅比较已迁移的 PostParse／查询方法，不把已变化、未迁移的随机选择方法纳入复用结论。原程序集／metadata、167份构造器源码、39项应用身份比对、配置对象及输入输出哈希见[玩家外观来源](battle-animation/scene-player-appearance-source-manifest.json)。不读取或复制动画SDK实现；新增载荷未进入完整快照。

当前完整场景动作载荷：`prepare_battle_scene_motions.py` 核对所选884_156692男女两个原动作bundle、独立原生转换凭据及当前骨骼动作名称闭包，保留448条男性和455条女性AnimationClip的原始字段。27份已导入原clip按实际输入身份复用，876份新增载荷逐字节复制，只有导入meta的GUID显式重绑定；动画子资产的容器地址不是唯一动作地址，目录按原性别与动作名称绑定。原包、对象、转换输入、原始与导入meta、输出哈希、完整目录及原始时长差异见[场景动作来源](battle-animation/scene-motions-source-manifest.json)。真实技能clip经过原应用Playable与统一原生绘制端的接线身份见[技能动作接线来源](battle-animation/scene-skill-motions-source-manifest.json)。该清单也记录独立曲线端对原恒定线性区间的精确常量处理及对应源轨道回归入口，保留原始曲线载荷与IK范围保护。未读取或复制动画SDK实现，新增载荷未进入完整快照；实现与实际验证范围只在[战斗执行](../../docs/features/battle-execution.md)维护。

当前独立武器播放时钟：原应用 `AppearanceAnimUtils.PlayAnimation`／`SetAnimation`、`AppearanceSkeletonPart`、两份当前集成武器 prefab 时序字段、公开 UpdateTiming 枚举常量和独立实现接线身份见[武器播放来源](battle-animation/scene-weapon-playback-source-manifest.json)，重建入口为 `record_battle_scene_weapon_playback.py`。六条原骨骼动作继续复用已核验与当前包对象字节一致的历史871原生载荷，保留原来源标签；本批无素材变动或完整快照，不读取或复制动画SDK实现。

当前统一场景MixNext绘制与通知：`record_battle_scene_bound_mix_next.py` 沿用当前884_156692人物载荷、已有独立混合端与原应用部件通知消费代码，记录统一姿态入口、绘制接线和验证入口的当前身份。原应用、公开行为文档、依赖及输入输出哈希见[统一绘制与通知来源](battle-animation/scene-bound-mix-next-source-manifest.json)。不读取或复制动画SDK实现，不改变人物／武器素材载荷；实现与实际验证范围只在[战斗执行](../../docs/features/battle-execution.md)维护。

当前原生MixNext输入与独立接线：`record_battle_native_mix_next.py` 核对当前884_156692的PlayerModel_Game载荷中原translator配置，并仅从原global-metadata.dat读取相关公开枚举常量，不读取SDK方法实现。原载荷／元数据身份、公开行为文档、局部通道与约束混合、真实Animator验证入口及当前输入输出哈希见[MixNext来源清单](battle-animation/native-mix-next-source-manifest.json)。没有修改人物／武器素材载荷；功能和实际验证范围只在[战斗执行](../../docs/features/battle-execution.md)维护。

当前原生场景Mecanim部件通知：`ProfessionNativeSceneRenderer.BindAnimatorPose` 显式接入原生Animator姿态生产者，并通过既有 `IProfessionAppearancePartMecanim` 交给当前原版 `AppearanceMecanimPartHandler`。原应用来源、独立接入、验证入口及输入输出哈希见[场景Mecanim部件来源](battle-animation/scene-mecanim-parts-source-manifest.json)。该记录不表示完整混合、物理或正式战斗宿主已经完成。

当前原生Animator姿态输入：`ProfessionNativeAnimatorPose` 将真实Unity控制器的原clip身份与时钟传给显式姿态求解委托，当前原控制器、人物输入及既有约束端分别保留来源身份；没有读取SDK实现。独立端口、验证入口、编译依赖、生成器及相关输入输出哈希见[Animator姿态来源](battle-animation/native-animator-pose-source-manifest.json)。该记录不表示混合、物理或正式战斗场景已接通。

当前骨骼缓存配置：`record_battle_mecanim_cache_scope.py` 核对所选884应用程序集、9份游戏应用源码、实际配置bundle和其骨骼／clip引用，读取原 `BoneTransformCacheManagerConfig`。男女各自的配置列表只有 `map/idle_01`、`map/run_01`；原对象字节、APK／云端归属、配置字段、输入及工具哈希见[骨骼缓存来源](battle-animation/mecanim-cache-scope-source-manifest.json)。仅记录来源，不读取动画SDK源码。

当前玩家Animator选择：`prepare_battle_scene_player_animator.py` 从所选884应用源码迁移 `PlayerAnimatorAppearance`、`PlayerPhase` 和原 `GetPlayerAnimatorPath` 方法，保留全部场景路径分支，以现有玩家资料绑定提供基础职业、显式委托提供位置查询和同步加载。复用已与实际APK图／参数／转换资源核验的4份当前Game控制器及完整37项原生依赖闭包，按原完整地址显式绑定，不改原控制器或标记曲线。代码、程序集、共享Animator依赖身份、输入输出哈希与绑定见[玩家Animator来源](battle-animation/scene-player-animator-source-manifest.json)；其他场景地址规则不代表已接入其资源。未读取动画SDK实现。

当前场景 Low 人物纹理与材质：按 `884_156692` 索引从实际 `881_156673` APK 优先提取，缺少的包使用已核验同版云缓存；恢复原702组 Low 映射、977个按需 GUID、764条皮肤配置和男女937份原材质。原 ASTC 使用固定 astc_encoder 0.1.12 解码为 Windows 原生 RGBA32，保留原 mip／采样／色彩空间及材质字段，世界 shader 复用字节身份一致的既有当前转换；历史效果载荷缺少采样版本时另生成当前资产。原包／对象、编码与解码字节、工具／运行绑定和全部输出哈希见 [场景纹理来源](battle-animation/scene-texture-source-manifest.json)。素材未生成完整快照。

公共战斗人物头像材质参数：`record_battle_character_portrait_material.py` 按当前 `884_156692` 索引、APK 优先及已校验同版云端依赖，读取男女原骨骼→图集→全部937份材质，核对原 `_WorldClipHeight` 统一为 -1,000,000。原包／对象、指针、参数、工具和运行绑定哈希见 [头像材质来源](battle-hud/character-portrait-material-source-manifest.json)。仅恢复临时摄影模型的原裁剪参数，没有生成或修改美术载荷；摄影参数和旧版来源仍各自保留。


伙伴名册候选范围：`prepare_partner_roster_scope.py` 核对选定 `884_156692` 的 `NpcInfoParser.GetNpcs` 排除语义、原名册及手动相遇条件消费，将商人／职业导师的原图像纳入显式卡片绑定。新增资源仍经 APK 优先、同版本云端、Sprite RenderDataKey 与原对象身份闭合。原语言表没有的 `ui_npc_name_10609` 沿原客户端返回键名；卡片的原空字符串 myKey 按原对象修复，不创建替代名称或美术。原方法、输入／输出哈希与替换范围见[名册范围来源](partner-roster-scope-source-manifest.json)，具体行为见[伙伴功能](../../docs/features/partners.md)。未生成完整素材快照。


### 当前伙伴名册条件来源

`scripts/prepare_partner_conditions.py` 复用选定 `884_156692` 的反编译公共代码，逐字节核对工作来源与 `current/884_156692/official-code/`，提取 `DataHub`、条件枚举、字段默认值及 19 份条件／相遇／信物／比较原方法或代码块。输入为当前 `partner-rules.json` 中 148 条 NPC 的相遇／显示／等待条件与置顶功能 1011，引用闭包为 72 个条件号、九类条件；没有重新标记历史配置版本。原源码／方法、配置、生成器、适配绑定与输出哈希见 [partner-condition-source-manifest.json](partner-condition-source-manifest.json)。


### 当前伙伴自定义称谓来源

`prepare_partner_gift_player.py` 新增核对选定 `884_156692` 的 `NpcDetailPanelGiftContent.OnCustomFriendShipTag` 及 `PlayerNpcDataWrap` 原字段。输入、方法、当前存档结构、默认本地称谓提供者、玩家适配与验证源码哈希见 [partner-gift-player-source-manifest.json](partner-gift-player-source-manifest.json)。单机审核差异及实际验证范围统一见 [伙伴功能](../../docs/features/partners.md)。未新增美术载荷，未生成完整素材快照。

技能连续重放来源：当前 `884_156692` 解码 `Console.dll`、代码回执及原重放配置／槽提供者／客户端消费契约、被动重放状态／四段标签查询／类型替换值契约，与保留 `854_156383` 身份的既有 `skill-rules.json`／`battle-rules.json` 分别记录。输入、适配代码和验证入口的 SHA256 由 `scripts/record_battle_skill_replay_sources.py` 写入 [来源清单](battle-rules/skill-replay-source-manifest.json)；服务器实现缺口、用户确认的单机规则与验证范围集中见[战斗执行](../../docs/features/battle-execution.md)。

装备词条替换参数来源：`record_battle_equipment_term_sources.py` 核对当前 `884_156692` 解码程序集与 `BattleFormulaHandler`、词条参数／祝福查询原方法，保留既有 `854_156383` 装备和战斗运行表身份；原输入、适配代码和验证入口 SHA256 见[来源清单](battle-rules/equipment-term-source-manifest.json)，实际接入与验证范围见[装备功能](../../docs/features/equipment.md)。

状态组件参数来源：`prepare_battle_status_parameters.py` 核对 `884_156692` 解码程序集，从原组件访问器与 Info 字段声明解析重载类型和继承关系；保留 `854_156383` 战斗配置身份。原方法、字段声明、输入、生成契约与适配／验证入口的 SHA256 见[来源清单](battle-rules/status-parameters-source-manifest.json)，行为与验证范围集中见[战斗执行](../../docs/features/battle-execution.md)。

状态属性来源：`prepare_battle_status_property_reference.py` 核对当前解码程序集，提取原状态初始化、叠层更新、属性转换及共享公式方法；验证适配器和临时编译结果均保持独立命名空间。`884_156692` 原方法、`854_156383` 配置、生成器、适配和验证入口身份见[来源清单](battle-rules/status-properties-source-manifest.json)，行为与验证范围见[战斗执行](../../docs/features/battle-execution.md)。


### 当前伙伴自动结识来源

`prepare_partner_automatic_meeting.py` 核对选定 `884_156692` 的 `PlayerNpcDataWrap.CreateNpcDataByInfo`、`MsgPlayerNpc.OnMessage` 和 `NPCHandler.MainPlayer_OnAddNewNpc` 原码，并记录当前自动伙伴条件、原默认好感等级与经验、创角／读档接线和验证源码哈希。来源见 [partner-automatic-meeting-source-manifest.json](partner-automatic-meeting-source-manifest.json)，触发时机的单机决策与实际实现／验证范围仅在 [伙伴功能](../../docs/features/partners.md) 维护。未新增美术载荷，未生成完整素材快照。


### 当前伙伴好感属性界面输入

`prepare_partner_property_ui_configuration.py` 按选定 `884_156692` 读取 APK 优先的主 Luban `entry`、当前属性表及中文 WwiseBnk 语言载荷，保留实际 APK `881_156673` 身份；完整二进制消费、原构造器、包／对象／语言输入与配置输出哈希见 [属性界面来源](partner-property-ui-source-manifest.json)。原奖励比较器、实际字段调用、枚举与迁移输出见 [奖励排序来源](partner-reward-comparer-source-manifest.json)。本地服务及 Resources 引用的代码、原 prefab／配置与显式绑定哈希见 [服务绑定来源](partner-friendship-services-source-manifest.json)。功能行为与验证范围统一见 [伙伴功能](../../docs/features/partners.md)。未新增美术载荷，未生成完整素材快照。

日常入口匹配面板：`prepare_dungeon_matching_panels.py` 按当前 `884_156692`、实际 APK `881_156673` 优先及同版已核验资源，读取五个原 prefab 的匹配小窗、`SafeArea`、布局参数、目录初始状态与声音事件对象；原代码、输入、生成器与输出哈希见 [匹配面板来源](dungeon-ui/matching-panel-source-manifest.json)。`TeamMatch_Waiting_1` 复用历史 Map12 已转换 PCM，生成器实际核对当前 `ui_sfx_01` 整包 MD5／长度／SHA256 与其历史输入字节相同，并校验 WAV 输出哈希；历史音频回执保持原版本身份。本次仅新增绑定描述，不新增音频／美术载荷，未生成完整素材快照；行为与验证范围见 [游记与日常副本](../../docs/features/dungeons.md)。

公共副本路线：`record_dungeon_route_presenter.py` 将既有 `871_156598` 路线窗、房间与评级原生 prefab，以及未改写的历史 `official-bindings.json`／原来源清单登记在 `dungeon-ui/route-presenter-source-manifest.json`；当前 `884_156692` 原路线代码、已迁移的路线／房间类、公共显示器与两个调用方分别登记哈希。未新增美术或音频载荷，也未改写旧版来源身份。


### 当前伙伴赠礼动态资源与服务输入

`prepare_partner_gift_presentation_assets.py` 依照选定 `884_156692` 的关系配置、资源地址、原 Sprite RenderDataKey 与已核验原生转换缓存，按 APK 优先／同版云端资源闭合八张称谓背景及图集／Windows 贴图，并提取二十条原中文称谓。实际 APK 仍保持 `881_156673` 身份。原包、对象、网格、转换输入输出、复用载荷与 Resources 绑定哈希见 [赠礼动态资源来源](partner-gift-presentation-assets-source-manifest.json)。原赠礼提交、称谓校验、语言键、图片前缀及旅行属性门槛的源方法和本地服务适配哈希见 [赠礼服务来源](partner-gift-services-source-manifest.json)；具体行为与验证范围仅在 [伙伴功能](../../docs/features/partners.md) 维护。未生成完整素材快照。


### 当前伙伴详情属性区域输入

`prepare_partner_detail_properties_ui.py` 核对选定 `884_156692` 的详情属性控制器、属性公式、原 UI 复位和简报回调源码；`prepare_partner_detail_properties_native.py` 沿当前 `NpcDetailPanel`、共享原属性组及 Sprite／Animator 依赖恢复原生区域，按 GUID 和实际字节复用现有载荷。输入包、对象、层级、转换和输出哈希见 [详情属性来源](partner-detail-properties-native-source-manifest.json)。八条原中文文字与 Resources 显式绑定见 [资源绑定来源](partner-detail-properties-assets-source-manifest.json)。当前 `level_prop.g.bytes` 追加三类天赋属性，仍保持实际 APK `881_156673` 与所选云端 `884_156692` 各自身份，输入和输出哈希见 [属性目录来源](partner-properties-source-manifest.json)。功能与验证范围仅在 [伙伴功能](../../docs/features/partners.md) 维护。未生成完整素材快照。

### 当前伙伴动态立绘原生资源

`prepare_partner_native_portraits.py` 按 `884_156692` 当前伙伴配置核对实际 APK `881_156673` 后，解析选定云端资源的容器地址、CAB、原对象及依赖，提取 54 份开放范围立绘。原始骨骼采用既有免费链路的 MIT `SkelToJson 1.0.1` 格式读取器，保留浮点精度并验证完整消费；原 binary、图集、SkeletonDataAsset／材质字段及贴图对象均登记原字节 SHA256。`prepare_partner_portrait_assets.py` 校验同版 Windows 原生 Texture2D 转换缓存，以逐伙伴 Resources 显式绑定安装原生载荷。原始对象、APK／云端来源、读取器 wheel、工具、转换输入、运行代码和 489 个输出文件哈希见 [partner-portrait-assets-source-manifest.json](partner-portrait-assets-source-manifest.json)。原 UI 材质／shader 的验证入口核对同版已校验的 Windows 转换缓存；行为、支持范围及实际验证结果统一在[伙伴功能](../../docs/features/partners.md)维护。本批素材未纳入素材快照。

### 当前伙伴称谓输入窗口资源

`prepare_partner_input_dialog.py` 按选定 `884_156692` 恢复原 `InputDialog` 和 `MainCanvas` 默认窗口容器引用的普通遮罩；实际 APK 仍保持 `881_156673` 身份。沿原容器 PPtr、脚本身份和列表索引确认遮罩，不按名称猜测对象；共享 native 恢复器支持核对完整子树并只解除根父节点。原对象字段用于恢复空字符串，原编译曲线用于恢复空动画根路径；新资源引用通过 Resources 目录显式绑定。输入包、CAB／PathID、Windows 转换、原中文文本、原声音 Event／bank／媒体、工具、复用与输出哈希见[称谓输入来源](partner-input-dialog-source-manifest.json)。功能及实际验证范围仅在[伙伴功能](../../docs/features/partners.md)维护。本批未生成完整素材快照。

### 当前伙伴赠礼确认窗口资源

`prepare_partner_confirm_dialog.py` 按同一选定 `884_156692` 核对原 `ConfirmDialog` 的对象层级、组件字段、CAB 依赖及 Windows 原生转换载荷；实际 APK 身份仍为 `881_156673`。依据原序列化字段恢复空字符串及空集合，依据编译曲线恢复空动画根路径；相同 GUID 且字节一致的伙伴资源复用现有载荷。输入版本、包／对象身份、原中文文本、Wwise 声音、工具、资源复用及输出哈希见[赠礼确认来源](partner-confirm-dialog-source-manifest.json)。显式加载与实际行为范围在[伙伴功能](../../docs/features/partners.md)维护。本批未生成完整素材快照。

### 当前伙伴好感说明窗口与模糊遮罩资源

`prepare_partner_friendship_window.py` 按当前 `884_156692` 恢复完整 `NpcFriendshipDialog`，沿原 `MainCanvas` 默认容器的类型 3 遮罩引用恢复确切子树；实际 APK 保持 `881_156673` 身份。窗口与旧版绑定使用独立 GUID，字节相同的依赖复用既有伙伴素材。当前 APK `CustomRenderTexture/ScreenBlur` 程序单独转换为 Windows shader，保持原采样表达式；历史 shader 及材质模板身份分别记录，不混改来源。原空字段、动画根路径、窗口／遮罩原码、Wwise 声音及复用的同事件 PCM、输入／输出及显式绑定哈希见[好感窗口来源](partner-friendship-window-source-manifest.json)。行为与验证范围仅在[伙伴功能](../../docs/features/partners.md)维护。本批未生成完整素材快照。

### 当前伙伴对话名册与原生条目来源

当前 `884_156692` 的三个对话名册控制器、共享排序／红点／名称替换原码见 `partner-dialogue-list-ui-source-manifest.json`；原 `NpcPanel` 的外部 `NpcPanelDialogueItem` 引用及 66 对象子模板、原生转换缓存、资源复用、输入输出哈希与显式目录绑定见 `partner-dialogue-entry-native-source-manifest.json`。资源先核对固定 APK，再使用选定云端资源及现有 Windows 转换缓存；未混用版本，未新增素材快照。

### 当前伙伴星神分页与分组模板来源

`prepare_partner_star_god_ui.py` 校验选定 `884_156692` 的四个星神控制器及共享排序、配置、图像定位原码；输入与输出见 [星神控制器来源](partner-star-god-ui-source-manifest.json)。`prepare_partner_star_god_native.py` 沿原 `NpcPanel` 星神区域及其真实外部 `StarGodNpcItemGroupItem` 引用恢复完整层级，依组件对象身份保留跨 prefab PPtr，并闭合三个星神的地区图像、九种相遇信物原图标、中文文本、动画、原 Wwise Event 对应 PCM 及 Windows 原生资源。先核对实际 APK `881_156673`，再使用所选云端资源和已校验转换缓存；输入包／对象、工具、资源复用、输出和 Resources 显式绑定哈希见 [星神原生资源来源](partner-star-god-native-source-manifest.json)。行为与验证范围仅在[伙伴功能](../../docs/features/partners.md)维护。本批未生成完整素材快照。

星神相遇材料枚举直接取当前 `NpcHelper.GetMeetNpcItems`，由 `partner-condition-source-manifest.json` 记录原方法、生成器与运行输出；当前图标同时保留原地址、manifest GUID、Sprite 对象及 Windows 转换身份。本地状态与原页面加载绑定的输入输出哈希见 [星神本地绑定来源](partner-star-god-local-source-manifest.json)。

### 当前完整伙伴窗口来源

`prepare_partner_panel_native.py` 按选定 `884_156692` 恢复完整 `NpcPanel`、外部对话条目和实际资源依赖，复用已校验名册卡片与星神分组。先核对 APK，再使用所选云端资源与 Windows 转换缓存；原控制器常量校验、同来源动画 GUID 复用、图节点原码、状态动画支持及安装输出哈希见[完整窗口原生来源](partner-panel-native-source-manifest.json)。独立原生 Canvas 后端、显式 Resources 绑定与验证输入见[窗口运行绑定来源](partner-panel-runtime-source-manifest.json)。行为与验证范围仅在[伙伴功能](../../docs/features/partners.md)维护，本批未纳入素材快照。

公共主线地图网格提取：沿用已核实的 Map13 原共享视野代码／配置来源，不重新标记为当前 APK 版本；离线适配的 Git 前身、未修改的官方实现、公共类与程序集迁移、显式消费绑定和归一化文本 SHA256 见[公共网格来源](official-map/mainline-grid-visibility-source-manifest.json)。生成工具为 `record_mainline_grid_visibility_sources.py`，无新增美术素材；实现及验证范围见[公共地图模块](../../docs/features/mainline-map-modules.md)。

### 完整伙伴窗口宿主绑定

选定 `884_156692` 的完整 `NpcPanel` 与原 `TabBottomBarCellController` 继续由 `prepare_partner_panel_native.py` 恢复；覆盖控制器按 Unity 原生资源类型 2 绑定，不更改原动画或状态机。`partner_panel_language.py` 从两份原中文语言二进制读取完整窗口键，输入哈希及 Resources 输出记录在 `partner-panel-native-source-manifest.json`。宿主、原生外观连接与隔离验证源码哈希记录在 `partner-panel-runtime-source-manifest.json`；功能范围见 [伙伴页](../../docs/features/partners.md)。

完整窗口音效由原 `UIClickSound` 的 Wwise Event 指针追踪至所选资源事件／媒体，复用既有 Wwise 解码器保留原增益。新增 `Main_Tag.wav`（事件 680474747，媒体 69663821，48 kHz 双声道，16,800 帧，PCM SHA256 `99bdf2db7f7db2cbd4c80a13237906fe8cf86591b05d2fe3a68c9c6867785e9b`）；返回点击复用已核实的事件 3772241779 资源。包内检索、事件图、转换工具、原增益、输出及 Resources 显式绑定哈希见 `partner-panel-native-source-manifest.json`；共享好感声音适配器的原 Git 身份和接口适配记录同步到 `partner-friendship-window-source-manifest.json`。本批未生成完整素材快照。

当前伙伴通话配置来自选定 `884_156692` 的 `npc_dialogue.g` 实际载荷，按该版本生成构造器解码；原包／对象、载荷、递归消费格式源码、元数据和安装绑定 SHA256 见 `partner-dialogue-rules-source-manifest.json`。`prepare_partner_dialogue_rules.py` 从同版共享 `PlayerNpcDialogueDataWrap` 提取创建与节点状态更新方法，保留原方法哈希并记录数据库／时钟／玩家端口适配，见 `partner-conversation-rules-source-manifest.json`。本批无新增美术资源，未生成完整素材快照；行为与验证范围仅在[伙伴页](../../docs/features/partners.md)维护。

公共地图白雾来源见 `official-map/mainline-grid-fog-source-manifest.json`，提取入口为 `scripts/prepare_mainline_grid_fog.py`。当前 884_156692 的 MapRoot_11／12／13／16 官方根对象及 MapRoot_15 明确空雾层绑定、CAB 实际凭据、完整雾层 subtree 与依赖对象原始字节 SHA256、ASTC／RGBA mip 哈希、shader 程序哈希及 Unity 原生输出分别记录；当前原客户端 `CloudController2` 与历史已迁移代码的身份核对单独记录，不覆盖历史输入版本。`LocalGridVisibilityAgent` 记录当前官方完整源文件、本地生命周期适配产物及转换工具哈希；运行绑定和验证范围见[主线地图公共模块](../../docs/features/mainline-map-modules.md)及[泽之国](../../docs/maps/map-13.md)。

鲸背岛实体旋转进度图使用所选 `884_156692` AssetRipper 输出的 `Img_RoundLoad.asset` 与 `World_Atlas` 原图；`prepare_mainline_map16_entity_rendering_assets.py` 固定源 Sprite／Texture2D GUID 与输入 SHA256，保留原 Sprite 原生 YAML、网格、UV和 atlas 坐标，将完整 1024×2048 ASTC 解码 PNG 作为未压缩 2D 纹理导入。`official-map/map16-entity-rendering-assets-source-manifest.json` 记录转换输入／输出哈希。原 Sprite／图集资源及当前验证边界见[鲸背岛](../../docs/maps/map-16.md)；本批未生成完整素材快照。

公共地图网格输入来源见 `official-map/mainline-grid-visibility-data-source-manifest.json`。`prepare_mainline_grid_visibility_data.py` 从当前版本原根网格、实体放置／身体／视野与区域解锁包读取真实载荷，使用已核对程序集与已准备源码清单的生成格式解码，保留坐标、区域顺序及营地关系；记录原包对象／载荷、全部消费格式源码、输出哈希。与历史 Map13 输入分别登记，未混写旧版本清单。

各图交互配置的独立绑定见 `official-map/mainline-interaction-maps-source-manifest.json`。`prepare_mainline_interaction_maps.py` 复用原交互转换器，严格核对 Map12／13 各自 `871_156598` 原表及 EC 模板字节，记录地区、奖励预览、区域刷新、英雄结果索引与本地载入配置的输入／输出哈希；不覆盖早期共享表或 Map14 的来源记录。本批无新增美术，未生成素材快照。

Map15 遗漏配置行的恢复见 `official-map/map15-configurations-source-manifest.json`，由 `prepare_mainline_map15_configurations.py` 从所选 `884_156692` 实际官方载荷解码世界、地区、入口条件与完整实体扩展参数原行，核对全部消费构造源码及实际 750 格导航；记录历史定义的固定 Git 身份和历史输入，与当前包／对象／载荷分开。更新后的定义、载荷大小与输出 SHA256 由 `unity/Assets/Official/World/Map/15/source-manifest.json` 绑定；本批无新增美术，未生成素材快照。

`prepare_partner_conversation_conditions.py` 从同版共享条件类与 `Formula` 提取实际通话条件闭包，记录原文件／方法、生成模板、复用规则、玩家端口适配及输出 SHA256，见 `partner-conversation-conditions-source-manifest.json`。无新增美术资源；未生成完整素材快照。

`partner-conversation-local-source-manifest.json` 记录当前 `PlayerNpcDialogueData`／包装器身份、本地 JsonUtility 字段适配、已迁移规则依赖、实际存档读取与验证源码哈希；生成入口为 `record_partner_conversation_local_sources.py`。无新增美术资源，未生成完整素材快照。

`prepare_partner_conversation_world.py` 从选定 `884_156692` 的 MapInfoWrap 和实体扩展原载荷提取通话地图事实，并记录实际包／对象、字段格式源码、构造器、已确认的生产存档单位及全部本地安装绑定哈希，见 `partner-conversation-world-source-manifest.json`。未生成完整素材快照。

替身地面绑定来源见 `battle-rules/voodoo-grid-source-manifest.json`。`prepare_battle_voodoo_grid.py` 从884_156692所选官方APK读取4600移动组件与同版 `EC/entity_grid_mover` 表，使用同版格式代码解码，记录包内实际对象、原始字节、代码、生成器及C#编译绑定哈希；已绑定旧战斗目录仅作组件身份核对，版本分别记录。无新增美术载荷，未生成完整素材快照；行为和验证范围见[战斗执行页](../../docs/features/battle-execution.md)。

伙伴通讯录的原通知消费与本地存档绑定凭据见 [partner-dialogue-local-ui-source-manifest.json](partner-dialogue-local-ui-source-manifest.json)，记录选定官方客户端／共享源码、原 UI 契约、适配代码及验证输入输出哈希。

伙伴通话每日限制的原 `Player`／`TimeUtils` 方法、记录字段、配置依赖及本地存档适配哈希见 [partner-conversation-day-limits-source-manifest.json](partner-conversation-day-limits-source-manifest.json)，重建入口为 `prepare_partner_conversation_day_limits.py`。本批无新增美术，未生成素材快照。

Map15 原模型／镜头与标签数据来源见 `official-map/map15-world-view-source-manifest.json`，提取入口为 `prepare_mainline_map15_world_view.py`；分别记录当前 APK 对象字节、消费格式程序集、语言 rawfile、原地区配置、历史共享样式／位阶表及原生 prefab 输入。公共标签／环境绑定的固定代码前身、原区域条件代码及安装输出哈希由 `record_mainline_native_ground_sources.py` 写入 `official-map/mainline-native-ground-source-manifest.json`；不覆盖历史 Map14 来源清单，无新增美术载荷，未生成完整素材快照。

Map15 入口当前原生地面来源见 `official-map/map15-current-ground-source-manifest.json`，提取入口为 `prepare_mainline_map15_native_ground.py`。按所选 `884_156692` 的真实 CAB／PPtr、完整字段导出与已校验 Windows 纹理／shader 转换逐项重绑，保留原层级、瓦片 Sprite、标签和渲染参数；新增载荷独立放在 `NativeCurrent/`，不覆盖历史 `NativeTerrain/`。四份原骨骼、网格、路径、动画和 atlas 经固定哈希的 MIT 格式读取器解码，绑定项目独立原生动画绘制端；不复制或反编译动画 SDK 实现。收据分别记录原对象、脚本绑定、工具和逐文件输出哈希；本批未进入完整素材快照。

伙伴通话节点／好感等级原方法与本地事务边界的源码、字段适配、原数据及验证哈希见 [partner-conversation-transactions-source-manifest.json](partner-conversation-transactions-source-manifest.json)，记录入口为 `record_partner_conversation_transaction_sources.py`；原好感等级方法经过声明适配后的逐词核对。本批无新增美术，未生成素材快照。

伙伴原通话播放代码与完整窗口的输入版本、原代码、APK／云端对象、Windows 转换、同源资源复用、语言及显式加载绑定哈希见 [partner-dialogue-playback-ui-source-manifest.json](partner-dialogue-playback-ui-source-manifest.json)、[partner-dialogue-playback-native-source-manifest.json](partner-dialogue-playback-native-source-manifest.json)；入口为 `prepare_partner_dialogue_playback_ui.py`／`prepare_partner_dialogue_playback_native.py`。本地服务与验证源码指纹见 [partner-dialogue-playback-runtime-source-manifest.json](partner-dialogue-playback-runtime-source-manifest.json)，入口为 `record_partner_dialogue_playback_sources.py`。本批素材未纳入完整素材快照。

伙伴当前身份／职业／战力运行数据来源见 [partner-npc-runtime-source-manifest.json](partner-npc-runtime-source-manifest.json)。`prepare_partner_shared_identity.ps1`／`prepare_partner_npc_runtime.py` 记录选定 `884_156692` 的官方程序集、元数据与消费源码、原始 Luban／属性二进制、程序集名称及 Windows 文件读取适配、逐方法指令指纹、LiteDB 5.0.20 发布者包和许可证、显式加载绑定及安装 SHA256。与历史地图输入分别登记；本批未生成完整素材快照。

战斗角色移动轨迹来源见 [battle-animation/grid-motion-source-manifest.json](battle-animation/grid-motion-source-manifest.json)。`prepare_battle_grid_motion.py` 按现有规则选择的21个角色移动动作，从所选884_156692官方APK优先、同版已校验云端缓存其次解析原prefab及MyPlayableTrack／MyCurveTrack引用，分别记录对象字节、原游戏应用／共享代码、曲线及阶跃切线、原序列时间与Windows显式资源绑定哈希；历史规则的来源单独保留，无新增自绘素材，未生成完整素材快照。

鲸背岛当前定义、地面及模型／镜头／标签来源分别见 `official-map/map16-definition-source-manifest.json`、`map16-current-ground-source-manifest.json` 和 `map16-world-view-source-manifest.json`。三个提取入口按所选884_156692的真实 APK／匹配版本缓存、原格式源码、CAB／PPtr与已校验 Windows 转换记录输入及逐文件输出；公共 `GroundSource` 复用转换路径，资产 GUID 按地图隔离。原生地面公共绑定清单另记录当前 CustomWater.dll 与历史已迁移程序集的字节一致性及六份已安装水面输出校验，不声明整版等价；本批未生成完整素材快照。

伙伴通讯录头像与职业图标来源见 [partner-npc-icons-source-manifest.json](partner-npc-icons-source-manifest.json)。`prepare_partner_npc_icons.py` 记录选定 `884_156692` 的原 NPC／职业二进制表、实际 APK 优先及同版云端缓存来源、Sprite 渲染键和 CAB／对象、原生 Sprite 网格／索引及 Windows 纹理转换缓存、同 GUID 同载荷依赖复用、151 个图标地址的显式绑定与安装 SHA256；原消费代码及本地绑定源码分别登记。未生成完整素材快照。

战斗移动 Animator 来源见 [move-animators-source-manifest.json](battle-animation/move-animators-source-manifest.json)：选定884_156692的原 `MyFightSkill`／`MyFightActionSkillMove` prefab、默认 `MyPlayableSequence` 与 `MyPlayableAnimator` 节点，保留APK优先与校验云端来源、对象字段、输出哈希和未消费的非 Animator 节点；由 `scripts/prepare_battle_move_animators.py` 提取为 `Combat/move-animators`，历史技能／规则载荷保持各自原版本。

伙伴本地身份存档适配的输入及输出哈希随 [通话事务来源](partner-conversation-transactions-source-manifest.json) 登记；复用所选 884_156692 的原节点／好感等级方法与现有配置，新增适配代码、迁移验证源码及记录工具分别记录 SHA256。本批无新增美术载荷或素材快照。

地区时间公共适配来源见 [mainline-region-time-source-manifest.json](official-map/mainline-region-time-source-manifest.json)。`prepare_mainline_region_time.py`／`record_mainline_region_time_sources.py` 核对所选884_156692的原地区时间判定、日期偏移、UniTask刷新状态机和时间格式源码，记录每实例日期／语言服务绑定与安装输出哈希；当前格式源码仅此文件与历史语言来源逐字节一致，不声明外围程序集或整版等价。地图标签语言键由各图原配置及原时间格式消费路径提取，未新增替代字体或美术；本批未生成完整素材快照。
战斗移动朝向的原客户端／共享实现、保留854版本的规则字段、22份动作配置和输出哈希见 [移动朝向来源](battle-animation/movement-facing-source-manifest.json)；记录脚本为 `scripts/record_battle_movement_facing.py`，只读取原游戏应用／共享代码与既有已校验规则，不读取动画SDK实现。

战斗移动声音的选定884_156692原技能／动作序列、Wwise事件引用、APK优先与已校验云端银行、原HIRC路由／效果参数载荷、WEM和解码PCM身份见[移动声音来源](battle-animation/movement-sounds-source-manifest.json)。`scripts/prepare_battle_movement_sounds.py` 复用银行身份核验与vgmstream解码，保留未归一化、未烘焙增益的原PCM并生成Unity显式资源；规则选择载荷保留854来源，不与当前音频或871历史战斗音频混标。

战斗移动地形选择与四个原生粒子资源的选定884_156692应用／框架源码、APK优先及匹配云端来源、已核验原生转换输入、当前shader的520个Windows程序、三张贴图的全部原mip解码与逐文件输出哈希见[运行代码来源](battle-animation/ground-effects-runtime-source-manifest.json)及[资源来源](battle-animation/ground-effects-assets-source-manifest.json)。入口为 `prepare_battle_ground_effect_runtime.py`／`prepare_battle_ground_effect_assets.py`；既有水波淡出脚本仅核对所复用的原应用文件与当前源码字节一致，不声明整版等价。本批未生成完整素材快照。

战斗移动特效来源见[移动特效清单](battle-animation/movement-effects-source-manifest.json)，重建入口为 `prepare_battle_movement_effects.py`。选定884_156692应用／框架代码及21套移动技能／动作配置按APK优先与匹配云端凭据读取；记录10个原序列的可达特效子图、原DLL脚本fileID校验、复用代码逐方法核对、125项原生资产闭包、53张原贴图全部mip及2份完整Windows shader转换的输入输出哈希。导出PNG只用于方向／解码对照，实际贴图由原压缩mip解码；相邻字节的解码器舍入差异不修改输入哈希。原生转换缓存逐文件核对其来源清单，动画／轨迹／声音分别保留自己的来源，本批未生成完整素材快照。

小地图区域地面来源见 [Map10当前资源](official-map/map10-current-minimap-regions-source-manifest.json)、[Map11当前资源](official-map/map11-current-minimap-regions-source-manifest.json)、[Map12当前资源](official-map/map12-current-minimap-regions-source-manifest.json)、[Map13当前资源](official-map/map13-current-minimap-regions-source-manifest.json)、[Map14当前资源](official-map/map14-current-minimap-regions-source-manifest.json)、[Map16当前资源](official-map/map16-current-minimap-regions-source-manifest.json)与[公共绑定代码](official-map/mainline-minimap-regions-source-manifest.json)。`prepare_mainline_minimap_regions.py` 复用公共原生恢复器，记录所选884_156692的APK优先／匹配缓存、原Ground子树、地区prefab、YooAsset索引GUID及Windows转换依赖和逐文件哈希。原YooAsset GUID与转换缓存Unity GUID分别保留；仅核对两份区域管理源码与历史输入逐字节一致，不声明整版等价。历史小地图来源独立保留，本批未生成完整素材快照。

完整小地图根、离线协调组件及离线标记当前初始化的输入、输出与适配来源见[主线小地图根凭据](official-map/mainline-minimap-root-source-manifest.json)。`prepare_mainline_minimap_root.py` 从所选884源码迁移；历史871标记管理器凭据独立保留，当前凭据仅覆盖其中Init方法，重新生成历史管理器后须再运行本转换器。未生成完整素材快照。

完整原生小地图 prefab 来源见[Map10根资源](official-map/map10-current-minimap-root-source-manifest.json)、[Map11根资源](official-map/map11-current-minimap-root-source-manifest.json)、[Map12根资源](official-map/map12-current-minimap-root-source-manifest.json)、[Map13根资源](official-map/map13-current-minimap-root-source-manifest.json)、[Map14根资源](official-map/map14-current-minimap-root-source-manifest.json)、[Map16根资源](official-map/map16-current-minimap-root-source-manifest.json)。`prepare_mainline_minimap_native_root.py` 按所选884的实际APK／同版缓存、原层级与外部prefab引用恢复，记录CAB／PPtr、原Tile全部字段、Sprite身份、Windows转换、脚本逐文件等价凭据、显式绑定及公共实例工厂哈希；历史版本来源分别保留，不声明整版等价。元数据按仓库LF规范记录哈希，不改变字段或GUID。本批未生成完整素材快照，行为与验证范围见[公共模块页](../../docs/features/mainline-map-modules.md)。

主线原生小地图矩形兼容：`prepare_mainline_minimap_native_root.py` 在当前完整根、共享HUD／渲染槽／全屏页及资源点目录的恢复链中，为官方 Rectf 的 `x/y/width/height` 字段写入 Unity YAML `serializedVersion: 2`，四个原数值保持不变。各来源凭据记录该转换、原输入与重新生成的输出哈希；历史871载荷独立保留。

主线当前小地图标记：`prepare_mainline_minimap_markers.py` 从所选884版原放置字节、Luban表及刷新分组候选选择共享标记，复用完整原生prefab恢复链；资源点与标记通过 `mainline_minimap_native_sprite.py` 统一恢复原Sprite逻辑几何及有效图集UV。当前目录和历史871目录分开保存，输入CAB／对象／程序集、脚本等价依据与输出哈希记录于 `mainline-current-minimap-markers-source-manifest.json` 和各图当前资源点来源凭据。

共享标记查询数据由 `prepare_mainline_minimap_marker_catalog.py` 从所选884版原 `entity_extend_parameter`、`entity_map_ui_display` 和 `map` Luban 表提取，选取范围与当前原生标记目录一致；保留完整匹配显示行及原顺序，仅投影标记端口消费的扩展字段。原 `EntityMapUIDisplayInfoParser.Get` 与初始化正文逐一核对，原 `PostParse` 只替换全局数据入口；APK优先输入对象、构造器源码、输出JSON与本地绑定哈希见[共享查询来源](official-map/mainline-minimap-marker-catalog-source-manifest.json)，历史来源独立保留，未生成完整素材快照。

伙伴主角头像的选定884_156692原摄影棚、PlayerPortraitSettings／AppearanceConfig对象字段、三个原Sprite及Windows转换依赖见[主角头像来源](partner-player-portraits-source-manifest.json)。`prepare_partner_player_portraits.py` 记录APK优先／匹配缓存、CAB／对象身份、枚举列表恢复、显式脚本／资源绑定和逐文件哈希；当前角色与外观输入引用各自已校验来源，不混标历史头像配置，本批未生成完整素材快照。

鲸背岛交互配置来源见 [map16-interactions-source-manifest.json](official-map/map16-interactions-source-manifest.json)。`prepare_mainline_map_interactions.py` 按地图ID复用所选884_156692的定义输入与公共英雄结果索引转换器，额外解码原状态效果／周期随机奖励表；记录APK优先／匹配缓存、原构造器源码、模板来源凭据、逐文件输出哈希与工具，不混入历史871配置。本批无新增美术或完整素材快照。

鲸背岛独立地图准备条件、地图激活条件与七条剧情标识来自所选 `884_156692` APK 的 `TbMapWorldAloneInfo`、`TbConditionInfo` 和 `TbStoryInfo`。`prepare_map16_alone_configuration.py` 使用同版反编译 Luban 构造器解码，锁定原准备参数、条件116、奖励规则770／771及剧情路径／次数字段；输入对象、载荷与配置输出哈希见[独立地图配置清单](official-map/map16-alone-configuration-source-manifest.json)。`prepare_map16_story_source.py` 进一步提取七张鲸背岛原剧情图、对白／选项、中文文本与配对服务端记录；节点类型、实际 StoryGraph 对象、官方代码版本、逐文件哈希及转换结果见[鲸背岛剧情来源清单](official-map/map16-story-source-manifest.json)。该产物是官方源数据，不是可运行剧情配置，不实现服务端逻辑或 Windows 表现。

公共剧情载荷由 `prepare_unity_map13_story_runtime.py` 的官方图归一化生成，分别读取查询节点 `HandInItemId` 和执行节点 `HandleItemId`。Map13／Map16 的输入、转换器及输出哈希见[泽之国剧情运行时来源](official-map/map13-story-runtime-source-manifest.json)和[鲸背岛剧情运行时来源](official-map/map16-story-runtime-source-manifest.json)；实现与验证范围见所属地图页。

公共交付窗口从所选 `884_156692` APK优先解析原 prefab、CAB依赖、中文rawfile和 Wwise 事件；APK未包含的对象按同版索引读取已取得云端bundle。`prepare_mainline_handin_ui.py` 提取原43／19节点、101组件及19张Sprite，核验原按钮controller／clip和gray shader后复用原生序列化资产；两条声音按当前事件链、原增益解码，与已有官方PCM逐字节核对后显式复用。输入对象、版本、工具、语言、事件链及输出哈希见[窗口来源](official-map/mainline-handin-ui-source-manifest.json)；`record_unity_map14_handin_ui.py --mainline` 记录当前公共原生prefab、动画、材质与脚本绑定，见[原生绑定来源](official-map/mainline-handin-ui-bindings-source-manifest.json)。历史871版来源保持独立；实现与验证范围仅维护于[公共模块](../../docs/features/mainline-map-modules.md)。

鲸背岛现有定义与来源收据通过 `World/OfficialWorldMapAssetCatalog` 的地图16条目显式绑定；加载入口为 `LoadMap(16)`，输入输出摘要仍由[地图定义清单](official-map/map16-definition-source-manifest.json)及原 `source-manifest.json` 提供，未重新生成或替换官方数据载荷。


伙伴主角血量读取／恢复采用所选884_156692的Common.Player、FormulaHandler、TimeManager与BattleFormulaHandler，原头像血条采用同版PlayerIconMeHpSliderNormal；输入、代码绑定与依赖哈希见[主角血量来源](partner-player-health-source-manifest.json)，由 `record_partner_player_health_sources.py` 记录。本批不新增美术载荷或素材快照。

### 伙伴当前玩家资料控件

`partner-player-info-source-manifest.json` 记录所选 `884_156692` 的 `PlayerInfoUI`、`ActorTargetData`、`NpcTargetData`、代码清单和程序集哈希，以及当前运行代码、生成／验证脚本与复用依赖的哈希。`scripts/prepare_partner_player_info.py` 从该客户端源码迁移资料控件，复用既有显式服务适配；未重新提取美术，也不改写其他职业控件的历史版本来源。

### 伙伴当前主角资料投影

`partner-main-player-source-manifest.json` 记录所选 `884_156692` 的 `PlayerInfoData(Player)`、`PlayerTargetData`、`PlayerPetExt`、`Player`、继承目标及程序集输入，以及本地主角资料适配、现有稳定身份／属性／外观依赖与验证脚本的逐文件哈希。由 `scripts/record_partner_main_player_sources.py` 记录；本批没有美术提取或素材快照。

伙伴当前 UI 外观的选定 `884_156692` 应用源码、程序集及原生端点哈希见[UI 外观代码来源](partner-ui-appearance-source-manifest.json)，入口为 `prepare_partner_ui_appearance.py`。当前 APK 的材质配置对象、UI 染色 shader 与复用默认材质的各自来源、原对象字节及 Windows 显式绑定哈希见[UI 外观资源来源](partner-ui-appearance-assets-source-manifest.json)，由 `prepare_partner_ui_appearance_assets.py` 记录；历史来源不混标，本批未生成完整素材快照。 High 加载器的 APK／同版缓存对象、702 项映射、977 个纹理 GUID、原 ASTC 全部 mip、Windows 转换／同载荷占位图复用及显式加载哈希见[High 纹理来源](partner-ui-high-textures-source-manifest.json)，入口为 `prepare_partner_ui_high_textures.py`。

公共战前助战选择复用既有 Map12 原生邀请面板与伙伴条目，`scripts/record_battle_assist_selection.py` 直接读取选定884_156692的APK优先资源，对照保留871来源的逐对象提取记录；外部引用表编号变化逐项解析并核对目标原始载荷，移除的旧在线外观脚本单独排除。没有修改美术或布局载荷；当前APK／bundle／CAB、历史输入、运行prefab／meta及接线哈希见[来源清单](battle-hud/assist-selection-source-manifest.json)，实现与验证边界见[NPC邀请](../../docs/features/npc-assistance.md)。

竞技场既有871原生面板的布局输入按[原副本面板来源](dungeon-ui/prefab-binding-source-manifest.json)所记历史官方CDN地址恢复，保留原bundle MD5／SHA256／字节数并验证全部887个原生层级路径；历史布局与当前884入口数据分别记录于[入口初始化来源](dungeon-ui/matching-panel-source-manifest.json)，生成器为 `scripts/prepare_dungeon_matching_panels.py`。资源版本及运行边界见[副本](../../docs/features/dungeons.md)。

伙伴主角物理输入沿用所选 `884_156692` 当前角色数据包及其原骨骼／约束／待机通道；原包与对象身份引用当前 [scene-core 来源](battle-animation/scene-core-source-manifest.json)。[主角物理来源清单](partner-player-physics-source-manifest.json) 记录实际数据包、公开调用所用既有二进制及取样／回放文件的哈希与处理方式；可选数值取样仅写隔离目录，未复制 SDK 实现或新增素材载荷。

伙伴当前主角连续姿态沿用所选 `884_156692` 角色数据包、原皮肤集合和 UI 应用入口；[连续姿态来源](partner-player-pose-source-manifest.json) 记录原数据、依赖来源清单、当前独立原生代码及既录数值输入的哈希。`record_partner_player_pose.py` 记录来源；可选公开调用仅在隔离目录取样，JSON 参照传输显式恢复原二进制投影省略的变换约束零值，未修改原输入或复制 SDK 实现。本批未新增美术或完整素材快照。

公共小地图 HUD 使用所选 `884_156692` 的 `defaultpackage_assets_prefabs_ui_map_minimap.bundle`，输入先核对 APK 清单与实际包内对象，APK 未打包后取匹配云端实际载荷（SHA256 `76d3697a2d0cd78c990a8a487e5bdfd49b4e2791bd4d43e0df11d01706ecb981`）。完整原 `MiniMapMaskedUI.prefab` 的79个序列化对象、原脚本／字体／图形依赖、语言组配置及 `Main_Cursor_Normal` 事件解码依据见 [HUD 原代码凭据](official-map/mainline-minimap-hud-code-source-manifest.json)与[原生转换凭据](official-map/mainline-current-minimap-hud-source-manifest.json)。`prepare_mainline_minimap_native_hud.py` 保留原布局字段及 fileID，按实际 CAB/PPtr 和所选 Windows 转换哈希显式绑定；声音记录原事件、bank、媒体、工具和输出PCM。历史原公共 UI 仅在逐文件原输入哈希相等时复用，元数据文本按 LF／无行尾空格记录。

共享小地图世界数据由 `scripts/prepare_mainline_minimap_world_data.py` 解析所选884实际 `config_ec_maprootentitylite`、Luban地图／地区／小地图地区显示表及原构造器；逐项比较已安装地图定义的全部导航字段、每格内容、地图小地图身份及地区归属，仅对这些字段记录等价，保留历史定义版本身份。四条完整地区显示行显式加载到 `Resources/MainlineMiniMap/current-region-displays`；原表对象、输入、构造器、原解析代码、地图定义与输出／接线哈希见[共享世界数据凭据](official-map/mainline-minimap-world-data-source-manifest.json)，未生成完整素材快照。

公共全屏小地图使用同版原 `MiniMapFullScreenPanel`、两个筛选依赖 prefab、按钮控制器、字体、图形及 `Main_Cursor_Normal`／`Main_Tag` Wwise 事件，APK 优先与匹配缓存来源、实际 CAB/PPtr、原对象和 Windows 输出哈希见[全屏资源凭据](official-map/mainline-current-minimap-panel-source-manifest.json)。`prepare_mainline_minimap_native_panel.py` 核对原生按钮控制器全部外部动作，按原压缩常量通道的路径哈希、float32字节及循环参数验证展开曲线；原地图／标志 Luban 表和97个动态图标分别记录实际包内对象、构造器输入与显式引用。当前原 WorldCanvas 的CanvasScaler字段与原共享地图插槽渲染器分别按实际对象记录；后者由 `prepare_mainline_minimap_native_renderer.py` 恢复，见[共享渲染器凭据](official-map/mainline-current-minimap-renderer-source-manifest.json)，保留原相机及内置资源的GUID／fileID组合，并记录原创建地图绑定代码。`prepare_mainline_minimap_panel_code.py` 的当前原输入、适配后源码与工具哈希见[全屏代码凭据](official-map/mainline-minimap-panel-code-source-manifest.json)，本批未生成完整素材快照。 原根／HUD／窗口／共享渲染器凭据同时记录实时可见性适配及验证入口；`prepare_mainline_minimap_native_root.py --refresh-integration --include-shared` 先核对不变的原输入、资源输出、官方脚本和MonoScript元数据，再刷新本地适配源码哈希，不替换原始资源哈希。

伙伴当前原 UI 主角组件的运行绑定沿用已记录的 `884_156692` 外观代码、材质与 High 纹理目录；[运行绑定来源](partner-ui-player-bindings-source-manifest.json) 记录这些来源清单及本地接线、逐文件输出和验证工具的哈希。入口为 `scripts/record_partner_ui_player_bindings.py`；没有重新提取或修改原素材载荷，历史凭据保持原输入身份。

伙伴原通话头像的本地主角资料、血条与原生头像接线凭据见[实际主角控件来源](partner-local-player-widgets-source-manifest.json)，入口为 `scripts/record_partner_local_player_widgets.py`；记录既有来源依赖、适配代码及验证工具哈希，并分别核对 `PlayerIconMe`、`PlayerInfoData` 与所选 `884_156692` 原码的逐文件字节一致性，不将其他历史模块混标为当前版本。本批没有美术载荷变更或完整素材快照。

爬塔游记绑定从所选 `884_156692` 的 `TravelNotesPanel` 解码 `OldPlayEntryBtn.mEntryBtn`、旧 `DemonBattleEntry` 状态／Sprite GUID／层数引用及原语言格式；保留原 prefab 节点与隐藏状态。`WorldMapProgressCondition` 与 `Formula.Int2Float` 来源为同版官方共享代码，既有 `854_156383` 家园条件载荷单独记录；原始对象、输入／输出哈希和显式图片绑定见 [dungeon-ui/matching-panel-source-manifest.json](dungeon-ui/matching-panel-source-manifest.json)，重建入口为 `scripts/prepare_dungeon_matching_panels.py`。

伙伴整页打开时的通话事务与原通讯录／播放服务接线凭据见[窗口通话接线来源](partner-conversation-window-source-manifest.json)，入口为 `scripts/record_partner_conversation_window.py`。原通话规则、原整页和图像／声音输入沿用各自所选 `884_156692` 来源清单，本批单独记录组合代码与验证入口哈希；未修改原素材或生成完整快照。

公共战报窗口绑定：`scripts/prepare_shared_battle_report.py` 从当前所选 `884_156692`（APK `881_156673`）的 `defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportpanel.bundle` 解码面板、玩家／技能视图、五个页签和 PopupWindow 的9个组件，核对135个原节点及1,080项 Transform／RectTransform 字段与既有原生 prefab 一致。新增 `Combat/BattleReport/report-window-bindings.json` 显式绑定及稳定 meta，保留原生图形、字体和 prefab 的既有历史来源，不重标版本或替换素材。遮罩复用既有854原 WorldCanvas 载荷，单独保留其源对象与原 RawImage UV、文档输入及资源引用，未混记为884资产。原对象来源、官方客户端与 SkillUserType 代码、原生 prefab、生成器、运行适配及输出哈希见[窗口来源清单](battle-hud/report-window-source-manifest.json)。功能与验证范围见[战斗执行](../../docs/features/battle-execution.md)。

伙伴原提示图执行器使用所选 `884_156692` 官方客户端的节点管理器／仓库、图资源缓存、UI 状态控制器、系统生命周期和屏蔽策略管理器；原文件与适配输出、既有节点／原图依赖及工具哈希见[提示图执行器来源](partner-tipdot-runtime-source-manifest.json)。`scripts/prepare_partner_tipdot_runtime.py` 直接转换游戏自有源码，将 DI、玩家／配置／资源及订阅替换为明确宿主端口，恢复原脏节点异步循环；未复制第三方 SDK 实现，未修改原素材或生成完整快照。

伙伴提示图目录复用已验证的所选 `884_156692` APK Luban 表提取缓存及当前原整页来源清单中的三张原图；[提示图目录来源](partner-tipdot-catalog-source-manifest.json) 记录 `tip_dot.g.bytes` 载荷凭据、所选官方构造器、既有原图哈希与新目录输出。入口为 `scripts/prepare_partner_tipdot_catalog.py`；原 376 条表记录和原图分别保留输入身份，生成 Unity 原生配置引用，不改原图、节点或 prefab，未生成完整素材快照。

伙伴天赋提示的图结构与计算使用所选 `884_156692` 的游戏自有 `NpcTalentTreeNpcMapInfoParser`、`PlayerNpcTalentTreeExt`、节点／好感条件与客户端 `NpcTalentTreeUIUtils` 方法；既有 `partner-rules.json` 保留原天赋、条件、物品及设置表输入。来源、方法摘要、适配输出、模板和验证工具哈希见[天赋计算来源](partner-talent-source-manifest.json)，入口为 `scripts/prepare_partner_talent_rules.py`。仅将静态配置及玩家读取替换为显式端口；未复制第三方实现，未修改素材或生成完整快照。

全员战报绑定：`scripts/prepare_shared_battle_report.py` 继续解码所选884_156692的 `fightendallplayerreportpanel.bundle`（167节点、1,336项布局）与 `fightendreportsplit.bundle`，核对原玩家行／分隔线资源引用和原标题绑定，生成独立 `Combat/BattleReport/all-player-report-window-bindings.json` 与稳定 meta。四项分组中文从对应 WwiseBnk 原语言 rawfile 提取，先检查所选 APK，按相同索引的实际文件及长度／MD5验证缓存；语言来源与旧854报告语言载荷分开记录。个人绑定清单同步记录扩展后的公共运行适配及生成器哈希；没有替换既有 prefab、美术或字体。原对象、代码、APK／缓存语言、布局核对、生成及运行输出见[全员窗口来源清单](battle-hud/all-player-report-window-source-manifest.json)，功能与验证范围维护于[战斗执行](../../docs/features/battle-execution.md)。

共享小地图地区计时接线复用所选 `884_156692` 的 `Common.MapRegionInfo`、地区限制刷新助手、原 UniTask 倒计时状态机与 `ShowRemainTimeInfo`；原输入、时钟适配、每实例地区行及语言绑定、验证入口和工具哈希见[地区时间凭据](official-map/mainline-region-time-source-manifest.json)。入口为 `scripts/prepare_mainline_region_time.py` 和 `scripts/record_mainline_region_time_sources.py`。原生小地图根、HUD、全屏页及共享渲染器的原输出经原提取工具重建核对，载荷内容保持一致，所属凭据更新脚本及接线哈希；未生成完整素材快照。

共享小地图本地响应继续复用所选 `884_156692` 的原离线标记初始化和协调器；用户确认的筛选／最新状态消费适配由[完整根来源](official-map/mainline-minimap-root-source-manifest.json)另记，原响应 DTO 的本地策略字段由[遮罩与响应来源](official-map/map13-minimap-masks-code-source-manifest.json)记录。历史871输入缓存按既有 SHA256 从相同源码恢复，保留原版本身份。单分线 HUD 与本地数据适配、验证入口哈希写入各原生小地图根及共享 HUD／全屏页／渲染器的接线凭据；原 prefab 与图像没有替换，未生成完整素材快照。

伙伴天赋条件的本地玩家读取沿用既有 `NpcTalentTrees`、好感与库存存档字段，原读取依据为所选 `884_156692` 的 `Player`、`PlayerSingleNpcTalentTreeData`、`PlayerItemManagerExt` 与 `ItemType`。本地只读接线、既有原计算依赖及验证工具哈希见[天赋玩家读取来源](partner-talent-player-source-manifest.json)，入口为 `scripts/record_partner_talent_player.py`；不新增存档格式、资源载荷或完整素材快照。

`scripts/prepare_battle_result_report.py` 从所选 `884_156692` 原包读取两种活动结算的 `GameEndPanelBase` 四个按钮引用，以及战报玩家条目的 `PlayerInfoUI`／`HeadCharacterPortrait` 状态和头像引用；逐项核对既有原生 prefab 的层级与布局，生成 `Combat/BattleReport/result-report-bindings.json` 和 `player-identity-bindings.json`。运行时复用既有官方图形、玩家头像拍摄与伙伴图标载荷，不新增或替换美术。原对象、bundle、代码、历史载荷凭据、输出和消费代码哈希见 [结算战报绑定来源](../../assets/reference/battle-hud/result-report-source-manifest.json)。

伙伴档案迁移复用所选 `884_156692` 的原档案控制器、章节标签、滚动助手、好感条件与配置解析方法，按 APK 后云端缓存的顺序恢复原 `NpcDetailPanel` 档案子树及依赖；所选原语言、奖励物品图像和既有原 `ItemIconDefault` 经实际对象、索引、版本及哈希校验后生成 Resources 显式绑定。[原生恢复凭据](partner-profile-native-source-manifest.json)、[语言与奖励图像凭据](partner-profile-assets-source-manifest.json) 分别记录方法、包／对象、转换工具和输入输出哈希；[本地接线凭据](partner-profile-source-manifest.json) 记录已确认的档案领取事务适配与验证工具。入口为 `scripts/prepare_partner_profile_ui.py`、`prepare_partner_profile_native.py`、`prepare_partner_profile_assets.py` 和 `record_partner_profile.py`；没有替代美术，未生成完整素材快照。

伙伴提示的当前玩家接线复用所选 `884_156692` 的原 `Player` 通知参数、`MsgPlayerNpc/MsgPlayerNpcRemove`、物品枚举与已迁入提示节点读取端口，输入、当前存档读取／事件适配、程序集绑定及验证工具哈希见[提示玩家来源](partner-tip-player-source-manifest.json)。入口为 `scripts/record_partner_tip_player.py`；复用既有伙伴配置与档案、赠礼、天赋存档，不新增资源载荷或完整素材快照。

原技能附件跟随使用所选 `884_156692` 的游戏自有 `AttachmentFollower`、`SkeletonAttachmentActiveTracker`、`MyPlayableFollowSpinePart` 及既有 `ApplyByBone` 应用代码；程序集、原方法摘要、原生接口适配、生成及验证工具哈希见[代码凭据](battle-animation/spine-follower-runtime-source-manifest.json)。`scripts/prepare_battle_spine_follower_assets.py` 先核验 APK 数字状态对象，再按完整原生导出图恢复24207／24227及CAB闭包，复用全部原mip纹理与Windows shader转换；原对象、原生输入、转换哈希、显式脚本／Resources绑定和80个输出见[素材凭据](battle-animation/spine-follower-assets-source-manifest.json)。运行时只消费随包原生资源，没有替代美术；本次未生成完整素材快照。

`scripts/prepare_activity_battle_native.py` 使用选定 `884_156692` 的官方 APK／资源清单、程序集字段描述和既有 `native-export-all` 转换凭据，逐字节核对五个活动入口／结算／难度 prefab 及其 `.meta` 输入；原 CAB／PathID、原组件字段、原生对象对应和中间输出哈希见[活动战斗原生界面来源](../../assets/reference/daily-challenges/native-ui-source-manifest.json)。中间数据写入会话验证目录，绑定后的可运行资产另按相应素材接入流程记录。

伙伴条件订阅迁移复用所选 `884_156692` 的 `Common.ConditionHandler`、`ConditionInfoParser.Expand/Distinct` 和当前实际条件类的 `Type` 声明，沿用既有完整原条件枚举与伙伴配置；原方法、条件类型、订阅结构、存档事件边界和验证工具的哈希见[条件订阅来源](partner-condition-handler-source-manifest.json)，入口为 `scripts/prepare_partner_condition_handler.py`。不新增素材载荷或完整素材快照。

技能实体表现源码取自所选 `884_156692` 解码程序集及客户端 `MyFightSkill`、`MyFightSkillAgent`、`Common.IFightGetUsePlayable`；解码程序集哈希先与code-manifest核对，再比较完整类／接口。`Common.FightSkillComponent`、`FightSkillComponentInfo` 和 `FightSkillAgentComponent` 只作为抽象消费契约来源记录，未将其抽象端口称为完整共享实现。源文件、原脚本标识、规范化整类摘要、逐项适配、输出和工具哈希见[技能实体表现代码凭据](battle-animation/skill-view-runtime-source-manifest.json)，生成入口为 `scripts/prepare_battle_skill_view_runtime.py`。不新增美术载荷。

完整施法图采用所选`884_156692`原APK／云端依赖、已核验原生导出及Windows贴图／shader转换缓存；输入、原GameObject对象、完整prefab／动作载体／SDK事件引用、压缩贴图各mip、解码像素、转换器和绑定文件哈希集中于[施法图凭据](battle-animation/cast-assets-source-manifest.json)。声音从官方`Skill_10032`事件的原bank路由定位媒体248412158，以已记录哈希的vgmstream解码为未归一化48kHz双声道PCM；bank、事件动作、媒体来源及PCM哈希均在同一凭据。入口为`scripts/prepare_battle_cast_assets.py`，复用共享完整图转换器；SDK引用只重绑已迁入的字段桥，图内节点及资源引用完整保留。导入器末尾空白和换行统一为LF，保留GUID与字段值。

原生技能轨迹使用所选 `884_156692` 的游戏自有 `MyTrack`、`MyCurveTrack`、`AnimationTrack`、`ParabolicTrack` 和 `MyPlayableTrack` 完整源码；程序集、整类与方法摘要、原 `MyContainer.ToLocalPos` 坐标方法、显式接口适配及工具哈希见[代码凭据](battle-animation/track-runtime-source-manifest.json)，入口为 `scripts/prepare_battle_track_runtime.py`。`prepare_battle_track_assets.py` 按已核验的APK对象、完整原生导出图及CAB引用恢复10951_2／10584_move／11171三图，保留原曲线和序列参数；子图独立纹理按导出meta中的原bundle精确加载并继续验证原对象及哈希。输出meta仅规范化行尾空白，保留全部GUID与字段值。28项原生资源、10张全mip纹理、完整Windows shader、显式Resources／脚本绑定及输出哈希见[素材凭据](battle-animation/track-assets-source-manifest.json)。既有五类素材凭据同步记录共享转换器身份；没有替代美术，本批未生成完整素材快照。

技能Spine残影捕获迁移取所选 `884_156692` 的 `MyPlayableAfterimage`、`AfterimageData` 与 `MeshRendererZSortComparer`；原码、输出及其RTHandle scope适配哈希见[残影代码凭据](battle-animation/skill-afterimage-runtime-source-manifest.json)。离线Battle Playable scope持有RT池并在销毁时释放；完整prefab图资源和生产调用接线尚未完成，范围见[技能](../../docs/features/skills.md)。

技能战斗相机演出行为取所选 `884_156692` 的 `MyPlayableFightCameraPerformance`；本地以显式宿主端口承接原相机切换和输入封锁，源码、接口、scope适配和 meta 哈希见[相机演出来源清单](battle-animation/skill-camera-performance-runtime-source-manifest.json)。生产战斗的相机／输入宿主及技能图仍未接线。

技能角色组合皮肤 Playable 使用所选 `884_156692` 的 `MyPlayableAppearanceCombineSkin` 与原 `AppearanceCombineSkin` 操作契约；源文件、输出和 Unity meta 哈希见[组合皮肤来源清单](battle-animation/skill-appearance-skin-runtime-source-manifest.json)，实跑范围见[技能](../../docs/features/skills.md)。

技能角色母体替换复用所选 `884_156692` 的 `ApplyOverrideToMaster` 与 `MyFightRoleCharacter.Component.MasterId` 查找规则；本地由显式 `IBattleMasterEntityResolver` 适配缺失的离线实体容器，并保留原 `UseSourceDataToOverride`／`OverrideSource` 分支。源码、resolver、scope、Unity meta 与 Editor Play Mode 验收哈希见[母体覆盖来源清单](battle-animation/skill-master-override-runtime-source-manifest.json)，生产场景 resolver 的接入与验证范围见[战斗执行](../../docs/features/battle-execution.md)，原代码迁移范围见[技能](../../docs/features/skills.md)。
技能动作 Playable 使用所选 `884_156692` 的 `MyPlayableAnimation`、`MyPlayableDefault`、`MyPlayableRenderSortLayer`、`MyPlayableLight` 和 `MyPlayableLight2D`；本地保留原 Animation 播放、Transform 持续时间、Renderer 排序及 Light／Light2D 强度曲线字段，来源、输出与 Unity meta 哈希见[动作 Playable 来源清单](battle-animation/skill-animation-playable-runtime-source-manifest.json)。
技能翻转触发器使用所选 `884_156692` 的 `MyPlayableFixFlipXTrigger` 和 `MyGridTransform`；本地在现有 EntityLifecycle 程序集中保留 source/target 选择与 `SetScaleByFlipX` 调用，来源、输出与 Unity meta 哈希见[翻转触发器来源清单](battle-animation/skill-trigger-playable-runtime-source-manifest.json)。
技能效果淡出触发序列使用所选 `884_156692` 的 `MyPlayableFadeEffectSequence`，调用既有原 `MyEffectFadeOut`；原数组顺序、起始时间、持续时间及重触发取消保持不变，UniTask 逐帧状态机适配为 Unity Coroutine。输入、输出与 meta 哈希见[触发序列来源清单](battle-animation/skill-trigger-fade-sequence-source-manifest.json)。

技能相机震动使用所选 `884_156692` 的 `MyPlayableCameraShake`，保留原 Cinemachine Impulse Source 定位、默认偏移、广播调用和持续时间；程序集显式引用项目自带 Cinemachine。官方源码、输出、Unity meta、程序集与 Play Mode 验收哈希见[相机震动代码凭据](battle-animation/skill-camera-shake-runtime-source-manifest.json)。


伙伴提示配置服务的接线复用所选 `884_156692` 的 NPC、档案、好感、礼物与天赋解析器及提示节点端口，原码、既有配置／规则依赖、本地接线和验证工具的输入输出哈希见[配置服务来源](partner-tip-services-source-manifest.json)。入口为 `scripts/record_partner_tip_services.py`；历史人物载荷保留原 `854_156383` 身份，当前伙伴配置保持所选版本，不新增美术或完整素材快照。

活动战斗难度选择条：`scripts/prepare_activity_battle_selector_native.py` 从所选884原包与已验证原生转换恢复 `ActivityDomainBossNumCell` 和详情页 `mNumScrollView` 原子树，复用现有官方脚本、Windows 贴图／shader／Sprite／Animator 与解码音频；按官方 CN／zh_CN 字体配置核对 FZY4K TTF 长度和 MD5，并显式恢复离线源字体绑定。`activity-selector-runtime-source-manifest.json` 记录三项客户端类与迁移代码哈希，`activity-selector-native-source-manifest.json` 记录原包身份、资源闭包、字体配置、转换输入、安装输出和显式绑定；两份清单位于 `assets/reference/daily-challenges/`。功能范围与验证结论集中维护于 [圣兽试炼与双影幻境](../../docs/features/daily-challenges.md)。

伙伴馈赠规则与本地领取迁移复用所选 `884_156692` 的原普通／星神好感任务、成就键、`AchievementInfo`、`PlayerAchievementRecordDataWrap`、`Player` 领取计算与当前完整配置表；输入、选用方法、生成规则、本地事务、存档字段和验证工具哈希见[馈赠规则与事务来源](partner-achievement-rules-source-manifest.json)，入口为 `scripts/prepare_partner_achievements.py`。不新增美术载荷或完整素材快照；单机决策与功能范围见[伙伴](../../docs/features/partners.md)。

伙伴原生馈赠页迁移使用所选 `884_156692` 原 `NpcDetailPanel.mAchievementPanel` 实际引用、成就面板原码、奖励配置、原语言文件及 APK／同版本官方缓存；通过既有原生转换缓存恢复 Windows prefab、图像、字体、动画、材质及脚本绑定。重建入口为 `scripts/prepare_partner_achievement_ui.py`、`scripts/prepare_partner_achievement_native.py`、`scripts/prepare_partner_achievement_assets.py`，源码、对象、输入输出、引用选择和复用差异哈希见[原生馈赠页来源](partner-achievement-ui-source-manifest.json)。本轮未生成素材快照，功能范围与验证结论维护于[伙伴](../../docs/features/partners.md)。

伙伴详情原生对白使用所选 `884_156692` 的 `NpcTalkUI`、`NpcCustomSizeController`、原详情 prefab 对白引用及 `npc_talk` 原表，复用工程已有官方打字机、字体与 Windows 资源转换。`scripts/prepare_partner_detail_talk.py` 按 APK Legacy Animation 的未压缩实际路径恢复转换为空值的根曲线路径；原子树、三段动画、气泡 Sprite、脚本／资源绑定、输入输出哈希和工具身份集中于[详情对白来源](partner-detail-talk-source-manifest.json)。本批未生成完整素材快照；功能范围与验证结论维护于[伙伴](../../docs/features/partners.md)。

原生战斗结算操作区：`scripts/prepare_battle_result_actions_native.py` 从所选884的 APK／配套云端原结果包恢复两种 `BtnGroup`、原祖先几何及 Canvas／GraphicRaycaster，原包、转换输入、Sprite／纹理闭包、音频与安装载荷哈希见 `daily-challenges/battle-result-actions-native-source-manifest.json`；原 `GameEndPanelBase`／`EndPanelAnimEventReceiver` 代码及适配哈希见 `daily-challenges/battle-result-actions-runtime-source-manifest.json`。已迁移854关卡载荷的 Unity 序列化适配以前一 Git 载荷为精确输入，前后哈希见 `daily-challenges/battle-stages-serialization-source-manifest.json`。所选884 `BattleFormulaHandler.CalcSummonMonsterInheritProp` 缺行分支的代码输入及移植哈希见 `battle-rules/summon-missing-config-source-manifest.json`，不改动既有战斗配置版本。

技能分身图脚本复用所选 `884_156692` 解码程序集、客户端 `MyPlayableSpineClone`／`PlayableSpinePlayerCloneInit`、`AppearanceSystem.Runtime` 外观类、`ApplyOnStopCheckSourceDie`／`ECFightRoleCharacterView` 及 Framework 空标记 `PreviewTool`。原码、解码程序集、迁移组件、接口适配、meta 与生成工具哈希见[分身代码来源](battle-animation/spine-clone-runtime-source-manifest.json)，入口为 `scripts/prepare_battle_spine_clone_runtime_receipt.py`。`prepare_battle_spine_clone_sources.py` 读取同版本 13100 原包内五项分身目标的实际 MeshRenderer／SkeletonAnimation 字段，原 bundle 哈希、对象哈希、逐字段内容与共享配置输出哈希见 [分身配置来源](battle-animation/spine-clone-settings-source-manifest.json)；不新增替代美术或完整素材快照。

技能演出图原生资产：`scripts/prepare_battle_skill_graph_assets.py` 按所选 `884_156692` 的官方导出 prefab、代码 ID 和 APK／同版本云端对象恢复完整引用闭包，复用逐 mip／像素核验的 Windows 纹理及完整 shader 转换缓存。原始输入、实体字段绑定、应用脚本映射、纹理／shader 对象哈希、转换输入及安装输出哈希见 [技能图资产来源](battle-animation/skill-native-graph-assets-source-manifest.json)；原实体审计入口为 `scripts/inspect_battle_skill_graph_bindings.py`，运行时清单为 `Combat/skill-native-graphs`。本批没有生成完整素材快照；功能与验证范围维护于 [技能](../../docs/features/skills.md)。

角色技能部件显隐配置取所选 `884_156692` 原始 `ECViewDriver` 的 SerializeReference 注册表，经原 `mECViews` 有序引用读取409份 `ECFightRoleCharacterView.mDelayVisibilityParts`；原角色对象、引用的 GameObject／Transform、bundle版本／哈希、严格解码器、官方代码和公共配置输出哈希见[角色显隐来源](battle-scenes/character-visibility-source-manifest.json)。入口为 `scripts/prepare_battle_character_visibility.py`，未从省略托管注册表的原生YAML推测默认值，未生成新美术或完整素材快照；运行绑定范围见[战斗执行](../../docs/features/battle-execution.md)。

角色阴影子树取所选 `884_156692` 的19份实际角色 prefab，逐项核对原 `ShadowBoneFollower` 的父节点变换、对象／组件、Sprite与纹理绑定；原始导出、APK对象、应用跟随代码、独立空间适配、完整mip转换缓存和输出SHA256见[角色阴影来源](battle-scenes/character-shadow-source-manifest.json)。入口为 `scripts/prepare_battle_character_shadows.py`，沿用原Sprite几何与解码纹理，不生成替代美术或完整素材快照；运行接入与验证范围见[战斗执行](../../docs/features/battle-execution.md)。

伙伴应用动画控制器绑定：`scripts/prepare_partner_native_battle_animation_bindings.py` 从所选 `884_156692` APK／对应云端实体包读取34份原 `AnimationController` 与 Animator 引用，保留68个原组件对象、组件模式、原应用代码及安装载荷的身份；输入输出 SHA256、包来源和显式绑定见[动画绑定来源](partner-native-animation-bindings-source-manifest.json)。没有新增美术或生成完整素材快照；功能与验证范围见[伙伴助战](../../docs/features/npc-assistance.md)。
