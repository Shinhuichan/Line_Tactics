using UnityEngine;
using System.Collections;

public class BomberAbility : UnitAbility
{
    [Header("자폭 설정")]
    public float explosionRadius = 1.5f;    // 폭발 범위 (몸체보다 약간 크게 설정 추천)
    public GameObject explosionVFX;         // 폭발 이펙트 프리팹
    public float explosionDamageMultiplier = 1.0f; // 공격력 대비 폭발 데미지 배율

    [Header("업그레이드 키")]
    public string kamikazeKey = "KAMIKAZE"; // 업그레이드 키

    [Header("상태 (Read Only)")]
    public bool hasExploded = false;        // 중복 폭발 방지

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        hasExploded = false; 
    }

    // 1. 공격 명령이 내려오자마자 즉시 자폭
    public override bool OnAttack(GameObject target)
    {
        if (hasExploded) return true;
        
        // 딜레이 없이 즉시 폭발
        ExecuteExplosion();
        return true; 
    }

    // 2. 사망 시 자폭 (기존 유지)
    public override bool OnDie()
    {
        if (!hasExploded)
        {
            ExecuteExplosion();
        }
        return true; 
    }

    // 3. 💥 [신규] 충돌 시 자폭 (몸으로 비빌 때 즉시 폭발)
    // UnitController가 공격 명령을 내리기 전이라도, 물리적으로 닿으면 터집니다.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasExploded) return;

        // 적군 유닛이나 기지와 충돌했는지 확인
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy"))
        {
            // 아군은 제외 (자폭병이 아군과 부딪혀서 터지면 안 되므로)
            if (!collision.gameObject.CompareTag(owner.gameObject.tag))
            {
                ExecuteExplosion();
            }
        }
    }

    // 🔥 자폭 실행 로직 (공통)
    void ExecuteExplosion()
    {
        if (hasExploded) return;
        hasExploded = true;

        // 1. 이펙트 생성
        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, transform.position, Quaternion.identity);
        }

        // 2. 범위 데미지 처리
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        
        // 카미카제 업그레이드 확인
        bool isKamikazeActive = false;
        if (UpgradeManager.I != null)
        {
            isKamikazeActive = UpgradeManager.I.IsAbilityActive(kamikazeKey, owner.gameObject.tag);
        }

        foreach (var hit in hits)
        {
            // 자신은 제외
            if (hit.gameObject == gameObject) continue;

            // 적군 판별 (기지 포함)
            bool isEnemy = false;
            if (owner.CompareTag("Player") && hit.CompareTag("Enemy")) isEnemy = true;
            else if (owner.CompareTag("Enemy") && hit.CompareTag("Player")) isEnemy = true;

            if (isEnemy)
            {
                float finalDamage = owner.attackDamage * explosionDamageMultiplier;

                // 유닛 처리
                UnitController enemyUnit = hit.GetComponent<UnitController>();
                if (enemyUnit != null)
                {
                    enemyUnit.TakeDamage(finalDamage, false);
                    
                    // 카미카제 효과 (스턴 + 넉백)
                    if (isKamikazeActive)
                    {
                        enemyUnit.ApplyStun(1.0f);
                        enemyUnit.ApplyBurn(); 
                        
                        Vector3 knockbackDir = (enemyUnit.transform.position - transform.position).normalized;
                        if (knockbackDir == Vector3.zero) knockbackDir = Random.insideUnitCircle.normalized;
                        enemyUnit.ApplyKnockback(knockbackDir, 5.0f);
                    }
                }
                // 기지 처리
                else
                {
                    BaseController enemyBase = hit.GetComponent<BaseController>();
                    if (enemyBase != null)
                    {
                        // 기지에는 보통 더 큰 피해를 주거나 그대로 줌
                        enemyBase.TakeDamage(finalDamage);
                        if (FloatingTextManager.I != null)
                            FloatingTextManager.I.ShowText(enemyBase.transform.position, "Siege Dmg!", Color.yellow, 30);
                    }
                }
            }
        }

        // 3. 자폭병 사망 처리 (즉시 제거)
        // OnDie 루프 방지를 위해 상태를 먼저 변경했으므로 안전함
        if (owner != null)
        {
            owner.currentHP = 0;
            owner.FinishDeath(); // UnitController의 사망 처리 호출
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}