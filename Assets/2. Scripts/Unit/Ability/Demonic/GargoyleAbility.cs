using UnityEngine;

public class GargoyleAbility : UnitAbility
{
    [Header("가고일 능력: 석화 광선 (Prismatic Beam)")]
    [Tooltip("공격마다 증폭될 피해량 비율 (0.15 = 1.15배씩 곱연산)")]
    public float damageAmpRatio = 0.15f;

    [Tooltip("최대 중첩 횟수 (0 = 무제한, 곱연산은 스노우볼이 매우 크므로 주의)")]
    public int maxStacks = 15; 

    [Header("업그레이드: 수정 파열 (Crystal Shatter)")]
    public string shatterUpgradeKey = "CRYSTAL_SHATTER"; // 🌟 업그레이드 키
    public float shatterRange = 0.5f;        // 폭발 범위
    public float shatterDmgCoef = 0.15f;     // 스택당 데미지 계수 (15%)
    public GameObject shatterEffect;         // (선택) 폭발 이펙트

    [Header("상태 (Read Only)")]
    [SerializeField] private GameObject currentTarget;
    [SerializeField] private UnitController targetUnit; // 최적화를 위해 캐싱
    [SerializeField] private int currentStack = 0;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override void OnUpdate()
    {
        // 🌟 타겟이 다른 아군에 의해 죽었을 경우 감지
        if (targetUnit != null)
        {
            // 죽었거나 비활성화되었다면 폭발
            if (targetUnit.isDead || !targetUnit.gameObject.activeInHierarchy)
            {
                TriggerCrystalShatter(targetUnit.transform.position);
            }
        }
        else if (currentTarget != null && !currentTarget.activeInHierarchy)
        {
            // UnitController가 없는 대상(건물 등)이 파괴되었을 때
            TriggerCrystalShatter(currentTarget.transform.position);
        }
    }

    public override bool OnAttack(GameObject target)
    {
        // 1. 타겟 변경 확인 및 스택 관리
        if (target != currentTarget)
        {
            // 타겟이 바뀌면 초기화
            currentTarget = target;
            targetUnit = target.GetComponent<UnitController>(); // 캐싱
            currentStack = 0;
        }
        else
        {
            // 같은 타겟 계속 공격 시 스택 증가
            if (maxStacks == 0 || currentStack < maxStacks)
            {
                currentStack++;
            }
        }

        // 2. 데미지 계산 (기본 스택형 데미지)
        float multiplier = Mathf.Pow(1.0f + damageAmpRatio, currentStack);
        float finalDamage = owner.attackDamage * multiplier;

        // 3. 데미지 적용 및 킬 체크
        bool isDead = false;
        
        if (targetUnit != null)
        {
            // 데미지 적용
            targetUnit.TakeDamage(finalDamage, false);
            // 가고일의 공격으로 죽었는지 확인
            if (targetUnit.isDead || targetUnit.currentHP <= 0) isDead = true;
        }
        else
        {
            BaseController enemyBase = target.GetComponent<BaseController>();
            if (enemyBase != null) 
            {
                enemyBase.TakeDamage(finalDamage);
                if (enemyBase.currentHP <= 0) isDead = true;
            }
        }

        // 🌟 4. 처치 시 수정 파열 발동
        if (isDead)
        {
            TriggerCrystalShatter(target.transform.position);
        }

        return true; 
    }

    // 💎 수정 파열 발동 함수
    void TriggerCrystalShatter(Vector3 centerPos)
    {
        // 스택이 없거나 타겟이 없으면 무시 (이미 터졌거나 초기화됨)
        if (currentStack <= 0) return;

        // 업그레이드 확인
        if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(shatterUpgradeKey, owner.tag))
        {
            // 데미지 계산: (쌓인 스택 * 공격력 * 0.05)
            float explosionDamage = currentStack * owner.attackDamage * shatterDmgCoef;

            // 범위 피해
            Collider2D[] hits = Physics2D.OverlapCircleAll(centerPos, shatterRange);
            foreach (var hit in hits)
            {
                // 적군만 타격
                if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
                {
                    UnitController enemy = hit.GetComponent<UnitController>();
                    if (enemy != null && !enemy.isDead)
                    {
                        enemy.TakeDamage(explosionDamage, false);
                    }
                    else
                    {
                        BaseController baseCtrl = hit.GetComponent<BaseController>();
                        if (baseCtrl != null) baseCtrl.TakeDamage(explosionDamage);
                    }
                }
            }

            // 시각 효과 (텍스트)
            if (FloatingTextManager.I != null)
            {
                FloatingTextManager.I.ShowText(centerPos, $"Shatter! ({Mathf.RoundToInt(explosionDamage)})", Color.cyan, 30);
            }
            
            // (선택) 파티클 이펙트 생성
            if (shatterEffect != null)
            {
                Instantiate(shatterEffect, centerPos, Quaternion.identity);
            }
        }

        // 폭발 후 초기화 (중복 폭발 방지)
        currentStack = 0;
        currentTarget = null;
        targetUnit = null;
    }

    private void OnDrawGizmosSelected()
    {
        // 파열 범위 확인 (하늘색)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, shatterRange);
    }
}