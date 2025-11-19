using System;

namespace Unity.Profiling
{
    public readonly struct ProfilerMarker
    {
        public ProfilerMarker(string name) { }

        public AutoScope Auto() => new AutoScope();

        public readonly struct AutoScope : IDisposable
        {
            public void Dispose() { }
        }
    }

    public readonly struct ProfilerMarker<T>
    {
        public ProfilerMarker(string name, string unitName = "") { }

        public AutoScope Auto(T value = default) => new AutoScope();

        public readonly struct AutoScope : IDisposable
        {
            public void Dispose() { }
        }
    }
}

