using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Briefing.Core.Map;

namespace Briefing.Tests;

// Unity.VectorGraphics 는 바닐라 EFT 에 없다. Briefing.Client 가 이것을 참조하면 다른 모드가 모든 어셈블리에 GetTypes 를 부를 때
// ReflectionTypeLoadException 이 나서 게임 로딩이 멈춘다(DynamicMaps 가 없을 때). 그래서 VectorGraphics 는 Briefing.Svg 만 참조한다.
public class AssemblyBoundaryTests
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    [Fact]
    public void Client_does_not_reference_vector_graphics()
    {
        var refs = AssemblyRefs(Built("Briefing.Client"));
        Assert.Contains("UnityEngine.CoreModule", refs); // 목록을 제대로 읽었는지
        Assert.DoesNotContain("Unity.VectorGraphics", refs);
        Assert.DoesNotContain("Unity.InternalAPIEngineBridge.003", refs);
        Assert.DoesNotContain("Briefing.Svg", refs);
    }

    // SvgLoader 는 이 목록의 타입이 다 있을 때만 Briefing.Svg 를 올린다. 목록에서 빠진 타입이 있으면 점검을 통과하고도
    // 타입 로드가 깨질 수 있다.
    [Fact]
    public void Svg_uses_only_vector_graphics_types_the_loader_checks()
    {
        var used = TypeRefs(Built("Briefing.Svg"), "Unity.VectorGraphics");
        Assert.Contains("Unity.VectorGraphics.SVGImage", used);
        Assert.Empty(used.Except(SvgRequirements.VectorGraphicsTypes).OrderBy(n => n));
    }

    private static string Built(string project)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Briefing.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir.FullName, "client", project, "bin", Configuration, "netstandard2.1", project + ".dll");
        Assert.True(File.Exists(path), $"{path} not built");
        return path;
    }

    private static List<string> AssemblyRefs(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        return md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name)).ToList();
    }

    // assembly 에서 오는 TypeRef 의 이름(중첩 타입은 Outer+Inner).
    private static HashSet<string> TypeRefs(string path, string assembly)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var names = new HashSet<string>();
        foreach (var h in md.TypeReferences)
        {
            var name = Name(md, h, out var scope);
            if (scope.Kind == HandleKind.AssemblyReference &&
                md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name) == assembly)
                names.Add(name);
        }
        return names;
    }

    private static string Name(MetadataReader md, TypeReferenceHandle h, out EntityHandle scope)
    {
        var t = md.GetTypeReference(h);
        if (t.ResolutionScope.Kind == HandleKind.TypeReference)
            return Name(md, (TypeReferenceHandle)t.ResolutionScope, out scope) + "+" + md.GetString(t.Name);
        scope = t.ResolutionScope;
        var ns = md.GetString(t.Namespace);
        return ns.Length == 0 ? md.GetString(t.Name) : ns + "." + md.GetString(t.Name);
    }
}
