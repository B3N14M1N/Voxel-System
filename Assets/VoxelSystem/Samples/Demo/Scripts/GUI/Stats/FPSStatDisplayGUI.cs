using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoxelSystem.Utils;

public class FPSStatDisplayGUI : MonoBehaviour, IStatDisplay
{
    private const string CounterName = "FPSCOUNTER";
    [SerializeField] private TMP_Text mainText;
    private StringBuilder mainTextString;

    [SerializeField] private GameObject content;
    [SerializeField] private TMP_Text contentText;
    private StringBuilder contentTextString;

    private bool displayContent {  get; set; }

    private float updateInterval = 1.0f;
    private float lastInterval; // Last interval end Time
    private float frames = 0; // Frames over current interval


    // Start is called before the first frame update
    void Start()
    {
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener( delegate { DisplayContent(); });
        }
        mainText.gameObject.hideFlags = HideFlags.HideAndDontSave;
        contentText.gameObject.hideFlags = HideFlags.HideAndDontSave;
        AvgCounter.AddCounter(CounterName);


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
        ++frames;

        var timeNow = Time.realtimeSinceStartup;

        if (timeNow > lastInterval + updateInterval)
        {
            float fps = frames / (timeNow - lastInterval);
            AvgCounter.UpdateCounter(CounterName, fps);

            UpdateMainText();
            if(displayContent)
                UpdateContentText();
            lastInterval = timeNow;
            frames = 0;
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
        contentTextString.AppendFormat("Avg : {0}\t\r\nMin : {1}\t\r\nMax: {2}\t",
            string.Format("{0:0.0}", AvgCounter.GetCounter(CounterName).AVG),
            string.Format("{0:0.0}", AvgCounter.GetCounter(CounterName).MinTime),
            string.Format("{0:0.0}", AvgCounter.GetCounter(CounterName).MaxTime));
        contentText.text = contentTextString.ToString();
    }

    public void UpdateMainText()
    {
        mainTextString.Length = 0;
        mainTextString.AppendFormat("FPS: {0}\t",
            string.Format("{0:0.0}", AvgCounter.GetCounter(CounterName).Time));
        mainText.text = mainTextString.ToString();
    }
}
