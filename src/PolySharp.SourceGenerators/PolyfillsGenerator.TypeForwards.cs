using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PolySharp.SourceGenerators.Extensions;
using PolySharp.SourceGenerators.Helpers;
using PolySharp.SourceGenerators.Models;

namespace PolySharp.SourceGenerators;

/// <inheritdoc/>
partial class PolyfillsGenerator
{
    /// <summary>
    /// The collection of fully qualified type names for types that could require a <c>modreq</c>.
    /// </summary>
    private static readonly ImmutableArray<string> ModreqCandidateFullyQualifiedTypeNames = ImmutableArray.Create(
        "System.Index",
        "System.Range",
        "System.Runtime.CompilerServices.IsExternalInit");

    /// <summary>
    /// Gets the types from the BCL that should potentially receive type forwards.
    /// </summary>
    /// <param name="compilation">The current <see cref="Compilation"/> instance.</param>
    /// <param name="token">The cancellation token for the operation.</param>
    /// <returns>The collection type names to create type forwards for.</returns>
    private static ImmutableArray<string> GetCoreLibTypes(Compilation compilation, CancellationToken token)
    {
        // Same check as when generating polyfills (if none can be generated, there's no need for type forwards)
        if (!compilation.HasLanguageVersionAtLeastEqualTo(LanguageVersion.CSharp8))
        {
            return ImmutableArray<string>.Empty;
        }

        IAssemblySymbol coreLibAssemblySymbol = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly;

        using ImmutableArrayBuilder<string> builder = ImmutableArrayBuilder<string>.Rent();

        // Gather all types from the candidates list that are defined in 
        foreach (string name in ModreqCandidateFullyQualifiedTypeNames)
        {
            if (coreLibAssemblySymbol.GetTypeByMetadataName(name) is not null)
            {
                builder.Add(name);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Checks whether a given type forward is selected for generation.
    /// </summary>
    /// <param name="info">The input info for the current generation.</param>
    /// <returns>Whether the current type forward is selected for generation</returns>
    private static bool IsCoreLibTypeSelected((string FullyQualifiedTypeName, GenerationOptions Options) info)
    {
        // If type forwards are disabled, never select any type
        if (info.Options.ExcludeTypeForwardedToDeclarations)
        {
            return false;
        }

        // If the type is not selected for generation, no type forward is needed
        if (!IsAvailableTypeSelected(info))
        {
            return false;
        }

        // For Index and Range, the type forward is only needed if they are public. This is
        // because if they are internal, there is no need to worry about them being in public APIs.
        if (info.FullyQualifiedTypeName is "System.Index" or "System.Range")
        {
            return info.Options.UsePublicAccessibilityForGeneratedTypes;
        }

        return true;
    }

    /// <summary>
    /// Selects the current type forward name.
    /// </summary>
    /// <param name="info">The input info for the current generation.</param>
    /// <param name="token">The cancellation token for the operation.</param>
    /// <returns>A type forward name for generation.</returns>
    private static string GetCoreLibType((string FullyQualifiedTypeName, GenerationOptions Options) info, CancellationToken token)
    {
        return info.FullyQualifiedTypeName;
    }

    /// <summary>
    /// Emits a type forwarding for a given type.
    /// </summary>
    /// <param name="context">The input <see cref="SourceProductionContext"/> instance to use to emit code.</param>
    /// <param name="fullyQualifiedTypeName">The fully qualified type name of the type to generate the forwarding for.</param>
    private static void EmitTypeForwards(SourceProductionContext context, string fullyQualifiedTypeName)
    {
        // Finally generate the source text
        context.AddSource($"{fullyQualifiedTypeName}.g.cs", $"""
            // <auto-generated/>
            #pragma warning disable

            [assembly: global::System.Runtime.CompilerServices.TypeForwardedTo(typeof(global::{fullyQualifiedTypeName}))]
            """);
    }
}