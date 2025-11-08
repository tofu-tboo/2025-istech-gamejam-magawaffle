using UnityEngine;

public class Door : MonoBehaviour
{
    public void Open()
    {
        Debug.Log("문이 열립니다!");
        gameObject.SetActive(false); // 간단하게 비활성화
    }

    public void Close()
    {
        Debug.Log("문이 닫힙니다!");
        gameObject.SetActive(true); // 다시 활성화
    }
}