# Quick Start Guide

## Prerequisites

- .NET 10.0 SDK
- IDE: VS Code, Visual Studio, or Rider
- NuGet access (nuget.org)

## 1. Create Project

```bash
dotnet new webapi -n MyApi
cd MyApi
```

## 2. Add Package

```bash
dotnet add package NuciAPI.Controllers --version 2.3.1
```

## 3. Configure Authorisation

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Register authorisation (singleton for static key)
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var key = config["ApiKey"] ?? "dev-key-change-in-production";
    return NuciApiAuthorisation.ApiKey(key);
});

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
```

```json
// appsettings.json
{
  "ApiKey": "your-secure-api-key-here",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## 4. Create Controller

```csharp
// Controllers/UsersController.cs
using Microsoft.AspNetCore.Mvc;
using NuciAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : NuciApiController
{
    private readonly IUserService _users;
    private readonly NuciApiAuthorisation _auth;

    public UsersController(IUserService users, NuciApiAuthorisation auth)
    {
        _users = users;
        _auth = auth;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<UserDto> Get(Guid id)
    {
        return ProcessRequest(new GetUserRequest { Id = id }, () =>
        {
            var user = _users.GetById(id);
            return user != null ? new UserDto(user) : null;
        }, _auth);
    }

    [HttpPost]
    public ActionResult<UserDto> Create(CreateUserRequest request)
    {
        return ProcessRequest(request, () =>
        {
            var user = _users.Create(request);
            return new UserDto(user);
        }, _auth);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request)
    {
        return await ProcessRequestAsync(request, async () =>
        {
            var user = await _users.UpdateAsync(id, request);
            return new UserDto(user);
        }, _auth);
    }
}
```

## 5. Define Request/Response Models

```csharp
// Models/Requests.cs
public class GetUserRequest
{
    public Guid Id { get; set; }
}

public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(12)]
    public string Password { get; set; } = string.Empty;

    [StringLength(100)]
    public string? DisplayName { get; set; }
}

public class UpdateUserRequest
{
    [StringLength(100)]
    public string? DisplayName { get; set; }
}

// Models/Responses.cs
public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public UserDto() { }

    public UserDto(User user)
    {
        Id = user.Id;
        Email = user.Email;
        DisplayName = user.DisplayName;
        CreatedAt = user.CreatedAt;
    }
}
```

## 6. Add Service

```csharp
// Services/IUserService.cs
public interface IUserService
{
    User? GetById(Guid id);
    User Create(CreateUserRequest request);
    Task<User> UpdateAsync(Guid id, UpdateUserRequest request);
}

// Services/UserService.cs
public class UserService : IUserService
{
    private readonly List<User> _users = new();

    public User? GetById(Guid id) => _users.FirstOrDefault(u => u.Id == id);

    public User Create(CreateUserRequest request)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName ?? request.Email.Split('@')[0],
            CreatedAt = DateTime.UtcNow
        };
        _users.Add(user);
        return user;
    }

    public async Task<User> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        await Task.Yield(); // Simulate async
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user == null) throw new KeyNotFoundException("User not found");

        if (request.DisplayName != null) user.DisplayName = request.DisplayName;
        return user;
    }
}

// Domain/User.cs
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

```csharp
// Program.cs - Register service
builder.Services.AddScoped<IUserService, UserService>();
```

## 7. Run and Test

```bash
dotnet run
```

### Test with curl

```bash
# Get user (will 404 - not found)
curl -H "Authorization: Bearer your-secure-api-key-here" \
  http://localhost:5000/api/users/00000000-0000-0000-0000-000000000000

# Create user
curl -X POST -H "Authorization: Bearer your-secure-api-key-here" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"SecurePass123!","displayName":"Test User"}' \
  http://localhost:5000/api/users

# Update user (use ID from create response)
curl -X PUT -H "Authorization: Bearer your-secure-api-key-here" \
  -H "Content-Type: application/json" \
  -d '{"displayName":"Updated Name"}' \
  http://localhost:5000/api/users/{id}
```

## 8. Enable Swagger (Optional)

```csharp
// Program.cs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

Visit `https://localhost:7xxx/swagger` for interactive docs.

## Next Steps

| Topic | Document |
|-------|----------|
| Async patterns | [API Usage Examples](../api-usage-examples.md) |
| HMAC authentication | [HMAC Extraction Flow](../flows/hmac-extraction.md) |
| Error handling | [Error Handling](../error-handling.md) |
| Testing | [Testing Guide](../testing.md) |
| Deployment | [Build & Deployment](../build-and-deployment.md) |
| Architecture | [Architecture](../architecture.md) |

## Complete Example Structure

```
MyApi/
├── Controllers/
│   └── UsersController.cs
├── Models/
│   ├── Requests.cs
│   └── Responses.cs
├── Services/
│   ├── IUserService.cs
│   └── UserService.cs
├── Domain/
│   └── User.cs
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
└── MyApi.csproj
```

## Common Issues

| Issue | Solution |
|-------|----------|
| 401 Unauthorized | Check `Authorization: Bearer <key>` header |
| 400 Bad Request | Validate request model (DataAnnotations) |
| DI Error | Register `NuciApiAuthorisation` and services |
| CS4014 Warning | Add `await` to async `ProcessRequestAsync` calls |