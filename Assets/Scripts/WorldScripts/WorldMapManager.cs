using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// World Map hub controller for Adventures of Phisherman.
///
/// Handles:
///   - Top-down player movement (WASD / arrows + click-to-walk)
///   - Player sprite flipping based on movement direction
///   - Proximity exclamation marks above all available NPCs
///   - Click-on-NPC or walk-into-door to start dialogue
///   - Line-by-line dialogue with accept/decline choice
///   - Loading the chosen minigame scene
///   - M key toggles the world map overlay
///
/// Built by WorldMapBuilder. Edit InitializeDialogues() to change NPC content.
/// </summary>
public class WorldMapManager : MonoBehaviour
{
    // =========================================================================
    // Data types
    // =========================================================================

    [System.Serializable]
    public struct NPCDialogue
    {
        public string speakerName;
        public string npcDescription;        // short label shown on map overlay
        [TextArea(2, 5)] public string[] lines;
        public string acceptText;
        public string declineText;
        public string sceneToLoad;
        public string declineResponse;
    }

    // =========================================================================
    // Inspector refs — all wired by WorldMapBuilder
    // =========================================================================

    [Header("Player")]
    public Transform playerTransform;
    public SpriteRenderer playerRenderer;   // for left/right flip
    public float playerSpeed = 4f;
    public Vector2 boundsX = new Vector2(-8f, 8f);
    public Vector2 boundsY = new Vector2(-3.8f, 1.2f);

    [Header("NPCs")]
    public Transform[] npcTransforms;            // one per NPC, same order as dialogues[]
    public GameObject[] exclamationMarks;        // one per NPC
    public float exclamationProximity = 3.5f;   // how close before ! appears
    public float interactRadius = 1.2f;          // auto-talk radius (doors/NPCs)

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public Button advanceButton;
    public TMP_Text speakerNameText;
    public TMP_Text dialogueText;
    public TMP_Text continueHint;
    public GameObject choicePanel;
    public Button acceptButton;
    public TMP_Text acceptButtonText;
    public Button declineButton;
    public TMP_Text declineButtonText;

    [Header("World Map Overlay")]
    public GameObject worldMapPanel;            // M key shows/hides this

    // =========================================================================
    // Private state
    // =========================================================================

    private NPCDialogue[] dialogues;
    private bool inDialogue;
    private int currentNpcIndex = -1;
    private int currentLineIndex;
    private bool waitingForChoice;
    private Vector3? moveTarget;
    private bool mapOpen;
    private bool[] npcTriggered;    // prevents re-triggering while standing still

    // =========================================================================
    // Lifecycle
    // =========================================================================

    void Start()
    {
        InitializeDialogues();
        npcTriggered = new bool[dialogues.Length];

        dialoguePanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        if (worldMapPanel != null) worldMapPanel.SetActive(false);

        // Turn off the invisible click-stealing button when the game starts
        if (advanceButton != null) advanceButton.gameObject.SetActive(false);

        // Exclamation marks: all hidden on start, shown by proximity
        if (exclamationMarks != null)
            foreach (var em in exclamationMarks)
                if (em != null) em.SetActive(false);
    }

    void Update()
    {
        // M key toggles world map
        if (Input.GetKeyDown(KeyCode.M)) ToggleMap();

        if (inDialogue || mapOpen) return;

        UpdatePlayer();
        UpdateExclamationMarks();
        CheckProximityTriggers();
        DetectClick();
    }

    // =========================================================================
    // World map toggle
    // =========================================================================

    void ToggleMap()
    {
        mapOpen = !mapOpen;
        if (worldMapPanel != null) worldMapPanel.SetActive(mapOpen);
        // pause dialogue panel while map is open
        if (mapOpen && inDialogue) dialoguePanel.SetActive(false);
    }

    public void CloseMap()
    {
        mapOpen = false;
        if (worldMapPanel != null) worldMapPanel.SetActive(false);
    }

    // =========================================================================
    // Player movement
    // =========================================================================

    void UpdatePlayer()
    {
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        if (input.sqrMagnitude > 0.01f)
        {
            moveTarget = null;
            if (input.magnitude > 1f) input.Normalize();
            Vector3 newPos = playerTransform.position
                + (Vector3)input * playerSpeed * Time.deltaTime;
            newPos.x = Mathf.Clamp(newPos.x, boundsX.x, boundsX.y);
            newPos.y = Mathf.Clamp(newPos.y, boundsY.x, boundsY.y);
            playerTransform.position = newPos;

            // Flip sprite to face direction of movement
            if (playerRenderer != null && Mathf.Abs(input.x) > 0.01f)
                playerRenderer.flipX = input.x < 0;
            return;
        }

        if (moveTarget.HasValue)
        {
            Vector3 toTarget = moveTarget.Value - playerTransform.position;
            float dist = toTarget.magnitude;
            if (dist < 0.05f) { playerTransform.position = moveTarget.Value; moveTarget = null; return; }
            Vector3 step = toTarget.normalized * playerSpeed * Time.deltaTime;
            if (step.magnitude > dist) step = toTarget;
            playerTransform.position += step;
            if (playerRenderer != null && Mathf.Abs(toTarget.x) > 0.01f)
                playerRenderer.flipX = toTarget.x < 0;
        }
    }

    // =========================================================================
    // Exclamation mark proximity
    // =========================================================================

    void UpdateExclamationMarks()
    {
        if (npcTransforms == null || exclamationMarks == null) return;
        float time = Time.time;

        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (i >= exclamationMarks.Length || exclamationMarks[i] == null) continue;
            if (npcTransforms[i] == null) continue;

            float dist = Vector3.Distance(playerTransform.position, npcTransforms[i].position);
            bool show = dist < exclamationProximity;
            exclamationMarks[i].SetActive(show);

            if (show)
            {
                // Bob up and down with a per-NPC phase offset
                float bob = Mathf.Sin(time * 4.5f + i * 1.1f) * 0.12f;
                exclamationMarks[i].transform.position = new Vector3(
                    npcTransforms[i].position.x,
                    npcTransforms[i].position.y + 1.2f + bob,
                    0f);
            }
        }
    }

    // =========================================================================
    // Auto-trigger dialogue when walking into NPC / door zone
    // =========================================================================

    void CheckProximityTriggers()
    {
        if (npcTransforms == null) return;
        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (npcTransforms[i] == null) continue;
            float dist = Vector3.Distance(playerTransform.position, npcTransforms[i].position);
            if (dist < interactRadius && !npcTriggered[i])
            {
                npcTriggered[i] = true;
                StartDialogue(i);
                return;
            }
            // Reset trigger once player moves away
            if (dist > interactRadius + 0.5f) npcTriggered[i] = false;
        }
    }

    // =========================================================================
    // Click detection — NPC or tap-to-walk
    // =========================================================================

    void DetectClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.IsPointerOverGameObject()) return;

        Vector2 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(wp);

        if (hit != null)
        {
            var marker = hit.GetComponent<NPCMarker>();
            if (marker != null)
            {
                moveTarget = null;
                StartDialogue(marker.npcIndex);
                return;
            }
        }

        moveTarget = new Vector3(
            Mathf.Clamp(wp.x, boundsX.x, boundsX.y),
            Mathf.Clamp(wp.y, boundsY.x, boundsY.y), 0f);
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

        // Turn the advance button back on so the player can click through the conversation
        if (advanceButton != null) advanceButton.gameObject.SetActive(true);

        // Hide this NPC's exclamation while talking
        if (exclamationMarks != null && npcIndex < exclamationMarks.Length
            && exclamationMarks[npcIndex] != null)
            exclamationMarks[npcIndex].SetActive(false);

        ShowCurrentLine();
    }

    void ShowCurrentLine()
    {
        var dlg = dialogues[currentNpcIndex];
        if (speakerNameText != null) speakerNameText.text = dlg.speakerName;
        if (dialogueText != null) dialogueText.text = dlg.lines[currentLineIndex];
        if (continueHint != null) continueHint.text = "Click to continue…";
    }

    public void AdvanceDialogue()
    {
        if (!inDialogue || waitingForChoice) return;
        currentLineIndex++;
        var dlg = dialogues[currentNpcIndex];
        if (currentLineIndex >= dlg.lines.Length) ShowChoice();
        else ShowCurrentLine();
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
            SceneManager.LoadScene(dlg.sceneToLoad);
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
        else EndDialogue();
    }

    IEnumerator EndAfterDelay(float d) { yield return new WaitForSeconds(d); EndDialogue(); }

    void EndDialogue()
    {
        dialoguePanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);

        // Turn the button back off once the conversation is over so you can walk around again
        if (advanceButton != null) advanceButton.gameObject.SetActive(false);

        inDialogue = false;
        currentNpcIndex = -1;
    }

    // =========================================================================
    // NPC content — World 1 all 5 scenarios
    // =========================================================================

    void InitializeDialogues()
    {
        dialogues = new NPCDialogue[]
        {
            new NPCDialogue
            {
                speakerName     = "Grandma Rose",
                npcDescription  = "Grandma's House",
                lines = new[]
                {
                    "Oh, hello dear! I'm so glad you stopped by.",
                    "I've been getting the most frightening emails lately. One says my bank account is frozen, and another claims the government wants to send me a refund — but I need to click a link first!",
                    "My friend Margaret clicked one of those links last year and lost her savings. I don't want that to happen to me.",
                    "Could you sit at my computer and help me figure out which emails are real and which are scams?"
                },
                acceptText      = "Of course, let's sort those emails!",
                declineText     = "Maybe later, Grandma",
                sceneToLoad     = "EmailSwiper",
                declineResponse = "Oh, alright dear. But please come back soon — my inbox keeps filling up!"
            },
            new NPCDialogue
            {
                speakerName     = "Uncle Tony",
                npcDescription  = "Pizza Shop",
                lines = new[]
                {
                    "Hey, come in! I was just about to call you actually.",
                    "The pizza shop computer has been getting these pop-ups and emails saying our system is infected with a virus.",
                    "They want me to call a number and give them remote access so they can 'fix' it. Seems fishy to me.",
                    "I run a business — I can't afford to get scammed. Can you take a look at the inbox and help me decide what's legit?"
                },
                acceptText      = "Let's check those emails, Tony!",
                declineText     = "Can't right now",
                sceneToLoad     = "EmailSwiper",
                declineResponse = "Okay, but hurry back. That tech support guy keeps calling!"
            },
            new NPCDialogue
            {
                speakerName     = "Grandpa Sal",
                npcDescription  = "Grandpa's Study",
                lines = new[]
                {
                    "Ah, just the person I wanted to see. Sit down, sit down.",
                    "I received an email that looks exactly like it's from my bank. The logo, the colours, everything.",
                    "But something feels off. My grandson always says if you look closely enough, fake emails have tiny mistakes.",
                    "I need a sharp pair of eyes. Can you compare this suspicious email to a real one and spot the differences?"
                },
                acceptText      = "I'll find those differences!",
                declineText     = "Not right now",
                sceneToLoad     = "SpotDifference",
                declineResponse = "Very well. But don't wait too long — identity theft is no joke, my friend."
            },
            new NPCDialogue
            {
                speakerName     = "Mrs. Patel",
                npcDescription  = "Mrs. Patel's House",
                lines = new[]
                {
                    "Oh good, you're here! I order packages online almost every day, so I get a LOT of delivery emails.",
                    "Lately some of them look like they're from Amazon or FedEx, but the links inside go somewhere strange.",
                    "My password manager keeps warning me about suspicious sites, but I keep almost clicking anyway!",
                    "Help me defend my account. These fake emails are coming fast and I need to block the weak passwords before they get through!"
                },
                acceptText      = "Let's defend that tower!",
                declineText     = "Maybe another time",
                sceneToLoad     = "TowerDefense",
                declineResponse = "Alright, but those phishers won't wait forever, dear!"
            },
            new NPCDialogue
            {
                speakerName     = "Uncle Rajan",
                npcDescription  = "Uncle Rajan's Place",
                lines = new[]
                {
                    "Ah, perfect timing! Come, come. I want to show you something.",
                    "I got a message saying it was from my nephew — said he was stranded abroad and needed money urgently.",
                    "I almost sent it! But then I noticed the writing style seemed different. Too formal, too panicked.",
                    "These scammers pretend to be family members in trouble. Can you look carefully and spot what gives them away?"
                },
                acceptText      = "I'll spot those red flags!",
                declineText     = "Not today",
                sceneToLoad     = "SpotDifference",
                declineResponse = "Okay. But share this with your family — anyone could fall for this one."
            }
        };
    }
}

/// <summary>
/// Placed on an NPC's collider GameObject so WorldMapManager knows
/// which dialogue to trigger on click.
/// </summary>
public class NPCMarker : MonoBehaviour
{
    public int npcIndex;
}