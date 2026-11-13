# 共用界面、文字与资源运行

[总索引](../README.md)

状态：未完成

## 功能简介

本页维护跨玩法共用的窗口层级、通用界面资源和本地玩家显示规则。具体界面、视觉资源与交互缺项由所属功能／地图页维护。

## 原始官方实现

官方通用弹窗由 `WorldPopupWindow` 管理画布排序，并通知子画布和排序组更新层级。原 prefab、字体、材质、动画、特效和音频通过各功能的资源目录绑定。

## 完整还原部分

- `WorldPopupWindow.SetCanvasSortingOrder` 按官方调用顺序更新画布排序并发送变更通知；`SortingOrderSetter` 随通知更新子节点排序。
- 通用界面转换保留原 prefab 层级、组件字段、动画曲线和资源引用；Windows 运行时通过本地资源目录加载。

## 自定义实现

离线版不生成远端玩家身份或头像；涉及在线玩家槽位时使用本地角色数据。具体显示入口由所属功能页维护。

### 原生纹理共用

原生 `Texture2D` 仅在文件字节与去除 GUID 后的导入设置完全相同时共用同一载荷，材质、Sprite 与 prefab 保留各自原对象、fileID 和字段，只将本地纹理引用指向保留的 GUID。原始 APK／CAB／PathID、转换输入及其预期哈希继续由各模块来源清单保存；[共用纹理来源清单](../../assets/reference/shared-textures-source-manifest.json)补充原输出路径／GUID、实际载荷／元数据哈希和保留目标的对应关系。

保留全部 `Resources` 地址、可读／运行时可写纹理、转换脚本直接使用的模板，以及其他会话正在修改或未提交的新素材；不按旧版本目录名删除资源，不合并导入设置不同的副本。没有改变像素、mip、采样参数或官方表现。

`scripts/deduplicate_unity_textures.py` 提供检查、归一化、验证和本会话事务回滚；重新生成已登记输出后必须再次执行归一化。静态工程检查拒绝重新出现的已登记副本。按 GUID 遍历依赖的隔离验证继续沿真实引用复制资源；仅复制模块目录的夹具须通过 `stage_shared_textures(project)` 补齐保留目标，不能把已删除的副本当成缺失官方素材。

当前验证范围：去重工具的 7 项测试覆盖导入设置、动态纹理、其他会话引用保护、来源哈希链、重复生成与事务恢复；静态工程检查、Unity 导入／编译和 `Zjcs.Editor.ProjectTools.Validate` 通过。`SharedTextureRoundAcceptance` 实际加载全部登记的保留纹理，复用原生战斗时间轴夹具验证头像、卡片、图集、死亡更新、显隐与模拟速度按钮点击；隔离工程中的 `OfficialMainlineMiniMapRootAcceptance` 验证小地图原生根、HUD、全屏面板、筛选点击、区域与可见性更新。两项 Play Mode 均通过，已查看实际运行截图。

小地图全量来源核验仍有未修改的夹具脚本、程序集定义、共享目录资产及标记目录代码的既有预期哈希不一致；保留其原预期值。各功能完整验证范围继续由所属页维护。

## 尚未实现

各功能页仍列有未恢复的官方界面、字体、材质、动画、特效、音频及输入流程；本页不重复维护模块清单。

## 代码与资源入口

[WorldPopupWindow.cs](../../unity/Assets/Game/Runtime/Official/WorldUI/WorldPopupWindow.cs) · [SortingOrderSetter.cs](../../unity/Assets/Game/Runtime/Official/TeamUI/SortingOrderSetter.cs) · [资源迁移与验证流程](../asset-management.md)
