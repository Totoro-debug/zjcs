"""Migrate selected native quick-buy/pay-change controllers with explicit local services."""
import json
from pathlib import Path
import re

from prepare_home_auto_increase_runtime import ROOT, CODE, metadata, sha, method

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeCurrencyWindowsUI'


def build():
    inputs, outputs = {}, {}

    def read(name, folder='client-source'):
        path = CODE/folder/(name+'.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def emit(name, text):
        path = DEST/(name+'.cs')
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text('// Selected original currency-window implementation; typed local service boundaries.\n'
            'using Object=UnityEngine.Object;\n'
            'using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'
            'using StateSwitcher=Zjcs.Official.WorldUI.WorldStateSwitcher<bool>;\n'
            'using PlayerItemDataWrap=Zjcs.Official.ProfessionUI.IProfessionItemData;\n'
            'using AwardSourceData=System.Object;\n'
            'using System;\nusing System.Collections;\nusing System.Collections.Generic;\n'
            'using UnityEngine;\nusing UnityEngine.Events;\nusing UnityEngine.UI;\n'
            'using Cysharp.Threading.Tasks;\nusing Zjcs.Official.ProfessionUI;\n'
            'using Zjcs.Official.HomeCountControlsUI;\n'
            'using Zjcs.Official.World.NativeLabelUI.Framework;\n'
            'namespace Zjcs.Official.HomeCurrencyWindowsUI\n{\n'+text+'\n}\n',
            'utf8', newline='\n')
        for item in (path, metadata(path)):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item)

    def replace(text, old, new):
        assert old in text, old
        return text.replace(old, new)

    def replace_method(text, signature, replacement):
        original = method(text, signature)
        return replace(text, original, '\t'+replacement+'\n')

    def clean(text):
        text = re.sub(r'^using [^\n]+;\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
        text = re.sub(r'^\[UIWindow\([^\n]*\n', '', text, flags=re.M)
        text = re.sub(r'\s*\[Inject\]\s*private [^\n]+ \w+ \{ get; set; \}', '', text)
        text = text.replace('StateSwitcher<bool>', 'StateSwitcher')
        text = text.replace('\t[Rename("数量为0时隐藏货币信息")]\n', '')
        text = text.replace('MeManager.MainPlayer', 'Services.Player')
        text = text.replace('Parsers.GameSettings.Pay', 'Services.Pay')
        text = text.replace('Parsers.GameSettings.FlareStarId', 'Services.FlareStarId')
        text = text.replace('Parsers.GameSettings.DiamonId', 'Services.DiamonId')
        text = text.replace('Parsers.GameSettings.MoneyId', 'Services.MoneyId')
        text = text.replace('Parsers.Instance.ItemInfoParser.Get(', 'Services.GetItemInfo(')
        text = text.replace('Player mainPlayer', 'IHomeCurrencyWindowPlayer mainPlayer')
        text = text.replace('ItemInfo itemInfo', 'HomeCurrencyWindowItemInfo itemInfo')
        text = text.replace('ItemInfo itemInfo2', 'HomeCurrencyWindowItemInfo itemInfo2')
        text = text.replace('GlobalColors.Value.', 'Services.')
        text = text.replace('R3.DisposableBag', 'HomeCurrencyDisposableBag').replace('private DisposableBag ', 'private HomeCurrencyDisposableBag ')
        return text

    # The retained fields are the decoded configuration fields. Luban loading
    # remains at the local configuration boundary; the three computations are verbatim.
    source = read('QuickBuyInfo', 'common-source/Common')
    rules = '''public sealed class QuickBuyInfo
{
    public long MaxBuyCount;
    public int ItemId, CurrencyId, SubRankScaleId, LevelScaleId;
    public long BaseCount;
    public EQuickBuyRefreshType RefreshType;
    public List<long> PriceList, PriceBuyCount;
    public Func<int,int,double> GetNumberDouble;
'''
    for signature in ('public bool GetPrice(long alreadyBuyCount, long willBuyCount, out long price)',
        'public long GetMaxBuyCount(long alreadyBuy, long currencyCount, bool isLimitByCost = true)',
        'public long GetAwardItemCount(int level, SubRank subRank)'):
        body = method(source, signature).replace('SubRank subRank', 'int subRank')
        body = body.replace('Parsers.Instance.LevelNumberParser.GetNumberDouble(', 'GetNumberDouble(')
        rules += body+'\n'
    emit('QuickBuyInfo', rules+'}\n')
    emit('EQuickBuyRefreshType', read('EQuickBuyRefreshType', 'common-source/Common').replace('namespace Common;\n',''))

    text = clean(read('PlayerCostMoneyContent'))
    text = text.replace('public sealed class PlayerCostMoneyContent : UIBase\n{',
        'public sealed class PlayerCostMoneyContent : UIBase\n{\n    public IHomeCurrencyWindowServices Services { private get; set; }')
    text = replace(text, 'MoneyUtils.ShowLackOfItem(CostItemId, lackCount, this);', 'Services.ShowLackOfItem(CostItemId, lackCount, this);')
    text = replace(text, 'BroadcastManager.ShowErrText(1659, null, false, itemInfo.Name);', 'Services.ShowError(1659, itemInfo.Name);')
    text = replace_method(text, 'private void OnImageBtnClick()', '''private void OnImageBtnClick()
    {
        Services.PopupItemDetail(this, CostItemId, 0L);
    }''')
    text = replace(text, 'Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(itemInfo.Icon), ref mDisposable).Result',
        'mDisposable.Add(Services.LoadSprite(itemInfo.Icon)).Result')
    text = replace(text, 'itemInfo.HasEffect<ItemEffectAutoIncrease>()', 'itemInfo.HasAutoIncrease')
    emit('PlayerCostMoneyContent', text)

    text = clean(read('PayMoneyChangeDialog'))
    text = replace(text, 'public class PayMoneyChangeDialog : UIBase\n{',
        'public class PayMoneyChangeDialog : UIBase\n{\n    public IHomeCurrencyWindowServices Services { private get; set; }')
    text = replace(text, 'AwardManager.Instance.RegisterHandler(AwardHandle);', 'Services.RegisterAwardHandler(AwardHandle);')
    text = replace(text, 'if (AwardManager.Instance != null)', 'if (Services.AwardServiceAvailable)')
    text = replace(text, 'AwardManager.Instance.RemoveHandler(AwardHandle);', 'Services.RemoveAwardHandler(AwardHandle);')
    text = replace_method(text, 'private void OnConfirmBtnClick()', '''private void OnConfirmBtnClick()
    {
        long itemAmount = Services.Player.GetItemAmount(Services.FlareStarId);
        if (itemAmount <= 0 || mCurrentCount > itemAmount)
        {
            Services.PopupConfirm(this, "UI_Pay_Change_Item_Not_Enough_Confirm", () =>
            {
                Services.PopupDirectPay(this, () => Services.Close(gameObject));
            });
        }
        else
        {
            Services.PlayerDiamonChange(mCurrentCount, () => CloseWindow());
        }
    }''')
    text = replace(text, 'MoneyUtils.ClosePayMoneyChangeDialog();', 'Services.ClosePayMoneyChangeDialog();')
    text = replace_method(text, 'private bool AwardHandle(PlayerAwardMsg msg)', '''private bool AwardHandle(HomeCurrencyAwardMessage msg)
    {
        if (!Services.IsPayAward(msg)) return false;
        Services.PopupAward(msg);
        return true;
    }''')
    text = replace(text, 'Pay pay = Services.Pay;', 'HomeCurrencyPaySettings pay = Services.Pay;')
    emit('PayMoneyChangeDialog', text)

    text = clean(read('QuickBuyDialog', 'client-source-resolved'))
    proof = CODE/'client-source-resolved/QuickBuyDialog.decompilation.json'
    inputs[proof.relative_to(ROOT).as_posix()] = sha(proof)
    decompiled = json.loads(proof.read_bytes())
    assert decompiled['sha256'] == sha(CODE/'client-source-resolved/QuickBuyDialog.cs')
    for path,digest in decompiled['inputs'].items():
        assert sha(ROOT/path) == digest, path
        inputs[path] = digest
    text = replace(text, 'public class QuickBuyDialog : UIBase\n{',
        'public class QuickBuyDialog : UIBase\n{\n    public IHomeCurrencyWindowServices Services { private get; set; }')
    text = replace(text, 'Parsers.Instance.QuickBuyInfoParser.Get(itemId)', 'Services.GetQuickBuyInfo(itemId)')
    text = replace(text, 'itemInfo.GetEffect<ItemEffectAutoIncrease>() != null', 'itemInfo.HasAutoIncrease')
    text = replace(text, 'Parsers.Instance.ItemCountLimitInfoParser.Get(mItemId).MaxCount', 'Services.GetItemCountLimit(mItemId)')
    text = replace(text, 'EnterNewDaySubscriber.Subscribe(OnEnterNewDay).AddTo(ref mDisposableBag);',
        'mDisposableBag.Add(Services.SubscribeEnterNewDay(OnEnterNewDay));')
    text = replace(text, 'ItemAutoIncreaseSubscriber.Subscribe(OnAutoIncreaseItemUpdate).AddTo(ref mDisposableBag);',
        'mDisposableBag.Add(Services.SubscribeAutoIncrease(OnAutoIncreaseItemUpdate));')
    text = text.replace('LanguageUtils.GetItemNameKey(', 'Services.GetItemNameKey(')
    text = replace(text, 'ItemIconManager.GetIcon(itemInfo, mIconSlot)', 'Services.GetIcon(itemInfo, mIconSlot)')
    text = replace(text, 'mIcon != null && ItemIconManager != null', 'mIcon != null && Services.IconServiceAvailable')
    text = replace(text, 'ItemIconManager.RecycleIcon(mIcon);', 'Services.RecycleIcon(mIcon);')
    text = replace(text, 'ResourceLoader.LoadSprite(itemInfo.Icon).AddTo(ref mDisposableBag).Result',
        'mDisposableBag.Add(Services.LoadSprite(itemInfo.Icon)).Result')
    text = replace(text, 'LanguageUtils.QuickBuyLimitedCountTextKey(mInfo.RefreshType)', 'Services.QuickBuyLimitedCountTextKey(mInfo.RefreshType)')
    text = replace_method(text, 'private async UniTask OnConfirmBuy()', '''private async UniTask OnConfirmBuy()
    {
        if (mCurrentValue == 0L)
        {
            EBuyState state = GetState(0L);
            NotifyForBuyLimited(state);
            return;
        }
        IHomeCurrencyWindowPlayer mainPlayer = Services.Player;
        long awardItemCount = mInfo.GetAwardItemCount(mainPlayer.Level, mainPlayer.SubRank);
        if (await Services.CheckBuyLimitCountItem(mItemId, awardItemCount))
        {
            Services.PlayerQuickBuy(mItemId, mCurrentValue);
            Services.Close(gameObject);
        }
    }''')
    text = replace(text, 'MoneyUtils.ShowLackOfItem(mInfo.CurrencyId, (long)price - currenct);',
        'Services.ShowLackOfItem(mInfo.CurrencyId, (long)price - currenct, null);')
    text = text.replace('ItemAutoIncreaseEventData data', 'HomeCurrencyAutoIncreaseEvent data')
    text = text.replace('IEnterNewDay _', 'HomeCurrencyEnterNewDay _')
    text = text.replace('WindowManager.Instance.Close(gameObject);', 'Services.Close(gameObject);')
    text = text.replace('BroadcastManager.Instance.SystemMsg(', 'Services.SystemMessage(')
    start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler ')
    end = text.index('\n\t\t}', start)
    text = text[:start]+'\t\t\tDebug.LogError("QuickBuyDialog " + currencyId + " unknown item types");'+text[end:]
    emit('QuickBuyDialog', text)

    clamp = method(read('MathUtils','common-source/Common'), 'public static long Clamp(long val, long min, long max)')
    emit('MathUtils', 'internal static class MathUtils\n{\n'+clamp+'}\n')

    # This R3 bag port is already backed by the selected official implementation.
    bag_path = ROOT/'unity/Assets/Game/Runtime/Official/HomePlayerPanelUI/HomePanelDisposableBag.cs'
    inputs[bag_path.relative_to(ROOT).as_posix()] = sha(bag_path)
    bag = bag_path.read_text('utf8')
    bag = bag[bag.index('public struct HomePanelDisposableBag'):].rsplit('\n}',1)[0]
    bag = bag.replace('HomePanelDisposableBag', 'HomeCurrencyDisposableBag')
    # Return the original operation for immediate Result consumption.
    bag = bag.replace('public void Add(IDisposable item)', 'public T Add<T>(T item) where T : IDisposable')
    bag = bag.replace('item.Dispose();\n            return;', 'item.Dispose();\n            return item;')
    bag = bag.replace('items[count++] = item;', 'items[count++] = item;\n        return item;')
    emit('HomeCurrencyDisposableBag', bag)

    emit('IHomeCurrencyWindowServices', '''public sealed class HomeCurrencyPaySettings
{
    public long DiamonChangeScale, DiamonChangeMin, DiamonChangeMax;
}
public sealed class HomeCurrencyWindowItemInfo
{
    public int Id;
    public string Icon, Name;
    public bool HasAutoIncrease;
}
public sealed class HomeCurrencyAutoIncreaseEvent { public int ItemId; }
public sealed class HomeCurrencyEnterNewDay { }
public sealed class HomeCurrencyAwardMessage { public object Value; }
public interface IHomeCurrencyWindowPlayer
{
    int Level { get; }
    int SubRank { get; }
    long GetItemAmount(int itemId);
    bool HasItem(int itemId, long count);
    bool IsBanCostItem(int itemId);
    long QuickBuyGetItemBuyCount(int itemId, EQuickBuyRefreshType refreshType);
    event Action<IProfessionItemData,long,object> OnItemCountUpdate;
}
public interface IHomeCurrencyWindowServices
{
    IHomeCurrencyWindowPlayer Player { get; }
    int MoneyId { get; }
    int DiamonId { get; }
    int FlareStarId { get; }
    HomeCurrencyPaySettings Pay { get; }
    Color White { get; }
    Color LightRed { get; }
    HomeCurrencyWindowItemInfo GetItemInfo(int itemId);
    QuickBuyInfo GetQuickBuyInfo(int itemId);
    long GetItemCountLimit(int itemId);
    string GetItemNameKey(int itemId);
    string QuickBuyLimitedCountTextKey(EQuickBuyRefreshType type);
    IProfessionAssetOperation<Sprite> LoadSprite(string path);
    bool IconServiceAvailable { get; }
    ItemIcon GetIcon(HomeCurrencyWindowItemInfo item, Transform slot);
    void RecycleIcon(ItemIcon icon);
    IDisposable SubscribeEnterNewDay(Action<HomeCurrencyEnterNewDay> action);
    IDisposable SubscribeAutoIncrease(Action<HomeCurrencyAutoIncreaseEvent> action);
    UniTask<bool> CheckBuyLimitCountItem(int itemId, long awardItemCount);
    void PlayerQuickBuy(int itemId, long count);
    void PlayerDiamonChange(long count, Action success);
    void ShowLackOfItem(int itemId, long count, IWindowOperator owner);
    void ShowError(int error, string itemName);
    void SystemMessage(string text);
    void PopupItemDetail(IWindowOperator owner, int itemId, long count);
    void PopupConfirm(IWindowOperator owner, string key, Action confirm);
    void PopupDirectPay(IWindowOperator owner, Action initialized);
    void Close(GameObject window);
    void ClosePayMoneyChangeDialog();
    bool AwardServiceAvailable { get; }
    void RegisterAwardHandler(Func<HomeCurrencyAwardMessage,bool> handler);
    void RemoveAwardHandler(Func<HomeCurrencyAwardMessage,bool> handler);
    bool IsPayAward(HomeCurrencyAwardMessage message);
    void PopupAward(HomeCurrencyAwardMessage message);
}''')
    path = DEST/'Zjcs.Official.HomeCurrencyWindowsUI.asmdef'
    path.write_text(json.dumps(dict(name='Zjcs.Official.HomeCurrencyWindowsUI', references=[
        'Unity.ugui','UniTask','Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI',
        'Zjcs.Official.World.NativeLabelUI','Zjcs.Official.HomeCountControlsUI']),indent=2)+'\n', 'utf8', newline='\n')
    for item in (path,metadata(path),metadata(DEST,True)):
        outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    for name in ('PlayerCostMoneyContent','PayMoneyChangeDialog','QuickBuyDialog'):
        text=(DEST/(name+'.cs')).read_text('utf8')
        assert not any(t in text for t in ('Parsers.', 'MeManager.', 'WindowManager.', 'MoneyUtils.', 'ServiceProxyBase', '[Inject]', 'BroadcastManager.')),name
    receipt=dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        scope='Original native quick-buy/pay-change controllers, cost button and three shared pricing/award computations. Native windows and explicit production services must be bound before use.')
    (ROOT/'assets/reference/home-currency-windows-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original currency windows:',len(outputs),'outputs',flush=True)


if __name__=='__main__': build()
