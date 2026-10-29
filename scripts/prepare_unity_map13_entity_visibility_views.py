"""Restore original visibility/interaction views on explicit local data readers.

The readers consume catalog/ownership/interaction state. They do not create
server results. Original event dispatch and presentation branches are retained.
"""
from pathlib import Path
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record, method
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_map13_prop_parts import meta

NS = 'Zjcs.Official.World.Map13EntityVisibilityViews'
DEST = ROOT / ('unity/Assets/Game/Runtime/Official/World/Map13EntityVisibilityViews')
RECEIPT = ROOT / 'assets/reference/official-map/map13-entity-visibility-views-source-manifest.json'


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / n for n in ('Assembly-CSharp.dll', 'Console.dll')]
    for p in assemblies:
        assert sha(p.read_bytes()) == manifest['assemblies'][p.name]['decoded_sha256'], p
    inputs, outputs = [], []

    def source(name, common=False):
        p = CODE / ('common-source/Common' if common else 'client-source') / (name + '.cs')
        inputs.append(p)
        return p.read_text(encoding='utf-8-sig')

    def emit(name, body, imports=''):
        p = DEST / (name + '.cs')
        body = re.sub(r'(?m)^using (?:Common|Framework(?:\.[\w.]+)?);\n', '', body)
        body = body.replace('namespace Common;', '')
        header, _, code = body.partition('public ')
        attributes = ''.join(re.findall(r'(?m)^\[[^\n]+\]\n', header))
        header = re.sub(r'(?m)^\[[^\n]+\]\n', '', header)
        text = ('// Migrated original application/read-query code; see Map13 visibility-view receipt.\n'
                + imports + header + '\nnamespace ' + NS + '\n{\n' + attributes + 'public ' + code + '\n}\n')
        text = '\n'.join(line.rstrip() for line in text.splitlines()) + '\n'
        write(p, text.encode()); meta(p, 'MonoImporter')
        outputs.extend([p, Path(str(p) + '.meta')])

    common_imports = ('using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
                      'using Zjcs.Official.StorySourceFade;\n')
    for name in ['ECClientVisibilityView', 'ECEntityInteractionShowView']:
        text, _ = logging_adapter(source(name))
        text = text.replace('public class ', '[Serializable]\npublic class ', 1)
        if name == 'ECClientVisibilityView':
            text = re.sub(r'\bMyEntity\b', 'ECViewEntityBinding', text)
            text = text.replace('[Inject]\n\tprivate IEntityClientVisibilityHandlerFactory EntityClientVisibilityHandlerFactory { get; set; }',
                                'public IEntityClientVisibilityHandlerFactory EntityClientVisibilityHandlerFactory { get; set; }')
            text = text.replace('mMyEntity.ClientVisibilitySupporter?',
                                '((IMap13EntityVisibilityBinding)mMyEntity).ClientVisibilitySupporter?')
            text = re.sub(r'\bVisibilityComponent\b', 'Map13VisibilityData', text)
            # Restore the original receiver method/interface spelling after the
            # component type substitution, so source event dispatch stays exact.
            text = text.replace('EntityClientMap13VisibilityData_Updated', 'EntityClientVisibilityComponent_Updated')
        else:
            text = re.sub(r'\bEntitySourceComponent\b', 'Map13EntitySourceQuery', text)
            text = re.sub(r'\bEntityPlayerInteractionComponent\b', 'Map13EntityInteractionQuery', text)
            text = text.replace('EntityPlayerInteractionComponent_Update', 'EntityPlayerInteractionComponent_Update')
            text = text.replace('Map13EntityInteractionQuery_Update', 'EntityPlayerInteractionComponent_Update')
            text = text.replace('MeManager.MainPlayer.Id', 'Map13PresentationServices.For(mServiceProvider).PlayerId')
            text = text.replace('TimeManager.Instance', 'Map13PresentationServices.For(mServiceProvider)')
        emit(name, text, ('using System;\n' if name == 'ECEntityInteractionShowView' else '') + common_imports
             + 'using Zjcs.Official.World.Map13EntityRendering;\n')

    for name in ['ClientVisibilityType', 'EcEntitySourceType', 'EntityOwnershipType']:
        emit(name, source(name, True))
    emit('EntitySourceTypeShowNode', source('EntitySourceTypeShowNode'))
    emit('IMyVisibility', source('IMyVisibility'))
    for name in ['IClientVisibilityHandler', 'IEntityClientVisibilityHandlerFactory']:
        emit(name, source(name, name == 'IClientVisibilityHandler').replace('EcEntity ', 'ECViewEntityData '), common_imports)

    for name in ['EcEntityClientVisibilityUpdatedEventDelegate', 'EcEntityPlayerInteractionUpdateEventDelegate']:
        text = source(name, True).replace('VisibilityComponent', 'Map13VisibilityData').replace('EntityPlayerInteractionComponent', 'Map13EntityInteractionQuery')
        text = text.replace('EntityClientMap13VisibilityData_Updated', 'EntityClientVisibilityComponent_Updated')
        text = text.replace('Map13EntityInteractionQuery_Update', 'EntityPlayerInteractionComponent_Update')
        emit(name, text, 'using Zjcs.Official.World.Map13EntityEvents;\n')

    for name in ['IEcEntityClientVisibilityUpdatedEventReceiver', 'IEcEntityPlayerInteractionUpdateEventReceiver']:
        text = source(name, True)
        text = re.sub(r'\bVisibilityComponent\b', 'Map13VisibilityData', text)
        text = re.sub(r'\bEntityPlayerInteractionComponent\b', 'Map13EntityInteractionQuery', text)
        emit(name, text, 'using Zjcs.Official.World.Map13EntityEvents;\n')

    emit('PresentationBoundaries', '''using System;
using System.Collections.Generic;
using Zjcs.Official.EntityViews;
public interface IMap13EntityVisibilityBinding
{ IClientVisibilitySupporter ClientVisibilitySupporter { get; } }
public sealed class Map13OwnershipData { public EntityOwnershipType Type; public long OwnerId; }
public sealed class Map13EntityDurationData { public float DurationWhenExplore; public int EntityDuration; }
public sealed class Map13WorldActivityData { public int ActivityClassId; }
public abstract class Map13PresentationServices
{
    public abstract long PlayerId { get; }
    public abstract DateTime GetDateTime();
    public static Map13PresentationServices For(IServiceProvider provider) =>
        provider?.GetService(typeof(Map13PresentationServices)) as Map13PresentationServices
        ?? throw new InvalidOperationException("Original presentation player/time service is not bound.");
''' + method(source('TimeManager', True), 'public float CalcEndIntervalS(') + '\n}\n')

    text = source('VisibilityComponent', True)
    emit('Map13VisibilityData', '''using System;
public class Map13VisibilityData
{
    public EcEntityClientVisibilityUpdatedEventDelegate OnClientVisibilityUpdate = new EcEntityClientVisibilityUpdatedEventDelegate();
    public virtual ClientVisibilityType ClientVisibilityType { get; set; }
    public virtual bool IsShowNeedFadeIn { get; set; }
''' + '\n'.join(method(text, sig).replace('public void ', 'public virtual void ', 1) for sig in ['public void AddClientVisibilityRequest(', 'public void RemoveClientVisibilityType(']) + '\n}\n')

    text = source('EntitySourceComponent', True)
    queries = '\n'.join(method(text, sig) for sig in ['public bool IsMine(', 'public float GetDuration(',
                        'private float GetExploreEntityDuration(', 'private float GetActivityEntityDuration('])
    queries = queries.replace('Parsers.Instance.EntityExtendParameterInfoParser.Get(Entity.ClassId)', 'EntityParameters(Entity.ClassId)')
    queries = queries.replace('GetComponent<WorldActivityEntityComponent>()', 'Activity')
    queries = queries.replace('Parsers.Instance.MapActivityEntityInfoParser.Get(component.ActivityClassId)', 'ActivityParameters(component.ActivityClassId)')
    queries = queries.replace('EntityExtendParameterInfo', 'Map13EntityDurationData').replace('WorldActivityEntityComponent', 'Map13WorldActivityData').replace('MapActivityEntityInfo', 'Map13EntityDurationData')
    emit('Map13EntitySourceQuery', '''using System;
using Zjcs.Official.EntityViews;
public sealed class Map13EntitySourceQuery
{
    public ECViewEntityData Entity;
    public EcEntitySourceType EntitySourceType { get; set; }
    public Map13OwnershipData Ownership { get; set; } = new Map13OwnershipData();
    public DateTime? CreateTime { get; set; }
    public Map13WorldActivityData Activity;
    public Func<int, Map13EntityDurationData> EntityParameters, ActivityParameters;
''' + queries + '\n}\n')

    text = source('EntityPlayerInteractionComponent', True)
    queries = '\n'.join(method(text, sig) for sig in ['public bool CanInteract(long ', 'public bool IsPlayerInteracting(long '])
    emit('Map13EntityInteractionQuery', '''using System.Collections.Generic;
public sealed class Map13EntityInteractionQuery
{
    public EcEntityPlayerInteractionUpdateEventDelegate OnInteractingPlayerUpdate = new EcEntityPlayerInteractionUpdateEventDelegate();
    // Exact source parser fields must be supplied by the catalog host.
    public bool IsInteractionUnique, IsInteractionByOther;
    public Map13EntitySourceQuery mEntSrcComp;
    public HashSet<long> InteractingPlayerIdSet { get; set; } = new HashSet<long>();
''' + queries + '\n}\n')
    asm = DEST / (NS + '.asmdef')
    write(asm, encode(dict(name=NS, rootNamespace=NS, references=[
        'Zjcs.Official.EntityViews', 'Zjcs.Official.StorySourceFade', 'Zjcs.Official.StoryPlayables',
        'Zjcs.Official.World.Map13EntityRendering', 'Zjcs.Official.World.Map13EntityEvents'], autoReferenced=True)))
    meta(asm, 'AssemblyDefinitionImporter'); meta(DEST, folder=True)
    outputs.extend([asm, Path(str(asm) + '.meta'), Path(str(DEST) + '.meta')])
    reused = []
    for name in ['map14-entity-view-runtime-source-manifest.json', 'map14-story-source-fade-runtime-source-manifest.json',
                 'map13-entity-rendering-source-manifest.json', 'map13-entity-events-source-manifest.json']:
        p = RECEIPT.with_name(name); receipt = json.loads(p.read_bytes()); inputs.append(p)
        for output in receipt['outputs']:
            q = ROOT / output['path']; assert sha(q.read_bytes()) == output['sha256'], q
            inputs.append(q)
        reused.append(record(p))
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=13,
        method='Copy complete original visibility and interaction presentation views; preserve original event delegates, state/read-query branches and clock calculation. Adapt namespace, diagnostics, explicit player/time/catalog/entity-boundary injection. No Spine runtime code is used.',
        coverage='Presentation and consumption-only ownership/duration/interaction/visibility data. Visibility handlers/factory implementation, formal entity/UI/state host, map input/save and Player remain pending.',
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sorted(set(inputs))],
        outputs=[record(p) for p in outputs], tools=[record(Path(__file__).resolve())], reused_application_ports=reused)))
    print('Migrated original visibility/interaction views:', len(outputs), 'outputs')


if __name__ == '__main__':
    build()
