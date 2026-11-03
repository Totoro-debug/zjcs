# 主线地图公共模块

[总索引](../README.md)

状态：未完成（已提取共享宿主、部分地图网格、正式存档快照读取及离线小地图数据源组合入口；各玩法的原表现与内容缺项仍待接入）

## 功能简介

主线地图通过 `OfficialMainlineMapRuntime.Begin(mapClassId, …)` 使用同一套运行时。`OfficialMainlineMapProfile` 当前登记 Map10–15；各图数据、表现和游玩完成范围以所属地图页为准，登记或可进入不表示整图完成。地图资源、区域条件、剧情、实体和进度仍按地图 ID 隔离。新增地图须先迁移其官方数据与专属流程，再登记配置，不能仅替换地图 ID 后宣称地图完成。鲸背岛 Map16 尚未登记此运行时。

小地图区域地面由公共 `MainlineMiniMapRegions` 管理原 prefab 实例、原管理器的状态／范围事件及资源生命周期。各图提供 `MainlineMiniMapRegionAssets` 与 `IMainlineMiniMapRegionState`；模块直接订阅原可见范围事件，替换处理器时切换订阅，停用／销毁时退订；普通状态刷新不会重置原十秒回收期限。所选 `884_156692` 的 Map10／11／12／13／14／16 已分别转换5／77／99／106／111／43份原区域 Tilemap，复用同一实现；Map15 原配置没有独立小地图 prefab，不生成空目录。当前原生资源与各图历史来源独立记录，不混接不同版本的区域身份；完整原小地图根已提供公共实例绑定；白雾、标记及正式宿主的完整状态接线仍未完成。各图验证范围见[初始岛](../maps/map-10.md)、[森之国](../maps/map-11.md)、[山之国](../maps/map-12.md)、[泽之国](../maps/map-13.md)、[龙之国](../maps/map-14.md)和[鲸背岛](../maps/map-16.md)，来源见[公共小地图区域模块](../../assets/reference/official-map/mainline-minimap-regions-source-manifest.json)。

`MainlineMapMiniMapSnapshots.CreateOfflineDataSource` 将当前角色槽位的只读地图快照与原离线响应消费接口组合，避免调用方自行实现跨图实体筛选或状态恢复。已访问地图只提供本地探索过的实体及公开地区实体；隐藏／激活和图标状态从最新本地存档投影，读取不装载地图场景、不推进存档。地图外小地图调用与 Map13 正式地图回归现走同一组合入口；`OfficialMainlineMapRuntime` 在原小地图根激活前注入该数据源，保留原协调器当前图不请求、跨图请求的分支。完整白雾、标记状态、HUD输入与Player验证仍待完成。

`OfficialMainlineMapProfile.LocalChannelCount` 是单机分线配置；当前已迁移地图均固定为一条本地分线。公共 HUD 按原 `PublicMapBranchLineBtn.IsShowBtn` 的数量阈值隐藏分线按钮，同时不改变地图的私有／公开地区状态或其他地图玩法。未接入原生小地图 HUD 的地图仍须在正式宿主接线时消费同一配置。

## 原始官方实现

当前 `884_156692` 的完整原 `MiniMap` 根代码已迁入内部 `MainlineMiniMapRoot`，保留角色／全图视野处理器的缓存和订阅切换、原全屏显隐调用顺序、相机定位／边界裁剪、标记查找及关闭事件；配置、状态与资源从既有本地端口接入。公共 `MainlineMiniMap.Create` 通过具体脚本 `MainlineNativeMiniMapRoot` 绑定完整原 prefab，在激活前注入状态、角色、离线响应、资源、标记、点击和白雾坐标端口；实例统一管理显隐、全屏、关闭事件、纹理和销毁。初始岛、森之国、山之国、泽之国、龙之国与鲸背岛使用同一入口及各自配置，正式地图宿主／HUD接线仍未完成。离线标记初始化另按当前官方源码补入缺实体字典、容器实体及地区实体列表的分支；原64组条件、64次回调、24组归属条件及新增缺数据路径已在 Play Mode 通过。历史871源码凭据保留，只有 `OfflineMiniMapMarkManager.Init` 由当前源码覆盖，见[完整根与当前初始化来源](../../assets/reference/official-map/mainline-minimap-root-source-manifest.json)。

原 `OfflineMiniMap` 协调组件已复用同一公共根程序集：非当前地图且地图ID大于零时，先隐藏旧数据并请求本地数据端口；空响应／错误码保留隐藏状态，成功时按原顺序显示内容、初始化离线白雾、初始化离线标记。重新启用当前地图不请求离线数据，重新启用非当前地图重新请求。所选884的 `StateSwitcher` 与历史原输入哈希一致，继续复用已迁移实现；原仅供编辑器使用的 `RequireComponent(MiniMap)` 因公共根为抽象类而不迁入，完整原生实例绑定须在激活前校验根组件。`prepare_mainline_minimap_offline_validation.py` 准备隔离工程；Play Mode 使用已安装的泽之国原图层、57,600格导航、106地区、原可见数据编解码及隔离保存文件，验证成功／空响应／错误响应、切图重新启用、先白雾后标记、12格动态解锁、1,326格公开地区填充和四种雾效shader变体；已查看实时与离线两张实际渲染截图。此处验证的是协调组件与图层，正式地图存档到完整响应的投影及HUD／输入／Player仍未接通；完整原根实例验证见下文。

完整原 prefab 由 `prepare_mainline_minimap_native_root.py` 恢复，保留各图原序列化对象文档及原接缝线、主／支地区说明 prefab 引用。原 Tile 的 Sprite、颜色、矩阵、实例对象、标志和碰撞字段从实际对象恢复，`MapAreaTile.Area` 与原完整类型声明一并迁移。主根的16种应用脚本逐文件核对当前原输入与已有迁移凭据，仅离线标记 Init 的覆盖范围按当前根凭据另记；不同来源版本不作整版等价处理。完整根及各图已安装验证导航分开核对，参数如下：

| 地图 | 原根对象文档 | 验证导航 | 地区 | 原配置 | 来源 |
| --- | ---: | --- | ---: | ---: | --- |
| 初始岛 | 87 | 60×60 | 5 | 1 | [完整根](../../assets/reference/official-map/map10-current-minimap-root-source-manifest.json) |
| 森之国 | 531 | 240×180 | 77 | 2 | [完整根](../../assets/reference/official-map/map11-current-minimap-root-source-manifest.json) |
| 山之国 | 672 | 280×210 | 99 | 2 | [完整根](../../assets/reference/official-map/map12-current-minimap-root-source-manifest.json) |
| 泽之国 | 747 | 240×240 | 106 | 2 | [完整根](../../assets/reference/official-map/map13-current-minimap-root-source-manifest.json) |
| 龙之国 | 732 | 280×280 | 111 | 2 | [完整根](../../assets/reference/official-map/map14-current-minimap-root-source-manifest.json) |
| 鲸背岛 | 315 | 140×120 | 43 | 4 | [完整根](../../assets/reference/official-map/map16-current-minimap-root-source-manifest.json) |

隔离 GUI Play Mode 已验证六张完整原 prefab 的全部 MonoScript、托管引用类型和序列化对象引用。泽之国使用原57,600格／106地区夹具，通过延迟离线响应、原白雾及标记初始化、全屏视野切换与缓存恢复、关闭事件、重新启用、订阅释放；已查看温泉乡原层级实际渲染截图。原尺寸的小地图纹理复用同一实例，公共工厂销毁实例时先停用再释放纹理，重复销毁不重复处理。初始岛、森之国、山之国、龙之国与鲸背岛分别使用本图已安装的导航、地区和原配置，通过同一工厂验证原根加载、地区插槽数量、离线请求保持隐藏和销毁释放；未给它们套用泽之国离线响应。上述原根夹具的实体／资源查询为空，关闭事件直接调用原入口，HUD和全屏窗口另按下文验证；真实实体图标、采集资源、正式地图存档响应投影或Windows Player仍未完成。主工程 Unity 导入／编译、`ProjectTools.Validate` 与静态元数据检查通过。

公共小地图 HUD 另迁入当前原 `MiniMapMaskedUI`、`AreaInfoUI`、`PublicMapBranchLineBtn` 完整路径及 `MultiLanguageTMPControler`／语言组配置，原局部坐标、区域名称、原探索度公式、延迟弹窗回调取当前地图、公开地区与频道数显隐、战斗错误4131及状态配色分支均保留。`MainlineMiniMap.AttachHud` 在原父插槽内实例化完整79对象的 `MiniMapMaskedUI.prefab`，激活前绑定地图／角色／语言／字体／窗口／声音；与主实例共同显隐并先销毁 HUD，再释放纹理。原按钮声音 `Main_Cursor_Normal` 从实际 Wwise 事件、bank和媒体解码，不用替代声音。资源、脚本与逐文件原输入等价凭据见[原 HUD 代码](../../assets/reference/official-map/mainline-minimap-hud-code-source-manifest.json)和[原生 HUD](../../assets/reference/official-map/mainline-current-minimap-hud-source-manifest.json)。

隔离 GUI Play Mode 使用泽之国当前地图实例，通过原角色渲染器将真实已探索地块绘入原尺寸 RenderTexture，再由原圆形 HUD 显示；已查看实际截图。模拟鼠标经原 GraphicRaycaster／Button 验证打开全屏页的请求及延迟初始化地图ID、战斗中禁止切线和非战斗时请求分线窗口；多分线检查显式使用原按钮的兼容性夹具，窗口内容不接入单机入口。真实导航地区查询、坐标事件、探索度更新、单频道隐藏／多频道显示、当前状态配色、模块显隐／销毁和全部订阅释放通过。单机分线决定集中在[世界地图](world-map.md#自定义实现)；女神头像地区通知、正式 WorldCanvas 插槽、全屏窗口正式接线、正式存档状态及 Player 接线仍待完成。

主线地图共享原 WorldCanvas／WorldStage 界面、地区条件、导航、交互、剧情、机关、雕像、小地图和进度规则；单图放置及差异由地图页维护。

全屏小地图另迁入所选884的完整 `MiniMapFullScreenPanel`、页签、主角／道具方向指引、原几何计算和关闭按钮，保留原已访问地图／切换组枚举、半秒页签冷却、异步请求版本丢弃、当前图复用、相机与滑条同步、筛选刷新及禁用恢复顺序。`MainlineMiniMap.OpenFullScreen` 在激活前绑定原窗口依赖，实例关闭、停用和销毁统一释放窗口；状态、头像和浏览其他地图仍由所属宿主适配。原窗口294个对象、两个筛选条目的15／31个对象及完整依赖由 `prepare_mainline_minimap_native_panel.py` 显式恢复；原按钮控制器的三个压缩常量缩放通道按路径哈希、浮点字节和循环参数逐项核对导出曲线。373条地图和201条标志记录按原构造器解码，只投影当前窗口消费字段；97个动态图标覆盖当前六图切换组及原有筛选条件，不为原表未选中的废弃记录制造替代素材。来源见[全屏原代码](../../assets/reference/official-map/mainline-minimap-panel-code-source-manifest.json)与[全屏原生资源](../../assets/reference/official-map/mainline-current-minimap-panel-source-manifest.json)。原 WorldCanvas 的 CanvasScaler 从当前实际对象提取，保留1080×1920参考尺寸及原缩放模式。正式 WorldCanvas／存档／跨图实体／头像与 Windows Player 接线仍未完成。

原 `MapRootSwitchMapSupport.CreateMiniMap` 还将当前图绑定到地图插槽共用的 `MiniMapNonMaskableManager`；此引用由公共工厂绑定，完整16对象的当前原渲染器由 `prepare_mainline_minimap_native_renderer.py` 恢复，来源见[共享渲染器](../../assets/reference/official-map/mainline-current-minimap-renderer-source-manifest.json)。原 Quad、内置材质／shader选择器、相机、排序、网格和材质引用保留；内置资源按GUID与fileID组合记录，不能按GUID合并成单一对象。窗口通过原 `PopupWindow.Show`／即时隐藏生命周期开启和释放。

隔离 GUI Play Mode 使用720×1280实际 Game View、当前原1080×1920缩放、原URP叠加相机和界面画布，通过公共模块打开完整窗口。原全屏相机实际绘出已探索地块、地区边线和说明，帧末截图已查看；原三列筛选条目完整显示。模拟鼠标经原射线／按钮验证相机滑条、打开筛选、全选写入端口、关闭恢复圆形HUD和重开；窗口、筛选钩子、网格位置订阅与纹理释放通过。资源板加载使用既有871原目录及本地适配器，保持其来源独立；当前夹具只登记泽之国为已访问，不决定跨图实体快照策略。筛选写入、主角资料／头像和世界HUD显隐由显式夹具提供，该窗口夹具未接通正式地图快照、实际头像、道具指引或完整存档宿主。

公共小地图工厂可通过 `MainlineMiniMapBindings.Visibility` 直接借用世界正在运行的原 `GridVisibilityAgentComponent`。工厂先核对当前地图、源实体地图ID和导航尺寸，再于原根激活前创建事件适配；关闭／销毁先禁用原遮罩，再释放订阅，创建失败也释放订阅。源组件及探索数据继续由世界拥有，存档回滚更换组件实例时须重建小地图，避免继续读取旧实例或另一张地图。

隔离 GUI Play Mode 已使用现有 `OfflineMapGridVisibility`、871原地图／身体配置和同一公共工厂，验证角色移动揭格、温泉乡区域解锁经实时事件移除原 `MiniMapMaskManager` 遮罩格；写入隔离文件后冷读得到相同原RLE字节，独立实例互不串用，小地图释放后世界可继续移动。圆形HUD、全屏窗口及筛选模拟点击回归通过，实际帧末画面已查看。此项仍使用显式地图宿主夹具，正式入口的状态／存档组合接线未完成。原资源输出不变时，`prepare_mainline_minimap_native_root.py --refresh-integration --include-shared` 校验原输入、原输出、官方脚本与MonoScript元数据后，仅更新本地适配源码及绑定哈希；不放宽原资源校验。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

遗失女神8364的原领奖层级另由同一提取入口读取 `defaultpackage_assets_prefabs_ui_map_upgraderesult.bundle`，包括触碰窗、三类结果窗及属性行；原 `ItemIconManagerConfig` 绑定 `ItemIconSkill`，新增394条主动／被动技能图标。完整来源数据现为65份原面板、945张Sprite和1,055条图标记录，保留35条原技能阶位、`MapLostGoddessAwardDialogConfig`、星级品质图、三类原标题及属性行动画。新增客户端代码、对象／bundle真实来源和提取输出哈希见 `unity/Assets/Official/World/Map/11/UI/source-manifest.json`；原生 prefab／动画、图标元数据和转换器的换行归一化哈希见同目录 `native-source-manifest.json`。Unity 导入器已同步层级与图标基础绑定；结果控制器、原关闭交互、地图入口、粒子／泛光／音效仍未完成，不能将提取或导入通过等同于领奖流程已迁移。

原结果窗的 Button 类型、离散启用曲线及各窗动画目标子集已在 Unity 中核对；共享原动画的转换结果按窗口分别保存，不覆盖其他结果窗。新增六份 prefab、十份被引用动画及其稳定元数据随源数据提交，验证范围见所属功能页。

固定 `871_156598` APK 优先提取；英雄、探寻和奖励 UI 的原面板／对象、sprite、动画及返回对象池事件由 `prepare_unity_map11_ui.py` 保留至59份原面板、535张sprite。`map11_support_ui.py` 按原 `ItemIconManagerConfig` 的ID／类型／默认优先级绑定661条本图奖励道具记录，使用原普通、装备、宝具模板；宝具素材取Map11原地区 `DropItems` 并集。名称、品质框、奖励高品质光效、GraphicRaycaster和原数字标题均显式绑定。完整输入、各对象来源、客户端／共享代码哈希及输出见 Map11 `UI/source-manifest.json`。奖励列表 `UIImgAdd` 的 `UI/EffectAdditive` shader 来自 APK `defaultpackage_unityshaders.bundle`（解码包 SHA256 `f6cc2ad49028110a87744a116bc2fd193af9d8e2f2e7e452b7d90c0bc934dabe`），使用既有 GLES→HLSL 转换并保留四种裁剪变体及原材质参数；输出和源码见 Map11 UI 来源清单。

`prepare_mainline_loop_scroll.py` 从固定官方 `LoopScrollRect.Runtime.dll` 完整反编译并恢复9个类，保留原拖动、惯性、网格和回收分支，仅修复C#反编译基类调用及工程命名空间；程序集、逐文件源码与输出哈希见 [循环列表来源](../../assets/reference/official-map/loop-scroll-source-manifest.json)。`prepare_mainline_popup_effects.py` 核对 APK 恢复的ARM64实现，迁移 `TMPColorGradient` 的原顶点计算以及 `ScreenBlur` 原程序；原窗口模糊保留4级下采样、3级上采样、七个权重、screen/13快照、原遮罩颜色及半秒关闭门槛。元数据／原生映像、方法地址／字节哈希、原shader／material及显式加载绑定见 [弹窗表现来源](../../assets/reference/official-map/popup-effects-source-manifest.json)。这些恢复只覆盖当前原窗口使用的分支，原动作、奖励粒子、图标附件和完整实体交互尚未完成；未生成素材快照。

## 完整还原部分

公共区域小地图已在 Unity 2022.3.57f1c2 图形 Play Mode 通过六图441份原 Tilemap 的公共 Interface 检查：地区公开状态、原 Z、范围事件替换／旧事件隔离、缓存复用、原游戏时间十秒回收、释放与重挂；12张区域小地图实际画面已查看。夹具按原 UniTask 的游戏时间计时并等待销毁帧，不以墙钟时间替代；地区／角色状态由夹具提供，未覆盖正式根、白雾／标记、HUD 输入、地图存档或 Windows Player。静态元数据检查与主工程 `ProjectTools.Validate` 均通过。

`Resources/Official/Progression/mainline-tasks.json` 的 `output_sha256` 对照 `TextAsset.text`，Git 固定其工作树换行为 LF，确保 Windows `core.autocrlf=true` 检出仍与清单一致。

公共代码位于 [`Presentation/World/Mainline`](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/)。`OfficialMainlineMapRuntime` 在 `Awake` 为每次入图分别创建 HUD、区域、导航、雕像、体力、探寻、小地图七个模块，负责按原顺序组装、更新和退出；窗口、路线、待交互对象、区域进度、刷新计时、动态实体和渲染目标由所属模块持有。模块实现使用宿主内的私有类型保持封装，不把单张地图或单个角色的可变状态放入全局单例。各图专属剧情、首战引导、机关、音频与实体载荷留在原适配层。

原生地形的公共绑定在独立程序集 `Zjcs.Presentation.World.MainlineNativeGround`：`MainlineWorldLabels` 将原区域描述／战力／多语言标签绑定到地图数据和 `IMainlineWorldLabelState`；`MainlineWorldEnvironment` 对原灯光、环境和分区组件提供同一份区域、进度、位置与揭格服务。状态与进度数组按 Region ID 索引，保留原配置的枚举顺序；不能把 Map14 原表中先76后75的顺序当成状态下标。Map14 分区视图复用这两项服务；`MainlineNativeGround` 为 Map15 整图 prefab 提供非激活实例化、依赖绑定、原 URP 管线切换和退出恢复。角色／镜头资源地址独立于 HUD 是否迁移，避免无 HUD 的地图跳过原模型缩放和镜头参数。此项仍不等于通天塔完整 HUD、楼层与交互已经迁移。来源见[原生地形公共绑定清单](../../assets/reference/official-map/mainline-native-ground-source-manifest.json)。

公共宿主通过 `OfficialMainlineMapRuntime.NativeGround.cs` 的 `MainlineNativeGroundState` Adapter 提供角色位置、等级／战力、地区状态、探索点及公共揭格；整图资源地址由地图档案传入。`NativeMap14.cs` 仅保留龙之国分区视图的创建、刷新与释放，消费同一状态 Interface。骨骼地面载荷通过 `MainlineGroundAnimation` 复用项目独立原生绘制端；原素材与具体运行验证范围见[通天塔入口](../maps/map-15.md)。

公共原生地面转换器 `GroundBinding` 以 `GroundSource` 显式接收地图 ID、原 prefab 地址和 bundle；Map15 的原 GUID 保持不变，Map16 采用独立范围。地图定义和角色／镜头／标签数据提取同样按地图 ID 消费实际官方输入，尚未迁移的地图不自动注册。`MainlineNativeGround.UnlockRegion` 保留原环境激活动作与揭格回调顺序，区别于普通状态刷新；女神像的公共地面调用使用该 Interface。两图已通过隔离图形 Play Mode 的同一宿主生命周期夹具，具体范围及缺口见[鲸背岛](../maps/map-16.md)。所用 CustomWater.dll 已与当前所选版本逐字节核对一致，仅此程序集可复用原水面代码凭据，不能据此认定其他历史输入等价。

原地区倒计时通过 `IMapRegionTime` 注入地区开放日期、当前时间和角色位阶，标签消费原刷新状态机及原时间格式；日期规则不散落在地形或 HUD 中。`OfflineCharacterRankRegionTime` 使用现有角色进度存档，标签只绑定自身地区副本和语言服务。已确认的本地日期策略、43区边界／存档检查与原倒计时画面范围集中见[鲸背岛](../maps/map-16.md)，正式地图宿主的接入尚未完成。代码与来源见[地区时间清单](../../assets/reference/official-map/mainline-region-time-source-manifest.json)。

完整原小地图工厂另通过 `MainlineMiniMapBindings.RegionTime` 消费同一地区计时模块，并核对时钟、地区数据与原根地图ID。每个实例复制原地区行和区域列表，地区说明与强度标签读取同一副本；原配置资源不保存窗口回调。原 `MapRegionLimitStatusRefreshHelper` 的时间分支及 `MapRegionLimitStatusRefreshForRankUnlockTime` 状态机已恢复到共享小地图标签路径，保持时间先于等级、到期刷新和取消顺序。原 `ShowRemainTimeInfo` 使用该实例的时钟和文本模块，保留原倒计时计算与格式；正式存档宿主组合仍未接通。

隔离图形 Play Mode 已通过同一工厂核对鲸背岛全部原时间偏移、超过位阶4立即通过、较低位阶继续受限、准确到期边界及两个窗口的时钟／配置隔离。完整当前原地区说明 prefab 正常初始化，实际画面中的原名称与倒计时已查看；推进注入时钟后由原 UniTask 自动解除时间限制，重进会重建计时。混合条件夹具组合本图原时间字段与泽之国一项原等级门槛，验证时间优先、随后切换等级提示及原等级事件解除，不写回地图配置。标签关闭后保留父小地图仍需的边线订阅，整个模块销毁后全部订阅释放；原 HUD／全屏页／筛选模拟点击与实时白雾回归通过。此项使用显式宿主与隔离时钟，未覆盖正式入口、完整鲸背岛小地图或 Windows Player。

入口组件保留原 MonoScript GUID 和 `MovedFrom`；纯 C# 模块沿用原脚本 `.meta`，原 prefab、层级、坐标、动画、数值、执行次序与存档 DTO 保持兼容。`OfficialMainlineUiCatalog` 按当前 `MapProfile.NativeUiRoot` 缓存原 UI 目录，HUD、雕像、奖励列表、探寻与小地图共用它；通用文本不再由女神像持有或加载。

公共英雄配置通过 `heroTemplateIds` 声明本图原模板列表，`OfficialMainlineInteractionCatalog` 校验模板身份、唯一性、顺序、原周期权重及地图／版本；历史四种模板的既有载荷保持兼容。共享转换器接收该列表，拒绝尚未迁移的周期组规则，英雄效果与每日访问仍由同一个雕像模块消费。当前鲸背岛已提取六种原模板，正式地图接线与验证范围见[鲸背岛](../maps/map-16.md)。

| 模块 | 当前代码与复用范围 |
| --- | --- |
| 地图配置与载荷 | `OfficialMainlineMapProfile` 集中已迁移地图的资源根、原生 UI 根、阶段／故事数量及必须存在的区域条件。`Data` 分部保留各图官方模板、故事图和数量校验。 |
| HUD、顶部栏、任务与功能入口 | `OfficialMainlineHudModule` 管理 `OfficialWorldUiRenderer`、HUD 控件、顶部栏、任务、功能入口、头像、快速购买与提示的状态和销毁；原生 prefab 路径从地图配置解析。Map10 现有 UI 迁移缺项继续保留在所属地图页。 |
| 区域开放 | `OfficialMainlineRegionsModule` 持有区域进度与雾层，`OfficialMainlineRegionPolicy` 可独立装载官方 `TbMapRegionInfo`／`TbConditionInfo`。保留原有全探索与剧情次数分支、比较运算、错误拒绝及配置检查；实体显隐、交互锁、小地图和任务消费同一份策略。开放条件不等于已实现雾或公开地区状态转换。 |
| 寻路与移动 | `OfficialMainlineNavigationModule` 持有当前位置、路线、到达回调和待交互目标，负责屏幕点击、实体命中、交互距离、路径跟随和逐格存档；继续调用 `OfficialWorldMapSession`、`OfficialMapPathfinder` 与 `OfficialMapGroundRules`。桌面输入提供器与原鼠标输入共用遮挡、命中和寻路流程。 |
| 女神像与失落的女神像 | `OfficialMainlineStatuesModule` 的 `Goddess.cs` 共用探索条件、原生弹窗、按原阶段顺序激活、回执与传送；地区名称按当前地图 ID 查询。当前接入范围为 Map11，珍宝预览等缺项见[森之国](../maps/map-11.md)。 |
| 英雄／魔女访问及魔女阶段 | `OfflineMainlineProgressSession.Statues` 提取女神激活、全图魔女计数、英雄／魔女每日共享额度、奖励、HP 与待战斗效果事务；同一 `OfficialMainlineStatuesModule` 的 `Witch` 实现保留原书页、预览、激活与周期访问，`HeroStatues` 实现已接入四种英雄雕像原弹窗、410 等级属性、首次激活与周期访问；原角色／粒子演出仍待恢复。 |
| 探索进度 | `OfficialMainlineRegionsModule` 与既有 `OfficialMapProgressAdapter`、`OfficialMapExplorationProgress`、`OfficialMapProgressCalculator` 共用官方实体状态和探索点计算。 |
| 网格可见性与存档 | `OfflineMapGridVisibility` 集中原角色视野、实体身体／营地揭格、区域激活、事件生命周期和 RLE 存档；地图 ID、矩形尺寸、角色模板、视野半径与解锁格通过配置传入。Map13 正式入口及其小地图检查改用这一公共类，原 `map13GridVisibility` JSON 字段和原数据断言保留在 Map13 适配层。其他地图仍须接入各自已核实的官方载荷；该类不承担白雾绘制。 |
| 地图体力点 | `OfficialMainlineStaminaModule` 与 `OfflineMapStaminaService` 共用金币8011、铁矿8021、时之砂8026和怪物5011的扣费、奖励、战斗回执及本地刷新；原区域／组间隔由 `interactions.json` 加载。 |
| 区域阴影 | `OfficialMainlineRegionsModule` 按 `MapProfile.HasRegionFog` 挂载 `OfficialMainlineRegionFog`，资源根与数量断言从当前地图配置传入，不再内置 Map11 路径；当前载荷恢复 Map11 的73份原 LightRegion 层级、Freeform网格、原衰减贴图及地区公开后的半径渐退；地表按原 Multiply 光照计算。其他地图及日夜场景装饰切换尚待接入。 |
| 命运果实与探寻 | `OfflineFruitTreeService` 共用生产、容量、时间推进和收取事务；`OfflineWorldExplorationService` 共用探寻费用、候选／保底、动态实体与历史事务，`OfficialMainlineExplorationModule` 管理原 HUD 入口、动态实体、确认／详情／令牌窗口、遮罩与释放。果树生产服务继续由家园消费同一角色存档，不在地图重建一套生产规则。完整实体交互与演出仍待完成，详见[命运探寻](world-exploration.md)。 |
| 小地图与图标 | `OfficialMainlineMiniMapModule` 管理相机、渲染纹理、圆形／全屏面板、图标、跨图页签和筛选，共用已迁移的原面板、图标、跨图浏览、缩放与档案筛选存储；各图几何继续按 ID 加载。 |
| 交互、剧情、战斗与存档 | `Interactions`、`Story`、`Dialogue`、`Battle`、`Persistence` 分部保留既有执行顺序；Map10 首战引导独立在 `Map10` 分部。森之国机关、动态怪物与音频仍消费其专属载荷。 |

`Modules.cs` 集中模块组装与各自的 UI／渲染目标释放；开发检查通过 `OfficialMainlineMapInspection` 定位所属模块，只用于 Editor／development Player 或显式启用 `ZJCS_MAINLINE_MODULE_VALIDATION` 的回归构建，正式运行流程不依赖反射。`map-{ID}-save.json`、角色进度、回执前缀和 `minimap-filter.json` 的现有格式保持兼容。`Map10Runtime…` 数据 DTO 名称暂保留，避免把运行时代码抽取混入资源／存档结构变更。

官方依据沿用已提取的 `WorldCanvas`、`WorldPanel`、`FunctionEntryDialog`、`MiniMapMaskedUI`、`MiniMapFullScreenPanel`、`GoddessTouchDialog`、`LostGoddessTouchDialog`、`WorldWitchTouchUI`、`WorldMapProgressCondition`、`StoryCountCondition` 以及现有地图导航／进度实现。各图来源与哈希见 Map10、Map11 所属页和原来源清单；动态怪物、战斗场景和时间轴的来源清单及生成器中的运行时代码绑定同步到公共目录，官方输入与素材哈希保持原值。七类模块与原 Git 实现的对应、官方客户端代码、输入／输出 SHA256、四份原页签动画及 prefab 显式绑定见 [mainline-modules-source-manifest.json](../../assets/reference/official-map/mainline-modules-source-manifest.json)。

验证入口为 `Zjcs.Editor.MainlineMapModuleValidation.Run`，使用 `-verify-mainline-modules <隔离输出目录>`；同一检查也可在 Windows development Player 运行。Unity `2022.3.57f1c2` 已通过编译、`ProjectTools.Validate`、主线／雕像事务检查与带图形的 Play Mode 回归。地图移动通过注入屏幕点击走 UI 遮挡、实体命中与寻路；Map11 小地图开关、跨图页签、筛选和女神激活使用原按钮的指针按下／抬起／点击事件，并核对最上层射线命中。检查覆盖区域探索／剧情门槛、Map10／Map11 位置冷读、筛选 JSON 冷读、女神激活恢复、英雄／魔女每日共享额度及待战斗效果恢复。七类模块拆分后已在隔离工程以  重跑该完整流程，并覆盖探寻概率／历史页签、令牌拖动、单次／十连费用、原引导宝箱地图点击免体力领取及冷读恢复。女神地区完成度及打开弹窗由测试设置，未覆盖自然探索到 100% 或从地图点击雕像的完整路线。

已实际查看 Map10 地图、Map11 HUD、全屏小地图和失落女神弹窗截图；Map10 HUD 当前不可见，Map11 地形／水面／遮挡仍有原有缺项，完整画面和地图验收继续由所属页维护。Python Map11 运行载荷生成与小地图转换三项检查通过。另一次 Map10 旧输入来源检查失败：未纳入当前 Git HEAD 的 `assets/official/adventure.json` 实际哈希为 `a6820d65…`，原清单期望 `84b92c87…`；未修改该输入或预期哈希，不能据此声明旧输入来源校验通过。

Windows 普通 Player 已成功构建，在内置屏以  真实运行并通过上述同一套模拟指针、拖动、区域条件、女神／英雄、金币采集／刷新、探寻和冷读检查；已查看小地图、清雾、探寻概率／历史的实际画面。构建入口 `BuildWindowsPlayer` 支持 `-mainline-player-output <exe>` 和 `-mainline-release-probe`；后者仅为普通回归构建增加 `ZJCS_MAINLINE_MODULE_VALIDATION`，启动参数仍为 `-verify-mainline-modules <隔离目录>`，并阻止正常启动流程另开存档界面，测试不写入用户档案。沿用本机已安装的 Mono Windows 支持，构建后恢复原脚本后端。回归版本位于 `.tools/mainline-modules-player/杖剑传说.exe`，不带验证参数时仍走正常游戏入口。当前 Unity  日志仍有原生 `PresentFrame／WaitForLastPresentationAndGetTimestamp` 呈现时序告警；development 控制台遮挡的截图不作为画面通过证据，未声明该引擎告警已修复。为既有实现的公共模块抽取，完整玩法缺项见下文，未覆盖桌面正式版本。

新增载荷固定 `871_156598`：`scripts/prepare_mainline_interactions.py` 导出英雄参数、地区等级、区域刷新和体力点奖励；`scripts/prepare_unity_world_exploration.py` 解码7种令牌、65条实体组变体、28种模板及68条递归条件，已接入公共探寻事务与原窗口入口。`prepare_unity_map11_ui.py` 扩展至59份官方面板、535张sprite，保留英雄、探寻、奖励列表层级与原动画事件；来源分别见 `mainline-interactions-source-manifest.json`、`world-exploration-source-manifest.json` 及 Map11 UI 清单。探寻令牌列表恢复原LoopScrollRect的9个类，模糊和渐变的原生方法及程序转换分别记录在所属来源清单。

遗失女神领奖 UI 的来源准备将提取数据进一步扩为65份面板、945张Sprite和1,055条图标记录，新增六份原层级及394项主动／被动技能图标；导入器同步接受原结果窗和技能图标基础组件。该批工作保留原阶位／窗口映射／星级图／属性行动画及来源哈希，尚未接通地图入口和结果控制器，完整边界见[命运探寻](world-exploration.md)。各地图未完成页及配套提取／转换工具一并保留，入口见[世界地图](world-map.md)。

65份原面板已通过 Unity 导入／编译与 `ProjectTools.Validate`，既有公共流程的内置屏  图形 Play Mode 回归通过；新增结果窗仅验证原基础组件与动画资产，不作为完整交互验收或 Windows 发布结论。

带图形 Play Mode 已通过英雄首次领取、永久生命170／HP恢复、周期访问与每日共享额度、冷读，以及通过地图模拟指针采集金币（扣5体力、入包、点位隐藏、原3秒间隔刷新）。隔离事务检查覆盖体力不足回滚、金币／铁矿／时之砂数量倍率、离线刷新、怪物开始去重、失败重试、胜利历战精华与重复回包。已查看英雄面板和实际金币采集画面；奖励条已恢复原叠加材质、CanvasGroup动画和位置，粒子尚未完成，故未声明本功能完整验收或发布。

Map11区域阴影已通过编译、`ProjectTools.Validate`、图形 Play Mode 和实际截图复查：未公开区域使用原 Light2D 颜色／强度，模拟指针激活女神后播放原 `map_region_active_anim`，半径从0增至30，在2.01666665秒关闭对应阴影，冷读按已公开地区恢复。保留原全局白光、0.7倍光照纹理、HDR格式选择、受光排序层16／17／18和原衰减纹理；GLES程序机械转换为HLSL，网格由固定Unity Graphics的 `LightUtility.GenerateShapeMesh`／UTess／Clipper生成。来源与全部输出哈希分别见 `map11-region-lights-source-manifest.json`、`map11-light-meshes-source-manifest.json`、`map11-light-shaders-source-manifest.json`。这项验证覆盖区域地表阴影；未把日夜专属装饰、其他局部灯光或小地图未探索遮罩算作完成，也未发布新的完整功能版本。

地图 12 的专属实现、决策和缺口见[山之国](../maps/map-12.md)。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

`assets/reference/official-map/mainline-interactions-source-manifest.json` 记录620类实体扩展参数、英雄410等级属性、Map10／11地区等级、刷新间隔与体力点奖励的官方输入；`world-exploration-source-manifest.json` 记录7种令牌、65条实体组变体、28份完整实体模板、68条递归条件，以及奖励／随机包／宝物选择器／等级映射的原配置。运行时显式加载 `Resources/World/Mainline/interactions.json`、英雄属性及 `exploration.json`。探寻本地生成、保底、费用、奖励和存档事务，以及原窗口鼠标操作、令牌拖动、十连、历史分页与动态实体冷启动已通过隔离 Unity 编辑器验证；完整生成实体交互、模型材质、动画与 Player 尚未完成，范围见[命运探寻](world-exploration.md)和[公共模块](mainline-map-modules.md)。旧云端缓存不再保留，历史重建显式复用当前871缓存的相同哈希字节，核对原清单MD5／大小及真实凭据SHA256，来源保持实际版本及URL，不重建旧目录或替换变化资源。

公共网格 Module 位于 `Runtime/Offline/World/MapGridVisibility/`。调用方提供官方可见性／身体配置、当前坐标、已公开地区、地区查询及实体活动状态，使用同一个实例完成移动揭格、区域解锁、实体通知和存档快照。复用原 `GridVisibilityAgentComponent`／`GridVisibilityAgentHandle`，没有另写一套视野算法；旧原实现仍在 `Runtime/Official/World/Map13GridVisibility/`，目录名仅反映最初的迁移来源。尺寸和地图身份沿配置传入内部容器、范围与地区查询，不再限制为 13 或 240×240。来源对应与归一化文本 SHA256 见 [公共网格来源](../../assets/reference/official-map/mainline-grid-visibility-source-manifest.json)。

`SharedMapGridVisibilityAcceptance.Run` 已通过 Unity 2022.3.57f1c2 编译与规则执行：原 Map13 的 148 份身体模板、2,690 份放置、106 地区、57,600 格和 RLE 冷读字节保持一致；四组 17×9 接口夹具验证不同地图身份、角色模板、半径、实例隔离、移动与公开地区、存档及损坏游程拒绝。夹具不代表相应地图已迁移。静态元数据检查与 `ProjectTools.Validate` 通过。图形 Play Mode 中的实际地图入口、移动与冷读范围见[泽之国](../maps/map-13.md)，不据规则测试声明白雾画面或其他地图完成。

公共白雾 Module 位于 `Presentation/World/Mainline/OfficialMainlineGridFog.cs`，以原根 prefab、相机、地形坐标、公共可见性实例和玩家／传送坐标为 Interface，内部完成原 prefab 实例化、独立材质、原 `CloudController2` 初始化与结束清理。`LocalGridVisibilityAgent` 从完整官方 `MyGridVisibilityAgent` 迁移，只将实体容器、DI 生命周期与角色查询换成本地 Adapter；原单格／多格事件、即时移除、按距离扩散、暂缓揭雾、源坐标覆盖及目标集合均保留。现有 `MapRootVisibility` 的容器生命周期 Adapter 继续保留供原实体宿主使用；公共地图宿主不依赖该动态容器。Map11／12／13 的正式宿主已通过公共载入层接入原白雾；Map15 复用可见性且保留原无白雾差异。当前验证范围在各地图页维护。

当前版 Map11／12／15／16 的真实网格与身体输入已按原生成格式解码，使用 `World/Mainline/Visibility/Map<ID>/` 的独立 Resources 地址；Map11／12／15 已接入正式 Adapter，Map16 等待接线。Map11／12／13／16 的原雾层 prefab 已分别恢复；Map15 原根的 `CloudController` 显式为空，该差异以配置保持。资源规格及接入／验证范围只在各地图页维护；共同来源见 `mainline-grid-visibility-data-source-manifest.json` 与 `mainline-grid-fog-source-manifest.json`。

静态元数据检查（85,050 GUID）与 `ProjectTools.Validate` 通过。`SharedMapGridVisibilityAcceptance.RunSourceMaps` 已在 Unity 2022.3.57f1c2 中通过：保留原 Map13 与四组小尺寸 Interface 夹具回归，再使用 Map11／12／15／16 的真实原版输入执行移动揭格、全部区域列表、独立实例及逐格／逐字节冷读。后四图使用显式规则夹具（实体全部活动、地区查询返回0、不检查通行），未代替各图真实地区 Adapter、导航或地图交互验证。

公共可见性的加载、坐标／实体来源核对、区域查询、快照、白雾绑定和清理集中至 `OfficialMainlineMapRuntime.GridVisibility.cs`。Map13 保留旧 JSON 字段，其他地图使用 `gridVisibility`；事务回滚更换可见性实例时同时重新绑定雾层事件，避免继续监听已释放实例。当前接入与验证范围见各地图页。

地图档案显式配置网格／身体／白雾地址、是否要求独立小地图 prefab 及交互配置版本，公共载入层不再按 Map14 写专用分支。Map12／13 的地区、奖励预览、区域刷新和英雄结果索引从各自已校验的 `871_156598` 原表转换，避免误用仅含 Map10／11 的早期共享地区数组。空奖励列表按原表保留；来源与绑定哈希见[各图交互配置](../../assets/reference/official-map/mainline-interaction-maps-source-manifest.json)。

## 自定义实现

公共原生小地图的单机差异沿[世界地图已确认规则](world-map.md#自定义实现)。`LocalMiniMapDataSource.Request(mapClassId)` 读取一份地图快照，校验到访身份、原RLE尺寸及唯一实体ID，选择实体后复制白雾、公开地区和最新类型／状态；延迟完成保持原协调器先订阅、再显示内容／初始化白雾／标记的次序。`UseLatestLocalEntityStates` 仅用于本地响应，让原标记初始化按最新类型判断完成隐藏，并略过公开地区的默认完成状态补建；未设置该字段的原消费分支保留。`SingleChannelMiniMapHud` 由 `AttachHud` 统一绑定，原按钮自行按 prefab 条件隐藏；地区、战斗、窗口和公开容器身份继续读取原宿主，HUD 文本复用所属实例的地区计时绑定。正式地图文件的快照读取已由下述公共入口提供；原生全屏页签与正式宿主的组合接线仍未完成，不据此声明整图或 Windows Player 完成。

`OfflineMapGridVisibility.ExploredEntityIds` 维护每个实例曾发现的实体放置ID：沿现有世界可见性判断，实际启用的世界对象所在坐标格已揭开时记录，移动、停用或完成后不删除。加载时使用当前实体位置，官方放置默认坐标仍保留用于原营地计算；移动、地区解锁、世界实体启用刷新及读取快照时更新记录，返回独立、有序副本；已揭格内新出现的实体在下一次交互前即被记住。正式共享宿主保存到各图原存档的 `exploredEntityPlacements`，沿原RLE和位置一起恢复；山之国机关与泽之国水面机关移动同步原可见性身体位置，存档失败先恢复机关状态，再重建可见性与探索记录。重复、负数或不属于本图的记录拒绝载入。旧档没有该字段时保留原探索格，从当前仍可见的活动实体补建；无法追溯旧档未记录的历史移动或已移除实体，不伪造历史。

`MainlineMapMiniMapSnapshots.Read(mapClassId)` 从所选档案读取已到访地图的正式文件，每次读取最新角色进度，复用当前地图的存档校验／备份读取、实体目录、机关依赖闭包、山之国完成回执恢复、原地区探索点计算及实体状态投影。世界刷新与跨图读取共用女神／英雄状态、剧情销毁、地区可进入条件和体力点公开／私有刷新判断；另恢复森之国魔女与泽之国水面实体状态。命运探寻生成实体沿已有模板与本地有效期读取，不执行探寻或领奖。读取不实例化地图、不创建地图记录、不修复损坏主文件、不写回奖励、进度或备份；缺少正式地图文件或探索白雾记录、未到访或主备份均损坏时拒绝读取。白雾保留原压缩字节，已完成／未激活实体从当前实体集合排除，由已有本地响应选择曾探索或公开地区实体。此入口尚待接入正式原生全屏浏览。当前正式揭格记录来自森之国、山之国、泽之国与通天塔；Map10／14 的公共网格记录和 Map16 的共享宿主尚未接通。

正式存档快照已在森之国、山之国、泽之国与通天塔的隔离图形运行中读取，核对实体显隐与地图实际活动实体一致、探索RLE保持原值，读取不创建场景对象且不修改存档文件。森之国与山之国另验证了最新本地实体状态、刷新时刻、备份回退及损坏／缺少白雾记录时拒绝读取。共享模块 GUI 回归覆盖主线地图 HUD、地区条件、地图移动与冷读、女神／英雄雕像、体力采集、命运探寻交互和探索奖励；规则验证覆盖主线进度、体力事务、世界探索。验证入口未覆盖正式原生全屏跨图窗口与 Windows Player。

实体探索记录已通过两组独立地图身份的17×9接口夹具：隐藏／未激活排除、当前坐标、实体移动与地区揭格、实例隔离、防御副本、磁盘冷读和非法记录拒绝。原 Map13 全部可见格／身体与原RLE回归，以及 Map11／12／15／16 真实输入规则检查通过。正式森之国、山之国、泽之国和通天塔入口层另通过带图形 Play Mode 的模拟地图点击，检查原可见数据、位置和实体探索记录保存／冷读一致；森之国与山之国女神揭雾回归通过，已查看四图实际运行画面，女神验证的前置地区完成度由夹具设置。静态元数据检查、隔离工程编译及 `ProjectTools.Validate` 通过；跨图窗口或 Windows Player 尚未覆盖。

隔离 GUI Play Mode 通过同一 `Request` 接口，冷读泽之国240×240和鲸背岛140×120配置的测试快照，验证探索／公开筛选、最新实体类型和状态、完成隐藏、默认公开标志不重复补建、每次浏览重读磁盘及错图／未到访／损坏白雾拒绝。实体状态组合是明确的测试数据，不作为官方实体配置。原 HUD 的单分线隐藏、公开地区保留、外部频道列表不改变单机频道数、无多余频道事件订阅、模拟鼠标打开／关闭全屏和筛选、地区倒计时及生命周期回归通过；已查看单分线圆形HUD、全屏地图、筛选和鲸背岛原倒计时画面。标记检查覆盖原管理器的异步 prefab 创建决定，未将其声称为所有实体图标／详情的实际渲染完成。

本批静态元数据检查、正式工程 Unity 导入／编译与 `ProjectTools.Validate` 通过；原标记管理器64组离线条件、24组归属条件及实时状态／地区事件在带图形 Play Mode 回归通过。原生来源、输出、脚本及接线哈希经重建和校验通过；验证使用隔离快照，不写用户档案，尚无正式 Windows Player 接线结论。

沿用已确认的本地地图存档、神像最近可站立格传送和英雄／魔女共享每日访问额度。规则细节分别见[森之国](../maps/map-11.md)、[战斗执行](battle-execution.md)；地图奖励沿用户确认方案：原权重选取、原数量乘等级／阶位倍率，小数按概率取整，再应用原属性29加成并向下取整。普通点位按原配置扣体力57×5；命运探寻来源实体须按官方来源规则免体力。刷新沿用户确认的本地方案：保留点位、区域开放、原组间隔与上限，计入离线时间。

## 尚未实现

- 英雄雕像原激活动作与三种周期效果的角色、粒子、音效和 MyPlayable 演出；界面与永久属性事务已有实际指针验证，完整表现未完成。
- [命运果实与命运探寻](world-exploration.md)的全部实体交互与演出。果树生产继续复用已有 [果树服务](fruit-tree.md)，探寻费用、候选／保底、动态实体、历史和原窗口已部分验证；遗失女神结算已接入规则层，原领奖界面与地图入口仍待完成。
- 地图体力怪物的实际战斗完整操作和冷读回归、铁矿／时之砂地图点击、奖励粒子及全部原生材质；不能仅以服务事务检查替代运行验证。
- Map11原日夜专属场景装饰、局部灯光和小地图未探索遮罩；区域地表阴影与原渐退已接通，但 `ActiveRegionTilemapDisplayHelper`／`ActiveRegionTileMapDisplayHandler` 及其他地图原载荷仍待接入。
- 正式宿主尚需以公共原生工厂接通小地图 HUD、全屏页签、正式快照读取、公共地区资源提供器及各图实体图标与详情；未完成的官方表现和各图完整路线继续按所属地图页维护，共享底层不自动完成单张地图。

共享原演出核心见 `Runtime/Official/StoryPlayables/` 与 `scripts/prepare_unity_story_playable_runtime.py`：原 `MyPlayable` 状态机、双阶段中心、序列、触发器、有序容器与粒子淡入淡出保留；离线显式注入及调用方帧阶段替代原服务注册。隔离 Unity 完整校验、Play Mode 调度／状态／异常恢复及 3 项迁移回归通过，视觉和输入未覆盖；正式宿主、原 prefab、动画／骨骼、Timeline、相机和音频仍待绑定。来源与验证范围见[龙之国](../maps/map-14.md)，不据此开放完整地图。

地图 14 的专属实现、决策和缺口见[龙之国](../maps/map-14.md)。

共享原声音及 Timeline 驱动已恢复至 `Runtime/Official/StorySounds/`：保留官方循环、停止、事件目标和手动时钟绑定；内置屏隔离 Play Mode 调度验证通过。原事件 ID 桥接复用官方 SDK Post，完整原声总线 DSP／RTPC、原演出 prefab／气泡与正式剧情入口仍待接通，范围见[龙之国](../maps/map-14.md)。

共享原 Animator 参数执行组件位于 `Runtime/Official/StoryAnimation/`，保留原缓冲、来源分层、暂停和淡出／停止恢复；隔离原生 Animator Play Mode 已验证生命周期及复用。来源和范围见[龙之国](../maps/map-14.md)；该检查尚不覆盖原骨骼／角色画面、控制器资源绑定和正式地图业务。

共享原剧情外部节点注册与 Cinemachine 轨道空绑定逻辑位于 `Runtime/Official/StoryNodes/`，两种异步等待保留官方阶段、令牌与取消行为，职业 UI 注册可共用管理器；隔离 Play Mode 注册／取消／轨道绑定验证通过。原 Timeline 场景和正式地图流程仍未完成，见[龙之国](../maps/map-14.md)。
