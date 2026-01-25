using UnityEngine;
using System.Collections.Generic;

public class NecromancerAbility : UnitAbility
{
    [Header("소환 설정")]
    public int maxSkeletons = 3;        
    public float summonRadius = 1.5f;   

    [Header("업그레이드: 해골 지배력")]
    public string masteryUpgradeKey = "SKELETON_MASTERY"; // 🌟 업그레이드 키
    public int bonusSkeletonCount = 1; // 업그레이드 시 추가될 마리 수 (+1)

    [Header("지휘 설정")]
    public bool useSpeedBuff = true;    
    public float skeletonSpeedMultiplier = 1.25f; 

    [Header("거리 유지 (AI)")]
    public float safeDistance = 3.5f;   

    [Header("상태 (Read Only)")]
    public List<SkeletonAbility> mySkeletons = new List<SkeletonAbility>();
    private float cooldownTimer = 0f;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        mySkeletons.Clear();
    }

    public override void OnUpdate()
    {
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        TrySummonSkeleton();

        GameObject nearestEnemy = FindNearestEnemy();

        CommandSkeletons(nearestEnemy);

        HandleNecromancerMovement(nearestEnemy);
    }

    void TrySummonSkeleton()
    {
        // 죽은 해골 리스트에서 정리
        mySkeletons.RemoveAll(s => s == null || s.GetComponent<UnitController>().isDead);

        // 🌟 [핵심 수정] 현재 최대 소환 가능 수 계산
        int currentLimit = maxSkeletons;

        // 업그레이드 확인
        if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(masteryUpgradeKey, owner.tag))
        {
            currentLimit += bonusSkeletonCount; // 3 + 1 = 4
        }

        // 한계치 도달 시 소환 중단
        if (mySkeletons.Count >= currentLimit || cooldownTimer > 0) return;

        SummonProcess();
        cooldownTimer = owner.attackCooldown; 
    }

    void SummonProcess()
    {
        if (SpawnManager.I == null || PoolManager.I == null) return;

        UnitData skelData = SpawnManager.I.GetUnitDataByType(UnitType.Skeleton); 
        if (skelData == null && SpawnManager.I.demonicUnits != null)
            skelData = SpawnManager.I.demonicUnits.Find(u => u.type == UnitType.Skeleton);

        if (skelData != null)
        {
            Vector3 spawnPos = transform.position + (Vector3)Random.insideUnitCircle * summonRadius;
            GameObject obj = PoolManager.I.Get(UnitType.Skeleton);
            
            if (obj != null)
            {
                obj.transform.position = spawnPos;
                obj.transform.rotation = transform.rotation;

                UnitController ctrl = obj.GetComponent<UnitController>();
                if (ctrl != null)
                {
                    ctrl.Initialize(skelData, owner.tag);
                    
                    SkeletonAbility ability = obj.GetComponent<SkeletonAbility>();
                    if (ability == null) ability = obj.AddComponent<SkeletonAbility>(); 
                    
                    ability.SetMaster(owner); 
                    mySkeletons.Add(ability);

                    if (FloatingTextManager.I != null)
                        FloatingTextManager.I.ShowText(spawnPos, "Rise!", Color.gray, 20);
                }
            }
        }
    }

    void CommandSkeletons(GameObject enemy)
    {
        if (enemy != null)
        {
            foreach (var skel in mySkeletons)
            {
                if (skel != null) skel.CommandAttack(enemy, useSpeedBuff, skeletonSpeedMultiplier);
            }
        }
        else
        {
            foreach (var skel in mySkeletons)
            {
                if (skel != null) skel.forcedTarget = null;
            }
        }
    }

    void HandleNecromancerMovement(GameObject enemy)
    {
        if (enemy != null)
        {
            owner.isManualMove = true; 

            // 테두리 거리 계산 로직 (기존 유지)
            Vector3 enemyPos = enemy.transform.position;
            Collider2D enemyCol = enemy.GetComponent<Collider2D>();
            if (enemyCol != null)
            {
                enemyPos = enemyCol.ClosestPoint(transform.position);
            }

            float dist = Vector3.Distance(transform.position, enemyPos);

            if (dist < safeDistance)
            {
                Vector3 dir = (transform.position - enemy.transform.position).normalized;
                owner.MoveTo(transform.position + dir * 3.0f);
            }
            else if (dist > owner.attackRange - 0.1f)
            {
                owner.MoveTo(enemyPos);
            }
            else
            {
                // 안정권 정지
            }
        }
        else
        {
            owner.isManualMove = false; 
        }
    }

    GameObject FindNearestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, owner.detectRange);
        GameObject nearest = null;
        float minDst = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                UnitController u = hit.GetComponent<UnitController>();
                if (u != null && u.isStealthed) continue; 

                float dst = Vector3.Distance(transform.position, hit.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    nearest = hit.gameObject;
                }
            }
        }
        return nearest;
    }

    public override bool OnDie()
    {
        foreach (var skel in mySkeletons)
        {
            if (skel != null && skel.gameObject.activeInHierarchy)
            {
                skel.OnMasterDied();
            }
        }
        return false; 
    }
}