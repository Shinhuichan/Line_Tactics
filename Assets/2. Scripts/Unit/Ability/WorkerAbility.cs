using UnityEngine;

public enum WorkerState
{
    Idle,           
    MovingToResource, 
    Mining,         
    ReturningToBase,
    Attack,
    Building,
    Repairing
}

public class WorkerAbility : UnitAbility
{
    [Header("노동자 상태")]
    public WorkerState currentState = WorkerState.Idle;
    public ResourceType targetResourceType = ResourceType.Iron;

    [Header("소속 관리")]
    public BaseController assignedBase; 

    [Header("채집 설정")]
    public float miningDuration = 0.5f; 
    public int ironMiningPower = 5; 
    public int oilMiningPower = 3; 

    [Header("능력치")]
    public int currentMaxCapacity; 
    public float interactionRange = 1.5f; 

    [Header("상태 정보")]
    public int currentLoad = 0; 
    private float miningTimer = 0f;
    private Transform targetNodeTransform; 
    private ResourceNode targetNodeScript; 

    [SerializeField]
    private ResourceType heldResourceType; 
    
    private ResourceType? pendingResourceType = null;

    public BaseController targetConstructionSite;

    // 수리 관련
    private BaseController targetRepairBase;
    private float repairTimer = 0f;
    private const float REPAIR_DURATION = 0.5f; 
    private const float REPAIR_AMOUNT = 50f;

    // 상태 복구용
    private WorkerState savedStateBeforeSiege;      
    private ResourceType savedResourceBeforeSiege;  
    private bool wasSiegeMode = false;
    private WorkerState lastState = WorkerState.Idle;

    // 🤖 [신규] 스마트 기능 활성화 여부 (PlayerBot 등에서 강제로 켜고 싶을 때 사용)
    [Header("AI 설정")]
    public bool isBotMode = false;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        currentMaxCapacity = 10; 

        // 태그가 Enemy면 자동으로 봇 모드 활성화 (스마트 이주 기능 사용)
        if (owner.CompareTag("Enemy"))
        {
            isBotMode = true;
        }

        FindAndJoinNearestBase();
    }

    void FindAndJoinNearestBase()
    {
        BaseController[] bases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        // 1. 가장 가까운 아군 기지 찾기 (태어난 곳)
        foreach(var b in bases)
        {
            if (!b.CompareTag(owner.tag)) continue;
            if (!b.isConstructed) continue; 

            float d = Vector3.Distance(transform.position, b.transform.position);
            if(d < minDst)
            {
                minDst = d;
                bestBase = b;
            }
        }

        // 2. 기지에 소속되고 명령 하달 받기
        if (bestBase != null)
        {
            JoinBase(bestBase);

            // 🌟 기지의 현재 태세에 따라 즉시 행동 개시
            switch (bestBase.currentTask)
            {
                case BaseTask.Iron:
                    SetStateToMine(ResourceType.Iron);
                    break;
                case BaseTask.Oil:
                    SetStateToMine(ResourceType.Oil);
                    break;
                case BaseTask.Idle:
                default:
                    SetStateToIdle();
                    break;
            }
        }
        else
        {
            assignedBase = null;
            SetStateToIdle();
        }
        
        if (WorkerDashboardManager.I != null) 
            WorkerDashboardManager.I.RebuildSlotList(); 
    }

    void JoinBase(BaseController baseCtrl)
    {
        assignedBase = baseCtrl;
        if (!baseCtrl.assignedWorkers.Contains(this))
        {
            baseCtrl.assignedWorkers.Add(this);
        }
    }

    void OnEnable()
    {
        if (UpgradeManager.I != null)
            UpgradeManager.I.OnUpgradeCompleted += OnWorkerUpgradeHandler;
    }

    void OnDisable()
    {
        if (UpgradeManager.I != null)
            UpgradeManager.I.OnUpgradeCompleted -= OnWorkerUpgradeHandler;
    }

    private void OnWorkerUpgradeHandler(string teamTag)
    {
        if (gameObject.CompareTag(teamTag)) RecalculateWorkerStats();
    }

    public override bool OnDie()
    {
        if (assignedBase != null)
        {
            if (assignedBase.assignedWorkers.Contains(this))
            {
                assignedBase.assignedWorkers.Remove(this);
            }
        }
        targetConstructionSite = null;
        currentState = WorkerState.Idle;
        return false; 
    }

    public void RecalculateWorkerStats()
    {
        if (UpgradeManager.I == null) return;

        int ironCap = 15;
        int oilCap = 5;
        string myTag = gameObject.tag;

        if (UpgradeManager.I.IsAbilityActive("MINING_2", myTag))
        {
            ironCap = 30;
            oilCap = 10;
        }
        else if (UpgradeManager.I.IsAbilityActive("MINING_1", myTag))
        {
            ironCap = 24;
            oilCap = 8;
        }

        UpdateCurrentCapacity(ironCap, oilCap);
    }

    void UpdateCurrentCapacity(int ironCap, int oilCap)
    {
        ResourceType typeToCheck = (currentLoad > 0) ? heldResourceType : targetResourceType;
        if (typeToCheck == ResourceType.Iron) currentMaxCapacity = ironCap;
        else currentMaxCapacity = oilCap;
    }

    public override void OnUpdate()
    {
        switch (currentState)
        {
            case WorkerState.Idle:
                break;
            case WorkerState.MovingToResource:
                ProcessMoveToResource();
                break;
            case WorkerState.Mining:
                ProcessMining();
                break;
            case WorkerState.ReturningToBase:
                ProcessReturnToBase(); 
                break;
            case WorkerState.Building:
                ProcessBuilding();
                break;
            case WorkerState.Repairing:
                ProcessRepairing();
                break;
            case WorkerState.Attack:
                break;
        }
    }

    public void SetStateToRepair(BaseController baseTarget)
    {
        if (baseTarget == null) return;

        targetConstructionSite = baseTarget; 
        currentState = WorkerState.Repairing;
        owner.isManualMove = true; 
    }

    void ProcessRepairing()
    {
        if (targetConstructionSite == null || targetConstructionSite.currentHP >= targetConstructionSite.maxHP)
        {
            targetConstructionSite = null;
            
            if (assignedBase != null)
            {
                BaseTask task = assignedBase.currentTask;
                if (task == BaseTask.Iron) SetStateToMine(ResourceType.Iron);
                else if (task == BaseTask.Oil) SetStateToMine(ResourceType.Oil);
                else SetStateToIdle();
            }
            else
            {
                SetStateToIdle();
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, targetConstructionSite.transform.position);
        
        if (dist > interactionRange)
        {
            MoveTowards(targetConstructionSite.transform.position);
        }
        else
        {
            float repairAmount = 100f * Time.deltaTime;
            targetConstructionSite.Repair(repairAmount);
        }
    }

    void ProcessMoveToResource()
    {
        if (targetNodeTransform == null)
        {
            if (assignedBase != null) FindResourceNearBase(assignedBase);
            else FindNearestResourceGlobal();

            if (targetNodeTransform == null)
            {
                // 🔄 [신규] 이동하려는데 자원이 없으면 여기서도 스마트 이주 체크 가능
                CheckSmartMigrationOrIdle(); 
                return;
            }
        }

        float distToEdge = GetDistanceToTargetEdge(targetNodeTransform);

        if (distToEdge <= interactionRange)
        {
            currentState = WorkerState.Mining;
            miningTimer = 0f;
        }
        else
        {
            MoveTowards(targetNodeTransform.position);
        }
    }

    void FindNearestResourceGlobal()
    {
        ResourceNode[] allNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        float closestDist = Mathf.Infinity;
        ResourceNode bestNode = null;

        foreach (var node in allNodes)
        {
            if (node.resourceType == targetResourceType)
            {
                float d = Vector3.Distance(transform.position, node.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    bestNode = node;
                }
            }
        }
        SetTargetNode(bestNode);
    }

    void FindResourceNearBase(BaseController baseCtrl)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(baseCtrl.transform.position, baseCtrl.resourceScanRange);
        float closestDist = Mathf.Infinity;
        ResourceNode bestNode = null;

        foreach (var hit in hits)
        {
            ResourceNode node = hit.GetComponent<ResourceNode>();
            if (node != null && node.resourceType == targetResourceType)
            {
                float d = Vector3.Distance(transform.position, node.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    bestNode = node;
                }
            }
        }
        SetTargetNode(bestNode);
    }

    void SetTargetNode(ResourceNode node)
    {
        if (node != null)
        {
            targetNodeTransform = node.transform;
            targetNodeScript = node;
        }
        else
        {
            targetNodeTransform = null;
            targetNodeScript = null;
        }
    }

    void ProcessReturnToBase()
    {
        BaseController targetBase = (assignedBase != null && assignedBase.isConstructed) ? assignedBase : FindNearestBase();

        if (targetBase == null) return;

        float dist = GetDistanceToTargetEdge(targetBase.transform);
        if (dist > interactionRange)
        {
            MoveTowards(targetBase.transform.position);
        }
        else
        {
            DepositResource();
        }
    }

    BaseController FindNearestBase()
    {
        BaseController[] bases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var b in bases)
        {
            if (!b.CompareTag(owner.gameObject.tag)) continue;
            if (!b.isConstructed) continue; 

            float dst = GetDistanceToTargetEdge(b.transform);
            if (dst < minDst)
            {
                minDst = dst;
                bestBase = b;
            }
        }
        return bestBase;
    }

    float GetDistanceToTargetEdge(Transform target)
    {
        if (target == null) return Mathf.Infinity;
        Collider2D targetCol = target.GetComponent<Collider2D>();
        if (targetCol != null)
        {
            Vector3 closestPoint = targetCol.ClosestPoint(transform.position);
            return Vector3.Distance(transform.position, closestPoint);
        }
        return Vector3.Distance(transform.position, target.position);
    }

    void ProcessMining()
    {
        // 자원이 고갈되거나 사라진 경우
        if (targetNodeScript == null || targetNodeScript.currentAmount <= 0) 
        {
            if (currentLoad > 0) 
            {
                currentState = WorkerState.ReturningToBase;
            }
            else 
            {
                // 🔄 [신규] 자원 고갈 시 스마트 이주 로직 호출
                AttemptFindNewResourceOrMigrate();
            }
            return;
        }

        if (currentLoad >= currentMaxCapacity)
        {
            currentState = WorkerState.ReturningToBase;
            return;
        }

        miningTimer += Time.deltaTime;
        if (miningTimer >= miningDuration)
        {
            miningTimer = 0f;
            int power = (targetResourceType == ResourceType.Iron) ? ironMiningPower : oilMiningPower;
            int spaceLeft = currentMaxCapacity - currentLoad;
            int amountToMine = Mathf.Min(power, spaceLeft);
            int harvested = targetNodeScript.Harvest(amountToMine);
            
            if (harvested > 0)
            {
                currentLoad += harvested;
                heldResourceType = targetResourceType;
                
                if (currentLoad >= currentMaxCapacity) currentState = WorkerState.ReturningToBase;
            }
            else
            {
                // 캤는데 0이 나오면 고갈된 것
                if (currentLoad > 0) 
                {
                    currentState = WorkerState.ReturningToBase;
                }
                else 
                {
                    // 🔄 [신규] 자원 고갈 시 스마트 이주 로직 호출
                    AttemptFindNewResourceOrMigrate();
                }
            }
        }
    }

    // 🔄 [신규] 자원 고갈 시: 주변 탐색 -> 실패 시 스마트 이주(Bot 전용) -> 실패 시 Idle
    void AttemptFindNewResourceOrMigrate()
    {
        // 1. 현재 기지 주변에 같은 자원이 더 있는지 확인
        if (assignedBase != null)
        {
            FindResourceNearBase(assignedBase);
            if (targetNodeTransform != null)
            {
                // 주변에 자원이 있으면 계속 캔다
                currentState = WorkerState.MovingToResource;
                return;
            }
        }

        // 2. 주변에 없다면 스마트 이주 시도 (Bot Only)
        CheckSmartMigrationOrIdle();
    }

    // 🔄 [신규] 스마트 이주 핵심 로직
    void CheckSmartMigrationOrIdle()
    {
        // 봇 모드(EnemyTag 등)일 때만 작동. 플레이어의 수동 조작 유닛은 건드리지 않음.
        if (isBotMode)
        {
            // 원하는 자원을 가진 다른 아군 기지를 검색
            BaseController newHome = BaseController.FindBaseWithResource(targetResourceType, owner.tag);

            if (newHome != null && newHome != assignedBase)
            {
                // 🌟 Q3: 소속을 바꾸면 자동으로 캐러 가도록 설정
                Debug.Log($"🤖 [SmartBot] Worker {name} migrated from {(assignedBase?assignedBase.name:"null")} to {newHome.name} for {targetResourceType}");
                TransferBase(newHome);
                SetStateToMine(targetResourceType);
                return;
            }
        }

        // 갈 곳도 없으면 Idle
        SetStateToIdle();
    }

    void MoveTowards(Vector3 targetPos)
    {
        if (owner != null)
        {
            owner.MoveToPosition(targetPos);
        }
    }

    void DepositResource()
    {
        if (owner.CompareTag("Player"))
        {
            if (ResourceManager.I != null) 
            {
                if(heldResourceType == ResourceType.Iron) ResourceManager.I.AddResource(currentLoad, 0);
                else ResourceManager.I.AddResource(0, currentLoad);
            }
        }
        else if (owner.CompareTag("Enemy") && EnemyResourceManager.I != null)
        {
             if(heldResourceType == ResourceType.Iron) EnemyResourceManager.I.AddResource(currentLoad, 0);
             else EnemyResourceManager.I.AddResource(0, currentLoad);
        }
        
        ShowDepositText();
        
        currentLoad = 0; 

        if (pendingResourceType.HasValue)
        {
            ResourceType next = pendingResourceType.Value;
            pendingResourceType = null; 
            SetStateToMine(next);       
            return;
        }
        
        if (assignedBase == null)
        {
            SetStateToIdle();
            return;
        }

        BaseTask baseOrder = assignedBase.currentTask;
        if (baseOrder == BaseTask.Idle)
        {
            SetStateToIdle(); 
            return;
        }

        ResourceType nextType = (baseOrder == BaseTask.Iron) ? ResourceType.Iron : ResourceType.Oil;
        SetStateToMine(nextType);
    }

    void ShowDepositText()
    {
        if (FloatingTextManager.I != null)
             FloatingTextManager.I.ShowText(transform.position, $"+{currentLoad}", Color.cyan, 20);
    }

    public void SetStateToMine(ResourceType type)
    {
        if (currentState == WorkerState.Building) return;

        owner.isManualMove = true;

        if (currentLoad > 0)
        {
            if (heldResourceType != type)
            {
                pendingResourceType = type; 
                currentState = WorkerState.ReturningToBase;
                return;
            }
            currentState = WorkerState.ReturningToBase;
            targetResourceType = type; 
            return;
        }

        targetResourceType = type;
        pendingResourceType = null;

        if (assignedBase == null) { SetStateToIdle(); return; }

        ResourceNode node = assignedBase.GetAvailableResource(type);

        if (node != null)
        {
            targetNodeTransform = node.transform;
            targetNodeScript = node;
            currentState = WorkerState.MovingToResource;
            RecalculateWorkerStats();
        }
        else
        {
            // 자원이 없으면 바로 멍때리지 말고 스마트 이주 체크
            AttemptFindNewResourceOrMigrate();
        }
    }

    public void SetStateToAttack()
    {
        if (currentState == WorkerState.Building) return;

        owner.isManualMove = false; 
        currentState = WorkerState.Attack;
        pendingResourceType = null; 
    }

    public void SetStateToBuild(BaseController site)
    {
        targetConstructionSite = site;
        currentState = WorkerState.Building;
        owner.isManualMove = true; 
    }

    // 🏗️ [수정] 건설 완료 시, 해당 기지로 소속을 옮기고 기지의 태세에 따라 채집 시작
    void ProcessBuilding()
    {
        if (targetConstructionSite == null)
        {
            currentState = WorkerState.Idle;
            return;
        }

        if (targetConstructionSite.isConstructed)
        {
            // 1. 소속 변경 (건설한 그 집이 이제 내 집이다)
            TransferBase(targetConstructionSite);
            BaseController constructedBase = targetConstructionSite; // 참조 저장

            // 2. 건설 타겟 초기화
            targetConstructionSite = null;

            // 🌟 Q2: 이미 지어진 곳으로 배치된다면 기지의 태세(currentTask)를 따른다.
            // BaseController의 기본 태세는 Iron이므로, 막 지어진 기지는 Iron을 캐러 감.
            ResourceType nextTask = ResourceType.Iron; 

            if (constructedBase.currentTask == BaseTask.Oil)
            {
                nextTask = ResourceType.Oil;
            }
            // (만약 Idle 상태라면 Iron을 기본으로 하거나 Idle로 가야하지만, 보통 확장은 자원때문이므로 Iron 기본값 유지)
            
            SetStateToMine(nextTask); 
            return;
        }

        float dist = Vector3.Distance(transform.position, targetConstructionSite.transform.position);
        
        if (dist > interactionRange)
        {
            MoveTowards(targetConstructionSite.transform.position);
        }
        else
        {
            targetConstructionSite.Construct(Time.deltaTime);
        }
    }

    public void TransferBase(BaseController newBase)
    {
        if (newBase == null) return;

        // 기존 기지 명부에서 제거
        if (assignedBase != null && assignedBase.assignedWorkers.Contains(this))
        {
            assignedBase.assignedWorkers.Remove(this);
        }

        // 새 기지로 등록
        assignedBase = newBase;
        if (!assignedBase.assignedWorkers.Contains(this))
        {
            assignedBase.assignedWorkers.Add(this);
        }

        // Debug.Log($"👷 Worker {name} transferred to {newBase.name}");
    }

    public void SetStateToIdle()
    {
        currentState = WorkerState.Idle;
        owner.isManualMove = false;
    }
}