using UnityEngine;

public class VerticalScrollCamera : MonoBehaviour
{
    [Header("🎮 이동 설정")]
    [Tooltip("W/S 키를 눌렀을 때의 이동 속도입니다.")]
    public float keyboardSpeed = 20f;

    [Tooltip("마우스를 화면 가장자리에 댔을 때의 이동 속도입니다.")]
    public float edgeScrollSpeed = 20f;

    [Tooltip("화면 가장자리 감지 두께(픽셀)입니다. 마우스가 이 영역 안에 들어가면 이동합니다.")]
    public float edgeThickness = 20f;

    [Tooltip("이동의 부드러움 정도입니다. 작을수록 빠릿하고, 클수록 부드럽게 미끄러집니다. (추천: 0.1 ~ 0.3)")]
    public float smoothTime = 0.2f;

    [Header("🚧 맵 제한 설정 (Gizmos 확인 가능)")]
    [Tooltip("카메라가 갈 수 있는 가장 아래쪽 Y좌표입니다.")]
    public float minY = -50f;

    [Tooltip("카메라가 갈 수 있는 가장 위쪽 Y좌표입니다.")]
    public float maxY = 50f;

    [Header("옵션")]
    [Tooltip("마우스 가장자리 이동 기능을 켤지 여부입니다.")]
    public bool useEdgeScrolling = true;
    
    [Tooltip("게임 시작 시 마우스 커서를 게임 화면 안에 가둘지 여부입니다. (창 모드에서 유용)")]
    public bool confineCursor = true;

    // 내부 변수
    private Vector3 _targetPosition;
    private Vector3 _currentVelocity; // SmoothDamp용 참조 변수

    void Start()
    {
        // 시작 시 현재 카메라 위치를 목표 지점으로 설정 (튀는 현상 방지)
        _targetPosition = transform.position;

        if (confineCursor)
        {
            Cursor.lockState = CursorLockMode.Confined; // 마우스가 게임 창 밖으로 나가지 않음
        }
    }

    void Update()
    {
        HandleInput();
        MoveCamera();
    }

    void HandleInput()
    {
        float moveY = 0f;

        // 1. 키보드 입력 (W/S 또는 화살표 위/아래)
        float vInput = Input.GetAxisRaw("Vertical"); // -1, 0, 1
        if (vInput != 0)
        {
            moveY += vInput * keyboardSpeed;
        }

        // 2. 마우스 엣지 스크롤 (화면 가장자리)
        if (useEdgeScrolling)
        {
            Vector3 mousePos = Input.mousePosition;
            
            // 화면 상단 (위로 이동)
            if (mousePos.y >= Screen.height - edgeThickness)
            {
                moveY += edgeScrollSpeed;
            }
            // 화면 하단 (아래로 이동)
            else if (mousePos.y <= edgeThickness)
            {
                moveY -= edgeScrollSpeed;
            }
        }

        // 3. 목표 위치 갱신 (프레임 보정 적용)
        // X, Z는 현재 위치 고정, Y만 변경
        _targetPosition += Vector3.up * moveY * Time.deltaTime;

        // 4. 맵 밖으로 나가지 않게 가두기 (Clamp)
        _targetPosition.y = Mathf.Clamp(_targetPosition.y, minY, maxY);
        _targetPosition.x = transform.position.x; // 좌우 고정
        _targetPosition.z = transform.position.z; // 깊이 고정
    }

    void MoveCamera()
    {
        // 🌟 [핵심] 목표 지점까지 부드럽게 감속하며 이동 (SmoothDamp)
        // Lerp보다 훨씬 자연스러운 관성 효과를 줍니다.
        transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _currentVelocity, smoothTime);
    }

    // 🎨 에디터에서 이동 가능 범위를 눈으로 보여주는 기능
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        
        // 현재 X, Z축을 기준으로 위아래 선을 그립니다.
        Vector3 top = new Vector3(transform.position.x, maxY, 0);
        Vector3 bottom = new Vector3(transform.position.x, minY, 0);
        
        // 상한선 표시
        Gizmos.DrawLine(top + Vector3.left * 5, top + Vector3.right * 5);
        Gizmos.DrawSphere(top, 0.5f);
        
        // 하한선 표시
        Gizmos.DrawLine(bottom + Vector3.left * 5, bottom + Vector3.right * 5);
        Gizmos.DrawSphere(bottom, 0.5f);
        
        // 두 선을 잇는 세로선
        Gizmos.DrawLine(top, bottom);
    }
}