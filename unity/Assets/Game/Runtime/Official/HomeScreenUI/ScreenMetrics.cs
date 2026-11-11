// Selected original screen adaptation; local platform/service boundaries only.
using System;
using UnityEngine;
namespace Zjcs.Official.HomeScreenUI
{
    // Windows follows AndroidSafeArea's uninitialized/non-Android Screen branch.
    // A scoped metrics port also permits rendering checks without changing the OS display.
    public interface IScreenMetrics
    {
        int Width { get; }
        int Height { get; }
        ScreenOrientation Orientation { get; }
        Rect SafeArea { get; }
    }
    public static class ScreenMetrics
    {
        private sealed class Native : IScreenMetrics
        {
            public int Width => Screen.width;
            public int Height => Screen.height;
            public ScreenOrientation Orientation => Screen.orientation;
            public Rect SafeArea => Screen.safeArea;
        }
        private static IScreenMetrics current = new Native();
        public static int width => current.Width;
        public static int height => current.Height;
        public static ScreenOrientation orientation => current.Orientation;
        public static Rect SafeArea => current.SafeArea;
        public static IDisposable Bind(IScreenMetrics metrics)
        {
            var previous=current;
            current=metrics ?? throw new ArgumentNullException(nameof(metrics));
            return new Binding(previous,current);
        }
        private sealed class Binding : IDisposable
        {
            private IScreenMetrics previous, bound;
            public Binding(IScreenMetrics previous,IScreenMetrics bound) { this.previous=previous;this.bound=bound; }
            public void Dispose()
            {
                if(bound==null)return;
                if(!ReferenceEquals(current,bound))throw new InvalidOperationException("Release screen bindings in reverse order.");
                current=previous;previous=null;bound=null;
            }
        }
    }
}
