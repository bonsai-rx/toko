using System.ComponentModel;
using System.Reflection;
namespace toko;

internal static class ReflectionExtensions
{
    internal static bool IsDefined(this IList<CustomAttributeData> attributes, string typeFullName)
    {
        return attributes.Any(attribute => attribute.IsSubclassOf(typeFullName));
    }

    internal static bool IsDefined(this IList<CustomAttributeData> attributes, string typeFullName, params object[] values)
    {
        return attributes.Any(attribute => attribute.IsSubclassOf(typeFullName, values));
    }

    internal static bool IsSubclassOf(this CustomAttributeData attribute, string typeFullName)
    {
        return IsSubclassOf(attribute.AttributeType, typeFullName);
    }

    internal static bool IsSubclassOf(this CustomAttributeData attribute, string typeFullName, params object[] values)
    {
        var arguments = attribute.ConstructorArguments;
        if (attribute.IsSubclassOf(typeFullName) && arguments.Count == values.Length)
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

    internal static string GetDescription(this IList<CustomAttributeData> attributes)
    {
        var descriptionAttribute = attributes.FirstOrDefault(attribute => attribute.IsSubclassOf(typeof(DescriptionAttribute).FullName));
        if (descriptionAttribute != null && descriptionAttribute.ConstructorArguments.Count == 1)
        {
            var description = descriptionAttribute.ConstructorArguments[0].Value as string;
            return description ?? string.Empty;
        }

        return string.Empty;
    }

    internal static ElementCategory GetElementCategory(this IList<CustomAttributeData> attributes)
    {
        var elementCategoryAttribute = attributes.FirstOrDefault(attribute => attribute.IsSubclassOf("Bonsai.WorkflowElementCategoryAttribute"));
        if (elementCategoryAttribute != null && elementCategoryAttribute.ConstructorArguments.Count == 1)
        {
            var elementCategory = (ElementCategory)elementCategoryAttribute.ConstructorArguments[0].Value;
            return elementCategory;
        }

        return ElementCategory.Combinator;
    }
}

public enum ElementCategory
{
    Source,
    Condition,
    Transform,
    Sink,
    Nested,
    Property,
    Combinator,
    Workflow
}