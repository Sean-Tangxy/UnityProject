using UnityEngine;

public class DialogueAutoPlay : MonoBehaviour
{
    public DialogueUI ui;
    public DialogueData dialogue;

    void Start()
    {
        if (ui != null && dialogue != null)
        {
            ui.StartDialogue(dialogue);
        }
    }
}
