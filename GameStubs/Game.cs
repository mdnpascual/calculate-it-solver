// NOT GAME CODE. This file lists names and signatures only (bodies are empty), so the CalcHook mod compiles on CI,
// where the game's Assembly-CSharp.dll can't be. It declares just what GameHook.MelonLoader uses.
// Every member must match the real declaration (kind, type, static, declaring type); tools/check-stubs.ps1 checks that
// against the installed game. Private fields the mod reads by reflection are not stubbed.
#pragma warning disable CS0649 // fields are never assigned: they only name the real ones
using UnityEngine;
using UnityEngine.UI;

public class StartLvl : MonoBehaviour
{
    public int round;
    public int number;
    public int rule;
    public int coins;
    public int last;
    public bool must_butted;
    public GameObject calculator;
    public GameObject calc_transform;
    public GameObject must_butt;
    public GameObject box;
    public GameObject[] shops;
    public GameObject[] items;
    public Text need;

    public bool IsBossRound { get { throw null; } }
    public bool IsTransitioning { get { throw null; } set { throw null; } }

    public void NextLvl() { throw null; }
    public void D20() { throw null; }
    public string GetAlchemyButtonKey(GameObject go) { throw null; }
}

public class MainCalc : MonoBehaviour
{
    public long total;
    public long operand;
    public long currentoperand;
    public Text output;
}

public class CountUses : MonoBehaviour
{
    public int uses;
    public string buttonKey;
    public bool glassed;
}

public class Blockirator : MonoBehaviour
{
    public bool inf_blocked;
}

public class QuestionNumber : MonoBehaviour
{
    public int digit;
}

public class NumberReader : MonoBehaviour
{
    public int number;
    public bool readed;
}

public class Infos : MonoBehaviour
{
    public bool passive;
    public int id;
}

public class Randomisator : MonoBehaviour
{
    public int cost;
    public bool glassed;
    public int type;
}

public class BuyBox : MonoBehaviour
{
    public GameObject toOpen;
}

public class CalcSize : MonoBehaviour
{
    public int size_x;
}

public static class GamePrefs
{
    public static int GetInt(string key, int defaultValue = 0) { throw null; }
}

public class RunSave : MonoBehaviour
{
    public static bool Restoring { get { throw null; } set { throw null; } }
}
