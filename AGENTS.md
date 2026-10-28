# 项目协作约定

## 项目目标

- 完整迁移在线多人冒险手机游戏《杖剑传说》，制作可完全离线运行的单机 Windows 版本，面向 Windows 10/11 原生桌面。
- 游戏引擎采用 Unity，语言采用 C#，工程入口为 `unity/`。保留原版 2D 表现、竖屏布局与按比例缩放。
- 迁移范围包含玩法、规则、功能、地图、角色、场景、HUD、界面、动画、特效、音频及存档；完成标准是实际可游玩的迁移成果。
- 规则、配置和存档均在本地，运行时不依赖网络。多人、组队、赛季、商城等内容按所属功能页已确认的单机方案迁移；未确认的服务器规则或单机差异须询问用户，不擅自删减功能或编造规则。

## 官方来源与代码迁移

| 内容 | 路径 |
| --- | --- |
| 原版游戏官方 APK | `.tools/source-packages/zjcs_M509085.apk` |
| 原版游戏已下载的云端资源 | `.tools/source-packages/cloud/` |
| 官方程序集与反编译代码 | `.tools/source-packages/official-code/` |
| 资源版本选择 | `official-source.json` |
| 来源、工具与哈希记录 | `assets/reference/SOURCES.md` 及各模块来源清单 |

- 迁移前必须先提取、解密并反编译官方 APK，取得相关官方代码，查清配置、调用关系和依赖，再复制或迁移官方代码到 Unity 工程。已有可核实来源的反编译结果可以复用。
- 官方代码目录中，`managed/decoded/` 保存程序集，`client-source/` 保存客户端反编译结果，`common-source/` 等保存共享实现。缺少所需结果时运行 `python scripts/prepare_official_sources.py --decompile-client`，按本机路径配置 `--dotnet`／`--ilspy`，并核对提取范围。
- 优先直接复用官方代码；反编译修复、C# 适配和离线迁移须保持官方数值、公式、分支、状态变化与执行顺序。不得跳过官方实现查找而自行重写一套近似玩法。
- APK 不一定包含服务器实现。缺少服务器逻辑时，先核对客户端计算、请求／响应消费、配置和已确认的单机方案；仍缺公式、奖励、条件或状态规则时，说明具体类／函数、已知证据和行为缺口，请用户决定。
- 在既定方案内主动完成提取、解码、反编译、依赖恢复和失败修复；首次报错、工具缺失或检索未命中不能作为停止理由。

## 官方 HUD、界面与美术

- HUD、界面、美术及其交互表现必须与官方完全一致。必须迁移官方 UI 代码、prefab、场景层级和资源绑定，不允许自行实现 UI、自行设计布局、绘制替代素材或用静态截图替代交互界面。
- 按官方坐标、锚点、尺寸、缩放、字体、颜色、材质、shader、骨骼、网格、动画参数和执行顺序恢复表现；Windows 输入与缩放适配须保持官方布局和视觉效果。
- 美术资源严格按以下顺序查找，不得跳过前一步：

  1. 先解析、反编译并提取官方 APK，核对包内清单、实际文件、内部对象、prefab 引用、加载绑定及 CAB 依赖。
  2. APK 中确实找不到时，再查 `.tools/source-packages/cloud/` 中已下载的官方资源，核对所选版本的索引、实际文件、哈希及依赖，提取并转换所需内容。
  3. 两处均核实找不到时，询问用户，列明资源／bundle 名称、查找范围、已尝试方法和缺失影响；不得擅自自绘、近似替代或降低表现。
- 未提取、未解码、未转换、加载失败或 `Not packaged or cached` 不等于官方资源缺失，须继续定位原因。索引中有名称也不等于实际文件已取得。
- 资源工具优先复用 `scripts/official_apk.py`、`scripts/fetch_official_assets.py` 和现有提取缓存；完整清单及版本记录见来源文档。APK、不同云端版本及历史来源分别记录，不混用版本或修改预期哈希绕过校验。
- 按 [Android → Windows 流程](docs/asset-management.md#android-资源迁移到-windows)恢复 Unity 原生资产、脚本、shader 和插件，完成显式加载绑定；复制 Android bundle 不等于 Windows 可用。
- AssetBundle 包头为 Unity `2022.3.57f1`，工程与本机编辑器为 `2022.3.57f1c2`；须实际验证兼容性。角色 Spine 4.2 沿用已确认的[免费原生动画迁移方案](docs/asset-management.md#spine-角色动画迁移决策)，复用官方贴图、骨骼、网格及动画数据，不以购买 runtime 为前置条件，不复制或反编译旧 npm runtime 构造替代。

## 持续推进、验证与文档

- 按用户指定范围持续完成代码迁移、资源接入、运行验证和失败修复。小模块提交或发布后继续剩余范围，不等待用户再次说“继续”。
- 功能／地图须接通玩法、官方界面与美术、本地状态和存档，并完成相关实际运行验证；规则核心、资源目录、空框架或静态检查通过不能代替完整成果。
- 按影响范围运行规则测试、静态检查 `python -B scripts/check_unity_project.py`、Unity 导入／编译和 `Zjcs.Editor.ProjectTools.Validate`。游戏功能、地图、界面、动画或资源绑定变动须在 Play Mode 实际运行受影响流程。
- 交互验证使用模拟鼠标点击，不要求操作系统真实鼠标输入；涉及键盘／拖拽时模拟对应输入。核对遮挡、渲染画面、状态变化和涉及的存档恢复；直接调用业务回调只证明对应逻辑。画面检查须查看实际运行画面或运行时截图，不使用 `-nographics`。发现失败先修复再重跑，如实区分已执行、未执行与阻塞项。
- 用户使用外接屏玩游戏；启动 Unity 编辑器或带窗口的 Unity 验证时，必须定位到电脑内置屏，不能按系统主屏或固定屏幕编号定位。打开工程用 `& ./scripts/unity.ps1 open`；带自定义验证参数时，先 `. ./scripts/unity_internal_display.ps1`，再调用 `Start-UnityOnInternalDisplay -Editor <编辑器路径> -ArgumentList <原有参数数组>`。隔离 worktree 验证也使用仓库根目录的这份启动工具。不得切换系统主屏、抢占前台或操作真实鼠标；保留截图验证。
- 本机已通过用户登录启动项启用 `UnityInternalDisplay` 定位守护，补充覆盖直接启动 `Unity.exe -projectPath D:/Desktop/zjcs/...` 的既有会话与隔离 worktree。配置与脚本在 `%LOCALAPPDATA%/UnityInternalDisplay/`，运行状态见其中 `status.json`；不要停用或绕开该守护。它只匹配本仓库下的 Unity 工程，按内置屏硬件标识定位窗口，不改变系统主屏。
- 每次修改或完成功能／地图，都必须同步更新相应文档，完成相关检查后进行 commit。代码、素材接入与文档在同次提交中保持一致，不能只改实现而不更新文档。
- `docs/README.md` 是唯一总索引；功能维护 `docs/features/` 下所属页，地图维护 `docs/maps/map-ID.md`。状态或范围变化同步索引，共享规则在所属页维护并由其他页引用。
- 所属页顶部写明已完成／未完成并回链索引；正文使用“功能简介／地图简介”“完整还原部分”“自定义实现”，未完成时增加“尚未实现”。记录实际规则、交互、官方来源、确认的单机差异、代码／资源入口和验证范围；没有自定义内容写“无”。完整实现并验证后才移除对应缺项。
- 不另建验收、审计、收尾、迁移进度或交接报告，不保留日期流水、日志汇编及截图索引。结论写回所属页，历史通过 Git 追溯。
- 需要用户决定时及时提出具体问题，同时继续独立工作；阻塞时说明卡点、已尝试方法及结果、所需答复或外部条件。未确认的推测不得写成官方规则或完成结论。

## 每个功能／地图的可执行版本

- 每完成一个功能或一张地图，都必须构建并发布包含该成果的 Windows 可执行版本，供用户验证；发布是该功能／地图的完成条件之一，不等待整个游戏全部迁移完成。
- 发布前完成该功能／地图的相关验证，并真实启动构建出的 Windows Player，检查入口、受影响流程、模拟输入、画面和存档。构建或运行失败须定位、修复并重新验证，不能以空框架构建代替。
- 可执行版本统一发布到 `D:/Desktop/杖剑传说/`，入口为 `杖剑传说.exe`，桌面快捷方式指向该文件。复用现有构建缓存和增量资源，交付时明确可执行文件路径、本次可验证内容及尚未完成范围。
- 每次发布须与已提交的代码、资源和所属文档一致；保护已有用户存档，不用测试存档覆盖用户数据。构建产物和 Unity 缓存不提交到 Git。
- 本节规定功能／地图逐项完成后的发布要求；其他文档中的构建与发布条件若与本节冲突，以本节为准。

## 素材与临时产物管理

- `unity/Assets/Game/` 保存受 Git 管理的场景、C#、shader、配置及 `.meta`；`Runtime/Official/` 保存迁移的官方代码，`Runtime/Offline/` 保存单机适配。`unity/Assets/Official/` 保存独立素材载荷及 `.meta`，不得放手写代码。
- `.tools/` 保存原始包、工具与缓存，不提交、不进入游戏导出。运行时只使用随游戏打包交付的本地资源；素材目录、ZIP 快照与恢复规则见[素材管理](docs/asset-management.md)。
- 测试／验证的临时 Unity 工程、`Library`、Windows Player、编译输出、日志、截图和隔离存档统一放在仓库 `output/validation/<模块>-<GUID>/`，不得新增到 `.tools/`。点源 `scripts/validation_workspace.ps1` 后使用 `New-ZjcsValidationWorkspace`／`Remove-ZjcsValidationWorkspace`，或用 `Invoke-ZjcsValidation` 在 `finally` 自动清理；Python 使用 `scripts/validation_workspace.py`。仅删除本会话明确创建且已停止使用的目录，结论写回所属文档、实际查看截图后清理；不得清空整个 `output/` 或其他会话目录。目录迁移不能保证跨会话审批策略放行，若仍被拒绝须如实报告，不绕过审批。
- 新验证会话先设置 `ZJCS_VALIDATION_DIR` 为上述会话目录，`ZJCS_REPOSITORY_ROOT` 为仓库绝对路径；验证入口使用对应输出参数和仓库绝对路径。`output/` 内的 Unity 工程也必须通过仓库的 `Start-UnityOnInternalDisplay` 启动并等待退出。正式发布目录仍为 `D:/Desktop/杖剑传说/`。
- 文档只维护当前目录约定与使用方式，不保留旧文件、旧目录的盘点、占用统计或清理记录；历史通过 Git 追溯。
- 素材变动同步 `assets/reference/SOURCES.md`、所属来源清单、输入输出哈希、处理方式及显式加载绑定。只有迁移、素材交付或备份需要时集中生成完整快照并更新 `asset-pack/lock.json`；未入快照的素材不能声称已备份。
- 优先使用现有导入缓存，只导入受影响素材。清理前核对静态／动态引用、生成脚本和旧存档兼容，删除时同步引用与文档；不执行 `git add --renormalize assets`。
- 一次性脚本、日志、截图和隔离存档在结论写回所属页后清理，仅删除本会话产物，不清空其他会话输出。Unity 缓存、IDE 产物和独立验收报告不进入素材快照。

## 提交与多会话协作

- 开始前读取最新 HEAD、暂存区及工作区差异，写入共享文件前后复核最新内容，保留其他会话工作；只暂存并提交自己的文件或修改片段。
- 每次功能／地图修改在同步文档、完成相关检查并清理本会话临时产物后及时提交；每完成一个可独立核对的小模块就提交并继续后续工作。提交标题使用 `类型(模块): 中文说明`。
- 最终共用本地 `master`，必要时使用分支／worktree，通过 fast-forward 或逐个 cherry-pick 接入。保持 commit 树为单父级线性历史，不产生 merge commit，不用 amend、reset 或强制推送改写历史。
- 提交前重读 HEAD 和相关差异；HEAD 前进时接到最新提交并检查影响，按需重跑验证。提交后核实唯一父提交等于提交前最新 HEAD、自己的修改已提交、其他会话未提交内容完整保留。

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
