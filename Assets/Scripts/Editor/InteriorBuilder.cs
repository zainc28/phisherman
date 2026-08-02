using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds every interior dialogue scene in the game.
/// Run via: Phisherman > Build Interior Scenes
///
/// Each scene has:
///   Intro dialogue → Choice (play / back)
///   Post-win dialogue → return to world (completion marked)
///   Post-lose dialogue → Retry panel (retry / give up)
///
/// WORLD 1 (existing, unchanged): ApartmentInterior, PizzaInterior, OfficeInterior
/// WORLD 3 (new): AuntCarolInterior, UncleMarcusInterior, GrandpaLouInterior
/// WORLD 4 (new): GrandmaIrisInterior, UncleFelixInterior, AuntDanaInterior
/// WORLD 5 (new): GrandpaErnestInterior, AuntPriyaInterior, UncleDiegoInterior
///
/// Each world spreads across all three minigame types (EmailSwiper,
/// TowerDefense, SpotDifference) and a different NPC sprite category
/// (aunt/uncle/grandma/grandpa) so nothing repeats back-to-back.
/// These scene names match WorldProgress.WorldRequirements exactly —
/// keep them in sync if you rename anything here.
/// </summary>
public static class InteriorBuilder
{
    [MenuItem("Phisherman/Build Interior Scenes")]
    public static void BuildAll()
    {
        // =====================================================================
        // WORLD 1  (unchanged)
        // =====================================================================

        // ── Apartment  (Grandma Rose → EmailSwiper) ───────────
        BuildInterior(
            sceneName: "ApartmentInterior",
            bgName: "home_1_interior",
            npcSpriteName: "grandmas_1",
            npcName: "Grandma Rose",
            minigame: "EmailSwiper",
            accent: Hex("#FF9F1C"),
            introLines: new[]
            {
                ("NPC",    "Oh dear, you're here! Come in, come in. I've been so worried."),
                ("Player", "What's going on, Grandma?"),
                ("NPC",    "I keep getting emails saying my bank account is frozen!"),
                ("Player", "That sounds like a phishing scam. Let me take a look."),
                ("NPC",    "Another one claims the government wants to send me a refund — but I need to click a link first."),
                ("Player", "Classic trick. They use urgency and fear so you act without thinking."),
                ("NPC",    "My friend Margaret clicked one of those links and lost her savings..."),
                ("Player", "Don't worry. I'll help you sort through these and spot every red flag."),
            },
            winLines: new[]
            {
                ("NPC",    "Oh my goodness! I never would have caught all of those on my own."),
                ("Player", "The key is to slow down. Scammers count on you panicking."),
                ("NPC",    "I'll remember that. Always check the sender's address first, right?"),
                ("Player", "Exactly. And never click links in emails — go to the website directly."),
                ("NPC",    "Thank you so much, dear. I feel much safer now!"),
            },
            loseLines: new[]
            {
                ("NPC",    "Oh no... that wasn't quite right, was it?"),
                ("Player", "It's harder than it looks — scammers are getting very convincing."),
                ("NPC",    "Could we try again? I really need to learn this."),
            }
        );

        // ── Pizza Place  (Uncle Tony → EmailSwiper) ───────────
        BuildInterior(
            sceneName: "PizzaInterior",
            bgName: "pizza_interior",
            npcSpriteName: "uncle_1",
            npcName: "Uncle Tony",
            minigame: "EmailSwiper",
            accent: Hex("#FF6B6B"),
            introLines: new[]
            {
                ("NPC",    "Hey, come in! I was just about to call you."),
                ("Player", "What's up, Tony? You look stressed."),
                ("NPC",    "The shop computer keeps getting pop-ups saying it's infected with a virus!"),
                ("Player", "Let me guess — they want you to call a number and give remote access?"),
                ("NPC",    "Exactly! They say they'll fix it for free. Seems fishy to me."),
                ("Player", "Good instinct. That's a tech support scam. Never give access to someone who contacts you first."),
                ("NPC",    "Can you check my inbox? There are some suspicious emails in there too."),
                ("Player", "Absolutely. Let's sort through them together."),
            },
            winLines: new[]
            {
                ("NPC",    "Wow, you spotted every single one! That's impressive."),
                ("Player", "Once you know the patterns, they're not hard to catch."),
                ("NPC",    "The fake sender addresses were a dead giveaway, right?"),
                ("Player", "Exactly. Real businesses never ask for login info by email."),
                ("NPC",    "I'm sharing this with my staff. Thanks a million!"),
            },
            loseLines: new[]
            {
                ("NPC",    "Hmm, a couple of those tripped you up, eh?"),
                ("Player", "They were pretty convincing. Want me to try again?"),
                ("NPC",    "Please! I need this place protected."),
            }
        );

        // ── Office  (Mrs. Patel → TowerDefense) ───────────────
        BuildInterior(
            sceneName: "OfficeInterior",
            bgName: "office_interior",
            npcSpriteName: "aunt_2",
            npcName: "Mrs. Patel",
            minigame: "TowerDefense",
            accent: Hex("#4ECDC4"),
            introLines: new[]
            {
                ("NPC",    "Oh good, you're here! I order packages online almost every day."),
                ("Player", "And you're getting fake delivery emails, aren't you?"),
                ("NPC",    "Yes! Some look exactly like Amazon or FedEx but the links go somewhere strange."),
                ("Player", "Scammers love impersonating delivery companies. It's very common."),
                ("NPC",    "My password manager warns me but I nearly clicked one this morning!"),
                ("Player", "That's exactly what they count on — a moment of habit."),
                ("NPC",    "Can you help me defend my inbox? They're coming so fast!"),
                ("Player", "Let's do it. I'll show you how to stop them before they get through."),
            },
            winLines: new[]
            {
                ("NPC",    "You blocked all of them! That was incredible to watch."),
                ("Player", "Strong, unique passwords are your first line of defence."),
                ("NPC",    "And I should never reuse them across sites, right?"),
                ("Player", "Never. Use a password manager — you already have one!"),
                ("NPC",    "Thank you. I feel like I can finally stop worrying."),
            },
            loseLines: new[]
            {
                ("NPC",    "Oh, they got through! What do I do?"),
                ("Player", "Don't panic. Want to try again? I'll explain what to watch for."),
                ("NPC",    "Yes please! I can't let them win."),
            }
        );

        // =====================================================================
        // WORLD 3  —  Urban Mobile Quarter
        // =====================================================================

        // ── Aunt Carol's flat  (SMS/email mix → EmailSwiper) ──
        BuildInterior(
            sceneName: "AuntCarolInterior",
            bgName: "home_1_interior",
            npcSpriteName: "aunt_1",
            npcName: "Aunt Carol",
            minigame: "EmailSwiper",
            accent: Hex("#2BB3A3"),
            introLines: new[]
            {
                ("NPC",    "There you are! My phone's been buzzing all day with strange messages."),
                ("Player", "Let me guess — texts pretending to be your mobile carrier?"),
                ("NPC",    "Yes! One says my bill failed and I need to 'update payment' right now."),
                ("Player", "Classic urgency trick. Real carriers don't threaten to cut you off in an hour."),
                ("NPC",    "There's a whole inbox of these. Can you help me sort the real from the fake?"),
                ("Player", "Of course. Let's go through them one by one."),
            },
            winLines: new[]
            {
                ("NPC",    "You caught every fake one! I feel silly for almost falling for a few."),
                ("Player", "Don't — these are designed to fool busy people. That's not silly, that's normal."),
                ("NPC",    "I'll double check the sender before I ever click again."),
                ("Player", "That's exactly the habit that keeps you safe."),
            },
            loseLines: new[]
            {
                ("NPC",    "Ooh, a few of those got past us."),
                ("Player", "They're sneaky ones. Let's give it another go."),
                ("NPC",    "Please — I want to get this right."),
            }
        );

        // ── Uncle Marcus's repair shop  (fake tech-support flood → TowerDefense) ──
        BuildInterior(
            sceneName: "UncleMarcusInterior",
            bgName: "pizza_interior",
            npcSpriteName: "uncle_2",
            npcName: "Uncle Marcus",
            minigame: "TowerDefense",
            accent: Hex("#FF9F1C"),
            introLines: new[]
            {
                ("NPC",    "Hey! Perfect timing — my shop's login page keeps getting hammered."),
                ("Player", "Hammered how? Are people trying to log in as you?"),
                ("NPC",    "Emails, texts, all pretending to be 'account security' asking me to confirm my password."),
                ("Player", "That's a credential-stuffing wave. We need to hold the line and only let the real logins through."),
                ("NPC",    "Can you help me tell strong logins from the fakes before they overwhelm me?"),
                ("Player", "Let's set up a defense — weak or suspicious ones get blocked, the real ones get through."),
            },
            winLines: new[]
            {
                ("NPC",    "You held them all off! My shop account is safe."),
                ("Player", "The trick is never reusing the same password across sites."),
                ("NPC",    "Noted. One breach anywhere shouldn't mean a breach everywhere."),
                ("Player", "Exactly right."),
            },
            loseLines: new[]
            {
                ("NPC",    "A few got through the gate..."),
                ("Player", "It happens. Let's tighten up and try again."),
                ("NPC",    "Let's do it — I don't want to lose this shop's account."),
            }
        );

        // ── Grandpa Lou's place  (compare real vs fake bank email → SpotDifference) ──
        BuildInterior(
            sceneName: "GrandpaLouInterior",
            bgName: "home_1_interior",
            npcSpriteName: "grandpas_1",
            npcName: "Grandpa Lou",
            minigame: "SpotDifference",
            accent: Hex("#FFD93D"),
            introLines: new[]
            {
                ("NPC",    "Ah, good, you're here. I've got two emails from my bank that look almost identical."),
                ("Player", "Almost identical is exactly how these scams work."),
                ("NPC",    "One must be fake, but I can't tell which — they both have the logo and everything."),
                ("Player", "Scammers copy the visuals perfectly. The tell is always in the small details."),
                ("NPC",    "Show me what to look for?"),
                ("Player", "Let's go through this one line at a time and find every red flag."),
            },
            winLines: new[]
            {
                ("NPC",    "You found every single difference! I never would have noticed the misspelled domain."),
                ("Player", "That's the one detail scammers hope you skip past."),
                ("NPC",    "I'll read much more carefully from now on."),
                ("Player", "That's all it takes."),
            },
            loseLines: new[]
            {
                ("NPC",    "I think we missed a couple of those..."),
                ("Player", "They hide them well. Let's look again."),
                ("NPC",    "Good — I want to really learn this one."),
            }
        );

        // =====================================================================
        // WORLD 4  —  Social Plaza
        // =====================================================================

        // ── Grandma Iris's booth  (impersonation flood → TowerDefense) ──
        BuildInterior(
            sceneName: "GrandmaIrisInterior",
            bgName: "home_1_interior",
            npcSpriteName: "grandmas_2",
            npcName: "Grandma Iris",
            minigame: "TowerDefense",
            accent: Hex("#2ECC71"),
            introLines: new[]
            {
                ("NPC",    "Oh, thank goodness. Someone's been sending messages pretending to be my grandchildren!"),
                ("Player", "That's an impersonation scam — usually asking for money urgently."),
                ("NPC",    "Yes! 'Grandma, I'm stranded, please send money' — over and over, from different accounts."),
                ("Player", "We need to hold them off and only let real messages from family through."),
                ("NPC",    "Can you help me tell my real family from the fakes?"),
                ("Player", "Let's build a defense — the impostors get stopped, the real ones get through safely."),
            },
            winLines: new[]
            {
                ("NPC",    "Not one impostor got through! I feel so much better."),
                ("Player", "Always call to verify before sending money — no exceptions."),
                ("NPC",    "I'll call my grandkids directly from now on, every time."),
                ("Player", "That one habit stops almost all of these scams cold."),
            },
            loseLines: new[]
            {
                ("NPC",    "Oh dear, a few impostors got through..."),
                ("Player", "It's alright — let's reinforce the defense and try again."),
                ("NPC",    "Please — I don't want to be fooled by one of these."),
            }
        );

        // ── Uncle Felix's stall  (compare fake vs real friend request → SpotDifference) ──
        BuildInterior(
            sceneName: "UncleFelixInterior",
            bgName: "office_interior",
            npcSpriteName: "uncle_3",
            npcName: "Uncle Felix",
            minigame: "SpotDifference",
            accent: Hex("#A29BFE"),
            introLines: new[]
            {
                ("NPC",    "Hey! I got two 'friend request confirmation' emails and they look the same to me."),
                ("Player", "One's probably a fake trying to get you to click through to a lookalike site."),
                ("NPC",    "How can they look so close to the real thing?"),
                ("Player", "They copy the layout pixel for pixel — but small details always give it away."),
                ("NPC",    "Walk me through spotting them?"),
                ("Player", "Let's compare these two side by side and find every mismatch."),
            },
            winLines: new[]
            {
                ("NPC",    "You caught every difference — even the tiny font change!"),
                ("Player", "Scammers rely on you scanning quickly instead of reading closely."),
                ("NPC",    "I'll slow down and actually read these from now on."),
                ("Player", "That's really all it takes."),
            },
            loseLines: new[]
            {
                ("NPC",    "I think we missed one or two..."),
                ("Player", "They're subtle. Let's take another pass."),
                ("NPC",    "Good — I want to get sharp at this."),
            }
        );

        // ── Aunt Dana's shop  (online shopping scam → EmailSwiper) ──
        BuildInterior(
            sceneName: "AuntDanaInterior",
            bgName: "home_1_interior",
            npcSpriteName: "aunt_2",
            npcName: "Aunt Dana",
            minigame: "EmailSwiper",
            accent: Hex("#FF6B6B"),
            introLines: new[]
            {
                ("NPC",    "Oh good, you're here! My inbox is flooded with order confirmations I don't recognize."),
                ("Player", "Sounds like fake shopping receipts — they usually want you to click a 'cancel order' link."),
                ("NPC",    "Yes, exactly! Some look like real stores I actually shop at."),
                ("Player", "That's what makes them dangerous — they borrow the branding of stores you trust."),
                ("NPC",    "Can you help me sort my real orders from the scams?"),
                ("Player", "Let's go through your inbox together."),
            },
            winLines: new[]
            {
                ("NPC",    "You sorted every single one correctly!"),
                ("Player", "The order numbers and links are always the giveaway."),
                ("NPC",    "I'll check my account directly instead of clicking email links from now on."),
                ("Player", "That's exactly the right instinct."),
            },
            loseLines: new[]
            {
                ("NPC",    "A couple of those fooled me..."),
                ("Player", "They're well made. Let's try that batch again."),
                ("NPC",    "Please — I order online too often not to know this."),
            }
        );

        // =====================================================================
        // WORLD 5  —  The Inner Island (final world)
        // =====================================================================

        // ── Grandpa Ernest's cabin  (sophisticated scam wave → EmailSwiper) ──
        BuildInterior(
            sceneName: "GrandpaErnestInterior",
            bgName: "office_interior",
            npcSpriteName: "grandpas_3",
            npcName: "Grandpa Ernest",
            minigame: "EmailSwiper",
            accent: Hex("#3498DB"),
            introLines: new[]
            {
                ("NPC",    "You made it all the way out here. These last emails are the trickiest I've seen."),
                ("Player", "The ones out here are polished — real logos, real-sounding language, everything."),
                ("NPC",    "Some don't even ask for money right away. They just want me to 'log in to verify'."),
                ("Player", "That's the newest trick — steal the login, then everything else follows."),
                ("NPC",    "I need your sharpest eye on this batch."),
                ("Player", "You've got it. Let's sort every last one."),
            },
            winLines: new[]
            {
                ("NPC",    "You caught every last one — even the ones I was sure were real!"),
                ("Player", "That's the whole game: never assume, always verify."),
                ("NPC",    "You've taught this old man well."),
                ("Player", "You taught yourself — I just pointed out what to look for."),
            },
            loseLines: new[]
            {
                ("NPC",    "These last ones are clever, aren't they?"),
                ("Player", "The best ones always are. Let's go again."),
                ("NPC",    "I'm ready when you are."),
            }
        );

        // ── Aunt Priya's office  (crypto investment scam compare → SpotDifference) ──
        BuildInterior(
            sceneName: "AuntPriyaInterior",
            bgName: "office_interior",
            npcSpriteName: "aunt_3",
            npcName: "Aunt Priya",
            minigame: "SpotDifference",
            accent: Hex("#1ABC9C"),
            introLines: new[]
            {
                ("NPC",    "Glad you're here. I've got two investment platform emails that look nearly identical."),
                ("Player", "One's likely a clone site trying to steal login details or deposits."),
                ("NPC",    "The returns promised in one look almost too good."),
                ("Player", "That's always a red flag on its own — but let's find every difference between these two."),
                ("NPC",    "Show me what real investment communication looks like versus a fake."),
                ("Player", "Let's compare them carefully, line by line."),
            },
            winLines: new[]
            {
                ("NPC",    "Every difference found — including that fake regulatory seal!"),
                ("Player", "If a return sounds guaranteed, it's almost never real."),
                ("NPC",    "I'll research any platform thoroughly before trusting it with money."),
                ("Player", "That habit alone will protect your savings."),
            },
            loseLines: new[]
            {
                ("NPC",    "A few of those slipped by us."),
                ("Player", "Investment scams are built to look legitimate. Let's try again."),
                ("NPC",    "Yes — this one matters too much to get wrong."),
            }
        );

        // ── Uncle Diego's harbor house  (final gauntlet → TowerDefense) ──
        BuildInterior(
            sceneName: "UncleDiegoInterior",
            bgName: "pizza_interior",
            npcSpriteName: "uncle_4",
            npcName: "Uncle Diego",
            minigame: "TowerDefense",
            accent: Hex("#E74C3C"),
            introLines: new[]
            {
                ("NPC",    "This is it — the last stretch. Every kind of scam you've seen is hitting me at once."),
                ("Player", "Weak passwords, impersonators, fake links — all in one wave?"),
                ("NPC",    "All of it. I need everything you've taught the others, right now."),
                ("Player", "Then let's put it all together and hold this line."),
                ("NPC",    "I'm ready. Let's finish this."),
                ("Player", "Together."),
            },
            winLines: new[]
            {
                ("NPC",    "We held every single wave! I don't think anything could get through now."),
                ("Player", "You've learned everything there is — weak passwords, impersonation, fake links, all of it."),
                ("NPC",    "Thanks to you, this harbor — and everyone in it — is safe."),
                ("Player", "You did the hard part. I just showed you where to look."),
            },
            loseLines: new[]
            {
                ("NPC",    "That was a lot all at once — a few got through."),
                ("Player", "It's the toughest wave yet. Let's regroup and try again."),
                ("NPC",    "Let's finish what we started."),
            }
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[InteriorBuilder] Built 12 interior scenes (World 1: 3, World 3: 3, World 4: 3, World 5: 3).");
    }

    // =========================================================================
    // Core builder
    // =========================================================================
    static void BuildInterior(
        string sceneName, string bgName, string npcSpriteName,
        string npcName, string minigame, Color accent,
        (string speaker, string text)[] introLines,
        (string speaker, string text)[] winLines,
        (string speaker, string text)[] loseLines)
    {
        string scenePath = "Assets/Scenes/" + sceneName + ".unity";
        if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite bgSpr = FindSprite(bgName);
        Sprite npcSpr = FindSprite(npcSpriteName);
        Sprite phSpr = FindSprite("phisherman");
        Sprite phTalk1 = FindSprite("phisherman_talking_1");
        Sprite phTalk2 = FindSprite("phisherman_talking_2");
        Sprite phTalk3 = FindSprite("phisherman_talking_3");

        LogFound(bgName, bgSpr); LogFound(npcSpriteName, npcSpr); LogFound("phisherman", phSpr);

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#1A1A2E");
        cam.orthographic = true; cam.orthographicSize = 4.5f;
        camGo.transform.position = new Vector3(0, 0, -10);

        // EventSystem
        var esGo = new GameObject("EventSystem"); esGo.AddComponent<EventSystem>(); esGo.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        // Background
        var bgImg = Img(canvasRT, "Background", bgSpr != null ? Color.white : Hex("#2A2438"));
        if (bgSpr != null) { bgImg.sprite = bgSpr; bgImg.preserveAspect = false; }
        Stretch(bgImg.rectTransform); bgImg.raycastTarget = false;

        // Dim
        var dim = Img(canvasRT, "Dim", new Color(0, 0, 0, 0.30f)); Stretch(dim.rectTransform); dim.raycastTarget = false;

        // NPC portrait (left)
        var npcImg = Img(canvasRT, "NPC_Portrait", Color.white);
        if (npcSpr != null) npcImg.sprite = npcSpr;
        npcImg.preserveAspect = true; npcImg.raycastTarget = false;
        var npcRT = npcImg.rectTransform;
        npcRT.anchorMin = new Vector2(0, 0); npcRT.anchorMax = new Vector2(0.35f, 0.85f);
        npcRT.offsetMin = new Vector2(20, 220); npcRT.offsetMax = new Vector2(-10, -20);

        // Phisherman portrait (right)
        var phImg = Img(canvasRT, "Player_Portrait", Color.white);
        if (phSpr != null) phImg.sprite = phSpr;
        phImg.preserveAspect = true; phImg.raycastTarget = false;
        var phRT = phImg.rectTransform;
        phRT.anchorMin = new Vector2(0.65f, 0); phRT.anchorMax = new Vector2(1, 0.85f);
        phRT.offsetMin = new Vector2(10, 220); phRT.offsetMax = new Vector2(-20, -20);

        // Dialogue panel (bottom 22%)
        var dp = Img(canvasRT, "DialoguePanel", new Color(0.06f, 0.08f, 0.16f, 0.94f));
        var dpRT = dp.rectTransform;
        dpRT.anchorMin = new Vector2(0, 0); dpRT.anchorMax = new Vector2(1, 0.22f); dpRT.offsetMin = dpRT.offsetMax = Vector2.zero;

        var dbord = Img(dpRT, "Border", accent); var dbrRT = dbord.rectTransform;
        dbrRT.anchorMin = new Vector2(0, 1); dbrRT.anchorMax = new Vector2(1, 1); dbrRT.pivot = new Vector2(0.5f, 1); dbrRT.sizeDelta = new Vector2(0, 4); dbord.raycastTarget = false;

        var nameBar = Img(dpRT, "NameBar", new Color(accent.r, accent.g, accent.b, 0.25f)); var nbRT = nameBar.rectTransform;
        nbRT.anchorMin = new Vector2(0, 1); nbRT.anchorMax = new Vector2(0.30f, 1); nbRT.pivot = new Vector2(0, 1); nbRT.sizeDelta = new Vector2(0, 44);
        var speakerTxt = Txt(nbRT, "SpeakerName", npcName, 28, accent, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Stretch(speakerTxt.rectTransform); speakerTxt.rectTransform.offsetMin = new Vector2(16, 0);

        var bodyTxt = Txt(dpRT, "BodyText", "...", 24, Color.white, TextAlignmentOptions.TopLeft);
        bodyTxt.textWrappingMode = TextWrappingModes.Normal;
        var btRT = bodyTxt.rectTransform; btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one; btRT.offsetMin = new Vector2(24, 40); btRT.offsetMax = new Vector2(-24, -52);

        var hintTxt = Txt(dpRT, "Hint", "Tap to continue...", 18, new Color(1, 1, 1, 0.45f), TextAlignmentOptions.MidlineRight);
        var htRT = hintTxt.rectTransform; htRT.anchorMin = new Vector2(0, 0); htRT.anchorMax = new Vector2(1, 0); htRT.pivot = new Vector2(0.5f, 0); htRT.sizeDelta = new Vector2(0, 32); htRT.anchoredPosition = new Vector2(0, 6);

        // Full-screen advance button
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(canvasRT, false); Stretch(advGo.GetComponent<RectTransform>());
        var advImg2 = advGo.AddComponent<Image>(); advImg2.color = new Color(0, 0, 0, 0); advImg2.raycastTarget = true;
        var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg2;

        // ── Choice panel (pre-minigame) ───────────────────────
        var choicePanel = MakeButtonPanel(dpRT, "ChoicePanel");
        var playBtn = MakeSmallButton(choicePanel.GetComponent<RectTransform>(), "PlayBtn", "Let's do it!", 26, Hex("#2ECC71"), Color.white, new Vector2(0, 0), new Vector2(0.48f, 1));
        var backBtn = MakeSmallButton(choicePanel.GetComponent<RectTransform>(), "BackBtn", "Maybe later", 26, Hex("#636E72"), Color.white, new Vector2(0.52f, 0), new Vector2(1, 1));

        // ── Retry panel (post-lose) ───────────────────────────
        var retryPanel = MakeButtonPanel(dpRT, "RetryPanel");
        var retryBtn = MakeSmallButton(retryPanel.GetComponent<RectTransform>(), "RetryBtn", "Try again!", 26, Hex("#FF9F1C"), Color.white, new Vector2(0, 0), new Vector2(0.48f, 1));
        var giveUpBtn = MakeSmallButton(retryPanel.GetComponent<RectTransform>(), "GiveUpBtn", "Give up", 26, Hex("#636E72"), Color.white, new Vector2(0.52f, 0), new Vector2(1, 1));

        // ── Manager ───────────────────────────────────────────
        var mgrGo = new GameObject("DialogueManager"); var mgr = mgrGo.AddComponent<InteriorDialogueManager>();
        mgr.npcName = npcName;
        mgr.npcSprite = npcSpr;
        mgr.minigameScene = minigame;
        mgr.returnScene = "WorldMap";

        mgr.dialoguePanel = dp.gameObject;
        mgr.speakerNameText = speakerTxt;
        mgr.dialogueBodyText = bodyTxt;
        mgr.continueHint = hintTxt;

        mgr.choicePanel = choicePanel;
        mgr.playButton = playBtn.GetComponent<Button>();
        mgr.backButton = backBtn.GetComponent<Button>();

        mgr.retryPanel = retryPanel;
        mgr.retryButton = retryBtn.GetComponent<Button>();
        mgr.giveUpButton = giveUpBtn.GetComponent<Button>();

        mgr.advanceButton = advBtn;
        mgr.npcPortrait = npcImg;
        mgr.playerPortrait = phImg;
        mgr.phishermanIdle = phSpr;
        mgr.activeSpeakerColor = Color.white;
        mgr.inactiveSpeakerColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);

        var talks = new System.Collections.Generic.List<Sprite>();
        if (phTalk1 != null) talks.Add(phTalk1); if (phTalk2 != null) talks.Add(phTalk2); if (phTalk3 != null) talks.Add(phTalk3);
        mgr.phishermanTalkFrames = talks.ToArray(); mgr.talkFps = 6f;

        // Dialogue arrays
        mgr.introLines = ToLines(introLines);
        mgr.winLines = ToLines(winLines);
        mgr.loseLines = ToLines(loseLines);

        // Wire all buttons
        UnityEventTools.AddPersistentListener(advBtn.onClick, mgr.AdvanceLine);
        UnityEventTools.AddPersistentListener(playBtn.GetComponent<Button>().onClick, mgr.OnPlay);
        UnityEventTools.AddPersistentListener(backBtn.GetComponent<Button>().onClick, mgr.OnBack);
        UnityEventTools.AddPersistentListener(retryBtn.GetComponent<Button>().onClick, mgr.OnRetry);
        UnityEventTools.AddPersistentListener(giveUpBtn.GetComponent<Button>().onClick, mgr.OnGiveUp);

        // Save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        AddToBuild(scenePath);
        Debug.Log($"[InteriorBuilder] Built → {scenePath}");
    }

    // ── Helpers ───────────────────────────────────────────────

    static InteriorDialogueManager.DialogueLine[] ToLines((string speaker, string text)[] raw)
    {
        var arr = new InteriorDialogueManager.DialogueLine[raw.Length];
        for (int i = 0; i < raw.Length; i++)
            arr[i] = new InteriorDialogueManager.DialogueLine { speaker = raw[i].speaker, text = raw[i].text };
        return arr;
    }

    /// Creates a horizontal button-pair container pinned to the bottom of the dialogue panel.
    static GameObject MakeButtonPanel(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(700, 60); rt.anchoredPosition = new Vector2(0, 8);
        go.SetActive(false);
        return go;
    }

    static GameObject MakeSmallButton(RectTransform parent, string name, string label, int fs, Color bg, Color tc, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>(); img.color = bg;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var t = Txt(go.transform, "Label", label, fs, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform);
        return go;
    }

    static Sprite FindSprite(string n) { foreach (var g in AssetDatabase.FindAssets(n + " t:Sprite")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).ToLower() == n.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; } } return null; }
    static void LogFound(string n, Sprite s) => Debug.Log($"[InteriorBuilder] {n}: " + (s != null ? "✓" : "✗"));
    static Image Img(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text Txt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}