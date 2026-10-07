using FitnessClub.Domain.Common;

namespace FitnessClub.Application.Reports;

internal sealed class ReportService(IReportQueries queries, TimeProvider timeProvider) : IReportService
{
    public const int DefaultActivityDays = 30;
    public const int DefaultLoadDays = 7;
    public const int MaxLoadDays = 93;

    private TimeZoneInfo Zone => timeProvider.LocalTimeZone;

    public async Task<ClientActivityReportResponse> GetClientActivityAsync(DateRangeRequest request, CancellationToken cancellationToken)
    {
        var today = Today();
        var to = request.To ?? today;
        var from = request.From ?? to.AddDays(1 - DefaultActivityDays);
        EnsureOrdered(from, to);

        var clients = await queries.ListClientActivityAsync(StartOf(from), StartOf(to.AddDays(1)), today, cancellationToken);
        return new ClientActivityReportResponse(from, to, clients);
    }

    public async Task<RevenueReportResponse> GetRevenueAsync(RevenueReportRequest request, CancellationToken cancellationToken)
    {
        var year = request.Year ?? Today().Year;
        if (year is < RevenueReportRequest.MinYear or > RevenueReportRequest.MaxYear)
            throw new DomainException($"Year must be between {RevenueReportRequest.MinYear} and {RevenueReportRequest.MaxYear}.");

        if (request.Month is < 1 or > 12)
            throw new DomainException("Month must be between 1 and 12.");

        var periods = request.Month is { } month ? DaysOf(year, month) : MonthsOf(year);
        var from = periods[0].From;
        var to = periods[^1].To;

        var payments = await queries.ListPaymentsAsync(StartOf(from), StartOf(to.AddDays(1)), cancellationToken);
        var paidOn = payments.Select(payment => (Date: LocalDateOf(payment.PaidAt), payment.Amount)).ToList();

        var breakdown = periods
            .Select(period =>
            {
                var inPeriod = paidOn.Where(payment => payment.Date >= period.From && payment.Date <= period.To).ToList();
                return new RevenueBucket(period.From, period.To, inPeriod.Sum(payment => payment.Amount), inPeriod.Count);
            })
            .ToList();

        return new RevenueReportResponse(
            year,
            request.Month,
            from,
            to,
            breakdown.Sum(bucket => bucket.Total),
            breakdown.Sum(bucket => bucket.PaymentCount),
            breakdown);
    }

    public async Task<LoadReportResponse> GetLoadAsync(DateRangeRequest request, CancellationToken cancellationToken)
    {
        var from = request.From ?? Today();
        var to = request.To ?? from.AddDays(DefaultLoadDays - 1);
        EnsureOrdered(from, to);

        var dayCount = to.DayNumber - from.DayNumber + 1;
        if (dayCount > MaxLoadDays)
            throw new DomainException($"The load report covers at most {MaxLoadDays} days.");

        var sessions = await queries.ListSessionLoadAsync(StartOf(from), StartOf(to.AddDays(1)), cancellationToken);
        var sessionsByDay = sessions.ToLookup(session => LocalDateOf(session.StartsAt));

        var days = Enumerable.Range(0, dayCount)
            .Select(offset => from.AddDays(offset))
            .Select(date => new DailyLoad(date, TrainerLoadsOf(sessionsByDay[date]), RoomLoadsOf(sessionsByDay[date])))
            .ToList();

        return new LoadReportResponse(from, to, days);
    }

    private static List<TrainerLoad> TrainerLoadsOf(IEnumerable<SessionLoadEntry> sessions) =>
        sessions
            .GroupBy(session => session.TrainerId)
            .Select(group => new TrainerLoad(
                group.Key,
                group.First().TrainerName,
                group.Count(),
                HoursOf(group),
                group.SelectMany(session => session.BookedClientIds).Distinct().Count()))
            .OrderBy(load => load.TrainerName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(load => load.TrainerId)
            .ToList();

    private static List<RoomLoad> RoomLoadsOf(IEnumerable<SessionLoadEntry> sessions) =>
        sessions
            .GroupBy(session => session.RoomId)
            .Select(group =>
            {
                var booked = group.Sum(session => session.BookedClientIds.Count);
                var total = group.Sum(session => session.Capacity);
                return new RoomLoad(
                    group.Key,
                    group.First().RoomName,
                    group.Count(),
                    HoursOf(group),
                    booked,
                    total,
                    total == 0 ? 0m : Math.Round(booked * 100m / total, 1, MidpointRounding.AwayFromZero));
            })
            .OrderBy(load => load.RoomName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(load => load.RoomId)
            .ToList();

    private static decimal HoursOf(IEnumerable<SessionLoadEntry> sessions)
    {
        var minutes = sessions.Sum(session => (decimal)(session.EndsAt - session.StartsAt).TotalMinutes);
        return Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
    }

    private static List<(DateOnly From, DateOnly To)> MonthsOf(int year) =>
        Enumerable.Range(1, 12)
            .Select(month => (new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month))))
            .ToList();

    private static List<(DateOnly From, DateOnly To)> DaysOf(int year, int month) =>
        Enumerable.Range(1, DateTime.DaysInMonth(year, month))
            .Select(day => new DateOnly(year, month, day))
            .Select(date => (date, date))
            .ToList();

    private static void EnsureOrdered(DateOnly from, DateOnly to)
    {
        if (from > to)
            throw new DomainException("'from' must be on or before 'to'.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private DateTimeOffset StartOf(DateOnly date)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, Zone.GetUtcOffset(midnight));
    }

    private DateOnly LocalDateOf(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);
}
