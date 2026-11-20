using System.Net.Http.Json;
using System.Text.Json;

namespace Lagerverwaltung.Services.Api
{
    /// <summary>
    /// Typed HttpClient for the REST API (Lagerverwaltung.Api).
    /// This is the ONLY way the web app reads or changes data: there is no DbContext any more.
    ///
    /// - The base address comes from appsettings.json ("Api:BaseUrl").
    /// - The bearer token is added by BearerTokenHandler.
    /// - Every error status becomes an ApiException with the German message from the API.
    ///   ApiExceptionFilter turns 401/403/404 into a login redirect, the access-denied page or 404;
    ///   controllers catch 400/409 themselves when the message belongs next to a form field.
    /// </summary>
    public class ApiClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly HttpClient _http;

        public ApiClient(HttpClient http)
        {
            _http = http;
        }

        // ---------------------------------------------------------------- Auth

        public Task<LoginResponse> LoginAsync(string username, string password) =>
            SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", new { username, password });

        public Task LogoutAsync() => SendAsync(HttpMethod.Post, "api/auth/logout");

        /// <summary>POST /api/auth/register. The API always creates a LehrerIn.</summary>
        public async Task<int> RegisterAsync(string username, string password) =>
            (await SendAsync<CreatedResponse>(HttpMethod.Post, "api/auth/register", new { username, password })).Id;

        // ---------------------------------------------------------------- Users

        public Task<List<UserDto>> GetUsersAsync() => SendAsync<List<UserDto>>(HttpMethod.Get, "api/users");

        public Task ChangeRoleAsync(int id, string role) =>
            SendAsync(HttpMethod.Put, $"api/users/{id}/role", new { role });

        public Task DeleteUserAsync(int id) => SendAsync(HttpMethod.Delete, $"api/users/{id}");

        // ---------------------------------------------------------------- Categories

        public Task<List<CategoryDto>> GetCategoriesAsync() => SendAsync<List<CategoryDto>>(HttpMethod.Get, "api/categories");

        public async Task<int> CreateCategoryAsync(string name, int? modelNum) =>
            (await SendAsync<CreatedResponse>(HttpMethod.Post, "api/categories", new { name, modelNum })).Id;

        public Task UpdateCategoryAsync(int id, string name, int? modelNum) =>
            SendAsync(HttpMethod.Put, $"api/categories/{id}", new { name, modelNum });

        public Task DeleteCategoryAsync(int id) => SendAsync(HttpMethod.Delete, $"api/categories/{id}");

        // ---------------------------------------------------------------- Manufacturers

        public Task<List<ManufacturerDto>> GetManufacturersAsync() => SendAsync<List<ManufacturerDto>>(HttpMethod.Get, "api/manufacturers");

        public async Task<int> CreateManufacturerAsync(string name) =>
            (await SendAsync<CreatedResponse>(HttpMethod.Post, "api/manufacturers", new { name })).Id;

        public Task UpdateManufacturerAsync(int id, string name) =>
            SendAsync(HttpMethod.Put, $"api/manufacturers/{id}", new { name });

        public Task DeleteManufacturerAsync(int id) => SendAsync(HttpMethod.Delete, $"api/manufacturers/{id}");

        // ---------------------------------------------------------------- Articles

        public Task<List<ArticleDto>> GetArticlesAsync(int? kategorieId = null, int? herstellerId = null)
        {
            var query = QueryString.Empty;
            if (kategorieId != null) query = query.Add("kategorieId", kategorieId.Value.ToString());
            if (herstellerId != null) query = query.Add("herstellerId", herstellerId.Value.ToString());
            return SendAsync<List<ArticleDto>>(HttpMethod.Get, "api/articles" + query);
        }

        public Task<ArticleDto> GetArticleAsync(int id) => SendAsync<ArticleDto>(HttpMethod.Get, $"api/articles/{id}");

        public async Task<int> CreateArticleAsync(string name, int? serienNum, int kategorieId, int? herstellerId) =>
            (await SendAsync<CreatedResponse>(HttpMethod.Post, "api/articles", new { name, serienNum, kategorieId, herstellerId })).Id;

        public Task UpdateArticleAsync(int id, string name, int? serienNum, int kategorieId, int? herstellerId) =>
            SendAsync(HttpMethod.Put, $"api/articles/{id}", new { name, serienNum, kategorieId, herstellerId });

        public Task DeleteArticleAsync(int id) => SendAsync(HttpMethod.Delete, $"api/articles/{id}");

        /// <summary>POST /api/articles/{id}/stock: "Zugang" into the Hauptlager.</summary>
        public Task AddStockAsync(int id, int menge) =>
            SendAsync(HttpMethod.Post, $"api/articles/{id}/stock", new { menge });

        // ---------------------------------------------------------------- Locations (Raum)

        public Task<List<RoomDto>> GetRoomsAsync() => SendAsync<List<RoomDto>>(HttpMethod.Get, "api/locations");

        /// <summary>
        /// GET /api/locations/mine. Accepts both a plain array and { "items": [...] },
        /// so the web app works with either answer shape of the API.
        /// </summary>
        public async Task<List<RoomDto>> GetOwnRoomsAsync()
        {
            var json = await SendAsync<JsonElement>(HttpMethod.Get, "api/locations/mine");
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("items", out var items))
                json = items;
            return json.Deserialize<List<RoomDto>>(Json) ?? new();
        }

        public async Task<int> CreateRoomAsync(string raumTyp, int benutzerId, int? raumNum) =>
            (await SendAsync<CreatedResponse>(HttpMethod.Post, "api/locations", new { raumTyp, benutzerId, raumNum })).Id;

        public Task UpdateRoomAsync(int id, int? raumNum) =>
            SendAsync(HttpMethod.Put, $"api/locations/{id}", new { raumNum });

        public Task AssignTeacherAsync(int raumId, int benutzerId) =>
            SendAsync(HttpMethod.Put, $"api/locations/{raumId}/teacher", new { benutzerId });

        public Task DeleteRoomAsync(int id) => SendAsync(HttpMethod.Delete, $"api/locations/{id}");

        public Task<List<InventoryDto>> GetInventoryAsync(int raumId) =>
            SendAsync<List<InventoryDto>>(HttpMethod.Get, $"api/locations/{raumId}/inventory");

        // ---------------------------------------------------------------- Transfers (Ausgabe / Übernahme)

        public async Task<int> CreateTransferAsync(int laborId, int artikelId, int menge) =>
            (await SendAsync<CreatedResponse>(HttpMethod.Post, "api/transfers", new { laborId, artikelId, menge })).Id;

        public Task<List<PendingTransferDto>> GetPendingTransfersAsync() =>
            SendAsync<List<PendingTransferDto>>(HttpMethod.Get, "api/transfers/pending");

        public Task ConfirmTransferAsync(int id) => SendAsync(HttpMethod.Post, $"api/transfers/{id}/confirm");

        // ---------------------------------------------------------------- Returns (Rückgabe)

        public async Task<int> CreateReturnAsync(int laborId, int artikelId, int menge) =>
            (await SendAsync<CreatedResponse>(HttpMethod.Post, "api/returns", new { laborId, artikelId, menge })).Id;

        public Task<List<PendingReturnDto>> GetPendingReturnsAsync() =>
            SendAsync<List<PendingReturnDto>>(HttpMethod.Get, "api/returns/pending");

        /// <summary>POST /api/returns/{id}/accept: Umbuchungslager -> Hauptlager, return closed.</summary>
        public Task AcceptReturnAsync(int id) => SendAsync(HttpMethod.Post, $"api/returns/{id}/accept");

        // ---------------------------------------------------------------- Movements

        public Task<List<MovementDto>> GetMovementsAsync(int? raumId = null, int? artikelId = null)
        {
            var query = QueryString.Empty;
            if (raumId != null) query = query.Add("raumId", raumId.Value.ToString());
            if (artikelId != null) query = query.Add("artikelId", artikelId.Value.ToString());
            return SendAsync<List<MovementDto>>(HttpMethod.Get, "api/movements" + query);
        }

        // ---------------------------------------------------------------- Plumbing

        private async Task SendAsync(HttpMethod method, string url, object? body = null)
        {
            using var response = await SendRawAsync(method, url, body);
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null)
        {
            using var response = await SendRawAsync(method, url, body);
            return (await response.Content.ReadFromJsonAsync<T>(Json))!;
        }

        private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, object? body)
        {
            using var request = new HttpRequestMessage(method, url);
            if (body != null)
                request.Content = JsonContent.Create(body, options: Json);

            var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return response;

            using (response)
                throw await ToExceptionAsync(response);
        }

        /// <summary>
        /// Reads the error body of the API:
        /// - { "error": "..." }                       (stored procedure THROW 504xx, login)
        /// - { "title": "...", "errors": { ... } }    (400 from [ApiController] model validation)
        /// </summary>
        private static async Task<ApiException> ToExceptionAsync(HttpResponseMessage response)
        {
            int status = (int)response.StatusCode;
            string? message = null;
            var fieldErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                            message = error.GetString();

                        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var field in errors.EnumerateObject())
                            {
                                if (field.Value.ValueKind != JsonValueKind.Array) continue;
                                fieldErrors[field.Name] = field.Value.EnumerateArray()
                                    .Select(e => e.GetString() ?? "").Where(s => s != "").ToArray();
                            }
                            message ??= fieldErrors.Values.SelectMany(v => v).FirstOrDefault();
                        }

                        if (message == null && root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                            message = title.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // Body is not JSON (e.g. a proxy error page): fall back to the default text below.
            }

            message ??= status switch
            {
                401 => "Ihre Sitzung ist abgelaufen. Bitte melden Sie sich erneut an.",
                403 => "Sie haben keine Berechtigung für diese Aktion.",
                404 => "Der Eintrag wurde nicht gefunden.",
                409 => "Die Aktion ist im aktuellen Zustand nicht möglich.",
                _ => $"Die API hat mit Status {status} geantwortet."
            };
            return new ApiException(status, message, fieldErrors);
        }
    }
}
