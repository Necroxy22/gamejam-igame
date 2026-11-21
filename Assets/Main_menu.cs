using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Main_menu : MonoBehaviour
{
    public void Main(){
        SceneManager.LoadScene(1);
    }

    public void Opsi(){
        SceneManager.LoadScene(2);
    }

    public void Keluar(){
        Application.Quit();
        UnityEditor.EditorApplication.isPlaying = false;
    }
    
}
