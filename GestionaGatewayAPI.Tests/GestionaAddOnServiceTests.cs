using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionaGatewayAPI.Tests;

public sealed class GestionaAddOnServiceTests
{
    [Fact]
    public async Task CreateAuthorizationAsync_ReturnsAuthIdFromLocationLastSegment()
    {
        string? receivedAddonToken = null;
        var apiClient = new TestGestionaApiClient
        {
            CreateAddOnAuthorizationAsyncHandler = (baseUrl, addonToken, cancellationToken) =>
            {
                receivedAddonToken = addonToken;
                return Task.FromResult(new GestionaApiCallResult<string?>(
                    201,
                    true,
                    "https://gestiona.example/rest/addon/authorizations/auth-123"));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.CreateAuthorizationAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("addon-token", receivedAddonToken);
        Assert.Equal("auth-123", result.Authorization!.AuthId);
    }

    [Fact]
    public async Task CreateAuthorizationAsync_WhenAddonTokenIsMissing_ReturnsConfigurationFailure()
    {
        var service = CreateService(
            new TestGestionaApiClient(),
            addonToken: null);

        var result = await service.CreateAuthorizationAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateAddOnAuthorizationFailureKind.Configuration, result.FailureKind);
    }

    [Fact]
    public async Task CreateAuthorizationAsync_WhenLocationIsMissing_ReturnsUpstreamFailure()
    {
        var apiClient = new TestGestionaApiClient
        {
            CreateAddOnAuthorizationAsyncHandler = (baseUrl, addonToken, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<string?>(
                    201,
                    true,
                    null))
        };
        var service = CreateService(apiClient);

        var result = await service.CreateAuthorizationAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateAddOnAuthorizationFailureKind.Upstream, result.FailureKind);
    }

    [Fact]
    public async Task GetAuthorizationAsync_WhenPending_ReturnsAuthorizeUrl()
    {
        var apiClient = new TestGestionaApiClient
        {
            GetAddOnAuthorizationAsyncHandler = (baseUrl, addonToken, authId, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<GestionaAddOnAuthorizationStatus?>(
                    401,
                    true,
                    new GestionaAddOnAuthorizationStatus("https://gestiona.example/authorize/auth-123", null)))
        };

        var result = await CreateService(apiClient).GetAuthorizationAsync("auth-123", CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.AuthorizationStatus!.Authorized);
        Assert.Equal("https://gestiona.example/authorize/auth-123", result.AuthorizationStatus.AuthorizeUrl);
        Assert.Null(result.AuthorizationStatus.AuthorizedInfo);
    }

    [Fact]
    public async Task GetAuthorizationAsync_WhenAuthorized_ReturnsUserIdAndAccessToken()
    {
        var apiClient = new TestGestionaApiClient
        {
            GetAddOnAuthorizationAsyncHandler = (baseUrl, addonToken, authId, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<GestionaAddOnAuthorizationStatus?>(
                    200,
                    true,
                    new GestionaAddOnAuthorizationStatus(
                        null,
                        new GestionaAddOnAuthorization("access-token", "user-42"))))
        };

        var result = await CreateService(apiClient).GetAuthorizationAsync("auth-123", CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.AuthorizationStatus!.Authorized);
        Assert.Equal("user-42", result.AuthorizationStatus.AuthorizedInfo!.UserId);
        Assert.Equal("access-token", result.AuthorizationStatus.AuthorizedInfo.AccessToken);
        Assert.Null(result.AuthorizationStatus.AuthorizeUrl);
    }

    private static GestionaAddOnService CreateService(
        TestGestionaApiClient apiClient,
        string? addonToken = "addon-token")
    {
        return new GestionaAddOnService(
            Options.Create(new GestionaOptions
            {
                GestionaApiBaseUrl = "https://gestiona.example/rest",
                AddonToken = addonToken
            }),
            apiClient,
            NullLogger<GestionaAddOnService>.Instance);
    }
}
