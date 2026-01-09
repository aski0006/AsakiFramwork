using System;
using System.Reflection;
using UnityEngine;

namespace Asaki.Editor.ModernInspector
{
    /// <summary>
    /// ComponentUtility 辅助类（使用反射兼容不同 Unity 版本）
    /// </summary>
    public static class ComponentUtilityHelper
    {
        private static Type componentUtilityType;
        private static MethodInfo moveComponentUpMethod;
        private static MethodInfo moveComponentDownMethod;
        private static MethodInfo copyComponentMethod;
        private static MethodInfo pasteComponentValuesMethod;
        private static MethodInfo canMoveComponentUpMethod;
        private static MethodInfo canMoveComponentDownMethod;
        private static MethodInfo canPasteComponentValuesMethod;

        static ComponentUtilityHelper()
        {
            // 查找 ComponentUtility 类型
            componentUtilityType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.ComponentUtility");
            
            if (componentUtilityType != null)
            {
                // 缓存所有方法
                moveComponentUpMethod = componentUtilityType. GetMethod("MoveComponentUp", 
                    BindingFlags. Static | BindingFlags.Public);
                
                moveComponentDownMethod = componentUtilityType.GetMethod("MoveComponentDown", 
                    BindingFlags.Static | BindingFlags.Public);
                
                copyComponentMethod = componentUtilityType.GetMethod("CopyComponent", 
                    BindingFlags.Static | BindingFlags.Public);
                
                pasteComponentValuesMethod = componentUtilityType.GetMethod("PasteComponentValues", 
                    BindingFlags.Static | BindingFlags.Public);
                
                // 这些方法可能不存在于某些版本
                canMoveComponentUpMethod = componentUtilityType.GetMethod("CanMoveComponentUp", 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                
                canMoveComponentDownMethod = componentUtilityType.GetMethod("CanMoveComponentDown", 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                
                canPasteComponentValuesMethod = componentUtilityType.GetMethod("CanPasteComponentValues", 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        /// <summary>
        /// 向上移动组件
        /// </summary>
        public static bool MoveComponentUp(Component component)
        {
            if (moveComponentUpMethod == null || component == null)
                return false;

            try
            {
                return (bool)moveComponentUpMethod. Invoke(null, new object[] { component });
            }
            catch (Exception ex)
            {
                Debug. LogError($"Failed to move component up: {ex. Message}");
                return false;
            }
        }

        /// <summary>
        /// 向下移动组件
        /// </summary>
        public static bool MoveComponentDown(Component component)
        {
            if (moveComponentDownMethod == null || component == null)
                return false;

            try
            {
                return (bool)moveComponentDownMethod.Invoke(null, new object[] { component });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to move component down: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 复制组件
        /// </summary>
        public static bool CopyComponent(Component component)
        {
            if (copyComponentMethod == null || component == null)
                return false;

            try
            {
                return (bool)copyComponentMethod.Invoke(null, new object[] { component });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to copy component: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 粘贴组件值
        /// </summary>
        public static bool PasteComponentValues(Component component)
        {
            if (pasteComponentValuesMethod == null || component == null)
                return false;

            try
            {
                return (bool)pasteComponentValuesMethod.Invoke(null, new object[] { component });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to paste component values: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查是否可以向上移动组件
        /// </summary>
        public static bool CanMoveComponentUp(Component component)
        {
            if (component == null)
                return false;

            // 如果方法存在，使用反射调用
            if (canMoveComponentUpMethod != null)
            {
                try
                {
                    return (bool)canMoveComponentUpMethod.Invoke(null, new object[] { component });
                }
                catch
                {
                    // 降级方案
                }
            }

            // 降级方案：手动检查
            return CanMoveComponentUpFallback(component);
        }

        /// <summary>
        /// 检查是否可以向下移动组件
        /// </summary>
        public static bool CanMoveComponentDown(Component component)
        {
            if (component == null)
                return false;

            // 如果方法存在，使用反射调用
            if (canMoveComponentDownMethod != null)
            {
                try
                {
                    return (bool)canMoveComponentDownMethod. Invoke(null, new object[] { component });
                }
                catch
                {
                    // 降级方案
                }
            }

            // 降级方案：手动检查
            return CanMoveComponentDownFallback(component);
        }

        /// <summary>
        /// 检查是否可以粘贴组件值
        /// </summary>
        public static bool CanPasteComponentValues(Component component)
        {
            if (component == null)
                return false;

            // 如果方法存在，使用反射调用
            if (canPasteComponentValuesMethod != null)
            {
                try
                {
                    return (bool)canPasteComponentValuesMethod.Invoke(null, new object[] { component });
                }
                catch
                {
                    // 降级方案
                }
            }

            // 降级方案：假设可以粘贴（让用户尝试）
            return true;
        }

        // =========================================================
        // 降级方案（手动实现）
        // =========================================================

        private static bool CanMoveComponentUpFallback(Component component)
        {
            if (component == null || component.gameObject == null)
                return false;

            // Transform 不能移动
            if (component is Transform)
                return false;

            var components = component.gameObject.GetComponents<Component>();
            int index = Array.IndexOf(components, component);

            // 第一个非 Transform 组件不能上移
            if (index <= 1) // 0 是 Transform
                return false;

            return true;
        }

        private static bool CanMoveComponentDownFallback(Component component)
        {
            if (component == null || component.gameObject == null)
                return false;

            // Transform 不能移动
            if (component is Transform)
                return false;

            var components = component.gameObject.GetComponents<Component>();
            int index = Array.IndexOf(components, component);

            // 最后一个组件不能下移
            if (index < 0 || index >= components.Length - 1)
                return false;

            return true;
        }
    }
}