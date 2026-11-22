using UnityEngine;

public class Persistentmusic : MonoBehaviour
{
    public static Persistentmusic instance;

    void Awake()
    {
        // Jika sudah ada pemutar musik dari scene sebelumnya, hancurkan yang baru
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Simpan objek ini agar tidak ikut hancur saat pindah scene
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}