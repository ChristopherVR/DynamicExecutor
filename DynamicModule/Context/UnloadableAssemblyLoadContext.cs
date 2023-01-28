using System.Reflection;
using System.Runtime.Loader;

namespace DynamicModule.Context;
/// <summary>
/// Roslyn CShapAnalysis generation does not unload assemblies by default. Use this class to ensure the dynamic assemblies are
/// removed from memory after being used.
/// </summary>
internal class UnloadableAssemblyLoadContext : AssemblyLoadContext
{
    public UnloadableAssemblyLoadContext()
        : base(true)
    {
    }

    protected override Assembly? Load(AssemblyName assemblyName) => null;
}
