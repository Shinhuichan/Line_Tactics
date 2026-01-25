using UnityEngine;
using System.Collections;

public class SpearmanAbility : UnitAbility
{
    [Header("기본 능력: 거리 유지 & 넉백")]
    public float keepDistanceRatio = 0.6f;
    public float basicKnockbackForce = 1.5f; // 🌟 [신규] 기본 공격 넉백 파워

    [Header("신규 능력: 충격파 (Shockwave)")]
    public string shockwaveUpgradeKey = "SHOCKWAVE";
    public float shockwaveCooldown = 25.0f;
    public float castTime = 0.5f;
    public float damageRatio = 0.66f;
    
    [Header("검기 설정")]
    public float projectileRange = 5.0f;
    public float projectileSpeed = 8.0f;
    public float projectileKnockback = 2.0f;
    // 🌟 [핵심] 검기 프리팹 (꼭 연결!)
    public GameObject shockwavePrefab;

    [Header("상태 (Read Only)")]
    public bool isCasting = false;
    private float cooldownTimer = 0f;

    public override bool IsBusy => isCasting;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    // 🌟 [복구] 기본 공격 시 넉백 적용
    public override bool OnAttack(GameObject target)
    {
        // 스킬 시전 중이면 일반 공격 취소
        if (isCasting) return true;

        UnitController enemyUnit = target.GetComponent<UnitController>();
        
        // 유닛 상대로는 넉백 적용
        if (enemyUnit != null)
        {
            // 1. 데미지
            enemyUnit.TakeDamage(owner.attackDamage);

            // 2. 넉백 (밀어내기)
            // 방향: 나 -> 적
            Vector3 pushDir = (target.transform.position - transform.position).normalized;
            enemyUnit.ApplyKnockback(pushDir, basicKnockbackForce);

            // (선택) 텍스트 연출
            // if (FloatingTextManager.I != null)
            //    FloatingTextManager.I.ShowText(target.transform.position, "Push!", Color.white, 20);

            return true; // 기본 로직 대신 처리했음을 알림
        }

        // 건물 등 넉백 불가능한 대상은 기본 공격 로직(UnitController)에 맡김
        return false;
    }

    public override void OnUpdate()
    {
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        // 1. 업그레이드 확인
        if (UpgradeManager.I == null || !UpgradeManager.I.IsAbilityActive(shockwaveUpgradeKey, owner.tag))
        {
            ProcessKeepDistance();
            return; 
        }

        // 2. 스킬 사용 조건: 쿨타임 끝남 && 적이 공격 사거리 내에 있음
        if (cooldownTimer <= 0 && !isCasting)
        {
            GameObject target = FindNearestEnemy();
            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist <= owner.attackRange)
                {
                    StartCoroutine(CastShockwave(target.transform.position));
                    return;
                }
            }
        }

        if (!isCasting)
        {
            ProcessKeepDistance();
        }
    }

    IEnumerator CastShockwave(Vector3 targetPos)
    {
        isCasting = true; 
        RotateTowards(targetPos);
        yield return new WaitForSeconds(castTime);
        CreateProjectile();
        cooldownTimer = shockwaveCooldown;
        isCasting = false; 
    }

    void CreateProjectile()
    {
        if (shockwavePrefab != null)
        {
            GameObject proj = Instantiate(shockwavePrefab, transform.position, transform.rotation);
            ShockwaveProjectile script = proj.GetComponent<ShockwaveProjectile>();
            if (script == null) script = proj.AddComponent<ShockwaveProjectile>();

            float dmg = owner.attackDamage * damageRatio;
            script.Initialize(dmg, projectileSpeed, projectileRange, projectileKnockback, owner.enemyTag, owner.targetBaseTag);
        }
        else
        {
            Debug.LogError("⚡ [SpearmanAbility] 검기 프리팹(Shockwave Prefab)이 없습니다!");
        }
    }

    void ProcessKeepDistance()
    {
        bool isSiegeMode = false;
        if (owner.CompareTag("Player") && TacticalCommandManager.I != null)
             isSiegeMode = (TacticalCommandManager.I.currentState == TacticalState.Siege);
        else if (owner.CompareTag("Enemy"))
             isSiegeMode = (EnemyBot.enemyState == TacticalState.Siege);

        if (isSiegeMode || owner.isManualMove) return;

        GameObject nearestEnemy = FindNearestEnemy();
        if (nearestEnemy != null)
        {
            float dist = Vector3.Distance(transform.position, nearestEnemy.transform.position);
            if (dist < owner.attackRange * keepDistanceRatio)
            {
                Vector3 dir = (transform.position - nearestEnemy.transform.position).normalized;
                transform.position += dir * owner.moveSpeed * 0.5f * Time.deltaTime;
            }
        }
    }

    GameObject FindNearestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, owner.attackRange);
        GameObject closest = null;
        float minDst = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                float dst = Vector3.Distance(transform.position, hit.transform.position);
                if (dst < minDst) { minDst = dst; closest = hit.gameObject; }
            }
        }
        return closest;
    }

    void RotateTowards(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
}