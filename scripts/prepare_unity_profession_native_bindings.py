"""Resolve the native profession prefab closure and verify every runtime binding.

Planning reads recovered native YAML. Installation stays gated until every original
script, managed-reference class, shader and dynamic resource has a verified port.
"""
from collections import Counter
import argparse
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_profession_native_inputs import ROOT, CACHE

GUID = re.compile(r"guid: ([0-9a-f]{32})")
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)
SCRIPT = re.compile(r"^  m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", re.M)
BUILTIN = {"00000000000000000000000000000000", "0000000000000000e000000000000000", "0000000000000000f000000000000000"}
TEXT = {".prefab", ".asset", ".mat", ".controller", ".overridecontroller", ".anim", ".shader", ".rendertexture", ".spriteatlas"}
ORIGINAL = "Prefabs/UI/Profession/CharacterPanelProfessionPanel.prefab"
WORLD = ROOT / "unity/Assets/Game/Runtime/Official/WorldUI"
PROFESSION = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
ALIASES = {
    "MultiLanguageText": "WorldMultiLanguageText", "PlayerPropItem": "ProfessionPlayerPropItem",
    "TabItem": "WorldTabItem", "TabGroup": "WorldTabGroup", "TabAnimatorTransition": "WorldTabAnimatorTransition",
    "ClampingScrollRect": "WorldClampingScrollRect", "SafeArea": "WorldSafeArea",
    "MaintainGlobalScaleUI": "WorldMaintainGlobalScaleUI", "LayoutElementClamper": "WorldLayoutElementClamper",
    "CountRedDotImage": "WorldCountRedDotImage", "PopupWindow": "WorldPopupWindow", "PreviewTool": "WorldPreviewTool",
    "UIBloomGroup": "WorldUIBloomGroup", "BgDialogElement": "WorldBgDialogElement",
    "ItemProgress": "WorldItemProgress",
}
STANDARD = {"Image", "RawImage", "Button", "Mask", "RectMask2D", "ScrollRect", "ContentSizeFitter", "LayoutElement",
            "HorizontalLayoutGroup", "VerticalLayoutGroup", "GridLayoutGroup", "AspectRatioFitter", "GraphicRaycaster", "Slider", "Shadow"}
MANAGED = {("Framework", "", "StateGameObjectConfig"): ("Zjcs.Official.WorldUI", "Zjcs.Official.WorldUI", "WorldStateGameObjectConfig"),
           ("Framework", "", "StateUIColorConfig"): ("Zjcs.Official.WorldUI", "Zjcs.Official.WorldUI", "WorldStateUIColorConfig")}


for _name in ("TipDotCommonNode", "TipDotAchievementConditionNode", "TipDotAnyProfessionPromotionCondtionNode",
              "TipDotFreedomProfessioningConditionDot", "TipDotProfessionCanUpgradeConditionNode", "TipDotProfessionPromotionCondtionNode"):
    MANAGED[("Assembly-CSharp", "", _name)] = ("Zjcs.Official.ProfessionUI", "Zjcs.Official.ProfessionUI", _name)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path):
    found = META_GUID.search(path.with_name(path.name + ".meta").read_text(encoding="utf-8"))
    if not found:
        raise ValueError("Native binding metadata has no GUID: " + str(path))
    return found[1]


def runtime_script(original):
    name = original.stem
    if name in STANDARD:
        package = ROOT / "unity/Library/PackageCache/com.unity.ugui@1.0.0"
        paths = [path.with_suffix("") for path in package.rglob(name + ".cs.meta") if "Editor" not in path.parts]
        if len(paths) != 1:
            raise ValueError("Inspect the actual local UGUI script: " + name + " " + str(paths))
        return paths[0]
    if name in ("TextMeshProUGUI", "TMP_FontAsset"):
        return ROOT / "unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime" / (name + ".cs")
    if name == "TMPEffect":
        return ROOT / "unity/Assets/Game/Runtime/Official/World/NativeText/TMPEffect.cs"
    if name == "UIParticle":
        return ROOT / "unity/Assets/Game/Runtime/Presentation/Opening/OfficialCoffeeUIParticle/UIParticle.cs"
    name = ALIASES.get(name, name)
    for folder in (PROFESSION, WORLD):
        path = folder / (name + ".cs")
        if path.exists():
            return path
    return None


def build(scope="main", bind_shaders=True):
    cache = CACHE / "typed"
    exported = cache / "export/ExportedProject/Assets"
    proof_path = ROOT / "assets/reference/profession-ui/native-export-source-manifest.json"
    proof = json.loads(proof_path.read_bytes())
    if proof["manifest"] != "871_156598":
        raise ValueError("Profession native export version differs")
    restored = {}
    for row in proof["outputs"]:
        path = ROOT / row["path"]
        if sha(path) != row["sha256"]:
            raise ValueError("Restored native asset changed: " + str(path))
        restored[path.relative_to(cache / "restored").as_posix()] = path
    index = {}
    for meta in exported.rglob("*.meta"):
        match = META_GUID.search(meta.read_text(encoding="utf-8"))
        if match:
            rel = meta.with_suffix("").relative_to(exported).as_posix()
            if match[1] in index:
                raise ValueError("Duplicate native export asset GUID: " + match[1])
            index[match[1]] = rel
    font_proof_path = ROOT / "assets/reference/profession-ui/fonts-source-manifest.json"
    font_proof = json.loads(font_proof_path.read_bytes())
    if font_proof["manifest"] != proof["manifest"]:
        raise ValueError("Native profession font source version differs")
    for row in font_proof["assets"]:
        path = ROOT / row["path"]
        if sha(path) != row["sha256"] or sha(path.with_name(path.name + ".meta")) != row["meta_sha256"]:
            raise ValueError("Recovered native font asset changed: " + str(path))
        if row["guid"] in index:
            raise ValueError("Recovered zero-size atlas conflicts with a converter output")
        index[row["guid"]] = row["native_relative_path"]
        restored[row["native_relative_path"]] = path
    dynamic_proof = None
    item_path = None
    if scope in ("main-dynamic", "item-icons"):
        dynamic_path = ROOT / "assets/reference/profession-ui/dynamic-resources-source-manifest.json"
        dynamic_proof = json.loads(dynamic_path.read_bytes())
        if dynamic_proof["manifest"] != proof["manifest"]:
            raise ValueError("Dynamic profession resource source version differs")
        for row in dynamic_proof["native_assets"]:
            path = ROOT / row["path"]
            if sha(path) != row["sha256"] or sha(path.with_name(path.name + ".meta")) != row["meta_sha256"] or guid(path) != row["guid"]:
                raise ValueError("Restored dynamic profession asset changed: " + str(path))
            if row["guid"] in index:
                raise ValueError("Restored dynamic profession GUID conflicts with converter output")
            index[row["guid"]] = row["native_relative_path"]
            restored[row["native_relative_path"]] = path
        seeds = [ORIGINAL] + [row["native_relative_path"] for row in dynamic_proof["resources"]]
        for row in dynamic_proof["resources"]:
            path = restored.get(row["native_relative_path"], exported / row["native_relative_path"])
            if sha(path) != row["export_sha256"] or guid(path) != row["export_guid"]:
                raise ValueError("Dynamic profession resource export changed: " + str(path))
        item_path = ROOT / "assets/reference/profession-ui/item-configs-source-manifest.json"
        if item_path.exists():
            item_inputs = json.loads(item_path.read_bytes())
            if item_inputs["manifest"] != proof["manifest"]:
                raise ValueError("Original item configuration version differs")
            for row in item_inputs["native_assets"]:
                path = ROOT / row["path"]
                if sha(path) != row["sha256"] or sha(path.with_name(path.name + ".meta")) != row["meta_sha256"] or guid(path) != row["guid"]:
                    raise ValueError("Original item configuration asset changed")
                if row["guid"] in index:
                    raise ValueError("Original item config GUID conflicts with export")
                index[row["guid"]] = row["native_relative_path"]
                restored[row["native_relative_path"]] = path
            seeds.extend(row["native_relative_path"] for row in item_inputs["native_assets"])
            seeds.append("Prefabs/UI/Item/Attachments/CenterHintContent.prefab")
        elif scope == "item-icons":
            raise ValueError("Restore the original item configuration first")
        if scope == "item-icons":
            seeds = [seed for seed in seeds if seed.startswith(("Prefabs/UI/Item/", "ProfessionDynamic/", "ScriptableConfig/Modules/ItemIcon/"))]
    elif scope == "main":
        seeds = [ORIGINAL]
    elif scope == "all":
        seeds = sorted(path.relative_to(exported).as_posix()
            for path in (exported / "Prefabs/UI/Profession").glob("*.prefab"))
    elif scope == "item-hint":
        seeds = ["Prefabs/UI/Item/Attachments/CenterHintContent.prefab"]
    else:
        raise ValueError("Unknown native profession binding scope: " + scope)
    texture_path = ROOT / "assets/reference/profession-ui/native-textures-source-manifest.json"
    if texture_path.exists():
        texture_inputs = json.loads(texture_path.read_bytes())
        if texture_inputs["manifest"] != proof["manifest"]:
            raise ValueError("Original UI texture version differs")
        for row in texture_inputs["native_assets"]:
            old = row["replaces_import"]
            path = ROOT / row["path"]
            if index[row["guid"]] != old["native_relative_path"] or sha(ROOT / old["path"]) != old["sha256"] or sha(ROOT / old["meta"]["path"]) != old["meta"]["sha256"]:
                raise ValueError("Original PNG import replacement changed")
            if sha(path) != row["sha256"] or sha(path.with_name(path.name + ".meta")) != row["meta_sha256"] or guid(path) != row["guid"]:
                raise ValueError("Lossless native UI texture changed")
            index[row["guid"]] = row["native_relative_path"]
            restored[row["native_relative_path"]] = path
    selected, missing, scripts, script_uses, managed, shaders = {}, [], {}, Counter(), Counter(), []
    queue = [(path, "explicit original prefab") for path in seeds]
    while queue:
        rel, owner = queue.pop()
        if rel in selected:
            continue
        path = restored.get(rel, exported / rel)
        if not path.exists():
            raise ValueError("Native dependency is not exported: " + rel)
        if path.suffix == ".cs":
            target = runtime_script(path)
            scripts[rel] = None if target is None else dict(path=target.relative_to(ROOT).as_posix(), guid=guid(target), sha256=sha(target))
            continue
        selected[rel] = dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path), owner=owner)
        if path.suffix.lower() not in TEXT:
            continue
        text = path.read_text(encoding="utf-8")
        if "Map13Original" in text:
            raise ValueError("Native asset still contains converter-only fields: " + rel)
        for script_guid in SCRIPT.findall(text):
            script_uses[index[script_guid]] += 1
        for match in re.finditer(r"(?m)^      type:\n        class: ([^\n]*)\n        ns:([^\n]*)\n        asm:([^\n]*)", text):
            cls, ns, asm = (value.strip() for value in match.groups())
            if cls:
                managed[(asm, ns, cls)] += 1
        if path.suffix == ".shader":
            shaders.append(rel)
        for reference in set(GUID.findall(text)) - BUILTIN:
            if reference not in index:
                missing.append(dict(owner=rel, guid=reference))
            else:
                queue.append((index[reference], rel))
    pending = [dict(script=path, uses=script_uses[path]) for path, binding in sorted(scripts.items()) if binding is None]
    references = [dict(assembly=key[0], namespace=key[1], type=key[2], count=count,
                       binding=list(MANAGED[key]) if key in MANAGED else None) for key, count in sorted(managed.items())]
    shader_proof_path = ROOT / "assets/reference/profession-ui/shader-bindings-source-manifest.json"
    shader_bindings = {}
    if bind_shaders and shader_proof_path.exists():
        shader_proof = json.loads(shader_proof_path.read_bytes())
        if shader_proof["manifest"] != proof["manifest"]:
            raise ValueError("Native profession shader version differs")
        for binding in shader_proof["bindings"]:
            native = ROOT / "unity" / binding["native"]["path"]
            rel = binding["native_relative_path"]
            if scope in ("item-hint", "item-icons") and rel not in selected:
                continue
            if sha(native) != binding["native"]["sha256"] or guid(native) != binding["native"]["guid"]:
                raise ValueError("Verified profession shader binding changed: " + rel)
            if rel not in selected or selected[rel]["sha256"] != binding["export_sha256"] or guid(ROOT / selected[rel]["path"]) != binding["export_guid"]:
                raise ValueError("Verified profession shader no longer matches the actual closure: " + rel)
            shader_bindings[rel] = binding["native"]
    pending_shaders = [path for path in shaders if path not in shader_bindings]
    result = dict(format=1, manifest=proof["manifest"], scope=scope, seeds=seeds,
        verified_export=dict(path=proof_path.relative_to(ROOT).as_posix(), sha256=sha(proof_path)),
        verified_fonts=dict(path=font_proof_path.relative_to(ROOT).as_posix(), sha256=sha(font_proof_path)),
        operation="Resolve all original native asset GUID references, retain restored registries, identify actual local script GUIDs and original shader dependencies; no prefab construction, stripping or approximate replacement.",
        generator=dict(path=Path(__file__).resolve().relative_to(ROOT).as_posix(), sha256=sha(Path(__file__))),
        assets=selected, script_bindings=scripts, pending_scripts=pending,
        managed_reference_types=references, shaders=shaders, shader_bindings=shader_bindings, pending_shaders=pending_shaders, missing=missing,
        verified_dynamic_resources=None if dynamic_proof is None else dict(path=dynamic_path.relative_to(ROOT).as_posix(), sha256=sha(dynamic_path)),
        verified_item_configs=None if item_path is None or not item_path.exists() else dict(path=item_path.relative_to(ROOT).as_posix(), sha256=sha(item_path)),
        verified_native_textures=None if not texture_path.exists() else dict(path=texture_path.relative_to(ROOT).as_posix(), sha256=sha(texture_path)),
        dynamic_resources="Reward icons and rule popup have explicit source-backed seeds in main-dynamic scope; portrait and tutor resources remain incomplete.")
    dest = cache / ("native-bindings-" + scope + ".json")
    dest.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Native profession binding closure:", len(selected), "assets;", len(scripts), "script types;", len(pending), "pending scripts;", len(references), "managed types;", len(shaders), "shaders;", len(pending_shaders), "pending shaders;", len(missing), "missing GUIDs")
    for item in pending:
        print("Pending:", item["uses"], item["script"])
    for item in references:
        if item["binding"] is None:
            print("Pending registry:", item["assembly"], item["namespace"], item["type"], item["count"])
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scope", choices=("main", "main-dynamic", "all", "item-hint", "item-icons"), default="main")
    build(parser.parse_args().scope)
