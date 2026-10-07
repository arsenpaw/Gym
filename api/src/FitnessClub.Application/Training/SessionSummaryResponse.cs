using System.Text.Json.Serialization;
using FitnessClub.Domain.Training;

namespace FitnessClub.Application.Training;

public sealed record SessionSummaryResponse(
    Guid Id,
    string Title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SessionType>))] SessionType Type,
    Guid TrainerId,
    Guid RoomId,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Capacity,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SessionStatus>))] SessionStatus Status,
    int ActiveBookingCount)
{
    public static SessionSummaryResponse FromEntity(TrainingSession session) =>
        new(
            session.Id,
            session.Title,
            session.Type,
            session.TrainerId,
            session.RoomId,
            session.Slot.Start,
            session.Slot.End,
            session.Capacity,
            session.Status,
            session.ActiveBookingCount);
}
