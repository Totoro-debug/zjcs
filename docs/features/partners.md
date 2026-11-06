# NPC伙伴、好感与餐桌通话

[总索引](../README.md)

状态：未完成

## 功能简介

伙伴包括 NPC 名册、相遇、置顶、详情、好感与馈赠、档案、餐桌通话、羁绊和战斗助战。联动伙伴开放范围统一见[离线访问](offline-access.md)。

## 原始官方实现

原 NPC 配置、伙伴关系、礼物偏好、好感等级、档案与通话条件共同驱动伙伴功能。官方页面包括名册、详情、赠礼、称谓、属性、档案、通讯录和通话窗口；具体条件、排序和奖励以当前客户端代码与配置为准。

原客户端入口与反编译代码见 `.tools/source-packages/official-code/client-source/`；输入和转换来源由[来源总览](../../assets/reference/SOURCES.md)及相关模块来源清单记录。

## 完整还原部分

- Home 已接入原生伙伴名册和本地伙伴服务。结识状态、好感、置顶、赠礼和称谓使用本地角色存档。
- `OfflinePartnerGiftPlayer` 按官方偏好、专属礼物、阶位上限和溢出确认分支提交库存与好感事务；送礼重复请求不重复扣除。
- 通话配置、条件、进度、通讯录和打开伙伴页时的条件刷新已有本地接线；节点奖励与历史共同保存，重复回看不重复领奖。
- 家园、伙伴名册、详情、赠礼、通话、星命及好感说明的共用 UI 桥由 `OfflineUiBindingModule.Register(ownerRoot, bindings)` 管理。窗口只释放自身令牌；图片和对象池按请求节点的最近所属根选择，本地化和字体按有效注册优先级选择，最后一个窗口关闭后恢复原绑定。原 prefab、控制器、资源加载和动画继续使用原入口。
- 日常挑战与日常副本已接入原助战邀请窗口；候选按伙伴资格、好感、战斗模型和次数限制筛选。战斗规则和其他入口见[NPC邀请与战前准备](npc-assistance.md)。
- 伙伴动态立绘、头像、详情、位置导航、物品详情和完整音效尚未全部接通。

## 自定义实现

- 用户确认自动伙伴在创建角色及读档后检查原相遇条件；符合条件且尚未拥有时按原配置结识并初始化好感。该触发时机是单机方案，不声称取得服务器实现。
- 用户确认本地称谓在原字符集和长度校验通过后保存，不等待线上文本审核；原伙伴资格及好感门槛仍有效。
- 用户确认只在打开伙伴页时按原通话顺序检查未开始且符合解锁／连锁条件的通话，并按 UTC+8 重置规则处理每日限制。奖励与通话历史同笔保存，回看不重复领奖。
- 用户确认馈赠、档案奖励和通话奖励以本地原子事务保存；失败回滚，重复凭证不重复扣除或发奖。超过全局最高好感的礼物须经原确认后才保留溢出经验。
- 10611 的配置立绘资源缺失时，按用户确认显示同 NPC 的官方角色图片；10709 错误资源映射改绑其自身官方模型。资源选择不改变伙伴规则。
- 史莱姆、孤独摇滚联动及其八位伙伴按用户决定暂停开放，见[暂不开放约定](offline-access.md#暂不开放的联动活动及伙伴)。

## 尚未实现

- 完成伙伴名册、详情、赠礼、称谓、档案、通讯录与通话的正式窗口交互及本地存档恢复。
- 接通全部原立绘／小模型、导航、赠礼物品详情、动画、粒子和声音；完整 Home 画面与资源绑定仍有缺项。
- 正式伙伴助战仍需覆盖各战斗入口、完整队伍显示、次数／资格边界及战斗返回。
- 以正式 Home 入口完成模拟输入、画面、交易回滚、冷读和 Windows Player 流程。

当前绑定验证：规则入口 `OfflineUiBindingAcceptance` 已验证嵌套与并列窗口、两种关闭顺序、重复释放、本地化回退、稳定分发入口、根节点销毁和全局绑定恢复。Home Play Mode 已通过名册、详情、好感说明、赠礼点击与保存回读，实际查看对应画面；该范围不代表全部伙伴流程完成，Windows Player 覆盖仍须另行验证。

## 代码与资源入口

- 本地馈赠服务：[OfflinePartnerGiftPlayer.cs](../../unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerGiftPlayer.cs)；Home 路由：[HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)。
- 官方伙伴代码：[NpcDialoguePanel.cs](../../.tools/source-packages/official-code/client-source/NpcDialoguePanel.cs)、[NpcPanelNpcItem.cs](../../.tools/source-packages/official-code/client-source/NpcPanelNpcItem.cs)。
- 资源版本、输入哈希和转换方法见[来源总览](../../assets/reference/SOURCES.md)。
