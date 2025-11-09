using UnityEngine;
// using System.Data.Common; // 데이터베이스 관련 네임스페이스이므로 필요 없으면 삭제

// 이 스크립트는 SpriteRenderer와 Collider2D가 반드시 필요합니다.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    [Header("문 설정")]
    [Tooltip("문이 열렸을 때의 투명도 (0.0 = 완전 투명, 1.0 = 불투명)")]
    [Range(0f, 1f)]
    [SerializeField] private float openAlpha = 0.3f;

    [Tooltip("이 문이 어떤 타입인지 지정합니다 (예: 'A', 'B'). DoorController의 currentDoorState와 일치하면 열립니다.")]
    [SerializeField] private string doorType; // 이 Door 오브젝트의 타입 (예: "A", "B")

    // 컴포넌트 참조
    private SpriteRenderer sr;
    private Collider2D col;

    // 문이 닫혔을 때의 원래 색상 (불투명 상태)
    private Color closedColor;

    void Awake()
    {
        // 컴포넌트 가져오기
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        // 시작 시점의 스프라이트의 원래 색상을 저장합니다.
        // 이 색상의 Alpha 값은 1.0 (불투명)이라고 가정합니다.
        closedColor = sr.color;
    }

    void Start()
    {
        // 게임이 시작되면 항상 '닫힌' 상태로 강제 설정하여 초기 상태를 보장합니다.
        Close();
    }

    void Update()
    {
        if (doorType != null)
        {
            // DoorController 클래스의 static 변수 currentDoorState를 직접 참조합니다.
            // DoorController 오브젝트가 씬에 없거나 스크립트가 비활성화되어 있어도 이 값은 접근 가능합니다.
            // 다만, DoorController가 초기화되지 않았다면 기본값 ""을 가질 수 있습니다.
            string currentControlState = DoorController.currentDoorState;

            // 이 문의 'doorType'이 DoorController가 지시하는 'currentControlState'와 일치하면 문을 엽니다.
            if (doorType == currentControlState)
            {
                Open();
            }
            else // 일치하지 않으면 문을 닫습니다.
            {
                Close();
            }
        }
    }

    /// <summary>
    /// 문을 엽니다 (투명해지고, 플레이어가 통과할 수 있도록 콜라이더를 비활성화합니다).
    /// </summary>
    public void Open()
    {
        // 콜라이더가 있다면 비활성화하여 통과 가능하게 만듭니다.
        if (col != null)
        {
            col.enabled = false;
        }

        // 스프라이트 렌더러가 있다면 '열린' 투명도로 변경합니다.
        if (sr != null)
        {
            // 원래 색상(R,G,B)은 유지하되, Alpha(투명도)만 'openAlpha' 값으로 설정합니다.
            sr.color = new Color(closedColor.r, closedColor.g, closedColor.b, openAlpha);
        }
    }

    /// <summary>
    /// 문을 닫습니다 (불투명해지고, 플레이어가 통과할 수 없도록 콜라이더를 활성화합니다).
    /// </summary>
    public void Close()
    {
        // 콜라이더가 있다면 활성화하여 벽 역할을 하게 만듭니다.
        if (col != null)
        {
            col.enabled = true;
        }

        // 스프라이트 렌더러가 있다면 원래의 불투명한 색상으로 복원합니다.
        if (sr != null)
        {
            sr.color = closedColor; // 저장해둔 원래 색상(불투명)으로 설정
        }
    }
}