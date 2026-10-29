# Reply to App Review: Guideline 2.1, Information Needed

Apple asks new developer accounts for a device recording plus five answers. Paste the **Reply** block into the
rejection message in App Store Connect (App Review → the 1.0 submission → Messages → Reply) and attach the recording.
Paste the same text into **App Review Information → Notes** on the 1.0 version page, so future submissions have it.

## The screen recording (do this on your iPhone)
1. Update the iPhone to the latest iOS (Settings → General → Software Update). Apple asks for the latest OS.
2. Install build 1.0.0 (11) from **TestFlight**. If Milky Space Diner is already on the phone, delete it first so the
   recording shows a first launch.
3. Add **Screen Recording** to Control Center (Settings → Control Center) if it isn't there. Start a recording, then
   go to the home screen.
4. Record this flow, about 2 to 3 minutes:
   1. Tap the app icon on the home screen (the recording must start with the launch).
   2. The launch screen, then the tracking prompt (tap either answer) and, if it appears, Google's consent form.
   3. The title screen: tap **SETTINGS**, toggle sound and music, show **RESTORE PURCHASES**, close it.
   4. Tap **NO ADS** to show Apple's purchase sheet. TestFlight purchases are free sandbox purchases, so you can buy
      it to show the paid feature working: the NO ADS button disappears and video bonuses become free. If you'd
      rather not buy, cancel the sheet.
   5. Tap **PLAY** and follow the tutorial: grill, carry the stack to the counter, serve the queue, collect cash.
   6. Stand on a build pad to unlock something, and use **TOSS** once.
   7. Tap the gear icon (top right) to show the pause menu, then resume.
5. Stop the recording. It saves to Photos. If the file is too big to attach, trim it in Photos, or AirDrop it to the
   Mac and attach it from there.

## Reply

```
Hello, and thank you for reviewing Milky Space Diner.

1. Screen recording
The attached recording was captured on an iPhone running the latest iOS with build 1.0.0 (11) from TestFlight. It starts at the home screen with a first launch and shows the tracking prompt, the title screen, Settings (sound, music, vibration, Restore Purchases), the Remove Ads in-app purchase, and the main gameplay loop. The app has no accounts, no login and no user-generated content.

2. Purpose and target audience
Milky Space Diner is a casual, single-player idle-arcade game for everyone (rated 4+). The player runs a burger diner on the Moon: grill food, carry it to the counter, serve a queue of space travelers, collect the cash and spend it on build pads that grow the diner. Once the diner is fully built it launches by rocket to Mars and then Europa, each with a new menu. The value is a relaxing, short-session game that plays offline, with a daily reward streak and offline earnings to come back to.

3. How to access the main features
No account, login or sample files are needed. Launch the app and tap PLAY on the title screen. A guided tutorial with an on-screen arrow walks through the whole loop: drag anywhere to move, stand on the grill to pick up food, walk to the counter (or hold TOSS to throw the stack), stand at the register to serve, then walk over the cash to collect it. Stand on a build pad to spend money and unlock tables, machines and staff.
- Remove Ads (non-consumable in-app purchase, product ID remove_ads): the NO ADS button on the title screen, and in the pause menu (gear icon, top right during play).
- Restore Purchases: Settings on the title screen, and the pause menu.
- Daily reward: appears once a day after the tutorial is finished.
- Rewarded videos (optional): the 2X CASH button, and the "collect 2X" choices on the daily reward and the welcome-back card.

4. External services
- Google AdMob (Google Mobile Ads SDK), including Google's User Messaging Platform consent form: optional rewarded video ads.
- Apple In-App Purchase (StoreKit), managed through RevenueCat: the Remove Ads purchase and restoring it.
- Apple App Tracking Transparency: asks permission before ads may use the advertising identifier.
- Local notifications (Apple UserNotifications): optional reminders scheduled on the device. There is no server or push service.
The game itself runs fully on the device and stores progress locally. It uses no AI services, login or authentication services, or data providers.

5. Regional differences
The app works the same in every region. The only differences are that Google's consent form is shown to players in regions that require it (such as the EEA, the UK and Switzerland), the Remove Ads price is shown in the local currency set by Apple, and how often rewarded videos are available depends on Google's ad inventory in that country.

6. Regulated industry and third-party material
The app is a game and is not in a regulated industry. It contains no protected third-party material: the 3D models and sound effects are from Kenney (kenney.nl), released under the CC0 public-domain license, and the music was created for this game.

Thank you,
Mello Vibes Games
```
