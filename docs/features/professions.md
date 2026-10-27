# 职业、晋升与自由转职

[总索引](../README.md)

状态：未完成

## 功能简介

人物页进入职业总览、职业树、详情、升级和成就；原导师入口在地图寻路并打开实际职业窗口。自由转职按原 Function 135 的人物 100 级及职业分支条件开放。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

已核对官方职业总览、职业树、详情、成就和相关弹窗的源码、prefab 及配置；职业包与规则弹窗共 8598 个组件的字段已经严格解码，并完成转换输入写入、重开和逐字段比对。原生导出已恢复 22 个资产中的 328 个托管引用注册表、387 条记录和 4938 个对象引用，保留 RID、顺序、浮点位值与层级身份。转换缓存尚未接通全部官方运行时类型和显式资源绑定。

主页面立绘按原 `ProfessionSettings` 的 GUID 从官方 Sprite 提取，38 个职业的男女映射复用 12 张原图片，保留 alpha、PPU、pivot、border；原 `ImageLocator` 的原生尺寸及锚点修正代码已迁移。职业总览的页签状态、属性行复用及雷达计算正在按官方组件迁移，不能把旧节点投影界面算作原生 prefab 迁移完成。

成就 C# 规则已按官方 `AchievementRawInfoParser`、`CycleTemplateInfo` 和 `AchievementInfo` 生成 10005 个档位。点击任一可领取行时，按原 `AchievementPanelItem` 的组 10／`count=-1` 请求及共享 `Player.TryRecordAlreadyGotAchievementReward`，领取该组所有已达成、未领取档位；本地奖励和记录使用一次事务。原配置边界、五行显示、循环尾档、批量领取和重复领取保护的独立 C# 测试通过。

隔离 Unity 工程已完成现有接线和新迁移组件的导入、编译及 `Zjcs.Editor.ProjectTools.Validate`。尚未完成职业原生 prefab 的 Play Mode 点击、画面与存档验证，以上不代表功能完整还原。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

自由转职服务端扣除方法未取得。用户确认生成三组方案后取消不消耗转职石，最终确认才消耗；本地费用适配器遵循免费 0 个／非免费 1 个转职石，另存当次报价支持恢复。免费间隔继续按原 168 小时计算，生成免费方案即推进时间，放弃不重置该时间。服务端请求改为本地原子存档事务，写盘失败回滚费用、方案、职业及奖励。

## 尚未实现

- 职业树卡片的官方角色 Spine／小人外观和动画；主职业页立绘、雷达及属性列表与游戏内画面的实际对照验收。
- 职业详情中的官方技能和属性、升级消耗与徽章领取差额；树中未解锁条件、导师寻路与晋升条件。
- 成就五行列表的领取状态与官方奖励图标／数量、批量领取、保存恢复和重复领取保护的实际运行验收。
- 职业转换与晋升；导师自由转职的比较、三组方案、确认、原奖励、撤回及免费间隔。
- Unity 编译、Play Mode 模拟点击与运行画面检查，Windows Player 构建／启动／存档验证和发布。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

总览、分支连线、详情、升级、普通转职及晋升使用原面板。升级消耗原徽章且不按人物等级额外截断；晋升核对前置职业等级、总职业等级和导师区域公开状态。历次职业等级属性持续累计。

进入职业详情或升级时按已拥有技能的阶位领取原徽章，只领取未领差额；重开与冷读不重复发放。职业成就使用组10固定档和循环奖励。

切换职业卸下不兼容武器、清除方案中不适用技能并重置祈愿职业选择，装备和已获技能不删除。男女预览使用原默认服装、六类武器、prefab挂点和完整父变换；晋升原时间线驱动换装、动作、粒子及音频。

自由转职从地图十处原导师剧情入口打开，按原节点顺序继续剧情。选择窗口使用原三职业立绘、多边形点击区和选中动画；比较窗口展示职业、武器、属性与费用，三套方案支持页签、战技／秘法列表、未获得标记和技能详情。

生成方案、放弃、最终确认、通知与奖励、八小时内撤回、反向技能预览、提前结束撤回期、期限届满提示均接入本地事务。生成与放弃确认使用原 prefab 的倒计时按钮锁定；方案、选择、免费间隔、当次费用和撤回期限在重启后恢复。技能、碎片、徽章及历次职业等级按原映射双向交换；切换后统一整理职业技能方案、武器和祈愿职业选择。

### 依据与代码定位

以下官方代码与规则用于核对接线：`ProfessionOverviewPanel`、`ProfessionTreePanel`、`ProfessionTreePanelItem`、`ProfessionPanel`、`ProfessionUpgradePanel`、`ProfessionPropRadarDrawer` 和 `Common/AchievementRawInfoParser`，均位于 `.tools/source-packages/official-code/`。职业图片输入、bundle 身份和输出哈希见[职业立绘来源清单](../../assets/reference/profession-ui/portraits-source-manifest.json)；Unity 面板包由 `scripts/prepare_unity_character_panels.py` 生成。迁移组件的方法范围、官方代码和 C# 输出哈希见[运行时代码来源](../../assets/reference/profession-ui/runtime-source-manifest.json)；属性行当前只迁移总览和雷达使用的字符串显示路径，完整彩色预览和祝福交互仍待对应调用方迁移。原生组件来源与转换分别由 `prepare_unity_profession_native_inputs.py`、`prepare_unity_profession_native_export.py` 恢复，凭据见[原生输入](../../assets/reference/profession-ui/native-input-source-manifest.json)及[原生导出](../../assets/reference/profession-ui/native-export-source-manifest.json)。成就独立测试入口为 `python -B scripts/test_profession_achievements.py`；相关界面仍未通过 Play Mode 和 Player 运行验证。


