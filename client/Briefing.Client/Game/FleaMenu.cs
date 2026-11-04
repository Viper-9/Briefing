using System.Linq;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.UI;
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
        var interactions = new HandbookContextInteractions(new DefaultItemContext(item, EItemViewType.Handbook), ui);
        if (!interactions.AllInteractions.Any(interactions.IsActive)) return;
        ui.ContextMenu.Show(position, interactions, null, item);
    }
}
