using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    public string sceneToLoad = "YourSceneName"; // Change this to your scene name

    private bool hasClicked = false;

    void Update()
    {
        // Listen for user click/tap anywhere on the screen
        if (Input.GetMouseButtonDown(0) && !hasClicked)
        {
            hasClicked = true; // Prevent multiple scene loads
            LoadNextScene();
        }
    }

    void LoadNextScene()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
}
