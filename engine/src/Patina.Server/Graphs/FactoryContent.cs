using Patina.Content;
using System.Runtime.CompilerServices;

namespace Patina.Server.Graphs;

/// <summary>Registers Patina's own compounds before any Server code reads the compound library.</summary>
/// <remarks>
///     A module initializer, because the palette, the stack vocabulary and the shelf each read
///     <c>TextureCompoundLibrary.Shipped</c> from static state, and a test that hosts the Server in its own process
///     reaches them without running <c>Program</c>. The Server is an executable, so this runs once, when it loads.
/// </remarks>
static class FactoryContent {
    [ModuleInitializer]
    internal static void Register() => FactoryCompounds.Register();
}
