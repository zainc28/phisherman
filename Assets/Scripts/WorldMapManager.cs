using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// World map hub controller — combines what used to be PlayerController +
/// NPCInteraction into one script.
///
/// Responsibilities:
///   - Top-down player movement (WASD / arrow keys, clamped to bounds)
///   - Click detection on NPCs (via Physics2D + the NPCMarker component)
///   - Dialogue flow: line-by-line advance, end-of-dialogue choice prompt
///   - Loading the chosen minigame scene
///   - Bobbing exclamation mark above the active NPC
///
/// Wired by WorldMapBuilder. NPC content is defined inline in
/// InitializeDialogues() — edit there to add NPCs or change lines.
/// </summary>
public class WorldMapManager : MonoBehaviour
{
    [System.Serializable]
    public struct NPCDialogue
    {
        public string speakerName;
        [TextArea(2, 5)] public string[] lines;
        public string acceptText;       // e.g. "Sure, I can help!"
        public string declineText;      // e.g. "Maybe later"
        public string sceneToLoad;      // e.g. "EmailSwiper"
        public string declineResponse;  // single line shown if declined
    }

    [Header("Scene Refs")]
    public Transform playerTransform;
    public Transform[] npcTransforms;
    public Transform exclamationMark;

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public Button advanceButton;        // full-overlay click target
    public TMP_Text speakerNameText;
    public TMP_Text dialogueText;
    public TMP_Text continueHint;       // "click to continue" prompt
    public GameObject choicePanel;
    public Button acceptButton;
    public TMP_Text acceptButtonText;
    public Button declineButton;
    public TMP_Text declineButtonText;

    [Header("Movement")]
    public float playerSpeed = 4f;
    public Vector2 boundsX = new Vector2(-7f, 7f);
    public Vector2 boundsY = new Vector2(-3.5f, 1f);

    private NPCDialogue[] dialogues;
    private bool inDialogue;
    private int currentNpcIndex = -1;
    private int currentLineIndex;
    private bool waitingForChoice;
    private Vector3? moveTarget;            // tap-to-walk destination, null when idle

    void Start()
    {
        InitializeDialogues();
        dialoguePanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
    }

    void Update()
    {
        if (inDialogue) return;
        UpdatePlayer();
        UpdateExclamationBob();
        DetectClick();
    }

    // =========================================================================
    // Player movement
    // =========================================================================

    void UpdatePlayer()
    {
        // WASD/arrow input takes priority — cancels any active tap target
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
        if (input.sqrMagnitude > 0.01f)
        {
            moveTarget = null;
            if (input.magnitude > 1f) input.Normalize();
            Vector3 newPos = playerTransform.position
                + (Vector3)input * playerSpeed * Time.deltaTime;
            newPos.x = Mathf.Clamp(newPos.x, boundsX.x, boundsX.y);
            newPos.y = Mathf.Clamp(newPos.y, boundsY.x, boundsY.y);
            playerTransform.position = newPos;
            return;
        }

        // No keyboard input — walk toward the tap-set target if any
        if (moveTarget.HasValue)
        {
            Vector3 toTarget = moveTarget.Value - playerTransform.position;
            float dist = toTarget.magnitude;
            if (dist < 0.05f)
            {
                playerTransform.position = moveTarget.Value;
                moveTarget = null;
                return;
            }
            Vector3 step = toTarget.normalized * playerSpeed * Time.deltaTime;
            if (step.magnitude > dist) step = toTarget;
            playerTransform.position += step;
        }
    }

    // =========================================================================
    // Exclamation mark animation
    // =========================================================================

    void UpdateExclamationBob()
    {
        if (exclamationMark == null) return;
        if (npcTransforms == null || npcTransforms.Length == 0) return;
        // For now the exclamation hovers above NPC index 0
        Vector3 npcPos = npcTransforms[0].position;
        float bob = Mathf.Sin(Time.time * 4.5f) * 0.12f;
        exclamationMark.position = new Vector3(npcPos.x, npcPos.y + 1.2f + bob, 0f);
    }

    // =========================================================================
    // Click input — NPC dialogue OR tap-to-walk
    // =========================================================================

    void DetectClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        // Skip clicks over UI
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.IsPointerOverGameObject()) return;

        Vector2 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(wp);

        // Click on an NPC ? talk (takes priority over move target)
        if (hit != null)
        {
            var marker = hit.GetComponent<NPCMarker>();
            if (marker != null)
            {
                StartDialogue(marker.npcIndex);
                moveTarget = null;
                return;
            }
        }

        // Click on empty ground ? walk there (clamped to map bounds)
        moveTarget = new Vector3(
            Mathf.Clamp(wp.x, boundsX.x, boundsX.y),
            Mathf.Clamp(wp.y, boundsY.x, boundsY.y),
            0f);
    }

    // =========================================================================
    // Dialogue flow
    // =========================================================================

    public void StartDialogue(int npcIndex)
    {
        if (dialogues == null || npcIndex < 0 || npcIndex >= dialogues.Length) return;
        inDialogue = true;
        currentNpcIndex = npcIndex;
        currentLineIndex = 0;
        waitingForChoice = false;

        dialoguePanel.SetActive(true);
        if (choicePanel != null) choicePanel.SetActive(false);
        if (exclamationMark != null) exclamationMark.gameObject.SetActive(false);

        ShowCurrentLine();
    }

    void ShowCurrentLine()
    {
        var dlg = dialogues[currentNpcIndex];
        if (speakerNameText != null) speakerNameText.text = dlg.speakerName;
        if (dialogueText != null) dialogueText.text = dlg.lines[currentLineIndex];
        if (continueHint != null) continueHint.text = "Click to continue...";
    }

    public void AdvanceDialogue()
    {
        if (!inDialogue || waitingForChoice) return;

        var dlg = dialogues[currentNpcIndex];
        currentLineIndex++;
        if (currentLineIndex >= dlg.lines.Length)
        {
            ShowChoice();
        }
        else
        {
            ShowCurrentLine();
        }
    }

    void ShowChoice()
    {
        var dlg = dialogues[currentNpcIndex];
        waitingForChoice = true;
        if (choicePanel != null) choicePanel.SetActive(true);
        if (acceptButtonText != null) acceptButtonText.text = dlg.acceptText;
        if (declineButtonText != null) declineButtonText.text = dlg.declineText;
        if (continueHint != null) continueHint.text = "";
    }

    public void OnAcceptHelp()
    {
        if (!inDialogue) return;
        var dlg = dialogues[currentNpcIndex];
        if (!string.IsNullOrEmpty(dlg.sceneToLoad))
        {
            SceneManager.LoadScene(dlg.sceneToLoad);
        }
    }

    public void OnDeclineHelp()
    {
        if (!inDialogue) return;
        var dlg = dialogues[currentNpcIndex];
        if (choicePanel != null) choicePanel.SetActive(false);
        waitingForChoice = false;

        if (!string.IsNullOrEmpty(dlg.declineResponse))
        {
            if (dialogueText != null) dialogueText.text = dlg.declineResponse;
            if (continueHint != null) continueHint.text = "";
            StartCoroutine(EndAfterDelay(2f));
        }
        else
        {
            EndDialogue();
        }
    }

    IEnumerator EndAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndDialogue();
    }

    void EndDialogue()
    {
        dialoguePanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        inDialogue = false;
        currentNpcIndex = -1;
        if (exclamationMark != null) exclamationMark.gameObject.SetActive(true);
    }

    // =========================================================================
    // NPC content (edit here to add or tweak NPCs)
    // =========================================================================

    void InitializeDialogues()
    {
        dialogues = new NPCDialogue[]
        {
            // Index 0 — Grandma ? EmailSwiper
            new NPCDialogue
            {
                speakerName = "Grandma",
                lines = new string[]
                {
                    "Oh, hello dear! Thank goodness you're here.",
                    "I keep getting these strange emails... they say my account has been suspended, or that I've won some lottery I never entered.",
                    "Some of them look so real! But my grandson said scammers send fake emails to trick people.",
                    "I'm worried I'll click the wrong one. Could you sit with me and help me sort through them?"
                },
                acceptText      = "Of course, let's go!",
                declineText     = "Maybe later",
                sceneToLoad     = "EmailSwiper",
                declineResponse = "Oh, alright dear. Come back when you have a moment."
            }
            // Add more NPCs here. Make sure their NPCMarker.npcIndex
            // matches the index in this array.
        };
    }
}

/// <summary>
/// Marker component placed on an NPC's collider GameObject. The world map
/// manager reads <c>npcIndex</c> on click to look up which dialogue plays.
/// </summary>
public class NPCMarker : MonoBehaviour
{
    public int npcIndex;
}