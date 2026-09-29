using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Talks to the page: rewarded ads (Poki / CrazyGames / dev fallback), gameplay events, analytics.
public class WebBridge : MonoBehaviour
{
    public static WebBridge I;
    Action<bool> pending;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void SD_Rewarded(string goName);
    [DllImport("__Internal")] static extern void SD_Gameplay(int on);
    [DllImport("__Internal")] static extern void SD_Event(string name, int value);
    [DllImport("__Internal")] static extern void SD_Ready();
    [DllImport("__Internal")] static extern int SD_AdsAvailable();
    [DllImport("__Internal")] static extern void SD_SaveMirror(string key, string json);
    [DllImport("__Internal")] static extern string SD_LoadMirror(string key);
#endif

    void Awake() { I = this; gameObject.name = "WebBridge"; }

    public static bool AdsAvailable
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SD_AdsAvailable() == 1;
#else
            return true;
#endif
        }
    }

    public void ShowRewarded(Action<bool> done)
    {
        pending = done;
        Sfx.I.SetMuted(true);
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Rewarded(gameObject.name);
#else
        OnRewarded("1");
#endif
    }

    // Called from JS via SendMessage.
    public void OnRewarded(string ok)
    {
        Sfx.I.SetMuted(Game.I.Save.muted);
        var cb = pending; pending = null;
        cb?.Invoke(ok == "1");
    }

    public static void Gameplay(bool on)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Gameplay(on ? 1 : 0);
#endif
    }

    public static void Event(string name, int value = 0)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Event(name, value);
#endif
    }

    public static void SaveMirror(string key, string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_SaveMirror(key, json);
#endif
    }

    public static string LoadMirror(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return SD_LoadMirror(key);
#else
        return null;
#endif
    }

    public static void Ready()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Ready();
#endif
    }
}
