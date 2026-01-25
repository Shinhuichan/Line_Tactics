using UnityEngine;
using System.Collections;

public class BomberAbility : UnitAbility
{
    [Header("자폭 설정")]
    public float explosionRadius = 1.0f;    // 광역 피해 범위
    public GameObject explosionVFX;         // 폭발 이펙트 프리팹

    [Header("업그레이드 키")]
    public string kamikazeKey = "KAMIKAZE"; // 업그레이드 키

    [Header("상태 (Read Only)")]
    public bool hasExploded = false;        // 중복 폭발 방지

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        
        // 🌟 [필수] 재소환 시 폭발 상태 초기화 (풀링 문제 방지)
        hasExploded = false; 
    }

    // 1. 공격 명령 시 자폭 (사거리에 닿았을 때)
    public override bool OnAttack(GameObject target)
    {
        if (hasExploded) return true;
        ExecuteExplosion();
        return true; 
    }

    // 2. 사망 시 자폭
    public override bool OnDie()
    {
        if (!hasExploded)
        {
            ExecuteExplosion();
        }
        return true; 
    }

    // 3. 💥 [신규] 충돌 시 자폭 (몸으로 비빌 때 즉시 폭발)
    // 사거리가 닿지 않아도 적과 물리적으로 닿으면 터집니다.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasExploded || owner.isDead) return;

        // 적군 유닛이나 적 기지와 부딪혔는지 확인
        if (collision.gameObject.CompareTag(owner.enemyTag) || 
            collision.gameObject.CompareTag(owner.targetBaseTag))
        {
            // 충돌 지점을 향해 조금 더 파고드는 느낌을 주려면 상대방 위치 사용 가능
            // 여기서는 깔끔하게 현재 위치에서 폭발
            ExecuteExplosion();
        }
    }

    void ExecuteExplosion()
    {
        if (hasExploded) return; // 이중 안전장치
        hasExploded = true;

        // 💥 이펙트 생성 및 자동 제거
        if (explosionVFX != null)
        {
            GameObject vfx = Instantiate(explosionVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 2.0f); 
        }

        Vector3 center = transform.position;
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(center, "KABOOM!", Color.red, 40);

        // 업그레이드 확인
        bool isKamikazeActive = false;
        if (UpgradeManager.I != null)
        {
            isKamikazeActive = UpgradeManager.I.IsAbilityActive(kamikazeKey, owner.tag);
        }

        // 💥 광역 피해 판정
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, explosionRadius);
        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;

            // 적군인지 확인
            if (col.CompareTag(owner.enemyTag)) 
            {
                UnitController enemyUnit = col.GetComponent<UnitController>();
                if (enemyUnit != null)
                {
                    float finalDamage = owner.attackDamage;

                    // 🏢 기계/건물 속성 2배 피해
                    if (enemyUnit.isMechanical)
                    {
                        finalDamage *= 2.0f;
                        if (FloatingTextManager.I != null)
                            FloatingTextManager.I.ShowText(enemyUnit.transform.position, "Structural Dmg!", Color.yellow, 25);
                    }

                    enemyUnit.TakeDamage(finalDamage, false);
                    enemyUnit.ApplyBurn(); 

                    // 🌪️ 카미카제 효과
                    if (isKamikazeActive)
                    {
                        enemyUnit.ApplyStun(1.0f);
                        Vector3 knockbackDir = (enemyUnit.transform.position - transform.position).normalized;
                        if (knockbackDir == Vector3.zero) knockbackDir = Random.insideUnitCircle.normalized;
                        enemyUnit.ApplyKnockback(knockbackDir, 5.0f);
                    }
                }
            }
            // 기지인지 확인
            else if (col.CompareTag(owner.targetBaseTag))
            {
                BaseController enemyBase = col.GetComponent<BaseController>();
                if (enemyBase != null)
                {
                    float finalDamage = owner.attackDamage * 2.0f;
                    enemyBase.TakeDamage(finalDamage);
                    if (FloatingTextManager.I != null)
                        FloatingTextManager.I.ShowText(enemyBase.transform.position, "Siege Dmg!", Color.yellow, 30);
                }
            }
        }

        owner.FinishDeath();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}