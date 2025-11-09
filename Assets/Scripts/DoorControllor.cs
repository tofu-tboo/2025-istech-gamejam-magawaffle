// DoorController.cs

using UnityEngine;

/// <summary>
/// A, B 타입 등 모든 문(Door)의 현재 상태를 전역적으로 관리합니다.
/// 씬에 Empty Object를 만들고 이 스크립트를 붙여 사용합니다.
/// </summary>
public class DoorController : MonoBehaviour
{
    // [핵심] 'static'으로 선언하여 모든 스크립트가 'DoorController.currentDoorState'로
    // 이 변수에 접근할 수 있게 합니다.
    public static string currentDoorState; // ""(빈 문자열)은 모든 문이 닫힌 상태

    /// <summary>
    /// [static 함수] 외부에서 문 상태를 변경합니다.
    /// (예: 버튼, 레버 등)
    /// </summary>
    /// <param name>"A", "B", "" 등</param>
    public static void SetDoorState()
    {
        if (currentDoorState == "B")
        {
            currentDoorState = "A";
        }
        else
        { currentDoorState = "B"; }
        
    }

    // (선택 사항) 만약 씬이 로드될 때마다 문 상태를 리셋하고 싶다면
    // GameManager처럼 씬 로드 이벤트를 구독하여 currentDoorState = ""; 를 실행할 수 있습니다.
    // 하지만 지금은 GameManager가 DontDestroyOnLoad이므로 이 컨트롤러도 DontDestroyOnLoad로 만들거나,
    // 씬이 바뀔 때마다 버튼 상태에 따라 문 상태가 유지되도록 그냥 두는 것이 나을 수 있습니다.
}