using System;
using System.Collections.Generic;

namespace KeyRebinder.Helpers
{
    public sealed class AutoDisposeList<T> : List<T>, IDisposable
        where T : IDisposable
    {
        private bool _disposed;

        public AutoDisposeList()
            : base() { }

        public AutoDisposeList(int capacity)
            : base(capacity) { }

        public AutoDisposeList(IEnumerable<T> collection)
            : base(collection) { }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (T disposable in this)
            {
                disposable?.Dispose();
            }

            _disposed = true;
        }
    }
}
