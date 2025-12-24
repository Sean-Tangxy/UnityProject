using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public Image portraitImage;
    public GameObject continueHint;

    [Header("Typing")]
    public float charInterval = 0.02f;
    public KeyCode nextKey = KeyCode.E;

    DialogueData current;
    int index;
    Coroutine typingCo;
    bool isTyping;

    void Awake()
    {
        panel.SetActive(false);
        if (continueHint) continueHint.SetActive(false);
    }

    void Update()
    {
        if (!panel.activeSelf) return;

        if (Input.GetKeyDown(nextKey))
        {
            if (isTyping) // 正在打字：按一次直接显示完整句子
            {
                StopTypingAndShowFull();
            }
            else // 已经显示完整：进入下一句
            {
                Next();
            }
        }
    }

    public void StartDialogue(DialogueData data)
    {
        current = data;
        index = 0;
        panel.SetActive(true);
        ShowLine();
    }

    void ShowLine()
    {
        if (continueHint) continueHint.SetActive(false);

        var line = current.lines[index];
        if (speakerText) speakerText.text = line.speaker;

        if (portraitImage)
        {
            portraitImage.enabled = line.portrait != null;
            portraitImage.sprite = line.portrait;
        }

        if (typingCo != null) StopCoroutine(typingCo);
        typingCo = StartCoroutine(TypeText(line.text));
    }

    IEnumerator TypeText(string full)
    {
        isTyping = true;
        bodyText.text = "";
        foreach (char c in full)
        {
            bodyText.text += c;
            yield return new WaitForSeconds(charInterval);
        }
        isTyping = false;
        if (continueHint) continueHint.SetActive(true);
    }

    void StopTypingAndShowFull()
    {
        if (typingCo != null) StopCoroutine(typingCo);
        var line = current.lines[index];
        bodyText.text = line.text;
        isTyping = false;
        if (continueHint) continueHint.SetActive(true);
    }

    void Next()
    {
        index++;
        if (index >= current.lines.Count)
        {
            End();
            return;
        }
        ShowLine();
    }

    void End()
    {
        panel.SetActive(false);
        current = null;
        index = 0;
        if (continueHint) continueHint.SetActive(false);
    }
}
