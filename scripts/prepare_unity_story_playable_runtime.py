"""Migrate the selected official playable scheduler and lifecycle to C# 9.

The offline caller supplies the same two frame phases and explicitly injects the
center. No duration approximation, native Spine runtime or invented server rule.
"""
import json
from pathlib import Path
import re
from official_apk import ROOT
from prepare_unity_map13_sources import encode, meta, sha, source_path, write

CODE = ROOT / ".tools/source-packages/current/871_156598/official-code"
OUTPUT = ROOT / "unity/Assets/Game/Runtime/Official/StoryPlayables"
RECEIPT = ROOT / "assets/reference/official-map/map14-story-playable-runtime-source-manifest.json"
NAMESPACE = "Zjcs.Official.StoryPlayables"

def runtime_meta(path):
    meta(path)
    output = Path(str(path) + ".meta")
    identity = re.search(r"(?m)^guid: ([a-f0-9]{32})$", output.read_text(encoding="utf8"))[1]
    header = "fileFormatVersion: 2\nguid: " + identity + "\n"
    if path.is_dir():
        text = header + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    elif path.suffix == ".cs":
        text = header + "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    elif path.suffix == ".asmdef":
        text = header + "AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    else:
        raise ValueError("Unsupported runtime metadata asset: " + str(path))
    write(output, text.encode("utf8"))

def logging_adapter(text):
    # ILSpy expands the original interpolated logger into a handler block.
    # Only that diagnostic block changes; catches and recovery remain intact.
    pattern = re.compile(r"(?m)^(?P<indent>[ \t]*)(?:GLogger(?:Error|Warn)InterpolatedStringHandler )?message = new GLogger(?:Error|Warn)InterpolatedStringHandler\([^;]+;\n(?P=indent)if \(enabled\)\n(?P=indent)\{\n(?:(?P=indent)[ \t]+message\.Append[^\n]*;\n)*(?P=indent)\}\n(?P=indent)Log\.(?P<kind>Error|Warn)\((?:(?P<exception>[A-Za-z_]\w*), )?ref message,[^\n]+;")
    changes = []
    def replacement(match):
        changes.append(match[0])
        exception = match["exception"]
        statement = ("UnityEngine.Debug.LogException(" + exception + ");" if exception else
                     "UnityEngine.Debug.Log" + ("Error" if match["kind"] == "Error" else "Warning") + "(\"Official playable diagnostic\");")
        return match["indent"] + statement
    result = pattern.sub(replacement, text)
    result = re.sub(r"(?m)^\s*GLogger(?:Error|Warn)InterpolatedStringHandler message;\n", "", result)
    result = re.sub(r"(?m)^\s*bool enabled;\n", "", result)
    if "GLogger" in result or "Log.Error" in result or "Log.Warn" in result:
        raise ValueError("Unresolved original logging adaptation")
    return result, len(changes)

def build():
    manifest = json.loads((CODE.parent / "code-manifest.json").read_bytes())
    assemblies = []
    for name in ("GameFramework.Runtime.dll", "Framework.FirstPass.Runtime.dll", "Console.dll"):
        path = CODE / "managed/decoded" / name
        if sha(path.read_bytes()) != manifest["assemblies"][name]["decoded_sha256"]:
            raise ValueError("Changed selected official playable assembly: " + name)
        assemblies.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    names = ["MyPlayable", "MyPlayableSequence", "MyPlayableApplyObj", "MyPlayableTrigger", "MyPlayableTriggerTimeCfg",
             "IMyPlayableNeedStop", "IMyPlayableStateMutex", "IMyPlayableCenter", "MyPlayableCenter",
             "MyPlayableOnInit", "MyPlayableApplyAttribute", "MyPlayableApplyUtils", "ApplyOnInit", "ApplyBySource", "ApplyEnableOnPlay"]
    sources = {name + ".cs": CODE / "gameframework-csharp9-source/GameFramework" / (name + ".cs") for name in names}
    sources.update({name + ".cs": CODE / "common-source/Common" / (name + ".cs") for name in ("EcEntityIdentifier", "ActionBehaviour", "FloatUtils")})
    sources.update({"ProfilerScope.cs": CODE / "common-source/ProfilerScope.cs",
                    "OrderedFastList.cs": CODE / "framework-firstpass-source/Framework.Collections/OrderedFastList.cs",
                    "IGameObjectPoolItem.cs": CODE / "framework-source/IGameObjectPoolItem.cs",
                    "GAction.cs": CODE / "framework-firstpass-source/Framework/GAction.cs"})
    outputs, adaptations, inputs = [], [], []
    for name, path in sources.items():
        text = path.read_text(encoding="utf8")
        inputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
        if name == "GAction.cs":
            text = text[:text.index("public sealed class GAction<T1, T2, T3>")]
        text = re.sub(r"(?m)^using (?:Framework(?:\.[\w.]+)?|Common|MessagePack);\n", "", text)
        text = re.sub(r"(?m)^namespace (?:GameFramework|Framework(?:\.[\w.]+)?|Common);?\n", "namespace " + NAMESPACE + (";\n" if name not in {n + ".cs" for n in names} else "\n"), text)
        # Flatten the reference-only file-scoped namespace for Unity C# 9.
        if "namespace " + NAMESPACE + ";" in text:
            text = text.replace("namespace " + NAMESPACE + ";", "namespace " + NAMESPACE + "\n{") + "\n}\n"
        if "namespace " + NAMESPACE not in text:
            text = "namespace " + NAMESPACE + "\n{\n" + text + "\n}\n"
        text = "// Migrated selected official source; see map14-story-playable-runtime source receipt.\nusing Object = UnityEngine.Object;\n" + text
        if name == "MyPlayable.cs":
            text = text.replace("[Inject]\n\t\tprivate IMyPlayableCenter MyPlayableCenter", "public IMyPlayableCenter MyPlayableCenter")
            text = text.replace("Time.frameCount", "UnityEngine.Time.frameCount")
        if name == "MyPlayableCenter.cs":
            text = text.replace(": BackgroundServiceBase, IMyPlayableCenter, IUpdatable, ILateUpdatable", ": IMyPlayableCenter")
            text = text.replace("void IUpdatable.Update()", "public void Update(float deltaTime)")
            text = text.replace("\t\t\t\tfloat deltaTime = Time.deltaTime;\n", "")
            text = text.replace("void ILateUpdatable.LateUpdate()", "public void LateUpdate()")
        if name == "MyPlayableApplyObj.cs":
            text = text.replace("using Object = UnityEngine.Object;", "using Object = UnityEngine.Object;\nusing MainModule = UnityEngine.ParticleSystem.MainModule;\nusing EmissionModule = UnityEngine.ParticleSystem.EmissionModule;\nusing SubEmittersModule = UnityEngine.ParticleSystem.SubEmittersModule;")
            text = re.sub(r"(?m)^\s*\[Rename\([^\n]+\n", "", text)
        if name == "EcEntityIdentifier.cs":
            text = re.sub(r"(?m)^\s*\[(?:MessagePackObject|Key)\([^\n]+\n", "", text)
        declarations = re.findall(r"(?m)^\s*(?:(?:public|private|protected|internal|static|virtual|override|sealed|readonly|async)\s+)+(?:[\w<>?,.\[\]]+\s+)(\w+)\s*\(", text)
        text, count = logging_adapter(text)
        remaining = re.findall(r"(?m)^\s*(?:(?:public|private|protected|internal|static|virtual|override|sealed|readonly|async)\s+)+(?:[\w<>?,.\[\]]+\s+)(\w+)\s*\(", text)
        if declarations != remaining:
            raise ValueError("Diagnostic adaptation changed an original method declaration: " + name)
        target = OUTPUT / name
        write(target, text.encode("utf8")); runtime_meta(target)
        outputs.extend([target, Path(str(target) + ".meta")])
        adaptations.append(dict(source=name, diagnosticBlocks=count))
    asmdef = OUTPUT / "Zjcs.Official.StoryPlayables.asmdef"
    write(asmdef, encode(dict(name="Zjcs.Official.StoryPlayables", rootNamespace=NAMESPACE, references=[], autoReferenced=True)))
    runtime_meta(asmdef); outputs.extend([asmdef, Path(str(asmdef) + ".meta")])
    # Parent folders are tracked together with the code.
    for path in [OUTPUT]:
        runtime_meta(path); outputs.append(Path(str(path) + ".meta"))
    receipt = dict(manifest="871_156598", mapClassId=14, assemblies=assemblies, inputs=inputs,
        tools=[dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes()))],
        outputs=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in outputs], adaptations=adaptations,
        operation="Copy official scheduler, ordered collection, lifecycle, triggers, sequences, apply-on-init and particle fade code. Preserve formulas, frame phases, callback and mutation order, exception recovery and refcounts. Explicit offline center injection and frame driving replace DI/R3 registration. Adapt only namespace, C# version, Unity nested type qualifications, inspector/wire attributes and diagnostics.",
        limitations=["Core runtime only; original prefab/script bindings, custom animation/bone followers, Timeline/camera/audio, native Spine-free model playback, formal map lifetime and actual live validation remain required."])
    write(RECEIPT, encode(receipt)); print("Original playable core migrated:", len(sources), "files", flush=True)
    return receipt

if __name__ == "__main__":
    build()
