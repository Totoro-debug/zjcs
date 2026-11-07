"""Recover the exact ElementDoor graph and gate activation sound from selected sources."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from UnityPy.classes.PPtr import PPtr
from map14_audio_source import AudioSource, sha
from prepare_unity_map14_playable_audio import audio_meta
from prepare_unity_map13_prop_parts import meta

VERSION = "884_156692"


def build():
    work = Path(os.environ["ZJCS_VALIDATION_DIR"])
    if work.parent.resolve() != (ROOT / "output/validation").resolve() or not (work / ".zjcs-validation.json").is_file():
        raise ValueError("Use an owned validation workspace")
    outputs = []
    with Apk.current() as apk:
        if apk.version != VERSION:
            raise ValueError("Review selected source")
        schemas = ScriptSchemas(code_root=ROOT / ".tools/source-packages/current" / VERSION / "official-code")
        graph = apk.asset("defaultpackage_assets_scriptableconfig_tipdotgraphassets_map_world.bundle", "/ElementDoor.asset")
        fields = schemas.read(graph)
        nodes = []
        for ref in fields["nodes"]:
            node = PPtr(**ref, assetsfile=graph.assets_file).deref()
            nodes.append(dict(source=apk.provenance(node), fields=schemas.read(node), payloadSha256=sha(node.get_raw_data())))
        types = [next(r["type"]["class"] for r in row["fields"]["references"]["RefIds"] if r["rid"] == row["fields"]["TipDotNode"]["rid"]) for row in nodes]
        if types != ["TipDotCommonNode", "TipDotMapStageConPropStandardCondtionNode", "TipDotMapWorldStageStateCondtionNode"]:
            raise ValueError("Original ElementDoor graph closure changed")
        root_connections = nodes[0]["fields"]["ports"]["values"][1]["connections"]
        if ([str(r["node"]["m_PathID"]) for r in root_connections] != [r["source"]["path_id"] for r in nodes[1:]]
                or fields["Type"] != 1 or fields["Scope"] != 2):
            raise ValueError("Original ElementDoor connections/type/scope changed")
        def preset(row):
            value = dict(row["fields"]["PresetData"])
            for name in ("NeedAllShow", "IsRoot", "IsReverse"):
                value[name] = bool(value[name])
            return value
        stage = nodes[2]["fields"]
        target = next(r["data"]["TargetState"] for r in stage["references"]["RefIds"] if r["rid"] == stage["TipDotNode"]["rid"])
        runtime = dict(manifest=VERSION, root=preset(nodes[0]), property=preset(nodes[1]), stage=preset(nodes[2]), targetState=target)
        graph_path = ROOT / "unity/Assets/Official/World/Map/14/UI/Roadblock/element-door-graph.json"
        graph_path.write_text(json.dumps(dict(manifest=VERSION, source=apk.provenance(graph), fields=fields, nodes=nodes), ensure_ascii=False, indent=2) + "\n", encoding="utf8")
        runtime_path = ROOT / "unity/Assets/Game/Resources/Map14/UI/roadblock-tip-graph.json"
        runtime_path.write_text(json.dumps(runtime, indent=2) + "\n", encoding="utf8")
        for path in (graph_path, runtime_path):
            meta(path); outputs.extend((path, Path(str(path) + ".meta")))
        print("Verified the three original ElementDoor nodes and their connections", flush=True)
    audio_records = []
    with AudioSource(expected_version=VERSION, work_directory=work) as source:
      for name, expected_id in (("Map_Entity_Activate_ElementDoor", 3132674326), ("Main_Cursor_Fail", 2458796094)):
        name = name
        identity, banks = source.load_event(name)
        if identity != expected_id or not banks:
            raise ValueError("Original gate sound identity differs")
        source.objects("wwisebnk_assets_wwisedata_android_minimum_pck.rawfile")
        directory = Path(source.temporary.name)
        txtp = directory / ("txtp-" + name)
        txtp.mkdir()
        wwiser = ROOT / ".tools/wwiser/wwiser.py"
        decoder = ROOT / ".tools/vgmstream/vgmstream-cli.exe"
        subprocess.run([sys.executable, str(wwiser), *map(str, sorted(directory.glob("*.bnk"))),
                        "-g", "-d", "none", "-go", str(txtp), "-gw", "../", "-gv", "1.0", "-gf", str(identity)],
                       check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        paths = list(txtp.glob("*.txtp"))
        if len(paths) != 1:
            raise ValueError("Inspect gate event rendering variants: " + str(paths))
        text = paths[0].read_text("utf8")
        print(text, flush=True)
        leaves = [line.strip() for line in text.splitlines() if line.strip() and not line.lstrip().startswith("#")]
        if len(leaves) != 1 or not re.fullmatch(r"\.\./\d+\.bnk #s\d+ #i  #v -?[\d.]+dB  ##\d+\.wem", leaves[0]) or "master volume: auto" in text:
            raise ValueError("Inspect original gate sound's additional playback behavior")
        rendered = directory / "gate.wav"
        subprocess.run([str(decoder), "-i", "-o", str(rendered), str(paths[0])], check=True,
                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        sound = ROOT / "unity/Assets/Official/World/Map/14/UI/Roadblock" / (name + ".wav")
        sound.write_bytes(rendered.read_bytes())
        audio_meta(sound); outputs.extend((sound, Path(str(sound) + ".meta")))
        audio_records.append(dict(event=name, eventId=identity, graph=text, packages=source.receipts(), outputSha256=sha(sound.read_bytes())))
      receipt = dict(manifest=VERSION, graphSource=apk.provenance(graph), graphNodes=nodes, audio=audio_records,
                       originalCode=[dict(path=str(path.relative_to(ROOT)), sha256=sha(path.read_bytes())) for path in
                           [ROOT / ".tools/source-packages/current" / VERSION / "official-code/client-source" / (name + ".cs") for name in types]],
                       tool=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes())),
                       outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in outputs])
      target_path = ROOT / "assets/reference/npc-assistance/roadblock-supplements-source-manifest.json"
      target_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
      print("Original gate graph and unnormalized Wwise event PCM restored", flush=True)


if __name__ == "__main__":
    build()
