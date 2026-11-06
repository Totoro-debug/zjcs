# 主线地图公共模块

[总索引](../README.md)

状态：未完成

## 功能简介

地图角色属性通过统一工厂完成装备与技能装配，剧情转职重载时保留完整属性来源；依赖与计算对照见[人物属性的共享计算边界](character.md#属性来源与共享计算边界)。

本页维护 Map10–16 共用的地图加载、玩家与地区状态、地图可见性、公共 HUD 和跨图服务。每张地图的专属场景、条件、剧情、资源与缺项见所属地图页。

## 原始官方实现

主线地图由官方程序集、配置、实体容器、地区进度、导航、剧情和地图 UI 共同驱动。共享数据生命周期、地图规则和资源来源见 [官方来源总览](../../assets/reference/SOURCES.md) 与 [地图来源清单](../../assets/reference/official-map/)。

客户端通过请求与响应调用服务器处理部分剧情、实体和地图状态。APK 未包含对应服务器实现时，本页只记录客户端可证行为及已确认的单机方案，不将离线适配描述为官方服务器逻辑。

## 完整还原部分

- `MainlineSharedWorld` 管理官方程序集、地图配置、实体数据和容器作用域；`MainlineSharedWorldStateModule` 持有投影玩家并集中执行激活、原始地区点数、实体／地区状态和角色位置的发布顺序；`OfficialMainlineMapRuntime` 按地图 profile 装配导航、地图状态、HUD、区域、交互及退出清理。
- 本地地图进度、地区私有／公开状态、探索记录、神像状态和已发现实体由地图存档读取。跨图读取复用同一实体状态解析，不创建场景对象。
- `OfflineMapGridVisibility` 和 `OfficialMainlineGridFog` 提供地图揭格及白雾状态；公共迷你地图、地图标记和资源点使用地图 profile 及本地图目录。
- 公共剧情运行时复用原节点、状态机、动画和声音入口；各图通过独立目录绑定原资源。通用剧情与实体表现不代替所属地图的正式入口和完整流程。
- 公共路线、战斗、物品提交及探索记录分别维护在[日常副本](dungeons.md)、[战斗执行](battle-execution.md)、[背包](inventory.md)和[世界探索](world-exploration.md)。

地图保存成功后，宿主只提供当前实体展示状态和角色坐标；公共世界状态模块先发布神像／巫师激活与原始地区点数，再调用官方实体状态及地区 `prepare → remove → add → status` 顺序，最后同步角色位置。首次加载沿同一发布顺序恢复保存状态，然后启动原始 Client。

状态发布顺序已由 `MainlineMapModuleValidation` 在主线模块验证入口中检查；主线地图完整流程仍按本页“尚未实现”项继续验证。

主线探针与对应 Editor 入口不再反射读取宿主私有字段或调用私有业务方法。地区条件、剧情条件和交互目录直接使用模块接口；女神、英雄、采集与探索通过场景实体和原按钮接收模拟指针，滚动列表接收模拟拖拽，退出通过可注入的 Escape 输入进入正常保存与销毁流程。初始坐标、地区和随机状态只在隔离存档或已有事务接口中准备，随后重新开图；位置、探索、奖励及跨图快照从本地存档和现有只读模块检查。官方资产序列化、动态类型加载及 Unity 编辑器视图设置所需反射保留。

Play Mode 已通过 Map10／11 导航、迷你地图页签与筛选冷读、女神公开与原阴影动画、英雄首次／每日交互、体力采集／奖励动画／重生、探索概率与明细／令牌滚动选择／扣费／十连／历史翻页，以及正常退出与冷恢复；Map15 进入、导航和重开也通过。画面检查仍可见原有实体材质／贴图表现缺项，公共模块验证不等于整张地图完成。

Map10／14 白雾专项已通过原白雾节点挂载、实际指针移动揭格、保存字节扩展、正常退出后的精确冷恢复，以及跨图只读快照不改写存档；玩家所在格的可见性由官方压缩解码接口直接检查。

## 自定义实现

- 所有主线地图的国度商店经验按实际消耗的专属货币 1:1 累计；商品、价格、库存和刷新周期仍取各地图官方配置。地图专属等级阈值及刷新规则在所属地图页维护。
- 新档和缺少地图字段的旧存档按已确认的本地初始化规则恢复玩家位置、地区状态和探索记录；不推导客户端未保存的服务器状态。
- 地图内时间、实体可见性、探索快照和商店进度使用本地存档与离线时钟；服务器控制的随机、首次开放或剧情结果只按已有客户端规则或用户确认的单机方案接入。
- 国度商店、剧情节点、地图机关和奖励的本地事务必须在所属页面说明具体差异；此处只维护所有地图共用的规则。

## 尚未实现

- Map10–16 的正式世界入口、角色和实体表现、HUD、迷你地图、剧情、交互、战斗及奖励尚未全部接通。
- 原始小地图完整页面、全部地图标记与详情、公共地区资源层及跨图返回流程仍有缺项。
- 原 Timeline、角色动画、粒子、音频和摄像机演出尚未全量接入正式地图宿主。
- 每张地图仍需分别完成模拟输入、画面、状态保存／冷读、失败恢复和 Windows Player 流程；公共模块完成不代表单张地图完成。

## 代码与资源入口

- 主线运行时：[OfficialMainlineMapRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs)、[OfficialMainlineMapProfile.cs](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs)。
- 离线地图规则：[Runtime/Offline/World](../../unity/Assets/Game/Runtime/Offline/World/)。
- 公共世界状态发布：[MainlineSharedWorldStateModule.cs](../../unity/Assets/Game/Runtime/Offline/World/MainlinePlayerState/MainlineSharedWorldStateModule.cs)、[MainlineSharedPlayerProjection.cs](../../unity/Assets/Game/Runtime/Offline/World/MainlinePlayerState/MainlineSharedPlayerProjection.cs)。
- 通用地图、资源和来源记录：[地图来源清单](../../assets/reference/official-map/)、[来源总览](../../assets/reference/SOURCES.md)。
