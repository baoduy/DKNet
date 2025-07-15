namespace DKNet.Fw.Extensions;

/// <summary>
///     Provides extension methods for working with dates.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    ///     Returns the last day of the month for the given date.
    /// </summary>
    /// <param name="date">The date to get the last day of the month for.</param>
    /// <returns>A nullable DateTime representing the last day of the month.</returns>
    public static DateTime? LastDayOfMonth(this DateTime? date) => date?.LastDayOfMonth();

    /// <summary>
    ///     Returns the last day of the month for the given date.
    /// </summary>
    /// <param name="date">The date to get the last day of the month for.</param>
    /// <returns>A DateTime representing the last day of the month.</returns>
    public static DateTime LastDayOfMonth(this DateTime date)
    {
        var lastDay = DateTime.DaysInMonth(date.Year, date.Month);
        return new DateTime(date.Year, date.Month, lastDay, date.Hour, date.Minute, date.Second, date.Millisecond,
            DateTimeKind.Local);
    }

    /// <summary>
    ///     Determines the quarter of the year for the given date.
    /// </summary>
    /// <param name="date">The date to determine the quarter for.</param>
    /// <returns>The quarter of the year (1, 2, 3, or 4).</returns>
    public static int Quarter(this DateTime date) => (date.Month - 1) / 3 + 1;
}