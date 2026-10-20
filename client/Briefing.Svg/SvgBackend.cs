using System.Threading.Tasks;
using Briefing.Map;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Svg;

// SvgLoader 가 리플렉션으로 만든다. 이름과 기본 생성자를 바꾸면 SvgLoader 도 바꿔야 한다.
internal sealed class SvgBackend : ISvgBackend
{
    public bool TryGet(string folder, string svg, out Sprite sprite) => SvgSprites.TryGet(folder, svg, out sprite);

    public Task<Sprite> BuildAsync(string folder, string svg, string text) => SvgSprites.BuildAsync(folder, svg, text);

    public Graphic AddImage(GameObject go)
    {
        var image = go.AddComponent<SVGImage>();
        image.raycastTarget = false;
        image.preserveAspect = false; // 층마다 viewBox 가 조금 달라도 같은 칸에 늘인다(웹과 같다)
        image.enabled = false;        // sprite 가 없으면 흰 사각형이 그려진다
        return image;
    }

    public void SetSprite(Graphic image, Sprite sprite) => ((SVGImage)image).sprite = sprite;

    public bool HasSprite(Graphic image) => ((SVGImage)image).sprite != null;
}
