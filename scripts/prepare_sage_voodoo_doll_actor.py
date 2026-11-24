"""Restore the original Mapping Substitute actor and its lifecycle playables."""
import hashlib
import json
from pathlib import Path

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_source import OfficialSource
from prepare_battle_skill_actor import build
from prepare_battle_scene_appearance_runtime import identity, write_changed


def main():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'sage-voodoo-doll'
    receipt = build(4600, cache=cache,
        destination=ROOT / 'unity/Assets/Official/Combat/SageVoodooDollActor',
        resource_root='Combat/NativeMonsters/SkillActors/VoodooDoll', receipt_prefix='sage-voodoo-doll',
        generator=Path(__file__), lifecycle_names={4600: dict(
            mDiePlayable='playable_die', mGeneratePlayable='playable_3193_generate')})
    with Apk.current() as apk:
        component, = [obj for obj in apk.objects('defaultpackage_assets_prefabs_ecentity_fight_fightother_4600.bundle')
            if obj.type.name == 'MonoBehaviour'
            and obj.read(check_read=False).m_Script.read().m_ClassName == 'MyFightRoleVoodooDoll']
        owner = component.read(check_read=False).m_GameObject.deref()
        if owner.read().m_Name != '4600':
            raise ValueError('Bind the original substitute link source hierarchy')
        proof = json.loads(receipt.read_bytes())
        proof['original_substitute_link'] = dict(source=apk.provenance(component),
            object_sha256=hashlib.sha256(component.get_raw_data()).hexdigest(),
            fields=CurrentSchemas(cache / 'inputs/Managed').read(component),
            application=identity(ROOT / '.tools/source-packages/current' / source.version
                / 'official-code/client-source/MyFightRoleVoodooDoll.cs'),
            runtime=identity(ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.VoodooDolls.cs'))
        write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
