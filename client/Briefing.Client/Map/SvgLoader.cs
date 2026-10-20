using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Briefing.Core.Map;

[assembly: InternalsVisibleTo("Briefing.Svg")]

namespace Briefing.Map;

// 지도를 처음 그릴 때 Briefing.Svg 를 한 번 불러온다. 메인 스레드에서만 부른다.
// Unity.VectorGraphics 와 Unity.InternalAPIEngineBridge.003 은 바닐라 EFT 에 없고 DynamicMaps 가 Managed 에 넣는다.
// 게임 쪽 사본이 있으면 그것을 쓰고, 없을 때만 lib/ 의 동봉본을 올린다. 동봉본과 Briefing.Svg 는 확장자가 .bin 이라
// BepInEx 체인로더와 FixPluginTypesSerialization 이 시작 때 미리 올리지 않는다.
// VectorGraphics 에 필요한 타입이 다 있을 때만 Briefing.Svg 를 올린다. 타입이 빠진 채로 올리면 다른 모드가 모든 어셈블리에
// GetTypes 를 부를 때 다시 ReflectionTypeLoadException 이 나서 게임 로딩이 멈춘다.
internal static class SvgLoader
{
    private const string VectorGraphics = "Unity.VectorGraphics";
    private const string Bridge = "Unity.InternalAPIEngineBridge.003";
    private const string SvgAssembly = "Briefing.Svg";
    private const string BackendType = "Briefing.Svg.SvgBackend";

    private static bool _tried;
    private static ISvgBackend _backend;

    private static string LibDir => Path.Combine(Path.GetDirectoryName(Plugin.Instance.Info.Location)!, "lib");

    // 실패하면 null 이고, 게임을 끌 때까지 다시 시도하지 않는다.
    internal static ISvgBackend Get()
    {
        if (_tried) return _backend;
        _tried = true;
        try { _backend = Load(); }
        catch (Exception e) { Log.Error($"svg: map drawing disabled: {e}"); }
        return _backend;
    }

    private static ISvgBackend Load()
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        var vg = Assembly.Load(VectorGraphics);
        var bundled = vg.Location.StartsWith(LibDir, StringComparison.OrdinalIgnoreCase);
        Log.Info($"svg: {VectorGraphics} from {vg.Location} ({(bundled ? "bundled" : "game")})");
        var missing = SvgRequirements.VectorGraphicsTypes.Where(n => vg.GetType(n, false) == null).ToList();
        if (missing.Count > 0)
        {
            Log.Error($"svg: map drawing disabled, {VectorGraphics} has no {string.Join(", ", missing)}");
            return null;
        }
        var svg = Assembly.LoadFile(Path.Combine(LibDir, SvgAssembly + ".bin"));
        return (ISvgBackend)Activator.CreateInstance(svg.GetType(BackendType, true), true);
    }

    // 이름으로 못 찾았을 때만 불린다. DynamicMaps 가 Managed 에 넣은 사본이 있으면 VectorGraphics 는 여기까지 오지 않는다.
    // Briefing.Svg 를 LoadFile 로 올리므로 그쪽이 찾는 Briefing.Client 도 여기서 돌려준다.
    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        if (name == "Briefing.Client") return typeof(SvgLoader).Assembly;
        if (name != VectorGraphics && name != Bridge) return null;
        var path = Path.Combine(LibDir, name + ".bin");
        return File.Exists(path) ? Assembly.LoadFile(path) : null;
    }
}
