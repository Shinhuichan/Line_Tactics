using UnityEngine;
using System.Collections;

public class ShielderAbility : UnitAbility
{
    [Header("고유 능력: 철벽 방어")]
    [Range(0, 100)] public int blockChance = 15; // 15% 확률

    [Header("신규 능력: 철벽 태세 (Iron Wall)")]
    public string ironWallKey = "IRON_WALL";
    public float damageReductionRatio = 0.25f; // 25% 데미지 감소
    public float moveSpeedMultiplier = 0.5f;   // 이동속도 50%
    public float switchDelay = 0.5f;           // 전환 선딜레이

    [Header("상태 (Read Only)")]
    public bool isStanceOn = false;    // 현재 켜져 있는가?
    public bool isSwitching = false;   // 전환 중인가? (선딜레이)

    // 🛑 전환 중일 때는 Busy 상태 -> 이동/공격 불가
    public override bool IsBusy => isSwitching;

    public override void OnUpdate()
    {
        // 1. 업그레이드 해금 여부 확인 (내 태그 전달)
        if (UpgradeManager.I == null || !UpgradeManager.I.IsAbilityActive(ironWallKey, owner.tag)) return;

        // 2. 적 감지 여부 확인
        bool hasEnemy = owner.HasEnemyInDetectRange();

        // 3. 상태 전환 판단
        // 적이 있는데 꺼져있고, 전환 중이 아니라면 -> 켠다
        if (hasEnemy && !isStanceOn && !isSwitching)
        {
            StartCoroutine(SwitchStanceRoutine(true));
        }
        // 적이 없는데 켜져있고, 전환 중이 아니라면 -> 끈다
        else if (!hasEnemy && isStanceOn && !isSwitching)
        {
            StartCoroutine(SwitchStanceRoutine(false));
        }
    }

    IEnumerator SwitchStanceRoutine(bool turnOn)
    {
        isSwitching = true; // 🛑 행동 정지 (IsBusy = true)

        // 텍스트 연출 (선택사항)
        if (FloatingTextManager.I != null)
        {
            string msg = turnOn ? "Stance On..." : "Stance Off...";
            FloatingTextManager.I.ShowText(transform.position, msg, Color.gray, 20);
        }

        // --- 선딜레이 0.5초 대기 ---
        yield return new WaitForSeconds(switchDelay);

        // 상태 적용
        isStanceOn = turnOn;
        isSwitching = false; // ✅ 행동 재개

        if (isStanceOn)
        {
            // 켜짐: 속도 감소
            owner.SetMultipliers(1.0f, moveSpeedMultiplier, 1.0f);
            
            // (선택) 방패가 빛나는 등의 시각 효과 추가 가능
            if (FloatingTextManager.I != null) 
                FloatingTextManager.I.ShowText(transform.position, "Iron Wall!", Color.cyan, 30);
        }
        else
        {
            // 꺼짐: 속도 원상복구
            owner.SetMultipliers(1.0f, 1.0f, 1.0f);
        }
    }

    public override float OnTakeDamage(float incomingDamage, GameObject attacker)
    {
        // 1. 기존 능력: 확률적 완전 방어 (Block)
        int dice = Random.Range(0, 100);
        if (dice < blockChance)
        {
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Block!", Color.cyan, 35);
            return 0f; // 데미지 0
        }

        // 2. 신규 능력: 철벽 태세 (데미지 감소)
        if (isStanceOn)
        {
            // 방어 실패 시에도 데미지 감소 적용
            // (수학적으로 여기서 줄이나 방어력 계산 후에 줄이나 비율은 동일함)
            float reducedDamage = incomingDamage * (1.0f - damageReductionRatio);
            return reducedDamage;
        }

        // 아무 효과 없으면 원래 데미지 리턴
        return incomingDamage;
    }

    void OnDisable()
    {
        // 유닛이 죽거나 비활성화되면 상태 초기화
        isStanceOn = false;
        isSwitching = false;
        if (owner != null) owner.SetMultipliers(1.0f, 1.0f, 1.0f);
    }
}