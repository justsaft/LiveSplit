using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using LiveSplit.Model;

namespace LiveSplit.UI.Components;

public class ComponentManager
{
    public static readonly string RuntimePath = Path.GetFullPath(Directory.GetCurrentDirectory());
    public static readonly string ComponentsFolder = @"Components/";
    public static IDictionary<string, IComponentFactory> ComponentFactories { get; protected set; }
    public static IDictionary<string, IRaceProviderFactory> RaceProviderFactories { get; set; }

    public static ILayoutComponent LoadLayoutComponent(string path, LiveSplitState state)
    {
        ComponentFactories ??= LoadAllFactories<IComponentFactory>();

        IComponent component = null;

        if (string.IsNullOrEmpty(path))
        {
            component = new SeparatorComponent();
        }
        else if (!ComponentFactories.ContainsKey(path))
        {
            return null;
        }
        else
        {
            component = ComponentFactories[path].Create(state);
        }

        return new LayoutComponent(path, component);
    }

    public static IDictionary<string, T> LoadAllFactories<T>()
    {
        string path = Path.Combine(RuntimePath, ComponentsFolder);
        return Directory
            .EnumerateFiles(path, "*.dll")
            .Select(x =>
            {
                T factory = LoadFactory<T>(x);
                return new KeyValuePair<string, T>(Path.GetFileName(x), factory);
            })
            .Where(x => x.Value != null)
            .ToDictionary(x => x.Key, x => x.Value);
    }

    public static T LoadFactory<T>(string path)
    {
        T factory = default;
        try
        {
            var attr = (ComponentFactoryAttribute)Attribute
                .GetCustomAttribute(Assembly.UnsafeLoadFrom(path), typeof(ComponentFactoryAttribute));

            if (attr != null)
            {
                factory = (T)attr.
                    ComponentFactoryClassType.
                    GetConstructor([]).
                    Invoke(null);
            }
        }
        catch { }

        return factory;
    }
}
