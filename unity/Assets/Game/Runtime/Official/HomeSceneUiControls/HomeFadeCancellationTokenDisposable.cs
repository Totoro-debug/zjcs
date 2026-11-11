using System;
using System.Threading;
namespace Zjcs.Official.HomeSceneUiControls
{
    // MessagePipe.CancellationTokenDisposable, verified against selected AOT:
    // .ctor creates CTS; Token returns CTS.Token; Dispose calls Cancel then
    // Dispose, in this order. It neither clears the field nor guards repeats.
    public sealed class HomeFadeCancellationTokenDisposable:IDisposable
    {
        readonly CancellationTokenSource cancellationTokenSource=new CancellationTokenSource();
        public CancellationToken Token=>cancellationTokenSource.Token;
        public void Dispose(){cancellationTokenSource.Cancel();cancellationTokenSource.Dispose();}
    }
}
