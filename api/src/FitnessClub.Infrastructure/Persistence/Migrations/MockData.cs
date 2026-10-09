using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FitnessClub.Infrastructure.Persistence.Migrations;

internal sealed class MockData
{
    private const string IdPrefix = "5eed";

    private static readonly string[] SeededRootTables =
        ["TrainingSessions", "Clients", "Trainers", "Rooms", "MembershipPlans"];

    private readonly DateTimeOffset _now;
    private readonly DateOnly _today;
    private readonly Random _random = new(20261008);
    private readonly Dictionary<IdKind, int> _lastIds = [];
    private readonly List<ActivePeriod> _activePeriods = [];

    private readonly List<object?[]> _plans = [];
    private readonly List<object?[]> _rooms = [];
    private readonly List<object?[]> _trainers = [];
    private readonly List<object?[]> _workingHours = [];
    private readonly List<object?[]> _trainerClients = [];
    private readonly List<object?[]> _clients = [];
    private readonly List<object?[]> _memberships = [];
    private readonly List<object?[]> _payments = [];
    private readonly List<object?[]> _visits = [];
    private readonly List<object?[]> _sessions = [];
    private readonly List<object?[]> _bookings = [];
    private readonly List<object?[]> _notifications = [];

    public MockData(DateTimeOffset now)
    {
        _now = now;
        _today = DateOnly.FromDateTime(now.DateTime);
        Build();
    }

    public void InsertInto(MigrationBuilder migrationBuilder)
    {
        Insert(migrationBuilder, "MembershipPlans", ["Id", "Name", "Price", "ValidityDays", "VisitLimit", "IsActive", "Version"], _plans);
        Insert(migrationBuilder, "Rooms", ["Id", "Name", "Capacity", "IsActive", "Version"], _rooms);
        Insert(migrationBuilder, "Trainers",
            ["Id", "FirstName", "LastName", "MiddleName", "Phone", "Email", "Specialization", "IdentityUserId", "IsActive", "Version"], _trainers);
        Insert(migrationBuilder, "TrainerWorkingHours", ["TrainerId", "Day", "Start", "End"], _workingHours);
        Insert(migrationBuilder, "Clients",
            ["Id", "FirstName", "LastName", "MiddleName", "DateOfBirth", "Phone", "Email", "RegisteredAt", "Version"], _clients);
        Insert(migrationBuilder, "TrainerClients", ["TrainerId", "ClientId", "AssignedAt"], _trainerClients);
        Insert(migrationBuilder, "Memberships",
            ["Id", "ClientId", "PlanId", "PlanName", "Price", "StartsOn", "EndsOn", "VisitLimit", "VisitsUsed", "LastVisitOn", "PurchasedAt", "CancelledAt"],
            _memberships);
        Insert(migrationBuilder, "Payments", ["Id", "ClientId", "MembershipId", "Amount", "Method", "PaidAt", "Version"], _payments);
        Insert(migrationBuilder, "Visits", ["Id", "ClientId", "MembershipId", "CheckedInAt", "Version"], _visits);
        Insert(migrationBuilder, "TrainingSessions",
            ["Id", "Title", "Type", "TrainerId", "RoomId", "StartsAt", "EndsAt", "Capacity", "Status", "Version"], _sessions);
        Insert(migrationBuilder, "Bookings", ["Id", "TrainingSessionId", "ClientId", "BookedAt", "CancelledAt"], _bookings);
        Insert(migrationBuilder, "Notifications",
            ["Id", "ClientId", "MembershipId", "Type", "Channel", "Recipient", "Message", "Status", "CreatedAt", "SentAt", "FailureReason", "Version"],
            _notifications);
    }

    public static void DeleteFrom(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"DELETE FROM [Bookings] WHERE {Seeded("TrainingSessionId")} OR {Seeded("ClientId")}");
        migrationBuilder.Sql($"DELETE FROM [Notifications] WHERE {Seeded("ClientId")}");
        migrationBuilder.Sql($"DELETE FROM [Payments] WHERE {Seeded("ClientId")}");
        migrationBuilder.Sql($"DELETE FROM [Visits] WHERE {Seeded("ClientId")}");
        migrationBuilder.Sql($"DELETE FROM [TrainerClients] WHERE {Seeded("TrainerId")} OR {Seeded("ClientId")}");
        migrationBuilder.Sql($"DELETE FROM [TrainerWorkingHours] WHERE {Seeded("TrainerId")}");
        migrationBuilder.Sql($"DELETE FROM [Memberships] WHERE {Seeded("ClientId")}");
        foreach (var table in SeededRootTables)
            migrationBuilder.Sql($"DELETE FROM [{table}] WHERE {Seeded("Id")}");
    }

    private void Build()
    {
        var single = AddPlan("Single visit", 150m, 1, 1);
        var monthly = AddPlan("Monthly unlimited", 900m, 30, null);
        var monthly12 = AddPlan("Monthly, 12 visits", 650m, 30, 12);
        var quarterly = AddPlan("Quarterly unlimited", 2400m, 90, null);
        var yearly = AddPlan("Yearly unlimited", 8000m, 365, null);
        AddPlan("Student monthly", 500m, 30, null, isActive: false);

        var mainFloor = AddRoom("Main gym floor", 40);
        var yogaStudio = AddRoom("Yoga studio", 18);
        var cyclingStudio = AddRoom("Cycling studio", 14);
        var ptRoom = AddRoom("Personal training room", 2);
        AddRoom("Old aerobics hall", 25, isActive: false);

        var olena = AddTrainer("Olena", "Kovalenko", "Ihorivna", "+380671110001", "olena.kovalenko@example.com", "Yoga and stretching",
            Weekdays(8, 16).Append((DayOfWeek.Saturday, 9, 14)), identityUserId: "auth0|6ac7949ea61c223ad590f4be");
        var dmytro = AddTrainer("Dmytro", "Shevchenko", null, "+380671110002", "dmytro.shevchenko@example.com", "Strength and conditioning",
            Weekdays(12, 21));
        var iryna = AddTrainer("Iryna", "Bondarenko", null, "+380671110003", null, "Cycling and HIIT",
            [
                (DayOfWeek.Monday, 15, 21), (DayOfWeek.Wednesday, 15, 21),
                (DayOfWeek.Tuesday, 7, 15), (DayOfWeek.Thursday, 7, 15), (DayOfWeek.Saturday, 7, 15), (DayOfWeek.Sunday, 9, 13),
            ]);
        AddTrainer("Andrii", "Melnyk", "Petrovych", "+380671110004", "andrii.melnyk@example.com", "Boxing", [], isActive: false);

        var mariia = AddClient("Mariia", "Tkachenko", null, new(1994, 3, 12), "+380501230001", "mariia.tkachenko@example.com", -245);
        for (var month = 7; month >= 0; month--)
            Sell(mariia, monthly, -10 - 30 * month, visitEvery: 3);

        var oleksandr = AddClient("Oleksandr", "Petrenko", "Viktorovych", new(1987, 11, 2), "+380501230002", "oleksandr.petrenko@example.com", -205);
        Sell(oleksandr, yearly, -200, visitEvery: 3);

        var sofiia = AddClient("Sofiia", "Kravchenko", null, new(2001, 6, 25), "+380501230003", "sofiia.kravchenko@example.com", -52);
        Sell(sofiia, monthly12, -50, visitEvery: 4);
        Sell(sofiia, monthly12, -20, visitEvery: 3);

        var maksym = AddClient("Maksym", "Boyko", null, new(1998, 1, 17), "+380501230004", "maksym.boyko@example.com", -60);
        Sell(maksym, monthly, -57, visitEvery: 4);
        Sell(maksym, monthly, -27, visitEvery: 3);

        var anastasiia = AddClient("Anastasiia", "Moroz", "Serhiivna", new(1979, 9, 8), "+380501230005", "anastasiia.moroz@example.com", -26);
        Sell(anastasiia, monthly12, -25, visitEvery: 4);

        var yurii = AddClient("Yurii", "Lysenko", null, new(1990, 12, 30), "+380501230006", "yurii.lysenko@example.com", -40);
        var yuriiMembership = Sell(yurii, monthly, -35, visitEvery: 5);
        AddNotification(yurii, yuriiMembership, NotificationOutcome.Sent, -12);

        var kateryna = AddClient("Kateryna", "Savchenko", null, new(1985, 4, 19), "+380501230007", "kateryna.savchenko@example.com", -63);
        Sell(kateryna, quarterly, -60, visitEvery: 3);

        var vladyslav = AddClient("Vladyslav", "Rudenko", null, new(2004, 8, 3), "+380501230008", "vladyslav.rudenko@example.com", -42);
        Sell(vladyslav, single, -40, visitEvery: 1);
        Sell(vladyslav, single, -17, visitEvery: 1);
        Sell(vladyslav, single, -3, visitEvery: 1);

        var tetiana = AddClient("Tetiana", "Marchenko", "Olehivna", new(1972, 2, 14), "+380501230009", "tetiana.marchenko@example.com", -110);
        Sell(tetiana, monthly, -105, visitEvery: 4);
        Sell(tetiana, monthly, -75, visitEvery: 4);
        Sell(tetiana, monthly, -45, visitEvery: 5);
        Sell(tetiana, monthly, -15, visitEvery: 4, cancelledOnDay: -12);

        AddClient("Bohdan", "Polishchuk", null, new(1996, 10, 5), "+380501230010", "bohdan.polishchuk@example.com", 0);

        var viktoriia = AddClient("Viktoriia", "Ivanenko", null, new(1983, 7, 21), "+380501230011", "viktoriia.ivanenko@example.com", -192);
        Sell(viktoriia, quarterly, -190, visitEvery: 4);
        Sell(viktoriia, quarterly, -100, visitEvery: 4);
        Sell(viktoriia, monthly, 3, visitEvery: 3);

        var roman = AddClient("Roman", "Hnatiuk", null, new(1976, 5, 9), "+380501230012", "roman.hnatiuk@example.com", -335);
        Sell(roman, yearly, -330, visitEvery: 4);

        var daria = AddClient("Daria", "Kozak", null, new(2000, 12, 1), "+380501230013", "daria.kozak@example.com", -6);
        Sell(daria, monthly, -5, visitEvery: 2);

        var artem = AddClient("Artem", "Kushnir", null, new(1999, 3, 28), "+380501230014", "artem.kushnir@example.com", -25);
        Sell(artem, monthly12, -24, visitEvery: 2);

        var yuliia = AddClient("Yuliia", "Tymoshenko", "Andriivna", new(1992, 9, 15), "+380501230015", "yuliia.tymoshenko@example.com", -118);
        for (var month = 3; month >= 0; month--)
            Sell(yuliia, monthly, 6 - 29 - 30 * month, visitEvery: 3);
        Sell(yuliia, monthly, 7, visitEvery: 3);

        var ihor = AddClient("Ihor", "Zakharchenko", null, new(1968, 1, 11), "+380501230016", "ihor.zakharchenko@example.com", -52);
        var ihorMembership = Sell(ihor, monthly, -49, visitEvery: 6);
        AddNotification(ihor, ihorMembership, NotificationOutcome.Failed, -26);

        Assign(dmytro, oleksandr);
        Assign(dmytro, roman);
        Assign(dmytro, maksym);
        Assign(olena, daria);
        Assign(olena, sofiia);
        Assign(iryna, kateryna);
        Assign(iryna, mariia);

        ClassTemplate[] week =
        [
            new("Morning yoga", [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], 9, 0, 60, olena, yogaStudio, 15),
            new("Weekend stretching", [DayOfWeek.Saturday], 10, 0, 75, olena, yogaStudio, 15),
            new("Strength circuit", [DayOfWeek.Tuesday, DayOfWeek.Thursday], 18, 0, 60, dmytro, mainFloor, 20),
            new("Indoor cycling", [DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday], 8, 0, 45, iryna, cyclingStudio, 14),
            new("HIIT", [DayOfWeek.Monday, DayOfWeek.Wednesday], 17, 0, 50, iryna, mainFloor, 25),
            new("Personal training", [DayOfWeek.Wednesday], 13, 0, 60, dmytro, ptRoom, 1, oleksandr),
            new("Personal training", [DayOfWeek.Friday], 14, 0, 60, dmytro, ptRoom, 1, roman),
            new("Private yoga", [DayOfWeek.Friday], 11, 0, 60, olena, ptRoom, 1, daria),
        ];

        var cancelledOne = false;
        for (var date = _today.AddDays(-28); date <= _today.AddDays(14); date = date.AddDays(1))
            foreach (var template in week.Where(t => t.Days.Contains(date.DayOfWeek)))
            {
                var start = At(date, template.Hour, template.Minute);
                var cancel = !cancelledOne && template.Title == "HIIT" && start > _now.AddDays(2);
                cancelledOne |= cancel;
                AddSession(template, start, cancel);
            }
    }

    private static IEnumerable<(DayOfWeek Day, int From, int To)> Weekdays(int from, int to) =>
        new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Select(day => (day, from, to));

    private Plan AddPlan(string name, decimal price, int validityDays, int? visitLimit, bool isActive = true)
    {
        var plan = new Plan(NextId(IdKind.Plan), name, price, validityDays, visitLimit);
        _plans.Add([plan.Id, name, price, validityDays, visitLimit, isActive, Guid.NewGuid()]);
        return plan;
    }

    private Guid AddRoom(string name, int capacity, bool isActive = true)
    {
        var id = NextId(IdKind.Room);
        _rooms.Add([id, name, capacity, isActive, Guid.NewGuid()]);
        return id;
    }

    private Guid AddTrainer(
        string firstName, string lastName, string? middleName, string phone, string? email, string specialization,
        IEnumerable<(DayOfWeek Day, int From, int To)> hours, bool isActive = true, string? identityUserId = null)
    {
        var id = NextId(IdKind.Trainer);
        _trainers.Add([id, firstName, lastName, middleName, phone, email, specialization, identityUserId, isActive, Guid.NewGuid()]);
        foreach (var (day, from, to) in hours)
            _workingHours.Add([id, day.ToString(), new TimeOnly(from, 0), new TimeOnly(to, 0)]);
        return id;
    }

    private Person AddClient(
        string firstName, string lastName, string? middleName, DateOnly dateOfBirth, string phone, string email, int registeredOnDay)
    {
        var person = new Person(NextId(IdKind.Client), firstName, phone, email, Earlier(At(_today.AddDays(registeredOnDay), 10, 15)));
        _clients.Add([person.Id, firstName, lastName, middleName, dateOfBirth, phone, email, person.RegisteredAt, Guid.NewGuid()]);
        return person;
    }

    private void Assign(Guid trainerId, Person client) =>
        _trainerClients.Add([trainerId, client.Id, Earlier(client.RegisteredAt.AddDays(1))]);

    private Sold Sell(Person client, Plan plan, int startsOnDay, int visitEvery, int? cancelledOnDay = null)
    {
        var id = NextId(IdKind.Membership);
        var startsOn = _today.AddDays(startsOnDay);
        var endsOn = startsOn.AddDays(plan.ValidityDays - 1);
        var cancelledOn = cancelledOnDay is { } day ? _today.AddDays(day) : (DateOnly?)null;
        var purchasedAt = Latest(client.RegisteredAt, Earlier(At(startsOn.AddDays(-_random.Next(0, 3)), _random.Next(9, 20), _random.Next(0, 60))));

        var lastVisitDay = Min(endsOn, _today.AddDays(-1), cancelledOn?.AddDays(-1) ?? DateOnly.MaxValue);
        var visitDays = new List<DateOnly>();
        for (var date = startsOn; date <= lastVisitDay && (plan.VisitLimit is null || visitDays.Count < plan.VisitLimit); date = date.AddDays(_random.Next(1, visitEvery + 1)))
            visitDays.Add(date);

        foreach (var date in visitDays)
            _visits.Add([NextId(IdKind.Visit), client.Id, id, At(date, _random.Next(7, 21), _random.Next(0, 60)), Guid.NewGuid()]);

        var cancelledAt = cancelledOn is { } cancelled ? Latest(purchasedAt, Earlier(At(cancelled, 12, 30))) : (DateTimeOffset?)null;
        _memberships.Add(
        [
            id, client.Id, plan.Id, plan.Name, plan.Price, startsOn, endsOn, plan.VisitLimit, visitDays.Count,
            visitDays.Count == 0 ? null : visitDays[^1], purchasedAt, cancelledAt,
        ]);
        _payments.Add([NextId(IdKind.Payment), client.Id, id, plan.Price, _random.Next(3) == 0 ? "Cash" : "Card", purchasedAt, Guid.NewGuid()]);

        var usedUp = plan.VisitLimit is { } limit && visitDays.Count >= limit;
        var activeUntil = Min(endsOn, cancelledOn?.AddDays(-1) ?? DateOnly.MaxValue, usedUp ? visitDays[^1] : DateOnly.MaxValue);
        _activePeriods.Add(new(client.Id, startsOn, activeUntil));

        return new Sold(id, plan.Name, endsOn);
    }

    private void AddNotification(Person client, Sold membership, NotificationOutcome outcome, int createdOnDay)
    {
        var createdAt = Earlier(At(_today.AddDays(createdOnDay), 6, 0));
        var endsOn = membership.EndsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _notifications.Add(
        [
            NextId(IdKind.Notification), client.Id, membership.Id, "MembershipExpiring",
            "Email",
            client.Email,
            $"Dear {client.FirstName}, your membership '{membership.PlanName}' expires on {endsOn}.",
            outcome.ToString(),
            createdAt,
            outcome == NotificationOutcome.Sent ? createdAt.AddMinutes(1) : null,
            outcome == NotificationOutcome.Failed ? "The email provider did not respond in time." : null,
            Guid.NewGuid(),
        ]);
    }

    private void AddSession(ClassTemplate template, DateTimeOffset start, bool cancelled)
    {
        var id = NextId(IdKind.Session);
        var type = template.Client is null ? "Group" : "Individual";
        _sessions.Add(
        [
            id, template.Title, type, template.TrainerId, template.RoomId, start, start.AddMinutes(template.Minutes),
            template.Capacity, cancelled ? "Cancelled" : "Scheduled", Guid.NewGuid(),
        ]);

        var date = DateOnly.FromDateTime(start.DateTime);
        var eligible = _activePeriods
            .Where(period => period.From <= date && date <= period.To)
            .Select(period => period.ClientId)
            .Distinct()
            .ToList();

        var attendees = template.Client is { } client
            ? eligible.Where(clientId => clientId == client.Id).ToList()
            : eligible.OrderBy(_ => _random.Next()).Take(_random.Next(template.Capacity / 4, template.Capacity / 2 + 1)).ToList();

        foreach (var clientId in attendees)
        {
            var bookedAt = Earlier(start.AddHours(-_random.Next(6, 96)));
            var cancelledAt = cancelled
                ? Latest(bookedAt, Earlier(start.AddDays(-1)))
                : _random.Next(10) == 0 && bookedAt.AddHours(2) < Min(start, _now) ? bookedAt.AddHours(2) : (DateTimeOffset?)null;
            _bookings.Add([NextId(IdKind.Booking), id, clientId, bookedAt, cancelledAt]);
        }
    }

    private Guid NextId(IdKind kind)
    {
        var number = _lastIds.GetValueOrDefault(kind) + 1;
        _lastIds[kind] = number;
        return Guid.Parse($"{IdPrefix}{(int)kind:x4}-0000-4000-8000-{number:x12}");
    }

    private DateTimeOffset At(DateOnly date, int hour, int minute) =>
        new(date.ToDateTime(new TimeOnly(hour, minute)), _now.Offset);

    private DateTimeOffset Earlier(DateTimeOffset value) => value < _now ? value : _now.AddMinutes(-5);

    private static DateTimeOffset Latest(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first < second ? first : second;

    private static DateOnly Min(params DateOnly[] dates) => dates.Min();

    private static string Seeded(string column) => $"CONVERT(char(36), [{column}]) LIKE '{IdPrefix}%'";

    private static void Insert(MigrationBuilder migrationBuilder, string table, string[] columns, List<object?[]> rows)
    {
        var values = new object?[rows.Count, columns.Length];
        for (var row = 0; row < rows.Count; row++)
            for (var column = 0; column < columns.Length; column++)
                values[row, column] = rows[row][column];

        migrationBuilder.InsertData(table, columns, values);
    }

    private enum IdKind
    {
        Plan = 1,
        Room,
        Trainer,
        Client,
        Membership,
        Payment,
        Visit,
        Session,
        Booking,
        Notification,
    }

    private enum NotificationOutcome
    {
        Sent,
        Failed,
    }

    private sealed record Plan(Guid Id, string Name, decimal Price, int ValidityDays, int? VisitLimit);

    private sealed record Person(Guid Id, string FirstName, string Phone, string Email, DateTimeOffset RegisteredAt);

    private sealed record Sold(Guid Id, string PlanName, DateOnly EndsOn);

    private sealed record ActivePeriod(Guid ClientId, DateOnly From, DateOnly To);

    private sealed record ClassTemplate(
        string Title, DayOfWeek[] Days, int Hour, int Minute, int Minutes, Guid TrainerId, Guid RoomId, int Capacity, Person? Client = null);
}
