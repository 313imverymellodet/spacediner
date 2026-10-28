using System;
using UnityEngine;
using UnityEngine.UI;

// Daily reward card: seven days in a row, today's tile glows gold, claimed days turn green, day 7 is the big one.
public partial class UI
{
    // day: 0-6 (today's slot in the streak). claim(true) means the player watched the ad for double.
    public void OpenDaily(int day, double[] amounts, string bonus, bool adOffer, Action<bool> claim)
    {
        var card = Modal(adOffer ? 1180 : 1040);
        var t = Txt(card, "DAILY REWARD", 64, new Vector2(.5f, 1), new Vector2(0, -88), Color.white); Shadow(t, 3);
        Txt(card, day == 0 ? "Come back every day to grow your streak" : "DAY " + (day + 1) + " STREAK · keep it going tomorrow", 30, new Vector2(.5f, 1), new Vector2(0, -160), Soft, TextAnchor.MiddleCenter, 860);

        RectTransform todayTile = null;
        for (int i = 0; i < 7; i++)
        {
            bool big = i == 6;
            var pos = big ? new Vector2(0, -760) : new Vector2(-280 + i % 3 * 280, -300 - i / 3 * 220);
            var size = big ? new Vector2(820, 170) : new Vector2(250, 190);
            Color fill = i < day ? Kit.A(Green, 0.32f) : i == day ? Gold : new Color(1, 1, 1, 0.08f);
            Color ink = i == day ? Ink : Color.white;
            var tile = PanelBox(card, new Vector2(.5f, 1), pos, size, i == day ? Color.white : Kit.A(Violet, 0.5f), 1f, fill);
            Txt(tile, "DAY " + (i + 1), 28, new Vector2(.5f, .5f), new Vector2(0, big ? 44 : 52), Kit.A(ink, 0.8f));
            var amt = Txt(tile, Kit.Money(amounts[i]), big ? 52 : 44, new Vector2(.5f, .5f), new Vector2(0, big ? 0 : 0), ink, TextAnchor.MiddleCenter, size.x);
            if (i == day) Shadow(amt, 2);
            if (big) Txt(tile, bonus, 26, new Vector2(.5f, .5f), new Vector2(0, -46), Kit.A(ink, 0.85f), TextAnchor.MiddleCenter, 760);
            else if (i < day) Txt(tile, "CLAIMED", 24, new Vector2(.5f, .5f), new Vector2(0, -54), Kit.A(Green, 1f));
            if (i == day) todayTile = tile;
        }
        if (todayTile) StartCoroutine(Pulse(todayTile));

        float y = 110;
        if (adOffer) Button(card, (Mobile.Native && Ads.AdFree ? "FREE: " : "WATCH AD: ") + "CLAIM 2× (" + Kit.Money(amounts[day] * 2) + ")", new Vector2(.5f, 0), new Vector2(0, y + 150), new Vector2(760, 120), Gold, Ink, () => { CloseModal(true); claim(true); }, out _);
        Button(card, "CLAIM " + Kit.Money(amounts[day]), new Vector2(.5f, 0), new Vector2(0, y), new Vector2(760, 120), Kit.Hex("#35C46A"), Color.white, () => { CloseModal(true); claim(false); }, out _);
        modalBack = () => { CloseModal(true); claim(false); };   // Android back still pays out
    }

    System.Collections.IEnumerator Pulse(RectTransform r)
    {
        for (float k = 0; r; k += Time.unscaledDeltaTime)
        {
            r.localScale = Vector3.one * (1f + Mathf.Sin(k * 5f) * 0.04f);
            yield return null;
        }
    }
}
