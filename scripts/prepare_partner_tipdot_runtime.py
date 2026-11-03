"""Port the selected game-owned tip graph executor; no third-party implementation."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_input_dialog import metadata

SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
TARGET = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots'
NAMES = ('TipDotNodeDataRepository', 'TipDotNodeManager', 'TipDotGraphHandler',
         'TipDotSystemController', 'TipDotSystem', 'TipDotBanManager', 'ITipDotBanStrategy')


def replace(text, before, after):
    if text.count(before) != 1:
        raise ValueError('Review official source anchor: ' + before)
    return text.replace(before, after)


def block(text, marker, replacement):
    start = text.index(marker)
    opening = text.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[:start] + replacement + text[end:]


def logging(text):
    # Remove only decompiler-generated log interpolation scaffolding. Preserve
    # surrounding branches, early returns and all game state operations.
    text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
    pattern = r'(?m)^(\s*)(?:ILogger logger =|GLogger\w+InterpolatedStringHandler message =)[\s\S]*?^\s*(?:GZLoggerExtensions\.(\w+)|Log\.(\w+))\([^\n]*;'
    def diagnostic(match):
        severity = match[2] or match[3]
        if severity == 'Trace': return ''
        method = 'LogWarning' if severity in ('Warn', 'Warning') else 'LogError'
        return match[1] + 'UnityEngine.Debug.' + method + '("Official tip graph ' + severity + '");'
    text = re.sub(pattern, diagnostic, text)
    text = re.sub(r'^\s*bool flag = default;\n', '\n', text, flags=re.M)
    text = re.sub(r'^\s*\[Inject\]\n\s*private ILogger<[^\n]+\n', '\n', text, flags=re.M)
    return text


def prepare():
    outputs = []
    for name in NAMES:
        text = (SOURCE / (name + '.cs')).read_text('utf8')
        text = text[text.index('public '):]
        if name == 'TipDotNodeManager':
            text = block(text, '\t[StructLayout', '')
        text = logging(text)
        text = re.sub(r'\s*\[Inject\]\n', '\n', text)
        if name == 'TipDotNodeDataRepository':
            text = text.replace(' : ITipDotNodeDataRepository', '')
        elif name == 'TipDotNodeManager':
            text = text.replace(': ITipDotNodeManager', ': IPartnerDynamicTipDotNodeManager, IDisposable')
            text = text.replace('DisposableBag mSubscriberDisposable', 'IDisposable mSubscriberDisposable')
            text = text.replace('ITipDotNodeDataRepository', 'TipDotNodeDataRepository').replace('ITipDotNodeFactory', 'TipDotNodeFactory').replace('ITipDotGraphHandler', 'TipDotGraphHandler')
            start = text.index('\tprivate ISubscriber<TipDotScopeInitData>')
            end = text.index('\n\tprivate TipDotInfo[] AllTipDotInfos', start)
            text = text[:start] + '\tpublic object ScopeDataCollector { get; private set; }\n' + text[end:]
            text = text.replace('TipDotInfo', 'PartnerTipDotGraphInfo')
            text = text.replace('Parsers.Instance.PartnerTipDotGraphInfoParser.GetAll()', 'bindings.GetAllGraphs()')
            text = block(text, '\tpublic TipDotNodeManager()', '''\tprivate readonly PartnerTipDotRuntimeBindings bindings;
\tprivate bool disposed;
\tpublic TipDotNodeManager(TipDotNodeDataRepository repository, TipDotNodeFactory factory,
        TipDotGraphHandler graphs, PartnerTipDotRuntimeBindings bindings)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        NodeFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        GraphHandler = graphs ?? throw new ArgumentNullException(nameof(graphs));
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        PeriodicConditionNodeUpdater().Forget();
    }
    public void Dispose() { Release(); disposed = true; }''')
            start = text.index('\t\t\tmSubscriberDisposable = default;')
            end = text.index('\n\t\t}', start)
            text = text[:start] + '\t\t\tmSubscriberDisposable = bindings.SubscribeScope(InitScopeData, DisposeScopeData);' + text[end:]
            text = text.replace('private void InitScopeData(TipDotScopeInitData initData)', 'private void InitScopeData(object collector)').replace('initData.Collector', 'collector')
            text = text.replace('private void DisposeScopeData(TipDotScopeDisposeData disposeData)', 'private void DisposeScopeData()')
            text = block(text, '\t[AsyncStateMachine', '''\tprivate async UniTaskVoid PeriodicConditionNodeUpdater()
    {
        // Restored from the game's async state machine. Dispose ends only the
        // owner lifetime; Init/Release retain the original worker semantics.
        while (!disposed)
        {
            if (bindings.HasPlayer() && mStartConditionNodeUpdate && mDirtyConditionNodeSet.Count > 0)
            {
                mProcessDirtyConditionNodeList.Clear();
                mProcessDirtyConditionNodeList.AddRange(mDirtyConditionNodeSet);
                mDirtyConditionNodeSet.Clear();
                for (int i = 0; i < mProcessDirtyConditionNodeList.Count; i++)
                {
                    try { mProcessDirtyConditionNodeList[i]?.UpdateNode(); }
                    catch (Exception error) { UnityEngine.Debug.LogException(error); }
                }
                mProcessDirtyConditionNodeList.Clear();
            }
            await UniTask.DelayFrame(1, (PlayerLoopTiming)8, default(System.Threading.CancellationToken), false);
        }
    }''')
            text = text.replace('MeManager.MainPlayer == null', '!bindings.HasPlayer()')
            text = text.replace('Stopwatch stopwatch = Stopwatch.StartNew();', 'System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();')
        elif name == 'TipDotGraphHandler':
            text = text.replace(' : ITipDotGraphHandler', '')
            start = text.index('\tprivate IResourceLoader')
            end = text.index('\n\tpublic TipDotNodeGraph GetNodeGraph', start)
            text = text[:start] + '''\tprivate readonly Func<string, TipDotNodeGraph> load;
    public TipDotGraphHandler(Func<string, TipDotNodeGraph> load)
    { this.load = load ?? throw new ArgumentNullException(nameof(load)); }
''' + text[end:]
            text = re.sub(r'\s*string tipDotGraphAssetPath = TipDotSystemUtils.GetTipDotGraphAssetPath\(graphName\);', '', text)
            text = text.replace('ResourceLoader.LoadAsset<TipDotNodeGraph>(tipDotGraphAssetPath).Result', 'load(graphName)')
        elif name == 'TipDotSystemController':
            text = text.replace(' : ITipDotSystemController', ' : IProfessionTipDotSystemController')
            text = re.sub(r'\s*public ITipDotNodeEventCenter EventCenter[^\n]*', '', text)
            text = text.replace('private ITipDotNodeManager NodeManager { get; set; }', '''private ITipDotNodeManager NodeManager { get; set; }
    public TipDotSystemController(ITipDotNodeManager manager)
    { NodeManager = manager ?? throw new ArgumentNullException(nameof(manager)); }''')
        elif name == 'TipDotSystem':
            text = text.replace(': BackgroundServiceBase', ': IProfessionTipDotSystem, IDisposable')
            text = text.replace('private ITipDotNodeManager', 'private TipDotNodeManager')
            text = text.replace('protected override void Initialize()', 'public void Initialize()').replace('protected override void Dispose()', 'public void Dispose()')
            text = text.replace('\t\tbase.Initialize();', '').replace('\t\tbase.Dispose();', '')
            text = text.replace('MeManager.Instance.OnPlayerUpdate += new Action(OnPlayerUpdate);', 'playerSubscription = bindings.SubscribePlayerUpdate(OnPlayerUpdate);')
            text = text.replace('MeManager.Instance.OnPlayerUpdate -= new Action(OnPlayerUpdate);', 'playerSubscription?.Dispose(); playerSubscription = null;')
            text = text.replace('private void OnPlayerUpdate()', 'public void OnPlayerUpdate()').replace('MeManager.MainPlayer != null', 'bindings.HasPlayer()')
            text = replace(text, '\tpublic static bool sTempLimitShowTip = true;', '''\tpublic static bool sTempLimitShowTip = true;
    private readonly PartnerTipDotRuntimeBindings bindings;
    private IDisposable playerSubscription;
    public bool TempLimitShowTip => sTempLimitShowTip;
    public TipDotGraphType? GetGraphType(string name) => bindings.GetGraphType(name);
    public TipDotSystem(TipDotNodeManager manager, ITipDotBanManager bans, PartnerTipDotRuntimeBindings bindings)
    {
        NodeManager = manager ?? throw new ArgumentNullException(nameof(manager));
        TipDotBanManager = bans ?? throw new ArgumentNullException(nameof(bans));
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }''')
        if any(token in text for token in ('[Inject]', 'ILogger', 'ZLogger', 'MeManager', 'Parsers.', 'ResourceLoader', 'Framework.', 'MessagePipe', 'AsyncStateMachine')):
            raise ValueError('Unmapped dependency: ' + name)
        text = '// Selected official game implementation; see partner-tipdot-runtime-source-manifest.json.\n' + \
            'using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing Cysharp.Threading.Tasks;\nusing UnityEngine;\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.ProfessionUI;\nnamespace Zjcs.Official.PartnersUI\n{\n' + text.rstrip() + '\n}\n'
        text = re.sub(r'\n[ \t]*\n(?:[ \t]*\n)+', '\n\n', text)
        path = TARGET / (name + '.cs')
        path.write_text(text, 'utf8', newline='\n')
        outputs.extend((path, metadata(path)))
    binding = TARGET / 'PartnerTipDotRuntimeBindings.cs'
    outputs.extend((binding, metadata(binding)))
    for path in outputs:
        if path.suffix == '.meta':
            path.write_text('\n'.join(line.rstrip() for line in path.read_text('utf8').splitlines()) + '\n', 'utf8', newline='\n')
    receipt = dict(manifest='884_156692', inputs={(SOURCE / (name + '.cs')).relative_to(ROOT).as_posix(): hashlib.sha256((SOURCE / (name + '.cs')).read_bytes()).hexdigest() for name in NAMES},
        outputs={path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in outputs},
        operation=['Direct game-source migration: repository, graph cache, graph manager, UI controller, lifecycle and ordered ban strategies.',
            'Replace DI, structured logging, player/config/resource lookup and event subscriptions with explicit owner bindings; retain branches, graph traversal and execution order.',
            'Restore the game async dirty-node state machine to async UniTaskVoid with original DelayFrame(1, timing 8). Dispose terminates the owner worker.',
            'Reuse the existing original node factory and condition nodes; no third-party SDK implementation is copied.'])
    receipt['helper_sha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    receipt['validation_helpers'] = {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in
        ('scripts/fixtures/partner_tipdot_runtime_validation.cs', 'scripts/prepare_partner_tipdot_runtime_validation.py')}
    receipt['dependencies'] = {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in
        ('assets/reference/partner-tipdot-source-manifest.json', 'assets/reference/partner-panel-native-source-manifest.json')}
    (ROOT / 'assets/reference/partner-tipdot-runtime-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared', len(NAMES), 'selected official tip graph runtime files')


if __name__ == '__main__': prepare()
