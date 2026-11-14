"""Recover selected item auto-increase rules and the current stamina configuration."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors

CODE = ROOT/'.tools/source-packages/current/884_156692/official-code'
DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeAutoIncrease'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def method(text, signature):
    start = text.index('\t'+signature)
    pos = text.index('{', start)
    depth = 1
    end = pos+1
    while depth:
        depth += (text[end] == '{')-(text[end] == '}')
        end += 1
    return text[start:end]+'\n'


def metadata(path, folder=False):
    path = Path(path)
    meta = Path(str(path)+'.meta')
    if not meta.exists():
        key = hashlib.md5(('zjcs:home-auto-increase:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer = 'folderAsset: yes\nDefaultImporter:\n' if folder else 'AssemblyDefinitionImporter:\n' if path.suffix == '.asmdef' else 'MonoImporter:\n  serializedVersion: 2\n' if path.suffix == '.cs' else 'TextScriptImporter:\n'
        meta.write_text('fileFormatVersion: 2\nguid: '+key+'\n'+importer+'  externalObjects: {}\n', 'utf8', newline='\n')
    text = '\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines())+'\n'
    if meta.read_bytes() != text.encode('utf8'):
        meta.write_text(text,'utf8',newline='\n')
    return meta


def build():
    inputs, outputs = {}, {}
    for name in ('official_apk.py','official_tables.py','official_source.py','official_bundle_reader.py'):
        p = ROOT/'scripts'/name
        inputs[p.relative_to(ROOT).as_posix()] = sha(p)

    def read(name):
        p = CODE/'common-source/Common'/f'{name}.cs'
        inputs[p.relative_to(ROOT).as_posix()] = sha(p)
        return p.read_text('utf-8-sig').replace('\r\n','\n')

    def write(path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, 'utf8', newline='\n')
        for p in (path, metadata(path)):
            outputs[p.relative_to(ROOT).as_posix()] = sha(p)

    def emit(name, text):
        write(DEST/(name+'.cs'), '// Selected original item auto-increase rules; clock/data boundaries only.\n'
            +'using System;\nusing System.Collections.Generic;\nnamespace Zjcs.Official.HomeAutoIncrease\n{\n'+text+'\n}\n')

    param = read('ItemParamAutoIncrease')
    fields = re.findall(r'public (double|DateTime|long) (\w+);', param)
    assert len(fields) == 5
    emit('ItemParamAutoIncrease', '[Serializable]\npublic sealed class ItemParamAutoIncrease\n{\n'
         +'\n'.join('    public '+kind+' '+name+';' for kind,name in fields)+'\n}\n')
    emit('SegmentedIncreaseData', read('SegmentedIncreaseData').split('namespace Common;')[1].strip())
    text = read('AutoIncreaseItemUtils').split('namespace Common;')[1].strip()
    text = text.replace('PlayerItemParamAutoIncreaseWrap', 'ItemParamAutoIncrease')
    emit('AutoIncreaseItemUtils', text)
    math = method(read('MathUtils'), 'public static long Clamp(long val, long min, long max)')
    emit('MathUtils', 'internal static class MathUtils\n{\n'+math+'}\n')
    time = method(read('TimeUtils'), 'public static double SecondToHour(double sec)')
    time += method(read('TimeUtils'), 'public static double HourToSecond(double hour)')
    emit('TimeUtils', 'internal static class TimeUtils\n{\n'+time+'}\n')
    elapsed = method(read('TimeManager'), 'public double CalcElapsedHour(DateTime startTime)')
    elapsed += method(read('TimeManager'), 'public double CalcElapsedSecond(DateTime startTime)')
    emit('HomeAutoIncreaseClock', 'public sealed class HomeAutoIncreaseClock\n{\n'
         +'    private readonly Func<DateTime> now;\n'
         +'    public HomeAutoIncreaseClock(Func<DateTime> now) { this.now = now ?? throw new ArgumentNullException(nameof(now)); }\n'
         +'    public DateTime GetDateTime() => now();\n'+elapsed+'}\n')
    wrap = read('PlayerItemDataWrap')
    names = ('public void SetCount(long count)', 'private long GetActualCountWithAutoIncrease()',
             'private void SetCountWithAutoIncrease(long value)', 'public void UpdateAutoIncreaseSpeed(double speed)',
             'public void UpdateAutoIncreaseMaxCount(long maxCount)')
    body = '\n'.join(method(wrap, name) for name in names)
    body = body.replace('PlayerItemParamAutoIncreaseWrap','ItemParamAutoIncrease').replace('this.GetItemParamAutoIncrease()', 'Parameter')
    body = body.replace('TimeManager.Instance', 'Clock')
    body = body.replace('AutoIncreasementPropProviderGetter?.Invoke()?.GetSegmentedIncreaseData', 'Segments?.Invoke')
    body = body.replace('Parsers.Instance.ItemCountLimitInfoParser.Get(Info.ClassId).MaxCount', 'ItemCountLimit')
    body = re.sub(r'GLoggerErrorInterpolatedStringHandler message =.*?Log.Error\(exception,.*?;\n',
                  'LogException?.Invoke(exception);\n',body,flags=re.S)
    assert 'GLogger' not in body and 'Parsers' not in body
    emit('HomeAutoIncreaseItem', '''public sealed class HomeAutoIncreaseItem
{
    private long _Count;
    public ItemParamAutoIncrease Parameter { get; }
    public HomeAutoIncreaseClock Clock { get; }
    public long ItemCountLimit { get; }
    public long Count => GetActualCountWithAutoIncrease();
    public long StoredCount => _Count;
    public Func<HomeAutoIncreaseItem, DateTime, DateTime, List<SegmentedIncreaseData>> Segments;
    public Action<Exception> LogException;
    public event Action<HomeAutoIncreaseItem, long, long, DateTime, double> OnAutoIncreaseCountUpdate;
    public event Action<HomeAutoIncreaseItem, double, double> OnAutoIncreaseSpeedUpdate;
    public event Action<HomeAutoIncreaseItem, long, long> OnAutoIncreaseMaxCountUpdate;
    public HomeAutoIncreaseItem(long count, ItemParamAutoIncrease parameter, long itemCountLimit, Func<DateTime> now)
    {
        _Count = count; Parameter = parameter; ItemCountLimit = itemCountLimit;
        Clock = new HomeAutoIncreaseClock(now);
    }
'''+body+'}\n')
    emit('AutoIncreaseItemPropUtils', read('AutoIncreaseItemPropUtils').split('namespace Common;')[1].strip())
    emit('Formula', 'internal static class Formula\n{\n'
         +method(read('Formula'), 'public static float Int2Float(int val)')+'}\n')
    body = method(read('AutoIncreaseItemHangUpRewardHandler'),
        'public void SettleHangUpReward(Player player, HangUpInfo hangUpInfo, double hangUpHour, double scaledNum, bool hasOutputAddition)')
    # The caller resolves the original reward/effect and binds that actual item.
    # Keep the complete numeric/state-changing body after that lookup boundary.
    body = ('public static void SettleHangUpReward(HomeAutoIncreaseItem itemById, double hangUpHour, '
        'bool hasOutputAddition, int outputAdditionPropValue, Action<double> recordRemainder)\n\t{'
        +body[body.index('\n\t\tPlayerItemParamAutoIncreaseWrap'):])
    body = body.replace('PlayerItemParamAutoIncreaseWrap', 'ItemParamAutoIncrease')
    body = body.replace('itemById?.GetItemParamAutoIncrease()', 'itemById?.Parameter')
    body = body.replace('CalcOffsetOutputAdditionHours(player, itemById.Info.ClassId, hangUpHour)',
        'AutoIncreaseItemPropUtils.CalcOffsetOutputAdditionTimeDuration(outputAdditionPropValue, hangUpHour)')
    body = body.replace('itemById.ActualCountWithoutAutoIncrease', 'itemById.StoredCount')
    body = body.replace('TimeManager.Instance', 'itemById.Clock')
    body = body.replace('player.HangUpData.AutoIncreaseItemTimeDict[itemById.Id] = num10;', 'recordRemainder(num10);')
    assert not any(token in body for token in ('player.', 'TimeManager', 'PlayerItem', 'Parsers'))
    emit('AutoIncreaseItemHangUpRewardHandler', 'public static class AutoIncreaseItemHangUpRewardHandler\n{\n'+body+'}\n')
    message = CODE/'common-source/MsgPlayerHangUpDataUpdate.cs'
    inputs[message.relative_to(ROOT).as_posix()] = sha(message)
    for name in ('PlayerItemParamAutoIncreaseWrap','ItemEffectAutoIncrease','PlayerItemManagerExt','PlayerAutoIncreateItemPropManager'):
        read(name)
    apk = Apk(reuse_current_cache=True)
    try:
        constructors = Constructors(CODE)
        mapping = {name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        tables, sources = {}, {}
        for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name != 'TextAsset': continue
            name = obj.read().m_Name.rsplit('/',1)[-1].removesuffix('.g')
            if name not in ('game_settings','item','item_count_limit'): continue
            raw = obj.read().m_Script.encode('utf8','surrogateescape')
            tables[name] = constructors.table(raw,mapping[name])
            sources[name] = dict(source=apk.provenance(obj), sha256=hashlib.sha256(raw).hexdigest(), constructor=mapping[name])
        stamina = tables['game_settings']['TiliId']
        item, = [row for row in tables['item'] if row['ClassId'] == stamina]
        effect, = [row for row in item['Effects'] if row['_type'] == 'ItemEffectAutoIncrease']
        limit, = [row for row in tables['item_count_limit'] if row['ClassId'] == stamina]
        config = dict(manifest=apk.version, itemId=stamina, increaseSpeed=effect['IncreaseSpeed'],
            maxCount=effect['MaxCount'], calcOverflow=effect['CalcOverflow'],
            increaseSpeedPercentProp=effect['IncreaseSpeedPercentProp'], itemCountLimit=limit['MaxCount'],
            outputAdditionItems=sorted(int(key) for key in tables['game_settings']['OutputAdditionSetting']['ItemOutputGroupTypeDict']))
        write(ROOT/'unity/Assets/Game/Resources/Home/item-auto-increase.json', json.dumps(config,indent=2)+'\n')
        for name in constructors.sources:
            p = CODE/'common-source/Common'/f'{name}.cs'
            if not p.is_file(): p = CODE/'common-source/Common.LubanGen'/f'{name}.cs'
            inputs[p.relative_to(ROOT).as_posix()] = sha(p)
    finally:
        apk.reader.close()
    write(DEST/'Zjcs.Official.HomeAutoIncrease.asmdef', json.dumps(dict(name='Zjcs.Official.HomeAutoIncrease',
        rootNamespace='Zjcs.Official.HomeAutoIncrease',references=[],noEngineReferences=True),indent=2)+'\n')
    outputs[metadata(DEST,True).relative_to(ROOT).as_posix()] = sha(metadata(DEST,True))
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        configuration=sources,stamina=config,
        adaptations=['Original parameter fields without transport wrapper; offline persistence encodes DateTime as ticks.',
            'Original selected PlayerItemDataWrap count/get/set/speed/max methods, original segmented helper and time conversions.',
            'Clock, item-count limit, segmented provider and diagnostic logging are explicit service boundaries.',
            'Item locks are excluded from this stack projection; home currency 1/2/57 has no offline lock records.',
            'AutoIncreaseItemHangUpRewardHandler retains its complete numeric/state body after selected reward/effect lookup; '
            'the caller supplies the bound actual item, output-addition property and deferred-remainder notification.',
            'MsgPlayerHangUpDataUpdate remainder consumption is applied in the same local profile transaction.'])
    (ROOT/'assets/reference/home-auto-increase-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print(json.dumps(config),flush=True)


if __name__ == '__main__':
    build()
