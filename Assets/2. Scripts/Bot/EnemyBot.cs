using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public enum BotResponsiveness { Stubborn, Reactive }
public enum ExpansionPolicy { ForwardBase, SafeExpand }

public class EnemyBot : MonoBehaviour
{
    [HideInInspector] public EnemyProductionManager production;
    [HideInInspector] public EnemyTacticsManager tactics;
    [HideInInspector] public EnemyScoutManager scout;

    [Header("🤖 AI Strategy Pools (Random Pick)")]
    public List<BotStrategyData> humanicStrategyPool;
    public List<BotStrategyData> demonicStrategyPool;

    [Header("Current Active Strategy")]
    public BotStrategyData activeStrategy; 

    [Header("Bot Personality")]
    public BotResponsiveness responsiveness = BotResponsiveness.Reactive;
    public string myTeamTag = "Enemy";

    [Header("🛠️ Developer & Tester Options")]
    public bool useDebugOverrides = false;
    public UnitRace debugRaceOverride = UnitRace.Humanic; 
    public BotStrategyData forceSpecificStrategy;

    private Queue<BuildStep> executionQueue = new Queue<BuildStep>();
    private bool isOpeningFinished = false;
    private bool hasLoadedOpening = false; 
    
    private TacticalState lastTacticalState = TacticalState.Defend;

    // ⚖️ 런타임 가중치 관리 리스트
    private List<BuildStep> runtimeMidGameBuildList = new List<BuildStep>();

    // 👷 [신규] 일꾼 관리 타이머
    private float workerManageTimer = 0f;
    private const float WORKER_MANAGE_INTERVAL = 1.0f; // 1초마다 체크

    [HideInInspector] public int currentWaveIndex = 0;
    [HideInInspector] public float gameTime = 0f;

    private static EnemyBot _instance;
    public static TacticalState enemyState => (_instance && _instance.tactics) ? _instance.tactics.currentState : TacticalState.Defend;
    public static Vector3 enemyFrontLinePos => (_instance && _instance.tactics) ? _instance.tactics.enemyFrontLinePos : Vector3.zero;
    
    public BotStrategyData Strategy => activeStrategy;
    public bool IsOpeningFinished => isOpeningFinished;

    void Awake()
    {
        _instance = this;
        production = GetComponent<EnemyProductionManager>();
        tactics = GetComponent<EnemyTacticsManager>();
        scout = GetComponent<EnemyScoutManager>();
    }

    void OnEnable()
    {
        // 📢 건설 완료 이벤트 구독
        BaseController.OnConstructionFinished += OnBaseBuiltHandler;
    }

    void OnDisable()
    {
        // 📢 이벤트 구독 해제 (메모리 누수 방지)
        BaseController.OnConstructionFinished -= OnBaseBuiltHandler;
    }

    // ⚡ 이벤트 핸들러: 기지가 다 지어지면 호출됨
    void OnBaseBuiltHandler(BaseController builtBase)
    {
        // 1. 내 팀(Enemy)의 건물이 아니면 무시
        if (!builtBase.CompareTag(myTeamTag)) return;

        Debug.Log($"🤖 [{myTeamTag}Bot] New Base Constructed: {builtBase.name}. Updating Frontline immediately!");

        // 2. 전술 관리자(Tactics)에게 전선 강제 갱신 및 병력 재배치 요청
        if (tactics != null)
        {
            tactics.ForceUpdateFrontline(); 
        }
    }

    void Start()
    {
        InitializeStrategy();

        production.Initialize(this);
        tactics.Initialize(this);
        scout.Initialize(this);

        FillProductionQueue();
    }

    void InitializeStrategy()
    {
        if (GameManager.I == null) return;

        UnitRace myRace = GameManager.I.enemyRace;

        if (useDebugOverrides)
        {
            myRace = debugRaceOverride;
            Debug.LogWarning($"🤖 [EnemyBot] Debug Mode Active! Forcing Race to: {myRace}");
        }

        List<BotStrategyData> targetPool = (myRace == UnitRace.Demonic) ? demonicStrategyPool : humanicStrategyPool;

        if (useDebugOverrides && forceSpecificStrategy != null)
        {
            activeStrategy = forceSpecificStrategy;
            Debug.LogWarning($"🤖 [EnemyBot] Strategy Forced: {activeStrategy.name}");
        }
        else
        {
            if (targetPool != null && targetPool.Count > 0)
            {
                int rnd = Random.Range(0, targetPool.Count);
                activeStrategy = targetPool[rnd];
                Debug.Log($"🤖 EnemyBot Selected Strategy: {activeStrategy.name} (Race: {myRace})");
            }
            else
            {
                Debug.LogError($"🚫 [{myRace}] 종족을 위한 전략이 Pool에 없습니다!");
            }
        }

        InitializeRuntimeBuildList();
    }

    void InitializeRuntimeBuildList()
    {
        runtimeMidGameBuildList.Clear();
        if (activeStrategy != null)
        {
            foreach (var step in activeStrategy.midGameComposition)
            {
                runtimeMidGameBuildList.Add(step);
            }
        }
    }

    void Update()
    {
        if (!GameManager.I.isGameStarted || GameManager.I.IsGameOver) return;
        
        gameTime += Time.deltaTime;

        scout.OnUpdate();
        tactics.OnUpdate();
        production.OnUpdate();

        CheckAttackWaves();
        FillProductionQueue();
        MonitorStrategyStatus();
        
        // 👷 [신규] 봇이 직접 일꾼 관리 (멍때리는 애들 재배치)
        ManageIdleWorkers();
    }

    // 👷 [신규] 일꾼 스마트 관리 로직
    private void ManageIdleWorkers()
    {
        workerManageTimer += Time.deltaTime;
        if (workerManageTimer < WORKER_MANAGE_INTERVAL) return;
        workerManageTimer = 0f;

        foreach (var unit in UnitController.activeUnits)
        {
            if (unit == null || unit.isDead || !unit.CompareTag(myTeamTag)) continue;
            if (unit.unitType != UnitType.Worker && unit.unitType != UnitType.Slave) continue;

            WorkerAbility worker = unit.GetComponent<WorkerAbility>();
            if (worker == null) continue;

            // [수정] Idle 상태뿐만 아니라, 할당된 기지가 없거나 기지 주변에 자원이 없는 경우 포함
            bool isIdle = worker.currentState == WorkerState.Idle;
            
            if (isIdle)
            {
                // 1. 가장 가까운 철(Iron) 광산이 있는 기지 찾기 (새 Outpost는 Iron 기반이므로)
                BaseController bestBase = BaseController.FindNearestBaseWithResource(ResourceType.Iron, myTeamTag, worker.transform.position);

                if (bestBase != null)
                {
                    // 2. 현재 내 기지가 아니거나, 기지에 할당되지 않았다면 이주 및 채굴
                    if (worker.assignedBase != bestBase)
                    {
                        Debug.Log($"🤖 [{myTeamTag}Bot] Worker assigned to new Iron Base: {bestBase.name}");
                        worker.TransferBase(bestBase);
                        worker.SetStateToMine(ResourceType.Iron);
                    }
                    else if (isIdle) // 이미 그 기지에 있는데 놀고 있다면 채굴 시작
                    {
                        worker.SetStateToMine(ResourceType.Iron);
                    }
                }
            }
        }
    }

    void MonitorStrategyStatus()
    {
        if (activeStrategy == null || activeStrategy.fallbackStrategy == null) return;

        bool shouldSwitch = false;
        string switchReason = "";

        if (activeStrategy.transitionTimeLimit > 0 && gameTime >= activeStrategy.transitionTimeLimit)
        {
            shouldSwitch = true;
            switchReason = "Time Limit Exceeded";
        }

        if (!shouldSwitch && activeStrategy.switchOnAttackFailure)
        {
            if (lastTacticalState == TacticalState.Attack && tactics.currentState == TacticalState.Defend)
            {
                shouldSwitch = true;
                switchReason = "Attack Failed (Retreat)";
            }
        }

        lastTacticalState = tactics.currentState;

        if (shouldSwitch)
        {
            SwitchStrategy(activeStrategy.fallbackStrategy, switchReason);
        }
    }

    void SwitchStrategy(BotStrategyData newStrategy, string reason)
    {
        if (newStrategy == null) return;
        activeStrategy = newStrategy;
        production.ClearQueue();
        executionQueue.Clear();
        isOpeningFinished = false;
        hasLoadedOpening = false;
        currentWaveIndex = 0; 
        InitializeRuntimeBuildList();
        FillProductionQueue();
    }

    void CheckAttackWaves()
    {
        if (activeStrategy == null) return;
        if (currentWaveIndex >= activeStrategy.attackWaves.Count) return;

        AttackWave nextWave = activeStrategy.attackWaves[currentWaveIndex];

        if (gameTime >= nextWave.timing)
        {
            if (tactics.TryTriggerWave(nextWave))
            {
                currentWaveIndex++;
                InitializeRuntimeBuildList(); 
            }
        }
    }

    void FillProductionQueue()
    {
        if (activeStrategy == null) return;

        // 1. 오프닝 빌드 (기존 유지)
        if (!isOpeningFinished) 
        {
            if (!hasLoadedOpening && activeStrategy.openingBuildOrder.Count > 0)
            {
                foreach (var step in activeStrategy.openingBuildOrder)
                {
                    if (step.stepType == BuildStepType.Unit && step.count > 1)
                        for (int i = 0; i < step.count; i++) executionQueue.Enqueue(step);
                    else
                        executionQueue.Enqueue(step);
                }
                hasLoadedOpening = true; 
            }

            if (executionQueue.Count > 0)
            {
                if (production.GetQueueCount() < 3) production.EnqueueStep(executionQueue.Dequeue());
            }
            else if (production.GetQueueCount() == 0)
            {
                isOpeningFinished = true;
                Debug.Log("🤖 EnemyBot: Opening Finished. Switching to Macro Mode.");
                InitializeRuntimeBuildList();
            }
        }
        else
        {
            // 2. 중반 운영 (스마트 확장 시스템 적용)
            if (production.GetQueueCount() < 2 && runtimeMidGameBuildList.Count > 0)
            {
                // ⛺ [스마트 확장 체크]
                int remainingIron = GetTotalRemainingIron();
                int currentIron = EnemyResourceManager.I != null ? EnemyResourceManager.I.currentIron : 0;
                
                UnitData outpostData = ConstructionManager.I.GetOutpostData(GameManager.I.enemyRace);
                int outpostCost = outpostData != null ? outpostData.ironCost : 300;

                bool isCriticalEconomicSituation = (currentIron + remainingIron) <= outpostCost;

                if (isCriticalEconomicSituation)
                {
                    if (ConstructionManager.I.HasFreeSpot())
                    {
                        Debug.Log("🤖 [EnemyBot] Critical Economy! Forcing Expansion (Last Stand).");
                        production.ClearQueue();
                        BuildStep expansionStep = new BuildStep { stepType = BuildStepType.Expansion, weight = 1000f };
                        production.EnqueueStep(expansionStep);
                    }
                    else
                    {
                        Debug.Log("🤖 [EnemyBot] No Land Left! All-In Attack Triggered!");
                        tactics.LaunchAllOutAttack(); 
                    }
                    return; 
                }

                List<BuildStep> candidates = new List<BuildStep>(runtimeMidGameBuildList);
                float expansionWeight = CalculateExpansionWeight(currentIron, remainingIron, outpostCost);
                candidates.Add(new BuildStep { stepType = BuildStepType.Expansion, weight = expansionWeight });

                BuildStep pickedStep = GetWeightedRandomStep(candidates);
                UpdateWeightsForNextWave(pickedStep);

                if (pickedStep.stepType == BuildStepType.Unit)
                    for (int i = 0; i < pickedStep.count; i++) production.EnqueueStep(pickedStep);
                else
                    production.EnqueueStep(pickedStep);
            }
        }
    }

    int GetTotalRemainingIron()
    {
        int total = 0;
        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl != null && baseCtrl.isConstructed && baseCtrl.CompareTag(myTeamTag))
            {
                total += baseCtrl.GetSurroundingResourceAmount(ResourceType.Iron);
            }
        }
        return total;
    }

    float CalculateExpansionWeight(int currentIron, int remainingIron, int outpostCost)
    {
        float weight = activeStrategy.expansionBaseWeight;
        float scarcity = Mathf.Max(0, (outpostCost * 3) - (currentIron + remainingIron)); 
        weight += scarcity * activeStrategy.expansionSensitivity;
        return Mathf.Max(1f, weight); 
    }

    void UpdateWeightsForNextWave(BuildStep pickedStep)
    {
        if (pickedStep.stepType == BuildStepType.Expansion) return; 

        if (currentWaveIndex >= activeStrategy.attackWaves.Count) return;
        AttackWave nextWave = activeStrategy.attackWaves[currentWaveIndex];
        
        bool isRequiredAndMissing = false;

        if (pickedStep.stepType == BuildStepType.Unit)
        {
            foreach (var req in nextWave.requiredUnits)
            {
                if (req.unitType == pickedStep.unitType)
                {
                    int currentCount = GetMyUnitCount(req.unitType);
                    if (currentCount <= req.count) isRequiredAndMissing = true;
                    break;
                }
            }
        }

        if (!isRequiredAndMissing)
        {
            HashSet<UnitType> missingTypes = new HashSet<UnitType>();
            foreach(var req in nextWave.requiredUnits)
            {
                int currentCount = GetMyUnitCount(req.unitType);
                if (currentCount < req.count) missingTypes.Add(req.unitType);
            }

            if (missingTypes.Count > 0)
            {
                for (int i = 0; i < runtimeMidGameBuildList.Count; i++)
                {
                    BuildStep step = runtimeMidGameBuildList[i];
                    if (step.stepType == BuildStepType.Unit && missingTypes.Contains(step.unitType))
                    {
                        step.weight *= 1.375f; 
                        runtimeMidGameBuildList[i] = step;
                    }
                }
            }
        }
    }

    int GetMyUnitCount(UnitType type)
    {
        int count = 0;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit != null && !unit.isDead && unit.CompareTag(myTeamTag) && unit.unitType == type)
                count++;
        }
        return count;
    }

    BuildStep GetWeightedRandomStep(List<BuildStep> options)
    {
        if (options == null || options.Count == 0) return default;
        float totalWeight = 0;
        foreach (var step in options) totalWeight += (step.weight <= 0) ? 1 : step.weight;
        float rnd = Random.Range(0, totalWeight);
        foreach (var step in options)
        {
            float w = (step.weight <= 0) ? 1 : step.weight;
            if (rnd < w) return step;
            rnd -= w;
        }
        return options[0]; 
    }
}