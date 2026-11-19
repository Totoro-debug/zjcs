"""Restore the original 3665 battle model without dropping installed models."""
import json
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_battle_live_monsters import build as extract, encoded, sha
from prepare_battle_live_monster_assets import build as install
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    receipt_path = ROOT / 'assets/reference/battle-animation/live-monsters-source-manifest.json'
    inventory_path = ROOT / 'unity/Assets/Official/Combat/NativeMonsters/inventory.json'
    previous = json.loads(receipt_path.read_bytes())
    if previous['manifest'] != '884_156692':
        raise ValueError('Review the selected common monster source version')
    for name, expected in previous['outputs'].items():
        if sha((ROOT / name).read_bytes()) != expected:
            raise ValueError('Preserve changed installed monster input: ' + name)
    installed = json.loads(inventory_path.read_bytes())
    ids = {row['entityId'] for row in installed['models']} | {3665}
    apk = Apk.current()
    monsters, pets = [], []
    try:
        for entity_id in sorted(ids):
            families = [name for name in ('fightmonster', 'fightpet')
                if f'defaultpackage_assets_prefabs_ecentity_fight_{name}_{entity_id}.bundle' in apk.index]
            if len(families) != 1:
                raise ValueError('Bind one original battle prefab family: ' + str(entity_id))
            (pets if families[0] == 'fightpet' else monsters).append(entity_id)
    finally:
        apk.archive.close()
    extract(monsters, pets)
    template_name = next(name for name in previous['outputs']
        if '/textures/' in name and name.endswith('.asset'))
    template = ROOT / template_name
    text = template.read_text('utf-8')
    header = '--- !u!28 &2800000\n'
    if text.count(header) != 1 or '  m_TextureFormat: 4\n' not in text:
        raise ValueError('Review the existing native RGBA32 serializer template')
    (work / 'monster-texture-template.asset').write_text(
        text.replace(header, '--- !u!28 &1\n'), encoding='utf-8', newline='\n')
    install()
    receipt = json.loads(receipt_path.read_bytes())
    if 'custom_summon_installer' in previous:
        receipt['custom_summon_installer'] = previous['custom_summon_installer']
    receipt['monster_3665_installer'] = dict(path='scripts/prepare_battle_monster_3665.py',
        sha256=sha(Path(__file__).read_bytes()))
    receipt['serializer_template_source'] = dict(path=template_name, sha256=sha(template.read_bytes()),
        adaptation='Reuse verified RGBA32 serialization; restore input object ID 1. Source fields and every mip are replaced by the common converter.')
    receipt_path.write_bytes(encoded(receipt))


if __name__ == '__main__':
    main()
