using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Lagerverwaltung.Services.Api
{
    /// <summary>
    /// Global MVC filter: turns API errors that a controller did not handle itself into sensible pages.
    ///
    ///   401  token expired or revoked  -> sign out, back to the login page with a message
    ///   403  no permission             -> "Zugriff verweigert" page with the API message
    ///   404  not found                 -> 404
    ///   400 / 409  refused             -> back to the previous page, message in the red alert box
    ///   5xx / API not reachable        -> not handled here (normal error page)
    ///
    /// Controllers still catch 400/409 themselves where the message belongs next to a form field.
    /// </summary>
    public class ApiExceptionFilter : IAsyncExceptionFilter
    {
        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public ApiExceptionFilter(ITempDataDictionaryFactory tempDataFactory)
        {
            _tempDataFactory = tempDataFactory;
        }

        public async Task OnExceptionAsync(ExceptionContext context)
        {
            if (context.Exception is not ApiException ex)
                return;

            var http = context.HttpContext;
            var tempData = _tempDataFactory.GetTempData(http);

            switch (ex.StatusCode)
            {
                case 401:
                    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    tempData["Fehler"] = "Ihre Sitzung ist abgelaufen. Bitte melden Sie sich erneut an.";
                    var returnUrl = HttpMethods.IsGet(http.Request.Method)
                        ? http.Request.PathBase + http.Request.Path + http.Request.QueryString
                        : null;
                    context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                    break;

                case 403:
                    tempData["Fehler"] = ex.Message;
                    context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                    break;

                case 404:
                    context.Result = new NotFoundResult();
                    break;

                case 400 or 409:
                    tempData["Fehler"] = ex.Message;
                    context.Result = new RedirectResult(ZurueckUrl(http));
                    break;

                default:
                    return;
            }

            context.ExceptionHandled = true;
        }

        /// <summary>The page the user came from (if it is our own and not this one), otherwise the dashboard.</summary>
        private static string ZurueckUrl(HttpContext http)
        {
            var current = http.Request.PathBase + http.Request.Path + http.Request.QueryString;
            var referer = http.Request.Headers.Referer.ToString();

            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
                && string.Equals(uri.Authority, http.Request.Host.Value, StringComparison.OrdinalIgnoreCase)
                && (!HttpMethods.IsGet(http.Request.Method) || uri.PathAndQuery != current))   // a GET page must not redirect to itself
                return uri.PathAndQuery;

            return http.Request.Path.StartsWithSegments("/Dashboard") ? "/Home/Error" : "/Dashboard";
        }
    }
}
