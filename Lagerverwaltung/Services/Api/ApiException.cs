namespace Lagerverwaltung.Services.Api
{
    /// <summary>
    /// The API answered with an error status (4xx/5xx).
    /// Message is the German text from the API body ({ "error": "..." }) or, for a 400 from
    /// model validation, the first validation message. FieldErrors holds all validation messages.
    /// </summary>
    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

        public ApiException(int statusCode, string message, IReadOnlyDictionary<string, string[]>? fieldErrors = null)
            : base(message)
        {
            StatusCode = statusCode;
            FieldErrors = fieldErrors ?? new Dictionary<string, string[]>();
        }

        public bool IsUnauthorized => StatusCode == 401;
        public bool IsForbidden => StatusCode == 403;
        public bool IsNotFound => StatusCode == 404;
        public bool IsConflict => StatusCode == 409;

        /// <summary>400, 404 and 409: the request was refused for a reason the user can fix or should read.</summary>
        public bool IsUserError => StatusCode is 400 or 404 or 409;
    }
}
