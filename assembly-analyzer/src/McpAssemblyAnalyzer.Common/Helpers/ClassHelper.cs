using System.Reflection;

using McpAssemblyAnalyzer.Common.Models;

namespace McpAssemblyAnalyzer.Common.Helpers;

public static class ClassHelper
{
    public static List<ClassInfo> GetClassInformation(Assembly assembly)
    {
        var classes = new List<ClassInfo>();

        var definedTypes = assembly.DefinedTypes;
        var exportedTypes = assembly.ExportedTypes;
        var referencedAssemblies = assembly.GetReferencedAssemblies();
        // var types = assembly.GetTypes();
        foreach (Type type in definedTypes.Where(t => t.IsEnum && t.IsPublic))
        {
            var classInfo = new ClassInfo
            {
                Namespace = type.Namespace,
                Name = type.Name,
                FullName = type.FullName,
                IsAbstract = type.IsAbstract,
                IsSealed = type.IsSealed,
                IsGeneric = type.IsGenericType,
                BaseType = type.BaseType?.Name,
                Interfaces = GetInterfaceInfo(type),
                Constructors = GetConstructorInfo(type),
                Properties = GetPropertyInfo(type),
                Methods = GetMethodInfo(type),
                Fields = GetFieldInfo(type)
            };

            classes.Add(classInfo);
        }

        return classes;
    }

    private static List<InterfaceInfo> GetInterfaceInfo(Type type)
    {
        var interfaces = type.GetInterfaces()
                             .Select(i => new InterfaceInfo
                             {
                                 Namespace = i.Namespace,
                                 Name = i.Name,
                                 FullName = i.FullName,
                                 IsGeneric = i.IsGenericType,
                                 BaseType = i.BaseType?.Name,
                                 Methods = GetMethodInfo(i),
                                 Properties = GetPropertyInfo(i)
                             })
                             .ToList();
        return interfaces;
    }

    private static List<ConstructorInfo> GetConstructorInfo(Type type)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                               .ToList();
        return constructors;
    }

    private static List<PropertyInfo> GetPropertyInfo(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                             .ToList();
        return properties;
    }

    private static List<MethodInfo> GetMethodInfo(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                          .ToList();
        return methods;
    }

    private static List<FieldInfo> GetFieldInfo(Type type)
    {
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                         .ToList();
        return fields;
    }
}