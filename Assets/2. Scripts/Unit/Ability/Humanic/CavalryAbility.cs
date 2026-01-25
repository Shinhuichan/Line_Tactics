using UnityEngine;

public class CavalryAbility : UnitAbility
{
    [Header("기마병 스탯")]
    public float detectionRange = 6.0f; 
    public float chargeSpeedMultiplier = 2.5f; 
    public float chargeAttackMultiplier = 2.5f; 
    public float knockbackDistance = 2f; 

    [Header("신규 능력: 치고 빠지기 (Hit and Run)")]
    public string hitAndRunKey = "HIT_AND_RUN";

    [Header("상태 (Read Only)")]
    public bool isCharging = false; 
    public bool isRetreating = false; 
    public bool hasAttacked = false;  
    
    private float originalSpeed; 
    private Vector3 lockedRetreatDir; 
    
    // 🛡️ [신규] 무한 질주 방지용 안전장치
    private float chargeDurationTimer = 0f;
    private const float MAX_CHARGE_DURATION = 3.0f; // 3초 동안 못 박으면 멈춤

    public override bool IsBusy => isCharging || isRetreating;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        originalSpeed = unit.moveSpeed; 
    }

    public override void OnUpdate()
    {
        if (isRetreating)
        {
            ProcessRetreat();
            return;
        }

        if (isCharging)
        {
            ProcessCharge(); // 🌟 돌진 처리 함수 분리
            return; 
        }

        if (hasAttacked)
        {
            if (!CheckEnemyInSight())
            {
                hasAttacked = false; 
            }
            return; 
        }

        if (CheckEnemyInSight())
        {
            StartCharge();
        }
    }

    // 🌟 [핵심 수정] 돌진 중 이동 및 자체 충돌 체크 로직
    void ProcessCharge()
    {
        float step = owner.moveSpeed * Time.deltaTime;
        
        // 1. 안전장치: 너무 오래 달리면 멈춤 (적을 놓쳤거나 죽었을 때)
        chargeDurationTimer += Time.deltaTime;
        if (chargeDurationTimer > MAX_CHARGE_DURATION)
        {
            StopCharge();
            hasAttacked = true; // 공격한 셈 치고 쿨타임 갖기
            return;
        }

        // 2. 이동 전에 앞에 적이 있는지 '직접' 확인 (IsBusy라 UnitController가 안 해줌)
        // 이동할 거리(step)보다 조금 더 길게 체크
        RaycastHit2D hit = Physics2D.Raycast(transform.position, transform.up, step + 0.5f);
        
        if (hit.collider != null && hit.collider.gameObject != gameObject) // 나 자신 제외
        {
            // 적이나 기지를 들이받았는지 확인
            if (hit.collider.CompareTag(owner.enemyTag) || hit.collider.CompareTag(owner.targetBaseTag))
            {
                // 💥 충돌! 수동으로 OnAttack 호출
                OnAttack(hit.collider.gameObject);
                return; // 충돌했으니 이동 스킵
            }
        }

        // 3. 충돌 안 했으면 앞으로 이동
        transform.Translate(Vector3.up * step);
    }

    public override bool OnAttack(GameObject target)
    {
        // 🌟 돌진 중에만 공격 효과 적용
        if (isCharging)
        {
            float finalDamage = owner.attackDamage * chargeAttackMultiplier;

            UnitController enemyUnit = target.GetComponent<UnitController>();
            if (enemyUnit != null)
            {
                enemyUnit.TakeDamage(finalDamage);
                Vector3 pushDir = (target.transform.position - transform.position).normalized;
                enemyUnit.ApplyKnockback(pushDir, knockbackDistance);
                
                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(target.transform.position, "Charge!", Color.red, 35);
            }
            else
            {
                BaseController baseCtrl = target.GetComponent<BaseController>();
                if (baseCtrl != null) baseCtrl.TakeDamage(finalDamage);
            }

            StopCharge(); // 충돌 즉시 멈춤

            // 치고 빠지기 확인
            if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(hitAndRunKey, owner.tag))
            {
                StartRetreat(); 
            }
            else
            {
                hasAttacked = true; 
            }

            return true; 
        }

        return false; 
    }

    void StartCharge()
    {
        if (isCharging || isRetreating) return;
        
        isCharging = true;
        chargeDurationTimer = 0f; // 타이머 초기화
        owner.moveSpeed = originalSpeed * chargeSpeedMultiplier; 
    }

    void StopCharge()
    {
        isCharging = false;
        owner.moveSpeed = originalSpeed; 
    }

    // ... (StartRetreat, ProcessRetreat, CheckEnemyInSight 등 기존 하단 로직은 그대로 유지) ...
    void StartRetreat()
    {
        isRetreating = true;
        GameObject myBase = GameObject.FindGameObjectWithTag(owner.myBaseTag);
        if (myBase != null) lockedRetreatDir = (myBase.transform.position - transform.position).normalized;
        else lockedRetreatDir = -transform.up; 
        
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Retreat!", Color.blue, 25);
    }

    void ProcessRetreat()
    {
        transform.position += lockedRetreatDir * originalSpeed * Time.deltaTime;
        float angle = Mathf.Atan2(lockedRetreatDir.y, lockedRetreatDir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.AngleAxis(angle, Vector3.forward), Time.deltaTime * 10f);

        if (!CheckEnemyInSight())
        {
            isRetreating = false; 
        }
    }

    private bool CheckEnemyInSight()
    {
        if (isRetreating)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange);
            foreach (var hit in hits)
            {
                if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
                {
                    UnitController u = hit.GetComponent<UnitController>();
                    if (u != null && !u.isStealthed) return true; 
                    if (hit.GetComponent<BaseController>() != null) return true;
                }
            }
            return false; 
        }
        else
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, transform.up, detectionRange);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject) continue;
                if (hit.collider.CompareTag(owner.enemyTag) || hit.collider.CompareTag(owner.targetBaseTag))
                {
                    return true; 
                }
            }
            return false;
        }
    }
}