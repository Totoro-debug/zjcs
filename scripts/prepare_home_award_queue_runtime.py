"""Port the selected original award queue and its item/path controllers."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

from official_apk import ROOT

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeAwardUI'
INPUTS, OUTPUTS = {}, {}


def read(name, common=False):
    path = CODE / ('common-source/Common' if common else 'client-source') / (name + '.cs')
    INPUTS[path.relative_to(ROOT).as_posix()] = sha(path)
    return path.read_text('utf-8-sig')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resolved_animation():
    source = CODE / 'client-source-resolved/AnimationAutoNext.cs'
    proof = source.with_suffix('.decompilation.json')
    ilspy = ROOT / '.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll'
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    references = CODE.parent / 'native-conversion/inputs/Managed'
    inputs = {p.relative_to(ROOT).as_posix(): sha(p) for p in (assembly, ilspy, *sorted(references.glob('*.dll')))}
    if not source.exists() or not proof.exists():
        command = [str(ROOT / '.tools/dotnet/dotnet.exe'), str(ilspy), '-t', 'AnimationAutoNext', '-r', str(references), str(assembly)]
        result = subprocess.run(command, check=True, capture_output=True,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        raw = result.stdout.decode('utf-8-sig').replace('\r\n', '\n')
        if 'private async UniTask PlayAnimation(' not in raw or 'IAsyncStateMachine' in raw:
            raise ValueError('Resolve selected AnimationAutoNext async dependencies.')
        source.write_text(raw, 'utf8', newline='\n')
        proof.write_text(json.dumps(dict(inputs=inputs, sha256=sha(source), command=command), indent=2) + '\n', 'utf8')
    evidence = json.loads(proof.read_bytes())
    if evidence['inputs'] != inputs or evidence['sha256'] != sha(source):
        raise ValueError('Preserve changed original animation decompilation inputs.')
    INPUTS.update(inputs)
    INPUTS[source.relative_to(ROOT).as_posix()] = sha(source)
    INPUTS[proof.relative_to(ROOT).as_posix()] = sha(proof)
    return source.read_text('utf8')


def clean(text):
    text = re.sub(r'^using [^\n]+;\n', '', text, flags=re.M)
    text = re.sub(r'^namespace Common;\s*', '', text, flags=re.M)
    text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
    text = re.sub(r'^\s*\[(?:Inject|UIWindow\([^\n]*|Key\(\d+\)|MessagePackObject\(false\))\]\n', '\n', text, flags=re.M)
    text = re.sub(r'\(\(Component\)this\)', 'this', text)
    text = text.replace('(Object)(object)', '(Object)')
    text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
    text = re.sub(r'\bDisposableBag\b', 'HomeAwardResourceBag', text)
    text = text.replace('= default;', '= new HomeAwardResourceBag();')
    return text


def diagnostics(text):
    # Replace only the compiler-generated logging handler, retaining each branch.
    pattern = r'(?P<indent>\t+)GLogger(?:Error|Warn)InterpolatedStringHandler message = .*?\n(?P=indent)Log\.(?P<level>Error|Warn)\(ref message,[^\n]*;'
    text = re.sub(pattern, lambda m: m['indent'] + 'Debug.Log' + ('Error' if m['level'] == 'Error' else 'Warning') + '("Original award UI: missing configured dependency.");', text, flags=re.S)
    return text


def emit(name, text):
    DEST.mkdir(parents=True, exist_ok=True)
    path = DEST / (name + '.cs')
    head = '''// Migrated from the selected official 884_156692 client/common source.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UIParticle = Coffee.UIExtensions.UIParticle;
namespace Zjcs.Official.HomeAwardUI
{
'''
    body = '\n'.join(line.rstrip() for line in text.strip().splitlines())
    path.write_text(head + body + '\n}\n', 'utf8', newline='\n')
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:home-award-ui:' + name).encode()).hexdigest() + '\n', 'utf8', newline='\n')
    for item in (path, meta): OUTPUTS[item.relative_to(ROOT).as_posix()] = sha(item)


def shared_code_compatibility():
    rows = []
    older = ROOT / '.tools/source-packages/official-code'
    for name in ('ItemIcon', 'ItemIconAttachment', 'ItemIconAttachmentCell', 'ItemIconAttachmentHint',
                 'ItemIconConfig', 'UIClickSound', 'UIBase', 'GameObjectPool', 'GameObjectPoolBase',
                 'GameObjectDestoryListener', 'ImageProxy', 'ImageLoaderProxy', 'MultiLanguageText', 'LayoutElementClamper'):
        selected, = CODE.rglob(name + '.cs')
        previous = older / selected.relative_to(CODE)
        def normalized(path):
            text = path.read_text('utf-8-sig')
            text = re.sub(r'^\s*//IL_[^\n]*', '', text, flags=re.M)
            return re.sub(r'"/Volumes/[^"\n]+"', '"SOURCE_PATH"', text)
        current = normalized(selected)
        if current != normalized(previous):
            raise ValueError('Review changed shared original implementation: ' + name)
        for path in (selected, previous): INPUTS[path.relative_to(ROOT).as_posix()] = sha(path)
        rows.append(dict(type=name, selected=selected.relative_to(ROOT).as_posix(),
            existing_port_source=previous.relative_to(ROOT).as_posix(),
            normalized_sha256=hashlib.sha256(current.encode()).hexdigest(),
            normalization='Only decompiler IL diagnostic comments and diagnostic /Volumes source paths; all executable source retained.'))
    return rows


def build():
    shared = shared_code_compatibility()
    folder_meta = Path(str(DEST) + '.meta')
    if not folder_meta.exists():
        folder_meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-award-ui-folder').hexdigest() + '\nfolderAsset: yes\n', 'utf8')
    for name in ('AwardListPanelBasePath', 'AwardListPanelNormalPath', 'AwardListPanelSinglePath', 'AwardListPanelItemType'):
        emit(name, clean(read(name)))
    text = diagnostics(clean(read('AwardListPanel')))
    text = text.replace('private IResourceLoader ResourceLoader { get; set; }', 'public IHomeAwardQueueServices Services { get; set; }')
    text = text.replace('private void Awake()', '''public void BindSoundHost(IProfessionSoundHost host)
    {
        foreach (var config in mAwardListPanelItemConfigDict.Values)
            if (config.AwardSound != null) config.AwardSound.InstanceHost = host;
    }

    private void Awake()''')
    text = text.replace('WindowManager.Instance.OnWindowOpened', 'Services.OnWindowOpened')
    text = text.replace('window.GetComponent<PopupWindow>().FullScreenDisplay', 'Services.IsFullScreen(window)')
    text = text.replace('Formula.Instance.MergeItemAndSort', 'HomeAwardFormula.MergeItemAndSort')
    text = text.replace('AwardUtils.IsIgnoreShowAward', 'Services.IsIgnoreShowAward')
    text = text.replace('MeManager.MainPlayer == null', '!Services.HasPlayer')
    text = text.replace('WindowManager.Instance.Close', 'Services.Close')
    text = text.replace('new GameObjectPool<AwardListPanelItem>(this.transform, awardListPanelItemPrefab)', 'Services.CreatePool(this.transform, awardListPanelItemPrefab)')
    text = text.replace('AssetOperationHandle<GameObject> assetOperationHandle = Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadAssetByGuid<GameObject>(value.AssetGuidProxy.AssetGuid), ref mPrefabDisposableBag);',
        'IHomeAwardAsset<GameObject> assetOperationHandle = mPrefabDisposableBag.Add(Services.LoadAssetByGuid(value.AssetGuidProxy.AssetGuid));')
    text = text.replace('private static PopupWindowOperation sPopupOperation;', 'private static IHomeAwardPopupOperation sPopupOperation;')
    # Static popup ownership is explicit; buffering/pending-operation ordering stays original.
    text = text.replace('public static void PopupAwardListPanel(PlayerAwardMsg msg,', 'public static void PopupAwardListPanel(IHomeAwardQueueServices services, IHomeAwardMessage msg,')
    text = text.replace('public static void PopupAwardListPanel(List<AwardData> awardList,', 'public static void PopupAwardListPanel(IHomeAwardQueueServices services, List<AwardData> awardList,')
    text = text.replace('public static void PopupAwardListPanel(IWindowOperator windowOperator,', 'public static void PopupAwardListPanel(IHomeAwardQueueServices services, IWindowOperator windowOperator,')
    text = text.replace('PopupAwardListPanel(null,', 'PopupAwardListPanel(services, null,')
    text = text.replace('PopupWindow window = WindowManager.Instance.GetWindow<AwardListPanel>();', 'GameObject window = services.GetWindow();')
    text = text.replace('((Component)window).GetComponent<AwardListPanel>()', 'window.GetComponent<AwardListPanel>()')
    text = text.replace('sPopupOperation = WindowManager.Instance.Popup(windowOperator, (AwardListPanel panel) =>', 'sPopupOperation = services.Popup(windowOperator, (AwardListPanel panel) =>')
    text = text.replace('panel.Init(sAwardListBuffer, notMergeAward);', 'panel.Services = services;\n\t\t\tpanel.Init(sAwardListBuffer, notMergeAward);')
    text = text.replace('}, instant: false, WindowLifeType.Local, null, autoAdjustByWindowContext: true);', '}, instant: false, local: true, autoAdjustByWindowContext: true);')
    emit('AwardListPanel', text)
    text = clean(read('AwardListPanelItem'))
    text = text.replace('private IItemAwardIconManager ItemIconManager { get; set; }', 'public IHomeAwardItemServices Services { get; set; }')
    text = text.replace('private IResourceLoader ResourceLoader { get; set; }', '')
    text = text.replace('private IScriptableObjectConfig<GlobalColors> GlobalColors { get; set; }', '')
    text = re.sub(r'\bItemAwardIcon\b', 'HomeAwardIcon', text)
    text = text.replace('Parsers.Instance.ItemInfoParser.Get', 'Services.GetItemInfo')
    text = text.replace('ItemIconManager.InstantiateAward', 'Services.InstantiateAward')
    text = text.replace('LanguageUtils.GetItemNameKey', 'Services.GetItemNameKey')
    text = text.replace('new CultureTextArg(', 'Services.CultureText(')
    text = text.replace('CultureNumberArg.Compact', 'Services.Compact')
    text = text.replace('new MultiTextDecorator(Services.Compact(extraAmount), new ColorWrapTextProcessor(GlobalColors.Value, Color32.op_Implicit(Color.green)))', 'Services.DecorateExtra(Services.Compact(extraAmount), Color.green)')
    text = text.replace('Parsers.GameSettings.MoneyId', 'Services.MoneyId').replace('Parsers.GameSettings.PlayerExpItemId', 'Services.PlayerExpItemId')
    text = text.replace('BattleFormula.Instance.GetProp(MeManager.MainPlayer.NonBattleProps, PropType.LingShiAdditionAwardPercent)', 'Services.GetMoneyAdditionPercent()')
    text = text.replace('BattleFormula.Instance.GetProp(MeManager.MainPlayer.NonBattleProps, PropType.ExpAdditionAwardPercent)', 'Services.GetExperienceAdditionPercent()')
    text = text.replace('Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(string.Format(ImgLightPath, itemInfo.Quality)), ref mDisposableBag).Result', 'mDisposableBag.Add(Services.LoadSprite(string.Format(ImgLightPath, itemInfo.Quality))).Result')
    emit('AwardListPanelItem', text)
    text = diagnostics(clean(read('ItemAwardIcon')))
    text = re.sub(r'\bItemAwardIcon\b', 'HomeAwardIcon', text)
    text = text.replace('private IItemIconManager ItemIconManager { get; set; }', 'public IHomeAwardIconServices Services { get; set; }')
    text = text.replace('ItemIconManager != null', 'Services != null').replace('ItemIconManager.', 'Services.')
    text = text.replace('Parsers.Instance.ItemInfoParser.Get', 'Services.GetItemInfo')
    text = text.replace('MeManager.MainPlayer.GetItem', 'Services.GetItem').replace('PlayerItemDataWrap item', 'IProfessionItemData item')
    text = text.replace('mItemInfo = item.Info;', 'mItemInfo = Services.GetItemInfo(item.Id);')
    text = text.replace('CultureNumberArg.Compact', 'Services.Compact')
    text = text.replace('ItemIconAppendAppearance', 'IHomeAwardAppearanceIcon').replace('ItemIconTreasure', 'IHomeAwardTreasureIcon')
    text = text.replace('ItemIconAttachmentOutputAddition attachment = ItemIcon.GetAttachment<ItemIconAttachmentOutputAddition>(ItemIconAttachmentType.OutputAddition);', 'IHomeAwardOutputAddition attachment = Services.GetOutputAddition(ItemIcon);')
    text = text.replace('(Object)attachment == (Object)null', 'attachment == null')
    text = text.replace('LanguageManager.Instance.GetText', 'Services.Text')
    emit('HomeAwardIcon', text)
    text = clean(read('ItemAwardIconGem'))
    text = re.sub(r'\bItemAwardIcon\b', 'HomeAwardIcon', text).replace('ItemIconGem', 'IHomeAwardGemIcon')
    text = text.replace('(Object)itemIconGem == (Object)null', 'itemIconGem == null')
    emit('ItemAwardIconGem', text)
    text = clean(read('ItemAwardIconGodGacha'))
    text = text.replace('private IItemIconManager ItemIconManager { get; set; }', 'public IHomeAwardIconServices Services { get; set; }')
    text = text.replace('ItemIconManager != null', 'Services != null').replace('ItemIconManager.', 'Services.')
    text = text.replace('Parsers.Instance.ItemInfoParser.Get', 'Services.GetItemInfo').replace('CultureNumberArg.Compact', 'Services.Compact')
    emit('ItemAwardIconGodGacha', text)
    read('AnimationAutoNext')
    text = clean(resolved_animation()).replace('CancellationDisposable', 'HomeAwardCancellation')
    emit('AnimationAutoNext', text)
    emit('ItemAwardIconManagerConfig', clean(read('ItemAwardIconManagerConfig')).replace('[ScriptableObjectConfig]', ''))
    for name in ('IMergeableItem', 'AddItemRewardType', 'ShowAttachmentType', 'ItemType'):
        emit(name, clean(read(name, True)))
    text = clean(read('AwardData', True)).replace(' : MySerializable,', ' :')
    text = text.replace('public override ushort GetOpCode()', 'public ushort GetOpCode()')
    text = text.replace('Parsers.Instance.ItemInfoParser.Get', 'Catalog.GetItemInfo').replace('Formula.Instance.GetAwardItemTypeScoreForSortAward', 'HomeAwardFormula.GetAwardItemTypeScoreForSortAward')
    text = text.replace('public ulong Index;', 'public IHomeAwardCatalog Catalog;\n\n\tpublic ulong Index;')
    # Polymorphic transport payloads remain owned by the receipt producer; these
    # controllers never read or modify them. Preserve their field identities.
    text = text.replace('Dictionary<string, ItemParam>', 'Dictionary<string, object>').replace('AwardExtraData ExtraData', 'object ExtraData')
    emit('AwardData', text)
    text = clean(read('RecycleAwardData', True)).replace(' : MySerializable', '').replace('public override ushort', 'public ushort')
    emit('RecycleAwardData', text)
    source = read('FormulaHandler', True)
    start = source.index('\tpublic void MergeItem<T>')
    end = source.index('\n\tpublic int CalcPlayerSkillSlotsCount', start)
    merging = source[start:end].replace('public void ', 'public static void ')
    start = source.index('\tpublic int GetAwardItemTypeScoreForSortAward')
    end = source.index('\n\tpublic string GetDailyDungeonAwardRandomSeedKey', start)
    score = source[start:end].replace('public int ', 'public static int ')
    extra = clean(read('IFormulaHandlerAwardExtra', True))
    extra = extra.replace('IFormulaHandlerAwardExtra', 'HomeAwardFormula').replace('this IFormulaHandlerAward formulaHandlerAward, ', '').replace('formulaHandlerAward.', '')
    extra = extra[:extra.rfind('}')] + score + '\n' + merging + '\n}'
    emit('HomeAwardFormula', extra)
    utils = read('AwardUtils')
    start = utils.index('\tpublic static bool IsAwardDataAgiotage')
    end = utils.index('\n\tpublic static void PopupAwardPanel', start)
    emit('HomeAwardUtils', 'public static class HomeAwardUtils\n{\n' + utils[start:end] + '\n}')
    for name in ('HomeAwardServices.cs', 'OfficialHomeAwardQueueAssetCatalog.cs'):
        path = DEST / name
        if path.exists(): INPUTS[path.relative_to(ROOT).as_posix()] = sha(path)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:home-award-ui:' + path.stem).encode()).hexdigest() + '\n', 'utf8')
        OUTPUTS[meta.relative_to(ROOT).as_posix()] = sha(meta)
    assembly = DEST / 'Zjcs.Official.HomeAwardUI.asmdef'
    assembly.write_text(json.dumps(dict(name='Zjcs.Official.HomeAwardUI', references=[
        'Unity.ugui', 'Unity.TextMeshPro', 'Zjcs.Official.WorldUI',
        'Zjcs.Official.ProfessionUI', 'Zjcs.Official.World.NativeLabelUI', 'Coffee.UIParticle', 'UniTask']), indent=2) + '\n', 'utf8', newline='\n')
    meta = Path(str(assembly) + '.meta')
    if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-award-ui-assembly').hexdigest() + '\n', 'utf8')
    for path in (assembly, meta, folder_meta): OUTPUTS[path.relative_to(ROOT).as_posix()] = sha(path)
    receipt = ROOT / 'assets/reference/official-client-code/source-manifest.json'
    INPUTS[receipt.relative_to(ROOT).as_posix()] = sha(receipt)
    sound_port = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/OfficialWwiseEvent.cs'
    INPUTS[sound_port.relative_to(ROOT).as_posix()] = sha(sound_port)
    for path in (CODE / 'framework-firstpass-source/PopupWindow.cs',
                 ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldPopupWindow.cs'):
        INPUTS[path.relative_to(ROOT).as_posix()] = sha(path)
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=INPUTS, outputs=OUTPUTS,
        shared_original_compatibility=shared,
        adaptations=[
            'Keep original queue, icon, split, merge and sort branches/order; replace global DI/resource/window/player access with explicit owner services.',
            'AwardData is a presentation receipt. Remove MessagePack/network base and attributes; retain all 16 original fields. Unconsumed ItemParam/AwardExtraData payloads remain opaque objects owned by the receipt producer.',
            'Recover AnimationAutoNext with all selected managed references, retaining scaled clip-length wait, delay, cancellation and next-animation order.',
            'Bind award sounds per panel instance without replacing the shared fallback sound host. Restore original serialized popup mFullScreen/mMaskType and FullScreenDisplay predicate.',
            'Create each queue pool through its owner so cloned item controllers receive their non-serialized services before initialization.'],
        scope='Selected original award queue, paths, item display and merge/sort data. Native closure, production XP receipt binding and graphical verification are recorded separately; complete bed window and general reward catalog remain separate.')
    (ROOT / 'assets/reference/home-award-queue-runtime-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    build()
