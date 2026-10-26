// Rewarded ads for the Google Play build. A stub for now: no ad network is wired in yet.
// AdFree is the "Remove Ads" pass: rewards are granted without watching.
// The game only uses these three members; the real AdMob + IAP version replaces this file.
public static class Ads
{
    public static bool Available => false;
    public static bool AdFree => false;

    public static void ShowRewarded(System.Action<bool> done) => done?.Invoke(AdFree);
}
