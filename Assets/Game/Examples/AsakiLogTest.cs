using UnityEngine;
using System;
using System.Threading;
using System.Threading.Tasks;
using Asaki.Unity.Services.Logging;
using Asaki.Core.Serialization;
using Asaki.Unity.Services.Serialization;

namespace Asaki.Tests
{
    public class AsakiLogTest : MonoBehaviour
    {
        // 模拟一个实现了 IAsakiSavable 的业务对象，用于测试 "Level 2" 极速序列化
        private class TestUser : IAsakiSavable
        {
            public string Name = "Asaki";
            public int Level = 99;
            public Vector3 Position = new Vector3(1, 2, 3);

            public void Serialize(IAsakiWriter writer)
            {
                writer.BeginObject("TestUser");
                writer.WriteString("n", Name);
                writer.WriteInt("lv", Level);
                writer.WriteVector3("pos", Position);
                writer.EndObject();
            }

            public void Deserialize(IAsakiReader reader) { /* Test only */ }
        }
        

        private void OnGUI()
        {
            float scale = 1.5f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 600));
            GUILayout.Label("<b>Asaki Logger V5 Test Suite</b>");

            GUILayout.Space(10);
            DrawBasicTests();
            
            GUILayout.Space(10);
            DrawPayloadTests();
            
            GUILayout.Space(10);
            DrawThreadTests();
            
            GUILayout.Space(10);
            DrawExceptionTests();

            GUILayout.EndArea();
        }

        private void DrawBasicTests()
        {
            GUILayout.Label("1. 基础级别 (Release下 Info/Debug 应消失)");
            if (GUILayout.Button("Log Info"))
            {
                // 应该在 Console 显示白色，Dashboard 显示 I
                ALog.Info("这是一条普通信息");
            }
            if (GUILayout.Button("Log Debug (Scope)"))
            {
                // 测试耗时统计
                using (new ALog.ScopedProfiler("TestCalculation"))
                {
                    Thread.Sleep(5); // 模拟耗时
                }
            }
            if (GUILayout.Button("Log Warning"))
            {
                // 应该在 Console 显示黄色，Dashboard 显示 W，带调用堆栈
                ALog.Warn("这是一条警告信息！");
            }
        }

        private void DrawPayloadTests()
        {
            GUILayout.Label("2. Payload 序列化路由");
            
            if (GUILayout.Button("Level 1: Primitives (ToString)"))
            {
                ALog.Info("基础类型 Payload", 12345);
                ALog.Info("Vector3 Payload", transform.position);
            }

            if (GUILayout.Button("Level 2: Native (IAsakiSavable)"))
            {
                // [核心验证] 应该走 AsakiJsonWriter，零 GC
                var user = new TestUser();
                ALog.Info("原生对象 Payload", user); 
            }

            if (GUILayout.Button("Level 3: Fallback (JsonUtility)"))
            {
                // [核心验证] 匿名类型，应该走 JsonUtility 兜底
                var anon = new { ID = 1, Status = "Active", Props = new { A = 1, B = 2 } };
                ALog.Info("匿名对象 Payload", anon);
            }
        }

        private void DrawThreadTests()
        {
            GUILayout.Label("3. 线程安全 (ThreadLocal)");
            if (GUILayout.Button("Run Multi-Thread Spam"))
            {
                // 启动 5 个线程并发写日志，验证 Queue 和 TLS Builder 是否冲突
                for (int i = 0; i < 5; i++)
                {
                    int id = i;
                    Task.Run(() => 
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            ALog.Info($"Thread[{id}] Msg[{j}]", new { ThreadID = id, Count = j });
                            Thread.Sleep(10);
                        }
                    });
                }
            }
        }

        private void DrawExceptionTests()
        {
            GUILayout.Label("4. 异常与堆栈 (Waterfall)");
            if (GUILayout.Button("Trigger Error (Message)"))
            {
                // 仅文本错误，Dashboard 应显示调用点堆栈
                ALog.Error("逻辑错误：索引越界", payload: new { Index = -1 });
            }

            if (GUILayout.Button("Trigger Exception (Catch)"))
            {
                try
                {
                    SimulateDeepCallStack();
                }
                catch (Exception ex)
                {
                    // 完整异常，Dashboard 应显示红色异常瀑布流
                    ALog.Error("捕获到异常", ex, new { Context = "GameLoop" });
                }
            }
            
            if (GUILayout.Button("Test Fatal (Simulate Crash)"))
            {
                ALog.Fatal("致命错误：系统核心崩溃！");
            }
        }

        private void SimulateDeepCallStack()
        {
            CallLayer1();
        }
        private void CallLayer1() => CallLayer2();
        private void CallLayer2() => CallLayer3();
        private void CallLayer3() => throw new InvalidOperationException("Deep Error Occurred!");
    }
}