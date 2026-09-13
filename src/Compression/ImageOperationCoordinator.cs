using System.Threading;

namespace MadsKristensen.ImageOptimizer
{
    internal static class ImageOperationCoordinator
    {
        private static int _isRunning;

        internal static bool TryStart(out IDisposable lease)
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                lease = null;
                return false;
            }

            lease = new OperationLease();
            return true;
        }

        private sealed class OperationLease : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    Volatile.Write(ref _isRunning, 0);
                }
            }
        }
    }
}
