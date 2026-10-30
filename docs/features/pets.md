# 幻兽编队、养成与改名

[总索引](../README.md)

状态：未完成

## 功能简介

幻兽页管理四阵位、随行、喂养、技能、潜力、进化、能力树和改名。取得与结缘演出见[幻兽结缘](pet-gacha.md)，NPC伙伴属于独立系统。

## 原始官方实现

原幻兽面板读取幻兽实体、列表肖像、改名、升级／突破、被动与主动技能及高赛季说明，属性沿用对应原表。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

核对原始 `PetListPanelItem`、`PetInfoContent.Init` 与 `PetSettings`，幻兽列表应替换 `PetInfoSlot/Mask/IconImg` 的图片，并按当前进化阶段加载 `pet_{ClassId + 100 × (phase - 1)}`。修正此前保留模板示例 `pet_1704` 并叠加通用圆形物品图标的行为，直接使用原卡片图片、遮罩、等级与名称节点；品质背景按实际 Quality 切换，底部类型图标按 PetSettings 的 0 / 1 / 2 映射绑定。

`prepare_pet_ui.py` 从固定 APK 的 `image_itemicon_pet`、`image_ui_pet` 与 `scriptableconfig_modules_pet` 提取 39 张现有阶段图片、三种类型标记与对应配置，新增 20 项运行时 Sprite，其余复用已有原图。没有重绘或生成图片。配置中的 `pet_1619` 不在原始幻兽图像 bundle 内，保留待补齐记录；该项不再显示其他幻兽的模板图片。

依据：[pet-ui/source-manifest.json](../../assets/reference/pet-ui/source-manifest.json)。

`prepare_fidelity_pet_ui.py` 当前转换 33 个原 prefab／引用模板和 857 组完整字段／材质绑定，包含能力图与连线、培养／重置、技能详情、进阶、通用碎片确认和原技能标签模板。遗物绑定采用相同修复。原 TextMeshPro 的自动字号、换行和溢出设置保留在转换数据中。原能力培养、技能进阶、取消、重置返还已实际接入；原编队、筛选和循环成就已接入；改名、导师、后期祝福、经验喂养成就统计和独立实体战斗仍未全量完成。

依据：[pet-fidelity-ui/source-manifest.json](../../assets/reference/pet-fidelity-ui/source-manifest.json)。

`prepare_fidelity_skill_rules.py` 依据原 Luban 构造器、MessagePack 格式化器及静态类型解析器，严格解码 3,824 个技能／状态／命中实体和 108 个等级属性类。运行数据位于 `assets/official/skill-rules.json`，输入配置、源码、格式化器及输出哈希见 [skill-rules/source-manifest.json](../../assets/reference/skill-rules/source-manifest.json)。原阶级倍率、等级属性、被动、召唤加成、显示排序及隐藏规则用于技能详情与升阶预览；完整技能战斗没有因此自动完成。

编队遮罩、筛选、成就、技能详情、进阶、重置、确认及能力重置截图已目视复核。没有原 APP 同状态实机截图，不作为全范围或逐像素一致结论。

原 PetPanelWriteMask / PetPanelMask 材质的 ColorMask、Stencil 比较与操作、AlphaClip 及九宫格图片已保存到转换记录；原 LoopScrollGridLayoutGroup 的最小间距、列数及动态间距由原程序集反编译核对。没有新增自绘图片。原成就循环与战力评分的源码哈希见非主线实施证据。

`scripts/prepare_pet_naming.py` 从固定 `871_156598` 的 `prefabs_ui_inputdialog` 提取原输入面板、字段绑定、显示／隐藏动画；确认面板复用已转换的同版 `ConfirmDialog`。原 `MainCanvas` 的 `WindowMaskType.Mask` 图像色值为 `[0,0,0,0.7803921699523926]`，另有原模糊确认遮罩。中文字符集经 `CustomContentSettings.LanguageType=0` 的 GUID 定位原 `FZY4K_Characters.txt`，共 21,993 字符；未混用其他地区字体集合。

运行数据为 `assets/official/pet-naming.json`，来源、对象 ID、原始字节、字符集、原语言包及输出哈希见 `assets/reference/pet-naming/source-manifest.json`。`scripts/generate_pet_name_reference.py` 在开发时执行原 `CJK.cs`，37 个字素权重向量及原代码／执行器哈希保存在同目录 `reference.json`。外部文字审核跳过，服务器缺失的次数变更采用已登记的本地规则；这两项不宣称为原服务器实现。原自定义名四角颜色渐变仍待全局文字效果适配。

prepare_pet_progress_ui.py 从固定 871_156598 的 prefabs_ui_tips、prefabs_ui_pet 解析原 PopTipMutilDesc、PopTipBlessProp、其 TipInfo2 和 PetDetailCommonContent 指向的 PlayerPropItem；保留原布局组、禁用 LayoutElement 的忽略布局字段、LayoutElementClamper、TMP 边距及 GreenRainbow 预设引用。4 份模板、60 份组件绑定和 11 张引用图片的原包／对象、输入输出 SHA256 见 pet-progress/source-manifest.json，输出为 assets/official/pet-progress/ui.json。10 张图片复用并逐字节验证现有原贴图，仅 ImgInfoBG_Bless 转为模块独立的原生 ImageTexture；没有重绘图片或重新设计坐标。prepare_pet_progress_text.py 从原 WwiseBnk 语言分包生成原文及源哈希，运行时替换原 Item61 名称。

`scripts/prepare_pet_naming.py --gradient-only` 从固定 APK 的 `PetInfoContent.mPetNameController.mPetName` 定位实际 TMP 组件及其 `PetName` TMP_ColorGradient，22 处引用使用同一原预设。四角颜色和组件／预设输入哈希记录在 `pet-naming/source-manifest.json`，运行数据为 `assets/official/pet-naming.json.name_gradient`； 按原 `PetNameController.Refresh` 仅对自定义名称启用逐字四角渐变，未自绘颜色。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

本地保存原阵位、技能、能力、潜力及各季祝福。赛季切换冻结当前记录，原第7赛季未配置的曲线不编造。

服务端 `PetMaxExpCost` 写入体未取得。用户确认经验喂养成就保留所有幻兽当前喂养总经验的历史最高值；等级重置不清除最高值和领奖记录，再次喂养重新计算当前总量，未超过旧最高值不重复累计。载入和喂养时观察现存阵位／赛季投入并更新最高值，不反推已经丢失的重置历史。该单机规则仍须接入 Unity 并验证。

服务端改名次数更新体缺失：本地自定义改名消耗一次，跨原UTC零点重记；恢复默认名不返还当日次数，仅跳过外部文字审核，不加费用。

幼年列表图标 `ItemIcon/Pet/pet_1619` 重新解析固定 APK 五包完整资源路径仍未列出，原幻兽 Sprite／Texture2D 对象、相关图集及185个新版同名补包亦未取得。进一步从官方当前目录 `202610141241870` 下载清单 `870_156522` 及匹配大小／MD5 的 `image_itemicon_pet`、`image_ui_pet` 包，完整新版资源路径与两包实际图像对象仍无该图。按用户“实在没有就算了”的意见继续隐藏图片、保留名称，不绘制替代图；9219详情／随行／战斗模型正常保留。可复查入口为 `prepare_pet_ui.py --recheck-portrait`，版本、URL、输入哈希及实际对象见 [pet-ui/source-manifest.json](../../assets/reference/pet-ui/source-manifest.json)。

### 已确认的本地接线与边界

`prepare_pet_ui.py --recheck-portrait` 重新解析固定 APK 的五包完整资源路径，并从官方引导配置的当前目录 `202610141241870` 取得 `870_156522` 主清单及 `image_itemicon_pet`、`image_ui_pet` 两包，独立校验大小、MD5、SHA256 后实际解析 Sprite／Texture2D。原、新完整路径和新版两包均无 `ItemIcon/Pet/pet_1619`；来源 URL、版本、哈希、对象名在 `pet-ui/source-manifest.json.portrait_sources`，新版原始缓存单独保留在 `.tools/source-packages/cloud-research/pet-870_156522/`，未混入原版本缓存或作为运行素材使用。按用户确认保留无图状态，未生成替代图，9219原模型未更动。

## 尚未实现

- 四阵位、随行、经验喂养、技能、潜力、进化、能力树、赛季祝福、改名及原模型／图标。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。

## 代码与资源入口

[PlayerPetUtils](../../.tools/source-packages/official-code/common-source/Common/PlayerPetUtils.cs)、[PropConfigUtils.CalcPetSkillViewProps](../../.tools/source-packages/official-code/common-source/PropConfigUtils.cs)、[BattleFormulaHandler.CalcSkillProps](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)、

[原版技能回放目录](../../assets/official/battle-animation/catalog.json)、[原版技能特效目录](../../assets/official/battle-presentation/effects.json)、[动画来源清单](../../assets/reference/battle-animation/source-manifest.json)

[特效来源清单](../../assets/reference/official-map/battle-effects-source-manifest.json)、[MyFightSkill.PlayPlayable](../../.tools/source-packages/official-code/client-source/MyFightSkill.cs)、[MyPlayableFollowBone](../../.tools/source-packages/official-code/client-source/MyPlayableFollowBone.cs)、[AttachPosRotCfg.GetBone](../../.tools/source-packages/official-code/client-source/AttachPosRotCfg.cs)、

`scripts/prepare_battle_effects.py`（来源工具标识）、[prepare_battle_animation.py](../../scripts/prepare_battle_animation.py)、

`scripts/prepare_pet_materials.py`（来源工具标识）、

[粒子材质来源](../../assets/reference/pet-materials/source-manifest.json)、`scripts/prepare_battle_post.py`（来源工具标识）、`scripts/prepare_battle_linear_textures.py`（来源工具标识）、[battle-post](../../assets/reference/battle-post/source-manifest.json)

[battle-linear](../../assets/reference/battle-linear/source-manifest.json)、

`scripts/native/pet_texture_sampler.c`（来源工具标识）、`scripts/prepare_pet_texture_sampler.py`（来源工具标识）、[来源清单](../../assets/reference/pet-texture-sampler/source-manifest.json)、

[source-manifest.json](../../assets/reference/pet-fidelity-ui/source-manifest.json)、[source-manifest.json](../../assets/reference/pet-progress/source-manifest.json)、

[pet-ui/source-manifest.json](../../assets/reference/pet-ui/source-manifest.json)
