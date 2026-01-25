using UnityEngine;

public class EnemyResourceManager : SingletonBehaviour<EnemyResourceManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("적 자원 상태")]
    public int maxIron = 3000;
    public int currentIron = 300; // 시작 자원
    public int maxOil = 500;
    public int currentOil = 0;

    [Header("자동 수급 (난이도 보정용)")]
    public bool useAutoRegen = true;
    public float regenInterval = 5.0f;
    public int ironRegenAmount = 5; 

    private float timer = 0f;

    void Update()
    {
        if (useAutoRegen && currentIron < maxIron)
        {
            timer += Time.deltaTime;
            if (timer >= regenInterval)
            {
                timer = 0f;
                
                // 🏰 [신규] 적군도 농성 모드면 자원 수급 5배
                int amountToAdd = ironRegenAmount;

                // EnemyBot.enemyState는 static 변수이므로 바로 접근 가능
                if (EnemyBot.enemyState == TacticalState.Siege)
                {
                    amountToAdd *= 5;
                }

                AddResource(amountToAdd, 0);
            }
        }
    }

    public bool CheckCost(int iron, int oil)
    {
        return currentIron >= iron && currentOil >= oil;
    }

    public void SpendResource(int iron, int oil)
    {
        currentIron -= iron;
        currentOil -= oil;
    }

    public void AddResource(int iron, int oil)
    {
        currentIron += iron;
        currentOil += oil;

        if (currentIron > maxIron) currentIron = maxIron;
        if (currentOil > maxOil) currentOil = maxOil;
    }
}