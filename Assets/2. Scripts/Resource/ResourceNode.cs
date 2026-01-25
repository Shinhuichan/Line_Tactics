using UnityEngine;

public enum ResourceType
{
    Iron,
    Oil
}
public class ResourceNode : MonoBehaviour
{
    public ResourceType resourceType;
    public string nodeName;
    
    // 🌟 [신규] UI 표시용 아이콘 (Inspector에서 할당 필수)
    [Header("UI Info")]
    public Sprite icon; 

    [Header("자원량 설정")]
    public int maxAmount = 500; // 총 매장량
    public int currentAmount;

    void Start()
    {
        currentAmount = maxAmount;
    }

    // ⛏️ 채집 요청 함수 (실제 캔 양을 반환)
    public int Harvest(int amountToHarvest)
    {
        if (currentAmount <= 0) return 0;

        int actualAmount = Mathf.Min(amountToHarvest, currentAmount);
        currentAmount -= actualAmount;

        if (currentAmount <= 0)
        {
            Deplete();
        }
        // 🌟 [신규] 호버 중이라면 실시간 갱신을 위해 UI 다시 호출 (선택 사항)
        // (UnitInfoPanel이 매 프레임 갱신하는 구조가 아니므로, 변화가 있을 때 다시 호출하면 좋음)
        // 하지만 마우스가 위에 있을 때만 갱신하는 것이 효율적이므로 여기서는 생략하고
        // 필요하다면 OnMouseOver()에서 호출할 수 있습니다.
        
        return actualAmount;
    }

    void Deplete()
    {
        // 고갈 시 정보창 끄기 (마우스가 위에 있어도 사라지므로)
        if (UnitInfoPanel.I != null) UnitInfoPanel.I.HideInfo();

        Debug.Log($"{nodeName} 자원이 고갈되었습니다.");
        Destroy(gameObject);
    }

    // ==================================================================================
    // 🖱️ [신규] 마우스 호버 시 정보창 표시 (Collider2D 필요)
    // ==================================================================================
    private void OnMouseEnter()
    {
        if (UnitInfoPanel.I != null && icon != null)
        {
            UnitInfoPanel.I.ShowResourceInfo(this);
        }
    }

    // 마우스가 머무르는 동안 계속 갱신하고 싶다면 아래 주석 해제 (자원 채취 시 숫자 줄어드는 거 보임)
    /*
    private void OnMouseOver()
    {
        if (UnitInfoPanel.I != null && icon != null)
        {
            UnitInfoPanel.I.ShowResourceInfo(this);
        }
    }
    */

    private void OnMouseExit()
    {
        if (UnitInfoPanel.I != null)
        {
            UnitInfoPanel.I.HideInfo();
        }
    }
}