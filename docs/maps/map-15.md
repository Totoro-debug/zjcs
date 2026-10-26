# 15 通天塔入口

[总索引](../README.md)

状态：未完成

## 地图简介

地图 ID：15。原目录：通天塔/群落/通天塔入口。场景类型：`map_world`；原区域配置1项，BGM配置15。

当前验收对象为 Unity / C#。

## 完整还原部分

尚无本图在 Unity 完整运行并验收的结论。共享地图 DTO、导航／进度规则以及保留的转换数据不能计作本图已还原。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

按用户明确确认，沿用龙之国的单机自动解谜方案。固定版客户端／Common 的 `StageResultAutoPuzzleComponentImplement`、`StageResultResetPuzzleComponentImplement` 及相关请求保留目标、费用和结果标记，缺少服务端执行器；本地适配不标为原服务端逻辑。依据为 [`MyStageResultAutoPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultAutoPuzzle.cs)、[`MyStageResultResetPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultResetPuzzle.cs) 及对应 Common 标记组件。通天塔 5 处火炮终端只清除原 `NeedRemoveLoEntityIds`，2 处特殊锁及 5 处普通锁终端只解开原 `LockLoEntityIds`，使用原交互费用（道具 325×1），不补发炮、失败演出或奖励。重置仅恢复指定目标，保留其他状态、背包、奖励凭据和角色位置；原配置仅 4 个终端有重置分支，保留其余终端的原菜单，不新增重置按钮。

六处遗迹沿固定客户端／Common `WorldRuinDialog`、`GameRuinEnterNextStageDialog`、`GameNextStageUIHandler`、`FightRuinConfigComponentImplement`、`StageBehStageRuinComponent` 与 RPC 10160 核对原 20 层配置、战斗与奖励数据；服务端推进／结算执行器未取得。按用户明确确认的离线方案，进入后立即执行当前未探索层，沿原 `StageId`／层序推进；战斗事务挂起并回放，战败保留当前层供重试。胜利后按原 `GameNextStageUIShowDelayTime=0.8` 秒返回层推进或奖励阶段。奖励只按原追加物配置领取本层奖励；最后一层原奖励结算后标记已探索，退出保留已领取奖励及进度，不增加额外奖励或随机规则。遗迹 17–19 的层配置原 `MapClassId=42062` 而放置于 42063，配置不改写。经用户明确确认的背景修正只把两条 `EnterNextStageUIBgImg` 的加载映射到固定版同名 `BigMapStageBg` 原图，原始配置仍保留 `MapStageBg` 路径。

资格、位置与部分机关标记是明确的测试夹具，不代表普通角色游玩进度。

手动火炮采用用户明确确认的单机处理：读取原 `ActionBulletComponent` 的速度、10 格上限和可飞地形，按当前镜面 `TurnInfo` 转向，命中后执行原目标阶段，遇障碍停止。固定版 `MyActionFireBullet`／Common 请求未包含服务端碰撞执行器，故不将本地碰撞规则标为原服务端实现。每次成功事务只保留一份有界发射记录，保存失败时随目标、任务和背包全部回滚；加载存档不再次执行命中。在明确的资格、相邻前置机关及镜面状态夹具下，检查五处原炮台场地的全部 11 座火炮、9 次真实目标命中、21 段镜面转向和 JSON 保存／拒绝保存回滚，共 181 项通过；这不是连续游玩路线。原炮弹、炮口、镜面受击、震屏及四个原音效需接入。

用户已明确允许炮弹／炮口的 11 个、元素连线的 39 个原 `ParticleSystem.NoiseModule` 发射器采用现有 FastNoiseLite 采样，共 50 个。原 bundle 参数、GLES 程序和固定 APK 的 `libunity.so` 已核对，未确定该原生模块内部的三维粒子噪声算法；此适配保留原素材、频率、强度、滚动、曲线及阻尼，仅噪声采样使用本地方案，粒子扰动轨迹与 Unity 存在差异。此授权只涉及上述 50 个发射器，不把 FastNoiseLite 标为原算法；镜头震屏仍使用已精确迁移的原 `Mathf.PerlinNoise`。

## 尚未实现

按通天塔地图组15恢复60张地图、118区、2899个实体及81条主线；入口与幻想阶梯分开，六处遗迹、各楼层连接、小地图、原地形／灯光／水面逐图绑定与验证。

按原地图配置恢复场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口；原 prefab、骨骼、材质、粒子、动画及音频需在 Unity / Windows 逐项恢复并绑定。 原地形：`Map_Tower15_SnowFloor1/MapGround_Tower15_SnowFloor1`。 后继地图：`42001`。

- 完成本图独立的本地资源绑定、玩家入口、正常游玩、战斗／交互结算、失败回滚、退出与冷读恢复。
- 在 Unity 编辑器实际运行，并验证 Windows 目标表现；

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

、[`prepare_map15_ruins.py`](../../scripts/prepare_map15_ruins.py)、[`verify_map15_ruins.py`](../../scripts/verify_map15_ruins.py)、

、[`prepare_map15_element_link.py`](../../scripts/prepare_map15_element_link.py)、[`verify_map15_element_link.py`](../../scripts/verify_map15_element_link.py)

、[`prepare_map15_ui.py`](../../scripts/prepare_map15_ui.py)、[`tower-ui-source-manifest.json`](../../assets/reference/official-map/tower-ui-source-manifest.json)

[`verify_map15_ui.py`](../../scripts/verify_map15_ui.py)、

、[adventure.json](../../assets/official/adventure.json)、[15.json](../../assets/official/regions/15.json)、[`MyStageResultAutoPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultAutoPuzzle.cs)、[`MyStageResultResetPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultResetPuzzle.cs)
