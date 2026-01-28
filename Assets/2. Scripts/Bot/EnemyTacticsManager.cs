using UnityEngine;
using System.Collections.Generic;

public class EnemyTacticsManager : MonoBehaviour
{
    private EnemyBot brain;
    private float tacticsTimer = 0f;
    private float siegeCooldown = 0f;

    // 🌟 [수정] 자체 변수 대신 EnemyCommandManager 참조 (PlayerBot 구조와 통일)
    // 외부(EnemyBot 등)에서 currentState를 참조해도 문제 없도록 프로퍼티로 연결
    public TacticalState currentState
    {
        get 
        { 
            if (EnemyCommandManager.I == null) return TacticalState.Defend;
            return EnemyCommandManager.I.currentState; 
        }
        private set 
        {
            if (EnemyCommandManager.I != null) EnemyCommandManager.I.SetState(value);
        }
    }

    [Header("전선 관리")]
    public Vector3 enemyFrontLinePos; 
    public BaseController currentFrontBase;

    public void Initialize(EnemyBot bot)
    {
        this.brain = bot;
        
        // CommandManager 초기화 확인 (없으면 생성됨)
        if (EnemyCommandManager.I == null)
        {
            GameObject mgrObj = new GameObject("EnemyCommandManager");
            mgrObj.AddComponent<EnemyCommandManager>();
        }

        // 초기 상태 설정
        if (EnemyCommandManager.I != null)
            EnemyCommandManager.I.SetState(TacticalState.Defend);

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

    // 🌟 [핵심 수정] 건설 중인 기지를 최우선 방어 지점으로 설정하여 오락가락 방지
    void UpdateFrontline()
    {
        // 1. 건설 중인 아군 기지가 있는지 먼저 확인 (최우선 순위)
        BaseController constructionBase = null;
        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl == null) continue;
            // 내 기지이고, 아직 건설이 안 끝났다면
            if (baseCtrl.CompareTag(brain.myTeamTag) && !baseCtrl.isConstructed)
            {
                constructionBase = baseCtrl;
                break; // 하나라도 찾으면 즉시 해당 위치 사수
            }
        }

        if (constructionBase != null)
        {
            currentFrontBase = constructionBase;
            enemyFrontLinePos = constructionBase.transform.position;
            return; // 🛑 더 계산하지 않고 리턴 (전선 고정)
        }

        // 2. 건설 중인 기지가 없다면 기존 로직대로 "적과 가장 가까운 기지" 탐색
        Vector3 targetPos = Vector3.zero;
        if (brain.scout != null && brain.scout.primaryTargetPos != Vector3.zero)
        {
            targetPos = brain.scout.primaryTargetPos;
        }
        else
        {
            GameObject playerBase = GameObject.FindGameObjectWithTag("Player");
            if (playerBase != null) targetPos = playerBase.transform.position;
        }

        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var baseCtrl in BaseController.activeBases)
        {
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
            enemyFrontLinePos = transform.position;
        }
    }

    void RallyTroopsToFrontline()
    {
        if (currentFrontBase == null) return;

        foreach (var unit in UnitController.activeUnits)
        {
            if (unit == null || unit.isDead || !unit.CompareTag(brain.myTeamTag)) continue;
            
            if (unit.unitType == UnitType.Worker || unit.unitType == UnitType.Slave) continue;
            if (unit.unitType == UnitType.BaseArcher || unit.unitType == UnitType.BaseCorpse) continue;

            float distToFront = Vector3.Distance(unit.transform.position, enemyFrontLinePos);
            
            if (distToFront > 8.0f) 
            {
                Vector3 rallyPoint = enemyFrontLinePos + (Vector3)Random.insideUnitCircle * 4.0f;
                unit.SetStateToAttackMove(rallyPoint);
            }
        }
    }

    public void ForceUpdateFrontline()
    {
        UpdateFrontline();
        RallyTroopsToFrontline();
        Debug.Log("⚔️ [EnemyTactics] Frontline Force Updated via Construction Event.");
    }

    public bool TryTriggerWave(AttackWave wave)
    {
        // 1. 유닛 수량 충족 여부 확인
        if (wave.requiredUnits != null && wave.requiredUnits.Count > 0)
        {
            foreach (var pair in wave.requiredUnits)
            {
                int currentCount = CountMyUnit(pair.unitType);
                if (currentCount < pair.count) return false; 
            }
        }

        // 2. 전력 비율 확인 (Power Ratio)
        if (wave.requiredPowerRatio > 0)
        {
            // 🌟 [수정] 적 전력이 0이면 (전멸 혹은 극초반) 무조건 공격 가능 (Infinite Ratio)
            // 기존: if (enemyPower <= 0) return false; (공격 불가) -> 수정됨
            if (brain.scout.enemyTotalPower > 0)
            {
                float myPower = CalculateMyCombatPower();
                float ratio = myPower / brain.scout.enemyTotalPower;

                if (ratio < wave.requiredPowerRatio) return false; 
            }
            // else: 적 전력이 0이면 통과 (공격 감행)
        }

        LaunchAllOutAttack();
        return true;
    }

    void LaunchAllOutAttack()
    {
        Debug.Log("⚔️ [EnemyBot] All-Out Attack Triggered!");
        
        // 🌟 [수정] CommandManager를 통해 상태 변경
        if (EnemyCommandManager.I != null)
            EnemyCommandManager.I.SetState(TacticalState.Attack);

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
        TacticalState current = (EnemyCommandManager.I != null) ? EnemyCommandManager.I.currentState : TacticalState.Defend;

        if (current == TacticalState.Attack)
        {
            float myPower = CalculateMyCombatPower();
            
            // 🌟 [수정] 후퇴 임계점 완화 (100 -> 20)
            // 공격을 시작했는데 병력이 100 이하면 바로 후퇴하는 문제 해결
            if (myPower < 20f) 
            {
                 if (EnemyCommandManager.I != null)
                    EnemyCommandManager.I.SetState(TacticalState.Defend);
            }
            return;
        }

        bool underAttack = IsBaseUnderAttack();

        if (underAttack)
        {
             if (EnemyCommandManager.I != null)
                EnemyCommandManager.I.SetState(TacticalState.Defend);
        }
        else
        {
            RallyTroopsToFrontline();
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