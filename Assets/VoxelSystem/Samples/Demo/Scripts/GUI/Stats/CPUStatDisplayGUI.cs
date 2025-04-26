using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CPUStatDisplayGUI : MonoBehaviour, IStatDisplay
{
    private const string CounterName = "CPUCOUNTER";
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

        var timeNow = Time.realtimeSinceStartup;

        if (timeNow > lastInterval + updateInterval)
        {
            AvgCounter.UpdateCounter(CounterName, 1000.0f * Time.deltaTime);

            if (displayContent)
                UpdateContentText();
            lastInterval = timeNow;
        }

        UpdateMainText();
    }

    public void DisplayContent()
    {
        displayContent = !displayContent;
        content.SetActive(displayContent);
    }

    public void UpdateContentText()
    {
        if (AvgCounter.GetCounter(CounterName).AVG != 0.0f)
        {
            contentTextString.Length = 0;
            contentTextString.AppendFormat("Avg : {0}\tms\r\nMin : {1}\tms\r\nMax: {2}\tms",
                string.Format("{0:0.###}", AvgCounter.GetCounter(CounterName).AVG),
                string.Format("{0:0.###}", AvgCounter.GetCounter(CounterName).MinTime),
                string.Format("{0:0.###}", AvgCounter.GetCounter(CounterName).MaxTime));
            contentText.text = contentTextString.ToString();
        }
    }

    public void UpdateMainText()
    {
        mainTextString.Length = 0;
        mainTextString.AppendFormat("CPU: {0}\tms\r\n",
            string.Format("{0:0.0#}", AvgCounter.GetCounter(CounterName).Time));
        mainText.text = mainTextString.ToString();
    }
}
