using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Asaki.Editor.Utilities.Tools.Logging
{
    /// <summary>
    /// 日志瘦身核心算法
    /// <para>原理：合并分散的 $INC 指令，将 N 条增量记录压缩为 1 条。</para>
    /// </summary>
    public static class AsakiLogOptimizer
    {
        public struct OptimizationResult
        {
            public int OriginalLines;
            public int OptimizedLines;
            public long OriginalSize;
            public long OptimizedSize;
            public string OutputPath;
        }

        public static OptimizationResult Process(string srcPath)
        {
            if (!File.Exists(srcPath)) throw new FileNotFoundException(srcPath);

            var result = new OptimizationResult
            {
                OriginalSize = new FileInfo(srcPath).Length,
                OriginalLines = 0
            };

            // === 1. 内存聚合 (Memory Aggregation) ===
            // 保持 ID 的出现顺序，以便输出时大致符合时间流
            var idOrder = new List<int>();
            // ID -> 完整的 $DEF 行文本
            var defLines = new Dictionary<int, string>();
            // ID -> 总计数 (初始为 0，遇到 DEF 设为 1，遇到 INC 累加)
            var counts = new Dictionary<int, int>();
            
            var headers = new StringBuilder();

            using (var reader = new StreamReader(srcPath, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    result.OriginalLines++;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // 保留头部元数据 (#VERSION, #SESSION)
                    if (line.StartsWith("#"))
                    {
                        headers.AppendLine(line);
                        continue;
                    }

                    // 处理定义行
                    if (line.StartsWith("$DEF|"))
                    {
                        // 格式: $DEF|ID|...
                        int firstPipe = 4; // "$DEF".Length
                        int secondPipe = line.IndexOf('|', firstPipe + 1);
                        if (secondPipe > -1)
                        {
                            if (int.TryParse(line.Substring(firstPipe + 1, secondPipe - firstPipe - 1), out int id))
                            {
                                if (!defLines.ContainsKey(id))
                                {
                                    idOrder.Add(id);
                                    defLines[id] = line;
                                    counts[id] = 1; // DEF 本身代表 1 次
                                }
                            }
                        }
                    }
                    // 处理增量行
                    else if (line.StartsWith("$INC|"))
                    {
                        // 格式: $INC|ID|Count
                        var parts = line.Split('|'); // 为了简单直接 split，工具类性能要求相对宽松
                        if (parts.Length >= 3 && int.TryParse(parts[1], out int id) && int.TryParse(parts[2], out int inc))
                        {
                            if (counts.ContainsKey(id))
                            {
                                counts[id] += inc;
                            }
                        }
                    }
                }
            }

            // === 2. 写入新文件 (Rewriting) ===
            
            string dir = Path.GetDirectoryName(srcPath);
            string fileName = Path.GetFileNameWithoutExtension(srcPath);
            string ext = Path.GetExtension(srcPath);
            string destPath = Path.Combine(dir, $"{fileName}_Optimized{ext}");

            using (var writer = new StreamWriter(destPath, false, Encoding.UTF8))
            {
                // 1. 写头
                writer.Write(headers.ToString());

                // 2. 按顺序写日志
                foreach (int id in idOrder)
                {
                    // A. 写 $DEF
                    if (defLines.TryGetValue(id, out string defLine))
                    {
                        writer.WriteLine(defLine);
                        result.OptimizedLines++;
                    }

                    // B. 写合并后的 $INC
                    // 如果总数 > 1，说明需要补一个 INC
                    // 注意：DEF 本身贡献了 1，所以 INC 的量是 Total - 1
                    if (counts.TryGetValue(id, out int total) && total > 1)
                    {
                        int incAmount = total - 1;
                        writer.WriteLine($"$INC|{id}|{incAmount}");
                        result.OptimizedLines++;
                    }
                }
            }

            result.OutputPath = destPath;
            result.OptimizedSize = new FileInfo(destPath).Length;
            return result;
        }
    }
}