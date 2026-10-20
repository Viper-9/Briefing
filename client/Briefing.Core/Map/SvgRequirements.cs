using System.Collections.Generic;

namespace Briefing.Core.Map;

// Briefing.Svg 가 쓰는 Unity.VectorGraphics 타입. 로드된 VectorGraphics 에 하나라도 없으면 Briefing.Svg 를 올리지 않는다
// (DynamicMaps 가 다른 버전을 넣어 둔 경우). Assembly.GetType 이름 형식이라 중첩 타입은 '+' 로 잇는다.
// Briefing.Svg 에서 새 타입을 쓰면 AssemblyBoundaryTests 가 실패하므로 여기에 더한다.
public static class SvgRequirements
{
    public static readonly IReadOnlyList<string> VectorGraphicsTypes = new[]
    {
        "Unity.VectorGraphics.GradientFill",
        "Unity.VectorGraphics.GradientStop",
        "Unity.VectorGraphics.IFill",
        "Unity.VectorGraphics.PathProperties",
        "Unity.VectorGraphics.Scene",
        "Unity.VectorGraphics.SceneNode",
        "Unity.VectorGraphics.Shape",
        "Unity.VectorGraphics.SolidFill",
        "Unity.VectorGraphics.Stroke",
        "Unity.VectorGraphics.SVGImage",
        "Unity.VectorGraphics.SVGParser",
        "Unity.VectorGraphics.SVGParser+SceneInfo",
        "Unity.VectorGraphics.VectorUtils",
        "Unity.VectorGraphics.VectorUtils+Alignment",
        "Unity.VectorGraphics.VectorUtils+Geometry",
        "Unity.VectorGraphics.VectorUtils+TessellationOptions",
        "Unity.VectorGraphics.ViewportOptions",
    };
}
