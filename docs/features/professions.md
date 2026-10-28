# 职业、晋升与自由转职

[总索引](../README.md)

状态：未完成

## 功能简介

人物页进入职业总览、职业树、详情、升级和成就；原导师入口在地图寻路并打开实际职业窗口。自由转职按原 Function 135 的人物 100 级及职业分支条件开放。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

已核对官方职业总览、职业树、详情、成就和相关弹窗的源码、prefab 及配置。职业包 8357 个、规则弹窗包 241 个组件，奖励图标包 1900 个、说明弹窗包 100 个组件，以及字体、外观和提示图依赖共 16035 个组件的字段已经严格解码，完成转换输入写入、重开和逐字段比对。原生导出已恢复 1525 个资产中的 1853 个托管引用注册表、3377 条记录、12734 个对象引用和 1665 组打包数组，保留 CAB／对象身份、RID、顺序、浮点位值与层级身份。转换缓存尚未接通全部官方运行时类型和显式资源绑定。

主页面立绘按原 `ProfessionSettings` 的 GUID 从官方 Sprite 提取，38 个职业的男女映射复用 12 张原图片，保留 alpha、PPU、pivot、border；原 `ImageLocator` 的原生尺寸及锚点修正代码已迁移。职业总览、雷达、职业树的五种状态与条件分组，以及馈赠的原面板控制代码已迁入 `Runtime/Official/ProfessionUI/`。关闭、倒计时、导师入口和剧情节点登记通过服务接口连接本地运行环境；服务和原生 prefab 接线仍未完成。不能把旧节点投影界面算作原生 prefab 迁移完成。

原 `ProfessionCharacterPortrait`、`CharacterPortraitBase`、请求器、缓存、纹理对／池及两个接口共 8 个类型已迁入，保留请求键、原始回调次序、60 秒过期、订阅占用、后进先出复用、每桶每次仅缩减一个纹理及低内存清理行为。MessagePipe 的取消对象按官方原生指令恢复为先取消再释放；管理器与低内存事件使用显式绑定，外观数据在此边界按原对象引用传递。隔离 Unity 的实际 Play Mode 验证 GPU 纹理读回／字节重载、请求替换、禁用取消与通知顺序、重启用订阅和缓存／池生命周期，通过并检查测试纹理画面。测试纹理仅用于读回验证；真实角色拍摄、持久化、外观动画和原生页面仍未接通。来源见[肖像生命周期](../../assets/reference/profession-ui/portrait-lifecycle-source-manifest.json)。

原 `CharacterPortraitPersister`、磁盘集合、状态、详细请求数据及其接口已迁入，复用原 `IStorage`／`IPersistentState` 和 UniTask 契约。保留版本 3 失配时先清缓存、再改版本并写状态、最后清过期项的次序；文件名只含请求 ID 与外观哈希，不含 VersionId，原始纹理字节不重新编码，过期按最后访问时间加三天判断。Windows 沙箱根目录由调用方显式传入，尚未选择正式玩家存档路径或接入正式状态提供器。隔离 Play Mode 实际写盘、重新创建状态提供器／持久化器并读回 GPU 像素，再载入新纹理；核对版本、空数据、文件独占读写失败、过期与清空后复用，通过并查看读回纹理画面。文件占用探针产生的两条共享冲突和两条空数据错误为已断言的原失败分支。来源见[肖像持久化](../../assets/reference/profession-ui/portrait-persistence-source-manifest.json)。此验证不覆盖真实角色拍摄或职业页面的存档恢复。

原 `PlayerPortraitSettings`、完整序列化字典和皮肤类型枚举已迁入；四组拍摄参数及默认／加载／禁用三张 Sprite 直接取自 APK，保留原网格、UV、pivot、边框和纹理采样字段，ASTC 各 mip 解码为 Windows RGBA32 原生 Texture2D。职业拍摄尺寸为 260×310、正交尺寸 1.9、偏移为 `(0, 1.9, -5)`、深度 24，并捕获粒子；其他三组参数、皮肤哈希权重与排除项均保留。隔离 Play Mode 核对全部配置、原字典反序列化和删除分支、延迟纹理封装及不销毁标记，三张 Sprite 几何与每个 mip 的 GPU 读回哈希通过，运行图片已查看。原占位肖像只用于其默认／加载／禁用分支；真实角色拍摄及职业树角色仍未接通。来源见[肖像配置与资源](../../assets/reference/profession-ui/portrait-settings-source-manifest.json)，代码入口为 `PlayerPortraitSettings.cs`，资源入口为 `Assets/Official/ProfessionUI/Portrait/PlayerPortraitSettings.asset`；未生成完整素材快照。

原 `CharacterPortraitManager` 的队列、缓存更新、回收和皮肤哈希映射已迁入；两个 UniTask 状态机按原分支恢复，等待结束并释放关联取消对象后再次读取外观，再拍摄并按 `NeedPersist` 决定写盘。读磁盘不受该标记限制，更新会在同一帧处理全部排队请求。保留十秒清理间隔、订阅保护及每四次清理缩减一次纹理池。原 `CharacterPortraitUtils` 中的四个参数／详细请求／哈希／有效性方法单独迁入 `CharacterPortraitDetails`：从 `ModifyTimeStamp` 开始，只对权重大于零的皮肤哈希逐项异或，不乘权重；有效性只比较外观哈希。只读适配器在现有对象边界访问原 `SkinMap`／`SkinId` 字段，零或负权重不会读取对应皮肤值。

隔离 Play Mode 通过原哈希、空的未选皮肤、同帧队列、拍摄前外观重读、原持久化器真实磁盘复用、选择性写盘、取消／无令牌／拍摄失败分支、原加载／禁用资源及销毁取消验证。夹具推进私有清理时钟，核对订阅保护、过期后移除和第四次清理仅释放一个纹理，没有等待四十秒；实际 GPU 红色测试纹理已查看。此处的拍摄器和外观对象均为验证夹具，正式原 studio prefab、玩家外观对象、皮肤表和游戏后台服务尚未绑定，不能据此声称真实角色或职业界面已完成。来源见[肖像调度](../../assets/reference/profession-ui/portrait-manager-source-manifest.json)与[原哈希方法](../../assets/reference/profession-ui/portrait-details-source-manifest.json)。

原 `AppearanceConfig` 及其九个值类型／枚举已完整迁入；配置直接取自 APK，保留 13 类皮肤及基础／可设置标记、四组附加皮肤、三个挂点动画组、男女覆盖 ID、1,742 条有符号 64 位皮肤哈希和 466 条骨骼路径。隔离 Unity 导入和实际 Play Mode 逐项核对全部字段、查询返回的原列表／实例、延迟字典、重复键异常及异常后部分缓存、哈希追加不去重／排序与反向删除，均通过；变更测试只修改克隆，不写入正式配置。此配置不包含渲染器，真实角色、studio 和完整职业页接线仍未完成。来源见[外观配置](../../assets/reference/profession-ui/appearance-config-source-manifest.json)，代码入口为 `AppearanceConfig.cs`，资源入口为 `Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset`。

原 `CharacterPortraitPhotographer` 的相机、临时 RenderTexture、CommandBuffer、材质／子网格绘制、纹理池读回、构建器缓存及异常／释放顺序已迁入；两个原渲染器收集方法、粒子提供器和世界 Z 倒序比较器一并迁移。外观准备、构建器及渲染后端通过显式接口连接，尚未安装原 studio prefab 或正式角色。隔离 Direct3D Play Mode 使用测试网格，实际检查原 260×310／100×120 尺寸、透明背景、最后一个非空子网格材质、相机参数、缓存与调用／释放次序；原根渲染器／分段／挂件／粒子收集、粒子缓存与 ColorMask 以及三条设置失败分支通过，GPU 输出图片已查看。测试网格不代表真实角色画面或职业页完成。来源见[原肖像拍摄与收集](../../assets/reference/profession-ui/photographer-source-manifest.json)。

原 `CharacterPortraitManagerConfig` 的对象指针已定位到 APK 中的 `CharacterPortraitStudio.prefab`；其八个 bundle 的闭包及 15 个脚本组件已严格解码、写回并重开比对。转换后的两组根资源核对 5 个 GameObject／Transform、11 个脚本组件、相机、MeshFilter 和 MeshRenderer，共 485 项值、85 处指针；保留完整挂点路径、浮点负零、原脚本和组件引用，并显式核对原未烘焙光照及空 Enlighten 流。场景和原配置只保留在转换缓存，未安装带缺失脚本的 prefab，尚未覆盖 Windows 导入或真实角色运行。来源见[原拍摄场景](../../assets/reference/profession-ui/portrait-studio-source-manifest.json)，转换入口为 `scripts/prepare_unity_profession_portrait_studio.py`。

独立的 `ProfessionNativeCurve` 将原三种待机动作（`home/idle_01`、`100/idle_01`、`200/idle_01`）的标量骨骼轨道转为 Unity 加权 `AnimationCurve`，保留原时间、数值、绝对 Bezier 控制点、线性／阶跃分段及首关键帧之前不应用的语义；不使用 Spine runtime。男女共 1,171 条曲线、5,177 个关键帧，隔离 Play Mode 以每个原关键帧、每段三个内部时间及两端边界共 19,537 个点，对照独立二分反解的三次 Bezier，最大绝对差 `0.00022315979`，最大归一化差 `0.0000860095`，满足本数值检查的 `0.0002 + 0.000005 × abs(value)` 容差。未输出角色画面，也未完成骨骼世界变换、附件组合、IK／变换／物理约束及实际角色绑定，不能作为动作视觉一致性的结论。来源见[原生曲线适配](../../assets/reference/profession-ui/native-curves-source-manifest.json)。

独立的 `ProfessionNativeBonePose` 已将六组原待机曲线按原 setup 旋转／位置相加、缩放相乘，接入男女共 3,542 根骨骼的局部姿态；保留首关键帧之前不应用和所选皮肤骨骼／祖先启用，普通及仅平移的世界继承使用 Unity 仿射矩阵。隔离 Play Mode 核对 1,171 条原曲线、48 个局部姿态，共 425,108 项标量，最大局部绝对差 `0.00006389618`，满足 `0.0004 + 0.000007 × abs(expected)` 的本次容差；夹具世界矩阵对照原生 Unity Transform，并检查剪切、仅平移、皮肤启用和源对象隔离。选择性旋转／缩放／反射继承明确拒绝，此处未求解变换／物理约束，待机 IK 接线见下文，也没有角色网格或完整原世界姿态的验证。来源见[原生骨骼姿态](../../assets/reference/profession-ui/bone-pose-source-manifest.json)。

原男女角色各八条 IK 约束的 setup、顺序与六组待机的常量混合／弯曲方向键已核对；这些动作实际使用的链为单位缩放、无剪切。独立 `ProfessionNativeIk` 使用平面朝向和余弦定理解双关节；隔离 Play Mode 的 384 个原数据几何用例（192 个双关节、192 个单关节，其中 188 个零混合）与独立圆交点参考角度对照，最大圆周角差 `0.0000305175781°`，满足 `0.0002°` 的本次容差；可达／夹取半径端点、弯曲方向、分数混合、镜像目标坐标和显式失败分支均通过。几何求解已接到下述骨骼姿态适配，尚未接到真实角色，不覆盖原 SDK 的完整世界姿态、一般缩放／剪切、压缩／伸长／软约束及重合目标分支。来源见[原生待机 IK 几何](../../assets/reference/profession-ui/native-ik-source-manifest.json)。

`ProfessionNativeIdleIk` 按原顺序将八条约束接到局部骨骼姿态；世界变换按所需祖先延迟计算，每次旋转使后续缓存失效，未实现的选择性继承仍显式拒绝。隔离 Play Mode 核对六组动作的 48 个姿态、576 个最终旋转和 960 个世界矩阵；与独立几何参考的最大圆周角差为 `0.00020724535°`，满足 `0.0004°` 容差，世界值最大绝对差 `0.00008010864`，满足 `0.001 + 0.000007 × abs(expected)`。原顺序、零混合、父子缓存失效和不支持链的失败检查通过。此处未使用 SDK 实现或姿态参考，不代表完整原世界姿态、物理／变换约束、角色渲染和页面验收。来源见[待机 IK 姿态绑定](../../assets/reference/profession-ui/idle-ik-source-manifest.json)。

原 `AppearanceMaterialUtils` 全部材质操作已迁入：保留染色通道 ID 缓存／最大通道、贴图／染色／光照与头发裁剪重置、四个效果关键字、Stencil 与透明裁剪字段，以及启用父 Mask 查找和根排序 Canvas 的模板深度。`Clear` 只清最大通道、不清缓存和已有材质值，按原行为保留。Mask 参数适配为 Component，原方法只访问其 Transform，不依赖骨骼 SDK。隔离 Direct3D Play Mode 检查这些分支，并以已有官方转换的 UI shader 实际绘制、读回和查看透明背景蓝色测试网格；未新增 shader 变体。原转换程序未声明 `UNITY_UI_ALPHACLIP`，Unity 将其保留在材质关键字列表，`IsKeywordEnabled` 仍返回 false；验证区分这两个状态，未改写原方法。测试不覆盖真实角色几何、皮肤裁剪、动画或完整页面。来源见[原外观材质操作](../../assets/reference/profession-ui/appearance-materials-source-manifest.json)。

原皮肤配置、资源代理、贴图引用／句柄／请求、分辨率枚举及按需加载器共 14 个应用类型已迁入，保留原方法和状态顺序。YooAsset 操作及图集材质访问改由显式本地接口提供；原两个序列化资源指针保留为 ScriptableObject，未使用或移植骨骼 SDK 实现。隔离 Play Mode 检查同步代理复用／等待／释放、原 `LoadAsync` 的同步加载及回调队列、失败释放、主图／染色图状态、空结果完成、占位图索引与恢复、引用计数及基于帧龄／强制回收。首次请求只回调而不写回替代引用、染色图非空但材质为空时的原异常、原两个未实现查询及字典重复键后的部分状态，均按源码保留并验证。资源和图集为验证夹具，正式贴图／图集／骨骼资源服务和原配置资产未安装。主面板 881 个资源、87 类脚本的闭包重查后，剩余 12 类脚本、0 个缺失 GUID；类型可绑定不代表运行服务已就绪。来源见[原皮肤与贴图加载链](../../assets/reference/profession-ui/texture-loader-source-manifest.json)。

原 `AppearanceSkinClip` 全部方法、四个序列化字段和启停／刷新顺序已迁入；角色、骨骼、边界附件和 UI 网格事件通过显式接口连接。保留四点数量检查、MeshScale、原 TransformPoint、凹四边形重排、镜像方向、四边与对角线 shader 参数，以及按材质或主贴图匹配 CanvasRenderer。隔离 Direct3D Play Mode 检查系数、缩放／镜像／非均匀变换、凹四边形、全部材质重置、重复启停、网格与皮肤事件、缺失数据和原顶点数量错误分支；GPU 截图的测试网格中心按原 shader 被裁去、外围保留，画面已查看。边界顶点与骨骼为夹具，真实角色加权边界、皮肤和动画后端仍待连接。来源见[原头发／头饰裁剪](../../assets/reference/profession-ui/skin-clip-source-manifest.json)。

独立 `ProfessionNativeSlotPose` 保留原插槽名称、骨骼、颜色／暗色标记、混合模式、附件与绘制顺序，按六组待机的离散关键帧切换手型、眨眼及武器层级；空附件清除、首帧前 setup 和未启用骨骼跳过均保留。转换 JSON 的 varint 位值 `4294967290` 按 32 位有符号值恢复为原偏移 `-6`，不改写原转换文件；指定槽位放到目标索引，其余槽位保持原相对顺序。隔离 Play Mode 核对 252 个插槽、54 条时间线、84 个附件键、4 个绘制顺序键、109 个时间样本和 27,468 项精确检查，含关键帧边界／前后、逆向取样、武器 `+4/-6` 目标、数据所有权和非法绑定。此处只实现未混合的离散状态，尚未连接皮肤查找、网格、动画混合或真实角色画面。来源见[原插槽与绘制顺序](../../assets/reference/profession-ui/slot-pose-source-manifest.json)。

独立 `ProfessionNativeVertexAttachment` 读取两具原骨骼的所有网格、包围框和裁剪附件顶点，保留骨骼索引、局部坐标、影响顺序和原权重，不归一化权重；未加权顶点使用原插槽骨骼，输出仍为原数据单位。隔离 Play Mode 对 11,747 个附件、165,433 个顶点、264,312 个骨骼影响，使用独立双精度仿射参考逐坐标核对，最大绝对差 `0.000366210938`，通过 `0.0004 + 0.000007 × abs(expected)` 容差；另核对反射／剪切、不归一化权重、零权重的未启用骨骼、输入所有权及非法布局。这里的矩阵为几何验证合成输入，不能当作官方动作或角色画面。变形时间线、区域附件／图集 UV 与生产渲染尚未接通。来源见[原加权顶点](../../assets/reference/profession-ui/vertex-geometry-source-manifest.json)。

独立 `ProfessionNativeAtlasRegion` 接入当前版本男女图集的 19,805 个区域／929 页描述，保留裁边、原图尺寸、四种旋转及原附件的位移／旋转／缩放。所有 11,716 个原网格的 165,309 组 UV、22,230 个区域附件的 88,920 个四角在隔离 Play Mode 与独立中心仿射参考比较通过；最大 UV 差 `5.96046448E-08`、原坐标差 `0.0000305175781`。来源按 APK 优先解析：女图集来自 APK，男图集来自已校验的当前版云端 bundle，保留各自来源和哈希；所有图片路径均解析到原区域。实际图页的生产 GPU 采样、材质查找和角色渲染仍未接通，未使用旧烘焙 prefab。来源见[原图集几何](../../assets/reference/profession-ui/atlas-geometry-source-manifest.json)。

原 `AppearanceCharacter` 全部方法及序列化字段、`AppearanceRuntimeData`、`HsvChange`、外观接口与原性别枚举已迁入；角色路径复用原地址工具的三个方法。资源／部件通过显式接口连接，骨骼资产仅在边界改为 ScriptableObject 引用。隔离 Play Mode 检查原资源加载／结果读取／释放顺序、相同资源引用、原性别枚举和路径、先释放再检查有效性、皮肤过滤／排序／附加项重复、部件路由、染色值复制与单通道缓存、空值／无效分支和销毁。原代码重复加载时会覆盖旧 split 句柄而不提前释放，该行为按源代码保留；不自行添加资源状态变化。验证使用资源／外观／部件夹具，正式角色数据、部件服务和真实骨骼后端仍待接入。来源见[原角色外观生命周期](../../assets/reference/profession-ui/appearance-character-source-manifest.json)。

原应用层 `AppearanceSpine` 全部方法和序列化字段已迁入，按原顺序清空／复用组合皮肤、依次加入存在的皮肤，保留名称重复和后项覆盖。首次切换与相同皮肤重设使用原两条分支；重设只替换原 setup 名称能查到的非空附件，随后更新缓存、复位 setup 并通知。初始化的空值／同对象／不同对象分支、事件异常捕获、HSV 材质写入、配置失败和部分／完整释放顺序均在隔离 Play Mode 检查通过。骨骼资产改为 ScriptableObject 引用，皮肤／插槽／骨骼行为由显式接口提供；验证端点为夹具，原生数据后端和真实角色渲染尚未接通，未移植骨骼 SDK。来源见[原组合皮肤与骨骼调用](../../assets/reference/profession-ui/appearance-spine-source-manifest.json)。

独立 `ProfessionNativeSkin`／`ProfessionNativeSkinCatalog` 实现原应用接口，读取全部 1,007 个原皮肤、34,009 个插槽／名称附件键、9,749 个骨骼成员及 9,633 个约束成员，保留附件对象共享身份、后加入皮肤覆盖和成员有序并集。查找优先活动皮肤，再回退到 `default`，名称按原大小写区分。隔离 Play Mode 核对全部原条目与 1,007 组组合，清空／复用、输入所有权和源表不被修改均通过。验证附件目前为身份对象，尚未绑定生产几何／材质；换皮肤时的插槽迁移、世界约束和实际渲染仍待连接。来源见[原生皮肤表](../../assets/reference/profession-ui/native-skins-source-manifest.json)。

独立 `ProfessionNativeSkinSlots` 按公开的皮肤行为约定接入原槽位附件身份：首次皮肤仅附上初始姿态可见项；后续仅替换仍使用旧皮肤对象的对应槽位／键，保留手动附件、空槽和缺少替换项的旧对象；显式复位再按当前皮肤、默认皮肤解析。隔离 Play Mode 对全部 1,007 套原皮肤的 4,028 次切换、888,174 项槽位状态逐项精确比对通过，并检查对象身份、原键遍历、输入所有权及非法布局。尚未连接完整骨骼／约束激活和动画混合。来源见[原皮肤槽位切换](../../assets/reference/profession-ui/skin-slots-source-manifest.json)。

全部原附件已接入独立的原生区域／网格、包围框、裁剪及点对象，保留原三角索引、UV、权重、类型与骨骼数据缩放。隔离 Play Mode 对 34,009 个附件、254,385 个缩放后顶点／点和 635,730 个三角索引检查通过；最大坐标差 `3.81469727E-06`，最大 UV 差 `5.96046448E-08`。原 `body_200_M_01` 高分辨率主图及染色图按原加载器的占位贴图和当前 AssetTable GUID 解析，APK 优先、逐 mip 解码并保留采样／色彩空间；同名两图使用独立路径，载荷与哈希分别核对。实际 D3D11 渲染含 25,719 个可见像素、22,359 个彩色像素，与独立参考几何零像素超出每通道 2 的差值，运行截图已查看。这是合成矩阵下的原附件几何验证，尚未验证原约束角色、生产图形组件或完整职业页。代码位于 `Runtime/Offline/ProfessionUI/ProfessionNativeAttachment.cs`，纹理位于 `Assets/Official/ProfessionUI/CharacterTextures/`，来源见[原生附件与衣服图集](../../assets/reference/profession-ui/native-attachments-source-manifest.json)。本次未生成完整素材快照。

两份原始字体由官方 `FontAssetFontConfig` 的路径、长度和 MD5 取回。原 `MPLUSRounded1c-Medium Atlas` 是 0×0、无像素载荷的动态 Alpha8 图集，已恢复并验证原生导入。当前含动态物品／配置的主面板依赖检查覆盖 881 个资产、87 种脚本、8 种托管类型和 7 个 shader，断开 GUID 为 0；尚有 4 种运行组件及相关服务未接入，主要是外观预览、离屏角色渲染、玩家信息和音频对象，不能视为原生职业界面已经可运行。

馈赠使用的 `ItemIconDefault`、总职业等级说明使用的 `RuleDialog` 及四种奖励 Sprite 已从 APK 的实际对象取回，并记录调用、配置、CAB、对象 ID 和原始哈希。当前版本的成就、物品及规则提示三张 Luban 表已完全解码；组 10 的十条成就配置、四种奖励物品定义及 `ProfessionLevelInfo` 的标题／三条说明与现有打包配置相符。原图标管理器的 ID 覆盖、类型覆盖、默认图标选择顺序已核对，这四种奖励均使用原 `ItemIconDefault`，没有改用手写图标控件。

转换工具对单张奖励 Sprite 的裁剪 pivot 与原 Sprite 不同，现按原逻辑矩形、pivot、PPU、border、网格／索引字节、UV 变换及纹理引用恢复 4 个原生 Sprite；原 ASTC 按既有 UNORM8 解码流程转成 4 个原生 RGBA32 Texture2D，保留 mip、颜色空间和采样参数，Windows 不再二次压缩。`ProfessionRewardSpriteValidation.Run` 在隔离 Unity 中核对全部原始顶点、三角形顺序、UV、pivot、尺寸、PPU、border、纹理绑定、采样与格式；四张纹理上传 GPU 后读回的 SHA256 与解码 RGBA 完全一致。此验证没有覆盖职业馈赠列表的实际渲染和点击。

馈赠图标的原 `ImageProxy`、`ImageLoaderProxy`、`LoaderProxyBase`、`PreviewableProxyBase`、`AssetGuidProxy` 已按原源码迁入，保留嵌套序列化字段、Image 属性委托、路径前缀、同步／GUID／异步加载、当前请求身份检查和清理／销毁顺序。资源操作替换为显式本地接口；原 R3 资源集合的按当前数量正序释放、末尾清空及销毁标记顺序由原 APK 原生指令核实。`ProfessionImageLoaderValidation.Run` 在隔离 Unity 中以原奖励 Sprite 验证乱序完成、失败结果、清理后迟到结果、已完成请求的即时订阅，以及销毁后的请求拒绝和资源释放。原 `ItemIcon` 与本地 Sprite 提供器的接入和验证见下文；此加载器验证没有覆盖馈赠列表。代码及原生证据见[图标加载来源](../../assets/reference/profession-ui/image-resource-source-manifest.json)。


`UIBase`、物品附件／自定义附件、提示附件、品质与附件配置等 12 个原始类型已迁入，保留初始化前后启停回调、异常边界、重置顺序、附件缓存、排序及回收。原附件配置的 `HintTip=13` 按实际 AssetTable GUID 指向 `CenterHintContent.prefab`；其 6 个原生资产（含原 atlas Sprite、贴图、字体和材质）已安装到 `Assets/Official/ProfessionUI/Native/`，沿用已核实的 Windows 字体 shader。隔离 Unity 的实际 Play Mode 验证原层级／引用、延迟创建、缓存不自动激活、显式显示隐藏及回收重借，并通过 GPU 渲染检查，运行截图已查看。测试数字仅用于附件控件验证，未覆盖完整馈赠列表或鼠标输入。来源见[附件代码](../../assets/reference/profession-ui/item-components-source-manifest.json)与[原生附件资源](../../assets/reference/profession-ui/item-resources-source-manifest.json)。

原 `ItemIconAttachmentManager`、`ItemIconAttachmentPool`、`GameObjectPool`、`GameObjectPoolBase`、销毁监听和非泛型 `GAction` 六类已迁入。保留配置首次匹配、GUID 资源句柄、先入先出队列、借出时设置父节点／显隐后重置、归还时先清理再隐藏／入队、反向缩减及低内存释放顺序；场景注入通过显式实例化边界接入，未绑定时拒绝创建。隔离 Unity 的实际 Play Mode 验证这些顺序、原附件同实例重借、低内存释放后原对象销毁并重新借出、管理器释放时取消事件订阅，以及原字体／附件 GPU 渲染，全部通过，画面已查看。完整职业窗口、应用级内存策略及详情弹窗服务仍待接通。来源见[原管理器和对象池](../../assets/reference/profession-ui/item-pools-source-manifest.json)。

原 `ItemIcon` 的字段、初始化／清理顺序、品质与效果分支、计数、进度、附件及点击行为已迁入，依赖通过原代码实际消费的接口连接。原 `ItemIconConfig` 的 8 个有序品质引用（含重复白色）、附件配置的 42 个 GUID，以及四种当前奖励的图标路径／品质已核对；`ChestInfo` 和 `AppendAppearanceInfo` 两种图标覆盖表均不包含这些物品。33 个原生资产安装到 `Assets/Official/ProfessionUI/Native/`，本地资源目录保持原逻辑地址和 AssetTable GUID，不生成界面层级。来源见[原控件](../../assets/reference/profession-ui/item-icon-source-manifest.json)、[配置](../../assets/reference/profession-ui/item-configs-source-manifest.json)和[本地绑定](../../assets/reference/profession-ui/item-catalog-source-manifest.json)。

公共图集、品质图集、遮罩和字体图集恢复为原生 Texture2D，保留全部 mip、颜色空间、可读性和采样参数；原 ASTC 解码成 RGBA32，原 Alpha8／RGBA32 保持像素字节，不经过 PNG importer 再次压缩。替换保留原 GUID／fileID，并将 Texture2D 外部引用类型从导入资产 3 改为原生资产 2。隔离 Unity GPU 读回四张纹理的每个 mip，字节数与 SHA256 均与源转换结果一致；动态图集检查在生成新字形之前进行。来源见[原生纹理](../../assets/reference/profession-ui/native-textures-source-manifest.json)。

原 `FontAssetFontService` 已迁入，保留延迟加载配置、源字体已绑定判断、句柄保留、反射绑定、按名称释放及销毁顺序；两个原始 TTF 以 Windows 动态 Font 交付，原 JSON 字典在打包时解析为等值本地配置，运行时核对原 JSON 后交回原服务。未迁移 Android 流式缓存路径，也未声称完成多语言字体服务。隔离 Unity 的实际 Play Mode 使用正式本地图标／字体目录渲染四种奖励，逐项核对原品质背景、文字和字体归属，模拟鼠标按下／抬起进入原 `ItemIcon` 详情调用入口，清理后核对监听解除和控件状态；四项通过，实际运行画面已查看。附件提示也使用原字体重跑。测试中的统一数字 300 只验证控件，正式馈赠数量仍由成就配置决定。此处没有验证详情弹窗内容、完整馈赠列表或职业主页面。字体来源见[字体服务](../../assets/reference/profession-ui/font-service-source-manifest.json)。


`RuleDialog` 的全部设置重载、原列表复用、行激活及 `UIUtils` 的变换复位顺序和 `TipInfo` 的图文设置代码已迁入；原规则解析器结果通过本地接口提供，实际说明弹窗仍待接线。已核对并复用原 `BgDialogElement`、`ItemProgress` 的现有迁移组件，以及从原生代码恢复的 `TMPEffect`，未用空脚本消除原 prefab 引用。包含上述动态资源及物品配置的闭包为 881 个资产、87 种脚本，断开 GUID 为 0，仍有 12 种运行脚本待接入。

原 `TopBarHandler`、`TopBarItem`、管理器接口、货币数据及枚举等七类已迁入，保留空列表判断、父节点与矩形复位、窗口显示／隐藏订阅、停止数量更新及回收顺序；外部货币控件通过原代码消费的 Component／方法接口接入。主面板闭包内三个原始顶部栏的两份货币列表均为空，原分支不会借出栏位。隔离 Play Mode 控件测试验证空列表、更新、窗口事件和重复启停；非空货币栏的完整 prefab／服务仍待迁移。来源见[顶部栏](../../assets/reference/profession-ui/auxiliary-source-manifest.json)。

原 `WikiEntry` 保留启停订阅、服务器／玩家／配置／条件检查顺序、URL 分支、SDK 参数、上报字段和通知顺序。当前完整 `wiki` 表有四条 US 配置，原解析器固定筛选 CN，因此职业入口按原缺配置分支隐藏。完整表和原地区筛选已打包；隔离 Play Mode 控件测试验证各短路条件，并以模拟鼠标核对连续启停后的单次点击、SDK 结果消息、上报／通知及空 URL 提示，测试渲染已查看。此测试使用服务夹具，不代表完整职业 prefab 的布局、外观或其他地区 Web 服务已完成。来源见[百科入口](../../assets/reference/profession-ui/wiki-source-manifest.json)。


音效触发字段已按 APK 原始组件恢复。转换工具曾把 8 个 `AkEvent.triggerList` 的原始值 `-320808462` 写成无效的十六进制字符串；现以原始整数的小端字节重新写入，并逐个保留原层级、事件引用及其余字段。原生 `AkTriggerHandler.OnEnable` 的指令核实该值为启用触发。`ButtonSound`、`UIClickSound`、`PopupWindowSound` 及实际使用的 `AkEvent` 启用／Start、事件引用和单参数 `Post` 路径已迁入；动作、回调等本页未使用分支显式保留为未接入范围。隔离 Unity 导入验证 8 组触发列表、原生事件 ID／名称／GUID、自身发声对象及原序列化播放 ID，核对无效引用返回零和有效请求传递／保存返回 ID 的顺序。该探针只证明原生字段与请求契约，该字段探针没有覆盖声音播放或界面输入；Windows 播放适配另见下述验证。来源见[音效字段与代码](../../assets/reference/profession-ui/audio-source-manifest.json)。

五个原始职业声音事件 `Main_Tag`、`Main_Upgrade`、`Main_Cursor_Fail`、`Main_Cursor_Back`、`Main_Cursor_Normal` 已从原生 SDK 引用追踪到当前版本的 `EventBankMapping`，核实原事件／动作／声音／媒体 ID；`minimum.pck` 与 `ui_sfx_01.pck` 使用 APK 中哈希相符的实际载荷，映射使用当前云端已下载的哈希校验载荷。原 Wwise Vorbis 经 wwiser／vgmstream 转成五份本地 PCM WAV，禁用自动音量提升，分别保留原 −6／−8／−16 dB 增益、声道、采样率和时长；Windows AudioImporter 使用 PCM，不归一化、不重采样、不再次有损压缩。资源及显式事件目录位于 `Assets/Official/ProfessionAudio/`，本地播放桥位于 `Runtime/Offline/ProfessionUI/`，没有替换原 UI 控件。

`ProfessionAudioPlaybackValidation.Run` 已在隔离 Unity 的实际 Play Mode 核对五份 AudioClip 的全部 PCM 采样、采样率／声道／帧数及显式绑定，再通过原事件包装向 Windows 播放桥发出五个声音请求，逐项观察真实 AudioSource 的样本推进与 AudioListener 非零输出。验证在正式进入 Play Mode 并等待实际游戏帧后开始，五项播放全部通过。此范围只完成声音载荷和平台播放，正式职业窗口生命周期及本地设置混音仍未连接，也不替代职业界面鼠标操作验收。处理与哈希见[Windows 音频来源](../../assets/reference/profession-ui/windows-audio-source-manifest.json)；未生成完整素材快照。


职业代码、工具和来源凭据在 Git 中保留原始字节，避免检出时的换行转换破坏已记录哈希。职业转换缓存使用 `.tools/source-packages/current/871_156598/profession-ui/`；验证工程、日志和隔离数据使用 `output/validation/professions-<GUID>/`，通过 `ZJCS_VALIDATION_DIR`／`ZJCS_REPOSITORY_ROOT` 显式定位并检查会话标记。重建转换缓存时保留已接入资源的 Unity GUID，其余按所选版本和原导出相对路径固定标识；先一致重写引用，再核对原层级、托管注册表、对象身份、浮点位值和事件数组。重复恢复不改变本轮已核实的 1525 项输入／输出资产哈希。原 GUID 对照见[转换资源标识](../../assets/reference/profession-ui/native-guid-identities.json)。

转换输入工具允许显式选择隔离 Unity 工程的实际 TMP／UGUI／URP 编译程序集作为类型元数据输入，逐项记录来源与 SHA256；默认仍使用主工程。本次刷新后 357 个 bundle 的转换输入字节与上次完全一致，复用既有导出，再按来源恢复事件数组，没有绕过哈希校验。


红点系统按原 `TipDotNodeBase`、`TipDotConditionNodeBase` 和 `SafeAction` 迁移聚合、激活、优先级／等级选择、全部子节点门槛、忽略等级、反转、屏蔽和脏刷新，保留去重及可重入回调。职业、晋升、自由转职及总等级成就的 5 种条件节点通过只读解析器／玩家事件接口接入本地；接口实现仍待连接。原图资产的读取与连接遍历代码、UI linker 和计数显示控制代码已恢复；图编辑器及故事／服务器节点执行 API 不属于该界面的运行范围。隔离 Unity 真实导入官方 2 张图、16 个节点，核对 6 种托管类型、节点字段、条件数据、预设、端口顺序及每条连接，导入和核心行为验证通过。

主面板所需的字体、灰化、粒子及 Spine UI 共 7 个 shader，按原 CAB、对象 ID 和原始载荷 SHA256 复用工程中已从官方 GLES 转换的结果，保留原材质引用的映射；`Hidden/JSZX/TextMeshPro` 的双描边、底色、辉光、灰化与裁剪分支均在原转换范围内。隔离 Unity 验证 7 个原生 GUID、源码哈希、加载及支持状态，662 组原始程序的 1324 个 Windows D3D 顶点／片元阶段全部编译通过；尚未据此声称实际职业界面渲染通过。

成就 C# 规则已按官方 `AchievementRawInfoParser`、`CycleTemplateInfo` 和 `AchievementInfo` 生成 10005 个档位，保留已领取／处理中条目的过滤和排序。点击任一可领取行时，按原 `AchievementPanelItem` 的组 10／`count=-1` 请求及共享 `Player.TryRecordAlreadyGotAchievementReward`，领取该组所有已达成、未领取档位；本地奖励和记录使用一次事务。原配置边界、五行显示、循环尾档、批量领取和重复领取保护的独立 C# 测试通过。

隔离 Unity 工程已完成迁入控制组件的导入、编译及 `Zjcs.Editor.ProjectTools.Validate`。`ProfessionProgressionValidation.Run` 使用独立存档验证总等级 5 时一次取得 1200 个原奖励物品、四条记录，重复请求不写盘，冷读恢复，总等级 6 时只领取循环起始奖励；真实文件锁导致的写盘失败和物品数量溢出均保持原存档与内存状态。职业预览的服装／武器处理按原 `ProfessionUtils.SetProfessionAppearance` 克隆玩家外观，不改写装备和存档。尚未完成职业原生 prefab 的 Play Mode 点击、画面与保存恢复验证，以上不代表功能完整还原。

原 `AppearanceUI` 的完整应用层代码已迁入，保留多 Canvas 初始化、网格越界清空、零 stencil 时延迟换装／染色、原字典引用、材质与贴图请求顺序、动画及释放逻辑。隔离 Play Mode 使用实际 Unity Graphic、CanvasRenderer、排序 Canvas、嵌套 Mask 和材质验证：遮罩回调重试／解除、换装先于染色、重复占位贴图复用材质、加载引用计数、异常网格清空、释放与原订阅行为均通过。骨骼和资源端点使用验证对象，尚未据此验证真实角色画面。来源见[原 UI 外观代码](../../assets/reference/profession-ui/appearance-ui-source-manifest.json)。

原 `OffScreenUISpinePlayer` 的两帧异步更新和取消流程已恢复；隔离 Play Mode 逐帧核对重复事件取消、令牌释放、包含未激活子对象的原遍历顺序、所有角色更新模式归零、仅活跃非根角色重新计算边界、禁用取消及销毁退订。保留原代码只检查层级激活状态的行为。原 prefab 的两处角色参数已核对：循环、速度 1、未冻结、按实际帧更新；不能使用静态姿态替代完整动画。来源见[原离屏角色刷新](../../assets/reference/profession-ui/offscreen-player-source-manifest.json)。

原生几何裁剪已覆盖两具原模型的 6 个凸四边形边界。隔离 Play Mode 对 960 组求交、5,160 个输出顶点与 GEOS 双精度参考比较，最大面积差 `6.6130726E-05`、重心差 `3.20112824E-05`、UV 差 `1.1920929E-07`，正反边界、绕序和完全内／外及部分相交分支通过。`ProfessionNativeMeshBatches` 保留原完整槽位绘制顺序、连续图集／混合分组、显式分隔、槽位颜色和网格复用；原两处 prefab 的裁剪开启、非预乘顶点色和零 Z 间距已核对。结束槽位包含自身，空／禁用结束槽位同样结束裁剪，结束等于开始则持续至绘制末尾。隔离 D3D11 使用原转换 UI shader 验证裁剪后的红色几何及结束后的完整蓝色几何，实际截图已查看。几何与白贴图为验证夹具，尚未连接生产材质／部件、真实角色约束或完整职业页；不支持的凹边界或重叠裁剪区间显式报错。来源见[原边界求交](../../assets/reference/profession-ui/native-clipping-source-manifest.json)及[原绘制参数与批次](../../assets/reference/profession-ui/mesh-batches-source-manifest.json)。

原 `PlayerAppearanceInfo`、`PlayerAppearance`、`PlayerAnimationAppearance` 与 `ProfessionAppearance` 及其回调／覆盖契约已迁入，保留玩家事件登记／停用／恢复、忽略地图与晋升事件、轻量信息回调替换／释放、运行数据对象复用、覆盖还原和动画先于加载的原顺序。`Clear` 保留原覆盖器及未释放的轻量信息回调，不额外改变原行为。职业预览按原 `ProfessionUtils.SetProfessionAppearance` 克隆数据，再修改默认主／副武器与追加外观奖励，最后应用外观并循环播放 UI 动画；原玩家数据不变。APK 内 7 条动画命名规则按原字段和顺序恢复为 `Assets/Official/ProfessionUI/Portrait/AppearanceAnimNameCombineRule.asset`。隔离 Play Mode 验证上述生命周期、缺失性别默认值、None 分支、动画名缓存／loop 和原配置反序列化／格式化通过。数据、回调及资源／动画提供方为验证对象，正式本地玩家事件、外观数据构建和角色后端仍待连接，不代表职业页可运行。来源与入口见[玩家外观及职业预览](../../assets/reference/profession-ui/player-appearance-source-manifest.json)；本批素材未生成完整快照。

原 `PlayerWeapon`、`CharacterPartPoolEntry`、部件借还接口和完整职业枚举已迁入，保留预绑定世界／UI 组件选择、独立部件借还、UI 不入池／世界入池、未激活子骨骼查找、局部位置／旋转／缩放复位、原皮肤缓存和朝向动画。加载事件异常按原逻辑先记录并重抛，由刷新外层捕获后仍执行朝向播放；释放仅归还有效条目，不额外清除原保留字段。隔离 Play Mode 检查上述执行顺序、重复预绑定换皮肤、无效借用／骨骼、左右朝向与皮肤兜底，以及条目原 20 秒存活时间和默认缩放恢复通过。骨骼、资源和池为验证端点，未验证真实武器动画／美术或完整职业页。来源与入口见[原武器外观调用链](../../assets/reference/profession-ui/player-weapon-source-manifest.json)。

原 `AppearancePartHandler`／`AppearanceUIPartHandler`、部件条目、槽位分组、部件接口、分隔槽配置及部件模式枚举已迁入，保留过滤、皮肤缓存、效果子节点逆序提取、额外挂件队列、主部件初始化及事件顺序。原分隔指令和前／后挂载、默认保留世界变换、动态槽位去重、逐帧脏状态复位，以及延迟销毁和立即卸载／句柄释放顺序均通过真实 Play Mode 帧检查；空 prefab 的原句柄保留行为也按源码核对。首次验证预期误将后置挂件设为前层，修正验证后通过，原实现未改。骨骼／跟随器／资源和分隔渲染器仍为验证端点，真实部件 prefab 和生产后端尚未绑定。来源见[原 UI 部件挂载](../../assets/reference/profession-ui/ui-parts-source-manifest.json)。主页面静态依赖重查为 881 个资产、87 类脚本、4 类尚未迁入组件、0 个缺失 GUID；不代表完整页面可运行。

官方游戏自有的 `BaseSkeletonFollower`、UI／世界跟随器、跟随接口及效果／骨骼部件共 6 个类型已迁入，骨骼 SDK 的类型消费改由显式原生接口提供，未读取或移植其实现。隔离 Play Mode 核对父子／跨层级变换、RectTransform 骨骼坐标缩放、UI 的原 Z 间距 ×100 与世界间距、绘制顺序索引历史、旋转／镜像／缩放开关、未激活骨骼和附件身份显隐，以及启停／清理／销毁／重建订阅。原部件按目标动画名建立源名称字典，但播放仍传入原请求名；该分支、缓存与配置初始化失败后不重试的状态均按源码保留并通过检查。验证使用骨骼／插槽／图形端点，真实角色和动态部件 prefab 尚未连接。代码位于 `Runtime/Official/ProfessionUI/`，来源见[原挂点跟随与部件](../../assets/reference/profession-ui/followers-source-manifest.json)。

## 自定义实现

角色动画沿用已确认的[免费原生迁移方案](../asset-management.md#spine-角色动画迁移决策)。当前自定义适配完成源骨骼标量曲线到 Unity 加权曲线的映射、原局部姿态、普通／仅平移继承及待机 IK 几何／姿态、插槽／绘制顺序绑定的验证，未改变动作或游戏规则；完整角色动画尚未实现。

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

自由转职服务端扣除方法未取得。用户确认生成三组方案后取消不消耗转职石，最终确认才消耗；本地费用适配器遵循免费 0 个／非免费 1 个转职石，另存当次报价支持恢复。免费间隔继续按原 168 小时计算，生成免费方案即推进时间，放弃不重置该时间。服务端请求改为本地原子存档事务，写盘失败回滚费用、方案、职业及奖励。

## 尚未实现

- 红点图的本地玩家事件、图生命周期／controller 和 UI linker 实际接线，以及红点显隐和数量的交互验证。
- 职业树卡片的官方角色 Spine／小人外观和动画；主职业页立绘、雷达及属性列表与游戏内画面的实际对照验收。
- 职业详情中的官方技能和属性、升级消耗与徽章领取差额；树中未解锁条件、导师寻路与晋升条件。
- 成就五行列表的领取状态与官方奖励图标／数量、批量领取、保存恢复和重复领取保护的实际运行验收。
- 职业转换与晋升；导师自由转职的比较、三组方案、确认、原奖励、撤回及免费间隔。
- 全部原生组件与资源接线后的 Unity 编译、Play Mode 模拟点击和运行画面检查，Windows Player 构建／启动／存档验证和发布。
- 在 Unity 中验证规则、正常操作、动画／音效、保存恢复和失败回滚；相关参考测试的旧通过结果不计入 Unity 验收。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

总览、分支连线、详情、升级、普通转职及晋升使用原面板。升级消耗原徽章且不按人物等级额外截断；晋升核对前置职业等级、总职业等级和导师区域公开状态。历次职业等级属性持续累计。

进入职业详情或升级时按已拥有技能的阶位领取原徽章，只领取未领差额；重开与冷读不重复发放。职业成就使用组10固定档和循环奖励。

切换职业卸下不兼容武器、清除方案中不适用技能并重置祈愿职业选择，装备和已获技能不删除。男女预览使用原默认服装、六类武器、prefab挂点和完整父变换；晋升原时间线驱动换装、动作、粒子及音频。

自由转职从地图十处原导师剧情入口打开，按原节点顺序继续剧情。选择窗口使用原三职业立绘、多边形点击区和选中动画；比较窗口展示职业、武器、属性与费用，三套方案支持页签、战技／秘法列表、未获得标记和技能详情。

生成方案、放弃、最终确认、通知与奖励、八小时内撤回、反向技能预览、提前结束撤回期、期限届满提示均接入本地事务。生成与放弃确认使用原 prefab 的倒计时按钮锁定；方案、选择、免费间隔、当次费用和撤回期限在重启后恢复。技能、碎片、徽章及历次职业等级按原映射双向交换；切换后统一整理职业技能方案、武器和祈愿职业选择。

### 依据与代码定位

以下官方代码与规则用于核对接线：`ProfessionOverviewPanel`、`ProfessionTreePanel`、`ProfessionTreePanelItem`、`ProfessionPanel`、`ProfessionUpgradePanel`、`ProfessionPropRadarDrawer` 和 `Common/AchievementRawInfoParser`，均位于 `.tools/source-packages/official-code/`。职业图片输入、bundle 身份和输出哈希见[职业立绘来源清单](../../assets/reference/profession-ui/portraits-source-manifest.json)；Unity 面板包由 `scripts/prepare_unity_character_panels.py` 生成。迁移组件的方法范围、官方代码和 C# 输出哈希见[运行时代码来源](../../assets/reference/profession-ui/runtime-source-manifest.json)；属性行当前只迁移总览和雷达使用的字符串显示路径，完整彩色预览和祝福交互仍待对应调用方迁移。动态加载凭据与 Sprite／纹理处理见[动态资源来源](../../assets/reference/profession-ui/dynamic-resources-source-manifest.json)，转换入口为 `prepare_unity_profession_dynamic_sources.py`、`prepare_unity_profession_reward_sprites.py`；隔离导入由 `prepare_unity_profession_reward_probe.py --project <隔离 Unity 工程>` 准备。原生组件来源与转换分别由 `prepare_unity_profession_native_inputs.py`、`prepare_unity_profession_native_export.py` 恢复，凭据见[原生输入](../../assets/reference/profession-ui/native-input-source-manifest.json)及[原生导出](../../assets/reference/profession-ui/native-export-source-manifest.json)。成就独立测试入口为 `python -B scripts/test_profession_achievements.py`；相关界面仍未通过 Play Mode 和 Player 运行验证。


