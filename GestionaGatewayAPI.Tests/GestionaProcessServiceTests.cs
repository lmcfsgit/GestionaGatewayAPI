using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionaGatewayAPI.Tests;

public sealed class GestionaProcessServiceTests
{
    [Fact]
    public async Task GetProcessThirdsAsync_WhenResolvingFromProcessCode_UsesResolvedFileId()
    {
        var resolvedProcessCode = string.Empty;
        var requestedFileId = string.Empty;
        var apiClient = new TestGestionaApiClient
        {
            GetFileIdFromProcessCodeHandler = (baseUrl, token, processCode, cancellationToken) =>
            {
                resolvedProcessCode = processCode;
                return Task.FromResult(new GestionaApiCallResult<string?>(200, true, "file-123"));
            },
            GetProcessThirdIdsAsyncHandler = (baseUrl, token, processId, cancellationToken) =>
            {
                requestedFileId = processId;
                return Task.FromResult(new GestionaApiCallResult<IReadOnlyList<string>>(
                    200,
                    true,
                    new[] { "third-1", "third-2" }));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetProcessThirdsAsync(
            "PROC-2026-001",
            resolveFileIdFromProcessCode: true,
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("PROC-2026-001", resolvedProcessCode);
        Assert.Equal("file-123", requestedFileId);
        Assert.Equal("file-123", result.ProcessId);
        Assert.Equal("third-1;third-2", result.Thirds);
    }

    [Fact]
    public async Task GetProcessThirdsAsync_WhenFileResolutionReturnsNoContent_ReturnsNotFound()
    {
        var apiClient = new TestGestionaApiClient
        {
            GetFileIdFromProcessCodeHandler = (baseUrl, token, processCode, cancellationToken) =>
            {
                return Task.FromResult(new GestionaApiCallResult<string?>(204, false, null));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetProcessThirdsAsync(
            "PROC-2026-404",
            resolveFileIdFromProcessCode: true,
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GetProcessThirdsFailureKind.NotFound, result.FailureKind);
        Assert.Contains("No Gestiona file", result.ErrorMessage);
    }

    [Fact]
    public async Task GetProcessThirdsAsync_WhenAccessTokenOverrideIsProvided_UsesOverrideToken()
    {
        var receivedTokens = new List<string>();
        var apiClient = new TestGestionaApiClient
        {
            GetFileIdFromProcessCodeHandler = (baseUrl, token, processCode, cancellationToken) =>
            {
                receivedTokens.Add(token);
                return Task.FromResult(new GestionaApiCallResult<string?>(200, true, "file-123"));
            },
            GetProcessThirdIdsAsyncHandler = (baseUrl, token, processId, cancellationToken) =>
            {
                receivedTokens.Add(token);
                return Task.FromResult(new GestionaApiCallResult<IReadOnlyList<string>>(
                    200,
                    true,
                    Array.Empty<string>()));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetProcessThirdsAsync(
            "PROC-2026-001",
            resolveFileIdFromProcessCode: true,
            accessTokenOverride: "request-token",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new[] { "request-token", "request-token" }, receivedTokens);
    }

    private static GestionaProcessService CreateService(TestGestionaApiClient apiClient)
    {
        return new GestionaProcessService(
            Options.Create(new GestionaOptions
            {
                GestionaApiBaseUrl = "https://gestiona.example/rest",
                AccessToken = "token"
            }),
            apiClient,
            NullLogger<GestionaProcessService>.Instance);
    }
}
