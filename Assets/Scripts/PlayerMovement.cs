using JejakMaya;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class PlayerMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private Camera mainCam;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        mainCam = Camera.main;
    }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        // Abaikan klik jika kursor sedang menekan UI (Panel Inspeksi/Feedback/Result).
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Jangan gerak saat panel game terbuka.
        if (GameManager.Instance != null && GameManager.Instance.IsUIActive())
            return;

        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        // Jangan menjadikan pemain sendiri sebagai tujuan.
        if (hit.transform == transform || hit.transform.IsChildOf(transform)) return;

        agent.SetDestination(hit.point);
    }
}