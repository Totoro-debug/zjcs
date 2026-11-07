"""Bind original roadblock native assets through the verified Map14 converter."""
from official_apk import ROOT
from prepare_map14_roadblock_native_inputs import workspace, IDS
from prepare_unity_map14_hero_statues_native import build
import hashlib
import json

if __name__ == "__main__":
    build(cache=workspace(), class_ids=IDS, source_manifest="884_156692",
          destination=ROOT / "unity/Assets/Official/World/Map/14/NativeRoadblocks",
          receipt_path=ROOT / "assets/reference/npc-assistance/roadblock-native-source-manifest.json",
          additional_scripts={"ApplyBySource": "StoryPlayables/ApplyBySource.cs",
                              "MyPlayableSound": "StorySounds/MyPlayableSound.cs",
                              "MyPlayableAnimator": "StoryAnimation/MyPlayableAnimator.cs",
                              "WwiseEventReference": "ProfessionUI/WwiseEventReference.cs"},
          additional_types={"EcStageResultOverrideBodyGroundTypeView": "EntityWorldStages"})
    receipt_path = ROOT / "assets/reference/npc-assistance/roadblock-native-source-manifest.json"
    record = json.loads(receipt_path.read_text(encoding="utf8"))
    for key, name in (("converter_proof", "export-source-manifest.json"), ("converter_input", "input-source-manifest.json")):
        target = receipt_path.with_name("roadblock-native-" + name)
        target.write_bytes((workspace() / name).read_bytes())
        record[key] = dict(path=target.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(target.read_bytes()).hexdigest())
    receipt_path.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
