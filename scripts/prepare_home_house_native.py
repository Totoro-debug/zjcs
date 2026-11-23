"""Bind the complete original default house and its original model dependencies."""
import hashlib
import json
from pathlib import Path
import uuid

from home_native_assets import install_staged_assets
from home_native_prefab import bind_prefab, load_controller, load_mesh, load_text, namespace_loaded, register_script
from official_apk import ROOT
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery
from prepare_home_storage_native import StorageBinding
from prepare_partner_card_native import PartnerCardBinding, sha


DEST = ROOT / 'unity/Assets/Official/Home/House/Native'
RECEIPT = ROOT / 'assets/reference/home-house-native-source-manifest.json'
BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_theme_house.bundle'
PREFAB = 'Prefabs/Home/Dynamic/Theme/House/House.prefab'
SCOPE = 'zjcs:home-house-native'


class HouseBinding(StorageBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {
            'HomeSlotEntityData': runtime / 'HomeEntityRuntime/HomeSlotEntityData.cs',
            'HomeLightController': runtime / 'HomeModelEnvironment/HomeLightController.cs',
            'Home.EnvDrive.HomeTimeDriveClock': runtime / 'HomeModelEnvironment/HomeTimeDriveClock.cs',
            'HomeMutexLightLayer': runtime / 'HomeRelations/HomeMutexLightLayer.cs',
            'UnityEngine.Rendering.Universal.Light2D': ROOT / 'unity/Packages/com.unity.render-pipelines.universal/Runtime/2D/Light2D.cs',
            'Spine.Unity.SkeletonAnimation': ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonAnimation.cs',
            'Spine.Unity.SkeletonDataAsset': ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Asset Types/SkeletonDataAsset.cs',
            'Spine.Unity.SpineAtlasAsset': ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Asset Types/SpineAtlasAsset.cs',
        }
        for name, path in paths.items():
            register_script(self, name, path)

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        if obj is not None and obj.type.name == 'TextAsset':
            result = load_text(self, source_guid, obj)
        elif obj is not None and obj.type.name == 'AnimatorController':
            result = load_controller(self, source_guid, obj)
        elif obj is not None and obj.type.name == 'Mesh':
            result = load_mesh(self, source_guid, obj)
        else:
            if obj is not None and obj.type.name == 'MonoBehaviour':
                script = obj.read(check_read=False).m_Script.deref().read()
                if script.m_Namespace == 'Spine.Unity' and script.m_ClassName == 'SkeletonDataAsset':
                    if self.recovery.fields(obj)['zSlots']:
                        raise ValueError('Migrate nonempty original custom Spine Z slots before binding this model')
            result = PartnerCardBinding.load(self, source_guid)
        return namespace_loaded(self, result, SCOPE)


def build():
    source = OfficialSource(); cache = ROOT / '.tools/source-packages/current' / source.version / 'home-house-native'
    work = ROOT / 'output/validation' / ('home-house-native-' + uuid.uuid4().hex); work.mkdir(parents=True)
    (work / '.zjcs-validation.json').write_text(json.dumps(dict(format=1, repository=str(ROOT))), 'utf8')
    recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=BUNDLE)
    try:
        binding = HouseBinding(work, recovery=recovery)
        outputs, staged, prefab, selection = bind_prefab(binding, BUNDLE, PREFAB, 'House', SCOPE)
        previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
        installed = install_staged_assets(work, staged, outputs, DEST, 'native-default-house', previous_outputs=previous)
        for name in ('prepare_home_house_native.py', 'home_native_prefab.py', 'prepare_home_storage_native.py', 'partner_gift_animator_binding.py',
            'native_spine_assets.py', 'home_native_assets.py'):
            path = ROOT / 'scripts' / name; binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=source.version, inputs=binding.inputs, selection=selection, recovery=recovery.proof,
            native_scripts=binding.script_sources, resources=binding.proof, prefab=prefab, outputs=installed,
            validation_inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (
                ROOT / 'scripts/fixtures/home_model_environment_validation.cs', ROOT / 'scripts/test_home_model_environment.ps1') if p.is_file()},
            scope='Complete original default-house hierarchy and original model resource closure. Local owner injection, original actor graphs and full main-scene/Player verification remain separate.')
        stage = work / 'bound-source-manifest.json'; stage.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n'); stage.replace(RECEIPT)
        print('Bound complete original default house:', len(installed), 'files; work', work)
    finally:
        recovery.apk.reader.close()


if __name__ == '__main__':
    build()
