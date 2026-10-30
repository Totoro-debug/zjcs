# 素材管理与共用操作

[返回文档索引](README.md)

本页只维护通用操作。模块实现、关键决策及当前验证范围写在所属功能／地图页；输入版本、哈希与凭据见[官方来源](../assets/reference/SOURCES.md)。

## 目录与版本管理

| 位置 | 用途 |
| --- | --- |
| `unity/Assets/Game/` | Git 管理的场景、C#、shader、配置与 `.meta`；官方迁移代码在 `Runtime/Official/`，单机适配在 `Runtime/Offline/` |
| `unity/Assets/Official/` | 独立官方素材载荷与 `.meta`；不得放手写代码 |
| `assets/official/`、`assets/characters/` | 独立配置／角色转换载荷，不进入 Git；运行所需内容须随 Player 打包 |
| `assets/reference/` | 来源身份、哈希及必要参照 |
| `.tools/` | 原始包、工具、可复用转换缓存和素材快照；不进入游戏导出 |
| `output/validation/<模块>-<GUID>/` | 单次验证的工程、Library、Player、日志、截图与隔离存档 |
| `D:/Desktop/杖剑传说/` | 正式 Windows 交付目录 |

Unity 缓存、IDE 输出、日志和临时构建不提交或进入素材快照。保留稳定 `.meta`／GUID，清理前核对静态／动态引用、生成器和旧存档兼容。

暂存文件超过 50 MB 时，提交钩子将其转为 LFS 指针并在同次提交登记精确路径。新克隆运行 `python -B scripts/install_lfs_size_hook.py`；要求 Git LFS 已安装，安装器保留自定义 pre-commit，LFS 推送钩子负责上传对象。

## 验证临时产物

PowerShell 点源 `scripts/validation_workspace.ps1`，使用 `New-ZjcsValidationWorkspace`／`Remove-ZjcsValidationWorkspace`；同一调用可用 `Invoke-ZjcsValidation` 在 finally 清理。Python 使用 `scripts/validation_workspace.py` 的 `temporary_validation_directory(prefix)`。

```powershell
. ./scripts/validation_workspace.ps1
$work = New-ZjcsValidationWorkspace -Name module
$env:ZJCS_VALIDATION_DIR = $work
$env:ZJCS_REPOSITORY_ROOT = (Get-Location).Path
try {
    # 临时工程放 $work/unity，Player 放 $work/player；等待外部进程退出并检查退出码。
    # 完成模拟输入、查看实际运行截图，将当前结论写回所属页。
} finally {
    Remove-Item Env:ZJCS_VALIDATION_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:ZJCS_REPOSITORY_ROOT -ErrorAction SilentlyContinue
    Remove-ZjcsValidationWorkspace -Path $work
}
```

只删除本会话明确创建且已停止使用的目录。清理工具拒绝根目录、目录链接、其他存活会话及仍被 Editor／Player 使用的目录；有占用先停止占用再重试，不清空整个 output。跨调用需查看截图时先保留，结论写回后清理。执行策略拒绝时如实报告，不绕过审批。PowerShell 预检以 .NET 枚举逐项检查文件与目录（包括隐藏项），进入每层目录前再次核对链接属性；枚举或属性读取失败即停止清理。

## 官方来源与转换

按 `official-source.json` 选择输入，先查 APK 的清单、实际对象、prefab 绑定和 CAB，再查所选版本已下载的官方云端缓存。未提取、未解码、未转换、加载失败或 `Not packaged or cached` 均不等于资源缺失。两处均核实找不到时，列出资源／bundle、查找范围、方法与影响交由用户决定，不自行替代。

保留真实版本、URL、原对象／CAB／PathID、输入输出哈希、工具与处理方式、`.meta` 和显式加载绑定。`Apk.current()`、`Apk()` 与 `Apk(manifest=None)` 均读取 `official-source.json` 选定的当前输入；需要复核非当前来源时必须显式指定其 manifest，不能把旧对象冒充当前版本。来源清单保留后续转换哈希链，不按旧基线覆盖其他会话新内容，不修改预期哈希绕过失败。

资源迁移优先使用当前选择版本 `871_156598`。已转换到 Unity 工程中的内容一律保留。若经清单、对象与依赖闭包核实，当前版本确实没有对应原始资源，则保留既有处理结果并在所属功能／地图页记录缺项；不得伪写来源或宣称整个模块已迁完。

## Spine 角色动画迁移决策

免费原生动画迁移路线、许可边界及兼容缺口统一见[角色创建：Spine 迁移决策](features/character-creation.md#spine-迁移决策)。共享角色调用方引用该决定，不另维护一套方案。

## Android 资源迁移到 Windows

流程为：已校验官方输入 → 依赖闭包 → Unity 原生资产／脚本恢复 → Windows 绑定与实际运行。复制 Android bundle、编辑器加载成功或 shader 编译成功均不能证明 Player 可用。

### 1. 定位模块和核对环境

先读所属页，确认原代码／调用关系、配置、资源身份、所选版本及运行入口。工程编辑器固定 `2022.3.57f1c2`，原包头为 `2022.3.57f1`；逐项核对序列化版本、BuildTarget、CAB 和平台格式，不推定二者完全兼容。`& ./scripts/unity.ps1 info` 检查本机环境。

### 2. 一键检查并准备 CAB 依赖闭包

`scripts/prepare_unity_bundle_inputs.py` 复用 `official_apk.py`／`official_cloud_cache.py`，校验 APK SHA256、包大小／MD5、下载来源 SHA256、序列化外部引用和 Unity 内置依赖。使用与 `.tools/character-art-python` 中 UnityPy 1.25.4 扩展 ABI 匹配的 Python；不修改系统环境。

```powershell
# 检查不创建转换目录；多个主包可重复 --bundle。
python -B scripts/prepare_unity_bundle_inputs.py --bundle <实际原包名>
# 准备时目标必须尚不存在，并位于本会话目录。
python -B scripts/prepare_unity_bundle_inputs.py --bundle <实际原包名> --stage-dir "$work/inputs" --verbose
if ($LASTEXITCODE -ne 0) { throw '先修复输入或依赖错误' }
```

退出码 0 仅表示输入及序列化文件级闭包完成；1 表示失败。清单记录实际来源、解码前后哈希、CAB、版本、平台及依赖边。缺 CAB 输出 `MISSING: 引用方 -> 外部路径` 并停止，不生成伪完整目录；不联网、不自动混入新版、不覆盖已有目标。缩小范围时按目标 PPtr、调用与动态加载建立对象级闭包并登记，不能忽略 MISSING。

### 3. 按资产类型恢复 Unity 原生文件

| 类型 | 必须保留 |
| --- | --- |
| 场景／prefab／UI | 原层级、节点／组件顺序、字段、引用、坐标／锚点／尺寸、布局及输入参数 |
| Sprite／纹理／图集 | 原像素、裁切／旋转、边框、轴心、PPU、网格／UV、mip、色彩空间与采样参数 |
| 网格／骨骼／动画 | 原顶点、索引、权重、约束、插槽、附件、事件、曲线、混合与时序 |
| 材质／shader／粒子 | 原数值、纹理引用、关键字、混合／裁切／排序、粒子字段和生命周期；Windows shader 按原程序恢复 |
| 字体／音频 | 原字体指标、图集、材质和本地化；原事件、播放图、媒体、增益、循环及停止行为 |
| 脚本／插件 | 原类／程序集身份、序列化字段、公式、分支及执行顺序；按所属页已确认范围适配离线和 Windows |

可使用本机 AssetRipper 恢复原生资产，源版本及设置写入模块清单。导出位于本会话目录；检查 `ShaderExportMode`，Dummy shader、占位脚本和可解析 GUID 不等于原实现已恢复。原生资产恢复后仍核对字段／指针和运行表现。

### 4. 接入 Windows 工程并验证

恢复原资源提供方、catalog、场景或 Resources 显式目录，并检查名称／ID、反射、配置、动画事件和跨模块动态选择。运行时只消费随 Player 交付的本地资产，不依赖 .tools、Editor 对象、Android bundle 临时句柄或联网响应。

转换／导入与验证已有原生资产使用独立入口；缺原生资产时不静默回退直接加载 Android bundle。复用输入、工具、元数据、输出、Unity 版本及目标平台均匹配且绑定检查通过的缓存；内容变化再重建，不日常删除 Library 或全量重导入。

### 常见失败的处理顺序

先保留原异常与路径，依次查来源哈希、版本／BuildTarget、CAB、对象引用、脚本／插件、shader／平台格式和显式加载。序列化头错误属于导入问题，不据此反复安装 Hub／编辑器。编译失败时先修复 C#／程序集依赖，再检查 executeMethod 入口；静态或隔离检查通过不能替代完整启动链。

## 可选 Unity 素材快照

快照只是本机转换素材的恢复副本，不是 Unity 构建、游戏运行或常规验证的输入。项目当前资源已保存在 `asset-pack/layout.json` 所列载荷目录中；`asset-pack/lock.json` 记录可选归档的文件名、逐文件 SHA256、代码／工具／绑定哈希。原 APK／CDN／设备缓存另存，不在转换 ZIP 内；Library、IDE 输出、临时报告不打包。

`.tools/asset-packs/` 是可清理的本地快照目录。删除其中的 ZIP 和清单不会删除项目资源或原始来源；之后 `verify`／`restore` 需等重新运行 `create` 生成快照才可使用。保留 `asset-pack/lock.json` 作为快照登记；重新创建会更新登记。

```powershell
python -B scripts/current_asset_snapshot.py create
python -B scripts/current_asset_snapshot.py verify --archive .tools/asset-packs/<lock中的归档>
python -B scripts/current_asset_snapshot.py restore --archive .tools/asset-packs/<lock中的归档>
# 只管理 Unity 官方载荷时：
python -B scripts/unity_asset_pack.py create
python -B scripts/unity_asset_pack.py verify .tools/asset-packs/unity-official-<sha>.zip
python -B scripts/unity_asset_pack.py restore .tools/asset-packs/unity-official-<sha>.zip --unity-root <工程>/unity
```

创建／登记须核对完整文件集合、代码指纹、ZIP 每项 CRC／SHA256和本地文件 SHA256；时间戳变化重算哈希，不以时间戳替代字节核验。恢复前完整验证并暂存，冲突默认停止，复核后才用 `--replace`。只有实际生成并通过校验的归档才算备份；新增载荷或代码变化不自动被旧快照覆盖。

并行生成的候选归档在压缩进程退出后用 `scripts/finalize_current_asset_snapshot.py --archive <候选ZIP>` 重新全量核验并登记，不能覆盖已登记归档。按需选项：

- `--refresh-source-inventories`：核验当前凭据后只替换有效来源清单。
- `--include-new-files`：核对新增来源后仅追加新路径，不覆盖既有载荷。
- `--refresh-activated-payloads`：从归档原哈希沿已核实转换链找到当前字节后才替换；无完整链则停止。

以上选项仍执行完整归档／本地核验。文件删除、无凭据字节变化或目录持续变化时停止登记；代码原始字节按哈希保留并记录在 `preserved_code_sources`。模块新增字段不得被恢复旧整份 UI 覆盖。

## 日常功能验收

| 改动 | 检查范围 |
| --- | --- |
| 文档 | 结构、归属、链接、关键决策和差异；不导入／构建游戏 |
| 规则／状态／脚本 | 相关规则测试、引用检查；影响行为时运行实际入口和状态变化 |
| UI／地图／动画／资源绑定 | 导入／编译、Play Mode、模拟输入、实际画面及相关存档／失败恢复 |
| 完成功能／地图或修改发布链 | 构建并启动 Windows Player，验证入口、受影响流程、画面与存档 |

公共检查为 `python -B scripts/check_unity_project.py` 和 `Zjcs.Editor.ProjectTools.Validate`。共享改动按调用方扩大检查；测试入口存在、退出码 0、无异常或资源存在都不等于玩法完整。

### 实际运行验证方法与要求

1. 从所属页确定入口、官方预期、已确认差异及受影响调用方。先确认现有 Unity 实例，不关闭其他会话，也不同时打开同一工程。使用隔离存档，保护用户数据。
2. 打开工程用 `& ./scripts/unity.ps1 open`。自定义命令先点源 `scripts/unity_internal_display.ps1`，再用 `Start-UnityOnInternalDisplay -Editor <路径> -ArgumentList <原参数数组>`；隔离工程同样使用仓库根工具并等待退出。按内置屏硬件身份定位，不改主屏、不抢前台、不操作真实鼠标，不停用定位守护。
3. 从 `unity/Assets/Game/Scenes/Bootstrap.unity` 实际玩家入口进入功能，检查进入／操作／返回／再进入和取消、材料不足、重复操作等分支。隔离夹具、直接业务回调与正式入口明确区分。
4. 使用模拟指针走 EventSystem／Raycaster、按下／抬起／点击；涉及键盘、拖拽、滚动和文本输入时模拟相应输入。核对焦点、禁用态、弹窗阻挡和缩放坐标；直接 Button.onClick 或业务方法不能代替输入命中。
5. 实际查看 Game 画面或打开运行时截图，核对层级、布局、遮罩、排序、字体、材质和遮挡；以 450×800 基准及受影响尺寸验证比例。不得用 `-nographics`、Scene 预览、空白图或仅保存截图代替画面检查。
6. 持续检查动画／特效的开始、循环、切换和结束，以及音频实际播放／停止；单帧不证明连续动作，声音派发不证明听验。保存后退出并冷读，检查 UI／状态、奖励去重及失败回滚。
7. 检查 Console／Editor.log／Player.log，自动化设置就绪条件、断言和超时；失败修复后重跑相关流程。未执行、失败、受阻及不适用分别记录，不能用较低层通过推断较高层通过。

所属页只维护当前实现、决策和验证覆盖／缺口，不保留轮次、日期流水、日志汇编或截图索引。结论写回并实际查看截图后清理本会话临时产物。模块隔离通过不推定整图／全工程／Player 通过。

## Windows 发布

每完成功能或地图，完成相关检查与运行验证后构建、真实启动并发布包含该成果的 Player。检查入口、受影响流程、模拟输入、画面和存档，失败修复后重跑；发布是逐项完成条件，不等待全游戏迁移结束。

正式入口为 `D:/Desktop/杖剑传说/杖剑传说.exe`，桌面快捷方式指向该文件；复用增量缓存，保护用户存档。交付与已提交代码、资源和所属文档一致，明确可验证内容及尚未完成范围。临时构建留在验证目录，空框架构建不算功能交付。
