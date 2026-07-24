namespace TaskManager.Helpers
{
    /// <summary>
    /// Npgsql rejects DateTime Kind=Unspecified for timestamptz columns.
    /// Mobile DatePickers and some JSON payloads send Unspecified — normalize before save.
    /// </summary>
    public static class DateTimeUtc
    {
        public static DateTime? Normalize(DateTime? value)
        {
            if (value is null) return null;
            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }

        public static DateTime Normalize(DateTime value) =>
            Normalize((DateTime?)value)!.Value;
    }
}
