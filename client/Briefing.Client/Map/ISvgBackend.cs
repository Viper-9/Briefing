using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Map;

// 층 SVG 를 Sprite 로 만들고 화면에 붙이는 일. 구현은 Briefing.Svg 에 있고 SvgLoader 가 불러온다.
// Briefing.Client 가 VectorGraphics 타입을 직접 쓰지 않게 하려고 SVGImage 는 Graphic 으로만 넘긴다.
internal interface ISvgBackend
{
    bool TryGet(string folder, string svg, out Sprite sprite);

    // 메인 스레드에서만 부른다. 실패하면 null(캐시하지 않는다).
    Task<Sprite> BuildAsync(string folder, string svg, string text);

    // go 에 SVGImage 를 붙인다. raycast 와 preserveAspect 는 끄고, sprite 를 넣을 때까지 꺼 둔다.
    Graphic AddImage(GameObject go);

    void SetSprite(Graphic image, Sprite sprite);

    bool HasSprite(Graphic image);
}
