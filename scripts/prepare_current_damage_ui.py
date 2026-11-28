"""Replace world-space damage resources from the fixed current APK/CDN source."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery
from prepare_current_battle_hud import (CurrentBattleHudSources, identity,
                                      verify_cache, windows_references, work_directory)
from prepare_battle_damage_ui_native import DamageBinding, DEST, RECEIPT, main as install


class CurrentDamageRecovery(PartnerNativeRecovery):
    def finalize(self):
        super().finalize()
        layout = object.__new__(CurrentBattleHudSources)
        layout.apk, layout.recovery = self.apk, self
        for _, _, data, record in self.outputs:
            record['restored_layout'] = layout.restore_layout(data)
            record['original_layout_fields'] = layout.validate_layout(data)


def normalize_checkout(proof):
    """Record bytes that survive this repository's Git checkout conversion."""
    paths = list(proof['installed'])
    for start in range(0, len(paths), 30):
        batch = paths[start:start+30]
        raw = subprocess.check_output(['git', 'check-attr', '-z', 'text', 'filter', 'eol', '--', *batch], cwd=ROOT)
        values = raw.decode().rstrip('\0').split('\0')
        attributes = {}
        for index in range(0, len(values), 3):
            name, attribute, value = values[index:index+3]
            attributes.setdefault(name, {})[attribute] = value
        for name in batch:
            path, attrs = ROOT/name, attributes[name]
            data = path.read_bytes()
            if attrs['text'] == 'unset' or attrs['filter'] == 'lfs' or b'\0' in data[:8192]:
                continue
            if path.suffix not in ('.meta', '.prefab', '.asset', '.mat', '.anim', '.controller', '.shader', '.json'):
                continue
            normalized = data.replace(b'\r\n', b'\n')
            if attrs['eol'] != 'lf': normalized = normalized.replace(b'\n', b'\r\n')
            if data != normalized: path.write_bytes(normalized)
            proof['installed'][name] = identity(path)['sha256']
    proof['checkout_serialization'] = 'Git text/eol attributes; CRLF text checkout; binary and LFS payloads retain exact bytes.'


def prepare(work, cache):
    work = work_directory(work)
    source = OfficialSource()
    managed = verify_cache(cache)
    windows = windows_references(cache)
    # These serialized behaviours are already complete ports. Require exact
    # current source equality before retaining their existing script bindings.
    runtime = ROOT/'assets/reference/battle-hud/damage-ui-runtime-source-manifest.json'
    prior_runtime = json.loads(runtime.read_bytes())
    original = {Path(row['path']).name: row for row in prior_runtime['inputs']}
    compatible = []
    for name in ('CharacterDamageUI', 'DamageNumberUI', 'DamageFloatSize',
                 'DamageCustomActionUI', 'ElementConfrontationUI',
                 'DamageAutoSizeTextBack', 'FightDamageSettings'):
        path = source.prepared_path/'official-code/client-source'/(name+'.cs')
        row = identity(path)
        if row['sha256'] != original[path.name]['sha256']:
            raise ValueError('Migrate changed serialized damage behaviour first: '+name)
        compatible.append(row)
    previous = json.loads(RECEIPT.read_bytes())
    # Validate and copy the entire previous installation before replacing or
    # retiring anything. A completed snapshot remains in this owned workspace.
    snapshot = work/'previous-installation'
    rows = []
    for relative, expected in previous['installed'].items():
        path = ROOT/relative
        if path.is_file():
            row = identity(path)
            if row['sha256'] != expected:
                raise ValueError('Previous damage resource changed: '+relative)
            rows.append(row)
        else:
            raise FileNotFoundError(path)
    rows.append(identity(RECEIPT))
    if snapshot.exists():
        saved = json.loads((snapshot/'snapshot.json').read_bytes())
        if not saved['completed'] or saved['files'] != rows:
            raise ValueError('Retain the different previous snapshot: '+str(snapshot))
    else:
        snapshot.mkdir()
        for row in rows:
            target = snapshot/row['path']
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT/row['path'], target)
        (snapshot/'snapshot.json').write_text(json.dumps(dict(completed=True, files=rows), indent=2)+'\n', 'utf-8')
    for row in rows:
        if identity(snapshot/row['path'])['sha256'] != row['sha256']:
            raise ValueError('Previous snapshot copy differs: '+row['path'])
    recovery = CurrentDamageRecovery(native_cache=cache, export_name='native-export',
        bundle='defaultpackage_assets_prefabs_ui_fight_damagescene_battledamagefloateffect_combine_scene.bundle')
    try:
        binding = DamageBinding(work, recovery=recovery,
            resource_cache=source.prepared_path/'native-conversion')
        binding.font_source_hashes = {Path(path).name: value for path, value in previous['inputs'].items()
            if Path(path).name in ('MultiLanguageFontService.cs', 'FontAssetFontService.cs', 'MultiLanguageText.cs')}
        for row in managed+compatible:
            binding.inputs[row['path']] = row['sha256']
        for name in ('prepare_current_damage_ui.py', 'prepare_partner_card_native.py',
                     'prepare_partner_roster_native.py', 'prepare_partner_gift_native.py',
                     'prepare_battle_round_scene_native.py', 'prepare_current_battle_hud.py',
                     'prepare_partner_panel_native.py', 'partner_panel_animator_binding.py'):
            row = identity(ROOT/'scripts'/name)
            binding.inputs[row['path']] = row['sha256']
        install(binding)
        proof = json.loads(RECEIPT.read_bytes())
        normalize_checkout(proof)
        proof.update(apk_sha256=source.apk_sha256,
            input_manifest=identity(cache/'source-manifest.json'),
            native_export=identity(cache/'native-export-source-manifest.json'),
            windows=windows, retained_serialized_behaviour_sources=compatible,
            retained_font_source_hashes=binding.font_source_hashes,
            generator=identity(Path(__file__).resolve()))
        RECEIPT.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2)+'\n').replace('\n','\r\n').encode('utf-8'))
        verify()
    finally:
        recovery.apk.close()


def verify():
    proof = json.loads(RECEIPT.read_bytes())
    source = OfficialSource()
    if proof['manifest'] != source.version or proof['apk_sha256'] != source.apk_sha256:
        raise ValueError('Installed damage source differs')
    rows = [proof['input_manifest'], proof['native_export'], proof['generator']]
    rows += proof['windows']['files']
    rows += [proof['windows'][key] for key in ('inventory', 'original_inputs', 'shared_inventory')]
    rows += [row['source'] for row in proof['windows']['catalogs']]
    rows += [dict(path=path, sha256=expected) for path, expected in proof['inputs'].items()]
    rows += [dict(path=path, sha256=expected) for path, expected in proof['installed'].items()]
    for row in rows:
        if identity(ROOT/row['path'])['sha256'] != row['sha256']:
            raise ValueError('Damage source or installed payload changed: '+row['path'])
    if len(proof['prefabs']) != 13:
        raise ValueError('Incomplete damage prefab installation')
    for path in proof['installed']:
        relative = (ROOT/path).relative_to(DEST.parent)
        if any(part in ('pinned', source.version) or part[:1].isdigit() for part in relative.parts[:-1]):
            raise ValueError('Use generic Unity damage resource paths: '+path)
    print('Verified current damage installation:', len(proof['installed']), 'files.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path)
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    if args.verify:
        verify()
    else:
        if args.work is None: parser.error('--work is required for replacement')
        prepare(args.work, OfficialSource().prepared_path/'battle-damage-ui-native')
