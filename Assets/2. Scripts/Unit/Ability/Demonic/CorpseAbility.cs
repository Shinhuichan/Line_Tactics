using UnityEngine;

public class CorpseAbility : UnitAbility
{
    [Header("시체병 설정")]
    public float slowIntensity = 0.2f; // 20% 둔화

    [Header("업그레이드: 썩은 내 (Rotten Stench)")]
    public string stenchUpgradeKey = "ROTTEN_STENCH"; // 🌟 업그레이드 키
    public float stenchRange = 0.5f;      
    public float stenchDamage = 2.0f;     
    
    private float stenchTimer = 0f;       

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        stenchTimer = 0f;
    }

    // 매 프레임 호출
    public override void OnUpdate()
    {
        HandleRottenStench();
    }

    void HandleRottenStench()
    {
        // 1초마다 체크 (최적화를 위해)
        stenchTimer += Time.deltaTime;

        if (stenchTimer >= 1.0f)
        {
            stenchTimer = 0f;

            // 🌟 [핵심] 업그레이드가 활성화되었는지 확인
            if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(stenchUpgradeKey, owner.tag))
            {
                ApplyStenchDamage();
            }
        }
    }

    void ApplyStenchDamage()
    {
        // 범위 내 모든 콜라이더 검사
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, stenchRange);
        
        bool hitAny = false; // (선택) 이펙트용 플래그

        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue; 

            // 적군 유닛이거나 적 기지인 경우
            if (col.CompareTag(owner.enemyTag) || col.CompareTag(owner.targetBaseTag))
            {
                UnitController enemy = col.GetComponent<UnitController>();
                if (enemy != null)
                {
                    enemy.TakeDamage(stenchDamage, false);
                    hitAny = true;
                }
                else
                {
                    BaseController enemyBase = col.GetComponent<BaseController>();
                    if (enemyBase != null)
                    {
                        enemyBase.TakeDamage(stenchDamage);
                        hitAny = true;
                    }
                }
            }
        }

        // (선택) 피해를 입혔을 때 시각적 피드백 (독구름 효과 등)
        if (hitAny && FloatingTextManager.I != null)
        {
            // 너무 자주 뜨면 지저분하므로 확률적으로 표시하거나 생략 가능
            FloatingTextManager.I.ShowText(transform.position + Vector3.up, "Stench", new Color(0.2f, 0.8f, 0.2f), 15);
        }
    }

    public override bool OnAttack(GameObject target)
    {
        UnitController enemy = target.GetComponent<UnitController>();
        if (enemy != null)
        {
            // 1. 데미지 적용
            enemy.TakeDamage(owner.attackDamage, false);

            // 2. 독 상태 부여 
            enemy.ApplyPoison();

            // 3. 둔화 상태 부여
            enemy.ApplySlow(slowIntensity);
        }
        else
        {
            // 건물인 경우 데미지만
            BaseController enemyBase = target.GetComponent<BaseController>();
            if (enemyBase != null) enemyBase.TakeDamage(owner.attackDamage);
        }

        return true; 
    }

    private void OnDrawGizmosSelected()
    {
        // 범위 확인용 기즈모
        Gizmos.color = new Color(0.4f, 0.8f, 0.2f, 0.5f); 
        Gizmos.DrawWireSphere(transform.position, stenchRange);
    }
}