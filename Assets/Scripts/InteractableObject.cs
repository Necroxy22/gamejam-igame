using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JejakMaya
{
    public class InteractableObject : MonoBehaviour
    {
        [Header("Scenario Link")]
        public int scenarioIndex;
        public float interactRange = 2.5f;

        [Header("Visual")]
        public GameObject footprintVisual;
        public Color hoverTint = Color.yellow;

        [Header("Runtime State")]
        [SerializeField] private bool isCompleted = false;
        public bool IsCompleted => isCompleted;

        private Color originalColor;
        private Renderer objRenderer;
        private Material runtimeMaterial;
        private Transform playerTransform;
        private bool isHighlighted;

        void Start()
        {
            objRenderer = GetComponent<Renderer>();
            if (objRenderer != null)
            {
                // .material membuat instance material; simpan agar tidak bocor tiap frame.
                runtimeMaterial = objRenderer.material;
                originalColor = runtimeMaterial.color;
            }

            if (footprintVisual != null)
            {
                footprintVisual.SetActive(false);
            }

            TryFindPlayer();
        }

        private void TryFindPlayer()
        {
            if (playerTransform != null) return;

            // Cari lewat komponen agar tidak bergantung pada tag "Player"
            // yang belum tentu terdaftar di TagManager.
            PlayerMovement player = FindObjectOfType<PlayerMovement>();
            if (player != null)
            {
                playerTransform = player.transform;
                return;
            }

            GameObject playerObj = GameObject.Find("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        void Update()
        {
            if (playerTransform == null) TryFindPlayer();

            if (isCompleted || playerTransform == null || GameManager.Instance == null || GameManager.Instance.IsUIActive())
            {
                SetHighlight(false);
                return;
            }

            if (IsClosestInteractable())
            {
                SetHighlight(true);

                if (Input.GetKeyDown(KeyCode.E))
                {
                    SetHighlight(false);
                    GameManager.Instance.OpenScenario(this);
                }
            }
            else
            {
                SetHighlight(false);
            }
        }

        private bool IsClosestInteractable()
        {
            float myDistance = Vector3.Distance(transform.position, playerTransform.position);
            if (myDistance > interactRange) return false;

            InteractableObject[] allObjects = FindObjectsOfType<InteractableObject>();
            foreach (InteractableObject other in allObjects)
            {
                if (other == this || other.isCompleted) continue;

                float otherDistance = Vector3.Distance(other.transform.position, playerTransform.position);
                if (otherDistance < myDistance) return false;
            }

            return true;
        }

        private void SetHighlight(bool active)
        {
            if (runtimeMaterial == null || isHighlighted == active) return;

            isHighlighted = active;
            runtimeMaterial.color = active ? hoverTint : originalColor;
        }

        private void OnDisable()
        {
            SetHighlight(false);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

        public void ShowNegativeConsequence()
        {
            if (footprintVisual != null)
            {
                footprintVisual.SetActive(true);
            }
        }

        public void MarkAsCompleted()
        {
            isCompleted = true;
            SetHighlight(false);
        }
    }
}
