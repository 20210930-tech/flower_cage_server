using System.Net;

namespace FlowerCageServer.Common.Exceptions;

public class ApiException : Exception
{
    public ApiException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
