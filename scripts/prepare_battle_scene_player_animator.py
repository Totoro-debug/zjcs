"""Bind current original player Animator selection to verified native controllers.

Only game application code and extracted Unity controller assets are read.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re

from official_source import OfficialSource
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed
from prepare_unity_profession_appearance_resolver import method
from prepare_unity_profession_character_resources import asset, guid
from prepare_unity_profession_player_appearance import strip


def main():
    assert OfficialSource().version == "884_156692"
    code = ROOT / ".tools/source-packages/current/884_156692/official-code"
    receipt = code.parent / "code-manifest.json"
    assembly = code / "managed/decoded/Assembly-CSharp.dll"
    assert identity(assembly)["sha256"] == json.loads(receipt.read_bytes())["assemblies"][assembly.name]["decoded_sha256"]
    destination = ROOT / "unity/Assets/Game/Runtime/Official/CombatAppearance"
    sources, outputs = [], []

    def save(name, source, text):
        path = destination / (name + ".cs")
        write_changed(path, "namespace Zjcs.Official.CombatAppearance\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.ProfessionUI;\n" + text + "\n}\n")
        meta(path); sources.append(identity(source)); outputs.extend((path, path.with_name(path.name + ".meta")))

    source = code / "client-source/AppearanceSystem.Runtime/PlayerPhase.cs"
    save("PlayerPhase", source, source.read_text(encoding="utf-8-sig").replace("namespace AppearanceSystem.Runtime;\n", ""))
    source = code / "client-source/AppearanceSystem.Runtime/AppearanceAddressUtils.cs"
    text = method(source.read_text(encoding="utf-8-sig"), "public static string GetPlayerAnimatorPath(")
    text = text.replace("Profession profession", "int profession").replace("IResourceLoader resourceLoader", "IProfessionPlayerAppearanceBindings bindings, Func<string, bool> locationExists")
    text = text.replace("AppearanceUtils.GetBaseProfession(profession)", "(Profession)bindings.GetBaseProfession(profession)")
    text = text.replace("resourceLoader.CheckLocationExists(text)", "locationExists(text)").replace("RegionCodeEnum.CN", '"CN"')
    save("AppearanceAddressUtils", source, "using System;\npublic static class AppearanceAddressUtils\n{\n" + text + "\n}\n")
    source = code / "client-source/AppearanceSystem/PlayerAnimatorAppearance.cs"
    text = strip(source.read_text(encoding="utf-8-sig"))
    start, end = text.index("\t[Inject]"), text.index("\tpublic override void RefreshAppearance")
    text = text[:start] + '''    private Func<string, bool> mLocationExists;
    private Func<string, RuntimeAnimatorController> mLoadAnimator;
    public void ConfigureAnimatorResources(Func<string, bool> locationExists, Func<string, RuntimeAnimatorController> loadAnimator)
    {
        mLocationExists = locationExists ?? throw new ArgumentNullException(nameof(locationExists));
        mLoadAnimator = loadAnimator ?? throw new ArgumentNullException(nameof(loadAnimator));
    }

''' + text[end:]
    text = text.replace("PlayerPhase, ResourceLoader)", "PlayerPhase, PlayerBindings, mLocationExists)")
    text = text.replace("ResourceLoader.LoadAsset<RuntimeAnimatorController>(playerAnimatorPath).Result", "mLoadAnimator(playerAnimatorPath)")
    start = text.index("\t\t\tGLoggerErrorInterpolatedStringHandler")
    end = text.index("\n\t\t}\n\t\tSetAnimatorController(result);", start)
    text = text[:start] + '            Debug.LogError($"没有找到 AnimatorController: {playerAnimatorPath}. Sex: {sex} Profession: {profession} Phase: {PlayerPhase}");' + text[end:]
    text = "using Zjcs.Official.StoryAnimation;\n" + text
    assert all(token not in text for token in ("[Inject]", "ResourceLoader", "Framework", "Spine.", "GLogger"))
    save("PlayerAnimatorAppearance", source, text)

    # These current original controller inputs were already verified against the
    # compiled APK graphs; retain their identity and reference their native assets.
    controllers_receipt = ROOT / "assets/reference/battle-animation/game-animators-source-manifest.json"
    controllers = json.loads(controllers_receipt.read_bytes())
    assert controllers["manifest"] == "884_156692"
    entries, reused = [], []
    for row in controllers["assets"]:
        for path, expected in row["outputs"].items():
            target = ROOT / path
            assert identity(target)["sha256"] == expected, target
            reused.append(identity(target))
        if row["original"]["type"] not in ("AnimatorController", "AnimatorOverrideController"): continue
        path = next(ROOT / path for path in row["outputs"] if not path.endswith(".meta"))
        # m_Name omits the extension, while the official resource address retains it.
        extension = ".controller" if row["original"]["type"] == "AnimatorController" else ".overrideController"
        entries.append(dict(Address="Assets/AppearanceAssets/Character/Model/Anim/" + row["original"]["name"] + extension,
            Controller=dict(fileID=9100000 if extension == ".controller" else 22100000, guid=guid(path), type=2)))
    assert len(entries) == 4 and len({row["Address"] for row in entries}) == 4
    runtime = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneAnimatorCatalog.cs"
    meta(runtime); outputs.extend((runtime, runtime.with_name(runtime.name + ".meta")))
    catalog = ROOT / "unity/Assets/Official/Combat/SceneCharacterAppearance/Resources/Combat/SceneAppearance/player-animators.asset"
    asset(catalog, "Current original Game player Animators", runtime, dict(entries=entries)); outputs.extend((catalog, catalog.with_name(catalog.name + ".meta")))
    for relative in ("unity/Assets/Game/Runtime/Official/ProfessionUI/Zjcs.Official.ProfessionUI.asmdef",
                     "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAnimator.cs",
                     "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs"):
        path = ROOT / relative
        if path.suffix == ".cs": meta(path)
        outputs.append(path)
        if path.suffix == ".cs": outputs.append(path.with_name(path.name + ".meta"))
    runtime_receipt = ROOT / "assets/reference/battle-rules/animator-runtime-source-manifest.json"
    dependencies = json.loads(runtime_receipt.read_bytes())
    for row in dependencies["identicalDependencySources"]: assert identity(ROOT / row["path"]) == row
    for row in dependencies["runtimeDependencies"]:
        assert hashlib.sha256((ROOT / row["path"]).read_bytes().replace(b"\r\n", b"\n")).hexdigest() == row["generatedLfSha256"]
    proof = dict(manifest="884_156692", code_receipt=identity(receipt), assembly=identity(assembly), application=sources,
        controller_receipt=identity(controllers_receipt), reused_controller_outputs=reused, entries=entries,
        animator_runtime=identity(runtime_receipt), verified_animator_dependencies=dependencies["identicalDependencySources"],
        outputs=[identity(path) for path in outputs], generator=identity(Path(__file__).resolve()),
        dependencies=[identity(ROOT / path) for path in ("assets/reference/battle-animation/scene-player-appearance-source-manifest.json",)],
        binding="Combat/SceneAppearance/player-animators; original full resource addresses map to four exact native Game controllers. ConfigureAnimatorResources supplies synchronous lookup/load, independently of player profile bindings.",
        adaptations=["Copy PlayerAnimatorAppearance and PlayerPhase. Retain controller-before-appearance-before-Update(0), same/different controller branches, ReapplyOwnAnimator and OnAnimatorChanged ordering.",
            "Port only the application GetPlayerAnimatorPath method; supply original base-profession lookup through existing player bindings and location checks through a delegate. Preserve all phase branches and the original fixed CN enum string.",
            "Replace framework asset-handle access with explicit synchronous delegates; retain missing-controller error and subsequent setter branch. Adapt namespaces, enum profession input and diagnostic logging only."],
        limits=["Only four original Game controller locations are bound. Other phase address rules are retained, but their assets are not added here.",
            "Controller clips remain original name carriers; this change does not supply the native Mecanim pose consumer, animation mixing, physics/cache, complete prefab or production battle/Windows Player integration."])
    write_changed(ROOT / "assets/reference/battle-animation/scene-player-animator-source-manifest.json", json.dumps(proof, ensure_ascii=False, indent=2) + "\n")
    print("Current original player Animator lifecycle and four exact Game controller locations bound.")


if __name__ == "__main__": main()
