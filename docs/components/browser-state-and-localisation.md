# Components: Browser State and Localisation

## Overview

NuciAPI.Controllers is a **server-side library** for ASP.NET Core REST APIs. It has **no browser-specific code**, no client-side state management, and no built-in localisation. This document describes how consumers handle browser state and localisation when building web applications that use NuciAPI.Controllers.

## Browser State (Client-Side)

### Library: No Browser State

| Aspect | Library Support |
|--------|-----------------|
| Cookies | Not managed |
| LocalStorage/SessionStorage | Not managed |
| IndexedDB | Not managed |
| Service Workers | Not managed |
| WebSockets/SSE | Not managed |
| Authentication tokens (client) | Not managed |

### Consumer: Full Responsibility

Consumers building SPAs (React, Vue, Blazor, etc.) or traditional web apps must handle all browser state.

## Authentication State in Browser

### Token Storage Options

| Storage | Pros | Cons | Use Case |
|---------|------|------|----------|
| `HttpOnly` Cookie | Secure, automatic send | CSRF risk, domain-bound | Traditional web apps |
| `localStorage` | Persistent, large capacity | XSS vulnerable | SPAs with refresh tokens |
| `sessionStorage` | Tab-isolated, cleared on close | Lost on tab close | Short-lived sessions |
| Memory (JS variable) | Most secure (XSS) | Lost on refresh | High-security SPAs |

### Recommended: HttpOnly Cookie + CSRF

```csharp
// Program.cs - Server sets HttpOnly cookie
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Strict,
    HttpOnly = HttpOnlyPolicy.Always,
    Secure = CookieSecurePolicy.Always
});

// Login endpoint
[HttpPost("login")]
public ActionResult Login(LoginRequest request)
{
    return ProcessRequest(request, () =>
    {
        var token = _authService.Authenticate(request.Email, request.Password);

        Response.Cookies.Append("auth-token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        });

        return new LoginResponse { Success = true };
    }, NuciApiAuthorisation.None);
}

// CSRF protection for state-changing operations
app.UseAntiforgery();
```

### SPA: Access Token + Refresh Token

```typescript
// TypeScript client
class AuthService {
    private accessToken: string | null = null;
    private refreshToken: string | null = null;

    async login(credentials: LoginRequest) {
        const response = await fetch('/api/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(credentials),
            credentials: 'include' // For HttpOnly cookies
        });

        if (response.ok) {
            const data = await response.json();
            this.accessToken = data.accessToken;
            this.refreshToken = data.refreshToken; // Store in memory
        }
    }

    async apiCall<T>(url: string, options: RequestInit = {}): Promise<T> {
        const response = await fetch(url, {
            ...options,
            headers: {
                ...options.headers,
                'Authorization': `Bearer ${this.accessToken}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.status === 401) {
            await this.refreshAccessToken();
            return this.apiCall(url, options); // Retry
        }

        return response.json();
    }
}
```

## API Key in Browser

### Never Expose API Keys in Browser

| Approach | Security |
|----------|----------|
| API key in JS code | Exposed in bundle |
| API key in localStorage | XSS steals it |
| API key in cookie | CSRF risk |
| **Backend-for-Frontend (BFF)** | Recommended |

### BFF Pattern

```
Browser -> BFF (NuciAPI.Controllers) -> Upstream API
         ^
    HttpOnly cookie
```

```csharp
// BFF Controller - hides upstream API key
[HttpGet("data")]
public async Task<ActionResult> GetData()
{
    return await ProcessRequestAsync(new DataRequest(), async () =>
    {
        // Server-side: uses API key from config (not browser)
        var upstream = await _httpClient.GetAsync("https://api.upstream.com/data",
            HttpCompletionOption.ResponseHeadersRead);

        upstream.EnsureSuccessStatusCode();
        return await upstream.Content.ReadFromJsonAsync<DataResponse>();
    }, NuciApiAuthorisation.None); // Browser auth via cookie
}
```

## Localisation (i18n)

### Library: No Localisation

| Feature | Library Support |
|---------|-----------------|
| Resource files (.resx) | No |
| `IStringLocalizer` | No |
| Culture detection | No |
| Date/number formatting | No |
| RTL support | No |

### Consumer: Full Implementation

## Server-Side Localisation

### Request Culture Detection

```csharp
// Program.cs
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en-US"),
        new CultureInfo("de-DE"),
        new CultureInfo("fr-FR"),
        new CultureInfo("ro-RO")
    };

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Priority: Query string -> Cookie -> Header -> Default
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

var app = builder.Build();
app.UseRequestLocalization();
```

### Localised Responses

```csharp
[HttpGet("greeting")]
public ActionResult GetGreeting(
    [FromServices] IStringLocalizer<GreetingController> localizer)
{
    return ProcessRequest(new GreetingRequest(), () =>
    {
        return new GreetingResponse
        {
            Message = localizer["WelcomeMessage", "User"]
        };
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

### Resource Files

```
Resources/
├── Controllers.GreetingController.resx          # Default (en-US)
├── Controllers.GreetingController.de-DE.resx
├── Controllers.GreetingController.fr-FR.resx
└── Controllers.GreetingController.ro-RO.resx
```

```xml
<!-- Controllers.GreetingController.de-DE.resx -->
<data name="WelcomeMessage" xml:space="preserve">
    <value>Willkommen, {0}!</value>
</data>
```

### Localised Error Responses

```csharp
// Custom error response with localisation
public class LocalisedErrorResponse : ApiResponse
{
    public LocalisedErrorResponse(IStringLocalizer localizer, string errorCode, params object[] args)
    {
        Error = new ApiError
        {
            Code = errorCode,
            Message = localizer[errorCode, args]
        };
    }
}

// Usage
return ProcessRequest(request, () =>
{
    if (!userExists)
        throw new NotFoundException(localizer["UserNotFound", userId]);

    return new UserResponse { Data = user };
}, auth);
```

## Client-Side Localisation

### React/i18next Example

```typescript
// i18n.ts
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import HttpApi from 'i18next-http-backend';

i18n
    .use(HttpApi)
    .use(initReactI18next)
    .init({
        lng: 'en',
        fallbackLng: 'en',
        supportedLngs: ['en', 'de', 'fr', 'ro'],
        backend: {
            loadPath: '/locales/{{lng}}/{{ns}}.json'
        },
        interpolation: { escapeValue: false }
    });

export default i18n;
```

### Locale Files (Public)

```
wwwroot/locales/
├── en/
│   ├── common.json
│   └── errors.json
├── de/
│   ├── common.json
│   └── errors.json
└── ro/
    ├── common.json
    └── errors.json
```

```json
// wwwroot/locales/en/errors.json
{
    "InvalidRequest": "The request was invalid.",
    "NotFound": "The requested resource was not found.",
    "Unauthorized": "Authentication failed.",
    "Forbidden": "Insufficient permissions."
}
```

### Using in Components

```tsx
// Greeting.tsx
import { useTranslation } from 'react-i18next';

export function Greeting() {
    const { t } = useTranslation('common');

    return <h1>{t('welcome', { name: 'User' })}</h1>;
>
}
```

## Date/Time Localisation

### Server: UTC Storage, Local Display

```csharp
// Store in UTC
public class Entity
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

// Return UTC, let client format
public class EntityResponse
{
    public string CreatedAtUtc { get; set; } = string.Empty; // ISO 8601
}

// Client formats
const date = new Date(response.createdAtUtc);
const formatted = date.toLocaleString(navigator.language, {
    dateStyle: 'medium',
    timeStyle: 'short'
});
```

### NodaTime (Recommended)

```csharp
// Program.cs
builder.Services.AddSingleton<IClock>(SystemClock.Instance);

// Service
public class AuditService
{
    private readonly IClock _clock;

    public AuditService(IClock clock) => _clock = clock;

    public Instant Now => _clock.GetCurrentInstant();

    public ZonedDateTime InUserZone(Instant instant, string ianaZone)
    {
        return instant.InZone(DateTimeZoneProviders.Tzdb[ianaZone]);
    }
}
```

## Number/Currency Localisation

### Server: Invariant Culture for APIs

```csharp
// Always use invariant for API serialization
var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
{
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
});

// Or explicit culture
var formatted = value.ToString("G", CultureInfo.InvariantCulture);
```

### Client: Locale-Aware Formatting

```typescript
// TypeScript
function formatCurrency(amount: number, currency: string = 'EUR'): string {
    return new Intl.NumberFormat(navigator.language, {
        style: 'currency',
        currency
    }).format(amount);
}

function formatNumber(value: number): string {
    return new Intl.NumberFormat(navigator.language).format(value);
>
}
```

## Time Zone Handling

### Server: Store IANA Time Zone

```csharp
public class UserPreferences
{
    public string TimeZoneId { get; set; } = "UTC"; // IANA: "Europe/Bucharest"
}

// Convert for display
public DateTimeOffset ConvertToUserTime(DateTimeOffset utc, string userTimeZone)
{
    var tz = TimeZoneInfo.FindSystemTimeZoneById(userTimeZone);
    return TimeZoneInfo.ConvertTime(utc, tz);
>
}
```

### Client: Detect and Send

```typescript
// Detect user's time zone
const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
// "Europe/Bucharest"

// Send with requests
fetch('/api/schedule', {
    headers: {
        'X-Time-Zone': timeZone
    }
});
```

## Right-to-Left (RTL) Support

### Server: No RTL Logic

API returns data only. RTL is purely client-side.

### Client: CSS Logical Properties

```css
/* RTL-aware CSS */
.container {
    padding-inline-start: 1rem;
    padding-inline-end: 1rem;
    margin-inline-start: auto;
    margin-inline-end: auto;
}

.text {
    text-align: start; /* Not left/right */
}
```

```tsx
// React: set dir attribute
<html dir={i18n.dir()}> {/* 'ltr' or 'rtl' */}
```

## Browser Compatibility

### Minimum Requirements (Consumer Choice)

| Feature | Minimum Browser |
|---------|-----------------|
| Fetch API | Chrome 42, Firefox 39, Safari 10, Edge 14 |
| Intl API | Chrome 24, Firefox 29, Safari 10, Edge 12 |
| localStorage | Chrome 4, Firefox 3.5, Safari 4, Edge 12 |
| HttpOnly Cookies | All modern browsers |
| SameSite Cookies | Chrome 51, Firefox 51, Safari 12, Edge 79 |

### Polyfills (If Needed)

```bash
# npm
npm install core-js regenerator-runtime
```

```typescript
// polyfills.ts
import 'core-js/stable';
import 'regenerator-runtime/runtime';
```

## Summary: Library vs Consumer

| Concern | Library | Consumer |
|---------|---------|----------|
| Browser storage | No | Full control |
| Authentication tokens | No | HttpOnly cookie / memory |
| API key exposure | Prevents via BFF | Implements BFF |
| Server localisation | No | `IStringLocalizer`, resources |
| Client localisation | No | i18next, format.js, Intl |
| Date/time formatting | Returns UTC ISO 8601 | `toLocaleString`, NodaTime |
| Number/currency | Invariant culture | `Intl.NumberFormat` |
| Time zones | No | IANA zones, client detection |
| RTL | No | CSS logical properties |
| Browser compat | No | Polyfills, targets |