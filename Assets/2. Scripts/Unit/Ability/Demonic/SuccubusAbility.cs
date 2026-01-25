using UnityEngine;
using System.Collections.Generic;

public class SuccubusAbility : UnitAbility
{
    [Header("몽마병 설정")]
    public float scanRange = 8.0f;     
    public float scanInterval = 0.5f;  
    private float scanTimer = 0f;

    [Header("업그레이드: 수확 (Harvest)")]
    public string harvestUpgradeKey = "HARVEST"; // 🌟 업그레이드 키
    public float harvestRange = 4.0f;            // 사용 범위
    public int harvestConditionCount = 3;        // 발동 조건 (수면 상태 3명 이상)
    public float harvestCooldown = 1.0f;         // 내부 쿨타임 (난사 방지)

    private float harvestTimer = 0f;

    [Header("이펙트")]
    public GameObject harvestEffect; // 수확 발동 시 이펙트

    // 현재 노리고 있는 "가장 건강한" 타겟
    private GameObject priorityTarget;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override void OnUpdate()
    {
        // 1. 기존 타겟 탐색 로직
        scanTimer += Time.deltaTime;
        if (scanTimer >= scanInterval)
        {
            scanTimer = 0f;
            FindHealthiestTarget();
        }

        // 2. 🌙 [신규] 수확(Harvest) 로직 체크
        HandleHarvest();
    }

    // 🌙 수확 로직 구현
    void HandleHarvest()
    {
        // 업그레이드 확인
        if (UpgradeManager.I == null || !UpgradeManager.I.IsAbilityActive(harvestUpgradeKey, owner.tag))
            return;

        // 쿨타임 체크
        if (harvestTimer > 0)
        {
            harvestTimer -= Time.deltaTime;
            return;
        }

        // 발동 조건 체크 (범위 4 안의 수면 상태 적 3명 이상)
        List<UnitController> sleepingEnemies = GetSleepingEnemiesInRange();

        if (sleepingEnemies.Count >= harvestConditionCount)
        {
            CastHarvest(sleepingEnemies);
            harvestTimer = harvestCooldown; // 쿨타임 적용
        }
    }

    List<UnitController> GetSleepingEnemiesInRange()
    {
        List<UnitController> sleepers = new List<UnitController>();
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, harvestRange);

        foreach (var hit in hits)
        {
            // 적군인지 확인
            if (hit.CompareTag(owner.enemyTag))
            {
                UnitController enemy = hit.GetComponent<UnitController>();
                // 살아있고 + 수면 상태인지 확인
                if (enemy != null && !enemy.isDead && enemy.isSleeping)
                {
                    sleepers.Add(enemy);
                }
            }
        }
        return sleepers;
    }

    void CastHarvest(List<UnitController> targets)
    {
        float totalDamageDealt = 0f;
        float damageAmount = owner.attackDamage;

        foreach (var enemy in targets)
        {
            if (enemy == null || enemy.isDead) continue;

            float hpBefore = enemy.currentHP;

            // 피해 입힘 (방어력 적용됨) -> 데미지를 입으면 UnitController에 의해 잠에서 깸
            enemy.TakeDamage(damageAmount, false);

            // 실제 입힌 피해량 계산 (방어력 등으로 깎인 수치 반영)
            // 적이 죽었을 경우(hpBefore -> 0 이하)도 포함
            float actualDamage = Mathf.Max(0, hpBefore - enemy.currentHP);
            totalDamageDealt += actualDamage;

            // (선택) 개별 타격 이펙트가 있다면 여기서 생성
        }

        // 💗 체력 회복 (준 피해만큼)
        if (totalDamageDealt > 0)
        {
            owner.Heal(totalDamageDealt, true);
        }

        // 시각 효과
        if (harvestEffect != null)
        {
            Instantiate(harvestEffect, transform.position, Quaternion.identity);
        }

        if (FloatingTextManager.I != null)
        {
            FloatingTextManager.I.ShowText(transform.position + Vector3.up, "Harvest!", new Color(1f, 0.4f, 0.7f), 35);
        }
    }

    // 🎯 기존 타겟팅 로직 (유지)
    void FindHealthiestTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, scanRange);
        
        GameObject bestTarget = null;
        float highestHpRatio = -1.0f; 

        foreach (var hit in hits)
        {
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                UnitController enemy = hit.GetComponent<UnitController>();
                if (enemy != null && !enemy.isDead && !enemy.isStealthed)
                {
                    // 수면 상태인 적은 굳이 깨우지 않도록 우선순위에서 제외할 수도 있지만,
                    // 몽마병은 수면을 거는 역할이므로 그냥 공격해서 재우는 게 나을 수 있음.
                    // 기획 의도에 따라 유지.
                    
                    float ratio = enemy.currentHP / enemy.maxHP;
                    if (ratio > highestHpRatio)
                    {
                        highestHpRatio = ratio;
                        bestTarget = hit.gameObject;
                    }
                }
            }
        }

        priorityTarget = bestTarget;
    }

    public override bool OnAttack(GameObject target)
    {
        if (priorityTarget != null && target != priorityTarget)
        {
            float dist = Vector3.Distance(transform.position, priorityTarget.transform.position);
            if (dist <= owner.attackRange)
            {
                target = priorityTarget;
            }
        }

        UnitController enemy = target.GetComponent<UnitController>();
        if (enemy != null)
        {
            enemy.TakeDamage(owner.attackDamage, false);
            enemy.ApplySleep();
        }
        else
        {
            BaseController enemyBase = target.GetComponent<BaseController>();
            if (enemyBase != null) enemyBase.TakeDamage(owner.attackDamage);
        }

        return true; 
    }

    private void OnDrawGizmosSelected()
    {
        // 타겟 스캔 범위 (노랑)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scanRange);

        // 수확 범위 (분홍)
        Gizmos.color = new Color(1f, 0.4f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, harvestRange);
    }
}