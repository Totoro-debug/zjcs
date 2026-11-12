"""Migrate the original handlers selected by the HomeUI currency factory."""
import json
from pathlib import Path
from prepare_home_auto_increase_runtime import ROOT, CODE, metadata, sha

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeCurrencyActionsUI'


def build():
    inputs, outputs = {}, {}
    def read(name):
        path = CODE/'client-source'/(name+'.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        return path.read_text('utf-8-sig').replace('\r\n','\n')
    def emit(name, body):
        path = DEST/(name+'.cs'); path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text('// Original selected home currency handler; typed local parser/player/window ports.\n'
            +'using System;\nusing UnityEngine;\nusing Zjcs.Official.ProfessionUI;\nnamespace Zjcs.Official.HomeCurrencyActionsUI\n{\n'+body+'\n}\n','utf8',newline='\n')
        for item in (path, metadata(path)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    interface = read('IPlayerItemUIHandler')
    assert 'dialog.Init(ItemId, 0L);' in interface
    for name in ('PlayerDefaultItemUIHandler','PlayerItemQuickyShopHandler','PlayerDiamondItemUIHandler'):
        text = read(name).replace('using Common;\n','').replace('using System;\n','')
        text = text.replace('public '+name+'(int itemId)', 'public '+name+'(int itemId, IHomeCurrencyActionServices services)')
        pos = text.index('{')+1
        text = text[:pos]+'\n    private readonly IHomeCurrencyActionServices services;\n    public void HandleClick(Vector2 mousePosition) => services.PopupItemDetail(ItemId, 0L);\n'+text[pos:]
        constructor = 'public '+name+'(int itemId, IHomeCurrencyActionServices services)\n\t{'
        assert constructor in text
        text = text.replace(constructor, constructor+'\n        this.services = services ?? throw new ArgumentNullException(nameof(services));', 1)
        if name == 'PlayerDefaultItemUIHandler':
            text = text.replace('ItemInfo itemInfo = Parsers.Instance.ItemInfoParser.Get(ItemId);','HomeCurrencyActionItemInfo itemInfo = services.GetItemInfo(ItemId);')
            text = text.replace('itemInfo.GetEffect<ItemEffectAutoIncrease>() == null','!itemInfo.HasAutoIncrease')
            old='PlayerItemDataWrap itemById = MeManager.MainPlayer.GetItemById(itemInfo.ClassId);\n\t\tmaxLimitCount = itemById.GetItemParamAutoIncrease().MaxCount;'
            assert old in text
            text = text.replace(old,'maxLimitCount = services.GetAutoIncreaseMaxCount(itemInfo.ClassId);')
        elif name == 'PlayerItemQuickyShopHandler':
            text = text.replace('QuickBuyInfo','HomeCurrencyQuickBuyInfo').replace('Parsers.Instance.HomeCurrencyQuickBuyInfoParser.Get(itemId)','services.GetQuickBuyInfo(itemId)')
            text = text.replace('mInfo.Condition.IsMet(MeManager.MainPlayer)','services.IsMet(mInfo.Condition)')
            text = text.replace('new PlayerDefaultItemUIHandler(itemId)','new PlayerDefaultItemUIHandler(itemId, services)')
            old='WindowManager.Instance.Popup((QuickBuyDialog go) =>\n\t\t\t{\n\t\t\t\tgo.Init(mInfo.ItemId, 0L);\n\t\t\t});'
            assert old in text
            text = text.replace(old,'services.PopupQuickBuy(mInfo.ItemId, 0L);')
        else:
            text = text.replace('MeManager.MainPlayer.IsFunctionLimit(FunctionType.Pay)','services.IsPayFunctionLimited')
            text = text.replace('MoneyUtils.PopupPayMoneyChangeDialog(0L)','services.PopupPayMoneyChange(0L)')
        assert all(token not in text for token in ('Parsers.', 'MeManager.', 'WindowManager.', 'MoneyUtils.'))
        emit(name,text)
    factory = read('PlayerItemUIHandlerFactory')
    assert 'case PlayerItemType.Diamon:\n\t\t\treturn new PlayerDiamondItemUIHandler(playerItemUIItemIdProvider.ItemId);' in factory
    assert 'case PlayerItemType.Money:' in factory and 'return new PlayerItemQuickyShopHandler(playerItemUIItemIdProvider.ItemId);' in factory
    emit('HomeCurrencyActionFactory', '''public static class HomeCurrencyActionFactory
{
    // HomeUI serializes exactly these three types. Other windows retain their
    // own factories and are deliberately rejected by this scoped entry point.
    public static IPlayerItemUIHandler Create(PlayerItemType type, PlayerItemUIItemIdProvider id, IHomeCurrencyActionServices services)
    {
        switch(type)
        {
            case PlayerItemType.Money: return new PlayerItemQuickyShopHandler(id.ItemId, services);
            case PlayerItemType.Diamon: return new PlayerDiamondItemUIHandler(id.ItemId, services);
            case PlayerItemType.TiliId: return new PlayerDefaultItemUIHandler(id.ItemId, services);
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}''')
    emit('IHomeCurrencyActionServices', '''public interface IHomeCurrencyPurchaseCondition { }
public sealed class HomeCurrencyQuickBuyInfo { public int ItemId; public IHomeCurrencyPurchaseCondition Condition; }
public sealed class HomeCurrencyActionItemInfo { public int ClassId; public bool HasAutoIncrease; }
public interface IHomeCurrencyActionServices
{
    HomeCurrencyQuickBuyInfo GetQuickBuyInfo(int itemId);
    bool IsMet(IHomeCurrencyPurchaseCondition condition);
    bool IsPayFunctionLimited { get; }
    HomeCurrencyActionItemInfo GetItemInfo(int itemId);
    long GetAutoIncreaseMaxCount(int itemId);
    void PopupItemDetail(int itemId, long count);
    void PopupQuickBuy(int itemId, long count);
    void PopupPayMoneyChange(long count);
}''')
    path=DEST/'Zjcs.Official.HomeCurrencyActionsUI.asmdef'
    path.write_text(json.dumps(dict(name='Zjcs.Official.HomeCurrencyActionsUI', references=['Zjcs.Official.ProfessionUI']),indent=2)+'\n','utf8',newline='\n')
    for item in (path, metadata(path), metadata(DEST,True)): outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    receipt=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        scope='Only HomeUI selected Money/Diamon/TiliId factory branches, original click/add/limit handlers. Local data and original popup windows must be supplied by the production host.')
    (ROOT/'assets/reference/home-currency-actions-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original selected home currency actions:',len(outputs),'outputs',flush=True)
    return receipt

if __name__=='__main__': build()
