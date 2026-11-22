using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class play_ui_manager : MonoBehaviour
{
    public GameObject panelCraft;
    public GameObject panelPause;
    public GameObject panelOpsi;

    void Start()
    {
        // Hentikan dan hapus pemutar musik Main Menu saat masuk ke scene Play
        if (Persistentmusic.instance != null)
        {
            GameObject musikMenu = Persistentmusic.instance.gameObject;
            Persistentmusic.instance = null;
            Destroy(musikMenu);
        }

        // Pastikan waktu berjalan normal setiap kali level dimulai
        Time.timeScale = 1f;

        // Cek satu per satu agar jika satu kosong, yang lain tetap jalan
        if (panelCraft != null) panelCraft.SetActive(false);
        if (panelPause != null) panelPause.SetActive(false);
        if (panelOpsi != null) panelOpsi.SetActive(false);
    }

    void Update()
    {
        
    }

    public void craft(){
        if(panelCraft != null){
            panelCraft.SetActive(true);
        }
    }

    public void tutupCraft(){
        if(panelCraft != null){
            panelCraft.SetActive(false);
        }
    }

    public void Pause(){
        if(panelPause != null){
            panelPause.SetActive(true);
        }
    }

    public void tutuppause(){
        if(panelPause != null){
            panelPause.SetActive(false);
        }
    }

    public void keluar(){
        SceneManager.LoadScene(0);
    }

    public void opsi(){
        panelPause.SetActive(false);
        if(panelOpsi != null){
            panelOpsi.SetActive(true);
        }
    }

    public void tutupopsi(){
       if(panelOpsi != null){
            panelOpsi.SetActive(false);
       }
    }
}