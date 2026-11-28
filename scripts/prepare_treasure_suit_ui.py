"""Extract the original detail-window suit controllers for reuse by the complete treasure windows."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from official_source import ROOT, OfficialSource
from prepare_pet_score_chest_preview import original_method

DEST = ROOT/'unity/Assets/Game/Runtime/Official/TreasureDetailUI'
RECEIPT = ROOT/'assets/reference/treasure-suit-ui-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare(check=False):
    source = OfficialSource()
    code = source.prepared_path/'official-code'
    consumed = []

    def read(name, common=False):
        path = code/('common-source/Common' if common else 'client-source')/(name+'.cs')
        consumed.append(path)
        return path.read_text('utf-8-sig')

    header = '''using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using PropType = System.Int32;
using Object = UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {
'''

    def adapt(text):
        text = re.sub(r'^using [^\n]+\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_.*\n', '', text, flags=re.M)
        text = text.replace('internal class ', 'public class ')
        text = re.sub(r'\bPlayerPropItem\b', 'ProfessionPlayerPropItem', text)
        text = text.replace('MultiLanguageText', 'WorldMultiLanguageText').replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('LanguageUtils.', 'TreasureSuitLanguage.')
        return text

    member = adapt(read('TreasureDetailSuitItem'))
    prop = adapt(read('TreasureSuitPropItem'))
    prop = prop.replace('Parsers.Instance.LevelPropParser.GetProps(propId, 0)', 'Tables.GetProps(propId, 0)')
    prop = prop.replace('PropItem.Init(propType, value, -1L)', 'PropItem.Init((ProfessionPropertyType)propType, value, -1L)')
    prop = prop.replace('public class TreasureSuitPropItem : MonoBehaviour\n{', '''public class TreasureSuitPropItem : MonoBehaviour
{
    private ITreasurePropertyTables Tables;
    public void Configure(ITreasurePropertyTables tables) => Tables=tables??throw new ArgumentNullException(nameof(tables));
''')
    original = read('TreasureDetailDialog')
    methods = '\n'.join(original_method(original, signature) for signature in (
        'private void SetSuitInfo(', 'private void DoRefreshSuitTreasureInfo(', 'private void DoRefreshSuitProps('))
    methods = adapt(methods).replace('TreasureInfo.ClassId', 'TreasureClassId')
    methods = re.sub(r'^\s*IServiceProvider sceneContainer = .*?;\n', '', methods, flags=re.M)
    methods = methods.replace('ServiceProviderExtensions.Instantiate<Transform>(sceneContainer, mySuitItemPrefab, mySuitItemNameRoot, false)',
                              'Services.InstantiateSuitMember(mySuitItemPrefab, mySuitItemNameRoot)')
    # Original popup selection and flags, with the actual window owner supplied by the enclosing panel.
    popup = '''
    private void PopupSuitDetailPanel(int treasureClassId)
    {
        bool flag=Services.HasItem(treasureClassId,1L);
        if(!mCanOperation | flag)
        {
            Services.PopupTreasure(treasureClassId,mCanOperation);
            return;
        }
        int pieceIdByTreasureId=Services.GetPieceIdByTreasureId(treasureClassId);
        Services.PopupPiece(pieceIdByTreasureId,true);
    }
'''
    # Fail if the original window branching changed before using the local window port.
    original_popup = original_method(original, 'private void PopupSuitDetailPanel(')
    for token in ('if (!mCanOperation | flag)', 'panel.SetCanOperation(mCanOperation)', 'panel.ShowBtns()', 'Formula.GetPieceIdByTreasureId(treasureClassId)'):
        if token not in original_popup:
            raise ValueError('Review original suit-member popup behavior')
    fields = adapt(original[original.index('\tpublic GameObject mySuitRoot;'):original.index('\t[Header("操作")]')])
    content = '''
    public interface ITreasureSuitViewServices
    {
        bool HasItem(int itemId,long count);
        int GetPieceIdByTreasureId(int treasureClassId);
        Transform InstantiateSuitMember(Transform prefab,Transform parent);
        // The window owner initializes first, then applies these original operation flags.
        void PopupTreasure(int treasureClassId,bool canOperation);
        void PopupPiece(int pieceId,bool showButtons);
    }
    public sealed class TreasureSuitContent : MonoBehaviour
    {
        private ITreasureSuitViewServices Services;
        private int TreasureClassId;
        private bool mCanOperation;
        public void SetCanOperation(bool value)=>mCanOperation=value;
        public void Configure(ITreasureSuitViewServices services) => Services=services??throw new ArgumentNullException(nameof(services));
        public void Show(int treasureClassId,bool canOperation,TreasureSuitInfo suit,Dictionary<int,int> activatedStars)
        {
            TreasureClassId=treasureClassId;mCanOperation=canOperation;
            SetSuitInfo(suit,activatedStars);
        }
''' + fields + methods + popup + '\n    }\n'
    lang = read('LanguageUtils', True)
    language = 'public static class TreasureSuitLanguage {\n'+'\n'.join(original_method(lang, s) for s in (
        'public static string GetItemNameKey(', 'public static string GetEnumKey('))+'\n}\n'
    helper = original_method(read('TreasureSuitHelper', True), 'public static void GetSuitStartInfo(')
    helper = helper.replace('this TreasureSuitInfo info, Player player', 'TreasureSuitInfo info, ITreasurePropertyPlayer player')
    helper = helper.replace('PlayerItemDataWrap itemById = player.GetItemById(treasure)',
                            'Zjcs.Official.TreasureCultivation.TreasureState itemById = player.GetTreasureState(treasure)')
    helper = helper.replace('PlayerItemParamTreasureWrap param = itemById.GetParam<ItemParamTreasure, PlayerItemParamTreasureWrap>()',
                            'Zjcs.Official.TreasureCultivation.TreasureState param = itemById')
    queries = 'public static class TreasureSuitQueries {\n'+helper+'\n}\n'
    outputs = {DEST/(name+'.cs'): ('// Selected original suit UI; see its source receipt.\n'+header+text+'\n}\n').encode()
               for name, text in (('TreasureDetailSuitItem', member), ('TreasureSuitPropItem', prop),
                                  ('TreasureSuitContent', content), ('TreasureSuitLanguage', language), ('TreasureSuitQueries', queries))}
    outputs[DEST/'Zjcs.Official.TreasureDetailUI.asmdef'] = (json.dumps(dict(name='Zjcs.Official.TreasureDetailUI', references=[
        'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI',
        'Zjcs.Official.TreasureCultivation', 'Zjcs.Official.TreasureUI', 'Zjcs.Official.TreasureIconUI',
        'Zjcs.Official.ItemDetailUI', 'Zjcs.Official.HomeCurrencyWindowsUI', 'Zjcs.Official.HomePlayerInfoUI',
        'Unity.ugui']), indent=2)+'\n').encode()
    for path in list(outputs):
        token=hashlib.md5(('zjcs:treasure-suit-ui:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\n'+
            ('AssemblyDefinitionImporter' if path.suffix=='.asmdef' else 'MonoImporter')+':\n  externalObjects: {}\n').encode()
    outputs[Path(str(DEST)+'.meta')]=('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:treasure-suit-ui:folder').hexdigest()+
        '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    inputs=source.verify_code_files(consumed)
    for path in (Path(__file__), ROOT/'scripts/prepare_pet_score_chest_preview.py',
                 ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionUseItemTransformExtensions.cs',
                 ROOT/'assets/reference/treasure-properties-source-manifest.json'):
        inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
    record=dict(manifest=source.version,inputs=inputs,methods=['TreasureDetailDialog.SetSuitInfo','TreasureDetailDialog.DoRefreshSuitTreasureInfo',
        'TreasureDetailDialog.DoRefreshSuitProps','TreasureDetailDialog.PopupSuitDetailPanel','TreasureSuitHelper.GetSuitStartInfo'],
        adaptations=['Extract the original suit region and methods for reuse by complete detail windows.',
                     'Supply existing property tables, player reads and native window/clone owners explicitly.'],
        outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()})
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
    for path,raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes()!=raw:raise ValueError('Original suit source differs: '+str(path))
        else:
            if path!=RECEIPT and path.exists() and path.read_bytes()!=raw and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve another suit edit: '+str(path))
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
    print('Prepared original treasure suit UI and query',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');prepare(parser.parse_args().check)
