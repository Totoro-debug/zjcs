# 设置、拍照与本地存档

[总索引](../README.md)

状态：未完成

## 功能简介

启动进入本地存档选择页，可以新建角色、继续已有旅程或恢复未完成的创角；主城功能菜单已接官方设置窗口。角色切换、损坏存档恢复、离线收益和拍照仍属未完成范围。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

已有 [ProfileSlotCatalog](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs) 的槽位发现／创建和 [LocalJsonSaveFile](../../unity/Assets/Game/Runtime/Offline/State/LocalJsonSaveFile.cs) 的临时写入、备份、损坏原字节保留与恢复代码。主城设置入口使用官方 `SettingsDialog` 控件树、30 张固定版 Sprite 和点击音效；[HomeSettingsProfile](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSettingsProfile.cs) 按角色槽保存音量、帧率和数值显示，写入失败时维持旧设置。设置页目前接通 30／60 帧、总／音乐／音效／语音滑杆和完整／简略显示；消息推送及单机不适用控件隐藏，游戏页语言控件固定为当前中文。

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

- 启动存档选择、新建和角色切换、完整角色数据保存／恢复、实际进入时离线收益及主城拍照。
- 设置页的 URP 分辨率缩放、Bloom、其他画质组和主城背景音乐尚未接入；当前 Unity 包清单没有 URP，现有设置页隐藏这些未接线选项。
- 恢复启动存档与拍照使用的官方 Unity 代码、配置、prefab、资源和调用关系，完成对应本地流程与界面接线。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 待迁移的规则与表现

以下项目仍须继续接入和验证；已接入设置的实际运行范围另行标明。

已接官方游戏、画质、声音、其他四页、中文资源、选中态和 `click.wav`。音量值按官方 `SoundSettingsUI` 的 0～1 语义保存；默认总音量100%、音乐／音效／语音0%采用已确认的单机设置。主控作用于 `AudioListener`，音效与语音控制主城设置点击、扭蛋演出音效及果树教学配音；主城背景音乐源尚未接入，音乐滑杆目前只保存独立配置。

30／60帧默认60已按官方 `EFrameRateMode` 接到 `Application.targetFrameRate`；官方90帧／动态项隐藏。完整／简略按确认的万／亿截断规则影响主城顶栏货币。分辨率低／中／高／极致的540／720／1080／窗口原生宽须复原 `RenderQualitySetter` 使用的 URP `renderScale` 后再开放；当前工程未引用 URP。

Bloom 的0／25／50／100四档、反射及其他质量效果须恢复原 URP Volume／设备调用后再开放；官方 Bloom 默认由设备档位和旧存档决定，不能用固定100代替。

设置使用角色槽 `settings.json` 立即原子保存，不推进玩法随机；失败时保留并重新应用旧值。模态可点空白关闭或按 Esc 返回。四页真实输入、画面、重启恢复、保存失败和音频表现尚待 Play Mode 验收。

原可保留的玩法操作都经费用、奖励、随机、库存和回执共同提交，关闭保存失败不立即退出。

### 依据与代码定位

[SettingsDialog.cs](../../.tools/source-packages/official-code/client-source/SettingsDialog.cs)、[SoundSettingsUI.cs](../../.tools/source-packages/official-code/launch-source/SoundSettingsUI.cs)、[FrameRateSettingsItem.cs](../../.tools/source-packages/official-code/client-source/FrameRateSettingsItem.cs)、[NumberDisplayRuleSettings.cs](../../.tools/source-packages/official-code/client-source/NumberDisplayRuleSettings.cs)、[RenderQualitySetter.cs](../../.tools/source-packages/official-code/launch-source/RenderQualitySetter.cs)、[BloomIntensityService.cs](../../.tools/source-packages/official-code/launch-source/BloomIntensityService.cs)

[settings source manifest](../../assets/reference/settings/source-manifest.json)、[settings runtime generator](../../scripts/prepare_unity_home_settings.py)、[HomeHudView.Settings.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Settings.cs)、[HomeSettingsProfile.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSettingsProfile.cs)、[ProfileSlotCatalog.cs](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs)、[LocalJsonSaveFile.cs](../../unity/Assets/Game/Runtime/Offline/State/LocalJsonSaveFile.cs)。载荷与规则测试：`python -B scripts/test_prepare_unity_home_settings.py`。
