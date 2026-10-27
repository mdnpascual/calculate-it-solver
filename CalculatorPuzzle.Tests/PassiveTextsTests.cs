using CalcHook;

namespace CalculatorPuzzle.Tests;

public class PassiveTextsTests
{
    [Fact]
    public void ParsesNameAndDescription()
    {
        var list = PassiveTexts.Parse("1. Near1: You win the round, even if you are 1 short of the required amount;");
        Assert.Single(list);
        Assert.Equal(("Near1", "You win the round, even if you are 1 short of the required amount"), list[0]);
    }

    [Fact]
    public void NoSpaceAfterColon()
    {
        var list = PassiveTexts.Parse("2. Jackpot:Every time you get the number 777, you will get 10 coins;");
        Assert.Equal(("Jackpot", "Every time you get the number 777, you will get 10 coins"), list[0]);
    }

    [Fact]
    public void NoSpaceAfterDot()
    {
        var list = PassiveTexts.Parse("3.Долой босса!:Позволяет пропустить босса;");
        Assert.Equal(("Долой босса!", "Позволяет пропустить босса"), list[0]);
    }

    [Fact]
    public void CrLfAndBomAndEmptyLinesKeepIndexes()
    {
        var list = PassiveTexts.Parse("﻿1. A: one;\r\n\r\n\n2. B: two;\r\n");
        Assert.Equal(2, list.Count);
        Assert.Equal(("A", "one"), list[0]);
        Assert.Equal(("B", "two"), list[1]);
    }

    [Fact]
    public void UnreadableLineKeepsNextIndex()
    {
        var list = PassiveTexts.Parse("1. A one\n2. B: two;\n3. C: three;");
        Assert.Equal(3, list.Count);
        Assert.Equal((null, null), list[0]);
        Assert.Equal(("B", "two"), list[1]);
        Assert.Equal(("C", "three"), list[2]);
    }

    [Fact]
    public void QuotesStayInDescription()
    {
        var list = PassiveTexts.Parse("5. Professor: Adds 3 coins if you use \"+\",\"-\",\"*\",\"/\";");
        Assert.Equal(("Professor", "Adds 3 coins if you use \"+\",\"-\",\"*\",\"/\""), list[0]);
    }

    [Fact]
    public void EmptyTextGivesEmptyList() => Assert.Empty(PassiveTexts.Parse(null));
}
