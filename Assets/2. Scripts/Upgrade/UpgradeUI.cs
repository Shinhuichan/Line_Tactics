using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UpgradeUI : SingletonBehaviour<UpgradeUI>
{
    protected override bool IsDontDestroy() => false; 

    [Header("UI Control")]
    public GameObject uiPanel; 

    [Header("References")]
    public Transform contentParent; 
    public GameObject upgradeSlotPrefab; 
    public Button closeButton;

    private List<UpgradeSlot> createdSlots = new List<UpgradeSlot>();

    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => CloseUI());

        if (uiPanel != null)
            uiPanel.SetActive(false); 
            
        // 시작 시 일단 생성 (기본값: Humanic)
        GenerateSlots();
    }

    // 🔄 [핵심 수정] 강제 리프레시
    public void RefreshUI()
    {
        // ❌ 기존: if (createdSlots.Count == 0) GenerateSlots();
        // 이유: 이렇게 하면 처음에 Humanic으로 생성된 뒤, Demonic을 골라도 갱신이 안 됨.
        
        // ✅ 수정: 무조건 다시 그리기
        GenerateSlots();
    }

    public void OpenUI()
    {
        if (uiPanel != null) uiPanel.SetActive(true);
    }

    public void CloseUI()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
    }

    void GenerateSlots()
    {
        if (contentParent == null) return;
        if (UpgradeManager.I == null || GameManager.I == null) return;
        if (UpgradeManager.I.allUpgrades == null) return;

        // 1. 기존 슬롯 싹 지우기 (초기화)
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);
        createdSlots.Clear();

        // 2. 현재 플레이어 종족 확인 (GameManager가 최신 정보를 가짐)
        UnitRace myRace = GameManager.I.playerRace;
        
        // Debug.Log($"[UpgradeUI] UI 갱신 시작. 현재 종족: {myRace}");

        // 3. 필터링하여 슬롯 생성
        foreach (var data in UpgradeManager.I.allUpgrades)
        {
            if (data == null) continue;

            // 🧬 필터링: 공용이거나 OR 내 종족과 일치하는 것만
            bool isCompatible = data.isCommonUpgrade || (data.raceRequirement == myRace);

            if (isCompatible)
            {
                if (upgradeSlotPrefab != null)
                {
                    GameObject go = Instantiate(upgradeSlotPrefab, contentParent);
                    UpgradeSlot slot = go.GetComponent<UpgradeSlot>();
                    if (slot != null)
                    {
                        slot.Setup(data);
                        createdSlots.Add(slot);
                    }
                }
            }
        }
    }
}