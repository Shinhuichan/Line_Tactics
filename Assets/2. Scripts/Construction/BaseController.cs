using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System; // Action 사용을 위해 추가

public enum BaseTask
{
    Iron, // 0 (Default)
    Oil,  // 1
    Idle  // 2
}

public class BaseController : MonoBehaviour
{
    // 📋 맵에 존재하는 모든 기지를 관리하는 정적 리스트
    public static List<BaseController> activeBases = new List<BaseController>();

    // 📢 [신규] 건설 완료 이벤트 (PlayerProductionManager 등에서 구독)
    public static event Action<BaseController> OnConstructionFinished;

    [Header("기지 종족 설정")]
    public UnitRace buildingRace = UnitRace.Humanic;

    [Header("기지 설정")]
    public float maxHP = 10000f; 
    public float currentHP;

    [Header("UI Info (Mouse Hover)")]
    public string baseName = "Base"; 
    public Sprite icon;              
    
    [Header("명령 상태")]
    public BaseTask currentTask = BaseTask.Iron; 

    [Header("방어 설정")]
    public float detectRange = 15.0f; 

    [Header("건설 설정 (Outpost 전용)")]
    public bool isOutpost = false;      
    public bool isConstructed = true;   
    public float constructionTime = 10f; 
    
    [Header("건설 상태 (Read Only)")]
    public float currentProgress = 0f;  

    [Header("건설 구역 참조")]
    public ConstructionSpot linkedSpot; 

    [Header("소속 노동자")]
    public List<WorkerAbility> assignedWorkers = new List<WorkerAbility>();
    public float resourceScanRange = 10.0f;

    [Header("농성/방어 병력 (Garrison)")]
    public List<UnitController> garrisonedUnits = new List<UnitController>();

    [Header("UI 연결")]
    public Slider hpSlider;
    public Slider constructionSlider; 
    public Image hpFillImage; 
    public Color colorHigh = Color.green;
    public Color colorMedium = Color.yellow;
    public Color colorLow = new Color(1f, 0.5f, 0f); 
    public Color colorCritical = Color.red;

    public Transform hitPoint;

    private float raceTraitTimer = 0f;
    private const float DEMONIC_REGEN_INTERVAL = 5.0f;
    private const float DEMONIC_REGEN_AMOUNT = 5.0f;

    public bool IsBeingRepaired
    {
        get
        {
            foreach (var w in assignedWorkers)
            {
                if (w.currentState == WorkerState.Repairing) return true;
            }
            return false;
        }
    }

    public bool HasIronNear => GetAvailableResource(ResourceType.Iron) != null;
    public bool HasOilNear => GetAvailableResource(ResourceType.Oil) != null;

    void Awake()
    {
        activeBases.Add(this);
        currentHP = isConstructed ? maxHP : 100f; 
    }

    void OnDestroy()
    {
        activeBases.Remove(this);
        if (linkedSpot != null)
        {
            linkedSpot.FreeSpot();
        }
    }

    public static BaseController FindBaseWithResource(ResourceType type, string teamTag)
    {
        foreach (var baseCtrl in activeBases)
        {
            if (baseCtrl == null) continue;
            if (!baseCtrl.isConstructed) continue;
            if (!baseCtrl.CompareTag(teamTag)) continue;

            if (baseCtrl.GetNearestResourceNode(type) != null)
            {
                return baseCtrl;
            }
        }
        return null; 
    }

    public static BaseController FindNearestBaseWithResource(ResourceType type, string teamTag, Vector3 fromPos)
    {
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var baseCtrl in activeBases)
        {
            if (baseCtrl == null) continue;
            if (!baseCtrl.isConstructed) continue;
            if (!baseCtrl.CompareTag(teamTag)) continue;

            if (baseCtrl.GetNearestResourceNode(type) != null)
            {
                float dst = Vector3.Distance(fromPos, baseCtrl.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    bestBase = baseCtrl;
                }
            }
        }
        return bestBase;
    }

    public ResourceNode GetNearestResourceNode(ResourceType type)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, resourceScanRange);
        float minDst = Mathf.Infinity;
        ResourceNode bestNode = null;

        foreach (var hit in hits)
        {
            ResourceNode node = hit.GetComponent<ResourceNode>();
            if (node != null && node.resourceType == type && node.currentAmount > 0)
            {
                float d = Vector3.Distance(transform.position, node.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    bestNode = node;
                }
            }
        }
        return bestNode;
    }

    public int GetSurroundingResourceAmount(ResourceType type)
    {
        int totalAmount = 0;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, resourceScanRange);
        
        foreach (var hit in hits)
        {
            ResourceNode node = hit.GetComponent<ResourceNode>();
            if (node != null && node.resourceType == type)
            {
                totalAmount += node.currentAmount;
            }
        }
        return totalAmount;
    }

    public void Initialize(UnitData data, string teamTag)
    {
        if (data == null) return;

        this.baseName = data.unitName;
        this.maxHP = data.hp;
        this.constructionTime = data.constructionTime; 
        this.buildingRace = data.race;
        this.icon = data.icon; 

        if (data.worldSprite != null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = data.worldSprite;
            }
        }

        this.gameObject.tag = teamTag;

        if (isConstructed)
        {
            currentHP = maxHP; 
            currentProgress = 1f;
            if (hpSlider != null) hpSlider.maxValue = maxHP;
            UpdateUI();
        }
    }

    void Start()
    {
        currentTask = BaseTask.Iron;

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = isOutpost ? "Outpost" : "Command Center";
        }

        if (isConstructed)
        {
            currentHP = maxHP;
            currentProgress = 1f;
            if (constructionSlider != null) constructionSlider.gameObject.SetActive(false);
        }
        else
        {
            currentHP = maxHP * 0.1f; 
            currentProgress = 0f;
            if (constructionSlider != null) 
            {
                constructionSlider.gameObject.SetActive(true);
                constructionSlider.value = 0f;
            }
        }
        
        if (hpSlider != null) hpSlider.maxValue = maxHP;
        UpdateUI();
    }

    void Update()
    {
        if (isConstructed)
        {
            HandleRaceTraits();
        }
    }

    void HandleRaceTraits()
    {
        if (buildingRace == UnitRace.Demonic)
        {
            if (currentHP < maxHP)
            {
                raceTraitTimer += Time.deltaTime;
                if (raceTraitTimer >= DEMONIC_REGEN_INTERVAL)
                {
                    raceTraitTimer = 0f;
                    Repair(DEMONIC_REGEN_AMOUNT);
                }
            }
            else
            {
                raceTraitTimer = 0f;
            }
        }
    }

    public void GarrisonUnit(UnitController unit)
    {
        if (!garrisonedUnits.Contains(unit))
        {
            garrisonedUnits.Add(unit);
            unit.gameObject.SetActive(false);
        }
    }

    public void ReleaseAllGarrisoned()
    {
        for (int i = garrisonedUnits.Count - 1; i >= 0; i--)
        {
            UnitController unit = garrisonedUnits[i];
            if (unit != null)
            {
                unit.gameObject.SetActive(true);
                unit.transform.position = transform.position + (Vector3)UnityEngine.Random.insideUnitCircle * 3.0f;
            }
        }
        garrisonedUnits.Clear();
    }

    public void Construct(float workAmount)
    {
        if (isConstructed) return;

        float progressIncrease = workAmount / constructionTime;
        currentProgress += progressIncrease;

        float hpIncrease = maxHP * progressIncrease;
        currentHP += hpIncrease;
        if (currentHP > maxHP) currentHP = maxHP;

        if (currentProgress >= 1.0f)
        {
            currentProgress = 1.0f;
            isConstructed = true;
            OnConstructionComplete();
        }

        UpdateUI();
    }

    void OnConstructionComplete()
    {
        if (constructionSlider != null) constructionSlider.gameObject.SetActive(false);
        if (currentTask == BaseTask.Idle) currentTask = BaseTask.Iron;

        Debug.Log($"{gameObject.name} 건설 완료! 현재 명령: {currentTask}");

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "건설 완료!", Color.cyan, 30);

        // 🌟 [핵심] 건설 완료 이벤트 발생 (PlayerProductionManager가 듣고 있음)
        OnConstructionFinished?.Invoke(this);
    }

    public void Repair(float amount)
    {
        if (currentHP >= maxHP) return;

        currentHP += amount;
        if (currentHP > maxHP) currentHP = maxHP;

        UpdateUI();

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, $"+{Mathf.RoundToInt(amount)}", Color.green, 25);
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        ShowDamageText(damage);
        UpdateUI();

        if (currentHP <= 0)
        {
            DestroyBase();
        }
    }

    void DestroyBase()
    {
        for (int i = assignedWorkers.Count - 1; i >= 0; i--)
        {
            if (assignedWorkers[i] != null)
            {
                assignedWorkers[i].assignedBase = null;
                assignedWorkers[i].SetStateToIdle();
            }
        }
        assignedWorkers.Clear();

        ReleaseAllGarrisoned(); 

        Debug.Log($"{gameObject.name} 파괴됨!");
        Destroy(gameObject);
    }

    void UpdateUI()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP; 
            hpSlider.value = currentHP;
        }

        if (constructionSlider != null && !isConstructed)
        {
            constructionSlider.value = currentProgress;
        }
        UpdateHealthColor();
    }

    void UpdateHealthColor()
    {
        if (hpFillImage == null) return;
        float ratio = currentHP / maxHP;

        if (ratio > 0.75f) hpFillImage.color = colorHigh;
        else if (ratio > 0.5f) hpFillImage.color = colorMedium;
        else if (ratio > 0.25f) hpFillImage.color = colorLow;
        else hpFillImage.color = colorCritical;
    }

    void ShowDamageText(float damage)
    {
        if (FloatingTextManager.I == null) return;
        Vector3 spawnPos = hitPoint != null ? hitPoint.position : transform.position + Vector3.up * 1.5f;
        FloatingTextManager.I.ShowText(spawnPos, $"-{Mathf.RoundToInt(damage)}", Color.red, 20);
    }

    public ResourceNode GetAvailableResource(ResourceType type)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, resourceScanRange);
        float minDst = Mathf.Infinity;
        ResourceNode bestNode = null;

        foreach (var hit in hits)
        {
            ResourceNode node = hit.GetComponent<ResourceNode>();
            if (node != null && node.resourceType == type && node.currentAmount > 0)
            {
                float d = Vector3.Distance(transform.position, node.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    bestNode = node;
                }
            }
        }
        return bestNode;
    }

    private void OnMouseEnter()
    {
        if (UnitInfoPanel.I != null && icon != null)
        {
            UnitInfoPanel.I.ShowBaseInfo(this);
        }
    }

    private void OnMouseExit()
    {
        if (UnitInfoPanel.I != null)
        {
            UnitInfoPanel.I.HideInfo();
        }
    }
}