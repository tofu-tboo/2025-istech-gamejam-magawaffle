using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement; // 씬 관리를 위한 네임스페이스 추가
using System; // 리스트 사용을 위해 필수

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
    
    [Header("Scene Management")] // 씬 관리 설정을 위한 헤더
    [SerializeField] private int nextSceneName = 2 ; // 다음 씬의 이름

    // [추가] 전체 Body 스폰 횟수 추적 변수
    private int _totalBodiesSpawned = 0;
    
    private Scene currentScene; 

    /// <summary>
    /// [추가] 전체 게임에서 Body가 생성된 총 횟수입니다.
    /// </summary>
    public int TotalBodiesSpawned => _totalBodiesSpawned;


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
    
     private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // [추가] GameManager가 활성화될 때 현재 활성화된 씬 정보를 가져옵니다.
        // 이것은 에디터에서 플레이 버튼을 눌러 첫 씬이 로드될 때 유용합니다.
        currentScene = SceneManager.GetActiveScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

     private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // [수정] 현재 로드된 씬 정보를 저장
        currentScene = scene; 
        Debug.Log($"GameManager: 씬 '{currentScene.name}' 로드 완료. 빌드 인덱스: {currentScene.buildIndex}");

        // 1. 새 씬에 있는 "RespawnPoint" 태그를 가진 오브젝트를 찾습니다.
        GameObject spawnPointObj = GameObject.FindWithTag("Respawn");
        playerSoul = GameObject.FindWithTag("Player").GetComponent<Character>();

        if (spawnPointObj != null)
        {
            respawnPoint = spawnPointObj.transform;
            Debug.Log($"GameManager: 씬 '{currentScene.name}'의 RespawnPoint를 찾았습니다.");

            if (playerSoul != null)
            {
                playerSoul.HandleBodyDestruction();
                SpawnAndPossessBody(respawnPoint.position);
            }
            else
            {
                Debug.LogError("GameManager: playerSoul이 할당되지 않았습니다!");
            }
        }
        else
        {
            Debug.LogError($"GameManager: 씬 '{currentScene.name}'에 'RespawnPoint' 태그를 가진 오브젝트가 없습니다!");
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
        //HandleResurrection();
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
        public void LoadNextScene()
    {
        Debug.Log($"GameManager: 다음 씬 '{nextSceneName}'으로 이동합니다.");
        SceneManager.LoadScene(nextSceneName);
        nextSceneName += 1;
    }

    // --- Body 컨트롤 로직 ---

    /// <summary>
    /// 리스폰 지점에 새 'Body'를 생성하고 '영혼'이 'Playing' 상태로 빙의합니다.
    /// </summary>
    private void HandleResurrection()
    {
        Debug.Log("영혼: 리스폰 지점에 새 육체를 생성하고 빙의합니다.");

        GameObject newBodyObj = Instantiate(bodyPrefab, respawnPoint.position, respawnPoint.rotation);
        
        // [추가] Body 생성 횟수 증가
        _totalBodiesSpawned++;

        Body newBody = newBodyObj.GetComponent<Body>();

        if (newBody != null)
        {
            activeBodies.Add(newBodyObj);
            // 'Body'의 상태를 'playing'으로 설정 (HandleResurrection은 즉시 빙의)
            // newBody.state = BodyState.playing;
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
        if (bodyPrefab == null)
        {
            Debug.LogError("GameManager: bodyPrefab이 할당되지 않았습니다!");
            return;
        }

        if (respawnPoint == null)
        {
            Debug.LogError("GameManager: respawnPoint가 할당되지 않았습니다!");
            return;
        }

        Debug.Log("GameManager: 리스폰 포인트에 새 육체 생성 요청.");

        // 1. 새 Body 오브젝트 생성
        GameObject newBodyObj = Instantiate(bodyPrefab, respawnPoint.position, Quaternion.identity);
        _totalBodiesSpawned++;

        // 2. Body 스크립트에 접근하여 초기 상태 설정
        Body newBody = newBodyObj.GetComponent<Body>();
        if (newBody != null)
        {
            newBody.state = BodyState.undead; // 초기 상태는 'undead'
            activeBodies.Add(newBodyObj); // 리스트에 추가 (옵션)
        }
        else
        {
            Debug.LogError("bodyPrefab에 Body.cs 스크립트가 없습니다!");
        }
    }
    public void SpawnAndPossessBody(Vector3 spawnPosition)
    {
        Debug.Log($"GameManager: {spawnPosition}에 새 육체를 생성하고 빙의합니다.");

        // 1. 지정된 위치에 Body 프리팹 생성
        GameObject newBodyObj = Instantiate(bodyPrefab, spawnPosition, Quaternion.identity);
        
        // 2. 스폰 카운터 증가
        _totalBodiesSpawned++;

        Body newBody = newBodyObj.GetComponent<Body>();

        if (newBody != null)
        {
            // 3. 생성된 Body 리스트에 추가
            activeBodies.Add(newBodyObj);
            
            // 4. 'playerSoul' (Character)에게 이 Body에 빙의하라고 명령
            if (playerSoul != null)
            {
                playerSoul.PossessBody(newBody);
            }
            else
            {
                Debug.LogError("GameManager: playerSoul이 할당되지 않았습니다!");
            }
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
    
    public List<Body> GetOverlapped(Vector2 globalPosition, float range, bool multi = false)
    {
        var circleHits = Physics2D.OverlapCircleAll(globalPosition, range, _bodyLayerMask);
        
        // [디버그 1] 몇 개의 '콜라이더'를 찾았는지 확인
        Debug.Log($"[GameManager] OverlapCircleAll found {circleHits.Length} colliders on 'Body' layer.");

        var results = new List<Body>(multi ? circleHits.Length : 1);
        float closestSqrDist = float.MaxValue;
        Body closest = null;

        foreach (var hit in circleHits)
        {
            if (hit != null)
            {
                // [수정] 콜라이더에서 Body 컴포넌트를 먼저 가져옵니다.
                Body body = hit.gameObject.GetComponent<Body>();

                if (multi)
                {
                    // [수정] body가 null이 아닌 경우에만 리스트에 추가합니다.
                    if (body != null)
                    {
                        results.Add(body);
                    }
                    else
                    {
                        // (래그돌 자식 콜라이더 등이 감지된 경우)
                        Debug.LogWarning($"[GameManager] Found collider '{hit.gameObject.name}' but it has no Body.cs script.");
                        var ancestor = hit.transform.parent;
                        var hitScene = hit.gameObject.scene;
                        while (ancestor != null && ancestor.gameObject.scene == hitScene)
                        {
                            if (ancestor.TryGetComponent(out Body ancestorBody))
                            {
                                results.Add(ancestorBody);
                                break;
                            }

                            ancestor = ancestor.parent;
                        }
                    }
                }
                else // multi == false 로직 (R키 부활 시)
                {
                    // [수정] body가 null이 아닌 경우에만
                    if (body != null)
                    {
                        var sqrDist = ((Vector2)hit.transform.position - globalPosition).sqrMagnitude;
                        if (sqrDist < closestSqrDist)
                        {
                            closestSqrDist = sqrDist;
                            closest = body;
                        }
                    }
                }
            }
        }

        if (!multi && closest != null)
        {
            results.Add(closest);
        }

        // [디버그 2] 몇 개의 'Body 스크립트'를 찾았는지 확인 (이것이 "2"에서 "1"로 바뀔 것입니다)
        Debug.Log($"[GameManager] Returning {results.Count} valid bodies.");
        
        return results;
    }
}   
