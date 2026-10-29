# 官方来源与提取入口

[文档索引](../../docs/README.md) · [素材管理](../../docs/asset-management.md)

本页只记录官方输入与来源凭据。各模块的原始实现、转换／绑定细节、当前实现和验证边界见所属功能／地图页；逐文件记录以模块来源清单为准。

## 输入身份与版本

| 输入 | 固定身份／位置 |
| --- | --- |
| 官方 APK | `.tools/source-packages/zjcs_M509085.apk`；manifest `854_156383`；1,024,203,578 字节 |
| APK SHA256 | `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9` |
| 官方下载 | [zjcs_M509085.apk](https://zhangjcsdlf.leiting.com/full/zjcs_M509085.apk)，入口来自[官网](https://zjcs.leiting.com/)配置接口 `https://zjcs.leiting.com/client/gameV2/home` |
| 资源版本选择 | 根目录 `official-source.json`；当前五包 `871_156598`，资源目录 `202610211330871` |
| 当前云端输入 | `.tools/source-packages/cloud/runtime/202610211330871/`；包含原清单、`inventory.json` 与 `receipts.jsonl` |
| 官方更新入口 | [110001_Main.dis](https://zhangjcsupdate.leiting.com/zhangjcs/version/release/110001_Main.dis) |
| 官方程序集／反编译 | `.tools/source-packages/official-code/managed/decoded/`、`client-source/`、`common-source/` 等 |
| 分包 | Boot、Launch、HybridCLR、DefaultPackage、WwiseBnk |

APK 与热更新版本分别记录。原输入、转换产物及其前后哈希不统一改名为当前版本。历史版本的重建须显式恢复对应输入，或证明当前缓存文件与原清单大小、MD5及真实下载凭据 SHA256 一致；不能替换变化文件或修改预期哈希。当前保留范围由 `official-source.json` 标识，既有 Unity 成品与用户存档独立保留；本地保留不等于仓库外备份。

## 凭据位置

| 内容 | 入口 |
| --- | --- |
| 原始包与逐文件下载来源 | [runtime-cloud-source-manifest.json](official-map/runtime-cloud-source-manifest.json) 及缓存中的 `receipts.jsonl` |
| 当前来源切换与转换链 | `assets/reference/official-map/current-*-source-manifest.json`、`assets/reference/character-creation/current-*-source-manifest.json` |
| 客户端／共享代码来源 | [官方程序集与反编译目录](../../.tools/source-packages/official-code/) 与各模块源码来源清单 |
| 模块资源来源 | 本目录各模块的 `source-manifest.json`、`*-source-manifest.json` 及所属功能／地图页中的入口 |
| 森之国女神珍宝详情原 prefab 与 Unity 转换 | [转换清单](official-map/map11-treasure-detail-source-manifest.json)：APK `854_156383` bundle／PathID／输入哈希、转换工具哈希、Unity prefab 输出哈希 |
| Windows Player 官方应用图标 | [来源清单](application-icon-source-manifest.json)：APK 内 `res/mipmap-xxxhdpi/app_icon.png` 原始 PNG 与 SHA256，绑定至 Windows Standalone PlayerSettings |
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

技能附属窗口由固定 APK `854_156383` 的原 prefab、组件字段、字体与动画提取，运行时显式绑定 270 张官方 UI 精灵；窗口、原代码适配、字体／shader、工具及输入输出 SHA256 见 `skill-ui/unity-icon-source-manifest.json`。列表附件恢复原配置的类型 4／8／10／13／14、原 prefab 和脚本类绑定；培养窗口恢复原状态字段、材料计数、星级与跨品质通知，培养配色／原代码与横向虚拟滚动组件的输入输出 SHA256 记录在 auxiliary_inputs.cultivation_code、cultivation_colors、cultivation_scroll，来源、输入及输出哈希同见该清单；结算附加皮肤来自原 `AppearanceConfig`，网格、骨骼与图集见 `character-creation/native-geometry-source-manifest.json`，纹理显式绑定 `ProfessionUI/character-textures`。技能包内缺失的 18 个原技能图标按 `Assets/Image/ItemIcon/Skill/skill_<id>` 解析同版本 Sprite，地址、bundle、对象哈希及转换后字节哈希记录在 `auxiliary_sprites`。七套方案取原 `GameSettings.SkillPlanMaxCount` 与中文键 `skill_plan_default_name_1..7`；原方案 Scrollbar 对象 `-4354697964205878964` 的 SHA256 为 `53972465f66e438fa93ab2217ff461a62d6b579c6ffbcea23b42e78b90e219fc`。状态与运行验证见[技能功能页](../../docs/features/skills.md)，新增载荷未声明进入完整素材快照。技能方案输入框恢复 `TMP_InputField` 的完整 580 字节及 APK 分支字段 `m_EncodingType`；名称规则直接迁移官方 `Common.CJK`，公式、错误码、客户端调用与元数据哈希记录在技能载荷的 `auxiliary_inputs.plan_name_rules`、`plan_input`、`plan_input_schema`。全部技能模板与 SystemMsgPanel 的原 TMP 文字、顶点渐变预设及 TMPEffect 原组件字节哈希纳入 text_style_inputs；安装遮罩、取消按钮、四槽高亮和祝福文字从原组件字段绑定，不另画遮罩或提示。

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
