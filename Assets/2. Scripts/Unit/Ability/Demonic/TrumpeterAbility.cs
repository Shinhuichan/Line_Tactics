using UnityEngine;

public class TrumpeterAbility : UnitAbility
{
    [Header("나팔병 버프 설정")]
    public float buffAmount = 0.1f; // 공격력 증가량 (기본 10%)
    public float buffDuration = 3.0f;
    
    [Header("업그레이드: 살육의 나팔")]
    public string slaughterUpgradeKey = "SLAUGHTER_HORN"; // 🌟 업그레이드 키
    
    // 공격 속도(AttackCooldown)를 버프 주기로 사용
    private float buffCooldownTimer = 0f;

    [Header("이펙트")]
    public GameObject buffEffect; 

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override void OnUpdate()
    {
        // 쿨타임 관리
        if (buffCooldownTimer > 0) buffCooldownTimer -= Time.deltaTime;

        // 쿨타임이 돌았으면 버프 대상 탐색
        if (buffCooldownTimer <= 0)
        {
            if (TryBuffAlly())
            {
                buffCooldownTimer = owner.attackCooldown; // 버프 성공 시 쿨타임 적용
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

                // ⛔ [수정] 노예병(Slave) 및 성채 시체병(BaseCorpse)은 버프 대상에서 아예 제외
                // (우선순위 계산조차 하지 않고 무시합니다)
                if (ally.unitType == UnitType.Slave || ally.unitType == UnitType.BaseCorpse) 
                {
                    continue; 
                }

                // 2. 건물 제외 (기존 로직 유지 - BaseArcher 등도 여기서 걸러짐)
                if (ally.IsStaticUnit) continue; 

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
            // 🩸 업그레이드 확인
            bool isSlaughter = false;
            if (UpgradeManager.I != null)
            {
                isSlaughter = UpgradeManager.I.IsAbilityActive(slaughterUpgradeKey, owner.tag);
            }

            // 버프 적용 (공격력 증가량은 buffAmount 사용, 살육 모드 전달)
            bestTarget.ApplyTrumpeterBuff(buffAmount, buffDuration, isSlaughter);
            
            // 연출
            if (buffEffect != null)
                Instantiate(buffEffect, transform.position, Quaternion.identity, transform);
            
            return true;
        }

        return false;
    }

    // 📊 우선순위 점수 계산표 (기존 유지)
    int CalculatePriorityScore(UnitController unit)
    {
        // 이미 버프가 있으면 후순위
        if (unit.HasTrumpeterBuff) return 10; 

        // 효율 낮은 유닛 (일꾼 등)
        if (IsLowPriorityUnit(unit.unitType)) return 1;

        // 일반 전투 유닛
        return 100;
    }

    bool IsLowPriorityUnit(UnitType type)
    {
        switch (type)
        {
            case UnitType.Worker:
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