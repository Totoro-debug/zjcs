"""Read the unchanged original parent layout used to host the detached native slot subtree during validation."""
import argparse,json,hashlib
from pathlib import Path
from official_source import ROOT
from prepare_current_partner_native import documents
from prepare_partner_card_native import script_id

def prepare(work):
    work=work.resolve()
    if not work.is_relative_to(ROOT/'output/validation'):raise ValueError('Use owned temporary validation outputs')
    proof=json.loads((ROOT/'assets/reference/treasure-slots-native-source-manifest.json').read_bytes())['recovery']['prefabs'][0]
    source=ROOT/proof['nativeSource'];raw=source.read_bytes()
    if hashlib.sha256(raw).hexdigest()!=proof['native_sha256']:raise ValueError('Original parent layout changed')
    docs=documents(raw.decode('utf8'));parent_id=proof['subtree']['original_parent']['fileID'];transform=docs[parent_id]['RectTransform']
    parent=docs[transform['m_GameObject']['fileID']]['GameObject']
    layout,=[docs[c['component']['fileID']]['MonoBehaviour'] for c in parent['m_Component']
        if docs[c['component']['fileID']].get('MonoBehaviour',{}).get('m_Script',{}).get('fileID')==script_id('UnityEngine.UI','VerticalLayoutGroup')]
    (work/'layout.json').write_text(json.dumps(dict(source=proof['nativeSource'],sha256=proof['native_sha256'],rect=transform,layout=layout),indent=2)+'\n','utf8')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--work',type=Path,required=True);prepare(p.parse_args().work)
