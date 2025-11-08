using UnityEngine;

public enum BodyState
{
    playing,
    undead,
    dead
}

public class Body : MonoBehaviour
{
    public BodyState state = BodyState.playing;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
