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

        public T Get<T>() where T : Component =>
            (T)AllComponents.FirstOrDefault(c => c.GetType() == typeof(T));
        
        public void Set<T>(T component) where T : Component
        {
            var existingComponent = Components.FirstOrDefault(c => typeof(T).IsAssignableFrom(c.GetType()));
            if (existingComponent != null)
                Components.Remove(existingComponent);

            Components.Add(component);
        }

        public IEnumerable<Component> AllComponents
        {
            get
            {
                yield return this;

                foreach (var component in Components.ToList())
                    foreach (var x in component.AllComponents.ToList())
                        yield return x;
            }
        }

        public bool HasAllComponents(IList<Type> components)
        {
            var found = new List<Type>();

            foreach (var component in AllComponents.ToList())
                //if (components.Any(c => c.GetType().IsAssignableFrom(component.GetType())))
                if (components.Contains(component.GetType()) && !found.Contains(component.GetType()))
                    found.Add(component.GetType());

            return found.Count == components.Count;
        }

        public bool HasComponent(Type type) => Components.Where(c => c.GetType() == type).Any();
    }
}