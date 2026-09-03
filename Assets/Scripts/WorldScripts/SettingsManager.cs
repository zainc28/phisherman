using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  SettingsManager  (placeholder — wire sliders/toggles later)
// ============================================================
public class SettingsManager : MonoBehaviour
{
    [Header("Volume sliders (assign in Inspector)")]
    public Slider masterVolume;
    public Slider musicVolume;
    public Slider sfxVolume;

    [Header("Accessibility toggles (assign in Inspector)")]
    public Toggle largeTextToggle;
    public Toggle highContrastToggle;

    const string KeyMaster = "vol_master";
    const string KeyMusic = "vol_music";
    const string KeySfx = "vol_sfx";
    const string KeyLargeText = "acc_largetext";
    const string KeyHighContrast = "acc_highcontrast";

    void Start()
    {
        if (masterVolume != null) { masterVolume.value = PlayerPrefs.GetFloat(KeyMaster, 1f); masterVolume.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat(KeyMaster, v); }); }
        if (musicVolume != null) { musicVolume.value = PlayerPrefs.GetFloat(KeyMusic, 0.8f); musicVolume.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(KeyMusic, v)); }
        if (sfxVolume != null) { sfxVolume.value = PlayerPrefs.GetFloat(KeySfx, 1f); sfxVolume.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(KeySfx, v)); }
        if (largeTextToggle != null) { largeTextToggle.isOn = PlayerPrefs.GetInt(KeyLargeText, 0) == 1; largeTextToggle.onValueChanged.AddListener(v => PlayerPrefs.SetInt(KeyLargeText, v ? 1 : 0)); }
        if (highContrastToggle != null) { highContrastToggle.isOn = PlayerPrefs.GetInt(KeyHighContrast, 0) == 1; highContrastToggle.onValueChanged.AddListener(v => PlayerPrefs.SetInt(KeyHighContrast, v ? 1 : 0)); }
        AudioListener.volume = PlayerPrefs.GetFloat(KeyMaster, 1f);
    }

    public void ResetToDefaults()
    {
        if (masterVolume != null) masterVolume.value = 1f;
        if (musicVolume != null) musicVolume.value = 0.8f;
        if (sfxVolume != null) sfxVolume.value = 1f;
        if (largeTextToggle != null) largeTextToggle.isOn = false;
        if (highContrastToggle != null) highContrastToggle.isOn = false;
        PlayerPrefs.DeleteKey(KeyMaster); PlayerPrefs.DeleteKey(KeyMusic); PlayerPrefs.DeleteKey(KeySfx);
        PlayerPrefs.DeleteKey(KeyLargeText); PlayerPrefs.DeleteKey(KeyHighContrast);
    }
}
