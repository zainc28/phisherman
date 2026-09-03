using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
//  WorldMapManager
//
//  FIX (map toggle bug):
//  The previous version called ToggleMap() and then immediately
//  checked `if (mapOpen && ...)` in the same frame. After ToggleMap()
//  sets mapOpen = true, the second condition was also true and called
//  CloseMap() — so the map opened and closed in the exact same frame,
//  appearing to never open.
//
//  Fix: capture the key state ONCE at the top of Update() into a bool
//  (`pressedM`), use it for the toggle, and never check mapOpen again
//  in the same expression that set it.
//
//  FIX (works in all 5 worlds):
//  WorldMapManager is a single shared class. Dropping this file in
//  fixes Worlds 1-5 simultaneously. Each world builder assigns
//  worldMapPanel at build time via WireManager / the World1 builder —
//  that wiring is unchanged.
// ============================================================
public class WorldMapManager : MonoBehaviour
{
    [System.Serializable]
    public struct NPCDialogue
    {
        public string speakerName, npcDescription;
        [TextArea(2, 5)] public string[] lines;
        public string acceptText, declineText, sceneToLoad, declineResponse;
    }

    [Header("Player")]
    public Transform playerTransform; public SpriteRenderer playerRenderer;
    public float playerSpeed = 3.5f;
    public Vector2 boundsX = new Vector2(-8f, 8f), boundsY = new Vector2(-3.8f, 1.2f);
    MapNavAgent _playerNav;

    [Header("NPCs")]
    public Transform[] npcTransforms; public GameObject[] exclamationMarks;
    public float exclamationProximity = 3.5f, interactRadius = 1.2f;

    [Header("Dialogue UI")]
    public GameObject dialoguePanel; public Button advanceButton;
    public TMP_Text speakerNameText, dialogueText, continueHint;
    public GameObject choicePanel; public Button acceptButton, declineButton;
    public TMP_Text acceptButtonText, declineButtonText;

    [Header("World Map Overlay")]
    public GameObject worldMapPanel;

    NPCDialogue[] dialogues;
    bool inDialogue, mapOpen;
    int currentNpcIndex = -1, currentLineIndex;
    bool waitingForChoice;
    Vector3? moveTarget;
    bool[] npcTriggered;

    void Start()
    {
        InitializeDialogues();
        npcTriggered = new bool[dialogues.Length];
        if (dialoguePanel) dialoguePanel.SetActive(false);
        if (choicePanel) choicePanel.SetActive(false);
        if (worldMapPanel) worldMapPanel.SetActive(false);
        // Ensure the full-screen advance button never blocks the map hint click at start
        if (advanceButton) advanceButton.gameObject.SetActive(false);
        if (exclamationMarks != null) foreach (var em in exclamationMarks) if (em) em.SetActive(false);

        if (playerTransform) _playerNav = playerTransform.GetComponent<MapNavAgent>();
        if (!_playerNav)
        {
            var pg = GameObject.FindWithTag("Player");
            if (pg) { _playerNav = pg.GetComponent<MapNavAgent>(); if (!playerTransform) playerTransform = pg.transform; if (!playerRenderer) playerRenderer = pg.GetComponent<SpriteRenderer>(); }
        }
    }

    void Update()
    {
        // ── Capture input once — never re-read after acting on it ────────
        bool pressedM = Input.GetKeyDown(KeyCode.M);
        bool pressedEsc = Input.GetKeyDown(KeyCode.Escape);

        // M toggles the map regardless of dialogue state
        if (pressedM)
            ToggleMap();

        // Escape closes the map (if open) — checked separately so a single
        // M press doesn't both open and then immediately close via this block
        if (pressedEsc && mapOpen)
            CloseMap();

        // Gameplay blocked while map is open or during dialogue
        if (inDialogue || mapOpen) return;
        if (!playerTransform) return;

        UpdatePlayer();
        UpdateExclamationMarks();
        CheckProximityTriggers();
        DetectClick();
    }

    // ── Map ───────────────────────────────────────────────────
    public void ToggleMap()
    {
        mapOpen = !mapOpen;
        if (worldMapPanel) worldMapPanel.SetActive(mapOpen);
    }

    public void CloseMap()
    {
        mapOpen = false;
        if (worldMapPanel) worldMapPanel.SetActive(false);
    }

    // ── Player ────────────────────────────────────────────────
    void UpdatePlayer()
    {
        float h = Input.GetAxisRaw("Horizontal"), v = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(h, v);
        if (input.sqrMagnitude > 0.01f)
        {
            moveTarget = null; if (_playerNav != null && _playerNav.IsNavigating) _playerNav.CancelPath();
            if (input.magnitude > 1f) input.Normalize();
            Vector3 np = playerTransform.position + (Vector3)input * playerSpeed * Time.deltaTime;
            np.x = Mathf.Clamp(np.x, boundsX.x, boundsX.y); np.y = Mathf.Clamp(np.y, boundsY.x, boundsY.y);
            playerTransform.position = np;
            if (playerRenderer && Mathf.Abs(input.x) > 0.01f) playerRenderer.flipX = input.x < 0;
            return;
        }
        if (moveTarget.HasValue)
        {
            if (_playerNav != null) { moveTarget = null; return; }
            Vector3 to = moveTarget.Value - playerTransform.position; float dist = to.magnitude;
            if (dist < 0.05f) { playerTransform.position = moveTarget.Value; moveTarget = null; return; }
            Vector3 step = to.normalized * playerSpeed * Time.deltaTime; if (step.magnitude > dist) step = to;
            playerTransform.position += step;
            if (playerRenderer && Mathf.Abs(to.x) > 0.01f) playerRenderer.flipX = to.x < 0;
        }
    }

    void UpdateExclamationMarks()
    {
        if (npcTransforms == null || exclamationMarks == null || !playerTransform) return;
        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (i >= exclamationMarks.Length || !exclamationMarks[i] || !npcTransforms[i]) continue;
            float dist = Vector3.Distance(playerTransform.position, npcTransforms[i].position);
            bool show = dist < exclamationProximity; exclamationMarks[i].SetActive(show);
            if (show) { float bob = Mathf.Sin(Time.time * 4.5f + i * 1.1f) * 0.12f; exclamationMarks[i].transform.position = new Vector3(npcTransforms[i].position.x, npcTransforms[i].position.y + 1.2f + bob, 0f); }
        }
    }

    void CheckProximityTriggers()
    {
        if (npcTransforms == null || !playerTransform) return;
        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (!npcTransforms[i]) continue;
            float dist = Vector3.Distance(playerTransform.position, npcTransforms[i].position);
            if (dist < interactRadius && !npcTriggered[i]) { npcTriggered[i] = true; StartDialogue(i); return; }
            if (dist > interactRadius + 0.5f) npcTriggered[i] = false;
        }
    }

    void DetectClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.IsPointerOverGameObject()) return;
        if (!Camera.main) return;
        Vector2 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(wp);
        if (hit) { var m = hit.GetComponent<NPCMarker>(); if (m) { moveTarget = null; StartDialogue(m.npcIndex); return; } }
        if (_playerNav != null) { _playerNav.RequestPath(new Vector3(wp.x, wp.y, playerTransform.position.z)); moveTarget = null; }
        else moveTarget = new Vector3(Mathf.Clamp(wp.x, boundsX.x, boundsX.y), Mathf.Clamp(wp.y, boundsY.x, boundsY.y), 0f);
    }

    // ── Dialogue ──────────────────────────────────────────────
    public void StartDialogue(int idx)
    {
        if (dialogues == null || idx < 0 || idx >= dialogues.Length) return;
        inDialogue = true; currentNpcIndex = idx; currentLineIndex = 0; waitingForChoice = false;
        if (dialoguePanel) dialoguePanel.SetActive(true);
        if (choicePanel) choicePanel.SetActive(false);
        if (advanceButton) advanceButton.gameObject.SetActive(true);
        if (exclamationMarks != null && idx < exclamationMarks.Length && exclamationMarks[idx]) exclamationMarks[idx].SetActive(false);
        ShowCurrentLine();
    }

    void ShowCurrentLine()
    {
        var dlg = dialogues[currentNpcIndex];
        if (speakerNameText) speakerNameText.text = dlg.speakerName;
        if (dialogueText) dialogueText.text = dlg.lines[currentLineIndex];
        if (continueHint) continueHint.text = "Click to continue...";
    }

    public void AdvanceDialogue()
    {
        if (!inDialogue || waitingForChoice) return;
        currentLineIndex++;
        var dlg = dialogues[currentNpcIndex];
        if (currentLineIndex >= dlg.lines.Length) ShowChoice(); else ShowCurrentLine();
    }

    void ShowChoice()
    {
        var dlg = dialogues[currentNpcIndex]; waitingForChoice = true;
        if (choicePanel) choicePanel.SetActive(true);
        if (acceptButtonText) acceptButtonText.text = dlg.acceptText;
        if (declineButtonText) declineButtonText.text = dlg.declineText;
        if (continueHint) continueHint.text = "";
    }

    public void OnAcceptHelp()
    {
        if (!inDialogue) return;
        var dlg = dialogues[currentNpcIndex];
        if (!string.IsNullOrEmpty(dlg.sceneToLoad)) SceneManager.LoadScene(dlg.sceneToLoad);
    }

    public void OnDeclineHelp()
    {
        if (!inDialogue) return;
        var dlg = dialogues[currentNpcIndex];
        if (choicePanel) choicePanel.SetActive(false); waitingForChoice = false;
        if (!string.IsNullOrEmpty(dlg.declineResponse)) { if (dialogueText) dialogueText.text = dlg.declineResponse; StartCoroutine(EndAfterDelay(2f)); }
        else EndDialogue();
    }

    IEnumerator EndAfterDelay(float d) { yield return new WaitForSeconds(d); EndDialogue(); }

    void EndDialogue()
    {
        if (dialoguePanel) dialoguePanel.SetActive(false);
        if (choicePanel) choicePanel.SetActive(false);
        if (advanceButton) advanceButton.gameObject.SetActive(false);
        inDialogue = false; currentNpcIndex = -1;
    }

    void InitializeDialogues()
    {
        dialogues = new NPCDialogue[]
        {
            new NPCDialogue { speakerName="Grandma Rose", lines=new[]{"Oh, hello dear! I'm so glad you stopped by.","I've been getting frightening emails saying my bank account is frozen!","Could you help me figure out which emails are real and which are scams?"}, acceptText="Let's sort those emails!", declineText="Maybe later", sceneToLoad="ApartmentInterior", declineResponse="Oh alright dear. But please come back soon!" },
            new NPCDialogue { speakerName="Uncle Tony",   lines=new[]{"Hey, come in! I was just about to call you.","The shop computer keeps getting pop-ups saying it's infected with a virus.","They want remote access to 'fix' it. Seems fishy to me."}, acceptText="Let's check those emails!", declineText="Can't right now", sceneToLoad="PizzaInterior", declineResponse="Okay, but hurry back!" },
            new NPCDialogue { speakerName="Grandpa Sal",  lines=new[]{"Ah, just the person I wanted to see.","I got an email that looks exactly like it's from my bank.","Can you spot the differences between this and a real one?"}, acceptText="I'll find those differences!", declineText="Not right now", sceneToLoad="SpotDifference", declineResponse="Very well. But don't wait too long." },
            new NPCDialogue { speakerName="Mrs. Patel",   lines=new[]{"Oh good, you're here! I order packages online every day.","Some delivery emails look like Amazon or FedEx but the links go somewhere strange.","Help me defend my account — these fake emails are coming fast!"}, acceptText="Let's defend that tower!", declineText="Maybe another time", sceneToLoad="TowerDefense", declineResponse="Alright, but those phishers won't wait!" },
            new NPCDialogue { speakerName="Uncle Rajan",  lines=new[]{"Ah, perfect timing! I got a message saying it was from my nephew.","He said he was stranded abroad and needed money urgently.","Can you look carefully and spot what gives them away?"}, acceptText="I'll spot those red flags!", declineText="Not today", sceneToLoad="SpotDifference", declineResponse="Okay. Share this with your family." },
        };
    }
}
