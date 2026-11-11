"""Port the original main-player currency count observer and one-second coroutine."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

from prepare_home_auto_increase_runtime import CODE, ROOT, metadata, sha

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeCurrencyUI'


def build():
    inputs, outputs = {}, {}
    def source(name):
        p = CODE/'client-source'/(name+'.cs')
        inputs[p.relative_to(ROOT).as_posix()] = sha(p)
        return p.read_text('utf-8-sig').replace('\r\n','\n')
    def emit(name, body):
        p = DEST/(name+'.cs')
        p.parent.mkdir(parents=True,exist_ok=True)
        p.write_text('// Selected original currency count observer; typed player/parser boundaries only.\n'
            +'using System;\nusing Zjcs.Official.ProfessionUI;\nnamespace Zjcs.Official.HomeCurrencyUI\n{\n'
            +body+'\n}\n','utf8',newline='\n')
        for item in (p,metadata(p)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    text = source('PlayerItemUIMainPlayerCountProviderBase')
    for line in ('using Common;\n','using Framework.Logging;\n','using Framework.Logging.InterpolatedStringHandler;\n'):
        text = text.replace(line,'')
    text = text.replace('MeManager.Instance == null','!Bindings.ManagerAvailable').replace('MeManager.Instance != null','Bindings.ManagerAvailable')
    text = text.replace('MeManager.Instance.OnPlayerUpdate','Bindings.OnPlayerUpdate').replace('MeManager.MainPlayer','Bindings.MainPlayer')
    text = re.sub(r'GLoggerErrorInterpolatedStringHandler message =.*?Log.Error\(ref message,.*?;\n',
        'UnityEngine.Debug.LogError(GetType().Name + " Init Failed, MeManager.Instance Is NULL");\n',text,flags=re.S)
    text = re.sub(r'\bPlayer\b','IHomeCurrencyPlayer',text)
    pos = text.index('{')+1
    text = text[:pos]+'''
    protected IHomeCurrencyBindings Bindings { get; }
    protected PlayerItemUIMainPlayerCountProviderBase(IHomeCurrencyBindings bindings)
    { Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }
'''+text[pos:]
    emit('PlayerItemUIMainPlayerCountProviderBase',text)
    source('PlayerItemUIPlayerItemCountProvider')
    resolved = CODE/'client-source-resolved/PlayerItemUIPlayerItemCountProvider.cs'
    proof = resolved.with_suffix('.decompilation.json')
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    ilspy = ROOT/'.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll'
    references = CODE.parent/'native-conversion/inputs/Managed'
    hashes = {p.relative_to(ROOT).as_posix():sha(p) for p in [assembly,ilspy,*sorted(references.glob('*.dll'))]}
    if not resolved.exists() or not proof.exists():
        command = [str(ROOT/'.tools/dotnet/dotnet.exe'),str(ilspy),'-t','PlayerItemUIPlayerItemCountProvider','-r',str(references),str(assembly)]
        result = subprocess.run(command,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        text = result.stdout.decode('utf-8-sig').replace('\r\n','\n')
        assert 'private async UniTask AutoRefreshCount' in text and 'IAsyncStateMachine' not in text
        resolved.parent.mkdir(parents=True,exist_ok=True)
        resolved.write_text(text,'utf8',newline='\n')
        proof.write_text(json.dumps(dict(inputs=hashes,sha256=sha(resolved),command=command),indent=2)+'\n','utf8',newline='\n')
    evidence = json.loads(proof.read_bytes())
    assert evidence['inputs'] == hashes and evidence['sha256'] == sha(resolved)
    inputs.update(hashes)
    for p in (resolved,proof): inputs[p.relative_to(ROOT).as_posix()] = sha(p)
    text = resolved.read_text('utf8').replace('using Common;\n','')
    text = re.sub(r'\bPlayer\b','IHomeCurrencyPlayer',text)
    text = text.replace('PlayerItemDataWrap','HomeCurrencyItem').replace('AwardSourceData','object')
    text = re.sub(r'\bItemInfo\b','HomeCurrencyItemInfo',text)
    text = text.replace('PlayerItemUIPlayerItemCountProvider(int itemId)',
                        'PlayerItemUIPlayerItemCountProvider(int itemId, IHomeCurrencyBindings bindings) : base(bindings)')
    text = text.replace('Parsers.GameSettings.OutputAdditionSetting.ItemOutputGroupTypeDict.ContainsKey(mItemId)', 'Bindings.IsOutputAdditionItem(mItemId)')
    text = text.replace('Parsers.Instance.ItemInfoParser.Get(mItemId)','Bindings.GetItemInfo(mItemId)')
    text = text.replace('mItemInfo.HasEffect<ItemEffectAutoIncrease>()','mItemInfo.HasAutoIncrease')
    assert 'Parsers.' not in text and 'HasEffect<' not in text
    emit('PlayerItemUIPlayerItemCountProvider',text)
    emit('IHomeCurrencyBindings','''public interface IHomeCurrencyBindings
{
    IHomeCurrencyPlayer MainPlayer { get; }
    bool ManagerAvailable { get; }
    SafeAction OnPlayerUpdate { get; }
    HomeCurrencyItemInfo GetItemInfo(int itemId);
    bool IsOutputAdditionItem(int itemId);
}
public interface IHomeCurrencyPlayer
{
    long GetItemAmount(int itemId);
    event Action<HomeCurrencyItem, long, object> OnItemCountUpdate;
    event Action<HomeCurrencyItem> OnAutoIncreaseItemDataUpdate;
}
public sealed class HomeCurrencyItem { public int Id; public long Count; }
public sealed class HomeCurrencyItemInfo { public bool HasAutoIncrease; }
''')
    p = DEST/'Zjcs.Official.HomeCurrencyUI.asmdef'
    p.write_text(json.dumps(dict(name='Zjcs.Official.HomeCurrencyUI',rootNamespace='Zjcs.Official.HomeCurrencyUI',
        references=['Zjcs.Official.ProfessionUI','UniTask']),indent=2)+'\n','utf8',newline='\n')
    for item in (p,metadata(p),metadata(DEST,True)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    receipt = dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        scope='Original player rebinding, item/auto-increase event filters, one-second refresh and cancellation lifecycle; non-home currency factories are not included.')
    (ROOT/'assets/reference/home-currency-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original currency count observers:',len(outputs),'outputs',flush=True)


if __name__ == '__main__':
    build()
