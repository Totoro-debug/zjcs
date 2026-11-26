"""Record the original node sources and the local fruit-tree story boundary."""
import argparse
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/home-fruit-tree-story-runtime-source-manifest.json'


def digest(path):
    return hashlib.sha256((ROOT / path).read_bytes()).hexdigest()


def build():
    upstream = 'assets/reference/home-fruit-tree-unlock-story-source-manifest.json'
    source = json.loads((ROOT / upstream).read_bytes())
    original = {path: value for path, value in source['sources'].items()
                if '/client-source/' in path}
    prefix = next(path.rsplit('/client-source/', 1)[0] for path in original)
    for relative in ('client-source/ClientRunNode.cs', 'client-source/StoryUtils.cs',
                     'common-source/PlayerServiceProxyImpl.cs'):
        path = prefix + '/' + relative
        original[path] = digest(path)
    for path, expected in original.items():
        if digest(path) != expected:
            raise ValueError('Original story source changed: ' + path)
    inputs = [upstream, 'assets/reference/home-fruit-tree-unlock-story-audio-source-manifest.json',
              'scripts/prepare_home_fruit_tree_story_runtime.py']
    runtime = [
        'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.FurnitureStories.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.FurnitureStoryWaits.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Tutorial.cs',
        'unity/Assets/Game/Runtime/Offline/HomeStoryDialog/HomeFurnitureStoryDialogNode.cs',
    ]
    validation = [
        'scripts/test_home_fruit_tree_weather.ps1',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.FruitTreeUnlock.cs',
    ]
    # This shared file also contains independent building work. Record the
    # fruit-tree mask binding member alone, keeping that work out of this port.
    shared = 'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.NativeStorage.cs'
    text = (ROOT / shared).read_text(encoding='utf-8-sig')
    start = text.index('            internal bool TryGetFurnitureMaskTarget(')
    end = text.index('\n            internal ', start + 1)
    return dict(manifest=source['manifest'], original_sources=original,
                inputs={path: digest(path) for path in inputs},
                runtime_bindings={path: digest(path) for path in runtime},
                validation_inputs={path: digest(path) for path in validation},
                runtime_members={shared + '#TryGetFurnitureMaskTarget':
                                 hashlib.sha256(text[start:end].encode()).hexdigest()})


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = build()
    if args.check:
        if json.loads(RECEIPT.read_bytes()) != result:
            raise SystemExit('Fruit-tree story runtime provenance differs')
        print('Fruit-tree story runtime provenance verified')
    else:
        RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
