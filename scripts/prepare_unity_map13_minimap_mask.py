"""Migrate the original minimap edge/corner mask implementation and grid data."""
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_prop_parts import meta

DEST = ROOT / "unity/Assets/Game/Runtime/Official/World/Map13MiniMap"
CODE = ROOT / ".tools/source-packages/current/871_156598/official-code"
RECEIPT = ROOT / "assets/reference/official-map/map13-minimap-mask-code-source-manifest.json"


def build():
    inputs, outputs = [], []

    def emit(name, text):
        path = DEST / name
        write(path, text.encode())
        meta(path, "MonoImporter" if path.suffix == ".cs" else "AssemblyDefinitionImporter")
        outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))

    def read(path):
        inputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
        return path.read_text(encoding="utf-8-sig").replace("\r\n", "\n")

    source = read(CODE / "client-source/MiniMapMaskHandler.cs")
    source = source.replace("using Common;", "using Zjcs.Official.World.NativeTerrain.Common;\nusing Zjcs.Official.World.NativeLabelUI;")
    source = source.replace("using Framework.Logging;\n", "").replace("using Framework.Logging.InterpolatedStringHandler;\n", "")
    source = source.replace("public class MiniMapMaskHandler", "namespace Zjcs.Official.World.Map13MiniMap\n{\nusing Object = UnityEngine.Object;\npublic class MiniMapMaskHandler", 1)
    source = source.replace("UIUtils.GetTilePosByGrid(mMapClassId, pos)", "GetTilePosByGrid(mMapClassId, pos)")
    source = source.replace("Vector4.op_Implicit(transformMatrix.GetColumn(3))", "(Vector3)transformMatrix.GetColumn(3)")
    at = source.index("\n\tpublic void Init(")
    source = source[:at] + '''

    // Local resource/session binding supplies the original MapUtils lookup.
    public Func<int, Pos2d, Vector3> GridToWorld { private get; set; }
    private Vector3Int GetTilePosByGrid(int mapClassId, Pos2d grid)
    {
        if (GridToWorld == null) throw new InvalidOperationException("Original minimap grid binding is missing.");
        Vector3 val = GridToWorld(mapClassId, grid);
        return new Vector3Int((int)((val.x - 0.5f) / 1f), (int)((val.y - 0.5f) / 1f), (int)(val.z / 1f));
    }
''' + source[at:]
    source += "\n}\n"
    emit("MiniMapMaskHandler.cs", "// Migrated from selected official code; see minimap mask source receipt.\n" + source)
    read(CODE / "client-source/UIUtils.cs")
    for name in ("MapRootEntityLiteGridInfo", "MapRootEntityLiteAreaInfo", "MapRootEntityLiteSingleInfo"):
        source = read(CODE / "common-source/Common" / (name + ".cs"))
        source = source.replace("using MessagePack;\n", "")
        source = re.sub(r"(?m)^\s*\[(?:MessagePackObject\([^\n]*|Key\([^\n]*)\]\n", "\n", source)
        source = source.replace("namespace Common;", "using Zjcs.Official.World.NativeTerrain.Common;\nnamespace Zjcs.Official.World.Map13MiniMap\n{")
        emit(name + ".cs", "// Original shared map data; only MessagePack transport annotations removed.\n" + source + "\n}\n")
    emit("MiniMapDiagnostic.cs", '''using System.Text;
using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Preserve original diagnostic calls without the online logging backend.
    internal struct GLoggerErrorInterpolatedStringHandler
    {
        private StringBuilder text;
        public GLoggerErrorInterpolatedStringHandler(int literals, int formatted, out bool enabled)
        { text = new StringBuilder(literals); enabled = true; }
        public void AppendLiteral(string value) => text.Append(value);
        public void AppendFormatted<T>(T value, int alignment = 0, string format = null, string expression = null) => text.Append(value);
        public override string ToString() => text.ToString();
    }
    internal static class Log
    {
        public static void Error(ref GLoggerErrorInterpolatedStringHandler message, object context, string member, string file, int line)
        { Debug.LogError(message.ToString()); }
    }
}
''')
    emit("Zjcs.Official.World.Map13MiniMap.asmdef", encode(dict(name="Zjcs.Official.World.Map13MiniMap",
        references=["Zjcs.Official.World.NativeTerrain", "Zjcs.Official.World.NativeLabelUI"], autoReferenced=True)).decode())
    meta(DEST, folder=True)
    result = dict(format=1, manifest="871_156598", mapClassId=13, inputs=inputs, outputs=outputs,
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Copy the complete official MiniMapMaskHandler including original pattern ordering, four rotations, maskability/lock branches, cache updates and Tilemap transforms. Replace only namespaces, IL decompiler vector syntax, diagnostic backend and MapUtils/session lookup with an explicit instance delegate; preserve the exact UIUtils tile-position formula. Copy unchanged shared grid data and methods, removing only MessagePack transport annotations.",
        limitations=["Core migration only. Complete original MiniMap_13 component bindings, native asset import, rendering/input/save verification and Windows Player remain required."])
    write(RECEIPT, encode(result))
    print("Original Map13 minimap mask code:", len(outputs), "files", flush=True)
    return result


if __name__ == "__main__":
    build()
