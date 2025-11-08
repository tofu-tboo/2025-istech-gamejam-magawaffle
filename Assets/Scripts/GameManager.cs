using UnityEngine;
using System.Collections.Generic; // 리스트 사용을 위해 필수

public class GameManager : MonoBehaviour
{
    // 1. 싱글톤 인스턴스
    public static GameManager Instance { get; private set; }

    [Header("영혼 (플레이어)")]
    public Character playerSoul; // 씬에 있는 'Character' (영혼) 오브젝트

    [Header("육체 설정 (Body 컨트롤)")]
    public GameObject bodyPrefab; // 'Body.cs'가 부착된 프리팹
    public Transform respawnPoint; // 'Body'가 생성될 리스폰 위치

    [Header("키 설정")]
    public KeyCode resurrectKey = KeyCode.R; // '새 육체 생성 및 빙의' 키

    [Header("스테이지 기믹 (Button 컨트롤)")]
    public Door mainDoor; // 씬에 있는 'Door' 스크립트를 할당
    public GameObject trapBridge; // 씬에 있는 '다리' 오브젝트를 할당

    // 씬에 생성된 모든 'Body'들을 추적하는 리스트
    private List<GameObject> activeBodies = new List<GameObject>();
    [SerializeField] private string bodyLayerName = "Body";
    private int _bodyLayerMask = ~0;

    void Awake()
    {
        // 싱글톤 패턴 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 유지
            CacheLayerMask();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // playerSoul이 할당되었는지 확인
        if (playerSoul == null)
        {
            Debug.LogError("GameManager: playerSoul이 할당되지 않았습니다!");
            return;
        }
        // 게임 시작 시, '유령' 상태인 플레이어에게 첫 '육체'를 줍니다.
        HandleResurrection();
    }

    void Update()
    {
        // '새 육체 생성' 키 (유령 상태일 때만)
        if (Input.GetKeyDown(resurrectKey) && playerSoul.state == CharacterState.ghost)
        {
            HandleResurrection();
        }
    }

    private void CacheLayerMask()
    {
        var layerIndex = LayerMask.NameToLayer(bodyLayerName);
        _bodyLayerMask = layerIndex < 0 ? Physics2D.AllLayers : (1 << layerIndex);
        if (layerIndex < 0)
        {
            Debug.LogWarning($"Layer '{bodyLayerName}' not found. Using all layers for overlap queries.");
        }
    }

    // --- Body 컨트롤 로직 ---

    /// <summary>
    /// 리스폰 지점에 새 'Body'를 생성하고 '영혼'이 'Playing' 상태로 빙의합니다.
    /// </summary>
    private void HandleResurrection()
    {
        Debug.Log("영혼: 리스폰 지점에 새 육체를 생성하고 빙의합니다.");

        GameObject newBodyObj = Instantiate(bodyPrefab, respawnPoint.position, respawnPoint.rotation);
        Body newBody = newBodyObj.GetComponent<Body>();

        if (newBody != null)
        {
            activeBodies.Add(newBodyObj);
            // 'Body'의 상태를 'playing'으로 설정 (HandleResurrection은 즉시 빙의)
            newBody.state = BodyState.playing;
            // '영혼'에게 새 'Body'에 빙의하라고 명령
            playerSoul.PossessBody(newBody);
        }
        else
        {
            Debug.LogError("bodyPrefab에 Body.cs 스크립트가 없습니다!");
        }
    }

    /// <summary>
    /// [중요] Character가 'dead' Body를 버렸을 때 호출할 'public' 함수입니다.
    /// 'undead' 상태의 새 Body를 리스폰 지점에 생성합니다.
    /// </summary>
    public void SpawnNewUndeadBody()
    {
        Debug.Log("GameManager: 'dead' 알림 수신. 새 'undead' Body를 리스폰합니다.");

        GameObject newBodyObj = Instantiate(bodyPrefab, respawnPoint.position, respawnPoint.rotation);
        Body newBody = newBodyObj.GetComponent<Body>();

        if (newBody != null)
        {
            // [중요] 생성된 Body의 상태를 'undead'로 설정 (빙의하지 않음)
            newBody.state = BodyState.undead; 
            activeBodies.Add(newBodyObj);
        }
        else
        {
            Debug.LogError("bodyPrefab에 Body.cs 스크립트가 없습니다!");
        }
    }


    /// <summary>
    /// 맵의 모든 'Body'를 파괴하고 새로 시작합니다. (퍼즐 리셋용)
    /// </summary>
    public void ResetCurrentPuzzle()
    {
        Debug.Log("퍼즐 리셋: 모든 육체를 파괴합니다.");

        foreach (GameObject body in activeBodies)
        {
            Destroy(body);
        }
        activeBodies.Clear();

        // Character(영혼)의 감지 리스트 등 내부 상태도 리셋
        if (playerSoul != null)
        {
            playerSoul.ResetGhostState();
        }

        // 플레이어에게 새 'Body'를 주며 리셋
        HandleResurrection();
    }

    // --- Button 기믹 컨트롤 로직 ---

    public void OnButtonEvent(string buttonID, bool isActive)
    {
        Debug.Log($"GameManager: ButtonID '{buttonID}' 이벤트 수신. 상태: {isActive}");

        switch (buttonID)
        {
            case "OpenMainDoor":
                if (mainDoor != null)
                {
                    if (isActive) mainDoor.Open();
                    else mainDoor.Close();
                }
                break;

            case "ActivateTrapBridge":
                if (trapBridge != null)
                {
                    trapBridge.SetActive(isActive);
                }
                break;

            default:
                Debug.LogWarning($"GameManager: 처리되지 않은 buttonID '{buttonID}' 입니다.");
                break;
        }
    }
    
    public List<GameObject> GetOverlapped(Vector2 globalPosition, float range, bool multi = false)
    {
        var circleHits = Physics2D.OverlapCircleAll(globalPosition, range, _bodyLayerMask);
        var results = new List<GameObject>(multi ? circleHits.Length : 1);
        float closestSqrDist = float.MaxValue;
        GameObject closest = null;

        foreach (var hit in circleHits)
        {
            if (hit != null)
            {
                if (multi)
                {
                    results.Add(hit.gameObject);
                }
                else
                {
                    var sqrDist = ((Vector2)hit.transform.position - globalPosition).sqrMagnitude;
                    if (sqrDist < closestSqrDist)
                    {
                        closestSqrDist = sqrDist;
                        closest = hit.gameObject;
                    }
                }
            }
        }

        if (!multi && closest != null)
        {
            results.Add(closest);
        }

        return results;
    }
}