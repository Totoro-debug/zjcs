# 单人公会领地

[总索引](../README.md)

状态：未完成

## 功能简介

底栏公会按原15级条件进入单人领地，保留个人建设、研究、生产、商店、首领及本地 NPC 公会战。

## 原始官方实现

原公会包含成员、领地、建筑及关联玩法；官方建筑和属性表、原公会界面是单人范围迁移依据。

### 待迁移的规则与表现

原建筑坐标、碰撞、镜头、拖动与点击入口接入；名称／徽章采用原改名卡优先及晨星费用。捐赠使用原五档，每日五次，奖励及材料取原表。

建设、研究、生产按原前置、费用、进度、占地、时长、照料和工具执行；图书馆属性进入人物和战斗。库存按原产速／容量，商店按建筑、价格与日／累计限购执行。

公会首领读取 `stage_legionboss` 层数对应的原关卡（4101 起，关卡类型 `LegionBoss`），按 `MonsterType.NormalBoss` 识别并承载跨次剩余生命。挑战进入本地逐步实体战斗；次数、单场伤害、击败状态、参与奖励、击杀／首通奖励与待领取状态在战斗完成后一起结算，领取前不能再次挑战。

公会战继续使用原10名成员／300活跃度报名门槛，本地招募生成 NPC 成员以满足单人存档条件；对手公会与防守结算在本地模拟，不连接真人或在线服务。每次 NPC 公会战实际进攻记录到 Type 28 `AchievementTaskLegionFightJoinBattleCount`。

首领页使用原首领模型与 Spine、首领生命条和首通奖励；规则按钮打开原 `LegionBossInfoDialog` 并展示 `LegionBossJoinReward`／`LegionBossKillReward` 对应奖励。结算使用原 `GameEndPanelLegionBoss`，展示本层首领实体、单场伤害及分开的参与／击杀奖励。当前层 4101 的首领实体为 3601。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

第二轮完整 UI 迁移：`python scripts/prepare_guild_assets.py` 从固定 APK 与同版本 `871_156598`、具 MD5 / SHA256 凭据的官方 CDN 缓存恢复 213 个公会、领地及关联公共 prefab，包括实际运行时池中的列表项、研究节点 / 连线、工具、排行、聊天、确认框和三种公会专用战斗结算面板。第一轮 33 个外壳与通用内容布局属于历史实现，已替换。

`python scripts/prepare_guild_scene.py` 转换原始 `LegionMap` 的 188 个 Sprite 实例、32 个 Mesh 和 16 组 Spine 数据，保留十二座建筑的位置、点击碰撞区域、镜头聚焦 / 拖动参数、24 个建筑名称和等级标签，以及未建造、施工、正常状态。生产使用三个原始世界 Canvas 背景、九个原始生产物件视图和骨骼；另有 10 组 UI Spine 数据。运行数据为 `assets/official/guild-ui.json`、`guild.json`、`guild-ui-spine.json` 和 `guild-scene.json`，全部为原素材解码 / 网格转换，没有自行绘制或生成图片。

完整消费 25 张 `legion_*` / `stage_legionboss` 表及 `achievement`，共 26 张，保留原始费用、建筑 / 研究前置、速率、容量、生产占地、工具、商品、首领和战斗奖励。公会馈赠三个分组保留固定档位和循环增量。所需 440 项配置图标均取得。bundle / 对象路径、输入版本与哈希、中文语言分包及输出哈希见 [guild-ui/source-manifest.json](../../assets/reference/guild-ui/source-manifest.json)；场景、镜头、碰撞区域和世界物件见 [guild-ui/scene-manifest.json](../../assets/reference/guild-ui/scene-manifest.json)。全局来源和显式加载清单已纳入资源，可运行 `python scripts/verify_guild_assets.py` 单独复核。

公会成员、申请、其他公会和留言回应使用本地 NPC。服务器公会战周排期改为每天一轮的准备 / 战斗 / 结算，赛季每周结算；战斗复用本地模拟器，生活防守使用本地方向交互。领地地图按原始横向拖动和建筑入口操作，联网匹配、Elo 和实时多人通信采用离线玩法适配。历史“公会专有包未取得、通用菜单”记录保留为旧阶段状态。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

按明确单机范围不连接真人公会或在线匹配；公会战使用本地 NPC 成员与对手，报名仍检查原始10人及300活跃度门槛，成员由本地招募补齐。公会战进攻、NPC防守、结算及赛季奖励在本地保存；全服公会榜不生成。公会首领各项怪物属性按用户确认缩至原值四分之一。默认领地不收旧模拟创建费，原虚构记录归档但不重置共享余额和领奖标记。

其他页面持续计时，离线最多补八小时，超容量旧库存保留且容量以下前不新增；回拨不倒退时间。服务端公会首领共享血量映射到本地单人存档；费用、生产、随机、奖励和进度在同一事务提交。

## 尚未实现

- 单人领地的建筑、生产、招募、跨次首领血量及原经营界面；按已确认范围接入NPC公会战、报名、攻防与奖励。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。

## 代码与资源入口

[AchievementTaskLegionFightJoinBattleCount](../../.tools/source-packages/official-code/common-source/Common/AchievementTaskLegionFightJoinBattleCount.cs)、[AchievementTaskLegionBossBattleCount](../../.tools/source-packages/official-code/common-source/Common/AchievementTaskLegionBossBattleCount.cs)、[AchievementTaskLegionBossProgressTop](../../.tools/source-packages/official-code/common-source/Common/AchievementTaskLegionBossProgressTop.cs)、
