using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// Model构建器接口，标准化所有ModelBuilder的实现模式
    /// 提供同步和异步两种构建方式，支持进度报告和取消
    /// </summary>
    /// <typeparam name="TModel">模型类型</typeparam>
    /// <typeparam name="TArgs">构建参数类型</typeparam>
    internal interface IModelBuilder<TModel, TArgs>
    {
        /// <summary>
        /// 同步构建模型
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        /// <param name="args">构建参数</param>
        /// <returns>构建的模型</returns>
        TModel Build(CachedSnapshot snapshot, TArgs args);

        /// <summary>
        /// 异步构建模型（支持进度报告和取消）
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        /// <param name="args">构建参数</param>
        /// <param name="progress">进度报告</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>构建的模型</returns>
        Task<TModel> BuildAsync(
            CachedSnapshot snapshot,
            TArgs args,
            IProgress<BuildProgress> progress = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 构建进度信息
    /// </summary>
    public struct BuildProgress
    {
        /// <summary>
        /// 当前阶段名称
        /// </summary>
        public string Stage { get; set; }

        /// <summary>
        /// 进度百分比（0-100）
        /// </summary>
        public int Percent { get; set; }

        /// <summary>
        /// 当前阶段的详细信息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 已处理的项数
        /// </summary>
        public int ProcessedItems { get; set; }

        /// <summary>
        /// 总项数
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// 已用时间
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        public override string ToString()
        {
            return $"{Stage} - {Percent}% ({ProcessedItems}/{TotalItems}) - {Message}";
        }
    }

    /// <summary>
    /// ModelBuilder基类，提供通用的异步构建模板方法
    /// </summary>
    /// <typeparam name="TModel">模型类型</typeparam>
    /// <typeparam name="TArgs">构建参数类型</typeparam>
    internal abstract class ModelBuilderBase<TModel, TArgs> : IModelBuilder<TModel, TArgs>
    {
        /// <summary>
        /// 同步构建模型（子类必须实现）
        /// </summary>
        public abstract TModel Build(CachedSnapshot snapshot, TArgs args);

        /// <summary>
        /// 异步构建模型（默认实现：在后台线程调用同步Build）
        /// 子类可以重写以提供更细粒度的进度报告
        /// </summary>
        public virtual async Task<TModel> BuildAsync(
            CachedSnapshot snapshot,
            TArgs args,
            IProgress<BuildProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;

            // 报告开始
            progress?.Report(new BuildProgress
            {
                Stage = "Building",
                Percent = 0,
                Message = "Starting build...",
                ProcessedItems = 0,
                TotalItems = 0,
                ElapsedTime = TimeSpan.Zero
            });

            // 在后台线程执行同步构建
            var model = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Build(snapshot, args);
            }, cancellationToken);

            // 报告完成
            progress?.Report(new BuildProgress
            {
                Stage = "Completed",
                Percent = 100,
                Message = "Build completed",
                ProcessedItems = 0,
                TotalItems = 0,
                ElapsedTime = DateTime.Now - startTime
            });

            return model;
        }
    }
}

