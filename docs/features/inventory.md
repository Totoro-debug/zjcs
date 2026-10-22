# 行囊、物品与宝箱

[总索引](../README.md)

状态：未完成

## 功能简介

储物箱或行囊入口浏览堆叠物品和独立装备，打开物品详情、宝箱、自选奖励及对应使用入口。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

`OfflineInventoryCatalog.Query` 从固定版物品表和 `GameSettings` 读取分类，将装备档案中的独立装备实例与堆叠物品分别列出，按 `ShowInBag` 过滤，并实现原 `PlayerBagPanel` 的类型、品质和取得时间排序。原版 `BagItemClientComparer` 的穿戴与限时优先级、反向排序规则也已迁移。`OfflineMainlineProgressSession.ApplyInventoryTransaction` 提供同一玩家存档的事务提交和事件幂等记录；`OfflineInventoryOperations` 提供堆叠增减、逐份取得／到期记录、严格“大于”到期判断和最早到期消耗。

上述规则和存档代码尚未接入实际行囊窗口、物品详情及使用流程；尚无本功能在 Unity 完整接入并运行验收的结论。原代码、配置、已转换素材和旧引擎测试不作为界面及玩法已还原的证据。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

物品使用、奖励、随机与计数由本地事务统一提交。当前格式中无效或已停用的专用物品不作转换，也不制造补偿。

用户确认的缺图物品保留真实名称、数量与品质框，不绘制替代图；赛季宝石旧回收分支关闭且不补造回收奖励。

原版资源检索确认12个物品图标没有对应 Sprite（物品 ID：1370、1475—1478、1484—1487、1719、3034、3036）；沿 `ItemIconDefault.FakeIcon` 显示物品名末四字，行囊及通用物品控件共用此兜底，保留品质框和数量，不用其他图片代替。原始资源检索范围和缺项记录见[物品来源清单](../../assets/reference/bag-items-source-manifest.json)及[素材来源](../../assets/reference/SOURCES.md)。

用户于2026-10-19授权补齐特殊物品本地结算，替代此前砥石仅预览的边界。服务端请求的资格复核、物品扣除、词条生成、自动锁定及修复退款改由同一本地存档事务提交；保存失败整体回滚，取消不消耗也不推进随机数。工具包重置保留已积累的产量和解锁状态。

砥石逐份记录到期时间，使用／分解优先处理最早到期的一份，本地时钟用于期限推进；到期条件沿原 `PlayerItemDataWrap.IsExpired` 的严格“大于”判断。每份物品的获得时间随当前格式的新增记录写入。

用户确认联动活动以本地存档创建时间对应官方服务器开服时间限制，并以本地存档事务替代服务端抽奖结算。新档创建时保存创建时间。

史莱姆与孤独摇滚联动及八位关联伙伴暂不开放；此决定优先于此前联动日历与常驻入口安排，范围见[暂不开放约定](offline-access.md#暂不开放的联动活动及伙伴)。

## 尚未实现

- 行囊原 prefab／素材恢复、场景入口、分类控件、列表图标、筛选排序交互及独立装备详情窗口。
- 来源导航、宝箱及特殊物品的官方专用详情与本地结算；奖励反馈、随机、扣费、取消、保存失败回滚尚未接线。
- 恢复其余官方 Unity 代码、配置、prefab、资源与调用关系，完成页面返回栈、实际入口和跨模块界面接线。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

物品、品质、数量、分类、排序和专用详情读取原配置；装备保留独立身份和穿戴标记。宝箱及自选奖励检查原数量、候选、职业和实际持有，取消不消耗，领取不重复。

普通装备碎片合成见[装备](equipment.md)，技能碎片见[技能](skills.md)。原图标、奖励面板、详情和数量控件按须接入物品类型使用，原 `ShowInBag=false` 的货币不强行显示在行囊。

奖励窗口接收完整 `PlayerAwardMsg`，保留 `AwardSourceData`、`AwardResultData`、实例编号、`ParamDict`、转换、回收及额外字段；显示时按原反向提取合并、类型／品质／宝石排序和隐藏配置处理，不改写原消息。原 `ItemAwardIcon` 的转换剩余数量、回收奖励及六种附件须实现代码与独立素材验证，正式接入状态由[装备](equipment.md#完整还原部分)维护。点击奖励打开对应原详情；从详情跳转获取途径后保留原消息、物品、需求总数及页面返回链，返回时恢复奖励窗口和详情。关闭详情不会先关闭奖励，原奖励关闭等待时间仍生效。

远古砥石·攻击140已接通原装备选择、源词条选择、20组候选词条、同件互斥阻止、已穿戴装备互斥提醒、二次确认及前后结果窗口。奇迹／红色品质、净化中排除和攻击词条要求来自 `EquipReplaceEquipSelecter`；替换数值及技能品质按 `EquipInfo.GenerateRandomEquipAttribute` 和 `equip_attribute_replace_range` 生成，成功后原装备自动锁定。手动分解及168小时持有期结束均按 `RecycleReward` 回收为1000份41302；关闭详情时一并停止并释放倒计时。

修复工具包331使用原确认窗口和成功文案，按 `GameSettings.ResetHangUpIdList` 重置3—9号生产项至初始1级，逐级汇总 `hang_up_upgrade_material.LevelUpCost` 全额返还。

特殊物品按原 `ItemDetailDialogFactory` 分派至宝箱／自选、随机合成、幻兽碎片、古遗物／碎片、技能碎片、改名、祝福卡、裂隙次数、配方分解、修复及砥石专用流程。料理、装备碎片、技能和家园外观详情接入各自模块；确认后统一扣除实际物品、发放奖励并保存，取消不结算。

物品详情按原 `ItemSourceDataManager.Register` 的顺序列出获取途径，覆盖固定表349条及合成、料理、祈愿、宝箱、关卡、国度商人、地图资源和地区古遗物动态来源。来源槽位按当前详情 prefab 的原 `ItemDetailDialog.ItemSourceGo` 字段路径挂载，兼容特殊物品详情 prefab；保留屏蔽条件、原限制提示、商人地图／实体／区域／商店组及需求总数。快捷购买、合成按“需求总数减已持有数量”初始化，地图来源将目标和数量交给原资源指引。料理来源按原处理器打开料理面板；没有导航接口的关卡来源不新增跳转行为。活动导航保留原 `NavigateKey`、`NavigateType` 与完整参数；圣诞入口进入原伐木活动入口，音乐节、春节、周年庆和万圣节按活动日历及存档开服时间进入各自原入口。来源目标覆盖商店弹窗时保留原详情与页面栈，关闭商店后恢复详情来源上下文，再次返回恢复行囊。

外观礼盒接通原 `AppearanceChestItemUseDetailDialog` → `PreviewAppearanceDialog` → 开启与奖励流程，覆盖固定配置的69种武器、头饰、面饰及面妆。试穿读取原男女皮肤、职业允许集合和互斥规则，保留当前捏脸／染色，去掉原持武；职业不适配时选择原枚举中的首个可用职业。点击角色切换原两组皮肤动作或三组武器动作，武器演示保留原开头动作、退出比例与混合时长。点击预览区外关闭，沿 `PopupWindow.CanCloseByMask` 保留半秒防误触保护。试穿只使用临时外观，不修改存档；已持有奖励时沿原状态禁止重复开盒。

四个联动活动读取固定版本 CN/OpenTest 的 `activity`、`region_release_time`、`local_time_offset`、`conditions` 和 `function_limit`；开始时刻包含、结束时刻不包含，持续时间取官方 `Duration`。官方时间统一按 UTC+8 解析，不推断下一年度复开。音乐节（Activity 206）为 2025-09-25 08:00 至 2025-10-09 07:00，持续335小时；春节（212）为 2026-02-12 08:00 至 2026-03-05 07:00，持续503小时；周年庆（217）为 2026-05-28 08:00 至 2026-06-18 07:00，持续503小时；万圣节（226）为 2026-10-25 08:00 至 2026-11-15 07:00，持续503小时。入口仍执行原地区、地图11及等级20开放条件。

音乐节转盘按 `activity_special_gacha` 206 和 `special_gacha` 2030 结算：单抽及官方 `MaxTimes=5` 五连每次消耗1份道具23；保底、奖组权重、奖品命中次数限制及 `reward_rule` 奖励均读取官方配置。抽奖次数、奖组保底、奖品命中、消耗、奖励和随机种子在同一本地存档事务提交；保存失败整体回滚。原 `IsActivityRecord=false` 对应以扭蛋 ID 2030 保存共享记录。

联动活动来源、原表及窗口数据哈希见[联动活动来源清单](../../assets/reference/linkage-activities/source-manifest.json)。活动日历、开服限制和本地抽奖规则仍须接入 Unity 并验证。原客户端依据包括 `ActivityEntryLinkageActivityHandler.Refresh`、`ActivityEntryPanelCommon.OnEnableAndInited`、`WheelGachaContentBase.DoGahcaByCount`、`SpecialGachaInfoParser`、`StandardRandomGroup` 和 `SpecialGachaUtils`。

### 依据与代码定位

当前 Unity 查询、排序与事务实现见 [OfflineInventoryCatalog](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineInventoryCatalog.cs)、[OfflineInventoryOperations](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineInventoryOperations.cs) 和 [OfflineMainlineProgressSession](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs)。

原行囊代码依据为 [PlayerBagPanel](../../.tools/source-packages/official-code/client-source/PlayerBagPanel.cs)、[BagItemClientComparer](../../.tools/source-packages/official-code/common-source/BagItemClientComparer.cs)、[ItemDetailDialogFactory](../../.tools/source-packages/official-code/client-source/ItemDetailDialogFactory.cs) 与 [ItemSourceDataManager](../../.tools/source-packages/official-code/common-source/Common/ItemSourceDataManager.cs)。原窗口层级、配置、图标绑定与缺失项见[物品来源清单](../../assets/reference/bag-items-source-manifest.json)。

特殊物品、外观预览和联动活动的客户端依据分别见 [AppearanceChestItemUseDetailDialog](../../.tools/source-packages/official-code/client-source/AppearanceChestItemUseDetailDialog.cs)、[PreviewAppearanceDialog](../../.tools/source-packages/official-code/client-source/PreviewAppearanceDialog.cs)、[ActivityEntryLinkageActivityHandler](../../.tools/source-packages/official-code/client-source/ActivityEntryLinkageActivityHandler.cs) 与 [WheelGachaContentBase](../../.tools/source-packages/official-code/client-source/WheelGachaContentBase.cs)。活动及预览数据来源见[联动活动来源清单](../../assets/reference/linkage-activities/source-manifest.json)和[外观礼盒预览来源](../../assets/reference/appearance-chest-preview/source-manifest.json)。
