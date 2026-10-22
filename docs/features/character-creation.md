# 角色创建与正式换装

[总索引](../README.md)

状态：未完成

## 功能简介

每个新建存档首次进入选择男性／女性、装扮、名字和战士／法师；正式角色从外观入口打开换装。

Unity 已有按存档槽位保存的创角草稿存储层；角色创建界面、正式角色数据和换装流程尚未接入。

## 完整还原部分

Unity [CharacterCreationDraftStore](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationDraft.cs) 按选中槽位读写 `character-draft.json`，使用版本 4 格式，校验页面、男女两套完整外观字段、颜色格式和字段分组；读取支持现有 JSON 保存层的备份恢复，写入经格式校验并原子替换。该层不生成默认外观、不迁移旧版本草稿，也不覆盖槽位正式进度。

草稿读写还会加载 [CharacterCreationAssetCatalog](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationAssetCatalog.cs) 中由官方 `appearance-options.json` 解析出的男女发型、眉毛、眼睛、嘴型、妆容和特征 ID 集合；未知或性别不匹配的部件 ID 会使草稿校验失败。官方选项目录结构、非空列表、重复 ID 和身体归属在目录加载时校验。[CharacterCreationDraftJson](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationDraft.cs) 对草稿拒绝未知及重复字段并校验 JSON 语法。

Unity 载荷现包含原 CreatePlayerPanel 层级／演出目录、男女外观选项目录、正式换装面板字段、衣柜规则及衣柜 UI 数据；Editor 入口 [CharacterCreationAssetTools](../../unity/Assets/Game/Editor/CharacterCreation/CharacterCreationAssetTools.cs) 绑定并核对载荷哈希。以上是数据恢复，不等于原角色 prefab、Spine 骨骼／动画、UI 图片及可运行界面已经接入。

## 自定义实现

以下为已确认的单机处理要求与动画技术适配决策，尚未代表完整 Unity 流程已经接入或验收。

男女角色预览、创角演出与正式换装采用用户已确认的免费方案：将官方 Spine 4.2 角色数据独立转换为 Unity 原生动画，并补齐必要的播放、换装及染色适配。共用实现边界、旧 Spine 工具依赖处理和验证要求统一见[Spine 角色动画迁移决策](../asset-management.md#spine-角色动画迁移决策)。兼容性、实际表现及运行链尚待验证；本次未确认静态预览降级或重绘角色。

离线创角不提供随机生成名字的功能，玩家必须自行输入名字。原版随机名按钮调用服务端 `OperPlayerGetRandomName(Sex)`，没有迁移成本地名字库或生成规则；Unity 草稿不保存随机名状态。

创角草稿与正式进度在本地分别保存，包含男女独立外观；损坏草稿不覆盖正式档，保存失败整体回滚。旧格式兼容迁移不开放，版本不符或缺少必需字段时不得补造外观。无效草稿的恢复界面及失败状态提示仍未接入。

各存档分别保留自己的创角草稿；体型页返回存档选择后可恢复编辑，正式创角只写入所选档。多存档入口及切换规则见[设置、拍照与本地存档](settings-and-save.md#多存档与角色切换)。

创角页仍按已确认的职业选择流程进入游戏，不重新加入已取消的原型初始战斗。正式改名通过行囊中的原改名物品流程完成，按实际物品扣除并与本地存档事务一起提交；见[行囊、物品与宝箱](inventory.md)。

## 尚未实现

- 按[Spine 角色动画迁移决策](../asset-management.md#spine-角色动画迁移决策)恢复男女骨骼、网格、附件、原动作曲线及事件，接通 Unity 原生播放、动态换装和染色；验证所需约束、遮罩、层级及动画切换的兼容性。角色资源已经恢复或预处理不等于已接通 Unity 预览。
- 将官方创角／换装 UI 图像及音效转换为 Windows 可用载荷，绑定原 prefab 层级、布局、动画事件和按钮音效；当前已恢复的 JSON 目录还没有完整 Unity 可运行界面。
- 迁移男女外观选项、部件染色、角色预览、原动画事件、手动命名、职业选择、正式易容费用、性别冲突确认和衣柜互斥规则，接通 CreatePlayerPanel、ChangeAppearancePanel 与实际角色入口；不实现服务端随机名功能。
- 将正式外观、职业和改名物品交易并入现有槽位进度事务，接入各槽位草稿恢复、失败反馈与回滚。
- 尚未在 Unity 中验证创角规则、正常操作、动画／音效、保存恢复和失败回滚；

### 依据与代码定位

以下为官方来源、既有规则与回归参考：

、[InitialWeaponCondition.cs](../../.tools/source-packages/official-code/common-source/Common/InitialWeaponCondition.cs)、[PlayerProfessionDataEx.cs](../../.tools/source-packages/official-code/common-source/Common/PlayerProfessionDataEx.cs)、[ChooseInitialWeaponNode.cs](../../.tools/source-packages/official-code/client-source/ChooseInitialWeaponNode.cs)、[CreatePlayerPanel](../../.tools/source-packages/official-code/client-source/CreatePlayerPanel.cs)

[CreatePlayerSelectSexContent](../../.tools/source-packages/official-code/client-source/CreatePlayerSelectSexContent.cs)、[CreaterPlayerRunAnimation](../../.tools/source-packages/official-code/client-source/CreaterPlayerRunAnimation.cs)、[CreatPlayerRoleContent](../../.tools/source-packages/official-code/client-source/CreatPlayerRoleContent.cs)、[AppendAppearanceGridItem](../../.tools/source-packages/official-code/client-source/AppendAppearanceGridItem.cs)、[AppearanceUtils](../../.tools/source-packages/official-code/client-source/AppearanceSystem.Runtime/AppearanceUtils.cs)

[wardrobe source manifest](../../assets/reference/wardrobe/source-manifest.json)、[free-parts source manifest](../../assets/reference/character-genders/free-parts-source-manifest.json)

[creation performance source manifest](../../assets/reference/creation-performance/source-manifest.json)

[ItemUseEffectChangeName](../../.tools/source-packages/official-code/client-source/ItemUseEffectChangeName.cs)、[PlayerOperUtils](../../.tools/source-packages/official-code/client-source/PlayerOperUtils.cs)、[character-genders](../../assets/reference/character-genders)

[prepare_map_player_actions.py](../../scripts/prepare_map_player_actions.py)、[official_appearance_catalog.py](../../scripts/official_appearance_catalog.py)
