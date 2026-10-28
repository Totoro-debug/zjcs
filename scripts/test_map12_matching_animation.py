"""Check Map12 matching duration against the original APK AnimationClip ARM64 kernel."""
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT/'.tools/character-art-python'), str(ROOT/'.tools/map12-native-kernel-python')]
from UnityPy.classes.PPtr import PPtr
from official_apk import Apk
from commerce_clip_time import legacy_duration
from read_official_native import EngineMethods
from test_official_animation_length import native_length
from prepare_dungeon_ui_bindings import relative_transform_path


def main():
    apk = Apk.current()
    root = apk.asset('prefabs_ui_dailydungeon', '/DailyDungeonDialogPrivateMap.prefab').read()
    host = next(go for go, _, names, _ in relative_transform_path(root)
                if names[-3:] == ('NormalGo', 'GameMatch', 'Content'))
    animation = next(pair.component.deref() for pair in host.m_Component if pair.component.type.name == 'Animation')
    clip = PPtr(**animation.read_typetree()['m_Animation'], assetsfile=animation.assets_file).read()
    assert clip.m_Legacy and clip.m_Name == 'DailyDungeonDetailContent_GameMatch_Show' and clip.m_SampleRate == 60
    engine = EngineMethods(ROOT/'.tools/source-packages/official-code/libunity.so')
    assert engine.by_name['UnityEngine.AnimationClip::get_length'][0] == 0x5755e0
    actual = native_length(engine.image, clip, [event.time for event in clip.m_Events])
    # The existing native fixture supplies float curves and events. Verify that every
    # other real curve ends within that range before using its result for this clip.
    for field in ('m_RotationCurves', 'm_EulerCurves', 'm_PositionCurves', 'm_ScaleCurves'):
        assert all(key.time <= actual for curve in getattr(clip, field) for key in curve.curve.m_Curve)
    assert not clip.m_PPtrCurves and not clip.m_CompressedRotationCurves
    assert actual == legacy_duration(clip)
    receipt = json.loads((ROOT/'assets/reference/official-map/map12-matching-animation-source-manifest.json').read_bytes())
    assert receipt['binding']['clip']['duration'] == actual, (receipt['binding']['clip']['duration'], actual)
    assert clip.m_Events[0].time < actual and clip.m_Events[0].intParameter == 0
    print('Map12 original AnimationClip kernel passed: length=', actual, 'event=', clip.m_Events[0].time)


if __name__ == '__main__':
    main()
