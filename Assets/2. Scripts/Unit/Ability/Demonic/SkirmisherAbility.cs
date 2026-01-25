using UnityEngine;

public class SkirmisherAbility : UnitAbility
{
    [Header("척후병 생존 본능")]
    [Range(0f, 1f)] public float fleeHpRatio = 0.3f;      // 30% 이하일 때 도망
    [Range(0f, 1f)] public float reengageHpRatio = 0.7f;  // 70% 이상일 때 복귀
    public float fleeSpeedMultiplier = 1.5f;              // 도망칠 때 이동속도 50% 증가 (매우 빠름)

    [Header("상태 (Read Only)")]
    public bool isFleeing = false;

    // 도망치는 중에는 '바쁨(Busy)' 상태로 간주하여 UnitController의 기본 공격 AI를 막음
    public override bool IsBusy => isFleeing;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override void OnUpdate()
    {
        // 1. 체력 상태 체크 및 모드 전환
        CheckHealthState();

        // 2. 도망 로직 실행
        if (isFleeing)
        {
            ProcessFleeToBase();
        }
    }

    void CheckHealthState()
    {
        float hpRatio = owner.currentHP / owner.maxHP;

        // 전투 중 -> 도망 모드
        if (!isFleeing)
        {
            if (hpRatio <= fleeHpRatio)
            {
                StartFleeing();
            }
        }
        // 도망 중 -> 전투 복귀
        else
        {
            // 데모닉 종족 특성으로 체력이 차올라서 기준치를 넘으면 복귀
            if (hpRatio >= reengageHpRatio)
            {
                StopFleeing();
            }
        }
    }

    void StartFleeing()
    {
        isFleeing = true;
        owner.isManualMove = true; // AI 제어권을 가져옴 (내가 직접 움직인다)
        
        // 이동속도 대폭 증가
        owner.SetMultipliers(1.0f, fleeSpeedMultiplier, 1.0f);

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Run Away!", Color.white, 20);
    }

    void StopFleeing()
    {
        isFleeing = false;
        owner.isManualMove = false; // AI 제어권 반납 (다시 싸우러 감)
        
        // 이동속도 원상복구
        owner.SetMultipliers(1.0f, 1.0f, 1.0f);

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "I'm Back!", Color.red, 25);
    }

    void ProcessFleeToBase()
    {
        // 1. 가장 가까운 아군 기지 찾기
        BaseController safeHouse = FindNearestFriendlyBase();

        if (safeHouse != null)
        {
            // 2. 기지 방향으로 이동
            // 기지 중심부보다는 약간 앞에서 멈추거나 내부로 들어가도 됨
            MoveTowards(safeHouse.transform.position);
        }
        else
        {
            // 🚨 만약 아군 기지가 하나도 없다면? (엘리전 상황)
            // 차선책: 적 반대 방향으로 도망
            FleeFromNearestEnemy();
        }
    }

    BaseController FindNearestFriendlyBase()
    {
        BaseController[] bases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var b in bases)
        {
            // 내 편이고 + 파괴되지 않은 건물만
            if (b.CompareTag(owner.tag) && b.isConstructed)
            {
                float dst = Vector3.Distance(transform.position, b.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    bestBase = b;
                }
            }
        }
        return bestBase;
    }

    void FleeFromNearestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, owner.detectRange);
        GameObject nearestEnemy = null;
        float minDst = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                float dst = Vector3.Distance(transform.position, hit.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    nearestEnemy = hit.gameObject;
                }
            }
        }

        if (nearestEnemy != null)
        {
            Vector3 dir = (transform.position - nearestEnemy.transform.position).normalized;
            MoveTowards(transform.position + dir * 5.0f);
        }
    }

    void MoveTowards(Vector3 targetPos)
    {
        float step = owner.moveSpeed * Time.deltaTime; // 버프된 속도 적용됨
        transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

        // 회전 (이동 방향 보게)
        Vector3 dir = targetPos - transform.position;
        if (dir != Vector3.zero)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.AngleAxis(angle, Vector3.forward), Time.deltaTime * 10f);
        }
    }
}