# 游记入口与日常副本

[总索引](../README.md)

状态：未完成

## 功能简介

游记入口汇集日常副本、地图、竞技及每日／每周挑战；各玩法的具体规则由所属页面维护。

日常副本已统一公共规则、关卡建模、官方界面、伙伴邀请、战斗与本地存档；本轮正在完成最终 Windows 发布验证。游记其他玩法与竞技备战仍有独立缺项。

## 原始官方实现

日常副本由 `DailyDungeonEntryPanel`、`DailyDungeonDetailContent`、路线与结果面板及 `DailyDungeonBattleInfo`、`DailyDungeonMapGroupInfo` 配置驱动。难度、等级、区域进度、评级、房间、战斗和奖励规则使用各自原配置；Type 22 是按累计成功领奖次数发放奖励的成就页，不增加每日领奖次数。

竞技场入口使用 `ArenaEntry`、守卫、名次、匹配和奖励配置。原客户端将榜单、对手和战斗结果请求交给服务器，未提供完整的服务端结算规则。

日常副本规则见[运行来源清单](../../assets/reference/dungeons-runtime-source-manifest.json)，当前 884 界面绑定见[副本界面来源清单](../../assets/reference/dungeon-ui/884_156692/prefab-binding-source-manifest.json)；871 Map12 私有路线仍引用[历史界面绑定回执](../../assets/reference/dungeon-ui/prefab-binding-source-manifest.json)。竞技场来源版本与项目范围见[官方来源总表](../../assets/reference/SOURCES.md)。

## 完整还原部分

### 日常副本

`HomeFeatureRuntime` 统一使用公共 `DailyDungeonModule`；`OfflineDailyDungeonModule` 仅保留兼容入口。世界之树、机神山、海之宫遗迹、其他公开副本及 Map12 私有路线都选择官方目录行、参数和适配器，不复制副本规则。入口、路线／HUD、结算与馈赠分别由 `DailyDungeonEntryPageModel`、`DailyDungeonRouteHudModel`、`DailyDungeonSettlementHudModel`、`DailyDungeonGiftModel` 聚合。公共模块统一处理区域与难度解锁、评级、奖励概率与翻倍、领奖次数与购买、个人排行榜和 Type 22／57 馈赠；路线、关卡、榜单、翻倍加成、经济与有效福利通过对应接口接入。

当前 884 原生入口、详情、难度页签、路线、购买、排行榜、馈赠和结算 prefab 保留官方层级、布局、图片、字体与交互区域。`DailyDungeonUiBindingModule.Apply` 封装原字段、版本选择、Transform、Sprite、TMP、材质、动画和缓存；调用方只提供 prefab 标识与实例。38 组封面／背景／建筑图读取原 `DailyDungeonSettings.Datas` 的 Sprite GUID，不按编号猜测资源名；原 `NoDrawingRayCast` 的透明点击区域、颜色、padding、启用和射线状态统一恢复。实际目录画面已核对世界之树、机神山与海之宫遗迹。

目录名称、默认难度说明、详情标题与难度说明沿官方 `LanguageUtils.GetDailyDungeonNameWithoutDifficultyKey`、`GetDailyDungeonNameKey`、`GetDailyDungeonDescKey` 取中文；私有详情沿无难度名称。入口、详情、购买与结算的次数、价格、评级、等级范围等动态参数填入原语言模板。原“前往副本”按钮进入或继续本地路线／待领奖，开战前复用公共 `OfficialBattleAssistSelectionView`、原伙伴邀请资源与 `NpcAssistPlayType.All`；服务器匹配、取消匹配及在线队伍邀请按钮隐藏。

详情 `AwardGrid` 由 `BuildRewardPreview` 汇总当前难度评级奖励与房间惊喜奖励，应用官方条件、等级／转职计数和道具排序，保留固定数量与随机候选。原 `ItemIcon` 配合原 `ProbabilityGo` 附件显示 `DailyDungeonUtils.ShowDialogAllAward` 的概率标识，不伪造单项百分比。`RandomReward` 装备占位按配置权重生成真实装备实例。公共装备目录仍保留其 `854_156383` 来源身份；已核对当前 884 的 1,223 条装备及对应物品、品质／类型／词条／被动候选、等级上限、来源组、祝福等级和三个掉落等级参数，生成所需配置一致。

`DailyDungeonRewardAssetCatalog` 覆盖奖励预览、随机装备池和两类馈赠：1,164 个道具显式绑定 586 张原 Sprite，包含 861 件装备、71 个普通装备碎片、1 个普通道具碎片和 231 个普通道具。它按所选 884 `ItemDetailDialogFactory` 保存四种详情分流类型，并校验来源版本、唯一标识、Sprite 与类型数量。贴图保留打包旋转、紧密网格遮罩、逻辑矩形留白、pivot、border 和 PPU。四类详情 prefab、字段与各自实际动画已提取，公共物品控制器、合成、使用和来源跳转按用户决定另行迁移，当前副本图标不开放该入口；资源接入不表示详情交互完成。见[奖励图标来源](../../assets/reference/dungeon-ui/daily-dungeon-reward-icons-source-manifest.json)、[详情原生资源](../../assets/reference/dungeon-ui/884_156692/daily-dungeon-result-ui-source-manifest.json)与[字段来源](../../assets/reference/dungeon-ui/884_156692/daily-dungeon-result-bindings-source-manifest.json)。

奖励由 `OfflineDailyDungeonSettlementModule.Preview`／`Claim`／`SettleClearedRooms` 统一结算，保持“预留次数 → 主档奖励与实际明细入库 → 确认副本次数与领取状态”的顺序。预览以领取回执为种子重放正式概率、奖励顺序和装备随机流，不改存档；正式领取沿同一回执生成结果，零掉落也保存回执。历史已入库回执读取原明细，不重新抽取；主档已提交而副本确认失败时，冷打开补齐确认，不重复抽奖、发奖或扣次。结果页播放原 `GameEndPanelBase_Show`，使用原模糊遮罩，恢复与家园叠加画布一致的排序层及前后顺序；领取区按原 `DailyDungeonCostMoneyContent.Refresh` 顺序恢复 `FreeStateSwitcher` 与 `mAwardTimesSwitcher`，逐项保留 `StateGameObjectConfig` 的共用对象及 `StateMultiLanguageConfig` 文案；单次领奖隐藏原批量标记，领取后切换原已领取背景／文案且不可交互，次数参数同步更新。

结算保留房间掉落与评级开箱奖励的来源标记；旧明细缺少该标记时保留原明细并显示在掉落区，不重抽或推测来源。主档实际发放与随机流顺序不变，原结果底部只展示房间掉落，评级奖励进入玩家行原宝箱。同一道具来自房间掉落与评级奖励时分别保存，存档按“道具 ID、奖励来源”联合校验，仍拒绝同一来源的重复行，避免合法的两类奖励被拒绝入库。四种原 `map_baoXiang` 骨骼、atlas 和像素接入公共独立原生 Canvas 后端，按原评级选择与 `close_loop → open_01 → open_loop` 序列播放；奖励槽使用原 prefab、动画事件、逐项显示间隔与自动滚动参数。原九个开箱／品质 Playable 图、粒子、网格、Windows 材质和三个 Wwise PCM 事件在本地绑定。来源见[原生宝箱](../../assets/reference/dungeon-ui/884_156692/daily-dungeon-chest-native-source-manifest.json)与[结果特效](../../assets/reference/dungeon-ui/884_156692/daily-dungeon-result-playables-source-manifest.json)；内置屏 Play Mode 已实测机神山完整路线、开箱、逐项奖励与购买／领奖，已查看实际开箱前后画面；Windows Player 复验仍在执行。

公共路线倒计时由 `DailyDungeonModule` 保存起始 UTC 时刻并生成剩余秒数，`OfficialDungeonRoutePresenter` 使用原 `mNextStageConfirmTimeTxt` 与 `MultiLanguageText.Format` 显示；时长读取所选官方 `DailyDungeonSelectNextStageOverTime`（10 秒）。超时和手动选择都沿同一房间前进与伙伴邀请入口；旧路线首次显示选择页时初始化起点，已有起点在重开后保持。规则回归已覆盖优先级、同类顺序、10 秒边界、手动立即前进与冷恢复；内置屏 Play Mode 通过实际倒计时、超时前进、手动房间点击和中途结算，并已查看对应画面。Windows Player 复验仍在执行。

`DailyDungeonBattleRunner` 按原房间战斗配置接入公共战斗控制器。`DailyDungeonBattleEntryModule` 统一公开副本、Map12 私有路线与每日挑战的入场一致性：恢复时核对关卡、StageType、助战身份及 checkpoint；新战斗按“提交助战 → 挂载战斗 → 首次保存 checkpoint”执行，失败只回滚本次提交。玩法各自保留入场条件、存档适配器、挑战结算和空助战／已结束恢复分支。路线、已清房间、战斗检查点及待领奖状态均持久化。

排行榜使用当前原生 `DailyDungeonTopPanel`、`TopRankTeamItem`、原 `LoopVerticalScrollRect`、页签、对象状态与布局。`DailyDungeonLevelDefinition.LeaderboardGrades` 独立读取所选 884 `DailyDungeonBattleInfo.TopRecordGradeTypeDict`；APK 优先取得原 `config_luban` 字节并用同版 Luban 构造器解码，136 个副本／难度组合仅开放 S 榜，私有副本不开放。本人行按完整排序定位名次与用时，使用本地玩家原头像；伙伴图标使用原 NPC 图标控制器，接入公共 `OfficialPartnerNpcIconImages` 及原名称节点的本地化控制器；冷打开首通存档后已核对头像、中文名称、职业图标与遮挡。每个副本、难度、评级保存首次完整通关的实际邀请队伍，之后最快用时更新不替换首通队伍；旧最佳队伍字段不能证明首通阵容，不回填推测头像。来源见[排行榜配置与绑定](../../assets/reference/dungeon-ui/884_156692/daily-dungeon-leaderboard-source-manifest.json)。

Type 22 按累计成功领奖次数生成原成就行，不增加每日次数。Type 57 从当前官方 409 条奖励门槛按 Group／Index 生成固定标识；`FinishAndPersist` 在公开完整通关事务中累计指定副本、难度、准确评级的次数，退出、私有路线和重复完成不累计。领奖回执进入主档奖励事务后保存已领取状态。原表 137 组中，44／Hell 无上榜配置且缺标题，保留原行与来源记录，不开放入口。馈赠页恢复原点击区域、状态切换顺序、进度条和经输入哈希核实的同源 Windows `UI/Gray`，保留可领取／已领取共用宝箱。见[排行榜馈赠来源](../../assets/reference/dungeon-ui/daily-dungeon-top-achievements-source-manifest.json)。

当前 884 UI 转换保留 `UIImgAdd`、默认／染色 Spine 材质参数与官方 shader 程序身份，Windows 绑定写入[原生 UI 来源](../../assets/reference/dungeon-ui/884_156692/source-manifest.json)。原显示／隐藏／循环动画的曲线与事件保留，脚本引用适配 Windows UGUI／TMP；默认 Spine UI 的原六组程序由 `OfficialSparseShaderVariants` 限定构建。依赖内 11 个原 DLL 保留字节、GUID 和来源哈希，但禁用所有 PluginImporter 平台入口，运行本地迁移代码。历史 871 Map12 界面继续保留历史版本身份，不修改预期哈希。

开箱与品质 Playable 的原 `Noise_Mask_Pro` 另登记到同一 Windows 构建筛选器：保留两个 Pass 各自的 260 组原始关键词程序，仅编译原文件实际存在的组合，新增 1,040 个 GPU 阶段。载荷与原始来源哈希沿用上述结果特效凭据；登记后已通过清理构建缓存的完整内容 Windows 构建，日志无着色器错误，全部 55 项稀疏程序登记合计 50,868 个 GPU 阶段。该构建检查不作为开箱 Player 流程完成的依据。

验证已通过公共模块规则、Unity 导入／编译与 `ProjectTools.Validate`。规则覆盖目录与首档条件、跨日次数／购买重置、概率与翻倍适配、预留／入库／确认失败重试、冷打开去重、同一道具的房间／评级双来源持久化及同来源重复拒绝、首通队伍在更快重跑及两次重开后保持、Type 57 难度隔离及失败重试。窗口化 Play Mode 通过目录、奖励预览、路线保存恢复、中途退出、结算领奖、榜单页签／关闭、Type 22 往返和 Type 57 未达标／达标／已领取；结算、榜单和馈赠按钮采用射线模拟指针，部分路线夹具通过模块推进，不冒充完整战斗。已查看实际目录、详情、伙伴邀请、战场、榜单、馈赠、结算与领取后画面；本轮结果页无洋红材质。

`DailyDungeonPlayerValidation` 只在显式参数 `--zjcs-validate-daily-dungeon <输出目录>` 下运行：等待真实启动页，创建独立档案，模拟原按钮与房间点击，邀请伙伴，经 `DailyDungeonBattleRunner` 的真实战斗推进路线，再检查领奖扣次、冷打开、关闭去重和首通队伍。仅在隔离测试档案中通过已有额外属性来源提高角色生命、攻击与速度以缩短战斗；在战斗构造前准备，包含原开场状态结算，不调用完成回调。当前 Play Mode 已走过 8 个原房间与 4 场战斗；最终 Windows Player 及发布路径验证正在执行。`scripts/check_unity_project.py` 与 `scripts/test_dungeon_ui_binding_paths.py` 通过，后者重放 3,229 个原节点、原 Button Graphic 与点击／动画绑定；静态检查不代替上述实际运行。

### 竞技场

`OfflineArenaSession` 与 `ArenaBattleRunner` 使用原守卫、名次、匹配区间和战斗场景。地图进度达到原入口条件后开放；离线对手取自本地守卫模板，支持刷新、购买次数、挑战、战斗或直接胜利结算。

竞技场结算迁移所选 884 的完整原生 `GameEndPanelArena` 及 `ArenaShowFightResult`、`ArenaOpponentIcon`、`CombatRatingPromoteWayItem`，保留原对象层级、胜负分支、标题、守卫与本人外观、名次变化、奖励数量、动画和 Wwise 事件绑定。覆盖本地主动挑战守卫分支；防守通知及他人战斗不生成虚构记录。结算使用已经提交的实际胜负、前后名次、守卫与奖励快照，窗口和统计重开不再次发奖。正常战斗在最终演出和战报交接后显示结果，战场保留至原按钮离开；直接胜利不创建战斗和统计。原“提升技能／装备”按钮离开战斗后进入现有角色功能；装备目的页仍受[装备功能](equipment.md)的还原缺项限制。

资源与转换见[竞技场结算来源](../../assets/reference/arena-result-native-source-manifest.json)，入口为 `HomeHudView.ArenaResult`、`ArenaResultWindow` 与 `OfficialArenaResultAssets`。内置屏 Play Mode 使用真实入口和模拟点击，已通过胜利、失败、主动退出的取消／确认及直接胜利；核对一张入场票、胜负次数、前后名次、原奖励入库、磁盘冷打开结果及重复结算去重，正常战斗战报两次重开、双方姓名／头像、保留战场和原按钮离开后的释放均通过。已查看四类结果及统计运行截图；测试仅调整隔离存档的对手、生命、攻击和速度以缩短实际战斗，不直接调用完成回调。场景进入结果前仍有既有无 AudioListener 提示；Windows Player 已构建，并从 `D:/Desktop/杖剑传说/杖剑传说.exe` 实际启动专项入口，四类流程和原生结果／统计截图通过，音效输出仍未单独验证。

竞技场 Windows Player 专项入口为 `ArenaResultPlayerValidation`，通过显式参数 `--zjcs-validate-arena-result <验证输出目录>` 启用。它等待真实启动页后创建独立存档，模拟游记、竞技、挑战、开始、退出、统计与离开按钮；只缩短测试生命／攻击／速度并选取原守卫，不调用完成回调。检查结果、截图与存档保留在指定验证目录；专项 Player 已返回通过，实际查看胜利、失败和技能统计画面，四类流程均完成。

### 私有副本

Map12 的 121／50 机神山路线共用日常副本运行时，其奖励、次数隔离和结算边界见[山之国地图](../maps/map-12.md)。

## 自定义实现

- 每日领奖按官方存档的总剩余次数结转：跨日取前一日剩余次数与每日免费次数的较大值，购买次数、价格档位和购买回执每日重置。单机仅在成功领取最终奖励时扣总次数，进场、失败和未领奖均不扣；界面不把总次数拆成无法由官方存档证明的免费／购买余额。官方客户端单次批量领奖次数为 `min(1 + 当前有效福利 ExtraClaimCount, 今日总剩余领奖次数)`；无有效福利时最大为 1。Type 22 只累计成功领奖次数。
- 按用户确认保留自动选路与倒计时，倒计时结束前手动点击房间立即前进，不等待在线投票。官方客户端 `DailyDungeonBigMapDialog.CountDown` 只读取服务器 `FightNextStageDailyDungeonData`，未包含超时选择算法；单机按已确认方案使用原 `DailyDungeonRoomPriorityList`，同类按原 `nextRoomIds` 配置顺序选择，不推断未提供的队伍血量或在线投票算法。房间清理时保存倒计时起点，路线页活动时处理到期；重新打开按已保存时刻继续，选择成功后清除旧起点，防止同一超时重复前进。私有剧情路线继续使用其原调用方的前进规则。
- 首个公开难度也读取其自身 `unlockConditionId`；入口页显示锁定状态，公共模块在条件满足前拒绝进入，条件适配器只由调用方提供。
- 离线排行榜只保留官方评级和通关时长两个排序字段：评级降序，同评级按通关时长升序；完全并列时保留存档原顺序，不再按记录 ID 排序，也不加入职业、伤害或客户端虚构的贡献分。本人行使用完整排序独立定位本地记录，不受滚动榜显示数量上限影响。公开副本开战保存公共伙伴邀请系统实际选中的 NPC 标识、等级与位阶；每个副本、难度、评级的首次完整通关队伍写入独立快照，后续最佳用时更新不改写队伍。旧最佳用时队伍不作为首通信息回填；没有首通快照的旧记录不显示推测队员。
- 排行榜馈赠 Type 57 使用已确认的单机方案：按本档案在指定副本、难度、评级下的累计通关次数判断，保留官方奖励与数量阈值。旧存档没有历次通关总数，新计数字段从 0 开始累计；保留已有最佳评级、排行榜与奖励回执。
- 评级使用官方地图评级行，不增加客户端未使用的职业贡献系数。`ServerRankUnlockDaysCondition` 按档案创建后经过的完整 24 小时计算。
- 奖励按官方条件、`Percent` 和候选权重结算；评级双倍概率为官方基础值 5%，未接入的本地加成按 0% 处理。单次结算领奖次数由 `IDailyDungeonAwardTimesAdapter` 提供，默认是官方基础值 1；只有本地档案已有且按官方福利来源、有效期判定为有效的 `DailyDungeonExtraClaim` 才按“1 + ExtraClaimCount”计算，不改变每日领奖池，也不新增福利发放方式。服务器没有提供的行为不推断为官方规则。
- 本次日常副本模块迁移不包含公共物品功能的完整迁移。用户已确认装备合成、道具使用、来源跳转等公共物品功能另行处理；四类奖励详情资源和分流配置作为后续公共物品模块的接入依据，不将资源接入表述为详情交互完成。
- 已确认的旧回执兼容：只保存入库标记而没有实际明细的历史奖励继续去重并补齐副本确认，提示“历史奖励已入库，明细不可恢复”，不重新抽取或推测历史装备。
- 竞技场用本地守卫和个人名次代替在线玩家及服务器赛季同步；胜利后使用原胜负奖励与对手名次结算。守卫附加外观按模板读取，性别在可用选项中均衡随机并保存。
- 既有单机“正常挑战／直接胜利”选择复用官方通用 `ConfirmDialog` 的 prefab、遮罩及动画，关闭原窗口后再进入挑战，避免战斗隐藏家园画布时打断关闭。取消遮罩不自动选中正常挑战。该选择不是完整官方 `ArenaFightDialog`；原备战界面的对手资料、技能及费用展示仍未迁移。
- 按用户确认，竞技场主动退出按失败结算，沿用 `CompleteChallenge(false)` 的失败次数及原失败奖励事务，清除本场检查点，不保留可继续的挑战。入场已经扣除的一张挑战票不退还，退出不重复扣票。战斗中先显示原失败确认，取消继续当前战斗；准备阶段按原单人分支直接离开。按钮、阶段限制和原生确认窗口由[公共战斗模块](battle-execution.md)维护。
- 按用户确认，日常副本中途退出正常结算：按已清理房间所在行取得当前评级，使用该评级配置奖励和已清理房间掉落；不补算当前未清理房间。达到评级的退出进入原待领奖事务，成功领取时扣领奖次数并累计领奖成就；未达到任何评级时仅发房间掉落。未走到终点不增加完整通关次数或排行榜记录。`OfflineDailyDungeonSession.Leave` 同时供路线退出和战斗退出使用，已提交退出的检查点可恢复后完成交接；确认文字按此单机方案调整，官方窗口布局不变。隔离内置屏 Play Mode 已核对退出确认、交接前中断与磁盘冷恢复；规则回归核对已清行评级、原房间／评级奖励、正常领奖扣次和重复请求不重复发放。已查看确认及返回画面，仍有既有家园表现缺项；此变更尚未通过 Windows Player 发布验证。

## 尚未实现

- 公共物品详情、装备合成、道具使用和来源跳转按用户确认另行迁移；四类副本奖励详情的原生资源及分流元数据已保留，图标入口暂不开放。公共工厂其他类型不能被假定为普通物品详情；地图探索奖励组预览不能代替物品详情。
- 游记其他玩法入口与竞技备战的完整还原不属于本轮日常副本公共模块；竞技对手资料、技能及费用展示仍见本页竞技部分。服务器匹配、在线目标和服务器榜单按单机方案处理。
- 最终 Windows Player 完整路线、实际领奖和首通伙伴画面正在验证，尚未据此标记日常副本发布完成。官方奖励找回记录／响应不在当前副本领奖流程内，仍需所属公共功能接入。

## 代码与资源入口

[DailyDungeonModule.cs](../../unity/Assets/Game/Runtime/Offline/World/DailyDungeonModule.cs)、[OfflineDailyDungeonModule.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineDailyDungeonModule.cs)、[OfflineDailyDungeonSession.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineDailyDungeonSession.cs)、[DailyDungeonBattleEntryModule.cs](../../unity/Assets/Game/Runtime/Offline/World/DailyDungeonBattleEntryModule.cs)、[OfflineArenaSession.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineArenaSession.cs)、[DailyDungeonBattleRunner.cs](../../unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs)。

排行榜 NPC 图标的伙伴数据由[伙伴 NPC 运行时程序集](../../unity/Assets/Game/Runtime/Offline/PartnersNpc/Zjcs.Offline.PartnersNpc.asmdef)提供，[Home 程序集](../../unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef)为此显式声明直接引用。

排行榜首通次数读取保留存档的 `long` 类型；规则验证按副本、难度、层数和玩家这组公开字段定位排行榜记录，分别核对首通队伍与最快通关队伍。

[日常副本规则与资源](../../assets/reference/dungeons-runtime-source-manifest.json)、[竞技场来源版本与范围](../../assets/reference/SOURCES.md)、[当前副本界面绑定](../../assets/reference/dungeon-ui/884_156692/prefab-binding-source-manifest.json)、[历史 Map12 界面绑定](../../assets/reference/dungeon-ui/prefab-binding-source-manifest.json)。
