using UnityEngine;

public class ConstructionSpot : MonoBehaviour
{
    [Header("상태 정보")]
    public bool isOccupied = false; // 누군가 선점했는가?
    public BaseController currentBuilding; // 현재 지어진 건물

    // 건물이 파괴되었을 때 호출되어 상태 초기화
    public void FreeSpot()
    {
        isOccupied = false;
        currentBuilding = null;
        Debug.Log($"🏗️ 건설 구역({name})이 다시 비었습니다.");
    }

    // 건설 시작 시 호출
    public void OccupySpot(BaseController building)
    {
        isOccupied = true;
        currentBuilding = building;
    }
    
    // (선택) 에디터에서 위치 확인용
    void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(2, 2, 0));
    }
}