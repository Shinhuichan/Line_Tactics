using UnityEngine;

public class GluttonyAbility : UnitAbility
{
    [Header("폭식 (Gluttony)")]
    [Tooltip("적 처치 시 증가할 최대 체력 비율 (0.2 = 20%)")]
    public float growthFactor = 0.2f;

    [Tooltip("최대 성장 한계치 (밸런스 및 UI 깨짐 방지용)")]
    public float maxHpCap = 5000f; 

    [Header("잡식 (Omnivore) - 업그레이드")]
    public string omnivoreUpgradeKey = "OMNIVORE"; // 🌟 업그레이드 키
    public float lifestealRatio = 0.1f;            // 흡혈 비율 (10%)

    [Header("이펙트")]
    public GameObject devourEffect; // 꿀꺽 삼키는 이펙트

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override bool OnAttack(GameObject target)
    {
        UnitController enemyUnit = target.GetComponent<UnitController>();
        BaseController enemyBase = target.GetComponent<BaseController>();

        float damage = owner.attackDamage;
        bool isKill = false;
        float damageDealt = 0f;

        // 1. 데미지 적용 및 실제 피해량 계산
        if (enemyUnit != null)
        {
            float hpBefore = enemyUnit.currentHP;
            
            // 킬 각 계산 (방어력 무시한 단순 계산이므로 참고만 함)
            if (enemyUnit.currentHP <= damage) isKill = true; 
            
            enemyUnit.TakeDamage(damage, false);
            
            // 🌟 [핵심] 실제 입힌 피해량 계산 (방어력 등으로 감소된 수치 반영)
            // 죽어서 파괴되었을 경우 hpBefore 전체를 피해량으로 간주
            if (enemyUnit == null || enemyUnit.gameObject == null) 
            {
                damageDealt = hpBefore;
                isKill = true; // 확실한 확인
            }
            else
            {
                damageDealt = Mathf.Max(0, hpBefore - enemyUnit.currentHP);
                if (enemyUnit.currentHP <= 0) isKill = true;
            }
        }
        else if (enemyBase != null)
        {
            float hpBefore = enemyBase.currentHP;
            
            if (enemyBase.currentHP <= damage) isKill = true;
            
            enemyBase.TakeDamage(damage);
            
            if (enemyBase == null || enemyBase.gameObject == null)
            {
                damageDealt = hpBefore;
            }
            else
            {
                damageDealt = Mathf.Max(0, hpBefore - enemyBase.currentHP);
            }
        }

        // 2. 🍖 [신규] 잡식(Omnivore) 능력 발동: 공격 흡혈
        if (damageDealt > 0 && UpgradeManager.I != null)
        {
            if (UpgradeManager.I.IsAbilityActive(omnivoreUpgradeKey, owner.tag))
            {
                float healAmount = damageDealt * lifestealRatio;
                if (healAmount >= 1.0f)
                {
                    // UnitController.Heal은 기본적으로 텍스트를 띄웁니다.
                    owner.Heal(healAmount, true);
                }
            }
        }

        // 3. 처치 성공 시 폭식(성장) 발동
        if (isKill)
        {
            TriggerGluttony();
        }

        return true; 
    }

    void TriggerGluttony()
    {
        // 한계 도달 시 성장 중단
        if (owner.maxHP >= maxHpCap) return;

        // 1. 증가량 계산 (현재 최대 체력의 20%)
        float increaseAmount = owner.maxHP * growthFactor;

        // 2. 최대 체력 증가 & 현재 체력 회복
        owner.maxHP += increaseAmount;
        owner.currentHP += increaseAmount;

        // 3. UI 갱신
        if (owner.hpSlider != null)
        {
            owner.hpSlider.maxValue = owner.maxHP;
            owner.hpSlider.value = owner.currentHP;
        }

        // 4. 피드백 (텍스트 및 이펙트)
        if (FloatingTextManager.I != null)
        {
            // 🌟 성장 회복량 표시
            FloatingTextManager.I.ShowText(transform.position + Vector3.up * 1.5f, $"+{Mathf.RoundToInt(increaseAmount)}", Color.green, 35);
        }
        
        if (devourEffect != null) 
        {
            Instantiate(devourEffect, transform.position, Quaternion.identity);
        }
    }
}