using System.Reflection;

namespace Patina.Contracts;

/// <summary>What every Patina process reports about itself.</summary>
public static class ProductInfo {
    /// <summary>The release version: one value for all assemblies, set in Directory.Build.props.</summary>
    public static string Version { get; } =
        typeof(ProductInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
