using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum TacticalState { Defend, Attack, Siege } 

public class EnemyTacticsManager : MonoBehaviour
{
    private EnemyBot brain;

    [Header("전술 상태")]
    public TacticalState currentState = TacticalState.Defend;
    
    [Header("전선 관리")]
    public Vector3 enemyFrontLinePos; 
    public BaseController currentFrontBase; // 현재 최전선 기지

    private float tacticsTimer = 0f;
    private float siegeCooldown = 0f;
    private float rallyTimer = 0f; 

    public void Initialize(EnemyBot bot)
    {
        this.brain = bot;
        currentState = TacticalState.Defend;
        UpdateFrontline(); // 시작 시 전선 설정
    }

    public void OnUpdate()
    {
        if (siegeCooldown > 0) siegeCooldown -= Time.deltaTime;

        // 1. 전술 상태 판단 (0.5초 주기)
        tacticsTimer += Time.deltaTime;
        if (tacticsTimer >= 0.5f) 
        {
            tacticsTimer = 0f;
            DecideTacticalState();
            UpdateFrontline(); // 전선 위치 갱신
        }

        // 2. 병력 집결 명령 (2초 주기)
        rallyTimer += Time.deltaTime;
        if (rallyTimer >= 2.0f)
        {
            rallyTimer = 0f;
            if (currentState == TacticalState.Defend)
            {
                RallyTroopsToFrontline();
            }
        }
    }

    // 🌟 [핵심 수정] 건설 중인 기지도 전선으로 인정
    void UpdateFrontline()
    {
        // 적 본진(Player) 위치 파악
        Vector3 targetPos = Vector3.zero;
        if (brain.scout.primaryTargetPos != Vector3.zero)
        {
            targetPos = brain.scout.primaryTargetPos;
        }
        else
        {
            GameObject playerBase = GameObject.FindGameObjectWithTag("Player");
            if (playerBase != null) targetPos = playerBase.transform.position;
        }

        // 내 기지 중 적과 가장 가까운 곳 찾기
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var baseCtrl in BaseController.activeBases)
        {
            // 🛑 [수정] 건설 중(!isConstructed)이어도 전선 기지로 인정하기 위해 체크 제거
            if (baseCtrl == null) continue; 
            if (!baseCtrl.CompareTag(brain.myTeamTag)) continue;

            float dst = Vector3.Distance(baseCtrl.transform.position, targetPos);
            if (dst < minDst)
            {
                minDst = dst;
                bestBase = baseCtrl;
            }
        }

        if (bestBase != null)
        {
            currentFrontBase = bestBase;
            enemyFrontLinePos = bestBase.transform.position;
        }
        else
        {
            // 기지가 다 터졌으면 봇 위치를 임시 거점으로
            enemyFrontLinePos = transform.position;
        }
    }

    // 🌟 [신규] 병력 전진 배치 (Frontline Rally)
    void RallyTroopsToFrontline()
    {
        if (currentFrontBase == null) return;

        foreach (var unit in UnitController.activeUnits)
        {
            if (unit == null || unit.isDead || !unit.CompareTag(brain.myTeamTag)) continue;
            if (unit.unitType == UnitType.Worker || unit.unitType == UnitType.Slave) continue;
            if (unit.unitType == UnitType.BaseArcher || unit.unitType == UnitType.BaseCorpse) continue;

            // 현재 위치가 최전선 기지에서 너무 멀다면 이동 명령
            float distToFront = Vector3.Distance(unit.transform.position, enemyFrontLinePos);
            
            if (distToFront > 8.0f) 
            {
                Vector3 rallyPoint = enemyFrontLinePos + (Vector3)Random.insideUnitCircle * 4.0f;
                unit.SetStateToAttackMove(rallyPoint);
            }
        }
    }

    public bool TryTriggerWave(AttackWave wave)
    {
        if (wave.requiredUnits != null && wave.requiredUnits.Count > 0)
        {
            foreach (var pair in wave.requiredUnits)
            {
                int currentCount = CountMyUnit(pair.unitType);
                if (currentCount < pair.count) return false; 
            }
        }

        if (wave.requiredPowerRatio > 0)
        {
            if (brain.scout.enemyTotalPower <= 0) return false;

            float myPower = CalculateMyCombatPower();
            float ratio = myPower / brain.scout.enemyTotalPower;

            if (ratio < wave.requiredPowerRatio) return false; 
        }

        LaunchAllOutAttack();
        return true;
    }

    public void LaunchAllOutAttack()
    {
        Debug.Log("⚔️ [EnemyBot] All-Out Attack Triggered!");
        currentState = TacticalState.Attack;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(brain.myTeamTag) && !unit.isDead)
            {
                if (unit.unitType != UnitType.Worker && unit.unitType != UnitType.Slave && 
                    unit.unitType != UnitType.BaseArcher && unit.unitType != UnitType.BaseCorpse)
                {
                    Vector3 target = brain.scout.primaryTargetPos;
                    unit.SetStateToAttackMove(target);
                }
            }
        }
    }

    void DecideTacticalState()
    {
        if (currentState == TacticalState.Attack)
        {
            float myPower = CalculateMyCombatPower();
            if (myPower < 100f) 
            {
                currentState = TacticalState.Defend;
            }
            return;
        }

        bool underAttack = IsBaseUnderAttack();

        if (underAttack)
        {
            currentState = TacticalState.Defend;
        }
    }

    bool IsBaseUnderAttack()
    {
        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl.CompareTag(brain.myTeamTag))
            {
                if (CalculateLocalEnemyPower(baseCtrl.transform.position, 15f) > 0)
                    return true;
            }
        }
        return false;
    }

    float CalculateLocalEnemyPower(Vector3 center, float radius)
    {
        float power = 0f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player")) 
            {
                UnitController unit = hit.GetComponent<UnitController>();
                if (unit != null && !unit.isDead)
                    power += GetUnitPower(unit); 
            }
        }
        return power;
    }

    public float CalculateMyCombatPower()
    {
        float total = 0f;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(brain.myTeamTag) && !unit.isDead)
            {
                if (unit.unitType == UnitType.Worker || unit.unitType == UnitType.Slave) continue;
                total += GetUnitPower(unit); 
            }
        }
        return total;
    }

    float GetUnitPower(UnitController unit)
    {
        if (unit.unitType == UnitType.BaseArcher || unit.unitType == UnitType.BaseCorpse)
        {
            return 0f;
        }

        float power = unit.currentHP * 0.1f + unit.attackDamage;
        return power;
    }

    int CountMyUnit(UnitType type)
    {
        int count = 0;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(brain.myTeamTag) && !unit.isDead && unit.unitType == type)
            {
                count++;
            }
        }
        return count;
    }

    // ⚡ [신규] 외부(Bot)에서 호출하여 즉시 전선을 갱신하고 병력을 이동시킴
    public void ForceUpdateFrontline()
    {
        // 1. 전선 위치 데이터 갱신 (방금 지어진 Outpost가 최전선이 될 확률 높음)
        UpdateFrontline(); 
        
        // 2. 병력들에게 "새 전선으로 이동해!" 명령 하달
        RallyTroopsToFrontline(); 
        
        Debug.Log("⚔️ [Tactics] Frontline Force Updated via Construction Event.");
    }
}