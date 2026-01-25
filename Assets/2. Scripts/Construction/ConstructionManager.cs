using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro; 

public class ConstructionManager : SingletonBehaviour<ConstructionManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("설정")]
    public GameObject outpostPrefab;    

    [Header("본진(Main Base) 데이터")]
    public UnitData humanicMainBaseData; 
    public UnitData demonicMainBaseData;   

    [Header("확장 기지(Outpost) 데이터")]
    public UnitData humanicOutpostData; 
    public UnitData demonicOutpostData; 

    [Header("UI 연결")]
    public TextMeshProUGUI buildButtonInfoText;

    [Header("건설 구역")]
    public List<ConstructionSpot> constructionSpots; 
    public List<Transform> tacticalPoints = new List<Transform>();

    protected override void Awake()
    {
        base.Awake();
        InitializeTacticalMap();
    }

    void InitializeTacticalMap()
    {
        tacticalPoints.Clear();
        GameObject playerBase = GameObject.FindGameObjectWithTag("Player");
        if (playerBase != null) tacticalPoints.Add(playerBase.transform);

        if (playerBase != null)
        {
            constructionSpots = constructionSpots
                .OrderBy(spot => Vector3.Distance(playerBase.transform.position, spot.transform.position))
                .ToList();
        }

        foreach (var spot in constructionSpots) tacticalPoints.Add(spot.transform);

        GameObject enemyBase = GameObject.FindGameObjectWithTag("Enemy");
        if (enemyBase != null) tacticalPoints.Add(enemyBase.transform);
    }

    public UnitData GetOutpostData(UnitRace race)
    {
        return (race == UnitRace.Demonic) ? demonicOutpostData : humanicOutpostData;
    }

    // 🌟 [신규] 맵에 남은 빈 부지가 있는지 확인
    public bool HasFreeSpot()
    {
        if (constructionSpots == null) return false;
        return constructionSpots.Any(spot => !spot.isOccupied);
    }

    public void UpdateBuildButtonUI()
    {
        if (buildButtonInfoText == null || GameManager.I == null) return;

        UnitRace race = GameManager.I.playerRace;
        UnitData data = GetOutpostData(race);

        if (data != null)
        {
            buildButtonInfoText.text = $"{data.unitName}\n<size=70%>Fe:{data.ironCost}</size>";
        }
    }

    public void InitializeStartingBases(UnitRace playerRace, UnitRace enemyRace)
    {
        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl == null) continue;

            if (!baseCtrl.isOutpost)
            {
                if (baseCtrl.CompareTag("Player"))
                {
                    ApplyBaseData(baseCtrl, playerRace, "Player");
                }
                else if (baseCtrl.CompareTag("Enemy"))
                {
                    ApplyBaseData(baseCtrl, enemyRace, "Enemy");
                }
            }
        }
    }

    private void ApplyBaseData(BaseController baseCtrl, UnitRace race, string teamTag)
    {
        UnitData targetData = (race == UnitRace.Demonic) ? demonicMainBaseData : humanicMainBaseData;
        
        if (targetData != null)
        {
            baseCtrl.Initialize(targetData, teamTag);
            Debug.Log($"🏰 [{teamTag}] 기지 초기화 완료: {targetData.unitName} (종족: {race})");
        }
        else
        {
            Debug.LogWarning($"⚠️ [{teamTag}] 초기화 실패: {race}용 MainBaseData가 ConstructionManager에 연결되지 않음!");
        }
    }

    public void OnBuildOutpostClick() 
    { 
        if (TryResumeAbandonedConstruction("Player")) return;
        TryBuildOutpost("Player", Vector3.zero, false); 
    }

    bool TryResumeAbandonedConstruction(string teamTag)
    {
        foreach (var spot in constructionSpots)
        {
            if (spot.isOccupied && spot.currentBuilding != null && 
                !spot.currentBuilding.isConstructed && spot.currentBuilding.CompareTag(teamTag))
            {
                BaseController targetBase = spot.currentBuilding;
                if (IsAnyoneBuildingThis(targetBase)) continue;

                DispatchWorker(targetBase, teamTag);
                
                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(targetBase.transform.position, "Resume Build!", Color.cyan, 30);
                
                return true; 
            }
        }
        return false; 
    }

    bool IsAnyoneBuildingThis(BaseController target)
    {
        WorkerAbility[] workers = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        foreach (var w in workers)
        {
            if (w.currentState == WorkerState.Building && w.targetConstructionSite == target)
            {
                return true;
            }
        }
        return false;
    }

    public bool TryBuildEnemyOutpost(ExpansionPolicy policy)
    {
        Vector3 searchOrigin = Vector3.zero;
        GameObject enemyBase = GameObject.FindGameObjectWithTag("Enemy");
        
        if (enemyBase == null) return false;

        switch (policy)
        {
            case ExpansionPolicy.ForwardBase:
                searchOrigin = Vector3.zero; 
                break;
            case ExpansionPolicy.SafeExpand:
                searchOrigin = enemyBase.transform.position;
                break;
            default:
                searchOrigin = enemyBase.transform.position;
                break;
        }

        return TryBuildOutpost("Enemy", searchOrigin, true);
    }

    public bool TryBuildPlayerOutpost(ExpansionPolicy policy)
    {
        Vector3 searchOrigin = Vector3.zero;
        GameObject playerBase = GameObject.FindGameObjectWithTag("Player");
        
        if (playerBase == null) return false;

        switch (policy)
        {
            case ExpansionPolicy.ForwardBase:
                searchOrigin = Vector3.zero; 
                break;
            case ExpansionPolicy.SafeExpand:
                searchOrigin = playerBase.transform.position;
                break;
            default:
                searchOrigin = playerBase.transform.position;
                break;
        }

        return TryBuildOutpost("Player", searchOrigin, true);
    }

    private bool TryBuildOutpost(string teamTag, Vector3 searchOrigin, bool useSearchOrigin)
    {
        if (GameManager.I == null) return false;

        GameObject teamBase = GameObject.FindGameObjectWithTag(teamTag);
        if (teamBase == null) return false;

        UnitRace race = (teamTag == "Player") ? GameManager.I.playerRace : GameManager.I.enemyRace;
        UnitData data = GetOutpostData(race);

        if (data == null)
        {
            Debug.LogError($"[ConstructionManager] {race} Outpost Data missing!");
            return false;
        }

        Vector3 origin = useSearchOrigin ? searchOrigin : teamBase.transform.position;
        ConstructionSpot bestSpot = GetNearestFreeSpot(origin);

        if (bestSpot == null) return false;

        bool canAfford = false;
        
        if (teamTag == "Player")
        {
            if (ResourceManager.I.CheckCost(data.ironCost, data.oilCost))
            {
                ResourceManager.I.SpendResource(data.ironCost, data.oilCost);
                canAfford = true;
            }
        }
        else 
        {
            if (EnemyResourceManager.I != null && EnemyResourceManager.I.CheckCost(data.ironCost, data.oilCost))
            {
                EnemyResourceManager.I.SpendResource(data.ironCost, data.oilCost);
                canAfford = true;
            }
        }

        if (!canAfford) return false;

        StartConstruction(bestSpot, teamTag, data); 
        return true;
    }

    void StartConstruction(ConstructionSpot spot, string teamTag, UnitData data)
    {
        Quaternion rot = (teamTag == "Player") ? Quaternion.identity : Quaternion.Euler(0, 0, 180);
        GameObject newObj = Instantiate(outpostPrefab, spot.transform.position, rot);
        
        BaseController newBase = newObj.GetComponent<BaseController>();
        newObj.tag = teamTag;
        
        if (newBase != null && data != null)
        {
            newBase.Initialize(data, teamTag);
        }
        
        spot.OccupySpot(newBase);
        newBase.linkedSpot = spot;

        DispatchWorker(newBase, teamTag);
    }

    ConstructionSpot GetNearestFreeSpot(Vector3 fromPos)
    {
        ConstructionSpot best = null;
        float minDst = Mathf.Infinity;

        foreach (var spot in constructionSpots)
        {
            if (spot.isOccupied) continue;

            float dst = Vector3.Distance(fromPos, spot.transform.position);
            if (dst < minDst)
            {
                minDst = dst;
                best = spot;
            }
        }
        return best;
    }

    void DispatchWorker(BaseController targetBuilding, string teamTag)
    {
        WorkerAbility[] workers = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        WorkerAbility bestWorker = null;
        float minDst = Mathf.Infinity;
        Vector3 targetPos = targetBuilding.transform.position;

        foreach (var w in workers)
        {
            if (!w.CompareTag(teamTag)) continue;
            if (w.currentState == WorkerState.Building) continue;

            float dst = Vector3.Distance(w.transform.position, targetPos);
            if (dst < minDst)
            {
                minDst = dst;
                bestWorker = w;
            }
        }

        if (bestWorker != null)
        {
            bestWorker.SetStateToBuild(targetBuilding);
        }
    }
}