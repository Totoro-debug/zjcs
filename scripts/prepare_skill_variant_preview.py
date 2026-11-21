"""Verify a reused skill-variant preview against the selected official VideoClip."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from UnityPy.helpers.ResourceReader import get_resource_data


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--effect', type=int, required=True)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    with Apk.current() as apk:
        names = [name for name in apk.index if name.endswith(f'skilldisplayvedio_{args.effect}.bundle')]
        assert len(names) == 1, ('Current official preview bundle is not unique', args.effect, names)
        clips = [obj for obj in apk.objects(names[0]) if obj.type.name == 'VideoClip']
        assert len(clips) == 1, 'Current preview bundle must contain one original VideoClip'
        obj = clips[0]
        clip = obj.read()
        assert clip.m_Name == f'SkillDisplayVedio_{args.effect}', 'Current preview identity changed'
        resource = clip.m_ExternalResources
        payload = get_resource_data(resource.m_Source, obj.assets_file, resource.m_Offset, resource.m_Size)
        assert b'ftyp' in payload[:16], 'Current official preview is not an MP4 payload'
        video = ROOT / f'unity/Assets/StreamingAssets/Character/SkillSkinVideos/{args.effect}.mp4'
        assert video.read_bytes() == payload, 'Installed preview differs from the current official VideoClip; extract the current payload before completing this variant'
        index = ROOT / 'unity/Assets/StreamingAssets/Character/skill-skin-videos.json'
        row = [entry for entry in json.loads(index.read_bytes())['videos'] if entry['effect_skill_id'] == args.effect]
        assert len(row) == 1 and row[0]['sha256'] == hashlib.sha256(payload).hexdigest(), 'Runtime preview identity changed'
        result = dict(format=1, manifest=apk.version, effect_skill_id=args.effect,
                      source=apk.provenance(obj), clip=dict(name=clip.m_Name, width=int(clip.Width),
                          height=int(clip.Height), frames=int(clip.m_FrameCount), frame_rate=float(clip.m_FrameRate)),
                      output=identity(video), runtime_index=identity(index), generator=identity(Path(__file__)))
    target = ROOT / f'assets/reference/battle-animation/skill-variant-{args.effect}-preview-source-manifest.json'
    if args.write:
        target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    else:
        assert json.loads(target.read_bytes()) == result, 'Current skill preview receipt changed'
    print(f'Current official skill preview verified: {args.effect}, {len(payload)} bytes')


if __name__ == '__main__':
    main()
