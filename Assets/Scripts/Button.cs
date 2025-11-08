using UnityEngine;
using UnityEngine.Events; // UnityEvent를 사용하기 위해 필수

[RequireComponent(typeof(Collider2D))] // Collider2D 강제
[RequireComponent(typeof(SpriteRenderer))] // SpriteRenderer 강제
public class Button : MonoBehaviour
{
    [Header("버튼 이벤트")]
    [Tooltip("버튼이 처음 눌렸을 때 (0개 -> 1개) 호출됩니다.")]
    public UnityEvent OnPressed;

    [Tooltip("마지막 오브젝트가 버튼에서 떠났을 때 (1개 -> 0개) 호출됩니다.")]
    public UnityEvent OnReleased;

    // [추가] 스프라이트 설정
    [Header("시각 효과 (Sprites)")]
    [SerializeField] private Sprite spriteReleased; // 기본 (꺼진) 스프라이트
    [SerializeField] private Sprite spritePressed;  // 눌렸을 때 (켜진) 스프라이트
    
    private SpriteRenderer sr; // 스프라이트 렌더러 컴포넌트

    // 버튼 위에 올라와 있는 활성체(Body, Piston)의 수
    private int activatorCount = 0;
    
    // 감지할 레이어 마스크 (Body + Player)
    private LayerMask detectionMask;
    
    // 감지할 태그 (PistonPress)
    private const string PISTON_TAG = "PistonPress";

    void Awake()
    {
        // "Player" 레이어와 "Body" 레이어를 모두 감지하도록 설정
        int playerLayer = LayerMask.NameToLayer("Player");
        int bodyLayer = LayerMask.NameToLayer("Body");
        detectionMask = (1 << playerLayer) | (1 << bodyLayer);
        
        // [추가] 스프라이트 렌더러 가져오기 및 초기화
        sr = GetComponent<SpriteRenderer>();
        if (sr != null && spriteReleased != null)
        {
            sr.sprite = spriteReleased; // 시작 시 '꺼진' 스프라이트로 설정
        }

        // Collider2D를 Trigger로 강제 설정
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 지정된 레이어(Player, Body) 또는 태그(PistonPress)인지 확인
        if (IsInDetectionMask(other.gameObject.layer) || other.CompareTag(PISTON_TAG))
        {
            activatorCount++;
            
            // 카운트가 0에서 1이 되는 '첫 번째' 순간
            if (activatorCount == 1)
            {
                // [추가] '켜진' 스프라이트로 변경
                if (sr != null && spritePressed != null)
                {
                    sr.sprite = spritePressed;
                }
                
                OnPressed.Invoke(); // 이벤트 호출
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 지정된 레이어(Player, Body) 또는 태그(PistonPress)인지 확인
        if (IsInDetectionMask(other.gameObject.layer) || other.CompareTag(PISTON_TAG))
        {
            activatorCount--;
            
            // 카운트가 1에서 0이 되는 '마지막' 순간
            if (activatorCount == 0)
            {
                // [추가] '꺼진' 스프라이트로 변경
                if (sr != null && spriteReleased != null)
                {
                    sr.sprite = spriteReleased;
                }
                
                OnReleased.Invoke(); // 이벤트 호출
            }
        }
    }

    // 레이어 마스크(detectionMask)에 해당 레이어가 포함되어 있는지 확인
    private bool IsInDetectionMask(int layer)
    {
        // (1 << layer)는 해당 레이어 비트만 1로 켠 마스크입니다.
        // detectionMask와 AND 연산을 했을 때 0이 아니면 포함된 것입니다.
        return (detectionMask.value & (1 << layer)) != 0;
    }
}