# 古遗物收藏、任务与培养

[总索引](../README.md)

状态：未完成

## 功能简介

家园书桌进入五元素收藏，每元素四槽。扭蛋及女神牌购买见[古遗物扭蛋](treasure-gacha.md)。

## 原始官方实现

官方古遗物包含收藏、培养、套装、赐福及战斗计数，原详情／培养／快捷操作窗口消费相应配置。

### 待迁移的规则与表现

- 原首页、四槽、元素页签、筛选、碎片、套装、详情及培养界面须接入。遗物只能放同元素，同件移槽不复制；替换保留收藏，等级投资属于槽位。
- 已激活收藏按原等级／星级和任务计数计算，陈列后加原20%槽位加成；未陈列持有物按同元素四槽最低共鸣读取。任务原 `PerCount`、生效上限和属性截断不变。
- 升星使用原品质、星级、累计碎片差额、特殊碎片和材料；重复取得按 `Agio` 返还。原六进一自动合成只扣实际不足，不制造中间领奖；觉醒开关独立，品质5普通觉醒消耗733×120。
- 通用觉醒选材窗口迁移原 `TreasureAwakeMaterialPanel`、`UseItemSliderGroup` 和 `CommonMaterial`：原布局、同品质候选、预留供体自身满星所需碎片、余量升序、数量滑条、总需求约束、一键选择、空态、确认返回及重开保留选择。入口遵循 `TreasureUpgradePanel.RefreshCommanMaterials`，仅费用包含 `-2147483555` 时显示；固定版配置不含该占位项，普通觉醒仍使用原733材料，不添加新费用。
- 元素收藏使用原 `MainTypeTitleTreasure`、`TreasurePanelTreasureSet` 和四个 `TreasureMultiLayerItem` 槽位；按品质／首个等级属性分组，标题遵循原 `_For_Treasure` 文案回退，沿用筛选／反向排序的结果顺序，每组独立换行。保留原尺寸、边距、间距、星级、觉醒状态、等级、未激活／可合成提示和对应点击行为。
- 收藏页头显示同元素实际持有数／配置总数及战斗外亲和、庇护；空态区分筛选无结果与已有陈列。列表沿用原 `LoopScrollRect` 的拖动门槛、惯性、回弹和减速参数，向下寻找可操作项按原提示优先级显示跳转按钮，并按原0.9秒定位到对应物品行。
- 持有十件且存在可操作项时显示原快捷按钮，激活优先；激活／合成后陈列同元素首个空槽，满槽不替换。
- 培养材料按原 `TreasureLevelupPanel.RefreshMaterials` 标记可合成物品：仅当材料存在可用 `ItemMerge` 来源，且八卦炉与该材料的合成功能均已解锁时，叠加原 `MergeTip` 类型9附件；沿用官方附件层级与原 prefab。
- 升星页按原 `TreasureUpgradePanelStarInfo.Refresh` 显示当前星级、下一星及空星；仅 Upgrade 状态播放目标星原 `Star_Twinkle_On` 循环动画，Wake 与 Waked 状态不闪烁。
- 培养成功后按原 `PropEffectPlayAgent.mPropItems` 顺序启动属性行效果；前后属性值来自事务前后状态。固定版 prefab 的 `mEffectInterval` 序列化值为 `0`，所以各行同帧启动；文字在原 `PlayerPropItem.myChangeTextTime` 后切换，特效按 `myEffectDuration` 结束。
- 基础槽位、当季赐福分开保存，首次赐福核对五元素二十槽陈列满级、原人物／共鸣条件和费用。原数量／赐福拆分说明、当季名和满星状态须接入。
- 任务读取真实消耗、取得来源、技能培养、装备等级、分解、日常、已结束地图实体及战斗记录。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

原梅克娜对话分支 23002、动作 12 和继承面板复用已转换固定 `854_156383` 素材，不新增自绘或替代入口。原分组构建器、条件过滤、无参数打开／等待关闭代码及节点记录见 [equipment-npc/source-manifest.json](../../assets/reference/equipment-npc/source-manifest.json)。地图内选材／确认、退出载入与关闭已接入；原 S2 资格不变，普通新档切季仍未完成。上文“NPC 入口未完成”保留为首次继承模块完成时的历史状态，以本段及最新接续记录为当前范围。

`scripts/prepare_treasure_cultivation.py` 从已校验同版 `defaultpackage_assets_prefabs_ui_treasure.bundle` 补读 7 份原培养组件字段，合并到已有 `relic_promote`／`relic_awake` 树，原图片及几何保留。原始包／对象、字段原字节哈希、客户端源码和输出指纹见 [treasure-cultivation/source-manifest.json](../../assets/reference/treasure-cultivation/source-manifest.json)。实际接入独立强化／觉醒自动合成开关、原材料递归及最大星级状态；没有新增图片。固定材料表没有通用碎片占位，品质 5 普通觉醒消耗原 120 个 733，未为展示选择器自造需求。

遗物批量 RPC 的返回结构及缺失处理器检索见 [treasure-cultivation/batch-research.json](../../assets/reference/treasure-cultivation/batch-research.json)。技能通知原 `ArrowLoop` 的两条失效路径与原 prefab 完整层级核对见 [skill-cultivation/loop-binding-research.json](../../assets/reference/skill-cultivation/loop-binding-research.json)；保留原引用及未转换记录，未补造目标。两项均为来源研究，未新增运行时素材。

地图 93 音乐节使用固定版本 `854_156383` 的活动配置、原面板、人物与武器 Spine 数据。原始绑定及输入/输出哈希见 `official-map/activity-hud-source-manifest.json`、`player-actions-source-manifest.json` 和 `music-weapons-source-manifest.json`；荧光棒按原共享武器预制体比例接到当前男角色的原持械骨骼。服务端处理器缺失，离线报名免费发放一根所选荧光棒，应援每原配置 29.9 秒使用音符奖池 73005，奖励使用原首日分组上限并持续保存、不随重进刷新；离场回到原默认入口。这些是本地执行策略。 舞台歌手、原生登场触发器和唱跳动画、动作持续时间的来源见 `official-map/music-stage-source-manifest.json`。原舞蹈状态的 64 位确定性选择器按本地演出时间执行，演出批次取该活动原通用区域（Region 0）时间表最早安排的一场并立即开始；已完成演出重新进入会重开该场，奖励上限保留。这些是离线时间表适配。 三张音乐节地图的音符与聚光灯使用本活动原事件组件，避免与其他场景同编号怪物混用；在本地随机选择原区域内的可行走格，原事件数量和奖池保留，经过物件即拾取且仅发奖一次。原音符粒子的一项共享依赖在固定版本缺失，独立使用已校验的 `864_156496` 同名包：`defaultpackage_share_534482ac5bf3346d8821a9effb2cc43c.bundle`，仅以完全相同 CAB 身份解析所需对象并核对依赖。新版来源及原缺项继续分别登记于舞台来源清单；未覆盖原缓存或修改预期哈希。 服务端路径与事件物件到期处理缺失，离线同类事件每批替换上一批物件；演出重开清理前场物件，分组奖励计数继续保留。

地图 97 音乐节使用固定版本 `854_156383` 的活动配置、原面板、人物与武器 Spine 数据。服务端处理器缺失，离线报名免费发放一根所选荧光棒，应援每原配置 29.9 秒使用音符奖池 73783，奖励使用原首日分组上限并持续保存、不随重进刷新；离场回到原默认入口。这些是本地执行策略。这些是离线时间表适配。 返场演出沿用原批次 4，第 846 秒登场的原角色 7019 使用其官方 npc_11033 骨骼和原唱跳绑定。最后一曲 BGM 214 使用固定版本原音频包 activity_025_luotianyi_2_cn 中的 902121378，原 Wwise 媒体解码为本地 Ogg；包来源、媒体偏移及输入/输出哈希见 audio-source-manifest.json。

scripts/prepare_relic_bindings.py只读固定854_156383的TreasureElementTab、TreasureDetailDialog、IntroductionPopBtn、BlessPropBtn、TMPEffect和TextMeshProUGUI原组件，按原TMP_ColorGradient引用读取GreenRainbow四色；完整包／CAB／对象。PopTipMutilDesc及序列化引用TipInfo2沿原PPtr提取，保留禁用LayoutElement的ignoreLayout与真实布局；图片逐字节复用既有官方转换，ImgInfoBG_Bless通过pet-progress/source-manifest.json核对同bundle／path_id、解码PNG输入及原生ImageTexture输出SHA256后引用。没有重绘图片或新增独立布局。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

用户确认获取／激活前的真实历史和超过当前等级上限的次数保留，升级后立即生效；不从当前等级或库存补造历史。批量培养按用户确认每件最多一星、低星优先，不觉醒、不用通用碎片。

三类服务端写入时机由用户确认本地接线：圣境当天实际参战计一次，失败也计；逐次实际命中统计角色暴击／被暴击／格挡／被格挡，未命中、免疫和治疗不计，护盾吸收仍计；普通／首领地图目标获胜各计一次。进行中事件随检查点保存，结算写历史，继续和重读不重复。

服务器开放条件采用[离线政策](offline-access.md)，材料、个人培养和赛季归属仍保留。

### 已确认的本地接线与边界

prepare_relic_text.py保留原中文rawfile、TreasureDetailDialog及IntroductionPopBtn源码哈希，生成108条原文并按原Item61替换。着色器是跨引擎适配代码，游戏颜色和描边来自固定版。快捷操作依据TreasureElementTab、TreasureHelper及Player.AutoSetupTreasure，服务端循环缺项和用户确认的处理意见另见对应功能／地图文档，不伪称取得服务端实现。

## 尚未实现

- 五元素各四槽、收藏、装备、任务历史、基础／祝福培养、觉醒、碎片和批量培养。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。

## 代码与资源入口

`scripts/prepare_treasure_cultivation.py`（来源工具标识）、`scripts/prepare_bag_items.py`（来源工具标识）、[source-manifest.json](../../assets/reference/treasure-cultivation/source-manifest.json)

[bag-items-source-manifest.json](../../assets/reference/bag-items-source-manifest.json)、

`scripts/prepare_relic_components.py`（来源工具标识）、
