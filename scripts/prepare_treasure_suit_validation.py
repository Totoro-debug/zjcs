"""Read the original detail-content parent and verify the suit's consumed source closure."""
import argparse
import hashlib
import json
from pathlib import Path
from official_source import ROOT
from prepare_current_partner_native import documents
from prepare_partner_card_native import script_id


def prepare(work):
    work=work.resolve()
    if not work.is_relative_to(ROOT/'output/validation') or not (work/'.zjcs-validation.json').is_file():
        raise ValueError('Use an owned validation workspace')
    receipt=json.loads((ROOT/'assets/reference/treasure-suit-native-source-manifest.json').read_bytes())
    for key,digest in receipt['inputs'].items():
        if hashlib.sha256((ROOT/key).read_bytes()).hexdigest()!=digest:raise ValueError('Consumed original suit input differs: '+key)
    proof=receipt['recovery']['prefabs'][0]
    source=ROOT/proof['nativeSource'];raw=source.read_bytes()
    if hashlib.sha256(raw).hexdigest()!=proof['native_sha256']:raise ValueError('Original detail parent differs')
    docs=documents(raw.decode('utf8'));rect=docs[proof['subtree']['original_parent']['fileID']]['RectTransform']
    parent=docs[rect['m_GameObject']['fileID']]['GameObject']
    layouts=[docs[c['component']['fileID']]['MonoBehaviour'] for c in parent['m_Component']
        if docs[c['component']['fileID']].get('MonoBehaviour',{}).get('m_Script',{}).get('fileID') in (
            script_id('UnityEngine.UI','VerticalLayoutGroup'),script_id('UGUIJszx','VerticalLayoutGroupJSZX'))]
    layout,=layouts
    custom=layout['m_Script']['fileID']==script_id('UGUIJszx','VerticalLayoutGroupJSZX')
    fields={k:v for k,v in layout.items() if k.startswith('m_') and k not in (
        'm_GameObject','m_Script','m_ObjectHideFlags','m_CorrespondingSourceObject','m_PrefabInstance','m_PrefabAsset',
        'm_EditorHideFlags','m_Name','m_EditorClassIdentifier')}
    (work/'layout.json').write_text(json.dumps(dict(source=proof['nativeSource'],sha256=proof['native_sha256'],
        rect=rect,layout=fields,custom=custom),indent=2)+'\n','utf8')
    (work/'native.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8')
    print('Verified consumed suit closure and original detail-content parent',flush=True)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--work',type=Path,required=True);prepare(p.parse_args().work)
