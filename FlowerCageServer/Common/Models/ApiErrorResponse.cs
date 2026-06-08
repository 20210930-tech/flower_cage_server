namespace FlowerCageServer.Common.Models;

public class ApiErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime TimestampUtc { get; set; }
}
