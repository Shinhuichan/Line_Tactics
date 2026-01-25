using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeSlot : MonoBehaviour
{
    [Header("UI Components")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI costText;
    public Button purchaseButton;
    
    [Header("State Overlays")]
    public GameObject lockCover;     // 잠김 상태 (선행 연구 부족)
    public GameObject completeCheck; // 연구 완료 표시
    public GameObject researchingCover; // (선택) 연구 중 표시

    private UpgradeData _data;

    // 초기화 (UpgradeUI에서 호출)
    public void Setup(UpgradeData data)
    {
        _data = data;

        // 1. 기본 정보 표시 (변하지 않는 값)
        if (iconImage != null) iconImage.sprite = data.icon;
        if (nameText != null) nameText.text = data.upgradeName;
        if (descriptionText != null) descriptionText.text = data.description;

        // 버튼 리스너 연결
        purchaseButton.onClick.RemoveAllListeners();
        purchaseButton.onClick.AddListener(OnPurchaseClick);
        
        // 툴팁 설정 (선택 사항)
        if (UIManager.I != null)
        {
            string tooltip = $"<color=yellow>비용: 철 {data.ironCost} / 기름 {data.oilCost}</color>";
            UIManager.I.TrySetTooltip("UpgradePopup", "Slot", tooltip, data.upgradeName);
        }

        // 초기 상태 한 번 갱신
        UpdateState();
    }

    // 🔄 매 프레임 상태 갱신 (자원 변동, 연구 완료 실시간 반영)
    void Update()
    {
        if (_data == null) return;
        UpdateState();
    }

    void UpdateState()
    {
        if (UpgradeManager.I == null) return;

        string myTag = "Player"; // UI는 플레이어 전용

        // 1. 이미 완료된 연구인가?
        if (UpgradeManager.I.IsUnlocked(_data, myTag))
        {
            purchaseButton.interactable = false;
            if (completeCheck) completeCheck.SetActive(true);
            if (lockCover) lockCover.SetActive(false);
            if (researchingCover) researchingCover.SetActive(false);
            if (costText) costText.text = "완료";
            return;
        }

        // 2. 현재 연구 중인가? (시간이 걸리는 연구일 경우)
        if (UpgradeManager.I.IsResearching(_data, myTag))
        {
            purchaseButton.interactable = false;
            if (completeCheck) completeCheck.SetActive(false);
            if (lockCover) lockCover.SetActive(false);
            if (researchingCover) researchingCover.SetActive(true);
            if (costText) costText.text = "연구 중...";
            return;
        }
        
        // 연구 중 커버가 있다면 끄기
        if (researchingCover) researchingCover.SetActive(false);

        // 3. 연구 가능한가? (선행 연구 조건)
        if (UpgradeManager.I.IsResearchable(_data, myTag))
        {
            // A. 자원 체크
            bool canAfford = false;
            if (ResourceManager.I != null)
            {
                canAfford = ResourceManager.I.CheckCost(_data.ironCost, _data.oilCost);
            }

            // 자원이 부족해도 버튼은 활성화(눌러서 피드백 받기 위함) 하거나, 비활성화 선택
            // 여기서는 버튼은 켜두되 텍스트 색상으로 경고
            purchaseButton.interactable = true; 
            if (completeCheck) completeCheck.SetActive(false);
            if (lockCover) lockCover.SetActive(false);

            // 텍스트 색상 처리
            string ironColor = (ResourceManager.I.currentIron >= _data.ironCost) ? "blue" : "red";
            string oilColor = (ResourceManager.I.currentOil >= _data.oilCost) ? "blue" : "red";
            
            // 기름이 필요 없는 경우 철만 표시
            if (_data.oilCost > 0)
                costText.text = $"<color={ironColor}>{_data.ironCost}Fe</color> / <color={oilColor}>{_data.oilCost}Oil</color>";
            else
                costText.text = $"<color={ironColor}>{_data.ironCost}Fe</color>";
        }
        else
        {
            // 4. 잠김 (선행 연구 부족)
            purchaseButton.interactable = false;
            if (completeCheck) completeCheck.SetActive(false);
            if (lockCover) lockCover.SetActive(true); // 어둡게 처리
            if (costText) costText.text = "잠김";
        }
    }

    void OnPurchaseClick()
    {
        if (UpgradeManager.I != null)
        {
            UpgradeManager.I.PurchaseUpgrade(_data, "Player");
            // 클릭 즉시 상태 갱신 (반응성 향상)
            UpdateState();
        }
    }
}