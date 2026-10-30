# 行囊、物品与宝箱

[总索引](../README.md)

状态：未完成

## 功能简介

储物箱或行囊入口浏览堆叠物品和独立装备，打开物品详情、宝箱、自选奖励及对应使用入口。

## 原始官方实现

官方行囊按物品类型分类，使用 ItemDetail、来源、使用／选择／回收等窗口；装备、技能、古遗物和特殊物品由各自原配置及处理类分派。

### 待迁移的规则与表现

物品、品质、数量、分类、排序和专用详情读取原配置；装备保留独立身份和穿戴标记。宝箱及自选奖励检查原数量、候选、职业和实际持有，取消不消耗，领取不重复。

普通装备碎片合成见[装备](equipment.md)，技能碎片见[技能](skills.md)。原图标、奖励面板、详情和数量控件按须接入物品类型使用，原 `ShowInBag=false` 的货币不强行显示在行囊。

奖励窗口接收完整 `PlayerAwardMsg`，保留 `AwardSourceData`、`AwardResultData`、实例编号、`ParamDict`、转换、回收及额外字段；显示时按原反向提取合并、类型／品质／宝石排序和隐藏配置处理，不改写原消息。原 `ItemAwardIcon` 的转换剩余数量、回收奖励及六种附件须实现代码与独立素材验证，正式接入状态由[装备](equipment.md#完整还原部分)维护。点击奖励打开对应原详情；从详情跳转获取途径后保留原消息、物品、需求总数及页面返回链，返回时恢复奖励窗口和详情。关闭详情不会先关闭奖励，原奖励关闭等待时间仍生效。

远古砥石·攻击140已接通原装备选择、源词条选择、20组候选词条、同件互斥阻止、已穿戴装备互斥提醒、二次确认及前后结果窗口。奇迹／红色品质、净化中排除和攻击词条要求来自 `EquipReplaceEquipSelecter`；替换数值及技能品质按 `EquipInfo.GenerateRandomEquipAttribute` 和 `equip_attribute_replace_range` 生成，成功后原装备自动锁定。手动分解及168小时持有期结束均按 `RecycleReward` 回收为1000份41302；关闭详情时一并停止并释放倒计时。

修复工具包331使用原确认窗口和成功文案，按 `GameSettings.ResetHangUpIdList` 重置3—9号生产项至初始1级，逐级汇总 `hang_up_upgrade_material.LevelUpCost` 全额返还。

特殊物品按原 `ItemDetailDialogFactory` 分派至宝箱／自选、随机合成、幻兽碎片、古遗物／碎片、技能碎片、改名、祝福卡、裂隙次数、配方分解、修复及砥石专用流程。料理、装备碎片、技能和家园外观详情接入各自模块；确认后统一扣除实际物品、发放奖励并保存，取消不结算。

原 `Player.ShowBagCanUseItemRedDot` 先要求物品数量大于0，再检查 `GameSettings.ShowCanUseRedDotItemTypes` 白名单；装备碎片、普通物品碎片、随机古遗物碎片和幻兽碎片分别以对应合成表需求数量判断，祝福卡检查周期卡配置、购买次数和最大持续天数，料理配方检查配方映射、料理配置及 `IsLimit`，其余白名单类型直接亮点。当前规则载荷已接入白名单和装备／普通物品碎片合成阈值；行囊图表的随机古遗物兑换表也已用于随机古遗物碎片的精确数量判定。Unity 库存视图对已实现条件的物品使用官方附件8红点、15蓝点；幻兽碎片、周期卡和料理配方因本地存档尚无对应宠物、支付和料理条件状态，仍为未知，暂不显示。当列表存在任一未知判定时，视图不应用红点排序键；全部判定已知时，按官方可用物品的 `RedDotType` 降序排序。随机兑换与周期卡项目表见[物品来源清单](../../assets/reference/bag-items-source-manifest.json)，幻兽、料理和周期卡支付表的 APK 提取哈希见[官方规则清单](../../assets/reference/official-city/rules-manifest.json)；相关资源均已取得，缺少的是运行状态，不是官方资源。原比较器中不可用物品的红点分数为0。依据为官方 [Player.ShowBagCanUseItemRedDot](../../.tools/source-packages/official-code/common-source/Common/Player.cs)、`CanMergeEquipPiece`、`CanMergeItemPiece`、`CanMergeRandomTreasurePiece`、`CanMergePetPiece`、`CanUsePeriodCardItem`、`CanUseCookingFormula`、[ItemIconAttachmentType](../../.tools/source-packages/official-code/client-source/ItemIconAttachmentType.cs) 与 [BagItemClientComparer.GetScore](../../.tools/source-packages/official-code/common-source/BagItemClientComparer.cs)。

物品详情按原 `ItemSourceDataManager.Register` 的顺序列出获取途径，覆盖固定表349条及合成、料理、祈愿、宝箱、关卡、国度商人、地图资源和地区古遗物动态来源。来源槽位按当前详情 prefab 的原 `ItemDetailDialog.ItemSourceGo` 字段路径挂载，兼容特殊物品详情 prefab；保留屏蔽条件、原限制提示、商人地图／实体／区域／商店组及需求总数。快捷购买、合成按“需求总数减已持有数量”初始化，地图来源将目标和数量交给原资源指引。料理来源按原处理器打开料理面板；没有导航接口的关卡来源不新增跳转行为。活动导航保留原 `NavigateKey`、`NavigateType` 与完整参数；圣诞入口进入原伐木活动入口，音乐节、春节、周年庆和万圣节按活动日历及存档开服时间进入各自原入口。来源目标覆盖商店弹窗时保留原详情与页面栈，关闭商店后恢复详情来源上下文，再次返回恢复行囊。

外观礼盒接通原 `AppearanceChestItemUseDetailDialog` → `PreviewAppearanceDialog` → 开启与奖励流程，覆盖固定配置的69种武器、头饰、面饰及面妆。试穿读取原男女皮肤、职业允许集合和互斥规则，保留当前捏脸／染色，去掉原持武；职业不适配时选择原枚举中的首个可用职业。点击角色切换原两组皮肤动作或三组武器动作，武器演示保留原开头动作、退出比例与混合时长。点击预览区外关闭，沿 `PopupWindow.CanCloseByMask` 保留半秒防误触保护。试穿只使用临时外观，不修改存档；已持有奖励时沿原状态禁止重复开盒。

四个联动活动读取固定版本 CN/OpenTest 的 `activity`、`region_release_time`、`local_time_offset`、`conditions` 和 `function_limit`；开始时刻包含、结束时刻不包含，持续时间取官方 `Duration`。官方时间统一按 UTC+8 解析，不推断下一年度复开。音乐节（Activity 206）为 2025-09-25 08:00 至 2025-10-09 07:00，持续335小时；春节（212）为 2026-02-12 08:00 至 2026-03-05 07:00，持续503小时；周年庆（217）为 2026-05-28 08:00 至 2026-06-18 07:00，持续503小时；万圣节（226）为 2026-10-25 08:00 至 2026-11-15 07:00，持续503小时。入口仍执行原地区、地图11及等级20开放条件。

音乐节转盘按 `activity_special_gacha` 206 和 `special_gacha` 2030 结算：单抽及官方 `MaxTimes=5` 五连每次消耗1份道具23；保底、奖组权重、奖品命中次数限制及 `reward_rule` 奖励均读取官方配置。抽奖次数、奖组保底、奖品命中、消耗、奖励和随机种子在同一本地存档事务提交；保存失败整体回滚。原 `IsActivityRecord=false` 对应以扭蛋 ID 2030 保存共享记录。

联动活动来源、原表及窗口数据哈希见[联动活动来源清单](../../assets/reference/linkage-activities/source-manifest.json)。活动日历、开服限制和本地抽奖规则仍须接入 Unity 并验证。原客户端依据包括 `ActivityEntryLinkageActivityHandler.Refresh`、`ActivityEntryPanelCommon.OnEnableAndInited`、`WheelGachaContentBase.DoGahcaByCount`、`SpecialGachaInfoParser`、`StandardRandomGroup` 和 `SpecialGachaUtils`。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

从同一固定 APK 追加转换 `ItemIconCooking`、`ItemIconGem`、`ItemIconTreasurePanel`、`ItemIconTreasurePiecePanel`、`Common/Slider/SetCountSlider` 五个原始 prefab。`prepare_optional_interfaces.py` 从 `image_ui_common` 提取六档可取得的古遗物品质框；`prepare_gameplay_assets.py` 将料理与古遗物 / 碎片纳入完整配置范围，按 RawIcon 定位并提取实际打包图标。新增 89 项运行时资源、150 项图标映射；当前为 1,434 项资源、1,209 项图标映射、80 个 prefab、77 张配置表。图片均为原始 Sprite 解码 / 裁切，来源哈希与显式加载清单同步更新。

仓库按原 prefab 的四列、200×200 单元和原始间距排列，装备在同一底栏内切换；普通物品不额外显示名称，详情关闭返回原分类并恢复滚动。烹饪接入原始物品模板及制作数量控件；古遗物接入已有 `image_ui_treasure` 背景 / 空槽、原始六角品质框与图标，提供元素分类、收藏 / 碎片及本地装备操作。

`defaultpackage_assets_prefabs_ui_cooking.bundle`、`defaultpackage_assets_image_ui_cooking.bundle`、`defaultpackage_assets_prefabs_ui_treasure.bundle` 经清单哈希和 APK ZIP 条目核对均未打包。烹饪和古遗物页面的完整位置、动画与演出仍是待补齐项，当前本地布局不能称为完整原版还原。缺失图标不以其他图像替代。

prepare_bag_items.py 从固定 APK／匹配原版 CDN 的 prefabs_ui_playerbagpanel、prefabs_ui_itemdetail、prefabs_ui_confirmdialog 转换真实窗口，沿 ScrollRect.DefaultPrefabSource.Prefab 与 ItemChooseDialog.mOptionPrefab 提取原引用行；保留坐标、锚点、布局、状态、脚本字段、Slider 原生字段与官方语言。另读取 config_luban 的 item_tag、item_time_limit、random_treasure_exchange、period_card_item 以及 config_mapinfowrap 的 WorldStageTagMapInfo；未绘制素材或自行设计窗口。每个组件／输入／输出哈希和来源记录于 assets/reference/bag-items-source-manifest.json，运行数据为 assets/official/bag-items.json。独立转换模式不覆盖不同内容的共用 Sprite，资源登记由 --register-only 在共享锁内执行。另沿 ItemIconManagerConfig、ItemIconAttachmentConfig、ItemAwardIconManagerConfig 与技能／古遗物池配置提取真实图标模板、附件、奖励行和背景；3157 个物品 RawIcon 绑定来自固定版本原 Sprite，仍缺的 12 个输入与检索包名逐项保留在 unresolved_item_icon_inputs，按用户意见采用原客户端文字分支，不生成替代素材。补充外观箱与远古矿石时，沿 ChestInfo.GetIconPath／AppendAppearanceInfo 提取 69 个实际奖励外观的原图标，累计 3226 个物品绑定、1544 张不同图标 PNG、74 个窗口及引用模板；EquipReplacePropContent.mPrefabObj 沿原 PPtr 提取专用属性行，预览取 EquipAttributeReplaceItemInfo／EquipAttributesPropsInfo 与 SingleProp 策略。预览标题等动态语言键直接读取组件序列化字段；重复开启错误文本由 BroadcastManager 的 ErrCode 字段名规则取得，未改写原文。

行囊物品品质底图来自固定 APK 的 `defaultpackage_assets_image_ui_itemquality.bundle`（APK SHA256 `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9`，bundle MD5 `74e6afc6b39b88d0e8977d8f12267b52`）。`ItemIconConfig.QualityBgImages` 八个索引对应白、白、蓝、紫、橙、金、红、彩虹七张唯一 Sprite；路径 ID、解码输出 SHA256 和索引顺序登记在 `assets/reference/inventory-quality-backgrounds-source-manifest.json`，与现有官方源 PNG 像素一致。

## 完整还原部分

`OfflineInventoryCatalog.Query` 从固定版物品表和 `GameSettings` 读取分类，将装备档案中的独立装备实例与堆叠物品分别列出，并按 `ShowInBag` 过滤。比较器已按官方优先级处理红点分数、已穿戴、限时、类型／品质／取得时间与 ClassId；红点条件含未知状态时为避免误判，不对当前列表启用红点排序。类型排序只反转类型优先级，品质排序只反转品质，取得时间排序只反转取得时间；红点类型分数及其他次级键保持原方向。`OfflineMainlineProgressSession.ApplyInventoryTransaction` 提供同一玩家存档的事务提交和事件幂等记录；`OfflineInventoryOperations` 提供堆叠增减、逐份取得／到期记录、严格“大于”到期判断和最早到期消耗。

`scripts/prepare_unity_inventory_assets.py` 按物品来源清单核对 JSON 与 1,672 条官方素材哈希，将已转换的官方行囊／详情层级数据和对应 Sprite 恢复至 `unity/Assets/Official/Inventory`；`OfficialInventoryAssetCatalog` 可读取原窗口模板、列表行、官方分类绑定、物品图标映射及图标 prefab 选择顺序。当前尚未完成素材导入及 ScriptableObject 绑定；Unity 编译仍受其他模块的 `AudioClip` 解析错误阻断，不能视为运行时已接入。

`OfficialInventoryPanelView.Initialize` 已能依据官方行囊模板构造竖屏 Canvas、分类页签、排序控件、滚动列表、物品图标和基础详情窗口。每个页签分别记忆类型／品质／取得时间排序，切换页签时恢复该页上次选择；反向状态沿原界面全局保留。基础详情绑定官方物品名称、数量和 `ItemType` 本地化文本。图标绑定按各官方 prefab 实际子节点名称匹配图像、缺图文字与数量，而非假定所有物品共用空图标层级；品质图按官方 `ItemIcon.QualityImg` 和 `ItemIconConfig.GetQualityBgImage` 的枚举索引绑定 APK 中的白、蓝、紫、橙、金、红、彩虹背景（None 与白色共用白底）。`scripts/prepare_unity_inventory_assets.py` 与[品质来源清单](../../assets/reference/inventory-quality-backgrounds-source-manifest.json)核对七张输出 PNG 哈希及原 APK AssetBundle。326 装备碎片、334 普通物品碎片、340 随机古遗物碎片按官方 `ItemIcon.*Piece.ShowProgress` 显示 prefab 中的 `Progress/LeftTxt` 与 `RightTxt`，需求数分别读取装备／物品碎片合成表与随机兑换表，颜色取已登记的 `GlobalColors` 进度配置。数量格式已按官方 `CultureNumberArg.Compact`、简中 `NumberCultureFormatter_CN` 及 `NumberFormatHelper` 迁移 10,000 进位、3 位有效数字和“万／亿／兆／京”单位。该组件目前尚无场景／导航入口接线，也未通过 Unity 资源导入及运行验证。幻兽碎片的拥有状态分支、完整筛选行为和各类详情动作尚未实现；基础详情目前只有关闭／取消行为，不能替代官方专用详情流程。

`OfflineInventoryCatalog.DetailKind` 按原 `ItemDetailDialogFactory.GetItemDetailDialogType` 的顺序判定技能、外观／家具及可使用物品的专用详情类别；可用物品的工具包、快挂机和物品使用效果仅在原 `CanUse` 分支下分类。

上述规则和存档代码仅部分连接到基础行囊视图；尚无本功能在 Unity 完整接入并运行验收的结论。原代码、配置、已转换素材和旧引擎测试不作为界面及玩法已还原的证据。

## 自定义实现

物品使用、奖励、随机与计数由本地事务统一提交。当前格式中无效或已停用的专用物品不作转换，也不制造补偿。

用户确认的缺图物品保留真实名称、数量与品质框，不绘制替代图；赛季宝石旧回收分支关闭且不补造回收奖励。

原版 APK 和 CDN 资源逐项核对了清单列出的24个候选图标包：22个 CDN 缓存匹配原清单大小／MD5及收据 SHA256，2个 APK ZIP 项匹配原清单大小／MD5；UnityPy 1.25.4 解码并扫描 Sprite 与 Texture2D 后，12个目标对象名均不在对应包中。范围与校验结果记于[物品来源清单](../../assets/reference/bag-items-source-manifest.json)。这些图标确认为原版资源缺项。原 `ItemIconHelper.GetFakeIcon` 取本地化物品名末四字，宝箱碎片先按 `Formula.GetTreasureIdByPieceId` 转为宝箱 ID；行囊及通用图标应复用此规则，保留官方品质框和数量，不以其他图片代替。原 `ItemIconManagerConfig` 按物品 ID 特配、物品类型、默认 prefab 的顺序选图标模板，Unity 目录读取层现已提供对应模板映射和缺图文本截取入口，仍待实际控件及界面接入。

用户于2026-10-19授权补齐特殊物品本地结算，替代此前砥石仅预览的边界。服务端请求的资格复核、物品扣除、词条生成、自动锁定及修复退款改由同一本地存档事务提交；保存失败整体回滚，取消不消耗也不推进随机数。工具包重置保留已积累的产量和解锁状态。

砥石逐份记录到期时间，使用／分解优先处理最早到期的一份，本地时钟用于期限推进；到期条件沿原 `PlayerItemDataWrap.IsExpired` 的严格“大于”判断。每份物品的获得时间随当前格式的新增记录写入。

用户确认联动活动以本地存档创建时间对应官方服务器开服时间限制，并以本地存档事务替代服务端抽奖结算。新档创建时保存创建时间。

史莱姆与孤独摇滚联动及八位关联伙伴暂不开放；此决定优先于此前联动日历与常驻入口安排，范围见[暂不开放约定](offline-access.md#暂不开放的联动活动及伙伴)。

### 已确认的本地接线与边界

`scripts/prepare_special_items.py` 从固定 `871_156598` APK／匹配 CDN 的 `prefabs_ui_equip` 读取远古砥石的装备页、词条行、候选、确认和结果 prefab，并从 `config_luban_equipattribute` 解码 `equip_attribute_replace_range.g.bytes` 的20条记录。运行数据为 `assets/official/special-items.json`，原精灵复用或无损转换，不绘制替代窗口。逐对象、原客户端／共用逻辑、配置与输出哈希登记在 `assets/reference/special-items-source-manifest.json`；服务端请求的本地替代及用户确认边界见行囊功能文档。

## 尚未实现

- 行囊场景／导航入口及 Unity 导入后的实际显示；现有代码已生成基础分类、排序、列表和简易详情控件，但尚未验证官方 UI 树与 Sprite 的实际导入绑定。红点排序、幻兽碎片的已拥有状态／进度分支、完整筛选交互及独立装备详情仍未实现。
- 来源导航、宝箱及特殊物品的官方专用详情与本地结算；奖励反馈、随机、扣费、取消、保存失败回滚尚未接线。
- 恢复其余官方 Unity 代码、配置、prefab、资源与调用关系，完成页面返回栈、实际入口和跨模块界面接线。
- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。

## 代码与资源入口

当前 Unity 查询、排序与事务实现见 [OfflineInventoryCatalog](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineInventoryCatalog.cs)、[OfflineInventoryOperations](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineInventoryOperations.cs) 和 [OfflineMainlineProgressSession](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs)。

行囊数量格式依据为官方 [CultureNumberArg](../../.tools/source-packages/official-code/framework-firstpass-source/Framework/CultureNumberArg.cs)、[NumberCultureFormatter_CN](../../.tools/source-packages/official-code/framework-firstpass-source/Framework/NumberCultureFormatter_CN.cs) 和 [NumberFormatHelper](../../.tools/source-packages/official-code/framework-firstpass-source/Framework/NumberFormatHelper.cs)。

原行囊代码依据为 [PlayerBagPanel](../../.tools/source-packages/official-code/client-source/PlayerBagPanel.cs)、[BagItemClientComparer](../../.tools/source-packages/official-code/common-source/BagItemClientComparer.cs)、[ItemDetailDialogFactory](../../.tools/source-packages/official-code/client-source/ItemDetailDialogFactory.cs) 与 [ItemSourceDataManager](../../.tools/source-packages/official-code/common-source/Common/ItemSourceDataManager.cs)。原窗口层级、配置、图标绑定与缺失项见[物品来源清单](../../assets/reference/bag-items-source-manifest.json)。

特殊物品、外观预览和联动活动的客户端依据分别见 [AppearanceChestItemUseDetailDialog](../../.tools/source-packages/official-code/client-source/AppearanceChestItemUseDetailDialog.cs)、[PreviewAppearanceDialog](../../.tools/source-packages/official-code/client-source/PreviewAppearanceDialog.cs)、[ActivityEntryLinkageActivityHandler](../../.tools/source-packages/official-code/client-source/ActivityEntryLinkageActivityHandler.cs) 与 [WheelGachaContentBase](../../.tools/source-packages/official-code/client-source/WheelGachaContentBase.cs)。活动及预览数据来源见[联动活动来源清单](../../assets/reference/linkage-activities/source-manifest.json)和[外观礼盒预览来源](../../assets/reference/appearance-chest-preview/source-manifest.json)。
