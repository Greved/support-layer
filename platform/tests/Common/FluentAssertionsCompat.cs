using System.Collections;
using NUnit.Framework;

namespace FluentAssertions;

public static class AssertionExtensions
{
    public static ObjectAssertions Should(this object? actual) => new(actual);

    public static StringAssertions Should(this string? actual) => new(actual);

    public static EnumerableAssertions<T> Should<T>(this IEnumerable<T>? actual) => new(actual);

    public static ActionAssertions Should(this Action action) => new(action);
}

public sealed class ObjectAssertions(object? actual)
{
    private readonly object? _actual = actual;

    public void Be(object? expected, string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.EqualTo(expected));

    public void NotBe(object? unexpected, string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.Not.EqualTo(unexpected));

    public void BeNull(string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.Null);

    public void NotBeNull(string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.Not.Null);

    public void BeTrue(string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.EqualTo(true));

    public void BeFalse(string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.EqualTo(false));

    public void BeGreaterThan(IComparable expected, string because = "", params object[] becauseArgs)
    {
        Assert.That(_actual, Is.AssignableTo<IComparable>());
        if (TryToDecimal(_actual, out var actualNumber) && TryToDecimal(expected, out var expectedNumber))
        {
            Assert.That(actualNumber, Is.GreaterThan(expectedNumber));
            return;
        }

        var comparable = (IComparable)_actual!;
        var coercedExpected = CoerceComparableExpected(comparable, expected);
        Assert.That(comparable.CompareTo(coercedExpected), Is.GreaterThan(0));
    }

    public void BeGreaterThanOrEqualTo(IComparable expected, string because = "", params object[] becauseArgs)
    {
        Assert.That(_actual, Is.AssignableTo<IComparable>());
        if (TryToDecimal(_actual, out var actualNumber) && TryToDecimal(expected, out var expectedNumber))
        {
            Assert.That(actualNumber, Is.GreaterThanOrEqualTo(expectedNumber));
            return;
        }

        var comparable = (IComparable)_actual!;
        var coercedExpected = CoerceComparableExpected(comparable, expected);
        Assert.That(comparable.CompareTo(coercedExpected), Is.GreaterThanOrEqualTo(0));
    }

    public void BeOneOf(params object?[] expectedValues) =>
        Assert.That(expectedValues, Does.Contain(_actual));

    public void BeAfter(DateTime expected, string because = "", params object[] becauseArgs)
    {
        Assert.That(_actual, Is.TypeOf<DateTime>());
        Assert.That((DateTime)_actual!, Is.GreaterThan(expected));
    }

    private static bool TryToDecimal(object? value, out decimal number)
    {
        switch (value)
        {
            case byte v:
                number = v;
                return true;
            case sbyte v:
                number = v;
                return true;
            case short v:
                number = v;
                return true;
            case ushort v:
                number = v;
                return true;
            case int v:
                number = v;
                return true;
            case uint v:
                number = v;
                return true;
            case long v:
                number = v;
                return true;
            case ulong v:
                number = v;
                return true;
            case float v:
                number = (decimal)v;
                return true;
            case double v:
                number = (decimal)v;
                return true;
            case decimal v:
                number = v;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static object CoerceComparableExpected(IComparable comparable, object expected)
    {
        var actualType = comparable.GetType();
        var expectedType = expected.GetType();
        if (actualType == expectedType)
            return expected;

        return Convert.ChangeType(expected, actualType);
    }
}

public sealed class StringAssertions(string? actual)
{
    private readonly string? _actual = actual;

    public void Be(string? expected, string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.EqualTo(expected));

    public void Contain(string expected, string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Does.Contain(expected));

    public void NotContain(string expected, string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Does.Not.Contain(expected));

    public void StartWith(string expected, string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Does.StartWith(expected));

    public void NotBeNull(string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.Not.Null);

    public void NotBeNullOrEmpty(string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.Not.Null.And.Not.Empty);

    public void NotBeNullOrWhiteSpace(string because = "", params object[] becauseArgs)
    {
        Assert.That(_actual, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(_actual), Is.False);
    }
}

public sealed class EnumerableAssertions<T>(IEnumerable<T>? actual)
{
    private readonly IEnumerable<T>? _actual = actual;

    public void NotBeNull(string because = "", params object[] becauseArgs) =>
        Assert.That(_actual, Is.Not.Null);

    public void Contain(T expected, string because = "", params object[] becauseArgs)
    {
        Assert.That(_actual, Is.Not.Null);
        Assert.That(_actual!, Does.Contain(expected));
    }

    public void Contain(Func<T, bool> predicate, string because = "", params object[] becauseArgs)
    {
        Assert.That(_actual, Is.Not.Null);
        Assert.That(_actual!.Any(predicate), Is.True);
    }

    public void NotContain(Func<T, bool> predicate, string because = "", params object[] becauseArgs)
    {
        Assert.That(_actual, Is.Not.Null);
        Assert.That(_actual!.Any(predicate), Is.False);
    }

    public void HaveCount(int expected, string because = "", params object[] becauseArgs)
    {
        Assert.That(_actual, Is.Not.Null);
        Assert.That(_actual!.Count(), Is.EqualTo(expected));
    }

    public void BeEmpty(string because = "", params object[] becauseArgs)
    {
        Assert.That(_actual, Is.Not.Null);
        Assert.That(_actual!, Is.Empty);
    }
}

public sealed class ActionAssertions(Action action)
{
    private readonly Action _action = action;

    public TException Throw<TException>() where TException : Exception =>
        Assert.Throws<TException>(() => _action())!;
}
