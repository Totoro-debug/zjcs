"""Bind the complete original treasure star prefab and its native animation/resource closure."""
import json
import uuid
from pathlib import Path

from home_native_assets import install_staged_assets
from home_native_prefab import (bind_prefab, load_controller, namespace_loaded, register_script,
    restore_original_empty_values, restore_root_animation_curve_paths)
from official_source import ROOT
from prepare_current_partner_native import PartnerNativeRecovery
from prepare_partner_card_native import PartnerCardBinding, sha
from prepare_treasure_ui_controls import prepare as prepare_controls
from prepare_treasure_ui_native import BUNDLE, prepare as prepare_cache

DEST = ROOT/'unity/Assets/Game/Resources/Treasure/NativeStars'
RECEIPT = ROOT/'assets/reference/treasure-stars-native-source-manifest.json'
SCOPE = 'zjcs:treasure-native-stars'


class TreasureStarBinding(PartnerCardBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT/'unity/Assets/Game/Runtime/Official'
        register_script(self, 'StarMiniInfo', runtime/'ProfessionUI/StarMiniInfo.cs')
        register_script(self, 'TreasureStarInfo', runtime/'TreasureUI/TreasureStarInfo.cs')

    def load(self, source_guid):
        if source_guid in self.redirects: return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        if obj is not None and obj.type.name == 'AnimatorController':
            result = load_controller(self, source_guid, obj)
        else:
            result = super().load(source_guid)
            if obj is not None:
                for _, token, documents in self.queue:
                    if token == result['guid'] and isinstance(documents, dict):
                        for document in documents.values():
                            fields, = document.values()
                            restored=restore_original_empty_values(self.recovery.fields(obj), fields)
                            if obj.type.name == 'AnimationClip':
                                restored += restore_root_animation_curve_paths(self, obj, fields)
                            if restored:
                                self.proof[next(key for key,row in self.proof.items() if row.get('guid') == token)]['restored_original_empty_values']=restored
        return namespace_loaded(self, result, SCOPE)


def prepare():
    prepare_controls(True)
    cache = prepare_cache()
    work = ROOT/'output/validation'/('treasure-stars-native-'+uuid.uuid4().hex)
    work.mkdir(parents=True)
    (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1, repository=str(ROOT)))+'\n','utf8')
    print('Treasure star native binding:',work,flush=True)
    recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=BUNDLE)
    try:
        binding = TreasureStarBinding(work, recovery=recovery)
        outputs, staged, pointer, selection = bind_prefab(binding, BUNDLE,
            'Prefabs/UI/Treasure/TreasureStarInfo.prefab', 'TreasureStarInfo', SCOPE)
        prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
        installed = install_staged_assets(work, staged, outputs, DEST, 'treasure-stars-native', previous_outputs=prior.get('outputs'))
        for name in ('prepare_treasure_stars_native.py','prepare_treasure_ui_native.py',
            'home_native_prefab.py','home_native_assets.py','partner_gift_animator_binding.py','prepare_partner_card_native.py'):
            path = ROOT/'scripts'/name
            binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=recovery.apk.version, inputs=binding.inputs, selection=selection,
            recovery=recovery.proof, native_scripts=binding.script_sources, resources=binding.proof,
            prefab=pointer, outputs=installed,
            scope='Original TreasureStarInfo hierarchy, serialized state selectors, native quality sprites and star twinkle Animator graph. Full treasure panels remain separate.')
        text=json.dumps(receipt,ensure_ascii=False,indent=2)+'\n'
        (work/'bound-source-manifest.json').write_text(text,'utf8',newline='\n')
        RECEIPT.write_text(text,'utf8',newline='\n')
        print('Installed original treasure star closure:',len(installed),'files',flush=True)
    finally: recovery.apk.close()


if __name__=='__main__': prepare()
