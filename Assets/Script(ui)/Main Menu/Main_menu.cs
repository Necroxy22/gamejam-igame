using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Main_menu : MonoBehaviour
{
    public GameObject panelOpsi;
    public GameObject panelTutorial;
    public static Main_menu instance;

    void Awake() {
        instance = this;
    }

    private void Start() {
        panelTutorial = GameObject.FindWithTag("Tutorial");
        panelOpsi = GameObject.FindWithTag("Opsi");
        if (panelOpsi != null)
        {
            panelOpsi.SetActive(false);
        }
        if (panelTutorial != null)
        {
            panelTutorial.SetActive(false);
        }

    }

    public void Main(){
        SceneManager.LoadScene(1);
    }

    public void Opsi(){
        if (panelOpsi != null)
        {
            panelOpsi.SetActive(true);
        }    
    }

    public void tutupOpsi(){
        if (panelOpsi != null)
        {
            panelOpsi.SetActive(false);
        }    
    }

    public void Keluar(){
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    public void Kredit(){
        SceneManager.LoadScene(2);
    }
    
    public void Back(){
        SceneManager.LoadScene(0);
    }

    public void Tutorial(){
        if (panelTutorial != null)
        {
            panelTutorial.SetActive(true);
        }  
    }

    public void tutupTutorial(){
        if (panelTutorial != null)
        {
            panelTutorial.SetActive(false);
        }  
    }
}
