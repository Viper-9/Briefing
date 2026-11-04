using System.Linq;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using UnityEngine;

namespace Briefing.Game;

// 게임 도감의 아이템 메뉴를 그대로 띄운다. 프리셋 편집, 플리마켓 검색 3종, 위시리스트뿐이라 보유하지 않은 아이템에 써도 안전하다.
// 퀘스트 아이템과 레이드 중에는 띄우지 않는다. 조사하지 않은 아이템처럼 남는 항목이 없을 때도 빈 상자가 뜨지 않게 거른다.
internal static class FleaMenu
{
    internal static void Show(Item item, Vector2 position)
    {
        if (item == null || item.QuestItem || TasksPanel.InRaid()) return;
        var ui = ItemUiContext.Instance;
        if (ui == null) return;
        var interactions = new Interactions(new DefaultItemContext(item, EItemViewType.Handbook), ui);
        if (!interactions.AllInteractions.Any(interactions.IsActive)) return;
        ui.ContextMenu.Show(position, interactions, null, item);
    }

    // 플리마켓을 닫으면 게임이 기억하는 탭이 열리므로, 검색을 누를 때 브리핑 탭 복귀를 요청해 둔다.
    private sealed class Interactions : HandbookContextInteractions
    {
        internal Interactions(ItemContext context, ItemUiContext ui) : base(context, ui)
        {
        }

        public override void Search(RagfairSearch ragfairSearch)
        {
            TraderJump.RequestReturn();
            base.Search(ragfairSearch);
        }
    }
}
