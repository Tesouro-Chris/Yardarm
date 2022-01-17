using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.OpenApi.Models;
using Yardarm.Enrichment.Compilation;
using Yardarm.Generation;
using Yardarm.Helpers;
using Yardarm.Names;
using Yardarm.SystemTextJson.Helpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Yardarm.SystemTextJson.Internal
{
    /// <summary>
    /// Adds <see cref="JsonSerializableAttribute"/> for each schema to the ModelSerializerContext.
    /// </summary>
    internal class JsonSerializableEnricher : ICompilationEnricher
    {
        private const string ListPrefix = "System.Collections.Generic.List<";

        private readonly ITypeGeneratorRegistry<OpenApiSchema> _schemaGeneratorRegistry;
        private readonly string _rootNamespacePrefix;

        public Type[] ExecuteAfter { get; } =
        {
            typeof(ResourceFileCompilationEnricher),
            typeof(SyntaxTreeCompilationEnricher)
        };

        public JsonSerializableEnricher(ITypeGeneratorRegistry<OpenApiSchema> schemaGeneratorRegistry, IRootNamespace rootNamespace)
        {
            _schemaGeneratorRegistry = schemaGeneratorRegistry ?? throw new ArgumentNullException(nameof(schemaGeneratorRegistry));

            _rootNamespacePrefix = rootNamespace.Name + ".";
        }

        public ValueTask<CSharpCompilation> EnrichAsync(CSharpCompilation target, CancellationToken cancellationToken = default)
        {
            foreach (var syntaxTree in target.SyntaxTrees)
            {
                var compilationUnit = syntaxTree.GetRoot(cancellationToken);
                var declarations = compilationUnit
                    .GetAnnotatedNodes(JsonSerializerContextGenerator.GeneratorAnnotation)
                    .OfType<ClassDeclarationSyntax>()
                    .ToArray();

                if (declarations.Length > 0)
                {
                    var newCompilationUnit = compilationUnit.ReplaceNodes(
                        declarations,
                        AddAttributes);

                    target = target.ReplaceSyntaxTree(syntaxTree,
                        syntaxTree.WithRootAndOptions(newCompilationUnit, syntaxTree.Options));
                }
            }

            return new(target);
        }

        public ClassDeclarationSyntax AddAttributes(ClassDeclarationSyntax _, ClassDeclarationSyntax currentDeclaration)
        {
            // Collect a list of schemas which have types generated
            var types = _schemaGeneratorRegistry.GetAll()
                .Select(p =>
                {
                    YardarmTypeInfo typeInfo = p.TypeInfo;

                    TypeSyntax modelName = typeInfo.Name;
                    bool isList = false;
                    if (!typeInfo.IsGenerated)
                    {
                        if (WellKnownTypes.System.Collections.Generic.ListT.IsOfType(modelName,
                                out var genericArgument))
                        {
                            isList = true;
                            modelName = genericArgument;
                        }
                    }

                    return (typeInfo, modelName, isList);
                })
                .Where(p => p.isList || p.typeInfo.IsGenerated);

            // Generate a JsonSerializable attributes for each type
            var typeInfoPropertyName = NameEquals(IdentifierName("TypeInfoPropertyName"));
            var attributeLists = types.Select(type =>
                AttributeList(SingletonSeparatedList(Attribute(
                    SystemTextJsonTypes.Serialization.JsonSerializableAttributeName,
                    AttributeArgumentList(SeparatedList(new [] {
                        AttributeArgument(TypeOfExpression(type.typeInfo.Name)),
                        AttributeArgument(
                            typeInfoPropertyName,
                            default,
                            SyntaxHelpers.StringLiteral(GetPropertyName(type.modelName, type.isList)))
                    }))))));

            return currentDeclaration.AddAttributeLists(attributeLists.ToArray());
        }

        private string GetPropertyName(TypeSyntax typeName, bool isList)
        {
            string typeNameString = typeName.ToString();

            // Trim the root namespace to keep the length down, if present
            typeNameString = typeNameString.StartsWith(_rootNamespacePrefix)
                ? typeNameString.Substring(_rootNamespacePrefix.Length).Replace(".", "")
                : typeNameString.Replace(".", "");

            if (isList)
            {
                typeNameString = "List" + typeNameString;
            }

            return typeNameString;
        }
    }
}
