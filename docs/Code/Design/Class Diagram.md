---
tags: [design, uml, api]
date: 2026-10-09
---

# Class Diagram

UML class diagrams drawn from the code in `api/src`. The first diagram is the domain model (`FitnessClub.Domain`). The second shows the whole system by layer, with one feature (Clients) in full. The design behind it is in [[2026-10-05-domain-model-and-architecture-design]], the patterns in [[Design Patterns]], and the runtime view in [[Architecture]].

Notation: `*--` composition (child lives inside the aggregate), `-->` association by id (aggregates never hold each other, only `Guid` ids), `..|>` realization, `..>` dependency, `<|--` inheritance. `$` marks a static member. Only key members are shown.

## 1. Domain model

Every aggregate root is `sealed`, has private setters and a private constructor, and is created only through a static factory method. Children (`Membership`, `Booking`, `WorkingHours`, `ClientAssignment`) are changed only through their root. Value objects in `SharedKernel` validate themselves on creation and throw `DomainException`.

```mermaid
classDiagram
    direction TB

    class Entity {
        <<abstract>>
        +Guid Id
    }
    class AggregateRoot {
        <<abstract>>
    }
    Entity <|-- AggregateRoot

    class Client {
        +PersonName Name
        +DateOnly DateOfBirth
        +EmailAddress Email
        +PhoneNumber? Phone
        +DateTimeOffset RegisteredAt
        +IReadOnlyCollection~Membership~ Memberships
        +Register(name, dateOfBirth, email, phone, now)$ Client
        +UpdateProfile(name, dateOfBirth, email, phone, now)
        +AgeOn(date) int
        +ActiveMembershipOn(date) Membership?
        +HasActiveMembershipOn(date) bool
        +PurchaseMembership(plan, startsOn, method, now) Payment
        +CancelMembership(membershipId, now)
        +CheckIn(now) Visit
        +Owns(membership) bool
        +NeedsExpiryNotice(membership, today) bool
        +MembershipsNeedingExpiryNotice(today, endsBy) IReadOnlyList~Membership~
    }

    class Membership {
        +Guid PlanId
        +string PlanName
        +Money Price
        +DateOnly StartsOn
        +DateOnly EndsOn
        +int? VisitLimit
        +int VisitsUsed
        +DateOnly? LastVisitOn
        +DateTimeOffset PurchasedAt
        +DateTimeOffset? CancelledAt
        +bool IsCancelled
        +int? RemainingVisits
        +bool HasVisitsRemaining
        ~Create(plan, startsOn, purchasedAt)$ Membership
        +IsActiveOn(date) bool
        ~Blocks(startsOn, endsOn) bool
        ~RegisterVisit(today)
        ~Cancel(now)
    }

    class MembershipPlan {
        +string Name
        +Money Price
        +int ValidityDays
        +int? VisitLimit
        +bool IsActive
        +Create(name, price, validityDays, visitLimit)$ MembershipPlan
        +Update(name, price, validityDays, visitLimit)
        +Activate()
        +Deactivate()
    }

    class Payment {
        +Guid ClientId
        +Guid MembershipId
        +Money Amount
        +PaymentMethod Method
        +DateTimeOffset PaidAt
        ~ForMembership(clientId, membership, method, paidAt)$ Payment
    }

    class Visit {
        +Guid ClientId
        +Guid MembershipId
        +DateTimeOffset CheckedInAt
        ~Record(clientId, membershipId, checkedInAt)$ Visit
    }

    class Trainer {
        +PersonName Name
        +PhoneNumber Phone
        +EmailAddress? Email
        +string Specialization
        +string? IdentityUserId
        +bool IsActive
        +IReadOnlyCollection~WorkingHours~ WorkingHours
        +IReadOnlyCollection~ClientAssignment~ Clients
        +Hire(name, phone, email, specialization)$ Trainer
        +UpdateProfile(name, phone, email, specialization)
        +LinkIdentity(identityUserId)
        +SetWorkingHours(workingHours)
        +IsWorkingDuring(slot) bool
        +AssignClient(clientId, now)
        +UnassignClient(clientId)
        +Activate()
        +Deactivate()
    }

    class WorkingHours {
        <<value object>>
        +DayOfWeek Day
        +TimeOnly Start
        +TimeOnly End
        +Create(day, start, end)$ WorkingHours
        +Overlaps(other) bool
        +Covers(day, start, end) bool
    }

    class ClientAssignment {
        <<value object>>
        +Guid ClientId
        +DateTimeOffset AssignedAt
    }

    class Room {
        +string Name
        +int Capacity
        +bool IsActive
        +Create(name, capacity)$ Room
        +Update(name, capacity)
        +Activate()
        +Deactivate()
    }

    class TrainingSession {
        +string Title
        +SessionType Type
        +Guid TrainerId
        +Guid RoomId
        +TimeSlot Slot
        +int Capacity
        +SessionStatus Status
        +int ActiveBookingCount
        +IReadOnlyCollection~Booking~ Bookings
        ~Create(title, type, trainer, room, slot, capacity, now)$ TrainingSession
        ~Book(client, now) Booking
        +CancelBooking(clientId, now)
        +Cancel(now)
    }

    class Booking {
        +Guid ClientId
        +DateTimeOffset BookedAt
        +DateTimeOffset? CancelledAt
        +bool IsActive
    }

    class Notification {
        +Guid ClientId
        +Guid? MembershipId
        +NotificationType Type
        +NotificationChannel Channel
        +string Recipient
        +string Subject
        +string Message
        +string? HtmlBody
        +NotificationStatus Status
        +DateTimeOffset CreatedAt
        +DateTimeOffset? SentAt
        +string? FailureReason
        +MembershipExpiring(client, membership, content, now)$ Notification
        +ExpiryReminder(client, membership, content, now)$ Notification
        +Promotion(client, content, now)$ Notification
        +MarkSent(now)
        +MarkFailed(reason)
        +Retry(content)
    }

    class NotificationContent {
        <<value object>>
        +string Subject
        +string Text
        +string? Html
        +Create(subject, text, html)$ NotificationContent
    }

    class PersonName {
        <<value object>>
        +string FirstName
        +string LastName
        +string? MiddleName
        +string FullName
        +Create(firstName, lastName, middleName)$ PersonName
    }
    class EmailAddress {
        <<value object>>
        +string Value
        +Create(value)$ EmailAddress
    }
    class PhoneNumber {
        <<value object>>
        +string Value
        +Create(value)$ PhoneNumber
    }
    class Money {
        <<value object>>
        +decimal Amount
        +Of(amount)$ Money
    }
    class TimeSlot {
        <<value object>>
        +DateTimeOffset Start
        +DateTimeOffset End
        +TimeSpan Duration
        +Create(start, end)$ TimeSlot
        +Overlaps(other) bool
    }

    class ISessionScheduler {
        <<interface>>
        +ScheduleAsync(title, type, trainer, room, slot, capacity, now, ct) Task~TrainingSession~
        +BookAsync(session, client, now, ct) Task~Booking~
    }
    class SessionScheduler {
        <<domain service>>
    }

    class SessionType {
        <<enumeration>>
        Group
        Individual
    }
    class SessionStatus {
        <<enumeration>>
        Scheduled
        Cancelled
    }
    class PaymentMethod {
        <<enumeration>>
        Cash
        Card
    }
    class NotificationType {
        <<enumeration>>
        MembershipExpiring
        ExpiryReminder
        Promotion
    }
    class NotificationStatus {
        <<enumeration>>
        Pending
        Sent
        Failed
    }
    class NotificationChannel {
        <<enumeration>>
        Email
        Sms
    }

    AggregateRoot <|-- Client
    AggregateRoot <|-- MembershipPlan
    AggregateRoot <|-- Payment
    AggregateRoot <|-- Visit
    AggregateRoot <|-- Trainer
    AggregateRoot <|-- Room
    AggregateRoot <|-- TrainingSession
    AggregateRoot <|-- Notification
    Entity <|-- Membership
    Entity <|-- Booking

    Client "1" *-- "0..*" Membership : memberships
    TrainingSession "1" *-- "0..*" Booking : bookings
    Trainer "1" *-- "0..*" WorkingHours : working hours
    Trainer "1" *-- "0..*" ClientAssignment : clients

    Membership "0..*" --> "1" MembershipPlan : PlanId
    Payment "0..*" --> "1" Client : ClientId
    Payment "1" --> "1" Membership : MembershipId
    Visit "0..*" --> "1" Client : ClientId
    Visit "0..*" --> "1" Membership : MembershipId
    TrainingSession "0..*" --> "1" Trainer : TrainerId
    TrainingSession "0..*" --> "1" Room : RoomId
    Booking "0..*" --> "1" Client : ClientId
    ClientAssignment "0..*" --> "1" Client : ClientId
    Notification "0..*" --> "1" Client : ClientId
    Notification "0..*" --> "0..1" Membership : MembershipId

    Client ..> Payment : creates
    Client ..> Visit : creates
    Notification ..> NotificationContent : uses

    Client *-- PersonName
    Client *-- EmailAddress
    Client *-- PhoneNumber
    Trainer *-- PersonName
    MembershipPlan *-- Money
    TrainingSession *-- TimeSlot

    TrainingSession --> SessionType
    TrainingSession --> SessionStatus
    Payment --> PaymentMethod
    Notification --> NotificationType
    Notification --> NotificationStatus
    Notification --> NotificationChannel

    ISessionScheduler <|.. SessionScheduler
    SessionScheduler ..> TrainingSession : creates, books
```

`~` marks `internal` factory and mutator methods: only the owning aggregate (or `SessionScheduler` for `TrainingSession`) may call them. For example, a `Membership` is created only by `Client.PurchaseMembership`, and a `Booking` only by `SessionScheduler.BookAsync` → `TrainingSession.Book`.

### Repository interfaces (Domain)

Each aggregate root has one repository interface in Domain. Infrastructure implements them.

```mermaid
classDiagram
    class IRepository~TAggregate~ {
        <<interface>>
        +GetByIdAsync(id, ct) Task~TAggregate?~
        +Add(aggregate)
    }
    class IClientRepository {
        <<interface>>
        +ListAsync(ct)
        +EmailExistsAsync(email, excludeId, ct)
        +ListWithMembershipsEndingBetweenAsync(from, to, ct)
    }
    class IMembershipPlanRepository {
        <<interface>>
        +ListAsync(includeInactive, ct)
        +NameExistsAsync(name, excludeId, ct)
    }
    class ITrainerRepository {
        <<interface>>
        +ListAsync(includeInactive, ct)
        +PhoneExistsAsync(phone, excludeId, ct)
        +GetByIdentityUserIdAsync(identityUserId, ct)
    }
    class IRoomRepository {
        <<interface>>
        +ListAsync(includeInactive, ct)
        +NameExistsAsync(name, excludeId, ct)
    }
    class ITrainingSessionRepository {
        <<interface>>
        +TrainerHasSessionDuringAsync(trainerId, slot, ct)
        +RoomIsBookedDuringAsync(roomId, slot, ct)
        +ClientHasBookingDuringAsync(clientId, slot, ct)
        +ListStartingBetweenAsync(from, to, ct)
        +ListForTrainerStartingBetweenAsync(trainerId, from, to, ct)
    }
    class IVisitRepository {
        <<interface>>
        +ListForClientAsync(clientId, skip, take, ct)
        +CountForClientAsync(clientId, ct)
    }
    class IPaymentRepository {
        <<interface>>
        +ListPaidBetweenAsync(from, to, ct)
    }
    class INotificationRepository {
        <<interface>>
        +ExistsForMembershipAsync(membershipId, type, ct)
        +ListPendingAsync(ct)
        +ListForClientAsync(clientId, ct)
        +ListAsync(status, ct)
    }

    IRepository <|-- IClientRepository
    IRepository <|-- IMembershipPlanRepository
    IRepository <|-- ITrainerRepository
    IRepository <|-- IRoomRepository
    IRepository <|-- ITrainingSessionRepository
    IRepository <|-- IVisitRepository
    IRepository <|-- IPaymentRepository
    IRepository <|-- INotificationRepository
```

## 2. Whole system by layer

Dependencies point inwards: Api → Application → Domain, and Infrastructure implements the interfaces that Domain and Application declare. Clients is shown in full. The other areas have the same shape: `{Name}Controller` → `I{Name}Service` → `{Name}Service` → `I{Root}Repository`.

```mermaid
classDiagram
    direction LR

    namespace Api {
        class ClientsController {
            +List(ct)
            +Get(id, ct)
            +Register(request, ct)
            +Update(id, request, ct)
            +PurchaseMembership(id, request, ct)
            +CancelMembership(id, membershipId, ct)
            +CheckIn(id, ct)
            +ListVisits(id, query, ct)
        }
        class OtherControllers {
            MembershipPlansController
            TrainersController
            RoomsController
            SessionsController
            NotificationsController
            ClientMessagesController
            ReportsController
        }
        class ExceptionToProblemDetailsHandler {
            +TryHandleAsync(httpContext, exception, ct) bool
        }
    }

    namespace Application {
        class IClientService {
            <<interface>>
            +ListAsync(ct)
            +GetAsync(id, ct)
            +RegisterAsync(request, ct)
            +UpdateAsync(id, request, ct)
            +PurchaseMembershipAsync(id, request, ct)
            +CancelMembershipAsync(id, membershipId, ct)
            +CheckInAsync(id, ct)
            +ListVisitsAsync(id, query, ct)
        }
        class ClientService {
            <<internal>>
        }
        class OtherServices {
            <<internal>>
            MembershipPlanService
            TrainerService
            RoomService
            TrainingSessionService
            ExpiryNotificationService
            ClientMessageService
            ReportService
        }
        class IUnitOfWork {
            <<interface>>
            +SaveChangesAsync(ct)
        }
        class INotificationSender {
            <<interface>>
            +SendAsync(notification, ct)
        }
        class IEmailTemplates {
            <<interface>>
            +ExpiryReminder(email) NotificationContent
            +Promotion(email) NotificationContent
        }
        class IReportQueries {
            <<interface>>
            +ListClientActivityAsync(visitsFrom, visitsTo, today, ct)
            +ListPaymentsAsync(from, to, ct)
            +ListSessionLoadAsync(from, to, ct)
        }
        class ClientRequest {
            <<record>>
        }
        class ClientDetailsResponse {
            <<record>>
            +FromEntity(client, today)$
        }
    }

    namespace Domain {
        class Client
        class IClientRepository {
            <<interface>>
        }
        class IVisitRepository {
            <<interface>>
        }
        class IPaymentRepository {
            <<interface>>
        }
        class IMembershipPlanRepository {
            <<interface>>
        }
    }

    namespace Infrastructure {
        class FitnessClubDbContext {
            <<internal>>
        }
        class Repository~TAggregate~ {
            <<abstract>>
            #DbSet Set
            +GetByIdAsync(id, ct)
            +Add(aggregate)
        }
        class ClientRepository {
            <<internal>>
        }
        class UnitOfWork {
            <<internal>>
            +SaveChangesAsync(ct)
        }
        class SendGridNotificationSender {
            <<internal>>
        }
        class LoggingNotificationSender {
            <<internal>>
        }
        class EmailTemplates {
            <<internal>>
        }
        class ReportQueries {
            <<internal>>
        }
        class ExpiryNotificationJob {
            <<internal>>
            +RunAsync(ct)
        }
        class RecurringJobs {
            <<static>>
            +Register(recurringJobs)$
        }
    }

    ClientsController ..> IClientService
    ClientsController ..> ClientRequest
    ClientsController ..> ClientDetailsResponse
    IClientService <|.. ClientService
    ClientService ..> IClientRepository
    ClientService ..> IMembershipPlanRepository
    ClientService ..> IVisitRepository
    ClientService ..> IPaymentRepository
    ClientService ..> IUnitOfWork
    ClientService ..> Client
    ClientDetailsResponse ..> Client
    OtherControllers ..> OtherServices
    OtherServices ..> INotificationSender
    OtherServices ..> IEmailTemplates
    OtherServices ..> IReportQueries
    OtherServices ..> IUnitOfWork

    Repository <|-- ClientRepository
    IClientRepository <|.. ClientRepository
    Repository ..> FitnessClubDbContext
    IUnitOfWork <|.. UnitOfWork
    UnitOfWork ..> FitnessClubDbContext
    INotificationSender <|.. SendGridNotificationSender
    INotificationSender <|.. LoggingNotificationSender
    IEmailTemplates <|.. EmailTemplates
    IReportQueries <|.. ReportQueries
    ReportQueries ..> FitnessClubDbContext
    ExpiryNotificationJob ..> OtherServices : IExpiryNotificationService
    RecurringJobs ..> ExpiryNotificationJob
```

## Persistence mapping

`FitnessClubDbContext` maps each aggregate root to its own SQL Server table through an `IEntityTypeConfiguration` in `Infrastructure/Persistence/Configurations`. Children (`Membership`, `Booking`, `WorkingHours`, `ClientAssignment`) are owned types in their own tables (`Memberships`, `Bookings`, `TrainerWorkingHours`, `TrainerClients`), so they are saved and loaded with their root. `PersonName` and `TimeSlot` are owned types in the root's table. Single-value objects (`Money`, `EmailAddress`, `PhoneNumber`) are stored as one column through value conversions (`ValueConversions.cs`). Every root has a shadow `Version` concurrency token. The schema is created by the EF migrations `InitialCreate` and `SeedMockData`.
