using UnityEngine;

public class HealerAbility : UnitAbility
{
    [Header("치유 설정")]
    public float healCooldownTime = 1.5f;
    private float lastHealTime;

    [Header("신규 능력: 상태 치유 (Status Cure)")]
    public string statusCureKey = "STATUS_CURE";
    public float statusBonusHealRatio = 0.25f; // 상태이상 해제 시 추가 힐량

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override void OnUpdate()
    {
        if (Time.time - lastHealTime < healCooldownTime) return;

        if (TryHealAlly())
        {
            lastHealTime = Time.time;
        }
    }

    bool TryHealAlly()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, owner.attackRange);

        UnitController bestTarget = null;
        float bestScore = -1.0f; // 점수제 (높을수록 좋음)

        foreach (Collider2D col in colliders)
        {
            if (!col.CompareTag(owner.gameObject.tag)) continue;
            if (col.gameObject == owner.gameObject) continue;
            if (col.GetComponent<BaseController>() != null) continue;

            UnitController allyUnit = col.GetComponent<UnitController>();
            if (allyUnit != null)
            {
                bool isHurt = allyUnit.currentHP < allyUnit.maxHP;
                
                // 😷 상태 이상 보유 여부 체크 (기절, 둔화 추가)
                bool hasBadStatus = allyUnit.IsBurning || allyUnit.IsPoisoned || allyUnit.IsShocked || 
                    allyUnit.isStunned || allyUnit.IsSlowed || allyUnit.isSleeping; // 💤 수면 추가

                // 아픈 곳도 없고 상태 이상도 없으면 패스
                if (!isHurt && !hasBadStatus) continue;

                // 점수 계산 (잃은 체력 비율 + 상태 이상 가산점)
                float hpRatio = 1.0f - (allyUnit.currentHP / allyUnit.maxHP); // 잃은 체력이 많을수록 점수 높음
                float currentScore = hpRatio;

                // 치유병끼리는 서로 덜 치료함 (우선순위 낮춤)
                if (allyUnit.unitType == UnitType.Healer) currentScore -= 0.5f; 
                
                // 상태 이상이 있으면 우선순위 대폭 상승 (구조대!)
                if (hasBadStatus) currentScore += 0.5f;

                if (currentScore > bestScore)
                {
                    bestScore = currentScore;
                    bestTarget = allyUnit;
                }
            }
        }

        if (bestTarget != null)
        {
            float healAmount = owner.attackDamage; 
            bool statusCured = false;

            // 🌟 상태 치유 업그레이드 확인
            if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(statusCureKey, owner.tag))
            {
                // 하나라도 해제되면 보너스 힐 적용
                if (bestTarget.IsBurning || bestTarget.IsPoisoned || bestTarget.IsShocked || 
                    bestTarget.isStunned || bestTarget.IsSlowed)
                {
                    bestTarget.CureBurn();
                    bestTarget.CurePoison();
                    bestTarget.CureShock();
                    bestTarget.CureStun();
                    bestTarget.CureSlow();
                    bestTarget.CureSleep(); // 💤 수면 해제 추가
                    statusCured = true;
                    
                    if (FloatingTextManager.I != null)
                        FloatingTextManager.I.ShowText(bestTarget.transform.position + Vector3.up, "Cured!", Color.green, 20);
                }
            }

            // 상태 이상을 치료했다면 힐량 증가
            if (statusCured)
            {
                healAmount *= (1.0f + statusBonusHealRatio);
            }

            // 최종 치유 적용
            bestTarget.Heal(healAmount);
            
            // 힐 이펙트/사운드 (선택)
            
            return true;
        }

        return false;
    }
}