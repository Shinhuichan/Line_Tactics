using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SquadUI : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public Transform slotContainer;
    public GameObject slotUIPrefab;
    
    [Header("버튼 연결")]
    public Button addSlotButton; // (+) 버튼
    public Button musterButton;  // 🌟 [신규] 출동(Apply) 버튼

    private Squad mySquad;

    void Update()
    {
        // 🌟 실시간 버튼 상태 갱신
        if (mySquad != null && musterButton != null)
        {
            // 이미 출동했으면 버튼 숨기기 or 비활성화
            if (mySquad.state == SquadState.Active)
            {
                musterButton.interactable = false;
                musterButton.GetComponentInChildren<TextMeshProUGUI>().text = "Active";
            }
            else
            {
                // 편성 중: 슬롯이 3개 이상이어야 출동 가능
                bool canMuster = mySquad.slots.Count >= 3;
                musterButton.interactable = canMuster;
                musterButton.GetComponentInChildren<TextMeshProUGUI>().text = canMuster ? "Muster!" : "Need 3+";
            }
        }
    }

    public void Setup(Squad squad)
    {
        mySquad = squad;
        titleText.text = squad.squadName;

        RefreshSlots();

        // (+) 버튼: 슬롯 추가
        addSlotButton.onClick.RemoveAllListeners();
        addSlotButton.onClick.AddListener(() => 
        {
            SquadManager.I.selectionPopup.Open(squad, -1);
        });

        // 🌟 [신규] 출동 버튼: 상태 변경
        if (musterButton != null)
        {
            musterButton.onClick.RemoveAllListeners();
            musterButton.onClick.AddListener(() =>
            {
                mySquad.ActivateSquad(); // 상태를 Active로 변경
                SquadManager.I.RefreshSquadUI(); // UI 갱신
            });
        }
    }

    // 슬롯 UI 다시 그리기 (외부에서 호출 가능하게 public)
    public void RefreshSlots()
    {
        foreach(Transform child in slotContainer) Destroy(child.gameObject);
        
        for(int i=0; i<mySquad.slots.Count; i++)
        {
            GameObject obj = Instantiate(slotUIPrefab, slotContainer);
            obj.GetComponent<CombatSlotUI>().Setup(mySquad, i);
        }
    }
}