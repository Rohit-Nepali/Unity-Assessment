using TMPro;
using UnityEngine;
using System.Linq;

public class QuadrantUIManager : MonoBehaviour
{
    [System.Serializable]
    public class QuadrantUI
    {
        public CameraFollow.CameraQuadrant quadrant;
        public RectTransform panel;
        public TextMeshProUGUI infoText;
        public Agent agent;

        public bool isDashboard;

        [HideInInspector]
        public float timer;

        [HideInInspector]
        public float totalDistance;

        [HideInInspector]
        public Vector3 lastPosition;

        [HideInInspector]
        public bool missionComplete;

        [HideInInspector]
        public float completionTime;
    }

    [Header("Agent Quadrants (First 3)")]
    [SerializeField]
    private QuadrantUI[] quadrantUIs = new QuadrantUI[4];

    [SerializeField]
    // private string dashboardTitle = "MISSION DASHBOARD";

    void Start()
    {
        // Initialize agent quadrants (first 3)
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            var quad = quadrantUIs[i];

            if (quad.agent != null && quad.infoText != null)
            {
                quad.timer = 0f;
                quad.totalDistance = 0f;
                quad.lastPosition = quad.agent.transform.position;
                quad.missionComplete = false;
                quad.completionTime = 0f;

                quad.infoText.text = "Waiting for agent...";
            }
        }
    }
    void Update()
    {
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            var quad = quadrantUIs[i];

            // DASHBOARD quadrant
            if (quad.isDashboard)
            {
                UpdateDashboardText(quad.infoText);
                continue;
            }

            // AGENT quadrants
            if (quad.agent != null && quad.infoText != null)
            {
                UpdateQuadrantUI(ref quadrantUIs[i]);
            }
        }
    }


    private void UpdateQuadrantUI(ref QuadrantUI quadUI)
    {
        Agent agent = quadUI.agent;
        Task3ParcelManager parcelSystem = agent.GetComponent<Task3ParcelManager>();
        Task3Agent task3Agent = agent.GetComponent<Task3Agent>();

        int parcels = parcelSystem != null ? parcelSystem.ParcelCount : 0;
        string phase = task3Agent != null ? task3Agent.GetCurrentPhase() : "Active";

        // Check if mission complete (for Task3Agent)
        if (task3Agent != null && !quadUI.missionComplete && task3Agent.IsMissionComplete())
        {
            MarkMissionComplete(agent);
        }

        // Only increment timer if agent is NOT idle at start
        // Use Task3Agent's moving state
        bool isMoving = task3Agent != null && (task3Agent.IsMoving() || task3Agent.IsCutting() || task3Agent.IsDelivering() || task3Agent.IsReturningHome());

        if (isMoving && !quadUI.missionComplete)
        {
            quadUI.timer += Time.deltaTime;
        }

        // Distance (per agent)
        float distanceTraveled = 0f;
        Vector3 currentPos = agent.transform.position;
        if (isMoving)
        {
            Vector3 delta = currentPos - quadUI.lastPosition;
            Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);
            distanceTraveled = horizontalDelta.magnitude;
            quadUI.totalDistance += distanceTraveled;
        }
        quadUI.lastPosition = currentPos;

        float animSpeed = agent.currentSpeed;

        // Format the UI text
        string statusColor = quadUI.missionComplete ? "#00FF00" : "#FFFFFF";
        string statusText = quadUI.missionComplete ? "COMPLETE" : phase;

        quadUI.infoText.text =
            $"<color=#FFFF00><b>Agent:</b></color> {agent.gameObject.name}\n"
            + $"<color=#00FF00><b>Time:</b></color> {quadUI.timer:F2}s\n"
            + $"<color=#00FFFF><b>Speed:</b></color> {animSpeed:F2} u/s\n"
            + $"<color=#FF00FF><b>Distance:</b></color> {quadUI.totalDistance:F2} units\n"
            + $"<color=#FFA500><b>Parcels:</b></color> {parcels}\n"
            + $"<color={statusColor}><b>Status:</b></color> {statusText}";
    }

    private void UpdateDashboardText(TextMeshProUGUI dashboardText)
    {
        int completed = 0;
        float totalTime = 0f;
        float totalDistance = 0f;

        string content = "<b><size=24>DASHBOARD</size></b>\n\n";

        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            var quad = quadrantUIs[i];

            if (quad.agent == null || quad.isDashboard) continue;

            totalTime += quad.timer;
            totalDistance += quad.totalDistance;
            if (quad.missionComplete) completed++;

            string status = quad.missionComplete ? "✓ Complete" : "● Active";

            content +=
                $"<b>{quad.agent.name}</b>\n" +
                $"Time: {quad.timer:F1}s\n" +
                $"Distance: {quad.totalDistance:F1}u\n" +
                $"Status: {status}\n\n";
        }

        content +=
            $"<b>Completed:</b> {completed}/3\n" +
            $"<b>Total Time:</b> {totalTime:F1}s\n" +
            $"<b>Total Distance:</b> {totalDistance:F1}u";

        dashboardText.text = content;
    }


    private string GetAgentColor(int index)
    {
        switch (index)
        {
            case 0: return "#FF6B6B";  // Red
            case 1: return "#4ECDC4";  // Teal
            case 2: return "#95E1D3";  // Mint
            default: return "#FFFFFF"; // White
        }
    }

    // Mark an agent's mission as complete
    public void MarkMissionComplete(Agent agent)
    {
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            if (quadrantUIs[i].agent == agent && !quadrantUIs[i].missionComplete)
            {
                quadrantUIs[i].missionComplete = true;
                quadrantUIs[i].completionTime = quadrantUIs[i].timer;
                Debug.Log($"[Dashboard] {agent.gameObject.name} completed in {quadrantUIs[i].timer:F2}s");
                break;
            }
        }
    }

    // Reset method for when you want to restart the simulation
    public void ResetMetrics()
    {
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            if (quadrantUIs[i].agent != null)
            {
                quadrantUIs[i].timer = 0f;
                quadrantUIs[i].totalDistance = 0f;
                quadrantUIs[i].lastPosition = quadrantUIs[i].agent.transform.position;
                quadrantUIs[i].missionComplete = false;
                quadrantUIs[i].completionTime = 0f;
            }
        }
    }

    // Get specific agent stats (useful for other scripts)
    public float GetAgentTime(int index)
    {
        if (index >= 0 && index < quadrantUIs.Length && quadrantUIs[index].agent != null)
        {
            return quadrantUIs[index].timer;
        }
        return 0f;
    }

    public float GetAgentDistance(int index)
    {
        if (index >= 0 && index < quadrantUIs.Length && quadrantUIs[index].agent != null)
        {
            return quadrantUIs[index].totalDistance;
        }
        return 0f;
    }
}
