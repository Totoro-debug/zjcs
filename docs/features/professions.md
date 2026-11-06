# 职业、晋升与自由转职

[总索引](../README.md)

状态：未完成

## 功能简介

本功能包含职业总览、职业树、职业详情、属性、升级、晋升、导师寻路及自由转职。UI、规则和存档事务均须接入原职业配置及当前角色状态。

## 原始官方实现

职业配置定义前置、条件、分支、职业等级、历次属性、技能／装备关系、晋升和导师入口。原页面包括职业总览、职业树、详情、升级、属性雷达、晋升及转职窗口；客户端代码与输入版本见[官方来源总览](../../assets/reference/SOURCES.md)。

## 完整还原部分

- 官方职业数据、原代码和部分外观／图标资源已有 Unity 运行载荷；官方 `ProfessionUpgradePanel`、职业总览及职业树代码已迁移，但正式人物页的入口、控制器和完整交互尚未全部接通。
- 本地外观预览使用角色档案的只读投影；武器和肖像资源使用显式本地目录。资源接入不代表职业树、详情及升级页完成。
- 职业属性、徽章消耗、导师寻路、升级／晋升和自由转职尚未形成可游玩的完整流程。

## 自定义实现

- 职业动画沿用[Spine 原生迁移方案](../asset-management.md#spine-角色动画迁移决策)；当前适配只覆盖已接入的曲线、姿态、插槽和待机 IK 分支，不改变职业规则。
- 自由转职服务器扣除逻辑未取得。按用户确认，生成三组方案后取消不消耗转职石，最终确认才扣除；免费方案消耗 0 个、非免费消耗 1 个转职石。免费间隔为原 168 小时，生成免费方案时推进间隔，放弃方案不重置。费用、方案、职业与奖励使用本地原子事务，写盘失败时回滚。

## 尚未实现

- 职业树、红点、立绘、属性雷达、技能与详情的正式入口、原生资源绑定和实际画面。
- 升级消耗与徽章差额、未解锁条件、导师寻路、晋升前置及原奖励。
- 职业选择／晋升、自由转职三组方案、确认／撤回和存档恢复的完整窗口流程。
- 正式人物页的模拟输入、结果画面、失败回滚、冷读和 Windows Player 流程。

## 代码与资源入口

- 官方职业 UI：[`Runtime/Official/ProfessionUI`](../../unity/Assets/Game/Runtime/Official/ProfessionUI/)。
- 本地职业适配：[`Runtime/Offline/ProfessionUI`](../../unity/Assets/Game/Runtime/Offline/ProfessionUI/)。
- 原客户端代码：[ProfessionOverviewPanel.cs](../../.tools/source-packages/official-code/client-source/ProfessionOverviewPanel.cs)、[ProfessionTreePanel.cs](../../.tools/source-packages/official-code/client-source/ProfessionTreePanel.cs)、[ProfessionUpgradePanel.cs](../../.tools/source-packages/official-code/client-source/ProfessionUpgradePanel.cs)。
- 输入版本与资源清单见[来源总览](../../assets/reference/SOURCES.md)。
