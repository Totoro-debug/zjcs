"""Compare encouragement's local inputs with the selected official APK and CDN."""
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack


def verify():
    combat = ROOT / "unity/Assets/Official/Combat"
    skill = json.loads((combat / "skill-rules.json").read_bytes())
    battle = json.loads((combat / "battle-rules.json").read_bytes())
    skill_receipt = json.loads((combat / "skill-rules-source-manifest.json").read_bytes())
    battle_receipt = json.loads((combat / "battle-rules-source-manifest.json").read_bytes())
    expected = {
        11550: (skill, skill_receipt["selected"]["11550"]["path"], ("FightSkillComponent",)),
        11551: (battle, battle_receipt["entities"]["selected"]["11551"]["path"],
                ("GridTransformComponent", "FightHitFixedComponent")),
        11552: (battle, battle_receipt["entities"]["selected"]["11552"]["path"],
                ("GridTransformComponent", "FightDamageComponent")),
        24237: (battle, battle_receipt["entities"]["selected"]["24237"]["path"],
                ("ActionComponent", "FightStatusComponent", "FightStatusPropComponent")),
    }
    paths = {entry[1] for entry in expected.values()}
    with Apk.current() as apk:
        formats = Formats(ROOT / ".tools/source-packages/current" / apk.version / "official-code")
        found = set()
        for name, ptr in apk.container("config_ec"):
            if name not in paths:
                continue
            reader = ptr.deref()
            raw = reader.read().m_Script.encode("utf8", "surrogateescape")
            export = ROOT / ".tools/source-packages/current" / apk.version / "native-conversion/native-export-all/ExportedProject" / name
            if raw != export.read_bytes():
                raise ValueError("Extracted EC input does not match its verified official bundle: " + name)
            for _, identity, components in unpack(raw)[0]:
                if identity not in expected or expected[identity][1] != name:
                    continue
                document, _, required = expected[identity]
                available = {kind.removeprefix("Common."): (kind, data) for kind, data in components}
                for component in required:
                    kind, data = available[component]
                    latest = formats.component(kind, data)
                    local = document["entities"][str(identity)][component]
                    if identity == 11552 and component == "FightDamageComponent":
                        # The older payload predates this field; the local dispenser
                        # implements the verified exclusions for this damage entity.
                        latest = dict(latest)
                        disperse = dict(latest["DisperseStatus"])
                        if sorted(disperse.pop("ExcludeCharacterTypes")) != [3, 7]:
                            raise ValueError("Current encouragement dispel exclusions changed")
                        latest["DisperseStatus"] = disperse
                    if latest != local:
                        raise ValueError(f"Current official {identity}/{component} differs from local input")
                    found.add((identity, component))
        required = {(identity, component) for identity, (_, _, components) in expected.items()
                    for component in components}
        if found != required:
            raise ValueError("Current official encouragement components are incomplete")
        return apk.version


if __name__ == "__main__":
    print("Official encouragement inputs verified against", verify())
