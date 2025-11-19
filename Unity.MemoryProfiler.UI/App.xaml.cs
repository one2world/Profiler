using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace Unity.MemoryProfiler.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 解析命令行参数
        string? snapshotPath = null;
        string? compareSnapshotPath = null;
        
        if (e.Args.Length >= 2)
        {
            // 传递了两个参数，进入比较模式
            snapshotPath = e.Args[0];
            compareSnapshotPath = e.Args[1];
        }
        else if (e.Args.Length == 1)
        {
            // 只传递了一个参数，正常加载
            snapshotPath = e.Args[0];
        }
        else
        {
            // 没有参数，使用默认快照（或尝试自动对比）
            var defaultSnapPath1 = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                @"..\..\..\..\MemoryCaptures\00.snap");
            
            var defaultSnapPath2 = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                @"..\..\..\..\MemoryCaptures\11.snap");
            
            // 如果两个测试快照都存在，自动进入对比模式
            if (File.Exists(defaultSnapPath1) && File.Exists(defaultSnapPath2))
            {
                snapshotPath = Path.GetFullPath(defaultSnapPath1);
                compareSnapshotPath = Path.GetFullPath(defaultSnapPath2);
            }
            else
            {
                // 否则尝试加载单个默认快照
                var defaultSnapPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    @"..\..\..\..\MemoryCaptures\unity-2023.2.2-00000000000000000.snap");
                
                if (File.Exists(defaultSnapPath))
                {
                    snapshotPath = Path.GetFullPath(defaultSnapPath);
                }
            }
        }
        
        // 传递snap路径到MainWindow
        var mainWindow = new MainWindow(snapshotPath, compareSnapshotPath);
        mainWindow.Show();
    }
}

