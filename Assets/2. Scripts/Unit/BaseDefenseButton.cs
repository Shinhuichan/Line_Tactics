using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 🖱️ 마우스 이벤트 필수

public class BaseDefenseButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 연결")]
    public Image iconImage; 
    private Button btn;

    void Start()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClick);
    }

    void Update()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        if (SpawnManager.I == null) return;

        // 1. 현재 데이터 가져오기 (이름, 아이콘용)
        UnitData data = SpawnManager.I.GetBaseDefenseData("Player");

        if (data != null)
        {
            // 아이콘 갱신
            if (iconImage != null && data.icon != null)
            {
                iconImage.sprite = data.icon;
                iconImage.enabled = true;
            }
        }
    }

    void OnClick()
    {
        if (SpawnManager.I != null)
        {
            // 방어 유닛 소환 시도
            SpawnManager.I.TrySpawnBaseDefense("Player");
        }
    }

    // 🖱️ [신규] 마우스가 버튼에 들어왔을 때 (호버 시작)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SpawnManager.I != null && UnitInfoPanel.I != null)
        {
            // 현재 내 종족에 맞는 방어 유닛 데이터 가져오기
            UnitData myData = SpawnManager.I.GetBaseDefenseData("Player");
            
            // 정보 패널에 표시 요청
            UnitInfoPanel.I.ShowUnitInfo(myData);
        }
    }

    // 🖱️ [신규] 마우스가 버튼에서 나갔을 때 (호버 종료)
    public void OnPointerExit(PointerEventData eventData)
    {
        if (UnitInfoPanel.I != null)
        {
            // 정보 패널 숨기기
            UnitInfoPanel.I.HideInfo();
        }
    }
}