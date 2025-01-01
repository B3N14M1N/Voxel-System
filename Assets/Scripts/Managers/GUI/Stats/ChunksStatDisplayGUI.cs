using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

public class ChunksStatDisplayGUI : MonoBehaviour, IStatDisplay
{
    [SerializeField] private TMP_Text mainText;
    private StringBuilder mainTextString;

    [SerializeField] private GameObject content;
    [SerializeField] private TMP_Text contentText;
    private StringBuilder contentTextString;

    private bool displayContent { get; set; }

    private float updateInterval = 1.0f;
    private float lastInterval; // Last interval end Time

    private int meshVertices = 0;
    private int meshIndices = 0;
    private int colliderVertices = 0;
    private int colliderIndices = 0;
    private int chunks = 0;

    // Start is called before the first frame update
    void Start()
    {
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { DisplayContent(); });
        }
        mainText.gameObject.hideFlags = HideFlags.HideAndDontSave;
        contentText.gameObject.hideFlags = HideFlags.HideAndDontSave;

        mainTextString = new StringBuilder();
        mainTextString.Capacity = 200;
        contentTextString = new StringBuilder();
        contentTextString.Capacity = 200;
        UpdateMainText();
        UpdateContentText();
    }

    public void OnEnable()
    {
        if (mainTextString != null && contentTextString != null)
        {
            UpdateMainText();
            UpdateContentText();
        }
    }

    // Update is called once per frame
    void Update()
    {

        var timeNow = Time.realtimeSinceStartup;

        if (timeNow > lastInterval + updateInterval)
        {
            (chunks, meshVertices, meshIndices, colliderVertices, colliderIndices) = ChunksManager.Instance.ChunksMeshAndColliderSize();
            UpdateMainText();
            if (displayContent)
                UpdateContentText();
            lastInterval = timeNow;
        }
    }

    public void DisplayContent()
    {
        displayContent = !displayContent;
        content.SetActive(displayContent);
    }

    public void UpdateContentText()
    {
        contentTextString.Length = 0;
        contentTextString.AppendFormat("MESH\r\nVerts: {0}\t\r\nTris  : {1}\t\r\nCOLLIDER\r\nVerts: {2}\t\nTris  : {3}\t",
            meshVertices.ToString("N0"), meshIndices.ToString("N0"),
            colliderVertices.ToString("N0"), colliderIndices.ToString("N0"));
        contentText.text = contentTextString.ToString();
    }

    public void UpdateMainText()
    {
        mainTextString.Length = 0;
        mainTextString.AppendFormat("Chunks: {0}\t\r\n", chunks);
        mainText.text = mainTextString.ToString();
    }
}
