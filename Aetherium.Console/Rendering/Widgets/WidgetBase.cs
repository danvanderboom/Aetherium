using System;

namespace Aetherium.Rendering.Widgets
{
    /// <summary>
    /// Base class for widgets with common functionality
    /// </summary>
    public abstract class WidgetBase : IWidget
    {
        public string Id { get; protected set; }
        public bool IsVisible { get; set; } = true;
        public int ZOrder { get; set; } = 0;

        protected WidgetBase(string id)
        {
            Id = id;
        }

        public abstract object GetRenderData();
    }
}


