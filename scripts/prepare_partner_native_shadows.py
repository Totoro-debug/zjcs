"""Recover the original partner shadow subtrees using the shared native converter."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_character_shadows import main as prepare


def main():
    source = ROOT / 'unity/Assets/Official/Combat/NativePartners/catalog.json'
    selected = json.loads(source.read_bytes())
    if selected['manifest'] != '884_156692':
        raise ValueError('Review the original partner shadow version')
    roles = [dict(prefab=str(row['entityId']), parts=['/ShadowBoneFollower'],
        sourcePrefab='Prefabs/EcEntity/Fight/FightNpc/' + str(row['entityId']) + '.prefab')
        for row in selected['fighters']]
    base = ROOT / 'unity/Assets/Official/Combat/NativePartnerAssets/Resources/Combat/NativePartnerAssets'
    prepare(destination=base / 'RoleShadows', data=base / 'partner-shadow-bindings.json',
        receipt=ROOT / 'assets/reference/partner-native-shadows-source-manifest.json',
        role_catalog=dict(manifest=selected['manifest'], roles=roles), catalog_source=source,
        resource_root='Combat/NativePartnerAssets/RoleShadows', generator=Path(__file__))


if __name__ == '__main__':
    main()
