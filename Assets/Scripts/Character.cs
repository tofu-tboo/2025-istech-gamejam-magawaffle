using UnityEngine;

public enum CharacterState
{
    moving,
    ghost
}

public class Character : MonoBehaviour
{
    public CharacterState state = CharacterState.moving;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
