# 单人公会领地

[总索引](../README.md)

状态：未完成

## 功能简介

原公会包含成员、领地、建筑及关联玩法；官方建筑和属性表、原公会界面是单人范围迁移依据。

## 原始官方实现

原公会包含成员、领地、建筑及关联玩法；官方建筑和属性表、原公会界面是单人范围迁移依据。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

第二轮完整 UI 迁移：`python scripts/prepare_guild_assets.py` 从固定 APK 与同版本 `854_156383`、具 MD5 / SHA256 凭据的官方 CDN 缓存恢复 213 个公会、领地及关联公共 prefab，包括实际运行时池中的列表项、研究节点 / 连线、工具、排行、聊天、确认框和三种公会专用战斗结算面板。第一轮 33 个外壳与通用内容布局属于历史实现，已替换。

`python scripts/prepare_guild_scene.py` 转换原始 `LegionMap` 的 188 个 Sprite 实例、32 个 Mesh 和 16 组 Spine 数据，保留十二座建筑的位置、点击碰撞区域、镜头聚焦 / 拖动参数、24 个建筑名称和等级标签，以及未建造、施工、正常状态。生产使用三个原始世界 Canvas 背景、九个原始生产物件视图和骨骼；另有 10 组 UI Spine 数据。运行数据为 `assets/official/guild-ui.json`、`guild.json`、`guild-ui-spine.json` 和 `guild-scene.json`，全部为原素材解码 / 网格转换，没有自行绘制或生成图片。

完整消费 25 张 `legion_*` / `stage_legionboss` 表及 `achievement`，共 26 张，保留原始费用、建筑 / 研究前置、速率、容量、生产占地、工具、商品、首领和战斗奖励。公会馈赠三个分组保留固定档位和循环增量。所需 440 项配置图标均取得。bundle / 对象路径、输入版本与哈希、中文语言分包及输出哈希见 [guild-ui/source-manifest.json](../../assets/reference/guild-ui/source-manifest.json)；场景、镜头、碰撞区域和世界物件见 [guild-ui/scene-manifest.json](../../assets/reference/guild-ui/scene-manifest.json)。全局来源和显式加载清单已纳入资源，可运行 `python scripts/verify_guild_assets.py` 单独复核。

公会成员、申请、其他公会和留言回应使用本地 NPC。服务器公会战周排期改为每天一轮的准备 / 战斗 / 结算，赛季每周结算；战斗复用本地模拟器，生活防守使用本地方向交互。领地地图按原始横向拖动和建筑入口操作，联网匹配、Elo 和实时多人通信采用离线玩法适配。历史“公会专有包未取得、通用菜单”记录保留为旧阶段状态。

## 完整还原部分

尚无完整 Unity 流程的完成结论。

## 自定义实现

开放与本地保存遵循[共同离线政策](offline-access.md)，具体本地接线和未确认规则见下文。

## 尚未实现

接通官方代码、原生界面／资源、本地状态与存档，完成实际入口、模拟输入、画面及失败恢复验证，并发布对应 Windows 版本。
