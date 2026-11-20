using System.Net.Http.Headers;
using System.Security.Claims;

namespace Lagerverwaltung.Services.Api
{
    /// <summary>
    /// Adds "Authorization: Bearer &lt;token&gt;" to every request of ApiClient.
    ///
    /// The token comes from the login answer of the API and is stored as a claim inside the
    /// authentication cookie. That cookie is encrypted and signed by ASP.NET Core Data Protection
    /// and is HttpOnly, so the browser's JavaScript can't read the token.
    /// </summary>
    public class BearerTokenHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BearerTokenHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = _httpContextAccessor.HttpContext?.User.FindFirstValue(ApiClaims.Token);
            if (!string.IsNullOrEmpty(token) && request.Headers.Authorization == null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return base.SendAsync(request, cancellationToken);
        }
    }

    public static class ApiClaims
    {
        /// <summary>Claim type of the API bearer token in the authentication cookie.</summary>
        public const string Token = "api_token";
    }
}
