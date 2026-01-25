using UnityEngine;
using System.Collections;

public class ArcherAbility : UnitAbility
{
    [Header("기존 능력: 카이팅")]
    public float recoilDistance = 0.5f;

    [Header("신규 능력: 불화살 (Fire Arrow)")]
    public string fireUpgradeKey = "FIRE_ARROW";
    public float fireRange = 4.0f;          
    public float fireDamage = 10.0f;        // 1타 데미지 (이건 장궁병 고유 스펙이므로 유지)
    
    // 🗑️ [삭제] 화상 관련 변수 제거 (UnitController의 상수 사용)
    // public float fireBurnDps = 5.0f; 
    // public float fireBurnDuration = 3.0f;

    public float fireCooldown = 10.0f;      
    public float castTime = 0.5f;

    [Header("상태 (Read Only)")]
    public bool isCastingFire = false;      // 현재 시전 중인가?
    private float fireCooldownTimer = 0f;

    // 🌟 [핵심] 시전 중일 때는 Busy 상태라고 알림 -> 이동/공격 중지됨
    public override bool IsBusy => isCastingFire;

    public override void OnUpdate()
    {
        // 쿨타임 돌리기
        if (fireCooldownTimer > 0) fireCooldownTimer -= Time.deltaTime;

        // 1. 업그레이드 확인
        // 🌟 [수정] owner.tag 전달
        if (UpgradeManager.I == null || !UpgradeManager.I.IsAbilityActive(fireUpgradeKey, owner.tag)) return;

        // 2. 사용 가능 조건: 쿨타임 끝남 AND 시전 중 아님
        if (fireCooldownTimer <= 0 && !isCastingFire)
        {
            // 3. 사거리 내 적 확인 (기존 사거리보다 긴 fireRange 사용)
            // 장궁병은 UnitController에 타겟팅 로직이 있지만, 스킬은 별도로 사거리를 잼
            GameObject target = FindFireTarget();
            
            if (target != null)
            {
                StartCoroutine(CastFireArrow(target));
            }
        }
    }

    IEnumerator CastFireArrow(GameObject target)
    {
        isCastingFire = true; 

        Vector3 dir = target.transform.position - transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        yield return new WaitForSeconds(castTime);

        if (target != null && target.activeInHierarchy)
        {
            UnitController enemy = target.GetComponent<UnitController>();
            if (enemy != null)
            {
                // 1. 즉발 데미지
                enemy.TakeDamage(fireDamage);
                
                // 2. 🔥 [수정] 화상 적용 (인자 없이 호출 -> UnitController 상수가 적용됨)
                enemy.ApplyBurn();

                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(target.transform.position, "Fire!", new Color(1f, 0.5f, 0f), 35);
            }
            else
            {
                BaseController baseCtrl = target.GetComponent<BaseController>();
                if (baseCtrl != null) baseCtrl.TakeDamage(fireDamage);
            }
        }

        fireCooldownTimer = fireCooldown;
        isCastingFire = false; 
    }

    // 불화살 사거리(4.0) 내의 가장 가까운 적 찾기
    GameObject FindFireTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, fireRange);
        GameObject bestTarget = null;
        float minDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                // 은신 유닛 감지 불가 등 조건 체크
                UnitController u = hit.GetComponent<UnitController>();
                if (u != null && u.isStealthed) continue;

                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    bestTarget = hit.gameObject;
                }
            }
        }
        return bestTarget;
    }

    // 기존 카이팅 로직 (일반 공격 시)
    public override bool OnAttack(GameObject target)
    {
        // 🛑 불화살 시전 중이면 일반 공격 안 함 (이중 공격 방지)
        if (isCastingFire) return true; 

        // ... (기존 농성 체크 및 카이팅 로직) ...
        bool isSiegeMode = false;
        if (owner.CompareTag("Player"))
        {
            if (TacticalCommandManager.I != null)
                isSiegeMode = (TacticalCommandManager.I.currentState == TacticalState.Siege);
        }
        else if (owner.CompareTag("Enemy"))
        {
            isSiegeMode = (EnemyBot.enemyState == TacticalState.Siege);
        }

        if (isSiegeMode) return false; 

        transform.Translate(Vector3.down * recoilDistance);
        return false; 
    }
    
    // 에디터에서 사거리 확인용
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.5f, 0, 0.5f); // 주황색
        Gizmos.DrawWireSphere(transform.position, fireRange);
    }
}