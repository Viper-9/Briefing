using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.VectorGraphics;
using UnityEngine;

namespace Briefing.Svg;

// 층 SVG 를 Sprite 로. DynamicMaps 의 SvgUtils 와 같은 순서다(ilspycmd 로 확인).
// ImportSVG 다음 TessellateScene 은 꼭짓점 65,500 을 넘지 않는 가장 고운 설정으로, 그다음 BuildSprite.
// (폴더, svg) 마다 한 번 만들어 게임을 끌 때까지 둔다. 실패는 캐시하지 않고 그 층만 빠진다.
// 파싱과 테셀레이션은 스레드 풀에서, Sprite 만들기는 메인 스레드에서 한다. 층마다 걸린 시간과 꼭짓점 수를 로그에 남긴다.
internal static class SvgSprites
{
    private const int VertexBudget = 65500;
    private static readonly Regex ViewBoxRegex = new Regex(@"<svg[^>]*\sviewBox=""([^""]+)""", RegexOptions.Compiled);
    private static readonly VectorUtils.TessellationOptions[] Presets =
    {
        Preset(1.5f, 0.2f, 0.2f, 0.04f),
        Preset(2f, 0.3f, 0.25f, 0.05f),
        Preset(4f, 0.4f, 0.3f, 0.06f),
        Preset(6f, 0.5f, 0.4f, 0.07f),
        Preset(8f, 0.6f, 0.5f, 0.08f),
    };
    private static readonly System.Threading.SemaphoreSlim TessellateGate = new System.Threading.SemaphoreSlim(1, 1);
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Task<Sprite>> Building = new Dictionary<string, Task<Sprite>>(); // 진행 중인 빌드

    internal static bool TryGet(string folder, string svg, out Sprite sprite) => Cache.TryGetValue(Key(folder, svg), out sprite) && sprite != null;

    // 같은 층을 이미 만드는 중이면 그 Task 에 합류해 두 번 테셀레이션하지 않는다. 같은 키면 같은 파일이라 합류한 쪽의 text 는 버린다.
    // 메인 스레드에서만 부르므로 두 사전은 잠그지 않는다.
    internal static Task<Sprite> BuildAsync(string folder, string svg, string text)
    {
        var key = Key(folder, svg);
        if (TryGet(folder, svg, out var cached)) return Task.FromResult(cached);
        if (Building.TryGetValue(key, out var running)) return running;
        var task = BuildNewAsync(key, text);
        if (!task.IsCompleted) Building[key] = task; // viewBox 가 없으면 바로 끝나므로 넣지 않는다
        return task;
    }

    // 실패해도 목록에서 빠지므로 다음 호출이 새로 만든다.
    private static async Task<Sprite> BuildNewAsync(string key, string text)
    {
        try { return await BuildSpriteAsync(key, text); }
        finally { Building.Remove(key); }
    }

    private static async Task<Sprite> BuildSpriteAsync(string key, string text)
    {
        var viewBox = ViewBox(text);
        if (viewBox == null)
        {
            Log.Warn($"map svg {key}: no usable viewBox, layer skipped");
            return null;
        }
        // ImportSVG 와 TessellateScene 은 스레드 안전하지 않다. 두 SVG 를 차례로 하면 1초 남짓인데 동시에 돌리면
        // 60초가 지나도 끝나지 않았다. 그래서 한 번에 하나만 돌린다.
        var waited = Stopwatch.StartNew();
        await TessellateGate.WaitAsync();
        var waitMs = waited.ElapsedMilliseconds;
        Tessellation work;
        try { work = await Task.Run(() => Tessellate(text)); }
        finally { TessellateGate.Release(); }
        if (work.Geoms == null)
        {
            Log.Warn($"map svg {key}: over the vertex budget at every preset, layer skipped");
            return null;
        }
        var sw = Stopwatch.StartNew();
        var sprite = VectorUtils.BuildSprite(work.Geoms, viewBox.Value, 1f, VectorUtils.Alignment.Center, Vector2.zero, 32, true);
        Log.Info($"map svg {key}: {text.Length} chars, parse {work.ParseMs} ms + tessellate {work.TessellateMs} ms off the main thread " + (waitMs > 0 ? $"(wait {waitMs} ms) " : "") +
            $"(preset {work.Preset}, {work.Vertices} vertices), sprite {sw.ElapsedMilliseconds} ms");
        if (work.Flattened > 0) Log.Info($"map svg {key}: flattened {work.Flattened} gradient(s) to solid colors");
        if (sprite != null) Cache[key] = sprite;
        return sprite;
    }

    private sealed class Tessellation
    {
        internal List<VectorUtils.Geometry> Geoms;
        internal int Preset;
        internal int Vertices;
        internal long ParseMs;
        internal long TessellateMs;
        internal int Flattened;
    }

    // 그라디언트가 있으면 BuildSprite 가 아틀라스 텍스처를 붙이는데, 게임에 VectorGradientUI 셰이더가 없어 SVGImage 가
    // 기본 UI 머티리얼로 그 텍스처를 곱하고 층 전체가 어두워진다. 그래서 그라디언트를 stop 평균색 단색으로 바꾼다.
    // DynamicMaps 는 그라디언트를 뺀 SVG 를 따로 싣는다. 바꾼 개수를 돌려준다.
    private static int Flatten(SceneNode node)
    {
        if (node == null) return 0;
        var count = 0;
        if (node.Shapes != null)
            foreach (var shape in node.Shapes)
            {
                if (shape == null) continue;
                if (shape.Fill is GradientFill fill)
                {
                    shape.Fill = new SolidFill { Color = Average(fill), Opacity = fill.Opacity };
                    count++;
                }
                var stroke = shape.PathProps.Stroke; // PathProps 는 struct 라 복사본이지만 Stroke 는 class 라서 바꾸면 반영된다
                if (stroke?.Fill is GradientFill strokeFill)
                {
                    stroke.Fill = new SolidFill { Color = Average(strokeFill), Opacity = strokeFill.Opacity };
                    count++;
                }
            }
        if (node.Children != null)
            foreach (var child in node.Children) count += Flatten(child);
        return count;
    }

    // stop 이 없으면 투명.
    private static Color Average(GradientFill fill)
    {
        var stops = fill.Stops;
        if (stops == null || stops.Length == 0) return new Color(0f, 0f, 0f, 0f);
        float r = 0, g = 0, b = 0, a = 0;
        foreach (var s in stops) { r += s.Color.r; g += s.Color.g; b += s.Color.b; a += s.Color.a; }
        var n = stops.Length;
        return new Color(r / n, g / n, b / n, a / n);
    }

    private static Tessellation Tessellate(string text)
    {
        var sw = Stopwatch.StartNew();
        SVGParser.SceneInfo scene;
        using (var reader = new StringReader(text)) scene = SVGParser.ImportSVG(reader, ViewportOptions.OnlyApplyRootViewBox);
        var result = new Tessellation { Flattened = Flatten(scene.Scene.Root) };
        result.ParseMs = sw.ElapsedMilliseconds;
        for (var i = 0; i < Presets.Length; i++)
        {
            var geoms = VectorUtils.TessellateScene(scene.Scene, Presets[i], scene.NodeOpacity);
            var vertices = geoms.Sum(g => g.Vertices?.Length ?? 0);
            if (vertices > VertexBudget) continue;
            result.Geoms = geoms;
            result.Preset = i;
            result.Vertices = vertices;
            break;
        }
        result.TessellateMs = sw.ElapsedMilliseconds - result.ParseMs;
        return result;
    }

    private static string Key(string folder, string svg) => folder + "/" + svg;

    private static VectorUtils.TessellationOptions Preset(float step, float cord, float tan, float sampling) =>
        new VectorUtils.TessellationOptions { StepDistance = step, MaxCordDeviation = cord, MaxTanAngleDeviation = tan, SamplingStepSize = sampling };

    // DynamicMaps 의 GetViewbox 와 같은 규칙. 숫자 4개이고 폭과 높이가 0 보다 커야 한다.
    private static Rect? ViewBox(string text)
    {
        var match = ViewBoxRegex.Match(text ?? "");
        if (!match.Success) return null;
        var parts = match.Groups[1].Value.Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return null;
        var v = new float[4];
        for (var i = 0; i < 4; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return null;
        return v[2] > 0 && v[3] > 0 ? new Rect(v[0], v[1], v[2], v[3]) : (Rect?)null;
    }
}
