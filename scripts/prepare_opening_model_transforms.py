"""Recover opening unit transforms from the pinned APK's fight entity prefabs."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk
from prepare_character_creation_opening import ROOT, PAYLOAD, REFERENCE, VERSION, json_bytes, write_with_meta


def main():
    apk = Apk()
    units = []
    for class_id in (3281, 3282, 3283, 3481, 3803, 4201, 4202):
        kind = "fightnpc" if class_id >= 4200 else "fightmonster"
        bundle = f"defaultpackage_assets_prefabs_ecentity_fight_{kind}_{class_id}.bundle"
        root = next(p.read() for path, p in apk.container(bundle)
                    if p.type.name == "GameObject" and path.endswith(f"/{class_id}.prefab"))
        matches = []

        def visit(go, position, scale, path, sources):
            path += "/" + go.m_Name
            pointer = next((c.component for c in go.m_Component if c.component.type.name == "Transform"), None)
            if pointer is None:
                return
            transform = pointer.read()
            rotation = transform.m_LocalRotation
            if (rotation.x, rotation.y, rotation.z, rotation.w) != (0, 0, 0, 1):
                # Particle/UI branches are outside the fighter's identity-rotation chain.
                return
            local_position = [getattr(transform.m_LocalPosition, axis) for axis in "xyz"]
            local_scale = [getattr(transform.m_LocalScale, axis) for axis in "xyz"]
            position = [position[i] + scale[i] * local_position[i] for i in range(3)]
            scale = [scale[i] * local_scale[i] for i in range(3)]
            source = dict(apk.provenance(pointer.deref()), object_path=path,
                          sha256=hashlib.sha256(pointer.deref().get_raw_data()).hexdigest())
            sources = sources + [source]
            scripts = [c.component.deref().read(check_read=False).m_Script.read().m_ClassName
                       for c in go.m_Component if c.component.type.name == "MonoBehaviour"]
            if "SkeletonMecanim" in scripts or "SkeletonAnimation" in scripts:
                materials = []
                for component in go.m_Component:
                    if component.component.type.name != "MeshRenderer":
                        continue
                    for material in component.component.read().m_Materials:
                        fields = dict(material.read().m_SavedProperties.m_Floats)
                        materials.append(dict(apk.provenance(material.deref()),
                            sha256=hashlib.sha256(material.deref().get_raw_data()).hexdigest(),
                            straight_alpha=fields.get("_StraightAlphaInput", 0)))
                alpha = {m["straight_alpha"] for m in materials}
                if len(alpha) > 1:
                    raise ValueError("Opening model has ambiguous atlas alpha input: " + path)
                matches.append(dict(class_id=class_id, position=position, scale=scale,
                                    object_path=path, sources=sources, materials=materials,
                                    straight_alpha=next(iter(alpha)) if alpha else None))
                return
            for child in transform.m_Children:
                visit(child.read().m_GameObject.read(), position, scale, path, sources)

        visit(root, [0, 0, 0], [1, 1, 1], "", [])
        if len(matches) != 1:
            raise ValueError(f"Expected one official fighter model: {class_id}: {len(matches)}")
        units.extend(matches)
    output = write_with_meta(PAYLOAD / "model-transforms.json",
                             json_bytes(dict(manifest=VERSION, units=units)))
    receipt = json.loads(REFERENCE.read_text(encoding="utf-8"))
    receipt["model_transforms"] = dict(units=units, generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    receipt["outputs"] = [row for row in receipt["outputs"] if row["path"] != output["path"]] + [output]
    variants = ROOT / "unity/Assets/Game/Resources/Opening/OpeningShaderVariants.shadervariants"
    variant_output = dict(path=variants.relative_to(ROOT).as_posix(), bytes=variants.stat().st_size,
                          sha256=hashlib.sha256(variants.read_bytes()).hexdigest())
    receipt["outputs"] = [row for row in receipt["outputs"] if row["path"] != variant_output["path"]] + [variant_output]
    receipt["windows_shader_variants"] = dict(output=variant_output, shader_sources=[
        dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())
        for path in sorted((ROOT / "unity/Assets/Game/Runtime/Presentation/Opening").glob("OpeningUi*.shader"))])
    REFERENCE.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps([dict(class_id=u["class_id"], position=u["position"], scale=u["scale"]) for u in units]))


if __name__ == "__main__":
    main()
