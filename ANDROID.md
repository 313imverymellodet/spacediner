# Publishing Space Diner on Google Play

This repo builds both the browser game (`DinerBuild` → `dist/`) and the Android app (`AndroidBuild` → `android/`). The web page's portal ads (Poki / CrazyGames), the anonymous analytics beacon and the Sign in with Base badges live in the WebGL template and are not part of the app. In the app, monetization is `unity/Assets/Scripts/Ads.cs` (the same file as the other Mello Vibes games): **AdMob rewarded ads** behind Google's UMP consent form, plus a one-time **Remove Ads** purchase (`remove_ads`, $2.99) that works as an ad-free pass, so every reward is granted without a video. The pass is restored from Play after a reinstall. The rewarded offers are the game's existing ones: **2× cash for 2 minutes** (the gold boost button) and **2× offline earnings** (the WELCOME BACK popup). When no video is loaded, the boost falls back to the free, rarer 90-second version and the popup offers a plain collect. There are no interstitials or banners. **NO ADS · $2.99** and **PRIVACY** (EEA/UK only) are on the pause screen. The phone gets native pause, Android back-button handling, safe areas for notches, haptics, the share sheet, and offline earnings when you come back to the app.

| | |
|---|---|
| App name | Space Diner |
| Package name | `com.mellovibes.spacediner` (permanent once uploaded) |
| Min Android | 8.0 (API 26) |
| Target API | highest installed in Unity (Play needs the current level) |
| Architectures | ARM64 + ARMv7, IL2CPP |
| Orientation | Portrait |

## 1. One-time setup (Windows)

1. **Unity Hub → Installs → 6000.6.3f1 → ⚙ → Add modules → Android Build Support**, with **OpenJDK** and **Android SDK & NDK Tools** ticked.
2. **Create your upload key** (do this once and use it for all Mello Vibes apps). In PowerShell:

   ```powershell
   mkdir C:\keys
   & "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" -genkeypair -v -keystore C:\keys\mellovibes-upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000
   ```

   Pick a strong password and **back up the `.keystore` file and password** somewhere safe (a password manager plus a USB stick). Never commit it; `.gitignore` already blocks `*.keystore`. Google Play App Signing holds the real app signing key, so if you lose this upload key Google can reset it, but it takes days.

## 2. Build

From the repo root in PowerShell:

```powershell
$env:MV_KEYSTORE      = "C:\keys\mellovibes-upload.keystore"
$env:MV_KEYSTORE_PASS = "your-keystore-password"
$env:MV_KEY_ALIAS     = "upload"
$env:MV_KEY_PASS      = "your-keystore-password"
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -buildTarget Android -projectPath unity -executeMethod AndroidBuild.Release -versionCode 1 -quit -logFile android.log
```

- Output: `android\spacediner.aab`. Search `android.log` for `ANDROID BUILD OK`.
- **Every upload needs a higher `-versionCode`** (1, 2, 3…). Bump `Version` in `unity/Assets/Editor/AndroidBuild.cs` for user-visible versions (1.0.1…).
- In the editor you can also use the **Android** menu → *Build AAB for Google Play* or *Build test APK*.

### Test on your phone first

Turn on Developer options → USB debugging on the phone, plug it in, then:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -buildTarget Android -projectPath unity -executeMethod AndroidBuild.TestApk -quit -logFile android.log
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" install -r android\spacediner.apk
```

Check: the frame rate is smooth (60 Hz+), nothing is hidden under the notch or the gesture bar, the **II** button and the **back button** pause the game (back closes the upgrades panel first, and back on the pause screen goes to the home screen), switching apps pauses the game, sound and vibration toggles in the pause menu work, **Share my diner** opens the Android share sheet. Once AdMob has a video ready, the boost reads **AD: 2X CASH** and the WELCOME BACK popup offers **WATCH AD: COLLECT 2×**. Don't tap them on your own phone with the real IDs unless it's registered as a test device.

**Saves and offline earnings:** progress is saved every few seconds and whenever the app is backgrounded, so swiping the app away or Android killing it loses nothing. Leave it for 2+ minutes (home button, or swipe it away) and come back: a **WELCOME BACK** popup pays offline earnings (up to 3 hours).

## Ads and Remove Ads (before the first real release)

`Ads.cs` has Space Diner's real AdMob IDs (app `ca-app-pub-1440285360223827~3675397332`, rewarded unit `ca-app-pub-1440285360223827/8713643580`), and the build copies the app ID into the Google Mobile Ads settings. **Never tap your own ads** on a real-ID build: AdMob treats it as invalid traffic. To test the ad flow, add your phone as a test device in AdMob (Settings → Test devices). A new ad unit can take up to an hour to start serving, so "no fill" right after setup is normal. If an `Ads.cs` with Google's test IDs is ever back, `AndroidBuild.Release` refuses it unless you pass `-testAds`.

1. AdMob app and rewarded unit: done (IDs above). Link the AdMob app to the Play listing once it's published.
2. AdMob → Privacy & messaging → create a **GDPR** message (and a US states message) for the app, otherwise the consent form never shows.
3. **Play Console → Monetize → Products → In-app products** → create `remove_ads`, non-consumable ("Remove Ads", $2.99). It only appears in the app after a build has been uploaded to a testing track and the tester installed it from Play.

## 3. Play Console

Create the app at <https://play.google.com/console> → **Create app** → name `Space Diner`, Game, Free.

**Store listing** (everything is in `store/`):

- App name: `Space Diner: Idle Burger Chef`
- Short description: `Grill, serve and grow a burger diner on the Moon, then launch it to Mars!`
- Full description: `store/listing.md`
- App icon: `store/icon-512.png` · Feature graphic: `store/feature-graphic.png`
- Phone screenshots: `store/screenshots/*.png` (1080×1920)
- Category: **Simulation** · Tags: Idle, Simulation, Casual, Offline, Single player, Stylized
- Contact email: your email · Website: `https://play.mellovibes.io`

**App content** (Policy → App content):

| Section | Answer |
|---|---|
| Privacy policy | `https://play.mellovibes.io/privacy` |
| Ads | **Yes** (AdMob rewarded ads; no interstitials or banners) |
| App access | All functionality is available without special access |
| Content rating (IARC questionnaire) | Everyone (no violence, no gambling, no user interaction) |
| Target audience | 13 and over (simplest; avoids the Families program paperwork) |
| Data safety | Advertising ID, approximate location, app interactions and diagnostics are **collected and shared with Google AdMob for advertising**. Encrypted in transit. No account creation. Saves stay on the device. |
| Government / financial / health / news | No |

**Release:**

1. *Testing → Internal testing* → create a release → upload `spacediner.aab` → add yourself as a tester → install from the opt-in link.
2. **New personal developer accounts must run a closed test with at least 12 testers for 14 days** before production. Start this early: *Testing → Closed testing* with friends' Gmail addresses.
3. Then *Production → Create release*.
