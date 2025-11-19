namespace Unity.MemoryProfiler.Editor.Format
{
    /// <summary>
    /// 文件读取进度信息
    /// 用于异步加载时向UI报告进度
    /// </summary>
    public struct FileReadProgress
    {
        /// <summary>
        /// 已读取的字节数
        /// </summary>
        public long BytesRead { get; set; }
        
        /// <summary>
        /// 总字节数
        /// </summary>
        public long TotalBytes { get; set; }
        
        /// <summary>
        /// 当前正在读取的Entry类型
        /// </summary>
        public string CurrentEntryType { get; set; }
        
        /// <summary>
        /// 进度百分比 (0-100)
        /// </summary>
        public float ProgressPercentage => TotalBytes > 0 ? (float)BytesRead / TotalBytes * 100 : 0;
        
        public override string ToString()
        {
            return $"Reading {CurrentEntryType ?? "snapshot"}: {ProgressPercentage:F1}% ({BytesRead}/{TotalBytes} bytes)";
        }
    }
    
    /// <summary>
    /// Crawler爬取进度信息
    /// </summary>
    public struct CrawlProgress
    {
        /// <summary>
        /// 当前已处理的项数
        /// </summary>
        public int Current { get; set; }
        
        /// <summary>
        /// 总项数
        /// </summary>
        public int Total { get; set; }
        
        /// <summary>
        /// 当前正在处理的阶段描述
        /// </summary>
        public string Stage { get; set; }
        
        /// <summary>
        /// 进度百分比 (0-100)
        /// </summary>
        public float ProgressPercentage => Total > 0 ? (float)Current / Total * 100 : 0;
        
        public override string ToString()
        {
            return $"{Stage ?? "Crawling"}: {ProgressPercentage:F1}% ({Current}/{Total})";
        }
    }
    
    /// <summary>
    /// Model构建进度信息
    /// </summary>
    public struct BuildProgress
    {
        /// <summary>
        /// 当前阶段 (0-based)
        /// </summary>
        public int CurrentStage { get; set; }
        
        /// <summary>
        /// 总阶段数
        /// </summary>
        public int TotalStages { get; set; }
        
        /// <summary>
        /// 阶段名称
        /// </summary>
        public string StageName { get; set; }
        
        /// <summary>
        /// 当前阶段进度百分比 (0-100)
        /// </summary>
        public float StageProgress { get; set; }
        
        /// <summary>
        /// 总体进度百分比 (0-100)
        /// </summary>
        public float OverallProgress => TotalStages > 0 
            ? ((CurrentStage + StageProgress / 100f) / TotalStages) * 100 
            : 0;
        
        public override string ToString()
        {
            return $"Stage {CurrentStage + 1}/{TotalStages} ({StageName}): {OverallProgress:F1}%";
        }
    }
}

