using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音视频配置与播放；BGM/音效挂载于全局 AudioRoot。
/// </summary>
public class GameDataMgr
{
    private sealed class OneShotAudioClipRelease : MonoBehaviour
    {
        public AudioClip clip;

        private void OnDestroy()
        {
            if (clip != null)
            {
                AddressablesMgr.Instance.Release(clip);
                clip = null;
            }
        }
    }

    private static readonly GameDataMgr instance = new();
    public static GameDataMgr Instance => instance;

    public AudioData audioData;
    public VideoData videoData;

    private List<AudioSource> musicList = new();
    private List<AudioSource> soundList = new();

    private static GameObject audioRoot;

    /// <summary>
    /// DontDestroy 音频根节点。
    /// </summary>
    private static GameObject AudioRoot
    {
        get
        {
            if (audioRoot == null)
            {
                audioRoot = new GameObject("AudioRoot");
                Object.DontDestroyOnLoad(audioRoot);
            }
            return audioRoot;
        }
    }

    /// <summary>
    /// 从 Json 加载配置。
    /// </summary>
    private GameDataMgr()
    {
        audioData = JsonMgr.Instance.LoadData<AudioData>("AudioData");
        videoData = JsonMgr.Instance.LoadData<VideoData>("VideoData");
    }

    /// <summary>
    /// 持久化显示设置。
    /// </summary>
    public void SaveVideoData()
    {
        JsonMgr.Instance.SaveData(videoData, "VideoData");
    }

    /// <summary>
    /// 应用分辨率、帧率、画质。
    /// </summary>
    public void ApplyVideoSettings()
    {
        int w = videoData.resolutionIndex == 0 ? 2560 : 1920;
        int h = videoData.resolutionIndex == 0 ? 1440 : 1080;
        bool fullscreen = videoData.displayModeIndex == 0;
        Screen.SetResolution(w, h, fullscreen);

        switch (videoData.frameRateIndex)
        {
            case 0: Application.targetFrameRate = 120; break;
            case 1: Application.targetFrameRate = 90; break;
            case 2: Application.targetFrameRate = 60; break;
            default: Application.targetFrameRate = -1; break;
        }

        QualitySettings.SetQualityLevel(videoData.qualityIndex);
    }

    /// <summary>
    /// 持久化音频设置。
    /// </summary>
    public void SaveAudioData()
    {
        JsonMgr.Instance.SaveData(audioData, "AudioData");
    }

    /// <summary>
    /// 通道：音乐 / 音效。
    /// </summary>
    public enum AudioType
    {
        Music,
        Sound
    }

    /// <summary>
    /// 播放 BGM。
    /// </summary>
    public void PlayMusic(string res)
    {
        PlayAudio(AudioType.Music, res);
    }

    /// <summary>
    /// 播放音效。
    /// </summary>
    public void PlaySound(string res)
    {
        PlayAudio(AudioType.Sound, res);
    }

    /// <summary>
    /// 加载并播放；BGM 会停掉其它 BGM，音效播完销毁物体并 Release 句柄。
    /// </summary>
    public void PlayAudio(AudioType type, string soundRes)
    {
        AddressablesMgr.Instance.LoadAsset<AudioClip>(soundRes, (obj) =>
        {
            AudioClip clip = obj as AudioClip;
            if (clip == null)
            {
                return;
            }

            GameObject soundObj = new GameObject($"Audio_{soundRes}");
            soundObj.transform.SetParent(AudioRoot.transform);

            AudioSource source = soundObj.AddComponent<AudioSource>();
            source.clip = clip;

            if (type == AudioType.Music)
            {
                StopAllMusic();

                musicList.Add(source);
                source.volume = audioData.musicVolume;
                source.mute = !audioData.music;
                source.loop = true;
            }
            else
            {
                soundList.Add(source);
                source.volume = audioData.soundVolume;
                source.mute = !audioData.sound;
                source.loop = false;
            }

            source.Play();

            if (type == AudioType.Sound)
            {
                OneShotAudioClipRelease releaseHook = soundObj.AddComponent<OneShotAudioClipRelease>();
                releaseHook.clip = clip;
                Object.Destroy(soundObj, clip.length);
            }
        });
    }

    /// <summary>
    /// 销毁全部 BGM 并 Release 其 Clip。
    /// </summary>
    private void StopAllMusic()
    {
        foreach (var m in musicList)
        {
            if (m != null)
            {
                if (m.clip != null)
                {
                    AddressablesMgr.Instance.Release(m.clip);
                }
                Object.Destroy(m.gameObject);
            }
        }
        musicList.Clear();
    }

    /// <summary>
    /// 按配置刷新音量/静音并剔除已销毁引用。
    /// </summary>
    public void RefreshAudio()
    {
        musicList.RemoveAll(m => m == null);
        soundList.RemoveAll(s => s == null);

        foreach (AudioSource music in musicList)
        {
            music.volume = audioData.musicVolume;
            music.mute = !audioData.music;
        }

        foreach (AudioSource sound in soundList)
        {
            sound.volume = audioData.soundVolume;
            sound.mute = !audioData.sound;
        }
    }
}
