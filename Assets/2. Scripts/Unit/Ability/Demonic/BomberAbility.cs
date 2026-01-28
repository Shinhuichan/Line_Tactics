using UnityEngine;

public class BomberAbility : UnitAbility
{
    [Header("Bomber Settings")]
    public float explosionRadius = 3.0f;
    public float explosionDamage = 50.0f;
    public GameObject explosionEffectPrefab;

    [Header("Upgrade Keys")]
    public string kamikazeKey = "KAMIKAZE"; // ⚡ 복구: 업그레이드 키

    private bool isExploded = false; // 💥 중복 폭발 방지용 플래그

    public override void Initialize(UnitController controller)
    {
        base.Initialize(controller);
        isExploded = false; // 초기화
    }

    // ⚔️ 공격 시 = 자폭
    public override bool OnAttack(GameObject target)
    {
        if (isExploded) return true;

        Explode();
        return true; 
    }

    // 💀 죽을 때 = 자폭
    public override bool OnDie()
    {
        if (isExploded) return false;

        Explode();
        return true; 
    }

    // 💥 충돌 시 = 자폭
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isExploded || owner.isDead) return;

        GameObject target = collision.gameObject;
        // 적 유닛이나 적 기지와 부딪히면 즉시 폭발
        if (target.CompareTag(owner.enemyTag) || target.CompareTag(owner.targetBaseTag))
        {
            Debug.Log($"{owner.name} collided with {target.name} -> BOOM!");
            Explode();
        }
    }

    private void Explode()
    {
        if (isExploded) return;
        isExploded = true;

        SpawnExplosionEffect(); // 🧹 VFX 자동 삭제 포함됨
        ApplyAreaDamage();      // ⚡ 업그레이드 효과 포함됨
        
        // 자폭했으므로 유닛 제거
        owner.FinishDeath();
    }

    private void ApplyAreaDamage()
    {
        // ⚡ [복구] 업그레이드 활성화 여부 확인
        bool isKamikazeActive = false;
        if (UpgradeManager.I != null)
        {
            isKamikazeActive = UpgradeManager.I.IsAbilityActive(kamikazeKey, owner.tag);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            // 아군 오폭 방지
            if (hit.gameObject == owner.gameObject) continue;
            if (hit.CompareTag(owner.tag) || hit.CompareTag(owner.myBaseTag)) continue;

            // 1. 유닛 피격 처리
            UnitController targetUnit = hit.GetComponent<UnitController>();
            if (targetUnit != null)
            {
                targetUnit.TakeDamage(explosionDamage);

                // ⚡ [복구] 업그레이드 시 상태이상 부여 (CC기)
                if (isKamikazeActive)
                {
                    targetUnit.ApplyStun(1.0f); // 1초 기절
                    targetUnit.ApplyBurn();     // 화상 적용
                    
                    // 넉백 방향 계산 (폭발 중심에서 바깥으로)
                    Vector3 knockbackDir = (targetUnit.transform.position - transform.position).normalized;
                    if (knockbackDir == Vector3.zero) knockbackDir = Random.insideUnitCircle.normalized;
                    
                    targetUnit.ApplyKnockback(knockbackDir, 2.5f); // 넉백
                }
                continue;
            }

            // 2. 기지 피격 처리
            BaseController targetBase = hit.GetComponent<BaseController>();
            if (targetBase != null)
            {
                targetBase.TakeDamage(explosionDamage);
            }
        }
    }

    private void SpawnExplosionEffect()
    {
        if (explosionEffectPrefab != null)
        {
            GameObject vfx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            // ✨ [유지] 맵 더러워짐 방지: 2초 후 자동 삭제
            Destroy(vfx, 2.0f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}