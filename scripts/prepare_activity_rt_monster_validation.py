#!/usr/bin/env python3
"""Copy the verified 3706 native RT inputs into the owned rendered fixture."""
import json

from official_apk import ROOT
from prepare_battle_live_monsters import sha
from prepare_activity_rt_validation import main as prepare_component
from validation_workspace import validation_directory


def main():
    prepare_component()
    project = validation_directory() / 'unity'
    proof = json.loads((ROOT / 'assets/reference/daily-challenges/activity-rt-monster-assets-source-manifest.json').read_bytes())
    bindings = ['unity/Assets/Game/Runtime/Offline/' + name for name in (
        'ProfessionUI/ProfessionNativeOrderedPhysicsPose.cs',
        'ProfessionUI/BattleNativeMonsterRenderer.cs',
        'ProfessionUI/BattleNativeMonsterRenderer.Animator.cs',
        'ProfessionUI/ProfessionNativeAnimatorPose.cs',
        'PartnersUI/PartnerNativeUiPhysicsPose.cs')]
    for relative in bindings + [name + '.meta' for name in bindings]:
        source = ROOT / relative
        target = project / source.relative_to(ROOT / 'unity')
        target.write_bytes(source.read_bytes())
    for relative, expected in proof['outputs'].items():
        path = ROOT / relative
        raw = path.read_bytes()
        if sha(raw) != expected: raise ValueError('Original 3706 native RT output changed: ' + relative)
        destination = project / path.relative_to(ROOT / 'unity')
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists() or destination.read_bytes() != raw: destination.write_bytes(raw)
    print('Prepared original 3706 RT skeleton, controller, materials and Low textures.', flush=True)


if __name__ == '__main__': main()
