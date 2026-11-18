using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace Lagerverwaltung.Api.Helpers
{
    /// <summary>
    /// Global filter: turns errors thrown by the stored procedures into HTTP answers.
    ///
    /// The procedures use  THROW 504xx, 'message', 1  where 504xx = 50000 + HTTP status.
    /// Example: THROW 50409, 'Nicht genügend Bestand vorhanden.', 1  ->  409 { "error": "Nicht genügend Bestand vorhanden." }
    ///
    /// Any other database error is an unexpected bug: it is logged, and the client only gets a
    /// generic 500 message (no SQL details, as the task requires).
    ///
    /// Registered once in Program.cs, so controllers don't need try/catch.
    /// </summary>
    public class SqlExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<SqlExceptionFilter> _logger;

        public SqlExceptionFilter(ILogger<SqlExceptionFilter> logger) => _logger = logger;

        public void OnException(ExceptionContext context)
        {
            if (context.Exception is not SqlException ex)
                return;

            if (ex.Number >= 50400 && ex.Number <= 50499)
            {
                context.Result = new ObjectResult(new { error = ex.Message }) { StatusCode = ex.Number - 50000 };
            }
            else
            {
                _logger.LogError(ex, "Unexpected database error {Number}", ex.Number);
                context.Result = new ObjectResult(new { error = "Interner Datenbankfehler." }) { StatusCode = 500 };
            }
            context.ExceptionHandled = true;
        }
    }
}
