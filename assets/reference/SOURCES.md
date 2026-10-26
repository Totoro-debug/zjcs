# 素材来源与官方资源提取流程

[返回文档索引](../../docs/README.md)

## 当前官方输入与历史重建

当前默认输入由根目录 `official-source.json` 固定：原生 APK 为 `854_156383`，当前热更新五包为 `871_156598`，资源目录版本为 `202610211330871`。官网接口仍指向同一个 `zjcs_M509085.apk`；服务端大小、MD5 与包尾均匹配本地 APK，不能把热更新版本记成新安装包。原始云端文件全部保存在 `.tools/source-packages/cloud/runtime/202610211330871/`，ADB 模拟器原始缓存继续保留。

`python -B scripts/activate_official_source.py` 完整验证 APK 与云端文件后，更新五包提取视图及资源索引；当前共 20,999 项，云端 18,559 项无缺项。旧提取视图与旧索引保存在 `.tools/source-packages/history/854_156383/`，原 APK、旧 CDN 缓存及新版原始缓存均保留。`fetch_official_assets.py`、`official_manifest.py`、`prepare_unity_bundle_inputs.py` 默认选择当前版本，历史重建显式传入 `--manifest 854_156383`。读取 API 用 `Apk.current()` 或 `Apk(manifest=None)` 选择当前输入；既有 `Apk()` 暂保留历史版本，避免尚未迁移的转换器错误标注新旧来源。

原始输入切换与本次既有 Unity 素材的重新转换、绑定复核已经完成；未变化的输出逐字节保留，发生变化的输出及替换前字节分别记录。现有模块来源清单保留其真实转换版本，原版布局依据不会统一改名成新版。当前来源凭据见 [热更新原始来源](official-map/runtime-cloud-source-manifest.json)。实际运行范围见素材管理及所属功能页，不能据此声明整游戏完成。

### 当前转换载荷

当前 `871_156598` 的表格、战斗组件、材质、家园层级、中文和变化 Sprite 已转换到现有载荷；原版来源不会统一改名成新版。角色模型重导出保留 GUID，创角／人物网格包及男体原生 prefab 已核对新版索引；女体 451 个动画独立解码后与旧版一致。具体输入、旧文件备份、转换方式与输出哈希分别见 `character-creation/current-mesh-source-manifest.json`、`current-native-prefabs-source-manifest.json` 和 `official-map/current-*-source-manifest.json`。万圣节已接入媒体及既有事件路由逐字节一致，保留已有音频输出；新增路由及控制对象保留在完整原始缓存内。新版资源替换的运行验证范围见[素材管理](../../docs/asset-management.md#官方来源与转换)，功能完成状态仍以所属页为准。

战斗 Boss HP 层数表单独从固定 APK `854_156383` 的 `defaultpackage_assets_config_luban.bundle` 对象 `Assets/Config/Luban/monster_hp_plies.g.bytes` 解码，164 行输入 SHA256 为 `66b6496732f115b7370bc4fa3114d76c66f552de1fa73f70c06fa59b485e342e`。`python -B scripts/prepare_unity_combat_hp_plies.py` 通过固定版 `TbMonsterHpPliesInfo` 构造器生成 `unity/Assets/Official/Combat/monster-hp-plies.json`；输出、APK 对象、bundle 和转换版本记录在 [Boss HP 层数来源清单](official-map/monster-hp-plies-source-manifest.json)。

### 人物属性与公会建筑规则表

`python -B scripts/prepare_unity_character_catalog.py --catalog-only` 从固定 APK `854_156383` 的 Luban 输入生成 Unity 人物成长目录。该目录包含 166 条 `legion_map_skill`、498 条 `legion_map_skill_study_level`、12 条 `legion_map_building`、468 条 `legion_map_building_level`、75 条 `legion_map_building_level_hall` 和 10 条 `legion_map_hangup`；输入 SHA256 与官方 `Tb*` 构造器来源见[人物来源清单](../../unity/Assets/Official/Character/source-manifest.json)。`--catalog-only` 只更新人物配置并保留现有 Sprite 输出。

### 特殊物品原窗口与替换配置

`scripts/prepare_special_items.py` 从固定 `854_156383` APK／匹配 CDN 的 `prefabs_ui_equip` 读取远古砥石的装备页、词条行、候选、确认和结果 prefab，并从 `config_luban_equipattribute` 解码 `equip_attribute_replace_range.g.bytes` 的20条记录。运行数据为 `assets/official/special-items.json`，原精灵复用或无损转换，不绘制替代窗口。逐对象、原客户端／共用逻辑、配置与输出哈希登记在 `assets/reference/special-items-source-manifest.json`；服务端请求的本地替代及用户确认边界见行囊功能文档。

### 外观礼盒试穿模型与动作

`scripts/prepare_appearance_chest_preview.py` 从固定 `854_156383` APK／校验 CDN 的 `prefabs_ui_pay_appearancegift` 读取 `PreviewAppearanceDialog` 原窗口、绑定和 `PreviewAnimConfigItem` 动作序列；解码 `spine_skin`、`character_skin`、`character_skin_id_group`，核对69种礼盒奖励的男女皮肤及职业集合。武器读取原 `PlayerPart/*_UI_Integrated.prefab` 的 follower、变换和正反面切换，复用已核验的原男女骨骼及职业基础部件；图集按原包无损解码。

武器动作序列按原退出比例和 `AnimationState` 混合参数采样，循环段保留独立起点。运行数据位于 `assets/characters/item-preview/` 与 `assets/official/appearance-chest-preview.json`，不使用运行时骨骼扩展或自绘素材。逐对象、配置、原代码、复用输入及输出哈希见 `assets/reference/appearance-chest-preview/source-manifest.json`。

## 仓库与独立素材包

Git 保留本页、`LEGACY_SOURCES.md` 及代码目录中的提取／转换工具；官方转换素材、完整模块来源清单和哈希随独立素材包保存。当前功能／地图验收由[唯一总索引](../../docs/README.md)链接的子文档维护，历史测试过程通过Git追溯，素材包不保存独立验收记录。新工作区先按 [素材包管理](../../docs/asset-management.md) 恢复，才能查看下文模块凭据或运行来源核验。原始 APK、CDN 缓存及开发依赖不包含在转换素材包中。

当前路径及共享清单变化见 [迁移记录](../../asset-pack/migration.json)。旧记录中的 现对应 。素材包锁文件只证明包内容及代码配对，不改变下文官方版本、缺项、自绘与实际替换状态。

## 当前使用状态（2026-10-17）

当前素材包以 `asset-pack/lock.json` 指定的 ZIP 为准，包含现有转换素材与真实来源历史。旧独立验收文件、运行日志、截图及其仓库外归档已删除；下文按日期记录的源包、转换方法、输出哈希与实际替换历史保留，历史包的未纳入状态不代表当前包仍有同一缺项。

2026-10-15 NPC 好感属性、档案与置顶接续使用固定 `854_156383`：`npc-cultivation.json` 独立保存原 18 类等级属性及 385 条档案配置与中文；`npc-cultivation-ui.json` 保留原 NPC 面板字段、33 组件绑定及 125 布局字段凭据。图片复用官方 Sprite，仅登记原任务图集 `ImgBg_Countdown`，没有自绘。独立数据在运行时合并，不重跑或改写旧全量规则／界面目录；全部新增路径进入显式加载清单。

同次登记战斗 UI 的 7 个原 prefab、29 个组件绑定及 48 个 Sprite，输入与输出见 [战斗 UI 来源](combat-ui/source-manifest.json)。这部分仅完成转换及显式加载登记，原战斗界面和完整流程仍在实施，不能因已入素材包声明功能完成。

固定版战斗 world UI 中的 `BattleCustomDamageFloatEffect_Scene`（bundle `14e38fc9585d8e00567ae2f9f4b0e8bb`，对象 `-2976502348647371213`）由 `scripts/prepare_unity_battle_assets.py --runtime-hud-only` 转为 Windows 原生 3D TMP／SpriteRenderer 节点数据，按 Wwise 语言包生成 `EStatusCustomAction` 文案。输入 `world-ui.json` SHA256 为 `5c9a4daa0c4c9722df2ecbcbbe735749de491f423c2aef24d5010bfcd886bdf4`，运行载荷与输出哈希见 `unity/Assets/Game/Resources/Combat/battle-custom-action-world-runtime-source-manifest.json`；该 UI 的运行验证状态见[战斗执行](../../docs/features/battle-execution.md)。

自定义战斗飘字 TMP 组件的 `m_fontAsset` PPtr（path ID `-1054575886599457342`）解析到固定版字体依赖 `CAB-43221b4d126447a2b398f3477e814a2c`，名称为 `FZY4K SDF - dynamic`；其 bundle SHA-256 为 `ebee921d1efee60b02445c5d09faebdef159fdd6c7284658d2c05cf9e3826434`。运行时 HUD 绑定 `FZY4K-Dynamic.asset`（GUID `e9b69727f25b00e4c82f8bc68bd859c5`），该资产的源 TTF GUID `6318d84275f8ea84bb687a21c99efcf7` 与官方 `FZY4K.ttf` SHA-256 `90023872dffe6a58503de655bd0631bf3334ce3b6b72cc75a6625b685c94c386` 已由专项测试校验。

运行时使用固定 `854_156383` APK／校验 CDN 的角色、家园、界面、区域地形、模型、动画、粒子和音频。大陆已接入区域探索、主线解释器与地图活动，古遗物、炼金炉、伙伴和幻兽专用界面也已落地。当前功能与剩余边界统一见 [功能索引](../../docs/README.md)；本页后续按日期保留的“缺包／未接入”和数量是当时阶段记录。

原版本 185 个缺项仍保留。新版 `864_156496` 同名包中适用的同 CAB 对象，已经用于场景粒子、UI 粒子和音乐节舞台依赖，分别登记于 [场景粒子新版凭据](official-map/particle-counterparts-source-manifest.json)、[UI 粒子新版凭据](official-map/ui-particle-counterparts-source-manifest.json) 及 [舞台来源](official-map/music-stage-source-manifest.json)。仅按独立哈希和依赖核验使用，不覆盖原版本缓存。

当前资源总数以生成的全局清单和本次核查为准；旧阶段统计不能相加。官方原包、有效缓存、转换依赖与历史来源保留；已完成的临时提交／导出快照和过期日志可清理。

2026-10-13 设置功能补齐：`prepare_settings_assets.py` 从固定 `854_156383` 的 `SettingsDialog.prefab` 重建五个分页、原始控件和选中标记，并读取固定版本中文语言分包替换俄文 / 数字预览文本。分辨率、帧率和长数字选项按原组件的序列化枚举绑定核对。点击音效取自 APK 内 `Minimum.pck`：由原 `UIClickSound` 引用定位 `Main_Tag` 事件（680474747），经 HIRC 解析到 media 69663821，再用 vgmstream 解码为 `assets/official/settings/click.wav`。无自绘或合成音频；图片复用官方 Sprite，独立 UI 数据位于 `assets/official/settings/ui.json`。输入来源、bundle / 对象 ID、语言文件哈希及输出哈希见 [设置来源清单](settings/source-manifest.json)。主城运行载荷由 `scripts/prepare_unity_home_settings.py` 从该官方树生成，复制30张原 Sprite 与点击音效并将输出哈希登记在同一清单。运行边界见 [设置功能](../../docs/features/settings-and-save.md)；URP画质、反射和录像尚未移植，不能因官方资源已取得而声明完成。

2026-10-12 大陆总览移植：新增 `prepare_official_maps.py`，直接从固定 APK 转换 `WorldMapIsland`、`WorldMapSky`、`WorldMapSpace` 的 RectTransform、原图、九个地点 ID、背景自适应和缩放参数。登记 33 个原始 Sprite 与 3 张星球 Spine 图集，其中实际新增 29 张 PNG；其余图像复用既有文件。原始数据、输出哈希、bundle / 对象 ID、完整消费的三张地图配置表和效果缺口见 [地图来源清单](official-map/source-manifest.json)。

按名称筛选的地图相关 1,438 个 bundle 候选中，23 个在 APK ZIP 内，另外 1,415 个现已从官方 CDN 补齐；九个地区的实际地形 bundle 全部未打包，现均有经过校验的云端缓存。Unity 专用海浪、迷雾、扭曲材质与 29 个粒子系统尚未转换，渲染通道专用黑白遮罩不作为普通图片叠到地图上。当前提供原始地图总览浏览，不代表区域探索、解锁与地图战斗已经移植。无新增自绘、截图衍生或生成图片。

地图重建命令为 `python scripts/prepare_official_maps.py`，复用本页既有 UnityPy / Spine 开发依赖和固定 APK 校验，通过 APK ZIP 按需解析实际依赖；中间骨骼与采样数据放在 `.tools/source-packages/map-skeletons/`。运行时数据为 `assets/official/maps.json`，新图像纳入 Unity 显式加载清单，来源记录由 `verify_official_assets.py` 校验。

2026-10-13 官方云端补充分包：按用户授权解析 SDK 并观察官方客户端初始化，确认渠道 `leiting`、渠道编号 `110001`、推广标识 `M509085`、SDK `v3.4.25.1`。实际服务器配置为 [110001_Main.dis](https://zhangjcsupdate.leiting.com/zhangjcs/version/release/110001_Main.dis)，资源路径为 `https://zhangjcsdl.leiting.com/patch/{ResVersion}/Android/{Package}/{FileHash}.bundle`，备用域名为 `zhangjcsdl.roguelike.com`；`Android` 区分大小写。

固定 APK 的五个清单共 20,994 个文件，ZIP 内 2,572 个，缺失 18,422 个（2,473,539,303 字节）。已下载并按原清单大小 / MD5 校验取得 18,237 个（2,473,076,062 字节），同时记录 SHA256 与实际来源 URL。九个地区的原始地形包均已补齐；这些文件来自官方 CDN，不能记成 APK ZIP 内提取。剩余 185 个文件共 463,241 字节，在配置公开的三个资源目录及两个 CDN 域名均返回 404，继续登记原版本待补齐。完整依据、缓存凭据哈希及缺项见 [云端调查记录](official-map/cloud-discovery.json)。

另已下载官方 manifest `864_156496` 的 185 个同名包，保存在 `.tools/source-packages/cloud-research/missing-counterparts/{MD5}.bundle`。文件对应关系、独立哈希、URL 与内部资源路径见 [缺项用途核对](official-map/missing-resource-classification.json)，定位时使用 `counterpart_md5`。按新版内容推断，涉及 174 个通用特效包、9 个地图特效包、1 个活动 UI 特效包和 1 个活动小游戏配置包。早期仅用于核对；现在已有部分依赖独立转换并接入，见本页当前状态。其余包仍须核对内容、依赖和真实版本后使用。新版文件不能混入原版本缓存或记成原版本补齐。

使用 `python scripts/official_manifest.py --verify-packaged` 重建完整资源索引并验证 APK 内文件；`python scripts/fetch_official_cloud_assets.py` 补齐 / 复核缺失文件。缓存位于 `.tools/source-packages/cloud/854_156383/`，`receipts.jsonl` 逐项保存 URL、大小、MD5、SHA256，`download-report.json` 保存失败文件及所有请求结果。提取命令优先读取 APK ZIP，再读取已校验且有来源凭据的云端缓存；`official_apk.py` 也支持离线读取该缓存及 CAB 引用。

### ADB 客户端热更新与全量云端缓存

通过 ADB 在 Android 11 Google APIs 模拟器中运行固定 APK，沿用 ARMv7 翻译与安装目录内的原 ARM `libnesec.so` 库名适配；安装后的 `base.apk` SHA256 仍为本页固定值。模拟器使用宿主 GPU 与 `-feature GLESDynamicVersion`，实际显示官方启动画面并下载首批更新。客户端选中的 `ResVersion` 为 `202610211330871`，五包清单均为 `871_156598`；ADB 读取的五份已激活 V3 清单 SHA256 与电脑缓存逐包一致。客户端首批更新约 122.59 MB，随后在“下载内容”选择完整资源 2,362.60 MB；实际下载完成后，原 `PlayAsYouDownloadClientData.Downloaded` 为 `true`，日志确认 `Init yooasset[Full] process finish!`。模拟器缓存的全部 18,559 个所需云端文件，大小与 SHA256 均逐项匹配电脑原始缓存，缺项、大小不符和 SHA256 不符均为 0。期间官方主 CDN 存在请求超时，客户端重试及备用域名最终完成下载。

`python -B scripts/fetch_official_runtime_assets.py --expected-resource-version 202610211330871` 按实际服务器配置获取 Boot、Launch、HybridCLR、DefaultPackage、WwiseBnk 的 `.version`、`.v3.hash` 和 `.v3.bytes`，校验清单 MD5 后遍历完整 BundleTable。共 20,999 个条目，2,440 个 APK 内条目逐项通过大小与 MD5 校验，另 18,559 个云端文件共 2,605,908,370 字节，已全部按大小、MD5 与凭据 SHA256 校验，失败数为 0；包含完整音频及边玩边下资源。本次从官方 CDN 新下载 648 个文件（221,975,904 字节），另 17,911 个同哈希文件（2,383,932,466 字节）复用核验通过的原版本缓存，以硬链接或复制保存。已有缓存使用 `python -B scripts/fetch_official_runtime_assets.py --verify-cache 202610211330871` 离线复核，逐包核对保存清单的版本、大小、MD5 与 SHA256，并完整检查 APK 内条目及所有云端文件，无须再次访问 CDN。

新版原始缓存独立位于 `.tools/source-packages/cloud/runtime/202610211330871/`，包含五包原清单、`inventory.json`、`receipts.jsonl` 与下载结果。复用文件保留真实旧版来源 URL、原下载时间及 `source_manifest`，新下载文件保存当前官方 URL；完整输入、清单哈希与 ADB 观察依据见 [客户端热更新来源清单](official-map/runtime-cloud-source-manifest.json)。这些记录说明原始资源已取得；固定 `854_156383` 的 185 个旧哈希缺项、既有提取入口及 Unity 运行资源版本保持各自来源状态，新版的转换、兼容性与加载绑定仍须按素材流程逐项处理。原始缓存不在转换素材 ZIP 快照内。

按用户要求长期保留原始下载缓存，供后续解压、转换与来源核对：固定 APK、`cloud/854_156383/`、`cloud/runtime/202610211330871/`、历史新版同名包、五包原清单及逐文件来源凭据均保存在 `.tools/source-packages/`；模拟器完整下载数据保存在 `.tools/official-android-probe/avd/zjcs-official-probe.avd/` 的持久磁盘中，原始清单调查缓存也保留。日常临时产物清理不得删除这些原始输入。尚未建立仓库外备份或原始缓存快照，不将本地保留表述为已备份。

本页索引：[官方来源与缺失补充](#官方素材来源与缺失补充) · [提取约定](#提取与整理) · [男性角色重建](#已完成男性角色原始资源) · [场景与界面重建](#场景与界面的完整重建) · [旧素材替换](#现存旧素材的-apk-替换清单) · [历史整理](#已完成的整理记录)

2026-10-12 全界面自查：检查 78 个界面状态，以及 APK 全部 434 个 UI bundle；其中 72 个实际打包，含 1,802 个 prefab。新增接回初始职业选择、料理使用详情和古遗物详情，UI 目录现有 83 个 prefab、1,482 项显式素材资源。运行时 1,510 张图片全部来自 APK，包含另行登记的 30 张角色贴图 / 遮罩；没有在用的自绘或截图衍生图片。清理 28 个旧布局函数、3 个无产品导航调用的旧菜单及 9 个无用文件。

此前界面与主城交互修复阶段接入 56 个 UI prefab、1,167 项资源和 892 项图标映射。`prepare_official_panels.py` 同时解析 Unity Animation / SimpleAnimation、Animator 压缩标量曲线，以及 StateSpriteAssetProxyConfig 的 GUID 引用。动画保留来源、关键帧和 Hermite 切线；不支持的绑定如实保留，不将本地布局适配称为完整原作界面。

男性角色另接入原始 `home/idle_01`，按 24 fps 转换为原生网格采样，来源和输出哈希见 [idle-animation.json](male-remodel/idle-animation.json)。正式头像按原始 PlayerPortraitSettings 与 GraphicCapsuleMeshClipper 裁切。初始职业选择页已接入 APK 的 Login/CreateRoleSelectProfessionPanel，删除已替代的 meadow.svg 和 portrait.png；旧存档 ID 仍可迁移，视觉统一使用原始男性网格。完整战斗动作、幻兽模型、粒子与复杂材质仍有缺口。

2026-10-12 玩法补齐：新增 `prepare_gameplay_assets.py`，复用固定 APK 校验、Sprite 解码与来源清单，实际新增 122 个运行时图像资源，总清单 692 项。物品 ID 图标目录共 232 项，同图标物品不重复造图。来源对象、bundle、哈希与处理方式见 `official-city/source-manifest.json`；候选未找到的图标记入 `gameplay_icons_not_packaged`。默认古遗物池图标已检索但未取得，游戏仅显示原始物品名称，没有代用图。

配置扩展到 67 张完整消费的官方表，输入 / 输出哈希见 `official-city/rules-manifest.json`。实际功能与结算适配见 [功能模块索引](../../docs/README.md)。

素材约定更新：2026-10-13。角色、场景、UI 图像、图标、动画、音乐和音效的新增及替换，优先使用本地官方 APK 与已下载的官方资源；只有所需官方素材确实缺失时才允许自行绘制补充。完整约定及操作流程以根目录 [AGENTS.md](../../AGENTS.md) 为准。

创角色板来源：缓存 APK 内 `character_skin_color.g.bytes` 与 `character_skin_dye.g.bytes`，由 `prepare_official_rules.py` 按 Console.dll 中的 Luban 构造器完整解码。运行时按照 `FixedColorIdList` 和 `FixedColorItem` 的 HSVToRGB 转换使用原有顺序，肤色 9 种、发色 7 种、瞳色 7 种；输入和输出哈希见 [rules-manifest.json](official-city/rules-manifest.json)。旧存档的合法 RGB 值保留以兼容已有形象，新界面只提供上述固定色板。

## 官方素材来源与缺失补充

### 2026-10-13 图鉴接入

按原版 `CollectBookPanel` 接入技能、古遗物两类图鉴。`python scripts/prepare_codex_assets.py` 使用固定 manifest `854_156383` 的 `prefabs_ui_collectbook`（原版本官方 CDN）、`image_ui_collectbook`、原始 ItemIcon / Map23 图标及其 CAB 依赖，转换 17 个原始 prefab 和裁去非当前页面的 shell 变体。输出 `assets/official/codex-ui.json`、`codex.json`、`codex-props.json` 及对应原始图片，实际载入 376 个玩家技能和 1,382 个古遗物条目的完整图标；本次未使用新版同名包或自绘补充。

职业归属来自原始 profession_base 的 Skills 和玩家 SkillUserType，元素来自 `config_ec/SkillLiteInfoPack.g.bytes`，奖池与开放条件来自现有原始规则表，套装另从 `config_luban/treasure_suit.g.bytes` 完整消费解码，收藏基础属性使用原版本 `level_prop.g.bytes`。中文文本使用已校验 WwiseBnk 两份原始中文语言表。每个 prefab 的 APK / CDN 来源、URL、bundle / 对象、图像处理方式、输入 / 输出哈希、语言凭据与原版行为源码哈希见 [图鉴来源清单](codex/source-manifest.json)；动态图片同时登记全局来源清单和 。

仅图鉴已改用完整官方图标目录；历史收藏 / 养成页的还原程度按各自记录保留。图鉴现已迁移原FancyScrollView书封惯性／吸附、技能新标记清理和古遗物真实属性推导；动态列表／分类翻页及完整技能详情仍有缺项，当前范围见[图鉴验收](../../docs/features/codex.md)。专项验证命令：`python scripts/verify_codex_assets.py`。

| 项目 | 固定记录 |
| --- | --- |
| 本地缓存 | `.tools/source-packages/zjcs_M509085.apk` |
| 官方下载地址 | [zjcs_M509085.apk](https://zhangjcsdlf.leiting.com/full/zjcs_M509085.apk) |
| 下载地址来源 | [杖剑传说官网](https://zjcs.leiting.com/) 的配置接口 `https://zjcs.leiting.com/client/gameV2/home` |
| 大小 | 1,024,203,578 字节 |
| SHA256 | `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9` |
| manifest | `854_156383` |
| 官方 CDN 补充入口 | [110001_Main.dis](https://zhangjcsupdate.leiting.com/zhangjcs/version/release/110001_Main.dis)，仅接受固定 manifest 中相同大小及 MD5 的文件 |
| CDN 本地缓存及逐文件凭据 | `.tools/source-packages/cloud/854_156383/`、`receipts.jsonl` |
| 新版官方同名包缓存 | `.tools/source-packages/cloud-research/missing-counterparts/`，manifest `864_156496`；对应关系与凭据见 [缺项用途核对](official-map/missing-resource-classification.json) |
| 本地缓存元数据 | `.tools/source-packages/source.json` |
| 已落地角色资源及哈希 | [male-remodel/source-manifest.json](male-remodel/source-manifest.json) |

缓存有效时直接复用，不重复下载。工具会校验固定 SHA256，解压所选 ZIP 条目时校验 CRC；校验失败须检查文件与来源，不可修改预期哈希绕过检查。

新增及替换素材优先从此 APK 和经过校验的原版本 CDN 缓存提取；原版本缺失时先核对新版官方同名包，适用时单独转换并记录版本与来源。上述官方来源确实缺失所需素材时，记录检索范围、资源名和缺失原因后才允许自行绘制补充，须另记绘制方式、输出路径及哈希，不能登记为官方素材；官方资源补齐后优先替换。已有资源尚未解码、转换或接入不算缺失。界面布局、交互、本地规则及必要的资源转换由项目代码实现。

## 提取与整理

1. 在 `.tools/source-packages/extracted/bundle-index.json` 查找资源对应的 bundle 名称。索引记录 manifest 名称与 bundle 哈希；索引存在不代表资源已在基础 APK 中。
2. 使用 `python scripts/fetch_official_assets.py --extract '<bundle 名称正则>'` 按需提取到 `.tools/source-packages/extracted/`，`bundle-sources.json` 记录 APK ZIP / CDN 来源。工具输出 `Not packaged or cached` 时，记录原版本缺项并核对新版同名包。现有命令和 `official_apk.py` 不会自动加载新版包，使用新版时须依据独立记录定位文件，用 UnityPy 单独解析并核对依赖。
允许裁切、透明边缘整理、格式转换、原始遮罩染色和网格转换；处理过程保留可追溯的原始资源身份。
4. 在本文件和对应来源清单登记 APK 或资源 manifest 版本 / 哈希、bundle、原始对象名、目标路径、处理方法与输出哈希；云端补充分包另记 `origin: official_cdn`、实际 URL、`bundle_size` 与 `bundle_sha256`，避免与转换后文件的 `sha256` 混淆。自绘补充单独登记缺失依据及绘制方式。仅将实际使用的资源放入运行时目录，动态资源纳入显式加载清单。
5. 替换后检查中文界面、视觉效果、旧头像 / 稳定 ID 兼容及 Windows 导出，再删除对应旧资源。

完整 APK、提取缓存及工具留在已忽略的 `.tools/`；游戏运行只使用已转换、打包的资源，无需 Python、Node 或网络。

## 已完成：男性角色原始资源

当前角色使用以下 7 个 bundle，均已从上述 APK 缓存提取并验证重建：

| bundle 名称（均带 `defaultpackage_assets_appearanceassets_` 前缀） | 用途 |
| --- | --- |
| `character_model_male_1.bundle` | 男性 Spine 骨骼与 atlas 描述 |
| `character_model_male_1_atlas_default.bundle` | 男性基础衣装、发型及面部贴图 |
| `character_model_male_1_colormap_default.bundle` | 对应染色遮罩 |
| `character_model_common.bundle` | 共用角色资源描述 |
| `character_model_common_atlas_default.bundle` | 共用五官、妆容及特征贴图 |
| `character_model_common_colormap_default.bundle` | 共用染色遮罩 |
| `character_pweapon_atlas_default.bundle` | 初始剑 / 杖贴图 |

以下为历史男角色转换流程及依赖记录，保留其原素材来源和产物说明，不代表当前 Unity 已接入或完成验收。用户已确认改走[Spine 数据转换为 Unity 原生动画方案](../../docs/asset-management.md#spine-角色动画迁移决策)。历史使用的 Spine Core 4.2.110 受 Spine runtime 许可约束，不作为当前免费方案的必装依赖；以下 npm 命令和旧转换入口仅作追溯，后续须由独立转换链替代，替换状态与实际输出哈希按模块登记。

历史开发依赖安装命令：

```powershell
python -m pip install --target .tools/character-art-python UnityPy==1.25.4 numpy pycryptodome
npm install --prefix .tools/character-art-node @esotericsoftware/spine-core@4.2.110
```

历史男角色提取与重建命令（提取可复用，旧转换脚本的 Spine 依赖尚待替换）：

```powershell
python scripts/fetch_official_assets.py --extract 'character_model_(male_1|common)(_atlas_default|_colormap_default)?\.bundle$|character_pweapon_atlas_default\.bundle$'
python scripts/prepare_original_character.py
```

输出为 `assets/characters/original/` 中的 17 张原始贴图、13 张染色遮罩及 `assets/characters/original/meshes.json` 中的 36 组部件 / 126 个网格。统一逻辑画布为 320×400，原作姿势按 2 倍坐标保留精度。

 接入原始红 / 绿遮罩，分别处理肤色、发色和瞳色。大预览、缩略图、正式头像及战报共享原始部件组合。当前为男性基础衣装、6 款发型，各 6 款眉 / 眼 / 嘴、3 款妆容、5 款特征及「无」、剑 / 杖；显示名、首期数量和静态武器锚点属于本地配置。旧 `freckles` ID 为兼容保留，对应实际暖橙妆。已将 `home/idle_01` 的 65 个采样帧转换为原生网格，取代本地正弦浮动；完整行走和战斗动画仍待转换。

## 场景与界面的完整重建

除上述 Python / Node 依赖，场景和界面转换使用 .NET 10、ILSpyCmd 11.1.0.9782 与 vgmstream r2117。工具用于解码和静态检查，不是素材来源。已有缓存与工具直接复用，首次安装缺少的工具时执行：

```powershell
Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/ilspycmd/11.1.0.9782/ilspycmd.11.1.0.9782.nupkg' -OutFile .tools/ilspycmd.zip
Expand-Archive .tools/ilspycmd.zip -DestinationPath .tools/ilspycmd -Force
gh release download r2117 --repo vgmstream/vgmstream -p vgmstream-win64.zip -D .tools/vgmstream
Expand-Archive .tools/vgmstream/vgmstream-win64.zip -DestinationPath .tools/vgmstream -Force
```

ILSpyCmd 默认位置为 `.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll`，可用 `--ilspy` 指定；音频解码器为 `.tools/vgmstream/vgmstream-cli.exe`。先完成上一节男性角色提取与转换，再按以下顺序重建。`--dotnet` 替换为实际 .NET 路径，`--decompile-client` 额外导出审计源。

```powershell
python scripts/fetch_official_assets.py --extract 'config_luban\.bundle$|scenes_main\.bundle$'
python scripts/prepare_official_sources.py --dotnet 'D:\Program Files\dotnet\dotnet.exe' --decompile-client
python scripts/prepare_official_rules.py
python scripts/prepare_official_audio.py --decoder .tools/vgmstream/vgmstream-cli.exe
python scripts/prepare_official_city.py
python scripts/prepare_gameplay_assets.py
python scripts/prepare_official_panels.py
python scripts/prepare_optional_interfaces.py
python scripts/prepare_dungeon_assets.py
```

`official_apk.py` 按需读取 bundle 与 CAB 依赖，不要求重新下载或预先解包整个 APK。

## 现存旧素材的 APK 替换清单

以下是当前文件的实际状态；来源约定生效不等于这些文件已经完成替换。后续优先使用上述 APK 及官方下载资源替换，完成后补充实际版本、bundle、对象名和哈希；确实缺失时的自绘补充按上述约定登记。

| 范围 / 位置 | 当前实际来源与状态 | 按统一约定需完成的替换 |
| --- | --- | --- |
| 创角界面 | 已接入 APK 的 CreatePlayerPanel：背景、主 / 次分类、选项框、固定色板、命名、随机 / 重置及导航控件；旧 meadow.svg 及导入文件已删除 | 动态款式缩略图及角色预览为原生网格接入；初始职业选择页已接入原始 Login prefab，按离线规则保留两职业；选武器 / 初始岛步骤已按需求删除 |
| 主城 / 家园 `assets/city/reference/` | 场景、设施、图标已实际替换为 `assets/official/` 原始资源；旧文件已移出源目录；旧 `portrait.png` 已删除，兼容存档改用原始男性网格 | 旧头像兼容、尚缺的场景效果及原始交互继续按实际状态追踪 |
| 原探索界面 `assets/ui/*.png` | 早期截图及独立探索脚本 / 场景已移出源目录 | 已接入原区域场景、探索和本地战斗；完整主线验收及原实体战斗仍待完成 |
| 原场景背景音乐，| 自绘图标脚本已移出源目录；主城使用原始 Sprite 图标 | 先核对 APK 与官方下载资源；确实缺失时才可自绘并登记来源 |
| 原 背景音乐 | 自定义主城脚本及其合成音乐已移出源目录；实际播放 APK 的 `Home_BGM_01` | 点击音效、地图音乐与剧情配音已接入；部分容器缺项见音频清单 |
| 幻兽、遗物、藏品与角色动画 | 原图、模型、技能／遗物图鉴及地图／剧情角色动画已接入 | 部分特殊表现、完整实体战斗与个别缺失图像继续按各模块清单追踪 |

历史图片、网页和视频的真实来源及早期处理记录归档于 [LEGACY_SOURCES.md](LEGACY_SOURCES.md)，只用于旧资源追溯和已有流程证据。`scripts/prepare_city_art.py`、`scripts/prepare_reference_assets.py` 及会生成重绘背景的 `scripts/prepare_character_art.py` 已移出源目录；当前角色重建使用 `prepare_original_character.py`。

## 已接入：官方家园、UI、配置与音乐

主城从 挂载 。首轮迁移实际提取 542 个 Sprite、26 张家园骨骼 atlas、原始 FZY4K 字体和家园音乐，当时共 570 项显式加载资源；转换 384 个场景图层、24 组骨骼、34 个 UI prefab 及 42 张完整 Luban 配置表。图像仅进行原始 Sprite / atlas 解码、裁切、旋转还原和格式转换；音乐从原始 Wwise 包解码为 PCM WAV，没有合成替换。

| 来源范围 | 实际使用的 APK 资源 |
| --- | --- |
| 家园 | `prefabs_home_dynamic_theme_*`、`prefabs_home_dynamic_furniture_*`、`scenes_main` 的原始对象及依赖图集 |
| UI | `prefabs_ui_home/furniture/public/functionentry/setup/character/playerbagpanel/gachapanel/skill/pet/shop/task/mainlandpanel/top/message/itemdetail/item/common` 的指定原始 prefab |
| 图标 | 原始 `image_itemicon_*` 中按 `item.RawIcon` 定位的 Sprite；缺少的七个商店商品图标没有替代图 |
| 字体 | `defaultpackage_assets_textmesh_pro_rawfonts_fzy4k.bundle` / `FZY4K` TextAsset 原始 TTF |
| 配置 | `defaultpackage_assets_config_luban.bundle` 中原始二进制表；根据 APK `Console.dll` 生成的 Luban 构造函数解码并严格校验消费字节数 |
| 音乐 | `wwisebnk_assets_wwisedata_android_home_bgm_01_pck.rawfile`，bundle hash `91efbabff5375669114cd914b7e6fa6c`，media `946883943` |

逐资源身份和输出 SHA256 见 [official-city/source-manifest.json](official-city/source-manifest.json)，配置证据见 [rules-manifest.json](official-city/rules-manifest.json)，程序集证据见 [code-manifest.json](official-city/code-manifest.json)，音乐见 [audio-manifest.json](official-city/audio-manifest.json)，基础包可用性见 [bundle-availability.json](official-city/bundle-availability.json)。

2026-10-23：家园运行层从既有 `assets/official/home.json`、`home-completion/{scene,ui}.json`、`rules.json`、`home-random-rewards/catalog.json`、`home-mail/ui.json`、`home-tutorial/talk.json` 等已登记输出整理出 `unity/Assets/StreamingAssets/Home/{scene,ui,rules,rewards,mail,actors,environment,tips}.json` 并复制被引用贴图。主城 FirstMergeItem 固定奖励 ID 7 的物品图标同时取自已按 SHA256 登记的固定奖励表，奖励 ID、物品 ID、输入哈希及每个输出哈希记录在 Home `source-manifest.json`。生成器为 [`prepare_unity_home_runtime.py`](../../scripts/prepare_unity_home_runtime.py)；该步骤不重新解析 APK、不新增或替换美术，运行时按显式清单加载这些本地文件。

`.tools/` 保留官方缓存和提取依赖，游戏运行不加载 DLL，不使用 Python / Node / vgmstream。

**尚未完整还原全部功能。后续来源记录中的阶段数量用于追溯，不替代当前功能说明。

2026-10-12 追加提取：原始 `ShopActivityEntryPanel`（含直购、两款月卡）、`PayDirectPayItem`、`ActivityEntryPanel`、`ActivityCommonEntry`、祈愿职业选择、积分奖励及商品详情等 13 个 prefab；`direct_pay`、`periodic_card_pay`、`money_cost`、每日 / 每周礼包、签到奖励及活动入口共 8 张配置表。新增图标仍按原始 RawIcon 定位；`prepare_optional_interfaces.py` 从 `image_ui_chestgacha_chestelementgacha_atlas` 和 `image_ui_treasure` 提取原始 Sprite。当前共 1,317 项运行时资源、1,052 项图标映射、69 个 prefab、75 张完整配置表。每项身份和输出哈希均记录于来源清单，动态图片进入显式加载清单。

完整 `defaultpackage_assets_prefabs_ui_chestgacha.bundle` 及 `image_ui_chestgacha_chestgachabg_*` 背景仍未打包，不能称为已取得完整原界面。家园食材 `item_8051` 至 `item_8057` 分别位于未打包的 `image_itemicon_materials_map12/13/14/17/19/21/23_noatlas.bundle`，保留文字、价格和原始解锁锁头；没有替代图片。两类缺项分别记录于 source-manifest.json 的 `chest_gacha_interface` 和 `shop_ingredient_icons`。活动内容采用本地入口与七日签到布局，月卡目前只实现立即 / 每日奖励及有效期，未宣称额外属性特权生效。

## 已完成的整理记录

[#6](https://github.com/Totoro-debug/zjcs/issues/6) 完成男性角色原始资源接入；[#7](https://github.com/Totoro-debug/zjcs/issues/7) 清理已替代旧稿、研究中间物和临时 EXE。当前角色转换只生成实际使用的 30 张贴图和遮罩，来源图片不进入 Windows 导出。

历史收尾曾移出 105 个旧代码、旧图片、重复测试及缓存文件，共 5,258,455 字节；旧清理报告和恢复备份现已删除，现行清理规则见[项目约定](../../AGENTS.md#恢复更新与清理)。完整 APK、当前转换依赖、来源记录保留。当时保留的旧头像与重绘创角背景已实际替换并删除。旧主城玩法执行逻辑已移除； / 仅保留旧存档结构和兼容读取所需内容。

## 游记与副本的可用资源

2026-10-12：`prepare_dungeon_assets.py` 从固定 APK 的 `image_ui_notes_dailydungeon_map_11`、`image_ui_notes_mopup` 提取原始 Sprite：日常场景、建筑、列表缩略图、秘境工具及卡片底图；按 StageType 18 的 `Stage.Icon` 提取八张关卡图。仅解码、裁切和格式转换，新增 12 项实际使用的运行时资源，显式加载清单同步更新。映射见 `assets/official/dungeons.json`，逐资源 bundle / 对象 / 输出哈希见 source-manifest.json。当前共 1,329 项资源、69 个 prefab、77 张完整配置表。

历史核查（2026-10-12）只检查 APK ZIP，因而将游记插画和专用面板记为未打包，并使用本地卡片与物品图标。2026-10-13 已通过固定 manifest `854_156383` 的校验 CDN 缓存实际转换并接入 77 个专用 prefab、662 个相关图片对象和 23 张配置表，替换游记首页、日常副本、四类采集场、幻想阶梯、羁绊冒险及每日 / 每周 / 竞技玩法入口；原采集配置的 GUID 绑定复用 50 份已转换的官方怪物网格，圣兽原始 UI 模型配置另复用七份；龙神的 `TempRTUI.RTTarget` 按原引用解析为原生网格，替代 Unity 专用渲染纹理相机。历史缺项保留在总清单 `resolved` 的 `previous` 中，不能再据此声称官方资源缺失。本次未使用自绘素材。

## 竞技场守卫配置与匹配规则

`scripts/prepare_battle_arena.py --runtime --verify-apk` 按固定 APK `854_156383` 校验并完整解码 `arena_guard_template`、`arena_rank_template`、`arena_match_rule`、`arena_match_strategy`、`arena_score_ratio`、`arena_daily_award`、`arena_season_award` 与 `item_count_limit` 八张原表。运行配置为 `assets/official/arena-rules.json`；守卫记录包含原始 `AppendAppearances` 的性别分组及皮肤类型／ID。各表源 bundle、对象路径、Luban 构造器、输入字节哈希及当前输出哈希见 [arena-source-manifest.json](arena-source-manifest.json)。竞技面板继续使用已转换的原 `ArenaPanel` prefab；未从静态守卫模板推断真人占位或赛季服务端记录。

## 人物、技能及装备界面修复

2026-10-12：追加转换固定 APK 中的 `CharacterPanelEquipComparePanel`、`CharacterPanelEquipListItem`、`ItemIconSkill`、`ItemIconSkillPiece`、`ItemIconEquip`、`ItemIconEmpty` 六个 prefab。重复同名的比较栏和按钮容器保留为独立路径，避免覆盖；按 `LanguageUtils.GetItemDetailQualitySmallBg` 提取七档 `Img{Quality}_PopUps_Small` 原图。新增 16 项运行时图片，当前共 1,345 项资源、1,059 项图标映射、75 个 prefab、77 张完整配置表。所有图片均为 APK 原始 Sprite 解码与裁切，清单和显式加载同步更新。

头像使用原始 `ImgBg_MainHp` / `ImgLoad_MainHp`：原图为 152×133 的圆弧裁切，圆心在 (76,76)，底部缺口属于原图。修正 uGUI Image 的径向填充字段读取，使用实际升级进度。技能和装备图标按原始 Mask 的 Sprite alpha 裁切后代图像，即使 showMaskGraphic 关闭也保留遮罩作用。人物面板按 SkeletonGraphic 的原始骨骼单位与底部轴心显示，保留 prefab 的缩放，避免按布局矩形再次缩小。

核对 `CharacterPanelPropPanel` / `CharacterPanelEquipItem` 客户端代码与 prefab 后，移除此前本地额外添加的人物页行囊、祝福、启程补给按钮；这不构成对线上最新版本 UI 的确认。装备槽接入原始按部位筛选、单栏穿戴及双栏比较弹窗，详情属性与操作继续绑定现有离线规则，未实现的原始装备系统不以示例字段显示。

## 仓库、烹饪及古遗物界面修复

2026-10-12：从同一固定 APK 追加转换 `ItemIconCooking`、`ItemIconGem`、`ItemIconTreasurePanel`、`ItemIconTreasurePiecePanel`、`Common/Slider/SetCountSlider` 五个原始 prefab。`prepare_optional_interfaces.py` 从 `image_ui_common` 提取六档可取得的古遗物品质框；`prepare_gameplay_assets.py` 将料理与古遗物 / 碎片纳入完整配置范围，按 RawIcon 定位并提取实际打包图标。新增 89 项运行时资源、150 项图标映射；当前为 1,434 项资源、1,209 项图标映射、80 个 prefab、77 张配置表。图片均为原始 Sprite 解码 / 裁切，来源哈希与显式加载清单同步更新。

仓库按原 prefab 的四列、200×200 单元和原始间距排列，装备在同一底栏内切换；普通物品不额外显示名称，详情关闭返回原分类并恢复滚动。烹饪接入原始物品模板及制作数量控件；古遗物接入已有 `image_ui_treasure` 背景 / 空槽、原始六角品质框与图标，提供元素分类、收藏 / 碎片及本地装备操作。

`defaultpackage_assets_prefabs_ui_cooking.bundle`、`defaultpackage_assets_image_ui_cooking.bundle`、`defaultpackage_assets_prefabs_ui_treasure.bundle` 经清单哈希和 APK ZIP 条目核对均未打包。烹饪和古遗物页面的完整位置、动画与演出仍是待补齐项，当前本地布局不能称为完整原版还原。缺失图标不以其他图像替代。

## 商店与伙伴入口核对

后续更新（2026-10-13）：伙伴交互与送礼已替换下述历史资料布局，当前来源与范围见本文末尾「伙伴相遇、互动与送礼」及 `partner-ui/source-manifest.json`。

2026-10-12：同一固定 APK 追加完整读取 `home_visit_shop`、`home_visit_shop_goods`、`npc` 三张表，构造器及字节哈希见 rules-manifest.json。按 NPC 配置中的 `Icon` / `FigureImage` 定位并提取实际打包的图标和立绘，新增 37 项运行时资源、39 项图标映射。当前共 1,471 项资源、1,248 项图标映射、80 个 prefab、80 张完整配置表，未添加外部或生成图像。

`HomeShopPanel` 原始两个 `ShopPanelTabItem.myShopId` 为 1 / 2，分别对应杂货、宝库；特卖和馈赠分别绑定 `HomeVisitShopContent`、`AchievementPanel`。商店中的嵌入商品和馈赠行直接复用已提取的 prefab 节点；物品内部图像按原遮罩留白和等比显示，槽位缩放作用于整个 prefab。

`HomeNpcChatEntry.OnClick` 与 `DiningTableNpcPopClick.OnValidClick` 证实铃铛桌子 / 气泡为 NPC 面板入口，修正原本误接的烹饪路由。`prefabs_ui_npc.bundle` 未打包，当前为已有 NPC 原图与本地图鉴布局；完整对话和养成仍待实现。

再次核对胡椒等 7 个食材图标的地图 bundle 哈希与 APK ZIP 条目，并检索 216 个已打包图像包的容器：无对应图像。缺项仍保留于 source-manifest.json，不把锁图像当作对应食材图标已经补齐。

## 祈愿、古遗物兑换与馈赠控件

2026-10-12：核对固定 APK 的 GachaPanel 内嵌 ShopContent 与 Shop4 / Shop5，序列化 ShopPanelTabItem.myShopId 分别为 4 / 5；TabItemTopBarSwitcher.ShowTypeList 分别为 PlayerItemType.Jingshenli (1) / SkillGachaByproduct (47)，对应物品 4 / 20。SkillExchageShopParent 按子分页切换面板，不跳转国家材料兑换。ChestGachaPanel.OnTabItemChanged 使用内嵌 Shop 子面板并明确商店 6；原始 shop 表中商店 6 的 FixedGoodsList=null、GroupIdList=[]，不虚构服务器货架。

ScoreRewardUI.RefreshReward 从下一档 AchievementInfo.Reward 构造图标，按分段目标计算圆形进度。现按此绑定原奖励并修正零宽 Slot 的居中；首档技能奖励箱 6800 的 ItemIcon/Box/item_6800 在未打包的 box_skill_noatlas 中，保留奖励名称，不冒用消耗品图像。古遗物页分宝箱 / 元素，使用已取得 ChestElementGacha 图集；完整 ChestGachaPanel、扭蛋机和背景仍未取得，共用原始底部导航和通用结果面板的部分属于本地适配，不声称像素级还原。具体缺项见 source-manifest.json 的 gacha_exchange_interface / wish_first_score_reward_icon。没有新增位图素材。

积分奖励列表再次核对 `ScoreRewardDialogItem` / `AchievementPanelItem.Refresh` 和 `ItemIconDefault`：行宽为 850.2618，标题位于原始背景框内；`AchievementPanelItem.Refresh` 对所有奖励执行 `SetCount`，包含数量 1。原 prefab 确有 `ImgItemIcon_NumBg` 深色数量底（颜色 #222222，alpha 0.851）及内容自适应布局。其 TMP 序列化当前字号为 18.8，基准字号为 36，自动缩放范围为 18–36；本地普通物品数量恢复基准字号和按数字测量的宽度，保留原数量底图。重新校验固定 APK 后提取 `image_itemicon_box_skill_noatlas.bundle` 仍返回 `Not packaged`，列表也按真实名称显示 6800，未声称补齐原图。没有新增位图素材。

物品数量的字号修正继续上移到共用 UI 控件，覆盖普通物品、技能、技能碎片、装备、料理、宝石、古遗物 / 碎片，以及页面内嵌的图标。统一使用 36 基准字号，小槽位按缩放补偿，在标准 450 像素宽窗口中不低于 10 像素；文字和背景按实际数字重排，动态数量更新也触发重排。技能碎片原始 `BgCountText ` 名称带末尾空格，按实际数量节点绑定，避免漏显。古遗物无数量底图的原始样式继续保留，并增加细描边以便辨认；这些最小字号与描边属于本地桌面适配，不声称是原版全部场景的字体规则。原数量底图、碎片标记和图片来源不变，没有新增位图素材。

2026-10-13：再次核对 `GachaProbabilityContent` / `GachaProbabilityItem`、`ShopGoodsItemIconBase` / `ShopPanelGoodsItem` 与原始 prefab。祈愿详情现从 `chest.RewardRules` 和实际职业池生成按品质、类别分组的基础概率，并复用详情内的 `RuleRateInfoItemGacha` 图标网格；只读取配置，不推进随机或保底。商品信息使用已提取 `ShopGoodsItemInfoCommon` 的名称、图片、数量节点，禁用被误作名称的额外 `BottomDescGo`，为名称与数量划分空间。馈赠内嵌 `ScrollRect` 缺少滚动脚本，现显式绑定实际奖励列表，支持滚轮及拖动；缺图箱保留真实名称。没有新增位图素材。

按用户要求，常驻与高阶的文字下拉框统一为人物当前转职路线的各阶段；此筛选属于本地行为调整，不沿用 APK 常驻跨路线筛选。两个奖池的记录入口也统一坐标。

底部导航再次从固定 APK 的 `PublicBottomBar.prefab` 核对：`FunctionTextBgImg` 使用原始 `ImgBg_NameSel`，黑色圆角底图只在选中项显示，`PublicBottomBarCell_On` 的最终尺寸为 163 × 42，`Off` 隐藏文字与底图。五个文字底图上的 `VerticalLayoutGroup` 和 `ContentSizeFitter` 原始 `m_Enabled` 均为 0；提取器现保留其禁用行为；本次重新提取底栏，仅移除对应十个误启用的布局字段。图片、原始边距数据和动画曲线均未替换。主导航在五个主界面中保持可用，输入顺序与显示层级一致。

## 成长目标与额外快捷栏核对

2026-10-12：本地成长目标不再使用通用文字卡片，改用已提取 HomeShopPanel 内的 AchievementPanel / AchievementPanelItem 原始背景、宝箱图像、进度、领奖状态和 RewardSlot 物品图标。没有新增位图素材，资源数量不变。

独立 defaultpackage_assets_prefabs_ui_achievement.bundle 的清单哈希为 95045fde8488e6c55418ef3a9131ede9，APK ZIP 中没有对应文件；本地成长目标名称、目标筛选和组合位置不能据此视为已取得完整独立原版布局。具体状态记录在 source-manifest.json 的 growth_goals_interface。

删除  的 nav 生成入口及所有额外跨页快捷栏，删除挑战页上方本地新增的技能 / 装备 / 幻兽快捷按钮。核对 TaskPanel.mTabGroupGo / mTabToggleGroup 与已提取 prefab 的 BottomGo/TabGroup_11，原始底部任务分类保留；未将名称相同的原始分类误删为额外导航。

## 古遗物扭蛋原始布局接入

2026-10-13：`prepare_relic_draw.py` 从已校验的同版本官方 CDN 缓存（manifest `854_156383`）读取 `prefabs_ui_chestgacha` 及依赖、`chestgachabg_*` 和 `scriptableconfig_modules_treasure`；复用固定 APK 校验及 `Apk` 来源读取器。完整接入 `ChestGachaPanel` 的原始 RectTransform、宝箱 / 元素 / 兑换 / 馈赠四个底部页签、宝箱四种卡片及进度条、元素选择弹层和原始空商店。此前关于扭蛋专用布局尚未取得的记录描述的是当时仅有基础 APK 的状态，此次由匹配清单的 CDN 缓存补齐。

提取 134 项 Sprite / Texture2D 来源记录，包括图集裁切及原始扭蛋机 / 胶囊 Spine 图集解码；脚本合并来源清单和显式加载清单，原始 bundle / 对象、实际 URL、输入校验与输出 SHA256 见 [relic-draw/source-manifest.json](relic-draw/source-manifest.json)。按 `TreasureGachaSetting` 的品质分布、元素皮肤和 `shock_01..10` 末帧组合九种奖池、十个胶囊的静止姿势，不调用抽奖 RNG。原版入场动画独立播放，开蛋粒子及完整演出仍未转换，结果使用既有原始通用结果页。

积分 / 规则 / 历史 / 结果弹窗以当前原始扭蛋页为底层，底层不接收指针。馈赠复用原始 `ChestGachaAchievementPanel` 成就条目并绑定滚轮 / 拖动。修正 Unity 无 Graphic 的零高 Mask 容器适配，避免元素图标被误裁空。

2026-10-23：`prepare_treasure_gacha.py` 按固定版16个原 prefab 读取 `Image` 组件并核对层级路径，登记245个原 Sprite 引用，解码60张原 UI Sprite；运行时恢复图片类型、色调、PPU、pivot、九切片边框及射线标记。`ImgCostMoneyBtn_Bg` 保留原边框 `[20,19,20,20]`。输入对象、输出 SHA256 与绑定数据见 [treasure-gacha/source-manifest.json](treasure-gacha/source-manifest.json)。

## 家园商店图标与原始显示逻辑补齐

2026-10-13：`prepare_shop_assets.py` 按商店 1 / 2 的固定商品与全部商品组、家园特卖表限定提取范围，补齐 24 个图标，其中包括胡椒、糖、盐、木果粉、乐园牛奶、奶油、黄玉奶酪七项食材。所有补充资源来自已校验的同版本 `854_156383` 官方 CDN 缓存，家园三类货架的 48 种物品均有原始图标。此前“七项食材未打包”的历史记录对应基础 APK 状态，此次补充来源、实际 URL、bundle 哈希和输出 SHA256 见 [shop-ui/source-manifest.json](shop-ui/source-manifest.json)。

同一脚本读取原始 `ShopGoodsItemShowConfig.asset` 的品质到 Sprite GUID 映射，解码七种原始商品品质背景。商品使用已提取的 `ShopGoodsItemInfoCommon` 布局、图片、数量与阴影；数量文字扩大到原数量容器宽度，避免三位数截掉乘号。没有绘制替代图片。

核对原始 `Common/ShopInfo.GenerateFixedGoods`，未满足 `Condition` 的固定商品不加入货架，因此不显示空图或锁图占位。`ShopGoodsItemIconBase` 根据 `ShowName` 控制名称；家园普通与特卖上下文的 `CanShowBottomTxt` 均返回 false，恢复此规则，不再将物品名写入底部说明区。馈赠绑定实际 Grid 的滚轮 / 拖动，并移除本地只生成前 30 项的限制。

## 幻兽列表卡片图片绑定

2026-10-13：核对原始 `PetListPanelItem`、`PetInfoContent.Init` 与 `PetSettings`，幻兽列表应替换 `PetInfoSlot/Mask/IconImg` 的图片，并按当前进化阶段加载 `pet_{ClassId + 100 × (phase - 1)}`。修正此前保留模板示例 `pet_1704` 并叠加通用圆形物品图标的行为，直接使用原卡片图片、遮罩、等级与名称节点；品质背景按实际 Quality 切换，底部类型图标按 PetSettings 的 0 / 1 / 2 映射绑定。

`prepare_pet_ui.py` 从固定 APK 的 `image_itemicon_pet`、`image_ui_pet` 与 `scriptableconfig_modules_pet` 提取 39 张现有阶段图片、三种类型标记与对应配置，新增 20 项运行时 Sprite，其余复用已有原图。来源、对象身份和输出哈希见 [pet-ui/source-manifest.json](pet-ui/source-manifest.json)。没有重绘或生成图片。配置中的 `pet_1619` 不在原始幻兽图像 bundle 内，保留待补齐记录；该项不再显示其他幻兽的模板图片。

## 床铺详情与动态进度

2026-10-13：`prepare_bed_ui.py` 从固定 APK 的 `prefabs_ui_furniture.bundle` 转换 `BlessingLevelRuleDialog` 及其内嵌 `RuleFormDataItem`，复用六项原始 Sprite 来源记录，未绘制替代图片。运行时恢复 `BlessingRuleBtn` 的规则弹窗、`FurnitureBedDialog` 两组独立折叠项，并按 `BlessingLevelRuleItem` 读取当前赐福等级附近 11 级的原始基准产量和小推车上限。

床铺圆环按 `FurnitureBedDialog.HangUpTimer` 的累计量 / 容量比例绑定，使用本地同一产量预览的储存上限，打开和累计时刷新、领取后更新，清除空储存的可领取提示。文本更新后重排，避免累计数量变长而裁掉数字。保留原图、原填充方向及颜色。

本地存档只保存汇总产量属性，加成展开项以实际参与计算的比例、固定经验和最终比例显示，不伪造月卡或古遗物等来源分项。规则说明文字、表头换行及 1100 高的滚动区域为桌面适配；弹窗、预览行与素材来源仍为固定 APK。

## 伙伴相遇、互动与送礼

2026-10-13：`prepare_partner_ui.py` 校验固定 APK，并读取同版本 `854_156383`、具有 MD5 / SHA256 下载凭据的官方资源缓存，转换 `NpcPanel`、`NpcPanelNpcItem`、`NpcDetailPanel`、`ItemIconNpcGift`、`ReduceDot` 五个原始 prefab；恢复原始三列伙伴卡片、角色背景、送礼区、偏爱标记和减少按钮。通过原资源路径及祖级 atlas bundle 定位补齐伙伴立绘、头像及礼物图像，不使用模板图替代。每项原资源的 APK / 官方缓存来源、bundle 身份、对象和输出哈希见 [partner-ui/source-manifest.json](partner-ui/source-manifest.json)。

完整消费并追加 `npc_gift`、`npc_friendship_level`、`npc_friendship_tag`、`npc_talk`、`npc_unlock_item` 五张 APK 配置表。相遇、专属礼物限制、偏好倍率、累计阈值、位阶限制和自动选量对照原客户端 / 公共代码实现；礼物扣除及好感增加通过本地同一存档事务保存。原始回应使用 `npc_talk`，静态角色触摸时的小幅缩放为本地反馈适配。

固定安装包没有完整剧情 / 档案语言文件。逻辑配置路径 `Config/Binary/level_prop.g.bytes` 不位于主配置包目录，但固定 APK 的 WwiseBnk 中包含对应原始表 `assets/yoo/WwiseBnk/11e11b3ef06eaece6061f07f8f399542.rawfile`；已按 APK SHA256 `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9` 提取并用于原属性数据恢复，不属于资源缺失。未编写替代剧情或虚构属性加成。另有八位后续联动伙伴（11015–11022）的立绘、头像、背景和专属礼物图像不在当前资源中，共 32 项路径，逐项记录为待补齐；运行时可用伙伴清单保存在 `assets/official/partners.json`，这些伙伴暂不开放，避免空白卡片和缺失形象的互动。该范围不能计作完整原作剧情、档案、羁绊冒险或战斗养成已经实现。

全量资源校验仍受共享地图工作中已有的 `adventure.json` 哈希记录不一致影响；本次伙伴资源、四个相关数据目录和显式加载覆盖单独验证，没有改写地图预期哈希。

## 炼金炉原始界面与料理入口

2026-10-13：核对原始 `BagualuFurniture.OnClick → BagualuPanel`，锅应打开料理、合成、装备分解、宝石合成及馈赠，原本本地“石块 50 + 罗拉 500”的固定兑换不属于此原始逻辑，已移除。`prepare_kitchen_ui.py` 从固定 APK 及同版本 `854_156383`、具下载哈希凭据的官方缓存转换八个原始 prefab，保留完整背景、底部页签、料理五列网格、材料状态位置、数量控件及配方学习 / 合成目标弹窗。

追加完整消费的 `item_merge`（38 行）、`gem`（360 行）、`gem_merge`（14 行）三张表。料理使用原 `Materials / Count / Condition`，学习原配方物品后记录 `UsedItemCondition`；各页签读取原功能解锁条件，馈赠使用原内嵌成就卡片及分解 / 宝石 / 料理三个分类。466 项图像 / prefab 资源全部有输出哈希与显式加载记录，所需图标缺失 0 项；原对象身份、bundle、处理方式见 [kitchen-ui/source-manifest.json](kitchen-ui/source-manifest.json)。没有重绘或生成图片。

原炉的 RenderTexture / UIMesh 及完整制作特效尚未转换，在同一原始图像槽使用原 `UI/Cooking/ImgStove` 静态图片。本地宝石按同 ID 堆叠、自动选择同类材料；独立宝石实例、混合材料、镶嵌、高阶 NPC 熔炉和赛季赐福进度未接入，一键合成维持原赛季条件锁定。装备分解保留既有本地强化材料返还；原作不同料理的食用效果仍待补齐。上述范围不记作完整战斗 / 赛季逻辑已经实现。

### 活动图片、主题签到与常驻活动（2026-10-13）

`prepare_activity_ui.py` 从固定 `854_156383` APK / 匹配版本官方 CDN 缓存解码 `ActivityEntryPanel` 所引用的图片卡片与签到、SevenDayTask、BingoActivity、ScratchCardActivity 界面。此次登记 109 个原始 prefab、757 项运行资源、13 张完整消费的活动配置表，以及同版本中文语言表；运行目录为 `assets/official/activities.json`，动态贴图进入显式加载清单。各项原始包名、哈希、对象路径及输出 SHA256 见 [活动来源清单](activity-ui/source-manifest.json)。没有生成、截图裁切或重绘素材。

当次转换有 32 个入口（29 个主题签到 + 成长试炼 / 宾果 / 刮刮乐）；主题活动只开放已接入的签到模块，其余节日小游戏仍待迁移。取消活动开始 / 结束排期、倒计时及开服等待；签到每日一次，宾果 / 刮刮乐原始 30 套配置分别保存进度。刮卡的本地预抽结果和三个匹配图案结算属于离线规则适配，不声称已取得服务器实现。

后续补入赛季七日任务100～105的六套原面板、六套任务行及六套积分格，来自固定版本的 `prefabs_ui_seasonactivity_season1`～`season5` 与 `prefabs_ui_activity_s6`。当前 [活动 UI 来源清单](activity-ui/source-manifest.json)登记127个 prefab、801项运行资源；`ActivityEntrySevenDayTaskHandler.OnActivityOpen` 按 `DisplayCommonEntry` 过滤通用入口，赛季目录卡片为本地导航，面板与配置仍为原版。签到表2仅在 `activity_sign_days` 存在 ID 2 奖励配置，没有对应原导航或专用面板；本地入口复用已登记的官方通用签到面板，独立进度按自定义单机规则保存。

检索固定版本完整资源清单和本地已缓存容器后，`ActivitySlime/Sign/ActivitySlimeSignDialog` 未定位到资源包；物品 `1478`、`1487`、`35968`、`35986`、`35987` 的 RawIcon 未定位到 Sprite，均在来源清单 `missing` 中保留。孤独摇滚签到的背景和 NPC Sprite 实际为 16px 白色占位，真实美术未取得，因此不放入活动目录。不使用其他图片冒充。部分原始动态角色预览仍未转换，以上 prefab / 图集已提取不代表该动画已迁移。历史“签到专有面板缺失、活动文字入口”的说明保留为此前状态，以本节和运行时清单为当前状态。

## 公会界面、领地与离线玩法

2026-10-13 第二轮完整 UI 迁移：`python scripts/prepare_guild_assets.py` 从固定 APK 与同版本 `854_156383`、具 MD5 / SHA256 凭据的官方 CDN 缓存恢复 213 个公会、领地及关联公共 prefab，包括实际运行时池中的列表项、研究节点 / 连线、工具、排行、聊天、确认框和三种公会专用战斗结算面板。第一轮 33 个外壳与通用内容布局属于历史实现，本轮已替换。

`python scripts/prepare_guild_scene.py` 转换原始 `LegionMap` 的 188 个 Sprite 实例、32 个 Mesh 和 16 组 Spine 数据，保留十二座建筑的位置、点击碰撞区域、镜头聚焦 / 拖动参数、24 个建筑名称和等级标签，以及未建造、施工、正常状态。生产使用三个原始世界 Canvas 背景、九个原始生产物件视图和骨骼；另有 10 组 UI Spine 数据。运行数据为 `assets/official/guild-ui.json`、`guild.json`、`guild-ui-spine.json` 和 `guild-scene.json`，全部为原素材解码 / 网格转换，没有自行绘制或生成图片。

完整消费 25 张 `legion_*` / `stage_legionboss` 表及 `achievement`，共 26 张，保留原始费用、建筑 / 研究前置、速率、容量、生产占地、工具、商品、首领和战斗奖励。公会馈赠三个分组保留固定档位和循环增量。所需 440 项配置图标均取得。bundle / 对象路径、输入版本与哈希、中文语言分包及输出哈希见 [guild-ui/source-manifest.json](guild-ui/source-manifest.json)；场景、镜头、碰撞区域和世界物件见 [guild-ui/scene-manifest.json](guild-ui/scene-manifest.json)。全局来源和显式加载清单已纳入资源，可运行 `python scripts/verify_guild_assets.py` 单独复核。

公会成员、申请、其他公会和留言回应使用本地 NPC。服务器公会战周排期改为每天一轮的准备 / 战斗 / 结算，赛季每周结算；战斗复用本地模拟器，生活防守使用本地方向交互。领地地图按原始横向拖动和建筑入口操作，联网匹配、Elo 和实时多人通信采用离线玩法适配。历史“公会专有包未取得、通用菜单”记录保留为旧阶段状态。

## 角色属性综合、细分与说明

2026-10-13：`python scripts/prepare_character_props.py` 校验固定 APK，提取 `854_156383` 的 `Public/PlayerPropItem1`、`CharacterPanel/PropDialogDetailedPropItem`、`Tips/PopTip/PopTipEntry` 和 `Item/EntryDetailItem`，接入现有 `Character/CharacterDetailPropsDialog`。属性名称、来源分类和说明来自同版本官方中文语言分包及 `prop_cfg / entry` 原表；新增图像均为原始 Sprite 解码，没有自行绘制。

综合页使用原始 Main / Addition 分类、零值隐藏、关联数值和点击说明；细分页使用原始来源排序、独立折叠卡片和箭头动画。属性及来源汇总使用本地战斗规则；旧档累计加成无来源台账时显示为“其它加成”，尚无本地原版分母的同等级估算行不显示虚构比例。公会加成单独归入公会分组。此变更不代表完整原版战斗公式已经移植。

来源、bundle / 对象、中文分包凭据、输入 / 输出哈希和原客户端行为证据见 `assets/reference/character-properties/source-manifest.json`；运行时文本数据为 `assets/official/character-properties.json`，五个 prefab 纳入 `ui.json`，所有新图像纳入显式本地加载清单。

同日背景复核：重新读取原始 `Character/CharacterDetailPropsDialog`，面板背景 `Image` 和两层 `CanvasGroup` 的 alpha 均为 1，`ImgBg_PopUps` 的浅灰主体像素 alpha 为 255，透明通道用于外缘。`BgDialog1/BgImg/BgTitle` 的 `MirrorImage` 组件原本禁用，此次停止绘制该装饰。接回 `PopupWindow` 的 `BlurMask / Dialog / close_by_mask` 设置及 `UIDialog_Show / UIDialog_Hide` 原曲线；共核对五个 prefab、50 项 Sprite 记录。

背景蒙层来自已校验同版本 CDN 的 `defaultpackage_assets_prefabs_ui_main.bundle`（MD5 `5ce6cf884ed57ba5d3aeaf1222681726`），对象 `/MainCanvas/Content/Popup/BlurMask` 的原始 `RawImage` 颜色为 `(124/255,124/255,124/255,1)`。蒙层下方保留原始人物页并隔离指针；它使用原始四级缩小层数和颜色，未逐像素移植 Unity 的多遍滤波与 1/13 渲染目标。来源、代码哈希和适配范围已登记；专项复核命令为 `python scripts/verify_character_props_assets.py`。

## 角色职业总览、职业树与养成

2026-10-13：核对固定版本 `854_156383` 的 `ProfessionOverviewPanel / ProfessionPanel / ProfessionTreePanel / ProfessionUpgradePanel / ProfessionChangePanel / ProfessionPromotionPanel` 及公共 `BattleFormulaHandler`。此前角色职业入口误用了属性弹窗，仅显示描述和临时升级按钮；现改用 `CharacterPanelProfessionPanel` 的原始不透明背景、职业立绘、特点雷达、总等级属性、底部页签和内嵌分支树，并接入职业详情、升级、转职与晋升的专有原始界面。原位阶突破界面保留为独立 `rank_upgrade`，不再混作职业页。

`python scripts/prepare_profession_ui.py` 校验固定 APK，读取 APK 及同版本、具有下载凭据的官方 CDN 缓存，转换七个 prefab 入口及其内嵌布局、动画、状态切换、动态引用和中文文本。官方 `ProfessionSettings` 的 GUID 用于男女完整立绘、职业颜色和武器标记；补齐全部 376 个职业技能的原始图标。572 项相关运行资源均记录原 bundle、APK / CDN 来源、对象、输入身份、操作及输出 SHA256，并进入显式加载清单；来源见 [profession-ui/source-manifest.json](profession-ui/source-manifest.json)。没有绘制替代图片。

升级使用原 `profession_level_cost` 的徽章需求和等级上限，晋升使用原 `PromoteConditon` 的前置职业及总职业等级，不追加角色等级门槛。累计等级属性来自官方中文 / `level_prop.g.bytes` 分包，完整读取 30 个职业属性类别；当前职业特点的基础属性倍率按原 `HandlePropRate → HandleBasePercentProps` 顺序计算，分别截断增量，并与历次等级属性接入本地战斗及属性来源。原总职业等级成就读取 `AchievementType.TotalProfessionLevel`，恢复固定奖励和后续 2000 轮循环奖励；各项扣费、职业切换、已领取记录均通过本地事务保存。

职业树卡片使用官方职业立绘；原专属换装网格、导师寻路、服务器晋升任务和完整演出尚未迁移，后两者在单机版使用本地晋升条件与确认操作。职业相关属性已接入现有单机战斗，角色基础属性及完整技能 / 战力公式仍沿用已登记的本地适配，不宣称完整原版战斗已经还原。

2026-10-13 追加修复职业图标及等级说明：按原 `ProfessionOverviewPanel.UpdateProfessionView`，图标本体和阴影均使用 `GetSecondIconPath`（`Icon2`），替换误用的方形 `Icon3`；换职业时同步当前 Sprite 的画布 / 裁切，保留原前景色与灰色阴影的透明度。按原 `OnViewHelp → RuleDialog.SetRule("ProfessionLevelInfo")`，从 APK 提取 `RuleDialog` 及其通用包依赖 `TipInfo1`，解码 `rule_tip.g.bytes` 的 557 条规则并按 Order 取三条原始中文等级说明。职业来源清单现含九个 prefab 入口、576 项素材凭据，保留此前已提取技能图标的来源与输出哈希；重复转换不会丢失缓存素材凭据。

## 技能首页与安装流程复核

2026-10-13：复核固定版本 `854_156383` 的 `CharacterPanelSkillPanel`、`SkillPanelSkillDetailDialog`、`SkillRecommendDialog`、`SkillRecommendDialogItem`、`SkillTabGroupItem`，以及客户端 `CharacterPanelSkillContent.BeginPrepareEquip / RefreshEquipedSkill` 和 `SkillPanelSkillDetailDialog.RefreshBtn`。原有实现未给七个空技能槽生成 `ItemIconEmpty`，将“推荐”误接为祈愿，并使用通用 `SkillDetailDialogPanel`；安装时直接追加技能，无法选择位置或替换满槽。

`python scripts/prepare_skill_ui.py` 使用既有 APK 读取器、同版本 CDN 缓存和已校验中文文本分包，转换上述五个原始 prefab、禁用图像状态、遮罩、高亮框、中文文字及脚本引用目标；运行时资源继续纳入 `ui.json` 和显式本地加载清单。`skill_recommend.g` 经原始 `TbSkillRecommendInfo` 构造器完整读取，名称和说明取自原始中文表，输出为 `assets/official/skills.json`。bundle / 对象、APK / CDN 来源、原始配置与输出 SHA256、中文分包凭据及行为来源代码哈希见 [skill-ui/source-manifest.json](skill-ui/source-manifest.json)。本次没有新增自绘图片。

技能页恢复四个战技与四个秘法槽位的原始空槽素材，详情使用技能专用 prefab 和同一套官方遮罩图标；点击安装 / 替换后使用原版暗色遮罩与同类型四槽高亮，选择目标槽位后才提交本地事务，取消不改档。推荐页使用原始方案与说明，仅安装已拥有且职业适用的技能，未获得部分保持空位。新增槽位位置与方案名称兼容旧紧凑数组，并随本地存档保存；原技能等级、升阶费用和战斗公式仍属已有单机适配。

## 2026-10-13 常驻 / 高阶祈愿复核与图像修复

使用固定 manifest `854_156383`，`prepare_wish_assets.py` 重新解码并逐项比对 13 张 Luban 配置表，记录原始字节 SHA256；同时核对 `SkillGachaPanel`、`SkillSeniorGachaPanel`、`GachaPanelBase`、职业过滤、路线回溯、循环成就和记录客户端逻辑。证据见 `official-city/wish-source-audit.json`，玩法与离线边界见 `docs/features/wishes.md`。

常驻选中图 `ImgSel1` 的 500×328 原画布仅有约 500×174.85 的 atlas 裁切片，纵向偏移约 67.08；此前裁切片被拉伸为整幅高度。已恢复 `rect_size`、`trim_offset`、`trim_size`。高阶 `ImgSel` 保留原九宫格。两尊雕像 prefab 均使用 808×988 原画布，已在各自不同宽度的 Slot 中按同一尺寸居中，并恢复常驻雕像裁切参数。积分弹窗保留来源祈愿背景，馈赠释放重复的内嵌顶栏 / 底栏预留，技能商品库存文字与图标分开。未新增自绘图片。

首档自选 1 阶技能箱 `6800` 已补齐并实际用于圆圈、积分弹窗及馈赠，替代历史缺图名称占位。原始 `defaultpackage_assets_image_itemicon_box_skill_noatlas.bundle` 不在基础 APK 内，但现有同版本官方 CDN 缓存可用：MD5 `ea8a28849473cdeb177a30a44b54782e`，大小 512,743 字节，SHA256 `1756ede5d29cbeec9497fecdf06cc64d6abd18111c37948338705f28ef016c2b`。实际 URL 为 `https://zhangjcsdl.leiting.com/patch/202609271456857/Android/DefaultPackage/ea8a28849473cdeb177a30a44b54782e.bundle`。Sprite `item_6800`、path ID `2600191277797411432`，转换输出 `assets/official/sprites/item_6800-15e9a49313.png`，输出 SHA256 `6dc025b9463e576af3628b5ab2cea07ff05ba1b1e7bd5b26ea404d4f65302d19`，已登记来源清单及显式加载清单。

前文的 `Not packaged` 和 `wish_first_score_reward_icon` 是当时仅核对基础 APK 的历史结果，现以其 `resolution` 及本次清单记录实际替换状态；没有混入新版文件或改变原预期哈希。原客户端完整服务端动态概率公式未取得，`WeightScaleFactor=2` 不据此猜造概率；本地实现明确的基础权重、品质保底和当前阶段保证，并标注这一限制。

## 祈愿环境、品质与奖励特效补充

2026-10-13：对照原版本新增官方下载缓存和固定 APK，补迁此前未转换的祈愿视觉资源。本次实际使用的 29 项图像依赖均来自固定 APK `854_156383`，没有将已缓存新版特效包当作原版补齐。前一节从同版本 CDN 恢复的技能箱 `6800` 继续使用；其来源与历史缺图记录保留。

`python scripts/prepare_wish_visuals.py` 从原 `SkillGachaPanel / SkilSeniorlGachaPanel / PetGachaSubPanel` 读取常驻、高阶与幻兽结缘环境粒子，从 `GachaTenRewardGo` 读取四组奖励粒子，恢复八个白／蓝／紫／橙品质光效 prefab 和 `GachaLightConfig` 的五种品质 Sprite。输出 20 张原始 Texture2D 解码 PNG 到 `assets/official/wish/`，相关 Sprite 继续使用 `assets/official/sprites/`，参数写入 `assets/official/wish_visuals.json`；所有动态图像均纳入显式本地加载清单。原 bundle、MD5、对象 path ID、操作、输出 SHA256、客户端代码哈希与字段绑定见 [wish-visuals/source-manifest.json](wish-visuals/source-manifest.json)。没有新增自绘位图。

常驻／高阶页面已使用环境星光，幻兽页面使用星光与落叶；结果页使用原版品质光、奖励出现闪光、新技能光圈及重复转换碎光。`Fragment` 和 `Aperture` 的用途按原脚本字段与 prefab PPtr 核实，分别对应重复转换和首次获得。视觉随机源独立于抽取随机源，状态快照仅保留在界面实例中，不改变本地结算与存档。

使用原始发射曲线、爆发、颜色、尺寸、寿命、数量上限、纹理滚动、扰动与淡出；Unity 3D 湍流、Bloom 后处理、完整星轨和结缘角色演出仍未逐项还原。

## 古遗物扭蛋导航与概率界面修正

2026-10-13：`python scripts/prepare_relic_navigation.py` 离线读取固定 APK 与已校验的同版本 CDN 缓存，转换 `ChestMapSwitchPanel`、`ChestMapSwitchItem` 和 `ChestGachaConfig` 的 17 组地图入口图片。40 项 Sprite 依赖继续输出至 `assets/official/sprites/`，两项 prefab 合并至 `assets/official/ui.json`，地图入口配置写入 `assets/official/relic_navigation.json`，均纳入显式本地加载清单。输入 bundle 的真实来源、大小、MD5、SHA256、CDN URL（如适用）、对象名、对象 path ID、转换方式与输出 SHA256 见 [relic-navigation/source-manifest.json](relic-navigation/source-manifest.json)。没有使用新版替代包或新增自绘图像。

右上角地图选择接入原版面板与地图图标，确认时持久化所选系列，切换标题、收集进度、背景、图鉴入口及实际宝箱奖池；四类宝箱采用原 `ItemFilterForTreasureGacha` 的系列过滤，元素池保留原元素范围。书本入口复用已转换的 `CollectBook` 古遗物图鉴，概率列表复用 [codex/source-manifest.json](codex/source-manifest.json) 登记的原始遗物图标。原扭蛋机、胶囊、收藏框与历史资源来源保留。

概率页沿用原 `GachaDetailDialog`、品质分组与滚动区域，按说明文字高度布局，显示原表基础概率、当前材料和保底进度；这一修正不表示 Unity 完整开蛋演出或服务端动态概率公式已迁移。来源清单保留共享 `ui.json` 的转换时快照哈希，并独立记录本次两项 prefab 的规范化 JSON 哈希，供共享目录后续扩展后核验。

## 2026-10-13 商城、礼包与耀星余额复核

本节记录首次修复；后续实现已覆盖下文历史记录中尚未接入的礼包组、代金券、续期和月卡属性。

彩色货币为原表 `FlareStarId=15` 的「耀星」，黄色货币为 `DiamonId=2` 的「晨星」，金币为「罗拉」。三种官方图标及库存字段原本已存在；旧顶部默认仅绑定罗拉、晨星，原表又将货币设为 `ShowInBag=false`，造成耀星没有可见余额入口。现主城显示三种主要货币，商城 / 月卡 / 礼包显示晨星、耀星，点击余额使用原物品详情 prefab 显示名称、原始说明及实际库存。调整顶部控件的鼠标顺序，恢复页面上方的实际点击。

`python scripts/prepare_commerce_assets.py` 复用固定 APK 及已校验 `854_156383` CDN 缓存，转换原 `DailyPayGiftDialog / WeeklyPayGiftPanel / PayGiftItem`，并根据 `PayGiftIconConfig` 的 GUID 解码六个原始礼包立绘 prefab。首购 / 再次赠送数值继续读取原表，六档商品名称与货币说明补读官方中文文本分包；每日 / 每周礼包展示完整奖励，补接原表每日免费晨星、每周赠礼、整组购买和限购状态。新增运行目录为 `assets/official/commerce-ui.json`，全部 74 项相关图像资源、来源 bundle / 对象、输出哈希、中文分包及行为代码凭据见 [commerce-ui/source-manifest.json](commerce-ui/source-manifest.json)，动态图片均纳入显式加载清单。第 1 组礼包此前缺少的五个奖励图标已从官方资源转换，其余已存在图标重新核验登记。没有新增自绘图片。

每日礼包替换原通用货架卡片，每周礼包恢复原专用卡片、立绘及所有奖励图标。免费入库、本地日 / 七日周期与第 1 组礼包仍为离线适配，未模拟服务器赛季换组或支付 / 代金券服务。

### 同日追加：完整核心商城目录与本地结算

继续从固定 APK 及匹配 `854_156383` 的已校验 CDN 提取原 `ShopActivityEntryPanel`、`PayVoucherItem`、`PayGiftConfirmDialog`、`PayMoneyChangeDialog`、`PayInfoTableDialog / PayInfoTableRowItem`、`AccumulatedPayPanel / AccumulatedPayAwardItem / AccumulatedPayCoreAwardGo / AccumulatedPayCoreMulltiAwardGo`、`FirstPayDialog` 和 `RuleDialog`。与每日 / 每周界面和六个礼包立绘合计 21 个原始 prefab，276 项资源哈希。所有 4 组每日 / 85 组每周礼包、累计奖励及首购奖励共用的 170 个物品图标，以及六档代金券立绘全部核验并接入显式加载清单，没有自绘补充。

追加解析 `daily_pay_gift_group`、`weekly_pay_gift_group`、`pay_voucher`、`accumulated_pay_award`、`first_pay_award`、`rule_tip` 六张原表，补读全部礼包名称、代金券标题、卡片特权与规则中文。输入哈希、消耗字节数、原构造器、每个 prefab 与 Sprite 来源、转换输出及原客户端 / 公共逻辑文件哈希仍记录于 [commerce-ui/source-manifest.json](commerce-ui/source-manifest.json)。原版代金券规则明确「充值不获星值，消费时获得」；本地结算按此实现。月度奖励原表属性 `6004=500 / 1034=500` 分别进入经验最终产量、小推车产出及角色属性显示。

用户选择离线提供赛季 / 礼包目录，随时查看和购买全部原版内容；原服务器换组改为手动目录选择，各目录本期限购独立保存，每周采用原周一刷新规则。金额购买是明确标记的本地模拟；代金券购买实际扣除本地库存；线上支付平台、订单、退款及跨账号转移不接入。首购主视觉使用已转换的官方角色网格，武器属性预览复用本地装备规则。

## 2026-10-13 祈愿与扭蛋材料购买

`python scripts/prepare_gacha_purchase.py` 从固定 APK 与已校验的 `854_156383` CDN 缓存读取原 `quick_buy.g.bytes`、`furniture.g.bytes`，转换 `CostItemConfirmDialog` 和 `ShopQuickBuyDialog` 两套购买 prefab。运行数据为 `assets/official/gacha-purchase.json`，图像继续使用原 Sprite 解码文件，30 项依赖均登记并纳入显式本地资源清单。完整 26 条快捷购买原表、3 条本模块使用的材料购买记录、免费自动修复家具条件、bundle 来源与对象、图像处理方式及输入／输出哈希，见 [gacha-purchase/source-manifest.json](gacha-purchase/source-manifest.json)。没有使用新版替代包或自绘图片。

原价为星之愿 `110` 每个晨星 ×100、稀有女神牌 `301` 每张晨星 ×8、结缘材料 `107` 每个晨星 ×160。高阶材料 `118`、女神牌 `302–304`、元素牌 `311` 没有快捷购买记录。材料不足时使用原确认弹窗购买差额；数量购买使用原数量滑条、加减与最大按钮。购买后先补入材料，另行点击抽取，保留原流程；顶部余额加号与物品详情为购买入口，祈愿页的“购买材料”为本地便捷入口。

核验命令为 `python scripts/verify_gacha_purchase.py`；此前“在线购买材料不属于离线实现”的范围说明已由本次本地晨星购买替代，真实支付仍不接入。

### 2026-10-17 时光扭蛋机开放提示文字

`python scripts/prepare_relic_unlock_text.py` 将固定 `854_156383` 的五个中文键迁移为 Git 内代码绑定 ：`PLAYER_FUNCTION_LIMITED`、`home_notice_Level_tip`、`function_limit_mapUnlock`、`Mainland_11`、`confirm`。输入来自固定 APK `assets/yoo/WwiseBnk/a373f2acc39aeb5c4560f1456879b997.rawfile`（launch language，SHA256 `a068ee47db184c96c8923172a104ed90b37f72e2b4adb5d73211d894d7ec9001`）和 `assets/yoo/WwiseBnk/9a0b6ab8c5136050fd005ead8f567863.rawfile`（language，SHA256 `738069eafacd7fe33aae970aa5edacd76f48ed02acdd07ededad1c4a499b93b8`）；APK SHA256 为 `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9`。输出按 UTF-8/LF 计算 SHA256 为 `2c791a489f18072bfb71307a6029cb27ca1619faa3ec39bcfbdf92a6940d489b`；转换工具严格检查两份输入哈希，不改写源缓存。

提示控件复用已转换的 `ConfirmDialog.prefab`：原版本 CDN bundle `e9fb11a95f5ca61a90de42fc3fd90258`，对象 `5569979765835515756`，实际 URL `https://zhangjcsdl.leiting.com/patch/202609271456857/Android/DefaultPackage/e9fb11a95f5ca61a90de42fc3fd90258.bundle`，输入 bundle SHA256 `872b72513cc17d918891383990fb4dbe92c0bb5add97a4561840197b468578d3`。控件来自 `ui.json` 中现有的 `common_confirm` 绑定，遮罩颜色沿用原公共 PopupWindow 配置；本次不产生新的图像、素材目录输出或显式资源清单条目。官方 `FurnitureBase`、`HomeMyPlayerData`、`ChestGachaPanelNavigateHandler`、`ConditionNotice` 与 `LanguageUtils.GetConditionTextWrap` 为条件与文字调用依据。

## 2026-10-13 祈愿价格底板与刷新次数复核

固定 `854_156383` APK 的常驻抽取及兑换刷新 prefab 确实默认显示 `ImgCostMoneyBtn_Bg`，`PlayerCostMoneyContent` 在正数成本下显示整个成本区域，本次核对的余额状态切换配置为空。原资源中存在黑色价格底板，不能声称原版会在材料足够时隐藏它。这属于本地展示调整，既有官方图片像素与来源未改动，没有新增自绘或重新转换图片。

原 `ShopPanel.SetRefreshUI` 显示 `RefreshCountsLimit - RefreshedCount`，原商店 5 为每天十次付费刷新；现显示完整剩余整数 `10→0`，并为徽标、文字、晨星和价格分配独立边界。四项原始成本组件字段、21 项 prefab 图片绑定、6 项复用 Sprite 哈希、客户端代码哈希及原刷新价格数组见 [wish-button-costs/source-audit.json](wish-button-costs/source-audit.json)。

## 2026-10-13 全地图场景与交互素材转换

本节记录后续素材转换及实际替换，保留前文地图总览阶段的来源历史。范围为固定版本全部 373 个地图定义，包含九个大陆地区、副本和活动地图。

来源继续使用固定 APK `854_156383` 和匹配原清单的已校验 CDN 缓存。373 组原始导航 / 放置表输入凭据见 [map-layout-source-manifest.json](official-map/map-layout-source-manifest.json)。地形、实体模板、外观、交互包装、界面、昼夜层、水面、剧情媒体及音频分别登记在 `official-map/` 对应 `*-source-manifest.json`；当前地图资源注册包含 18,559 项本地资源，并保存输出数据 SHA256。已缓存新版同名包仍为独立研究来源，没有冒充原版本补齐。没有新增自绘位图。

`prepare_map_minimaps.py` 读取原 `MiniMapPrivateRegionMaskManager` 的动态分区 Tilemap GUID、原 manifest 资源路径、Grid 单元尺寸和世界变换，补转换静态根 prefab 未包含的地图地块。149 个地图配置共用 65 份原始小地图，包含 1,788 条配置分区引用；转换记录 3,148 项输入对象凭据、5 张解码纹理及网格输出哈希，见 [minimaps-source-manifest.json](official-map/minimaps-source-manifest.json)。原编辑器玩家占位图不作为地块叠加。全屏地图接入 169 种原 MiniMapMark 外观、筛选、定位、滚轮 / 原滑条缩放、拖动和已激活入口传送。地图交互对话根据原 DialogIconPrefab 或 NPC FigureImage 绑定官方图像。

原 WorldStage 组件、970 个原始剧情图及 15 个旧版对话适配图在本地执行。NPC Touch 与 Function 分开提供交谈和委托，支持原始提交物品流程。私有 / 公共区域生成条件、区域状态条件和原探索度的 100 点激活加成已接入。无女神像区域在私有探索全部完成后转为公共状态，是缺少服务端实现时的单机适配；没有把这个推断标为已取得的原服务端规则。原战斗属性计算仍使用项目的本地规则。

活动的 522 条原始关卡配置保留赛季和关卡各自进度。踩石板接入方向、进入次数和传送落点事件；活动棋盘和世界机关的完成标记分别保存，离开地图再返回恢复当前棋盘。

当前地图功能及剩余实现范围见 [世界地图功能文档](../../docs/features/world-map.md)。

踩石板解法工具 `scripts/solve_map_plates.py` 使用独立开发目录 `.tools/map-solver-python` 中的 OR-Tools 9.15.6755，不进入运行资源。测试解法只产生操作序列，不能直接写入机关完成状态。

2026-10-13 浅水与浮桥补齐：`prepare_map_fording.py --scan-all` 检查全部 2,355 个实体模型 prefab，转换 132 个使用原始 `CustomWater.FordingSpriteRenderer` 的模型，并刷新对应状态外观。网格按原 Sprite、pivot、pixels per unit、颜色和世界变换生成；原 `Sprite-Lit-Fording` 的压缩 GLES 程序解码后移植水深、噪声、浅深色混合和边缘公式，使用原 WaterController 的参数与 FordingTex。来源及哈希见 [fording-source-manifest.json](official-map/fording-source-manifest.json)。另检查全部 373 个地形引用的 6,177 个 prefab，未发现需要补转换的 FordingSpriteRenderer，见 [地形运行组件检查](official-map/ground-runtime-renderer-audit.json)。未新增替代位图。

浮桥采用原 `Sprites/Submersion` 的高度贴图和双通道颜色高度偏移；原 Mecanim 升降关键帧驱动物件位置、遮罩高度和阴影透明度，见 [材质来源](official-map/materials-source-manifest.json)。

离线规则对单状态、DestroyOnEnd 且没有后续结果的一次性 Touch 记录完成；多状态传送和重复开关仍按各自结果执行。这是本地完成状态适配，不将客户端未提供的服务端逻辑声称为直接提取。

`map-legacy-displays.json` 先匹配原地图路径，再使用名称精确对应 `class_view` 的唯一原始 prefab 或 `Map/Common` 原始 prefab；有歧义的地区变体不作全局选择。真实输入、候选路径、输出哈希见 `official-map/extra-models-source-manifest.json`，完整绑定记录见 `official-map/views-source-manifest.json`。平台按原始 OffsetPath 逐格移动，使用原 GridMover 地面掩码等待被升起路面挡住的轨道，携带站立角色并保存当前站点、方向和运行状态；触发后的单段行程及本地定时推进是缺少服务端实现时的离线适配。

2026-10-13 状态、活动刷新点与剧情导演补充：`prepare_map_status_effects.py` 读取原始状态效果、作用范围及地图 Action 组件，输出 `assets/official/map-status.json`，凭据见 [status-source-manifest.json](official-map/status-source-manifest.json)。本地执行战斗作用范围、次数、离图移除、原始移动倍率和环境伤害；烟雾保留施放格及三格范围，护盾、烟雾按原 Action 的十秒持续时间执行，变身解除原隐身。状态图标的五秒寿命和地图 Action 的十秒寿命分别保存，重载不重新计时。雷霆随机落点及其完整场景演出仍需补齐，不能据此称全部状态效果完成。

`prepare_map_activity_points.py` 提取原始活动刷新点类别、逻辑格、分区和变换，输出 `assets/official/map-activity-points.json`，凭据见 [activity-points-source-manifest.json](official-map/activity-points-source-manifest.json)。悬赏及躲猫猫道具选择原类别的可用点；金币使用原 ActivityEntity 的奖励规则，刷新数量与上限使用原关卡 / 道具表。被捕后的抓捕次数、狂暴倍率、协助金币已按配置接入。追捕参与者由本地代理控制，这是单机适配；未取得的服务器 AI 和匹配规则不作原样迁移声明。

`prepare_map_timelines.py` 离线解析原 Story Timeline GUID、PlayableDirector 绑定、父子导演、时间缩放、Cinemachine 相机 / 镜头参数、Mecanim 曲线、AnimatorController 参数与状态、Spine 片段、激活、信号和 Wwise 事件。转换 138 个原始场景，含 1,010 个 Animator 组件记录，运行文件在 `assets/official/map-timelines/` 与 `map-timelines.json`。Spine 分包按原 SkeletonData 的 SplitBinarySO 字符串表、动画名和 GUID 获取并解码，不把已有但未解码的动画列为素材缺失。原始 bundle / 对象 / CDN 凭据、逐文件输出哈希及未解码绑定记录见 [timelines-source-manifest.json](official-map/timelines-source-manifest.json)。解码辅助 schema 使用 Unity Timeline 1.7.6、Cinemachine 2.9.7 和 Spine 4.2 官方源码，其下载 URL、SHA256 及 schema 代码哈希均独立记录；这些开发依赖不进入游戏导出。

原剧情引用 139 个导演 GUID，其中 `FeiLeCaptured2` 法杖分支的 `7efc66a61627d5d4c9014986ad32a5cf` 未出现在原版本五包 AssetTable 或已核对的新版 AssetTable 中。相同事件附近另有原版 `CelestialFeatherRealm_Babel_StoryShow02_2.prefab`，目前仍在核对其适用性，尚未声称原缺失引用已恢复。27 项动画绑定 / 缺名动画记录保留在清单；相机跟随、部分材质曲线、复杂 Animator 混合及玩家专属参数演出仍需核对。这不是全部剧情演出完成的结论。

私人副本胜利改为推进一个原始入口状态，避免跳过结晶螺旋树的战后剧情；公共重复挑战保留建筑激活状态。旧本地存档仅恢复尚未播放的原战后检查点，不重复发放已领战斗奖励。

## 2026-10-13 非主线专用面板与原规则接入

本节记录非主线还原实施的实际替换，保留前文来源历史。使用固定 `854_156383` 的 APK 与已校验同版本 CDN，不混入新版同名包，没有新增自绘位图。

运行 `python scripts/prepare_fidelity_ui.py` 转换原公共 ConfirmDialog、TaskPanel 与任务行和详情、AchievementGroupPanel 与组内容和行、TreasurePanel、四槽、套装行、TreasureDetailDialog、TreasurePieceDialog、TreasurePromotePanel 和觉醒选材面板。输入 prefab 的真实 bundle、MD5、对象 ID、纹理输出及 SHA256 记录在 [fidelity/source-manifest.json](fidelity/source-manifest.json)。已转出的星命入口、容器和详情 prefab 尚未完整接入，不将提取完成当作功能完成。

原 Luban 表包括 treasure_suit、achievement_group、fixed_reward、reward_config、system_level_limit、system_level_gap_limit 和 level_prop_default_value；根据原 Console 构造器完整解码并记录哈希。原 WwiseBnk level_prop 二进制使用 LevelPropBinaryReader 的索引和有符号 varint 格式，提取遗物所需 21 个属性类，保留原值和默认值。运行数据进入 `assets/official/fidelity-rules.json`。原语言表按 XXHash64 键读取，任务、成就、遗物描述和 prefab 文本进入 `assets/official/fidelity-texts.json`，原语言文件哈希及输出哈希同样登记。

实际已接入：任务与独立成就专用界面、遗物主页和详情／培养／碎片／套装、原关闭导航及本地恢复公共弹窗。遗物属性计算按 BattleFormulaHandler，替换此前每级固定攻击／生命的简化公式；原槽位升级和累计碎片费用同时用于 UI 与事务。1382 个遗物及其碎片图标来自原 manifest 的精确路径或原子目录下唯一同名对象，没有用别的图标替代。

星命按原 AstrologicalDestiny 代码接回入口、契约、属性、等级与标签、规则以及六种季节 prefab，原长按加速、520 级消耗和属性已接入；季节资格、历史快照、商店和结算仍未完成。不能将六种 prefab 的提取作为季节玩法完整实现的证据。

`prepare_fidelity_pets.py` 转换 40 套原模型、40 条形态绑定及原初始网格动画，使用原 PetUI 与 PetModelSpawnerForUI 的挂点、缩放和皮肤；游戏运行不依赖 Spine。输入 bundle／对象与输入输出哈希见 [pet-models/source-manifest.json](pet-models/source-manifest.json)。`prepare_fidelity_pet_ui.py` 完整读取原幻兽面板绑定、托管 StateSwitcher、语言及 LevelPropBinaryReader 属性，转换详情、食物、潜力行、属性行、进化预览和原幻兽技能图标，来源见 [pet-fidelity-ui/source-manifest.json](pet-fidelity-ui/source-manifest.json)。原技能图标位于 PetSkill 下的原子目录，按唯一同名对象转换；保留实际 GUID 与 bundle 来源。经验与属性规则迁移不等于能力树、编队、导师、祝福及幻兽独立战斗已完成。

原果树、公会首领完整流程、后期养成及原战斗尚未全量完成，未删除仍有引用的共享资源。

## 2026-10-14 地图粒子、剧情序列与战后交互追加

本节更新地图模块的实际接入状态，前文 2026-10-13 的资源数量及“新版尚未接入”保留为历史记录。`prepare_map_particles.py --use-verified-counterparts --upgrade-particle-motion` 检查当前实体、地形、Timeline、Playable 和剧情角色引用的 8,706 个 prefab，转换 3,010 个带粒子的 prefab、41,551 个发射器及原始材质纹理，当前转换依赖无失败。转换结果为空时移除旧缓存，清理 54 个已失效 prefab 的 278 个隐藏发射器。资源、对象、输入哈希、原始 URI 及逐文件输出哈希见 [particles-source-manifest.json](official-map/particles-source-manifest.json)；运行文件位于 `assets/official/map-particles/` 和 `map-particles.json`。未新增自绘位图。

原版仍缺失的粒子依赖通过 `official_counterpart_assets.py` 显式补充。累计使用 68 个已校验的 `864_156496` 独立缓存包，以精确 CAB 身份及可用外部依赖核验后解析；凭据含新版真实 URL、大小、MD5、SHA256、对象 ID 和所对应的原版缺项，见 [particle-counterparts-source-manifest.json](official-map/particle-counterparts-source-manifest.json)。原版预期哈希和缓存没有被修改，新版文件没有冒充原版补齐。

粒子转换保留原生命周期、发射、突发、形状、旋转、速度、颜色、尺寸、UV 帧、网格、状态显隐、材质滚动、噪声参数及拖尾。Unity 内部噪声使用本地 Perlin 播放，噪声算法一致性不作原样声明。子发射器、碰撞及部分 3D 参数仍需继续核对，具体未转换模块保留在来源清单。373 张最新地图截图仍在重新生成。

`prepare_story_playables.py` 转换 36 个原始序列；原引用中两项 GUID 未匹配，未采用相邻素材替代。原始开始时间、持续时间、层级、动画及可解析效果见 [playables-source-manifest.json](official-map/playables-source-manifest.json)，运行数据为 `map-playables.json` 和 `map-playables/`。`prepare_story_actors.py` 转换 95 个剧情角色场景，保留原变换、Animator、动画关键帧与 Spine 分包动画，见 [story-actors-source-manifest.json](official-map/story-actors-source-manifest.json)。

剧情副本引导根据原目标建筑推进入口点击状态，再接续原私人副本；

躲猫猫准备区限制按原 `MapHideAndSeekInputSupportHandler`、`HideAndSeekHiderVisualStateUtils` 和 `Region0Enterable` 实现；生存金币按原间隔和金额执行，感染后的追捕身份按原 GameMode 区分。完整雷霆视觉演出仍在核对。全部主线仍在通过原移动、机关、任务和剧情进行连续验收，当前记录不表示所有主线已经通关。

## 2026-10-14 非主线幻兽能力与技能追加

`prepare_fidelity_pet_ui.py` 当前转换 33 个原 prefab／引用模板和 857 组完整字段／材质绑定，包含能力图与连线、培养／重置、技能详情、进阶、通用碎片确认和原技能标签模板。遗物绑定采用相同修复。原 TextMeshPro 的自动字号、换行和溢出设置保留在转换数据中。来源、纹理、字段和输出哈希见 [pet-fidelity-ui/source-manifest.json](pet-fidelity-ui/source-manifest.json)。原能力培养、技能进阶、取消、重置返还已实际接入；原编队、筛选和循环成就已接入；改名、导师、后期祝福、经验喂养成就统计和独立实体战斗仍未全量完成。

`prepare_fidelity_skill_rules.py` 依据原 Luban 构造器、MessagePack 格式化器及静态类型解析器，严格解码 3,824 个技能／状态／命中实体和 108 个等级属性类。运行数据位于 `assets/official/skill-rules.json`，输入配置、源码、格式化器及输出哈希见 [skill-rules/source-manifest.json](skill-rules/source-manifest.json)。原阶级倍率、等级属性、被动、召唤加成、显示排序及隐藏规则用于技能详情与升阶预览；完整技能战斗没有因此自动完成。

编队遮罩、筛选、成就、技能详情、进阶、重置、确认及能力重置截图已目视复核。没有原 APP 同状态实机截图，不作为全范围或逐像素一致结论。

原 PetPanelWriteMask / PetPanelMask 材质的 ColorMask、Stencil 比较与操作、AlphaClip 及九宫格图片已保存到转换记录；原 LoopScrollGridLayoutGroup 的最小间距、列数及动态间距由原程序集反编译核对。没有新增自绘图片。原成就循环与战力评分的源码哈希见非主线实施证据。

## 2026-10-14 非主线装备强化追加

`prepare_fidelity_equipment.py` 从原 promotepanel / character 包转换 18 个 prefab／引用模板，严格读取 213 组字段绑定、9 张原表及 101 个等级属性类；七种 EquipQualityBg 按 LanguageUtils 的资源名和原图子目录定位。来源、源码、构造器、原始字段及输入输出哈希见 [equipment-fidelity/source-manifest.json](equipment-fidelity/source-manifest.json)。强化、锁定和分解规则已接入；继承、宝石、共鸣等面板当前仅完成转换，未全量接入。新装备按原权重及参数生成，普通／特殊／被动词条与互斥、套装已参与属性与战力；旧实例保留兼容值。继承、镶嵌、完整说明及后期祝福仍待完成，不以素材转换宣称装备完整一致。

记录见非主线实施证据。没有新增自绘素材。

## 2026-10-14 人物属性与技能槽位培养

`prepare_fidelity_skill_rules.py` 新增原人物等级、位阶、战斗角色组和非战斗属性类，总计 108 类。、 按原属性处理与分块评分接入；

`prepare_fidelity_skill_ui.py` 从固定 854_156383 的 promotepanel、skill、itemdetail、item、character、commonnotify、public 包转换 17 个原 prefab／模板，严格读取原序列化对象字段，含技能碎片详情、合成确认、数量进度、普通技能详情及批量升阶通知模板；两张原技能槽位费用表共 1,270 行，原 SkillItemRangeInfos.g.bytes 严格按 MessagePack 索引读取 1,365 份作用范围。输入版本、CAB／对象、原始数据、源码和输出哈希见 [skill-cultivation/source-manifest.json](skill-cultivation/source-manifest.json)。固定 `item` 表漏掉的 18 个碎片 ID 由 `Formula.GetPieceIdBySkillId` 反推对应技能；同一配置中其余 190 条技能碎片行的名称后缀、RawIcon、ItemType、Rank、Quality 及所有展示相关字段取值一致，运行时只对这类技能 ID 使用该一致映射生成缺失元数据，不将其记录为原始缺失行或服务器返回值。槽位升级音效沿原 CharacterPanelSkillItem.LevelUpSound 的 WwiseObjectReference、Event、Play、Sound 与媒体链转换，输出 `assets/official/skill-audio/473101969.ogg` SHA256 `421c23910fd77aaf8a184f3e2fc439b80df147e195eb3ec41c798d36b658773e`；Unity 播放载荷为 `unity/Assets/Official/Skills/Audio/Skill_Upgrade.ogg`，引用于角色资源目录。原 GlobalColors 品质及进度颜色配置另存于运行数据；炫彩渐变的边界在技能功能文档维护；ArrowLoop 的两条空绑定已核对原 prefab 全部 Transform，原层级本就没有对应目标，没有新增自绘素材。

## 2026-10-14 非主线战斗公式与实体配置

`prepare_fidelity_battle.py` 从固定 854_156383 的原始表和 config_ec 转换 7 张表、45 个怪物等级属性类、7,417 个原实体，共 139 类组件，当前解码无失败且严格核对全部组件字节。包含 471 种怪物及 2,699 组技能／属性配置。原有同 ID 的旧格式实体优先使用相同客户端内的正式配置，选择、跳过范围、原始来源、格式化器及哈希均保留在 [battle-rules/source-manifest.json](battle-rules/source-manifest.json)。运行数据为 `assets/official/battle-rules.json`，纳入显式资源清单。

、 移植原伤害公式与怪物属性计算。`generate_battle_reference.py` 编译原 C# 方法生成 97 组伤害及 8 组怪物属性对照，只去掉日志并提供配置读取适配；输出和源码／校验程序哈希见 [damage-reference.json](battle-rules/damage-reference.json)。追加 [secondary-reference.json](battle-rules/secondary-reference.json) 的 72 组原 C# 治疗、效果命中、生命消耗、护盾换算等对照；副本执行器尚未接入完整实体流程；素材／数据转换和独立公式通过不代表实际战斗已经还原。没有新增自绘素材。

## 2026-10-14 地图复杂音乐与活动首领追加

地图剧情挂点补充：`scripts/prepare_map_story_bindings.py` 检索固定版全部 MapWorld 逻辑实体 prefab、地图显示 prefab，以及 373 个地图的基础地形和分区昼夜场景，从原 `StoryNodeRegister` / `StoryNodeDelayRegister` 提取目标与父级变换。11 个剧情引用键全部找到，分布在 13 个原逻辑实体类；没有缺项或解析失败。转换结果为 `assets/official/map-story-bindings.json`，原 schema、bundle / 对象、输入哈希、原坐标及输出哈希见 [story-bindings-source-manifest.json](official-map/story-bindings-source-manifest.json)。重复分区使用同名注册键时，离线运行选择当前地图最近的可见注册实体，此选择规则单独登记，不声称与 Unity 流式加载顺序相同。

重建挂点使用 Python 3.12 与既有 UnityPy / numpy 开发依赖，设置 `PYTHONPATH=.tools/character-art-python;scripts` 后运行 `scripts/prepare_map_story_bindings.py`，再运行 `scripts/register_map_assets.py`。`GetEntityPos`、坐标类型转换、NPC 删除和镜头目标使用实际原实体引用；相机转换包含地图原点与格子中心。原三处 `TimeSequenceNode` 的七个并行移动执行器，以及主线的移动交互节点，接入本地保存的独立时钟；没有新增或替代图像。

全屏剧情插图复用已转换的原 `StoryScreenImagePanel`、`map-story-media.json` 中的官方插图与 `screen_image_panel_front_shake` 原曲线。淡入、双图混合、关闭淡出、等待震动结束和演出时钟按原节点执行；插图面板与曲线不是自行绘制或合成的素材。

两场音乐节的 `Map/Common/FirstMusicFestival/7003_view` 与 `Map/Common/LoTIanYiMusicFestival/7003_93_view` 原 `ECEntityOverlapDisplayView.mDisplaySwitcher` 控制占用时的粒子显隐。`prepare_map_interactions.py` 保留该显示类型，`prepare_map_particles.py` 按原布尔状态转换隐藏的官方发射器；运行时检查角色与可见实体的实际身体格交集。未占用、占用与移开使用原粒子资源，输入／输出哈希保留在 `particles-source-manifest.json` 和显式资源清单；没有自行绘制踏板高亮。

`prepare_map_audio.py` 搜索全部已校验的原版 Wwise PCK，随后运行 `prepare_map_music.py` 转换复杂事件；重建时保持这个顺序，后者会扩充前者生成的目录。复杂转换严格解析 113 个原始 bank、734 份 media，使用 [wwiser](https://github.com/bnnm/wwiser) `v20260808`（commit `94580dd3de3280933d69c435e96d4e2c3b443207`）生成原生 TXTP，以 [vgmstream](https://github.com/vgmstream/vgmstream) `r2117` 渲染原始分段、叠层、音量和循环，再用 libsndfile 编码本地 Ogg。保留原 bank 音量、开头与循环起点；没有合成或替代音乐。15 个复杂音乐事件共有 29 个配置分支，另转换 3 份 Map23 过渡音频；13 段内嵌 bank 语音亦已取得。版本、包来源、输入和输出哈希、严格解析结果及工具哈希见 [music-source-manifest.json](official-map/music-source-manifest.json)。

运行目录共覆盖 4,632 个音乐／语音事件、49 个 BGM 编号；火／冰、普通／混沌、三种岩层区域和活动歌单按原控制事件选择。`chapter_5_main_36_17` 在已取得的原版音频包及对应事件检索中未找到，继续保留缺项；Map23 的九段顺序过渡片段与原 132 BPM／四拍节拍、来源专用过渡规则、淡入淡出、前奏／尾奏提示点已接入跨区域播放，记录见 music-source-manifest.json。

`prepare_map_activity_boss.py` 按固定版原构造器完整消费解码 4 张首领表，另解码 `monster_group`、`step_award`、`chest` 的共享池和奖励。来源、构造器与转换器哈希、字节数和输出哈希见 [activity-boss-source-manifest.json](official-map/activity-boss-source-manifest.json)。地图 94、95、96、98 使用原首领、模型挂点、面板、关卡、难度修正、每日挑战／参与奖／击败上限、伤害阶梯和宝箱奖励；大额伤害拆成有界整数保存，符合现有地图存档格式。使用已转换的原始怪物属性公式，实际回合仍由项目本地战斗执行器结算。

原联网全服首领改由本地玩家累计伤害，个人与队伍成绩均来自本次单机角色；没有生成其他玩家成绩。普通首领使用原 region-zero 关卡；首次生成的首领等级按角色阶位约束在原修正表的有效等级范围内，随后保存。周年庆保留原二十倍场外生命值和每日最多三次击败，重复挑战不额外设置次数上限。没有新增位图。

## NPC 助战原规则迁移（854_156383）

`scripts/prepare_fidelity_npc_assist.py` 从固定 APK 的 `defaultpackage_assets_config_luban.bundle` 读取 `npc_level_detail`、`npc_profession_detail`、`player_robot_invite`、`team_target`、`game_play_mode`，逐字节对照提取缓存后按原构造器完整解析，共 5 表 2,178 条。另从已校验原版本 WwiseBnk 的 `level_prop.g.bytes` 提取 450–456 共 7 个属性类，输出 `assets/official/npc-assist.json`，纳入显式加载清单。

来源、对象 ID、输入／输出哈希、原代码哈希和音频包中的属性原始文件凭据见 `assets/reference/npc-assist/source-manifest.json`。`scripts/generate_npc_assist_reference.py` 编译固定原 C# 属性方法，产生 24 组 NPC 属性及 7 组最终属性对照，见同目录 `reference.json`。没有新增图像、自绘或新版混入；这里的转换与规则验证不表示邀请界面、助战等级下发或实际战斗已经完成。

### 羁绊冒险战斗伙伴模型与战前过场

`scripts/prepare_npc_adventure_models.py` 根据固定版 `npc`、`npc_level_detail` 与 `FightEntityId` 确定 95 名可配置羁绊助战候选；固定版 AssetTable 中有 91 名候选的 `FigureModelUI` 与 `FigureModelWithWeaponUI` 原 prefab 路由，并据此转换皮肤合成、SkeletonGraphic、Idle／Run 动画及引用贴图。输入对象、原客户端源码、转换器与输出哈希见 [npc-adventure-models/source-manifest.json](npc-adventure-models/source-manifest.json)。运行时按原 `NpcAdventureModelItem` 规则优先选择 WithWeaponUI，缺失时回退 UI 模型，并读取该 NPC 的原 IdleAnimName／RunAnimName。

固定版 AssetTable 的 UI prefab 路由仅覆盖其中 91 名；11015–11018 的 FigureModelUI 与 FigureModelWithWeaponUI 均无路径记录。已取得的固定版 NPC 骨骼包不含这些 prefab；新版 `864_156496` manifest（`.tools/source-packages/cloud-research/current-manifest.v3.bytes`，SHA256 `30d8debb4b9541668321f57c5e116e916d38a8bcb7a78f3b2f3161a902acfbb7`）也无四名 NPC 的对应 UI prefab 路径。因此来源清单分别记录全部候选、91 名可转换候选及四组缺失路径，不重建替代 prefab。

羁绊主角复用固定版男女角色资源；`scripts/prepare_profession_appearance.py` 同时输出原 `100/run_03`、`200/run_03` 职业动作，再由 `scripts/prepare_character_appearance.py --art` 转换到实际使用的男女外观皮肤和武器。战前曲线来自 `assets/official/ui.json` 的 `NpcAdventurePanel_CombatTrigger`（0.5666666627秒）；原 `NpcAdventurePanelMainContent.cs` 先停地图旋转并切换待机，再由 `NpcAdventurePanelMainContentAnimController.cs` 在动画结束后调用挑战。适配沿用项目离线战斗执行，不改原角色、伙伴、动画或战斗配置，不绘制替代素材。

## 2026-10-14 周年庆地图修复与商店补齐

`prepare_map_anniversary.py` 从固定版本官方 `defaultpackage_assets_config_luban.bundle` 完整解码 `anniversary_map_build`、`anniversary_map_build_item`、`activity_festival_shop` 三表，保留逐表输入／构造器哈希和消费字节数，生成 `assets/official/map-anniversary.json`。原建筑与 NPC Tag 对应八组修复交互；活动 217 对应原商店 514 的十九件商品。二十九个材料、奖励和商品图标从原 APK／已校验同版本 CDN 的原 Sprite 提取，没有自绘补充；来源、实际 URL、bundle、对象、输入／输出哈希见 [anniversary-source-manifest.json](official-map/anniversary-source-manifest.json)。

运行界面使用原 AnniversaryMapBuildDetailDialog、AnniversaryMapBuildSelectDialog、材料 Item 与 ActivityFestivalAnniversaryShopPanel／ShopItem，显示原建筑模型。每日总提交二次、每建筑限额、物资消耗、修复进度与奖励、完成奖励防重、原货币与库存均本地执行。

联网共同建设采用离线适配：首次进入开始本地计时，阶段长度使用原 168／168／167 小时；庆典阶段自动完成共享修复并保留个人贡献。该阶段自动完成属于本项目离线设计，不能据此声称已验证原服务器的自动完成规则。
# 幻兽改名来源补充（2026-10-14）

`scripts/prepare_pet_naming.py` 从固定 `854_156383` 的 `prefabs_ui_inputdialog` 提取原输入面板、字段绑定、显示／隐藏动画；确认面板复用已转换的同版 `ConfirmDialog`。原 `MainCanvas` 的 `WindowMaskType.Mask` 图像色值为 `[0,0,0,0.7803921699523926]`，另有原模糊确认遮罩。中文字符集经 `CustomContentSettings.LanguageType=0` 的 GUID 定位原 `FZY4K_Characters.txt`，共 21,993 字符；未混用其他地区字体集合。

运行数据为 `assets/official/pet-naming.json`，来源、对象 ID、原始字节、字符集、原语言包及输出哈希见 `assets/reference/pet-naming/source-manifest.json`。`scripts/generate_pet_name_reference.py` 在开发时执行原 `CJK.cs`，37 个字素权重向量及原代码／执行器哈希保存在同目录 `reference.json`。外部文字审核跳过，服务器缺失的次数变更采用已登记的本地规则；这两项不宣称为原服务器实现。原自定义名四角颜色渐变仍待全局文字效果适配。

## 赛季依赖与装备继承接续（2026-10-14）

`prepare_official_seasons.py` 按固定 `854_156383` 原 Luban 构造器转换 14 张赛季依赖表、642 行，并记录服务器阶位／赛季日数条件、原 UTC 日界与发布类型的代码指纹，见 [seasons/source-manifest.json](seasons/source-manifest.json)。独立服务器阶位不按人物进度补造历史；手动切季仍待完整资格及结算实现。

`prepare_fidelity_equipment.py` 新增原继承选材详情、两种词条模板及原词条说明语言，当前共 21 个 prefab／引用模板、287 份完整组件字段和 10 份 TMP 文字记录、9 张表、101 个属性类；重复引用路径合并后为 220 组不同的路径／脚本绑定。按原 `RawIcon` 解析固定版装备图标，1223 个装备定义均有映射，来源清单包括 530 个装备图标对象，共 654 项图像／资源记录。输出仍为原 Sprite 的裁切／转换；没有自绘或混入新版。来源、对象与输入／输出指纹见 [equipment-fidelity/source-manifest.json](equipment-fidelity/source-manifest.json)。

继承使用原帮助函数的条件式宝石复制，目标已有宝石时保留目标；本地对未转移的来源宝石做等量返还，对销毁的已穿戴来源清除引用。这两项没有服务端实现证据，按离线适配登记，不声称已还原原 `AwardData`。NPC 对话入口、成功奖励演出及完整动态附件仍未完成；

### 星石镶嵌与专用详情追加

上述 21 个模板和 297 份输入记录是继承模块完成时的范围。继续从同一固定版本提取 `prefabs_ui_itemdetail` 的原 `GemDetailDialog`，追踪其完整组件字段及 TMP 换行、溢出和边距；原语言补齐星石说明及镶嵌部位。当前输出为 22 个 prefab／引用模板、295 份完整组件字段、68 份 TMP 输入记录（58 个不同文字路径）、228 组不同路径／脚本绑定、655 项资源记录及 58 份原代码指纹。原星石图标全部复用已转换同版素材，未增加自绘或新版来源。字段输入与去重后绑定数量分别登记，不合并为一个夸大的“绑定数”。

 依据原 `GemPartInfoParser`、`GemPropChunck`、`PlayerEquipDataExt`、`EquipLevelCondition`、候选比较器与镶嵌界面实现；360 个固定版本星石均使用特殊属性组首组。 接入原槽位／列表、短按镶嵌、一秒长按专用详情、移动取消和外点击。卸下时返还一枚原星石是服务端 `AwardData` 缺失时的离线所有权适配，不标为已取得原服务端返还实现；未补造手续费、额外奖励或材料返还。

地图 94 活动首领使用固定版本 `854_156383` 的原 Luban 配置、既有官方怪物及面板。`scripts/prepare_map_activity_boss.py` 解码原七张表，运行数据按已提交地图筛选；各原始表完整消费字节数、bundle 及输入哈希、筛选范围和输出哈希见 `assets/reference/official-map/activity-boss-source-manifest.json`。

地图 96 活动首领使用固定版本 `854_156383` 的原 Luban 配置、既有官方怪物及面板。`scripts/prepare_map_activity_boss.py` 解码原七张表，运行数据按已提交地图筛选；各原始表完整消费字节数、bundle 及输入哈希、筛选范围和输出哈希见 `assets/reference/official-map/activity-boss-source-manifest.json`。

地图 98 活动首领使用固定版本 `854_156383` 的原 Luban 配置、既有官方怪物及面板。`scripts/prepare_map_activity_boss.py` 解码原七张表，运行数据按已提交地图筛选；各原始表完整消费字节数、bundle 及输入哈希、筛选范围和输出哈希见 `assets/reference/official-map/activity-boss-source-manifest.json`。

地图 95 活动首领使用固定版本 `854_156383` 的原 Luban 配置、既有官方怪物及面板。`scripts/prepare_map_activity_boss.py` 解码原七张表，运行数据按已提交地图筛选；各原始表完整消费字节数、bundle 及输入哈希、筛选范围和输出哈希见 `assets/reference/official-map/activity-boss-source-manifest.json`。

地图 92 音乐节使用固定版本 `854_156383` 的活动配置、原面板、人物与武器 Spine 数据。原始绑定及输入/输出哈希见 `official-map/activity-hud-source-manifest.json`、`player-actions-source-manifest.json` 和 `music-weapons-source-manifest.json`；荧光棒按原共享武器预制体比例接到当前男角色的原持械骨骼。服务端处理器缺失，离线报名免费发放一根所选荧光棒，应援每原配置 29.9 秒使用音符奖池 73005，奖励使用原首日分组上限并持续保存、不随重进刷新；离场回到原默认入口。这些是本地执行策略。 固定表已退休地图 92 的迎宾绑定，本地仅对原 TrackTag 重接仍保留的 MusicPartyWelcome1/2 原图。

本会话剧情挂点使用固定版原 StoryNodeRegister / StoryNodeDelayRegister，来源、原坐标、父级变换及输出指纹见 official-map/story-bindings-source-manifest.json。剧情演出的既有官方 Timeline、MyPlayable、角色与插图转换依赖随地图 12 提交，来源分别见 timelines、playables、story-actors、story-media、ui-spines 来源清单；未补造挂点或插图。

星系 UI 测试使用的原星系层级、故事书、插图、彩蛋和原面板依赖，来源分别见 official-map/galaxy-source-manifest.json、ui-source-manifest.json 及 ui-spines-source-manifest.json。运行时只加载显式清单中的本地转换资源。

地图音频测试所依赖的原 Wwise 音乐分支、桥段及混音资源，输入、工具版本、实际输出与哈希见 official-map/audio-source-manifest.json 和 music-source-manifest.json。混音在开发时完成，运行时离线播放本地资源。

音乐踏板使用原 ECEntityOverlapDisplayView.mDisplaySwitcher 的占据状态及官方粒子，未绘制高亮替代图。按实际使用的踏板 prefab 筛选输出，来源和转换指纹见 official-map/particles-source-manifest.json；不代表全部世界粒子已接入。

## NPC 继承入口与遗物培养追加（2026-10-14）

原梅克娜对话分支 23002、动作 12 和继承面板复用已转换固定 `854_156383` 素材，不新增自绘或替代入口。原分组构建器、条件过滤、无参数打开／等待关闭代码及节点记录见 [equipment-npc/source-manifest.json](equipment-npc/source-manifest.json)。地图内选材／确认、退出载入与关闭已接入；原 S2 资格不变，普通新档切季仍未完成。上文“NPC 入口未完成”保留为首次继承模块完成时的历史状态，以本段及最新接续记录为当前范围。

`scripts/prepare_treasure_cultivation.py` 从已校验同版 `defaultpackage_assets_prefabs_ui_treasure.bundle` 补读 7 份原培养组件字段，合并到已有 `relic_promote`／`relic_awake` 树，原图片及几何保留。原始包／对象、字段原字节哈希、客户端源码和输出指纹见 [treasure-cultivation/source-manifest.json](treasure-cultivation/source-manifest.json)。实际接入独立强化／觉醒自动合成开关、原材料递归及最大星级状态；没有新增图片。固定材料表没有通用碎片占位，品质 5 普通觉醒消耗原 120 个 733，未为展示选择器自造需求。

遗物批量 RPC 的返回结构及缺失处理器检索见 [treasure-cultivation/batch-research.json](treasure-cultivation/batch-research.json)。技能通知原 `ArrowLoop` 的两条失效路径与原 prefab 完整层级核对见 [skill-cultivation/loop-binding-research.json](skill-cultivation/loop-binding-research.json)；保留原引用及未转换记录，未补造目标。两项均为来源研究，未新增运行时素材。

地图 93 音乐节使用固定版本 `854_156383` 的活动配置、原面板、人物与武器 Spine 数据。原始绑定及输入/输出哈希见 `official-map/activity-hud-source-manifest.json`、`player-actions-source-manifest.json` 和 `music-weapons-source-manifest.json`；荧光棒按原共享武器预制体比例接到当前男角色的原持械骨骼。服务端处理器缺失，离线报名免费发放一根所选荧光棒，应援每原配置 29.9 秒使用音符奖池 73005，奖励使用原首日分组上限并持续保存、不随重进刷新；离场回到原默认入口。这些是本地执行策略。 舞台歌手、原生登场触发器和唱跳动画、动作持续时间的来源见 `official-map/music-stage-source-manifest.json`。原舞蹈状态的 64 位确定性选择器按本地演出时间执行，演出批次取该活动原通用区域（Region 0）时间表最早安排的一场并立即开始；已完成演出重新进入会重开该场，奖励上限保留。这些是离线时间表适配。 三张音乐节地图的音符与聚光灯使用本活动原事件组件，避免与其他场景同编号怪物混用；在本地随机选择原区域内的可行走格，原事件数量和奖池保留，经过物件即拾取且仅发奖一次。原音符粒子的一项共享依赖在固定版本缺失，独立使用已校验的 `864_156496` 同名包：`defaultpackage_share_534482ac5bf3346d8821a9effb2cc43c.bundle`，仅以完全相同 CAB 身份解析所需对象并核对依赖。新版来源及原缺项继续分别登记于舞台来源清单；未覆盖原缓存或修改预期哈希。 服务端路径与事件物件到期处理缺失，离线同类事件每批替换上一批物件；演出重开清理前场物件，分组奖励计数继续保留。

地图 97 音乐节使用固定版本 `854_156383` 的活动配置、原面板、人物与武器 Spine 数据。原始绑定及输入/输出哈希见 `official-map/activity-hud-source-manifest.json`、`player-actions-source-manifest.json` 和 `music-weapons-source-manifest.json`；荧光棒按原共享武器预制体比例接到当前男角色的原持械骨骼。服务端处理器缺失，离线报名免费发放一根所选荧光棒，应援每原配置 29.9 秒使用音符奖池 73783，奖励使用原首日分组上限并持续保存、不随重进刷新；离场回到原默认入口。这些是本地执行策略。 舞台歌手、原生登场触发器和唱跳动画、动作持续时间的来源见 `official-map/music-stage-source-manifest.json`。原舞蹈状态的 64 位确定性选择器按本地演出时间执行，演出批次取该活动原通用区域（Region 0）时间表最早安排的一场并立即开始；已完成演出重新进入会重开该场，奖励上限保留。这些是离线时间表适配。 三张音乐节地图的音符与聚光灯使用本活动原事件组件，避免与其他场景同编号怪物混用；在本地随机选择原区域内的可行走格，原事件数量和奖池保留，经过物件即拾取且仅发奖一次。原音符粒子的一项共享依赖在固定版本缺失，独立使用已校验的 `864_156496` 同名包：`defaultpackage_share_534482ac5bf3346d8821a9effb2cc43c.bundle`，仅以完全相同 CAB 身份解析所需对象并核对依赖。新版来源及原缺项继续分别登记于舞台来源清单；未覆盖原缓存或修改预期哈希。 服务端路径与事件物件到期处理缺失，离线同类事件每批替换上一批物件；演出重开清理前场物件，分组奖励计数继续保留。 返场演出沿用原批次 4，第 846 秒登场的原角色 7019 使用其官方 npc_11033 骨骼和原唱跳绑定。最后一曲 BGM 214 使用固定版本原音频包 activity_025_luotianyi_2_cn 中的 902121378，原 Wwise 媒体解码为本地 Ogg；包来源、媒体偏移及输入/输出哈希见 audio-source-manifest.json。
# 2026-10-15 商城支付与确认布局复核

购买选择依据固定 `854_156383` 的 `CommonPayBtn`、`PayGiftConfirmDialog`、`PlayerPeriodicPayGiftDataExt` 及已转换 `commerce-ui.json`。旧购买入口接回足额代金券优先规则，确认弹窗恢复原布局和奖励网格；本项没有新增或重绘图像。原服务器支付订单仍以原报价的本地成功事务适配。原期数选择和所有活动仍未据此完成。

### 2026-10-15 地图 10 原生材质与 JSON Spine 纠正

地图 10 单独重建固定版本 `854_156383` 的 `MapGround_10.prefab`；来源仍为原 APK / 已校验原 CDN。`scripts/prepare_map10_materials.py` 仅输出地图 10 的地形、独立蝴蝶数据与水面 10 条目，不改共享 `regions/skeletons.json`，不生成新版替代或自绘图。详细原对象、输入 / 输出 SHA、旧输出与纠正依据见 `official-map/materials-10-source-manifest.json`；原 `regions` / `water` 来源历史保留。
Map10 Windows 原生地形和怪物帧资源由 `scripts/prepare_unity_map10_rendering.py` 生成，逐项来源、路径和 SHA256 见 `unity/Assets/Official/World/Map/10/rendering-source-manifest.json`。地形来自固定版 `assets/official/regions/10.geom`（SHA256 `cdd26c364ce85d62bd5225bd23fda7b1f7b5ec7d7dd446fba5f3bec6f08618ff`），恢复 1,404 个地形网格批次和 166 张原始纹理；11 个地图战斗怪物按 `map10.json` 官方 `Stages` 值关联原始模型及骨骼 idle 帧，共 309 帧。`scripts/prepare_unity_map10_battle_data.py` 按固定 APK 的官方 `TbStageInfo`、`TbStageMonsterArrayInfo`、`TbSkillInfo`、`MonsterGroupInfo`、等级属性及已核验战场数据生成 21 个阶段、13 个阵形、逐只怪物的战斗属性/技能/站位及对应战场网格；`battle-rules.json`、`skill-rules.json`、`rules.json` 和公式源码均校验固定版本及来源哈希，详细输入输出见 `assets/reference/official-map/map10-battles-source-manifest.json`。模型、骨骼及 atlas 的原始来源继续由 `models-source-manifest.json`、`spine-pages-source-manifest.json` 和 rendering manifest 交叉记录。Unity 侧由 `OfficialMap10TerrainView`、`OfficialMap10MonsterView` 生成运行时网格；地形水效、实体场景接入和游戏运行尚未完成。

Map10 的 `FeiLe_appear`、`FeiLe_disappear`、`npc_disappear` 由 `scripts/prepare_unity_map10_playables.py` 按原始 playable 与 `map-particles.json` 转为 Unity Resources 粒子载荷，并校验材质、贴图、网格及输出 SHA256；详细输入/输出见 `assets/reference/official-map/map10-playables-source-manifest.json`。固定版缺失材质 `2db68f5a7be450142e43` 的原 CDN 包 MD5 为 `959e064e1a060a6ed93bad71ca11a8f0`，已按独立记录使用官方 `864_156496` 同名包 `758d1b10bbe6ee211e8de79dbb727ec1.bundle`；未将其标为固定版来源。其他材质来源均逐项注明 `854_156383` APK 或原版本 CDN。原 APK 的 GLES 编译 shader 无 Windows HLSL，`OfficialMap10PlayableView.shader` 是经用户确认、按官方材质参数和贴图重建的 Windows shader，不宣称逐像素等同。

Map10 `MapBgmId=10` 的固定版 `bgm_event_key` 记录为 `Map_10_Play/Stop/Pause/Resume`；`Map_10_Play` 复用已核验 MapAudio OGG，离线运行以循环音轨适配官方 BGM Play/Stop 生命周期。剧情和 MapTalk 共 39 个 `VoiceEventKey` 由 `scripts/prepare_unity_map10_audio.py` 接入 `unity/Assets/StreamingAssets/Map10Audio/`：25 个 OGG 复用已核验的 `assets/official/map-audio.json` 与 `audio-source-manifest.json`，14 个缺失项从固定 APK `zh_cn_voice_chapter_01_pck.rawfile` 按官方 Event→Action→Sound→WEM 解码为 PCM WAV。三个 `FeiLe_appear`、`FeiLe_disappear`、`npc_disappear` playable 声音事件也按原轨道加入本地映射；声音媒体分别位于固定 `map_sfx_01` PCK 中记录的 bank/offset/size，提取器校验包 MD5、银行范围和 WEM SHA256。对话/演出时间、原来源、输出哈希与映射见 `assets/reference/official-map/map10-audio-source-manifest.json` 和 `Map10Audio/map10-audio.json`；运行播放由 `OfficialMap10AudioPlayer` 绑定。

8402 初始岛故事 NPC 的 `npc_8402/appear_01`、`npc_8402/disappear_01` 由 `prepare_unity_map10_rendering.py` 从固定版 `story-actors-source-manifest.json` 验证并生成 Unity 原生帧载荷；帧数、贴图与输出 SHA256 记在 `unity/Assets/Official/World/Map/10/rendering-source-manifest.json` 的 `storyActors` 条目中。两个载荷复用官方 `npc_8402` atlas，不含自绘资源。

26 个 `Sprites/Submersion` 分层保留 Rock01/Rock03/Rock04 的原 `_HeightMap`、atlas UV、renderer 高度偏移和 0.5 水位；官方 `General_Manual_mask_atlas` 解码为原贴图。水下层在水面后绘制，上水层在水面前绘制。4 个原 `Sprite-Lit-CustomWater2` 水面仅在地图 10 使用原 GLES 的红色高度、深浅色、岸线噪声、法线、焦散和泡沫参数。原 `Sprites/WaterMask` 的 `clamp(texture.a*renderer.a+renderer.r-renderer.g)` / `BlendOp Max` 按 430 次原遮罩绘制离线合成为 `assets/official/map-water-masks/10.png`；world rect 为 `[-24,-29,50,53]`，64 像素 / 世界单位，3200×3392。这是官方数据派生转换，使用固定采样密度，不声称与 Unity 的逐帧屏幕像素完全相同。

历史 `missing_skeleton_data: Effect_Butterfly` 是转换器只接受 `.skel`、漏识别原 `.json` 导致的误判，不是官方素材缺失。原包实际含 JSON Spine 4.1.17、atlas、Texture2D 和动画。新增 `assets/official/regions/10-butterflies.json` 保存 15 个原激活实例、原分配的 `loop1` / `loop2`、12 fps 网格帧与原精确循环时长；同时接入 `Eff_Scene_Butterfly01` 原飞行 / 旋转曲线（3.983333349 秒循环）。15 个静态序列化网格实际剔除，原 `Effect/Noise_Mask_Pro` 的 `USE_MAINTEX`、绿色 `_MainColor` 和 `_ColorIntensity=4` 应用到动态网格。旧缺失记录保留并在新清单中明确纠正。转换后地形为 1404 批、17856 顶点，15 个独立原骨骼实例，不再绘制原静态蝴蝶替身。

Unity 资源转换器现将 Map10 的 4 个原水面网格、15 个蝴蝶骨骼实例、两段蝴蝶动画和 5 张官方贴图复制到 `Assets/Official/World/Map/10/Resources/Map10/Environment/`。`map10-water.json` 是固定版 `map-water.json` 的 Map10 条目，`10-butterflies.json` 保持原始字节；对应的 `map10-water-runtime.json`、`map10-butterflies-runtime.json` 和两个循环帧载荷供 `OfficialMap10EnvironmentView` 运行时实例化。`rendering-source-manifest.json.environment` 记录固定版输入哈希、输出哈希、纹理 URI 到 `Resources.Load` 路径、材质参数与蝴蝶动画帧数。水法线按 NormalMap/线性导入，派生水遮罩按线性导入；`Map10Water.shader` 和 `Map10Butterfly.shader` 是经用户确认按官方参数、关键字与贴图重建的 Windows shader，保留来源值但不保证与 GLES 逐像素相同。Unity `Zjcs.Editor.ProjectTools.Validate` 已通过水面/蝴蝶载荷、贴图绑定、shader 导入及 19 个运行时渲染器检查；完整地图 Play Mode 流程仍未验收。

CAB 读取器通过原清单中唯一相同 MD5 解析旧缓存的 `_materials.bundle` 别名；真实名称为 `defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle`。原 APK、缓存、预期 MD5 / SHA 和来源版本均未修改。新资源须在协调会话最终共享注册与素材包锁生成时一并纳入。

### 2026-10-15 地图 10 祈愿教学与共享注册

祈愿教学使用固定 `854_156383` 的原 `FurnitureRepairDialog`、`FurnitureRepairedNotifyDialog` 与 `StatueOfGod` prefab，运行目录为 `assets/official/home-wish-tutorial.json`；独立来源为 `home-wish-tutorial/source-manifest.json`，保留原 prefab / bundle / path_id、客户端代码指纹、9 项原语言键和 18 张既有官方贴图来源。18 张贴图的重新解码结果、旧共享清单与当前磁盘 SHA 完全相同，本次仅复用，未新增或重绘这些贴图。最终教学 catalog SHA 为 `aad8443772a737548cad9f88eb009f25ce93087c46c9641c25c37a8ba1dc94e8`，独立来源 SHA 为 `8c7653b998fef0fe73f70501a84af496c67ced42c67512390867d9dcfd331b60`。

教学在原家园设施碰撞入口执行修复、成功通知、奖励与实际祈愿点击检查点，使用原家具 4004 条件 / 修复费用、fixed_reward 8 奖励及原祈愿 / 快捷购买价格；本地事务与持久化承担离线执行。其他家园教学图的完整调度、场景 NPC IdleTreeRight / PortalLeave 演出和原家具动态分类不在这次适配范围。

另一张 `FirstMergeItem` 教学图请求固定奖励 ID 7。固定版 `fixed_reward.g.bytes` 由原 `TbFixedRewardInfo` 构造器完整解码，ID 7 是罗拉 350、水 10、青禾 5，`CanGetTimes=1`；`prepare_unity_wish_catalog.py` 将 ID 7 与祈愿教学奖励 ID 8 一起写入 Unity 运行目录，输入和输出哈希记录在 `assets/reference/wishes/source-manifest.json`。

临时来源 `assets/reference/official-map/wish-tutorial-source-manifest.json` 已迁至独立模块；原来源 SHA `d59a5aefd96ee96229452f35357c73f85bebd4a401b027679e255489d85d6186` 与旧 catalog SHA `4da99eb146fe351591fdb6af7ae25abcbb387f851c4c9e024d64084b3f6f2261` 继续保留于迁移记录，没有改写成新的来源历史。

最终共享登记使用 `python scripts/register_map_assets.py --map10`，仅合并 `official-map/materials-10-source-manifest.json` 与独立教学模块，核对实际文件 SHA，保留同字节复用资源的原共享凭据。地图 10 的四份运行数据、两张转换图及教学 catalog 共七条运行路径全部有数据 / 资源哈希和显式加载绑定；新原生 shader / UID 位于 Git 代码目录。`--map10 --check` 可只读检查重复登记结果。范围外 NPC 旅行 / 果树新增资产由各自会话维护，本次没有登记它们；独立素材包与锁由主协调会话随后按批准增量生成。

### 2026-10-15 NPC 旅行 UI 共享登记

固定 `854_156383` 的 7 个原旅行 prefab、44 份组件绑定与 3,489 条中文使用独立 `npc-travel-ui/source-manifest.json` 记录；运行目录为 `assets/official/npc-travel-ui/`，包含 `panels.json` 与 72 张原贴图。共享接入按来源 SHA 验证全部 73 条运行路径并合并中央来源及显式清单，保留既有预期哈希。此项证明转换、登记和加载；完整旅行功能及下一批匹配素材包尚待各自验收，未据此声明完成。

第二批补入原 `MarkableConfirmDialog` 及其 5 张依赖图像，现为 8 个 prefab、59 份组件绑定、3,493 条中文和 78 条运行路径。前批 72 张 PNG 的来源、实际文件和中央绑定 SHA 均未改变；`panels.json` 按新增原确认框重新转换并登记新 SHA，旧 73 条路径哈希、原来源指纹和 72 张图像完整凭据保存在 `npc-travel-ui/registration-history/registered-v1.json` 及相邻历史文件。首批未另存完整来源文件原字节，该限制如实记录；第二批保存完整来源和增量快照。

旅行功能冻结版补入原 `ui_common_confirm_dialog_no_tip` 一词，现为 3,494 条中文，最终 `panels.json` SHA 为 `7ce474936a2b863bf3ed816f9c7f33b8efcc68f613762f0a7ee13270ebf602c9`。77 张 PNG 逐项对照前版来源、实际文件及中央绑定，全部同字节；本次没有新增 PNG。完整冻结版来源和前版 JSON 哈希保存在 `npc-travel-ui/registration-history/source-v3.json`、`registered-v3.json`。

Unity 离线旅行 UI 载荷由 `scripts/prepare_unity_npc_travel_ui.py` 从固定版 `assets/official/ui.json`、旅行 prefab 树及 NPC 配置头像生成，写入 `unity/Assets/StreamingAssets/Home/Travel/`；当前含50个面板树及243张运行贴图，伙伴头像按 `FigureImage` 优先、同一 NPC 的 `Icon` 回退，其中87个使用 FigureImage、2个使用 Icon，另有8个两种图像均缺失。派生输入／输出与稳定 `.meta` GUID 哈希记录在运行目录的 `source-manifest.json`，由 `HomeFeatureRuntime` 显式加载。NPC 11015–11022 的 `FigureImage` 和 `Icon` 在固定版、当前热更新及已核对新版同名资源包中均无可用图像；各自配置模型路由与缺项见该回执的 `missing_npc_avatars`。

旅行状态演出依据固定 `854_156383`：`inspect_npc_travel_ui_followup.py` 提取原出发／领奖／伙伴详情／条件提示 prefab、动画剪辑和事件；`prepare_npc_travel_followup.py` 与 `prepare_npc_travel_brief_extension.py` 转换四条状态时间线和原羁绊规则窗。`NpcAdventureModelItem.cs`、`NpcAdventureTravelStartStatusDialog.cs`、`NpcAdventureTravelEndStatusDialog.cs`、`NpcAdventureTravelEndStatusAnimController.cs`、`INpcAdventureTravelStatusShow.cs` 与 `AppearanceSystem.Runtime/AppearanceAnimUtils.cs` 的调用及 SHA 随模块清单登记。`prepare_npc_travel_model_extension.py` 按原 `NpcAdventureSettings`／伙伴配置，为76位可派遣伙伴转换 `FigureModelUI` 与 `FigureModelWithWeaponUI` 共152个原 prefab；NPC 11011 的两个原模型缺少 `100/idle_01` 与 `100/run_03`；固定客户端 `AppearanceAnimUtils.PlayAnimation` 在查找失败时不改当前轨道，运行时沿用该回退，四条具体绑定列在模块清单。装配、来源哈希、运行资源和显式加载由 `assemble_npc_travel_extension.py`、`publish_npc_travel_assets.py` 校验并登记；正式资源为 `assets/official/npc-travel-ui/travel-models.json`，完整原始对象与输出凭据位于 `npc-travel-ui/source-manifest.json`。出发剪辑的 `CanvasGroup.m_Alpha` 路径哈希 `588897702` 在固定弹窗和官方动态插入的152个模型层级中均无对应目标，来源清单保留 `target_absent`；运行时不将该无目标曲线套用到其他对象。

### 2026-10-15 季节原 UI 增量共享登记

独立 `season-lifecycle/source-manifest.json` 记录固定 `854_156383` 的配置、客户端代码、原组件绑定、语言和图像来源；运行数据为 `season-lifecycle.json`、`season-lifecycle-ui.json`，现含 11 个 prefab（9 个既有原布局及 2 个原规则等级行）。共享接入逐路径校验通知清单的 69 条输出 SHA，并通过固定 APK / 已校验原 CDN 解析 67 张 PNG 对应的原 bundle、MD5、Sprite 对象名及 path_id。3 张既有 PNG 复用原中央凭据，64 张原玩法图标首次新增中央来源和显式绑定，另登记两个独立 JSON。

这 64 张 PNG 及其 `.import` 当时未包含在 `d7dd2730b6af88e0` 素材包；此前一次通知误称已入包，已更正。实际新增集合和修正记录保存在 `season-lifecycle/registration-history/registered-v1.json`，应按新增 64 张 PNG、64 份 `.import`、两个 JSON 与实际来源历史统计，不能称仅新增两个 JSON。来源快照和输出哈希保留于该历史目录；这些文件已纳入 2026-10-16 的当前素材包。

### 2026-10-15 果树原两级设施独立转换

`scripts/prepare_fruit_tree_assets.py` 已实际执行，固定 `854_156383` 的原 `FruitTree.prefab`、两个子家具、`HomeNotice`、果实效果模板和四个原维修/图标 prefab 均按原 bundle/CAB 解析。独立运行数据 `fruit-tree.json` 包含原状态切换路径和可见标志、碰撞范围、6 层 Sprite、三段 `loop_1/2/3` 的 12 fps 原生网格及循环时长、23 个场景粒子发射器、十个果实槽位和原果实效果发射器；维修效果时序为原 0.6 / 1.5 秒。原成功通知的 `mPropItemPrefab` 另精确提取为 `FurnitureProductionProp`，原语言、对象、输入和输出 SHA 保存在 `fruit-tree/source-manifest.json`。

本模块来源共引用 50 张官方 PNG：最初登记 47 项时新增 14 张图像，属性行使引用数增加 3 项，但这 3 项与已有中央登记文件同字节，实际不新增 PNG。此前一次通知将“新增引用”误称“新增图像”，最终差集和修正说明保留于 `fruit-tree/registration-history/registered-v2.json`。该版转换 JSON SHA 为 `ab58e8e997ae5eb0ae68fec1005fc5f8f64cca0164d4b1d195f316dc64cbd5fd`，初版 JSON / 来源指纹和本版完整来源快照保留于该历史目录。

这些是稳定的已转换素材，原场景状态、两级原 UI、维修、生产和两段教程仍在运行接入与原生鼠标验收中，不能据此声明完整果树功能完成。原 RPC 23624 的最终奖算法仍未找到，实际 `fruit_harvest` 原子失败并保留果实、计时和存档；未补造收获礼包。下一素材包仅按实际文件差集纳入该转换结果，功能完成情况单独验收。

### 2026-10-15 季节原 GotTip 与语言补充共享登记

固定 `854_156383` 的 `ItemIconAttachmentConfig.asset` 中，Type 2 唯一指向原 GUID `9717aac89f12f8c4bb521b6b3f66a83a` 及 `Assets/Prefabs/UI/Item/Attachments/GotObj.prefab`；配置及 prefab 均来自原 APK。新 `season_reward_got_tip` 保留原三节点、inactive 根、RectTransform、颜色和 `ItemIconAttachmentCell.mLayer=7`，没有绘制替代图。原 `ImgQuality_Mask-49ff34b468.png` 与 `ImgTick-20d85e1c96.png` 已有中央来源、显式绑定和导入，本次只新增季节模块的两条同字节引用；原 67 张 PNG 不变，实际新增 PNG 为 0，共 69 张 PNG 和两个 JSON、71 条既有显式路径。

原语言 rawfile 哈希未变。此前已存在的 56 项完整保留，补入 211 项原中文，独立目录现为 267 项；此前检索的其余 3 项已存在，6 项未用值／示例键的缺失说明继续保留，未补造语言。本批只更新 `season-lifecycle.json` 与 `season-lifecycle-ui.json` 的中央数据哈希，并登记独立来源 `season-lifecycle/source-manifest.json`；复用 PNG 的中央凭据及生成绑定原字节保留。完整 GUID／APK 路径、原 bundle／对象及输出 SHA 见独立来源，旧来源原字节继续位于 `history/source-manifest-e0e5c90bdc54db8abc34b9c5f8bfb1eab6bc7d18da54a4c2d320e1c057fca750.json`。

这次登记记录静态路径、实际文件哈希和原语言；完整玩法状态见功能文档。

## 2026-10-15 石板谜题 41001 原生界面接入

地图 41001 的目录四态、原关名、快捷购买、结算、退出确认与下一关入口复用原版 `ActivityPuzzlePanel`、`ActivityPuzzlePanelItem`、`PuzzleStagePanel`、`GameEndPanelPuzzle` 及 `CostItemConfirmDialog` 的已转换官方资源。素材版本为 `854_156383`。

本次素材增量为 `assets/official/map-localization.json` 及 `assets/reference/official-map/localization-source-manifest.json`。从原 APK 内两份中文语言 rawfile 按 XXHash64 键查询增加八条官方原文，累计 4687 条；输入 SHA、APK 路径、旧输出 SHA 与本次输出 SHA 均保存在来源清单的 `increments` 中。协调方与已提交素材包逐项对比，原 4679 条内容一致。原音乐 100 的事件 `Activity_05_Map_Play` 复用 APK 内 UI_BGM_01 音频，WEM 媒体 87243485 的提取与 OGG 输出哈希见同一来源文档。

### 2026-10-15 果树原文字、教学遮罩与模型边界转换增量

本批已实际执行固定 APK `854_156383` 的转换，前一版 `ab58e8e9…`、元数据版 `c15d78b8…` 和遮罩中间版 `1777289a…` 的原 JSON／来源文件保存在 `fruit-tree/history/`。转换时曾沿 `ProcessGuideHint` 的 MonoBehaviour 指针误用完整组件读取；修正为仅通过原 MonoBehaviour 头定位其 GameObject，原遮罩字节布局仍按完整长度严格校验，未放宽或绕过原来源哈希。

最终 `fruit-tree.json` SHA256 为 `60a63410ff717a22486306170620ecaf0d54a5d6cd7fad623d296702dd5681bc`，来源 SHA256 为 `7e465e21973c79180e08d5154d8a159258fe49241b1b3b3b21aa0a5cce176bef`。保留原状态、网格及粒子，补入 22 条 TMP 换行、溢出、边距／自动字号记录、118 个含实际家园槽位平移的对象矩阵及 4 个原 BoxCollider 的世界边界。原成功通知 `CloseObj/Text` 为 overflow 0，接入现有公共 UI 的不裁切显示；本批未改公共 UI 文件。

原 `RepairFurnitureNode` 在对白后先等待再次点击真实设施，再打开维修窗口并突出维修按钮。现已接入这个控制顺序，教学遮罩区域来自原模型位置、活跃子 Collider 合并边界及节点 Offset／Scale。原 `StoryMask.MaskAssetProxy` GUID `8578cbcb823445046ba91b8e152fe61b` 对应 `Assets/Prefabs/UI/Story/Mask.prefab`；原 `StoryMaskGraphic` 的圆角半径为 23、每角 10 段，颜色 alpha 为 199/255，射线洞口为矩形。

当前模块引用 57 张官方 PNG，比前一登记增加 7 条模块引用；57 张均与中央登记及先前独立恢复的配套包同字节，实际新增 PNG 为 0，原有 50 张无修改。58 条运行路径已在原显式加载清单内，本批只更新中央 JSON／来源指纹，保留共享图片原凭据及绑定字节。原两份语言二进制文件均经转换核对；全部 20 个文字键在来源中登记。

果树 prefab 实际没有独立 `HomeDayNight` 组件，`day_night` 为空。夜光来自 `HomePlayableClipBind` 的原 24 秒曲线：接入原家园 30 倍时间及 FunctionType 90 的门槛／锁定 12 点状态，并按实际路径应用两条显隐和三条 Sprite 颜色曲线。三条原 Light2D 强度曲线及原粒子／渲染绑定的后续接入见“家园灯光与材质迁移”；没有按名称添加昼夜开关。

原通知通过 `FurnitureRepairedNotifyDialog → HangUpUtils → BattleFormulaHandler → PlayerPropItem → PlayerComplexProp → LanguageUtils` 先把每小时收益转 long，再格式化。原 hang_up 100 使用 HourlyOutput 0.25、比例属性 1030、整数属性 1029，1029 的 Float／Percent 均为 false，因此无附加属性时显示 0 是原版行为；本批按原配置与 fixed 的 single 转换取值，不补造小数显示。

原 RPC23624 最终领奖仍无可验证算法，继续保持原子失败；转换与来源登记不表示完整果树玩法完成。

## 地图41005传送22081官方资源（2026-10-15）

新增原Action22081的Start/End播放定义、31个粒子发射器、原分体map/portal_01动画、6张纹理、1份粒子网格及1段0.6秒原音效；27份既有官方资产沿用已锁定路径。原Map_Action_Flash_1/2保留各自事件、action、sound身份，解析到同一media281889249。3001缺CommonEffectSlot、6543/6549该项为空，按原AttachPosRotCfg.GetBone使用对象根。未自绘。

主体来自固定APK及匹配854_156383的校验CDN。两个原缺失CAB的材质依赖明确使用864_156496独立同名包，大小、MD5、SHA256、实际URL、内部对象与兼容关系见 assets/reference/map-action-transports/22081/conversion-manifest.json；原185缺项与预期哈希保留。原始提取、转换、动画、跨包Wwise音效和锚点证据独立保存在该目录。

本记录对应素材已提取和打包；可复用转换脚本按代码目录管理，不置入忽略素材目录。

既有41001语言输出与固定APK两份原语言表的全部4687个键值再次核对一致；其模块来源清单已记录实际输出，但锁定ff包的中央data_sha遗留旧摘要。此次按原来源凭据补齐中央登记，历史差异、原输入哈希及逐项核验见 official-map/registration-history/41001-localization-central-2026-10-15.json。语言运行文件字节、原APK/CDN预期哈希均未修改。

可复用转换入口为 scripts/prepare_map_transport_22081.py 及同前缀的 _visuals、_animation、_audio、_anchors 四模块，已移入代码目录；冻结原脚本和原来源历史保留。独立重建10项输出中9项逐字节一致，Ogg仅容器随机序号/CRC不同，其原WEM、编码音频载荷和PCM一致；本包保留已冻结Ogg原输出哈希。

这是同一磁盘的本地备份，不代表异地备份。

## 初次位阶突破教学与任务目标文字

2026-10-16：复用固定 APK manifest `854_156383` 已转换的 `PlayerUpgrade` 剧情图、两组五句对白、原角色／突破面板、`TalkDialog`、固定奖励 24 和本地教学语音。不会把位阶教学视为职业转职。现有 [剧情来源](official-map/story-source-manifest.json)、[地图 UI 来源](official-map/ui-source-manifest.json)、[音频来源](official-map/audio-source-manifest.json) 与角色面板来源清单保留其真实输入与对象，未新增图像或语音文件。

`python scripts/prepare_map_localization.py --task-growth` 按已登记且校验 SHA256 的原版中文 rawfile 增量提取 `condition_SubRank_desc`、`jump_to_home_tip0` 和 `Common.SubRank` 枚举名称，追加到 `assets/official/map-localization.json`；输入 URL／版本／哈希与新增键、输出哈希见 [official-map/localization-source-manifest.json](official-map/localization-source-manifest.json)，枚举源文件 SHA256 另行登记。位阶目标沿用原译名；累计祈愿目标的中文句式及实时计数由本地控件生成，不冒充原本地化字符串。


## 通用战斗角色与怪物实体配置

2026-10-16：`prepare_fidelity_battle.py` 补入固定 `854_156383` 的 `FightCharacter_part1.g.bytes` 和 `FightMonster_part1` 至 `part21`，与既有 FightSkill／FightStatus／FightPet／FightOther 使用相同原生成格式读取器。运行 `battle-rules.json` 包含原角色体积、旋转中心、移动配置、角色类型和初始状态；完整输入对象、版本、APK／校验 CDN 来源及输入输出 SHA256 见 `battle-rules/source-manifest.json`。此前转换范围与原输出哈希保存在 `battle-rules/history/source-before-character-monster.json`，不将既有未转换实体记为官方缺失。没有新增自绘素材。


## 通用战斗动作、特效与助战配置

2026-10-16：`prepare_fidelity_battle.py` 增量转换固定 manifest `854_156383` 的原 stage_npc、npc_level_detail、npc_profession_detail、npc_template、FightNpc 及幻兽额外属性。battle-rules/source-manifest.json 记录输入对象、真实APK/CDN来源及输入输出哈希，前一来源收据保存为 battle-rules/history/source-before-npc.json。

`python scripts/prepare_battle_presentation.py` 从固定原男角色 SplitBinarySO 提取216段100／200战斗及配套动作，复用当前官方可定制皮肤、贴图和原始染色遮罩，采样为本地原生网格帧；4,672段怪物／NPC／召唤体动作复用已登记原模型骨骼、皮肤和贴图。原技能预制体 AnimationClip 覆盖名称用于绑定动作。当前初始剑／杖仍使用既有官方位图及网格，按原pWeapon_R2骨骼相对已有地图站姿变换接入；这是本地挂点适配，不是新绘素材或新的官方原武器网格。逐对象来源及输出SHA256见 official-map/battle-presentation-source-manifest.json，既有模型／角色来源收据哈希保留。

`python scripts/prepare_battle_effects.py` 按原 MyPlayableSequence 时间、FollowBone／Track 挂点和 ParticleSystem／材质转换技能及伤害粒子资源。3,543组绑定、4,226组粒子配置复用原资源和通用粒子转换器；原版本缺包依赖通过独立校验的新版同CAB资源解析，真实864_156496凭据与原版本来源分开保存在 official-map/battle-effects-source-manifest.json 的 counterparts 字段；未填入或改写原版本缓存。声音保留原Wwise事件引用，尚未将其等同于已转换音频。上述来源收据记录实际输出和缺失依赖，运行只使用本地转换后的显式资源。


## 森之国地图11官方材质、机关与六碑文字

`prepare_unity_map11_base_ground.py` 补取固定版 `defaultpackage_assets_partition_mapground_11_mapground_11_bare.bundle` 的原 `Ground` Tilemap：17,752 格、55 张原 Sprite、55 批多边形，保留原 Tile 矩阵、锚点、颜色及 Unity 坐标；`Resources/Map11/BaseGround/geometry.bytes` SHA256 为 `f7e948cec6dce80d3fbe252715bfcdf8ed2b7b984621cb37d6f7e105889d0cba`。同一转换器读取原世界角色 `3001.prefab` 的模型偏移、缩放和渲染槽位，以及原 `WorldCamera.prefab` 的视野适配、裁剪面。原对象、bundle 哈希、Sprite 及几何输出哈希、`WorldCameraBaseController`／`OrthographicCameraResolutionAdaptor` 源码哈希见 `unity/Assets/Official/World/Map/11/base-ground-source-manifest.json`。当前地面沿用既有材质适配，原 URP 光照、水面与遮挡排序未完整恢复；不能据此声称场景完整还原。

Map11 女巫原界面沿固定版本 `854_156383` 的 `defaultpackage_assets_prefabs_ui_worldmap.bundle` 提取 `WorldWitchTouchDialog`（PathID `-3082449800091456431`）、`WorldStageInteractionPreviewDialogWitch`（PathID `8230908895915710719`）和 `WorldTouchDialogWitchPeriodicityInteraction`（PathID `640011520031964919`）。该 bundle 原始 SHA256 `b89571df3f7eca7a6d9bf9f7d6f03af49b38191d4f3324fbd40559fe70f9916a`，大小 1,186,915 字节。逐对象来源、原始引用、可选动画目标核对及输出哈希统一保存在 `unity/Assets/Official/World/Map/11/UI/source-manifest.json`；当前行为与验证边界见地图所属页。

Unity 森之国载荷固定使用 `854_156383`。`prepare_unity_map11_runtime_data.py` 将基础实体模板与地图 `Lo` 覆盖分别保留，生成 110 类实体组件、44 个故事图（666 节点）与四个子图（64 节点）、50 条地图对话、61 条选项、8 条固定奖励、24 条主线及 129 个战斗阶段；输入、官方客户端／共享程序集来源与输出哈希见 `official-map/map11-runtime-source-manifest.json`。`prepare_unity_map10_battle_data.py --map-id 11` 保留 57 个怪物编队数组、17 个场景和 591 个实体阶段绑定，来源见 `unity/Assets/Official/World/Map/11/battle-source-manifest.json`。

森之国 `5110` 动态怪物显示：`prepare_unity_map11_world_monsters.py` 沿固定版 `ECStageBehStageView.GetMonsterId`、`Formula.GetMainMonster`、`MapMonsterLoader` 和 `MapMonsterLoadHandler` 核验六个阶段绑定，以及 3212、3215、3216、3217、3218 的官方 `MonsterModel` prefab。原 `5110.prefab` 的 `ModelSlot`、`Shadow` 挂点及偏移均记录在来源清单；`MonsterShadow` prefab 跟随 `shadow` 骨骼，其 Sprite 从 APK 原 bundle 提取为 `unity/Assets/Official/World/Map/11/Resources/Map11/Textures/MonsterShadow.png`。官方 CDN／APK 来源 URL、bundle 哈希、CAB 名、path ID、对象与纹理哈希、缩放和复用的战斗骨骼映射见 `official-map/map11-world-monsters-source-manifest.json`；运行时显式从 `Resources/Map11/Textures/MonsterShadow` 加载。

机关规则从原 EC／Lo 配置恢复 116 个相关记录、九组锁与七座终端。锁 413 对缺失实体 423 的原结果保留，独立记录用户确认的单机跳过例外；已重新反编译原程序集并核对原始地图数据，未创建替代实体或修改官方源配置。

终端物品 325 的 `quick_buy` 行从同版 APK 的 `config_luban` 提取，保留 50 晨星、每次一张、无购买次数上限及物品 4013 持有条件。剧情分支清单记录 `SelectorNode`、`SwitchNode`、`StoryGraph`、布尔条件及 `SerializableObj` 原代码哈希；按用户确认补全服务器缺失的首项匹配规则，未改动故事图的端口、顺序或默认出口。

剧情来源清单同时记录 `GetEntityBaseNode`／`GetEntityPosNode` 的枚举和偏移代码，以及 `GetMapWitchExploreInfoNode`／`WorldWitchTouchUI` 的查询与跨地图计数调用。单机沿用户确认使用所有地图魔女激活账本，按地图／放置实体去重，计数与主角定位均已用实际故事图检查；魔女界面和完整主线仍见地图页缺项。

`prepare_unity_map11_audio.py` 先核对 APK 的 WwiseBnk 清单及实际包内文件，再读取同版云端包，验证 `map_bgm_01` 的包与原 Wwise 媒体哈希。原 `750073388` Vorbis 转换载荷复制为原生 AudioClip；四个事件的动作、无限循环、Sine 曲线和 1,000／250／250／500 毫秒参数从原 bank 解出。代码、输入、处理方式及输出哈希见 `official-map/map11-audio-source-manifest.json`，显式加载绑定为 `Resources/Map11/Audio`；试听、战斗音乐和语音不属于已完成验证。

`prepare_unity_map11_dialogue_audio.py` 同样先查 APK 实际内容，再核对固定版云端语音包，验证 207 个剧情事件的 Play 动作、原媒体切片和已转换 Vorbis 哈希；`Voice_StopAll` 的两个立即停止动作保留原调用顺序。原语音以 Windows 原生流式 AudioClip 绑定 `Resources/Map11/Audio/voices`，来源、代码及输入输出哈希见 `official-map/map11-dialogue-audio-source-manifest.json`。带图形的后台 Play Mode 检查了全部导入、实际主图／子图换句与关闭、过期加载取消及语音音量，音源静音，未进行试听或真实输入检查。

`prepare_unity_map11_story_spatial.py` 从 APK 实际 DefaultPackage 清单定位 EC 索引及 113 类 Map11／本地玩家 prefab，严格读取原 `MapEntityTag`、`StoryNodeRegister`、玩家活动范围及距离设置，保留 bundle、对象和组件来源哈希；结果绑定 `Resources/Map11/story-spatial.json`，清单为 `official-map/map11-story-spatial-source-manifest.json`。Play Mode 核对了 6101 世界树注册和主角回退位置。原 `StoryUtils` 会根据动态 AttachAgent 骨骼可见性筛掉地图 NPC，这部分自制地图视图尚未复原，不能将空间查询记作完整 NPC 可见性迁移。

Map11 实体转换保留 105 类模型中的 24 类状态模型、64 份状态几何和空状态原始字节。补充纹理 `building_naka1`、`building_luzi` 从原 `6330_11_view` 的真实 SpriteRenderer 引用定位至 `defaultpackage_assets_image_mapentity_entity11_building_grave.bundle`（SHA256 `6b24112c0669a6a45628ee51d92a568a8eecedea97ab91eb12ba877a2ec263ae`）；重新提取 PNG 与既有文件逐字节一致。161 份几何、27 个 Spine 实例、203 张纹理及额外对象来源保存在 `unity/Assets/Official/World/Map/11/entity-source-manifest.json`，未修改原 APK／CDN 预期哈希。

`prepare_unity_map11_ui.py`、`map11_support_ui.py` 与 `map11_minimap_ui.py` 从 APK 实际文件及固定版本补充资源恢复 37 份原生 panel、325 张 Sprite、37 份原生小地图标记和 1,707 条实体标记绑定。包含 WorldCanvas、TalkDialog／OptionTalkDialogPanel、圆形／全屏小地图及筛选、FunctionEntryDialog、原 FirstPayBtn、任务项、物品及女神／魔女窗口。跨图标签提取同组 Map10／Map11 的原 `MapInfo`、`MiniMapSettings` 和 `ImgMapSwitch_0`／`_1`，原 parser 顺序、图标及逐图缩放配置均进入同一载荷；运行时只显示已访问地图。原 `MiniMapWorldStageSwitcherSupport` 的对象状态引用及源码哈希一并导出，由本地实体阶段驱动标记图标互斥切换。功能入口／快捷消息的默认 Animator 状态、默认片段与 Controller 来源同样保留在该载荷中。原 MonoBehaviour 引用、严格读取的 UI／Framework 字段、CanvasScaler／遮罩启用状态、中文、GlobalColors、PopupWindow／Animation 曲线和半程事件、UGUIJszx 横纵布局及自定义网格、MirrorImage、战力推荐和数字格式均记录输入代码、对象及资源哈希；输出 `map11-ui.json` SHA256 为 `f30126a7a7a619aaec8f5a12da04c083e0d71bcb47c5c24e0b1b5fc01b3be5ae`。`map11_tmp_sprites.py` 提取 Common 七个原 TMP 字形、字符及 400×200 图集，恢复 Texture2D／材质及原序列化哈希。显式加载为 `Resources/Map11/UI`。后台图形 Play Mode 已核对 HUD、单／双人对话、选项、小地图筛选冷读、跨图页签和几何切换及功能菜单；真实输入、跨图离线标记及点击、完整场景渲染和 Windows Player 仍未完成验证。失落女神经验／罗拉各 +1%、最近可站立格传送及兑换关闭后的剧情返回属于所属页记录的单机差异，未改动官方输入或预期哈希。

2026-10-16：地图11继续使用固定 manifest `854_156383` 的 APK／匹配 CDN。`prepare_map11_rendering.py` 直接读取 `MapGround_11_bare.prefab` 的活跃 Renderer、原材质和原输入对象，不再依赖临时检查输出。转换1188个地形Sprite、67510个Tile、192个Mesh与1803个放置实体；Ground1按原高度图保留水面上下两遍，Water2的原Mask按64采样／世界单位转换为本地纹理。`map11-rendering.json`、两份11区域文件、Mask及复用纹理的对象／输入输出哈希见 `official-map/materials-11-source-manifest.json`，历史区域／水面来源保留。

`prepare_map11_lighting.py` 从固定 APK／校验 CDN 转换 MapGround_11 的原全局 Light2D、85 个地图可用显示 prefab 中的89盏局部灯和 FalloffLookupTexture 衰减；43个活跃 Water2 渲染器均核对为排序层16。`assets/official/map11-lighting.json`、衰减贴图及所有输入／输出哈希见 `official-map/lighting-11-source-manifest.json`；其余原版本缺包记录保持不变。

6530原两种idle为空，仅0→1的原一秒Start播放8个原粒子发射器；声音由原 `Map_Entity_MiniGame_Bonus` Wwise事件解析到media163983865，其1.5秒尾音按原无StopSoundEvent规则播放。真实PCK来源、bank／event／action／sound／media身份、输入与OGG输出哈希见 `official-map/audio-11-state-source-manifest.json`；保留冻结OGG，不因重新编码的容器序号不同替换现有音频哈希。

`prepare_map11_stone_states.py` 从原6503 prefab、Animator、Spine及附着轨道重建close／middle／open与各自loop；按原中断规则和normalized entry mix采样64组12fps本地网格动画。输出及原对象来源见 `official-map/stone-11-source-manifest.json`。`prepare_map11_logic_views.py` 核对五包清单、原实体显示表与模板，确认6533的413／3210／6105是原定义的逻辑节点，没有对应显示Prefab；`official-map/logic-11-source-manifest.json` 记录检索范围与原对象，不为这些节点绘制假素材。

`prepare_map11_dialogs.py` 从原 `stage_battle_stele` 表选择900／901／902／925／926／927，并按XXHash64读取原两份中文语言二进制的17个名称／说明／界面键，输出 `assets/official/map11-dialogs.json`。原关卡表、语言来源及哈希见 `official-map/dialogs-11-source-manifest.json`；界面复用原BattleSteleWorldStageDialog和已转换官方敌人模型。

`register_map_assets.py --map11` 仅登记上述独立来源收据验证的输出，保留其余模块绑定与历史来源；不以当前文件哈希覆盖未核对的预期值。既有位阶教学中文增量另由 `--localization` 逐键核对两份原语言输入后登记，中央来源保留旧摘要和原增量链。资源进入显式本地加载清单及匹配素材包，旧APK／CDN预期哈希和原版本185个缺项保留。

同次登记发现既有 `season-lifecycle.json`／`season-lifecycle-ui.json` 的中央摘要停留在较早转换。`--season-lifecycle` 重新核对固定 APK 中的原表、完整构造器、中文二进制、客户端代码及序列化输入，再登记独立 `season-lifecycle/source-manifest.json` 已记录的现有输出；原摘要由该模块 `history/` 中真实旧来源证明并保留在中央 `registration_sources.previous_data`。没有重新绘制、替换现有素材或改写原包哈希。


## 原战场环境、网格与阵型

2026-10-16：`prepare_battle_stages.py` 从固定 manifest `854_156383` 的 FightStage EC 与原 `stage_monster_array`／`ai_priority` 表转换256个战场、474个阵型及40组AI优先级；原始表字节与APK内TextAsset逐字节核对。输入对象、APK／校验CDN来源、构造器及输出哈希见 [战场配置来源](official-map/battle-stages-source-manifest.json)。

`prepare_battle_scenes.py` 按 `FightComponent.SceneName` 解析229个原预制体，跟随 `FightEnvironmentLoader` 的昼夜GUID和父变换，转换基础层、Sprite／Tilemap／Mesh、Spine及ParticleSystem到本地二维资源。677份场景层包含1354个JSON／几何文件，统一目录为 `assets/official/battle-scenes/`；入口为 `battle-scenes.json`。原BG02副本的atlas键不匹配时，唯一同名且Rect／Pivot／Border／PPU完全相同的原atlas Sprite用于解析，原副本、atlas与实际对象分别登记，没有另绘位图。完整对象、处理及输入输出SHA256见 [战场环境来源](official-map/battle-scenes-source-manifest.json)。

`scripts/prepare_unity_battle_scenes.py` 校验上述固定版目录和256个阶段绑定，恢复229个唯一基础场景、非空昼夜网格及6,376张贴图，共6,656项资源；Unity 输出及每项SHA256记录在 `unity/Assets/Official/Combat/Scenes/scene-assets-source-manifest.json`。运行目录 `BattleScenes/catalog.json` 将GameId绑定到 `Resources.Load` 路径；[`OfficialBattleEnvironmentView`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleEnvironmentView.cs) 以官方网格顶点、UV、颜色、排序层和贴图建立场景，并按调用方覆盖值或原prefab默认昼夜加载环境几何。昼夜运行时规则见固定版 `FightTimeTypeSwitcher`：消费 `StageInfo.TimeType`，`MapActivedAuto` 再检查地图区域激活；当前Map10入口尚未传入运行时昼夜值。Unity端目前用 `Sprites/Default` 建立材质；原材质／shader、环境粒子与Spine，以及真实Editor／Play Mode画面仍未验证。

19个原版本缺失CAB依赖按既有独立校验机制读取新版官方同CAB包，真实 `864_156496` 版本、URL、MD5／SHA256和所用对象登记在上述收据的 `counterparts` 字段；其余场景主体保持854版本。没有修改原APK／CDN缓存或原版本185个缺项。场景入口保留原预制体根Transform，逻辑格经地面原点和根矩阵映射到原世界坐标；已转换几何保持同一世界坐标。转换资源加入显式加载清单及Windows导出过滤，运行时无需原Unity包、Python或网络。


## 山之国地图12官方材质与七碑文字

2026-10-17：`prepare_map12_rendering.py` 复用固定 manifest `854_156383` 的 APK／匹配 CDN，重新读取 `MapGround_12_bare.prefab` 的原 TilemapRenderer 材质与输入对象。522个Sprite层、110176个Tile、448个Mesh、2591个放置实体保持原地形；水面57个启用Water2保留原四类纹理、参数及0.55水位，另一个原禁用Sea不绘制。WaterMask按原最大值混合和64采样／世界单位转换为本地遮罩，原Submersion上下两遍保留高度图和atlas映射。来源及输入输出哈希见 `official-map/materials-12-source-manifest.json`，旧区域与水面来源保留。

三处原 `Sprites/Lava` Tilemap材质从官方编译Shader的GLES程序解码，使用原完整Sprite纹理、噪声、世界坐标、0／90／180度流向及原速度、扰动、亮度起伏参数，转换为可供 Unity 验证的格式。原Shader对象、原始字节哈希和解码程序随上述来源收据保存。采用原unlit分支，未移植Unity URP形状光叠加；格式适配不声明与原版逐像素相同。没有自绘图像。共同6530平台仍复用已核对的原空idle和一秒Start；本地图收据独立记录原配置与8个发射器，声音复用既有官方Wwise转换。

七座战碑按放置的原StageId选择905／906／907／908／930／931／932，`prepare_map11_dialogs.py` 的地图参数选择原关卡表与19个原中文键，输出 `assets/official/map12-dialogs.json`；表与语言来源哈希见 `official-map/dialogs-12-source-manifest.json`。界面复用原BattleSteleWorldStageDialog及官方模型。`register_map_assets.py --map12` 仅登记这两份原来源收据的实际输出，`verify_official_assets.py --map12` 核对原输入对象、输出和显式绑定；原APK／CDN预期哈希与185个原版缺项保持原记录。


## 森林奖励箱子共用原图标

2026-10-17：不同阶数的历战精华匣／箱子按原Item.RawIcon共用图片，运行时对缺少独立ID绑定的9200／9217等解析到完整同名路径。白箱`ItemIcon/Other/itembox_white_5`与蓝箱`ItemIcon/Other/itembox_blue_5`复用已转换的`assets/official/sprites/itembox_white_5-9cc50ec3de.png`、`itembox_blue_5-fd88304460.png`；没有新增或重新绘制图像，没有修改图标目录或图片字节。

两图来自固定854_156383的校验CDN包`defaultpackage_assets_image_itemicon_other_homeresource_noatlas.bundle`，MD5 `88c8ec63019259f1e41216df57277da1`；原Sprite对象分别为1938551758088215332与-661215029578351316。原URL、bundle SHA256、Sprite解码／atlas裁切及输出SHA256均沿用[原城市素材来源](official-city/source-manifest.json)的resources条目；显式加载绑定和既有素材包已包含两图。此项仅补运行时物品与已有原图片的对应关系，原来源和历史记录保留。

山之国机神山原教学弹窗另由 `scripts/prepare_map12_dialogs.py` 读取854_156383的DailyDungeonSettings（输入对象SHA256 fd285604245dbaf7f6740bf2f4d186ec55d1e4edea18214879cb55c1aa10061e），按原公共显示ID21／私人关卡ID121绑定原GUID。背景DetailBg_Map_12_1与建筑DetailBuilding_Map_12_1复用既有dungeon-ui来源收据及已转换PNG；原中文标题和描述从匹配语言分包解码，加入map12-dialogs.json。原GUID、对应资源、输入／输出哈希在dialogs-12-source-manifest.json登记；不是新绘图或其他地图占位图。

机神山奖励701熔铸碎片的原Sprite位于 `defaultpackage_assets_image_itemicon_box_equip_map12_noatlas.bundle`（854_156383，MD5 9a2b7c5c790e7766400b2a6f17782542），原路径 `Assets/Image/ItemIcon/Box/Equip_Map12_NoAtlas/item_701.png`。同脚本读取已校验CDN缓存并按原atlas裁切／旋转解码，输出 `assets/official/sprites/item_701-8d9d6962bf.png`，SHA256 3542a8aeb14a68f08e8aa684a1abc1a77c20027b4027dc81a9d4d3e22c852b4a；在icons.json增加701实际绑定。上述独立来源收据保留实际URL、包与对象输入哈希、PNG输出哈希；未用空图或自绘替代。

## 伙伴详情背景、立绘与加成说明

2026-10-17：`scripts/prepare_partner_detail.py` 从固定 APK／已校验原版本 CDN（854_156383）读取原 `NpcDetailPanel`、`NpcDetailPanelDetailContent`、`NpcDetailLiveItem` 及 23 个专用 Live prefab，覆盖当前本地支持的 76 位伙伴（52 位动态立绘、24 位静态立绘）。背景按 `Npc.BigBgImage` 选择原 `NpcBgFavor` Sprite；大立绘按 `FullBodyLiveImage`／`FullBodyImage`、`LiveOffset`／`LiveScale` 接入，右侧模型使用 `FigureModelUI` 与原 `SkeletonGraphicProxy` 指向的主图形。静态立绘遵循原 `ImageLocator` 的尺寸、pivot 和阴影透明度。没有自绘、替代背景或使用列表头像填充大立绘。

`assets/official/partner-detail/` 保存原 prefab 字段、中文文本、动态模型采样网格与配置目录；共享 PNG 位于 `assets/official/sprites/`。原 High texture-loader 的 GUID、颜色页／染色遮罩按原配置解析；原加法与乘法材质的混合参数已登记。原 `AppearanceAnimUtils` 在指定动画不存在时保留 prefab 当前动画，此分支也按原逻辑保留。

加成说明使用原 `NpcFriendshipDialog`／`NpcFriendshipPreviewPropItem`，当前已提供属性继续来自实际好感等级与已生效的珍宝加成；预览使用原等级变化及位阶可见条件。原对象／官方脚本输入哈希、包版本和来源、PNG 与 JSON 输出哈希记录在 [伙伴详情来源](partner-detail/source-manifest.json)，并登记到公共来源及显式加载清单。重建：`python scripts/prepare_partner_detail.py`；来源与输出核验：`python scripts/prepare_partner_detail.py --verify`。转换需持有 `partner-detail` 工作区锁；`--reuse-meshes` 仅用于保留既有已校验网格、刷新原 prefab 绑定。

### 装备详情的主、副属性与逐条附加词条（固定 854_156383）

`prepare_fidelity_equipment.py` 从固定版 `defaultpackage_assets_prefabs_ui_itemdetail.bundle`（MD5 `ed61998dccf8c8b2451eb16f49b433cb`）补充原 `Assets/Prefabs/UI/ItemDetail/EquipMutillPropItem.prefab`，GameObject path_id `-120004648565983737`。沿用 `EquipPropPanel.MainPropItem`、`SecPropItem`，以 `OtherPropGroup` 中逐条实例化的原模板显示附加属性，保留原状态切换、文字边距、加号设置、行距与交替底色；没有新增自绘。当前来源清单共 23 个 prefab／引用模板、231 组路径／脚本绑定、61 份 TMP 文字记录及 655 项资源记录。`EquipPropPanel`、`EquipMutillPropItem` 与装备候选／详情源码和完整输入输出哈希见 [equipment-fidelity/source-manifest.json](equipment-fidelity/source-manifest.json)。主、副属性和附加值分别依照原 `BattleFormulaHandler` 计算，不再将同类型附加值并入主词条显示。

## 2026-10-17 男女角色与原正式换装

本次在保留男性资源的同时接入固定854_156383的女性角色。实际输入、包MD5、对象path_id、原始对象SHA256、原配置／源码SHA256、复用来源及每项输出SHA256见[男女角色来源清单](character-genders/source-manifest.json)和[女性待机采样](character-genders/idle-animation.json)。女性使用character_Female_1骨骼、女性SplitBinarySO、Female_Map_Controller覆盖关系，以及对应原图集／染色遮罩；运行目录为assets/characters/female，。没有让女性借用男性动画顶点。

原包中有同名贴图的不同分辨率及共享副本，沿用既有角色转换器的最高宽度选择方式，所用PNG逐张匹配实际原Texture2D解码SHA256；未使用的同名版本单独登记texture_variants，不将其当作当前贴图来源。荧光棒复用已登记的原武器皮肤／贴图，挂点按女性pWeapon_R2采样。初始剑／杖仍是原有静态待机／地图展示锚点和战斗本地挂点适配，不声称已迁移完整武器骨骼。男女性可选部件保留原有六款发型、眉／眼／嘴各六款、三款妆容及无、五款特征及无的范围，沿用本地稳定ID和显示名；没有开放原版完整衣橱。

正式换装直接转换AppendAppearance/ChangeAppearancePanel的原层级、坐标、性别状态、双预览、职业立绘ImageLocator和费用栏，运行数据为assets/official/appearance-ui.json。来源、原字段、语言包、图片与输出哈希见[正式换装来源](character-genders/appearance-source-manifest.json)。逻辑依据ChangeAppearancePanel、PlayerCostMoneyContent、AppearanceSetContext、ImageLocator、MoneyUtils及PlayerServiceProxyImpl，费用读取GameSettings.ChangeAppearanceCostItemId／Count，即幻形魔镜119×1。图标实际来自原Common_NoAtlas/item_119.png子资源；不是根据RawIcon猜测独立GUID。支持切换男女、保存／放弃确认、未改动拒绝保存和不足提示；本地写盘事务使费用与形象一起提交或回滚。该段记录原正式换装迁移时的范围；现已由下述衣柜链路补齐 AppendAppearanceDict、装备皮肤及互斥解析，基础外观编辑仍见角色创建页。之前“仅男性／统一男性网格”的来源段落保留为历史状态，当前男女均保留。

费用图标沿原 `PlayerCostMoneyContent.ImageBtn` 打开已迁移的原 `ItemDetailDialog`，复用其图标、品质与数量展示；名称、道具类型及 `item_119_desc`／`item_119_func_desc` 均来自同版本语言表。原面板 `ui.json` 与 `icons.json` 输入哈希纳入换装来源清单。

## 2026-10-17 料理原效果组件与属性

运行数据 `assets/official/cooking-effects.json` 来自固定 APK manifest `854_156383`，转换工具为 `scripts/prepare_cooking_effects.py`。来源、bundle／对象、原始对象和代码 SHA256、语言及属性原始输入、输出 SHA256 见 [料理效果来源](cooking-effects/source-manifest.json)。从原 `CookingUseDetailDialog.mEffectContentUI.mEffectItemConfig` 的引用提取 Recovery／Buff／Prop 三种真实模板、各自属性行及适用范围行，同时提取 `CookingBuffReplaceConfirmDialog`、`CookingPropEffectFullDialog`、盈满行、原规则提示行、确认窗专用窄列属性行及 `PopTipEntry`／`EntryDetailItem`，共14个窗口／引用模板；保留 RectTransform、原布局、字段引用、颜色及状态配置。嵌入模板的字段路径仅按原父级重定位，不自行重画或重排。适用范围说明读取同版本 `entry` 表及语言，原条目均无图标；位置与最大高度按 `UIUtils.SetRectPopPosInScreen`、`LoopVerticalScrollWithMaxHeight`，标题编号按 `RuleTipNumer` 的序列化数字。

永久属性使用原 `cooking_prop_effect_full` 的 284 条阈值和原 `level_prop` 的 71 类数值；前者逐字节核对 APK，后者沿已登记原版本 WwiseBnk 来源。临时战斗效果复用既有 `map-status.json` 的 `StatusEffectInfo` 与 `battle-rules.json`／`skill-rules.json` 的原 FightStatus 实体及属性。新增适用范围背景 `ImgInfoBg_CookBuff` 为原 Sprite，已登记到全局来源和显式资源绑定；没有新增自绘图。中文数字显示依据原 `NumberCultureFormatter_CN`／`NumberFormatHelper`，颜色取原 `GlobalColors.Red`／`Orange`。

服务端 `OperPlayerUseCooking` 方法仍未取得；连续食用同种料理刷新效果由用户原作经验确认，处理意见和实现核对写在对应功能／地图验收文档中，不将该补证标为服务端代码来源。

生命上限变化的代码直接迁移自 `.tools/source-packages/official-code/common-source/Common/FightRoleFightComponent.cs` 的 `UpdateHpData`、`PropComponent_OnPropUpdated`，原文件 SHA256 为 `3b842762196f9f5701339f34be02179e9af3f352db79c770d83f0f637994c048`。`BattleFormulaHandler.MultAndCeiling` 对乘积向上取整；初始化使用原 float 生命比例，属性变化按原 double 比例同步当前生命，死亡时不补血。运行迁移位于 、不产生新素材；该公共组件已经包含实际方法，不再将本分支登记为服务端代码缺失。

布局交叉轴的可用空间约束迁移自 [Unity 官方 uGUI 的 HorizontalOrVerticalLayoutGroup.SetChildrenAlongAxis](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/Layout/HorizontalOrVerticalLayoutGroup.cs)：即使未强制扩展，也按可用空间、最小尺寸和首选尺寸夹取。该引擎算法已核对官方公开源码并登记链接，不将它冒充固定 APK 内反编译代码；游戏窗口自身的尺寸、锚点和布局参数仍取固定 APK。

### 人物／职业／技能：原条件、导师窗口与动态布局

固定版本854_156383的职业和技能迁移复用原ProfessionSettings、CharacterPanelProfessionPanel、ProfessionPanel、ProfessionPromotionPanel、ProfessionChangePanel、ProfessionChangeNotifyDialog、ProfessionSkillUnlockNotifyDialog及其独立属性／条件行模板。真实引用、输入哈希、中文语言和TMP／ClampingScrollRect／LoopScrollGridLayoutGroup字段见 profession-ui/source-manifest.json；技能自适应行、分隔行与碎片单元直接沿序列化PPtr提取，见 skill-ui/source-manifest.json。技能培养增强转换保留这些绑定。所有新增PNG均为原Texture2D／Sprite解码，没有绘制替代。

导师跳转读取原TutorEntityTag；NativeDialog职业动作与OpenProfessionPromotionWinNode改为等待真实窗口，不再打开即执行转职。晋升使用原前置职业、分离的总职业等级／职业等级条件及导师区域Public限制。通知使用原属性变化和技能说明组件；技能方案名称按原CJK.GetByteCount、CN上限和非法字符规则校验。以上C#输入哈希分别保存在两份模块来源清单；本地事务替代网络请求。职业服装与主副武器模型、地图晋升Playable不属于本条界面转换的完成范围，继续单独追踪。

### 行囊、物品使用与回收原始输入（854_156383）

prepare_bag_items.py 从固定 APK／匹配原版 CDN 的 prefabs_ui_playerbagpanel、prefabs_ui_itemdetail、prefabs_ui_confirmdialog 转换真实窗口，沿 ScrollRect.DefaultPrefabSource.Prefab 与 ItemChooseDialog.mOptionPrefab 提取原引用行；保留坐标、锚点、布局、状态、脚本字段、Slider 原生字段与官方语言。另读取 config_luban 的 item_tag、item_time_limit、random_treasure_exchange、period_card_item 以及 config_mapinfowrap 的 WorldStageTagMapInfo；未绘制素材或自行设计窗口。每个组件／输入／输出哈希和来源记录于 assets/reference/bag-items-source-manifest.json，运行数据为 assets/official/bag-items.json。独立转换模式不覆盖不同内容的共用 Sprite，资源登记由 --register-only 在共享锁内执行。另沿 ItemIconManagerConfig、ItemIconAttachmentConfig、ItemAwardIconManagerConfig 与技能／古遗物池配置提取真实图标模板、附件、奖励行和背景；3157 个物品 RawIcon 绑定来自固定版本原 Sprite，仍缺的 12 个输入与检索包名逐项保留在 unresolved_item_icon_inputs，按用户意见采用原客户端文字分支，不生成替代素材。补充外观箱与远古矿石时，沿 ChestInfo.GetIconPath／AppendAppearanceInfo 提取 69 个实际奖励外观的原图标，累计 3226 个物品绑定、1544 张不同图标 PNG、74 个窗口及引用模板；EquipReplacePropContent.mPrefabObj 沿原 PPtr 提取专用属性行，预览取 EquipAttributeReplaceItemInfo／EquipAttributesPropsInfo 与 SingleProp 策略。预览标题等动态语言键直接读取组件序列化字段；重复开启错误文本由 BroadcastManager 的 ErrCode 字段名规则取得，未改写原文。

行囊物品品质底图来自固定 APK 的 `defaultpackage_assets_image_ui_itemquality.bundle`（APK SHA256 `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9`，bundle MD5 `74e6afc6b39b88d0e8977d8f12267b52`）。`ItemIconConfig.QualityBgImages` 八个索引对应白、白、蓝、紫、橙、金、红、彩虹七张唯一 Sprite；路径 ID、解码输出 SHA256 和索引顺序登记在 `assets/reference/inventory-quality-backgrounds-source-manifest.json`，与现有官方源 PNG 像素一致。

### 2026-10-18 职业人物原模型与肖像

`scripts/prepare_profession_appearance.py` 与 `extract_profession_appearance.mjs` 从固定 APK／匹配原 CDN 的男女 Spine、职业皮肤表、主副武器部件、原 atlas／colormap 生成 `assets/characters/profession/`；来源对象、包版本、输入输出 SHA256 见 `profession-appearance/source-manifest.json`。职业肖像使用原 Profession 摄影尺寸、偏移、正交视野与透明背景，未知剪影着色逻辑来自原 GlobalUIConfig.SpineSolidColorMaterial。原 PointAttachment、部件缩放、正背面动画、UV 与网格变形均保留；没有绘制替代贴图。该目录只登记已经接入的职业 UI 服装及骨骼武器，独立装饰粒子和地图／战斗服装动作不混报为完成。

### 家园补齐窗口（固定 854_156383）

`prepare_home_ui.py` 从固定 APK 及匹配清单的 CDN 依赖转换原床铺、推车详情／升级／规则、装潢 prefab 与原 ItemIconManager／ItemAwardIconManager 配置，运行数据为 `assets/official/home-completion/ui.json`。40 个原窗口／引用模板、118 个资源的真实 bundle、对象、输入与输出 SHA256、原布局字段及语言来源统一保存在 `assets/reference/home-completion/source-manifest.json`。动态推车背景、升级图标、装备概率预览图标按原 LanguageUtils 路径／原 ItemInfo.RawIcon 取图；复用共同素材仅在 PNG 字节完全一致时进行，无重绘或替代。组件绑定、原 RectTransform、LayoutElementClamper、状态配置与原奖励图标池均保留。

### 技能列表与原动画锚点补充

技能列表排序／收藏字段的原 C# 输入哈希见 skill-ui/source-manifest.json 的 list_behavior；全部开关使用原 CommonCheckItem_On／Off。CharacterPanel_Skill 的原 AnchorMin／AnchorMax 曲线从固定 APK AnimationClip 解码，输入对象哈希见 character_animation_inputs；未用手调坐标补偿。收藏值保存到本地存档以替代原网络操作，界面状态和分组仍取原客户端实现。

### 原战斗动画控制器

固定 APK／匹配 854_156383 的 Appearance 与 FightMonster／FightNpc／FightCharacter／FightPet／FightSkill 资源，经 prepare_battle_animation.py 读取原控制器常量、m_TOS、默认参数、状态与过渡、覆盖片段和 MyPlayable 绑定，输出 assets/official/battle-animation/catalog.json。输入对象、程序集字段、原客户端代码及输出 SHA256 见 battle-animation/source-manifest.json。没有按动作名称前缀编造技能动画。现有 Spine 网格按 12 fps 采样，相同附件拓扑的顶点混合为引擎适配，逐骨骼混合的精度边界在对应功能／地图验收文档如实记录。

`prepare_unity_battle_skill_playables.py` 从上述固定目录提取 1,451 个含默认 Playable 的技能实体、1,480 个动作 cue（1,191 个 Animator 参数 cue、289 个 clip override），生成 `Combat/skill-playables.json` 并将官方 AnimationClip 复制到 `unity/Assets/Game/Resources/Combat/SkillAnimations/`。8 个 NPC／召唤物 clip 均由固定版技能 prefab 的 `OverrideClip` PPtr 解析到原始对象：7 个来自 APK 内 `defaultpackage_assets_appearanceassets_npc_npc_middle_model_anim.bundle`，另 1 个 `npc_10206/skill_01` 来自固定版本 CDN 的 `defaultpackage_assets_appearanceassets_npc_npc_big_model_anim.bundle`。7 个中型 NPC 动画来自既有 AssetRipper 缓存；新增大体型动画从该固定 bundle 单独转换，按 bundle 名、`m_Name` 和 `m_StopTime` 与原对象核对（1.8333 秒），原始 AnimationClip SHA256 为 `92777a742fd1146f6725d7b9d9a84686ac470bdc18f0a331caff6e5fbdf380c4`，导出 `.anim` SHA256 为 `b331f45ad0117359f3e34242bb01f8195612c014c93d8bf388c2c18295e410f7`。所有 132 个包内 clip 的来源文件／来源对象、APK 或 CDN bundle provenance、输入／输出 SHA256 及目录 GUID 写入 `battle-animation/skill-playables-source-manifest.json`。Unity Editor 导入和 Play Mode 动画结果尚未验证。

## 人物概览属性与位阶原绑定

`scripts/prepare_character_overview.py` 从固定 APK `854_156383` 的 CharacterPanel 读取 8 类原组件字段（含原 Slider 绑定），沿 PropPrefab PPtr 转换 CharacterPanelProp 行；原 Rank／SubRankSubLevel 语言、经验说明、赛季名称、player_subrank 图标及 EnumExt 分组生成 `assets/official/character-overview.json`。15 个实际 Sprite 和所有输入／输出 SHA256 登记于 `assets/reference/character-overview-source-manifest.json`；图标由原 Texture2D／Sprite 裁切，没有绘制替代。

Unity 人物运行目录由 `scripts/prepare_unity_character_catalog.py` 从上述人物/属性目录、固定版官方规则表、`role_prop_group.g.bytes`、`item_time_limit.g.bytes` 及衣柜资源清单生成。人物概览、属性、等级/位阶/赐福表、限时装扮表和原衣柜/属性图标 Sprite 保留固定版输入哈希；Unity 输出及每张图片的输入 SHA256 记录于 `unity/Assets/Official/Character/source-manifest.json`。角色外观、称号和衣柜 UI 数据仍由 `assets/characters/wardrobe/` 与 `Assets/Official/CharacterCreation/` 的来源清单分别追溯，Unity 载荷须随本模块资源快照恢复。


### 装备详情、赐福说明与普通碎片原窗口

`prepare_equipment_completion.py` 从固定 APK／匹配 `854_156383` 的 Character、ItemDetail、PromotePanel、Equip、Tips、ConfirmDialog 与 ItemIconAttachmentConfig 提取 16 个真实窗口／引用模板，沿原 PPtr 取得星石行、等级说明行及类型 13 的已拥有附件。保留原同名兄弟节点的对象引用、RectTransform、网格、布局、自适应高度、PopupWindow 遮罩、状态、TMP 字段与 TMPEffect 描边；没有绘制替代素材。真实 bundle、对象、语言、原 C#、输入输出 SHA256 与完整 276 行等级差表来源见 `assets/reference/equipment-completion/source-manifest.json`。脚本严格校验 TMP 字段、64 字节 AutoAlign 序列化范围和原始表输入，不把 SafeAction 当作 Unity 序列化字段。此清单由转换脚本生成；本轮先提交不冲突代码，共用素材登记待锁交接。

### 默认家园设施状态与通用修复界面（2026-10-18）

`scripts/prepare_home_scene.py` 从固定 854_156383 APK／校验原 CDN 读取 12 类默认设施，转换全部原状态分支、原碰撞、Spine、粒子与世界文字至 `assets/official/home-completion/scene.json`；来源对象、原始组件 SHA256、语言输入及资源／目录输出哈希见 `home-completion/scene-source-manifest.json`。未绘制替代图片。`prepare_home_ui.py` 另按原引用补入 FurnitureRepairDialog、FurnitureRepairedNotifyDialog、原 FurnitureIconWidget、PlayerPropItem 与 PopupWindow 入场／循环绑定，仍登记在 `home-completion/source-manifest.json`。运行加载绑定来自两份来源清单；独立导入复用一致的共享贴图。

### 2026-10-18 原战斗 HUD 与世界幻兽

`prepare_battle_hud.py` 从固定 854_156383 的 FightMainPanel、速度／托管、首领、行动条、战场状态说明、技能／幻兽与 StatusUI prefab 转换原 RectTransform、SafeArea、组件引用、Sprite、原生动画及字体。场景飘字直接转换 FightDamageSettings.EffectUIPoolPresents 的 13 个真实 prefab、TextMesh 位图字形和元素提示动画；保留原状态可见性、颜色、格式化、叠层／回合及首领分层生命／护盾计算。资源与对象输入输出哈希见 `battle-hud/source-manifest.json`，运行纹理在 `assets/official/battle-hud/`。

`prepare_battle_pet_models.py` 转换 40 个 FightPet 世界模型、40 个骨骼和 391 个原动画，保留世界层级、缩放、皮肤及静态几何；来源见 `battle-pets/source-manifest.json`，运行纹理、网格和动画在 `assets/official/battle-pets/`。没有用 UI 幻兽模型或自绘图片代替世界模型。世界模型与 HUD 使用独立显式 PNG 清单；动画绑定另见 `battle-animation/source-manifest.json`。

原控制器、方向 playable、生成／死亡、冻结／恢复在战斗时间线中执行；ShowSkillReplay、CDModify、技能扣血和元素提示绑定取原客户端与公共组件。尚未解析的图片地址原样列在 HUD 来源清单；没有替换或伪装缺项。共享导入、统一素材包和 Windows 正式成品需在资源锁交接后更新。

### 原战斗 Wwise 音效

 从1884个原 `MyFightSkill` 对象的 `PreferredPosToPlayable` 生成（输出SHA256 `5b661a9efbbd304977374446aa1c3b064b2b80605ff50b9fd88e8c3e7b426a90`），输入对象逐项匹配 `battle-audio/bindings-source-manifest.json` 的原对象哈希；文件内保留所用对象哈希集合的SHA256，重建入口为 `prepare_battle_audio.py --targets-only`。实体目标类型按原 `FightSkillComponentInfo.IsEntityTargetType` 的1／4／5迁移，用于保持Wwise对象范围和随机容器归属。

音量路由另读取 APK 的 `launch_assets_launch_scriptableconfig_wwisemanagerconfig.bundle`（MD5 `c68c296caddce110b1797a0f4c48e2e8`，SHA256 `a21c3dbc422bc531d05893c4085be52202e8ceaeb9e01cc12fd67b2ec00ab478`）：原 `Volume_ALL`／`Volume_SFX`／`Volume_Voice` 的ID分别为3240620755／3673881719／3676256460；`prepare_battle_audio.py` 的 `routing`／`check` 直接核验原引用与名称哈希。

`scripts/prepare_battle_audio.py` 沿固定 `854_156383` 的 MyFightSkill、全部 MyFightHit 系列、MyFightDamage、MyFightStatus、MyActionAgent、MyFightRoleCharacter 与 DefaultMoveEffectSupport 的 Playable 引用，提取方向、暴击／格挡、状态叠层、出场／死亡、序列时间、持续时间及停止事件；另从五种原阶梯楼层 prefab 读取 `StageDemonBattlefieldPanelItemPlayerPlayable` 的 Wwise 引用。从 28 个已校验原版 Wwise 分包（含 Init）转换 2172 个 Event、2068 份 Vorbis；新增阶梯 `Note_FantasyLadder_Flash`（2367806325）和 `Note_FantasyLadder_Elevator`（828673327）。wwiser 展开原 Event／Action／随机容器及原 dB 增益，vgmstream 解码，保留循环边界、随机变体、防重复／洗牌、停止对象范围与淡出。未使用自动音量归一化或替代音频。运行资源在 `assets/official/battle-audio/`，通过该目录 `resources.json` 和 显式加载；原包 URL／MD5／SHA256、原 Playable 对象／类型与输出哈希分别登记在 `battle-audio/source-manifest.json`、`battle-audio/bindings-source-manifest.json`。`--check` 校验来源包、全部输出哈希、循环与显式绑定覆盖。

### 职业 UI 武器的原 RectTransform 位置

固定 854_156383 的 pWeapon_100_G_007_UI_Integrated 与 pWeapon_200_G_007_UI_Integrated 中，Spine 子节点的 anchoredPosition 为 (253,0)、旋转 90°、缩放 0.5，序列化 localPosition 则为零。prepare_profession_appearance.py 沿原父节点尺寸、锚点、pivot 和 anchoredPosition 合成部件变换，不能以零 localPosition 替代；六类武器的中间节点及旋转均直接读取原 prefab。更新后的真实输入及输出哈希仍登记在 profession-appearance/source-manifest.json，没有增加手绘素材或经验偏移。

### 2026-10-18 原动画组件的参数与生命周期

从固定 APK 的解码 GameFramework.Runtime.dll 反编译 MyPlayable、MyPlayableSequence、MyPlayableCenter、MyPlayableApplyObj、MyPlayableTrigger 和 MyPlayableConfig，缓存原文位于 .tools/source-packages/official-code/gameframework-animation-source/。结合原 AnimationController、StateMergeList、MyPlayableAnimator、MyPlayableOverrideAnimator、MyPlayableAnimationControllerPause 核对冻结写入、初始化缓冲、前置状态限制、覆盖先后次序、淡出及停止恢复。prepare_battle_animation.py 额外导出真实 AnimationController 配置、覆盖片段时长／循环／采样率及原 MyPlayable 淡出参数；来源与哈希保存在 battle-animation/source-manifest.json，运行目录仍为 battle-animation/catalog.json。

现阶段这项授权不等于连续骨骼播放已经接入。


### 家园原领取飘条与属性升级粒子

prepare_home_ui.py 继续从固定 854_156383 转换 AwardListPanel、其序列化 normal-path GUID fe14ea0fc3099824683283c4671a9e49 指向的 AwardListPanelItem、原 AnimationClip 事件和 UI ParticleSystem 材质／纹理。UIParticle 的 14 个原 AOT 字段次序及 payload 全量读取由 prepare_map_ui_particles 校验；home_particle_schema.py 的独立 Coffee 类型只解析对象引用，粒子本体仍读取原字节，没有生成替代效果。home_ui_audio.py 沿原 Main_Get_2 Event 3442720740 的 Play／Sound／DIDX 媒体，使用 vgmstream 与 libsndfile 转为本地 Vorbis。源对象、包凭据、原始／输出 SHA256 见 home-completion/source-manifest.json，运行资源在 assets/official/home-completion/。


### 地图职业外观与官方晋升 playable

prepare_profession_appearance.py 在既有 UI 预览之外读取固定 854_156383 的六类 _Integrated 世界武器 prefab、SkeletonAnimation、原局部变换和 BaseSkeletonFollower，并转换男女原地图动作与各职业服装；62 套职业配置、27 段 UI／地图动作与 40 张图集的真实输入输出哈希保存在 profession-appearance/source-manifest.json。没有使用 UI 武器 prefab 代替世界武器。

prepare_profession_playable.py 从 PlayerWorldProfessionUpgradeConfig.asset 的实际 PPtr 取得内嵌 player_UpProfession_playable，转换 10 条 MyPlayable 轨道、25 个原粒子及 3001.prefab AttachAgent 层级。profession_upgrade_audio.py 按原 AKPK／HIRC Event→PlayAction→Sound→DIDX／DATA 路径取得 Bless_1／2／3 的原媒体，使用 vgmstream／libsndfile 转为本地 Vorbis。资源位于 assets/official/profession-upgrade/；原包、组件、公共代码、媒体偏移与输入输出 SHA256 见 official-map/profession-upgrade-source-manifest.json。没有替代素材、手调动作或自制音效。


### 自由转职公共计算代码

固定 854_156383 的 PlayerChangeProfessionDataExt、ProfessionBaseInfoParser、PlayerProfessionDataEx、PlayerChangeProfessionData 和 RandomUtils 对应的原代码与配置已核对；继续使用原 profession_base 与 ChangeProfessionSetting 配置。真实源码 SHA256 与配置键见 freedom-profession/source-manifest.json。`prepare_freedom_profession.py` 继续从固定原包及匹配 CDN 转换 13 份原窗口／动态模板、74 份组件绑定和 24 行奖励配置，保留原多边形选区、状态切换、坐标、动画、未获得标记、确认按钮倒计时及男女职业立绘引用；输出 `assets/official/freedom-profession.json`，来源、原始对象和输出哈希同存于该清单。没有自绘补充。

### 幻兽高赛季原说明与升级预览

prepare_pet_progress_ui.py 从固定 854_156383 的 prefabs_ui_tips、prefabs_ui_pet 解析原 PopTipMutilDesc、PopTipBlessProp、其 TipInfo2 和 PetDetailCommonContent 指向的 PlayerPropItem；保留原布局组、禁用 LayoutElement 的忽略布局字段、LayoutElementClamper、TMP 边距及 GreenRainbow 预设引用。4 份模板、60 份组件绑定和 11 张引用图片的原包／对象、输入输出 SHA256 见 pet-progress/source-manifest.json，输出为 assets/official/pet-progress/ui.json。10 张图片复用并逐字节验证现有原贴图，仅 ImgInfoBG_Bless 转为模块独立的原生 ImageTexture；没有重绘图片或重新设计坐标。prepare_pet_progress_text.py 从原 WwiseBnk 语言分包生成原文及源哈希，运行时替换原 Item61 名称。


### 原装潢皮肤、天气替换与场景槽位

prepare_home_decoration.py 从固定 854_156383 的 64 行 furniture_skin、19 行 home_theme_skin 和 48 行 home_skin_weather_override_prefab，按原 LanguageUtils 路径定位 131 个实际 prefab 变体；原 HomeEntityManagerMono／HomeSlotEntityData 提供主题及家具槽位，原 HomeEntityCreateData 提供位置偏移。保留全部状态分支、CustomRenderer 云层、原果树果实附件及昼夜动画，骨骼沿既有 Spine 转换工具生成原网格。输入对象、APK／CDN 凭据、916 个资源和输出 SHA256 见 home-decor/source-manifest.json，实际资源在 assets/official/home-decor/。材质属性动画的哈希字段按原材质字段及 AssetRipper CustomCurveResolver 的 Unity 格式解析，没有自拟游戏规则或美术。`prepare_unity_home_skins.py` 从这些转换结果生成固定版运行目录 `unity/Assets/StreamingAssets/Home/home-skins.json`，并按实际引用复制 628 张官方贴图；运行输出和逐张贴图 SHA256 写入家园 `source-manifest.json`。

prepare_home_ui.py 同时补充原 HomeViewController／Slider 控件、HomeSkinItem／ItemIconHomeSkin 字段，沿 ItemIconAttachmentConfig 的 WearingHomeSkin=31 GUID 提取原已装备附件，从原 image_itemicon_home 提取对应皮肤图标；来源继续登记于 home-completion/source-manifest.json。来源登记与尚未迁移的灯光／材质表现范围分别记录，不能将已提取视为已完整还原。




prepare_battle_spine.py 从固定 854_156383 已核验 APK／原版 CDN 的骨骼、atlas 和 SplitBinarySO 输入，重新组装原格式二进制供官方运行库直接读取；使用官方 @esotericsoftware/spine-core 4.2.110 解码定位字节边界，已开放皮肤及其链接依赖、骨骼／约束／槽／绘制顺序／事件轨道保持原字节，不烘焙姿态；未开放皮肤只保留原表索引并删除相关附件及变形／sequence 轨。图集只改页路径，贴图复用已有原转换或按需解码原 Texture2D，未绘制替代。739 份原骨骼及男女 227／237 动画在 assets/official/battle-spine/，显式目录由  读取；完整输入输出哈希及复用来源凭据见 battle-spine/source-manifest.json。

`prepare_battle_spine_pages.py` 从固定 APK 按已核验 Texture2D 对象恢复 Spine atlas 页并绑定生成材质；除开场创角录像所需的 monster 3803／3281／3282／3283／3481 与 NPC 8401／8402 外，本次为 Map10 已映射的 monster 3204／3206／3207／3254／3255／3281／3282／3283／3401／3411 提取并核验纹理，合计16页，路径、页尺寸、APK bundle/path ID 与输入／输出哈希见 `battle-spine/pages-source-manifest.json`。Map10 的 monster 3603 不在当前模型 identity 表中，因此未列入 Spine 贴图恢复；其时间轴头像仍由独立 portrait catalog 提供。

prepare_battle_animation.py 读取原 SkeletonRenderer／SkeletonMecanim／SkeletonAnimation 的完整序列化字段，按实际模型路径登记独立 prefab 绑定。客户端控制参照 PlayerAnimatorAppearance、AppearanceAddressUtils、AppearanceUtils、AppearanceSkeletonPart／MultiSidesPart、AppearancePartSideSwitcher、BaseSkeletonFollower 和原职业配置；来源与源文件 SHA256 分别登记于 battle-animation/source-manifest.json 及 battle-spine/source-manifest.json；这是引擎适配，未将 Unity 原生 Animator 内部实现冒称为已取得的游戏脚本。原过渡全部为 InterruptionSource=None，其混合中仅 AnyState 可中断的语义另核对 [Unity 官方过渡文档](https://docs.unity3d.com/2022.3/Documentation/Manual/class-Transition.html)，此引擎依据与 APK 游戏代码分别登记。

三份 DLL 与 [上游 LICENSE](https://github.com/EsotericSoftware/spine-runtimes/blob/4.2/LICENSE) 原样保留在 assets/official/spine-runtime/，逐项哈希、上游代码参考 URL 与客户端代码哈希在上述来源清单中。该运行库来自 Spine 官方，并非游戏 APK；游戏美术、骨骼、动画及控制配置来自固定原版。用户已明确授权这一运行时扩展例外。

## 技能祈愿原客户端结果演出

固定APK及匹配854_156383 CDN中的prefabs_ui_gachapanel、prefabs_ui_item、prefabs_ui_pet与原AnimationClip、ParticleSystem、Sprite、TMP字段和Wwise事件链由scripts/prepare_wish_performance.py转换，输出assets/official/wish-performance/。完整对象来源、客户端类SHA256、布局输入、图片／音频处理及输出哈希保存在[来源清单](wish-performance/source-manifest.json)。界面图片直接解码，动画保留原曲线与AnimationEvent，音频按原Event→Action→Sound→媒体解码为Vorbis；原停止事件登记为stop，无替代音效。LoopScrollRectBase来源于已解码的原LoopScrollRect.Runtime.dll，作为原百抽滚动方向和首屏行数计算依据。pet相关prefab绑定为继续迁移结缘所需的原始输入，提取完成本身不代表已接入运行时。


### 家园设施原提示图

prepare_home_tips.py 遍历 131 个原装潢 prefab 变体中的 TipDotNodeLinker，沿原 PPtr、有序 Childs 端口和嵌套动态选择器取得当前294个实际提示节点，保存条件字段、原优先级和状态覆盖规则。原对象与客户端源码 SHA256、配置输出哈希见 home-tips/source-manifest.json；运行数据位于 assets/official/home-tips/graphs.json。运行时由 official_home_tips／official_fruit_tips／official_home_tip_queries 接入真实系统查询；活动与星神仅在存在对应实际玩家记录时可产生子项，不生成虚构实例。


### 装潢新物品提示源码

ItemNewMarkManager 的零到正事件及登录清空、HomeSkinItem 离开列表清标记、HomeSkinMainTabItem／HomeSkinSubTabItem 分类聚合和 AppearanceDataEx 排除默认皮肤均按固定 854_156383 迁移。真实源码 SHA256 保存在 home-completion/source-manifest.json 的 mark_sources，由 prepare_home_ui.py 维护；继续使用原 NewMarkStateSwitcher、原文字和布局，没有新增替代图案。


### 原家园皮肤专用详情

prepare_home_ui.py 从原 prefabs_ui_itemdetail 的 FurnitureDecorationItemDetailDialog 提取完整布局和 mTryBtn／mFashionTxt／mIsShowFashion，按原 FashionExpUtil 和三类皮肤解析器取得 fashion_exp_group 的真实品质经验。home-completion/source-manifest.json 的 detail_sources、fashion_table、panels、inputs 与 resources 登记原版本代码、配置、对象和输出哈希。没有改用普通物品详情或自制预览按钮。

### 古遗物原快捷操作、TMP赐福与说明弹层

scripts/prepare_relic_bindings.py只读固定854_156383的TreasureElementTab、TreasureDetailDialog、IntroductionPopBtn、BlessPropBtn、TMPEffect和TextMeshProUGUI原组件，按原TMP_ColorGradient引用读取GreenRainbow四色；完整包／CAB／对象。PopTipMutilDesc及序列化引用TipInfo2沿原PPtr提取，保留禁用LayoutElement的ignoreLayout与真实布局；图片逐字节复用既有官方转换，ImgInfoBG_Bless通过pet-progress/source-manifest.json核对同bundle／path_id、解码PNG输入及原生ImageTexture输出SHA256后引用。没有重绘图片或新增独立布局。

prepare_relic_text.py保留原中文rawfile、TreasureDetailDialog及IntroductionPopBtn源码哈希，生成108条原文并按原Item61替换。着色器是跨引擎适配代码，游戏颜色和描边来自固定版。快捷操作依据TreasureElementTab、TreasureHelper及Player.AutoSetupTreasure，服务端循环缺项和用户确认的处理意见另见对应功能／地图验收文档，不伪称取得服务端实现。


### 结缘原始演出与角色击打动作

prepare_pet_gacha_performance.py 从固定 854_156383 的 APK／匹配清单 CDN 读取 PetGachaSettings、六个 Pet UI prefab、原 MyPlayable 轨迹／粒子／拖尾／Wwise 与男女角色 SplitBinarySO。pet-gacha-performance/source-manifest.json 登记源对象的 bundle、对象、来源及 SHA256、转换输出及客户端源码 SHA256。资源位于 assets/official/pet-gacha-performance/；六段角色动作通过 repack_battle_spine.mjs 保留原二进制动画，不使用插帧或重绘，图集仍引用已有官方原贴图。MainCanvas／UICamera 决定画布单位，原 PolygonCollider2D 决定百抽掉落范围；各 prefab 的 UIBloomGroup、UIBloomPlayerElement、UIBloomGraphicElement 归属路径与原组件来源随性能数据登记。


## 炼金炉制作演出与机械师熔炉

固定 APK／原版本已校验 CDN 的 `prefabs_ui_bagualupanel`、`prefabs_ui_mechanicalfurnace`、`prefabs_ui_confirmdialog` 与 `prefabs_ui_tips`，经 `scripts/prepare_kitchen_completion.py` 转换为 `assets/official/kitchen-completion.json`、同名目录中的原 Spine 网格与 Wwise 音频。使用原 RectTransform、状态切换、中心宝石 Animator、三套 Timeline 的 Control／Spine／AkEvent／Signal 轨道、147 个机械师发射器及料理炉的 3 个发射器。RawImage 保留原贴图，UIMesh 保留网格、UV 与材质参数，流动扭曲及结果扫光按 APK 中编译的 `UI/EffectDistort`／`UI/EffectAdditive` GLES 程序迁移；shader、源对象和输出 SHA256 见 `assets/reference/kitchen-completion/source-manifest.json`。未绘制替代素材。

四项原粒子依赖使用已登记的新版同名包中匹配 CAB 的官方对象，经 `CounterpartAssets` 独立读取，版本、对象、实际 URL 和输入输出哈希均保留于该清单；原版本 185 个 404 缺项仍保留，不混入或改名覆盖原缓存。Spine Timeline 混合遵循 EsotericSoftware `spine-runtimes` 4.2 的官方 `SpineAnimationStateMixerBehaviour.cs`，来源 URL 与取得文件 SHA256 同样登记；

规则依据为固定版本 `GemMergeManager`、`GemMergePanel`、`HighRankGemMergePage`、`GemMergePanelListPanelItem`、`GemPartInfoParser`、`PlayerGemDataExt`、`gem_merge`、`player_subrank`、原成就生成器及剧情 20000–20003。前三次保底剧情 `CompensateGems` 由未取得的服务端返回；本次失败材料的返还范围现已由用户原作经验补证，具体确认与实现核对记录于对应功能／地图验收文档，不标作已取得的服务端方法体。


2026-10-18：家园提示来源清单 assets/reference/home-tips/source-manifest.json 追加固定854_156383 FunctionEntryDialog 的原 HomeSkinBtn／TipDotNodeLinker 绑定，以及 HomeSkinNewMark 的2个原图节点。graphs.json合计232节点，保留原对象路径、状态配置、托管条件、有序连接、PresetTipLevel及HomeSkinBtn／HomeSkinUtils源码SHA256；没有新增或自制图片。来源转换脚本prepare_home_tips.py，。

### 结缘客户端演出运行接入

在原结缘演出转换基础上，补取固定 manifest `854_156383` 的完整 `PetGachaPanel` 外壳、`AwardFloatingSettings`、积分和保底刷新绑定及原语言文字。当前模块使用同一份 `assets/reference/pet-gacha-performance/source-manifest.json`，累计 1,955 个输入对象、128 个输出；记录 APK／原版本 CDN 来源、bundle／对象、客户端与补充反编译代码、两份原语言输入及输出 SHA256。来源顺序未变，未引入自绘或混用新版包。

`prepare_pet_gacha_performance.py` 从 APK 清单解析外壳真实 bundle，保留默认 Animation 片段及扩展粒子材质。`Fx_XingKon_0002` 没有显式 `_MainTex`，使用已解析的原 shader 默认白纹理，以常量资源表达官方默认行为；不是替代图片。10 个官方 NoiseModule 的参数和曲线直接进入运行模块；Unity 引擎原生采样算法和 quality 实现未在当前官方客户端源码中取得，因此不声明逐轴或逐帧一致；原 `UIBloomManager.SetActive` 仅在 Bloom 全局开关开启且需求计数大于零时激活，需求计数由 `UIBloomRequirer.OnEnable/OnDisable` 管理；结缘使用的 prefab 共登记 6 个 `UIBloomGroup`、`UIBloomPlayerElement`、`UIBloomGraphicElement`，没有 Requirer。结缘页不自行注册需求或强制启用 Bloom；结缘页隐藏／离树时， 按 `PetGachaResultPerformer.OnDisable` 清空队列与计时、释放工作中的奖励与特效、停止音效并关闭操作遮罩，恢复显示时应用官方默认动画；`OnWindowClosing` 的停止计时行为一并迁移。结缘场景、结果与新幻兽介绍由原 UI 层级、RectTransform、动画和调用时序迁移，运行入口为该控制器及其 reward／effect／popup 脚本。价格黑底隐藏沿用用户明确确认的界面调整，不标为原版表现。校验命令为 `python scripts/prepare_pet_gacha_performance.py --check`；素材包和 Windows 发布状态见对应功能／地图验收文档。

### 结缘新幻兽技能说明与词条

`prepare_pet_gacha.py` 从固定版 `rules.json` 读取幼年支援技能的 `HyperLinkTypes`／`HyperLinkDatas`，将官方 EntryId 与技能说明一起写入结缘运行目录。词条名称、说明、图标及 `EntryDetailDialog`／`EntryDetailItem` 原始层级来自 [codex-links载荷](../official/codex-links/links.json)；该输入的来源对象及输出图标哈希见 [codex-links来源清单](codex-links/source-manifest.json)。脚本把实际需要的六个词条及原图标复制进 `PetGacha` 动态资源目录，并在 `pet-gacha/source-manifest.json` 登记输入、输出和SHA256。客户端行为依据固定版 `PetGetNewPanel`、`SkillHyperLinkDataProvider` 与 `HyperLinkClickHandlerEntry`；没有自绘技能说明或词条图片。


2026-10-18：prepare_home_decoration.py 从固定 854_156383 的 131 个实际家具／主题／天气 prefab 原 Transform 追加 local_trs（原 m_LocalPosition、m_LocalRotation、m_LocalScale）。--transforms-only 核对既有实体 name 和完整源对象凭据，独立重读原对象，更新 assets/reference/home-decor/source-manifest.json 的输入与实体 SHA256；既有 AnimationClip 原曲线、插槽及创建偏移不变。运行时  迁移原父子局部矩阵，未添加自绘素材或自定关键帧。

### 宝石选择动态挂件

固定 854_156383 的 ItemIconAttachmentManager → ItemIconAttachmentConfig → GUID 对应 GotObj、Mask、RedDotContent 三个原 prefab，由 prepare_kitchen_completion.py --selection-only 按原 RectTransform、Image 色值及 ItemIconAttachmentCell 层级 7／0／20 转换。ImgTick、ImgQuality_Mask、ImgRedDot 复用已有官方贴图；资源清单当前共 181 项、数据输出 25 项，配置、挂件对象、客户端代码及语言输入／输出 SHA256 保留于 kitchen-completion/source-manifest.json。MergePanel.GemMerge_FullLevel 从固定版原语言包提取，未手写替代提示。完整重建亦包含这三项挂件；--language-only 仅重新提取已有界面使用的原文。

### 古遗物战斗计数键与原枚举

prepare_treasure_tasks.py补充固定854_156383的BattleEventType、原TreasureDataBattleStatus／TreasureDataGameDestroyMonsterKind、BlackboardDataType及MapEntityWorldStageType来源继续保留，75件选择键和配置未改变。本项只有代码输入及运行时接线，没有新增素材。服务端事件写入方法未取得，实际触发按用户确认迁移为本地接线，确认原文与实施核对仅记录于对应功能／地图验收文档。


2026-10-18：prepare_home_ui.py 从固定 854_156383 的 FurnitureFastHangUpDialog、其 ItemPrefab 指针 FurnitureSpeedUsedItem 迁移原层级／状态／滑条与长按字段，共60个窗口／条目、269张相关原图；原客户端／公共程序集的加速请求、预览和四类处理器代码输入 SHA256 登记在 home-completion/source-manifest.json 的 fast_sources。／／ 使用这些真实配置与代码；服务端普通结算体未取得，直接到账和超过设施储存上限的处理由用户经验补证，具体边界仅记对应功能／地图验收文档。未添加自绘素材或自定义布局。


### 幻兽兑换原商店7

2026-10-18：prepare_pet_exchange.py 从固定854_156383 APK／匹配原版本CDN提取PetGachaPanel商店区、ShopGoodsItem、ShopGoodsItemInfoCommon、ShopGoodsItemShowConfig及七档原品质图，保留原RectTransform、LayoutGroup／LayoutElement、StateSwitcher、ListItemShowSequence和中文键。资产写入assets/official/pet-exchange/，288个源对象、70个输出的bundle／对象、输入输出SHA256、原客户端与Common源码及两份语言包哈希统一在pet-exchange/source-manifest.json登记。ShopOpenController实际mShopId=7；当前商品类型使用原默认Common图标模板，名称按原ShowName显示，没有绘制替代图或添加第二套商品坐标。

购买成功／失败声音由ShopGoodsItemShowConfig的实际事件引用追到原UI／Minimum Wwise包的Event→Action→Sound／DIDX媒体，转为Vorbis并保留包哈希和原媒体哈希；Main_Cursor_Fail位于Minimum包，未用其他声音替换。购买确认及模糊遮罩复用已有原ConfirmDialog与默认BlurMask资源。源校验：python scripts/prepare_pet_exchange.py --check；运行规则、测试、尚未迁移项与素材包／正式发布状态仅记对应功能／地图验收文档。


### 机械师保底剧情原立绘与语音

prepare_kitchen_completion.py --story-only 从固定 manifest 854_156383 的 prefabs_ui_story 和 prefabs_ui_portrait 迁移 TalkDialog、OptionTalkDialog、Npc_Portrait_8422 的原 RectTransform、PopupWindow／角色动画和托管字段。PortraitFace.LoadMain 的真实 GUID a0a47b1eef578c948abe2f44204ceee4 对应 npc_portrait_8422，按原 SetNativeSize 及 Main 偏移显示；三个表情保持独立原图，剧情空表情时隐藏 Face 层，不使用 NPC 列表半身图替代。chapter_3_side_5_01–12 从原中文 chapter_03 Wwise 分包的 Event→Play→Sound→media 调用提取；原包 MD5、实际来源、媒体偏移、输入与 Vorbis 输出 SHA256 均登记于 kitchen-completion/source-manifest.json。当前资源记录204项、数据输出25项。运行时按原设置的 StoryList 播放前三次补偿剧情，原20003仍保留为配置内容；本地退款范围的用户补证与原子保存适配见对应功能／地图验收文档。


2026-10-18：新增 home-fast/source-manifest.json，prepare_home_fast.py 将固定854_156383的 FastHangUpProgressBarDialog、ConfirmDialog、男女home/sleep_02分离动作、Sleep_Bubble、AppearanceConfig覆盖0（男10206／女20206、33201、34000）及对应原图集和原染色遮罩转换到 assets/official/home-fast/。14个运行输出、真实bundle／对象、原CanvasScaler像素比例、HomeEnvironmentConfig的FastCycleCurve和两个动画事件、原客户端调用代码及spine_skin表均保留来源与SHA256；没有自绘素材或自定演出时长。原动作转换器复用 prepare_pet_gacha_performance.py 的player_actions，额外皮肤页从原角色图集包解析，不借用不匹配图集。


### 家园设施教学原对话与立绘

`assets/official/home-tutorial/` 由 `python scripts/prepare_home_tutorial.py` 生成；来源固定 `854_156383` 的 `prefabs_ui_story/OptionTalkDialog`、`TalkDialog`、`prefabs_ui_portrait/Npc_Portrait_8402` 与 `Npc_Portrait_8411`，及其实际GUID引用主图／表情。26个输出含25张原PNG和一份原层级／字段／动画数据；保留真实SetNativeSize、表情差异偏移、左右翻转与动画事件，无自绘或截图代替。源bundle、对象和输入／输出哈希、语言及调用代码登记于 `home-tutorial/source-manifest.json`。运行时家园教学使用原Common代理对应的OptionTalkDialogPanel，并复用 `map-audio.json` 已登记的原中文语音。独立导入：`python scripts/import_official_subset.py assets/reference/home-tutorial/source-manifest.json`。


家园对话平铺背景：原 OptionTalkDialog 的 BgImg／BgImg2 与选项按压背景为 Image.Type.Tiled，APK 的 FillCenter 与 PixelsPerUnitMultiplier（该批为1）由 prepare_official_city.py 读取，原 Sprite 边框及尺寸保留。 按 Unity 官方 uGUI Image.GenerateTiledSprite／GetAdjustedBorders 迁移平铺、边框缩小、末块UV裁切和65000顶点限制，反转PNG纵轴并保留原Sprite裁切范围；未绘制新图。框架源为 https://raw.githubusercontent.com/Unity-Technologies/uGUI/main/com.unity.ugui/Runtime/UGUI/UI/Core/Image.cs ，取得内容SHA256为74a8d2f63d1ea3783d34f2ba9d778191cc9b27bea5cfa0670d69f1defd9e29dd，缓存于 .tools/source-packages/official-code/ugui-reference/（Image.cs与source.json）。这是Unity官方UI框架来源，与游戏客户端来源分别登记；对话原图及实际输入／输出哈希见home-tutorial/source-manifest.json。重建需恢复该框架来源缓存；已转换对话记录Image原字段，其他尚无tiling字段的旧转换不作参数推定。


### 家园原场景月相（854_156383）

`home-environment/source-manifest.json` 登记主场景 HomeEnvironmentManagerMono.HomeMoon 的三个 SpriteRenderer、HomePlayable 的 Night 绑定及 Night_Common 中9条月亮曲线。原 `image_home_dynamic_moon` 分包解码配置会选择的1至15与17号月亮／描边／光晕，共48张PNG；连同 moon.json 共49项输出逐项登记SHA256，保留原Sprite尺寸、裁切、轴心、PPU、世界层级及排序。规则来源为 HomeMoon、HomeEnvironmentManage、HomeTimeDriver、HomePlayable/TimelinePlayable 和原 HomeEnvironmentConfig／HomeSkinConfig：自然日30天一轮、每相2天、08:00刷新、天气4902选择17号；阶段显示小时不改变自然日。转换命令 `python scripts/prepare_home_environment.py`，导入使用 `python scripts/import_official_subset.py assets/reference/home-environment/source-manifest.json`。原3个材质均为 Sprite-Unlit-Default；场景照明、云网格和积雪材质的实际迁移见后续相应来源段落。


### 家园原昼间太阳和光照时间轴（854_156383）

`home-environment/solar-timeline-source-manifest.json` 登记固定 APK 主场景 `HomeTimeline`（Timeline path ID 188）及 Day 绑定的 `Day_Common`（AnimationClip path ID 181）。`python scripts/extract_home_sky_timeline.py` 按原 Mecanim 绑定解出20条曲线、21个节点、9个SpriteRenderer、5个Light2D和两种原材质，恢复 Sprite 裁切偏移与逻辑尺寸，所有输入／输出记录 SHA256；`python scripts/prepare_unity_home_environment.py` 校验后复制 JSON 与 PNG 至 Unity `StreamingAssets/Home/environment-visuals/`。曲线包括太阳位置／缩放／显隐、Sprite透明度、Light2D颜色／强度／阴影强度，保留原 Hermite 切线。原材质 shader 为 URP `Sprite-Lit-ColorShift` 和 `Sprite-Lit-Default`。当前项目使用 Built-in 管线且未安装／启用 URP 2D，因此 [`HomeSkyTimelineView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSkyTimelineView.cs) 会安全禁用该图层并报告依赖；恢复 URP 2D Renderer 后仍须执行 Editor 导入、编译及 Play Mode 画面验证。


### 家园天空原ColorBlend编译逻辑

`home-environment/sky-source-manifest.json` 从固定版背景prefab的真实Material引用提取唯一 Sprite-Lit-ColorBlend 的GLES程序、8种背景材质及对象／分包／原始字节哈希，重建使用 `python scripts/prepare_home_sky.py`。全部实际材质 `_UseMask=0`；ShapeLight纹理及场景照明绑定尚待迁移，运行时仍未启用该输入，不能据此称环境光已还原。


### 家园原NPC强制行为与诺诺（854_156383）

`python scripts/prepare_home_npc.py` 从原主场景 NpcBehaviour 的 HomeActionBehaviourCatalog 转换全部9个动作图及2个原传送Playable；原节点顺序、坐标、朝向、等待、显隐、HomeMid02/10排序、21个粒子发射器参数均保留。HomeNpcModel_8402 使用原0.4管理缩放、0.72子节点缩放、body_8402皮肤与原分离动画 npc_8402/idle_01，通过官方Spine原生扩展持续计算；复用已校验的原版 map_9 分包NPC图集。20项新输出含16张PNG、原粒子网格、Spine二进制／atlas及运行数据；输入、图集复用、真实原MeshRenderer材质／shader与输出哈希登记 `home-npc/source-manifest.json`。MyPlayableCenter／Sequence／Manager 的原程序集反编译代码说明传送使用普通帧时间；NPC Animator 使用 HomeTimeDriver 的 PlayableDeltaTime；行为图Wait使用自然家园时间。原SkeletonEffect-2D编译程序和场景光绑定见“家园灯光与材质迁移”。提取器按原pass的m_BlobIndex区分可执行程序与参数块，不再误读参数块为GLSL。


### 家园原CustomRenderer云网格与HSL

`python scripts/prepare_home_clouds.py` 从固定版 Background／Background1stAnniversary 及其雪／永夜prefab提取实际挂载的39个CustomRenderer。保留原Sprite.vertices／triangles、轴心、PPU、裁切UV、层级矩阵与颜色／HSL；复用原PNG，不新增图。运行数据为 `home-environment/clouds.json`，来源清单 `home-environment/cloud-source-manifest.json` 登记实际Sprite／Texture／材质、程序集代码、两份原CR-Lit-ColorShift GLES程序及输入／输出哈希。39个云网格的实际昼夜曲线接入shader。固定版这些实体及主场景未挂载HomeCloud，不根据同名未使用类添加遮罩或Shrink规则。场景光纹理及实际挂载的其他特效云材质见“家园灯光与材质迁移”。


### 家园原HomeObjectSnow与降雪控制（854_156383）

`python scripts/prepare_home_snow.py` 读取固定版雪景实际挂载的1575个SpriteRenderer，保留紧密网格、原三角形、Sprite／SpriteAtlas的uvTransform、翻转及原矩阵；复用原数据中的层级、颜色和状态。52份独立原材质、64张完整原Texture2D及原HomeObjectSnow的两份GLES程序登记于 `home-snow/source-manifest.json`，运行数据为 `home-snow/snow.json`。保留原透明扩展、双雪色、细节平铺、Param和LightAtten分支。HomeWeatherSnow按原OnEnable满雪、OnChangeCreate两秒自然家园时间渐积雪及三组原粒子maxParticles曲线接入；无新增绘图或自定渐变。场景光纹理接入见“家园灯光与材质迁移”。


### 家园及祈愿／幻兽依赖总登记

`python scripts/register_home_assets.py` 仅按17份既有独立来源清单核对固定854_156383资源及转换输出后补齐总清单；PNG等验证原bundle清单MD5，音频额外核对APK内Wwise包MD5／SHA256及原media片段SHA256。已有资源或数据哈希冲突直接报错，不覆盖旧期望哈希。云／积雪shader与转换记录同步核对，`--check`只读检查登记及完整加载绑定；保留各模块完整来源凭据。月相／NPC／教学89张图的加载绑定恢复为按需加载，使用方式与其他官方资源一致。

### 委托任务地图阶段背景

`scripts/prepare_task_bindings.py --convert-art` 从固定 manifest `854_156383` 的 `defaultpackage_assets_image_ui_itemnotes.bundle` 读取原始 Sprite，并复用既有 Unity 解码、裁切规则，补入 12 张地图阶段背景；没有重绘或改变布局。来源、bundle MD5、对象 path_id、原始对象 SHA256 与 PNG SHA256 见 `assets/reference/task/source-manifest.json`。卡片使用 `Map/map_ui_display.MapBgPath` 及当前真实来源地图选择背景；发布者和怪物图片复用既有官方转换，原始 UI 字段和语言文件哈希内嵌于 。


### 赛季装备柜原界面与快照详情（854_156383）

`python scripts/prepare_season_cabinet_ui.py` 从已校验原版 `prefabs_ui_equip` 的 EquipCabinetEquipDetailPanel、EquipCabinetPropItemGroup、EquipCabinetPropRuleDialog、EquipCabinetRuleFormDataItem 和 EquipCabinetContent 转换层级、RectTransform、状态与组件绑定；沿实际 mOtherPropPrefab、mTipInfoPrefab 指针取得 EquipMutillPropItem 和原说明行，并读取原 prefabs_ui_tips／prefabs_ui_itemdetail 的祝福、套装、等级及技能说明。10 个模板、325 条对象／布局输入、87 项既有原资源引用、18 份客户端／公共代码 SHA256 与两份原语言输入登记在 。该文件只包含代码使用的原元数据及来源，不保存验收结果；所有贴图载荷复用已登记文件，提取器逐项核对原像素和文件哈希，不写入新素材。

评分及快照来源为 EquipCabinetPanel／EquipCabinetGridItem／EquipCabinetDetailPanel、PlayerEquipCabinetDataExt、EquipCabinetEvaluateInfoParser；属性行和规则来源为 PropGroupContent／EquipCabinetPropGroup／EquipCabinetPropRuleItem。品质文字按原 GridItem 对 TextColor 的隐式 Color 转换读取普通色，未擅自启用 GradientColor。全屏显示读取原 AstrologicalDestinyPanel 的 PopupWindow.mFullScreen，并参照 AutoHideBuiltInUIViaWindowManagerCounter，避免本地主城货币栏覆盖原总分。动态祝福／套装文字直接取原语言分包，属性枚举来自原 PropType.cs，GreenRainbow 四角颜色来自原 TMP 预设并保留对象及 SHA256。没有新增自绘、替代布局或新版来源；原位图、素材 ZIP 和锁文件的内容保持不变。


### 商城原组件字段与引用片段（854_156383）

`scripts/prepare_commerce_bindings.py`只读固定APK及已校验同版本缓存，沿现有commerce-ui的21个原prefab取得PopupWindow.mShowClip／mHideClip／mLoopClip、Animation默认播放引用，以及PayPeriodicCardItem、CommonPayBtn、FirstPayDialog／AwardItem／PreviewAppearance的序列化字段。沿ExtraPropItem实际指针取得PayPeriodicCardPropItem／PlayerPropItem原模板，复用已登记官方贴图，严格核对原Sprite解码输出，不新增位图或修改共享素材目录。

原片段、组件对象及相关客户端C#／PopupWindow／RotateAnimation代码的SHA256、原语言分包输入与模板来源内嵌于；21项片段引用包括FirstPayDialog_AniShow两条2793键的原四元数曲线，保留原时间、值和出入切线，仅接受原非加权曲线。生成器`--check`只读重建并比较代码元数据；该文件属于运行代码输入，不包含验收成绩。原图、资源包来源及哈希继续沿用commerce-ui、现有UI／物品素材各来源清单；本轮没有引入新版资源或自绘补充。具体运行边界与剩余未迁移项只记于对应功能／地图验收文档。

### 固定版副本评级、奖励说明与 NPC 邀请窗口

`scripts/prepare_dungeon_bindings.py` 从固定 manifest `854_156383` 的 `prefabs_ui_dailydungeon`、`prefabs_ui_demonbattlefield`、`prefabs_ui_mopup`、`prefabs_ui_npcadventure`、`prefabs_ui_team` 及 `prefabs_ui_common` 读取原组件字段、RectTransform、Grid／LayoutGroup、StateSwitcher 和嵌入的规则行；原 `rule_tip.g.bytes` 与语言包提供说明与品质／难度／排序名称。生成原始配置数据，未重新设计窗口。

此前未转换的四张原 Sprite 独立解码到 `assets/official/dungeon-ui/`。对象 CAB、bundle／APK或原 CDN 凭据、对象名、输入及输出 SHA256、原 prefab 和配置输入记录在 `assets/reference/dungeon-ui/source-manifest.json`；其余图像复用已转换的同一原对象。来源不包含新版同名包或自绘素材。官方规则和 NPC 战力公式分别来自 DailyDungeonTipInfoDialogItem、RewardRuleInfoParser、NpcFightData、BattleFormulaHandler 等原代码；用户补充的离线规则另见对应功能／地图验收文档。


### 活动签到缺项与原动态展示（854_156383）

`scripts/prepare_activity_completion.py` 独立输出 `assets/official/activity-completion/`，来源清单为 `activity-completion/source-manifest.json`，Unity 显式贴图绑定尚待接入。输入均为固定 APK 与经过原版本清单校验的 CDN 依赖；保留原 prefab、组件 path_id、原骨骼／图集／初始动画、Skin、材质、RectTransform 和转换输出 SHA256。7 个静态角色／宠物 SkeletonGraphic 使用原初始动画逐帧转换，不自绘；火锅签到嵌入面板的 ImgStove 来自原 RawImage 指向的 Texture2D，保留原面板的初始可见状态。

中秋原 prefab 存在重复同名对象，改按原对象身份建立唯一内部路径；节点层级、顺序、坐标及奖励不变。官方通用 ActivitySignDialog1／ActivitySignDialogItem1 用于用户明确授权的两处缺失处理：史莱姆 216 的专有签到／入口 prefab 不在完整固定版清单；孤独摇滚 223 的专有签到 prefab 存在，但背景和角色引用为白色占位。通用面板隐藏无关 IP 的人物、标志和介绍，使用真实活动标题及原奖励；14 天奖励在原槽位范围内滚动。该处理属于授权替代，不称为专有面板还原。

1478、1487、35968、35986、35987 的精确图像路径不在固定版完整资源清单，已解析礼物／外观 Sprite 容器中也无相符对象；现存 185 个新版同名缺包记录不提供这些图标。保留原品质框、数量和真实名称，不画或借用其他物品图片。7 处玩家奖励换装预览仍未完成迁移，与这五项图像缺失分开记录；未将“尚未接入”登记为官方资源不存在。


### NPC 礼物、餐桌对白与原交互模板（854_156383）

scripts/prepare_official_partner_interactions.py 复用固定 NpcPanel／NpcPanelNpcItem／NpcDetailPanel 和礼物模板，沿 prefabs_ui_npc 的 NpcDialoguePanel、NpcDialoguePanelItem、NpcDialogueOptionBtn、NpcDialogueEmojiOptionBtn、NpcPanelDialogueItem 及 prefabs_ui_item 的 GotGrayObj 读取原层级、锚点、尺寸、LayoutGroup、TMP 排版、109 项组件字段、StateSwitcher、原 AnimationClip 和 Animator 触发映射。原 npc_dialogue.g.bytes 全表和两份官方中文语言分包提供真实对白、分支及界面文字；源码 SHA256、表／语言输入、36 项 Sprite 的原对象与 APK／同版本 CDN 来源、输出哈希在 assets/reference/partner-interactions/source-manifest.json。仅 5 张贴图为新增解码，其余复用已转换原素材；没有新版替换、自绘图或自编对白。


### 原战前站位区域与拖拽输入（854_156383）

scripts/prepare_dungeon_bindings.py 从 prefabs_ui_fight_changebattleposarea 的 ChangeBattlePosArea SpriteRenderer 直接提取 ImgChangeBattlePosArea，并保留原颜色、边界、PPU、DrawMode、TileMode 与 SortingOrder；从 prefabs_ecentity_fight_fightcharacter_3100 的 PrepareBattlePlayerDrag 提取 DownKeepTime 与 DragThreshold。新增 assets/official/dungeon-ui/ImgChangeBattlePosArea-41679de83e.png 为官方同版本 PNG 解码，无自绘或新版替换。输入原对象与源包 URL／哈希、输出 SHA256 归入 assets/reference/dungeon-ui/source-manifest.json；ChangeBattlePosHandler、PrepareBattlePlayerDrag／Base、FightComponent、GridGroundComponentInfo 和 Pos2d 代码 SHA256 由  保存。

### 2026-10-18 商城倒计时与支付状态字段

prepare_commerce_bindings.py 从固定 APK／匹配 CDN 的 854_156383 补取全部 38 个商业节点绑定，以及 RuleDialog.mTipInfoPrefab、PayInfoTableDialog.mDescPrefab/mRowItemPrefab 的真实引用模板；GlobalColors.asset 的 White 和 Light 主题 Red 直接读取配置。 保存对象、原始字节及源代码 SHA256；时间格式文本来自相同版本语言表，规则来自 PayPeriodicCardItem、ShowRemainTimeInfo、CommonPayBtn、CultureTimeSpanArg。AccumulatedPayAwardItem 嵌套枚举读取使用原程序集 StateSwitcher<T> 的序列化字段布局，按原类声明顺序拼接，完整对象及托管引用字节仍严格校验。此轮为原有资源与程序集字段解码，没有自绘或新版替换。


### 每日／每周挑战原界面与首杀素材（854_156383）

`prepare_challenge_ui.py` 经 `official_apk.Apk` 从固定 APK 与同版本已校验 CDN 读取 SecretArea、JointStrike、VoidFissure、DemonInvasion 四个原 UI 包，转换67个 prefab； 保存原层级、锚点、尺寸、缩放、布局组、裁切、状态、组件字段、文本及脚本哈希。来源完整保存在 `challenge-modes/source-manifest.json`，含312项实际使用资源的输入／输出 SHA256、bundle 版本／URL／哈希和21项圣兽技能图标与原 ItemInfo.RawIcon 的对应关系。62张独立 PNG 保存在 `assets/official/challenge-ui/sprites/`；其余图片复用字节一致的既有官方转换图，不覆盖或重绘原图。

圣兽七种 StyleSetting 直接读取原 SecretAreaSettings：首杀列表与详情图片、规则预览图片、原模型缩放／偏移；怪物技能直接使用原 phase 表 ClientShowSkills 和实际同名 Sprite。图标收集目录可能含 lCommon_NoAtlas，按精确图标名在对应官方图标族中唯一定位，缺失或歧义即报错。首杀 NPC 的 FigureModelUI 复用已转换的官方伙伴模型，角色使用原角色网格。原语言资源按 DateTimeFormat 显示中国时区的击破日期。

圣兽／双影主页面按原 TopBarHandler 的空货币列表清除主城余额栏，保持原首杀按钮位置。原馈赠页使用 AchievementPanel／Base／Item 的五行槽位、原状态、文本、奖励及滚动；专用规则页使用 SecretAreaRuleDialog／JointStrikeRuleDialog 与 RuleTipNumer 原编号，不再以通用规则窗口替代。轮换预告及魔物详情仍待接线，不显示 prefab 示例值。

用户授权的单机调整：首杀记录本人当期首次击破；原隐藏的主页面邀请按钮显示后进入原 NPC 选择页；首杀说明同步为本人／伙伴规则，移除仅真人及同位面限制。主规则中的组队说明同步改为邀请伙伴，首杀说明同步为本人首杀；好友额外金币说明随已排除的在线社交功能隐藏。文字调整记录在运行控制器中，不改原表、prefab 或来源数据。四玩法主入口与其余交互仍在迁移；提取及转换完成不表示四套玩法已接入完毕。


### 家园与祈愿商店原组件（854_156383）

scripts/prepare_shop_bindings.py 从固定 APK 及通过原清单校验的同版本 CDN 读取 HomeShopPanel、GachaPanel、ShopGoodsItem、Common／ActiveSkillPiece／PassiveSkillPiece 信息组件、ShopGoodsItemSetSkill、MainTypeTitle、SkillPieceShopDetailDialog、ShopBuyBatchSettingDialog 和 ConfirmDialog 共 11 份真实 prefab；保存 RectTransform、布局组、内嵌覆盖、组件 PPtr、StateSwitcher、AnimationClip 曲线及 ListItemShowSequence 间隔。 保存 8246 个原对象来源、客户端与 LoopScrollRect.Runtime.dll／文本处理代码 SHA256、中文语言分包 SHA256，以及 112 张原图与三类原音效的显式本地加载哈希；已有图像按原对象标识或逐像素一致性复用。

新增 assets/official/shop-components/sprites/ImgBg-c72a37564e.png 为 ui_common 原 118×100 ImgBg 的 PNG 解码，源包 56625bf3a050a757882ec6e93d9fdeaa、对象 -4164920900724345309，输出 SHA256 e6621c90d2cd1dc2ae709253c29386e45d1ee65bcf7218323eea5bd7e5398f1f；assets/official/shop-components/audio/228187985.ogg 沿原 ShopPanel.mRefreshSuccSound 的 Shop_Refresh 事件 12446005，从对应原 UI 音频包解码。购买／失败音效核对原 Wwise 事件 ID 后复用 pet-exchange 的已转换媒体。原 URL、源包／原对象、输入与输出哈希见 assets/reference/shop-components/source-manifest.json 和绑定 sources／audio／files，没有新版替代或自绘内容。

馈赠奖励展示沿原 def_award_handler.g.bytes 的 SourceOperate.Achievement=18、AwardStyle.Popup=2，映射原 AwardPanel；技能商店 4／5 则沿各自 ShopGoodsInfo.AwardDialogType=1。该原表字节对象及 DefAwardHandler、DefAwardHandlerInfo、BuyFromShopAwardHandler、ShopBuyIntent、ItemDetailDialogAddition 原码哈希纳入绑定，复用已有家园奖励窗口的真实 prefab、素材和动画。用户确认的界面调整与单机结算边界记录在对应功能／地图验收文档，不标为官方服务端源码。

商店顶部货币复用 ui.json 中原 PublicTopBar（对象 -8955117352938210115）与 MoneyUI（对象 -8145708160856884442），均来自原 public 包 142e02f2b39cf67da8e4bb0e4c17167b；保留 ResourceList 和 MoneyUI／DescBtn 的布局组及原字体、缩放。显示沿 MePlayerItemUI.UpdateCount 的 CultureNumberArg.Compact，MePlayerItemUI／TopBarItem／TopBarManager 原码 SHA256 纳入商店绑定。该部分没有新增图片，替换旧商店顶部的手动格宽和原始整数显示。

### 2026-10-18 商城原动态布局及规则模板

prepare_commerce_bindings.py 追加固定854_156383的22个原prefab（既有21个commerce加PayDirectPayItem）完整UI定义、原LayoutElementClamper与AdaptiveHorizontalLayoutGroup字段，保存原组件字节SHA256及原类源码SHA256。92个业务／文本效果／页签／计时节点按原程序集严格解码，规则／价格行沿真实序列化指针取得并重定根路径；模板图片复用已校验的原PNG，不新绘图片。运行时恢复原Grid、字体尺寸、奖励排列与原MoneyCostInfoParser价格列表规则、未修改APK或预期哈希。


## 伙伴列表、相遇通知、关系称谓与好感馈赠

固定 manifest 854_156383 的 NpcPanel、NpcPanelNpcItem、NpcFriendshipLevelUpNotifyDialog／Item、NpcDetailPanel 中的 AchievementPanel，以及原 InputDialog 通过 prepare_official_partner_interactions.py 读取真实层级、RectTransform、TMP 排版、StateSwitcher／TabObjectTransition／Animator 参数和完整组件字段后迁移。关系背景通过固定 APK manifest 的真实地址解析 Level_1～4 对应 Sprite，不按文件名猜测包。完整来源见 partner-interactions/source-manifest.json：49 份原码哈希、4,454 个对象输入、213 条组件绑定与 100 项图片／音频输出，多数为既有资源复用；来源清单中的 resources／inputs／outputs 保存真实 bundle、对象及输入输出哈希。

NpcPanelNpcItem.MeetSound 指向 NpcAffinity_Get（事件 723930271），经固定版 UI Wwise 包的 Event→Action→Sound／媒体 368085030 转成 OGG；输入媒体、原分包和输出均有独立哈希。新增素材保留在 partner-interactions/，公共 Sprite 复用现有输出；原 APK／校验 CDN 来源不混为新版补包，无新绘制补充。中文对白继续使用同目录 dialogues.json 和原语言包文本。用户经验补证及运行边界记在对应功能／地图验收文档，不记为取得服务端实现。

### 采集角色原动作与工作工具（854_156383）

来源链：MopUpPerformer.mApeendSkins → AppearanceAddressUtils.GetPlayerPartToolPrefabPath → 原 Assets/Prefabs/Appearance/PlayerPart/workTool/M_gaoZi_R_01.prefab；AppearanceEffectPart／SkeletonGraphicFollower 指向 pWeapon_R2，按 W_jian_R/W_jian_R 的 Map_GaoZi 附件决定显隐。保留原 -90° 子变换、局部缩放和层序。原 workTools.skel／atlas 位于 character_worktools.bundle，真实 M_gaoZi.png 由 character_worktools_default.bundle 提取；没有将工具名当作角色皮肤或自行绘制工具。

prepare_gathering_actions.py 从三类材料原 SkeletonAnimReferenceAsset 取得 21 个角色动作，沿男女原 SkeletonData 的拆分动画引用取回，复用官方职业武器的跟随网格转换器按 24 fps 转换。assets/official/gathering/ 保存男女动作及工具贴图；assets/reference/dungeon-ui/gathering-actions-source-manifest.json 登记原 CDN URL、bundle／对象、脚本与输入／输出 SHA256。原皮肤、外观部件、UV、颜色、网格和附件显示条件保持。运行时先接入各材料真实待机与工具；攻击／开箱动作的队列和转场接线另行完成，不把已转换动作等同于全部表现接通。

### 商城组件、星眷奖励与入口原素材（2026-10-18）

`scripts/prepare_commerce_components.py` 按固定 manifest `854_156383` 读取已校验 APK／原版 CDN，复用完全相同的既有 Sprite，新增像素仅写入 `assets/official/commerce-components/`。`commerce-components/source-manifest.json` 记录 26 个 prefab 所用 172 个纹理资源、原对象及包版本／URL／大小／哈希、代码与语言输入；其中 23 个 PNG 位于独立目录，其余复用共享原素材。没有自绘或新版替代。

累计奖励运行时采用 `AccumulatedPayAwardShowConfig` 中原品质背景、锁定图片和缩放、颜色，原 UIMesh 的 MeshRenderer 材质、网格坐标／UV／三角形／顶点色与纹理平移参数；`RotateAnimation` 保留序列化速度；CoreMulti 使用原片段与 SwitchItem 事件。列表方向依据取得的 `LoopScrollRect.Runtime.dll` 反编译代码和该 prefab 的 `reverseDirection=true`、`reverseIndex=true`，不按截图猜测。新增入口原图先转换并登记，接入状态以验收全流程记录为准。


### 世界地图命运果实探寻（854_156383）

`prepare_world_explore.py` 从固定 APK／校验原版 CDN 的 `prefabs_ui_map_world`、`prefabs_ui_map_explore`、`prefabs_ecentity_mapworld_actionworld_actionworldexplorer` 读取原 WorldExploreBtn、八份探寻UI层级、序列化控件绑定及动作22002／22003／22041／22042。未自绘图片、重排确认窗或设计概率卡片。20个实体组图标及六种令牌图标按原Icon／RawIcon地址取得，重复素材复用现有输出。

独立输出 `assets/official/world-explore/`；`assets/reference/world-explore/source-manifest.json` 登记原bundle／对象／URL、版本、代码与配置输入SHA256、复用纹理及全部输出SHA256。规则使用 explore_token_group、explore_entity_group、map_explore_limit、rule_tip 及既有固定版 adventure／map-entities／rules 转换。原语言包提供地区、实体、规则、时间格式等文本，RuleTipNumer和TextReplacer按原代码处理编号及参数。

Map_Action_PeachExplore_1／2／3沿原map_sfx_01的Wwise Event→Action→Sound取得内嵌DIDX／DATA媒体53374149、990872803、88127107，经vgmstream解码再转换OGG。清单保留原分包凭据、媒体输入哈希、偏移处理和输出哈希。运行时使用显式加载绑定，无网络获取。服务端RPC缺失、用户批准的本地结算裁决与尚未确认的引导礼包触发，分别写入对应功能／地图验收文档，不冒称官方服务端实现。

### 商城原入口及规则绑定补齐（2026-10-18）

commerce-components/source-manifest.json 进一步覆盖 28 个 prefab、173 个实际纹理资源，包含 ActivityEntryBtn、ActivityEntryItemWrapper、每日／每周入口与 FirstPayIconEntry 的原物件和图片；新增图片仅写入独立目录。原 HomeUIActivityEntryController.mEntryRoot、home_right_entry_ui_activity 原表哈希、ActivityEntryOverview 的 LoopVerticalScrollRect、ShopActivityEntryPanel／PayDirectPay 状态与 RuleBtn.mRuleKey 直接迁移。来源保持固定 APK／原版 CDN manifest 854_156383，没有自绘或新版替代。

### 首购原人物、中文图片及粒子（2026-10-18）

prepare_commerce_first_preview.py 只写 assets/official/commerce-components/first-preview；first-preview-source-manifest.json 记录固定854_156383原包／对象输入哈希、原 FirstPayPreviewAppearance／FirstPayDialog 代码、外观与皮肤表、既有职业转换来源哈希及所有输出SHA256。原男女100/show_01、200/show_01四段动作、两种原首购主手皮肤、原Follower矩阵、原骨骼网格／UV／染色，以及原UIParticle／ParticleSystem／材质纹理按现有开发期转换链迁移；37个实际纹理绑定、68条显式运行资源，未绘图、未生成替代角色、未引入UI Spine运行时扩展。

commerce-components/source-manifest.json 扩展至28 prefab、177纹理；四张FirstPay中文图片通过MultiLanguageImage原Key与LanguageImagePathUtils原zh_CN／default路径顺序取得，CustomScaleByLanguageType与语言分组控制器使用原配置。运行时状态、核验和仍未迁移的部分见对应功能／地图验收文档。

### 商城 TopBarHandler 原挂载（2026-10-18）

commerce-components/source-manifest.json 补录 TopBarItem、TopBarManager、MePlayerItemUI、PlayerItemUIGetItemIdFactory 原代码输入哈希；沿固定 APK／854_156383 原 PublicTopBar、MoneyUI 及各页面 TopBarHandler 配置复用已有纹理。未添加手绘资源或自行排列货币栏。


### 圣兽／双影战斗结果组件绑定（854_156383）

`prepare_challenge_results.py` 严格读取固定版本两个原战斗结果 prefab、成员结算行及 PopupWindow，共24个 MonoBehaviour；连同开场 AnimationClip、原 UIMesh／MeshRenderer／Mesh／Material／Shader 共36项输入，原对象来源、bundle 凭据及输入 SHA256 嵌入 ，所有节点引用均核对既有 `map-ui.json`。保留原 BlurMask、开场曲线、伤害／波数／历史峰值文本、本人／NPC 原模型槽及离开按钮；战斗数值与紧凑数值沿原 CultureNumberArg 使用不同格式，相关格式化代码哈希另存 `format_code_sha256`。

底部原121顶点／200三角形网格按原 RectTransform 的三维旋转及三个缩放分量投影，材质 CommonJieSuanDown／UI/EffectDistort 沿已迁移原 GLES 着色程序、纹理坐标、混合与时间参数。唯一新增 PNG 为原 Mask_065_tex 的 Texture2D 解码，位于 `assets/official/challenge-results/`，输入／输出哈希和原始包凭据在 `assets/reference/challenge-results/source-manifest.json`，纳入显式加载与独立素材包；未自绘背景或替换原布局。

成员伤害服务端写入未取得，统计口径待用户意。动态圣兽 MonsterIcon 已定位到原对应 prefab，仍待适配。当前隔离结果页隐藏未接通内容，不用占位数值或其他模型代替，正式战斗区域接线另行完成。

### 首购武器详情及比较原窗口（2026-10-18）

commerce-components/source-manifest.json 新增原 CharacterPanelEquipComparePanel 与 ItemDetail/EquipDetailDialog，累计30 prefab、218纹理，按固定APK／854_156383来源解码。补录 FirstPayWeaponPreview 原代码输入哈希；物品样本使用已迁移的原 EquipInfo.GenerateItemParams 规则和零索引／零所有者缓存语义，未绘制或设计替代窗口。


### 阶梯与羁绊原布局字段补齐（854_156383）

固定APK与匹配CDN的prefabs_ui_demonbattlefield、prefabs_ui_npcadventure、npcadventuremap_map_11／12／13／14／17及npcadventuregeography原包，使用原RectTransform、LoopVerticalScrollRect、Slider尾字段、StateSwitcher引用、原AnimationClip、PlayerInfoUI／PlayerIcon本人模型绑定。prepare_dungeon_bindings.py记录各对象输入SHA256、实际包URL／MD5与语言文件哈希、素材输入清单并入assets/reference/dungeon-ui/source-manifest.json。复用既有667项原纹理，无自绘／拼图替代。NpcAdventureSettings.ConfigData保留原地形旋转6及快进参数；旋转实现来源为official-code/launch-source/RotateAnimation.cs。地域19未使用另国图替代，原／新版清单检索结果和处置边界见对应功能／地图验收文档。

### 代金券确认与兑换原组件（2026-10-18）

commerce-components/source-manifest.json 新增 ConfirmDialog/MarkableConfirmDialog，累计31 prefab、218纹理；补录 WeeklyPayGiftShowContext、DailyPayGiftItem、PayGiftIconUtils、PayVoucherUtils 原代码输入。Toggle 原对象344字节布局严格校验后解析原graphic／transition／isOn，原 UIToggle 与 MarkableConfirmDialog 调用链决定交互；其底层0.1秒graphic渐变与Unity官方[UGUI Toggle实现](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/Toggle.cs)核对，未用新版资源替换原包。现金SDK替换为本地原窗口确认属于用户批准的离线适配，其余确认／兑换控件、文案、图和规则沿固定854_156383原内容。


### 圣兽结果原 MonsterIcon 六种骨骼绑定（854_156383）

结果页按 GameEndPanelBoss.RefreshBoss 读取原 StageInfo.Monsters[0] 和 MonsterType.Normal=1 选择原 BossSlot／NormalSlot，不使用奖励计算的 GetMainMonster 替代。3703、3704、3705、3707、3708、3709 的 MonsterIcon 与已转换 MonsterUI 逐一核对 SkeletonDataAsset PPtr、皮肤、初始动画及网格哈希，复用同一原骨骼，保留各自0.42／0.44缩放、原枢轴和 SkeletonGraphic 参数。UIUtils.KeepScale=11 在实例化时清零位置与旋转，避免误沿编辑器预览位移。原 prefab／RectTransform／SkeletonGraphic 输入凭据及 UIUtils 代码哈希嵌入结果绑定的 icon_sources／icon_code_sha256；无新增图片或素材包变更。

3706 为原 TempRTUI，已取得相机正交尺寸3.5、640×512 RT、原目标位置／缩放和带粒子的 MonsterModel_3706_RT；该独立渲染链待迁移，保留 pending_icons 元数据，不用其他骨骼或自制图片代替。

### 商城原Wwise声音与BGM（2026-10-18）

commerce-components/audio-source-manifest.json 登记固定854_156383的minimum／UI声音与BGM原包MD5、SHA256、原Wwise引用／事件对象和ButtonSound／UIClickSound／PopupWindowSound／PopupWindowBgmController／BgmManager原代码输入；运行目录 audio/catalog.json 保存30个原prefab声音链接及16个事件动作，10条原声音经wwiser／vgmstream转换Vorbis，保留原源音量、剪辑、循环。规范Ogg容器序号及校验和仅消除编码器随机元数据，实际输出哈希逐项复建核验。不存在额外设计的商城BGM或替代首购音效。通用原ConfirmDialog加入后，commerce-components/source-manifest.json覆盖32 prefab、218纹理。


### 商城窗口与控件状态动画（2026-10-18）

commerce-components 来源清单补齐固定854_156383的 Selectable 原 ColorBlock／SpriteState／触发器、Btn_Base_Click／ToggleAnimatorController 状态机与实际覆盖Clip、PopupWindow 三类动作引用、CanvasGroup及 MainCanvas 的 Mask／BlurMask。原 PayDirectPayItem 独立纳入转换，当前33 prefab、222纹理。commerce_animation.py 只接受原单片段状态及 WriteDefaultValues 分支；保留原触发器、转换时长和曲线，不自制替代动作。输入输出哈希统一在 assets/reference/commerce-components/source-manifest.json；此处仅登记来源，运行结果在对应功能／地图验收文档。


### 商城确认文字原自动对齐（2026-10-18）

沿固定854_156383的 AutoAlignForSingleMultiLineText.AutoAlign 与 ConfirmDialog.OnContentTextRefresh 迁移文字布局完成后的单／多行判定、原514／513对齐值和提示同步；原两类确认 prefab 的字段由 commerce-components/source-manifest.json 登记，补入两份实际C#输入哈希。富文本沿相同布局判定，不用强制居中标签覆盖原多行左对齐。此项不代表 TMPEffect 双描边／阴影已还原；shader 原 GLSL 已从 defaultpackage_unityshaders.bundle 提取为开发输入，受保护原生方法的字段到uniform映射仍未恢复，未自拟百分比换算或阴影公式。


### 商城素材包与显式加载绑定（2026-10-18）

商城组件、首购原show_01开发期转换、原Wwise音频的三份来源清单和运行资源补入37a8f1db943eb3cb素材包；以2adebc529c12e803已提交包为基准，仅追加本会话91个文件，601项相关文件／导入描述对照来源及目录核对，其余原包内容保持逐项一致。全局显式绑定补入53项，合并三组catalog后的302条运行资源均有绑定；完整包与清单哈希见现行lock及全流程文档。素材包的创建不表示TMPEffect参数映射及原越界事件已解决。


### 商城声音的正式包加载（2026-10-18）

Wwise转换输入及十份原音频内容保持不变，运行时改走 official_resources 显式导入资源加载后复制并设置原循环参数；不再依赖发布目录不存在的原始Ogg文件。正式分包中302条商城资源和十份音频均直接挂载、读取／解码通过，未增自绘或替换音频，素材包仍为37a8f1db943eb3cb。

### 商城滚动与原输入阈值（854_156383）

prepare_commerce_components.py 与 commerce_scroll.py 从固定 prefab 转换九处 ScrollRect／LoopVerticalScrollRect／LoopVerticalScrollRectMulti 的原序列化指针及数值；EventSystemManagerConfig → EventSystemProxy 对应 public bundle 142e02f2b39cf67da8e4bb0e4c17167b，EventSystem path_id 5730249452741529571，dragThreshold=5。原输入对象、配置及 EventSystemManager／Config、ClampingScrollRect、已反编译 LoopScrollRectBase／LoopScrollRect 的输入哈希追加在 commerce-components/source-manifest.json；Multi 继承 LoopScrollRectBase 与固定 metadata 对齐。资源像素和302条动态加载路径未新增，素材包只替换该来源清单；lock.json 指向 ca29c4a721b9fc8a，完整SHA256 ca29c4a721b9fc8abf595bc03c911c5161979c3127ad1dc9ce6792cea9acec52，其余基包内容逐项保留。运行验证与字体／动画时序边界仅记对应功能／地图验收文档。

### 固定 APK 原生 IL2CPP 代码恢复输入

recover_official_native.py 仅读取固定 zjcs_M509085.apk 中 arm64-v8a/libil2cpp.so 的原字节（346091520 字节，SHA256 0c3cef25cfa9f079da6344825aae5a52e5eb025e0cc9ece737ec79a94cdfa544），与完整 APK ZIP 条目逐字节哈希对应；metadata 使用同 APK 的 assets/bin/Data/Managed/Metadata/global-metadata.dat（45312608 字节，SHA256 58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4）。工具在开发环境 Unicorn 2.1.4 中执行原 AArch64 初始化、解密及解压指令，按原构造顺序和重定位表恢复静态代码／数据，在 0x206f73c8 的 Android 初始化前边界停止；未知导入停止，不修改原可执行指令。应用原加载器 0x206f6968 的 R_AARCH64_RELATIVE 规则，外部导入不冒充已恢复实现。缓存为 .tools/source-packages/commerce-native/libil2cpp-restored.bin，317298920 字节，SHA256 8e49906e085ba3819c4b47fd3bd12dcdd594ff0fcc75d418a87ac35a4566305b；该文件是带原保护头的虚拟地址映像，不是可运行 ELF，也不进入游戏资源包。

read_official_native.py 沿固定 v31 metadata 的 image／type／method token 及恢复后的 CodeGenModule.methodPointers 定位真实方法，输出原指令及可核对的托管调用名称。Unity.TextMeshPro.dll 模块位于 0x11efc650，1694 个方法指针；TMProWarp.Refresh 为 metadata_index 296528／token 0x0600000d／VA 0x1121e07c，TMPro.TMPEffect.DoSetup 为 296788／0x06000111／0x1122f780，其材质回调为 296792／0x06000115／0x1123083c。此处来源指真实原生方法，与此前取得的另一组件 TMProWarpEx 区分；运行时迁移和验证结果只记对应功能／地图验收文档。

### 商城原生文字材质与曲线

`commerce_typography.py` 读取固定 manifest 854_156383 的 `defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle`（bf62732f37a5a423ddbac02681e054a4）中 `FZY4K SDF - dynamic.asset`（-1054575886599457342）、材质8668477128756491714、`MultiLanguageFontConfig.json`（-6461299786733695581）。pointSize40、atlasPadding4、GradientScale5均由原对象核验；中文版字体替换调用来自原 `MultiLanguageText.RefreshFont`。原字体仍为已有 `assets/official/FZY4K.ttf`，未写入新字体或替代贴图。

运行时 ／ 依据已恢复的原 `TMProWarp.Refresh` 0x1121e07c、`TMPEffect.DoSetup` 0x1122f780、材质回调0x1123083c及 `ShaderUtilities.UpdateShaderRatios` 0x1126eb34；shader来自固定 `defaultpackage_unityshaders.bundle`（cac97355bb4799c415e3cb1ef38b6dc5）的 `Hidden/JSZX/TextMeshPro`（-4286560471751938018）GLES3程序。原规则／参数不作视觉调参。三份原Warp序列化曲线及字体／材质输入哈希纳入 `assets/reference/commerce-components/source-manifest.json`。

### 星眷循环片段的原引擎时间范围

固定 APK arm64-v8a/libunity.so 原条目22360168字节，SHA256 5142b00b7ef6fa53828de13afd2cfd07a6a7763100d145cbb50a22e0114a0340，缓存 .tools/source-packages/official-code/libunity.so。read_official_native.py --engine 根据原 AArch64 icall 注册指令定位 AnimationClip::get_length 0x5755e0、时间范围0xf9c434／0xf9e900及 GetEventsInternal 0xa2bc84；commerce_clip_time.py 按原 Legacy 曲线／事件范围转换。原 AccumulatedPayCoreMulltiAwardGo_Loop 来自 defaultpackage_share_b2696e917c258280310268fe4e4132b1.bundle（MD5 71707dca67f83ef62156a5fe28e0bf81），对象6352346077181223357，Legacy／wrap2／sampleRate60，曲线末尾1.6666666269302368、SwitchItem事件1.8333333730697632；原引擎将事件计入长度。引擎和测试夹具仅作开发输入，不进入运行资源包；来源清单登记真实引擎SHA与方法链。


### 商城原字体样式与自定义 midsize 标签

固定 FZY4K SDF - dynamic.asset 的原序列化值为 m_ItalicStyle=15、FaceInfo.capLine=38.040000915527344、pointSize=40；15为斜切参数，原生成网格指令乘0.01使用，不作角度换算。34个迁移 prefab 中九处非零字体样式（五斜体、四下划线，包含原隐藏模板）来自各自原 TMP 序列化字段，权重均为400。GenerateTextMesh 0x11218a90–0x11218b4c 和原 GlyphMetrics getters 0x11443a8c／0x11443a7c 提供上下边斜切及半差平移公式；运行时对原字体四顶点应用同样计算。

原日礼包 ui_daily_pay_gift_buy_all_discount 使用 <midsize=42>；固定客户端 TMP_Text.ValidateHtmlTag 0x1125a540 的分支0x1125e08c、字号／栈处理0x1125f454、偏移0x1125f4e4和关闭恢复0x1125e64c已取得。基线偏移按 (基础字号−当前字号)×0.5×capLine／pointSize，并沿原正交开关保留缩放分支；GenerateTextMesh 0x11217e70／0x11218134实际读取此偏移。动态格式化保留原标签。以上方法均来自已登记固定 APK 恢复映像，read_official_native.py 读取时另校验该映像SHA256。

辅助阅读使用 Unity 官方 com.unity.textmeshpro 3.0.6 公开源码，下载URL https://download.packages.unity.com/com.unity.textmeshpro/-/com.unity.textmeshpro-3.0.6.tgz，SHA256 1ce172027b906a30be33cefe7b2ee46e1c8d35f729359b8b9785fc120d57b637；只用于定位原生结构，不代表固定客户端版本或其 midsize 自定义方法来源。

### 幻兽改名颜色预设

`scripts/prepare_pet_naming.py --gradient-only` 从固定 APK 的 `PetInfoContent.mPetNameController.mPetName` 定位实际 TMP 组件及其 `PetName` TMP_ColorGradient，22 处引用使用同一原预设。四角颜色和组件／预设输入哈希记录在 `pet-naming/source-manifest.json`，运行数据为 `assets/official/pet-naming.json.name_gradient`； 按原 `PetNameController.Refresh` 仅对自定义名称启用逐字四角渐变，未自绘颜色。

### 幻兽百连确认与页面动画

`prepare_pet_gacha_performance.py --panel-clips-only` 重新转换固定 APK 的 `PetGachaSubPanel_Default／Show／Hide`，包括 `CanvasGroup.m_BlocksRaycasts`，同步独立与嵌入主页面两份动画；原完整 Transform 层级核对了共享 Hide 的两条无目标绑定，保留原未绑定行为。原文 `ui_pet_gacha_is_times_hundred`、`ui_no_prompts_today` 来自同版本中文分包；确认窗直接复用原 `MarkableConfirmDialog`，界面来源仍由 commerce-components 清单登记，结缘动画、原文与 `GachaPanelUtils`／`MarkableConfirmDialog` 代码哈希在 `pet-gacha-performance/source-manifest.json`。

### 约尔德拉幼年列表图标来源复核

`prepare_pet_ui.py --recheck-portrait` 重新解析固定 APK 的五包完整资源路径，并从官方引导配置的当前目录 `202610141241870` 取得 `870_156522` 主清单及 `image_itemicon_pet`、`image_ui_pet` 两包，独立校验大小、MD5、SHA256 后实际解析 Sprite／Texture2D。原、新完整路径和新版两包均无 `ItemIcon/Pet/pet_1619`；来源 URL、版本、哈希、对象名在 `pet-ui/source-manifest.json.portrait_sources`，新版原始缓存单独保留在 `.tools/source-packages/cloud-research/pet-870_156522/`，未混入原版本缓存或作为运行素材使用。按用户确认保留无图状态，未生成替代图，9219原模型未更动。

### 果树活动产出加成

`prepare_output_additions.py` 按固定版本官方 Luban 构造函数解码 `output_addition`、`activity_output_addition`、`planes_merge_output_addition`，输出 `assets/official/output-additions.json`。原数据、构造函数、公共逻辑与消息代码及输出哈希见 `assets/reference/output-additions/source-manifest.json`。仅迁移已有加成记录及原公式，不生成活动实例；本地分段结算范围与用户确认见果树功能文档。

### 家园邮箱

`prepare_home_mail.py` 直接迁移固定版本 `prefabs_ui_mail` 的8个原窗口／条目／分页／信纸预制体，保留布局、原贴图、状态切换、正文、附件和动画绑定。猫头鹰使用原 `HomeMailDefault.NoMailAnim/HaveMailAnim` 两套骨骼动画；原输入对象、UI 布局、语言、代码与输出哈希见 `assets/reference/home-mail/source-manifest.json`。邮箱规则按 `PlayerMail`、`MailUtils`、`MailDialog` 与 `MailContentUiGiveItems` 接入本地存档，不生成运营或社交邮件。

## 古遗物扭蛋演出

原版 `854_156383` 的 `ChestGachaPanel`、`ChestGachaResultPanel`、`GachaponAwardGo`、各数量布局与 `ChestGachaTenRewardGo` 经 [prepare_relic_performance.py](../../scripts/prepare_relic_performance.py) 转换。Animator、SimpleAnimation、UIParticle、材质与 Wwise 音频均记录原对象及输出哈希。运行资源在 `assets/official/relic-performance/`，来源见 [relic-performance/source-manifest.json](relic-performance/source-manifest.json)。未使用新版同名包或自绘素材。

## 图鉴原版组件

图鉴列表、职业／国家分类、翻页及原技能详情由 [prepare_codex_components.py](../../scripts/prepare_codex_components.py) 从同版本 prefab 生成 ，共享图片逐字节核验后复用。技能超链接按官方 `SkillHyperLinkDataProvider`、词条及怪物导航调用链迁移；[prepare_codex_links.py](../../scripts/prepare_codex_links.py) 提取 119 个技能可达词条及49个属性说明词条、15 个召唤物窗口／模型及属性、技能图标和嵌套窗口，来源见 [codex-links/source-manifest.json](codex-links/source-manifest.json)。运行资源在 `assets/official/codex-links/`，模型同样在开发时转换为原生网格。上述目录纳入显式资源清单及独立素材包。

### 家园动物与落叶奖励

`scripts/prepare_home_random_rewards.py` 从固定 `854_156383` APK／匹配 CDN 读取 `Ground` 等8套地面上的 `HomeRandomReward`、11个动物／落叶预制体、`HomeSkinConfig` 与女神／售货机的 `HomeRandomRewardSlotData`。18套落叶图使用原 prefab／精灵。`home_random_reward_type`、`home_random_reward_group`、`home_random_reward_slot` 依原 C# 构造器解码，运行目录为 `assets/official/home-random-rewards/`，逐输入、输出及脚本 SHA256 见 `assets/reference/home-random-rewards/source-manifest.json`。刷新数量及离线补刷采用用户于2026-10-19确认的本地方案，规则区别在家园功能文档维护。

### 家园灯光与材质迁移

`prepare_home_materials.py` 从固定 `854_156383` APK／匹配 CDN 的实际家园主题、设施和主场景 Sky 读取 Light2D、ShadowCaster2D、HomeLightController、HomeMutexLightLayer、HomeMutex、HomeBloom、RenderExistingMesh，以及 Day_Common／Night_Common。保留原对象矩阵、逐排序层配置、灯光形状／cookie、FalloffLookupTexture、遮挡轮廓、颜色／强度／阴影曲线和灯光时段覆盖。包含原 Sprite-Lit 系列、Sprite-Bulb、Skeleton-Lit-Atten、Skeleton Lit、SkeletonEffect-2D、Spine-Bloom、HomeSpineOnlySnow、Tree-Bulb，云和物体积雪继续使用原独立端口并接入同层光照纹理。25个原 RenderExistingMesh 的引用及材质替换用于积雪／光晕覆盖网格，不自绘替代。

严格字段读取、原字节与输出 SHA256、24种实际着色程序、诺诺原材质、原生 `Spine.Extension.RenderExistingMesh.UpdateMaterials` 0x10967160 的 metadata 对应记录见 `home-materials/source-manifest.json`。辅助核对使用 Unity 官方 Graphics 仓库 2022.3/staging 的 URP Runtime/2D 六份源码，真实下载 URL 与各自 SHA256 在该清单的 urp_sources 中单独登记，不冒充固定客户端源码。

### 家园教学角色动作与流星

`prepare_home_player.py` 从固定主场景 HomeEnvironmentManagerMono 的实际引用取得 StoryWakeUp／Training 两张行为图、男女角色原分离动画 home/wakeUp_01／100/training_01、角色变换、HomePlayerSpineLightAtten、HomeRenderRuleAsset、主题锚点及 HomeMeteor 的时段／概率／间隔和原粒子模板。角色沿当前真实外观、职业与染色组装。运行素材为 `assets/official/home-player/`，输入输出、图集、生成脚本和原版本信息见 `home-player/source-manifest.json`。

流星主体来自原版854_156383；其一个原版缺包依赖使用经独立校验、同 CAB 的864_156496官方同名包，实际对象、包哈希及来源 URL 在 counterpart_dependencies 中登记。新版资源未混入原缓存，不新增自绘。教学只调用原实际挂载的行为图，不推测未挂载的自主动作。

### 家园原教学调用

／ 复用固定 `854_156383` 已完整转换的 `adventure.json` 中 RepaireFurniture2、GoHomeUnlockHeap、RepaireBed、BedSpeedUp、RepaireBaGuaLu、FirstMergeItem 六张图、原故事表和对白；原输入沿 `official-map/story-source-manifest.json` 追溯。原 OptionTalkDialog／TalkDialog 及立绘复用 `home-tutorial/source-manifest.json`，修复／成功通知和遮罩复用既有原控件，未新增替代素材。诺诺九张强制行为图、真实动作及原主场景 NpcBehaviour.DecisionGraph 空引用沿 `home-npc/source-manifest.json`；教学角色的两张实际动作图沿 `home-player/source-manifest.json`，没有添加未挂载的自主行为。

### 素材秘境原动作与切换过场

`prepare_gathering_motion.py` 从固定854_156383的 `prefabs_ui_mopup` 读取五个MopUpPanel动画的原事件，跟随49个实际使用的 `MopUpSkeletonGraphicMaterial.mSkeleton` 转换原待机、受击及消失动画。角色动作沿既有 `gathering-actions-source-manifest.json`，材质、工具及PlayerPos沿原绑定迁移；新增本地网格位于 `assets/official/gathering/targets/`。输入包、对象、原代码及输出哈希见 [gathering-motion-source-manifest.json](dungeon-ui/gathering-motion-source-manifest.json)，未使用自绘或近似动作。

`prepare_gathering_feedback.py` 接入同包的 `MopUpFloatingIconItem`、`MopUpFloatingIconItemCrit`、`MopUpFloatingIconItemQuick` 三套原奖励飘字预制体及原完成事件。普通／暴击数字复用已转换的 DamageNumber、CritDamageNumber 原位图字形和图集，另四张装饰贴图逐字节核验后复用现有资源；没有新增或自绘图片。原版本、URL、prefab、组件、曲线、字体对象、图集输出和原C#代码哈希在 的 `data.source` 中随生成绑定保存；对应位图字体的原提取来源继续见 `battle-hud/source-manifest.json`。

### 幻想阶梯自动推关界面

`prepare_tower_push.py` 从固定854_156383的 `prefabs_ui_fight_fightmainpanel_extmodel_fightdemonbattlefieldmainpanel`、`prefabs_ui_fight_gameendpaneldemonbattlefield`、`prefabs_ui_demonbattlefield` 和 `prefabs_ui_home` 迁移原自动按钮、胜负结算、后台结果及家园提醒。保留原层级、布局、状态切换、动画曲线、贴图和组件引用，文字从原语言二进制读取；本地运行资源位于 `assets/official/tower-push/`，原家园提醒复用既有主城贴图。包版本、实际URL、对象、语言和输入输出哈希见 [tower-push/source-manifest.json](tower-push/source-manifest.json)，未使用新版替代包或自绘素材。

幻想阶梯的滚动、楼层 prefab、粒子和状态数据来自固定版本 `854_156383` 的 `defaultpackage_assets_prefabs_ui_demonbattlefield.bundle` 与 `LoopScrollRectBase` 原组件；动作按原玩家 `SplitAnimationSO` 转换，Wwise 入／离场事件按原 prefab 引用补入本地战斗音频。对象及输入输出哈希分别见 `dungeon-ui/source-manifest.json`、`official-map/player-actions-source-manifest.json`、`character-genders/source-manifest.json` 和 `battle-audio/source-manifest.json`；重建入口为 `prepare_dungeon_bindings.py`、`prepare_map_player_actions.py`、`prepare_female_character.py` 与 `prepare_battle_audio.py --reuse-bindings --tower-only`。

### 混沌家园商人与首次对话

`prepare_weekly_merchant.py` 从固定 APK／匹配原版本 CDN 提取 `HomeNpcManagerConfig`、`npc_home_position`、主场景第0号生成点、`HomeNpcModel_8416`、`NpcModel_UI_8416` 和 `Npc_Portrait_8416`。保留原 ModelSlot 0.4 倍缩放、碰撞框、红点全局缩放、原骨骼／图集／body_8416 皮肤和 `npc_8416/idle_01` 动画，输出至 `assets/official/weekly-merchant/`。复用原 `DemonInavdeNpc` 四句剧情、8402／8416 立绘表情及 `activity_2_18`～`activity_2_21` 已转换语音。输入对象、资源及输出哈希登记于 `weekly-merchant/source-manifest.json`；商店从结算开始开放24小时为用户确认的单机调度，出场位置、首次对话和窗口内容来自原客户端。同一来源清单另登记原 `ItemIconAttachmentConfig` 类型7的 `ProbabilityTip` 预览标签，保留原 prefab 与 `ImgProbability` 贴图。

## 音乐节奏小游戏原演出

固定版 `854_156383` 的 `ActivityPickGameMainPanel`、`ActivityPickGameContentSceneEffect`、`AnimationSoundPlayer`、`PickButtonEffect` 和 `ButtonSound` 按原对象引用接入。`prepare_activity_game_assets.py` 转换三套原 NPC 骨骼、组合皮肤、材质、挂件及12个所需动画数据集和五张评级图；原 UI 层级、曲线及粒子复用已转换的 map-ui。来源、对象及输入／输出哈希见 [activity-games/source-manifest.json](activity-games/source-manifest.json)。

`prepare_activity_game_audio.py` 从原 prefab 中解析 Wwise 引用，按7个原版音频分包转换22个事件、26条音频，保留原容器随机、循环、增益与停止动作；完整对象依据、音频包来源和转换哈希分别见 [activity-games/audio/bindings-source-manifest.json](activity-games/audio/bindings-source-manifest.json) 与 [activity-games/audio/source-manifest.json](activity-games/audio/source-manifest.json)。未使用新版同名包或自绘素材。

## 三消小游戏原界面与演出

`prepare_match3_game.py` 从固定版 `854_156383` 提取菜狗、旅程庆典、春日出游及万圣节的原三消界面、关卡卡片、棋子、浮动分数、奖励已领取附件和对应活动内入口图，保留22套 prefab 的层级、布局、状态引用、动画曲线、UIParticle 和原中文规则；动态棋子及彩色棋子绑定原表的16条图片路径，八种奖励按原 `item` 表的 `RawIcon` 绑定图片。基础及万圣节布局复用同版本已转换的 map-ui，其余主题直接从原包转换；自适应奖励布局、原物品图标附件／文字效果及 `GlobalColors` 深色主题参数从原组件读取。输入对象、资源及输出哈希见 [match3-game/source-manifest.json](match3-game/source-manifest.json)，运行资源位于 `assets/official/match3-game/`。

`prepare_activity_game_audio.py --game match3` 从这些原 prefab 的 `ButtonSound`、`AnimationSoundPlayer` 和原动画事件读取13个事件，按5个固定版音频分包转换13条音频；保留原容器、增益、循环和停止动作。完整对象和音频来源、输入输出哈希见 [match3-game/audio/bindings-source-manifest.json](match3-game/audio/bindings-source-manifest.json)、[match3-game/audio/source-manifest.json](match3-game/audio/source-manifest.json)。结果奖励复用原 `AwardContent` 及既有原奖励卡片资源。未使用新版替代包或自绘素材。

## 拼图题图、原界面与音频

`prepare_jigsaw_game.py` 从固定854_156383 APK及匹配 CDN 转换七套已有主题界面及其共用行、卡片、星星、格子和六个实际入口，共37个 prefab；有配置和界面的六主题使用52幅原题图。图片保留 `JigsawGameContent.GenerateImage` 的原 Texture2D、`JigsawSuffixSettings` 中文标识图、归一化TRS及颜色，转换为本地PNG；没有自行绘制。状态、严格解析的RawImage、原EventSystem拖动阈值和GUID星星图保留原配置。其他主题未提供的模糊图请求对应默认隐藏且未被卡片状态启用的节点，逐项检查后不生成替代图。快速完成复用已转换的原 `ConfirmDialog/CostItemConfirmDialog.prefab`（`gacha-purchase.json`），补读原TMP换行／边距、自动对齐、默认语言键和数量／颜色配置。对象来源与输入／输出哈希见 [jigsaw-game/source-manifest.json](jigsaw-game/source-manifest.json)。`prepare_activity_game_audio.py --game jigsaw` 从三个原Wwise分包转换六个按钮／动画事件及六条音频，来源见 [jigsaw-game/audio/source-manifest.json](jigsaw-game/audio/source-manifest.json)。

史莱姆2161专用界面、Slime01～09题图与Money_3034在固定APK五包、新版864清单及原APK／已校验CDN相关容器内均未找到。27档奖励包含用于原兑换商品的4,950枚魔素结晶；按用户“若影响玩法与奖励则允许复用”的明确意。复用既有200（SimpleSunny）官方界面、入口外观和九张题图。缺失货币图使用既有原 `ItemIconDefault.FakeIcon` 文字占位，显示原物品名“魔素结晶”；没有绘制或伪造史莱姆素材。映射与缺项在  单独登记，2161的原玩法参数与奖励保持独立，相关源码及原配置仍按真实固定版本登记。

## 伐木场景、活动入口、角色动作与音频

`prepare_tree_felling.py` 从固定854_156383 APK及匹配 CDN 迁移 `FestivalMinigamePartyActivity` 的六个 UI prefab 及原圣诞活动入口、列表图标、任务面板和任务条目，共十个 UI prefab、`TreeFellingGameContent`、五个 `TreeSegment` 和 `Obstacles1`。保留原层级、坐标、缩放、深度、SpriteRenderer、背景适配、基座定位、动画曲线、Animator 条件和 UIParticle；138张原图片位于 `assets/official/tree-felling/sprites/`。男女角色从同版本原骨骼、图集及分离动画采样 `home/pugilism_idle`、三段 `home/pugilism_lianxu_*` 和 `map/failure_01`，原控制器驱动当前自定义身体与外观。原 UI 组件、表、语言、输入对象及输出哈希见 [tree-felling/source-manifest.json](tree-felling/source-manifest.json)。

`prepare_activity_game_audio.py --game tree` 按原 `ButtonSound`、`AnimationSoundPlayer`、动画事件和 `TreeFellingGameConfigSO.PlayerHitSound` 取得七个事件，从两个固定版 Wwise 分包转换七条音频，保留原容器、增益、循环及停止动作。完整来源与哈希见 [tree-felling/audio/bindings-source-manifest.json](tree-felling/audio/bindings-source-manifest.json)、[tree-felling/audio/source-manifest.json](tree-felling/audio/source-manifest.json)。未使用新版替代包或自绘素材；服务端每局轮数和等待时长写入器未取得；用户已明确同意单机按五组原参数各玩一轮，每轮11秒，提示按原动画播完继续。

### 签到原窗口动画与奖励换装

`prepare_activity_sign.py` 从固定854_156383签到 prefab 提取 `PopupWindow`、签到物品、状态切换、`ListItemShowSequence`、`PlayerAnimationAppearance` 和提示文字组件；复用既有原图片，另转换两张原品质框至 `assets/official/activity-sign/`。原开窗／循环／关窗曲线、`StartShow` 动画事件和职业皮肤组的输入哈希见 [activity-sign/source-manifest.json](activity-sign/source-manifest.json)。

`prepare_sign_appearance.py` 转换27种签到外观及绑定皮肤，7个原角色预览窗口使用男女原骨骼、完整分辨率图集、染色遮罩和24帧原待机动作，武器沿原 prefab 的骨骼／插槽／附件跟随。运行资源位于 `assets/characters/sign-preview/`；原脚本、组件、模型来源、复用输入及输出哈希见 [sign-preview/source-manifest.json](sign-preview/source-manifest.json)。未使用新版替代包或自绘素材。

### 祈愿材料的原快捷购买来源行

`prepare_gacha_item_sources.py` 从固定854_156383 `ItemDetailDialog` 的 `ItemSourceGo.ItemSourcePrefab` 转换实际引用的 `ItemSourceItem`，保留原布局、可用／不可用状态、箭头与背景贴图；来源枚举文案由同版本简中语言包提供，包括国度扭蛋获取与元素扭蛋获取。资源位于 `assets/official/gacha-item-sources/`，原组件、调用脚本、语言包哈希及输出哈希见 [gacha-item-sources/source-manifest.json](gacha-item-sources/source-manifest.json)。Unity 库存导入工具通过 `prepare_unity_inventory_assets.py` 校验并接入该来源行目录与三张图。

### 宾果与刮卡原布局、网格材质及音频

`prepare_bingo_activity.py` 和 `prepare_scratch_activity.py` 从固定854_156383转换原宾果六个、刮卡七个面板／单格／任务／详情／规则预制体，保留坐标、布局组、动画曲线与事件、状态切换和原奖励引用。原 `UIMesh` 几何、材质参数与 GLES 程序哈希分别登记；刮卡原19个图案同时保留 UI 裁切与网格所用完整透明纹理，按原 `ScratchCardGridItem.SetSprite` 使用。资源位于 `assets/official/bingo-activity/` 和 `assets/official/scratch-activity/`；原对象、脚本、配置／语言及输出哈希见 [bingo-activity/source-manifest.json](bingo-activity/source-manifest.json)、[scratch-activity/source-manifest.json](scratch-activity/source-manifest.json)。

`prepare_activity_card_audio.py` 沿原 `ButtonSound`、`AnimationSoundPlayer` 及动画事件转换宾果三个事件／三条音频、刮卡四个事件／四条音频，保留 Wwise 容器与增益。两模块的 `audio/bindings-source-manifest.json`、`audio/source-manifest.json` 登记原引用与输入输出哈希。未使用新版替代包或自绘素材；服务端图案排列未取得，已确认本地分布与三档预览的具体差异见[活动小游戏](../../docs/features/activity-minigames.md)。

## 泽之国原版场景、界面与小地图

固定 APK／匹配原版 CDN 清单 `854_156383`；`prepare_map13_rendering.py` 从 `MapGround_13_bare`、原水面控制器及材质程序转换地形、岸线遮罩、水桥和熔岩，保留原导航、放置及17个骨骼实例，去除对应重复静态网格。`prepare_map13_dialogs.py` 接入8块挑战碑文及海之宫遗迹／源水大社／黄泉阁的原文案、背景、奖励配置；`prepare_map13_minimap_ui.py` 转换 `MiniMap_13`、原全屏地图控件、102项标记配置、资源点板、玩家图标动画、原逐格迷雾、区域说明与边界 prefab。输入来源、对象、处理及输出哈希分别见 `official-map/materials-13-source-manifest.json`、`dialogs-13-source-manifest.json`、`minimap-13-source-manifest.json`；运行目录分别为 `assets/official/regions/13.*`、`map13-rendering.json`、`map13-dialogs.json`、`map13-minimap.json` 及清单所列贴图。无自绘替代。

`prepare_map13_lighting.py` 从固定 APK／校验 CDN 转换原 173 个 `Light2D`、106 个区域昼夜灯及 46 个 `MapLight`，按原 `MapEnvEffect` 状态和 `MapEnvData.FadeTime` 控制运行时；Water2 接收原排序层的场景灯光。运行数据为 `assets/official/map13-lighting.json` 及其原型衰减／Cookie 纹理，输入对象、原排序层、配置、输出哈希与阴影状态见 `official-map/lighting-13-source-manifest.json`。全图所有 `Light2D.shadow_enabled` 均为关闭，且没有 `ShadowCaster2D`；没有为原图增加投影阴影。

`prepare_map13_dialogs.py --task-bindings-only` 另从相同已校验中文语言包生成76项主线任务标题、描述、进度和目标区域文案； 保存语言输入及原 `TaskInfoParser`、`LanguageUtils`、`TaskListItem`、`PinnedTaskPanel`、`TaskDetailDialog`、`ClientWorldMapProgressCondition` 的SHA256，运行时由 按原任务条件格式化与绑定。

## 龙之国原版地图与小地图

地图 14 使用固定 APK／原版本 CDN 清单 `854_156383`。原始地形、导航、2,961 个实体放置、31 组地图分层和昼夜状态、地图粒子沿通用地图转换链运行；原 `MiniMap_14` 转换包含 102 条地图标记配置、原资源点板、玩家动画、区域说明、迷雾 TilePattern 和公开解锁格。转换入口为 `python -B scripts/prepare_map13_minimap_ui.py --map-id 14`；原始对象、依赖、客户端代码、中文语言表、贴图输入输出 SHA256 见 [minimap-14-source-manifest.json](official-map/minimap-14-source-manifest.json)，运行目录为 `assets/official/map14-minimap.json`、`assets/official/map14-minimap-sprites/`，Unity 显式加载绑定尚待接入。地形、分层和粒子输出分别登记于 `regions-source-manifest.json`、`layers-source-manifest.json` 与 `particles-source-manifest.json`；未使用新版替代包或自绘素材。

山之国小地图使用固定 `854_156383` 的 `MiniMap_12`、`MiniMapFullScreenPanel`、`MiniMapMaskedUI`、原筛选与三种标记弹窗 prefab，复用原图标并转换原角色三层动画、区域文字／强度、村庄高亮、虚线、迷雾 TilePattern、99 区公开视野数据及 `UI/Masked` 的灰度遮罩。原 `GridModule` 类型由严格字节解析恢复，未重绘图标或遮罩。区域通知保留 `MapNotifyUI` 的原图标、两秒动画曲线及队列；原动画中无法绑定到 prefab 层级的孤立 `mFillAmount` 轨道如实记录在清单，不据此生成控件。主地图迷雾沿原 `12.prefab` 的 `CloudController2`，保留速度5、两格外边界、原遮罩与云纹理、材质参数和 GLES 程序依据；转换入口为 `scripts/prepare_map12_minimap_ui.py`；原对象、APK／CDN 凭据、脚本及中文二进制哈希、输出哈希统一见 `assets/reference/official-map/minimap-12-source-manifest.json`，运行目录为 `assets/official/map12-minimap.json` 和 `assets/official/map12-minimap-sprites/`，Unity 独立加载绑定尚待接入。

`prepare_map12_dialogs.py --task-bindings-only` 从同一原版语言包及 `prefabs_ui_task` 提取山之国22个任务的89项文案和原状态控制器；`prefabs_ui_directionguider` 的两种方向控件、`prefabs_worldstageiconhead/6302_IconHead` 的层级及三个 `SkeletonGraphic` 参数直接转换为 Unity 可用格式。骨骼按相同 CAB／对象／皮肤身份复用已经转换的原网格和全部动画；未绘制头像或箭头。语言与官方调用代码的 SHA256 记录于来源清单。原 prefab／组件凭据及复用图片哈希见 的 `INPUTS`／`RESOURCE_SOURCES`。

`prepare_map12_stele_ui.py` 从固定版 `BattleSteleWorldStageDialog`、`WorldDialogEntityContent`、`6650_DialogIcon`、`6655_DialogIcon` 及相关官方 C#／prefab 配置生成 ；运行时Unity 接入时须绑定原布局组、尺寸适配、奖励插槽和快照图标。原战斗碑素材、中文键、组件字段及输入 SHA256 均保存在生成脚本和 的 `SOURCE_INPUT_SHA256`／`BINDINGS`／`RESOURCE_SOURCES`，未绘制替代窗口。

## 烧烤原界面、顾客与历史纪录资源

`prepare_bbq_game.py` 从固定854_156383 APK及匹配CDN转换15个原烧烤prefab、2个通用历史纪录prefab、原已领取／红点附件及劳动节烧烤入口，共20个布局；200张原图片保留裁切、坐标、挂点、食材生熟焦状态、奖励和纪录背景。29名顾客沿原 `FigureModelUI`，其中23个模型核验后复用既有 `partner-detail/catalog.json`，6个模型及垃圾桶的原骨骼、皮肤、材质、分离动画在开发时转换；玩家模型／头像空槽保留原运行时外观绑定。运行资源位于 `assets/official/bbq-game/`；原组件、动画事件、语言、AOT AkEvent入口、动态角色槽、复用输入及输出哈希见 [bbq-game/source-manifest.json](bbq-game/source-manifest.json)。未使用新版替代包或自绘素材。

`prepare_activity_game_audio.py --game bbq` 依据原按钮、动画事件、`BBQMainGameContent` 及SDK `AkEvent` 生命周期配置，从5个固定版Wwise分包转换18个事件、19条音轨，保留容器随机分支、增益及停止动作。`LuoXiaoHei_Fail_Play` 沿原事件引用取得 `Battle_ME_00` 内嵌媒体；`prepare_battle_audio.py.convert` 补齐实际TXTP所引用的跨声库媒体依赖，不改事件选择与音频内容。输入与输出哈希见 [bbq-game/audio/bindings-source-manifest.json](bbq-game/audio/bindings-source-manifest.json)、[bbq-game/audio/source-manifest.json](bbq-game/audio/source-manifest.json)。共享纪录的本地达标领奖规则属于用户确认的单机自定义，具体差异见[活动小游戏](../../docs/features/activity-minigames.md)。

## 缺项活动小游戏官方来源与本地迁移

缺项小游戏使用固定 `854_156383` 的已校验 APK／CDN 内容。官方逻辑来源为 `client-source/FootballCollisionPanel.cs`、`client-source/FootballGameWindowManager.cs`、`common-source/EntityComponent/FCGameStateComponent.cs`、`client-source/ActivityDreamDance.cs`、`client-source/ActivityDreamDanceQuizUI.cs`、`common-source/DreamDanceNetService.cs`、`client-source/ActivityJumpGamePanel.cs` 和 `client-source/KnowledgeQuizQuestionPanel.cs`；连线与能量回路分别来自 `client-source/ElementLinkGame.cs` 与 `client-source/EnergyCircuitGridPlayable.cs`。

已核验并提取的原资源包包括 `defaultpackage_assets_prefabs_ui_activityfootballfestival.bundle`、`defaultpackage_assets_config_luban_activitydreamdance.bundle`、`defaultpackage_assets_config_luban_activityminigame.bundle`、`defaultpackage_assets_scenes_doodlejumpgame.bundle`、`defaultpackage_assets_prefabs_ui_activity_jumpgamecommon.bundle`、`defaultpackage_assets_image_ui_elementlink.bundle` 和 `defaultpackage_assets_image_ui_energycircuit.bundle`。知识问答题目／阶段表另以 `scripts/fixtures/knowledge-quiz-reference.json` 保存固定版 `ActivityKnowledgeQuizQuestionInfo`／阶段表的核对结果。原 `activityjumpgame` ScriptableConfig 分包未在已校验原版本缓存中取得，因此跳跃使用已取得的场景、计分配置和官方反编译逻辑，不冒充该缺包已下载。

`prepare_football_assets.py` 从已校验原版 `defaultpackage_assets_prefabs_ui_activityfootballfestival.bundle` 的 `FootballCollisionPanel.prefab` 转换完整静态控件树到 `assets/official/activity-games/football-panel.json`，并解码其55项原 Sprite 引用，含 `ImgFootballCollision_Ground`； 使用该清单显式加载运行时图片。prefab、Sprite、来源代码及转换输出 SHA256 见 [activity-games/source-manifest.json](activity-games/source-manifest.json) 的 `football_offline`。

上述迁移保留官方参数、题目筛选、选项显示位、输入状态、阶段奖励和原子存档；足球房间同步、舞蹈网络请求、知识问答在线助战／排行，以及连线／能量回路服务端题盘生成仍按[活动小游戏](../../docs/features/activity-minigames.md)登记为单机边界内的自定义部分。

## 人物技能彩虹品质文字

`prepare_skill_quality_text.py` 从固定 `854_156383` 的 APK／匹配 CDN 读取 `GlobalColors.asset.QualityColorConfig` 的 Normal 调色板，以及 `SkillPromotePanel`、`SkillRankUpNotifyDialog`、`SkillPanelSkillDetailDialog`、`SkillDetailDialog` 中六个品质文字的 `TMPColorGradient`。原四个 RGB 关键点、两个独立 alpha 关键点、Blend 模式和各组件 0°／45° 角度导出到 `unity/Assets/Official/Skills/skill-quality-gradient.json`（SHA256 `bc42085e3ee9d8bcf4edcf8283d083516cced5c04c3eadbc6ff4abf6d1fba4e0`）；没有使用 `GreenRainbow` 四角预设或绘制图片。`OfficialSkillQualityTextGradient` 将官方关键点应用到技能详情、培养和升阶结果的 UGUI 文本网格。原 bundle、对象、CDN URL、输入字节、调用源码及生成绑定 SHA256 见 [skill-quality-text/source-manifest.json](skill-quality-text/source-manifest.json)。

渲染迁移依据恢复后的同 APK `TMPColorGradient.OnPreRenderText`（`0x1121dd44`）、`SetGradientColor` 和 `UpdateMesh`，保留每行未旋转范围与绕原 RectTransform 原点旋转后的顶点归一化、顶点颜色替换和 Color32 的 ties-to-even 舍入。`generate_skill_quality_reference.py` 直接执行原顶点计算块、原 `Matrix4x4.MultiplyPoint3x4` 及固定 `libunity.so` 的 `Gradient.Evaluate`，夹具保存在 `scripts/fixtures/skill_quality_native.json`；原生镜像、metadata、Unity 引擎和方法字节哈希同存来源清单。

## 角色装备皮肤与衣柜

`prepare_character_appearance.py` 从固定 APK／匹配原 CDN（854_156383）迁移1723条 spine_skin、1567条 append_appearance、男女职业集合、品质经验、衣柜属性及原 UI；运行数据位于 `assets/characters/wardrobe/`。`source-manifest.json`、`rules-source-manifest.json`、`ui-source-manifest.json`、`effects-source-manifest.json` 分别保存部件／图集、配置／代码、原控件及原粒子的真实输入输出哈希，均位于 `assets/reference/character-appearance/`。`ui-source-manifest.json` 另记录 `HonorTitleItem_39602` 的 `huoyan (3)` 原生 MeshRenderer 未绑定 Mesh；

`prepare_character_skill_appearance_ui.py` 按固定 APK manifest `854_156383` 转换技能幻彩页使用的 `AppearanceSkillContent.prefab`、`SkillSkinGridItem.prefab`、`ItemIconSkill.prefab` 及其原 Sprite，来源分别为 `defaultpackage_assets_prefabs_ui_appendappearance.bundle`、`defaultpackage_assets_prefabs_ui_skill.bundle`、`defaultpackage_assets_prefabs_ui_item.bundle`。附件配置 `ItemIconAttachmentConfig` 的 Type 12／4 原挂件一并解析。导出 36 条技能幻彩选项和 30 张原图至 `unity/Assets/Game/Resources/Character/skill-appearance-ui.json`；原对象、配置、输入输出哈希见 [skill-appearance-ui.json](character-appearance/skill-appearance-ui.json)，Unity 运行时由 `OfficialSkillAppearancePanelView` 显式读取。

原男女骨骼按24 fps转换男13种／女14种 UI／地图姿态及独立压缩部件，完整分辨率 atlas、colormap、绑定皮肤、主副武器／背饰 follower 和正背面切换来自原包。32种独立装扮特效使用原 PlayerEffect/UI 与 Renderer prefab、UIParticle 缩放、ParticleSystem 参数及骨骼／插槽／附件跟随；4种有 HasEffect 标记的主武器在集成 prefab 内使用 Spine，检查无独立 ParticleSystem。没有绘制替代素材。

人物页 profile idle 运行载荷由 `scripts/prepare_character_model_runtime.py` 按男性/女性职业1/10所需的四种原始24 fps待机姿态、网格和贴图构建至 `unity/Assets/StreamingAssets/Character/character-models.zip`。包内 catalog、姿态、网格和贴图运行数据共 7,566 个文件；ZIP SHA256、生成器哈希、逐项输入哈希及 URI 同哈希副本映射记于 `assets/reference/character-appearance/source-manifest.json` 的 `runtime_pack`；五份关联人物来源清单也逐份登记哈希。Unity 运行时校验包内 SHA256，不直接加载 Android AssetBundle。

原战斗 Spine 包保留全部迁移皮肤与所需 follower，`prepare_battle_spine.py` 生成每种皮肤依赖页面；运行时仅载入当前组合使用的图集页。所有动态纹理、JSON 及 mesh.gz 已纳入显式资源绑定和 Windows 分包清单。限时装扮重复获得的续期按用户确认的配置叠加与上限处理，差异在[人物与衣柜](../../docs/features/character.md)记录。

## 装备界面与碎片选择

`prepare_equipment_completion.py` 按固定 `854_156383` APK／匹配清单的 CDN 资源，转换装备详情与比较、碎片详情及候选、祝福／套装提示、确认框、技能词条详情、装备星石属性行、等级提示及说明行。16 个原 prefab／模板、转换图像、组件字段、Prefab 来源、表格及调用源码哈希登记于 [equipment-completion/source-manifest.json](equipment-completion/source-manifest.json)；运行数据写入 `assets/official/ui.json`、`fidelity-rules.json` 和 `fidelity-texts.json`。转换没有绘制替代界面或图片。

## 装备继承特效与奖励挂件

`prepare_equipment_inherit_feedback.py` 按同一固定版本读取 `EquipInheritPropItem`、其原粒子系统与 `UIParticle` 配置，以及 `ItemIconAttachmentConfig` 引用的六种原挂件 prefab；将六个粒子发射器、六种奖励挂件和关联的原纹理写入 `assets/official/equipment-feedback/catalog.json` 及正式素材目录。[equipment-feedback/source-manifest.json](equipment-feedback/source-manifest.json) 保存原 bundle／对象来源、输入输出哈希、附件配置和客户端调用源码哈希。Godot 在继承属性行和奖励图标上复用这些原资源，不生成替代特效或挂件。

Unity 规则载荷由 `scripts/prepare_unity_equipment_catalog.py` 从上述固定版规则投影生成，仅保留装备运行所需表，不重复携带全局规则文件。`unity/Assets/Official/Equipment/source-manifest.json` 记录三份来源清单哈希及输出路径；`Runtime/source-manifest.json` 记录精简规则表 SHA256、输入 SHA256 和保留的表名。

同一工具还恢复装备、强化、继承、星石、详情及碎片选择的 39 个原 prefab 层级投影和 132 张来源图片到 `unity/Assets/Official/Equipment/UI/`。`UI/source-manifest.json` 记录原 prefab 来源身份、逐图哈希、实际使用的 UI 汇总 SHA256 及既有 `equipment-completion/source-manifest.json` 中的历史汇总预期值。当前 `assets/official/ui.json` 汇总 SHA256 与该历史预期不一致；不改写历史哈希，当前迁移范围通过固定版逐 prefab 身份与原图输入哈希检查。独立 UI 输出和 Unity `.meta` 可由 `prepare_unity_equipment_catalog.py` 复现，并由 `Zjcs.Editor.Equipment.OfficialEquipmentValidation.Validate` 核验。该载荷尚未恢复成可操作的 Unity prefab/窗口，也未完成 Unity 编辑器运行验收；完整范围见[装备、强化、继承与星石](../../docs/features/equipment.md)。Unity 官方载荷通过 [unity_asset_pack.py](../../scripts/unity_asset_pack.py) 纳入独立载荷快照。

## 联动活动窗口与音乐节转盘

`scripts/prepare_linkage_activities.py` 从固定 `854_156383` APK／匹配原版本 CDN 转换音乐节 `ActivityMusicFestival/GachaPanel/LuoTianYiGachaPanel`、春节 `ActivityFestivalSpring/Entry/ActivityFestivalSpringEntryPanel`、周年庆 `ActivityFestivalAnniversary/Entry/ActivityFestivalAnniversaryEntryPanel` 和万圣节 `Activity/Halloween/Entry/ActivityHalloweenEntryPanel` 原 prefab。运行界面目录为 `assets/official/linkage-activities/catalog.json`；`assets/reference/linkage-activities/source-manifest.json` 登记原 bundle／对象／URL／哈希、prefab 窗口、表格输入与转换输出。

活动时间、持续时长、CN/OpenTest 区服、UTC+8 偏移、入口条件和音乐节扭蛋配置来自固定版 `activity`、`region_release_time`、`local_time_offset`、`conditions`、`function_limit`、`activity_special_gacha`、`special_gacha`、`special_gacha_award_group`、`reward_rule` 与 `navigate`。客户端逻辑依据 `ActivityEntryLinkageActivityHandler.Refresh`、`ActivityEntryPanelCommon.OnEnableAndInited`、`ActivityInfo.IsOutOfDate`、`LuoTianYiGachaContent`、`WheelGachaContentBase.DoGahcaByCount`、`StandardRandomGroup` 和 `SpecialGachaUtils` 迁移；单机开服时间映射及本地抽奖差异见[行囊、物品与宝箱](../../docs/features/inventory.md)。没有推断活动年度复开，没有使用新版替代包或自绘素材。

## 幻兽粒子原材质与战斗线性后处理

`prepare_pet_materials.py` 从固定 `854_156383` 的 APK／校验匹配 CDN 读取 `Effect/Noise_Mask_Pro` 编译后 GLES 程序、15 项幻兽主动技能的 134 个材质及 168 个 ParticleSystem／Renderer。13 种实际关键字变体输出至 `shaders/pet-particles/`；原 CustomDataModule、顶点流、材质默认值、采样参数与光栅状态输出至 `assets/official/pet-materials/data.json`，逐层解码原 ASTC mip 链，并为线性目标生成保留 alpha 的 RGBA16F 数据。固定版本资源与适用的已登记 `864_156496` 同 CAB 依赖分别追溯，原 bundle／URL、对象、原始及输出字节 SHA256 见 [pet-materials/source-manifest.json](pet-materials/source-manifest.json)。没有用 `_BloomFactor` 推导新规则；该字段不参与本 shader 的实际编译程序。

`prepare_battle_post.py` 校验固定 APK 的 Launch `launch_assets_settings_renderpipeline.bundle` 与 `launch_unityshaders.bundle`，按原 metadata 字段序和严格字节长度解析管线、Renderer2D、PostProcessData、229 个战斗主／UI 相机、Volume 及两份 Bloom Profile。原 shader 的提亮、横／纵模糊、向上合成、LDR LUT 与最终合成运算输出至 `shaders/battle-post/`；原配置位于 `assets/official/battle-post/data.json`。Unity 官方 Graphics `2022.3/staging` 代码仅作序列化结构及引擎数学参照，执行程序直接取自 APK，并核对恢复后的固定原生 `Bloom..ctor`、`PostProcessPass.SetupBloom`、`ColorGradingLutPass.ExecutePass` 和 `TextureCurve.GetTexture`；输入、原生镜像／方法和参照文件 SHA256 见 [battle-post/source-manifest.json](battle-post/source-manifest.json)。

Compatibility Canvas 不自动解码 sRGB 采样，`prepare_battle_linear_textures.py` 因此继续解析原 Texture2D／SpriteAtlas 的 `m_ColorSpace`，仅对 sRGB 颜色 PNG 做过滤前解码，原线性数据／噪声／遮罩保持数值；生成无损压缩的半浮点像素，不改透明度或绘制替代图。原 PNG 输入及转换输出 SHA256 见 [battle-linear/source-manifest.json](battle-linear/source-manifest.json)，原图来源继续由 battle-spine、battle-pets 和 official-map 各原清单追溯。原 Spine 图集另建线性页面别名，保持原区域、尺寸和 UV，避免污染界面使用的原颜色页面；原染色在已线性的输入上执行，避免重复转换。战斗用浮点视口组合场景、角色与粒子，再以原后处理显示；HUD 沿原相机保持独立，仍使用 Compatibility 渲染器。原相机与材质接入范围见[幻兽](../../docs/features/pets.md)。

仅为 6 个原无 mipmap 绑定的专用纹理传入双线性过滤与各向异性 5，实际采样由 GPU 完成，不修改全局等级。手写适配源码为 `scripts/native/pet_texture_sampler.c`，输入及 DLL 输出哈希见 [pet-texture-sampler/source-manifest.json](pet-texture-sampler/source-manifest.json)。 对 37 个原网格发射器保留归一化的原 Color32 顶点色、三轴尺寸、枢轴、速度、原相机／局部／速度朝向、ZXY 初始旋转、父节点三维矩阵与最终三角形绕序，投影后对 50200 的原 `qiliu04` 执行原正面剔除；三维顶点继续传入原材质运算。枢轴依据固定 APK `libunity.so` 的 `BakeMesh` 原生几何路径（`0x1114f30`、`0x1124518`），使用原网格联合边界与带负 Z 的枢轴偏移，镜像 SHA256 及地址登记在 `pet-materials/source-manifest.json` 的 `native_mesh_geometry`。该范围没有替换网格或自行绘制贴图。

## 创角过场与反馈

开场剧情 UI 的当前资源以 [opening-source-manifest.json](character-creation/opening-source-manifest.json) 与其输出哈希为准：固定 APK 中的 9 个 prefab 已恢复 47 个 Sprite 引用、5 个 `PopupWindow` 动画片段引用及 UIMesh 材质数据。下方早期提取记录中的“7 个 prefab”及未含 Sprite 描述记录的是首次恢复状态，已由当前提取结果补齐；逐个检查这 9 个 prefab 的 MonoBehaviour 序列化数据，没有 `StateSpriteAssetProxyConfig` 引用。

固定 APK `854_156383` 的 `CreatePlayerPanel` prefab 动画数据由 `CharacterCreationPerformanceTools.BuildAndValidate` 转换为 13 个 Unity AnimationClip，恢复男女选择、页面切换、选择高亮与 glow 循环；同时恢复两个 UIMesh 溶解网格和 `TransformReorderer` 性别层级曲线。原 `defaultpackage_unityshaders.bundle` 的 `UI/EffectDissolve` shader 按实测 bundle／程序哈希核验，并适配为 Windows HLSL `CharacterCreationEffectDissolve.shader`；输入、输出哈希及官方代码来源见 [creation-performance-source-manifest.json](character-creation/creation-performance-source-manifest.json)。Wwise 音频使用原版 `WwiseBnk` 包并由 [audio source manifest](creation-performance/audio/source-manifest.json) 校验；`Main_Page`、`Main_Cursor_Normal`、`Main_Tag`、`Main_Cursor_Back` 四个 UI 事件的 Unity 本地 OGG 副本及哈希也登记在该清单，由 `CharacterCreationAssetCatalog.FindAudio` 和 `CharacterCreationFlowController.PlayUiSound` 分发。Unity Editor 资产、shader 和来源哈希校验已通过；Play Mode 运行表现待验收。该 prefab 没有独立 ParticleSystem，效果来自官方动画、材质和角色动作；未使用新版替代包或自绘资源。

开场剧情与录像由 `scripts/prepare_character_creation_opening.py` 从固定 APK `854_156383` 与已转换官方剧情数据提取：`Assets/Config/Story/CreatePlayerFightRecord.bytes`（bundle `defaultpackage_assets_config_story.bundle`，bundle hash `50ee5a2f1b0c5236442618152533e59d`，34,004 字节 GZip，解压 14 回合／1,168 步／19 个实体）、`BeforeFight`／`AfterFight` 剧情图（`defaultpackage_assets_scriptableconfig_story_storyfight.bundle`，hash `157d0f0d505963840eae965ebef2846e`）、`FirstStory` 剧情图（`defaultpackage_assets_scriptableconfig_story_map10story_basicguide.bundle`，hash `ef2a5f3244e4a8d01713cac78aeafffe`）、`dialog` CSV 的三段共 6 条对白与 840100／840200 角色行、`stage` 表第 20 行（`Type=33`、`GameId=902`）及`game_902` 网格地面、波次怪物 3803／3282／3281／3283／3481 与 `model-identities.json` 的怪物模型键。剧情 UI 从 `defaultpackage_assets_prefabs_ui_story.bundle`、`defaultpackage_assets_prefabs_ui_global.bundle` 与`defaultpackage_assets_prefabs_ui_fight_fightmainpanel_extmodel_fightmainpanelrecordfight.bundle` 导出 7 个 prefab 的RectTransform 层级、图片颜色、按钮与文本节点；官方图片经 prefab 内运行时代理取图，导出载荷不含 sprite 引用，该缺项已记入[新建存档与角色创建](../../docs/features/character-creation.md)。输入输出、每项来源与 SHA256 见 [opening-source-manifest.json](character-creation/opening-source-manifest.json)，运行时载荷位于 `unity/Assets/StreamingAssets/Opening/`。未使用新版替代包或自绘素材。

DIY 外观选项缩略图使用固定 APK `854_156383` 的 `defaultpackage_assets_image_ui_playerskin.bundle`（APK entry `assets/yoo/DefaultPackage/d9e385395fb855fe84340d2e1c152e18.bundle`）。`scripts/prepare_character_creation_skin_icons.py` 按原 Sprite 名提取 155 张 PNG，校验 bundle MD5、APK SHA256 与男女六类 141 个选项图标；Unity 载荷及输入／输出哈希见 [player skin icon source manifest](character-creation/player-skin-icons-source-manifest.json)。`CharacterCreationAssetTools.Validate` 将其作为 Sprite 绑定到创角目录并在选项控件显示，Editor 校验与创角 Play Mode 选项显示、选择状态验证已通过。没有绘制或替代图标。

创角“幻装试穿”组图从固定 APK `854_156383` 的五个 no-atlas 图标 bundle 提取 12 个 `item_<id>` Sprite；组 ID、男女、解锁条件、`append_appearance` 组件皮肤来自固定表 `append_appearance_group`／`append_appearance` 与现有角色皮肤目录。官方组名使用 APK entry `assets/yoo/WwiseBnk/9a0b6ab8c5136050fd005ead8f567863.rawfile`，原文件 MD5 `9a0b6ab8c5136050fd005ead8f567863`、SHA256 `738069eafacd7fe33aae970aa5edacd76f48ed02acdd07ededad1c4a499b93b8`、5,291,479 字节；`prepare_character_creation_group_icons.py` 按官方 `LocalizationBinaryFileReader` 的 12 字节索引和 `XXHash64` 键读取 `append_appearance_group_<id>`。图标与标签输入、输出及逐项 SHA256 见 [appearance-group-icons-source-manifest.json](character-creation/appearance-group-icons-source-manifest.json)。12 个 Sprite 已导入并绑定到目录资产；试穿组仅作用于创角页预览，不合并进存档，5／6／15／16 按官方域类型条件隐藏。组选择、互斥预览和离线保存验收见[新建存档与角色创建](../../docs/features/character-creation.md)。

开场录像特效由 `scripts/prepare_opening_skill_effects.py` 按录像实际 ClassId，从固定版 effects catalog、逐项 `battle-effects-source-manifest.json` 及 Wwise 音频目录核验后生成 `unity/Assets/StreamingAssets/Opening/skill-effects.json`。当前目录含139条事件记录、258张贴图、47个网格和31个本地音频文件；粒子配置递归保留主系统、跟随子系统与 SubEmitter，来源清单逐对象记录 APK bundle／CAB／path ID、输入哈希与336项输出哈希。输入包括 `assets/official/battle-presentation/effects.json`（SHA256 `191b761659b054e1f816aab2658a6872079ee6fb8fcf98545a6b3c70cd689a59`）、`assets/official/battle-audio/catalog.json`（SHA256 `445355dd5fcf674d410c5d24c847f10c454063b68939518947a15eb670f74b5e`）及固定录像；完整收据见 [opening-skill-effects-source-manifest.json](character-creation/opening-skill-effects-source-manifest.json)，输出载荷 SHA256 `8f274af270e9b33b652e8f5236a833376339dca14d1057f9bc38e85401c86a9c`。固定包的 Android `Effect/Noise_Mask_Pro` 编译 shader 无 Windows HLSL 源码，`unity/Assets/Game/Resources/Opening/OpeningNoiseMaskPro.shader` 按原材质属性适配，SHA256 `1891237be4f946cd952275e736dd95df2bc96f7381f7942fbcd8a0967b1748fa`；无新版包混入或自绘贴图。

## NPC邀请、探索首领与节庆首领

`prepare_npc_assistance_ui.py` 从固定 `854_156383` APK／校验 CDN 转换 `ExploreBossTeamDialog`、`ActivityFestivalBossPanel`、`ActivityFestivalBossAssistDialog`、助战说明条目、邀请面板及邀请条目；运行布局为 `assets/official/npc-assistance-ui.json`，原 prefab、Sprite、调用代码、状态说明文本及 SHA256 见 [npc-assistance/source-manifest.json](npc-assistance/source-manifest.json)。道路属性门槛复用原 `RoadblockTeamPropStageDialog` 与地图 UI；未绘制替代图片。

`prepare_official_rules.py` 导出 `stage_map_explore_boss`、`activity_festival_boss`、`activity_festival_boss_layer`、`activity_festival_boss_npc`，并更新运行时 `rules.json` 的数据哈希与显式绑定；APK 表、Luban 构造器和客户端／共用 C# 调用哈希见 `official-city/rules-manifest.json`。调用依据包括 `ExploreBossTeamDialog.OnConfirmBtnClicked`、`ActivityFestivalBossPanelBattleContent.StartBattle`、`ActivityFestivalBossUtils.GetLevelOffset`／`GetLayerBossLevelAndRank`、`PlayerActivityFestivalBossDataExt.UpdateActivityFestivalBossLayerData` 与 `RoadblockTeamPropStageTouchUI.HandleTouchBtnClicked`。单机流程由 、 和 接入；服务端缺失及经用户确认的本地选择／结算／等级规则见 [NPC邀请与战前准备](../../docs/features/npc-assistance.md)。

通天塔地图组（map 15）任务 HUD：`scripts/prepare_map15_ui.py` 从固定版任务 prefab 与语言包提取 81 条任务、366 项文本及原控件状态字段，复用已转换地图 UI 和战力颜色配置。来源、原始对象与输入输出 SHA256 见 `assets/reference/official-map/tower-ui-source-manifest.json`；Unity 运行时显式绑定尚待接入。未重建开发素材 ZIP，本地新增 `assets/official/map15-ui.json` 尚未进入完整快照。

## 鲸背岛（map 16）

`prepare_map13_minimap_ui.py --map-id 16` 从固定 `854_156383` APK／已校验原版本 CDN 转换鲸背岛原 `MiniMap_16`、全屏／局部小地图、筛选、角色动画、区域说明、迷雾及标记弹窗；原 prefab、Sprite、中文语言包、调用代码和输入输出 SHA256 见 [official-map/minimap-16-source-manifest.json](official-map/minimap-16-source-manifest.json)。运行数据为 `assets/official/map16-minimap.json`；Unity 显式资源绑定尚待接入。未使用新版替代或自绘素材；本地新增素材尚未进入完整 ZIP 快照。

通天塔三张大区域（42061／42062／42063）小地图：`prepare_map13_minimap_ui.py` 按原 `MiniMapPath` 从固定版 Utopia／Bronze／Snow bundle 转换 HUD、全屏 UI、玩家动画、区域说明与迷雾，原配置与本地输出为 `assets/official/map42061-minimap.json`、`map42062-minimap.json`、`map42063-minimap.json` 及各自 sprites 目录。实际原对象、包凭据、官方调用代码、语言、输入输出 SHA256 分别见 `official-map/minimap-42061-source-manifest.json`、`minimap-42062-source-manifest.json`、`minimap-42063-source-manifest.json`；对应 显式绑定。复用既有原小地图几何；没有自绘或新版替代包，新增资源尚未进入完整素材 ZIP 快照。

`prepare_map16_dialogs.py` 绑定原 38 条探索任务和 157 条中文文本，原 `TaskInfoParser`、`ClientWorldMapProgressCondition`、任务卡／详情调用代码及语言输入哈希见 [official-map/dialogs-16-source-manifest.json](official-map/dialogs-16-source-manifest.json)；运行数据为 `assets/official/map16-dialogs.json`，文本常量尚待 Unity 端接入。

通天塔小楼层地形材质与水面：`prepare_map15_rendering.py` 从固定 APK／原版本 CDN 的 10 种原地形读取 TilemapRenderer 材质与整体染色，保留原骨骼，去除重复静态骨骼网格，并转换 SnowFloor2／Floor1 原 Water2 平面与 11 个原 WaterMask 绘制的高度遮罩。输入对象、包凭据、原材质、资源处理与输入输出 SHA256 分别见 `official-map/materials-15-{地形ID}-source-manifest.json`；输出为 `assets/official/regions/{地形ID}.{json,geom}`、`map15-rendering.json`、`map-water-masks/42001.png`／`42012.png` 及原贴图，绑定由 管理。没有自绘或新版替代包；新增资源尚未进入完整素材 ZIP 快照。

鲸背岛地形材质、水面与灯光由 `prepare_map16_rendering.py`、`prepare_map13_lighting.py` 转换；世界区域说明由 `prepare_map16_world_ui.py` 转换。原版地形包 `ac1ab099039df1aa41312abb27b4cef2.bundle` 来自 manifest `854_156383`，原 APK 依赖、CDN 凭据、对象与输出哈希分别登记于 `official-map/materials-16-source-manifest.json`、`lighting-16-source-manifest.json`、`world-ui-16-source-manifest.json`。区域激活直接迁移原动画曲线及固定 APK 的 `Hidden/Light2D-Shape/USE_RECESSION` 程序；不新增原地图未启用的几何阴影。

通天塔默认镜头：`scripts/prepare_map15_scene.py` 按固定版世界场景 Main Camera、CameraControlBrain 的实际 ResolutionAdaptor 引用和 CameraControlSettings 转换 `assets/official/map15-scene.json`；原对象、四份官方控制代码及输出哈希见 `assets/reference/official-map/tower-scene-source-manifest.json`。 使用原安全范围、默认视野及背景色，资源须加入 Unity 显式加载清单。

通天塔大地形材质：`scripts/prepare_map15_special_materials.py` 与 `prepare_map15_rendering.py` 从固定版三张幻境和顶层原 prefab／PartitionData 转换 Tilemap 材质、原 Movable-Sprite GLES 程序及 TransparentMask 颜色写入、有效范围和图集 UV；区域昼夜几何写入 `assets/official/map15-layers/`，原透明通道遮罩写入 `assets/official/map-transparent-masks/`，仍由本地图资源绑定显式加载。原输入、着色器程序、控制器代码和实际输出哈希见 `materials-15-42061-source-manifest.json`、`materials-15-42062-source-manifest.json`、`materials-15-42063-source-manifest.json`、`materials-15-42053-source-manifest.json`；

鲸背岛独立地图入口与登岛界面：`prepare_map16_alone_ui.py` 直接转换固定版 `AloneWorldMapEntryManager`、`AloneWorldMapEntryItemUI`、`AloneWorldMapEntryItemContentMap16`、`AloneWorldTouchDialog` 与 `Map16_DialogIcon`。原脚本、RectTransform／布局组、中文语言、状态及剧情按钮绑定、资源来源和输入输出哈希见 `official-map/alone-ui-16-source-manifest.json`；运行数据为 `assets/official/map16-alone-ui.json`，由 显式加载。没有自绘或新版替代，新增素材尚未进入完整 ZIP 快照。

通天塔原灯光与区域退散：`scripts/prepare_map15_lighting.py` 从固定版 Main Camera／Renderer2DData 与三张幻境、顶层 prefab 提取 165 个 Light2D、61 个 MapRegionLight 动画及原 GLES 程序，使用原色值、顺序、排序层、AlphaBlend／Additive 和 MapEnvData.FadeTime；运行绑定 ；输入、派生贴图、原动画与输出哈希见 `assets/reference/official-map/lighting-{42061,42062,42063,42053}-source-manifest.json`。Unity 2022.3 官方 Light2DBlendStyle 参考仅用于枚举和 Multiply 因子核对，URL／哈希另列，不冒充游戏代码。

鲸背岛角色区域灯：固定版 `maproot_16` 的 `ECMapCharacterSpawnerView.mMeMapCharacterExtendSupportPrefab` 指向原 `mapcharacter` 包，`CharacterRegionLightHandler` 使用默认 2 秒渐变，`ECActionMapRegionActiveView` 使用 1 秒淡出。`prepare_map13_lighting.py`／`prepare_map16_lighting.py` 读取原世界摄像机选定的 Renderer2DData、Light2D overlap 与原 Shader 分支，转换区域 Alpha 叠加和角色加法光；组件、CDN 凭据、调用代码及输入输出哈希见 `official-map/lighting-16-source-manifest.json`。未使用替代或自绘素材，新增素材尚未进入完整 ZIP 快照。

鲸背岛绳索解锁与提交次数：原 `CanHandInItemNode`、`HandInItemNode` 及 `Common.Player.CanHandInItem` 的代码 SHA256 纳入 `official-map/alone-ui-16-source-manifest.json`，剧情图与 `hand_in_item[3]` 继续使用既有 `adventure.json` 官方来源。查询节点只核对次数，确认提交时核对并扣除 `43039×5`；没有新增或推测奖励／解锁条件。离岛领奖使用原 `SourceOperate.AloneMapAward=222` 与 `def_award_handler` 的 Popup 配置；该二进制表、枚举、`DefAwardHandler／AwardUtils／AwardPanel` 调用代码的 SHA256 同样记录于此清单，复用既有原版奖励面板。

通天塔角色区域灯：固定版 `maproot_15` 的 `ECMapCharacterSpawnerView` 实际引用原角色扩展支持 prefab，`CharacterRegionLightHandler.mLight` 的原 Light2D 保留加法光、强度、排序层与 FalloffLookup cookie；普通切区 2 秒、区域激活 1 秒渐变。`prepare_map15_lighting.py`／`prepare_map13_lighting.py` 按原引用提取，运行时 与共用 使用当前地图区域状态；四份 `official-map/lighting-{42061,42062,42063,42053}-source-manifest.json` 登记原组件、来源、代码及输入输出哈希。新增素材尚未进入完整 ZIP 快照。

通天塔原环境显隐：`prepare_map15_environment.py` 从固定版 14 种原地形的 prefab／PartitionData 提取实际 `MapEffect` 注册、原区域和 Day／Night 状态及精确骨骼／粒子路径；344 个分区注册复用对应原昼夜层，顶层 9 个非分区原雾效保持原显隐状态。输出 `assets/official/map15-environment.json`，显式绑定 ；原对象、官方控制代码、已转换输入与实际输出哈希见 `assets/reference/official-map/tower-environment-source-manifest.json`。没有新增或自绘特效，新增配置尚未进入完整 ZIP 快照。

通天塔原任务目标查询：`prepare_map15_ui.py` 的 `tower-ui-source-manifest.json` 补记原任务区域追踪、世界出口／传送点查询、地图组当前地图、坐标距离和固定坐标指引代码哈希；复用原任务、地图与方向 prefab，无新增贴图。运行时 ／ 读取原目标、完成阶段和出口／传送配置，仅定位镜头。

通天塔原自由镜头拖拽：`prepare_map15_scene.py` 沿 CameraControlBrain 的实际 IgnoreUIDragController 引用补提原拖动参数与 WorldCameraIniter，`tower-scene-source-manifest.json` 保留六个原对象、十二份官方代码与输出哈希。 按原 DragControllerBase／DragControlUtils 接入锚点、导航边界、额外限位、惯性与弹性回位；独立引擎参照为 [Unity 2022.3 Mathf](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Runtime/Export/Math/Mathf.cs)，URL、缓存路径和 SHA256 另列，不标为游戏代码。更新配置尚未进入完整 ZIP 快照。

通天塔原渲染跟随：`prepare_map15_scene.py` 按原 CameraControlBrain→VirCamera→m_ComponentOwner→CinemachineTransposer 提取跟随管线，并核对焦点祖先旋转和 Main Camera 的 CinemachineBrain；`tower-scene-source-manifest.json` 保留十六个原对象、十三份官方游戏控制代码及输出哈希。引擎实现单独引用 Unity 官方 Cinemachine 2.9.7 UPM 包，记录原 URL、归档及 Transposer／Predictor／VirtualCameraBase／Brain 源码 SHA256； 迁移其标准指数阻尼与原 0.2 参数，渲染与裁剪共用平滑后的坐标。无新增图片；更新配置尚未进入完整素材 ZIP 快照。

通天塔原箭头镜头定位：`tower-scene-source-manifest.json` 补记 `CameraControlBrain`／`ADirectionGuiderItemUIBase` 官方代码哈希。／`focus_target` 沿原方向指引调用保留视野边界限位、14 世界单位切换阈值与显式立即定位；没有新增美术或配置数值。

通天塔原炮弹演出：`prepare_map15_cannons.py` 沿固定版 `actionworldpuzzle.bundle` 的 `MyActionBullet(22108)` 实际飞行／停止 Playable 引用提取 14 个粒子发射器、14 份材质及 3 个网格，保留附着变换、原 GLES 算术、mip 链、Bilinear／各向异性、三轴尺寸及 TagManager 排序层。；按用户确认的本地规则播放用户已确认的本地发射路径，不冒充服务端碰撞实现。固定 APK／854_156383 CDN 输入与 864_156496 的两个独立同 CAB 依赖（c31b86c7ea536f280fd88aeaa0b8b557、5380c94014dd85cc88f5b8749f3225cf）分别登记于 `official-map/tower-cannons-source-manifest.json`、`tower-cannon-materials-source-manifest.json`，记录真实来源、URL、99 个 Unity 对象及输入输出哈希；未混入原版本缓存。运行资源位于 `assets/official/map15-cannon*`，shader 位于 `shaders/map15-cannon-particles/`，须加入 Unity 显式加载清单；没有新增自绘素材，新增素材尚未进入完整 ZIP 快照。

通天塔炮口与镜面演出：`prepare_map15_cannon_actions.py` 读取固定版 `actionworldpuzzle.bundle` 的三个 Action View／ECViewDriver 管理引用和五个原实体显示 prefab 的 CommonEffectSlot，转换四方向炮口／转向发射与镜面受击共九个原序列、82 个发射器；合并到原炮弹材质目录后共 96 个发射器、31 份材质及 5 个原 GLES 变体。`map15-cannon-actions.json` 保留挂载、时序、声音与尚未接入的原震屏配置。四个 `Map_Entity_FireCanon_1..4` 原 Wwise 事件由原 map_sfx_01／minimum 银行解析，并从实际 battle_sfx_01 跨包媒体补齐；使用 WWiser TXTP／vgmstream 保留原音量，规范化 Ogg 容器序号。`tower-cannons-source-manifest.json`／`tower-cannon-materials-source-manifest.json` 核验范围扩展为 718 个 Unity 对象，另存 Wwise 包、实际媒体片段、解码器和输出哈希；两项新版同 CAB 依赖保持独立登记。Unity 运行绑定仍待接入，新增素材尚未进入完整 ZIP 快照。

通天塔原炮口震屏：`prepare_map15_impulse.py` 从固定 APK／匹配原 CDN 的世界场景读取唯一自由虚拟相机的 `CinemachineImpulseListener`、原五类炮口／镜面 `MyGridTransform` 和 `WorldGamePlay_part1.g.bytes` 的真实中心，输出 `assets/official/map15-impulse.json`，须加入 Unity 显式加载清单。震屏片段及原 NoiseSettings 随 `tower-cannons-source-manifest.json` 保留；本项独立 `tower-impulse-source-manifest.json` 保留 18 个原 Unity 对象、输入输出哈希、固定 APK `libunity.so` 原 ARM64 `Mathf.PerlinNoise` 函数／512 项置换表／归一化常量区段哈希、官方 Cinemachine 2.9.7 UPM 包实际 URL 和归档／源码哈希。`scripts/fixtures/map15-perlin-native.json` 的 56 组输入输出由 Unicorn 独立执行原 ARM64 入口产生；原三维距离、传播速度、包络、随机相位、指数衰减和监听器配置直接采用，二维呈现接收原 XY 位移；不新增原场景未配置的次级反应。

通天塔元素连线输入：`scripts/prepare_map15_element_link.py` 从固定版 `defaultpackage_assets_prefabs_ui_minigame.bundle` 的 24 个元素连线 prefab 提取实际组件引用、状态切换、元素编号顺序、尺寸基准及成功延迟，复用已转换原 UI 与粒子素材；587 个原 Unity 对象、固定版客户端／Common 代码、语言包与输入输出哈希记录在 `official-map/tower-element-link-source-manifest.json`。运行数据为 `assets/official/map15-element-link.json`。`scripts/fixtures/element_link_reference_harness.cs` 只适配引擎／显示依赖，转换时插入并独立执行原 `ElementLinkGame` 的 26 个方法，生成开发输入 `scripts/fixtures/map15-element-link-native.json`；不把引擎适配、测试样本或本地存档标为原服务端逻辑。该输入控制器尚未替换运行界面。

通天塔元素连线界面：`prepare_map15_element_link.py` 沿固定版 `854_156383` 的实际 prefab、PPtr、GUID 清单及配置转换六处石堡 182–187 的 ElementLinkGamePanel、24 个绑定 prefab、五种原线头、十种线段／消散段、RuleDialog、TipInfo1、ConfirmDialog 与 PopupWindow 片段。`tower-element-link-source-manifest.json` 保留 1,215 个原 Unity 对象及原代码、Luban MiniGameSettings／rule_tip 输入哈希；原 Canvas 顺序、状态切换、布局、缩放和动画使用原序列化值。129 个原粒子发射器、17 份材质、4 种 GLES 变体和原 mip 链独立输出至 `map15-element-materials` 与 `shaders/map15-element-particles`，输入输出哈希见 `tower-element-materials-source-manifest.json`。六个 Wwise 事件从原 UI_BGM_01、UI_SFX_01 等实际银行／跨包媒体提取，由 WWiser／vgmstream 保留原银行音量；原 BGM 循环点和全部音频哈希记录于主清单。以上使用 APK／匹配原版本 CDN，未混入新版包或自绘素材；用户已明确允许该界面的 39 个原 ParticleSystemNoiseModule 与火炮 11 个共同采用现有 FastNoiseLite 采样，保留原素材及频率、强度、滚动、曲线和阻尼；扰动轨迹与 Unity 存在差异，不声称 FastNoiseLite 为 Unity 原算法。输入差分依据和来源沿用上段；Unity 运行绑定尚待接入。

通天塔遗迹：固定 manifest `854_156383` 的原 WorldRuinDialog／StageDialog、两种建筑预览、GameRuinEnterNextStageDialog／ConfirmDialog、二十层配置及竹林／雪地背景已用于六处入口与原下一层界面；`prepare_map15_ruins.py`／`tower-ruins-source-manifest.json` 保留九个 prefab、1,433 个 Unity 对象、57 个资源、原代码／语言包／清单和实际输入输出哈希。原 Image 射线标记、PopupWindow 动画、层状态、进度开关和按钮按原绑定迁移。遗迹 17–19 原配置地图 42062 与实际放置地图 42063 的差异保留。固定版完整清单中两条层背景仅有同名 BigMapStageBg 原图；经用户确认，在资源绑定中修正 MapStageBg→BigMapStageBg，不修改原层配置或素材。服务端层推进／结算执行器缺失；首层立即进入、按原 StageId／顺序战斗、战败重试、本层原追加物领取、已探索保存及退出保留采用用户确认的离线适配，具体差异见 docs/maps/map-15.md。

创角原生 UI 与字体：`prepare_character_creation_runtime_ui.py` 核对原 `creation-performance.json` 与 `assets/official/ui.json` 来源哈希，恢复 CreatePlayerPanel、DIYContent 与 ConfirmDialog 的真实字段、47 张原 Sprite、布局、遮罩及 TMP 样式；官方 StepTitles 由原 CreatePlayerPanel MonoBehaviour（pathID -3964310077427183409）读取，并沿固定版语言包取标题。`CharacterCreationUiTools` 生成 Unity 原生 prefab，输入输出与生成工具哈希见 `character-creation/runtime-ui-source-manifest.json`、`native-ui-source-manifest.json`。原 `FZY4K` TextAsset TTF 与 `unity/Assets/Official/Commerce/Art/fonts/FZY4K.ttf` 字节一致，SHA256 `90023872dffe6a58503de655bd0631bf3334ce3b6b72cc75a6625b685c94c386`，使用已有 FZY4K-Dynamic TMP 资产；没有替代字体或自绘图片。

创角原生预览：`prepare_character_creation_meshes.py` 复用已有固定来源 24 fps 官方网格帧缓存，逐项沿 appearance、gender、sign-preview 与 `appearance-group-icons-source-manifest.json` 等来源清单核验输入及别名，打包男女 idle／run、捏脸选项及 12 组外观组件网格与原图集、遮罩为 `unity/Assets/StreamingAssets/CharacterCreation/creation-meshes.zip`；`character-creation/native-mesh-source-manifest.json` 保存来源清单哈希、输入、别名、包与逐条目哈希。C# `CharacterCreationMeshFramePlayer` 独立使用 Unity MeshRenderer 播放，加载时逐条核对 SHA256；`CharacterCreationMeshFrameDye.shader` 适配官方 HSV 遮罩公式，不依赖 Spine runtime。已完成捏脸、染色、草稿和双槽位 JSON 回读及画面验证，组装外观和创角后家园节点验收见功能页；新增载荷尚未纳入完整素材 ZIP 快照，不声称已备份。

## 2026-10-23 商城 HUD 入口、完整目录与运行界面

`scripts/prepare_unity_commerce_entry.py` 从固定 manifest `854_156383` 已提取的 `assets/official/ui.json` 合并官方 `FunctionEntryDialog`、`FunctionEntryShopItem`、`ActivityCommonEntry`、`ItemDetailDialog` 及商城直购顶部 `AccumulatedPayEntryGo` 子树到 `unity/Assets/Official/Commerce/commerce-ui.json`；所需 32 张图片逐项按 `assets/reference/official-city/source-manifest.json` 的原 bundle／对象／SHA256 复制到 Commerce 载荷，生成缺失的 `.meta` 但未改原图或生成替代图。`scripts/prepare_unity_commerce_catalog.py` 继续从 `commerce-ui.json`、`rules.json`、`settings.json` 与 `item` 表生成 `commerce-runtime.json`，新版本把每棵 prefab 树展平为 `nodes` 并记录 `has_image/has_text/...`，避免 Unity `JsonUtility` 对深层可选成员的默认实例化；同时补齐 `ImgBg_Recharge-993940c86f`、`ImgBg_First-995290a9ab`、`ImgBg_Additional-b0eb862c9a`、`ImgIcon_PeriodicGiftIcon_5-ccd318ec1a` 等原 prefab 实际引用的官方 Sprite。来源仍为固定 APK／匹配原版本 CDN，未使用新版同名包，未自绘图片。

运行时 `OfficialCommerceAssetCatalog.asset` 由 `Zjcs.Editor.Commerce.OfficialCommerceAssetTools.Validate` 生成，现绑定 350 张官方图片、169 个奖励物品、10 个官方音频事件与 27 个 prefab（含官方 `ItemDetailDialog`）。月卡补入的累计奖励品质框／锁图来自 `defaultpackage_assets_image_ui_pay_accumulated.bundle`（固定 manifest `854_156383`，MD5 `e1ed8b9eb1cda15317920de572ee33fb`）；物品详情品质框来自 `defaultpackage_assets_image_ui_itemquality.bundle`（MD5 `74e6afc6b39b88d0e8977d8f12267b52`）；`AccumulatedPayPanel_Show` 原生压缩曲线来自 `defaultpackage_assets_prefabs_ui_pay_accumulatedpay.bundle`（MD5 `0b11bb5781ce56f124f55e08687be9d7`，pathID `-4611456846825482089`），由 Editor 校验并转换为 Legacy AnimationClip。具体 Sprite 哈希及映射记录于 [commerce-ui/source-manifest.json](commerce-ui/source-manifest.json)；HUD 首购入口和活动菜单项为官方 `AccumulatedPayEntryGo`／`ActivityCommonEntry` 结构及既有官方图片的重新组合，原 `FirstPayIconEntry` prefab 未在当前 UI 载荷中，因此不声称首购 HUD 图标为原 prefab 一比一复刻；入口位置、标题和红点状态为本地适配。详细实现、验证范围与剩余表现差异只记录在 [商城功能页](../../docs/features/commerce.md)。

## 2026-10-23 Map10 战斗怪物头像

`scripts/prepare_unity_battle_monster_portraits.py` 按固定 manifest `854_156383` 提取 Map10／Map11 所需 38 种 Monster.IconId (`G_<id>`)。APK 来源包：`defaultpackage_assets_image_itemicon_gameheadicon_monster_map4countries_noatlas.bundle`，MD5 `0479892d6d89bf17fd2e592af3f03a49`，SHA256 `b07fe5bd436a230d6c31f8a88a07e6f3cd3cbcee95ebf1aee56cbd763020c10e`；`defaultpackage_assets_image_itemicon_gameheadicon_monster_mapsp_noatlas.bundle`，MD5 `2683984d4e613c8eb9411bc16ea483cb`，SHA256 `7b71fe37fd62f6255aaa3c42a4bccf181426cbfe0d1a230067e2e192b2e486e2`。魔女召唤所需 `G_3703` 位于 `monster_maptherion_noatlas.bundle`；APK 实际包内没有该 bundle，复用已缓存官方云端文件，MD5 `735124c8a8db27ba8846a5a6770c7f89` 与固定版索引一致，SHA256 `933bd8b252ec9949d39d28057deca746f830fb5b0f0e5ad9ddcfb5580dd18c07`。缓存下载版本 `871_156598` 与所选索引 `854_156383` 分别记录，不更改预期哈希。源输入、PNG／Sprite meta 和输出目录哈希见 `assets/reference/battle-hud/monster-portraits-source-manifest.json`，原 PNG 与 `.meta` 纳入 `unity/Assets/Official/Combat/MonsterPortraits/`，显式绑定 `Combat/OfficialBattleMonsterPortraitCatalog`；catalog SHA256 为 `f67e051f5dc1febb3d4f561f7b8c8502bdc6912668a9b5025bd72af2fc6d010f`。此次未生成新的完整素材快照。

## 2026-10-24 宝石合成规则

`scripts/prepare_unity_gem_forge_rules.py` 从固定 manifest `854_156383` 的 `assets/official/rules.json` 读取 `gem`、`gem_merge` 和 `GemMergeStorySetting.StoryList`，写入 `unity/Assets/Game/Resources/Equipment/gem-forge-rules.json`。输入由 `assets/reference/official-city/source-manifest.json` 校验；Unity 载荷与输出 SHA256 记录在同目录 `gem-forge-source-manifest.json`，运行时由 `OfflineGemForgeService.LoadDefault` 校验。当前提取 360 条宝石类型和 14 条合成规则。规则原值不改写；高阶炉的随机结算状态随本地角色装备存档保存。

## 2026-10-24 素材秘境规则

`scripts/prepare_material_gathering.py` 从当前热更新 `871_156598` 的解码 Luban 表生成 `unity/Assets/Game/Resources/Official/Progression/material-gathering.json`，包括四条秘境、17条目标、34条攻击/击破/目标生成奖励规则、所需阶位数值以及 `GameSettings.MopUp` 的伤害浮动和单次上限。目标生成规则13000–13003由 `MopUpDungeonInfo.GenerateMaterialRuleId` 引用；本地服务在五个 `NewbieRewardMaterials` 初始目标后按该规则的 `IdList/WeightList` 抽取后续目标。输出来源及 SHA256 记录在 `material-gathering-source-manifest.json`，运行时由 `OfficialMaterialGatheringCatalog.LoadDefault` 校验。服务器业务程序集不在当前官方代码包中；攻击/击破奖励按用户确认的离线规则，从 `IdList` 按 `WeightList` 抽取一项并使用官方数量算法，不能据此宣称取得服务端实现。

同一配置还包含官方 `stage_map_location` 中可用的秘境解锁关卡位置。客户端按 `Player.GetStageCount` 检查逐关通关，但离线档案尚无逐关记录；53个解锁关卡中仅23个有非空区域映射，另30个为无表行或空 `RegionInfos`。Home 当前区域满进度判断是临时代理，需依功能页待确认项处理。

## 2026-10-24 素材秘境 UI 图像

`scripts/prepare_material_gathering_ui_assets.py` 从当前版本 `871_156598` 的 `defaultpackage_assets_image_ui_notes_mopup.bundle` 提取68个原始 UI Sprite，输入来自 APK ZIP 中与 `854_156383` 一致的官方同名包，bundle SHA256 为 `12080a2c5a32c5e2741b792a6cd20c1b469a765a1c4afd83175f349bfa6cf7ae`。Unity 图像载荷在 `unity/Assets/Official/Gathering/UI/`，对象路径、逐图 SHA256 和来源 URL 记于 `assets/reference/dungeon-ui/gathering-ui-source-manifest.json`。

`scripts/prepare_material_gathering_assets.py` 从当前 `871_156598` 已核验的399包 CAB 闭包导出并迁移官方 `MopUpEntryPanel`、`MopUpPanel` 及19项非脚本依赖；完整闭包的184项外部序列化引用全部解析。Prefab 与 Unity Resources 运行映射、三项 AssetRipper GUID 重定向、92个导出 MonoScript GUID 及当前工程内仍未解析的19个 MonoScript GUID、所有输出 SHA256 记于 `assets/reference/dungeon-ui/material-gathering-prefab-source-manifest.json`。入口与采集按钮由 `HomeHudView.MaterialGathering.cs` 接入本地服务；剩余脚本驱动状态/动作尚未迁移，也未完成 Unity Play Mode 检验。

## 2026-10-24 圣兽试炼与双影幻境

`scripts/prepare_unity_daily_challenges.py` 从当前热更新 `871_156598` 提取圣兽活动／难度／属性修正、双影活动、08:00 活动时间、阶段奖励、宝箱及奖励规则表；输入 bundle 与构造器哈希、完整消费行数和输出哈希见 `daily-challenges/source-manifest.json`。`scripts/prepare_unity_daily_challenge_assistance.py` 从已核验当前 Luban 表缓存导出 `conditions`、`npc`、`npc_level_detail`、`npc_profession_detail`、`player_robot_invite` 与 `skill`，运行载荷及输入来源见 `daily-challenges/npc-assistance-source-manifest.json`。`scripts/prepare_unity_daily_challenges_assets.py` 使用 AssetRipper 1.3.14 对已核验 CAB 闭包转换官方活动页、挑战页、战斗 HUD、结果 prefab 和依赖；清单登记 15 个 prefab、82 项官方转换输出、6 个 Resources prefab 映射（共 88 项输出）、33 项载荷目录 GUID、193 个省略的官方 MonoBehaviour 脚本及 836 项 CAB 依赖。`unity/Assets/Official/DailyChallenges/` 为官方转换载荷，运行时资源映射列于该清单。

`OfficialDailyChallengeCatalog` 与 `OfflineDailyChallengeBattleCatalog` 读取当前规则及固定 APK 关卡数据；`OfflineDailyChallengeService` 保存首次进入时间、335 小时期次与最高期次、08:00 逻辑日高水位、参与回执、挑战检查点、档位领奖和双影进度／宝箱事务。`DailyDungeonBattleRunner` 将圣兽／双影 stage 接入共用战斗，按本地 HP 结算及护盾吸收记录双影成绩，领奖写入角色物品事务；宝箱使用用户确认的逐次本地随机种子并先存抽取结果以支持崩溃恢复。`prepare_unity_daily_challenges_assets.py --runtime-only` 校验来源 prefab SHA256 并映射圣兽／双影结果页至 `Resources`；胜负、伤害及已提交的胜利奖励明细接入结果页。当前版 NPC 候选规则表由独立清单记录；游记挑战入口支持逐项选择，按官方条件与邀请次数筛选，布阵使用官方起点及用户确认的邻近可行走空格策略，战斗检查点保存助战单位。现有模型目录命中 12 名助战候选的 `body_*` 绑定，但原动画数据只有 `map/run_01`，缺少战斗所需 `idle_01`／`run_01`，选择界面会禁用它们。官方 NPC 4202 的 65 个序列化文件 CAB 闭包已核验；85 个候选 `fightnpc_*` 战斗模型、动画及依赖尚未完成 Windows 原生转换和运行绑定。官方客户端 C# 行为由本地 C# 适配实现，未复制被省略脚本。Editor／Play Mode 验证状态见[圣兽与双影功能页](../../docs/features/daily-challenges.md)。

## 2026-10-24 幻想阶梯塔层战斗配置

`scripts/prepare_tower_push.py` 从固定 APK manifest `854_156383` 解码 `stage_demon_battlefield.g.bytes`、`stage.g.bytes`、`stage_monster_array.g.bytes`、`skill.g.bytes`，并复用已校验的官方战斗场景与战斗/技能/通用规则，生成四入口共585层的官方战斗数据。编队按 `StageMonsterArrayInfoParser.GenerateMonsters` 的连续 GroupId 顺序展开；小数等级按 `LevelPropParser.GetPropsByFloat` 与 `BattleFormulaHandler.LerpProps` 插值。`scripts/prepare_tower_push_ui.py` 转换 `defaultpackage_assets_prefabs_ui_demonbattlefield.bundle` 中的入口、入口行、逐层页与四种塔层行 prefab，并导出原面板 Sprite、动态入口图及逐项输入／输出哈希；`scripts/prepare_tower_push_audio.py` 按固定 Wwise 来源校验并打包 `Note_FantasyLadder_Flash`、`Note_FantasyLadder_Elevator` 两条 OGG。来源、代码、输入及输出 SHA256 记录于 `tower-push/tower-battle-source-manifest.json`、`tower-push/ui-source-manifest.json` 和 `battle-audio/source-manifest.json`；运行载荷位于 `unity/Assets/Game/Resources/TowerPush/` 与 `unity/Assets/StreamingAssets/TowerPush/`，角色动作载荷另见 `character-creation/native-mesh-source-manifest.json`。玩法及奖励已接入本地运行时；塔行状态动画和粒子仍待从官方动画依赖恢复，Unity Play Mode 尚未验收，详见[幻想阶梯](../../docs/features/fantasy-tower.md)。

## 战斗结算报告官方面板

`scripts/prepare_unity_battle_report_assets.py` 校验固定 APK／CDN manifest `854_156383` 的7个报告 bundle SHA256，并将个人、全员、玩家统计行、技能统计行及分隔线7个 prefab 和34项依赖资产导入 `unity/Assets/Official/Combat/BattleReport/`；7个 Unity Resources prefab 位于 `unity/Assets/Game/Resources/Combat/BattleReport/`。Prefab 的 UGUI／TMP 脚本 GUID 映射至工程内 Unity 包，所有外部 PPtr 均在转换后检查；逐 bundle URL、输入／导出／运行时 prefab SHA256 和资产路径见 [report-panels-source-manifest.json](battle-hud/report-panels-source-manifest.json)。其中8个 AssetRipper MonoBehaviour 文件是缺少 IL2CPP 脚本重建后的空壳，不含行为。官方客户端面板逻辑见 `.tools/source-packages/official-code/client-source/FightEndReportPanelProvider.cs`、`FightEndReportPanel.cs`、`FightEndReportPlayerTabView.cs`、`FightEndReportSkillTabView.cs` 及同目录相关 Item／Base 类；业务代码依赖官方服务器报告结构和客户端框架，尚未接入本地执行器，具体功能缺项维护于[通用战斗页](../../docs/features/battle-execution.md)。

## 通用战斗霸体状态飘字

固定版 `854_156383` 的 `assets/official/battle-rules.json` 中状态实体 `25275` 配置 `FightStatusUnflinchingOnDamageComponent.Rate=0.5`；来源与输出哈希由 `assets/reference/battle-rules/source-manifest.json` 核验。官方共享代码 `.tools/source-packages/official-code/common-source/Common/FightStatusUnflinchingOnDamageComponent.cs` 在状态 `OnInvoked` 时向持有者发出 `EStatusCustomAction.Unflinching`，枚举值为 2（`EStatusCustomAction.cs`）；服务端调用时机未取得。离线实现按用户确认的推断在每次实际 HP 损失时掷配置率，并将动作 2 写入战斗步进事件／回执；固定 HUD world prefab 数据的文案绑定为“霸体”。

## 通用战斗随机目标命中

固定版 `854_156383` 的 `assets/official/battle-rules.json` 为随机命中实体 `13992`、`59361`、`11202` 配置 `MiniHitTargetCount` 与 `NotSameTarget=true`。官方共享类 `.tools/source-packages/official-code/common-source/Common/FightHitRandomTargetComponentInfo.cs` 定义随机范围、HitList 和去重字段；`.tools/source-packages/official-code/common-source/Common/FightHitRandomTargetComponent.cs` 仅提供这些配置，客户端 `.tools/source-packages/official-code/client-source/MyFightHitRandomTarget.cs` 是空包装，未取得服务端选敌器。离线按用户确认的规则，在同一组件的 HitList 子命中间按格网选择：`NotSameTarget` 排除已选单位，`NotSameEmptyScope` 排除已选空格，`NotSameGrid` 排除已选格子；候选耗尽时跳过后续子命中。`FilterOutOfGroundScope=true` 时越界格在分配前滤除，关闭时越界格可作为空格候选；`AllowEmptyScope` 允许空格消耗子命中名额，`NotAllowHitEmpty` 禁止空格命中，空格本身不造成伤害或召唤。选中单位作为子范围命中中心，召唤子命中仅消费选中的有效单位。执行和覆盖测试位于 `FightBattleHitCollector`、`FightBattleTargeting`、`FightBattleExecutor` 及 `scripts/test_unity_combat.ps1`；`FilterCondition` 等其他区域过滤仍未实现。

## 通用战斗职业技能可用性

固定版 `854_156383` 的 `FightUtilsHandler.IsSkillMeetProfession` 经 `ProfessionBaseInfoParser.IsSkillAvailableForProfession` 计算玩家主动／被动技能可用性；对应官方共享实现位于 `.tools/source-packages/official-code/common-source/Common/FightUtilsHandler.cs`、`FightSkillData.cs`、`PassiveSkillData.cs`、`ProfessionBaseInfoParser.cs` 与 `ProfessionUtils.cs`，数据来自 `assets/official/rules.json` 的 `profession_base`。离线目录按 `Skills`、`FightUsedSkills` 和 `PrePro` 继承链计算同一标记；职业为0时依据 `OfflineEquipmentProfile` 的装备位置10解析当前武器类型，按官方 Sword→Zhanshi（1）、Wand→Fashi（10）映射优先回退当前武器，再回退初始武器。Map10、日常副本、幻想塔和竞技场玩家技能绑定均写入 `ProfessionAvailability`。客户端 `FightMainPanelSkillUI` 将不匹配技能显示为 `GameSkillIcon.State.ProfessionLimit`。未找到施放执行链消费 `IsAvailable` 并拒绝技能的证据，故本地只迁移可用性标记与 HUD 状态，不添加释放禁令。

Map10 水面 Windows D3D11 编译适配仅为 `TRANSFORM_TEX` 的复合 UV 参数补充整体括号；官方 shader 来源仍为固定版 `854_156383` 的 `defaultpackage_unityshaders.bundle` （bundle hash `cac97355bb4799c415e3cb1ef38b6dc5`、PathID `5093245133389786381`）。原生重建入口为 `unity/Assets/Game/Resources/World/Map10Water.shader`，输出 SHA256 为 `7d8488c9451eb99a4f688d288146dd5059dcc79c8a56c68a6c816536f00ecb43`；已同步 `unity/Assets/Official/World/Map/10/rendering-source-manifest.json` 中的重建输出记录。纹理、官方输入哈希和显式加载路径均沿用原记录，Windows 构建已无水面 shader 编译错误；动态画面仍按地图页验收。

开场对话原生布局与载荷完整性：`prepare_character_creation_opening.py --ui-layout-only` 从固定 APK `854_156383` 的 `defaultpackage_assets_prefabs_ui_story.bundle`（MD5 `5473f9c47986ea5caafb199bd112f7fe`）重新读取 `OptionTalkDialog` 的 22 个布局组件，记录各组件 pathID、原始字节 SHA256 与 bundle／APK 来源，沿已有官方 `UGUIJszx.HorizontalLayoutGroupJSZX` 恢复姓名框。输出 `unity/Assets/StreamingAssets/Opening/ui/opening-ui.json` 的 SHA256 为 `08c4186721aa51ff3103bf283e0060690a4c4c477b9603f6fea03bd5683e5f52`，清单为 `character-creation/opening-source-manifest.json`；原 Sprite、字体、材质和动画资源未替换。开场粒子网格保留 `opening-skill-effects-source-manifest.json` 与运行载荷已有 SHA256，五个文件仅恢复原 LF 字节，Git 属性固定换行，未调整预期哈希。模型沿原 SkeletonDataAsset 的 `0.01` 导入比例，并由 `prepare_opening_model_transforms.py` 提取 APK 内 7 个 FightMonster／FightNpc prefab 的完整模型 Transform 父链（怪物 XY 为 0.3，4201 为 0.3、4202 为 0.23，保留落脚偏移），输出 `Opening/model-transforms.json`，并恢复怪物原材质 `_StraightAlphaInput = 1` 与透明输入，消除透明区域白块，`Resources/Opening/OpeningShaderVariants.shadervariants` 显式保留相应 Windows shader 变体与四个转场 UI shader，避免 Player 剔除动态查找资源后黑屏；清单逐层记录 pathID、原始 SHA256 与 APK 来源，运行时取消按 Boss 高度二次归一缩放；对话选项按官方 `OptionSlot.Reset` 隐藏，Windows 点击推进为用户明确指定的单机交互差异。

开场战斗单位与 HUD：固定 APK `854_156383` 的 FightNpc 4201／4202、Appearance 8401／8402、Boss 3803、GameRoundUI／GameRoundCharacterItem、GamePlayManagerConfig、CharacterDamageUI／DamageSequenceImage 及五套原位图字体已按 `prepare_opening_fighters.py`、`prepare_opening_unit_ui.py`、`prepare_opening_damage_ui.py` 和 `prepare_opening_skill_effects.py` 提取并显式绑定。Boss 原 107 节点／47 粒子、模板缓冲遮罩、网格描边和星空纹理保留；Windows 程序适配保留原材质属性、槽位深度和常量颜色。原生 HP Sprite 的九宫边框与世界单位换算已恢复。输入 APK／bundle／对象、官方类与输出 SHA256 见 [opening-source-manifest.json](character-creation/opening-source-manifest.json) 和 [opening-skill-effects-source-manifest.json](character-creation/opening-skill-effects-source-manifest.json)。本次 47 个运行网格保留已有 SHA256，Git 以二进制字节保存；不修改预期哈希。新增素材未生成完整快照，不能视为已进入 asset-pack 备份。

通用战斗准备与规则补全：`854_156383` 原战斗 prefab 的407项输入／初始化绑定由 `prepare_battle_hud_graphic_bindings.py` 提取并在 Resources 显式加载；404项为 Graphic／CanvasGroup 字段，3项为官方组件字段指向的初始隐藏对象，见 [graphic-bindings-source-manifest.json](battle-hud/graphic-bindings-source-manifest.json) 和 [session-source-manifest.json](battle-hud/session-source-manifest.json)。`prepare_battle_prop_defaults.py` 从 APK 的 `config_luban` 重新读取73条默认属性，迁移 `LevelPropParser.TryGetPropDict` 的缺键补齐机制，编译绑定至 `FightOfficialLevelPropDefaults`，输入、源码及生成哈希见 [prop-defaults-source-manifest.json](battle-rules/prop-defaults-source-manifest.json)。`prepare_battle_rule_texts.py` 从当前 `871_156598` 已校验语言文件提取5149条原文，输出 `Assets/Official/Combat/battle-texts.json`，版本与输入输出记录见 [text-source-manifest.json](battle-rules/text-source-manifest.json)，详情弹窗绑定待接。战斗验证依赖的累充原生曲线从已有官方 bundle 经 AssetRipper 恢复，保留曲线并适配 Legacy Animation，见 [动画来源](commerce-components/accumulated-panel-animation-source-manifest.json)。本次未生成完整素材快照，新增载荷未声明已备份。

开场技能移动与受击：`prepare_opening_action_motion.py` 复用固定 APK `854_156383`，提取 11532／11833／15301 的 EC 配置、移动 prefab、轨迹曲线及 15300 的跑动触发器；按官方 `MyFightSkill` 的技能实体播放顺序恢复 9 个 NPC 专用动作。精英 3481／Boss 3803 的 `MeshMaterialProperty` 绑定、材质与 shader 对象及 `_HITEFFECT` GLES 程序哈希收录在 [opening-source-manifest.json](character-creation/opening-source-manifest.json) 的 `action_motion`；Windows 受击适配保留模板遮罩、透明裁剪及预乘混合，输出绑定为 `Opening/action-motion.json` 与 `Resources/Opening/OpeningSpineHit.shader`。`AppearanceRendererLite`／`CustomRenderExistingMesh` 的运行时材质登记也由同一来源清单记录，Boss 描边在受击克隆期间保持原替换关系；四个剧情 UI shader 调整 Windows 裁剪参数声明顺序，输出哈希列于同节。新增载荷未生成完整快照。

通用战斗场景和单位绑定：`scripts/prepare_battle_scene_bindings.py` 按 APK 优先、固定版 `854_156383` 云端缓存回退，恢复原 TagManager 排序层、344种单位模型 Transform 父链／MeshRenderer／血条排序及256个战斗场景的原相机位置和尺寸，严格解码 `GameCameraController` 依赖。输入对象、CAB、pathID、原始哈希和运行绑定哈希见 [layout-source-manifest.json](battle-scenes/layout-source-manifest.json)，载荷由 `Combat/battle-scene-bindings.json` 及 SHA256 侧车显式加载。现有场景网格恢复向下 Y／纹理 V，模型采样网格恢复纹理 V，并保留原 prefab 模型节点变换；这些是历史载荷的渲染修复，不代表已替换旧 Spine 转换依赖或完成原生角色动画。真实鼠标准备、运行至结算和场外节点／镜头恢复通过，角色、头像及完整 HUD 缺项仍见战斗所属页。

通用战斗复用角色绑定：`OfficialBattleCharacterAnimationTools.Build` 检查既有男女原生 prefab 与实际 AnimatorController，各绑定26个非空 Motion，排除442／449个空状态；`Resources/Combat/OfficialBattleCharacterAnimationCatalog.asset` 按控制器引用和片段名称显式加载。输入 prefab／控制器哈希、既有转换链凭据、生成器及输出哈希见 [character-animation-binding-source-manifest.json](battle-models/character-animation-binding-source-manifest.json)。3100模型父链复用上述场景凭据，角色退场还原世界层级、变换、显隐和动画状态。本次没有重新烘焙 Spine 数据，历史转换依赖和未转换动作继续保留原结论，动态头像仍未完成。

角色附件原生几何：`prepare_native_character_geometry.py` 从当前 `871_156598` 官方 APK 优先读取男女骨骼、分离动画原数据及实际图集，独立使用固定 MIT 格式读取器，保留二进制附件名称覆盖、原骨骼局部顶点、每个影响的权重、region 变换、图集裁剪／旋转及贴图路径。男性654／女性706个现有附件输出至 `Resources/CharacterCreation/native-character-geometry.json`，共享模型工厂绑定 `CharacterNativeAttachmentGeometry` 并校验 SHA256 侧车；原始输入、解码凭据、旧原生 prefab、生成器、运行绑定和输出哈希见 [native-geometry-source-manifest.json](character-creation/native-geometry-source-manifest.json)。男性旧 prefab 中498处失效网格引用已重建；有效旧网格 UV 一致性、全附件非零几何和骨骼变换恢复通过，男女战斗头发／五官实际画面已核对。该几何转换不调用 Spine runtime，也不使用网格帧；既有 Animator 的历史来源继续保留，完整约束／变形／未转换动作不在完成范围。新增载荷未生成完整素材快照。

断罪魔印累计伤害：`prepare_battle_accumulated_damage.py` 重新解析 `854_156383`／`871_156598` 两版 APK `config_ec` 的 `FightSkill_part703.g.bytes` 与 `FightSkill_part157.g.bytes`，核对24062累计组件、13148技能、13149命中与 Unity `battle-rules.json` 实际字段完全一致。原 bundle、CAB／对象、载荷哈希、反编译组件／枚举、当前中文 `entry_24062_desc` 和生成器哈希见 [accumulated-damage-source-manifest.json](battle-rules/accumulated-damage-source-manifest.json)。本地规则按说明累计非真实生命伤害、状态结束时执行原30%自定义伤害链；没有新增或替换美术。服务器完整触发排序及额外战报分摊未由此取得，承伤加成／优先选敌和来源死亡边界仍见战斗页。

战斗快捷入口状态：`prepare_battle_function_entry.py` 复用固定 `854_156383` APK 内 `defaultpackage_assets_animation_ui_functionentrysui.bundle` 的原控制器及三个片段，从已校验同版 FightMainPanel 缓存恢复 StateSwitcher、两个按钮及两项 SelfRect 引用；输入对象、bundle／APK 哈希、官方源码和九个可选缺目标通道的哈希记录见 [function-entry-source-manifest.json](battle-hud/function-entry-source-manifest.json)。`OfficialBattleFunctionEntryTools.Restore` 保留原60fps曲线、整数参数、三个状态／转换及收展事件，生成 `Assets/Official/Combat/HUD/Resources/Combat/FunctionEntrys.controller` 与三个原生 AnimationClip；输入、转换器和资产／meta SHA256 见 [function-entry-native-source-manifest.json](battle-hud/function-entry-native-source-manifest.json)。`OfficialBattleHudView` 按原节点显式绑定官方控件逻辑及桌面指针适配；没有绘制替代面板或恢复消息服务。新增载荷未生成完整素材快照。

战斗 HUD Sprite 导入：`OfficialBattleHudSpriteTools.Restore` 从上述30个固定版 prefab 节点读取88张实际引用图片的尺寸、边界、像素枢轴及 PPU，恢复38份受影响导入器；其中25张原九宫格边界先前未应用。`OfficialBattleHudAssetTools` 同步修复扁平树初始化和资源路径／文件名查找。原节点、官方来源清单／载荷、PNG、meta 及转换器 SHA256 见 [sprite-import-source-manifest.json](battle-hud/sprite-import-source-manifest.json)，显式绑定继续使用 `OfficialBattleHudCatalog`。实际 Play Mode 已核对快捷入口收展和 `ChatMiniPanel` 背景圆角；聊天服务和动态头像仍未迁移完毕，新增元数据未生成完整素材快照。

战斗头像拍摄：`prepare_battle_portraits.py` 分别读取 `854_156383`／`871_156598` 官方 APK 配置，确认头照摄影参数与场景分支开关相同，再从固定854 APK 提取摄影棚和两个场景时间轴 prefab 的原层级／组件引用。76项输入对象、版本、原始字节、官方代码、生成器、运行适配及 JSON 哈希见 [portrait-source-manifest.json](battle-hud/portrait-source-manifest.json)。运行载荷为 `Assets/Official/Combat/Portraits/Resources/Combat/battle-portraits.json`；`CharacterNativePortraitCapture` 以当前原生附件网格和已有官方姿势拍摄透明头像，不使用采样帧，不生成替代素材。此阶段尚未绑定完整场景时间轴及其材质、粒子与动画，新增载荷未生成完整素材快照。

场景时间轴迁移：`prepare_battle_timeline.py` 按固定 `854_156383` APK 优先及已校验同版依赖恢复 `GameRoundScene`／`GameRoundCharacterItem_Scene` 的原 Transform、实际序列化字段引用、10张 Sprite、3份材质、3种 GLES shader 及原 Animator 控制器／4个片段。源纹理／图集、裁剪与 Sprite 几何、原对象／CAB／pathID、官方代码及运行适配哈希见 [timeline-source-manifest.json](battle-hud/timeline-source-manifest.json)；Windows 原生 prefab、材质、Sprite、controller、AnimationClip 与 meta 哈希见 [timeline-native-source-manifest.json](battle-hud/timeline-native-source-manifest.json)。显式绑定为 `Combat/OfficialBattleTimelineAssets`，动态头像复用上述摄影参数和当前原生角色网格，原圆形网格实现保持客户端 UV 修正；shader 从实际使用的 GLES 变体转换。其他角色类别和完整事件调度仍未完成，未生成完整素材快照。

场景时间轴粒子：`prepare_battle_timeline_particles.py` 以固定 `854_156383` APK 的 `GameRoundCharacterItem_Scene` 及已校验依赖，经 AssetRipper 1.3.14 保留两组原生 ParticleSystem／Renderer 和四个 Transform，恢复两份材质／两张原贴图。Windows 材质绑定按原 `USE_MAINTEX` 第一粒子 pass 的 GLES 程序转换，保留第二地图投影 pass 原始程序供后续所属流程使用；没有启用原 prefab 关闭的 `UIParticle` 父节点。原 bundle／组件／材质／shader／贴图哈希、解码包、工具／生成器、导出输入和输出见 [timeline-particles-source-manifest.json](battle-hud/timeline-particles-source-manifest.json)，显式引用随场景时间轴原生 prefab 打包。原生序列化一致性检查与真实输入战斗返回通过，新增载荷未生成完整素材快照。

战斗 NPC 时间轴头像：`prepare_battle_npc_portraits.py` 以固定 `854_156383` APK 的 `npc.g.bytes` 与官方构造器解码148条配置，通过 APK 优先及已校验同版云端 bundle 提取89张原 Texture2D／Sprite，保留原尺寸、中心枢轴、PPU和采样／环绕参数。配置、资源清单、原对象／bundle／APK或云端哈希、官方代码、生成器、显式目录及输出见 [npc-portraits-source-manifest.json](battle-hud/npc-portraits-source-manifest.json)，绑定入口为 `Combat/OfficialBattleNpcPortraitCatalog`，由通用场景时间轴的 NPC 分支按 `NpcClassId` 解析。战斗两表引用61个非零NPC，已覆盖53个；11015—11022的8张配置图片经854／871清单和全部9个头像 bundle 实际对象核对仍未定位，查找输入分别记录，未混用版本或替换素材；49条 MapExploreBoss 历史图路径及2条空 Icon 保留未绑定。新增载荷未生成完整素材快照。

战斗主面板材质：`prepare_battle_hud_materials.py` 从固定 `854_156383` 官方 FightMainPanel prefab 的三处实际 Image 引用取得序列帧、加亮及胶囊边框材质，沿引用取得原图、Shader 和全部16个 GLES 程序。Windows HLSL 转换保留选定分支的原运算与12个 UI 裁剪变体、材质属性和渲染状态，另外4个原分支归档保留；不复用缺少循环分支的其他 shader。输入对象／CAB／bundle／版本、转换器、运行绑定和输出哈希见 [materials-source-manifest.json](battle-hud/materials-source-manifest.json)，Unity 导入参数与原生材质／目录／meta 见 [materials-native-source-manifest.json](battle-hud/materials-native-source-manifest.json)。显式绑定为 `Combat/OfficialBattleHudMaterialAssets`，实际序列帧及真实鼠标战斗返回已验证，新增载荷未生成完整素材快照。

## 森之国魔女结果与主线目录

`prepare_unity_map11_runtime_data.py` 沿固定版 `854_156383` 的实体 6620 EC 组件、`WitchInfo`、`map_periodicity_random_award_interaction` 与 APK 内 `reward_rule.g.bytes`，保留 14 条全图激活计数规则、七条周期奖励权重，以及原结果组件类型／索引；实体组 2000 解析到官方召唤阶段 20001，战斗载荷由 129 个放置阶段补为 130 个。输入对象、版本、奖励原始载荷及输出哈希见 `official-map/map11-runtime-source-manifest.json` 和 `unity/Assets/Official/World/Map/11/battle-source-manifest.json`，显式加载为 `Resources/Map11/stories` 与 `Resources/Map11/battle-runtime`。缺失服务端的结果结算、共享每日额度、下一场战斗状态及 HP 至少保留 1 点均按用户确认处理，不能标作服务器源码还原。

`prepare_unity_mainline_tasks.py` 复用已核验官方 `task`／`npc_friendship_level` 及奖励表，生成 33 项本地主线目录（含 1045～1160 的 24 项森之国目标）与 10,000 条好感门槛。来源与输出哈希随 `Resources/Official/Progression/mainline-tasks-source-manifest.json` 交付，由 `OfficialMainlineTaskCatalog` 加载；正式入口采用用户确认的 Map10 任务 1040 领取门槛，祈愿宝箱计数在原奖励／消耗事务中写入。整图与正式入口仍按地图页所列范围验证。
