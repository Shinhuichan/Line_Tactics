using UnityEngine;
using System.Collections.Generic;

public class PlayerScoutManager : MonoBehaviour
{
    private PlayerBot brain;

    [Header("적 정보 (Enemy)")]
    public float enemyTotalPower = 0f;
    public int enemyUnitCount = 0;
    public int enemyBaseCount = 0;
    public Vector3 primaryTargetPos;

    [Header("내 정보 (Player)")]
    public int myBaseCount = 0; 

    private float scanTimer = 0f;
    private const float SCAN_INTERVAL = 1.0f;

    public void Initialize(PlayerBot bot)
    {
        this.brain = bot;
    }

    public void OnUpdate()
    {
        scanTimer += Time.deltaTime;
        if (scanTimer >= SCAN_INTERVAL)
        {
            scanTimer = 0f;
            AnalyzeBattlefield();
        }
    }

    void AnalyzeBattlefield()
    {
        ResetData();

        // 1. 유닛 분석
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag("Enemy") && !unit.isDead)
            {
                enemyUnitCount++;
                enemyTotalPower += CalculateUnitPower(unit);
            }
        }

        // 2. 기지 분석
        BaseController[] allBases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        
        Vector3 myMainBasePos = transform.position; 
        float minDist = Mathf.Infinity;
        BaseController mainTarget = null;

        foreach (var baseCtrl in allBases)
        {
            if (baseCtrl.CompareTag("Player"))
            {
                myBaseCount++;
            }
            else if (baseCtrl.CompareTag("Enemy"))
            {
                enemyBaseCount++;
                float d = Vector3.Distance(myMainBasePos, baseCtrl.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    mainTarget = baseCtrl;
                }
            }
        }

        // 타겟 설정
        if (mainTarget != null)
        {
            primaryTargetPos = mainTarget.transform.position;
        }
        else
        {
            UnitController closestUnit = GetNearestEnemyUnit(myMainBasePos);
            if (closestUnit != null) primaryTargetPos = closestUnit.transform.position;
        }
    }

    // ⚡ [수정] 성채 유닛(장궁병, 시체병) 완전 제외
    float CalculateUnitPower(UnitController unit)
    {
        if (unit.unitType == UnitType.BaseArcher || unit.unitType == UnitType.BaseCorpse)
        {
            return 0f; 
        }

        float power = unit.currentHP * 0.1f + unit.attackDamage;
        if (unit.attackCooldown > 0) power += (1.0f / unit.attackCooldown) * 10f;
        return power;
    }

    UnitController GetNearestEnemyUnit(Vector3 fromPos)
    {
        UnitController nearest = null;
        float minDst = Mathf.Infinity;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag("Enemy") && !unit.isDead)
            {
                float d = Vector3.Distance(fromPos, unit.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    nearest = unit;
                }
            }
        }
        return nearest;
    }

    void ResetData()
    {
        enemyTotalPower = 0f;
        enemyUnitCount = 0;
        enemyBaseCount = 0;
        myBaseCount = 0; 
    }
}