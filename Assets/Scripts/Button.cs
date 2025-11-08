using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ButtonTrigger : MonoBehaviour
{
    [Header("버튼 식별자")]
    [SerializeField] private string buttonID; // 예: "OpenMainDoor"

    [Header("시각 효과 (선택적)")]
    [SerializeField] private Sprite spritePressed;   // 눌렸을 때 스프라이트
    [SerializeField] private Sprite spriteReleased; // 떨어졌을 때 스프라이트
    private SpriteRenderer sr;

    // 현재 버튼을 누르고 있는 'Body'의 수
    private int pressCount = 0;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>(); 
        GetComponent<Collider2D>().isTrigger = true; 
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // "Body" 태그를 가진 오브젝트만 감지 (필수!)
        if (other.CompareTag("Body"))
        {
            pressCount++;
            if (pressCount == 1) // 처음 눌렸을 때
            {
                ActivateButton();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Body"))
        {
            pressCount--;
            if (pressCount == 0) // 마지막 오브젝트가 떠났을 때
            {
                DeactivateButton();
            }
        }
    }

    private void ActivateButton()
    {
        if (sr != null && spritePressed != null) sr.sprite = spritePressed;
        
        // GameManager에게 보고
        if (GameManager.Instance != null)
            GameManager.Instance.OnButtonEvent(buttonID, true);
    }

    private void DeactivateButton()
    {
        if (sr != null && spriteReleased != null) sr.sprite = spriteReleased;
        
        // GameManager에게 보고
        if (GameManager.Instance != null)
            GameManager.Instance.OnButtonEvent(buttonID, false);
    }
}