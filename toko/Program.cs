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
        if (attribute.Matches(typeof(ObsoleteAttribute).FullName))
        {
            isElement = false;
            break;
        }

        if (attribute.Matches(typeof(DesignTimeVisibleAttribute).FullName, false))
        {
            isElement = false;
            break;
        }

        if (attribute.Matches("Bonsai.CombinatorAttribute"))
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

        var typeDeclaration = TypeDefinitionProvider.GetTypeDeclaration(type, importNamespaces);
        var typeDescription = typeDeclaration.CustomAttributes.GetDescription();
        var elementCategory = typeDeclaration.CustomAttributes.GetElementCategory();
        Console.WriteLine("---");
        Console.WriteLine($"name: {type.Name}");
        Console.WriteLine($"namespace: {type.Namespace}");
        Console.WriteLine($"assembly: {type.Assembly.GetName().Name}");
        Console.WriteLine($"category: {elementCategory}");
        Console.WriteLine($"summary: {typeDescription}");

        Console.WriteLine($"properties: ");
        foreach (var property in typeDeclaration.Members.OfType<CodeMemberProperty>())
        {
            var propertyAttributes = property.CustomAttributes;
            if (propertyAttributes.IsDefined(typeof(BrowsableAttribute).FullName, false))
            {
                continue;
            }

            var propertyDescription = propertyAttributes.GetDescription();
            Console.WriteLine($"  - name: {property.Name}");
            Console.WriteLine($"    type: {codeProvider.GetTypeOutput(property.Type)}");
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
        var combinatorAttribute = typeDeclaration.CustomAttributes.FirstOrDefault("Bonsai.CombinatorAttribute");
        foreach (CodeAttributeArgument argument in combinatorAttribute.Arguments)
        {
            if (argument.Name == "MethodName" && argument.Value is CodePrimitiveExpression expression)
            {
                methodName = (string?)expression.Value as string ?? methodName;
            }
        }

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
