using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionaGatewayAPI.Tests;

public sealed class GestionaThirdServiceTests
{
    [Fact]
    public async Task GetThirdByNifAsync_ResolvesThirdIdAndReturnsEnrichedThird()
    {
        var resolvedNif = string.Empty;
        var requestedThirdId = string.Empty;
        var apiClient = new TestGestionaApiClient
        {
            GetThirdIdByNifAsyncHandler = (baseUrl, token, nif, cancellationToken) =>
            {
                resolvedNif = nif;
                return Task.FromResult(new GestionaApiCallResult<string?>(200, true, "third-123"));
            },
            GetThirdAsyncHandler = (baseUrl, token, thirdId, cancellationToken) =>
            {
                requestedThirdId = thirdId;
                return Task.FromResult(new GestionaApiCallResult<Third?>(200, true, new Third(
                    FullName: "Luis Silva Fernandes",
                    FirstName: "Luis",
                    SecondSurname: "Fernandes",
                    NifCountry: "ESP",
                    Id: thirdId,
                    Nif: "196510880",
                    Type: "PHISIC",
                    Email: "luis@example.com",
                    Mobile: "913347827",
                    NifType: "OWN",
                    Address: null,
                    Number: null,
                    ZipCode: null,
                    Province: null,
                    Country: null,
                    TypeOfRoad: null,
                    Zone: null,
                    ParishCode: null)));
            },
            GetThirdDefaultAddressAsyncHandler = (baseUrl, token, thirdId, cancellationToken) =>
            {
                return Task.FromResult(new GestionaApiCallResult<ThirdDefaultAddress?>(200, true, new ThirdDefaultAddress(
                    "Rua das Cancelas",
                    "184",
                    "4440368",
                    "PORTO",
                    "Portugal",
                    "CL",
                    "Norte",
                    [
                        new GestionaLink("self", "https://gestiona.example/rest/thirds/third-123/default-address", null),
                        new GestionaLink("parish", "https://gestiona.example/rest/parishes/130101", null)
                    ])));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetThirdByNifAsync(
            "196510880",
            accessTokenOverride: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("196510880", resolvedNif);
        Assert.Equal("third-123", requestedThirdId);
        Assert.NotNull(result.Third);
        Assert.Equal("Luis Silva Fernandes", result.Third.FullName);
        Assert.Equal("Luis", result.Third.FirstName);
        Assert.Equal("Fernandes", result.Third.SecondSurname);
        Assert.Equal("Rua das Cancelas", result.Third.Address);
        Assert.Equal("4440368", result.Third.ZipCode);
        Assert.Equal("CL", result.Third.TypeOfRoad);
        Assert.Equal("Norte", result.Third.Zone);
        Assert.Equal("130101", result.Third.ParishCode);
    }

    [Fact]
    public async Task GetThirdByNifAsync_WhenNoSingleThirdIsResolved_ReturnsNotFound()
    {
        var apiClient = new TestGestionaApiClient
        {
            GetThirdIdByNifAsyncHandler = (baseUrl, token, nif, cancellationToken) =>
            {
                return Task.FromResult(new GestionaApiCallResult<string?>(200, true, null));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetThirdByNifAsync(
            "196510880",
            accessTokenOverride: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(
            result.FailureKind == GetThirdFailureKind.NotFound,
            $"Expected FailureKind to be {GetThirdFailureKind.NotFound} when no single third is resolved by NIF, but got {result.FailureKind}.");
        Assert.Contains("No single Gestiona third", result.ErrorMessage);
    }

    [Fact]
    public async Task GetThirdByNifAsync_WhenAccessTokenOverrideIsProvided_UsesOverrideToken()
    {
        var receivedTokens = new List<string>();
        var apiClient = new TestGestionaApiClient
        {
            GetThirdIdByNifAsyncHandler = (baseUrl, token, nif, cancellationToken) =>
            {
                receivedTokens.Add(token);
                return Task.FromResult(new GestionaApiCallResult<string?>(200, true, "third-123"));
            },
            GetThirdAsyncHandler = (baseUrl, token, thirdId, cancellationToken) =>
            {
                receivedTokens.Add(token);
                return Task.FromResult(new GestionaApiCallResult<Third?>(200, true, new Third(
                    FullName: "Luis Silva Fernandes",
                    FirstName: "Luis",
                    SecondSurname: "Fernandes",
                    NifCountry: "ESP",
                    Id: thirdId,
                    Nif: "196510880",
                    Type: "PHISIC",
                    Email: "luis@example.com",
                    Mobile: "913347827",
                    NifType: "OWN",
                    Address: null,
                    Number: null,
                    ZipCode: null,
                    Province: null,
                    Country: null,
                    TypeOfRoad: null,
                    Zone: null,
                    ParishCode: null)));
            },
            GetThirdDefaultAddressAsyncHandler = (baseUrl, token, thirdId, cancellationToken) =>
            {
                receivedTokens.Add(token);
                return Task.FromResult(new GestionaApiCallResult<ThirdDefaultAddress?>(200, true, null));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetThirdByNifAsync(
            "196510880",
            accessTokenOverride: "request-token",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new[] { "request-token", "request-token", "request-token" }, receivedTokens);
    }

    private static GestionaThirdService CreateService(TestGestionaApiClient apiClient)
    {
        return new GestionaThirdService(
            Options.Create(new GestionaOptions
            {
                GestionaApiBaseUrl = "https://gestiona.example/rest",
                AccessToken = "token"
            }),
            apiClient,
            NullLogger<GestionaThirdService>.Instance);
    }
}
