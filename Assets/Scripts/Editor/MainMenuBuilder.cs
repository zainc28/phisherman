using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEditor.Events;

public class MainMenuBuilder : EditorWindow
{
    [MenuItem("Tools/Build Phisherman Main Menu")]
    public static void BuildMenu()
    {
        // 1. Find the specific background Sprite
        Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/phisherman_menu_fish_v1.png");
        if (backgroundSprite == null)
        {
            backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/phisherman_menu_fish_v1.jpg");
        }

        if (backgroundSprite == null)
        {
            Debug.LogError("Could not find the background sprite. Ensure it is named exactly 'phisherman_menu_fish_v1' and is a PNG or JPG in Assets/Sprites/");
            return;
        }

        // 2. Create the UI Canvas
        GameObject canvasObj = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 3. Create Background Image
        GameObject bgObj = new GameObject("Background", typeof(Image));
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.GetComponent<Image>();
        bgImage.sprite = backgroundSprite;

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 4. Create Generated Text Logo (No external image needed)
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(canvasObj.transform, false);

        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.text = "THE ADVENTURES OF\n<size=150%>PHISHERMAN</size>";
        titleText.fontSize = 80;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1f, 0.8f, 0.2f); // Gold color
        titleText.fontStyle = FontStyles.Bold;

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -100);
        titleRect.sizeDelta = new Vector2(1200, 300);

        // 5. Create MenuManager and attach logic safely
        GameObject managerObj = new GameObject("MenuManager");
        MainMenuManager menuManager = managerObj.AddComponent<MainMenuManager>();

        if (menuManager == null)
        {
            Debug.LogError("Could not attach MainMenuManager. Make sure the file is named exactly MainMenuManager.cs and has no compile errors.");
            return;
        }

        // 6. Create Buttons and Wire Events
        GameObject storyBtnObj = CreateTextButton("Button_StoryMode", "Story Mode", canvasObj.transform, new Vector2(0, -50));
        Button storyBtn = storyBtnObj.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(storyBtn.onClick, menuManager.OnStoryModeClicked);

        GameObject arcadeBtnObj = CreateTextButton("Button_ArcadeMode", "Arcade Mode", canvasObj.transform, new Vector2(0, -130));
        Button arcadeBtn = arcadeBtnObj.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(arcadeBtn.onClick, menuManager.OnArcadeModeClicked);

        Undo.RegisterCreatedObjectUndo(canvasObj, "Build Main Menu Canvas");
        Undo.RegisterCreatedObjectUndo(managerObj, "Build Menu Manager");

        Debug.Log("🎉 Main Menu successfully built with generated text logo!");
    }

    private static GameObject CreateTextButton(string name, string labelText, Transform parent, Vector2 anchoredPos)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0, 0, 0, 0); // Hide default button background

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 80);
        rect.anchoredPosition = anchoredPos;

        GameObject textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = 40;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.75f, 0.2f);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return btnObj;
    }
}