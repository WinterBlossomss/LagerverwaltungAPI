using Lagerverwaltung.Api.Models;
using Lagerverwaltung.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Lagerverwaltung.Api.Controllers
{
    /// <summary>
    /// Base class for all API controllers.
    /// - Db gives access to the generated stored procedure wrappers (EF Core Power Tools).
    ///   The API never queries tables directly: every database access is a stored procedure.
    /// - TokenHash is SHA-256 of the bearer token from the Authorization header (null if missing).
    ///   It is passed to every protected procedure, which checks the token and the permissions
    ///   itself (spAuthorize) and answers 401/403 via THROW.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected readonly ILagerverwaltungssystemContextProcedures Db;

        protected ApiControllerBase(LagerverwaltungssystemContext context)
        {
            Db = context.Procedures;
        }

        protected byte[]? TokenHash => TokenService.GetTokenHash(Request);

        /// <summary>
        /// Runs a controller action and turns the EXPECTED errors from the stored procedures
        /// (THROW 50400..50499, e.g. 50401 "not logged in", 50403 "no permission", 50409 "conflict")
        /// into the matching HTTP status: 50403 -> 403 { "error": "..." }.
        ///
        /// Why here and not only in a global filter: these errors are normal answers, not bugs.
        /// Catching them in our own code means Visual Studio's debugger (F5) doesn't stop on every
        /// 401/403/409 test. Unexpected database errors are NOT caught here; they go on to the
        /// SqlExceptionFilter, which logs them and answers 500.
        /// </summary>
        protected async Task<IActionResult> Run(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (SqlException ex) when (ex.Number >= 50400 && ex.Number <= 50499)
            {
                return StatusCode(ex.Number - 50000, new { error = ex.Message });
            }
        }
    }
}
