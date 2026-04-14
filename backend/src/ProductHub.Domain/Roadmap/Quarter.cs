using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class Quarter : ValueObject
{
    public const int BacklogYear = 0;
    public const int BacklogNumber = 0;

    public int Year { get; }
    public int Number { get; } // 0 for backlog, else 1-4

    private Quarter(int year, int number)
    {
        Year = year;
        Number = number;
    }

    public bool IsBacklog => Year == BacklogYear && Number == BacklogNumber;

    public string Label => IsBacklog ? "Backlog" : $"Q{Number}/{Year.ToString()[2..]}";

    public static Quarter Create(int year, int number)
    {
        if (year == BacklogYear && number == BacklogNumber)
            return new Quarter(year, number);

        if (number is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(number), "Quarter number must be between 1 and 4.");
        if (year is < 2020 or > 2100)
            throw new ArgumentOutOfRangeException(nameof(year), "Year is out of valid range.");

        return new Quarter(year, number);
    }

    /// <summary>Parses a label like "Q1/26" into a Quarter.</summary>
    public static Quarter Parse(string label)
    {
        if (string.Equals(label?.Trim(), "Backlog", StringComparison.OrdinalIgnoreCase))
            return Create(BacklogYear, BacklogNumber);

        if (string.IsNullOrWhiteSpace(label) || !label.StartsWith('Q'))
            throw new FormatException($"Invalid quarter label: '{label}'.");

        var parts = label[1..].Split('/');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var number)
            || !int.TryParse(parts[1], out var shortYear))
            throw new FormatException($"Invalid quarter label: '{label}'.");

        return Create(2000 + shortYear, number);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Year;
        yield return Number;
    }

    public override string ToString() => Label;
}
