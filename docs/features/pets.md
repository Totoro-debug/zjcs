# 幻兽编队、养成与改名

[总索引](../README.md)

状态：未完成

## 功能简介

幻兽页管理四阵位、随行、喂养、技能、潜力、进化、能力树和改名。取得与结缘演出见[幻兽结缘](pet-gacha.md)，NPC伙伴属于独立系统。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

尚无本功能在 Unity 完整接入并运行验收的结论。原代码、配置、已转换素材和旧引擎测试仅是迁移依据，不作为本功能已还原的证据。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

本地保存原阵位、技能、能力、潜力及各季祝福。赛季切换冻结当前记录，原第7赛季未配置的曲线不编造。

服务端 `PetMaxExpCost` 写入体未取得。用户确认经验喂养成就保留所有幻兽当前喂养总经验的历史最高值；等级重置不清除最高值和领奖记录，再次喂养重新计算当前总量，未超过旧最高值不重复累计。载入和喂养时观察现存阵位／赛季投入并更新最高值，不反推已经丢失的重置历史。该单机规则仍须接入 Unity 并验证。

服务端改名次数更新体缺失：本地自定义改名消耗一次，跨原UTC零点重记；恢复默认名不返还当日次数，仅跳过外部文字审核，不加费用。

幼年列表图标 `ItemIcon/Pet/pet_1619` 重新解析固定 APK 五包完整资源路径仍未列出，原幻兽 Sprite／Texture2D 对象、相关图集及185个新版同名补包亦未取得。进一步从官方当前目录 `202610141241870` 下载清单 `870_156522` 及匹配大小／MD5 的 `image_itemicon_pet`、`image_ui_pet` 包，完整新版资源路径与两包实际图像对象仍无该图。按用户“实在没有就算了”的意见继续隐藏图片、保留名称，不绘制替代图；9219详情／随行／战斗模型正常保留。可复查入口为 `prepare_pet_ui.py --recheck-portrait`，版本、URL、输入哈希及实际对象见 [pet-ui/source-manifest.json](../../assets/reference/pet-ui/source-manifest.json)。

## 尚未实现

- 四阵位、随行、经验喂养、技能、潜力、进化、能力树、赛季祝福、改名及原模型／图标。
- 恢复官方 Unity 代码、配置、prefab、资源与调用关系，完成本地事务、实际入口和界面接线。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

[PlayerPetUtils](../../.tools/source-packages/official-code/common-source/Common/PlayerPetUtils.cs)、[PropConfigUtils.CalcPetSkillViewProps](../../.tools/source-packages/official-code/common-source/PropConfigUtils.cs)、[BattleFormulaHandler.CalcSkillProps](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)、

、[原版技能回放目录](../../assets/official/battle-animation/catalog.json)、[原版技能特效目录](../../assets/official/battle-presentation/effects.json)、[动画来源清单](../../assets/reference/battle-animation/source-manifest.json)

[特效来源清单](../../assets/reference/official-map/battle-effects-source-manifest.json)、[MyFightSkill.PlayPlayable](../../.tools/source-packages/official-code/client-source/MyFightSkill.cs)、[MyPlayableFollowBone](../../.tools/source-packages/official-code/client-source/MyPlayableFollowBone.cs)、[AttachPosRotCfg.GetBone](../../.tools/source-packages/official-code/client-source/AttachPosRotCfg.cs)、

[prepare_battle_effects.py](../../scripts/prepare_battle_effects.py)、[prepare_battle_animation.py](../../scripts/prepare_battle_animation.py)、

、[prepare_pet_materials.py](../../scripts/prepare_pet_materials.py)、

[粒子材质来源](../../assets/reference/pet-materials/source-manifest.json)、[prepare_battle_post.py](../../scripts/prepare_battle_post.py)、[prepare_battle_linear_textures.py](../../scripts/prepare_battle_linear_textures.py)、[battle-post](../../assets/reference/battle-post/source-manifest.json)

[battle-linear](../../assets/reference/battle-linear/source-manifest.json)、

、[pet_texture_sampler.c](../../scripts/native/pet_texture_sampler.c)、[prepare_pet_texture_sampler.py](../../scripts/prepare_pet_texture_sampler.py)、[来源清单](../../assets/reference/pet-texture-sampler/source-manifest.json)、

[source-manifest.json](../../assets/reference/pet-fidelity-ui/source-manifest.json)、[source-manifest.json](../../assets/reference/pet-progress/source-manifest.json)、

、[pet-ui/source-manifest.json](../../assets/reference/pet-ui/source-manifest.json)
