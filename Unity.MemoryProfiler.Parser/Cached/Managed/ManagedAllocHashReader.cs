using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Unity.MemoryProfiler.Editor.Managed
{
    /// <summary>
    /// 读取 .allocHash.txt 文件，获取 Managed 对象地址到堆栈Hash的映射
    /// 文件格式: MemoryPointer,StackHash
    /// 例如: 0x000001A81A301000,0x8b4ed2fd
    /// </summary>
    public static class ManagedAllocHashReader
    {
        public static async Task<Dictionary<ulong, uint>> ReadAsync(string snapshotPath)
        {
            var allocHashPath = snapshotPath + ".allocHash.txt";
            var result = new Dictionary<ulong, uint>();

            if (!File.Exists(allocHashPath))
            {
                return result;
            }

            try
            {
                using (var reader = new StreamReader(allocHashPath))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        var trimmed = line.Trim();
                        
                        // 跳过注释和空行
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        // 解析格式: 0x000001A81A301000,0x8b4ed2fd
                        var parts = trimmed.Split(',');
                        if (parts.Length != 2)
                            continue;

                        // 解析内存地址
                        var addressStr = parts[0].Trim();
                        if (addressStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            addressStr = addressStr.Substring(2);

                        if (!ulong.TryParse(addressStr, System.Globalization.NumberStyles.HexNumber, null, out var address))
                            continue;

                        // 解析堆栈Hash
                        var hashStr = parts[1].Trim();
                        if (hashStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            hashStr = hashStr.Substring(2);

                        if (!uint.TryParse(hashStr, System.Globalization.NumberStyles.HexNumber, null, out var hash))
                            continue;

                        result[address] = hash;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading allocHash file: {ex.Message}");
            }

            return result;
        }
    }
}

