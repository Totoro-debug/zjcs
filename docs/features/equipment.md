# 装备、强化、继承与星石

[总索引](../README.md)

状态：未完成

## 功能简介

人物五个装备槽和行囊装备分类进入候选、详情、比较、强化、继承、星石及碎片合成。

Unity 已接入角色五槽及行囊装备操作、实例存档事务、装备属性来源和官方装备 UI 数据载荷。运行界面目前由角色页节点工厂组合面板，尚未恢复装备 prefab 的完整原始坐标、动画音效、强化粒子及继承奖励表现；尚未完成 Unity 编辑器编译和 Play Mode 交互验收，因此模块仍未完成。

## 原始官方实现

官方装备由五部位实例、主副属性、词条、品质、套装、祝福、强化、继承、星石与碎片配置组成；随机实例生成和属性换算以 EquipInfo、EquipAttributeInfoParser 及对应共享公式为依据。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_fidelity_equipment.py` 从原 promotepanel / character 包转换 18 个 prefab／引用模板，严格读取 213 组字段绑定、9 张原表及 101 个等级属性类；七种 EquipQualityBg 按 LanguageUtils 的资源名和原图子目录定位。强化、锁定和分解规则已接入；继承、宝石、共鸣等面板当前仅完成转换，未全量接入。新装备按原权重及参数生成，普通／特殊／被动词条与互斥、套装已参与属性与战力；旧实例保留兼容值。继承、镶嵌、完整说明及后期祝福仍待完成，不以素材转换宣称装备完整一致。

依据：[equipment-fidelity/source-manifest.json](../../assets/reference/equipment-fidelity/source-manifest.json)。

记录见非主线实施证据。没有新增自绘素材。

上述 21 个模板和 297 份输入记录是继承模块完成时的范围。继续从同一固定版本提取 `prefabs_ui_itemdetail` 的原 `GemDetailDialog`，追踪其完整组件字段及 TMP 换行、溢出和边距；原语言补齐星石说明及镶嵌部位。当前输出为 22 个 prefab／引用模板、295 份完整组件字段、68 份 TMP 输入记录（58 个不同文字路径）、228 组不同路径／脚本绑定、655 项资源记录及 58 份原代码指纹。原星石图标全部复用已转换同版素材，未增加自绘或新版来源。字段输入与去重后绑定数量分别登记，不合并为一个夸大的“绑定数”。

依据原 `GemPartInfoParser`、`GemPropChunck`、`PlayerEquipDataExt`、`EquipLevelCondition`、候选比较器与镶嵌界面实现；360 个固定版本星石均使用特殊属性组首组。 接入原槽位／列表、短按镶嵌、一秒长按专用详情、移动取消和外点击。卸下时返还一枚原星石是服务端 `AwardData` 缺失时的离线所有权适配，不标为已取得原服务端返还实现；未补造手续费、额外奖励或材料返还。

地图 94 活动首领使用固定版本 `854_156383` 的原 Luban 配置、既有官方怪物及面板。`scripts/prepare_map_activity_boss.py` 解码原七张表，运行数据按已提交地图筛选；各原始表完整消费字节数、bundle 及输入哈希、筛选范围和输出哈希见 `assets/reference/official-map/activity-boss-source-manifest.json`。

地图 96 活动首领使用固定版本 `854_156383` 的原 Luban 配置、既有官方怪物及面板。

地图 98 活动首领使用固定版本 `854_156383` 的原 Luban 配置、既有官方怪物及面板。

地图 95 活动首领使用固定版本 `854_156383` 的原 Luban 配置、既有官方怪物及面板。

地图 92 音乐节使用固定版本 `854_156383` 的活动配置、原面板、人物与武器 Spine 数据。原始绑定及输入/输出哈希见 `official-map/activity-hud-source-manifest.json`、`player-actions-source-manifest.json` 和 `music-weapons-source-manifest.json`；荧光棒按原共享武器预制体比例接到当前男角色的原持械骨骼。服务端处理器缺失，离线报名免费发放一根所选荧光棒，应援每原配置 29.9 秒使用音符奖池 73005，奖励使用原首日分组上限并持续保存、不随重进刷新；离场回到原默认入口。这些是本地执行策略。 固定表已退休地图 92 的迎宾绑定，本地仅对原 TrackTag 重接仍保留的 MusicPartyWelcome1/2 原图。

本会话剧情挂点使用固定版原 StoryNodeRegister / StoryNodeDelayRegister，来源、原坐标、父级变换及输出指纹见 official-map/story-bindings-source-manifest.json。剧情演出的既有官方 Timeline、MyPlayable、角色与插图转换依赖随地图 12 提交，来源分别见 timelines、playables、story-actors、story-media、ui-spines 来源清单；未补造挂点或插图。

星系 UI 测试使用的原星系层级、故事书、插图、彩蛋和原面板依赖，来源分别见 official-map/galaxy-source-manifest.json、ui-source-manifest.json 及 ui-spines-source-manifest.json。运行时只加载显式清单中的本地转换资源。

地图音频测试所依赖的原 Wwise 音乐分支、桥段及混音资源，输入、工具版本、实际输出与哈希见 official-map/audio-source-manifest.json 和 music-source-manifest.json。混音在开发时完成，运行时离线播放本地资源。

音乐踏板使用原 ECEntityOverlapDisplayView.mDisplaySwitcher 的占据状态及官方粒子，未绘制高亮替代图。按实际使用的踏板 prefab 筛选输出，来源和转换指纹见 official-map/particles-source-manifest.json；不代表全部世界粒子已接入。

`prepare_fidelity_equipment.py` 从固定版 `defaultpackage_assets_prefabs_ui_itemdetail.bundle`（MD5 `ed61998dccf8c8b2451eb16f49b433cb`）补充原 `Assets/Prefabs/UI/ItemDetail/EquipMutillPropItem.prefab`，GameObject path_id `-120004648565983737`。沿用 `EquipPropPanel.MainPropItem`、`SecPropItem`，以 `OtherPropGroup` 中逐条实例化的原模板显示附加属性，保留原状态切换、文字边距、加号设置、行距与交替底色；没有新增自绘。当前来源清单共 23 个 prefab／引用模板、231 组路径／脚本绑定、61 份 TMP 文字记录及 655 项资源记录。`EquipPropPanel`、`EquipMutillPropItem` 与装备候选／详情源码和完整输入输出哈希见 [equipment-fidelity/source-manifest.json](../../assets/reference/equipment-fidelity/source-manifest.json)。主、副属性和附加值分别依照原 `BattleFormulaHandler` 计算，不再将同类型附加值并入主词条显示。

`prepare_equipment_completion.py` 从固定 APK／匹配 `854_156383` 的 Character、ItemDetail、PromotePanel、Equip、Tips、ConfirmDialog 与 ItemIconAttachmentConfig 提取 16 个真实窗口／引用模板，沿原 PPtr 取得星石行、等级说明行及类型 13 的已拥有附件。保留原同名兄弟节点的对象引用、RectTransform、网格、布局、自适应高度、PopupWindow 遮罩、状态、TMP 字段与 TMPEffect 描边；没有绘制替代素材。真实 bundle、对象、语言、原 C#、输入输出 SHA256 与完整 276 行等级差表来源见 `assets/reference/equipment-completion/source-manifest.json`。脚本严格校验 TMP 字段、64 字节 AutoAlign 序列化范围和原始表输入，不把 SafeAction 当作 Unity 序列化字段。此清单由转换脚本生成；先提交不冲突代码，共用素材登记待锁交接。

`prepare_equipment_completion.py` 按固定 `854_156383` APK／匹配清单的 CDN 资源，转换装备详情与比较、碎片详情及候选、祝福／套装提示、确认框、技能词条详情、装备星石属性行、等级提示及说明行。16 个原 prefab／模板、转换图像、组件字段、Prefab 来源、表格及调用源码哈希登记于 [equipment-completion/source-manifest.json](../../assets/reference/equipment-completion/source-manifest.json)；运行数据写入 `assets/official/ui.json`、`fidelity-rules.json` 和 `fidelity-texts.json`。转换没有绘制替代界面或图片。

`prepare_equipment_inherit_feedback.py` 按同一固定版本读取 `EquipInheritPropItem`、其原粒子系统与 `UIParticle` 配置，以及 `ItemIconAttachmentConfig` 引用的六种原挂件 prefab；将六个粒子发射器、六种奖励挂件和关联的原纹理写入 `assets/official/equipment-feedback/catalog.json` 及正式素材目录。[equipment-feedback/source-manifest.json](../../assets/reference/equipment-feedback/source-manifest.json) 保存原 bundle／对象来源、输入输出哈希、附件配置和客户端调用源码哈希。Godot 在继承属性行和奖励图标上复用这些原资源，不生成替代特效或挂件。

Unity 规则载荷由 `scripts/prepare_unity_equipment_catalog.py` 从上述固定版规则投影生成，仅保留装备运行所需表，不重复携带全局规则文件。`unity/Assets/Official/Equipment/source-manifest.json` 记录三份来源清单哈希及输出路径；`Runtime/source-manifest.json` 记录精简规则表 SHA256、输入 SHA256 和保留的表名。

同一工具还恢复装备、强化、继承、星石、详情及碎片选择的 39 个原 prefab 层级投影和 132 张来源图片到 `unity/Assets/Official/Equipment/UI/`。`UI/source-manifest.json` 记录原 prefab 来源身份、逐图哈希、实际使用的 UI 汇总 SHA256 及既有 `equipment-completion/source-manifest.json` 中的历史汇总预期值。当前 `assets/official/ui.json` 汇总 SHA256 与该历史预期不一致；不改写历史哈希，当前迁移范围通过固定版逐 prefab 身份与原图输入哈希检查。独立 UI 输出和 Unity `.meta` 可由 `prepare_unity_equipment_catalog.py` 复现，并由 `Zjcs.Editor.Equipment.OfficialEquipmentValidation.Validate` 核验。该载荷尚未恢复成可操作的 Unity prefab/窗口，也未完成 Unity 编辑器运行验收；完整范围见[装备、强化、继承与星石](equipment.md)。Unity 官方载荷通过 [unity_asset_pack.py](../../scripts/unity_asset_pack.py) 纳入独立载荷快照。

## 完整还原部分

`scripts/prepare_unity_equipment_catalog.py` 校验固定 `854_156383` 规则来源并恢复运行所需表。`OfficialEquipmentCatalog` / `OfficialEquipmentAssetCatalog.Load` 校验版本、输出长度、SHA256 与关键表；编辑器入口为 `Zjcs.Editor.Equipment.OfficialEquipmentValidation.Validate`。

`OfflineEquipmentProfile` 为五部位保留独立实例 ID、实例词条、星石、锁定状态、穿戴关系和各部位基础/赛季祝福强化状态。`OfflineMainlineProgressSession.ApplyCharacterMutation` 对装备数据与玩家背包使用同一存档副本及原子写盘，旧存档缺少装备字段时初始化为空装备档案。

`OfflineEquipmentOperations` 已接入穿戴职业检查、部位强化报价和材料事务、星石候选/镶卸、碎片职业过滤与部位排序、碎片实例生成回调、装备分解限制与原奖励/星石返还、以及继承属性事务。强化和祝福限制读取原 `system_level_limit`、`system_level_gap_limit` 与材料表；星石条件由调用方按原条件对象求值；继承重掷由调用方传入官方生成回调，以便单机存档事务整体回滚。

角色页的 `CharacterEquipSlots` 从五个原角色槽与背包调用穿戴、卸下、锁定、分解、强化、星石、继承、碎片合成操作；详情、候选排序、比较与操作状态绑定角色页。`OfflineEquipmentPropSource` 接入主副属性、普通/特殊词条、星石普通与特殊属性、祝福强化、套装阈值及装备共鸣；普通星石按原 `GemPartInfoParser.GetGemProp` 使用物品阶位和属性类型查 `level_prop`，特殊星石按 `EquipAttributesPropsInfo.GetGemSpecialProp` 取 `Props[0]`。有效词条按 `ExclusiveGroup`/`SelfExclusive` 比较战力后择优；`CalculateCombatRating` 已计算装备基础属性、套装评分、普通/特殊词条品质与祝福缩放。被动特殊词条的技能实体属性转换尚未合入战斗角色属性。角色属性页和战斗属性读取同一来源，分解/继承/镶卸/强化仍由 progression 存档事务提交。

`OfficialEquipmentGenerator` 按固定版 `EquipInfo.GenerateItemParams` 与 `EquipAttributeInfoParser` 的已还原数据迁移掉落等级、祝福等级、主副属性类型权重、随机词条类型/互斥、品质与阶位过滤、被动品质及套装抽取。代码依据为 `.tools/source-packages/official-code/common-source/Common/PlayerEquipDataExt.cs`、`EquipInfo.cs`、`EquipAttributeInfoParser.cs`、`EquipUpgradeMatInfoParser.cs` 和客户端 `EquipPromotePanelUpgradePanel.cs`、`EquipInheritSelectContent.cs`、`EquipSelectPanel.cs`。

Unity 官方载荷中有 39 个装备、强化、继承、星石、详情与碎片 prefab 层级目录和 132 张对应原图；由 `prepare_unity_equipment_catalog.py` 从已转换官方目录恢复，`Assets/Official/Equipment/UI/source-manifest.json` 记录面板、来源身份、输入/输出哈希与图片路径。编辑器校验会检查目录结构及原图导入/哈希。此处目前只完成原始 UI 数据和图片载荷，不表示已生成可操作 Unity 界面。

## 自定义实现

副本最终评级奖励读取 `dungeons-runtime.json` 的 `gradeRewards`、`rewardRules`：按官方条件选中当前评级行，并遵照客户端 `RewardRuleInfoParser` 的 `Percent × Weight / WeightTotal` 逐候选概率和数量结算；房间惊喜奖励读取房间配置引用的同一套规则。完整装备奖励的 `ItemType=110` 是官方随机装备占位项，`randomEquipmentPools` 由 `random_reward.IdList` / `WeightOfIdList` 导出为带权实际装备 ID 池；装备实例通过 `OfficialEquipmentGenerator.Generate` 按所选装备和官方生成逻辑创建。实例在一次性 `ApplyInventoryTransaction` 中写入装备档案；等级和祝福等级应用对应副本地图组、难度的官方区间，掉落来源使用 `SourceOperate.DailyDungeonReward = 61`。装备碎片等普通道具按库存事务发放。服务端领奖响应中的 `EquipItemData` 未下发生成参数，本地使用同一官方装备配置生成实例；提交后的重复领奖由 receipt 去重，不重掷。

星石卸下按现有 Godot 离线适配返还同一颗已拥有星石，不添加费用、升级或替代奖励。赛季装备柜的记录和只读详情见[赛季](seasons.md)；远古砥石的词条替换、自动锁定、分解与期限处理见[行囊](inventory.md)。

## 尚未实现

- 被动技能词条的品质、索引、`EquivalentProps` 战力估值及祝福倍率已接入；实际战斗效果仍缺。官方 `BattleFormulaHandler.CalcEquipTermExchangeProps` 按 `EquipAttributesPassiveSkillInfo.SkillId` 查技能实体，以 `PropsFactors`/品质计算技能属性，用 `SkillPropMapFactorIds` 做祝福缩放，再应用对应 `FightStatusPropComponent.TransitionPropCfgs` 将属性转换到玩家战斗属性。当前战斗组装只有角色自带被动技能，尚未把装备被动特殊词条转换加到玩家属性。
- 继承事务已执行来源/目标实例更新并保存，但成功后界面只关闭弹窗并刷新装备槽；原继承结果页逐条显示接收属性、替换属性和首项奖励装备实例的表现尚未接入。
- 当前交互面板使用已恢复节点模板组合，没有直接恢复装备原 prefab 完整坐标/锚点、字体和层级；尚缺星石长按原详情、部分重复合成确认及原材料不足/取消状态表现。
- 恢复 `Effect_ui_Upgrade` 六个粒子系统、目标词条顺序、六类原奖励挂件与原按钮/动画音效；检查 Android prefab/bundle 与编译 shader 的 Windows 兼容性。
- 当前 `assets/official/ui.json` 汇总 SHA256 与历史 `equipment-completion` 输出清单记录值不一致；逐面板 prefab 身份和图片来源哈希已核验，仍需独立从原始 bundle 重建 UI 投影并核对实际汇总哈希。
- 在 Unity 编辑器执行装备及 UI 载荷校验、编译并实际运行界面；覆盖正常操作、存档恢复、失败回滚、原资源绑定、动画/音频及不同窗口尺寸下的鼠标与键盘交互。
