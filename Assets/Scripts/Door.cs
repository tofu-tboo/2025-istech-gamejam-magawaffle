using UnityEngine;

// 이 스크립트는 SpriteRenderer와 Collider2D가 반드시 필요합니다.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    [Header("문 설정")]
    [Tooltip("문이 열렸을 때의 투명도 (0.0 = 완전 투명, 1.0 = 불투명)")]
    [Range(0f, 1f)]
    [SerializeField] private float openAlpha = 0.3f;

    // 컴포넌트 참조
    private SpriteRenderer sr;
    private Collider2D col;

    // 문이 닫혔을 때의 원래 색상
    private Color closedColor;

    void Awake()
    {
        // 컴포넌트 가져오기
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        // 시작 시점의 원래 색상(불투명)을 저장
        closedColor = sr.color;
    }

    void Start()
    {
        // 게임이 시작되면 항상 '닫힌' 상태로 강제 설정
        Close();
    }

    /// <summary>
    /// 문을 엽니다 (투명해지고, 통과 가능해짐).
    /// </summary>
    public void Open()
    {
        // 1. 콜라이더를 비활성화 (통과 가능하도록)
        if (col != null)
        {
            col.enabled = false;
        }

        // 2. 스프라이트를 '열린' 투명도로 변경
        if (sr != null)
        {
            // 원래 색상(R,G,B)은 유지하되, Alpha(투명도)만 openAlpha 값으로 설정
            sr.color = new Color(closedColor.r, closedColor.g, closedColor.b, openAlpha);
        }
    }

    /// <summary>
    /// 문을 닫습니다 (불투명해지고, 벽이 됨).
    /// </summary>
    public void Close()
    {
        // 1. 콜라이더를 활성화 (벽 역할)
        if (col != null)
        {
            col.enabled = true;
        }

        // 2. 스프라이트를 원래의 불투명한 색상으로 복원
        if (sr != null)
        {
            sr.color = closedColor;
        }
    }
}