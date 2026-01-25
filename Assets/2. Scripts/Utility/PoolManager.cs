using System.Collections.Generic;
using UnityEngine;

public class PoolManager : SingletonBehaviour<PoolManager>
{
    protected override bool IsDontDestroy() => false; 

    [System.Serializable]
    public struct PoolInfo
    {
        public UnitType type;       
        public GameObject prefab;   
        public int count;           
    }

    [Header("풀링 설정")]
    public List<PoolInfo> poolSetupList;

    // 실제 풀 저장소
    private Dictionary<UnitType, Queue<GameObject>> _poolDict = new Dictionary<UnitType, Queue<GameObject>>();
    
    // 부모 트랜스폼 캐싱 (생성 시 부모 찾기용)
    private Dictionary<UnitType, Transform> _poolParents = new Dictionary<UnitType, Transform>();

    protected override void Awake()
    {
        base.Awake();
        InitializePools();
    }

    void InitializePools()
    {
        _poolDict.Clear();
        _poolParents.Clear();

        foreach (var info in poolSetupList)
        {
            if (info.prefab == null) continue;

            if (!_poolDict.ContainsKey(info.type))
            {
                _poolDict[info.type] = new Queue<GameObject>();
            }

            // 부모 오브젝트 생성 및 캐싱
            GameObject groupObj = new GameObject($"Pool_{info.type}");
            groupObj.transform.SetParent(transform);
            _poolParents[info.type] = groupObj.transform;

            // 미리 생성
            for (int i = 0; i < info.count; i++)
            {
                CreateNewObject(info.type, info.prefab, groupObj.transform);
            }
        }
    }

    private GameObject CreateNewObject(UnitType type, GameObject prefab, Transform parent)
    {
        GameObject obj = Instantiate(prefab, parent);
        obj.SetActive(false);
        _poolDict[type].Enqueue(obj);
        return obj;
    }

    public GameObject Get(UnitType type)
    {
        if (!_poolDict.ContainsKey(type))
        {
            Debug.LogError($"[Pool] {type} 타입의 풀이 없습니다! Inspector 설정을 확인하세요.");
            return null;
        }

        Queue<GameObject> queue = _poolDict[type];

        // 🌟 [수정] 큐가 비었으면 -> 자동으로 확장(Expand)
        if (queue.Count == 0)
        {
            ExpandPool(type); // 하나 더 생성 시도
            
            // 그래도 비어있다면 진짜 오류
            if (queue.Count == 0)
            {
                Debug.LogError($"[Pool] {type} 풀 확장 실패! (프리팹 정보를 찾을 수 없음)");
                return null;
            }
        }

        GameObject obj = queue.Dequeue();
        
        // 방어 코드: 꺼낸 오브젝트가 혹시 삭제되었다면 다시 재귀 호출
        if (obj == null) return Get(type); 

        obj.SetActive(true);
        return obj;
    }

    // 🌟 [신규] 풀 확장 함수
    void ExpandPool(UnitType type)
    {
        // 1. 해당 타입의 프리팹 정보 찾기
        PoolInfo matchInfo = poolSetupList.Find(x => x.type == type);
        
        // 2. 프리팹이 유효하다면 생성
        if (matchInfo.prefab != null)
        {
            Transform parent = _poolParents.ContainsKey(type) ? _poolParents[type] : transform;
            CreateNewObject(type, matchInfo.prefab, parent);
            
            // (선택) 로그가 너무 많이 뜨면 주석 처리하세요.
            // Debug.Log($"[Pool] {type} 개수가 부족하여 1개 추가 생성했습니다.");
        }
    }

    public void Return(UnitType type, GameObject obj)
    {
        if (obj == null) return;
        
        obj.SetActive(false);

        if (_poolDict.ContainsKey(type))
        {
            _poolDict[type].Enqueue(obj);
        }
        else
        {
            Destroy(obj); 
        }
    }
}