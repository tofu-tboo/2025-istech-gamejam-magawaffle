using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [SerializeField] private string bodyLayerName = "Body";
    private int _bodyLayerMask = ~0;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CacheLayerMask();
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
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
