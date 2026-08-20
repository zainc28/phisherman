using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
//  PlayerPrefs keys used across the game
//  "interior_result"  : "win" | "lose" | ""   set by minigame
//  "completed_<scene>": 1  set when exiting after a win.
//                       KEYED ON THE INTERIOR SCENE NAME
//                       (e.g. "ApartmentInterior"), NOT the
//                       minigame scene name — multiple houses
//                       share the same minigame, so keying on
//                       the minigame would mark every house
//                       using it "complete" the moment any one
//                       of them was beaten.
// ============================================================

// ============================================================
//  Commentator  (unchanged)
// ============================================================
public class Commentator : MonoBehaviour
{
    [Header("Speaker")]
    public string speakerName = "Grandma";
    public Color portraitColor = new Color(1f, 0.72f, 0.78f);
    public string portraitInitial = "G";
    public Sprite portraitSprite;

    [Header("UI Refs")]
    public GameObject root;
    public Image portrait;
    public TMP_Text portraitLetter, bubbleText, speakerLabel;
    public Image bubbleBg;

    [Header("Timing")]
    public float defaultDuration = 2.6f;
    public float gapBetweenLines = 0.18f;

    readonly Queue<(string line, float dur)> _queue = new Queue<(string, float)>();
    Coroutine _routine;
    CanvasGroup _cg;

    void Awake()
    {
        if (root != null)
        {
            _cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            _cg.alpha = 0f; _cg.interactable = false; _cg.blocksRaycasts = false;
        }
        if (portrait != null && portraitSprite != null) portrait.sprite = portraitSprite;
        if (portrait != null) portrait.color = portraitColor;
        if (portraitLetter != null) portraitLetter.text = portraitInitial;
        if (speakerLabel != null) speakerLabel.text = speakerName;
    }

    public void SetSpeaker(string name, Sprite sprite = null, Color? color = null, string initial = null)
    {
        speakerName = name;
        if (speakerLabel != null) speakerLabel.text = name;
        if (sprite != null) { portraitSprite = sprite; if (portrait != null) portrait.sprite = sprite; }
        if (color.HasValue) { portraitColor = color.Value; if (portrait != null) portrait.color = color.Value; }
        if (initial != null) { portraitInitial = initial; if (portraitLetter != null) portraitLetter.text = initial; }
    }

    public void Say(string line, float duration = -1f)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        _queue.Enqueue((line, duration > 0 ? duration : defaultDuration));
        if (_routine == null) _routine = StartCoroutine(Loop());
    }
    public void SayRandom(string[] lines, float dur = -1f)
    { if (lines != null && lines.Length > 0) Say(lines[Random.Range(0, lines.Length)], dur); }
    public void Clear()
    { _queue.Clear(); if (_routine != null) StopCoroutine(_routine); _routine = null; if (_cg != null) _cg.alpha = 0f; }

    IEnumerator Loop()
    {
        while (_queue.Count > 0)
        {
            var (line, dur) = _queue.Dequeue();
            yield return Show(line, dur);
            if (_queue.Count > 0) yield return new WaitForSeconds(gapBetweenLines);
        }
        _routine = null;
    }

    IEnumerator Show(string line, float dur)
    {
        if (bubbleText != null) bubbleText.text = line;
        if (_cg != null) _cg.alpha = 1f;
        if (root != null)
        {
            root.transform.localScale = Vector3.one * 0.85f;
            float t = 0f;
            while (t < 0.18f) { t += Time.deltaTime; root.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.18f), 3f)); yield return null; }
            root.transform.localScale = Vector3.one;
        }
        yield return new WaitForSeconds(dur);
        if (_cg != null)
        {
            float t = 0f;
            while (t < 0.25f) { t += Time.deltaTime; _cg.alpha = 1f - Mathf.Clamp01(t / 0.25f); yield return null; }
            _cg.alpha = 0f;
        }
    }
}

// ============================================================
//  InteriorDialogueManager
//
//  STATE MACHINE:
//    Intro        → player taps through intro dialogue
//    Choice       → "Ready?" → play / back
//    PostWin      → player taps through win dialogue → Exit
//    PostLose     → "Try again?" → retry / give up (→ exit)
//
//  CROSS-SCENE COMMUNICATION (PlayerPrefs):
//    Write before loading minigame:
//      PlayerPrefs.SetString("interior_source", sceneName);
//    Minigame writes on result:
//      PlayerPrefs.SetString("interior_result", "win" | "lose");
//    On exit after win:
//      PlayerPrefs.SetInt("completed_" + <interior scene name>, 1);
//      → MapDoorCTA reads this to gray out the CTA label.
// ============================================================
public class InteriorDialogueManager : MonoBehaviour
{
    // ── Enums ─────────────────────────────────────────────────
    public enum Phase { Intro, Choice, PostWin, PostLose }

    // ── Dialogue line struct ──────────────────────────────────
    [System.Serializable]
    public struct DialogueLine
    {
        public string speaker;   // "NPC" or "Player"
        [TextArea(2, 5)] public string text;
    }

    // ── Inspector ─────────────────────────────────────────────
    [Header("Scenes")]
    public string minigameScene;
    public string returnScene = "WorldMap";

    [Header("Dialogue — Intro")]
    public DialogueLine[] introLines;

    [Header("Dialogue — Post Win")]
    public DialogueLine[] winLines;

    [Header("Dialogue — Post Lose")]
    public DialogueLine[] loseLines;

    [Header("NPC")]
    public string npcName = "NPC";
    public Sprite npcSprite;

    [Header("UI")]
    public GameObject dialoguePanel;
    public Button advanceButton;
    public TMP_Text speakerNameText, dialogueBodyText, continueHint;

    [Header("Choice panel (pre-minigame)")]
    public GameObject choicePanel;
    public Button playButton, backButton;

    [Header("Retry panel (post-lose)")]
    public GameObject retryPanel;
    public Button retryButton, giveUpButton;

    [Header("Portraits")]
    public Image npcPortrait, playerPortrait;
    public Sprite phishermanIdle;
    public Sprite[] phishermanTalkFrames;
    public float talkFps = 6f;

    [Header("Commentator (optional)")]
    public Commentator commentator;

    [Header("Visual")]
    public Color activeSpeakerColor = Color.white;
    public Color inactiveSpeakerColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    // ── State ─────────────────────────────────────────────────
    Phase _phase;
    int _lineIdx = -1;
    bool _waitingForInput;
    float _talkTimer;
    int _talkFrame;
    bool _playerTalking;
    DialogueLine[] _activeLines;

    // ── Lifecycle ─────────────────────────────────────────────
    void Start()
    {
        if (choicePanel != null) choicePanel.SetActive(false);
        if (retryPanel != null) retryPanel.SetActive(false);

        if (commentator != null)
            commentator.SetSpeaker(npcName, npcSprite, null,
                string.IsNullOrEmpty(npcName) ? "?" : npcName[0].ToString().ToUpper());

        string result = PlayerPrefs.GetString("interior_result", "");
        PlayerPrefs.DeleteKey("interior_result");

        if (result == "win")
            EnterPhase(Phase.PostWin);
        else if (result == "lose")
            EnterPhase(Phase.PostLose);
        else
            EnterPhase(Phase.Intro);
    }

    void Update()
    {
        if (_playerTalking && phishermanTalkFrames != null && phishermanTalkFrames.Length > 0)
        {
            _talkTimer += Time.deltaTime;
            if (_talkTimer >= 1f / talkFps)
            {
                _talkTimer -= 1f / talkFps;
                _talkFrame = (_talkFrame + 1) % phishermanTalkFrames.Length;
                if (playerPortrait != null) playerPortrait.sprite = phishermanTalkFrames[_talkFrame];
            }
        }
    }

    // ── Phase entry ───────────────────────────────────────────
    void EnterPhase(Phase p)
    {
        _phase = p;
        _lineIdx = -1;
        _waitingForInput = false;

        if (choicePanel != null) choicePanel.SetActive(false);
        if (retryPanel != null) retryPanel.SetActive(false);

        if (advanceButton != null) advanceButton.gameObject.SetActive(p != Phase.Choice && p != Phase.PostLose);

        switch (p)
        {
            case Phase.Intro:
                _activeLines = introLines;
                AdvanceLine();
                break;

            case Phase.PostWin:
                _activeLines = winLines;
                AdvanceLine();
                break;

            case Phase.PostLose:
                _activeLines = loseLines;
                AdvanceLine();
                break;

            case Phase.Choice:
                ShowChoicePanel();
                break;
        }
    }

    // ── Line advancement ──────────────────────────────────────
    public void AdvanceLine()
    {
        if (_waitingForInput) return;

        _lineIdx++;

        if (_activeLines == null || _lineIdx >= _activeLines.Length)
        {
            OnEndOfLines();
            return;
        }

        ShowLine(_activeLines[_lineIdx]);
    }

    void ShowLine(DialogueLine line)
    {
        bool isPlayer = line.speaker == "Player";

        if (speakerNameText != null) speakerNameText.text = isPlayer ? "Phisherman" : npcName;
        if (dialogueBodyText != null) dialogueBodyText.text = line.text;
        if (continueHint != null) continueHint.text = "Tap to continue...";

        if (npcPortrait != null) npcPortrait.color = isPlayer ? inactiveSpeakerColor : activeSpeakerColor;
        if (playerPortrait != null)
        {
            playerPortrait.color = isPlayer ? activeSpeakerColor : inactiveSpeakerColor;
            if (!isPlayer && phishermanIdle != null) playerPortrait.sprite = phishermanIdle;
        }

        _playerTalking = isPlayer;
        _talkTimer = 0f; _talkFrame = 0;
    }

    void OnEndOfLines()
    {
        _playerTalking = false;
        if (playerPortrait != null && phishermanIdle != null) playerPortrait.sprite = phishermanIdle;
        if (playerPortrait != null) playerPortrait.color = activeSpeakerColor;
        if (npcPortrait != null) npcPortrait.color = activeSpeakerColor;
        if (continueHint != null) continueHint.text = "";

        switch (_phase)
        {
            case Phase.Intro:
                EnterPhase(Phase.Choice);
                break;

            case Phase.PostWin:
                MarkComplete();
                GoBackToWorld();
                break;

            case Phase.PostLose:
                ShowRetryPanel();
                break;

            case Phase.Choice:
                break;
        }
    }

    // ── Choice panel ──────────────────────────────────────────
    void ShowChoicePanel()
    {
        if (speakerNameText != null) speakerNameText.text = npcName;
        if (dialogueBodyText != null) dialogueBodyText.text = "So... are you ready to help me out?";
        if (continueHint != null) continueHint.text = "";
        if (choicePanel != null) choicePanel.SetActive(true);
    }

    // ── CHANGED: set world theme before loading minigame ──────
    //
    //  GetWorldNumber() derives the world number from returnScene.
    //  returnScene for World 1 is "WorldMap", World 2 is "WorldMap2",
    //  etc. — this is already set by InteriorBuilder so no extra wiring
    //  is needed in the Inspector.
    // ─────────────────────────────────────────────────────────
    int GetWorldNumber()
    {
        if (returnScene.Contains("5")) return 5;
        if (returnScene.Contains("4")) return 4;
        if (returnScene.Contains("3")) return 3;
        if (returnScene.Contains("2")) return 2;
        return 1;
    }

    public void OnPlay()
    {
        if (choicePanel != null) choicePanel.SetActive(false);

        // Set world theme so the minigame knows which content to load
        MinigameTheme.Set(GetWorldNumber());

        PlayerPrefs.SetString("interior_source", SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();
        if (!string.IsNullOrEmpty(minigameScene))
            SceneManager.LoadScene(minigameScene);
    }

    public void OnBack()
    {
        SceneManager.LoadScene(returnScene);
    }

    // ── Retry panel ───────────────────────────────────────────
    void ShowRetryPanel()
    {
        if (speakerNameText != null) speakerNameText.text = npcName;
        if (dialogueBodyText != null) dialogueBodyText.text = "Want to give it another try?";
        if (continueHint != null) continueHint.text = "";
        if (retryPanel != null) retryPanel.SetActive(true);
    }

    public void OnRetry()
    {
        if (retryPanel != null) retryPanel.SetActive(false);

        // Set theme again on retry — same world, same content
        MinigameTheme.Set(GetWorldNumber());

        PlayerPrefs.SetString("interior_source", SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();
        if (!string.IsNullOrEmpty(minigameScene))
            SceneManager.LoadScene(minigameScene);
    }

    public void OnGiveUp()
    {
        if (retryPanel != null) retryPanel.SetActive(false);
        GoBackToWorld();
    }

    // ── Completion + exit ─────────────────────────────────────
    void MarkComplete()
    {
        string interiorScene = SceneManager.GetActiveScene().name;
        PlayerPrefs.SetInt("completed_" + interiorScene, 1);
        PlayerPrefs.Save();
    }

    void GoBackToWorld()
    {
        SceneManager.LoadScene(returnScene);
    }
}