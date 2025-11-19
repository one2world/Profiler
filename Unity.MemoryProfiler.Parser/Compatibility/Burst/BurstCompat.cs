using System;

namespace Unity.Burst
{
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class BurstCompileAttribute : Attribute
    {
        public bool CompileSynchronously { get; set; }
        public bool DisableDirectCall { get; set; }
        public bool DisableSafetyChecks { get; set; }
        public bool Debug { get; set; }

        public BurstCompileAttribute() { }
    }

    public enum OptimizeFor
    {
        Performance,
        Size
    }
}

