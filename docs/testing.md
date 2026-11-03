# Testing

## Test Strategy

### Pyramid

```
        /\
       /  \     E2E / Integration (few)
      /----\
     /      \   Component / Contract (some)
    /--------\
   /          \ Unit (many)
  /------------\
```

### Distribution Target

| Layer | % | Count (est.) | Speed |
|-------|---|--------------|-------|
| Unit | 70% | 100+ | <100ms |
| Component | 20% | 30+ | <1s |
| Integration | 10% | 15+ | <10s |

## Unit Testing

### Framework

```xml
<!-- NuciAPI.Controllers.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NUnit" Version="4.2.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\NuciAPI.Controllers\NuciAPI.Controllers.csproj" />
  </ItemGroup>
</Project>
```

### NuciApiController Tests

```csharp
[TestFixture]
public class NuciApiControllerTests
{
    private class TestController : NuciApiController { }

    private TestController _controller;
    private Mock<NuciApiAuthorisation> _mockAuth;

    [SetUp]
    public void SetUp()
    {
        _controller = new TestController();
        _mockAuth = new Mock<NuciApiAuthorisation>();
        _mockAuth.Setup(a => a.Authorise(It.IsAny<HttpRequest>()))
            .Returns(Task.CompletedTask);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Test]
    public void ProcessRequest_SyncAction_ReturnsOk()
    {
        var request = new TestRequest { Value = "test" };
        var executed = false;

        var result = _controller.ProcessRequest(request, () => executed = true, _mockAuth.Object);

        result.Should().BeOfType<OkResult>();
        executed.Should().BeTrue();
        _mockAuth.Verify(a => a.Authorise(It.IsAny<HttpRequest>()), Times.Once);
    }

    [Test]
    public void ProcessRequest_SyncFunc_ReturnsOkWithValue()
    {
        var request = new TestRequest { Value = "test" };
        const string expected = "result";

        var result = _controller.ProcessRequest(request, () => expected, _mockAuth.Object);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Test]
    public async Task ProcessRequestAsync_AsyncAction_ReturnsOk()
    {
        var request = new TestRequest { Value = "test" };
        var executed = false;

        var result = await _controller.ProcessRequestAsync(request, async () =>
        {
            await Task.Yield();
            executed = true;
        }, _mockAuth.Object);

        result.Should().BeOfType<OkResult>();
        executed.Should().BeTrue();
    }

    [Test]
    public async Task ProcessRequestAsync_AsyncFunc_ReturnsOkWithValue()
    {
        var request = new TestRequest { Value = "test" };
        const string expected = "async-result";

        var result = await _controller.ProcessRequestAsync(request, async () =>
        {
            await Task.Yield();
            return expected;
        }, _mockAuth.Object);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Test]
    public void ProcessRequest_AuthorisationThrows_ReturnsUnauthorized()
    {
        var request = new TestRequest { Value = "test" };
        _mockAuth.Setup(a => a.Authorise(It.IsAny<HttpRequest>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid key"));

        var result = _controller.ProcessRequest(request, () => { }, _mockAuth.Object);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Test]
    public void GetHmacToken_HeaderPresent_ReturnsValue()
    {
        _controller.ControllerContext.HttpContext.Request.Headers["X-HMAC"] = "test-hmac";

        var result = _controller.GetHmacToken();

        result.Should().Be("test-hmac");
    }

    [Test]
    public void GetHmacToken_HeaderMissing_ReturnsNull()
    {
        var result = _controller.GetHmacToken();

        result.Should().BeNull();
    }

    private class TestRequest { public string Value { get; set; } = string.Empty; }
}
```

### NuciApiAuthorisation Tests

```csharp
[TestFixture]
public class NuciApiAuthorisationTests
{
    [Test]
    public void None_Authorise_DoesNotThrow()
    {
        var auth = NuciApiAuthorisation.None;
        var context = new DefaultHttpContext();

        Assert.DoesNotThrowAsync(() => auth.Authorise(context.Request));
    }

    [Test]
    public void ApiKey_ValidKey_Authorises()
    {
        const string key = "valid-key";
        var auth = NuciApiAuthorisation.ApiKey(key);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {key}";

        Assert.DoesNotThrowAsync(() => auth.Authorise(context.Request));
    }

    [Test]
    public void ApiKey_InvalidKey_ThrowsUnauthorized()
    {
        var auth = NuciApiAuthorisation.ApiKey("correct-key");
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer wrong-key";

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.Authorise(context.Request));

        ex.Message.Should().Contain("Invalid API key");
    }

    [Test]
    public void ApiKey_MissingHeader_ThrowsUnauthorized()
    {
        var auth = NuciApiAuthorisation.ApiKey("key");
        var context = new DefaultHttpContext();

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.Authorise(context.Request));

        ex.Message.Should().Contain("Authorization header");
    }

    [Test]
    public void ApiKey_MalformedHeader_ThrowsUnauthorized()
    {
        var auth = NuciApiAuthorisation.ApiKey("key");
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "NotBearer token";

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.Authorise(context.Request));

        ex.Message.Should().Contain("Bearer");
    }
}
```

## Component Testing

### Controller Integration Tests

```csharp
[TestFixture]
public class MyControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MyControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<NuciApiAuthorisation>(NuciApiAuthorisation.None);
            });
        });
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task Get_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/my");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Post_ValidRequest_ReturnsCreated()
    {
        var request = new CreateRequest { Name = "Test" };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/my", content);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Post_InvalidRequest_ReturnsBadRequest()
    {
        var request = new CreateRequest { Name = "" };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/my", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

### Authorisation Integration Tests

```csharp
[TestFixture]
public class AuthorisationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthorisationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Test]
    public async Task ApiKey_Valid_ReturnsOk()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-key");

        var response = await client.GetAsync("/api/protected");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ApiKey_Invalid_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "wrong-key");

        var response = await client.GetAsync("/api/protected");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ApiKey_Missing_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/protected");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

## Integration Testing

### Testcontainers (Database)

```csharp
[TestFixture]
public class DatabaseIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("testdb")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private AppDbContext _dbContext;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        _dbContext = new AppDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    [Test]
    public async Task UserRepository_CreateAndRetrieve_Works()
    {
        var repo = new EfUserRepository(_dbContext);
        var user = new User { Email = "test@example.com" };

        var created = await repo.AddAsync(user);
        var retrieved = await repo.GetByIdAsync(created.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Email.Should().Be("test@example.com");
    }
}
```

### Testcontainers (Redis)

```csharp
[TestFixture]
public class CacheIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IDistributedCache _cache;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();

        var services = new ServiceCollection();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = _redis.GetConnectionString();
        });

        var provider = services.BuildServiceProvider();
        _cache = provider.GetRequiredService<IDistributedCache>();
    }

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
    }

    [Test]
    public async Task DistributedCache_SetAndGet_Works()
    {
        await _cache.SetStringAsync("key", "value");
        var result = await _cache.GetStringAsync("key");
        result.Should().Be("value");
    }
}
```

## Contract Testing

### OpenAPI Validation

```csharp
[TestFixture]
public class OpenApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Test]
    public async Task OpenApiDocument_IsValid()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = OpenApiDocument.Parse(content);
        document.Validate().Should().BeEmpty();
    }

    [Test]
    public async Task AllEndpoints_HaveResponses()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();
        var document = OpenApiDocument.Parse(content);

        foreach (var path in document.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                operation.Value.Responses.Should().ContainKey("200");
                operation.Value.Responses.Should().ContainKey("400");
                operation.Value.Responses.Should().ContainKey("401");
                operation.Value.Responses.Should().ContainKey("500");
            }
        }
    }
}
```

## Test Data

### Builders

```csharp
public class TestRequestBuilder
{
    private string _value = "default";
    private int _count = 1;

    public TestRequestBuilder WithValue(string value)
    {
        _value = value;
        return this;
    }

    public TestRequestBuilder WithCount(int count)
    {
        _count = count;
        return this;
    }

    public TestRequest Build() => new TestRequest
    {
        Value = _value,
        Count = _count
    };
}

var request = new TestRequestBuilder()
    .WithValue("custom")
    .WithCount(5)
    .Build();
```

### AutoFixture

```csharp
[TestFixture]
public class AutoFixtureTests
{
    private IFixture _fixture;

    [SetUp]
    public void SetUp()
    {
        _fixture = new Fixture()
            .Customize(new AutoMoqCustomization())
            .Customize(new SupportMutableValueTypesCustomization());
    }

    [Test]
    public void CreateRequest_GeneratesValidData()
    {
        var request = _fixture.Create<CreateUserRequest>();
        request.Email.Should().NotBeNullOrEmpty();
        request.Password.Should().NotBeNullOrEmpty();
    }
}
```

## Test Organization

### Directory Structure

```
tests/
├── NuciAPI.Controllers.Tests/
│   ├── Unit/
│   │   ├── Controllers/
│   │   │   └── NuciApiControllerTests.cs
│   │   ├── Authorisation/
│   │   │   ├── NuciApiAuthorisationTests.cs
│   │   │   ├── NuciApiKeyAuthorisationTests.cs
│   │   │   └── NuciApiNoneAuthorisationTests.cs
│   │   └── Helpers/
│   ├── Component/
│   │   ├── Controllers/
│   │   │   └── MyControllerTests.cs
│   │   └── Authorisation/
│   │       └── AuthorisationIntegrationTests.cs
│   ├── Integration/
│   │   ├── Database/
│   │   │   └── DatabaseIntegrationTests.cs
│   │   └── Cache/
│   │       └── CacheIntegrationTests.cs
│   └── Contract/
│       └── OpenApiContractTests.cs
```

### Naming Convention

```csharp
// Pattern: [ClassName]Tests.cs
// Method: [MethodName]_[Scenario]_[ExpectedResult]

[Test]
public void ProcessRequest_ValidRequest_ReturnsOk()

[Test]
public void ProcessRequest_AuthorisationFails_ReturnsUnauthorized()

[Test]
public async Task ProcessRequestAsync_AsyncOperation_ReturnsOk()
```

## CI/CD Integration

### GitHub Actions

```yaml
# .github/workflows/dotnet.yml
- name: Run tests
  run: |
    dotnet test NuciAPI.Controllers.Tests/NuciAPI.Controllers.Tests.csproj \
      --configuration Release \
      --no-build \
      --collect:"XPlat Code Coverage" \
      --results-directory TestResults \
      --logger "trx;LogFileName=test_results.trx"

- name: Upload coverage
  uses: codecov/codecov-action@v3
  with:
    files: TestResults/**/coverage.cobertura.xml
```

### Coverage Thresholds

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <CoverletOutputFormat>cobertura,opencover</CoverletOutputFormat>
  <Threshold>80</Threshold>
  <ThresholdType>line</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

## Test Checklist

- [ ] Unit tests for all public methods
- [ ] Authorisation tests: valid, invalid, missing, malformed
- [ ] Controller tests: sync, async, error paths
- [ ] Component tests with WebApplicationFactory
- [ ] Integration tests with Testcontainers
- [ ] Contract tests for OpenAPI
- [ ] Coverage ≥ 80% lines
- [ ] Tests run in CI on every PR
- [ ] Flaky tests identified and fixed
- [ ] Test data builders for complex objects