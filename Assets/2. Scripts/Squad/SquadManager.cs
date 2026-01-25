using UnityEngine;
using System.Collections.Generic;
using System.Linq; // 리스트 처리를 위해 필요

public class SquadManager : SingletonBehaviour<SquadManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("설정")]
    public float musterCheckInterval = 1.0f; // 1초마다 징집 시도
    
    [Header("데이터")]
    public List<Squad> activeSquads = new List<Squad>();
    
    [Header("UI 연결")]
    public Transform squadListContainer; // 분대 UI가 생길 부모
    public GameObject squadUIPrefab;     // 분대 하나를 표현할 프리팹
    public UnitSelectionPopup selectionPopup; // 팝업 UI

    private float timer = 0f;

    void Update()
    {
        // 주기적으로 놀고 있는 유닛 징집
        timer += Time.deltaTime;
        if (timer >= musterCheckInterval)
        {
            TryMusterUnits();
            timer = 0f;
        }

        // (테스트용) 우클릭 시 선택된 분대 이동 명령 로직은 
        // RTSControlManager 같은 별도 입력 처리기에서 SquadManager.I.CommandSelectedSquad(...) 호출 필요
    }

    // 🌟 분대 생성
    public void CreateNewSquad()
    {
        Squad newSquad = new Squad(activeSquads.Count);
        // 기본으로 3슬롯 정도 비워두거나, 0개로 시작해서 추가하게 할 수 있음.
        // 여기선 빈 상태로 시작.
        activeSquads.Add(newSquad);
        
        // UI 갱신
        RefreshSquadUI();
    }

    // 🌟 징집 로직 (Mustering)
    void TryMusterUnits()
    {
        // 맵의 모든 아군 유닛 가져오기
        List<UnitController> allUnits = FindObjectsByType<UnitController>(FindObjectsSortMode.None).ToList();

        foreach (var squad in activeSquads)
        {
            // 🌟 [핵심 수정] 편성 중(Drafting)인 분대는 징집하지 않음!
            if (squad.state == SquadState.Drafting) continue;

            foreach (var slot in squad.slots)
            {
                // 이미 채워진 슬롯은 패스
                if (slot.IsFilled) 
                {
                    // 혹시 유닛이 죽었으면 슬롯 비우기
                    if (slot.assignedUnit == null) 
                    {
                        // Debug.Log("분대원 전사! 재모집 필요.");
                    }
                    else
                    {
                        continue; 
                    }
                }

                // 빈 슬롯: 조건에 맞는 '무소속' 유닛 찾기
                UnitController recruit = FindBestRecruit(allUnits, slot.requiredType, squad);
                
                if (recruit != null)
                {
                    slot.assignedUnit = recruit;
                    recruit.assignedSquad = squad; // 유닛에게 소속 알려줌
                    
                    // 🌟 합류 명령: 분대가 이동 중이면 거기로, 아니면 집결지로
                    if (squad.currentCommandTarget.HasValue)
                    {
                        MoveUnitTo(recruit, squad.currentCommandTarget.Value);
                    }
                    else
                    {
                        Vector3 rallyPoint = GetSmartRallyPoint(squad);
                        MoveUnitTo(recruit, rallyPoint);
                    }
                }
            }
        }
    }

    UnitController FindBestRecruit(List<UnitController> candidates, UnitType type, Squad squad)
    {
        UnitController best = null;
        float minDst = Mathf.Infinity;
        Vector3 center = GetSquadCenter(squad); // 분대 중심점 기준

        foreach (var unit in candidates)
        {
            // 조건: 아군 + 타입 일치 + 소속 없음 + 노동병 아님
            if (!unit.CompareTag("Player")) continue;
            if (unit.unitType != type) continue;
            if (unit.assignedSquad != null) continue; // 이미 다른 분대 소속
            if (unit.unitType == UnitType.Worker) continue; // 노동병 제외

            float dst = Vector3.Distance(unit.transform.position, center);
            if (dst < minDst)
            {
                minDst = dst;
                best = unit;
            }
        }
        return best;
    }

    // 🧠 [Q1] 스마트 집결지 계산
    // 분대원들의 중간 지점에서 가장 가까운 '건물' or '건설터'
    public Vector3 GetSmartRallyPoint(Squad squad)
    {
        Vector3 center = GetSquadCenter(squad);

        // 1. 모든 거점(Base + Spot) 찾기
        List<Transform> points = new List<Transform>();
        
        var bases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        foreach(var b in bases) if(b.CompareTag("Player")) points.Add(b.transform);

        var spots = ConstructionManager.I.constructionSpots;
        foreach(var s in spots) if(s.isOccupied && s.currentBuilding != null && s.currentBuilding.CompareTag("Player")) points.Add(s.transform);
        
        // 거점이 하나도 없으면(망함) 그냥 본진 스폰 포인트
        if (points.Count == 0) return Vector3.zero; 

        // 2. 가장 가까운 거점 찾기
        Transform nearest = null;
        float minDst = Mathf.Infinity;
        foreach(var p in points)
        {
            float d = Vector3.Distance(center, p.position);
            if(d < minDst)
            {
                minDst = d;
                nearest = p;
            }
        }

        return nearest.position;
    }

    Vector3 GetSquadCenter(Squad squad)
    {
        if (squad.slots.Count == 0) return Vector3.zero; // 대충 0,0
        
        Vector3 sum = Vector3.zero;
        int count = 0;
        
        // 소속된 유닛들의 평균 위치
        foreach(var slot in squad.slots)
        {
            if(slot.IsFilled)
            {
                sum += slot.assignedUnit.transform.position;
                count++;
            }
        }

        // 아무도 없으면? 본진 위치 반환
        if (count == 0)
        {
            GameObject mainBase = GameObject.FindGameObjectWithTag("Player");
            return mainBase != null ? mainBase.transform.position : Vector3.zero;
        }

        return sum / count;
    }

    // 유닛 이동 명령 래퍼 (UnitController 기능에 따라 수정 필요)
    public void MoveUnitTo(UnitController unit, Vector3 target)
    {
        unit.isManualMove = true;
        // UnitAbility 등을 통해 이동 로직 실행...
        // 여기서는 임시로 직접 transform 이동 로직이 있다고 가정하거나, 
        // UnitController에 MoveTo 메서드를 만들어야 함.
        // 예: unit.GetComponent<UnitAbility>().MoveTo(target); 
    }
    
    // UI 갱신 (간략화)
    public void RefreshSquadUI()
    {
        // 기존 UI 삭제 후 재생성 방식 (최적화 여지 있음)
        foreach(Transform child in squadListContainer) Destroy(child.gameObject);
        
        foreach(var squad in activeSquads)
        {
            GameObject obj = Instantiate(squadUIPrefab, squadListContainer);
            obj.GetComponent<SquadUI>().Setup(squad);
        }
    }
}