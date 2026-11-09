# 羁绊冒险

[总索引](../README.md)

状态：部分完成

## 功能简介

从游记原羁绊入口进入NPC冒险；原配置880节点，当前按用户意见开放720节点，至第17国第8区第20节点15860。

## 原始官方实现

羁绊冒险读取原 NPC、地图、关卡、邀请资格、站位、战前演出与奖励配置，使用官方对应界面和队伍流程。

### 已确认的玩法规则

用户提供的规则截图显示：解锁羽之国后，从森之国开始羁绊冒险；每关可邀请3名伙伴，每天最多挑战10关，每次通关都会提高奇景委派的奖励收益。每天出现4个奇景委派位置，每名伙伴每天最多完成1次；手动刷新每次消耗100晨星。奇景刷新概率为稀有70%、史诗26%、传说3.5%、奇迹0.5%；连续刷新保底分别为史诗5次、传说30次、奇迹100次。奇景满足的委派条件提高大成功概率；大成功概率不超过100%时可获得2倍惊喜奖励，超过100%时可触发极大成功并获得3倍惊喜奖励。羁绊节点提升的旅行基础数量、基础百分比和额外成功概率按完成节点顺序累计，细节见[伙伴旅行](partner-travel.md)。

用户确认离线结算：进入战斗时立即扣 1 次；胜利按所挑战关卡的原 `stage.Awards` 配置发奖，失败或退出不发奖。中断后恢复同一战斗，不重复扣次；战斗启动挂载失败时回滚本次扣次。用户另确认首关手动挑战，通过首关后，一键碾压只处理当前下一关，共用每日挑战次数，按该关 `stage.Awards` 发奖；后续关卡不再依赖联网赛季段位判定。

### 待迁移的规则与表现

原MapSlot、GeographySlot、旋转、节点状态／头像／动画、挑战按钮、三人选择、刷新和基础奖励说明须接入。每天最多原十次，节点属性按连续实际通关推进。

羁绊战斗阵容读取助战类型6的三名NPC（不是旅行派遣队伍），按原NpcInfo的FigureModelWithWeaponUI／FigureModelUI路径显示原模型，并播放原RunAnimName／IdleAnimName或职业run_03／idle_01。主角播放原100/run_03或200/run_03，角色与地图使用原AnimTimeScale／RotateSpeeds。挑战时停止地图旋转、切换主角和伙伴待机，播放NpcAdventurePanel_CombatTrigger；原动画0.5667秒结束后才提交本地战斗动作。

原奖励说明展示当前区域全部未通关节点；区域全清后，直到下一地区首节点完成再切图。已取得原玩家模型放原槽位，第一节点通过后进入伙伴旅行，不跳到幻兽派遣。

地图、区域、当前节点、下一挑战、旅行候选与新派遣的节点属性共用实际开放范围。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`scripts/prepare_npc_adventure_models.py` 根据固定版 `npc`、`npc_level_detail` 与 `FightEntityId` 确定 95 名可配置羁绊助战候选；固定版 AssetTable 中有 91 名候选的 `FigureModelUI` 与 `FigureModelWithWeaponUI` 原 prefab 路由，并据此转换皮肤合成、SkeletonGraphic、Idle／Run 动画及引用贴图。输入对象、原客户端源码、转换器与输出哈希见 [来源总表](../../assets/reference/SOURCES.md)。运行时按原 `NpcAdventureModelItem` 规则优先选择 WithWeaponUI，缺失时回退 UI 模型，并读取该 NPC 的原 IdleAnimName／RunAnimName。

固定版 AssetTable 的 UI prefab 路由仅覆盖其中 91 名；11015–11018 的 FigureModelUI 与 FigureModelWithWeaponUI 均无路径记录。已取得的固定版 NPC 骨骼包不含这些 prefab；新版 `864_156496` manifest（`.tools/source-packages/cloud-research/current-manifest.v3.bytes`，SHA256 `30d8debb4b9541668321f57c5e116e916d38a8bcb7a78f3b2f3161a902acfbb7`）也无四名 NPC 的对应 UI prefab 路径。因此来源清单分别记录全部候选、91 名可转换候选及四组缺失路径，不重建替代 prefab。

羁绊主角复用固定版男女角色资源；`scripts/prepare_profession_appearance.py` 同时输出原 `100/run_03`、`200/run_03` 职业动作，再由 `scripts/prepare_character_appearance.py --art` 转换到实际使用的男女外观皮肤和武器。战前曲线来自 `assets/official/ui.json` 的 `NpcAdventurePanel_CombatTrigger`（0.5666666627秒）；原 `NpcAdventurePanelMainContent.cs` 先停地图旋转并切换待机，再由 `NpcAdventurePanelMainContentAnimController.cs` 在动画结束后调用挑战。适配沿用项目离线战斗执行，不改原角色、伙伴、动画或战斗配置，不绘制替代素材。

## 完整还原部分

`OfflineNpcAdventureProgress` 与 `OfflineNpcAdventureSession` 保存已完成节点、当前挑战节点、每日挑战计数、助战伙伴选择和战斗 checkpoint；会话读取选定 `884_156692` 的节点目录，按授权范围顺序计算下一关，并读取官方每日上限和重置时刻。游记中的原羁绊入口现在在羽之国解锁后可打开官方羁绊面板；战斗按钮接入三名助战选择、原地图关卡、实际战斗、checkpoint 恢复和原 `GameEndPanelNpcAdventure` 结算资源。战斗助战快照按 `PlayerRobotInvite` 的 `PlayType=6` 提交和清理。胜利奖励由 `stage.Awards` 投影并和节点进度原子保存。

一键碾压按钮独立结算当前下一关，不启动战斗或提交伙伴助战快照；挑战次数、关卡推进和该关奖励在一个本地库存事务中保存。结算后复用原关卡结果资源并刷新至下一开放地图。

游记入口、三名伙伴选择窗、伙伴条目、节点奖励条目与结算窗的原组件字段由 `prepare_unity_npc_adventure_ui_bindings.py` 从选定 884 官方 prefab 提取；图像、文字、布局和射线字段写入 `npc-adventure-bindings.json`，运行时复用公共官方绑定模块。结算物品按所选 884 原物品表读取质量、物品类型和原图标，覆盖 `stage.Awards` 涉及的 15 种物品，独立于日常副本奖励目录。来源与输出哈希见 `assets/reference/partner-travel/npc-adventure-ui-source-manifest.json`。Home 图像与此绑定模块的 Windows 文件读取支持长安装路径；地图与结算窗的 SafeArea 使用已迁移的官方安全区域组件，保留原零透明度 CanvasGroup。结算 UIMesh 使用原 `CommonJieSuanDown` 材质参数和已核验的 Windows `UI/EffectDistort` 转换；原生依赖、脚本与材质／shader 输出见 `assets/reference/npc-adventure-ui/884_156692/source-manifest.json`。

当前验证覆盖静态元数据检查、Unity 导入／编译、`ProjectTools.Validate`，以及开始扣次、失败结算、助战快照、单关碾压精确奖励、节点推进和每日上限的规则检查。Play Mode 使用隔离存档和模拟点击验证挑战打开选人、取消不扣次、首关禁止碾压、完成首关后碾压下一关、奖励条目图标绑定、继续挑战、选择三名伙伴和实际战斗挂载；检查了页面、结算、选人与战前准备截图。首次通关在交互验证中以规则夹具准备，未执行首关完整手动胜负流程；三名伙伴的战斗数据与 checkpoint 已验证，战斗模型在画面中的完整呈现尚未验收。完整冷启动恢复、逐帧官方表现与 Windows Player 构建／发布未完成，本次未更新正式可执行版本。

## 自定义实现

第19国160节点配置和旧通过记录保留，因原两版本完整清单尚未取得对应专用地图，用户明确暂不开放，取得真实地图后再恢复；不绘制替代地图。已有旅行仍按原派遣快照领取。

战斗开局次数与伙伴助战快照分别写入原有的角色进度和助战存档；checkpoint 由可恢复战斗写入。每日刷新沿用原配置的本地日界。扫荡的次数、进度和物品使用一个角色库存事务提交。三个伙伴助战入口（探索首领、节庆首领、联合行动）按用户决定保留未完成，羁绊冒险助战目录仍需与 884 关卡版本核对一致后才能宣称完整迁移。

## 尚未实现

- 地图切换 `NpcAdventurePanel_MapSwitchCloud` 已在页面创建后接入，已检查页面运行截图；官方战前 0.5667 秒地图／伙伴演出、地图节点与旅行提示的逐帧状态、官方角色小模型绑定，以及羁绊整条流程的冷启动／发布版画面验收尚未完成。当前授权只开放720节点至15860。
- 结算窗角色头像、奖励区布局与节点加成文字、部分伙伴技能槽、地图节点文本替换与角色小模型仍未完整绑定；已恢复的原窗口和按钮不代表整套官方表现验收完成。
- 羁绊助战目前复用历史 `871_156598` NPC 战斗目录，而关卡和节点目录来自 `884_156692`；需核对并统一伙伴数值、技能与资格配置版本。
- 第19国真实地图未取得，按用户决定不开放；NPC11015～11022所属联动继续暂停，不以替代模型或已取得资源开放。

## 代码与资源入口

`scripts/prepare_npc_adventure_models.py`（来源工具标识）、`scripts/prepare_profession_appearance.py`（来源工具标识）、`scripts/prepare_character_appearance.py`（来源工具标识）、[NpcAdventureModelItem.cs](../../.tools/source-packages/official-code/client-source/NpcAdventureModelItem.cs)

[NpcAdventurePanelMainContent.cs](../../.tools/source-packages/official-code/client-source/NpcAdventurePanelMainContent.cs)、[NpcAdventurePanelMainContentAnimController.cs](../../.tools/source-packages/official-code/client-source/NpcAdventurePanelMainContentAnimController.cs)、[ui.json](../../assets/official/ui.json)、
