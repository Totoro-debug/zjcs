# 官方来源与提取入口

[文档索引](../../docs/README.md) · [素材管理](../../docs/asset-management.md)

本页只记录官方输入与来源凭据。各模块的原始实现、转换／绑定细节、当前实现和验证边界见所属功能／地图页；逐文件记录以模块来源清单为准。

## 输入身份与版本

| 输入 | 固定身份／位置 |
| --- | --- |
| 官方 APK | `.tools/source-packages/zjcs_M509085.apk`；manifest `854_156383`；1,024,203,578 字节 |
| APK SHA256 | `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9` |
| 官方下载 | [zjcs_M509085.apk](https://zhangjcsdlf.leiting.com/full/zjcs_M509085.apk)，入口来自[官网](https://zjcs.leiting.com/)配置接口 `https://zjcs.leiting.com/client/gameV2/home` |
| 资源版本选择 | 根目录 `official-source.json`；当前五包 `871_156598`，资源目录 `202610211330871` |
| 当前云端输入 | `.tools/source-packages/cloud/runtime/202610211330871/`；包含原清单、`inventory.json` 与 `receipts.jsonl` |
| 官方更新入口 | [110001_Main.dis](https://zhangjcsupdate.leiting.com/zhangjcs/version/release/110001_Main.dis) |
| 官方程序集／反编译 | `.tools/source-packages/official-code/managed/decoded/`、`client-source/`、`common-source/` 等 |
| 分包 | Boot、Launch、HybridCLR、DefaultPackage、WwiseBnk |

APK 与热更新版本分别记录。原输入、转换产物及其前后哈希不统一改名为当前版本。历史版本的重建须显式恢复对应输入，或证明当前缓存文件与原清单大小、MD5及真实下载凭据 SHA256 一致；不能替换变化文件或修改预期哈希。当前保留范围由 `official-source.json` 标识，既有 Unity 成品与用户存档独立保留；本地保留不等于仓库外备份。

## 凭据位置

| 内容 | 入口 |
| --- | --- |
| 原始包与逐文件下载来源 | [runtime-cloud-source-manifest.json](official-map/runtime-cloud-source-manifest.json) 及缓存中的 `receipts.jsonl` |
| 当前来源切换与转换链 | `assets/reference/official-map/current-*-source-manifest.json`、`assets/reference/character-creation/current-*-source-manifest.json` |
| 客户端／共享代码来源 | [官方程序集与反编译目录](../../.tools/source-packages/official-code/) 与各模块源码来源清单 |
| 模块资源来源 | 本目录各模块的 `source-manifest.json`、`*-source-manifest.json` 及所属功能／地图页中的入口 |
| 历史输入与非运行参考 | [LEGACY_SOURCES.md](LEGACY_SOURCES.md)；历史版本证据保留原身份 |
| 转换素材备份 | `asset-pack/layout.json`、`asset-pack/lock.json`；实际覆盖以已生成并校验的归档为准 |

## 提取与登记

1. 先核对 APK 的包内清单、实际文件／内部对象、prefab 引用、绑定和 CAB 依赖；确认包内没有时，再查所选版本已下载官方云端资源及哈希。加载或解码失败不等于资源缺失。
2. 使用 `scripts/official_apk.py`、`fetch_official_assets.py` 和现有缓存。当前输入读取用 `Apk.current()` 或 `Apk(manifest=None)`；旧 `Apk()` 的历史语义不能冒充当前入口。
3. 缺少相关官方程序集／反编译结果时运行 `python scripts/prepare_official_sources.py --decompile-client`，按本机环境指定 `--dotnet`／`--ilspy`，核对提取范围与依赖。已有结果先核验来源再复用。
4. 模块清单保存版本、实际 URL、bundle 名／对象名或 CAB／PathID、原始输入 SHA256、大小／MD5、工具及处理方式、输出路径／SHA256和显式加载绑定。原始包哈希与转换输出哈希分开登记，未变化文件沿真实转换链复用。
5. 两处均核实缺失时，列出具体资源／bundle、查找范围、方法与影响，请用户决定；不自行绘制、近似替换或降低表现。服务器规则缺口写入所属功能／地图页。

```powershell
python -B scripts/official_manifest.py --verify-packaged
python -B scripts/fetch_official_runtime_assets.py --verify-cache 202610211330871
python -B scripts/fetch_official_assets.py --extract '<bundle 名称正则>'
```

原始包、工具及缓存不进入 Windows 运行目录。Android → Windows 的恢复、脚本／shader／插件适配和目标平台检查见[素材管理](../../docs/asset-management.md#android-资源迁移到-windows)。
