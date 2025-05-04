using UnityEngine;
using TMPro;

namespace VoxelSystem.Samples.Demo
{
    public class PlayerPositionDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI positionText;
        [SerializeField] private Transform playerTransform;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            // If playerTransform is not set, try to find the player in the scene
            if (playerTransform == null)
            {
                playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
                
                if (playerTransform == null)
                {
                    Debug.LogWarning("PlayerPositionDisplay: Player transform not assigned and could not find GameObject with 'Player' tag.");
                }
            }
            
            // If positionText is not set, try to get it from this GameObject
            if (positionText == null)
            {
                positionText = GetComponent<TextMeshProUGUI>();
                
                if (positionText == null)
                {
                    Debug.LogError("PlayerPositionDisplay: TextMeshProUGUI component not assigned or found.");
                    enabled = false; // Disable this component if we can't display text
                }
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (playerTransform != null && positionText != null)
            {
                Vector3 position = playerTransform.position;
                positionText.text = $"{Mathf.Round(position.x)}, {Mathf.Round(position.y)}, {Mathf.Round(position.z)}";
            }
        }
    }
}
