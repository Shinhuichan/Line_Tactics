using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PlayerProductionManager : MonoBehaviour
{
    private PlayerBot brain;
    private Queue<BuildStep> buildQueue = new Queue<BuildStep>();
    
    private float spawnTimer = 0f;
    private const float SPAWN_INTERVAL = 2.0f; 
    private int myWorkerId = -1;

    private float economyTimer = 0f;
    private const float ECONOMY_CHECK_INTERVAL = 1.0f; 

    public void Initialize(PlayerBot bot)
    {
        this.brain = bot;
        buildQueue.Clear();
        IdentifyMyWorkerType();
    }

    public void ClearQueue()
    {
        buildQueue.Clear();
        Debug.Log("[PlayerProduction] 🧹 Build Queue Cleared! (Strategy Switch)");
    }

    public void OnUpdate()
    {
        ProcessProductionQueue();
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

    private void ProcessProductionQueue()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer < SPAWN_INTERVAL) return;

        // 1. 자원 예약 계산
        int reservedIron = 0;
        int reservedOil = 0;
        if (buildQueue.Count > 0)
        {
            CalculateStepCost(buildQueue.Peek(), out reservedIron, out reservedOil);
        }

        // 2. 전략 큐 최우선 처리
        if (buildQueue.Count > 0)
        {
            BuildStep nextStep = buildQueue.Peek();
            bool isSuccess = false;
            string teamTag = brain.myTeamTag; 

            if (nextStep.stepType == BuildStepType.Unit)
            {
                if (CanAffordUnit((int)nextStep.unitType))
                {
                    if (TryPurchaseUnit((int)nextStep.unitType)) isSuccess = true;
                }
            }
            else if (nextStep.stepType == BuildStepType.Upgrade)
            {
                if (nextStep.upgradeData != null)
                {
                    if (UpgradeManager.I.IsUnlocked(nextStep.upgradeData, teamTag) ||
                        UpgradeManager.I.IsResearching(nextStep.upgradeData, teamTag))
                    {
                        buildQueue.Dequeue();
                        return;
                    }

                    if (ResourceManager.I.CheckCost(nextStep.upgradeData.ironCost, nextStep.upgradeData.oilCost))
                    {
                        if (UpgradeManager.I.IsResearchable(nextStep.upgradeData, teamTag))
                        {
                            UpgradeManager.I.PurchaseUpgrade(nextStep.upgradeData, teamTag);
                            isSuccess = true;
                        }
                    }
                }
                else
                {
                    buildQueue.Dequeue();
                    return;
                }
            }
            else if (nextStep.stepType == BuildStepType.Expansion)
            {
                if (ConstructionManager.I == null || GameManager.I == null) 
                {
                    buildQueue.Dequeue(); 
                    return; 
                }

                UnitData outpostData = ConstructionManager.I.GetOutpostData(GameManager.I.playerRace);
                if (outpostData != null)
                {
                    if (ResourceManager.I.CheckCost(outpostData.ironCost, outpostData.oilCost))
                    {
                        bool built = ConstructionManager.I.TryBuildPlayerOutpost(brain.Strategy.expansionPolicy);
                        if (built) isSuccess = true;
                        else { buildQueue.Dequeue(); return; }
                    }
                }
            }

            if (isSuccess)
            {
                buildQueue.Dequeue();
                spawnTimer = 0f;
                return; 
            }
        }

        // 3. 일꾼 자동 생산 (여유 자원 있을 때만)
        if (brain.IsOpeningFinished && NeedMoreWorkers())
        {
            UnitData workerData = SpawnManager.I.GetUnitDataByType((UnitType)myWorkerId);
            if (workerData != null)
            {
                int workerIron = workerData.ironCost;
                int workerOil = workerData.oilCost;

                bool hasSafeResources = false;
                if (ResourceManager.I != null)
                {
                    bool safeIron = ResourceManager.I.currentIron >= (workerIron + reservedIron);
                    bool safeOil = ResourceManager.I.currentOil >= (workerOil + reservedOil);
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
                UnitData data = ConstructionManager.I.GetOutpostData(GameManager.I.playerRace);
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

        if (ResourceManager.I != null)
        {
            if (ResourceManager.I.currentOil < oilCost) return ResourceType.Oil;
            if (ResourceManager.I.currentIron < ironCost) return ResourceType.Iron;
        }
        return null;
    }

    bool CanAffordUnit(int unitId)
    {
        if (SpawnManager.I == null || ResourceManager.I == null) return false;
        UnitData data = SpawnManager.I.GetUnitDataByType((UnitType)unitId);
        if (data == null) return false;
        return ResourceManager.I.CheckCost(data.ironCost, data.oilCost);
    }

    bool NeedMoreWorkers()
    {
        if (brain.Strategy == null) return false;
        int currentWorkers = 0;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(brain.myTeamTag) && !unit.isDead)
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
        return SpawnManager.I.TrySpawnPlayerUnit(unitId);
    }

    public int GetQueueCount() => buildQueue.Count;
}