using System.Collections;
using UnityEngine;
using TMPro;

[AddComponentMenu("Player/Quest HUD")]
public class QuestHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text questText;
    [SerializeField] private string titleLine = "Mision";
    [SerializeField, Range(5, 40)] private int separatorLength = 15;

    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color completeColor = new Color(0.35f, 1f, 0.35f);
    [SerializeField, Min(0f)] private float hideDelayAfterComplete = 3f;

    private string objectiveLabel = "";
    private int current;
    private int total;
    private bool completed;
    private Coroutine hideRoutine;

    private void Awake()
    {
        if (questText != null)
            questText.text = "";
    }

    public void ShowQuest(string label, int totalCount)
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        objectiveLabel = label;
        total = totalCount;
        current = 0;
        completed = false;

        if (questText != null)
            questText.gameObject.SetActive(true);

        Refresh();
    }

    public void UpdateProgress(int newCurrent)
    {
        current = newCurrent;
        Refresh();
    }

    public void CompleteQuest()
    {
        current = total;
        completed = true;
        Refresh();

        if (isActiveAndEnabled)
            hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelayAfterComplete);

        if (questText != null)
            questText.gameObject.SetActive(false);

        hideRoutine = null;
    }

    private void Refresh()
    {
        if (questText == null) return;

        string separator = new string('-', separatorLength);
        string objective = $"{objectiveLabel} ({current}/{total})";
        if (completed)
            objective = $"<s>{objective}</s>";

        string colorHex = ColorUtility.ToHtmlStringRGB(completed ? completeColor : activeColor);
        questText.text = $"<color=#{colorHex}>{titleLine}\n{separator}\n{objective}</color>";
    }
}
