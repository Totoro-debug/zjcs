# 设置、拍照与本地存档

[总索引](../README.md)

状态：未完成

## 功能简介

启动进入本地存档选择页，可以新建角色、继续已有旅程或恢复未完成的创角；游戏中从功能菜单的设置页切换存档。选中存档读取失败时进入该档的本地恢复页。功能菜单同时保留原设置和主城拍照。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

已有 [ProfileSlotCatalog](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs) 的槽位发现／创建和 [LocalJsonSaveFile](../../unity/Assets/Game/Runtime/Offline/State/LocalJsonSaveFile.cs) 的临时写入、备份、损坏原字节保留与恢复代码。启动界面、完整角色格式、设置、角色切换及离线收益尚未接通，不能据此认定存档系统已完成。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

Windows 采用竖屏等比缩放、中文和多个独立本地角色存档，移除推送、联网登录、账号、社交、振动和网络预下载。

### 多存档与角色切换

本地目录替代服务器／角色身份；每槽保存独立进度、草稿、库存、设置、随机状态及地图／战斗检查点。列表浏览不结算收益；进入时才处理该档时间。切换前保存，失败保留原会话。

### 存档字段与事务接线

扣费、奖励、随机、跨日和领取状态在同一本地事务提交，失败回滚并重新绑定状态。主档损坏优先读取有效备份；两者均损坏时保留原字节，由用户显式选择后续处理。时间回拨不刷新额度，负差按零，检查点只前进。完整 Unity 字段与版本仍须随模块接入定义。

新建槽位时记录本地创建 Unix 时间，并在打开进度存档时写入 `CreatedAtUnixSeconds`，供已确认以开服时间计算开放资格的功能读取。没有创建时间记录的旧槽位保持未知，不按目录时间补推。

本地主城拍照保存视口PNG，失败恢复界面；完整原相册、滤镜、姿态、贴纸与编辑页尚无 Unity 实现。Bloom 应恢复原 Unity 组件；旧引擎屏幕采样近似不沿用。

## 尚未实现

- 启动存档选择、新建和角色切换、设置、完整角色数据保存／恢复、实际进入时离线收益及主城拍照。
- 恢复官方 Unity 代码、配置、prefab、资源与调用关系，完成本地事务、实际入口和界面接线。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

原设置四页、图片、中文、选中状态和点击声须接入。总／音乐／音效／语音按0～100%分别控制，后三项默认静音；音量组合实际作用于BGM、界面声与剧情配音。

原分辨率低／中／高／极致对应540／720／1080／窗口原生宽，受实际窗口限制并即时更新视口；30／60帧默认60，原隐藏90帧／动态项仍隐藏。长位数完整／简略使用万／亿并截断，不高估余额。

Bloom 强度须恢复原 0／25／50／100 四档及原服务调用；原设备默认值尚未从服务初始化代码确定，不能将旧本地默认100标为官方默认。

设置立即保存且不推进玩法随机，失败恢复值和实际渲染／播放。关闭、空白与Esc沿原模态处理。

原可保留的玩法操作都经费用、奖励、随机、库存和回执共同提交，关闭保存失败不立即退出。

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

、

[source-manifest.json](../../assets/reference/settings/source-manifest.json)、[UIBloomManager.cs](../../.tools/source-packages/official-code/client-source/UIBloomManager.cs)、[BloomIntensitySettingsItem.cs](../../.tools/source-packages/official-code/client-source/BloomIntensitySettingsItem.cs)、[LoginServerSetterUI](../../.tools/source-packages/official-code/client-source/LoginServerSetterUI.cs)

[PlanesListPanel](../../.tools/source-packages/official-code/client-source/PlanesListPanel.cs)、

、[ProfileSlotCatalog.cs](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs)、[LocalJsonSaveFile.cs](../../unity/Assets/Game/Runtime/Offline/State/LocalJsonSaveFile.cs)
