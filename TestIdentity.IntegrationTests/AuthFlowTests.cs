using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestIdentity.Identity.DTO;
using TestIdentity.Identity.Stores;

namespace TestIdentity.IntegrationTests;

public sealed class AuthFlowTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public AuthFlowTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_AllowsANewUser()
    {
        using var client = CreateClient();
        var registerRequest = TestUsers.CreateRegisterRequest();

        var response = await client.PostAsJsonAsync("/register", registerRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_RejectsDuplicateUsernameAndEmail()
    {
        using var client = CreateClient();
        var registerRequest = TestUsers.CreateRegisterRequest();

        var firstResponse = await client.PostAsJsonAsync("/register", registerRequest);
        var secondResponse = await client.PostAsJsonAsync("/register", registerRequest);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Register_IgnoresRequestedRoles_WhenSelfAssignedRolesAreDisabled()
    {
        await using var restrictedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:AllowSelfAssignedRoles"] = "false"
                });
            });
        });

        using var client = restrictedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var registerRequest = TestUsers.CreateRegisterRequest(roleIds: [1, 2]);

        var registerResponse = await client.PostAsJsonAsync("/register", registerRequest);
        var loginResponse = await client.PostAsJsonAsync("/login", TestUsers.CreateLoginRequest(registerRequest));
        var meResponse = await client.GetAsync("/me");
        var payload = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Empty(payload.Roles);
        Assert.Empty(payload.Permissions);
    }

    [Fact]
    public async Task Login_SucceedsAndMeReturnsIdentityRolesPermissionsAndSid()
    {
        using var client = CreateClient();
        var registerRequest = TestUsers.CreateRegisterRequest(roleIds: [1, 2]);

        await RegisterAsync(client, registerRequest);

        var loginResponse = await client.PostAsJsonAsync("/login", TestUsers.CreateLoginRequest(registerRequest));
        var meResponse = await client.GetAsync("/me");
        var payload = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload.IsAuthenticated);
        Assert.Equal(registerRequest.Username, payload.Name);
        Assert.Contains("Superuser", payload.Roles);
        Assert.Contains("ReadSingleForecast", payload.Permissions);
        Assert.False(string.IsNullOrWhiteSpace(payload.CurrentSid));
    }

    [Fact]
    public async Task Login_InvalidPasswordReturnsUnauthorized()
    {
        using var client = CreateClient();
        var registerRequest = TestUsers.CreateRegisterRequest();

        await RegisterAsync(client, registerRequest);

        var response = await client.PostAsJsonAsync("/login", new LoginModel
        {
            Username = registerRequest.Username,
            Password = "invalid-password",
            RememberMe = false
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RoleProtectedEndpoint_AllowsAndDeniesAsExpected()
    {
        using var superuserClient = CreateClient();
        using var plainClient = CreateClient();

        var superuser = TestUsers.CreateRegisterRequest(roleIds: [2]);
        var plainUser = TestUsers.CreateRegisterRequest(roleIds: []);

        await RegisterAndLoginAsync(superuserClient, superuser);
        await RegisterAndLoginAsync(plainClient, plainUser);

        var allowedResponse = await superuserClient.GetAsync("/api/test-role");
        var deniedResponse = await plainClient.GetAsync("/api/test-role");

        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
    }

    [Fact]
    public async Task PermissionProtectedEndpoint_AllowsAndDeniesAsExpected()
    {
        using var adminClient = CreateClient();
        using var superuserClient = CreateClient();

        var admin = TestUsers.CreateRegisterRequest(roleIds: [1]);
        var superuser = TestUsers.CreateRegisterRequest(roleIds: [2]);

        await RegisterAndLoginAsync(adminClient, admin);
        await RegisterAndLoginAsync(superuserClient, superuser);

        var allowedResponse = await adminClient.GetAsync("/api/weatherforecast/single");
        var deniedResponse = await superuserClient.GetAsync("/api/weatherforecast/single");

        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
    }

    [Fact]
    public async Task Sessions_ListsOnlyTheAuthenticatedUsersSessions()
    {
        using var firstUserClient = CreateClient();
        using var secondUserClient = CreateClient();

        var firstUser = TestUsers.CreateRegisterRequest(roleIds: [1]);
        var secondUser = TestUsers.CreateRegisterRequest(roleIds: [2]);

        await RegisterAndLoginAsync(firstUserClient, firstUser);
        await RegisterAndLoginAsync(secondUserClient, secondUser);

        var firstUserSessionsResponse = await firstUserClient.GetAsync("/sessions");
        var firstUserSessions = await firstUserSessionsResponse.Content.ReadFromJsonAsync<SessionInfoResponse[]>();

        Assert.Equal(HttpStatusCode.OK, firstUserSessionsResponse.StatusCode);
        Assert.NotNull(firstUserSessions);
        Assert.Single(firstUserSessions);
        Assert.All(firstUserSessions, session => Assert.False(string.IsNullOrWhiteSpace(session.SessionId)));
    }

    [Fact]
    public async Task Sessions_CanTrackMultipleSessionsForTheSameUser()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();

        var user = TestUsers.CreateRegisterRequest(roleIds: [1, 2]);

        await RegisterAsync(firstClient, user);
        await LoginAsync(firstClient, user);
        await LoginAsync(secondClient, user);

        var sessionsResponse = await firstClient.GetAsync("/sessions");
        var sessions = await sessionsResponse.Content.ReadFromJsonAsync<SessionInfoResponse[]>();

        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
        Assert.NotNull(sessions);
        Assert.Equal(2, sessions.Length);
        Assert.All(sessions, session => Assert.NotNull(session.ExpiresUtc));
    }

    [Fact]
    public async Task Logout_WithSidRevokesOnlyOwnedSession()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        using var thirdClient = CreateClient();

        var user = TestUsers.CreateRegisterRequest(roleIds: [1, 2]);

        await RegisterAsync(firstClient, user);
        await LoginAsync(firstClient, user);
        await LoginAsync(secondClient, user);
        var firstClientCurrentUser = await GetCurrentUserAsync(firstClient);

        var secondUser = TestUsers.CreateRegisterRequest(roleIds: [1]);
        await RegisterAndLoginAsync(thirdClient, secondUser);

        var sessions = await GetSessionsAsync(firstClient);
        var sessionToRemove = sessions.Single(session => session.SessionId != null && session.SessionId != firstClientCurrentUser.CurrentSid);

        var removeResponse = await firstClient.PostAsync($"/logout?sid={sessionToRemove.SessionId}", content: null);
        var secondClientProtectedResponse = await secondClient.GetAsync("/api/weatherforecast");
        var thirdPartySessionAttempt = await thirdClient.PostAsync($"/logout?sid={sessionToRemove.SessionId}", content: null);

        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondClientProtectedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, thirdPartySessionAttempt.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_InvalidatesEverySessionForTheUser()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();

        var user = TestUsers.CreateRegisterRequest(roleIds: [1, 2]);

        await RegisterAsync(firstClient, user);
        await LoginAsync(firstClient, user);
        await LoginAsync(secondClient, user);

        var logoutAllResponse = await firstClient.PostAsync("/logout-all", content: null);
        var firstProtectedResponse = await firstClient.GetAsync("/api/weatherforecast");
        var secondProtectedResponse = await secondClient.GetAsync("/api/weatherforecast");

        Assert.Equal(HttpStatusCode.OK, logoutAllResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, firstProtectedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondProtectedResponse.StatusCode);
    }

    [Fact]
    public async Task Sessions_CleansUpOrphanedSessionIds()
    {
        using var client = CreateClient();
        var user = TestUsers.CreateRegisterRequest(roleIds: [1]);

        await RegisterAndLoginAsync(client, user);
        var currentUser = await GetCurrentUserAsync(client);

        var cache = _factory.Services.GetRequiredService<IDistributedCache>();
        await cache.RemoveAsync($"auth:ticket:{currentUser.CurrentSid}");

        var sessionsResponse = await client.GetAsync("/sessions");
        var sessions = await sessionsResponse.Content.ReadFromJsonAsync<SessionInfoResponse[]>();

        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
        Assert.NotNull(sessions);
        Assert.Empty(sessions);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    private static async Task RegisterAsync(HttpClient client, RegisterModel request)
    {
        var response = await client.PostAsJsonAsync("/register", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, RegisterModel request)
    {
        var response = await client.PostAsJsonAsync("/login", TestUsers.CreateLoginRequest(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task RegisterAndLoginAsync(HttpClient client, RegisterModel request)
    {
        await RegisterAsync(client, request);
        await LoginAsync(client, request);
    }

    private static async Task<CurrentUserResponse> GetCurrentUserAsync(HttpClient client)
    {
        var response = await client.GetAsync("/me");
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);

        return payload;
    }

    private static async Task<SessionInfoResponse[]> GetSessionsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/sessions");
        var payload = await response.Content.ReadFromJsonAsync<SessionInfoResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);

        return payload;
    }

    private static class TestUsers
    {
        public static RegisterModel CreateRegisterRequest(int[]? roleIds = null)
        {
            var unique = Guid.NewGuid().ToString("N")[..12];
            return new RegisterModel
            {
                Name = $"User {unique}",
                Email = $"{unique}@example.com",
                Username = $"user_{unique}",
                Password = "StrongPassword!1",
                Roles = roleIds?.ToList() ?? [1]
            };
        }

        public static LoginModel CreateLoginRequest(RegisterModel registerModel)
        {
            return new LoginModel
            {
                Username = registerModel.Username,
                Password = registerModel.Password,
                RememberMe = false
            };
        }
    }
}
