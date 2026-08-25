using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// Displays each city category as a row: label, progress bar (fillAmount = points/1000),
/// percentage text, colored by status (green/orange/red). Matches the reference stats screen
/// layout. Build one row per category in your Canvas, assign them here.
public class CityStatsPanel : MonoBehaviour
{
    public static CityStatsPanel Instance { get; private set; }

    [System.Serializable]
    public class CategoryRow
    {
        public BuildingCategory category;
        public TMP_Text categoryLabel;   // optional - can be pre-set as static text on the prefab instead
        public Image barFill;            // Image with Image Type = Filled, Fill Method = Horizontal
        public TMP_Text statusText;      // shows the percentage
    }

    public List<CategoryRow> rows = new List<CategoryRow>();

    [Header("Colors")]
    public Color healthyColor = new Color(0.3f, 0.85f, 0.3f);
    public Color warningColor = new Color(1f, 0.65f, 0.1f);
    public Color criticalColor = new Color(0.9f, 0.2f, 0.2f);

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        RefreshDisplay();
    }

    /// Called directly by CityStatsManager whenever stats change - no event/Action needed.
    public void RefreshDisplay()
    {
        if (CityStatsManager.Instance == null) return;

        foreach (var row in rows)
        {
            float points = CityStatsManager.Instance.GetPoints(row.category);
            float percent = points / 10f; // 0-1000 -> 0-100
            var status = CityStatsManager.Instance.GetStatus(row.category);

            Color statusColor = status switch
            {
                CityMetricStatus.Critical => criticalColor,
                CityMetricStatus.Warning => warningColor,
                _ => healthyColor
            };

            if (row.barFill != null)
            {
                row.barFill.fillAmount = CityStatsManager.Instance.GetNormalized01(row.category);
                row.barFill.color = statusColor;
            }

            if (row.statusText != null)
            {
                row.statusText.text = $"{percent:0}%";
                row.statusText.color = statusColor;
            }

            if (row.categoryLabel != null)
                row.categoryLabel.text = row.category.ToString();
        }
    }
}