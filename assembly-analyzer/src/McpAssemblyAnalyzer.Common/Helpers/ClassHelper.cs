using System.Reflection;

using McpAssemblyAnalyzer.Common.Models;

namespace McpAssemblyAnalyzer.Common.Helpers;

public static class ClassHelper
{
    public static List<ClassDetails> GetClassDetails(Assembly assembly)
    {
        var classes = new List<ClassDetails>();

        foreach (Type type in assembly.GetTypes().Where(t => t.IsClass && t.IsPublic))
        {
            var classInfo = new ClassDetails
            {
                Namespace = type.Namespace,
                Name = type.Name,
                FullName = type.FullName,
                IsAbstract = type.IsAbstract,
                IsSealed = type.IsSealed,
                IsGeneric = type.IsGenericType,
                BaseType = type.BaseType?.Name,
                Interfaces = GetInterfaceDetails(type),
                Constructors = GetConstructorDetails(type),
                Properties = GetPropertyDetails(type),
                Methods = GetMethodDetails(type),
                Fields = GetFieldDetails(type)
            };

            classes.Add(classInfo);
        }

        return classes;
    }

    private static List<InterfaceDetails> GetInterfaceDetails(Type type)
    {
        var interfaces = type.GetInterfaces()
                             .Select(i => new InterfaceDetails
                             {
                                 Namespace = i.Namespace,
                                 Name = i.Name,
                                 FullName = i.FullName,
                                 IsGeneric = i.IsGenericType,
                                 BaseType = i.BaseType?.Name,
                                 Methods = GetMethodDetails(i),
                                 Properties = GetPropertyDetails(i)
                             })
                             .ToList();
        return interfaces;
    }

    private static List<ConstructorDetails> GetConstructorDetails(Type type)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                               .Select(c => new ConstructorDetails
                               {
                                   Name = c.Name,
                                   Parameters = GetParameterDetails(c)
                               })
                               .ToList();
        return constructors;
    }

    private static List<PropertyDetails> GetPropertyDetails(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                             .Select(p => new PropertyDetails
                             {
                                 Name = p.Name,
                                 PropertyType = p.PropertyType.Name
                             })
                             .ToList();
        return properties;
    }

    private static List<MethodDetails> GetMethodDetails(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                          .Select(m => new MethodDetails
                          {
                              Name = m.Name,
                              ReturnType = m.ReturnType.Name,
                              Parameters = GetParameterDetails(m)
                          })
                          .ToList();
        return methods;
    }

    private static List<ParameterDetails> GetParameterDetails(ConstructorInfo constructor)
    {
        var parameters = constructor.GetParameters()
                                    .Select(p => new ParameterDetails
                                    {
                                        Name = p.Name,
                                        ParameterType = p.ParameterType.Name,
                                        IsOptional = p.IsOptional,
                                        DefaultValue = p.DefaultValue
                                    })
                                    .ToList();
        return parameters;
    }

    private static List<ParameterDetails> GetParameterDetails(MethodInfo method)
    {
        var parameters = method.GetParameters()
                               .Select(p => new ParameterDetails
                               {
                                   Name = p.Name,
                                   ParameterType = p.ParameterType.Name,
                                   IsOptional = p.IsOptional,
                                   DefaultValue = p.DefaultValue
                               })
                               .ToList();
        return parameters;
    }

    private static List<FieldDetails> GetFieldDetails(Type type)
    {
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                         .Select(f => new FieldDetails
                         {
                             Name = f.Name,
                             FieldType = f.FieldType.Name
                         })
                         .ToList();
        return fields;
    }
}