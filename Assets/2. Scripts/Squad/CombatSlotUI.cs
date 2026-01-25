using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatSlotUI : MonoBehaviour
{
    public Image iconImage; // 유닛 아이콘 (나중에 Sprite 매핑 필요)
    public TextMeshProUGUI statusText;
    public Button slotButton;

    public void Setup(Squad squad, int index)
    {
        CombatSlot slot = squad.slots[index];

        // 텍스트 표시
        string unitName = slot.requiredType.ToString();
        if (slot.IsFilled)
        {
            statusText.text = $"{unitName}\n(Ready)";
            statusText.color = Color.green;
        }
        else
        {
            statusText.text = $"{unitName}\n(Searching...)";
            statusText.color = Color.yellow;
        }

        // 클릭 시 변경 (팝업 호출)
        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(() => 
        {
             SquadManager.I.selectionPopup.Open(squad, index);
        });
    }
}