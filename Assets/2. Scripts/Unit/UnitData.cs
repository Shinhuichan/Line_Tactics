using UnityEngine;

public enum UnitRace
{
    Humanic, // 기본
    Demonic, // 재생 특성
    Angelic  // (추후 구현)
}

[CreateAssetMenu(fileName = "NewUnitData", menuName = "Game Data/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("기본 정보")]
    public string unitName;
    public UnitType type;
    public UnitRace race; 
    [TextArea(3, 10)] public string description;
    
    // 🖼️ UI 표시용 아이콘
    public Sprite icon; 

    // 🏗️ [신규] 인게임 건물 외형 (스프라이트)
    // 비어있으면 프리팹의 기본 이미지를 사용합니다.
    public Sprite worldSprite;

    [Header("타입 설정")]
    public bool isRangedUnit; 
    public bool isFlyingUnit;
    public bool isMechanical; // 건물은 이걸 체크해주세요.

    [Header("건물 설정 (건물형 유닛만 사용)")]
    // 🏗️ [신규] 건설 소요 시간 (기본 10초)
    public float constructionTime = 10f;

    [Header("비용 설정")]
    public int ironCost; 
    public int oilCost; 

    [Header("전투 스탯")]
    public float hp;
    public float defense; 
    public float moveSpeed;
    public float attackRange; 
    public float detectRange = 6.0f;
    public float attackDamage;
    public float attackCooldown;
    
    [Header("스킬 설정")]
    public float explosionRadius = 1.5f; 

    [Header("AI 설정")]
    public float defendDistance = 2.0f;

    [Header("특수 이펙트")]
    public GameObject racialShieldPrefab;
}