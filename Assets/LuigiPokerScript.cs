using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KModkit;
using Rnd = UnityEngine.Random;

public class LuigiPokerScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMSelectable[] Buttons;
    public KMSelectable StatusSelectable;
    public Sprite[] Sprites;
    public Image[] LuigiHandRends;
    public Image[] HandScores;
    public Image[] Icons;
    public Image BalanceCoin, BonusCoin, BonusNumTemplate, CondNumTemplate, PauseSymbol, TPSolveReady, WideNumTemplate, WinStatus;

    private KMAudio.KMAudioRef Sound = null, Sound2 = null;
    private float DefaultGameMusicVolume;
    private Coroutine[] FlashCoroutines = new Coroutine[16];
    private Coroutine[] CardAnimCoroutines = new Coroutine[5];
    private Coroutine MusicCoroutine;
    private List<Image> Digits = new List<Image>();
    private List<Image> BonusDigits = new List<Image>();
    private List<Image> Coins = new List<Image>();
    private List<Image> BonusCoins = new List<Image>();
    private List<int> Deck = new List<int>() { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5 };
    private List<int> YourHand = new List<int>();
    private List<int> LuigisHand = new List<int>();
    private List<Vector3> HandPositions = new List<Vector3>();
    private string[] CardNames = new string[] { "cloud", "mushroom", "fire flower", "luigi", "mario", "star" };
    private int CurrentHighlight = -1;
    private int Balance = 10;
    private int Bet;
    private bool[] Selected = new bool[5];
    private bool ByeByeCounter, CannotPress = true, Focused, HoldSolve, Muted, Proceed, Ready, Solved;

    private Sprite GetSpriteFromName(string name)
    {
        if (Sprites.Where(x => x.name == name.ToLower()).Count() == 0)
            return null;
        return Sprites.Where(x => x.name == name.ToLower()).First();
    }

    private List<int> SortHand(List<int> hand)
    {
        var priorityList = new List<float>();
        for (int i = 0; i < 5; i++)
            priorityList.Add(hand.Where(x => x == hand[i]).Count() - 1);
        if (priorityList.Where(x => x == 1).Count() == 4)
        {
            if (priorityList[0] == 1)
            {
                for (int i = 0; i < 5; i++)
                    if (hand[i] == hand[0])
                        priorityList[i] += 0.5f;
            }
            else
                for (int i = 1; i < 5; i++)
                    if (hand[i] == hand[1])
                        priorityList[i] += 0.5f;
        }
        return new[] { 0, 1, 2, 3, 4 }.OrderByDescending(x => priorityList[x]).ToList();
    }

    private string GetHandScore(List<int> hand)
    {
        var scores = new List<float>();
        for (int i = 0; i < 5; i++)
            scores.Add(hand.Where(x => x == hand[i]).Count() - 1);
        if (scores.Contains(4))
            return "5 of a kind";
        if (scores.Contains(3))
            return "4 of a kind";
        if (scores.Contains(2) && scores.Contains(1))
            return "full house";
        if (scores.Contains(2))
            return "3 of a kind";
        if (scores.Where(x => x == 1).Count() == 4)
            return "2 pairs";
        if (scores.Contains(1))
            return "1 pair";
        return "junk";
    }

    private int WinningHand()
    {
        var hand1 = YourHand.ToList();
        var hand2 = LuigisHand.ToList();
        var handScores = new List<int>();
        for (int i = 0; i < 2; i++)
        {
            var currHand = (i == 0 ? hand1 : hand2);
            var scores = new List<float>();
            for (int j = 0; j < 5; j++)
                scores.Add(currHand.Where(x => x == currHand[j]).Count() - 1);
            if (scores.Contains(4))
                handScores.Add(6);
            else if (scores.Contains(3))
                handScores.Add(5);
            else if (scores.Contains(2) && scores.Contains(1))
                handScores.Add(4);
            else if (scores.Contains(2))
                handScores.Add(3);
            else if (scores.Where(x => x == 1).Count() == 4)
                handScores.Add(2);
            else if (scores.Contains(1))
                handScores.Add(1);
            else
                handScores.Add(0);
        }
        Debug.LogFormat("[Luigi Poker #{0}] Your hand: {1} — {2}. Luigi's hand: {3} — {4}.", _moduleID, hand1.Select(x => new[] { "Cloud", "Mushroom", "Fire Flower", "Luigi", "Mario", "Star" }[x]).Join(", "),
            new[] { "Junk", "1 Pair", "2 Pairs", "3 of a Kind", "Full House", "4 of a Kind", "5 of a Kind" }[handScores[0]],
            hand2.Select(x => new[] { "Cloud", "Mushroom", "Fire Flower", "Luigi", "Mario", "Star" }[x]).Join(", "),
            new[] { "Junk", "1 Pair", "2 Pairs", "3 of a Kind", "Full House", "4 of a Kind", "5 of a Kind" }[handScores[1]]);
        if (handScores[0] == 0 && handScores[1] == 0)
            return 2;
        if (handScores[0] > handScores[1])
            return 0;
        if (handScores[1] > handScores[0])
            return 1;
        if (handScores.First() == 4)
        {
            var hand12 = hand1.Where(x => hand1.Where(y => y == x).Count() == 2);
            var hand22 = hand2.Where(x => hand2.Where(y => y == x).Count() == 2);
            var hand13 = hand1.Where(x => hand1.Where(y => y == x).Count() == 3);
            var hand23 = hand2.Where(x => hand2.Where(y => y == x).Count() == 3);
            for (int i = 0; i < 3; i++)
                FlashCoroutines[i] = StartCoroutine(BlinkCard(Buttons[Enumerable.Range(0, 5).Where(x => YourHand[x] == hand1.Where(y => hand1.Where(z => z == y).Count() == 3).First()).ToList()[i]].GetComponent<Image>()));
            for (int i = 5; i < 8; i++)
                FlashCoroutines[i] = StartCoroutine(BlinkCard(LuigiHandRends[Enumerable.Range(0, 5).Where(x => LuigisHand[x] == hand2.Where(y => hand2.Where(z => z == y).Count() == 3).First()).ToList()[i - 5]]));
            if (hand13.First() == hand23.First())
            {
                FlashCoroutines[hand13.First() + 10] = StartCoroutine(BlinkIcon(Icons[hand13.First()], 2));
                for (int i = 0; i < 2; i++)
                    FlashCoroutines[i] = StartCoroutine(BlinkCard(Buttons[Enumerable.Range(0, 5).Where(x => YourHand[x] == hand1.Where(y => hand1.Where(z => z == y).Count() == 2).First()).ToList()[i]].GetComponent<Image>()));
                for (int i = 5; i < 7; i++)
                    FlashCoroutines[i] = StartCoroutine(BlinkCard(LuigiHandRends[Enumerable.Range(0, 5).Where(x => LuigisHand[x] == hand2.Where(y => hand2.Where(z => z == y).Count() == 2).First()).ToList()[i - 5]]));
                if (hand12.First() == hand22.First())
                    FlashCoroutines[hand12.First() + 10] = StartCoroutine(BlinkIcon(Icons[hand12.First()], 2));
                else
                {
                    FlashCoroutines[hand12.First() + 10] = StartCoroutine(BlinkIcon(Icons[hand12.First()], 0));
                    FlashCoroutines[hand22.First() + 10] = StartCoroutine(BlinkIcon(Icons[hand22.First()], 1));
                }
            }
            else
            {
                FlashCoroutines[hand13.First() + 10] = StartCoroutine(BlinkIcon(Icons[hand13.First()], 0));
                FlashCoroutines[hand23.First() + 10] = StartCoroutine(BlinkIcon(Icons[hand23.First()], 1));
            }
            if (hand13.First() > hand23.First())
                return 0;
            if (hand23.First() > hand13.First())
                return 1;
            if (hand12.First() > hand23.First())
                return 0;
            if (hand22.First() > hand12.First())
                return 1;
        }
        else
        {
            hand1 = hand1.Where(x => hand1.Where(y => y == x).Count() > 1).ToList();
            hand2 = hand2.Where(x => hand2.Where(y => y == x).Count() > 1).ToList();
            hand1.Sort();
            hand2.Sort();
            for (int i = 0; i < Enumerable.Range(0, 5).Where(x => YourHand[x] == hand1.Last()).Count(); i++)
                FlashCoroutines[i] = StartCoroutine(BlinkCard(Buttons[Enumerable.Range(0, 5).Where(x => YourHand[x] == hand1.Last()).ToList()[i]].GetComponent<Image>()));
            for (int i = 5; i < Enumerable.Range(0, 5).Where(x => LuigisHand[x] == hand2.Last()).Count() + 5; i++)
                FlashCoroutines[i] = StartCoroutine(BlinkCard(LuigiHandRends[Enumerable.Range(0, 5).Where(x => LuigisHand[x] == hand2.Last()).ToList()[i - 5]]));
            if (hand1.Last() == hand2.Last())
            {
                FlashCoroutines[hand1.Last() + 10] = StartCoroutine(BlinkIcon(Icons[hand1.Last()], 2));
                if (handScores.First() == 2)
                {
                    for (int i = 2; i < 4; i++)
                        FlashCoroutines[i] = StartCoroutine(BlinkCard(Buttons[Enumerable.Range(0, 5).Where(x => YourHand[x] == hand1.First()).ToList()[i - 2]].GetComponent<Image>()));
                    for (int i = 7; i < 9; i++)
                        FlashCoroutines[i] = StartCoroutine(BlinkCard(LuigiHandRends[Enumerable.Range(0, 5).Where(x => LuigisHand[x] == hand2.First()).ToList()[i - 7]]));
                    if (hand1.First() == hand2.First())
                        FlashCoroutines[hand1.First() + 10] = StartCoroutine(BlinkIcon(Icons[hand1.First()], 2));
                    else
                    {
                        FlashCoroutines[hand1.First() + 10] = StartCoroutine(BlinkIcon(Icons[hand1.First()], 0));
                        FlashCoroutines[hand2.First() + 10] = StartCoroutine(BlinkIcon(Icons[hand2.First()], 1));
                    }
                }
            }
            else
            {
                FlashCoroutines[hand1.Last() + 10] = StartCoroutine(BlinkIcon(Icons[hand1.Last()], 0));
                FlashCoroutines[hand2.Last() + 10] = StartCoroutine(BlinkIcon(Icons[hand2.Last()], 1));
            }
            if (hand1.Last() > hand2.Last())
                return 0;
            if (hand2.Last() > hand1.Last())
                return 1;
            if (hand1.First() > hand2.First())
                return 0;
            if (hand2.First() > hand1.First())
                return 1;
        }
        return 2;
    }

    private int GetWinningsForHand()
    {
        var multipliers = new[] { 0, 2, 3, 4, 6, 8, 16 };
        var scores = new List<float>();
        for (int j = 0; j < 5; j++)
            scores.Add(YourHand.Where(x => x == YourHand[j]).Count() - 1);
        if (scores.Contains(4))
            return multipliers[6] * Bet;
        else if (scores.Contains(3))
            return multipliers[5] * Bet;
        else if (scores.Contains(2) && scores.Contains(1))
            return multipliers[4] * Bet;
        else if (scores.Contains(2))
            return multipliers[3] * Bet;
        else if (scores.Where(x => x == 1).Count() == 4)
            return multipliers[2] * Bet;
        else if (scores.Contains(1))
            return multipliers[1] * Bet;
        else
            return multipliers[0] * Bet;
    }

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        try
        {
            DefaultGameMusicVolume = GameMusicControl.GameMusicVolume;
        }
        catch (Exception) { }
        Bomb.OnBombExploded += delegate
        {
            try { GameMusicControl.GameMusicVolume = DefaultGameMusicVolume; } catch (Exception) { }
            try { Sound.StopSound(); } catch (Exception) { }
        };
        Bomb.OnBombSolved += delegate
        {
            try { GameMusicControl.GameMusicVolume = DefaultGameMusicVolume; } catch (Exception) { }
            try { Sound.StopSound(); } catch (Exception) { }
        };
        Module.GetComponent<KMSelectable>().OnFocus += delegate { Focused = true; if (!Muted) { MusicCoroutine = StartCoroutine(PlayMusic()); try { GameMusicControl.GameMusicVolume = 0; } catch (Exception) { } } };
        Module.GetComponent<KMSelectable>().OnDefocus += delegate { Focused = false; StopCoroutine(MusicCoroutine); if (Sound != null) Sound.StopSound(); try { GameMusicControl.GameMusicVolume = DefaultGameMusicVolume; } catch (Exception) { } if (ByeByeCounter) Audio.PlaySoundAtTransform("bye bye", transform); ByeByeCounter = !ByeByeCounter; };  //This is a bit of a bodgy solution — there's a bug in KTaNE where OnDefocus gets called twice. I've fixed this by having a counter that causes the “Bye bye!” sound to play every second time OnDefocus is called.
        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].transform.parent.GetComponent<Image>().color = new Color();
            Buttons[x].OnHighlight += delegate { CurrentHighlight = x; if (!CannotPress) Buttons[x].transform.parent.GetComponent<Image>().color = new Color(1, 1, 1); };
            Buttons[x].OnHighlightEnded += delegate { CurrentHighlight = -1; Buttons[x].transform.parent.GetComponent<Image>().color = new Color(); };
        }
        for (int i = 0; i < 5; i++)
        {
            int x = i;
            Buttons[x].OnInteract += delegate { if (!CannotPress) CardPress(x); return false; };
        }
        StatusSelectable.OnInteract += delegate { if (!Solved) StatusPress(); return false; };
        for (int i = 0; i < 5; i++)
            HandPositions.Add(Buttons[i].transform.parent.localPosition);
        for (int i = 0; i < 5; i++)
            HandPositions.Add(LuigiHandRends[i].transform.localPosition);
        Buttons[5].OnInteract += delegate { if (!CannotPress) { if (Bet == 5) Audio.PlaySoundAtTransform("cannot bet", transform); else StartCoroutine(AddBet()); } return false; };
        Buttons[6].OnInteract += delegate { if (!CannotPress) StartCoroutine(HandleMiddlePress()); return false; };
        Module.OnActivate += delegate { CalculateRound(); };
        TPSolveReady.transform.localPosition = new Vector3(0.09f, 0.0686f, 0);
        PauseSymbol.color = Color.clear;
        Initialise();
        DisplayBalance();
        StartCoroutine(BlinkBetButton());
    }

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    void StatusPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, StatusSelectable.transform);
        StatusSelectable.AddInteractionPunch();
        if (!Ready)
        {
            HoldSolve = !HoldSolve;
            PauseSymbol.color = HoldSolve ? Color.white : Color.clear;
        }
        else if (!Solved)
        {
            PauseSymbol.color = Color.clear;
            StartCoroutine(SolveRelease());
        }
    }

    void CardPress(int pos)
    {
        Selected[pos] = !Selected[pos];
        if (CardAnimCoroutines[pos] != null)
            StopCoroutine(CardAnimCoroutines[pos]);
        CardAnimCoroutines[pos] = StartCoroutine(AnimCardSelect(pos, Selected[pos]));
        if (Selected.Where(x => x).Count() == 0)
            Buttons[6].GetComponent<Image>().sprite = GetSpriteFromName("hold");
        else
            Buttons[6].GetComponent<Image>().sprite = GetSpriteFromName("draw");
    }

    void DisplayBalance()
    {
        if (!Solved && Balance >= 100 && !HoldSolve)
        {
            Module.HandlePass();
            StartCoroutine(YRS());
            Solved = true;
            Debug.LogFormat("[Luigi Poker #{0}] Your balance now equals 100. Module solved!", _moduleID);
        }
        else if (!Solved && Balance >= 100 && !Ready)
        {
            Ready = true;
            StartCoroutine(SolveReady());
            Debug.LogFormat("[Luigi Poker #{0}] Your balance now equals 100. Ready to release the solve!", _moduleID);
        }
        var temp = Balance;
        for (int i = 0; i < Digits.Count(); i++)
            Destroy(Digits[i].gameObject);
        Digits = new List<Image>();
        if (temp >= 100)
        {
            int i = 1;
            foreach (var digit in temp.ToString())
            {
                Digits.Add(Instantiate(CondNumTemplate, CondNumTemplate.transform.parent));
                Digits.Last().transform.localPosition = CondNumTemplate.transform.localPosition + new Vector3(0.006f * i, 0, 0);
                Digits.Last().sprite = GetSpriteFromName("number " + digit + " cond");
                Digits.Last().gameObject.SetActive(true);
                i++;
                temp /= 10;
            }
        }
        else
        {
            int i = 1;
            foreach (var digit in temp.ToString())
            {
                Digits.Add(Instantiate(WideNumTemplate, WideNumTemplate.transform.parent));
                Digits.Last().transform.localPosition = WideNumTemplate.transform.localPosition + new Vector3(0.01f * i, 0, 0);
                Digits.Last().sprite = GetSpriteFromName(digit == '-' ? "-" : "number " + digit);
                Digits.Last().gameObject.SetActive(true);
                i++;
            }
        }
    }

    private IEnumerator YRS()
    {
        yield return "solve";
    }

    void DisplayChange(int change)
    {
        BonusCoin.color = new Color(1, 1, 1);
        for (int i = 0; i < BonusDigits.Count(); i++)
            Destroy(BonusDigits[i].gameObject);
        BonusDigits = new List<Image>();
        int j = 1;
        if (change < 0)
        {
            BonusCoin.transform.localPosition = new Vector3(BonusCoin.transform.localPosition.x, -0.0225f, BonusCoin.transform.localPosition.z);
            BonusNumTemplate.transform.localPosition = new Vector3(BonusNumTemplate.transform.localPosition.x, -0.0225f, BonusNumTemplate.transform.localPosition.z);
        }
        else
        {
            BonusCoin.transform.localPosition = new Vector3(BonusCoin.transform.localPosition.x, -0.005f, BonusCoin.transform.localPosition.z);
            BonusNumTemplate.transform.localPosition = new Vector3(BonusNumTemplate.transform.localPosition.x, -0.005f, BonusNumTemplate.transform.localPosition.z);
            BonusDigits.Add(Instantiate(BonusNumTemplate, BonusNumTemplate.transform.parent));
            BonusDigits.Last().transform.localPosition = BonusNumTemplate.transform.localPosition + new Vector3(0.01f, 0, 0);
            BonusDigits.Last().sprite = GetSpriteFromName("+");
            BonusDigits.Last().color = new Color32(255, 132, 132, 255);
            BonusDigits.Last().gameObject.SetActive(true);
            j++;
        }
        foreach (var digit in change.ToString())
        {
            BonusDigits.Add(Instantiate(BonusNumTemplate, BonusNumTemplate.transform.parent));
            BonusDigits.Last().transform.localPosition = BonusNumTemplate.transform.localPosition + new Vector3(0.01f * j, 0, 0);
            BonusDigits.Last().sprite = digit == '-' ? GetSpriteFromName("-") : GetSpriteFromName("number " + digit);
            BonusDigits.Last().color = change < 0 ? new Color32(132, 222, 255, 255) : new Color32(255, 132, 132, 255);
            BonusDigits.Last().gameObject.SetActive(true);
            j++;
        }
    }

    void HideChange()
    {
        BonusCoin.color = new Color();
        for (int i = 0; i < BonusDigits.Count(); i++)
            Destroy(BonusDigits[i].gameObject);
        BonusDigits = new List<Image>();
    }

    void Initialise()
    {
        BonusCoin.color = new Color();
        BonusNumTemplate.color = new Color();
        for (int i = 0; i < 5; i++)
        {
            Buttons[i].transform.parent.localPosition = new Vector3(0, 0.1625f, 0);
            LuigiHandRends[i].transform.localPosition = new Vector3(0, 0.125f, 0);
        }
        Buttons[6].GetComponent<Image>().color = new Color();
        HandScores[0].transform.localPosition = new Vector3(HandScores[0].transform.localPosition.x, -0.1f, HandScores[0].transform.localPosition.z);
        HandScores[1].transform.localPosition = new Vector3(HandScores[1].transform.localPosition.x, 0.1f, HandScores[1].transform.localPosition.z);
        WinStatus.color = new Color();
        Buttons[6].GetComponent<Image>().sprite = GetSpriteFromName("hold");
    }

    void CalculateRound()
    {
        Initialise();
        if (Balance < 1)
        {
            Balance = 10;
            DisplayBalance();
        }
        Selected = new bool[5];
        Bet = 0;
        for (int i = 0; i < BonusDigits.Count(); i++)
            Destroy(BonusDigits[i].gameObject);
        BonusDigits = new List<Image>();
        for (int i = 0; i < Coins.Count(); i++)
            Destroy(Coins[i].gameObject);
        Coins = new List<Image>();
        for (int i = 0; i < FlashCoroutines.Length; i++)
            if (FlashCoroutines[i] != null)
                StopCoroutine(FlashCoroutines[i]);
        for (int i = 0; i < 5; i++)
        {
            Buttons[i].GetComponent<Image>().color = new Color(1, 1, 1);
            LuigiHandRends[i].color = new Color(1, 1, 1);
        }
        for (int i = 0; i < 6; i++)
            Icons[i].sprite = GetSpriteFromName(Icons[i].sprite.name.Replace(" red", "").Replace(" green", ""));
        StartCoroutine(AddBet());
        Deck = new List<int>() { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5 };
        Deck.Shuffle();
        YourHand = new List<int>();
        LuigisHand = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            var rand = Rnd.Range(0, Deck.Count());
            YourHand.Add(Deck[rand]);
            Deck.RemoveAt(rand);
            rand = Rnd.Range(0, Deck.Count());
            LuigisHand.Add(Deck[rand]);
            Deck.RemoveAt(rand);
            Buttons[i].GetComponent<Image>().sprite = GetSpriteFromName("back face");
            LuigiHandRends[i].sprite = GetSpriteFromName("back face");
        }
        StartCoroutine(DealCards(new[] { true, true, true, true, true }, true, 2));
    }

    void ChangePressableStatus(bool activate)
    {
        if (!activate)
        {
            for (int i = 0; i < Buttons.Length; i++)
                Buttons[i].transform.parent.GetComponent<Image>().color = new Color();
            Buttons[5].GetComponent<Image>().color = new Color();
            Buttons[6].GetComponent<Image>().color = new Color();
        }
        else
        {
            Buttons[5].GetComponent<Image>().color = new Color(1, 1, 1);
            Buttons[6].GetComponent<Image>().color = new Color(1, 1, 1);
            if (CurrentHighlight > -1)
                Buttons[CurrentHighlight].transform.parent.GetComponent<Image>().color = new Color(1, 1, 1);
        }
        CannotPress = !activate;
    }

    void RunLuigiCPU()
    {
        Selected = new bool[5];
        for (int i = 0; i < 5; i++)
            if (LuigisHand.Where(x => x == LuigisHand[i]).Count() < 2)
                Selected[i] = true;
    }

    private IEnumerator SolveReady(float duration = 0.05f)
    {
        Audio.PlaySoundAtTransform("solve ready", TPSolveReady.transform);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            TPSolveReady.transform.localPosition = Vector3.Lerp(new Vector3(0.09f, TPSolveReady.transform.localPosition.y, 0), new Vector3(0.015f, TPSolveReady.transform.localPosition.y, 0), timer / duration);
        }
        TPSolveReady.transform.localPosition = new Vector3(0.015f, TPSolveReady.transform.localPosition.y, 0);
    }

    private IEnumerator SolveRelease(float duration = 0.05f)
    {
        Module.HandlePass();
        StartCoroutine(YRS());
        Solved = true;
        Audio.PlaySoundAtTransform("solve ready", TPSolveReady.transform);
        Debug.LogFormat("[Luigi Poker #{0}] Solve released. Module solved!", _moduleID);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            TPSolveReady.transform.localPosition = Vector3.Lerp(new Vector3(0.015f, TPSolveReady.transform.localPosition.y, 0), new Vector3(0.09f, TPSolveReady.transform.localPosition.y, 0), timer / duration);
        }
        TPSolveReady.transform.localPosition = new Vector3(0.09f, TPSolveReady.transform.localPosition.y, 0);
    }

    private IEnumerator BlinkCard(Image card, float duration = 0.125f)
    {
        while (true)
        {
            card.color = new Color();
            float timer = 0;
            while (timer < duration)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            card.color = new Color(1, 1, 1);
            timer = 0;
            while (timer < duration)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

    private IEnumerator BlinkIcon(Image icon, int type, float duration = 0.125f)
    {
        while (true)
        {
            icon.sprite = GetSpriteFromName(icon.sprite.name.Replace(" red", "").Replace(" green", "") + (type % 2 == 0 ? " red" : type == 1 ? " green" : ""));
            float timer = 0;
            while (timer < duration)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            icon.sprite = GetSpriteFromName(icon.sprite.name.Replace(" red", "").Replace(" green", "") + (type == 2 ? " green" : ""));
            timer = 0;
            while (timer < duration)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

    private IEnumerator DrawAndDeal(bool yourHand, float height, float drawDuration = 0.4f, float shuffleDuration = 0.5f)
    {
        var originals = new List<Vector3>();
        for (int i = 0; i < 5; i++)
            originals.Add(yourHand ? Buttons[i].transform.parent.localPosition : LuigiHandRends[i].transform.localPosition);
        float timer = 0;
        while (timer < drawDuration)
        {
            yield return null;
            timer += Time.deltaTime;
            for (int i = 0; i < 5; i++)
                if (Selected[i])
                {
                    var temp = Vector3.Lerp(originals[i], new Vector3(originals[i].x, yourHand ? 0.175f : 0.1f), timer / drawDuration);
                    if (yourHand)
                        Buttons[i].transform.parent.localPosition = temp;
                    else
                        LuigiHandRends[i].transform.localPosition = temp;
                }
        }
        for (int i = 0; i < 5; i++)
            if (Selected[i])
            {
                if (yourHand)
                {
                    Buttons[i].transform.parent.localPosition = new Vector3(originals[i].x, 0.175f);
                    Deck.Add(YourHand[i]);
                }
                else
                {
                    LuigiHandRends[i].transform.localPosition = new Vector3(originals[i].x, 0.1f);
                    Deck.Add(LuigisHand[i]);
                }
            }
        Deck.Shuffle();
        for (int i = 0; i < 5; i++)
            if (Selected[i])
            {
                if (yourHand)
                    YourHand[i] = Deck[i];
                else
                    LuigisHand[i] = Deck[i];
                Deck.RemoveAt(i);
            }
        timer = 0;
        while (timer < shuffleDuration)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        StartCoroutine(DealCards(Selected, false, yourHand ? 0 : 1, true));
    }

    private IEnumerator HandleMiddlePress(float pause = 0.3f)
    {
        CannotPress = true;
        Audio.PlaySoundAtTransform("okay", transform);
        ChangePressableStatus(false);
        if (Selected.Where(x => x).Count() > 0)
        {
            Proceed = false;
            Audio.PlaySoundAtTransform("flip all", transform);
            for (int i = 0; i < 5; i++)
                if (Selected[i])
                    StartCoroutine(FlipCard(i, false, false, true, true));
            while (!Proceed)
                yield return null;
            Proceed = false;
            StartCoroutine(DrawAndDeal(true, 1.75f));
            while (!Proceed)
                yield return null;
        }
        RunLuigiCPU();
        float timer = 0;
        while (timer < pause * 2)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Proceed = false;
        if (Selected.Where(x => x).Count() != 0)
            StartCoroutine(DrawAndDeal(false, 1f));
        else
            Proceed = true;
        while (!Proceed)
            yield return null;
        timer = 0;
        while (timer < pause)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Proceed = false;
        StartCoroutine(AnimSortHands());
        while (!Proceed)
            yield return null;
        timer = 0;
        while (timer < pause)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Proceed = false;
        Audio.PlaySoundAtTransform("flip all", transform);
        for (int i = 5; i < 10; i++)
            StartCoroutine(FlipCard(i, true, false, i == 9));
        while (!Proceed)
            yield return null;
        Proceed = false;
        var whoWon = WinningHand();
        Debug.LogFormat("[Luigi Poker #{0}] {1}", _moduleID, new[] { "You win!", "Luigi wins.", "It's a tie!" }[whoWon]);
        StartCoroutine(RevealScores());
        while (!Proceed)
            yield return null;
        timer = 0;
        while (timer < 0.5f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        WinStatus.color = new Color(1, 1, 1);
        if (whoWon == 0)
        {
            Audio.PlaySoundAtTransform("win", transform);
            Audio.PlaySoundAtTransform("win jingle", transform);
            Sound2 = Audio.PlaySoundAtTransformWithRef("clapping", transform);
            WinStatus.sprite = GetSpriteFromName("status win");
        }
        else if (whoWon == 1)
        {
            Audio.PlaySoundAtTransform("lose", transform);
            Audio.PlaySoundAtTransform("lose jingle", transform);
            WinStatus.sprite = GetSpriteFromName("status lose");
        }
        else
        {
            Audio.PlaySoundAtTransform("draw", transform);
            Audio.PlaySoundAtTransform("draw jingle", transform);
            WinStatus.sprite = GetSpriteFromName("status draw");
        }
        timer = 0;
        while (timer < 1.5f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        if (Sound2 != null)
            Sound2.StopSound();
        WinStatus.color = new Color();
        HandScores[0].color = new Color();
        HandScores[1].color = new Color();
        Proceed = false;
        if (whoWon == 1)
            StartCoroutine(LoseBet());
        else if (whoWon == 0)
            StartCoroutine(WinBet());
        else
            StartCoroutine(DrawBet());
        while (!Proceed)
            yield return null;
        timer = 0;
        while (timer < 1f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        CalculateRound();
    }

    private IEnumerator DrawBet(float interval = 0.3f)
    {
        for (int i = Bet - 1; i > -1; i--)
        {
            StartCoroutine(ReturnCoin(Coins[i], i == 0));
            float timer = 0;
            while (timer < interval)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        Debug.LogFormat("[Luigi Poker #{0}] Your balance remains at {1} coin{2}.", _moduleID, Balance, Balance == 1 ? "" : "s");
    }

    private IEnumerator ReturnCoin(Image coin, bool proceed, float velocity = 0.25f)
    {
        var original = coin.transform.localPosition;
        float timer = 0;
        while (timer < (-original.x / velocity))
        {
            yield return null;
            timer += Time.deltaTime;
            coin.transform.localPosition = Vector3.Lerp(original, new Vector3(BalanceCoin.transform.localPosition.x, BalanceCoin.transform.localPosition.y, original.z), timer / (-original.x / velocity));
        }
        Balance++;
        coin.color = new Color();
        DisplayBalance();
        Proceed = proceed;
    }

    private IEnumerator LoseBet(float interval = 0.25f)
    {
        DisplayChange(-Bet);
        for (int i = Bet - 1; i > -1; i--)
        {
            StartCoroutine(RemoveCoin(Coins[i], i == 0));
            float timer = 0;
            while (timer < interval && i > 0)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        while (!Proceed)
            yield return null;
        for (int i = 0; i < Coins.Count(); i++)
            Destroy(Coins[i].gameObject);
        Coins = new List<Image>();
        Debug.LogFormat("[Luigi Poker #{0}] {1} coin{2} been removed from your balance. You now have {3} coin{4}.", _moduleID, Bet, Bet == 1 ? " has" : "s have", Balance, Balance == 1 ? "" : "s");
        Proceed = true;
    }

    private IEnumerator RemoveCoin(Image coin, bool proceed, float velocity = 0.3f)
    {
        Audio.PlaySoundAtTransform("lose coin", transform);
        var original = coin.transform.localPosition;
        float timer = 0;
        while (timer < (-original.x / velocity))
        {
            yield return null;
            timer += Time.deltaTime;
            coin.transform.localPosition = Vector3.Lerp(original, new Vector3(0, original.y, original.z), timer / (-original.x / velocity));
        }
        coin.transform.localPosition = new Vector3(0, original.y, original.z);
        original = coin.transform.localPosition;
        timer = 0;
        while (timer < ((0.1f - original.y) / velocity))
        {
            yield return null;
            timer += Time.deltaTime;
            coin.transform.localPosition = Vector3.Lerp(original, new Vector3(original.x, 0.1f, original.z), timer / ((0.1f - original.y) / velocity));
        }
        Proceed = proceed;
    }

    private IEnumerator WinBet(float interval = 0.1f, float delay = 0.25f, float speedUp = 2f)
    {
        for (int i = 0; i < Coins.Count(); i++)
            Destroy(Coins[i].gameObject);
        Coins = new List<Image>();
        var prize = GetWinningsForHand();
        DisplayChange(prize);
        while (prize > 0)
        {
            for (int i = 0; i < Mathf.Min(prize, 20); i++)
            {
                Coins.Add(Instantiate(BalanceCoin, BalanceCoin.transform.parent));
                Coins.Last().transform.SetAsFirstSibling();
                Coins.Last().transform.localPosition = new Vector3(-0.045f + (0.01f * (i % 10)), i / 10 == 0 ? -0.0275f : -0.03892851f, 0);
            }
            var oldPrize = prize;
            float timer = 0;
            for (int i = 0; i < Mathf.Min(oldPrize, 20); i++)
            {
                StartCoroutine(GainCoin(Coins[i], i == Mathf.Min(oldPrize, 20) - 1 && oldPrize <= 20, oldPrize > 20 ? interval / speedUp : interval));
                timer = 0;
                while (timer < (oldPrize > 20 ? interval / speedUp : interval))
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
                prize--;
                Balance++;
                DisplayBalance();
            }
            timer = 0;
            while (timer < delay)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            for (int i = 0; i < Coins.Count(); i++)
                Destroy(Coins[i].gameObject);
            Coins = new List<Image>();
        }
        Debug.LogFormat("[Luigi Poker #{0}] {1} coins have been added to your balance. You now have {2} coin{3}.", _moduleID, GetWinningsForHand(), Balance, Balance == 1 ? "" : "s");
        Proceed = true;
    }

    private IEnumerator GainCoin(Image coin, bool proceed, float duration)
    {
        Audio.PlaySoundAtTransform("coin", transform);
        var original = coin.transform.localPosition;
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            coin.transform.localPosition = Vector3.Lerp(original, BalanceCoin.transform.localPosition, timer / duration);
        }
        coin.color = new Color();
        Proceed = proceed;
    }

    private IEnumerator RevealScores()
    {
        HandScores[0].color = new Color(1, 1, 1);
        HandScores[1].color = new Color(1, 1, 1);
        HandScores[0].sprite = GetSpriteFromName(GetHandScore(YourHand));
        HandScores[1].sprite = GetSpriteFromName(GetHandScore(LuigisHand));
        float timer = 0;
        while (timer < 0.35f)
        {
            yield return null;
            timer += Time.deltaTime;
            HandScores[0].transform.localPosition = Vector3.Lerp(new Vector3(HandScores[0].transform.localPosition.x, -0.1f, HandScores[0].transform.localPosition.z), new Vector3(HandScores[0].transform.localPosition.x, -0.03825f, HandScores[0].transform.localPosition.z), timer / 0.35f);
            HandScores[1].transform.localPosition = Vector3.Lerp(new Vector3(HandScores[1].transform.localPosition.x, 0.1f, HandScores[1].transform.localPosition.z), new Vector3(HandScores[1].transform.localPosition.x, 0.00325f, HandScores[1].transform.localPosition.z), timer / 0.35f);
        }
        HandScores[0].transform.localPosition = new Vector3(HandScores[0].transform.localPosition.x, -0.03825f, HandScores[0].transform.localPosition.z);
        HandScores[1].transform.localPosition = new Vector3(HandScores[1].transform.localPosition.x, 0.00325f, HandScores[1].transform.localPosition.z);
        Proceed = true;
    }

    private IEnumerator AnimSortHands(float duration = 0.25f)
    {
        var hand1 = SortHand(YourHand);
        var hand2 = SortHand(LuigisHand);
        var inits1 = new List<Vector3>();
        for (int i = 0; i < 5; i++)
            inits1.Add(Buttons[i].transform.parent.localPosition);
        var inits2 = new List<Vector3>();
        for (int i = 0; i < 5; i++)
            inits2.Add(LuigiHandRends[i].transform.localPosition);
        var dontAnim = true;
        for (int i = 0; i < 5; i++)
            if (hand1[i] != i || hand2[i] != i)
                dontAnim = false;
        yield return null;
        if (!dontAnim)
        {
            Audio.PlaySoundAtTransform("sort", transform);
            float timer = 0;
            while (timer < duration)
            {
                yield return null;
                timer += Time.deltaTime;
                for (int i = 0; i < 5; i++)
                {
                    Buttons[i].transform.parent.localPosition = Vector3.Lerp(inits1[i], HandPositions[hand1.IndexOf(i)], timer / duration);
                    LuigiHandRends[i].transform.localPosition = Vector3.Lerp(inits2[i], HandPositions[hand2.IndexOf(i)], timer / duration);
                }
            }
            var temp1 = YourHand.ToList();
            for (int i = 0; i < 5; i++)
                YourHand[i] = temp1[hand1[i]];
            var temp2 = LuigisHand.ToList();
            for (int i = 0; i < 5; i++)
                LuigisHand[i] = temp2[hand2[i]];
            for (int i = 0; i < 5; i++)
            {
                Buttons[i].transform.parent.localPosition = inits1[i];
                Buttons[i].GetComponent<Image>().sprite = GetSpriteFromName(CardNames[YourHand[i]] + " face");
                LuigiHandRends[i].transform.localPosition = inits2[i];
            }
        }
        Proceed = true;
    }

    private IEnumerator AddBet(float duration = 0.1f)
    {
        Audio.PlaySoundAtTransform("add bet", transform);
        Buttons[5].GetComponent<Image>().color = new Color(1, 1, 1);
        Bet++;
        Balance--;
        DisplayBalance();
        Coins.Add(Instantiate(BalanceCoin, BalanceCoin.transform.parent));
        var coin = Coins.Last();
        var desired = -0.075f + ((Bet - 1) * 0.01f);
        coin.transform.SetAsFirstSibling();
        var init = coin.transform.localPosition;
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            coin.transform.localPosition = Vector3.Lerp(init, new Vector3(desired, 0.0475f, 0), timer / duration);
        }
        coin.transform.localPosition = new Vector3(desired, 0.0475f, 0);
    }

    private IEnumerator DealCards(bool[] affected, bool shuffle, int type, bool proceed = false, float dealDuration = 0.2f, float interval = 0.05f) //type: 0 = you, 1 = Luigi, 2 = both
    {
        if (shuffle)
        {
            Audio.PlaySoundAtTransform("shuffle", transform);
            float timer = 0;
            while (timer < 0.75f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        for (int i = 0; i < 5; i++)
        {
            if (affected[i])
            {
                Audio.PlaySoundAtTransform("deal", transform);
                float timer = 0;
                while (timer < dealDuration)
                {
                    yield return null;
                    timer += Time.deltaTime;
                    if (type % 2 == 0)
                        Buttons[i].transform.parent.localPosition = Vector3.Lerp(new Vector3(0, 0.1625f, 0), HandPositions[i], timer / dealDuration);
                    if (type > 0)
                        LuigiHandRends[i].transform.localPosition = Vector3.Lerp(new Vector3(0, 0.125f, 0), HandPositions[i + 5], timer / dealDuration);
                }
                if (type % 2 == 0)
                {
                    Buttons[i].transform.parent.localPosition = HandPositions[i];
                    StartCoroutine(FlipCard(i, true, affected.Where((x, ix) => ix > i && x).Count() == 0 && type == 2));
                    Audio.PlaySoundAtTransform("flip", transform);
                }
                if (type > 0)
                    LuigiHandRends[i].transform.localPosition = HandPositions[i + 5];
                timer = 0;
                while (timer < interval)
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
            }
        }
        Proceed = proceed;
    }

    private IEnumerator FlipCard(int pos, bool showFace, bool allowPress, bool proceed = false, bool disableButtons = false, float duration = 0.15f)
    {
        if (disableButtons)
            ChangePressableStatus(false);
        var names = showFace ? new[] { "back flip", "side", CardNames[pos / 5 == 1 ? LuigisHand[pos - 5] : YourHand[pos]] + " flip", CardNames[pos / 5 == 1 ? LuigisHand[pos - 5] : YourHand[pos]] + " face" } : new[] { CardNames[pos / 5 == 1 ? LuigisHand[pos - 5] : YourHand[pos]] + " flip", "side", "back flip", "back face" };
        for (int i = 0; i < 4; i++)
        {
            float timer = 0;
            while (timer < duration)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            if (pos / 5 == 0)
            {
                Buttons[pos].GetComponent<Image>().sprite = GetSpriteFromName(names[i]);
                Buttons[pos].GetComponent<Image>().rectTransform.sizeDelta = new Vector3(new[] { 16, 4, 16, 32 }[i], 48);
            }
            else
            {
                LuigiHandRends[pos % 5].sprite = GetSpriteFromName(names[i]);
                LuigiHandRends[pos % 5].rectTransform.sizeDelta = new Vector3(new[] { 16, 4, 16, 32 }[i], 48);
            }
        }
        if (allowPress)
        {
            ChangePressableStatus(allowPress);
            Audio.PlaySoundAtTransform("yah", transform);
        }
        Proceed = proceed;
    }

    private IEnumerator PlayMusic()
    {
        Sound = Audio.HandlePlaySoundAtTransformWithRef("music intro", transform, false);
        float timer = 0;
        while (timer < 3f + (19257f / 44100f))
        {
            yield return null;
            timer += Time.deltaTime;
        }
        if (Sound != null)
            Sound.StopSound();
        Sound = Audio.PlaySoundAtTransformWithRef("music", transform);
    }

    private IEnumerator AnimCardSelect(int pos, bool moveUp, float duration = 0.1f)
    {
        Audio.PlaySoundAtTransform((moveUp ? "" : "de") + "select", transform);
        var original = Buttons[pos].transform.parent.localPosition = new Vector3(Buttons[pos].transform.parent.localPosition.x, moveUp ? 0 : 0.01f, Buttons[pos].transform.parent.localPosition.z);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Buttons[pos].transform.parent.localPosition = Vector3.Lerp(original, original + (moveUp ? new Vector3(0, 0.01f, 0) : new Vector3(0, -0.01f, 0)), timer / duration);
        }
    }

    private IEnumerator BlinkBetButton(float speed = 0.25f)
    {
        var curr = 0;
        while (true)
        {
            float timer = 0;
            while (timer < speed)
            {
                yield return null;
                timer += Time.deltaTime;
                Buttons[5].GetComponent<Image>().sprite = GetSpriteFromName("bet " + new[] { "normal", "shining" }[curr]);
            }
            curr = 1 - curr;
        }
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} 123 bet5 go' to select cards 1, 2 & 3, bet 5 coins, then press 'Draw'. Each part of the command is optional: for example, '!{0} 123 go' will not change the bet and '!{0} bet5 go' won't draw any cards. '!{0} mute', '!{0} unmute' will mute/unmute the smooth jazz. Use '!{0} status' to press the status light.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var commandArray = command.Split(' ');
        if (command == "status")
        {
            yield return null;
            StatusSelectable.OnInteract();
            yield break;
        }
        else if (command == "mute")
        {
            yield return null;
            Muted = true;
            StopCoroutine(MusicCoroutine);
            if (Sound != null)
                Sound.StopSound();
            try 
            { 
                GameMusicControl.GameMusicVolume = DefaultGameMusicVolume;
            } 
            catch (Exception) { }
            yield break;
        }
        else if (command == "unmute")
        {
            yield return null;
            Muted = false;
            if (Focused && Sound == null)
            {
                MusicCoroutine = StartCoroutine(PlayMusic());
                try
                {
                    GameMusicControl.GameMusicVolume = 0;
                }
                catch (Exception) { }
            }
            yield break;
        }
        for (int i = 0; i < commandArray.Length; i++)
            if (!commandArray[i].RegexMatch(@"^(([1-5]+|bet[1-5]|go) )*([1-5]+|bet[1-5]|go)$"))
            {
                yield return "sendtochaterror Invalid command.";
                yield break;
            }
        yield return null;
        foreach (var comm in commandArray)
        {
            float timer = 0;
            if (comm.RegexMatch(@"^[1-5]+$"))
                foreach (var butt in comm)
                {
                    var ix = int.Parse(butt.ToString());
                    Buttons[ix - 1].OnInteract();
                    timer = 0;
                    while (timer < 0.1f)
                    {
                        yield return null;
                        timer += Time.deltaTime;
                    }
                }
            else if (comm.RegexMatch(@"^bet[1-5]$"))
            {
                if (int.Parse(comm.Last().ToString()) < Bet)
                    yield return "sendtochaterror Cannot decrease the bet.";
                else if (int.Parse(comm.Last().ToString()) > Bet)
                    while (Bet < int.Parse(comm.Last().ToString()))
                    {
                        yield return null;
                        Buttons[5].OnInteract();
                    }
            }
            else
                Buttons[6].OnInteract();
            timer = 0;
            while (timer < 0.1f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!Solved)
        {
            while (CannotPress)
            {
                if (Solved)
                    break;
                yield return true;
            }
            if (Ready)
            {
                StatusSelectable.OnInteract();
                break;
            }
            var choices = new bool[5];
            for (int i = 0; i < 5; i++)
                if (YourHand.Where(x => x == YourHand[i]).Count() < 2)
                    choices[i] = true;
            if (choices.Where(x => x).Count() == 5)
            {
                if (YourHand.Contains(5))
                    choices[YourHand.IndexOf(5)] = false;
                else
                    choices[YourHand.IndexOf(4)] = false;
            }
            for (int i = 0; i < 5; i++)
                if (choices[i] && !Selected[i] || !choices[i] && Selected[i])
                    Buttons[i].OnInteract();
            while (Bet < 5)
            {
                yield return true;
                Buttons[5].OnInteract();
            }
            Buttons[6].OnInteract();
        }
    }
}