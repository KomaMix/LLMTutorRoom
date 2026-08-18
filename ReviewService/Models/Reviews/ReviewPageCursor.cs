using System.Buffers.Binary;

namespace ReviewService.Models.Reviews;

internal readonly record struct ReviewPageCursor(DateTimeOffset SubmittedAt, int Id)
{
    private const int PayloadLength = sizeof(long) + sizeof(int);

    public static string Encode(DateTimeOffset submittedAt, int id)
    {
        Span<byte> payload = stackalloc byte[PayloadLength];
        BinaryPrimitives.WriteInt64BigEndian(payload, submittedAt.UtcTicks);
        BinaryPrimitives.WriteInt32BigEndian(payload[sizeof(long)..], id);

        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? value, out ReviewPageCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 16)
            return false;

        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            var payload = Convert.FromBase64String(base64);
            if (payload.Length != PayloadLength)
                return false;

            var ticks = BinaryPrimitives.ReadInt64BigEndian(payload);
            var id = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(sizeof(long)));
            if (ticks < DateTimeOffset.MinValue.UtcTicks
                || ticks > DateTimeOffset.MaxValue.UtcTicks
                || id <= 0)
            {
                return false;
            }

            cursor = new ReviewPageCursor(
                new DateTimeOffset(ticks, TimeSpan.Zero),
                id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
