using Microsoft.CSharp;
using System.CodeDom;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using toko;

static bool IsWorkflowElement(Type type, IList<CustomAttributeData> typeAttributes)
{
    if (type.IsSubclassOf("Bonsai.Expressions.ExpressionBuilder"))
    {
        return true;
    }

    var isElement = false;
    foreach (var attribute in typeAttributes)
    {
        if (attribute.IsSubclassOf(typeof(ObsoleteAttribute).FullName))
        {
            isElement = false;
            break;
        }

        if (attribute.IsSubclassOf(typeof(DesignTimeVisibleAttribute).FullName, false))
        {
            isElement = false;
            break;
        }

        if (attribute.IsSubclassOf("Bonsai.CombinatorAttribute"))
        {
            isElement = true;
        }
    }

    return isElement;
}

if (args.Length == 0)
{
    Console.WriteLine("toko docs generator");
    return;
}

var fileName = args[0];
var localDirectory = Path.GetDirectoryName(Path.GetFullPath(fileName));
var runtimeAssemblies = Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
var localAssemblies = Directory.EnumerateFiles(localDirectory, "*.dll");
var assemblyResolver = new PathAssemblyResolver(runtimeAssemblies.Concat(localAssemblies));

using var codeProvider = new CSharpCodeProvider();
using var loadContext = new MetadataLoadContext(assemblyResolver);
var assembly = loadContext.LoadFromAssemblyPath(fileName);
var importNamespaces = new HashSet<string>();

foreach (var type in assembly.GetTypes())
{
    if (type.IsPublic && !type.IsValueType && !type.ContainsGenericParameters && !type.IsAbstract)
    {
        var typeAttributes = type.GetCustomAttributesData(inherit: true);
        if (!IsWorkflowElement(type, typeAttributes) || type.GetConstructor(Type.EmptyTypes) == null)
        {
            continue;
        }

        var typeDescription = typeAttributes.GetDescription();
        var elementCategory = typeAttributes.GetElementCategory();
        Console.WriteLine("---");
        Console.WriteLine($"name: {type.Name}");
        Console.WriteLine($"namespace: {type.Namespace}");
        Console.WriteLine($"category: {elementCategory}");
        Console.WriteLine($"summary: {typeDescription}");

        Console.WriteLine($"properties: ");
        foreach (var property in type.GetProperties())
        {
            var propertyAttributes = property.GetCustomAttributesData(inherit: true);
            if (propertyAttributes.IsDefined(typeof(BrowsableAttribute).FullName, false))
            {
                continue;
            }

            var propertyDescription = propertyAttributes.GetDescription();
            var propertyTypeReference = new CodeTypeReference(property.PropertyType);
            Console.WriteLine($"  - name: {property.Name}");
            Console.WriteLine($"    type: {codeProvider.GetTypeOutput(propertyTypeReference)}");
            Console.WriteLine($"    description: {propertyDescription}");
        }

        Console.WriteLine($"methods: ");
        if (type.IsSubclassOf("Bonsai.Expressions.ExpressionBuilder"))
        {
            // dynamic builder, no automatically derived templates
            Console.WriteLine($"  - name: Build");
            Console.WriteLine($"    inputs: ExpressionBuilder");
            Console.WriteLine($"    output: ExpressionBuilder");
            continue;
        }

        var methodName = "Process";
        var combinatorAttribute = typeAttributes.First(attribute => attribute.IsSubclassOf("Bonsai.CombinatorAttribute"));
        foreach (var argument in combinatorAttribute.NamedArguments)
        {
            if (argument.MemberName == "MethodName" && argument.TypedValue.ArgumentType.FullName == typeof(string).FullName)
            {
                methodName = (string?)argument.TypedValue.Value ?? methodName;
            }
        }

        var typeDeclaration = TypeDefinitionProvider.GetTypeDeclaration(type, importNamespaces);
        foreach (var method in typeDeclaration.Members.OfType<CodeMemberMethod>())
        {
            if (method.Name != methodName)
            {
                continue;
            }

            Console.WriteLine($"  - name: {method.Name}");
            Console.WriteLine($"    inputs: ");
            foreach (var parameter in method.Parameters.Cast<CodeParameterDeclarationExpression>())
            {
                Console.WriteLine($"    - name: {parameter.Name}");
                Console.WriteLine($"      type: {codeProvider.GetTypeOutput(parameter.Type)}");
            }
            Console.WriteLine($"    output: {codeProvider.GetTypeOutput(method.ReturnType)}");
        }

        Console.WriteLine("---");
        Console.WriteLine();
    }
}
