using System;
using System.Threading;
namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
public sealed class CancellationDisposable : IDisposable
{
    readonly CancellationTokenSource cancellationTokenSource;
    public CancellationDisposable(CancellationTokenSource cancellationTokenSource) { this.cancellationTokenSource=cancellationTokenSource; }
    public CancellationDisposable()
        : this(new CancellationTokenSource())
    {
    }

    public CancellationToken Token => cancellationTokenSource.Token;

    public bool IsDisposed => cancellationTokenSource.IsCancellationRequested;

    public void Dispose() => cancellationTokenSource.Cancel();
}

}
