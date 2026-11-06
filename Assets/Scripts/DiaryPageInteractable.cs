using UnityEngine;

[DisallowMultipleComponent]
public class DiaryPageInteractable : MonoBehaviour
{
    [Range(1, 4)]
    public int pageNumber = 1;

    public string pageTitle = "Diary Entry - June 12";

    [TextArea(8, 26)]
    public string diaryEntry = "";

    [HideInInspector]
    public bool readOnce = false;
}