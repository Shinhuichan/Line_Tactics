using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UnitSelectionPopup : MonoBehaviour
{
    public GameObject buttonPrefab;
    public Transform buttonContainer;
    
    // 팝업이 떴을 때 어떤 슬롯을 수정 중인지 기억
    private Squad targetSquad;
    private int targetSlotIndex = -1; // -1이면 '새 슬롯 추가' 모드

    public void Open(Squad squad, int slotIndex = -1)
    {
        targetSquad = squad;
        targetSlotIndex = slotIndex;
        
        gameObject.SetActive(true);
        RefreshButtons();
    }

    void RefreshButtons()
    {
        foreach(Transform child in buttonContainer) Destroy(child.gameObject);

        // Enum의 모든 값 가져오기 (Worker 등 제외하고 싶으면 필터링)
        foreach(UnitType type in System.Enum.GetValues(typeof(UnitType)))
        {
            if (type == UnitType.Worker) continue; // 노동병 제외
            if (type == UnitType.BaseArcher) continue; 

            GameObject btnObj = Instantiate(buttonPrefab, buttonContainer);
            btnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = type.ToString();
            
            btnObj.GetComponent<Button>().onClick.AddListener(() => OnSelectType(type));
        }
    }

    void OnSelectType(UnitType type)
    {
        if (targetSlotIndex == -1)
        {
            // 새 슬롯 추가
            targetSquad.AddSlot(type);
        }
        else
        {
            // 기존 슬롯 변경
            // (기존 유닛이 있다면 해임 처리 로직 필요)
            var slot = targetSquad.slots[targetSlotIndex];
            if(slot.assignedUnit != null) slot.assignedUnit.assignedSquad = null; // 해임
            
            slot.requiredType = type;
            slot.assignedUnit = null; // 새 유닛을 찾아야 하므로 초기화
        }

        SquadManager.I.RefreshSquadUI(); // 전체 갱신
        gameObject.SetActive(false); // 팝업 닫기
    }
    
    public void Close()
    {
        gameObject.SetActive(false);
    }
}