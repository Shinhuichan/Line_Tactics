using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EnemyProductionManager : MonoBehaviour
{
    private EnemyBot brain;
    private Queue<BuildStep> buildQueue = new Queue<BuildStep>();
    
    private float spawnTimer = 0f;
    private const float SPAWN_INTERVAL = 2.0f; 
    private int myWorkerId = -1;

    private float economyTimer = 0f;
    private const float ECONOMY_CHECK_INTERVAL = 1.0f; 

    [Header("🔍 디버깅용 (Read Only)")]
    public string currentGoalDebug = "None"; 
    public string missingResourceDebug = "None"; 

    public void Initialize(EnemyBot bot)
    {
        this.brain = bot;
        buildQueue.Clear();
        IdentifyMyWorkerType();
    }

    public void ClearQueue()
    {
        buildQueue.Clear();
        Debug.Log("[EnemyProduction] 🧹 Build Queue Cleared! (Strategy Switch)");
    }

    public void OnUpdate()
    {
        ProcessProductionQueue();
        UpdateDebugInfo();
        ProcessEconomyBalancing();
    }

    void IdentifyMyWorkerType()
    {
        myWorkerId = (int)UnitType.Worker;
        if (brain.Strategy != null && brain.Strategy.openingBuildOrder.Count > 0)
        {
            if ((int)brain.Strategy.openingBuildOrder[0].unitType >= 100)
                myWorkerId = (int)UnitType.Slave;
        }
    }

    public void EnqueueStep(BuildStep step)
    {
        buildQueue.Enqueue(step);
    }

    void UpdateDebugInfo()
    {
        if (buildQueue.Count > 0)
        {
            BuildStep step = buildQueue.Peek();
            if (step.stepType == BuildStepType.Unit) 
                currentGoalDebug = $"Unit: {step.unitType}";
            else if (step.stepType == BuildStepType.Upgrade)
                currentGoalDebug = $"Upgrade: {(step.upgradeData != null ? step.upgradeData.upgradeName : "Null")}";
            else 
                currentGoalDebug = "🏗️ EXPANSION (Base)";

            ResourceType? missing = GetMissingResourceForNextItem();
            missingResourceDebug = missing.HasValue ? missing.Value.ToString() : "Ready";
        }
        else
        {
            currentGoalDebug = "Idle";
            missingResourceDebug = "None";
        }
    }

    // 🌟 [핵심 수정] 생산 우선순위 및 업그레이드 예외 처리 강화
    private void ProcessProductionQueue()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer < SPAWN_INTERVAL) return;

        // 1. 전략 큐(Build Queue) 최우선 처리
        bool queueSuccess = false;
        int reservedIron = 0; // 큐 아이템을 위해 남겨둬야 할 자원
        int reservedOil = 0;

        if (buildQueue.Count > 0)
        {
            BuildStep nextStep = buildQueue.Peek();
            
            // 다음 목표의 예상 비용 계산 (자원 보존을 위해)
            CalculateStepCost(nextStep, out reservedIron, out reservedOil);

            bool isSuccess = false;
            string teamTag = brain.myTeamTag; 

            // A. 유닛 생산
            if (nextStep.stepType == BuildStepType.Unit)
            {
                if (CanAffordUnit((int)nextStep.unitType))
                {
                    if (TryPurchaseUnit((int)nextStep.unitType)) isSuccess = true;
                }
            }
            // B. 업그레이드
            else if (nextStep.stepType == BuildStepType.Upgrade)
            {
                if (nextStep.upgradeData != null)
                {
                    // 1. 이미 완료했거나 연구 중이면 큐에서 제거
                    if (UpgradeManager.I.IsUnlocked(nextStep.upgradeData, teamTag) ||
                        UpgradeManager.I.IsResearching(nextStep.upgradeData, teamTag))
                    {
                        buildQueue.Dequeue();
                        return;
                    }

                    // 🛑 [신규] 선행 연구 조건 확인 (Prerequisites Check)
                    // 기획: 선행 업그레이드가 안 되어 있으면 대기열에서 Pass(제거)
                    // IsResearchable은 선행 연구가 완료되지 않았으면 false를 반환함
                    if (!UpgradeManager.I.IsResearchable(nextStep.upgradeData, teamTag))
                    {
                        Debug.Log($"🤖 [{teamTag}] 선행 연구 미달로 {nextStep.upgradeData.upgradeName} 스킵 (Pass)");
                        buildQueue.Dequeue();
                        return;
                    }

                    // 2. 자원 확인 및 구매 시도
                    if (EnemyResourceManager.I.CheckCost(nextStep.upgradeData.ironCost, nextStep.upgradeData.oilCost))
                    {
                        // 위에서 IsResearchable 체크를 통과했으므로 여기서는 자원만 있으면 구매 가능
                        UpgradeManager.I.PurchaseUpgrade(nextStep.upgradeData, teamTag);
                        isSuccess = true;
                    }
                }
                else
                {
                    // 데이터가 비어있으면 삭제
                    buildQueue.Dequeue();
                    return;
                }
            }
            // C. 확장
            else if (nextStep.stepType == BuildStepType.Expansion)
            {
                if (ConstructionManager.I == null || GameManager.I == null) 
                {
                    buildQueue.Dequeue(); 
                    return; 
                }

                UnitData enemyOutpostData = ConstructionManager.I.GetOutpostData(GameManager.I.enemyRace);
                if (enemyOutpostData != null)
                {
                    if (EnemyResourceManager.I.CheckCost(enemyOutpostData.ironCost, enemyOutpostData.oilCost))
                    {
                        bool built = ConstructionManager.I.TryBuildEnemyOutpost(brain.Strategy.expansionPolicy);
                        if (built) 
                        {
                            isSuccess = true;
                            // 🌟 [핵심] 건설 명령 내리자마자 바로 전선 갱신 -> 병력 이동 시작!
                            if (brain.tactics != null)
                            {
                                brain.tactics.ForceUpdateFrontline();
                            }
                        }
                        else { buildQueue.Dequeue(); return; } // 자리 없으면 스킵
                    }
                }
            }

            if (isSuccess)
            {
                buildQueue.Dequeue();
                spawnTimer = 0f;
                queueSuccess = true;
                return; // 큐 아이템 생산 성공 시, 이번 턴에는 일꾼 생산 안 함 (자원 보호)
            }
        }

        // 2. 일꾼 자동 생산 (큐 처리 실패 혹은 큐가 비었을 때 수행)
        if (brain.IsOpeningFinished && NeedMoreWorkers())
        {
            UnitData workerData = SpawnManager.I.GetUnitDataByType((UnitType)myWorkerId);
            if (workerData != null)
            {
                int workerIron = workerData.ironCost;
                int workerOil = workerData.oilCost;

                bool hasSafeResources = false;
                if (EnemyResourceManager.I != null)
                {
                    bool safeIron = EnemyResourceManager.I.currentIron >= (workerIron + reservedIron);
                    bool safeOil = EnemyResourceManager.I.currentOil >= (workerOil + reservedOil);
                    hasSafeResources = safeIron && safeOil;
                }

                if (hasSafeResources && buildQueue.Count < 3) 
                {
                    if (TryPurchaseUnit(myWorkerId))
                    {
                        spawnTimer = 0f;
                    }
                }
            }
        }
    }

    void CalculateStepCost(BuildStep step, out int iron, out int oil)
    {
        iron = 0;
        oil = 0;
        
        if (step.stepType == BuildStepType.Unit)
        {
            if (SpawnManager.I != null)
            {
                UnitData data = SpawnManager.I.GetUnitDataByType(step.unitType);
                if (data != null) { iron = data.ironCost; oil = data.oilCost; }
            }
        }
        else if (step.stepType == BuildStepType.Upgrade && step.upgradeData != null)
        {
            iron = step.upgradeData.ironCost;
            oil = step.upgradeData.oilCost;
        }
        else if (step.stepType == BuildStepType.Expansion)
        {
            if (ConstructionManager.I != null && GameManager.I != null)
            {
                UnitData data = ConstructionManager.I.GetOutpostData(GameManager.I.enemyRace);
                if (data != null) { iron = data.ironCost; oil = data.oilCost; }
            }
        }
    }

    private void ProcessEconomyBalancing()
    {
        economyTimer += Time.deltaTime;
        if (economyTimer < ECONOMY_CHECK_INTERVAL) return;
        economyTimer = 0f;

        ResourceType? missing = GetMissingResourceForNextItem();
        
        List<WorkerAbility> myWorkers = GetMyWorkers();
        if (myWorkers.Count == 0) return;

        List<WorkerAbility> ironMiners = myWorkers.Where(w => w.targetResourceType == ResourceType.Iron && IsMiningOrMoving(w)).ToList();
        List<WorkerAbility> oilMiners = myWorkers.Where(w => w.targetResourceType == ResourceType.Oil && IsMiningOrMoving(w)).ToList();

        if (missing == ResourceType.Oil)
        {
            if (ironMiners.Count > 0)
            {
                WorkerAbility worker = ironMiners[0];
                worker.SetStateToMine(ResourceType.Oil);
            }
        }
        else if (missing == ResourceType.Iron)
        {
            if (oilMiners.Count > 1) 
            {
                WorkerAbility worker = oilMiners[0];
                worker.SetStateToMine(ResourceType.Iron);
            }
        }
    }

    bool IsMiningOrMoving(WorkerAbility w)
    {
        return w.currentState == WorkerState.Mining || 
               w.currentState == WorkerState.MovingToResource || 
               w.currentState == WorkerState.ReturningToBase ||
               w.currentState == WorkerState.Idle;
    }

    List<WorkerAbility> GetMyWorkers()
    {
        List<WorkerAbility> list = new List<WorkerAbility>();
        WorkerAbility[] all = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        foreach(var w in all)
        {
            if (w.CompareTag(brain.myTeamTag) && !w.owner.isDead)
            {
                list.Add(w);
            }
        }
        return list;
    }

    public ResourceType? GetMissingResourceForNextItem()
    {
        if (buildQueue.Count == 0) return null;

        BuildStep next = buildQueue.Peek();
        int ironCost = 0;
        int oilCost = 0;

        CalculateStepCost(next, out ironCost, out oilCost); 

        if (EnemyResourceManager.I != null)
        {
            if (EnemyResourceManager.I.currentOil < oilCost) return ResourceType.Oil;
            if (EnemyResourceManager.I.currentIron < ironCost) return ResourceType.Iron;
        }
        return null;
    }

    bool CanAffordUnit(int unitId)
    {
        if (SpawnManager.I == null || EnemyResourceManager.I == null) return false;
        UnitData data = SpawnManager.I.GetUnitDataByType((UnitType)unitId);
        if (data == null) return false;
        return EnemyResourceManager.I.CheckCost(data.ironCost, data.oilCost);
    }

    bool NeedMoreWorkers()
    {
        if (brain.Strategy == null) return false;
        int currentWorkers = 0;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(gameObject.tag) && !unit.isDead)
            {
                if (unit.unitType == UnitType.Worker || unit.unitType == UnitType.Slave)
                    currentWorkers++;
            }
        }
        return currentWorkers < brain.Strategy.idealWorkerCount;
    }

    public bool TryPurchaseUnit(int unitId)
    {
        if (SpawnManager.I == null) return false;
        return SpawnManager.I.TrySpawnEnemyUnit(unitId);
    }

    public int GetQueueCount() => buildQueue.Count;
    
    public string GetNextItemName()
    {
        if (buildQueue.Count == 0) return "Empty";
        var next = buildQueue.Peek();
        if (next.stepType == BuildStepType.Unit) return next.unitType.ToString();
        if (next.stepType == BuildStepType.Expansion) return "EXPANSION"; 
        return next.upgradeData != null ? next.upgradeData.upgradeName : "Null Upgrade";
    }
}