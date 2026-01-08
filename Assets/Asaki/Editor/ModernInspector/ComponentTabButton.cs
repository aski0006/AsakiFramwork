using UnityEngine;
using UnityEngine.UIElements;

namespace Asaki.Editor.ModernInspector
{
	/// <summary>
	/// 组件选项卡按钮的自定义控件
	/// </summary>
	public class ComponentTabButton : Button
	{
		private static readonly string USS_CLASS_NAME = "component-tab-button";
		private static readonly string ACTIVE_CLASS = "tab-button-active";
        
		private Component component;
		private Image icon;
		private Label label;
        
		public Component Component => component;
		public bool IsActive { get; private set; }
        
		public ComponentTabButton(Component comp, System.Action<ComponentTabButton> onClickCallback)
		{
			component = comp;
            
			AddToClassList(USS_CLASS_NAME);
			AddToClassList("tab-button");
            
			// 创建图标
			icon = new Image
			{
				image = UnityEditor.EditorGUIUtility.ObjectContent(comp, comp.GetType()).image
			};
			icon.AddToClassList("tab-icon");
            
			// 创建标签
			label = new Label(comp.GetType().Name);
			label.AddToClassList("tab-label");
            
			Add(icon);
			Add(label);
            
			// 注册点击事件
			clicked += () => onClickCallback?. Invoke(this);
		}
        
		public void SetActive(bool active)
		{
			IsActive = active;
            
			if (active)
			{
				AddToClassList(ACTIVE_CLASS);
			}
			else
			{
				RemoveFromClassList(ACTIVE_CLASS);
			}
		}
        
		public void UpdateComponent(Component comp)
		{
			component = comp;
			icon.image = UnityEditor.EditorGUIUtility.ObjectContent(comp, comp.GetType()).image;
			label.text = comp.GetType().Name;
		}
	}
}
