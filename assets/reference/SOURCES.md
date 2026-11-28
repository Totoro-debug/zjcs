# 官方来源约定

[返回文档索引](../../docs/README.md)

固定来源按[项目协作约定](../../AGENTS.md#官方还原原则)执行。固定来源的 APK 和配套在线资源缓存使用通用目录，不使用版本名或 `pinned` 目录；读取入口以[仓库来源配置](../../official-source.json)为准，调用方不得硬编码输入路径。来源配置名来自在线资源清单，APK 内置清单版本以 `apk.manifest` 为准。原始包、工具及缓存留在 `.tools/`，运行资源必须随游戏本地交付。

来源、输入输出哈希、对象依赖及转换凭据由本目录机器可核验的清单和原始下载记录维护；不在说明文档重复资源清单、代码位置、实现过程、统计或历史验证。

各版本的输入保持独立，以原有哈希检查完整性和一致性。涉及官方输入的更新以固定来源复核相关配置、资源及依赖，并同步来源凭据。转换使用的 C# 按所选版本的逐文件反编译清单核验，并关联已解密程序集；不以另一版本的代码副本代替当前来源校验。旧版本资源按[素材管理中的模块替换约定](../../docs/asset-management.md#目录与版本管理)清理，溯源保留必要来源凭据。游戏运行时依据资源结构、对象引用及数据身份加载。

召唤物的属性来源核验覆盖固定属性、主人继承、品质系数及完整培养曲线；本地规则包哈希不能代替与官方输入的逐项比对。

补齐固定来源的本地输入时按来源配置校验 APK 和缓存。官方下载文件名复用时按内容哈希识别 APK。用户明确指定切换来源时，先核验并保留当前 APK 快照及其来源配置。程序集解密方式以所选来源的 `Launch.HCC.A` 为准，解密后只作静态反编译，不执行官方程序集。

新迁移先核对 APK，确实没有再核对已下载官方云端资源；未提取或加载失败不等于缺失。缺少代码时继续恢复反编译范围，仍缺规则或素材时按全局约定向用户确认。

## 必要来源凭据

风魔之影 `10569／25038→11670→11671→11672→24232` 的所选 APK 配置与反编译代码由 `scripts/prepare_battle_wind_shadow_sources.py` 核对，输入身份及哈希见[风魔之影来源](battle-rules/wind-shadow-source-manifest.json)；11672 的空驱散排除名单仅补全所选配置默认值。

战技与秘法碎片的官方类型集合及解码输入见 [skill-piece-types-source-manifest.json](skill-piece-types-source-manifest.json)。`scripts/prepare_skill_piece_types.py` 从所选 `884_156692` APK 解码 `game_settings` 与 `item`，核对 `SkillPieceTypes=[111,112]` 及 190 项战技碎片、186 项秘法碎片的原类型，并生成 `OfficialCharacterCatalog.SkillPieceTypes.cs` 的显式版本适配；原配置载荷与历史预期哈希保持各自身份。清单记录原枚举、道具判定、碎片列表、详情工厂、图标进度与 `SkillRankInfoParser` 初始品阶方法的代码输入，以及生成结果哈希。

六职业召唤模型来源：3181／3182／3183 按所选 `884_156692` 的 APK 优先解析顺序核对后，从同版已缓存官方 bundle 恢复完整骨骼、原 prefab 父链、Animator／AnimationController、材质、附件与阴影；复用 `prepare_battle_live_monsters.py`、`prepare_battle_live_monster_assets.py` 及原控制器／附件提取器，不使用早期采样骨骼。99 根骨骼、33 份动画、3181 的 3 个 IK 及 3182 的 8 个 IK／3 个路径约束保留在完整骨骼载荷中；全部原始输入、bundle／对象与转换输出 SHA-256 见 `battle-animation/live-monsters-source-manifest.json`、`monster-animator-bindings-source-manifest.json`、`monster-animation-controllers-source-manifest.json`、`monster-attachments-source-manifest.json` 和 `monster-additional-shadows-source-manifest.json`。新增 3181 阴影 prefab 固定为验证载荷的 LF 换行，防止 Windows 检出改变输出哈希。`Resources/Combat/NativeMonsters/catalog` 及原场景模型入口显式加载；新增素材尚未进入完整素材快照，实现与验证范围仅在[技能页](../../docs/character/skills.md)维护。

公共战斗持续状态的官方 `MyFightStatus`／`MyAction` 生命周期、既有状态与 3204 模型资源凭据，以及模型重建接线 SHA-256 见[状态与模型生命周期来源](battle-animation/status-model-lifecycle-source-manifest.json)，生成入口为 `scripts/record_battle_status_model_sources.py`。本项复用既有原生资产，未改写美术载荷；实现及验证范围见[公共战斗页](../../docs/combat/battle-execution.md)。

反射装甲 25035 原逻辑图、伤害结果定义、缺失服务端处理器与适配／验证身份见[反射装甲规则来源](battle-rules/reflection-armor-source-manifest.json)。原完整 25035 prefab 复用所选 884 的既有 APK 核对与原生导出缓存，输入、四项脚本重绑定及输出哈希见[补充状态实体来源](battle-animation/additional-status-assets-source-manifest.json)；原 11480／11481／11482 资产与声音复用[原生技能图来源](battle-animation/skill-native-graph-assets-source-manifest.json)。新增状态资产尚未生成素材快照；实现、单机确认与验证范围仅在[骑士页](../../docs/character/skills-knight.md#rank-3)维护。
