using FitnessClub.Domain.Training;

namespace FitnessClub.Application.Training;

public sealed record BookingResponse(Guid ClientId, DateTimeOffset BookedAt, DateTimeOffset? CancelledAt)
{
    public static BookingResponse FromEntity(Booking booking) => new(booking.ClientId, booking.BookedAt, booking.CancelledAt);
}
