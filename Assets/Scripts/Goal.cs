// Goal.cs

using UnityEngine;

public class Goal : MonoBehaviour
{
    [Tooltip("Goal에 닿았을 때 활성화될 오브젝트 (예: 파티클 효과, 문 열림 등)")]
    [SerializeField] private GameObject activateOnGoal;

    private bool isGoalReached = false; // 한 번만 작동하도록

    // Trigger 충돌 감지
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 이미 골에 도달했거나, 충돌한 오브젝트가 Body가 아니면 무시
        if (isGoalReached || other.gameObject.layer != LayerMask.NameToLayer("Player") || other.GetComponent<Body>().IsPlaying()) // Player 레이어 (빙의된 Body)만 감지
        {
            return;
        }

        // 충돌한 오브젝트가 Body의 일부인지 확인 (Character 스크립트가 붙은 최상위 Body 오브젝트를 찾음)
        // Physics2D.OverlapCircleAll 등으로 정확한 Body 오브젝트를 찾는 것이 더 안정적일 수 있으나,
        // 현재는 Body가 Player 레이어에 있으므로 layer 체크만으로 충분합니다.
        
        Debug.Log($"Goal: {other.gameObject.name}이 Goal에 도달했습니다!");
        isGoalReached = true; // 중복 작동 방지

        // 시각적 효과 활성화 (선택 사항)
        if (activateOnGoal != null)
        {
            activateOnGoal.SetActive(true);
        }

        // GameManager를 통해 다음 씬 로드
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadNextScene();
        }
        else
        {
            Debug.LogError("Goal: GameManager 인스턴스를 찾을 수 없습니다!");
        }
    }
}