using System.Reflection;

using McpAssemblyAnalyzer.Common.Models;

namespace McpAssemblyAnalyzer.Common.Helpers;

public static class EnumHelper
{
    public static List<EnumInfo> GetEnumInformation(Assembly assembly)
    {
        var enums = new List<EnumInfo>();

        var definedTypes = assembly.DefinedTypes;
        var exportedTypes = assembly.ExportedTypes;
        var referencedAssemblies = assembly.GetReferencedAssemblies();
        // var types = assembly.GetTypes();
        foreach (Type type in definedTypes.Where(t => t.IsEnum && t.IsPublic))
        {
            var enumInfo = new EnumInfo
            {
                Namespace = type.Namespace,
                Name = type.Name,
                FullName = type.FullName,
                Values = GetEnumValues(type)
            };

            enums.Add(enumInfo);
        }

        return enums;
    }

    private static List<KeyValuePair<int, string>> GetEnumValues(Type type)
    {
        var values = type.GetEnumValues()
                         .Cast<object>()
                         .Select(value => new KeyValuePair<int, string>(
                             Convert.ToInt32(value),
                             value.ToString() ?? string.Empty)).ToList();
        return values;
    }
}
