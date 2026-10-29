# Publishing Space Diner on the App Store

The iOS app is the same game as the Android one: AdMob rewarded **2× cash** and **2× offline earnings** (after Google's consent form and Apple's tracking prompt), a one-time **Remove Ads** pass with a **Restore** button on the pause screen, pause when you leave the app, offline earnings when you come back, haptics and the iOS share sheet. Unity on Windows exports an Xcode project; a Mac signs, archives and uploads it.

| | |
|---|---|
| App name | Space Diner |
| Bundle ID | `com.mellovibes.spacediner` (permanent once uploaded) |
| Minimum iOS | 15.0, iPhone and iPad (portrait, full screen) |
| In-app purchase | `remove_ads`, non-consumable, $2.99 |

## 1. One-time Apple setup

1. **developer.apple.com → Certificates, IDs & Profiles → Identifiers → +** → App IDs → App → Bundle ID (explicit) `com.mellovibes.spacediner`. Leave capabilities at the defaults (In-App Purchase is on by default).
2. **App Store Connect → Apps → + New App**: iOS, name `Space Diner`, the bundle ID above, SKU `spacediner`.
3. In the app: **Monetization → In-App Purchases → +** → Non-Consumable, reference name `Remove Ads`, product ID `remove_ads`, price $2.99, display name "Remove Ads", description "Get every reward instantly, no videos.", plus a review screenshot of the pause screen's NO ADS button. Sign the **Paid Apps agreement** (Business) first, or the product never loads.
4. **AdMob → Apps → Add app → iOS** (not listed yet) → name Space Diner → add a **Rewarded** ad unit. Paste the iOS app ID and unit ID into `IosAppId` / `IosRewardedId` in `unity/Assets/Scripts/Ads.cs` (they're Google's test IDs until then, and the export refuses them without `-testAds`).
5. Your **Team ID** (developer.apple.com → Membership, 10 characters) goes in `MV_APPLE_TEAM` so the export turns on automatic signing. It isn't secret.
6. **RevenueCat** (this game buys and restores through it, with the `appl_…` key in `Ads.cs`). Without these, NO ADS and RESTORE show a "Purchase failed" / "Restore failed" alert with RevenueCat's error code:
   1. **App Store Connect → Users and Access → Integrations → In-App Purchase → +**: generate a key and download the `.p8` (once only; keep it out of git). Note its **Key ID** and the **Issuer ID** shown above the list.
   2. **app.revenuecat.com → the project → Apps & providers → the App Store app**: bundle ID `com.mellovibes.spacediner`; upload the `.p8` under **In-app purchase key configuration** with the Key ID and Issuer ID, then save.
   3. **Product catalog → Products → + New**: the App Store app, identifier `remove_ads`.
   4. **Product catalog → Entitlements → + New**: identifier `no_ads`, then **Attach** `remove_ads`.
   5. **Product catalog → Offerings → + New**: identifier `default` with one package holding `remove_ads` (the game doesn't read offerings, but RevenueCat's health check wants one).
   6. Check from a terminal: every check in this report should say `passed`:
      `curl -s -H "Authorization: Bearer <appl_ key>" https://api.revenuecat.com/v1/subscribers/health_check/health_report`

## 2. Export the Xcode project (Windows)

```powershell
$env:MV_APPLE_TEAM = "ABCDE12345"
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -buildTarget iOS -projectPath unity -executeMethod IosBuild.Export -buildNumber 1 -quit -logFile ios.log
```

- Output: `ios\spacediner\`. Search `ios.log` for `IOS EXPORT OK`.
- `-testAds` exports a build that still uses Google's test ad IDs (fine for TestFlight, not for review).
- Every upload needs a higher `-buildNumber`. Bump `Version` in `IosBuild.cs` for user-visible versions.
- Copy the whole `ios\spacediner` folder to the Mac (zip it; iCloud Drive, AirDrop or a USB stick).

## 3. Build and upload (Mac)

One-time: install **Xcode** from the App Store, sign in with your Apple ID (Xcode → Settings → Accounts), and install CocoaPods: `brew install cocoapods` (or `sudo gem install cocoapods`).

```bash
cd ~/Downloads/spacediner        # the copied ios/spacediner folder
pod install                 # fetches the Google Mobile Ads and consent SDKs
open Unity-iPhone.xcworkspace   # the .xcworkspace, not the .xcodeproj
```

In Xcode:

1. Select the **Unity-iPhone** target → **Signing & Capabilities** → Team = your team, "Automatically manage signing" on. Do the same for **UnityFramework** if it asks.
2. Pick **Any iOS Device (arm64)** as the destination, then **Product → Archive**.
3. In the Organizer: **Distribute App → App Store Connect → Upload**.
4. After processing (10–30 min) the build shows in App Store Connect → **TestFlight**. Add yourself as an internal tester and install it through the TestFlight app.

## 4. Check on an iPhone (TestFlight)

The camera keeps the chef on screen; nothing sits under the notch or the home bar; **II** pauses and switching apps pauses; haptics and **Share my diner** work; "Allow tracking?" shows once on first launch (after Google's consent form in the EEA/UK); **AD: 2X CASH** plays a video and starts the boost; leaving for 3+ minutes brings a WELCOME BACK payout; NO ADS (pause screen) buys the pass with a Sandbox account and RESTORE brings it back after reinstalling. Progress saves on the phone; quitting from the app switcher loses nothing.

## 5. App Store Connect before review

| Section | Answer |
|---|---|
| Privacy Policy URL | `https://play.mellovibes.io/privacy` |
| App Privacy → Data collection | **Yes**. Collected by the AdMob SDK: **Identifiers → Device ID**, **Location → Coarse location**, **Usage data → Advertising data, Product interaction**, **Diagnostics → Crash data, Performance data**. Purposes: Third-party advertising, Analytics. **Linked to the user: No. Used for tracking: Yes** (Device ID, Advertising data) |
| Age rating | **4+** is likely (no violence, no mature themes); declare in-app purchases and that the app shows ads |
| Category | Games → Simulation (secondary: Casual) |
| Screenshots | 6.9" iPhone (1320×2868) and 13" iPad (2064×2752) are required; capture from TestFlight on device or the simulator (the 1080×1920 Play screenshots in `store/` don't fit these sizes) |
| Export compliance | Answered in the build (`ITSAppUsesNonExemptEncryption = NO`) |

Then **Add for Review** with the build, the `remove_ads` in-app purchase attached to the version, and submit. After approval, link the app in AdMob (**Apps → app → Add store**) so full ad serving starts.
