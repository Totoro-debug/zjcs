"""Migrate the complete original equipped-treasure page selector and selectable cell."""
import argparse
import hashlib
import json
import os
import re
import uuid
from pathlib import Path
from official_source import ROOT, OfficialSource

DEST = ROOT/'unity/Assets/Game/Runtime/Official/TreasureSlotsUI'
RECEIPT = ROOT/'assets/reference/treasure-page-controls-source-manifest.json'
NAMES = ('TreasurePageSelectItem', 'ItemIconTreasureSelectableItem')

def sha(raw): return hashlib.sha256(raw).hexdigest()

def adapt(name, text):
    text = re.sub(r'^using Common;\s*$', '', text, flags=re.M)
    text = re.sub(r'^\s*//IL_.*$', '', text, flags=re.M)
    text = text.replace('StateSwitcher<', 'WorldStateSwitcher<').replace('PlayerItemDataWrap', 'IProfessionItemData')
    text = text.replace('MeManager.MainPlayer.GetItemById(', 'Services.Icons.GetItemById(')
    text = text.replace('MeManager.MainPlayer.GetTreasureElementSlotData(', 'Services.GetTreasureElementSlotData(')
    text = text.replace('itemData.Info.ElementType', '(ElementType)Services.Icons.GetElement(itemData.Info.ClassId)')
    at = text.index('\n{', text.index('public class'))+2
    text = text[:at]+'''
    private ITreasurePageServices services;
    private ITreasurePageServices Services => services ?? throw new InvalidOperationException("Bind original treasure page services before activation.");
    public void Configure(ITreasurePageServices value) => services=value??throw new ArgumentNullException(nameof(value));
'''+text[at:]
    if 'MeManager' in text: raise ValueError('Unbound original page dependency: '+name)
    return ('// Complete original equipped-treasure page controller; explicit local services.\n'
        'using Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.TreasureUI;\n'
        'using Zjcs.Official.TreasureSlots;\nnamespace Zjcs.Official.TreasureSlotsUI {\n'+
        '\n'.join(line.rstrip() for line in text.split('\n'))+'\n}\n').encode()

def prepare(check=False):
    source=OfficialSource(); code=source.prepared_path/'official-code'
    proof=source.prepared_path/'code-manifest.json'; inputs=[Path(__file__),proof]; outputs={}
    for name in ('Assembly-CSharp.dll','Console.dll'):
        path=code/'managed/decoded'/name
        if sha(path.read_bytes())!=json.loads(proof.read_bytes())['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original code assembly differs: '+name)
        inputs.append(path)
    for name in NAMES:
        path=code/'client-source'/(name+'.cs');inputs.append(path)
        outputs[DEST/(name+'.cs')]=adapt(name,path.read_text('utf-8-sig'))
    inputs.extend(code/'client-source'/(name+'.cs') for name in ('CountRedDotBase','CountRedDotImage'))
    inputs.append(code/'client-source/TreasurePromotePanel.cs')
    inputs.extend((code/'common-source/Common/LanguageUtils.cs',DEST.parent/'WorldUI/WorldCountRedDotImage.cs',
                   DEST.parent/'WorldUI/WorldCountRedDotImage.cs.meta'))
    for path in list(outputs):
        token=hashlib.md5(('zjcs:treasure-page:'+path.name).encode()).hexdigest()
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\nMonoImporter:\n  externalObjects: {}\n').encode()
    for relative in ('Official/TreasureSlotsUI/ITreasurePageServices.cs','Offline/TreasureUI/OfflineTreasurePageServices.cs',
                     'Offline/TreasureUI/OfflineTreasurePageView.cs'):
        path=ROOT/'unity/Assets/Game/Runtime'/relative;inputs.extend((path,Path(str(path)+'.meta')))
    record=dict(manifest=source.version,inputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in inputs},
        outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        adaptations=['Preserve original element menu order, equipped-slot iteration order, empty-page skipping, selection callback order, icon setup and enable/disable cleanup; the promotion owner enables the hierarchy before initialization.',
                     'Bind original shared slot wrappers and item/icon services to the existing saved local player; page selection never writes progression.'])
    outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    work=None
    for path,raw in outputs.items():
        if path.exists() and path.read_bytes()==raw:continue
        if check:raise ValueError('Original page output differs: '+str(path))
        if path!=RECEIPT and path.exists() and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):
            raise ValueError('Preserve another page edit: '+str(path))
        if work is None:
            work=ROOT/'output/validation'/('treasure-page-write-'+uuid.uuid4().hex);work.mkdir(parents=True)
            (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8')
        replacement=work/'replacement';replacement.write_bytes(raw);path.parent.mkdir(parents=True,exist_ok=True);os.replace(replacement,path)
    print('Verified complete original treasure page controllers',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');prepare(parser.parse_args().check)
