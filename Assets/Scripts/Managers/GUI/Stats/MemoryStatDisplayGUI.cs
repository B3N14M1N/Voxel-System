using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

public class MemoryStatDisplayGUI : MonoBehaviour, IStatDisplay
{
    [SerializeField] private TMP_Text mainText;
    private StringBuilder mainTextString;

    [SerializeField] private GameObject content;
    [SerializeField] private TMP_Text contentText;
    private StringBuilder contentTextString;

    private bool displayContent { get; set; }

    private float updateInterval = 1.0f;
    private float lastInterval; // Last interval end Time

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
        contentTextString.AppendFormat("Sys  : {0}\tmb\r\nAlloc: {1}\tmb\r\nRes : {2}\tmb",
            SystemInfo.systemMemorySize,
            Profiler.GetTotalAllocatedMemoryLong() / 1048576,
            Profiler.GetTotalReservedMemoryLong() / 1048576);
        contentText.text = contentTextString.ToString();
    }

    public void UpdateMainText()
    {
        mainTextString.Length = 0;
        var str = string.Format("{0:0.0}", (Profiler.GetTotalAllocatedMemoryLong() / 1048576.0f / SystemInfo.systemMemorySize) * 100f);
        mainTextString.AppendFormat("Memory: {0}\t%\r\n",
            (str[1] == '.' ? " " : "") + str);
        mainText.text = mainTextString.ToString();
    }
}
