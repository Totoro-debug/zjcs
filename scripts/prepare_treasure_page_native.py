"""Bind the complete original promotion page selector subtree and its original dependencies."""
import argparse
import hashlib
import json
import os
import uuid
from pathlib import Path
from home_native_assets import install_staged_assets, write_staged_assets
from home_native_prefab import bind_prefab, register_script
from official_source import ROOT, OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity
from prepare_partner_card_native import script_id
from prepare_treasure_icon_native import TreasureIconBinding, component
from prepare_treasure_page_controls import prepare as prepare_controls, NAMES, sha
from prepare_treasure_ui_native import prepare as prepare_cache, BUNDLE

DEST=ROOT/'unity/Assets/Game/Resources/Treasure/NativePage'
RECEIPT=ROOT/'assets/reference/treasure-page-native-source-manifest.json'
SCOPE='zjcs:treasure-native-page'

class PageBinding(TreasureIconBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in NAMES:register_script(self,name,ROOT/'unity/Assets/Game/Runtime/Official/TreasureSlotsUI'/(name+'.cs'))
        for name in ('CountRedDotImage','ButtonSound'):
            register_script(self,name,ROOT/'unity/Assets/Game/Runtime/Official/WorldUI'/('World'+name+'.cs'))

def original_selector(root):
    found=[]
    def visit(go):
        components=[p.component.deref() for p in go.read().m_Component]
        if any(o.type.name=='MonoBehaviour' and o.read(check_read=False).m_Script.read().m_ClassName=='TreasurePageSelectItem' for o in components):found.append(go)
        for child in components[0].read().m_Children:visit(child.read().m_GameObject.deref())
    visit(root);result,=found;return result

def prepare(verified_work=None):
    prepare_controls(True)
    if verified_work is None:cache=prepare_cache()
    else:
        verified_work=verified_work.resolve()
        if not verified_work.is_relative_to(ROOT/'output/validation') or not (verified_work/'.zjcs-validation.json').is_file():
            raise ValueError('Reuse only an owned verified native recovery')
        proof=json.loads((verified_work/'source-manifest.json').read_bytes())['component_cache']
        cache=OfficialSource().prepared_path/'home-treasure-ui-native';receipt=cache/'native-export-source-manifest.json'
        if (ROOT/proof['path']).resolve()!=receipt.resolve() or sha(receipt.read_bytes())!=proof['sha256']:
            raise ValueError('Previously verified original native export differs')
    work=ROOT/'output/validation'/('treasure-page-native-'+uuid.uuid4().hex);work.mkdir(parents=True)
    (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8')
    print('Original treasure promotion pages:',work,flush=True)
    recovery=PartnerNativeRecovery(native_cache=cache,export_name='native-export',bundle=BUNDLE)
    try:
        binding=PageBinding(work,recovery=recovery,guid_prefix=SCOPE+':')
        relative='Prefabs/UI/Treasure/TreasurePromotePanel.prefab'
        panel,=[p.deref() for name,p in recovery.apk.container(BUNDLE) if name.casefold()==('Assets/'+relative).casefold()]
        root=original_selector(panel);native=documents((binding.native/relative).read_text('utf8'))
        original,=[v['MonoBehaviour'] for v in native.values() if v.get('MonoBehaviour',{}).get('m_Script',{}).get('fileID')==script_id('','TreasurePageSelectItem')]
        native_id=original['m_GameObject']['fileID']
        rect=native[native[native_id]['GameObject']['m_Component'][0]['component']['fileID']]['RectTransform']
        parents=[]
        while rect['m_Father']['fileID']:
            rect=native[rect['m_Father']['fileID']]['RectTransform'];parents.append(rect)
        _,staged,prefab,selection=bind_prefab(binding,BUNDLE,relative,'TreasurePageSelectItem',SCOPE,
            source_root=root,native_root_id=native_id,prepare_component=component,external_asset_types=('Sprite','AnimationClip','GameObject'))
        outputs=write_staged_assets(binding.queue,staged)
        prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
        installed=install_staged_assets(work,staged,outputs,DEST,'treasure-page-native',previous_outputs=prior.get('outputs'))
        for name in ('prepare_treasure_page_native.py','prepare_treasure_page_controls.py','prepare_treasure_icon_native.py',
                     'home_native_prefab.py','home_native_furniture_builder.py'):
            path=ROOT/'scripts'/name;binding.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        record=dict(manifest=recovery.apk.version,inputs=binding.inputs,selection=selection,recovery=recovery.proof,
            native_scripts=binding.script_sources,resources=binding.proof,prefab=prefab,audio=binding.audio_proof,
            original_parent_rects=parents,
            element_order=[recovery.fields(o)['mPageOrderElementMenus'] for o in
                [p.component.deref() for p in root.read().m_Component] if o.type.name=='MonoBehaviour' and
                o.read(check_read=False).m_Script.read().m_ClassName=='TreasurePageSelectItem'][0],
            identical_dependencies=binding.identical_dependencies,outputs=installed)
        raw=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
        replacement=work/'receipt-replacement.json';replacement.write_bytes(raw);os.replace(replacement,RECEIPT)
        (work/'bound-source-manifest.json').write_bytes(raw)
        print('Bound complete original treasure promotion pages:',len(installed),flush=True)
    finally:recovery.apk.close()

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--verified-work',type=Path)
    prepare(parser.parse_args().verified_work)
