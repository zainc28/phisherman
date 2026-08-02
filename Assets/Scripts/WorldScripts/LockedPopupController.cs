using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Small reusable popup controller: call Show(message) to display custom
/// text for a few seconds, auto-hiding afterward. Attach to any "locked"
/// popup panel — used both for world-locked messages and for the
/// "this door is already done, explore another building" message.
/// </summary>
public class LockedPopupController : MonoBehaviour
{
    public TMP_Text messageText;
    public float displayDuration = 2.8f;

    Coroutine _routine;

    public void Show(string message)
    {
        if (messageText != null) messageText.text = message;
        gameObject.SetActive(true);
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        gameObject.SetActive(false);
        _routine = null;
    }
}