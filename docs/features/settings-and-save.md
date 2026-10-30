# 设置、拍照与本地存档

[总索引](../README.md)

状态：未完成（启动存档页已转换为官方位面选择界面；设置完整表现、离线收益和拍照尚未完成）

## 功能简介

启动进入本地存档选择页，使用原版“选择位面”的面板、页签和角色卡片，可以新建角色、选择已有旅程或恢复未完成的创角；主城功能菜单已接官方设置窗口。完整角色切换、损坏存档恢复、离线收益和拍照仍属未完成范围。

## 原始官方实现

官方 SettingsDialog 定义设置分页与选项；PlanesListPanel／PlanesRoleItem 定义位面列表和角色卡。官方在线位面／角色服务与本地多存档适配分别记录。

### 待迁移的规则与表现

以下项目仍须继续接入和验证；已接入设置的实际运行范围另行标明。

已接官方游戏、画质、声音、其他四页、中文资源、选中态和 `click.wav`。音量值按官方 `SoundSettingsUI` 的 0～1 语义保存；默认总音量100%、音乐／音效／语音0%采用已确认的单机设置。主控作用于 `AudioListener`，音效与语音控制主城设置点击、扭蛋演出音效及果树教学配音；主城背景音乐源尚未接入，音乐滑杆目前只保存独立配置。

30／60帧默认60已按官方 `EFrameRateMode` 接到 `Application.targetFrameRate`；官方90帧／动态项隐藏。完整／简略按确认的万／亿截断规则影响主城顶栏货币。分辨率低／中／高／极致的540／720／1080／窗口原生宽须复原 `RenderQualitySetter` 使用的 URP `renderScale` 后再开放；当前工程未引用 URP。

Bloom 的0／25／50／100四档、反射及其他质量效果须恢复原 URP Volume／设备调用后再开放；官方 Bloom 默认由设备档位和旧存档决定，不能用固定100代替。

设置使用角色槽 `settings.json` 立即原子保存，不推进玩法随机；失败时保留并重新应用旧值。模态可点空白关闭或按 Esc 返回。四页真实输入、画面、重启恢复、保存失败和音频表现尚待 Play Mode 验收。

原可保留的玩法操作都经费用、奖励、随机、库存和回执共同提交，关闭保存失败不立即退出。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

设置功能补齐：`prepare_settings_assets.py` 从固定 `871_156598` 的 `SettingsDialog.prefab` 重建五个分页、原始控件和选中标记，并读取固定版本中文语言分包替换俄文 / 数字预览文本。分辨率、帧率和长数字选项按原组件的序列化枚举绑定核对。点击音效取自 APK 内 `Minimum.pck`：由原 `UIClickSound` 引用定位 `Main_Tag` 事件（680474747），经 HIRC 解析到 media 69663821，再用 vgmstream 解码为 `assets/official/settings/click.wav`。无自绘或合成音频；图片复用官方 Sprite，独立 UI 数据位于 `assets/official/settings/ui.json`。输入来源、bundle / 对象 ID、语言文件哈希及输出哈希见 [设置来源清单](../../assets/reference/settings/source-manifest.json)。主城运行载荷由 `scripts/prepare_unity_home_settings.py` 从该官方树生成，复制30张原 Sprite 与点击音效并将输出哈希登记在同一清单。运行边界见 [设置功能](settings-and-save.md)；URP画质、反射和录像尚未移植，不能因官方资源已取得而声明完成。

## 完整还原部分

### 启动存档选择界面

直接提取固定 `871_156598` 官方 APK 的 `defaultpackage_assets_prefabs_ui_login.bundle`，恢复 `PlanesListPanel` 和其滚动列表内嵌的 `PlanesRoleItem`，并复用同包版本启动页的模糊遮罩；共131个原始对象、37张 Sprite、16个动画片段。原黑色标题、双页签、白色列表、绿色选中卡片、头像边框、战力图标、“最近”标签、底部状态图例与原锚点／九宫格／字体／材质保持原绑定。页签沿用原 Animator 状态与转换，显示沿用 `PopupWindow` 的原 `UIDialog_Show` 曲线；背景按 `WindowBlurMask` 的四层模糊流程捕获实际启动主题。未用截图充当交互界面。

迁移官方 `PlanesListPanel.InitShow / OnTabItemChange`、`PlanesInfoItemBase.Init`、`PlanesInfoItemWithRoleInfo.InitRoleInfo` 的视图分支，由本地槽位替代联网角色列表。已创建角色显示真实姓名、等级与当前属性计算的战力；头像复用原模型外观、染色、骨骼网格及 `PlayerPortraitSettings` 的100×120头照摄影、排除部位和 `home/idle_01` 姿势。未创建的槽位使用原默认头像，标注“继续创建”。损坏的主档使用原灰色状态标记；点击显示中文错误并保留文件。空列表和新建页复用原 `EmptyTip` 与原按钮。

加载入口是 `Resources/SaveSelection/OfficialSaveSelectionAssets`，原始转换载荷在 `unity/Assets/Official/SaveSelection/`，Unity 原生 prefab、字体、材质、Sprite 与动画在 `unity/Assets/Game/Resources/SaveSelection/`。提取入口为 `scripts/prepare_official_save_selection.py`，原生恢复入口为 `Zjcs.Editor.Startup.OfficialStartupTools.RestoreSaveSelection`，运行适配为 `OfficialSaveSelectionView`。APK／CAB／对象／代码来源及输入输出哈希见[源清单](../../assets/reference/save-selection/source-manifest.json)和[原生绑定清单](../../assets/reference/save-selection/native-source-manifest.json)。素材尚未加入完整备份快照。

Unity 2022.3.57f1c2、 Play Mode 已验证15槽列表、男女原生头像、页签动画、鼠标滚轮、已有角色入口、损坏主档提示与新建独立草稿。交互使用 EventSystem 射线命中和模拟按下／抬起／点击，确认无控件遮挡；列表浏览前后所有隔离存档文件 SHA256 一致。已查看450×800、675×1200、900×800和360×1000运行截图并检查面板处于窗口内。静态检查、Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。复验入口为 `Zjcs.Editor.Startup.SaveSelectionPlayModeValidation.Run`，需指定环境变量 `ZJCS_SAVE_SELECTION_PROBE_OUTPUT` 为独立临时目录；Windows Player 使用 `--zjcs-validate-saves <临时目录>`，再次启动同目录执行冷启动回读检查。验证数据与用户存档隔离。

已构建并发布到 `D:/Desktop/杖剑传说/杖剑传说.exe`，桌面快捷方式保持指向该入口。真实启动该  Player，在内置 BOE 屏显示主窗口且不抢占前台，查看列表、新建页、滚动和错误提示运行画面；模拟输入通过已有角色入口、新建草稿及损坏文件原字节保留检查。再次独立启动 Player，读回16槽列表、最后选择的槽16及草稿，并模拟点击继续创角。验证器拒绝黑色截图，隐藏窗口中的逻辑通过不计入画面结论。检查前后9个用户数据文件 SHA256 一致。此输出包含共享工作区既有未提交依赖，属于当前工作区测试版；存档界面代码、素材与本页已同步提交，完整游戏和仅含全部已提交依赖的独立重建仍见[工程页](unity-project.md)。

### 设置与存档基础

已有 [ProfileSlotCatalog](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs) 的槽位发现／创建和 [LocalJsonSaveFile](../../unity/Assets/Game/Runtime/Offline/State/LocalJsonSaveFile.cs) 的临时写入、备份、损坏原字节保留与恢复代码。主城设置入口使用官方 `SettingsDialog` 控件树、30 张固定版 Sprite 和点击音效；[HomeSettingsProfile](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSettingsProfile.cs) 按角色槽保存音量、帧率和数值显示，写入失败时维持旧设置。设置页目前接通 30／60 帧、总／音乐／音效／语音滑杆和完整／简略显示；消息推送及单机不适用控件隐藏，游戏页语言控件固定为当前中文。

## 自定义实现

Windows 采用竖屏等比缩放、中文和多个独立本地角色存档，移除推送、联网登录、账号、社交、振动和网络预下载。

### 多存档与角色切换

原“选择位面／角色列表／可创角色”转换为“选择存档／已有存档／新建存档”；原服务器名称位置显示本地槽号，状态图例改为“可读取／无法读取”。最近选择的槽位在独立 `profile-selection.txt` 中原子保存，重开列表时置顶并选中。新增 `InspectCharacter` 只读路径，列表及头像预览不写回旧存档的创建时间，也不结算离线收益。Windows 鼠标滚轮灵敏度设为50，保留原滚动布局与拖动。列表使用原1080×1920参考尺寸并等比缩放，宽窗保持竖屏面板。新建和选择后接续已有开场／创角／初始武器及角色完成页，不改变这些后续页面的表现。

本地目录替代服务器／角色身份；每槽保存独立进度、草稿、库存、设置、随机状态及地图／战斗检查点。列表浏览不结算收益；进入时才处理该档时间。切换前保存，失败保留原会话。

### 存档字段与事务接线

扣费、奖励、随机、跨日和领取状态在同一本地事务提交，失败回滚并重新绑定状态。主档损坏优先读取有效备份；两者均损坏时保留原字节，由用户显式选择后续处理。时间回拨不刷新额度，负差按零，检查点只前进。完整 Unity 字段与版本仍须随模块接入定义。

新建槽位时记录本地创建 Unix 时间，并在打开进度存档时写入 `CreatedAtUnixSeconds`，供已确认以开服时间计算开放资格的功能读取。没有创建时间记录的旧槽位保持未知，不按目录时间补推。

本地主城拍照保存视口PNG，失败恢复界面；完整原相册、滤镜、姿态、贴纸与编辑页尚无 Unity 实现。Bloom 应恢复原 Unity 组件；旧引擎屏幕采样近似不沿用。

## 尚未实现

- 完整游戏内角色切换、所有功能角色数据保存／恢复、实际进入时离线收益及主城拍照。启动列表的新建、选择和草稿入口已接通，不据此声称后续全游戏流程完成。
- 设置页的 URP 分辨率缩放、Bloom、其他画质组和主城背景音乐尚未接入；当前 Unity 包清单没有 URP，现有设置页隐藏这些未接线选项。
- 存档面板原按钮／窗口音效尚未接入；拍照功能的完整官方 Unity 代码、配置、prefab、资源和调用关系仍须恢复。
- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。

## 代码与资源入口

[SettingsDialog.cs](../../.tools/source-packages/official-code/client-source/SettingsDialog.cs)、[SoundSettingsUI.cs](../../.tools/source-packages/official-code/launch-source/SoundSettingsUI.cs)、[FrameRateSettingsItem.cs](../../.tools/source-packages/official-code/client-source/FrameRateSettingsItem.cs)、[NumberDisplayRuleSettings.cs](../../.tools/source-packages/official-code/client-source/NumberDisplayRuleSettings.cs)、[RenderQualitySetter.cs](../../.tools/source-packages/official-code/launch-source/RenderQualitySetter.cs)、[BloomIntensityService.cs](../../.tools/source-packages/official-code/launch-source/BloomIntensityService.cs)

[settings source manifest](../../assets/reference/settings/source-manifest.json)、[settings runtime generator](../../scripts/prepare_unity_home_settings.py)、[HomeHudView.Settings.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Settings.cs)、[HomeSettingsProfile.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSettingsProfile.cs)、[ProfileSlotCatalog.cs](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs)、[LocalJsonSaveFile.cs](../../unity/Assets/Game/Runtime/Offline/State/LocalJsonSaveFile.cs)。载荷与规则测试：`python -B scripts/test_prepare_unity_home_settings.py`。
