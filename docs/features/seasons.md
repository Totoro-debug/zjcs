# 赛季记录、赐福与装备柜

[总索引](../README.md)

状态：未完成

## 功能简介

官方赛季记录、装备继承及装备柜按赛季配置、原快照字段和只读详情界面展示，阶段和期限规则保持各自身份。

## 原始官方实现

官方赛季记录、装备继承及装备柜按赛季配置、原快照字段和只读详情界面展示，阶段和期限规则保持各自身份。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_official_seasons.py` 按固定 `854_156383` 原 Luban 构造器转换 14 张赛季依赖表、642 行，并记录服务器阶位／赛季日数条件、原 UTC 日界与发布类型的代码指纹，见 [seasons/source-manifest.json](../../assets/reference/seasons/source-manifest.json)。独立服务器阶位不按人物进度补造历史；手动切季仍待完整资格及结算实现。

`prepare_fidelity_equipment.py` 新增原继承选材详情、两种词条模板及原词条说明语言，当前共 21 个 prefab／引用模板、287 份完整组件字段和 10 份 TMP 文字记录、9 张表、101 个属性类；重复引用路径合并后为 220 组不同的路径／脚本绑定。按原 `RawIcon` 解析固定版装备图标，1223 个装备定义均有映射，来源清单包括 530 个装备图标对象，共 654 项图像／资源记录。输出仍为原 Sprite 的裁切／转换；没有自绘或混入新版。

依据：[equipment-fidelity/source-manifest.json](../../assets/reference/equipment-fidelity/source-manifest.json)。

继承使用原帮助函数的条件式宝石复制，目标已有宝石时保留目标；本地对未转移的来源宝石做等量返还，对销毁的已穿戴来源清除引用。这两项没有服务端实现证据，按离线适配登记，不声称已还原原 `AwardData`。NPC 对话入口、成功奖励演出及完整动态附件仍未完成；

独立 `season-lifecycle/source-manifest.json` 记录固定 `854_156383` 的配置、客户端代码、原组件绑定、语言和图像来源；运行数据为 `season-lifecycle.json`、`season-lifecycle-ui.json`，现含 11 个 prefab（9 个既有原布局及 2 个原规则等级行）。共享接入逐路径校验通知清单的 69 条输出 SHA，并通过固定 APK / 已校验原 CDN 解析 67 张 PNG 对应的原 bundle、MD5、Sprite 对象名及 path_id。3 张既有 PNG 复用原中央凭据，64 张原玩法图标首次新增中央来源和显式绑定，另登记两个独立 JSON。

这 64 张 PNG 及其 `.import` 当时未包含在 `d7dd2730b6af88e0` 素材包；此前一次通知误称已入包，已更正。实际新增集合和修正记录保存在 `season-lifecycle/registration-history/registered-v1.json`，应按新增 64 张 PNG、64 份 `.import`、两个 JSON 与实际来源历史统计，不能称仅新增两个 JSON。来源快照和输出哈希保留于该历史目录；这些文件已纳入 2026-10-16 的当前素材包。

固定 `854_156383` 的 `ItemIconAttachmentConfig.asset` 中，Type 2 唯一指向原 GUID `9717aac89f12f8c4bb521b6b3f66a83a` 及 `Assets/Prefabs/UI/Item/Attachments/GotObj.prefab`；配置及 prefab 均来自原 APK。新 `season_reward_got_tip` 保留原三节点、inactive 根、RectTransform、颜色和 `ItemIconAttachmentCell.mLayer=7`，没有绘制替代图。原 `ImgQuality_Mask-49ff34b468.png` 与 `ImgTick-20d85e1c96.png` 已有中央来源、显式绑定和导入，只新增季节模块的两条同字节引用；原 67 张 PNG 不变，实际新增 PNG 为 0，共 69 张 PNG 和两个 JSON、71 条既有显式路径。

原语言 rawfile 哈希未变。此前已存在的 56 项完整保留，补入 211 项原中文，独立目录现为 267 项；此前检索的其余 3 项已存在，6 项未用值／示例键的缺失说明继续保留，未补造语言。只更新 `season-lifecycle.json` 与 `season-lifecycle-ui.json` 的中央数据哈希，并登记独立来源 `season-lifecycle/source-manifest.json`；复用 PNG 的中央凭据及生成绑定原字节保留。完整 GUID／APK 路径、原 bundle／对象及输出 SHA 见独立来源，旧来源原字节继续位于 `history/source-manifest-e0e5c90bdc54db8abc34b9c5f8bfb1eab6bc7d18da54a4c2d320e1c057fca750.json`。

这次登记记录静态路径、实际文件哈希和原语言；完整玩法状态见功能文档。

`python scripts/prepare_season_cabinet_ui.py` 从已校验原版 `prefabs_ui_equip` 的 EquipCabinetEquipDetailPanel、EquipCabinetPropItemGroup、EquipCabinetPropRuleDialog、EquipCabinetRuleFormDataItem 和 EquipCabinetContent 转换层级、RectTransform、状态与组件绑定；沿实际 mOtherPropPrefab、mTipInfoPrefab 指针取得 EquipMutillPropItem 和原说明行，并读取原 prefabs_ui_tips／prefabs_ui_itemdetail 的祝福、套装、等级及技能说明。10 个模板、325 条对象／布局输入、87 项既有原资源引用、18 份客户端／公共代码 SHA256 与两份原语言输入登记在 。该文件只包含代码使用的原元数据及来源，不保存验收结果；所有贴图载荷复用已登记文件，提取器逐项核对原像素和文件哈希，不写入新素材。

评分及快照来源为 EquipCabinetPanel／EquipCabinetGridItem／EquipCabinetDetailPanel、PlayerEquipCabinetDataExt、EquipCabinetEvaluateInfoParser；属性行和规则来源为 PropGroupContent／EquipCabinetPropGroup／EquipCabinetPropRuleItem。品质文字按原 GridItem 对 TextColor 的隐式 Color 转换读取普通色，未擅自启用 GradientColor。全屏显示读取原 AstrologicalDestinyPanel 的 PopupWindow.mFullScreen，并参照 AutoHideBuiltInUIViaWindowManagerCounter，避免本地主城货币栏覆盖原总分。动态祝福／套装文字直接取原语言分包，属性枚举来自原 PropType.cs，GreenRainbow 四角颜色来自原 TMP 预设并保留对象及 SHA256。没有新增自绘、替代布局或新版来源；原位图、素材 ZIP 和锁文件的内容保持不变。

## 完整还原部分

尚无完整 Unity 流程的完成结论。

## 自定义实现

开放与本地保存遵循[共同离线政策](offline-access.md)，具体本地接线和未确认规则见下文。

## 尚未实现

接通官方代码、原生界面／资源、本地状态与存档，完成实际入口、模拟输入、画面及失败恢复验证，并发布对应 Windows 版本。
