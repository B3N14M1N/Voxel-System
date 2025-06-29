using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using VoxelSystem.Settings;
using System.Collections.Generic;

namespace VoxelSystem.Samples.Demo
{
    public class DebuggingToggle : MonoBehaviour
    {
        [SerializeField] private Toggle debugToggle;
        [SerializeField] private TMP_Text debugText;
        [SerializeField] private int maxLogEntries = 30;
        
        private StringBuilder debugTextBuilder;
        private float updateInterval = 1f;
        private float lastUpdateTime;
        
        private List<string> infoLogs = new List<string>();
        private List<string> errorAndWarningLogs = new List<string>();

        private void Start()
        {
            // Initialize the StringBuilder
            debugTextBuilder = new StringBuilder();
            debugTextBuilder.Capacity = 1000;
            
            // Set up the toggle
            if (debugToggle != null)
            {
                debugToggle.isOn = WorldSettings.HasDebugging;
                debugToggle.onValueChanged.AddListener(OnToggleValueChanged);
            }
            
            // Hide the debug text if debugging is disabled
            if (debugText != null)
            {
                debugText.gameObject.SetActive(WorldSettings.HasDebugging);
            }
            
            // Register callback to capture debug logs
            Application.logMessageReceived += HandleLog;
        }
        
        private void OnDestroy()
        {
            // Unregister callback when this component is destroyed
            Application.logMessageReceived -= HandleLog;
        }
        
        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            // Only store logs if debugging is enabled
            if (!WorldSettings.HasDebugging) return;
            
            string logEntry = string.Empty;

            // Format log entries with different colors based on log type
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    logEntry = $"<color=red>[ERROR] {logString}</color>";
                    // Add to error logs and keep it limited
                    errorAndWarningLogs.Add(logEntry);
                    if (errorAndWarningLogs.Count > maxLogEntries / 2)
                    {
                        errorAndWarningLogs.RemoveAt(0);
                    }
                    break;
                    
                case LogType.Warning:
                    logEntry = $"<color=yellow>[WARN] {logString}</color>";
                    // Add to error logs and keep it limited
                    errorAndWarningLogs.Add(logEntry);
                    if (errorAndWarningLogs.Count > maxLogEntries / 2)
                    {
                        errorAndWarningLogs.RemoveAt(0);
                    }
                    break;
                    
                default:
                    logEntry = $"<color=white>[INFO] {logString}</color>";
                    // Add to info logs and keep it limited
                    infoLogs.Add(logEntry);
                    if (infoLogs.Count > maxLogEntries / 2)
                    {
                        infoLogs.RemoveAt(0);
                    }
                    break;
            }
        }

        private void Update()
        {
            // Only update the debug text periodically to avoid performance issues
            if (WorldSettings.HasDebugging && debugText != null && Time.realtimeSinceStartup > lastUpdateTime + updateInterval)
            {
                UpdateDebugText();
                lastUpdateTime = Time.realtimeSinceStartup;
            }
        }

        public void ToggleDebugging()
        {
            WorldSettings.HasDebugging = !WorldSettings.HasDebugging;
            
            if (debugToggle != null)
            {
                debugToggle.isOn = WorldSettings.HasDebugging;
            }
            
            if (debugText != null)
            {
                debugText.gameObject.SetActive(WorldSettings.HasDebugging);
            }
            
            // Clear log messages when turning debugging off
            if (!WorldSettings.HasDebugging)
            {
                errorAndWarningLogs.Clear();
                infoLogs.Clear();
            }
            else
            {
                // Add a log message to show that debugging was turned on
                Debug.Log("Debugging has been enabled");
            }
        }
        
        private void OnToggleValueChanged(bool isOn)
        {
            WorldSettings.HasDebugging = isOn;
            
            if (debugText != null)
            {
                debugText.gameObject.SetActive(isOn);
            }
        }
        
        private void UpdateDebugText()
        {
            if (debugText == null) return;
            
            debugTextBuilder.Clear();
            
            debugTextBuilder.AppendLine("<b>Debug Information</b>");

            // Add console logs section header
            debugTextBuilder.AppendLine("\n<b>Console Logs</b>");
            
            // Add errors and warnings first (they're more important)
            if (errorAndWarningLogs.Count > 0)
            {
                foreach (string logEntry in errorAndWarningLogs)
                {
                    debugTextBuilder.AppendLine(logEntry);
                }
                
                // Add a separator if we have both types of logs
                if (infoLogs.Count > 0)
                {
                    debugTextBuilder.AppendLine("---------------------");
                }
            }
            
            // Then add info logs
            if (infoLogs.Count > 0)
            {
                foreach (string logEntry in infoLogs)
                {
                    debugTextBuilder.AppendLine(logEntry);
                }
            }
            
            // If no logs at all, show a message
            if (errorAndWarningLogs.Count == 0 && infoLogs.Count == 0)
            {
                debugTextBuilder.AppendLine("<i>No logs to display</i>");
            }
            
            // Set the text
            debugText.text = debugTextBuilder.ToString();
        }
    }
}
