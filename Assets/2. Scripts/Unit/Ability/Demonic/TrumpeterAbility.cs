using UnityEngine;

public class TrumpeterAbility : UnitAbility
{
    [Header("나팔병 버프 설정")]
    public float buffAmount = 0.1f; // 공격력 증가량 (기본 10%)
    public float buffDuration = 3.0f;
    
    [Header("업그레이드: 살육의 나팔")]
    public string slaughterUpgradeKey = "SLAUGHTER_HORN"; 
    
    private float buffCooldownTimer = 0f;

    [Header("이펙트")]
    public GameObject buffEffect; 

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override void OnUpdate()
    {
        if (buffCooldownTimer > 0) buffCooldownTimer -= Time.deltaTime;

        if (buffCooldownTimer <= 0)
        {
            if (TryBuffAlly())
            {
                buffCooldownTimer = owner.attackCooldown; 
            }
        }
    }

    bool TryBuffAlly()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, owner.attackRange);
        
        UnitController bestTarget = null;
        int bestScore = -1;

        foreach (var col in colliders)
        {
            // 1. 아군 판별
            if (col.CompareTag(owner.tag) && col.gameObject != gameObject)
            {
                UnitController ally = col.GetComponent<UnitController>();
                if (ally == null || ally.isDead) continue;

                // 2. 건물 제외 (성채 유닛 등 기본 제외)
                if (ally.IsStaticUnit) continue; 

                // 🚫 [수정] 나팔병 자신, 노동병, 노예병, 그리고 "성채 시체병"은 버프 대상에서 아예 제외
                // (IsStaticUnit에 포함되어 있지만 이중 안전장치로 명시적 제외)
                if (ally.unitType == UnitType.Trumpeter) continue;
                if (ally.unitType == UnitType.Worker || ally.unitType == UnitType.Slave) continue;
                if (ally.unitType == UnitType.BaseCorpse) continue; // 🌟 추가됨

                // 3. 점수 계산 (우선순위)
                int score = CalculatePriorityScore(ally);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = ally;
                }
            }
        }

        // 타겟이 있으면 버프 실행
        if (bestTarget != null)
        {
            bool isSlaughter = false;
            if (UpgradeManager.I != null)
            {
                isSlaughter = UpgradeManager.I.IsAbilityActive(slaughterUpgradeKey, owner.tag);
            }

            bestTarget.ApplyTrumpeterBuff(buffAmount, buffDuration, isSlaughter);
            
            if (buffEffect != null)
                Instantiate(buffEffect, transform.position, Quaternion.identity, transform);
            
            return true;
        }

        return false;
    }

    // 📊 우선순위 점수 계산표
    int CalculatePriorityScore(UnitController unit)
    {
        if (unit.HasTrumpeterBuff) return 10; 
        if (IsLowPriorityUnit(unit.unitType)) return 1;
        return 100;
    }

    bool IsLowPriorityUnit(UnitType type)
    {
        switch (type)
        {
            case UnitType.Healer:
            case UnitType.FlagBearer:
            case UnitType.Bomber: 
                return true;
            default: 
                return false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (owner != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, owner.attackRange);
        }
    }
}