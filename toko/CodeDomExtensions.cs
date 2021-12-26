using System.CodeDom;
using System.ComponentModel;
namespace toko;

internal static class CodeDomExtensions
{
    internal static CodeAttributeDeclaration FirstOrDefault(this CodeAttributeDeclarationCollection attributes, string typeFullName)
    {
        return attributes.Cast<CodeAttributeDeclaration>()
                         .FirstOrDefault(attribute => attribute.AttributeType.BaseType == typeFullName);
    }

    internal static bool IsDefined(this CodeAttributeDeclarationCollection attributes, string typeFullName)
    {
        return attributes.Cast<CodeAttributeDeclaration>()
                         .Any(attribute => attribute.AttributeType.BaseType == typeFullName);
    }

    internal static bool IsDefined(this CodeAttributeDeclarationCollection attributes, string typeFullName, params object[] values)
    {
        return attributes.Cast<CodeAttributeDeclaration>().Any(attribute => attribute.Matches(typeFullName, values));
    }

    internal static bool Matches(this CodeAttributeDeclaration attribute, string typeFullName)
    {
        return attribute.AttributeType.BaseType == typeFullName;
    }

    internal static bool Matches(this CodeAttributeDeclaration attribute, string typeFullName, params object[] values)
    {
        var arguments = attribute.Arguments;
        if (attribute.AttributeType.BaseType == typeFullName && arguments.Count == values.Length)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (arguments[i].Value is not CodePrimitiveExpression expression ||
                    !values[i].Equals(expression.Value))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    internal static string GetDescription(this CodeAttributeDeclarationCollection attributes)
    {
        var descriptionAttribute = attributes
            .Cast<CodeAttributeDeclaration>()
            .FirstOrDefault(attribute => attribute.AttributeType.BaseType == typeof(DescriptionAttribute).FullName);
        if (descriptionAttribute != null && descriptionAttribute.Arguments.Count == 1)
        {
            var description = ((CodePrimitiveExpression)descriptionAttribute.Arguments[0].Value).Value as string;
            return description ?? string.Empty;
        }

        return string.Empty;
    }

    internal static string GetElementCategory(this CodeAttributeDeclarationCollection attributes)
    {
        var elementCategoryAttribute = attributes
            .Cast<CodeAttributeDeclaration>()
            .FirstOrDefault(attribute => attribute.AttributeType.BaseType == "Bonsai.WorkflowElementCategoryAttribute");
        if (elementCategoryAttribute != null && elementCategoryAttribute.Arguments.Count == 1)
        {
            var elementCategory = ((CodeSnippetExpression)elementCategoryAttribute.Arguments[0].Value).Value;
            return elementCategory.Substring(elementCategory.IndexOf('.') + 1);
        }

        return "Combinator";
    }
}
