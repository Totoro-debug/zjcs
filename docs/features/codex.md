# 图鉴分类、筛选与只读详情

[总索引](../README.md)

状态：未完成

## 功能简介

原图鉴包含技能与古遗物分类、筛选、收藏状态和只读详情；浏览不发奖、不改变随机状态。

## 原始官方实现

CollectBookPanel 使用原职业／国家分类、书封滚动／吸附、收藏状态和技能／古遗物详情；图标、品质、属性和套装来自对应原表。


### 官方代码与资源依据

固定版本的原始界面、配置和资源来源由[官方来源总表](../../assets/reference/SOURCES.md)登记。

## 完整还原部分

无完整图鉴流程已接入 Unity。古遗物扭蛋中的系列收藏弹窗读取当前系列物品，以本地库存显示持有状态和图标；相关接线位于 `HomeHudView.TreasureGacha`。

## 自定义实现

无。浏览不发奖、不改变随机状态。

## 自定义实现

持有、任务历史和赛季状态由本地真实记录替代服务器；图鉴不增加额外奖励或属性。

## 尚未实现

- 主菜单图鉴入口、职业／国家分类、收藏书封、列表、筛选、分页与完整只读详情。
- 技能词条、召唤物和嵌套详情，以及古遗物属性、套装和真实持有状态的完整图鉴展示。

## 代码与资源入口

[原 CollectBookPanel](../../.tools/source-packages/official-code/client-source/CollectBookPanel.cs)、[原 SkillDetailDialog](../../.tools/source-packages/official-code/client-source/SkillDetailDialog.cs)、[原 SkillHyperLinkDataProvider](../../.tools/source-packages/official-code/client-source/SkillHyperLinkDataProvider.cs)、[图鉴资源转换器](../../scripts/prepare_codex_assets.py)、[扭蛋系列收藏实现](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.TreasureGacha.cs)
