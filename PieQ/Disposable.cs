using System;

namespace PieQ
{
    public class Disposable : IDisposable
    {
        private readonly Action _dispose;

        public Disposable(Action begin, Action dispose)
        {
            _dispose = dispose;
            begin();
        }

        public void Dispose()
        {
            _dispose();
        }
    }
}