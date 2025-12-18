using TMPro;
using UnityEngine;

public class QuadrantUIManager : MonoBehaviour
{
    [System.Serializable]
    public class QuadrantUI
    {
        public CameraFollow.CameraQuadrant quadrant;
        public RectTransform panel;
        public TextMeshProUGUI infoText;
        public Agent agent;
    }

    [SerializeField]
    private QuadrantUI[] quadrantUIs = new QuadrantUI[4];

    private float timer = 0f;
    private float totalDistance = 0f;
    private Vector3 lastPosition = Vector3.zero;

    void Start()
    {
        // Initialize quadrants
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            if (quadrantUIs[i].agent != null && quadrantUIs[i].infoText != null)
            {
                quadrantUIs[i].infoText.text = "Waiting for agent...";
                lastPosition = quadrantUIs[i].agent.transform.position;
            }
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Update UI for each quadrant
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            if (quadrantUIs[i].agent != null && quadrantUIs[i].infoText != null)
            {
                UpdateQuadrantUI(ref quadrantUIs[i]);
            }
        }
    }

    private void UpdateQuadrantUI(ref QuadrantUI quadUI)
    {
        Agent agent = quadUI.agent;

        // Calculate distance traveled
        Vector3 tmpDir = agent.transform.position - lastPosition;
        float tmpDistance = tmpDir.magnitude;
        totalDistance += tmpDistance;
        lastPosition = agent.transform.position;

        float animSpeed = agent.currentSpeed;

        // Format the UI text
        quadUI.infoText.text =
            $"<color=#FFFFFF><b>{quadUI.quadrant.ToString()}</b></color>\n"
            + $"<color=#FFFF00><b>Agent:</b></color> {agent.gameObject.name}\n"
            + $"<color=#00FF00><b>Time:</b></color> {timer:F2}s\n"
            + $"<color=#00FFFF><b>Speed:</b></color> {animSpeed:F2} u/s\n"
            + $"<color=#FF00FF><b>Distance:</b></color> {totalDistance:F2} units";
    }

    // Reset method for when you want to restart the simulation
    public void ResetMetrics()
    {
        timer = 0f;
        totalDistance = 0f;
        lastPosition = Vector3.zero;
    }
}
