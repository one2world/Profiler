// ============================================================================
// Unity 官方 API 等效实现
// 
// 官方命名空间: Unity.Jobs
// 官方类型: JobHandle, IJob, IJobParallelFor
// 官方包版本: com.unity.jobs (Unity Engine 内置)
// 
// 实现说明:
// - Unity 的 Job System 用于多线程并行计算，基于 Burst 编译器优化
// - 官方 JobHandle 是一个轻量级的句柄，用于跟踪和同步作业
// - 本实现使用 Task 作为底层异步机制，但为了保持 unmanaged 约束，
//   JobHandle 不直接持有 Task 引用，而是使用状态标志
// - IJobParallelFor 使用 Parallel.ForEach 实现并行执行
// 
// 与官方的差异:
// - 使用 Task 而非 Unity 的原生线程池
// - 移除了 Burst 编译优化
// - JobHandle 简化为状态标志，不持有 Task 引用（保持 unmanaged）
// - 保持了核心 API 的行为一致性（Schedule、Complete 等）
// ============================================================================

using System;
using System.Threading.Tasks;

namespace Unity.Jobs
{
    /// <summary>
    /// 作业句柄（.NET 等效实现，简化为状态标志以保持 unmanaged 约束）
    /// </summary>
    public readonly struct JobHandle
    {
        // 为了保持 unmanaged 约束，不能持有 Task 引用
        // 使用简单的完成标志
        internal readonly bool IsCompleted;

        internal JobHandle(bool isCompleted)
        {
            IsCompleted = isCompleted;
        }

        /// <summary>
        /// 等待作业完成（.NET 实现中立即返回，因为我们使用同步执行）
        /// </summary>
        public void Complete()
        {
            // 在 .NET 实现中，作业已经同步执行完成
        }

        /// <summary>
        /// 组合多个作业句柄
        /// </summary>
        public static JobHandle CombineDependencies(JobHandle job1, JobHandle job2)
        {
            return new JobHandle(job1.IsCompleted && job2.IsCompleted);
        }

        /// <summary>
        /// 组合多个作业句柄
        /// </summary>
        public static JobHandle CombineDependencies(params JobHandle[] jobs)
        {
            foreach (var job in jobs)
            {
                if (!job.IsCompleted)
                    return new JobHandle(false);
            }
            return new JobHandle(true);
        }
    }

    /// <summary>
    /// 单线程作业接口
    /// </summary>
    public interface IJob
    {
        void Execute();
    }

    /// <summary>
    /// 并行作业接口
    /// </summary>
    public interface IJobParallelFor
    {
        void Execute(int index);
    }

    /// <summary>
    /// 作业调度扩展方法
    /// </summary>
    public static class IJobExtensions
    {
        /// <summary>
        /// 调度单线程作业（.NET 实现中同步执行）
        /// </summary>
        public static JobHandle Schedule<T>(this T jobData, JobHandle dependsOn = default) where T : struct, IJob
        {
            dependsOn.Complete();
            jobData.Execute();
            return new JobHandle(true);
        }
    }

    /// <summary>
    /// 并行作业调度扩展方法
    /// </summary>
    public static class IJobParallelForExtensions
    {
        /// <summary>
        /// 调度并行作业（.NET 实现中使用 Parallel.For）
        /// </summary>
        public static JobHandle Schedule<T>(this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default) where T : struct, IJobParallelFor
        {
            dependsOn.Complete();
            
            // 使用 Parallel.For 实现并行执行
            Parallel.For(0, arrayLength, index =>
            {
                jobData.Execute(index);
            });
            
            return new JobHandle(true);
        }

        /// <summary>
        /// 调度并行作业（简化版本）
        /// </summary>
        public static JobHandle Schedule<T>(this T jobData, int arrayLength, JobHandle dependsOn = default) where T : struct, IJobParallelFor
        {
            return Schedule(jobData, arrayLength, 64, dependsOn);
        }

        /// <summary>
        /// 按引用调度并行作业（.NET 实现中与 Schedule 相同，因为 C# 的 struct 默认按值传递）
        /// Unity 官方: 用于避免大型 struct 的复制开销
        /// .NET 实现: 简化为调用 Schedule，因为我们使用 Task 而非 Unity 的原生作业系统
        /// </summary>
        public static JobHandle ScheduleByRef<T>(this ref T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default) where T : struct, IJobParallelFor
        {
            return jobData.Schedule(arrayLength, innerloopBatchCount, dependsOn);
        }

        /// <summary>
        /// 按引用调度并行作业（简化版本）
        /// </summary>
        public static JobHandle ScheduleByRef<T>(this ref T jobData, int arrayLength, JobHandle dependsOn = default) where T : struct, IJobParallelFor
        {
            return jobData.Schedule(arrayLength, 64, dependsOn);
        }
    }


    /// <summary>
    /// 调度参数
    /// </summary>
    public struct ScheduleParams
    {
        public JobHandle Dependency;
        public ScheduleMode ScheduleMode;
    }

    /// <summary>
    /// 调度模式
    /// </summary>
    public enum ScheduleMode
    {
        Run,
        Parallel,
        Single
    }

    /// <summary>
    /// JobHandle 扩展方法
    /// </summary>
    public static class JobHandleExtensions
    {
        public static JobHandle CombineDependencies(this JobHandle job, JobHandle other)
        {
            return JobHandle.CombineDependencies(job, other);
        }
    }

    /// <summary>
    /// Job 静态类
    /// </summary>
    public static class Job
    {
        public static JobHandle Schedule<T>(T jobData, JobHandle dependsOn = default) where T : struct, IJob
        {
            return jobData.Schedule(dependsOn);
        }
    }
}

namespace Unity.Jobs.LowLevel.Unsafe
{
    /// <summary>
    /// 低级作业工具类（.NET 等效实现）
    /// </summary>
    public static class JobsUtility
    {
        public static int JobWorkerCount => Environment.ProcessorCount;
        public static int MaxJobThreadCount => Environment.ProcessorCount;
        public static bool IsExecutingJob => false;
        
        /// <summary>
        /// CPU 缓存行大小（.NET 实现中固定为 64 字节，这是现代 CPU 的标准值）
        /// </summary>
        public static int CacheLineSize => 64;
    }
}
