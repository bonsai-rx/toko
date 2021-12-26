using System.ComponentModel;
using System.Reflection;
namespace toko;

internal static class ReflectionExtensions
{
    internal static bool Matches(this CustomAttributeData attribute, string typeFullName)
    {
        return IsSubclassOf(attribute.AttributeType, typeFullName);
    }

    internal static bool Matches(this CustomAttributeData attribute, string typeFullName, params object[] values)
    {
        var arguments = attribute.ConstructorArguments;
        if (attribute.Matches(typeFullName) && arguments.Count == values.Length)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i].Equals(arguments[i].Value))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    internal static bool IsSubclassOf(this Type type, string typeFullName)
    {
        do
        {
            if (type.FullName == typeFullName)
            {
                return true;
            }
        }
        while ((type = type.BaseType) != null);
        return false;
    }

    internal static IList<CustomAttributeData> GetCustomAttributesData(this Type type, bool inherit)
    {
        var attributes = type.GetCustomAttributesData();
        if (inherit && type.BaseType != null)
        {
            var baseAttributes = GetCustomAttributesData(type.BaseType, inherit);
            return new List<CustomAttributeData>(attributes.Concat(baseAttributes));
        }

        return attributes;
    }

    internal static IList<CustomAttributeData> GetCustomAttributesData(this PropertyInfo target, bool inherit)
    {
        var attributes = target.GetCustomAttributesData();
        if (inherit && target.DeclaringType.BaseType != null)
        {
            var baseTarget = target.DeclaringType.BaseType.GetProperty(target.Name);
            if (baseTarget != null)
            {
                var baseAttributes = GetCustomAttributesData(baseTarget, inherit);
                return new List<CustomAttributeData>(attributes.Concat(baseAttributes));
            }
        }

        return attributes;
    }
}
