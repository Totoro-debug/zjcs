"""Stage selected original furniture story voices for isolated Unity playback."""
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    proof = json.loads((ROOT / 'assets/reference/home-furniture-unlock-story-audio-source-manifest.json').read_bytes())
    project = work / 'audio-unity'
    resource = project / 'Assets/Resources/Home/FurnitureStoryAudio'
    resource.mkdir(parents=True, exist_ok=True)
    records = []
    for row in proof['entries']:
        source = ROOT / ('unity/Assets/Official/Home/FurnitureStoryAudio/Resources/Home/FurnitureStoryAudio/'
                         + str(row['media_id']) + '.wav')
        shutil.copy2(source, resource / source.name)
        shutil.copy2(Path(str(source) + '.meta'), resource / (source.name + '.meta'))
        records.append(dict(eventKey=row['eventKey'], clip=row['clip'], channels=row['format']['channels'],
                            sampleRate=row['format']['sample_rate'], frames=row['format']['frames']))
    (resource.parent / 'FurnitureStoryAudioValidation.json').write_text(
        json.dumps(dict(clips=records), ensure_ascii=False), 'utf8', newline='\n')
    editor = project / 'Assets/Editor'
    editor.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/home_furniture_story_audio_validation.cs',
                 editor / 'HomeFurnitureStoryAudioValidation.cs')
    settings = project / 'ProjectSettings'
    settings.mkdir(exist_ok=True)
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', settings / 'ProjectVersion.txt')
    packages = project / 'Packages'
    packages.mkdir(exist_ok=True)
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies={
        'com.unity.modules.audio': '1.0.0', 'com.unity.modules.jsonserialize': '1.0.0'})), 'utf8')
    print('Staged isolated original furniture story audio:', project, flush=True)


if __name__ == '__main__':
    main()
