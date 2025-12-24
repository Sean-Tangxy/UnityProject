using System.Collections.Generic;
using UnityEngine;

[System.Serializable]   // ¡û ±ØÐëÓÐ
public class DialogueLine
{
    public string speaker;
    [TextArea(2, 6)]
    public string text;
    public Sprite portrait;
}

[CreateAssetMenu(menuName = "Dialogue/DialogueData")]
public class DialogueData : ScriptableObject
{
    public List<DialogueLine> lines = new List<DialogueLine>();
}
