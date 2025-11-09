using UnityEngine;
using UnityEngine.SceneManagement;

public class Title : MonoBehaviour {
    void Update() {
        if (Input.GetKeyUp(KeyCode.Space)) {
            SceneManager.LoadScene(1);
        }
    }
}


