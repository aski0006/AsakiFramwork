using System;
using System.Collections.Generic;
using System.IO;
using Asaki.Core.Logging;
using UnityEngine;

namespace Asaki.Unity.Services.Logging
{
    public static class AsakiLogFileReader
    {
        public static List<AsakiLogModel> LoadFile(string path)
        {
            var list = new List<AsakiLogModel>();
            var idMap = new Dictionary<int, AsakiLogModel>();

            if (!File.Exists(path)) return list;

            try
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    if (line.StartsWith("$DEF|"))
                    {
                        var parts = line.Split('|');
                        if (parts.Length < 7) continue;

                        // 解析基础信息
                        var model = new AsakiLogModel
                        {
                            ID = int.Parse(parts[1]),
                            Level = (AsakiLogLevel)int.Parse(parts[2]),
                            LastTimestamp = long.Parse(parts[3]),
                            Message = parts[4].Replace("¦", "|"), // 还原分隔符
                            PayloadJson = parts[5].Replace("¦", "|"),
                            Count = 1
                        };

                        // 解析 Caller (Path:Line)
                        var caller = parts[6].Split(':');
                        if (caller.Length >= 2)
                        {
                            model.CallerPath = caller[0];
                            int.TryParse(caller[1], out model.CallerLine);
                        }

                        // [V2.1] 解析堆栈
                        if (parts.Length >= 8)
                        {
                            try {
                                var json = parts[7].Replace("¦", "|");
                                var wrapper = JsonUtility.FromJson<StackWrapper>(json);
                                model.StackFrames = wrapper.F;
                            } catch { }
                        }

                        idMap[model.ID] = model;
                        list.Add(model);
                    }
                    else if (line.StartsWith("$INC|"))
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 3 && int.TryParse(parts[1], out int id) && int.TryParse(parts[2], out int inc))
                        {
                            if (idMap.TryGetValue(id, out var model))
                            {
                                model.Count += inc;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AsakiReader] Failed to load log: {ex.Message}");
            }

            return list;
        }

        [Serializable] private struct StackWrapper { public List<StackFrameModel> F; }
    }
}