# 杖剑传说离线版

面向 Windows 10/11 的 Unity／C# 单机迁移项目，保留原作 2D 表现与竖屏比例，运行时完全离线。

- [项目文档总索引](docs/README.md)：功能与地图的官方实现、当前实现、单机决策及缺项。
- [项目协作约定](AGENTS.md)：迁移、验证、发布和提交要求。
- [素材管理](docs/asset-management.md)：目录、转换、验证、快照和发布操作。
- [官方来源](assets/reference/SOURCES.md)：版本、哈希、来源凭据及提取入口。

开发入口为 [unity/](unity/)，编辑器固定 `2022.3.57f1c2`；通过 `& ./scripts/unity.ps1 open` 打开工程。Windows 交付入口为 `D:/Desktop/杖剑传说/杖剑传说.exe`，可验证范围见所属功能／地图页。
