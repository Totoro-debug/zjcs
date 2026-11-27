"""Extract the unchanged original TreasurePanel plan widget subtree with its complete native dependencies."""
import json
import re
import uuid
from pathlib import Path

from home_native_assets import install_staged_assets
from home_native_furniture_builder import HomeNativeFurnitureBinding
from home_native_prefab import bind_prefab, register_script
from npc_travel_language import language
from official_tables import Constructors
from official_source import ROOT
from prepare_current_partner_native import PartnerNativeRecovery,documents
from prepare_partner_card_native import sha,script_id
from prepare_battle_plan_widget import prepare as prepare_controls
from prepare_treasure_ui_native import prepare as prepare_cache,BUNDLE

DEST=ROOT/'unity/Assets/Game/Resources/Treasure/NativePlanWidget'
RECEIPT=ROOT/'assets/reference/battle-plan-widget-native-source-manifest.json'

class PlanWidgetBinding(HomeNativeFurnitureBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime=ROOT/'unity/Assets/Game/Runtime/Official'
        register_script(self,'PlanSwitchWidget',runtime/'BattlePlanUI/PlanSwitchWidget.cs')
        register_script(self,'ContentModerationClickMask',runtime/'WorldUI/WorldContentModerationClickMask.cs')

def original_widget(root):
    found=[]
    def visit(go):
        components=[p.component.deref() for p in go.read().m_Component]
        if any(obj.type.name=='MonoBehaviour' and obj.read(check_read=False).m_Script.read().m_ClassName=='PlanSwitchWidget' for obj in components):found.append(go)
        for child in components[0].read().m_Children:visit(child.read().m_GameObject.deref())
    visit(root)
    result,=found
    return result

def component(binding,script,fields):
    if script['type']=='ButtonSound':
        owner=binding.original(script['original'])
        fields['ClickEvent']=binding.sound(binding.recovery.fields(owner)['ClickEvent'],owner)

def prepare():
    prepare_controls(True);cache=prepare_cache()
    work=ROOT/'output/validation'/('battle-plan-widget-native-'+uuid.uuid4().hex);work.mkdir(parents=True)
    (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8')
    print('Original plan widget binding:',work,flush=True)
    recovery=PartnerNativeRecovery(native_cache=cache,export_name='native-export',bundle=BUNDLE)
    try:
        binding=PlanWidgetBinding(work,recovery=recovery,guid_prefix='zjcs:battle-plan-widget:')
        relative='Prefabs/UI/Treasure/TreasurePanel.prefab'
        panel_root,=[ref.deref() for path,ref in recovery.apk.container(BUNDLE) if path.casefold()==('Assets/'+relative).casefold()]
        root=original_widget(panel_root);native=documents((binding.native/relative).read_text('utf8'))
        native_id,=[value['MonoBehaviour']['m_GameObject']['fileID'] for value in native.values()
            if 'MonoBehaviour' in value and value['MonoBehaviour'].get('m_Script',{}).get('fileID')==script_id('','PlanSwitchWidget')]
        outputs,staged,pointer,selection=bind_prefab(binding,BUNDLE,relative,'PlanSwitchWidget','zjcs:battle-plan-widget',
            source_root=root,native_root_id=native_id,prepare_component=component)
        # Preserve the selected source's settings and all three plan-name families for this shared widget.
        constructors=Constructors(recovery.apk.source.prepared_path/'official-code')
        obj=recovery.apk.asset('defaultpackage_assets_config_luban.bundle','/game_settings.g.bytes')
        raw=obj.read().m_Script.encode('utf8','surrogateescape');settings=constructors.table(raw,'TbGameSettings')
        keys={f'{kind}_plan_default_name_{i}' for kind in ('skill','equip','treasure') for i in range(1,33)}
        keys.update(('ChangeSkillPlanNameSuc','ui_battle_plan_contentmoderation_confirm','ui_battle_plan_clear_confirm_tip',
            'ui_battle_plan_clear_success_tip','ui_treasure_plan_switch_tip','{0}'))
        texts,language_proof=language(keys,recovery.apk.source)
        payload=dict(NameCharacterDefaultMaxCount=settings['NameCharacterDefaultMaxCount'],
            NameCharacterMaxCountDict=[dict(Region=int(key),Count=value) for key,value in settings['NameCharacterMaxCountDict'].items()],
            SkillPlanMaxCount=settings['SkillPlanMaxCount'],EquipPlanMaxCount=settings['EquipPlanMaxCount'],TreasurePlanMaxCount=settings['TreasurePlanMaxCount'],
            Texts=[dict(Key=key,Value=value) for key,value in sorted(texts.items())])
        config=staged/'battle-plan-ui.json';config.write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        token=__import__('hashlib').md5(b'zjcs:battle-plan-widget:configuration').hexdigest()
        meta=Path(str(config)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+token+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        outputs[config.name]=sha(config.read_bytes());outputs[meta.name]=sha(meta.read_bytes())
        prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
        installed=install_staged_assets(work,staged,outputs,DEST,'battle-plan-widget-native',previous_outputs=prior.get('outputs'))
        for name in ('prepare_battle_plan_widget_native.py','home_native_prefab.py','home_native_furniture_builder.py','npc_travel_language.py'):
            path=ROOT/'scripts'/name;binding.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        result=dict(manifest=recovery.apk.version,inputs=binding.inputs,selection=selection,recovery=recovery.proof,
            native_scripts=binding.script_sources,resources=binding.proof,audio=binding.audio_proof,prefab=pointer,outputs=installed,
            configuration=dict(source=recovery.apk.provenance(obj),decoded_sha256=sha(raw)),language=language_proof,
            scope='Complete original TreasurePanel PlanSwitchWidget subtree, original parent detachment recorded; original buttons, input, mask, selection bar, localization, font, sprites and click audio. Enclosing panel remains separate.')
        text=json.dumps(result,ensure_ascii=False,indent=2)+'\n';(work/'bound-source-manifest.json').write_text(text,'utf8',newline='\n');RECEIPT.write_text(text,'utf8',newline='\n')
        print('Installed original plan widget closure:',len(installed),'files',flush=True)
    finally:recovery.apk.close()

if __name__=='__main__':prepare()
