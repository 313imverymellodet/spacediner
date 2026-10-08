mergeInto(LibraryManager.library, {
  // Rewarded ad. The page-level adapter (window.SD) picks Poki, CrazyGames, or a dev fallback.
  SD_Rewarded: function (goPtr) {
    var go = UTF8ToString(goPtr);
    var reply = function (ok) {
      try { window.unityInstance && window.unityInstance.SendMessage(go, "OnRewarded", ok ? "1" : "0"); } catch (e) {}
    };
    if (window.SD && window.SD.rewarded) window.SD.rewarded().then(function (ok) { reply(ok); }, function () { reply(false); });
    else reply(true);
  },
  SD_AdsAvailable: function () {
    return (window.SD && window.SD.adsAvailable && window.SD.adsAvailable()) ? 1 : 0;
  },
  SD_Gameplay: function (on) {
    if (window.SD && window.SD.gameplay) window.SD.gameplay(!!on);
  },
  SD_Event: function (namePtr, value) {
    if (window.SD && window.SD.track) window.SD.track(UTF8ToString(namePtr), value);
  },
  SD_Ready: function () {
    if (window.SD && window.SD.ready) window.SD.ready();
  },

  // synchronous localStorage mirror of the save (IndexedDB flushes are async and can be cut off by a tab close)
  SD_Vibrate: function (ms) { try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) {} },
  SD_SaveMirror: function (keyPtr, jsonPtr) { try { localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(jsonPtr)); } catch (e) {} },
  SD_LoadMirror: function (keyPtr) {
    var v = null;
    try { v = localStorage.getItem(UTF8ToString(keyPtr)); } catch (e) {}
    if (!v) return 0;
    var n = lengthBytesUTF8(v) + 1, buf = _malloc(n);
    stringToUTF8(v, buf, n);
    return buf;
  }
});
