using UnityEngine;
using TMPro; // UI 표시용

public class ResourceManager : SingletonBehaviour<ResourceManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("철제 설정 (자동 회복)")]
    public int maxIron = 1000;
    public int currentIron = 100;
    public float ironRegenInterval = 1.0f; // 1초마다
    public int ironRegenAmount = 5;        // 5씩 회복

    [Header("기름 설정 (회복 안됨)")]
    public int maxOil = 100;
    public int currentOil = 50;

    [Header("UI 연결")]
    public TextMeshProUGUI ironText;
    public TextMeshProUGUI oilText;

    private float timer = 0f;

    protected override void Awake()
    {
        base.Awake();
        UpdateUI();
    }

    void Update()
    {
        // 철제 자동 회복 로직
        if (currentIron < maxIron)
        {
            timer += Time.deltaTime;
            if (timer >= ironRegenInterval)
            {
                timer = 0f;

                // 🏰 [신규] 농성 모드 시 자원 수급량 5배 증가
                int amountToAdd = ironRegenAmount;
                
                if (TacticalCommandManager.I != null && 
                    TacticalCommandManager.I.currentState == TacticalState.Siege)
                {
                    amountToAdd *= 5;
                    
                    // (선택사항) 농성 효과가 적용 중임을 알리고 싶다면 여기에 로그나 효과 추가 가능
                    // Debug.Log("농성 보너스: 자원 수급 5배!");
                }

                currentIron += amountToAdd;
                if (currentIron > maxIron) currentIron = maxIron;
                UpdateUI();
            }
        }
    }

    // 💰 구매 가능 여부 확인
    public bool CheckCost(int iron, int oil)
    {
        return currentIron >= iron && currentOil >= oil;
    }

    // 💸 자원 소비
    public void SpendResource(int iron, int oil)
    {
        currentIron -= iron;
        currentOil -= oil;
        UpdateUI();
    }

    // ➕ 자원 획득 (나중에 적 처치 보상 등으로 사용)
    public void AddResource(int iron, int oil)
    {
        currentIron += iron;
        currentOil += oil;

        if (currentIron > maxIron) currentIron = maxIron;
        if (currentOil > maxOil) currentOil = maxOil;

        UpdateUI();
        
        // 획득 연출 (선택사항)
        if (FloatingTextManager.I != null && (iron > 0 || oil > 0))
        {
            // 화면 중앙 상단쯤에 텍스트 띄우기 등의 연출 가능
        }
    }

    void UpdateUI()
    {
        if (ironText != null) ironText.text = $"{currentIron} / {maxIron}";
        if (oilText != null) oilText.text = $"{currentOil} / {maxOil}";
    }
}