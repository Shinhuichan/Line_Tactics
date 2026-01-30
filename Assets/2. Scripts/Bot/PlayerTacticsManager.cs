using UnityEngine;
using System.Collections.Generic;

public class PlayerTacticsManager : MonoBehaviour
{
    private PlayerBot brain;
    private float tacticsTimer = 0f;
    private float siegeCooldown = 0f;

    // 🏳️ [신규] 후퇴 판단용 변수 (EnemyTacticsManager와 동일 로직 적용)
    private float initialWavePower = 0f;        // 공격 시작 시점의 아군 총 전력
    private float currentRetreatThreshold = 0f; // 현재 웨이브의 후퇴 임계값 (0~1)

    [Header("전선 관리")]
    public Vector3 playerFrontLinePos; 
    public BaseController currentFrontBase;

    public void Initialize(PlayerBot bot)
    {
        this.brain = bot;
        UpdateFrontline();
    }

    public void OnUpdate()
    {
        if (siegeCooldown > 0) siegeCooldown -= Time.deltaTime;

        tacticsTimer += Time.deltaTime;
        if (tacticsTimer >= 0.5f) 
        {
            tacticsTimer = 0f;
            DecideTacticalState();
            UpdateFrontline(); 
        }
    }

    // 1. 현재 전선(가장 적과 가까운 아군 기지) 찾기 및 명령 하달
    void UpdateFrontline()
    {
        Vector3 targetPos = Vector3.zero;
        if (brain.scout != null && brain.scout.primaryTargetPos != Vector3.zero)
        {
            targetPos = brain.scout.primaryTargetPos;
        }
        else
        {
            GameObject enemyBase = GameObject.FindGameObjectWithTag("Enemy");
            if (enemyBase != null) targetPos = enemyBase.transform.position;
        }

        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl == null) continue;
            if (!baseCtrl.CompareTag(brain.myTeamTag)) continue;
            if (!baseCtrl.isConstructed) continue; 

            float dst = Vector3.Distance(baseCtrl.transform.position, targetPos);
            if (dst < minDst)
            {
                minDst = dst;
                bestBase = baseCtrl;
            }
        }

        if (bestBase != null)
        {
            if (currentFrontBase != bestBase)
            {
                currentFrontBase = bestBase;
                playerFrontLinePos = bestBase.transform.position;

                if (TacticalCommandManager.I != null && ConstructionManager.I != null)
                {
                    SyncRallyPointToFront(bestBase);
                }
            }
        }
        else
        {
            playerFrontLinePos = transform.position; 
        }
    }

    void SyncRallyPointToFront(BaseController baseCtrl)
    {
        int bestIndex = -1;
        float minDist = 5.0f;

        for (int i = 0; i < ConstructionManager.I.tacticalPoints.Count; i++)
        {
            Transform point = ConstructionManager.I.tacticalPoints[i];
            if (point == null) continue;

            float dist = Vector3.Distance(point.position, baseCtrl.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                bestIndex = i;
            }
        }

        if (bestIndex != -1)
        {
            TacticalCommandManager.I.SetRallyPointByIndex(bestIndex);
        }
    }

    public void ForceUpdateFrontline()
    {
        UpdateFrontline();
        Debug.Log("⚔️ [PlayerTactics] Frontline Synced via Construction Event.");
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
            if (brain.scout.enemyTotalPower <= 0)
            {
                // 적 전력이 0이면 무조건 공격 가능하지만, 일단 로직 흐름상 유지
            } 
            else 
            {
                float myPower = CalculateMyCombatPower();
                float ratio = myPower / brain.scout.enemyTotalPower;
                if (ratio < wave.requiredPowerRatio) return false; 
            }
        }

        // 🏳️ [신규] 공격 시작 전, 현재 전력과 후퇴 기준 저장
        initialWavePower = CalculateMyCombatPower();
        currentRetreatThreshold = wave.retreatThreshold;

        LaunchAllOutAttack();
        return true;
    }

    void LaunchAllOutAttack()
    {
        Debug.Log($"⚔️ [PlayerBot] All-Out Attack Triggered! (Initial: {initialWavePower:F1}, Retreat At: {currentRetreatThreshold * 100}%)");
        
        if (TacticalCommandManager.I != null)
        {
            TacticalCommandManager.I.SetState(TacticalState.Attack);
        }

        // [추가] 모든 전투 유닛에게 적 기지(Scout이 찾은 타겟)로 공격 이동 명령 하달
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(brain.myTeamTag) && !unit.isDead)
            {
                // 일꾼 및 고정형 유닛 제외
                if (unit.unitType != UnitType.Worker && unit.unitType != UnitType.Slave && 
                    unit.unitType != UnitType.BaseArcher && unit.unitType != UnitType.BaseCorpse)
                {
                    // Scout Manager가 분석한 적의 주요 위치(주로 적 기지)를 타겟으로 설정
                    Vector3 target = brain.scout.primaryTargetPos;
                    unit.SetStateToAttackMove(target);
                }
            }
        }
    }

    void DecideTacticalState()
    {
        if (TacticalCommandManager.I == null) return;
        TacticalState currentState = TacticalCommandManager.I.currentState;

        // 🏳️ [수정] 공격 중 전력 손실 비율 체크 후 퇴각
        if (currentState == TacticalState.Attack)
        {
            float currentPower = CalculateMyCombatPower();
            
            // 전력 비율 계산 (초기 전력이 0이면 0으로 처리)
            float powerRatio = (initialWavePower > 0) ? (currentPower / initialWavePower) : 0f;

            // 1. 현재 전력이 0이거나
            // 2. 남은 전력 비율이 임계값 이하로 떨어지면 후퇴
            if (currentPower <= 0 || powerRatio <= currentRetreatThreshold)
            {
                Debug.Log($"🏳️ [PlayerBot] Retreating! Power dropped to {powerRatio * 100:F1}% (Threshold: {currentRetreatThreshold * 100}%)");
                TacticalCommandManager.I.SetState(TacticalState.Defend);
            }
            return;
        }

        // 본진이 공격받으면 방어(Defend)로 전환 (농성 중이 아닐 때만)
        bool underAttack = IsBaseUnderAttack();
        if (underAttack)
        {
            if(currentState != TacticalState.Defend && currentState != TacticalState.Siege)
                TacticalCommandManager.I.SetState(TacticalState.Defend);
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
            if (hit.CompareTag("Enemy")) 
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
            return 0f;

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
}