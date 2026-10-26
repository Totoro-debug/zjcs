"""Port verified official tile/chunk types and partition data to native Unity C#."""
from pathlib import Path
import json
import re
import subprocess

from official_apk import ROOT
from official_source import OfficialSource
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_water_components import import_meta


DESTINATION = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeTerrain"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-terrain-code-source-manifest.json"
CLIENT = ("PartitionElement", "PartitionGroup", "PartitionLayer", "PartitionLayerAssigner",
          "MyTile", "MapGroundTile", "TileChunkData", "PartitionData")
COMMON = ("GroundType", "Direction", "Pos2d")
NAMESPACE = "Zjcs.Official.World.NativeTerrain"


def original_base_call(assembly):
    cecil = ROOT / ".tools/map13-ilspy/tools/net10.0/any/Mono.Cecil.dll"
    command = "$ErrorActionPreference='Stop'\nAdd-Type -Path '" + cecil.as_posix() + "'\n"
    command += "$source=[Mono.Cecil.AssemblyDefinition]::ReadAssembly('" + assembly.as_posix() + "')\n"
    command += "try { $type=$source.MainModule.Types | Where-Object FullName -eq 'MyTile'; "
    command += "@(($type.Methods | Where-Object Name -eq 'GetTileData').Body.Instructions | ForEach-Object ToString) | ConvertTo-Json -Compress } finally { $source.Dispose() }"
    lines = json.loads(subprocess.check_output(["pwsh", "-NoProfile", "-Command", command], text=True))
    expected = ["IL_0000: ldarg.0", "IL_0001: ldarg.1", "IL_0002: ldarg.2", "IL_0003: ldarg.3",
        "IL_0004: call System.Void UnityEngine.Tilemaps.Tile::GetTileData(UnityEngine.Vector3Int,UnityEngine.Tilemaps.ITilemap,UnityEngine.Tilemaps.TileData&)",
        "IL_0009: ret"]
    if lines != expected: raise ValueError("Original MyTile base-call IL changed")
    return dict(tool=source_path(cecil), tool_sha256=sha(cecil.read_bytes()), instructions=lines)


def redirect_logging(text, name):
    text = text.replace("using Framework.Logging;\n", "").replace("using Framework.Logging.InterpolatedStringHandler;\n", "")
    replacements = {
        "Pos2d": ['UnityEngine.Debug.LogError($"Direction not supported: {dir}");'],
        "PartitionData": ['UnityEngine.Debug.LogError($"Scene Chunk is illegal, region:{region} res:{resPath} chunkPos:{chunkPos}");',
                          'UnityEngine.Debug.LogError($"Extension Chunk is illegal, region:{region} res:{resPath}");']}
    messages = iter(replacements.get(name, []))
    pattern = r"GLoggerErrorInterpolatedStringHandler message = [^\n]+;\n.*?Log.Error\([^\n]+;"
    def replace(match):
        try: return next(messages)
        except StopIteration: raise ValueError("Unexpected original logging block: " + name)
    text, count = re.subn(pattern, replace, text, flags=re.S)
    if count != len(replacements.get(name, [])): raise ValueError("Original logging block count changed: " + name)
    return text


def build():
    source = OfficialSource()
    code = ROOT / ".tools/source-packages/current" / source.version / "official-code"
    code_manifest_path = code.parent / "code-manifest.json"
    manifest = json.loads(code_manifest_path.read_text())
    assemblies = []
    for name in ["Assembly-CSharp.dll", "Console.dll"]:
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != manifest["assemblies"][name]["decoded_sha256"]:
            raise ValueError("Official terrain assembly differs: " + name)
        assemblies.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    il = original_base_call(code / "managed/decoded/Assembly-CSharp.dll")
    inputs, outputs = [], []
    for name in CLIENT + COMMON:
        common = name in COMMON
        original = code / ("common-source/Common" if common else "client-source") / (name + ".cs")
        text = original.read_text(encoding="utf8")
        text = redirect_logging(text, name)
        if common:
            if not text.count("namespace Common;\n") == 1: raise ValueError("Original common namespace changed")
            text = text.replace("namespace Common;\n", "namespace " + NAMESPACE + ".Common\n{\n") + "\n}\n"
        else:
            text = text.replace("using Common;", "using " + NAMESPACE + ".Common;")
            text = "namespace " + NAMESPACE + "\n{\n" + text + "\n}\n"
        if name == "Pos2d":
            text = text.replace("using MessagePack;\n", "").replace("[MessagePackObject(false)]\n", "")
            text = text.replace("\t[Key(0)]\n", "").replace("\t[Key(1)]\n", "")
            if text.count("public struct Pos2d(int x, int y)") != 1: raise ValueError("Original Pos2d constructor changed")
            text = text.replace("public struct Pos2d(int x, int y)", "public struct Pos2d")
            text = text.replace("public int X = x;", "public int X;").replace("public int Y = y;", "public int Y;")
            text = text.replace("\tpublic static readonly Pos2d Zero", "\tpublic Pos2d(int x, int y) { X = x; Y = y; }\n\n\tpublic static readonly Pos2d Zero", 1)
        if name == "MyTile":
            if text.count("((Tile)this).GetTileData(position, tilemap, ref tileData);") != 1:
                raise ValueError("Original MyTile decompiler shape changed")
            text = text.replace("((Tile)this).GetTileData(position, tilemap, ref tileData);", "base.GetTileData(position, tilemap, ref tileData);")
        raw = ("// Migrated from selected official terrain code; see Map13 source receipt.\n" + text).encode()
        path = DESTINATION / ("Common" if common else "") / original.name
        write(path, raw); import_meta(path, "MonoImporter")
        inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes())))
        outputs.append(dict(path=source_path(path), sha256=sha(raw)))
    import_meta(DESTINATION, "DefaultImporter", folder=True)
    import_meta(DESTINATION / "Common", "DefaultImporter", folder=True)
    asmdef = DESTINATION / "Zjcs.Official.World.NativeTerrain.asmdef"
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, references=[],
        includePlatforms=[], excludePlatforms=[], allowUnsafeCode=False, overrideReferences=False,
        precompiledReferences=[], autoReferenced=True, defineConstraints=[], versionDefines=[], noEngineReferences=False)))
    import_meta(asmdef, "AssemblyDefinitionImporter")
    outputs.append(dict(path=source_path(asmdef), sha256=sha(asmdef.read_bytes())))
    packages = ROOT / "unity/Packages/manifest.json"
    package_data = json.loads(packages.read_text())
    dependency = package_data["dependencies"].get("com.unity.modules.tilemap")
    if dependency not in (None, "1.0.0"): raise ValueError("Unexpected Tilemap module version")
    if dependency is None:
        package_data["dependencies"]["com.unity.modules.tilemap"] = "1.0.0"
        write(packages, (json.dumps(package_data, ensure_ascii=False, separators=(",", ":")) + "\n").encode())
    write(RECEIPT, encode(dict(format=1, mapClassId=13, manifest=source.version, assemblies=assemblies,
        code_manifest=dict(path=source_path(code_manifest_path), sha256=sha(code_manifest_path.read_bytes())),
        inputs=inputs, outputs=outputs, my_tile_original_il=il,
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        dependencies={"com.unity.modules.tilemap": "1.0.0"},
        adaptations=["Add native terrain namespaces; keep original serialized fields, enum values, formulas and execution order.",
                     "Convert Pos2d primary constructor into a Unity C# compatible explicit constructor; omit unused network MessagePack attributes only.",
                     "Redirect the three original error log messages to Unity Debug without changing validation branches or return behavior.",
                     "Repair MyTile decompiler cast into the verified IL base.GetTileData call; do not introduce recursive virtual dispatch."],
        limitations=["Original partition loading/environment controllers and native scene bindings remain required; these source types alone do not provide playable terrain."])))
    print("Migrated original Map13 terrain types:", len(inputs))


if __name__ == "__main__":
    build()
