# 古遗物收藏、任务与培养

[总索引](../README.md)

状态：未完成

## 功能简介

官方古遗物包含收藏、培养、套装、赐福及战斗计数，原详情／培养／快捷操作窗口消费相应配置。

## 原始官方实现

官方古遗物包含收藏、培养、套装、赐福及战斗计数，原详情／培养／快捷操作窗口消费相应配置。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

原梅克娜对话分支 23002、动作 12 和继承面板复用已转换固定 `854_156383` 素材，不新增自绘或替代入口。原分组构建器、条件过滤、无参数打开／等待关闭代码及节点记录见 [equipment-npc/source-manifest.json](../../assets/reference/equipment-npc/source-manifest.json)。地图内选材／确认、退出载入与关闭已接入；原 S2 资格不变，普通新档切季仍未完成。上文“NPC 入口未完成”保留为首次继承模块完成时的历史状态，以本段及最新接续记录为当前范围。

`scripts/prepare_treasure_cultivation.py` 从已校验同版 `defaultpackage_assets_prefabs_ui_treasure.bundle` 补读 7 份原培养组件字段，合并到已有 `relic_promote`／`relic_awake` 树，原图片及几何保留。原始包／对象、字段原字节哈希、客户端源码和输出指纹见 [treasure-cultivation/source-manifest.json](../../assets/reference/treasure-cultivation/source-manifest.json)。实际接入独立强化／觉醒自动合成开关、原材料递归及最大星级状态；没有新增图片。固定材料表没有通用碎片占位，品质 5 普通觉醒消耗原 120 个 733，未为展示选择器自造需求。

遗物批量 RPC 的返回结构及缺失处理器检索见 [treasure-cultivation/batch-research.json](../../assets/reference/treasure-cultivation/batch-research.json)。技能通知原 `ArrowLoop` 的两条失效路径与原 prefab 完整层级核对见 [skill-cultivation/loop-binding-research.json](../../assets/reference/skill-cultivation/loop-binding-research.json)；保留原引用及未转换记录，未补造目标。两项均为来源研究，未新增运行时素材。

地图 93 音乐节使用固定版本 `854_156383` 的活动配置、原面板、人物与武器 Spine 数据。原始绑定及输入/输出哈希见 `official-map/activity-hud-source-manifest.json`、`player-actions-source-manifest.json` 和 `music-weapons-source-manifest.json`；荧光棒按原共享武器预制体比例接到当前男角色的原持械骨骼。服务端处理器缺失，离线报名免费发放一根所选荧光棒，应援每原配置 29.9 秒使用音符奖池 73005，奖励使用原首日分组上限并持续保存、不随重进刷新；离场回到原默认入口。这些是本地执行策略。 舞台歌手、原生登场触发器和唱跳动画、动作持续时间的来源见 `official-map/music-stage-source-manifest.json`。原舞蹈状态的 64 位确定性选择器按本地演出时间执行，演出批次取该活动原通用区域（Region 0）时间表最早安排的一场并立即开始；已完成演出重新进入会重开该场，奖励上限保留。这些是离线时间表适配。 三张音乐节地图的音符与聚光灯使用本活动原事件组件，避免与其他场景同编号怪物混用；在本地随机选择原区域内的可行走格，原事件数量和奖池保留，经过物件即拾取且仅发奖一次。原音符粒子的一项共享依赖在固定版本缺失，独立使用已校验的 `864_156496` 同名包：`defaultpackage_share_534482ac5bf3346d8821a9effb2cc43c.bundle`，仅以完全相同 CAB 身份解析所需对象并核对依赖。新版来源及原缺项继续分别登记于舞台来源清单；未覆盖原缓存或修改预期哈希。 服务端路径与事件物件到期处理缺失，离线同类事件每批替换上一批物件；演出重开清理前场物件，分组奖励计数继续保留。

地图 97 音乐节使用固定版本 `854_156383` 的活动配置、原面板、人物与武器 Spine 数据。服务端处理器缺失，离线报名免费发放一根所选荧光棒，应援每原配置 29.9 秒使用音符奖池 73783，奖励使用原首日分组上限并持续保存、不随重进刷新；离场回到原默认入口。这些是本地执行策略。这些是离线时间表适配。 返场演出沿用原批次 4，第 846 秒登场的原角色 7019 使用其官方 npc_11033 骨骼和原唱跳绑定。最后一曲 BGM 214 使用固定版本原音频包 activity_025_luotianyi_2_cn 中的 902121378，原 Wwise 媒体解码为本地 Ogg；包来源、媒体偏移及输入/输出哈希见 audio-source-manifest.json。

scripts/prepare_relic_bindings.py只读固定854_156383的TreasureElementTab、TreasureDetailDialog、IntroductionPopBtn、BlessPropBtn、TMPEffect和TextMeshProUGUI原组件，按原TMP_ColorGradient引用读取GreenRainbow四色；完整包／CAB／对象。PopTipMutilDesc及序列化引用TipInfo2沿原PPtr提取，保留禁用LayoutElement的ignoreLayout与真实布局；图片逐字节复用既有官方转换，ImgInfoBG_Bless通过pet-progress/source-manifest.json核对同bundle／path_id、解码PNG输入及原生ImageTexture输出SHA256后引用。没有重绘图片或新增独立布局。

## 完整还原部分

尚无完整 Unity 流程的完成结论。

## 自定义实现

开放与本地保存遵循[共同离线政策](offline-access.md)，具体本地接线和未确认规则见下文。

### 已确认的本地接线与边界

prepare_relic_text.py保留原中文rawfile、TreasureDetailDialog及IntroductionPopBtn源码哈希，生成108条原文并按原Item61替换。着色器是跨引擎适配代码，游戏颜色和描边来自固定版。快捷操作依据TreasureElementTab、TreasureHelper及Player.AutoSetupTreasure，服务端循环缺项和用户确认的处理意见另见对应功能／地图文档，不伪称取得服务端实现。

## 尚未实现

接通官方代码、原生界面／资源、本地状态与存档，完成实际入口、模拟输入、画面及失败恢复验证，并发布对应 Windows 版本。
