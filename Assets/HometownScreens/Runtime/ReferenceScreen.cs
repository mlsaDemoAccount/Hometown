using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Hometown.Screens
{
    public enum ScreenKind { AudioSettings, DemolishConfirmation, NewMemory, OceanParkMemories, MemoryDiary, LevelUp }
    public class ReferenceScreen : MonoBehaviour
    {
        public ScreenKind kind;
        public AudioClip clickSound;
        public Text title, subtitle, status, levelText;
        public InputField description;
        public Slider generalVolume, musicVolume, sfxVolume;
        public Image photoPreview;
        public UnityEvent onAccept = new UnityEvent(), onCollect = new UnityEvent(), onCaptureRequested = new UnityEvent(), onUploadRequested = new UnityEvent(), onHome = new UnityEvent(), onCategories = new UnityEvent();
        public UnityEvent<string> onMemorySaved = new UnityEvent<string>();
        public int level = 5;
        public Text[] memoryLabels;
        public ReferenceScreen[] destinations;
        AudioSource audioSource;
        bool collected;
        int diaryIndex;
        const string MemoryKey = "Hometown.Screens.Memory";
        void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
            if (generalVolume) { generalVolume.SetValueWithoutNotify(PlayerPrefs.GetFloat("Hometown.Master", .8f)); generalVolume.onValueChanged.AddListener(SetMaster); }
            if (musicVolume) { musicVolume.SetValueWithoutNotify(PlayerPrefs.GetFloat("Hometown.Music", .9f)); musicVolume.onValueChanged.AddListener(SetMusic); }
            if (sfxVolume) { sfxVolume.SetValueWithoutNotify(PlayerPrefs.GetFloat("Hometown.Sfx", .9f)); sfxVolume.onValueChanged.AddListener(SetSfx); }
            if (kind == ScreenKind.AudioSettings) { SetMaster(generalVolume.value); SetMusic(musicVolume.value); SetSfx(sfxVolume.value); }
        }
        void OnEnable()
        {
            collected = false;
            if (levelText) levelText.text = level.ToString();
            if (kind == ScreenKind.OceanParkMemories && PlayerPrefs.HasKey(MemoryKey) && memoryLabels.Length > 0)
                memoryLabels[0].text = PlayerPrefs.GetString(MemoryKey);
            if (status) status.text = "";
        }
        public void PlayClick()
        {
            if (SoundManager.Instance) SoundManager.Instance.PlayButtonSound();
            else if (clickSound) AudioSource.PlayClipAtPoint(clickSound, Camera.main ? Camera.main.transform.position : Vector3.zero, PlayerPrefs.GetFloat("Hometown.Sfx", .9f));
        }
        public void Close() { GetComponent<ScreenMotion>().Close(); }
        public void Go(int index)
        {
            if (destinations != null && index >= 0 && index < destinations.Length && destinations[index]) { destinations[index].gameObject.SetActive(true); Close(); }
            else if (status) status.text = "Connect this screen in the Destinations list.";
        }
        public void Audio() { Go(0); }
        public void NewMemory() { Go(2); }
        public void Memories() { Go(3); }
        public void Diary() { Go(4); }
        public void Home() { onHome.Invoke(); Close(); }
        public void Categories() { onCategories.Invoke(); }
        public void Accept() { onAccept.Invoke(); Close(); }
        public void Collect() { if (collected) return; collected = true; onCollect.Invoke(); Close(); }
        public void Capture() { onCaptureRequested.Invoke(); if (status) status.text = "Photo capture requested."; }
        public void Upload() { onUploadRequested.Invoke(); if (status) status.text = "Photo selection requested."; }
        public void SetPhoto(Sprite photo) { if (photoPreview) { photoPreview.sprite = photo; photoPreview.color = Color.white; } }
        public void SaveMemory()
        {
            string text = description ? description.text.Trim() : "";
            if (string.IsNullOrEmpty(text)) { if (status) status.text = "Add a description before saving."; return; }
            PlayerPrefs.SetString(MemoryKey, text); PlayerPrefs.Save(); onMemorySaved.Invoke(text); Memories();
        }
        public void NextPage() { diaryIndex = 1-diaryIndex; if (subtitle) subtitle.text = diaryIndex == 0 ? "I go with your mom in 2015\nWe are very happy" : PlayerPrefs.GetString(MemoryKey, "A day together at Ocean Park.\nA memory worth keeping."); }
        public void SetMaster(float value) { AudioListener.volume = value; PlayerPrefs.SetFloat("Hometown.Master", value); UpdateValue(generalVolume,value); }
        public void SetMusic(float value) { if (SoundManager.Instance) SoundManager.Instance.SetMusicVolume(value); PlayerPrefs.SetFloat("Hometown.Music",value); UpdateValue(musicVolume,value); }
        public void SetSfx(float value) { if (SoundManager.Instance) SoundManager.Instance.SetSFXVolume(value); PlayerPrefs.SetFloat("Hometown.Sfx",value); UpdateValue(sfxVolume,value); }
        void UpdateValue(Slider slider, float value) { if (!slider) return; var label = slider.transform.Find("Value"); if (label) label.GetComponent<Text>().text = Mathf.RoundToInt(value*100) + "%"; }
        void OnDisable() { if (kind == ScreenKind.AudioSettings) PlayerPrefs.Save(); }
    }
}
