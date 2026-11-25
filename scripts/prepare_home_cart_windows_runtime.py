"""Port 903_156948 Cart UI, level-up, reward-preview and AwardPanel controllers."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import shutil

from prepare_home_auto_increase_runtime import ROOT, sha, metadata

CODE = ROOT / '.tools/source-packages/current/903_156948/official-code'
TEMPLATES = ROOT / 'scripts/templates/home_cart_windows'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeCartWindowsUI'


def build(source_root=None):
    source_root = Path(source_root or ROOT).resolve()
    code = source_root / CODE.relative_to(ROOT)
    inputs, outputs = {}, {}

    def read(name, folder='client-source'):
        path = code / folder / (name + '.cs')
        inputs[path.relative_to(source_root).as_posix()] = sha(path)
        if folder == 'client-source-resolved':
            receipt = path.with_suffix('.decompilation.json')
            proof = json.loads(receipt.read_bytes())
            if proof['sha256'] != sha(path):
                raise ValueError(f'Resolved sha mismatch for {name}')
            for relative, digest in proof['inputs'].items():
                target = (source_root if relative.startswith('.tools/') else ROOT) / relative
                if sha(target) != digest:
                    raise ValueError(f'Decompilation input mismatch: {relative}')
                inputs[relative] = digest
            inputs[receipt.relative_to(source_root).as_posix()] = sha(receipt)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def replace(text, old, new):
        if old not in text:
            raise AssertionError(f'Missing expected snippet:\n{old}')
        return text.replace(old, new)

    def strip_il(text):
        text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        text = text.replace('(Object)(object)', '(Object)')
        text = text.replace('(RectTransform)(object)', '(RectTransform)')
        text = re.sub(r'\(\(Component\)([^)]+)\)', r'\1', text)
        text = re.sub(r'\(\(UnityEvent\)([^)]+\.onClick)\)\.AddListener\(\(UnityAction\)([^)]+)\);',
                      r'\1.AddListener(\2);', text)
        text = re.sub(r'\(\(UnityEvent\)([^)]+\.onClick)\)\.RemoveListener\(\(UnityAction\)([^)]+)\);',
                      r'\1.RemoveListener(\2);', text)
        text = text.replace('((MonoBehaviour)this).', 'this.')
        return text

    def clean(text, add_services=True):
        text = strip_il(text)
        text = re.sub(r'^using [^\n]+;\n', '', text, flags=re.M)
        text = re.sub(r'^\[UIWindow\([^\n]*\n', '', text, flags=re.M)
        text = re.sub(r'\s*\[Inject\]\s*(protected|private|public) [^\n]+ \w+ \{ get; set; \}', '', text)
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('MeManager.MainPlayer', 'Services.Player')
        text = text.replace('(mainPlayer)', '(Services.Player)')
        text = text.replace('mainPlayer.', 'Services.Player.')
        text = text.replace('Parsers.Instance.HangUpInfoParser.Get(', 'Services.GetHangUpInfo(')
        text = text.replace('Parsers.Instance.FurnitureInfoParser.Get(', 'Services.GetFurnitureInfo(')
        text = text.replace('LanguageManager.Instance.GetTextFormated', 'Services.TextFormatted')
        text = text.replace('LanguageManager.Instance.GetText', 'Services.Text')
        text = text.replace('LanguageUtils.GetItemNameKey(', 'Services.GetItemNameKey(')
        text = text.replace('LanguageUtils.GetItemDescKey(', 'Services.GetItemDescKey(')
        text = text.replace('LanguageUtils.GetHangUpName(', 'Services.GetHangUpName(')
        text = text.replace('LanguageUtils.GetCartHangUpIconPath(', 'Services.GetCartHangUpIconPath(')
        text = text.replace('LanguageUtils.GetCartItemBgPath(', 'Services.GetCartItemBgPath(')
        text = text.replace('BroadcastManager.Instance.SystemMsg(', 'Services.SystemMessage(')
        text = text.replace('BroadcastManager.Instance.BroadcastErrCode(', 'Services.BroadcastErrCode(')
        text = text.replace('WindowManager.Instance.Close(', 'Services.Close(')
        text = re.sub(r'\bDisposableBag\b', 'HomeCartWindowResourceBag', text)
        if add_services:
            text = re.sub(
                r'(public (?:abstract )?class \w+ : [^\n]+\n\{)',
                r'\1\n    public IHomeCartWindowServices Services { protected get; set; }',
                text,
                count=1,
            )
        return text

    def emit(name, text, extra_header=''):
        DEST.mkdir(parents=True, exist_ok=True)
        path = DEST / (name + '.cs')
        header = (
            '// Selected 903_156948 client controllers; explicit player/configuration/presentation ports.\n'
            'using System;\n'
            'using System.Collections;\n'
            'using System.Collections.Generic;\n'
            'using UnityEngine;\n'
            'using UnityEngine.UI;\n'
            'using Cysharp.Threading.Tasks;\n'
            'using Zjcs.Official.HomeUI;\n'
            'using Zjcs.Official.HomeFurniture;\n'
            'using Zjcs.Official.HomeTipDots;\n'
            'using Zjcs.Official.HomeHangUpWindowsUI;\n'
            'using Zjcs.Official.HomeAwardUI;\n'
            'using Zjcs.Official.HomeSceneUiControls;\n'
            'using Zjcs.Official.ProfessionUI;\n'
            'using Zjcs.Official.BattleResultUI;\n'
            'using Zjcs.Official.WorldUI;\n'
            'using Zjcs.Official.World.NativeLabelUI;\n'
            'using Zjcs.Official.World.NativeLabelUI.Framework;\n'
            'using Zjcs.Official.World.Map13MiniMap;\n'
            'using Object = UnityEngine.Object;\n'
            'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'
            'using HangUpInfo = Zjcs.Official.HomeCartWindowsUI.HomeCartHangUpInfo;\n'
            'using Player = Zjcs.Official.HomeCartWindowsUI.IHomeCartWindowPlayer;\n'
            'using ItemAwardIcon = Zjcs.Official.HomeAwardUI.HomeAwardIcon;\n'
            'using ItemInfo = Zjcs.Official.HomeAwardUI.ItemInfo;\n'
            'using ItemType = Zjcs.Official.HomeAwardUI.ItemType;\n'
            'using AwardData = Zjcs.Official.HomeAwardUI.AwardData;\n'
            + extra_header
            + 'namespace Zjcs.Official.HomeCartWindowsUI\n{\n'
        )
        body = '\n'.join(line.rstrip() for line in text.strip().splitlines())
        path.write_text(header + body + '\n}\n', 'utf8', newline='\n')
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            meta.write_text(
                'fileFormatVersion: 2\nguid: '
                + hashlib.md5(('zjcs:home-cart-windows-ui:' + name).encode()).hexdigest()
                + '\n',
                'utf8',
                newline='\n',
            )
        for item in (path, meta):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item)

    # 1. FurnitureCartDialog.cs
    text = clean(read('FurnitureCartDialog'))
    text = replace(text, 'private IDisposable mDisposable;', 'private IDisposable mDisposable;')
    text = replace(
        text,
        'mDisposable = SubscriberExtensions.Subscribe<HangUpTipLevelChangeData>(Subscriber, (Action<HangUpTipLevelChangeData>)OnTipLevelChange, Array.Empty<MessageHandlerFilter<HangUpTipLevelChangeData>>());',
        'mDisposable = Services.SubscribeTips(OnTipLevelChange);',
    )
    text = replace(
        text,
        'BaseHangUpFurniture furnitureComponent = HomeEntityManager.GetFurnitureComponent<BaseHangUpFurniture>((FurnitureBindInfo)mFurniture);\n\t\tUpdateTipLevelDotSwitcher(furnitureComponent.TipLevel);',
        'UpdateTipLevelDotSwitcher(Services.GetTipLevel(mFurniture));',
    )
    text = text.replace('HangUpUtils.CanGetHangUpReward(', 'Services.CanGetHangUpReward(')
    text = replace(
        text,
        'ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerGetHangUpReward(new PlayerGetHangUpRewardRequest\n\t\t{\n\t\t\tHangUpIdList = mGetAwardHangUpIdList\n\t\t}).OnSuccess += (PlayerAwardMsg rep) =>',
        'Services.Collect(mGetAwardHangUpIdList, (IHomeHangUpAwardMessage rep) =>',
    )
    text = replace(
        text,
        'Services.Player.Award(rep.DataList, rep.SourceData);\n\t\t\t\tFormula.Instance.MergeAward(rep.DataList);\n\t\t\t\tif (mFurniture == EFurniture.Cart)\n\t\t\t\t{\n\t\t\t\t\tWindowManager.Instance.Popup((AwardPanel go) =>\n\t\t\t\t\t{\n\t\t\t\t\t\tgo.Init(rep);\n\t\t\t\t\t});\n\t\t\t\t}\n\t\t\t\telse\n\t\t\t\t{\n\t\t\t\t\tAwardListPanel.PopupAwardListPanel(rep.DataList);\n\t\t\t\t}\n\t\t\t}\n\t\t\telse\n\t\t\t{\n\t\t\t\tServices.SystemMessage(Services.Text("fast_hang_up_no_award"));\n\t\t\t}\n\t\t\tHangUpPublisher.PublishAsync(default(HangUpCollectEvent), default(CancellationToken));\n\t\t};',
        'Services.AwardCollected(rep.DataList, rep.SourceData);\n\t\t\t\tServices.MergeAward(rep.DataList);\n\t\t\t\tif (mFurniture == EFurniture.Cart)\n\t\t\t\t{\n\t\t\t\t\tServices.PopupAwardPanel(rep);\n\t\t\t\t}\n\t\t\t\telse\n\t\t\t\t{\n\t\t\t\t\tServices.PopupAwardList(rep.DataList);\n\t\t\t\t}\n\t\t\t}\n\t\t\telse\n\t\t\t{\n\t\t\t\tServices.SystemMessage(Services.Text("fast_hang_up_no_award"));\n\t\t\t}\n\t\t\tServices.PublishCollected();\n\t\t});',
    )
    text = text.replace('FurnitureInfo furnitureInfo', 'HomeWindowFurnitureInfo furnitureInfo')
    text = replace(
        text,
        'Parsers.Instance.HangUpInfoParser.RecordHangUpInfoDict.TryGetValue(mFurniture, out var value);',
        'Services.TryGetHangUpInfos(mFurniture, out var value);',
    )
    text = replace(
        text,
        'if (item.UnlockCondition.IsMet(Services.Player) || item.NoticeCondition.IsMet(Services.Player))',
        'if (Services.IsConditionMet(item.UnlockCondition) || Services.IsConditionMet(item.NoticeCondition))',
    )
    text = replace(
        text,
        'foreach (HangUpInfo item in value)',
        'if (value != null)\n\t\tforeach (HangUpInfo item in value)',
    )
    text = replace(
        text,
        'FurnitureCartHangUpItem furnitureCartHangUpItem = UIUtils.Instantiate<FurnitureCartHangUpItem>(HangUpItemPrefab, ItemSlot, EUIInstantiateFlag.AutoActive);',
        'FurnitureCartHangUpItem furnitureCartHangUpItem = Services.InstantiateCartHangUpItem(HangUpItemPrefab, ItemSlot);',
    )
    text = replace(
        text,
        'WindowManager.Instance.Popup((HangUpCartItemDetailDialog i) =>\n\t\t{\n\t\t\ti.Init(hangUpId, Input.mousePosition);\n\t\t});',
        'Services.OpenCartItemDetail(hangUpId, Input.mousePosition);',
    )
    emit('FurnitureCartDialog', text)

    # 2. FurnitureCartHangUpItem.cs
    text = clean(read('FurnitureCartHangUpItem'))
    text = text.replace('private HomeCartWindowResourceBag mDisposable;', 'private HomeCartWindowResourceBag mDisposable = new HomeCartWindowResourceBag();')
    text = text.replace('mDisposable = default;', 'mDisposable ??= new HomeCartWindowResourceBag();')
    text = text.replace('mDisposable.Dispose();', 'mDisposable?.Dispose();')
    text = replace(
        text,
        'Icon.sprite = Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(HangUpUtils.GetHangUpRewardItem(mHangUpId).Icon), ref mDisposable).Result;',
        'Icon.sprite = mDisposable.Add(Services.Resources.LoadAsset<Sprite>(Services.GetHangUpRewardItem(mHangUpId).Icon)).Result;',
    )
    text = replace(
        text,
        'Bg.sprite = Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(Services.GetCartItemBgPath(mHangUpId)), ref mDisposable).Result;',
        'Bg.sprite = mDisposable.Add(Services.Resources.LoadAsset<Sprite>(Services.GetCartItemBgPath(mHangUpId))).Result;',
    )
    text = replace(
        text,
        'Disposable.AddTo<ConditionHandlerRegistration>(MeManager.ConditionHandler.Subscribe(mHangUpInfo.UnlockCondition, OnUnlockCondition), ref mDisposable);',
        'mDisposable.Add(Services.SubscribeCondition(mHangUpInfo.UnlockCondition, OnUnlockCondition));',
    )
    text = replace(
        text,
        'if (ServerInfoManager.Instance.CheckFunctionDisabled(Services.Player, ServerFunctionType.HangUpUnlock))\n\t\t{\n\t\t\tServices.BroadcastErrCode(53852, null, false);\n\t\t}',
        'if (Services.CheckFunctionDisabled(IHomeCartWindowServices.ServerFunctionHangUpUnlock))\n\t\t{\n\t\t\tServices.BroadcastErrCode(IHomeCartWindowServices.ErrorHangUpUnlockDisabled);\n\t\t}',
    )
    text = replace(
        text,
        'if (!Services.Player.TryGetUnlockSingleHangUpData(mHangUpId, out var _) && Services.GetHangUpInfo(mHangUpId).UnlockCondition.IsMet(Services.Player))\n\t\t{\n\t\t\tServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerHangUpUnlock(new PlayerHangUpUnlockRequest\n\t\t\t{\n\t\t\t\tHangUpId = mHangUpId\n\t\t\t}).OnSuccess += (object e) =>\n\t\t\t{\n\t\t\t\tRefresh();\n\t\t\t};\n\t\t}',
        'if (!Services.Player.TryGetUnlockSingleHangUpData(mHangUpId, out var _) && Services.IsConditionMet(Services.GetHangUpInfo(mHangUpId).UnlockCondition))\n\t\t{\n\t\t\tServices.UnlockHangUp(mHangUpId, Refresh);\n\t\t}',
    )
    text = replace(
        text,
        'if (ServerInfoManager.Instance.CheckFunctionDisabled(Services.Player, ServerFunctionType.HangUpLevelUp))\n\t\t{\n\t\t\tServices.BroadcastErrCode(53853, null, false);\n\t\t\treturn;\n\t\t}\n\t\tWindowManager.Instance.Popup((HangUpLevelUpPanel t) =>\n\t\t{\n\t\t\tt.Init(mHangUpId, EFurniture.Cart);\n\t\t});',
        'if (Services.CheckFunctionDisabled(IHomeCartWindowServices.ServerFunctionHangUpLevelUp))\n\t\t{\n\t\t\tServices.BroadcastErrCode(IHomeCartWindowServices.ErrorHangUpLevelUpDisabled);\n\t\t\treturn;\n\t\t}\n\t\tServices.OpenHangUpLevelUp(mHangUpId, EFurniture.Cart);',
    )
    text = replace(
        text,
        'hangUpItemStatus = ((!mHangUpInfo.UnlockCondition.IsMet(Services.Player)) ? HangUpItemStatus.Lock : HangUpItemStatus.CanUnlock);',
        'hangUpItemStatus = ((!Services.IsConditionMet(mHangUpInfo.UnlockCondition)) ? HangUpItemStatus.Lock : HangUpItemStatus.CanUnlock);',
    )
    text = replace(
        text,
        'double hangUpReward = HangUpUtils.GetHangUpReward(hangUpId, 1.0, singleHangUpData.Level);\n\t\t\tNameTxt.SetKey("hang_up_output_hour", FurnitureOutputInfo.GetPropValue(hangUpReward));',
        'double hangUpReward = Services.GetHangUpReward(hangUpId, 1.0, singleHangUpData.Level);\n\t\t\tNameTxt.SetKey("hang_up_output_hour", FurnitureOutputInfo.GetPropValue(hangUpReward, n => CultureNumberArg.Compact(n).ToString()));',
    )
    emit('FurnitureCartHangUpItem', text)

    # 3. FurnitureCartHangUpOutputItem.cs
    text = clean(read('FurnitureCartHangUpOutputItem'))
    text = text.replace('private HomeCartWindowResourceBag mDisposableBag;', 'private HomeCartWindowResourceBag mDisposableBag = new HomeCartWindowResourceBag();')
    text = text.replace('mDisposableBag = default;', 'mDisposableBag ??= new HomeCartWindowResourceBag();')
    text = text.replace('mDisposableBag.Dispose();', 'mDisposableBag?.Dispose();')
    text = text.replace('ItemInfo hangUpRewardItem', 'HomeWindowItemInfo hangUpRewardItem')
    text = text.replace('HangUpUtils.', 'Services.')
    text = replace(
        text,
        'Icon.sprite = Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(hangUpRewardItem.Icon), ref mDisposableBag).Result;',
        'Icon.sprite = mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>(hangUpRewardItem.Icon)).Result;',
    )
    text = text.replace('TimeUtils.SecondToHour(mHangUpSeconds)', '(mHangUpSeconds / 3600.0)')
    emit('FurnitureCartHangUpOutputItem', text)

    # 4. HangUpCartItemDetailDialog.cs
    text = clean(read('HangUpCartItemDetailDialog'))
    text = text.replace('private CanvasScaler mCanvasScaler;', 'private CanvasScaler mCanvasScaler;\n    private IDisposable mPointerHook;\n    private int mOpenFrame;')
    text = replace(
        text,
        'private void OnEnable()\n\t{\n\t\tInputOperationModule.HookOnInput<OnGlobalPointerDownResult>(OnGameObjectPointerDown);\n\t}\n\n\tprivate void OnDisable()\n\t{\n\t\tInputOperationModule.UnhookOnInput<OnGlobalPointerDownResult>(OnGameObjectPointerDown);\n\t}',
        'private void OnEnable()\n\t{\n\t\tmOpenFrame = Time.frameCount;\n\t\tmPointerHook = Services?.SubscribeGlobalPointerDown(OnGameObjectPointerDown);\n\t}\n\n\tprivate void OnDisable()\n\t{\n\t\tmPointerHook?.Dispose();\n\t\tmPointerHook = null;\n\t}',
    )
    text = replace(
        text,
        'Vector2 pos = default;\n\t\tRectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)((transform is RectTransform) ? transform : null), Vector2.op_Implicit(mousePosition), mCanvas.worldCamera, ref pos);\n\t\tval.anchoredPosition = UIUtils.ClampRectPosInScreenByCorner(screenSize: UIUtils.GetRealCanvasSize(mCanvas, mCanvasScaler, out var _), rectTransform: val, pos: pos);',
        'Vector2 pos = default;\n\t\tCamera uiCam = mCanvas != null && mCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? (mCanvas.worldCamera ?? Services.GetUiCamera()) : null;\n\t\tRectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, (Vector2)mousePosition, uiCam, out pos);\n\t\tval.anchoredPosition = HomeCartRectUtils.ClampRectPosInScreenByCorner(screenSize: HomeCartRectUtils.GetRealCanvasSize(mCanvas, mCanvasScaler, out var _), rectTransform: val, pos: pos);',
    )
    text = replace(
        text,
        'private void OnGameObjectPointerDown(OnGlobalPointerDownResult result)\n\t{\n\t\tif (!UIUtils.CheckIfClickInsideUI((RectTransform)mFurnitureOutputInfo.transform, result.ScreenPos))\n\t\t{\n\t\t\tServices.Close(this.gameObject);\n\t\t}\n\t}',
        'private void OnGameObjectPointerDown(Vector2 screenPos)\n\t{\n\t\tif (Time.frameCount == mOpenFrame) return;\n\t\tCamera uiCam = mCanvas != null && mCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? (mCanvas.worldCamera ?? Services.GetUiCamera()) : null;\n\t\tif (!HomeCartRectUtils.CheckIfClickInsideUI((RectTransform)mFurnitureOutputInfo.transform, screenPos, uiCam))\n\t\t{\n\t\t\tServices.Close(this.gameObject);\n\t\t}\n\t}',
    )
    emit('HangUpCartItemDetailDialog', text)

    # 5. FurnitureCartRuleButton.cs
    text = clean(read('FurnitureCartRuleButton'))
    text = replace(
        text,
        'WindowManager.Instance.Popup((FurnitureCartRuleDialog panel) =>\n\t\t{\n\t\t\tpanel.Init();\n\t\t});',
        'Services.OpenCartRule();',
    )
    emit('FurnitureCartRuleButton', text)

    # 6. FurnitureCartRuleDialog.cs
    text = clean(read('FurnitureCartRuleDialog'))
    text = text.replace(
        'mRuleDialog.SetRule("CartRule");',
        'mRuleDialog.Rules = Services.RuleTips;\n\t\tmRuleDialog.SetRule("CartRule");',
    )
    text = text.replace(
        'private List<(int itemId, double probability, int amount)> mItemProbabilityAmount;',
        'private List<HomeCartEquipPreviewItem> mItemProbabilityAmount;',
    )
    text = replace(
        text,
        'HangUpInfo hangUpInfo = Services.GetHangUpInfo(mEquipHangUpId);\n\t\tPlayer mainPlayer = Services.Player;\n\t\tHangUpRewardInfo hangUpRewardInfo = Parsers.Instance.HangUpRewardInfoParser.Get(hangUpInfo.RewardId, mainPlayer);\n\t\tmItemProbabilityAmount = Parsers.Instance.RewardRuleInfoParser.GetRewardItemsProbabilitiesAndQuantities(hangUpRewardInfo.RewardRuleId, mainPlayer, Services.Player.Level, Services.Player.SubRank);\n\t\tfor (int num = mItemProbabilityAmount.Count - 1; num >= 0; num--)\n\t\t{\n\t\t\tif (mItemProbabilityAmount[num].probability == 0.0)\n\t\t\t{\n\t\t\t\tmItemProbabilityAmount.RemoveAt(num);\n\t\t\t}\n\t\t}',
        'mItemProbabilityAmount = new List<HomeCartEquipPreviewItem>(Services.GetEquipPreviewItems(mEquipHangUpId));\n\t\tfor (int num = mItemProbabilityAmount.Count - 1; num >= 0; num--)\n\t\t{\n\t\t\tif (mItemProbabilityAmount[num].Probability == 0.0)\n\t\t\t{\n\t\t\t\tmItemProbabilityAmount.RemoveAt(num);\n\t\t\t}\n\t\t}',
    )
    text = replace(
        text,
        'var (classId, val, num) = mItemProbabilityAmount[index];\n\t\tprobabilityText.SetKey("{0}", new LanguageUtils.StandardSignificantPercentArg(val));',
        'var preview = mItemProbabilityAmount[index];\n\t\tint classId = preview.ItemId; double val = preview.Probability; int num = preview.Amount;\n\t\tprobabilityText.SetKey("{0}", new HomeCartStandardSignificantPercentArg(val, Services.CurrentLanguage));',
    )
    emit('FurnitureCartRuleDialog', text)

    # 7. RewardPreviewDialog.cs & 8. RewardPreviewDialogItem.cs
    text = clean(read('RewardPreviewDialog'), add_services=False)
    text = re.sub(r'\s*\[Rename\([^\n]*\)\]\n', '\n', text)
    emit('RewardPreviewDialog', text)

    text = clean(read('RewardPreviewDialogItem'), add_services=False)
    emit('RewardPreviewDialogItem', text)

    # 9. HangUpLevelUpPanel.cs
    text = clean(read('HangUpLevelUpPanel', folder='client-source-resolved'))
    text = text.replace('private HomeCartWindowResourceBag mDisposableBag;', 'private HomeCartWindowResourceBag mDisposableBag = new HomeCartWindowResourceBag();')
    text = text.replace('mDisposableBag = default;', 'mDisposableBag ??= new HomeCartWindowResourceBag();')
    text = text.replace('mDisposableBag.Dispose();', 'mDisposableBag?.Dispose();')
    text = text.replace('PlayerItemDataWrap dataWrap, long count, AwardSourceData awardSourceData', 'IProfessionItemData dataWrap, long count, object awardSourceData')
    text = replace(
        text,
        'Parsers.Instance.HangUpInfoParser.RecordHangUpInfoDict.TryGetValue(furniture, out var value);\n\t\tforeach (HangUpInfo item in value)\n\t\t{\n\t\t\tif (item.CanLevelUp && Services.Player.TryGetUnlockSingleHangUpData(item.Id, out var _))\n\t\t\t{\n\t\t\t\tmHangUpInfoList.Add(item);\n\t\t\t}\n\t\t}',
        'if (Services.TryGetHangUpInfos(furniture, out var value))\n\t\tforeach (HangUpInfo item in value)\n\t\t{\n\t\t\tif (item.CanLevelUp && Services.Player.TryGetUnlockSingleHangUpData(item.Id, out var _))\n\t\t\t{\n\t\t\t\tmHangUpInfoList.Add(item);\n\t\t\t}\n\t\t}',
    )
    text = replace(
        text,
        'HangUpLevelUpSwitchItem item = UIUtils.Instantiate(mSwitchItemPrefab, mSwitchItemSlot);',
        'HangUpLevelUpSwitchItem item = Services.InstantiateLevelUpSwitchItem(mSwitchItemPrefab, mSwitchItemSlot);',
    )
    text = replace(
        text,
        '\t\tint i = 0;\n\t\tfor (int count = mSwitchItems.Count; i < count; i++)\n\t\t{\n\t\t\tHangUpLevelUpSwitchItem hangUpLevelUpSwitchItem = mSwitchItems[i];\n\t\t\tint id = mHangUpInfoList[i].Id;',
        '\t\tint i = 0;\n\t\tfor (int count = mSwitchItems.Count; i < count; i++)\n\t\t{\n\t\t\tHangUpLevelUpSwitchItem hangUpLevelUpSwitchItem = mSwitchItems[i];\n\t\t\tif (i >= mHangUpInfoList.Count) { hangUpLevelUpSwitchItem.gameObject.SetActive(false); continue; }\n\t\t\thangUpLevelUpSwitchItem.gameObject.SetActive(true);\n\t\t\tint id = mHangUpInfoList[i].Id;',
    )
    text = replace(
        text,
        'private async UniTaskVoid FocusSwitchItem()\n\t{\n\t\tawait UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);\n\t\tint num = mSwitchItems.IndexOf(mItem);\n\t\tfloat value = 1f * (float)num / (float)(mSwitchItems.Count - 1);\n\t\tmScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(value);\n\t}',
        'private async UniTaskVoid FocusSwitchItem()\n\t{\n\t\tawait UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);\n\t\tif (this == null || !gameObject.activeInHierarchy) return;\n\t\tif (mHangUpInfoList.Count <= 1 || mItem == null)\n\t\t{\n\t\t\tmScrollRect.horizontalNormalizedPosition = 0f;\n\t\t\treturn;\n\t\t}\n\t\tint num = mSwitchItems.IndexOf(mItem);\n\t\tfloat value = 1f * (float)num / (float)(mHangUpInfoList.Count - 1);\n\t\tmScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(value);\n\t}',
    )
    text = replace(
        text,
        'mIcon.sprite = ResourceLoader.LoadSprite(Services.GetCartHangUpIconPath(mHangUpId)).AddTo(ref mDisposableBag).Result;',
        'mIcon.sprite = mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>(Services.GetCartHangUpIconPath(mHangUpId))).Result;',
    )
    text = text.replace('Parsers.Instance.HangUpUpgradeMaterialInfoParser.GetMaxLevel(', 'Services.GetUpgradeMaxLevel(')
    text = text.replace('Parsers.Instance.HangUpUpgradeMaterialInfoParser.Get(', 'Services.GetUpgradeMaterialCost(')
    text = text.replace('FunctionType.HangUpCalcLevelUp', 'IHomeCartWindowServices.FunctionHangUpCalcLevelUp')
    text = text.replace('Parsers.GameSettings.HangUpCalcMaxLevelUp', 'Services.HangUpCalcMaxLevelUp')
    text = replace(
        text,
        'if (ServerInfoManager.Instance.CheckFunctionDisabled(Services.Player, ServerFunctionType.HangUpLevelUp))\n\t\t{\n\t\t\tServices.BroadcastErrCode(53853, null, false);\n\t\t}\n\t\tif (IsEligibleForLevelUp(out var _))\n\t\t{\n\t\t\tServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerHangUpLevelUp(new PlayerHangUpLevelUpRequest\n\t\t\t{\n\t\t\t\tHangUpId = mHangUpId,\n\t\t\t\tLevel = 1\n\t\t\t});\n\t\t}',
        'if (Services.CheckFunctionDisabled(IHomeCartWindowServices.ServerFunctionHangUpLevelUp))\n\t\t{\n\t\t\tServices.BroadcastErrCode(IHomeCartWindowServices.ErrorHangUpLevelUpDisabled);\n\t\t\treturn;\n\t\t}\n\t\tif (IsEligibleForLevelUp(out var _))\n\t\t{\n\t\t\tServices.LevelUpHangUp(mHangUpId, 1);\n\t\t}',
    )
    text = replace(
        text,
        'ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerHangUpLevelUp(new PlayerHangUpLevelUpRequest\n\t\t\t\t{\n\t\t\t\t\tHangUpId = mHangUpId,\n\t\t\t\t\tLevel = num\n\t\t\t\t});',
        'Services.LevelUpHangUp(mHangUpId, num);',
    )
    emit('HangUpLevelUpPanel', text)

    # 10. HangUpLevelUpSwitchItem.cs
    text = clean(read('HangUpLevelUpSwitchItem'))
    text = text.replace('private HomeCartWindowResourceBag mDisposableBag;', 'private HomeCartWindowResourceBag mDisposableBag = new HomeCartWindowResourceBag();')
    text = text.replace('mDisposableBag = default;', 'mDisposableBag ??= new HomeCartWindowResourceBag();')
    text = text.replace('mDisposableBag.Dispose();', 'mDisposableBag?.Dispose();')
    text = text.replace('ItemInfo hangUpRewardItem = HangUpUtils.GetHangUpRewardItem(mHangUpId);', 'HomeWindowItemInfo hangUpRewardItem = Services.GetHangUpRewardItem(mHangUpId);')
    text = replace(
        text,
        'Icon.sprite = Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(hangUpRewardItem.Icon), ref mDisposableBag).Result;',
        'Icon.sprite = mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>(hangUpRewardItem.Icon)).Result;',
    )
    emit('HangUpLevelUpSwitchItem', text)

    # 11. AwardPanel.cs
    text = clean(read('AwardPanel'))
    text = replace(
        text,
        'public void Init(PlayerAwardMsg msg, string titleKey = null, bool needMergeItem = true)\n\t{\n\t\tInit(msg.DataList, titleKey, needMergeItem);\n\t}',
        'public void Init(IHomeHangUpAwardMessage msg, string titleKey = null, bool needMergeItem = true)\n\t{\n\t\tvar list = new List<AwardData>();\n\t\tif (msg?.DataList != null)\n\t\t\tforeach (object entry in msg.DataList)\n\t\t\t\tif (entry is AwardData ad) list.Add(ad);\n\t\tInit(list, titleKey, needMergeItem);\n\t}',
    )
    text = replace(
        text,
        'ItemInfo itemInfo = Parsers.Instance.ItemInfoParser.Get(data.AwardId);\n\t\tif (data.Agiotage > 0 || mShowEffectItemTypes.Contains(itemInfo.ItemType))\n\t\t{\n\t\t\titemIcon.SetFrameEffect();\n\t\t}\n\t\tif (itemInfo.CanUse)\n\t\t{\n\t\t\titemIcon.ItemIcon.SetClickPopupInfoDialog(PopupItemDetailDialogCallBack);\n\t\t}',
        'ItemInfo itemInfo = Services.GetAwardItemInfo(data.AwardId);\n\t\tif (itemInfo != null && (data.Agiotage > 0 || mShowEffectItemTypes.Contains(itemInfo.ItemType)))\n\t\t{\n\t\t\titemIcon.SetFrameEffect();\n\t\t}\n\t\tif (Services.CanUseItem(data.AwardId))\n\t\t{\n\t\t\titemIcon.ItemIcon.SetClickPopupInfoDialog(PopupItemDetailDialogCallBack);\n\t\t}',
    )
    text = replace(
        text,
        'private void PopupItemDetailDialogCallBack(GameObject go)\n\t{\n\t\tgo.GetComponentInChildren<ItemDetailDialogUseContent>(true).ShowUseBtn(isShow: true);\n\t}',
        'private void PopupItemDetailDialogCallBack(GameObject go)\n\t{\n\t\tServices.EnableAwardItemDetailUseButton(go);\n\t}',
    )
    emit('AwardPanel', text)

    # 12. AwardContent.cs
    text = clean(read('AwardContent'))
    text = text.replace('private ItemInfoParser mInfoParser;\n', '')
    text = replace(
        text,
        '\t\tif (mInfoParser == null)\n\t\t{\n\t\t\tmInfoParser = Parsers.Instance.ItemInfoParser;\n\t\t}\n',
        '',
    )
    text = replace(
        text,
        'if (needMergeItem)\n\t\t{\n\t\t\tFormula.Instance.MergeAward(mAwardList);\n\t\t}\n\t\tfor (int num = mAwardList.Count - 1; num >= 0; num--)\n\t\t{\n\t\t\tAwardData awardData = mAwardList[num];\n\t\t\tif (AwardUtils.IsIgnoreShowAward((awardData.AgiotageId > 0) ? awardData.AgiotageId : awardData.AwardId))\n\t\t\t{\n\t\t\t\tmAwardList.RemoveAt(num);\n\t\t\t}\n\t\t}',
        'if (needMergeItem)\n\t\t{\n\t\t\tServices.MergeAwardData(mAwardList);\n\t\t}\n\t\tfor (int num = mAwardList.Count - 1; num >= 0; num--)\n\t\t{\n\t\t\tAwardData awardData = mAwardList[num];\n\t\t\tif (Services.IsIgnoreShowAward((awardData.AgiotageId > 0) ? awardData.AgiotageId : awardData.AwardId))\n\t\t\t{\n\t\t\t\tmAwardList.RemoveAt(num);\n\t\t\t}\n\t\t}',
    )
    text = text.replace('mItemsWaitingShowQueue.TryDequeue(ref awardItem)', 'mItemsWaitingShowQueue.TryDequeue(out awardItem)')
    emit('AwardContent', text)

    # 13. AwardPanelItemBase.cs & 14. AwardPanelItem.cs
    text = clean(read('AwardPanelItemBase'))
    emit('AwardPanelItemBase', text)

    text = clean(read('AwardPanelItem'), add_services=False)
    text = replace(
        text,
        'mAwardIcon = ItemIconManager.InstantiateAward(mAnchor, awardData, initItemIcon, autoTransition: true, isSelf);',
        'mAwardIcon = Services.AwardItemServices.InstantiateAward(mAnchor, awardData, initItemIcon, autoTransition: true, isSelf, showOutputAddition: true);',
    )
    emit('AwardPanelItem', text)

    # 15. MultiLanguageGroupStateControler.cs
    text = clean(read('MultiLanguageGroupStateControler'), add_services=False)
    text = text.replace(
        'public class MultiLanguageGroupStateControler : MultiLanguageUI\n{',
        'public class MultiLanguageGroupStateControler : MultiLanguageUI\n{\n    public LanguageGroupConfig LanguageGroupConfig { get; set; }',
    )
    text = text.replace('LanguageGroupConfig.Value.GetLanguageGroupByType(', 'LanguageGroupConfig.GetLanguageGroupByType(')
    emit('MultiLanguageGroupStateControler', text)

    # 16. LayoutElementMirrorForLoopRect.cs
    text = clean(read('LayoutElementMirrorForLoopRect', folder='client-source-resolved'), add_services=False)
    text = replace(text, 'public class LayoutElementMirrorForLoopRect : UIBehaviour, ILayoutElement', 'public class LayoutElementMirrorForLoopRect : UnityEngine.EventSystems.UIBehaviour, ILayoutElement')
    emit('LayoutElementMirrorForLoopRect', text, 'using UnityEngine.EventSystems;\n')

    # Hand-authored explicit service & catalog files copied from templates
    for name in ('HomeCartWindowServices', 'OfficialHomeCartWindowAssetCatalog'):
        src = TEMPLATES / (name + '.cs')
        dst = DEST / (name + '.cs')
        shutil.copyfile(src, dst)
        inputs[src.relative_to(ROOT).as_posix()] = sha(src)
        meta = Path(str(dst) + '.meta')
        if not meta.exists():
            meta.write_text(
                'fileFormatVersion: 2\nguid: '
                + hashlib.md5(('zjcs:home-cart-windows-ui:' + name).encode()).hexdigest()
                + '\n',
                'utf8',
                newline='\n',
            )
        for item in (dst, meta):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item)

    asmdef = DEST / 'Zjcs.Official.HomeCartWindowsUI.asmdef'
    asmdef.write_text(
        json.dumps(
            dict(
                name='Zjcs.Official.HomeCartWindowsUI',
                references=[
                    'Unity.ugui',
                    'Unity.TextMeshPro',
                    'UniTask',
                    'Zjcs.Official.HomeUI',
                    'Zjcs.Official.HomeFurniture',
                    'Zjcs.Official.HomeTipDots',
                    'Zjcs.Official.HomeHangUpWindowsUI',
                    'Zjcs.Official.HomeAwardUI',
                    'Zjcs.Official.HomeSceneUiControls',
                    'Zjcs.Official.ProfessionUI',
                    'Zjcs.Official.BattleResultUI',
                    'Zjcs.Official.WorldUI',
                    'Zjcs.Official.World.NativeLabelUI',
                    'Zjcs.Official.World.MainlineMiniMapHud',
                ],
            ),
            indent=2,
        )
        + '\n',
        'utf8',
        newline='\n',
    )
    asmdef_meta = metadata(asmdef)
    dest_meta = metadata(DEST, True)
    for item in (asmdef, asmdef_meta, dest_meta):
        outputs[item.relative_to(ROOT).as_posix()] = sha(item)

    receipt_path = ROOT / 'assets/reference/home-cart-windows-runtime-source-manifest.json'
    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    proof = dict(
        manifest=code.parent.name,
        generator_sha256=sha(Path(__file__)),
        inputs=inputs,
        outputs=outputs,
        scope='Original 903_156948 Cart window, item, detail, level-up, rule/reward-preview, and AwardPanel UI controllers.',
    )
    receipt_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print(f'Restored Cart UI runtime: {len(outputs)} outputs')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    build(parser.parse_args().source_root)
