﻿using System.CodeDom;
using System.Reflection;
namespace toko;

static class TypeDefinitionProvider
{
    static CodeTypeReference GetTypeReference(Type type, HashSet<string> importNamespaces)
    {
        var baseType = type.IsArray || type.IsPointer || type.IsByRef ? type.GetElementType() : type;
        if (baseType.IsPrimitive || baseType == typeof(string) || baseType == typeof(object))
        {
            return new CodeTypeReference(type);
        }

        importNamespaces.Add(type.Namespace);
        var reference = new CodeTypeReference(type.Name);
        if (type.IsArray) reference.ArrayRank = type.GetArrayRank();
        if (type.IsGenericType)
        {
            var typeParameters = type.GetGenericArguments();
            reference.TypeArguments.AddRange(Array.ConvertAll(typeParameters, parameter => GetTypeReference(parameter, importNamespaces)));
        }
        return reference;
    }

    static CodeAttributeDeclaration GetAttributeDeclaration(CustomAttributeData attribute, HashSet<string> importNamespaces)
    {
        importNamespaces.Add(attribute.AttributeType.Namespace);
        //var attributeName = attribute.AttributeType.Name;
        //var suffix = attributeName.LastIndexOf(nameof(Attribute));
        //attributeName = suffix >= 0 ? attributeName.Substring(0, suffix) : attributeName;
        var reference = new CodeTypeReference(attribute.AttributeType);
        var declaration = new CodeAttributeDeclaration(reference);
        foreach (var argument in attribute.ConstructorArguments)
        {
            CodeExpression value;
            if (argument.ArgumentType == typeof(Type))
            {
                var type = (Type)argument.Value;
                value = new CodeTypeOfExpression(GetTypeReference(type, importNamespaces));
            }
            else if (argument.ArgumentType.IsEnum)
            {
                var name = Enum.GetName(argument.ArgumentType, argument.Value);
                var enumType = GetTypeReference(argument.ArgumentType, importNamespaces);
                value = new CodeSnippetExpression($"{enumType.BaseType}.{name}");
            }
            else value = new CodePrimitiveExpression(argument.Value);
            declaration.Arguments.Add(new CodeAttributeArgument(value));
        }
        foreach (var argument in attribute.NamedArguments)
        {
            CodeExpression value;
            if (argument.TypedValue.ArgumentType == typeof(Type))
            {
                var type = (Type)argument.TypedValue.Value;
                value = new CodeTypeOfExpression(GetTypeReference(type, importNamespaces));
            }
            else value = new CodePrimitiveExpression(argument.TypedValue.Value);
            declaration.Arguments.Add(new CodeAttributeArgument(argument.MemberName, value));
        }
        return declaration;
    }

    static CodeTypeMember GetPropertyDeclaration(PropertyInfo property, HashSet<string> importNamespaces, HashSet<MethodInfo> getterSetters)
    {
        var declaration = new CodeMemberProperty();
        declaration.Name = property.Name;
        declaration.Attributes = MemberAttributes.Public | MemberAttributes.Final;
        var attributes = property.GetCustomAttributesData(inherit: true)
            .Select(a => GetAttributeDeclaration(a, importNamespaces))
            .ToArray();
        declaration.CustomAttributes.AddRange(attributes);

        var getter = property.GetGetMethod();
        if (getter != null)
        {
            declaration.HasGet = true;
            getterSetters.Add(getter);
        }
        else declaration.HasGet = false;

        var setter = property.GetSetMethod();
        if (setter != null)
        {
            declaration.HasSet = true;
            getterSetters.Add(setter);
        }
        else declaration.HasSet = false;

        declaration.Type = GetTypeReference(property.PropertyType, importNamespaces);
        return declaration;
    }

    static void AddGenericTypeParameters(CodeTypeParameterCollection declarationTypeParameters, Type[] typeParameters, HashSet<string> importNamespaces)
    {
        declarationTypeParameters.AddRange(Array.ConvertAll(typeParameters, parameter =>
        {
            var constraints = parameter.GetGenericParameterConstraints();
            var parameterDeclaration = new CodeTypeParameter(parameter.Name);
            for (int i = 0; i < constraints.Length; i++)
            {
                var constraintDeclaration = constraints[i] == typeof(ValueType)
                    ? new CodeTypeReference(constraints[i])
                    : GetTypeReference(constraints[i], importNamespaces);
                parameterDeclaration.Constraints.Add(constraintDeclaration);
            }

            var classConstraint = parameter.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint);
            if (classConstraint) parameterDeclaration.Constraints.Add(typeof(object));
            var structConstraint = parameter.GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint);
            var defaultConstructor = parameter.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint);
            parameterDeclaration.HasConstructorConstraint = defaultConstructor && !structConstraint;
            return parameterDeclaration;
        }));
    }

    static CodeTypeMember GetMethodDeclaration(MethodInfo method, HashSet<string> importNamespaces)
    {
        var declaration = new CodeMemberMethod();
        declaration.Name = method.Name;
        declaration.Attributes = MemberAttributes.Public;
        if (!method.IsVirtual) declaration.Attributes |= MemberAttributes.Final;
        declaration.ReturnType = GetTypeReference(method.ReturnType, importNamespaces);
        if (method.IsGenericMethod)
        {
            var typeParameters = method.GetGenericArguments();
            AddGenericTypeParameters(declaration.TypeParameters, typeParameters, importNamespaces);
        }

        declaration.Parameters.AddRange(Array.ConvertAll(method.GetParameters(), parameter =>
        {
            var declaration = new CodeParameterDeclarationExpression();
            declaration.Name = parameter.Name;
            declaration.Type = GetTypeReference(parameter.ParameterType, importNamespaces);
            declaration.Direction = parameter.ParameterType.IsByRef
                ? (parameter.IsOut ? FieldDirection.Out : FieldDirection.Ref)
                : FieldDirection.In;
            return declaration;
        }));
        return declaration;
    }

    public static CodeTypeDeclaration GetTypeDeclaration(Type type, HashSet<string> importNamespaces)
    {
        var getterSetters = new HashSet<MethodInfo>();
        var declaration = new CodeTypeDeclaration(type.Name);
        if (type.IsGenericType)
        {
            var genericSeparatorIndex = type.Name.LastIndexOf('`');
            declaration.Name = type.Name.Substring(0, genericSeparatorIndex);
            var typeParameters = type.GetGenericArguments();
            AddGenericTypeParameters(declaration.TypeParameters, typeParameters, importNamespaces);
        }

        var attributes = type.GetCustomAttributesData()
            .Select(a => GetAttributeDeclaration(a, importNamespaces))
            .ToArray();

        if (type.BaseType != null && type.BaseType != typeof(object))
        {
            declaration.BaseTypes.Add(GetTypeReference(type.BaseType, importNamespaces));
        }

        var interfaces = type.GetInterfaces();
        if (interfaces.Length > 0)
        {
            declaration.BaseTypes.AddRange(Array.ConvertAll(interfaces, i => GetTypeReference(i, importNamespaces)));
        }

        var properties = type.GetProperties().Select(p => GetPropertyDeclaration(p, importNamespaces, getterSetters));
        var methods = type.GetMethods().Except(getterSetters).Select(m => GetMethodDeclaration(m, importNamespaces));
        var members = properties.Concat(methods).Where(declaration => declaration != null).ToArray();
        declaration.CustomAttributes.AddRange(attributes);
        declaration.Members.AddRange(members);
        return declaration;
    }

    public static CodeCompileUnit GetTypeDefinition(Type type)
    {
        var result = new CodeCompileUnit();
        var globalNamespace = new CodeNamespace();
        var importNamespaces = new HashSet<string>();
        var typeNamespace = new CodeNamespace(type.Namespace);
        var typeDeclaration = GetTypeDeclaration(type, importNamespaces);
        typeNamespace.Types.Add(typeDeclaration);
        var importDeclarations = importNamespaces.Select(name => new CodeNamespaceImport(name)).ToArray();
        globalNamespace.Imports.AddRange(importDeclarations);
        result.Namespaces.Add(globalNamespace);
        result.Namespaces.Add(typeNamespace);
        return result;
    }
}
