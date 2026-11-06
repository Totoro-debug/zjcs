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

## 尚未实现

各功能页仍列有未恢复的官方界面、字体、材质、动画、特效、音频及输入流程；本页不重复维护模块清单。

## 代码与资源入口

[WorldPopupWindow.cs](../../unity/Assets/Game/Runtime/Official/WorldUI/WorldPopupWindow.cs) · [SortingOrderSetter.cs](../../unity/Assets/Game/Runtime/Official/TeamUI/SortingOrderSetter.cs) · [资源迁移与验证流程](../asset-management.md)
