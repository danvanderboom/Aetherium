using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace ConsoleGame.Core
{
    public abstract class Component
    {
        public string Id { get; protected set; }

        public Component Parent { get; set; }

        public List<Component> Components { get; protected set; } = new List<Component>();

        public IEnumerable<Component> AllComponents
        {
            get
            {
                yield return this;

                foreach (var component in Components)
                    yield return component;
            }
        }

        public bool HasAllComponents(IList<Type> components)
        {
            var componentsFound = 0;

            foreach (var component in components)
            {
            }

            return false;
        }

        public bool HasComponent(Type type) => Components.Where(c => c.GetType() == type).Any();

        //public bool DescendantsHaveComponent(Type type)
        //{
        //    foreach (var component in Components)
        //    {
        //        if (component.HasComponent())
        //    }
        //}
    }
}