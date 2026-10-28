# Unity 素材管理

[返回文档索引](README.md)

本页记录 Unity 官方素材的来源、转换、快照恢复与日常验收。工程入口为 [`unity/`](../unity/)；模块完成范围见[工程与启动流程](features/unity-project.md)。

## 目录与版本管理

- `unity/Assets/Game/` 保存纳入 Git 的 Unity 场景、C#、shader、配置及稳定 `.meta`。
- `unity/Assets/Official/` 保存独立管理的官方素材载荷，不在 Git 中；不得向该目录写入手写代码。
- Unity 的 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/` 与 IDE 生成文件不提交、不进入素材快照。
- `.tools/` 保存 APK、CDN 缓存、提取工具、开发依赖和素材快照；不提交且不进入 Windows 导出。
- `assets/official/` 保存独立管理的官方配置转换结果；战斗碑表位于 `assets/official/battle-stele.json`，由 `prepare_battle_stele.py` 生成，供地图转换脚本读取。该目录不纳入 Git，素材备份以实际生成的快照为准。
- `assets/reference/` 只保存来源记录和必要参照。运行时素材必须由 Unity 工程打包并随游戏交付。
- 原始 AssetBundle 与编译 shader 不默认视为 Windows 可用资源；须恢复、转换、绑定并验证目标平台表现。

## 验证临时产物与空间回收

测试编译输出、隔离 Unity 工程及其 `Library`、临时 Windows Player、日志、截图和测试存档统一写入仓库 `output/validation/<模块>-<GUID>/`（本机为 `D:/Desktop/zjcs/output/validation/`）；`.tools/` 继续保存官方输入、工具、复用的转换缓存与素材快照。正式游戏仍发布到 `D:/Desktop/杖剑传说/`。这项调整不改变游戏功能的完成状态。

PowerShell 统一入口为 [`validation_workspace.ps1`](../scripts/validation_workspace.ps1)：

```powershell
. ./scripts/validation_workspace.ps1
$work = New-ZjcsValidationWorkspace -Name map14
$env:ZJCS_VALIDATION_DIR = $work
$env:ZJCS_REPOSITORY_ROOT = (Get-Location).Path
try {
    # 临时工程放在 $work/unity，Player 放在 $work/player。
    # 点源仓库 scripts/unity_internal_display.ps1 后，
    # 用 Start-UnityOnInternalDisplay 启动窗口并等待退出。
    # 在此完成模拟输入、实际查看截图，并将结论写回所属功能／地图页。
} finally {
    Remove-Item Env:ZJCS_VALIDATION_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:ZJCS_REPOSITORY_ROOT -ErrorAction SilentlyContinue
    Remove-ZjcsValidationWorkspace -Path $work
}
```

同一 PowerShell 调用内完成的验证可使用 `Invoke-ZjcsValidation -Name map14 -ScriptBlock { param($work) ... }`，自动设置并恢复以上环境变量，在成功或异常退出时清理。外部程序必须等待结束并显式检查退出码；不要在回调中使用 `exit`，也不要在进程仍运行时清理。跨工具调用需要保留截图时，使用 `New` 创建并在核对画面、写回结论后使用 `Remove`。清理工具只接受本仓库标记的单个会话目录，拒绝 `output/validation/` 根目录、仓库目录、目录链接、其他存活会话及仍被 Editor／Player 命令行使用的目录；不会全局扫描删除旧会话。进程崩溃或文件锁可能留下目录，停止占用后按明确路径重试。

Python 使用 [`validation_workspace.py`](../scripts/validation_workspace.py) 的 `temporary_validation_directory(prefix)`，退出上下文时自动删除；优先使用 `ZJCS_VALIDATION_DIR`，未设置时在仓库 `output/validation/` 下新建独立目录。职业成就、技能成就、副本 UI 路径测试已采用此入口；职业奖励、音频字段、提示点探针只接受此临时范围内的隔离 Unity 工程，不再强制写入 `.tools`。

历史 Editor 验证代码仍有硬编码 `.tools` 输出及相对仓库定位。后续运行这些入口前必须适配本次输出参数／环境变量与实际仓库路径，不可直接复制旧命令继续生成 `.tools` 验证工程。`output/` 内的临时工程同样必须使用内置屏启动工具。输出目录不是审批豁免：本机已实际验证本次工具的创建及删除，其他会话若仍被审批策略拒绝，应报告具体操作和原因，不改用其他删除方式绕过。

空间核对采用不跟随 junction 的文件逻辑大小，`.tools` 合计约 **140.84 GiB**。以下旧验证缓存可以在对应 Unity 退出后重建，不属于官方输入或用户存档：

| 可重建缓存 | 逻辑大小 |
| --- | ---: |
| `.tools/skills-ui-local-validation/unity/Library/` | 14.56 GiB |
| `.tools/profession-ui-validation/unity/Library/` | 8.22 GiB |
| `.tools/map14-handin-validation/unity/Library/` | 8.22 GiB |
| `.tools/map13-ui-validation/unity/Library/` | 4.40 GiB |
| 合计 | **35.40 GiB** |

四个隔离工程整体约68.60 GiB；其中 `Assets` 是验证副本，但须先确认独有修复、探针及绑定已经同步，才能清理整个工程。部分素材与正式工程使用硬链接，删除副本不能按文件大小等量计算释放空间。上述表格同样不是已释放空间的声明；本次仅识别旧目录，不批量删除其他会话产物。

`.tools/official-android-probe/x86_64-30_r16.zip`（1.34 GiB）与 `emulator-windows_x64-16428233.zip`（0.42 GiB）是安装归档；已存在对应 revision 16 系统镜像及 build 16428233 模拟器安装，通常运行不再需要这两份 ZIP，删除后离线重装需重新取得归档。`.tools/assetripper-1.3.14/temp/` 约2.25 GiB 中仍有脚本读取的原生导出输入，不可仅凭 `temp` 名称整目录清理。

必须保留当前APK、CDN缓存、反编译代码、来源凭据、按哈希保存的历史转换输入和当前素材快照。`.tools/asset-packs/zjcs-assets-871_156598-9e78e198ee78b575.zip` 约17.17 GiB，是 `asset-pack/lock.json` 指定的备份，不当作临时构建删除。模拟器 `avd/zjcs-official-probe.avd/` 已明确保留官方完整下载数据，也不是普通测试残留；其 `userdata-qemu.img` 逻辑16 GiB、实际分配约0.14 GiB，不能声称删除可释放16 GiB。来源保留依据见 [SOURCES.md](../assets/reference/SOURCES.md)。

本次目录工具已通过真实成功／失败清理、环境变量恢复、越界及 junction 拒绝检查；三个迁移后的规则测试已执行通过。此范围不涉及游戏载荷、UI 或绑定，不计为新的 Play Mode／Windows 发布验证。

## 官方来源与转换

当前默认输入由根目录 `official-source.json` 选择：保留的 APK 为 `854_156383`，唯一保留的云端缓存为 `871_156598`（资源目录 `202610211330871`）。按所选清单优先读取 APK 内同哈希文件，再读该版本已校验的 CDN 缓存；保留真实版本、URL、bundle / 对象名及输入输出哈希。`854_156383` 的云端缓存及 `864_156496` 的185个同名补充包已按用户要求移入 Windows 回收站，历史来源记录保留，不能将新版文件改名冒充旧版。

`activate_official_source.py` 校验后更新原始提取视图。当前 `extracted/`、`Boot/`、`Launch/`、`HybridCLR/`、`WwiseBnk/` 五个目录全部为 `871_156598`，合计20,999项；原始清单、APK内2,440项以及提取文件的大小、MD5、来源SHA256均已逐项核验，云端18,559项也全部通过校验。旧版提取视图和索引 `.tools/source-packages/history/854_156383/`、旧骨骼对照缓存 `current/854_156383/` 一并移入回收站。用户已明确此次只清理缓存及旧提取副本，保留现有 Unity 转换成品；本次未改动游戏载荷、绑定或存档，不将原始提取核验视为新的 Play Mode 或 Windows Player 验证。来源及现状以 [SOURCES.md](../assets/reference/SOURCES.md) 和各模块来源清单为准。

后续提取和依赖准备使用当前版本；读取API使用 `Apk.current()` 或 `Apk(manifest=None)`。既有 `Apk()` 仍保留历史版本语义，不能把它视为新版入口；旧云端及旧对照缓存已不在工作区，依赖它们的历史重建／两版比较须先迁移读取入口或显式恢复对应原始输入，不能修改预期哈希绕过检查。`official-source.json` 中旧版配置仅保留版本与哈希凭据，并标记 `cloud_cache_retained: false`。

清理后以当前UI入口和地图10小地图检查CAB依赖，279个bundle来源及序列化文件全部通过。`python -B scripts/check_unity_project.py` 已执行，但共享工作区有21个Unity文件缺少稳定 `.meta`，涉及世界探索C#文件及地图11图像，因此未通过；这些文件由其他正在进行的修改引入，本次未修改其载荷或元数据，不能将缓存核验通过写成项目静态检查通过。

角色已接入素材的新版转换使用 `convert_official_skeletons.py`、`refresh_current_character_meshes.py` 和 `refresh_current_native_prefabs.py`：独立读取官方骨骼及拆分动画，先核对骨骼、约束、影响采样顶点的动画通道和附件几何，再更新创角／人物网格包及 92 个男体原生 prefab 的三角形索引。未变化的采样顶点与两条实际原生动画保留；这不代表新增动作已经接入。新版原始二进制和独立解码结果、按哈希保存的旧网格包与旧prefab仍保存在 `.tools/source-packages/`，旧版骨骼对照目录已移入回收站；转换输入输出哈希见 `assets/reference/character-creation/current-mesh-source-manifest.json`、`current-native-prefabs-source-manifest.json`。男女模型重新导出保留原 `.meta` 与 GUID。

`verify_current_native_prefab_bindings.py` 另外核对原生 prefab 的网格名称、顶点布局、三角形与逐通道数值；92 个男体变体共 654 个网格通过核对，其中 51 个仅有不超过 `1e-6` 的浮点舍入差异。再次生成导致文件编号变化时，保留前次凭据及已核验的中间输出哈希，由 `current_conversion_state.py` 和 `sync_current_output_inventories.py` 沿转换链核验实际绑定；不将其他生成工具的动画输出归入本转换器。

该角色资源替换已通过 Editor 导入／编译和创角 Play Mode 流程；实际查看了男女体型及染色界面截图。交互使用 Unity EventSystem 按坐标命中进行模拟，覆盖外观组、染色、命名、草稿恢复、存档及初始武器，不占用系统鼠标。此结果仅覆盖已接入的角色资源和上述流程，其他模块的新版替换与验证分别记录。

女体骨骼和全部 451 个动画经独立解码后与原版完全一致，男体发生变化的三角形索引按当前输入重建；等价性凭据包含两版原始对象及解码结果哈希。`verify_current_audio_sources.py` 完整解析新版万圣节 PCK、HIRC 和 EventBankMapping：9 段媒体及 117 个既有控制对象不变，新增 8 个对象和 2 条事件路由，既有 8,737 条路由不变；已接入的 6 份音频输出逐一核验后保留，详见 `assets/reference/official-map/current-audio-source-manifest.json`。

新版表格先按当前客户端构造器完整解码 450 张表，再通过 `prepare_current_table_payloads.py`／`activate_current_table_payloads.py` 更新 23 份已有载荷；投影保留现有本地字段。`prepare_current_combat_payloads.py` 使用两版生成的 formatter 对比并更新 6 份已接入战斗配置；`refresh_current_material_payloads.py` 更新 743 份载荷内实际绑定的材质属性，其中 Unity 直接使用 2 份。家园原生资产重新导出，保留 `.meta` 和 GUID；29 份层级／布局数据、15 份中文载荷、11 张 Sprite 及 3 份相关元数据分别转换并保留原文件。地图 10 当前表重建结果与现有接入数据完全一致，来源凭据在 `assets/reference/official-map/current-*-source-manifest.json`。这组来源转换不表示各功能的未实现部分已经完成。

当前运行入口中的 `Commerce/commerce-runtime.json`、`Home/rules.json` 与 `Home/scene.json` 同步保存本轮官方配置、文案及命运星象网格更新；共享文件仅纳入本轮来源转换差异，保留其他模块已有字段。

转换工具复跑会核对同一路径的后续转换哈希链，已激活的结果不会再次按旧基线覆盖；无法对应的工作区变化会停止写入。其他会话新增的官方表先核对当前完整表内容，保留其实际载荷并登记来源。`sync_current_output_inventories.py` 只同步输出位置的哈希，历史输入仍保留真实版本；处理时实际使用的旧工具版本也按 SHA256 保存在 `.tools/source-packages/history/converted/`。

按需提取时复用 `scripts/official_apk.py` 的 `Apk` 读取器及 CAB 依赖解析。提取命令先读 APK，再读已校验且有来源凭据的缓存；音频使用 `WwiseBnk` 分包流程。完整操作见 [`assets/reference/SOURCES.md`](../assets/reference/SOURCES.md)。

Unity 优先恢复官方 prefab、场景层级、骨骼、动画、材质和引用。只有在确有需要时才转换格式或适配 Windows。新增载荷时，同步更新模块来源清单、实际输入输出哈希、Unity `.meta` 和显式加载绑定；下载或复制原始 bundle 不代表完成迁移。

导入 AssetBundle 前须核对来源清单、文件哈希、序列化 Unity 版本、BuildTarget 和 CAB 外部依赖。`AssetBundle.LoadFromFile` 失败或报序列化头不匹配时，属于资源导入错误；即使 Unity 编辑器已启动并通过项目编译，也不能据此判断编辑器缺失。不要反复安装编辑器或 Hub 来处理此类错误；先恢复依赖闭包并用兼容流程转换为 Unity 原生资产，再验证目标平台和运行表现。Editor 导入工具应保留原始异常和 bundle 路径，提供版本、目标平台及 CAB 依赖的诊断方向。

## Spine 角色动画迁移决策

源骨骼解码使用固定 SHA256 的 MIT `SkelToJson 1.0.1` 格式读取器，保留 float32、隐式附件名称、IK setup mix 及皮肤引用的骨骼／IK／变换／路径／物理约束成员；非空引用逐项检查定义范围。龙之国 NPC 中型骨骼及 833 段拆分动画已验证完整消费，合计 834 段动画。`scripts/convert_official_skeletons.py --model-bundle … --skeleton-name …` 可复建该源缓存；这属于数据解码，原生播放与约束求解仍须逐项实现和验证，当前没有运行时完成结论。

用户已确认：当前没有 Spine 正式授权，角色 Spine 4.2 内容采用无需额外购买 Spine 授权的迁移路线。继续使用官方贴图、图集、骨骼、网格、权重、换装附件和动画数据，独立实现数据转换，以 Unity 原生资产及 2D Animation 为优先承载方式，按实际差异补齐 C# 播放和换装适配。该决策先落实到男女角色预览、创角与正式换装，并供共享角色显示调用方沿用。

这是已确认的技术适配方向，尚未证明现有 Spine 4.2 数据可完整转换，也不代表 Unity 2D Animation 能直接导入 Spine 文件。Unity 包版本须与本项目 `2022.3.57f1c2` 匹配，安装、导入、脚本绑定和实际运行均待实现与验证；不得仅凭安装依赖或生成转换文件标记完成。功能状态分别维护在[角色创建与正式换装](features/character-creation.md)和[人物属性、经验、位阶与衣柜](features/character.md)。

实施与验证要求：

1. 先核对官方角色数据、配置及调用关系，按真实使用范围恢复骨骼变换、插槽层级、附件切换、图集裁剪／旋转、网格权重／变形、约束、动画曲线／混合及事件；保留原动作时序、染色、换装组合和界面锚点。转换代码与必要适配独立编写，不复制、翻译、改名或反编译 Spine runtime 来构造所谓免费替代。
2. 同时考虑开发转换工具与游戏运行时的依赖。历史 `@esotericsoftware/spine-core@4.2.110` 也是 Spine runtime，不能把“只用于离线转换”或“npm 可下载”当作已有免费授权；旧脚本和缓存保留作历史追溯，新增免费迁移链须明确排除未获授权的 runtime 依赖。实际替换完成前，不声称旧依赖已移除或所有历史产物已重建。
3. 用男女角色和实际外观组合在 Unity 中逐项核对待机、所需动作、动画切换、染色、换装和动画事件；接入共享显示后检查对应调用方。输入输出哈希、转换方式及绑定仍按模块来源清单登记，兼容缺项和运行结论写在所属功能页。
4. 保持原角色表现和交互为目标。本次批准不包括自绘、重新设计动作、静态图／逐帧降级或改用其他动画系统；出现无法保持的表现差异时，先继续查找官方数据与适配方法，仍无法解决再说明具体缺项并请求处理意见。角色流程可继续推进，不再以缺少 Spine Unity runtime 为由重复询问购买决策。

依赖与许可依据：[Unity 2022.3 的 2D Animation 说明](https://docs.unity3d.com/cn/2022.3/Manual/Quickstart2DSetup.html)、[Spine Editor / runtime 许可](https://esotericsoftware.com/spine-editor-license)。这些链接用于说明工具能力与许可边界，不作为本项目转换兼容性或功能已完成的证据。

## Android 资源迁移到 Windows

本流程适用于所有地图、家园、角色、装备、技能、UI、特效和音频模块。默认路径是**已校验 Android 输入 → 恢复 Unity 原生资产 → 恢复官方脚本和依赖 → Windows 导入、绑定与运行验证**。先按本节判断所处阶段，再处理所属模块；不必重装 Hub 或全量处理其他模块。

Unity Editor 能尝试加载其他平台的 Bundle，但其中的平台专用格式可能无法读取或渲染；Windows Player 需要适用于 Windows 的资源。Editor 内加载成功也不能证明成品可用。依据：[Unity 的平台说明](https://docs.unity.cn/2023.2/Documentation/Manual/AssetBundles-Preparing.html)、[跨平台说明](https://activation.unity3d.com/blog/engine-platform/unity-asset-bundles-tips-pitfalls)。本项目 `2022.3.57f1` 原包在 `2022.3.57f1c2` 编辑器中的直接加载曾报序列化版本不匹配；该实测不能推导出所有同系列版本都兼容或都不兼容。

### 1. 定位模块和核对环境

先查所属功能／地图页、官方调用代码及配置，确定实际 prefab／场景／对象路径和动态选择器，再查 bundle 索引。仓库存在 `.codegraph/` 时先用 CodeGraph 定位代码。例如以下问题中的模块名、符号和资源名应换成本次范围：

```powershell
codegraph explore "OfficialHomeAssetTools ImportPrefab HomeRoot FurnitureBedDialog"
rg -n 'map_11|furniture|目标资源名' .tools/source-packages/cloud-research/bundle-inventory.json
& .\scripts\unity.ps1 info
```

编辑器路径从 [`unity-tools.json`](../unity-tools.json) 读取，当前为 `D:/Tool/Unity/Editor/Unity.exe`、`2022.3.57f1c2`。Hub 可选，离线转换不要求安装 Android Build Support；只有需要构建／运行 Android 对照程序时才单独考虑对应模块。Windows Build Support 用于 Windows 构建，不能修复 Android 包内的 shader 或缺失 CAB。项目当前的 [`Packages/manifest.json`](../unity/Packages/manifest.json) 不代表 URP、UGUI、TMP、Wwise 等依赖已经恢复，须按模块核对原程序集和组件版本；角色 Spine 内容按[原生动画迁移决策](#spine-角色动画迁移决策)转换与适配，不要求恢复官方 Spine runtime。

必要时按需提取当前版本文件或复核完整缓存：

```powershell
python -B scripts/fetch_official_assets.py --extract 'prefabs_map_map_11(_minimap)?\.bundle$'
python -B scripts/fetch_official_runtime_assets.py --verify-cache 202610211330871
```

提取主包不自动收集全部依赖；未命中名称、`Not packaged or cached`、哈希失败各有不同含义。不要以“索引中存在”代替实际文件核验，也不要把新版包改名填入原版本缓存。

### 2. 一键检查并准备 CAB 依赖闭包

统一入口为 [`prepare_unity_bundle_inputs.py`](../scripts/prepare_unity_bundle_inputs.py)。使用能加载 `.tools/character-art-python` 内 UnityPy 1.25.4 的 Python；本机该工具目录为 Python 3.12 的扩展模块，Python ABI 不匹配时应换用兼容解释器，安装方法见 [SOURCES.md](../assets/reference/SOURCES.md)。脚本自动加载该工具目录，不需修改系统 Python 环境。

仅检查时不生成转换目录：

```powershell
python -B scripts/prepare_unity_bundle_inputs.py --bundle defaultpackage_assets_prefabs_map_map_10_minimap.bundle
```

准备一个模块的多个主包时重复 `--bundle`；以下是其他地图的命令模板，名称须先与清单核对：

```powershell
$mapId = 11
python -B scripts/prepare_unity_bundle_inputs.py `
  --bundle "defaultpackage_assets_prefabs_map_map_$mapId.bundle" `
  --bundle "defaultpackage_assets_prefabs_map_map_$($mapId)_minimap.bundle" `
  --stage-dir "output/android-map-$mapId-inputs"
if ($LASTEXITCODE -ne 0) { throw '先处理缺包或校验错误，停止后续转换' }
```

同一工具也适用于 UI，例如：

```powershell
python -B scripts/prepare_unity_bundle_inputs.py `
  --bundle defaultpackage_assets_prefabs_ui_furniture.bundle `
  --stage-dir output/android-home-ui-inputs
```

脚本复用 [`official_apk.py`](../scripts/official_apk.py) 的 `Apk`、`canonical_bundle_name` 和 [`official_cloud_cache.py`](../scripts/official_cloud_cache.py) 的 `CloudCache`：验证固定 APK SHA256，按原清单核对 bundle 大小和 MD5，核对 CDN 来源凭据及 SHA256，递归解析序列化文件的外部引用，并从固定 APK 加载所需 Unity 内置资源。默认显示主包的 Unity 版本、BuildTarget 和外部引用数量；`--verbose` 显示各级依赖。

退出码 `0` 表示输入校验和序列化文件级依赖闭包完成，`1` 表示失败。指定 `--stage-dir` 时，只接受仓库 `output/` 内尚不存在的子目录；成功后输出解码后的 Bundle／内置文件和 `source-manifest.json`。该清单记录真实 APK/CDN 来源、请求的主包、所有输入的原始大小／MD5／SHA256、解码方式、解码后哈希、CAB、Unity 版本、平台及依赖边。原包缓存不改动，工具不联网、不改 Unity 工程、不自动混入新版文件；已有目标目录拒绝覆盖。缺 CAB 时打印 `MISSING: 引用方 -> 外部路径` 并停止，不生成可误认为完整输入的目录。

这是**转换输入准备工具**，不是所有资源的一键 Windows 转换器。闭包按整个 bundle 收集，可能包括目标对象未使用的依赖；脚本不会自行删除或忽略这些引用。确需缩小范围时，从目标对象的 PPtr、原调用和动态加载关系建立对象级闭包，并将每项取舍写入所属模块的转换工具及来源清单，不能直接忽略 `MISSING`。

依赖检查使用当前 `871_156598` 索引、原始缓存及提取目录，依赖名为 CAB 时核对当前包实际包含的 CAB。旧 [`missing-resource-classification.json`](../assets/reference/official-map/missing-resource-classification.json) 及 `CounterpartAssets` 仅保留历史对应关系和实现，不能继续读取已移除的 `864_156496` 同名包缓存。需要继续迁移该类资源时从当前完整缓存按原对象、字段、依赖和平台重新核验，单独记录真实输入哈希；不能改名或改哈希冒充旧文件。找不到或无法判断兼容时按“缺失与不确定内容”报告检索范围并询问处理，继续不依赖该缺项的工作。

### 3. 按资产类型恢复 Unity 原生文件

| 输入类型 | 恢复方法 | 必须保留与核对 |
| --- | --- | --- |
| 配置、TextAsset、Luban、EC／MessagePack | 优先复用 `official_tables.py`、`official_ec_codec.py`、`official_map_codec.py` 及模块解码器，输出 JSON／bytes；参考 `prepare_unity_map10_data.py`、`prepare_unity_combat_catalog.py` | 真实表行、字段、ID、顺序、公式、调用绑定及输入输出哈希 |
| Prefab、场景、Mesh、材质、动画、粒子、Timeline | 将完整输入集交给 AssetRipper 恢复 Unity 可编辑资产，或使用模块专用 UnityPy 转换器逐对象恢复 | 层级、Transform、RectTransform、组件、网格、骨骼、曲线、粒子模块、材质属性、对象身份及相互引用 |
| Texture、Sprite、图集、字体 | 解码原贴图／字体，保留或恢复 TextureImporter、Sprite 与图集参数 | 色彩空间、透明度、尺寸、filter/wrap、mipmap、PPU、pivot、border、裁剪、旋转、图集 UV 和字体材质 |
| Shader、渲染管线及渲染功能 | 查官方 shader 源码、shader 名称和属性、URP 及 Renderer 配置；以对应源码在目标平台重编译 | Pass、关键字、混合、裁剪、队列、stencil、深度、光照和 renderer feature；Android 编译数据不直接当作 Windows shader |
| 音频、Wwise bank | 沿原事件→bank→媒体映射恢复本地音频；Wwise 路线须核对相应 Windows 运行库和 bank 平台，必要时依据原媒体转换为 Unity 可用音频 | 原事件、播放时机、循环、响度、声道、采样率、混音及包来源；Android 原生库不能用于 Windows |
| MonoBehaviour、第三方组件、原生插件 | 按官方源码／反编译实现恢复 C# 类和插件依赖，再按已确认范围适配离线及 Windows | 原字段与序列化绑定、程序集／类身份、调用顺序、事件、功能行为；不能用空类或 schema 占位当作完成 |

已有解码 schema 入口 [`official_unity_schema.py`](../scripts/official_unity_schema.py) 的 `ScriptSchemas.read` 及 `unity_schema_types.cs` 用于读取官方序列化字段，不是运行时插件或官方脚本的替代。源码检索目录为 `.tools/source-packages/official-code/` 下的 `client-source/`、`common-source/`、`framework-source/`、`managed/decoded/` 等；URP、UGUI 等参照也在该目录，逐项确认所需依赖后才加入 Unity Packages／程序集。

本机 AssetRipper 位于 `.tools/assetripper-1.3.14/AssetRipper.GUI.Free.exe`。已核对该版本 CLI 只有 GUI 服务启动参数，没有通用的输入／导出 CLI 参数；不要编造 `--input`、`--output` 或把 `--headless` 当成自动转换。可先运行：

```powershell
& .\.tools\assetripper-1.3.14\AssetRipper.GUI.Free.exe --help
Start-Process -FilePath .\.tools\assetripper-1.3.14\AssetRipper.GUI.Free.exe -ArgumentList @('--log', 'false') -WindowStyle Hidden
```

在其浏览器界面加载本模块准备好的整个输入目录，按真实源 Unity 版本解析，导出到本次专用的 `output/<模块>-native/`。修改导出设置时记录版本、设置和影响；具体按钮／设置以本机实际界面为准。[AssetRipper 官方项目](https://github.com/AssetRipper/AssetRipper) 提供原生资产恢复能力，但恢复质量须逐类型核对。本机已有日志显示默认 `ShaderExportMode: Dummy`；这类占位 shader 不能进入已验收资产。导出脚本同样可能只是反编译或占位结果，必须核对官方实现。若版本提供本地 HTTP 自动化，先核对该版本实际路由和请求格式，再把有效调用固化进模块转换工具，不复用未验证的端点。

从导出结果的目标对象出发收集所需原生资产闭包，保持其 `.meta`、GUID、fileID 和依赖对应；不要只复制根 prefab，也不要直接搬入整个导出工程的 `ProjectSettings`、`Packages` 或脚本。目标工程中已有同 GUID／同名共享资源时，先核对来源与内容，复用或显式重映射，不能覆盖其他模块的素材。导出缺脚本、dummy shader、丢失 Sprite／材质／动画引用时回到对应类型恢复，不能通过删组件、换通用材质或猜坐标取得表面上的成功。

如果在兼容 Editor 中临时使用 `AssetBundle.LoadFromFile` 恢复资产，须先装配并保持全部依赖包，再加载目标对象；Unity 不会自动加载这些依赖，见 [2022.3 依赖说明](https://docs.unity3d.com/2022.3/Documentation/Manual/AssetBundles-Dependencies.html)。保存 prefab 前还须将其引用的 Mesh、Material、Texture 等持久化为项目资产并重绑引用；`Instantiate` 后直接 `SaveAsPrefabAsset` 不能证明依赖已落盘。卸载原 Bundle、关闭并重开工程后仍可加载，才可排除对内存中 Bundle 对象的依赖。

### 4. 接入 Windows 工程并验证

将已核对的原生载荷放入 `unity/Assets/Official/<模块>/`；手写官方迁移代码、离线适配、shader 和编辑器工具放入 `unity/Assets/Game/` 对应目录。保留稳定 `.meta`，更新本模块来源清单及 `assets/reference/SOURCES.md`。来源记录包含原始／解码／最终文件哈希、官方对象路径及 pathID、转换工具和设置、依赖映射及真实脚本／shader 适配范围；临时 `source-manifest.json` 只记录准备输入，不能代替最终 prefab／贴图等输出来源。

恢复官方资源提供方、catalog、场景或 `Resources` 等显式加载绑定；同时检查由配置、名称、ID、反射、动画事件及其他模块动态选择的资源。运行时资源解析改为随游戏交付的本地资产；网络和服务端决策沿用所属功能页已确认的离线方案。模块工具须将**转换／导入**和**验证已有原生资产**分开，验证入口不应在原生资产缺失时静默重试原始 Android Bundle。现有 Map10／Home 的直接 Bundle 导入入口不代表通用恢复链已经完成，后续接入须按本流程替换。

首先检查新增原生资产的 GUID／fileID、`AssetDatabase.GetDependencies` 结果、缺失脚本、组件字段与事件、Mesh／材质／Sprite／字体／动画／音频引用，以及 shader 支持和材质属性。Unity 内置资源 GUID 按实际内置映射处理，不当成缺文件；源内容本来允许为空的字段与损坏引用须区分。随后在 Windows 目标导入、编译，卸载原 Bundle 并重启工程，验证真实入口、层级布局、动画／特效／音频、交互、配置绑定、存档恢复及受影响共享调用方。图形验证不能只用 `-nographics`，shader 可编译也不能代替画面对照。

静态检查及启动命令：

```powershell
python -B scripts/check_unity_project.py
& .\scripts\unity.ps1 open
```

按本模块 Editor 校验入口和规则测试验证；全局 `ProjectTools.Validate` 还会检查其他模块，某一未完成地图导致全局失败时应保留其真实原因，同时运行本模块独立入口，不把全局失败或静态通过代替本模块结论。只有实际需要交付本地 Windows Bundle 时，才从恢复后的原生资产以 `BuildPipeline.BuildAssetBundles(..., BuildTarget.StandaloneWindows64)` 重建，并核对生成的依赖包和本地加载清单；普通 Unity 场景／prefab 引用可随 Player 打包，无需额外 Bundle。更改构建／分包链时按影响验证临时 Windows 构建；正式成品仍遵守本文发布限制。

更新唯一功能／地图页，区分输入已取得、依赖已收齐、资产已恢复、脚本已绑定和实际运行验证范围；不能将其中一个阶段写成整个功能完成。完成后将可重建的转换步骤保留为模块工具，删除本次 `output/` 下的输入副本、导出临时工程、日志、截图与独立存档；只清理本会话目录。素材快照按实际备份需求生成，不要求每个模块重建全量快照。

### 常见失败的处理顺序

| 现象 | 首先检查 | 处理方向 |
| --- | --- | --- |
| `Editor is missing`／文件版本不匹配 | `unity.ps1 info`、实际 exe 路径／版本与 `unity-tools.json` | 仅在确实缺失时修正工具环境；Hub 不是必需项 |
| `Invalid serialized file version`／CAB 头错误 | 原包哈希、解码方式、包和序列化文件各自版本、错误所指 CAB | 保留真实版本，采用可解析的恢复工具；禁止改版本字符串或预期哈希绕过检查 |
| `MISSING`／`Dependency wasn't found` | 完整 CAB 路径、引用方、原 APK／CDN 索引、新版独立记录 | 补齐真实依赖或建立有依据的对象级范围；不自行把丢失引用变为 null |
| 粉色、黑色、透明度／遮罩／排序异常 | 占位 shader、目标 GPU 支持、URP／Renderer、关键字及原材质字段 | 恢复 shader 源码和管线，核对原参数并验证实际图像；版本号相同不保证渲染正确 |
| 缺脚本、反序列化失败、事件不触发 | 类／程序集名、插件版本、字段布局、反编译与 schema 的区别 | 恢复实际组件和业务调用，按既定单机方案适配，不用空壳占位 |
| Editor 可用而重启／Windows 构建后失效 | 未持久化的 Bundle 对象、GUID/fileID、动态绑定、平台插件和本地分包 | 原生资产落盘并重绑，从 Windows 原生资产重建交付内容，在干净加载状态复验 |

官方 UI 遮罩恢复由 `refresh_current_ui_masks.py` 完整读取当前包中的 5,359 个 `UnityEngine.UI.Mask` 对象，按原始 `m_Enabled` 和 `m_ShowMaskGraphic` 补齐 20 份已有 UI 载荷。家园 22 个及祈愿 1 个遮罩均找到对应来源；`HomeUiRenderer` 与共享商城渲染器按这两个标记恢复启用及隐藏遮罩图形的行为。完整床铺背景子树已通过独立图形设备检查，验证包含官方九宫格边框、pivot、遮罩和实际截图；来源及替换前后哈希见 `current-ui-masks-source-manifest.json`。商城物品目录已按当前配置新增三个物品后精确要求 172 条。

## Unity 素材快照

当前转换来源凭据按路径及实际转换链复核；其中 UI 额外恢复官方 Mask、Image 射线参数、透明命中区域与页签组。`refresh_current_ui_text_bindings.py` 根据当前中文 rawfile 的实际键修正 13 份载荷中的 1,183 处 prefab 外语／旧文本，原始文字与转换前载荷保留。`sync_current_home_ui_bindings.py` 验证重生成的家园数据只缺少已核实的官方参数及中文绑定，再恢复既有转换字节；教程输入也保留这些参数，已实际重生成并核对语义一致。Unity Editor 的 `ProjectTools.Validate` 已通过工程、战斗烟测和地图 10 绑定检查；创角模拟输入、家园蒙版 GPU 检查和本次家园验收流程均已通过。更新中文后的抽卡画面已在同步当前代码／同一载荷的独立副本中复验，音频播放未被观测到。详细范围在所属功能页维护，以上不代表 Windows Player 或整游戏验收。

当前 APK／热更新迁移使用 `asset-pack/layout.json` 与 `asset-pack/lock.json` 记录完整素材快照，覆盖 `assets/official/`、`assets/characters/`、`assets/reference/`、`unity/Assets/Official/`、`unity/Assets/StreamingAssets/` 和 `unity/Assets/Game/Resources/`。转换工具、Unity 场景、shader 及 C# 绑定另记代码哈希；Library 与其他编辑器缓存不打包。原始 APK、CDN／设备缓存及替换前按哈希保留的载荷继续存放在 `.tools/source-packages/`。

当前完整快照为 `.tools/asset-packs/zjcs-assets-871_156598-9e78e198ee78b575.zip`，包含 263,240 个文件，压缩后 18,435,548,934 字节，原始载荷 52,542,375,449 字节。全部归档条目已读取并通过 CRC／SHA256 与本地文件 SHA256 核对；锁文件登记整个 ZIP 和清单的 SHA256，并指向 345 份匹配代码的原始字节。旧版 854 快照继续保留；原始 APK／CDN 缓存按前述独立目录保存。

```powershell
python -B scripts/current_asset_snapshot.py create
python -B scripts/current_asset_snapshot.py verify --archive .tools/asset-packs/<lock.json中的archive_file>
python -B scripts/current_asset_snapshot.py restore --archive .tools/asset-packs/<lock.json中的archive_file>
```

创建时检查打包前后的完整文件集合、时间戳及代码哈希；时间戳变化的文件重新计算 SHA256，只有实际字节与 ZIP 中保存的内容一致才接受重复生成，内容或代码变化则停止激活并保留旧 ZIP。验证同时核对整个 ZIP、清单、归档中每个文件及本地载荷的 SHA256；恢复前完整验证暂存，冲突默认停止，确认需替换时使用 `--replace`。实际生成并验证后，`lock.json` 才能作为当前素材已备份的依据。

多会话仅推进代码、素材仍相同时，可保留已完成压缩且尚未激活的候选 ZIP，待压缩进程退出后运行 `python -B scripts/finalize_current_asset_snapshot.py --archive .tools/asset-packs/current-871-candidate.zip`。该入口重新核对完整文件集合、ZIP 每项 CRC／SHA256 与本地每项 SHA256，全部一致才登记快照；文件集合或素材字节不符仍停止。代码指纹在核验完成时重新捕获，原始代码字节另存 `.tools/source-packages/history/converted/` 并由锁文件的 `preserved_code_sources` 记录，不能把压缩开始时的旧指纹冒充当前代码。候选含清单时须与本次逐项核验结果完全一致，不能覆盖已登记的正式快照。

若期间仅重新生成了来源清单，可加 `--refresh-source-inventories`：先核验全部当前转换凭据，再把实际有效的 `*source-manifest.json` 替换到候选归档；其余素材不自动替换。随后仍对归档和本地全部文件执行完整 CRC／SHA256 检查，登记的哈希来自实际归档字节，不修改官方包或转换载荷的预期哈希来跳过失败。

并行开发新增了素材时，先核对新增文件的实际来源，再加 `--include-new-files` 将完整素材目录中新出现的路径纳入候选。该选项仅追加不存在的路径，不替换既有归档载荷；新增项同样逐项读取 ZIP 校验 CRC／SHA256，并核对本地 SHA256。核验期间继续出现的新路径也执行相同检查；删除既有路径、修改既有素材字节或目录持续变化仍停止登记。

既有文件被其他已核实转换更新时，可加 `--refresh-activated-payloads`：按该路径从归档原 SHA256 沿当前版本转换凭据查到待写入字节的 SHA256，成功才替换候选中的对应项，再读取替换后的 ZIP 内容核对 CRC、SHA256 和本地字节；没有完整转换链则停止。来源清单在全量核验后再复核，期间变化的有效来源清单执行相同归档重读。家园重生成另检查既有面板逐字段不变，新增面板／字段与当前官方输入及实际生成器完全一致，保留新增内容并记录来源链，不能恢复旧整份 UI 覆盖其他会话新增面板。

使用 [`unity_asset_pack.py`](../scripts/unity_asset_pack.py) 创建、校验和恢复 Unity 官方载荷快照：

```powershell
python -B scripts/unity_asset_pack.py create
python -B scripts/unity_asset_pack.py verify .tools/asset-packs/unity-official-<sha>.zip
python -B scripts/unity_asset_pack.py restore .tools/asset-packs/unity-official-<sha>.zip --unity-root <工程>/unity
```

ZIP 清单登记逐文件 SHA256 和 Unity 版本。恢复前先验证并暂存全包；发生内容冲突时默认停止，复核后才显式使用 `--replace`。快照放在 `.tools/asset-packs/`，不进入 Git 或运行时。

素材快照必须与其记录的代码和绑定匹配。较早快照不能证明当前新增载荷已经备份；若当前代码哈希不符，应在匹配代码下恢复，再按后续来源记录重建新增模块并完成导入验证。

## 日常功能验收

功能验收直接使用 Unity 工程、本地转换素材和已有导入缓存。独立素材 ZIP 用于备份与恢复，Windows 成品用于发布，两者都不是每项功能验收的前置步骤。

耗时资源准备与玩法验收分开处理。人物验收使用 `CharacterAcceptancePreparation.EnsureModels`，在 `unity/Library/AcceptancePreparation/` 保存角色、幻兽各自的内容凭据：输入、转换代码、导入元数据、生成输出及 Unity 版本／目标平台均匹配，并且当前绑定检查通过，才复用原生模型。首次、缓存丢失或内容变化仍调用原转换工具；失败不记录成功凭据。此缓存不保存 Play Mode 测试结果，后续逻辑、输入、画面和存档断言照常执行。需要排查转换时，启动验收进程设置 `ZJCS_CHARACTER_FORCE_REBAKE=1` 或传入 `-forceAcceptanceRebuild`，强制重建仍遵守原工具的来源和输出保护检查；不要日常删除 Library 或全量重导入。

| 改动范围 | 必做验证 | 按需执行 |
| --- | --- | --- |
| 文档与约定 | 文档检查及差异检查 | 无需导入、素材打包或导出 |
| 规则、状态与脚本 | 相关规则测试、所选资源引用检查；影响游戏行为时在 Play Mode 验证实际入口及状态变化 | 共用规则按调用方扩大回归 |
| UI、场景与动画 | Play Mode 集成测试、实际画面与输入命中验证 | 新素材先导入；画面验证不得使用 `-nographics` |
| 模块素材、转换与动态绑定 | 模块来源和输出哈希、显式加载绑定、Play Mode 动态加载及对应表现验证 | 共享资源迁移按依赖扩大范围 |
| 构建、分包或更新成品 | Windows 构建、导出后启动及改动范围验证 | 首次构建或引擎变化时全量核验 |

Unity 框架静态检查为 `python -B scripts/check_unity_project.py`，Unity 内检查入口为 `Zjcs.Editor.ProjectTools.Validate`。静态检查不能代替编辑器导入、编译和实际运行。资源引用按所选文件和模块范围检查，不能代替来源哈希核验、动态加载测试或跨模块回归。

### 实际运行验证方法与要求

Unity 支持在编辑器 Play Mode 中真实运行场景、驱动交互并截取游戏画面；需要验证独立程序时再运行 Windows Player。游戏功能、UI、场景、动画或资源绑定变动，须按影响范围完成以下流程。纯文档修改只检查文档与差异；独立规则改动先做对应规则测试，不额外扩成整游戏验收。

#### 启动与验证入口

1. 阅读所属功能／地图页，确定真实入口、已确认规则、预期表现及受影响共享调用方。使用唯一命名的隔离存档和临时输出路径，禁止覆盖用户存档。已有 Unity 实例时先确认所属工程和正在执行的任务，复用可用实例；不得关闭其他会话实例或同时以两个编辑器打开同一工程。
2. 从仓库根目录运行 `& .\scripts\unity.ps1 info` 核对本机编辑器；工程未打开时用 `& .\scripts\unity.ps1 open`。使用固定版本 `2022.3.57f1c2`、Windows 目标及已有导入缓存，只导入本次新增或修改且需导入的素材，等待脚本编译完成。运行静态检查和相关模块 Editor 校验；`Zjcs.Editor.ProjectTools.Validate` 检查框架及资源，不会代替 Play Mode 游玩验证。
3. 打开实际启动场景 `unity/Assets/Game/Scenes/Bootstrap.unity`，进入 Play Mode，从玩家可见入口走到改动功能，检查进入、操作、返回和再次进入。可用独立场景／测试入口定位模块问题，但须区分模块隔离验证与真实启动链验证；临时创建对象或直接调用控制器不能证明玩家入口已接通。
4. 使用 Game 窗口的 450×800 基准尺寸，并在涉及布局或输入映射时改变窗口尺寸，检查竖屏比例缩放和输入坐标。自动化可使用模块 Play Mode 校验器或 Unity Test Framework；例如创角菜单 `Zjcs/Validate/Character Creation Play Mode` 对应 `Zjcs.Editor.CharacterCreation.CharacterCreationPlayModeAcceptance.Run`。测试入口的存在不代表已经执行或通过，运行后仍须核对其实际覆盖的逻辑、输入与画面范围。

#### 逻辑、输入与画面核对

| 核对项 | 实际操作与通过要求 |
| --- | --- |
| 玩法与状态 | 执行改动涉及的正常流程，以及适用的取消、条件不足、重复操作、切换和返回分支；同时核对 UI 反馈、规则数值、消耗／奖励、状态变化及执行顺序，以官方依据和已确认单机方案为预期，不能只以未抛异常判定通过。 |
| 鼠标与键盘 | 在可见控件位置使用实际鼠标／键盘或经当前输入系统处理的自动化输入；涉及拖拽、滚动、文本输入时分别验证。检查 EventSystem／Raycaster 命中、焦点、弹窗阻挡、禁用态及缩放后的坐标。直接调用业务方法、`Button.onClick.Invoke()` 或向指定对象发送事件会绕过部分输入链，须说明覆盖边界，并补做真实命中验证。 |
| UI 与场景画面 | 实际查看 Game 窗口，或捕获并打开运行时截图逐项核对官方层级、锚点、尺寸、字体、遮罩、排序、材质、透明度和场景内容；排查粉色／黑色材质、缺图、错位、裁切和遮挡。仅保存图片、确认 Renderer 存在或 shader 编译成功均不能算画面通过。 |
| 动画、特效与音频 | 持续运行并检查受影响的开始、播放、循环、切换和结束状态；截图只能证明对应帧，不能代替动作连续性、骨骼／网格／换装及特效时序检查。涉及音频时验证实际播放、切换与停止；未能听验时如实注明范围，不能用音频文件存在代替播放效果验证。 |
| 本地存档与离线加载 | 涉及持久化时，在隔离存档中完成修改、保存、退出 Play Mode 并重新进入，核对恢复后的 UI 和实际状态；涉及磁盘资源、初始化或缓存时再重启工程验证。运行时使用随游戏交付的本地资源，不依赖 `.tools/`、Editor 专有对象、原 Bundle 临时句柄或联网返回；测试准备阶段可使用官方来源与工具。 |

画面验证不得传入 `-nographics`，因为该模式不初始化图形设备。`-batchmode` 本身也不能证明图形验证完成：只有实际产生渲染结果并检查后才能写入画面结论；若自动化环境不能生成正确画面，改用带 Game 窗口的编辑器运行或按需 Windows Player。截图可用 `ScreenCapture.CaptureScreenshot`，须等待场景、UI 与目标动画状态就绪并完成渲染后捕获；不能用 Scene 窗口、prefab 预览或空白截图代替游戏运行画面。方法依据见 [Unity Game 窗口](https://docs.unity3d.com/2022.3/Documentation/Manual/GameView.html)、[Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/index.html)及 [ScreenCapture](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ScreenCapture.CaptureScreenshot.html)。

#### 失败修复与完成标准

测试前标明关键步骤的预期结果；运行中检查 Console／Editor.log，Player 验证时检查 Player.log，保留必要的临时错误上下文。自动化应有就绪条件、断言和超时，区分未启动、编译失败、流程失败、截图失败与实际通过；只看到进程启动、返回码为零或一条成功日志不够。新异常、丢失引用、渲染错误或交互失败须定位到调用方／资源绑定，修复后重跑失败流程及受影响共享调用方，直到本次范围通过；不得靠跳过断言、屏蔽异常或直接改状态制造通过。

全局校验被其他未完成模块阻塞时，记录真实错误并继续可独立完成的模块运行验证；隔离测试通过不能写成完整启动链通过。若最终仍受确实无法自行解决的外部条件或未确认规则阻塞，说明具体入口、报错／缺口、已尝试方法与结果，并提出需要的用户答复或条件，保留未验证状态。

所属功能／地图页只维护最终实现、已确认差异和实际验证范围：分别写明静态检查、Editor 导入／编译、Play Mode 逻辑、真实输入、画面／动画／音频、存档恢复，以及按需 Windows Player 的结果，注明实际入口、测试方法及关键分支。未执行、不适用、失败和受阻须明确区分，不得把较低层检查通过推断为较高层通过；本次要求的流程、表现和状态均已实现并验证后才能标记相应范围完成。结论写回后删除本会话临时日志、截图、录像、输出和隔离存档，不另建验收报告、截图索引或备份。

日常验收在 Unity 工程内完成即可；仅验证导出行为、修改构建／分包链或用户要求更新成品时构建 Windows，并真实启动 Player 检查受影响流程、输入、画面和存档。临时验证构建不得发布、覆盖正式成品或更改快捷方式；`BuildFrameworkWindows` 仅构建空框架，不能证明完整游戏通过。

## Windows 发布

Windows 正式成品统一输出到 `D:/Desktop/杖剑传说/`，入口为 `杖剑传说.exe`。完整 Unity 游戏尚未整体验证前，不得发布、覆盖现有成品或修改桌面快捷方式。只有需要验证导出行为、修改构建链或用户要求更新成品时才构建；复用正式目录的增量基准。

龙之国剧情气泡的恢复入口为 `scripts/prepare_unity_map14_bubble_ui.py`／`prepare_unity_story_bubble_runtime.py`，原生窗口在 `Resources/Map14/BubbleUI/`，官方投影在 `Assets/Official/World/Map/14/BubbleUI/`。当前 APK-first 原来源保留完整层级、组件字段、原始字节哈希和两张精灵；原 UI 坐标和绑定直接转换。Windows 运行时在独立字体副本中以官方 TTF 重建空动态图集内的旧字形记录，并校验原字形编号和指标，保持源素材不变。来源及输入输出哈希见两份 Map14 bubble UI／runtime 清单；导入、编译、全工程校验与内置屏两种竖屏画面、模拟点击／原打字机和跟随已验证，正式地图／Timeline／存档与 Windows Player 仍未完成，详见[龙之国](maps/map-14.md)。本批尚未进入完整素材快照。

实体冒泡 Timeline 恢复：`scripts/prepare_unity_story_bubble_timeline.py` 校验当前官方程序集后迁移五份原类，显式窗口服务保留创建、延后完成与暂停关闭顺序。清单 `official-map/map14-story-bubble-timeline-source-manifest.json` 记录原源码／程序集、Cecil 核对的非虚基类调用、代码／元数据、工具、依赖与验证脚本哈希。内置屏 Unity 导入／编译、全工程校验及原三轨道／四片段与原生气泡 UI 的 Play Mode 通过；原起止时间、四句台词、时长／间隔、绑定与关闭均已验证，实际画面已查看。原完整 Timeline 资产、相机／角色／动画／音频及正式地图／存档／Windows Player 仍待完成。 对应范围见[龙之国](maps/map-14.md)；本批未新增替代素材，也未生成完整素材快照。

Animator 参数 Timeline 恢复：`scripts/prepare_unity_story_animator_timeline.py` 从当前官方程序集迁移三份原类，保留首次帧写入、各参数快照、控制器优先查找、触发后原生 Update(0)、暂停重入及结束恢复分支。清单 `official-map/map14-story-animator-timeline-source-manifest.json` 记录原代码／程序集、Cecil 基类调用核对、输出、元数据、工具、依赖及验证代码哈希。内置屏 Unity 导入／编译、全工程校验与隔离原生 Animator／Timeline Play Mode 通过；四份原触发配置和参数分支已验证。隔离参数控制器不代表原角色或场景动画还原，原素材与完整地图／存档／Windows Player 仍待接通。 对应范围见[龙之国](maps/map-14.md)；本批未新增替代素材，也未生成完整素材快照。
