using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Patina.Server.Tests;

/// <summary>
///     Which methods one assembly's code can reach through Patina's own assemblies, read from their IL, and which of
///     them call a method that is not allowed. It over-reaches on purpose: every overload of a name, every virtual
///     method of a type that is made, and every async or iterator body of a method reached are counted, so a path it
///     misses is one through a framework or Vixen type, which it treats as a leaf.
/// </summary>
sealed class MethodReach : IDisposable {
    static readonly Dictionary<short, OperandType> Operands = typeof(OpCodes).GetFields()
        .Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static code => code.Value, static code => code.OperandType);

    readonly Dictionary<string, Module> modules = new(StringComparer.Ordinal);
    readonly Dictionary<Method, HashSet<Method>> callers = [];
    readonly Queue<Method> waiting = new();
    readonly SortedSet<(string Called, Method By)> found = new(FoundOrder.Instance);
    readonly Func<string, string, bool> forbidden;

    /// <summary>Loads every assembly of Patina's in <paramref name="folder" />.</summary>
    /// <param name="folder">Where the assemblies are.</param>
    /// <param name="forbidden">Whether a call to an assembly's type, by full name, is one to report.</param>
    MethodReach(string folder, Func<string, string, bool> forbidden) {
        this.forbidden = forbidden;
        var ours = Directory.EnumerateFiles(folder, "*.dll")
            .Where(static file => Path.GetFileName(file).StartsWith("patina", StringComparison.OrdinalIgnoreCase));
        foreach (var file in ours) {
            var module = Module.Open(file);
            modules[module.Name] = module;
        }
    }

    /// <summary>
    ///     The forbidden calls every method of <paramref name="assembly" /> can reach: each with the method that makes
    ///     it, and the methods of <paramref name="assembly" /> it is reached from.
    /// </summary>
    public static IReadOnlyList<string> Forbidden(
        string folder,
        string assembly,
        Func<string, string, bool> forbidden
    ) {
        using MethodReach reach = new(folder, forbidden);
        var root = reach.modules[assembly];
        foreach (var handle in root.Reader.MethodDefinitions) {
            reach.Reach(new(root, handle), null);
        }

        reach.Walk();
        return [..reach.found.Select(call => $"{call.Called} in {call.By}, reached from {reach.Roots(call.By, root)}")];
    }

    public void Dispose() {
        foreach (var module in modules.Values) {
            module.Dispose();
        }
    }

    void Walk() {
        while (waiting.TryDequeue(out var method)) {
            Companions(method);
            if (method.Definition.RelativeVirtualAddress == 0) {
                continue;
            }

            var body = method.Module.Pe.GetMethodBody(method.Definition.RelativeVirtualAddress);
            foreach (var token in Calls(body.GetILReader())) {
                Called(method, MetadataTokens.EntityHandle(token));
            }
        }
    }

    /// <summary>What runs without being called by name: a type's static constructor, and an async method's body.</summary>
    void Companions(Method method) {
        var reader = method.Module.Reader;
        var type = reader.GetTypeDefinition(method.Definition.GetDeclaringType());
        foreach (var handle in type.GetMethods()) {
            if (reader.StringComparer.Equals(reader.GetMethodDefinition(handle).Name, ".cctor")) {
                Reach(new(method.Module, handle), method);
            }
        }

        var prefix = $"<{reader.GetString(method.Definition.Name)}>";
        foreach (var nested in type.GetNestedTypes()) {
            if (reader.GetString(reader.GetTypeDefinition(nested).Name).StartsWith(prefix, StringComparison.Ordinal)) {
                ReachAll(method.Module, nested, method, static _ => true);
            }
        }

        if (reader.StringComparer.Equals(method.Definition.Name, ".ctor")) {
            ReachAll(method.Module, method.Definition.GetDeclaringType(), method, static d => (d.Attributes & MethodAttributes.Virtual) != 0);
        }
    }

    void ReachAll(Module module, TypeDefinitionHandle type, Method by, Func<MethodDefinition, bool> which) {
        foreach (var handle in module.Reader.GetTypeDefinition(type).GetMethods()) {
            if (which(module.Reader.GetMethodDefinition(handle))) {
                Reach(new(module, handle), by);
            }
        }
    }

    void Reach(Method method, Method? by) {
        if (!callers.TryGetValue(method, out var known)) {
            callers[method] = known = [];
            waiting.Enqueue(method);
        }

        if (by is not null) {
            known.Add(by);
        }
    }

    /// <summary>The methods of the root assembly a method is reached from, a few of them.</summary>
    string Roots(Method method, Module root) {
        HashSet<Method> seen = [method];
        Queue<Method> next = new([method]);
        SortedSet<string> roots = new(StringComparer.Ordinal);
        while (next.TryDequeue(out var at)) {
            if (at.Module == root) {
                roots.Add(at.ToString());
                continue;
            }

            foreach (var by in callers[at].Where(seen.Add)) {
                next.Enqueue(by);
            }
        }

        return string.Join(", ", roots.Take(8));
    }

    void Called(Method by, EntityHandle handle) {
        var reader = by.Module.Reader;
        switch (handle.Kind) {
            case HandleKind.MethodDefinition:
                Reach(new(by.Module, (MethodDefinitionHandle)handle), by);
                break;
            case HandleKind.MethodSpecification:
                Called(by, reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method);
                break;
            case HandleKind.MemberReference:
                var member = reader.GetMemberReference((MemberReferenceHandle)handle);
                if (member.GetKind() == MemberReferenceKind.Method && by.Module.Owner(member.Parent) is { } owner) {
                    Named(by, owner.Assembly ?? by.Module.Name, owner.Type, reader.GetString(member.Name));
                }

                break;
        }
    }

    void Named(Method by, string assembly, string type, string name) {
        if (forbidden(assembly, type)) {
            found.Add(($"{type}.{name}", by));
            return;
        }

        if (!modules.TryGetValue(assembly, out var module) || module.Type(type) is not { } handle) {
            return;
        }

        ReachAll(module, handle, by, d => module.Reader.StringComparer.Equals(d.Name, name));
    }

    static IEnumerable<int> Calls(BlobReader il) {
        List<int> tokens = [];
        while (il.RemainingBytes > 0) {
            short code = il.ReadByte();
            if (code == 0xFE) {
                code = (short)(0xFE00 | il.ReadByte());
            }

            var operand = Operands[code];
            if (operand == OperandType.InlineMethod) {
                tokens.Add(il.ReadInt32());
            } else {
                Skip(ref il, operand);
            }
        }

        return tokens;
    }

    static void Skip(ref BlobReader il, OperandType operand) {
        var size = operand switch {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 * il.ReadInt32(),
            _ => 4
        };
        il.Offset += size;
    }

    sealed class FoundOrder : IComparer<(string Called, Method By)> {
        public static readonly FoundOrder Instance = new();

        public int Compare((string Called, Method By) x, (string Called, Method By) y) {
            var called = string.CompareOrdinal(x.Called, y.Called);
            return called != 0 ? called : string.CompareOrdinal(x.By.ToString(), y.By.ToString());
        }
    }

    sealed record Method(Module Module, MethodDefinitionHandle Handle) {
        public MethodDefinition Definition => Module.Reader.GetMethodDefinition(Handle);

        public override string ToString() {
            var reader = Module.Reader;
            return $"{Module.FullName(Definition.GetDeclaringType())}.{reader.GetString(Definition.Name)}";
        }
    }

    sealed class Module : IDisposable {
        readonly Dictionary<string, TypeDefinitionHandle> types = new(StringComparer.Ordinal);

        Module(PEReader pe) {
            Pe = pe;
            Reader = pe.GetMetadataReader();
            Name = Reader.GetString(Reader.GetAssemblyDefinition().Name);
            foreach (var handle in Reader.TypeDefinitions) {
                types[FullName(handle)] = handle;
            }
        }

        public PEReader Pe { get; }
        public MetadataReader Reader { get; }
        public string Name { get; }

        public static Module Open(string file) =>
            new(new(ImmutableArray.Create(File.ReadAllBytes(file))));

        public TypeDefinitionHandle? Type(string fullName) =>
            types.TryGetValue(fullName, out var handle) ? handle : null;

        public string FullName(TypeDefinitionHandle handle) {
            var type = Reader.GetTypeDefinition(handle);
            var name = Reader.GetString(type.Name);
            return type.GetDeclaringType() is { IsNil: false } outer
                ? $"{FullName(outer)}/{name}"
                : Joined(Reader.GetString(type.Namespace), name);
        }

        /// <summary>The assembly, or null for this one, and the full name of the type a member belongs to.</summary>
        public (string? Assembly, string Type)? Owner(EntityHandle parent) =>
            parent.Kind switch {
                HandleKind.TypeDefinition => (null, FullName((TypeDefinitionHandle)parent)),
                HandleKind.TypeReference => Referenced((TypeReferenceHandle)parent),
                HandleKind.TypeSpecification => Generic((TypeSpecificationHandle)parent),
                _ => null
            };

        (string? Assembly, string Type)? Generic(TypeSpecificationHandle handle) {
            var blob = Reader.GetBlobReader(Reader.GetTypeSpecification(handle).Signature);
            if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) {
                return null;
            }

            _ = blob.ReadSignatureTypeCode();
            return Owner(blob.ReadTypeHandle());
        }

        (string? Assembly, string Type) Referenced(TypeReferenceHandle handle) {
            var type = Reader.GetTypeReference(handle);
            var name = Reader.GetString(type.Name);
            var scope = type.ResolutionScope;
            if (scope.Kind == HandleKind.TypeReference) {
                var (assembly, outer) = Referenced((TypeReferenceHandle)scope);
                return (assembly, $"{outer}/{name}");
            }

            var full = Joined(Reader.GetString(type.Namespace), name);
            return scope.Kind == HandleKind.AssemblyReference
                ? (Reader.GetString(Reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name), full)
                : (null, full);
        }

        static string Joined(string space, string name) => space.Length == 0 ? name : $"{space}.{name}";

        public void Dispose() => Pe.Dispose();
    }
}
