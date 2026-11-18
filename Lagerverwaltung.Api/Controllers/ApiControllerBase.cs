using Lagerverwaltung.Api.Models;
using Lagerverwaltung.Api.Services;
using Microsoft.AspNetCore.Mvc;

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
    }
}
