using UnityEngine;
using System.Collections;

public class SkeletonAbility : UnitAbility
{
    [Header("해골병 설정")]
    public float followDistance = 2.0f;     
    public float lifeTimeAfterMasterDeath = 3.0f; 

    [Header("상태 (Read Only)")]
    public UnitController masterUnit;       
    public GameObject forcedTarget;         
    public bool isBuffed = false;           

    private bool isMasterDead = false;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        isMasterDead = false;
        forcedTarget = null;
        isBuffed = false;
    }

    public void SetMaster(UnitController master)
    {
        masterUnit = master;
        owner.isManualMove = true; 
    }

    public override void OnUpdate()
    {
        // 마스터 사망 처리
        if (isMasterDead || masterUnit == null || masterUnit.isDead)
        {
            if (!isMasterDead) OnMasterDied();
            owner.isManualMove = false; 
            return;
        }

        // 1. 강제 타겟(적)이 있는 경우 - 공격 이동
        if (forcedTarget != null && forcedTarget.activeInHierarchy)
        {
            // 🌟 [핵심 수정] 테두리 거리 계산 로직
            Vector3 targetPos = forcedTarget.transform.position;
            Collider2D targetCol = forcedTarget.GetComponent<Collider2D>();
            
            // 콜라이더가 있다면 가장 가까운 지점(테두리)을 목표로 설정
            if (targetCol != null)
            {
                targetPos = targetCol.ClosestPoint(transform.position);
            }

            float dist = Vector3.Distance(transform.position, targetPos);
            
            // 사거리보다 멀면 이동 (테두리 기준)
            if (dist > owner.attackRange)
            {
                owner.MoveTo(targetPos);
            }
            // 사거리 안이면 멈춤 (MoveTo 호출 안 함 -> 공격 시작)
        }
        // 2. 타겟 없음 - 마스터 따라다니기
        else
        {
            float distToMaster = Vector3.Distance(transform.position, masterUnit.transform.position);
            if (distToMaster > followDistance)
            {
                Vector3 dest = masterUnit.transform.position + (transform.position - masterUnit.transform.position).normalized * followDistance;
                owner.MoveTo(dest);
            }
        }
    }

    public void CommandAttack(GameObject target, bool useSpeedBuff, float speedMultiplier)
    {
        forcedTarget = target;

        if (useSpeedBuff && !isBuffed)
        {
            isBuffed = true;
            owner.SetMultipliers(1.0f, speedMultiplier, 1.0f); 
            
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Charge!", Color.white, 20);
        }
    }

    public void OnMasterDied()
    {
        if (isMasterDead) return;
        if (!gameObject.activeInHierarchy) return;

        isMasterDead = true;
        owner.isManualMove = false; 
        forcedTarget = null;

        StartCoroutine(SelfDestructRoutine());
    }

    IEnumerator SelfDestructRoutine()
    {
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "3...", Color.red, 25);

        yield return new WaitForSeconds(lifeTimeAfterMasterDeath);
        
        if (gameObject.activeInHierarchy)
        {
            owner.TakeDamage(99999f, true); 
        }
    }
}