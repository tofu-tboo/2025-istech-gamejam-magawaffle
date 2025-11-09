using UnityEngine;

public class Dimmed : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Color _originalColor; // 각 파츠의 고유한 원본 색상
    void Awake()
    {
        // 자신의 SpriteRenderer 컴포넌트를 가져옵니다.
        _renderer = GetComponent<SpriteRenderer>();

        if (_renderer != null)
        {
            // 다른 스크립트가 색상을 변경하기 전에 원본 색상을 저장합니다.
            _originalColor = _renderer.color;
        }
        else
        {
            Debug.LogError($"'{gameObject.name}'에 SpriteRenderer 컴포넌트가 없습니다. Dimmer가 작동하지 않습니다.");
        }
    }
    
    public void AdjustBrightness(float factor)
    {
        if (_renderer == null) return;

        // 1. 원본 RGB 색상을 HSV로 변환
        float H, S, V;
        Color.RGBToHSV(_originalColor, out H, out S, out V);

        // 2. V(밝기)를 비율적으로 조정
        // 전달받은 factor를 곱합니다. factor가 1.0이면 V는 원래 V를 유지합니다.
        float newV = V * factor;
        
        // 3. 새 HSV 값으로 RGB를 재구성
        Color dimRgb = Color.HSVToRGB(H, S, newV);
        
        // 4. 알파 값은 원본 색상의 알파 값을 유지
        dimRgb.a = _originalColor.a; 
        
        // 5. 렌더러에 적용
        _renderer.color = dimRgb;
    }
}
