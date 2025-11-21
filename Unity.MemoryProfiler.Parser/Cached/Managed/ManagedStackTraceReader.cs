using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Unity.MemoryProfiler.Editor.Managed
{
    /// <summary>
    /// 堆栈帧信息
    /// </summary>
    public class StackFrame
    {
        public string Module { get; set; } = string.Empty;
        public string Function { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public ulong Address { get; set; }

        public string FullDescription => $"{Module} {Function}";
    }

    /// <summary>
    /// 完整的调用栈信息
    /// </summary>
    public class CallStack
    {
        public uint Hash { get; set; }
        public List<StackFrame> Frames { get; set; } = new List<StackFrame>();

        /// <summary>
        /// 获取顶层调用函数（通常是分配点）
        /// </summary>
        public string TopFunction => Frames.Count > 0 ? Frames[0].Function : string.Empty;

        /// <summary>
        /// 获取顶层模块
        /// </summary>
        public string TopModule => Frames.Count > 0 ? Frames[0].Module : string.Empty;
    }

    /// <summary>
    /// 读取 .stacktrace.txt 文件，获取堆栈Hash到完整调用栈的映射
    /// 文件格式:
    /// #:0x5f42d7
    ///  0x00007ff935d4fa9a GameAssembly.dll il2cpp::vm::Object::NewAllocSpecific [path:line]
    ///  0x00007ff935d4f9a4 GameAssembly.dll il2cpp::vm::Object::New [path:line]
    ///  ...
    /// </summary>
    public static class ManagedStackTraceReader
    {
        public static async Task<Dictionary<uint, CallStack>> ReadAsync(string snapshotPath)
        {
            var stackTracePath = snapshotPath + ".stacktrace.txt";
            var result = new Dictionary<uint, CallStack>();

            if (!File.Exists(stackTracePath))
            {
                return result;
            }

            try
            {
                using (var reader = new StreamReader(stackTracePath))
                {
                    string? line;
                    CallStack? currentStack = null;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        var trimmed = line.Trim();

                        // 跳过注释头
                        if (trimmed.StartsWith("# StackHash"))
                            continue;

                        // 新的堆栈开始: #:0x5f42d7
                        if (trimmed.StartsWith("#:0x"))
                        {
                            // 保存上一个堆栈
                            if (currentStack != null && currentStack.Frames.Count > 0)
                            {
                                result[currentStack.Hash] = currentStack;
                            }

                            // 解析新的堆栈Hash
                            var hashStr = trimmed.Substring(4); // 跳过 "#:0x"
                            if (uint.TryParse(hashStr, System.Globalization.NumberStyles.HexNumber, null, out var hash))
                            {
                                currentStack = new CallStack { Hash = hash };
                            }
                            else
                            {
                                currentStack = null;
                            }
                        }
                        // 堆栈帧行: 0x00007ff935d4fa9a GameAssembly.dll il2cpp::vm::Object::NewAllocSpecific [path:line]
                        else if (currentStack != null && trimmed.StartsWith("0x"))
                        {
                            var frame = ParseStackFrame(trimmed);
                            if (frame != null)
                            {
                                currentStack.Frames.Add(frame);
                            }
                        }
                        // 空行，可能是堆栈结束
                        else if (string.IsNullOrEmpty(trimmed) && currentStack != null)
                        {
                            if (currentStack.Frames.Count > 0)
                            {
                                result[currentStack.Hash] = currentStack;
                            }
                            currentStack = null;
                        }
                    }

                    // 保存最后一个堆栈
                    if (currentStack != null && currentStack.Frames.Count > 0)
                    {
                        result[currentStack.Hash] = currentStack;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading stacktrace file: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 解析堆栈帧行
        /// 格式: 0x00007ff935d4fa9a GameAssembly.dll il2cpp::vm::Object::NewAllocSpecific [E:\path\file.cpp:306]
        /// </summary>
        private static StackFrame? ParseStackFrame(string line)
        {
            try
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    return null;

                var frame = new StackFrame();

                // 解析地址
                var addressStr = parts[0];
                if (addressStr.StartsWith("0x"))
                    addressStr = addressStr.Substring(2);
                ulong address;
                ulong.TryParse(addressStr, System.Globalization.NumberStyles.HexNumber, null, out address);
                frame.Address = address;

                // 模块名
                frame.Module = parts[1];

                // 函数名（可能包含多个空格分隔的部分）
                var functionStartIndex = 2;
                var functionParts = new List<string>();
                
                for (int i = functionStartIndex; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith("["))
                    {
                        // 遇到文件路径部分，停止
                        // 解析文件路径和行号
                        var pathPart = string.Join(" ", parts, i, parts.Length - i);
                        ParseFilePathAndLine(pathPart, frame);
                        break;
                    }
                    functionParts.Add(parts[i]);
                }

                frame.Function = string.Join(" ", functionParts);

                return frame;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 解析文件路径和行号
        /// 格式: [E:\path\file.cpp:306]
        /// </summary>
        private static void ParseFilePathAndLine(string pathPart, StackFrame frame)
        {
            try
            {
                // 移除 [ 和 ]
                pathPart = pathPart.Trim('[', ']');

                // 分割路径和行号
                var lastColon = pathPart.LastIndexOf(':');
                if (lastColon > 0)
                {
                    frame.FilePath = pathPart.Substring(0, lastColon);
                    var lineStr = pathPart.Substring(lastColon + 1);
                    int lineNumber;
                    int.TryParse(lineStr, out lineNumber);
                    frame.LineNumber = lineNumber;
                }
                else
                {
                    frame.FilePath = pathPart;
                }
            }
            catch
            {
                // 忽略解析错误
            }
        }
    }
}

