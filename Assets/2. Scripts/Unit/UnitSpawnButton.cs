using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 🖱️ 마우스 이벤트 필수
using TMPro;
using CustomInspector;

// 🌟 인터페이스 추가 (IPointerEnterHandler, IPointerExitHandler)
public class UnitSpawnButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Unit Button")]
    [Range(0, 15)] public int unitIndex;

    [Header("UI References")]
    public Image unitIconImage; 
    private TextMeshProUGUI infoText; 
    private Button btn;

    void Start()
    {
        btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClick);

        infoText = GetComponentInChildren<TextMeshProUGUI>();
        
        UpdateUnitInfo();
    }

    public void UpdateUnitInfo()
    {
        if (SpawnManager.I != null)
        {
            UnitData data = SpawnManager.I.GetUnitData(unitIndex);

            if (data != null)
            {
                gameObject.SetActive(true);

                string costText = "";
                if (data.oilCost > 0)
                    costText = $"Fe:{data.ironCost} <color=red>Oil:{data.oilCost}</color>";
                else
                    costText = $"Fe:{data.ironCost}";

                if (infoText != null)
                    infoText.text = $"{data.unitName}\n<size=70%>{costText}</size>";

                if (unitIconImage != null && data.icon != null)
                {
                    unitIconImage.sprite = data.icon;
                    unitIconImage.enabled = true;
                }
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    void OnClick()
    {
        if (SpawnManager.I != null)
        {
            SpawnManager.I.SpawnUnit(unitIndex);
        }
    }

    // 🖱️ [신규] 마우스가 버튼에 들어왔을 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SpawnManager.I != null && UnitInfoPanel.I != null)
        {
            // 내 인덱스에 해당하는 데이터를 가져와서 패널에 전달
            UnitData myData = SpawnManager.I.GetUnitData(unitIndex);
            UnitInfoPanel.I.ShowUnitInfo(myData);
        }
    }

    // 🖱️ [신규] 마우스가 버튼에서 나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        if (UnitInfoPanel.I != null)
        {
            UnitInfoPanel.I.HideInfo();
        }
    }
}