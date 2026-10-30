# 图鉴分类、筛选与只读详情

[总索引](../README.md)

状态：未完成

## 功能简介

功能菜单进入图鉴书封，古遗物扭蛋书本直接进入当前系列。浏览不发奖、不改变随机状态。

## 原始官方实现

CollectBookPanel 使用原职业／国家分类、书封滚动／吸附、收藏状态和技能／古遗物详情；图标、品质、属性和套装来自对应原表。

### 待迁移的规则与表现

原 `CollectBookPanel`、书封滚动曲线、横向拖动、惯性、弹性与吸附参数须迁移；旁边书封先居中再进入，点击选中0.35秒OutCubic，吸附0.3秒InOutCubic。

技能按最高已解锁职业和前置职业分战技／秘法，共376个原配置；古遗物按国家及“其他”分组，共1382个配置，`ObtainedShow` 在取得前隐藏。总配置数不等于当前存档全开放。

原多选筛选、交集／并集、主要字段逆序、职业／国家页签和分组收集数须接入。古遗物详情用原 `TreasureDetailDialog`，按 `canOperation=false` 只读，属性取实际槽位、持有共鸣或未取得一级零星，并拆分基础／赐福。

分类使用原职业／国家卡片、四格分页、左右交错偏移和开放条件；列表按原 `MultiLayerItemPanel` 标题、子类型及三槽行生成，保留原框、品质底图、锁定／未持有灰色、底部页签和惯性／弹性滚动。筛选窗口使用原锚点、布局、自适应高度及开关动画，关闭遮罩不拦截列表点击。

技能使用原只读 `SkillDetailDialog.Init(itemId)`：一级预览、品质、职业、战技／秘法背景、标签、范围格、原描述及按配置推导的全部属性。详情采用该接口的原基础品阶；描述链接按持有品阶或未持有一级取值，不改为培养窗口。原文中的词条和召唤物链接可打开原窗口，包含168项技能／属性词条、召唤物模型、继承属性、详细属性及嵌套技能详情，返回先关闭最上层窗口。

原 `ItemNewMarkManager` 是登录内首次取得标记；退出技能列表清理该职业完整数据源中的持有技能，筛选不影响清理范围，不增加原版未绑定的可见“新”角标。返回保持书封、列表滚动或原扭蛋档位。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

按原版 `CollectBookPanel` 接入技能、古遗物两类图鉴。`python scripts/prepare_codex_assets.py` 使用固定 manifest `871_156598` 的 `prefabs_ui_collectbook`（原版本官方 CDN）、`image_ui_collectbook`、原始 ItemIcon / Map23 图标及其 CAB 依赖，转换 17 个原始 prefab 和裁去非当前页面的 shell 变体。输出 `assets/official/codex-ui.json`、`codex.json`、`codex-props.json` 及对应原始图片，实际载入 376 个玩家技能和 1,382 个古遗物条目的完整图标；未使用新版同名包或自绘补充。

职业归属来自原始 profession_base 的 Skills 和玩家 SkillUserType，元素来自 `config_ec/SkillLiteInfoPack.g.bytes`，奖池与开放条件来自现有原始规则表，套装另从 `config_luban/treasure_suit.g.bytes` 完整消费解码，收藏基础属性使用原版本 `level_prop.g.bytes`。中文文本使用已校验 WwiseBnk 两份原始中文语言表。每个 prefab 的 APK / CDN 来源、URL、bundle / 对象、图像处理方式、输入 / 输出哈希、语言凭据与原版行为源码哈希见 [图鉴来源清单](../../assets/reference/codex/source-manifest.json)；动态图片同时登记全局来源清单和 。

仅图鉴已改用完整官方图标目录；历史收藏 / 养成页的还原程度按各自记录保留。图鉴现已迁移原FancyScrollView书封惯性／吸附、技能新标记清理和古遗物真实属性推导；动态列表／分类翻页及完整技能详情仍有缺项，当前范围见[图鉴验收](codex.md)。专项验证命令：`python scripts/verify_codex_assets.py`。

缓存有效时直接复用，不重复下载。工具会校验固定 SHA256，解压所选 ZIP 条目时校验 CRC；校验失败须检查文件与来源，不可修改预期哈希绕过检查。

新增及替换素材优先从此 APK 和经过校验的原版本 CDN 缓存提取；原版本缺失时先核对新版官方同名包，适用时单独转换并记录版本与来源。上述官方来源确实缺失所需素材时，记录检索范围、资源名和缺失原因后才允许自行绘制补充，须另记绘制方式、输出路径及哈希，不能登记为官方素材；官方资源补齐后优先替换。已有资源尚未解码、转换或接入不算缺失。界面布局、交互、本地规则及必要的资源转换由项目代码实现。

图鉴列表、职业／国家分类、翻页及原技能详情由 `scripts/prepare_codex_components.py`（来源工具标识） 从同版本 prefab 生成 ，共享图片逐字节核验后复用。技能超链接按官方 `SkillHyperLinkDataProvider`、词条及怪物导航调用链迁移；[图鉴转换来源](../../assets/reference/codex-links/source-manifest.json) 提取 119 个技能可达词条及49个属性说明词条、15 个召唤物窗口／模型及属性、技能图标和嵌套窗口，来源见 [codex-links/source-manifest.json](../../assets/reference/codex-links/source-manifest.json)。运行资源在 `assets/official/codex-links/`，模型同样在开发时转换为原生网格。上述目录纳入显式资源清单及独立素材包。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

持有、任务历史和赛季状态由本地真实记录替代服务器；图鉴不增加额外奖励或属性。

## 尚未实现

- 原分类书封、分组列表、筛选与只读详情；技能词条、召唤物、嵌套技能、装备和古遗物数据按真实持有记录显示。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。

## 代码与资源入口

[TreasureDummyPropProvider](../../.tools/source-packages/official-code/client-source/TreasureDummyPropProvider.cs)、

[CollectBookSkillDetaiContent](../../.tools/source-packages/official-code/client-source/CollectBookSkillDetaiContent.cs)、[MultiLayerItemPanel](../../.tools/source-packages/official-code/client-source/MultiLayerItemPanel.cs)、[SkillDetailDialog](../../.tools/source-packages/official-code/client-source/SkillDetailDialog.cs)

[SkillHyperLinkDataProvider](../../.tools/source-packages/official-code/client-source/SkillHyperLinkDataProvider.cs)、[NonPlayerBriefInfo](../../.tools/source-packages/official-code/client-source/NonPlayerBriefInfo.cs)、`scripts/prepare_codex_components.py`（来源工具标识）、[codex-links/source-manifest.json](../../assets/reference/codex-links/source-manifest.json)、[source-manifest.json](../../assets/reference/codex/source-manifest.json)
