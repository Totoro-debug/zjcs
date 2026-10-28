"""Copy original entity fade-out destruction handlers with explicit map/owner reads."""
from pathlib import Path
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_map13_prop_parts import meta

NS = 'Zjcs.Official.World.Map13EntityFadeHandlers'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13EntityFadeHandlers'
RECEIPT = ROOT / 'assets/reference/official-map/map13-entity-fade-handlers-source-manifest.json'


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ['Assembly-CSharp.dll', 'GameFramework.Runtime.dll']]
    for assembly in assemblies:
        assert sha(assembly.read_bytes()) == manifest['assemblies'][assembly.name]['decoded_sha256']
    inputs, outputs = [], []

    def emit(name, text, imports=''):
        text = re.sub(r'(?m)^using (?:GameFramework|Framework(?:\.[\w.]+)?|Microsoft\.Extensions\.DependencyInjection);\n', '', text)
        text = text.replace('namespace GameFramework;', '')
        match = re.search(r'(?m)^public ', text)
        text = ('// Migrated original application fade handler; see Map13 source receipt.\n' + imports
                + text[:match.start()] + '\nnamespace ' + NS + '\n{\n' + text[match.start():] + '\n}\n')
        p = DEST / (name + '.cs'); write(p, ('\n'.join(l.rstrip() for l in text.splitlines()) + '\n').encode())
        meta(p, 'MonoImporter'); outputs.extend([p, Path(str(p) + '.meta')])

    imports = ('using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
               'using Zjcs.Official.EntityViewPlayables;\nusing Zjcs.Official.StoryPlayables;\n')
    for name in ['EcEntityFadeOutEffectHandlerBase', 'EcMapEntityFadeOutPlayableHandlerBase', 'EcMapEntityFadeOutDelayDestoryHandler']:
        p = CODE / 'client-source' / (name + '.cs'); inputs.append(p)
        text, _ = logging_adapter(p.read_text(encoding='utf-8-sig'))
        text = re.sub(r'\bMyEntity\b', 'IMap13EntityFadeOutBinding', text)
        text = text.replace('mSrcGo.GetComponent<IMap13EntityFadeOutBinding>()',
                            '(IMap13EntityFadeOutBinding)mSrcGo.GetComponent<ECViewEntityBinding>()')
        text = text.replace('ServiceProviderServiceExtensions.GetService<IMapRootManagerProxy>(ServiceProvider)',
                            '(IMap13FadeMapState)ServiceProvider.GetService(typeof(IMap13FadeMapState))')
        text = text.replace('IMapRootManagerProxy service', 'IMap13FadeMapState service')
        text = text.replace('LoadingPanelManager.Instance', 'service.LoadingPanel')
        emit(name, text, imports)
        # Keep serialization annotations inside the migrated namespace.
        target = DEST / (name + '.cs')
        text = target.read_text(encoding='utf-8')
        text = re.sub(r'(?m)^(public (?:abstract )?class )', r'[System.Serializable]\n\1', text, count=1)
        write(target, text.encode())

    p = CODE / 'gameframework-source/GameFramework/SplashStatus.cs'; inputs.append(p)
    emit('SplashStatus', p.read_text(encoding='utf-8-sig'))
    emit('FadePresentationBoundaries', '''using System;
public interface IMap13EntityFadeOutBinding
{
    // Original MyEntity multicast delegate, including its original last-result
    // semantics. The local entity host owns dispatch and actual destruction.
    Func<bool> FadeOutAction { get; set; }
}
public interface IMap13FadeMapState
{
    bool Initialized { get; }
    bool IsFront { get; }
    Map13LoadingPanelState LoadingPanel { get; }
}
public sealed class Map13LoadingPanelState
{
    public SplashStatus SplashStatus;
}
''')
    asm = DEST / (NS + '.asmdef')
    write(asm, encode(dict(name=NS, rootNamespace=NS, references=[
        'Zjcs.Official.EntityViews', 'Zjcs.Official.EntityViewPlayables', 'Zjcs.Official.StoryPlayables'], autoReferenced=True)))
    meta(asm, 'AssemblyDefinitionImporter'); meta(DEST, folder=True)
    outputs.extend([asm, Path(str(asm) + '.meta'), Path(str(DEST) + '.meta')])
    for name in ['map14-entity-playable-views-source-manifest.json', 'map14-entity-view-runtime-source-manifest.json',
                 'map14-story-playable-runtime-source-manifest.json']:
        p = RECEIPT.with_name(name)
        if not p.is_file():
            raise ValueError('Original reused application receipt is absent: ' + name)
        inputs.append(p)
        for entry in json.loads(p.read_bytes())['outputs']:
            q = ROOT / entry['path']; assert sha(q.read_bytes()) == entry['sha256'], q
            inputs.append(q)
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=13,
        method='Copy all three complete original fade-out effect/playable/delayed-destruction handler classes and original SplashStatus enum. Preserve original multicast hook ordering, map-front/splash gates, visibility, play/stop state, native Time.time deadlines and append-playable transition; adapt namespace, diagnostics and required owner/map consumption interfaces.',
        coverage='Source application handlers only. Real original entity/playable prefab binding, map lifetime/destruction host, map input/save and Windows Player remain pending.',
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sorted(set(inputs))],
        outputs=[record(p) for p in outputs], tools=[record(Path(__file__).resolve())])))
    print('Migrated original entity fade-out handlers:', len(outputs), 'outputs')


if __name__ == '__main__':
    build()
