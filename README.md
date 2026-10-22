# 杖剑传说离线版

Windows 10/11 桌面单机项目，使用 Unity / C# 迁移原作，保留原作的 2D 表现与竖屏布局，运行时无需联网。优先复制或转换官方原始代码与资源；多人／真人玩法、联网流程及服务器决策按已确认的单机范围实现，界面、布局与动画优先恢复官方 Unity 内容。

Unity 开发入口为 [unity/](unity/)，固定版本 **Unity 2022.3.57f1**。原版迁移已获授权并按模块进行，优先采用官方代码与资源，并遵循已确认的单机适配；当前 Unity 完成范围和缺项见[工程与启动流程](docs/features/unity-project.md)。

现有 Windows 成品位于 `D:\Desktop\杖剑传说\`，在完整 Unity 游戏整体验证前保留成品及桌面快捷方式。日常按[受影响范围](docs/asset-management.md#日常功能验收)检查，全量素材 ZIP 仅用于集中备份与恢复。

- [项目文档索引](docs/README.md)
- [功能验收](docs/README.md#功能验收)
- [单机开放条件与取舍](docs/features/offline-access.md)
- [项目基础设定与开发约定](AGENTS.md)
- [代码仓库结构与独立素材包](docs/asset-management.md)
- [地图验收](docs/README.md#地图验收)

各功能和地图的 Unity 已还原范围、自定义及缺项由[总索引](docs/README.md)统一链接到所属文档；功能须在实际迁移并验证后更新对应子文档。

Git 保存代码、文档和 `asset-pack/` 中的素材包摘要。`assets/official/`、`assets/characters/` 及完整模块来源凭据由独立 ZIP 管理，原始 APK／CDN 缓存另行备份；这些本地文件不进入 Git，但运行素材仍随 Windows 构建导出。

开发进度直接更新所属功能／地图验收文档，历史通过Git追溯；素材提取见 [来源说明](assets/reference/SOURCES.md)，文档检查见 [文档维护规则](docs/README.md#文档维护规则)，清理与提交遵循 [项目约定](AGENTS.md)。
