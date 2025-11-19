using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.MemoryProfiler.Editor.Format;
using Unity.MemoryProfiler.Editor.Format.QueriedSnapshot;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// 快照文件扫描器
    /// 扫描目录并解析snap文件的元数据
    /// </summary>
    public static class SnapshotScanner
    {
        /// <summary>
        /// 扫描指定目录下的所有.snap文件（仅文件系统信息，不读取快照内容）
        /// </summary>
        /// <param name="directory">目录路径</param>
        /// <returns>快照文件列表</returns>
        public static List<SnapshotFileModel> ScanDirectory(string directory)
        {
            var snapshots = new List<SnapshotFileModel>();

            if (!Directory.Exists(directory))
                return snapshots;

            foreach (var file in Directory.GetFiles(directory, "*.snap", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    
                    // ✅ 只读取文件系统信息，不打开快照内容
                    // 避免FileReader初始化导致的堆损坏问题
                    snapshots.Add(new SnapshotFileModel
                    {
                        FullPath = file,
                        Name = Path.GetFileNameWithoutExtension(file),
                        Date = fileInfo.LastWriteTime,
                        Size = fileInfo.Length,
                        SessionGUID = 0, // 不读取，所有快照在同一Session
                        ProductName = "", // 不读取
                        Platform = "",
                        UnityVersion = ""
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SnapshotScanner] 读取文件信息失败: {file}, 错误: {ex.Message}");
                }
            }

            // 按日期降序排序（最新的在前）
            return snapshots.OrderByDescending(s => s.Date).ToList();
        }

        /// <summary>
        /// 按Session分组快照（简化版：所有快照在一个组）
        /// </summary>
        public static List<SnapshotSessionGroup> GroupBySession(List<SnapshotFileModel> snapshots)
        {
            if (snapshots.Count == 0)
                return new List<SnapshotSessionGroup>();

            // ✅ 简化版：所有快照放在一个默认Session组
            // 因为不读取SessionGUID，无法真正分组
            return new List<SnapshotSessionGroup>
            {
                new SnapshotSessionGroup
                {
                    SessionGUID = 0,
                    SessionName = "All Snapshots",
                    Snapshots = new System.Collections.ObjectModel.ObservableCollection<SnapshotFileModel>(
                        snapshots.OrderByDescending(s => s.Date))
                }
            };
        }
    }
}

