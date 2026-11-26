"""Bind original ground snow-effect subtrees with their authored controller settings."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import uuid

from home_native_assets import install_staged_assets, write_staged_assets
from native_spine_assets import NativeSpineBinding
from official_apk import ROOT
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity, pointer
from prepare_partner_card_native import PartnerCardBinding, guid, script_id, sha


BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_theme_ground.bundle'
NAMES = ('GroundSnow', 'GroundHalloweenSnow', 'GroundChineseOdysseySnow')
DEST = ROOT / 'unity/Assets/Official/Home/WeatherNative'
RECEIPT = ROOT / 'assets/reference/home-snow-effect-native-source-manifest.json'


class SnowEffectBinding(PartnerCardBinding):
    native_path = NativeSpineBinding.native_path

    def register_scripts(self):
        self.script_guids = set()
        self.original_refs = {}
        path = ROOT / 'unity/Assets/Game/Runtime/Official/HomeWeather/HomeWeatherSnow.cs'
        ref = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
        self.scripts['Home.Skin.HomeWeatherSnow'] = ref
        self.class_ids[script_id('Home.Skin', 'HomeWeatherSnow')] = ref
        self.script_guids.add(ref['guid'])
        for item in (path, Path(str(path) + '.meta')):
            self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())

    def load(self, source_guid):
        result = super().load(source_guid)
        # Resolved resource pointers have already crossed the CAB/GUID boundary.
        self.script_guids.add(result['guid'])
        return result

    def build(self):
        selections = []
        for name in NAMES:
            relative = 'Prefabs/Home/Dynamic/Theme/Ground/' + name + '.prefab'
            root = self.recovery.apk.asset(BUNDLE, '/' + name + '.prefab')
            source_transform = root.read().m_Component[0].component.deref()
            effect, = [child.read().m_GameObject.deref() for child in source_transform.read().m_Children
                       if child.read().m_GameObject.read().m_Name == 'Eff_Home_Snow']
            controller, = [entry.component.deref() for entry in root.read().m_Component
                           if entry.component.type.name == 'MonoBehaviour'
                           and entry.component.deref().read(check_read=False).m_Script.read().m_ClassName == 'HomeWeatherSnow']
            native_path = self.native / relative
            native = documents(native_path.read_text('utf8'))
            native_root, = [key for key, value in native.items()
                            if value.get('GameObject', {}).get('m_Name') == name]
            native_effect, = [key for key, value in native.items()
                              if value.get('GameObject', {}).get('m_Name') == 'Eff_Home_Snow']
            self.recovery.recover(name, self.work / name, bundle=BUNDLE,
                native_relative=relative, source_root=effect, native_root_id=native_effect)
            panel = self.recovery.outputs[-1][2]
            if panel['scripts'] or panel['unresolved']:
                raise ValueError('Bind additional original snow-effect dependencies: ' + name)
            for key, ref in panel['external'].items():
                self.objects[ref['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
            docs = panel['documents']
            root_fields = copy.deepcopy(native[native_root]['GameObject'])
            transform_id = root_fields['m_Component'][0]['component']['fileID']
            controller_id, = [row['component']['fileID'] for row in root_fields['m_Component']
                if 'MonoBehaviour' in native[row['component']['fileID']]
                and native[row['component']['fileID']]['MonoBehaviour']['m_Script']['fileID']
                == script_id('Home.Skin', 'HomeWeatherSnow')]
            root_fields['m_Component'] = [dict(component=dict(fileID=key)) for key in (transform_id, controller_id)]
            root_transform = copy.deepcopy(native[transform_id])
            root_transform['Transform']['m_Children'] = [dict(fileID=docs[native_effect]['GameObject']['m_Component'][0]['component']['fileID'])]
            effect_transform = docs[native_effect]['GameObject']['m_Component'][0]['component']['fileID']
            docs[effect_transform]['Transform']['m_Father'] = dict(fileID=transform_id)
            fields = self.recovery.fields(controller)
            restored = copy.deepcopy(native[controller_id]['MonoBehaviour'])
            restored['m_Script'] = self.scripts['Home.Skin.HomeWeatherSnow']
            restored['mAppearEffectDuration'] = fields['mAppearEffectDuration']
            restored['mParticleAppearCurve'] = fields['mParticleAppearCurve']
            restored['mSnowParticleSystems'] = [dict(fileID=panel['objectMap'][identity(pointer(ref, controller))])
                                               for ref in fields['mSnowParticleSystems']]
            docs[native_root] = dict(GameObject=root_fields)
            docs[transform_id] = root_transform
            docs[controller_id] = dict(MonoBehaviour=restored)
            token = hashlib.md5(('zjcs:home-snow-effect:' + identity(root)).encode()).hexdigest()
            self.queue.append((Path('Resources/Home/Weather/SnowEffects') / (name + '.prefab'), token, docs))
            for obj in (root, source_transform, controller):
                self.proof[identity(obj)] = dict(type=obj.type.name,
                    source=dict(self.recovery.apk.provenance(obj)), original_sha256=sha(obj.get_raw_data()))
            selections.append(dict(name=name, original_ground=identity(root), effect=identity(effect),
                controller=identity(controller), source_fields=fields,
                prefab=dict(fileID=native_root, guid=token, type=3),
                resource='Home/Weather/SnowEffects/' + name, retained_objects=panel['objectMap'],
                retained_ground_components=[identity(source_transform), identity(controller)]))
        index = 0
        while index < len(self.queue):
            data = self.queue[index][2]
            if isinstance(data, dict):
                resolved = self.replace(data); data.clear(); data.update(resolved)
            index += 1
        # Keep this module's converted resources separate from earlier conversions.
        rewrites = {token: hashlib.md5(('zjcs:home-snow-effect-resource:' + token).encode()).hexdigest()
                    for _, token, _ in self.queue if token not in {row['prefab']['guid'] for row in selections}}
        def rewrite(value):
            if isinstance(value, dict):
                return {key: rewrites.get(item, item) if key == 'guid' else rewrite(item) for key, item in value.items()}
            if isinstance(value, list): return list(map(rewrite, value))
            return value
        self.queue = [(path, rewrites.get(token, token), rewrite(data)) for path, token, data in self.queue]
        for relative in ('scripts/prepare_home_snow_effect_native.py', 'scripts/home_native_assets.py',
                         'scripts/prepare_partner_card_native.py', 'scripts/native_spine_assets.py'):
            self.inputs[relative] = sha((ROOT / relative).read_bytes())
        return selections


def install(cache):
    work = ROOT / 'output/validation' / ('home-snow-effects-' + uuid.uuid4().hex)
    work.mkdir()
    (work / '.zjcs-validation.json').write_text(json.dumps(dict(format=1, repository=str(ROOT))), 'utf8')
    recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=BUNDLE)
    try:
        binding = SnowEffectBinding(work, recovery=recovery)
        selections = binding.build()
        staged = work / 'assets'
        outputs = write_staged_assets(binding.queue, staged)
        previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
        installed = install_staged_assets(work, staged, outputs, DEST, 'home-snow-effects', previous_outputs=previous)
        validation_inputs = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (
            ROOT / 'scripts/fixtures/home_snow_effect_native_validation.cs',
            ROOT / 'scripts/test_home_snow_effect_native.ps1')}
        receipt = dict(manifest=recovery.apk.version, inputs=binding.inputs, validation_inputs=validation_inputs,
            selections=selections,
            resources=binding.proof, recovery=recovery.proof, outputs=installed,
            adaptations=['Ground root, transform and HomeWeatherSnow retain their original identity and fields. '
                         'Only the complete Eff_Home_Snow subtree is included in this effect prefab. '
                         'Other ground visuals/controllers remain owned by the ground migration and are not replaced.'],
            scope='Original three ground snow particle effects and controller bindings. '
                  'Full ground prefab and production Home integration require separate validation.')
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Installed original snow effect closures:', len(selections), 'prefabs;', len(installed), 'files')
        print('Owned validation workspace:', work)
    finally:
        recovery.apk.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    install(parser.parse_args().cache.resolve())
