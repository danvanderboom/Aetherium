using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace ConsoleGame.Core
{
    public abstract class Component
    {
        public string ComponentId { get; protected set; } = Guid.NewGuid().ToString();

        public Component? Parent { get; set; }

        public ConcurrentDictionary<Type, Component> Components { get; protected set; } = new ConcurrentDictionary<Type, Component>();

        public Component()
        {
            Components = new ConcurrentDictionary<Type, Component>();
        }

        public Component(Component parent) : this()
        {
            Parent = parent;
        }

        public T Get<T>() where T : Component =>
            (T)AllComponents.First(c => c.GetType() == typeof(T));
        
        public void Set<T>(T component) where T : Component
        {
            if (Components.TryGetValue(typeof(T), out var existingComponent))
                if (existingComponent != null)
                    Components.TryRemove(typeof(T), out var _);

            if (Components.TryAdd(component.GetType(), component))
                component.Parent = this;
        }

        public bool Clear<T>() where T : Component => 
            Components.TryRemove(typeof(T), out var _);

        public bool Has<T>() where T : Component
            => AllComponents.Any(c => c.GetType() == typeof(T));

        public IEnumerable<Component> AllComponents
        {
            get
            {
                yield return this;

                foreach (var component in Components.Values.SelectMany(c => c.AllComponents).ToList())
                    yield return component;
            }
        }

        public bool HasAllComponents(params Type[] componentTypes) => 
            HasAllComponents(componentTypes.ToList());

        public bool HasAllComponents(IList<Type> componentTypes)
        {
            var found = new List<Type>();

            foreach (var component in AllComponents.ToList())
                if (componentTypes.Any(c => c.GetType().IsAssignableFrom(component.GetType())) && !found.Contains(component.GetType()))
                    found.Add(component.GetType());

            return found.Count == componentTypes.Count;
        }

        public bool HasComponent(Type type) => Components.Where(c => c.GetType() == type).Any();
    }
}