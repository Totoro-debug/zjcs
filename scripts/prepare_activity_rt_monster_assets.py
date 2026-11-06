#!/usr/bin/env python3
"""Install the exact original 3706 RT model inputs through the common native path."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_live_monster_assets import build
from prepare_battle_live_monsters import sha, encoded
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    catalog = json.loads((work / 'live-monsters/catalog.json').read_bytes())
    model, = catalog['models']
    if (model['entityId'] != 3706 or model['rtPrefab']['path'] !=
            'Assets/Prefabs/Appearance/Monster/3706/MonsterModel_3706_RT.prefab'
            or model['rtPrefab']['appearance']['LevelOfResolution'] != 1):
        raise ValueError('Bind the actual original 3706 RT variant and Low texture loader')
    # Reuse a verified existing Windows serializer template; source dimensions,
    # sampling flags, mip data and color space still come from the new texture.
    proof = json.loads((ROOT / 'assets/reference/battle-animation/live-monsters-source-manifest.json').read_bytes())
    path = next(name for name in proof['outputs'] if '/textures/' in name and name.endswith('.asset'))
    template = ROOT / path
    if sha(template.read_bytes()) != proof['outputs'][path]:
        raise ValueError('The common native texture serializer template changed')
    text = template.read_text('utf8')
    header = '--- !u!28 &2800000\n'
    if text.count(header) != 1:
        raise ValueError('Reinspect the verified common Windows texture object ID')
    text = text.replace(header, '--- !u!28 &1\n')
    (work / 'monster-texture-template.asset').write_text(text, 'utf8', newline='\n')
    receipt = ROOT / 'assets/reference/daily-challenges/activity-rt-monster-assets-source-manifest.json'
    build(destination=ROOT / 'unity/Assets/Official/DailyChallenges/ActivityRtMonsters',
        resource_path='DailyChallenges/ActivityRtMonsters/catalog', receipt_path=receipt)
    data = json.loads(receipt.read_bytes())
    data['activity_binding_generator_sha256'] = sha(Path(__file__).read_bytes())
    for name in ('ProfessionUI/ProfessionNativeOrderedPhysicsPose.cs',
                 'ProfessionUI/ProfessionNativeAnimatorPose.cs',
                 'PartnersUI/PartnerNativeUiPhysicsPose.cs'):
        relative = 'unity/Assets/Game/Runtime/Offline/' + name
        data['implementation'][relative] = sha((ROOT / relative).read_bytes())
    data['serializer_template_source'] = dict(path=path, sha256=sha(template.read_bytes()),
        adaptation='Restore the common serializer input object ID 1 from its existing output ID 2800000; preserve all other template fields.')
    data['scope'] = 'Original 3706 RT variant full skeleton/controller/material/Low texture inputs. Production TempRTUI target, native prefab hierarchy, attachment followers, particles and rendered validation remain pending.'
    receipt.write_bytes(encoded(data))


if __name__ == '__main__': main()
