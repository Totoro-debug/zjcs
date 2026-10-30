# 共用界面、文字与资源运行

[总索引](../README.md)

状态：未完成

## 功能简介

本页维护 Unity 竖屏场景、官方 UI prefab、控件、动画、字体、材质、粒子、音频及本地资源加载的共享要求；各玩法差异在所属页面维护。

## 原始官方实现

官方 prefab、组件和调用代码规定层级、锚点、布局、字体、遮罩、材质、输入、动画、粒子与声音；动态资源按原 CAB／PathID 或明确资源身份绑定。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

此前界面与主城交互修复阶段接入 56 个 UI prefab、1,167 项资源和 892 项图标映射。`prepare_official_panels.py` 同时解析 Unity Animation / SimpleAnimation、Animator 压缩标量曲线，以及 StateSpriteAssetProxyConfig 的 GUID 引用。动画保留来源、关键帧和 Hermite 切线；不支持的绑定如实保留，不将本地布局适配称为完整原作界面。

除上述 Python / Node 依赖，场景和界面转换使用 .NET 10、ILSpyCmd 11.1.0.9782 与 vgmstream r2117。工具用于解码和静态检查，不是素材来源。已有缓存与工具直接复用，首次安装缺少的工具时执行：

ILSpyCmd 默认位置为 `.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll`，可用 `--ilspy` 指定；音频解码器为 `.tools/vgmstream/vgmstream-cli.exe`。先完成上一节男性角色提取与转换，再按以下顺序重建。`--dotnet` 替换为实际 .NET 路径，`--decompile-client` 额外导出审计源。

`official_apk.py` 按需读取 bundle 与 CAB 依赖，不要求重新下载或预先解包整个 APK。

以下是当前文件的实际状态；来源约定生效不等于这些文件已经完成替换。后续优先使用上述 APK 及官方下载资源替换，完成后补充实际版本、bundle、对象名和哈希；确实缺失时的自绘补充按上述约定登记。

完整主线验收及原实体战斗仍待完成 |
| 原场景背景音乐，| 自绘图标脚本已移出源目录；主城使用原始 Sprite 图标 | 先核对 APK 与官方下载资源；确实缺失时才可自绘并登记来源 |
| 原 背景音乐 | 自定义主城脚本及其合成音乐已移出源目录；实际播放 APK 的 `Home_BGM_01` | 点击音效、地图音乐与剧情配音已接入；部分容器缺项见音频清单 |
| 幻兽、遗物、藏品与角色动画 | 原图、模型、技能／遗物图鉴及地图／剧情角色动画已接入 | 部分特殊表现、完整实体战斗与个别缺失图像继续按各模块清单追踪 |。

历史图片、网页和视频的真实来源及早期处理记录归档于 [LEGACY_SOURCES.md](../../assets/reference/LEGACY_SOURCES.md)，只用于旧资源追溯和已有流程证据。`scripts/prepare_city_art.py`、`scripts/prepare_reference_assets.py` 及会生成重绘背景的 `scripts/prepare_character_art.py` 已移出源目录；当前角色重建使用 `prepare_original_character.py`。

[#6](https://github.com/Totoro-debug/zjcs/issues/6) 完成男性角色原始资源接入；[#7](https://github.com/Totoro-debug/zjcs/issues/7) 清理已替代旧稿、研究中间物和临时 EXE。当前角色转换只生成实际使用的 30 张贴图和遮罩，来源图片不进入 Windows 导出。

完整 APK、当前转换依赖、来源记录保留。当时保留的旧头像与重绘创角背景已实际替换并删除。旧主城玩法执行逻辑已移除； / 仅保留旧存档结构和兼容读取所需内容。

从固定 APK 的解码 GameFramework.Runtime.dll 反编译 MyPlayable、MyPlayableSequence、MyPlayableCenter、MyPlayableApplyObj、MyPlayableTrigger 和 MyPlayableConfig，缓存原文位于 .tools/source-packages/official-code/gameframework-animation-source/。结合原 AnimationController、StateMergeList、MyPlayableAnimator、MyPlayableOverrideAnimator、MyPlayableAnimationControllerPause 核对冻结写入、初始化缓冲、前置状态限制、覆盖先后次序、淡出及停止恢复。prepare_battle_animation.py 额外导出真实 AnimationController 配置、覆盖片段时长／循环／采样率及原 MyPlayable 淡出参数；来源与哈希保存在 battle-animation/source-manifest.json，运行目录仍为 battle-animation/catalog.json。

现阶段这项授权不等于连续骨骼播放已经接入。

recover_official_native.py 仅读取固定 zjcs_M509085.apk 中 arm64-v8a/libil2cpp.so 的原字节（346091520 字节，SHA256 0c3cef25cfa9f079da6344825aae5a52e5eb025e0cc9ece737ec79a94cdfa544），与完整 APK ZIP 条目逐字节哈希对应；metadata 使用同 APK 的 assets/bin/Data/Managed/Metadata/global-metadata.dat（45312608 字节，SHA256 58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4）。工具在开发环境 Unicorn 2.1.4 中执行原 AArch64 初始化、解密及解压指令，按原构造顺序和重定位表恢复静态代码／数据，在 0x206f73c8 的 Android 初始化前边界停止；未知导入停止，不修改原可执行指令。应用原加载器 0x206f6968 的 R_AARCH64_RELATIVE 规则，外部导入不冒充已恢复实现。缓存为 .tools/source-packages/commerce-native/libil2cpp-restored.bin，317298920 字节，SHA256 8e49906e085ba3819c4b47fd3bd12dcdd594ff0fcc75d418a87ac35a4566305b；该文件是带原保护头的虚拟地址映像，不是可运行 ELF，也不进入游戏资源包。

read_official_native.py 沿固定 v31 metadata 的 image／type／method token 及恢复后的 CodeGenModule.methodPointers 定位真实方法，输出原指令及可核对的托管调用名称。Unity.TextMeshPro.dll 模块位于 0x11efc650，1694 个方法指针；TMProWarp.Refresh 为 metadata_index 296528／token 0x0600000d／VA 0x1121e07c，TMPro.TMPEffect.DoSetup 为 296788／0x06000111／0x1122f780，其材质回调为 296792／0x06000115／0x1123083c。此处来源指真实原生方法，与此前取得的另一组件 TMProWarpEx 区分；运行时迁移和验证结果只记对应功能／地图文档。

`prepare_pet_materials.py` 从固定 `871_156598` 的 APK／校验匹配 CDN 读取 `Effect/Noise_Mask_Pro` 编译后 GLES 程序、15 项幻兽主动技能的 134 个材质及 168 个 ParticleSystem／Renderer。13 种实际关键字变体输出至 `shaders/pet-particles/`；原 CustomDataModule、顶点流、材质默认值、采样参数与光栅状态输出至 `assets/official/pet-materials/data.json`，逐层解码原 ASTC mip 链，并为线性目标生成保留 alpha 的 RGBA16F 数据。固定版本资源与适用的已登记 `864_156496` 同 CAB 依赖分别追溯，原 bundle／URL、对象、原始及输出字节 SHA256 见 [pet-materials/source-manifest.json](../../assets/reference/pet-materials/source-manifest.json)。没有用 `_BloomFactor` 推导新规则；该字段不参与本 shader 的实际编译程序。

`prepare_battle_post.py` 校验固定 APK 的 Launch `launch_assets_settings_renderpipeline.bundle` 与 `launch_unityshaders.bundle`，按原 metadata 字段序和严格字节长度解析管线、Renderer2D、PostProcessData、229 个战斗主／UI 相机、Volume 及两份 Bloom Profile。原 shader 的提亮、横／纵模糊、向上合成、LDR LUT 与最终合成运算输出至 `shaders/battle-post/`；原配置位于 `assets/official/battle-post/data.json`。Unity 官方 Graphics `2022.3/staging` 代码仅作序列化结构及引擎数学参照，执行程序直接取自 APK，并核对恢复后的固定原生 `Bloom..ctor`、`PostProcessPass.SetupBloom`、`ColorGradingLutPass.ExecutePass` 和 `TextureCurve.GetTexture`；输入、原生镜像／方法和参照文件 SHA256 见 [battle-post/source-manifest.json](../../assets/reference/battle-post/source-manifest.json)。

Compatibility Canvas 不自动解码 sRGB 采样，`prepare_battle_linear_textures.py` 因此继续解析原 Texture2D／SpriteAtlas 的 `m_ColorSpace`，仅对 sRGB 颜色 PNG 做过滤前解码，原线性数据／噪声／遮罩保持数值；生成无损压缩的半浮点像素，不改透明度或绘制替代图。原 PNG 输入及转换输出 SHA256 见 [battle-linear/source-manifest.json](../../assets/reference/battle-linear/source-manifest.json)，原图来源继续由 battle-spine、battle-pets 和 official-map 各原清单追溯。原 Spine 图集另建线性页面别名，保持原区域、尺寸和 UV，避免污染界面使用的原颜色页面；原染色在已线性的输入上执行，避免重复转换。战斗用浮点视口组合场景、角色与粒子，再以原后处理显示；HUD 沿原相机保持独立，仍使用 Compatibility 渲染器。原相机与材质接入范围见[幻兽](pets.md)。

仅为 6 个原无 mipmap 绑定的专用纹理传入双线性过滤与各向异性 5，实际采样由 GPU 完成，不修改全局等级。手写适配源码为 `scripts/native/pet_texture_sampler.c`，输入及 DLL 输出哈希见 [pet-texture-sampler/source-manifest.json](../../assets/reference/pet-texture-sampler/source-manifest.json)。 对 37 个原网格发射器保留归一化的原 Color32 顶点色、三轴尺寸、枢轴、速度、原相机／局部／速度朝向、ZXY 初始旋转、父节点三维矩阵与最终三角形绕序，投影后对 50200 的原 `qiliu04` 执行原正面剔除；三维顶点继续传入原材质运算。枢轴依据固定 APK `libunity.so` 的 `BakeMesh` 原生几何路径（`0x1114f30`、`0x1124518`），使用原网格联合边界与带负 Z 的枢轴偏移，镜像 SHA256 及地址登记在 `pet-materials/source-manifest.json` 的 `native_mesh_geometry`。该范围没有替换网格或自行绘制贴图。

## 完整还原部分

共享 `WorldPopupWindow` 已补齐原 `Canvas` 读取与 `SetCanvasSortingOrder` 的“覆盖排序→设置顺序→发送通知”执行顺序，原 `SortingOrderSetter` 随弹窗通知更新子画布／排序组。通知复用原非泛型 GAction 的监听列表实现，保留先移除首个重复监听、跳过已移除监听和延后本次调用中新增监听的行为。相关规则、Unity 编译／全工程验证，以及山之国原队伍窗口两种尺寸的实际点击与显隐回归通过；新邀请页的完整表现仍见[山之国](../maps/map-12.md)。源码与来源见[原邀请模式与共享控件](../../assets/reference/official-map/map12-team-invite-panels-source-manifest.json)。

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

离线版没有远端玩家身份。已确认的233个玩家槽使用当前本地角色预览，不生成线上玩家头像或身份；Unity 须按原 prefab 的挂载位置恢复这项数据绑定。

Unity 须恢复官方组件、资源、材质与渲染参数；无法恢复的部分按实际官方证据另行列出。

## 尚未实现

- 官方prefab层级、布局组、字体、遮罩、材质、原生粒子、Spine动画／混合、音频及本地显式加载；逐项验证Windows兼容性。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。

## 代码与资源入口

`scripts/prepare_character_appearance.py`（来源工具标识）

[ui-source-manifest.json](../../assets/reference/character-appearance/ui-source-manifest.json)、[test_home_spine_clipping.mjs](../../scripts/test_home_spine_clipping.mjs)、[ui-spines-source-manifest.json](../../assets/reference/official-map/ui-spines-source-manifest.json)

[prepare_official_panels.py](../../scripts/prepare_official_panels.py)、[UIParticleBloom.cs](../../.tools/source-packages/official-code/client-source/UIParticleBloom.cs)、[UIBloomRequirer.cs](../../.tools/source-packages/official-code/client-source/UIBloomRequirer.cs)

`scripts/prepare_official_ui_materials.py`（来源工具标识）、[source-manifest.json](../../assets/reference/official-city/source-manifest.json)、`scripts/verify_official_assets.py`（来源工具标识）

`scripts/official_particle_codec.py`（来源工具标识）
