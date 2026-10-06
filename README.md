# SPACE DINER

A cozy idle-arcade game built in Unity 6 for mobile browsers.

## The hook: ZERO-G TOSS
It's low gravity on the Moon, so you don't have to walk your food to the counter: you can **fling it**.
- Carry food anywhere from 2.4 to 15 m from the counter and the **TOSS** button appears (Space on keyboards).
- **Hold** it: a power meter sweeps, and a floaty arc and a landing ring preview the throw. The ring turns green, and the button says **NOW!**, when it's on the counter.
- **Let go**: the whole stack sails over, tumbling in slow motion, and lands in the counter's stock.
- **Long tosses (5 m+) earn a tip**, and dead-centre bullseyes double it.
- **Misses** drift around the floor in the low gravity until you walk over them (they're gone after 40 s).
- The tutorial's counter step teaches it, and "HOLD TOSS!" coach marks pop up the first few times.
- Analytics: `toss_hit`, `toss_miss`, `toss_long`, `toss_bullseye` (value = distance in metres).
- Code: `Toss.cs` (aim, flyers, drifting misses, tips); `UI.SetToss` and `TossHold` for the button.

You run a burger joint on the Moon. Grill the food, carry stacks of it to the counter, serve the queue of space travelers, collect your cash, clean their tables, and spend your money on stand-on unlock pads to grow the diner. Once the Moon diner is fully built, you launch it to **Mars** and then to **Europa**. Each planet has a new menu and pays 4× more.

## Features
- **Core loop:** grill → carry (a wobbly stack) → counter → register → cash → build. Customers queue, order 1–4 items, sit, eat and leave dirty plates.
- **13 unlocks per planet:** tables, a second grill, a fryer/oven, drink machines, an upgrade terminal, and three hovering alien staff (cashier, runner, cleaner). The last unlock is the rocket launch.
- **Upgrades:** walk speed, carry capacity, profit and staff speed.
- **3 planets, 9 foods:** burgers, fries, soda (Moon) · hot dogs, pizza, ice cream (Mars) · tacos, donuts, coffee (Europa). After Europa the planets loop at higher multipliers.
- **Monetization hooks:**
  - a rewarded ad for *2× cash for 2 minutes*
  - a rewarded ad to *double offline earnings*
  - a page-level ad adapter that auto-detects the **Poki** or **CrazyGames** SDK (see `WebGLTemplates/SpaceDiner/index.html`)
- **Onboarding and retention:**
  - a guided tutorial with a pointer arrow that clamps to the screen edge when the target is off-screen
  - offline earnings of up to 3 hours
  - autosave to IndexedDB
- **Audio:** fully procedural sound effects and a lo-fi music loop. There are no audio files.
- **Size:** about 4.8 MB total, using a 4 MB Brotli WebGL build.

Art: [Kenney](https://kenney.nl) Food Kit, Mini Characters, Furniture Kit and Space Kit, all CC0. Crediting Kenney is appreciated but not required.

## Layout
```
unity/Assets/Scripts/
  Game.cs      world layout, planets, unlock progression, customers, tutorial, save, camera, launch
  Stations.cs  floor zones, producers, counter/register/queue, cash pile, tables, trash, pads, terminal
  Actors.cs    player (joystick + carry animation + autoplay), customers, alien staff bots
  ItemStack.cs arcing stack transfers (the genre's core juice), Items.cs item defs + pooling
  UI.cs        HUD, floating joystick, world-pinned bubbles, objective arrow, upgrade shop, dialogs
  Kit.cs       Kenney model spawner (normalizes pivots/scale), procedural textures
  Sfx.cs, WebBridge.cs + Plugins/WebGL/SpaceDiner.jslib
unity/Assets/Editor/  DinerBuild.cs (one-command build), KenneyImport.cs (import rules)
dist/          deployable build (Vercel serves this)
kenney/        the original asset packs
art/           share-card and icon sources
```

## Build
```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -projectPath unity -executeMethod DinerBuild.WebGL -quit -logFile build.log
```

## Run locally
```bash
node tools/serve.mjs 8081
```

### Test URL flags
| Flag | Effect |
|---|---|
| `?bot=1` | Autoplay: the chef plays using the A* nav grid. Works on the live site, for trailer and TikTok capture. |
| `?dev=1&fresh=1` | Start from a clean save. |
| `?dev=1&speed=4` | Fast-forward (1–8×). |
| `?dev=1&cheat=1` | Press **M** for +$1000 (scaled to the current planet). |
| `?dev=1&debug=1` | Log world object bounds to the console. |

Everything except `bot` requires `dev=1`, so players can't stumble into cheats.

**Ads:** rewarded ads only switch on when the Poki or CrazyGames SDK is on the page. Anywhere else, including Vercel, the 2× Cash button is labeled **FREE**, uses a longer cooldown, and the offline popup offers only a normal collect.

## Deploy
**Live at https://spacediner.vercel.app.** To redeploy, rebuild, then run `npx vercel deploy --prod` from this folder. `.vercelignore` makes sure only `dist/`, `tools/` and `vercel.json` are uploaded.

You can also import the repo in Vercel. The root `vercel.json` serves `dist/` with Brotli headers and adds your production URL to the share-card tags. For Poki or CrazyGames, upload the contents of `dist/`. Their SDK script is picked up automatically when it's present on the page.
