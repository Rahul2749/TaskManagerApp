// These tests execute the production viewmodel without opening a MAUI UI.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal sealed class QueryPropertyAttribute(string name, string queryId) : Attribute
{
    public string Name { get; } = name;
    public string QueryId { get; } = queryId;
}

internal sealed class Shell
{
    public static Shell Current { get; } = new();
    public Task GoToAsync(string route) => Task.CompletedTask;
}
