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
    public async Task GetProcessAsync_ResolvesFileIdFromProcessNumber()
    {
        var resolvedProcessCode = string.Empty;
        var apiClient = new TestGestionaApiClient
        {
            GetFileIdFromProcessCodeHandler = (baseUrl, token, processCode, cancellationToken) =>
            {
                resolvedProcessCode = processCode;
                return Task.FromResult(new GestionaApiCallResult<string?>(200, true, "file-123"));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetProcessAsync(
            "PROC-2026-001",
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("PROC-2026-001", resolvedProcessCode);
        Assert.Equal("file-123", result.ProcessId);
        Assert.Equal("PROC-2026-001", result.ProcessNumber);
    }

    [Fact]
    public async Task GetProcessAsync_WhenFileResolutionReturnsNoContent_ReturnsNotFound()
    {
        var apiClient = new TestGestionaApiClient
        {
            GetFileIdFromProcessCodeHandler = (baseUrl, token, processCode, cancellationToken) =>
            {
                return Task.FromResult(new GestionaApiCallResult<string?>(204, false, null));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetProcessAsync(
            "PROC-2026-404",
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GetProcessFailureKind.NotFound, result.FailureKind);
        Assert.Contains("No Gestiona file", result.ErrorMessage);
    }

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

    [Fact]
    public async Task GetProcessDocumentsAsync_ReturnsMappedDocumentsAndFolders()
    {
        var requestedFileId = string.Empty;
        var apiClient = new TestGestionaApiClient
        {
            GetProcessDocumentsAsyncHandler = (baseUrl, token, processId, documentId, cancellationToken) =>
            {
                requestedFileId = processId;
                IReadOnlyList<ProcessDocument> documents =
                [
                    new("DOC", "POC_SIGMA_Gestiona", "document-1"),
                    new("FOLDER", "xxxx", "folder-1")
                ];
                return Task.FromResult(
                    new GestionaApiCallResult<IReadOnlyList<ProcessDocument>>(200, true, documents));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetProcessDocumentsAsync(
            "file-123",
            documentId: null,
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("file-123", requestedFileId);
        Assert.Collection(
            result.Documents!,
            item =>
            {
                Assert.Equal("DOC", item.Type);
                Assert.Equal("POC_SIGMA_Gestiona", item.Name);
                Assert.Equal("document-1", item.Id);
            },
            item =>
            {
                Assert.Equal("FOLDER", item.Type);
                Assert.Equal("xxxx", item.Name);
                Assert.Equal("folder-1", item.Id);
            });
    }

    [Fact]
    public async Task GetProcessDocumentsAsync_WhenUpstreamReturnsNotFound_ReturnsNotFound()
    {
        var apiClient = new TestGestionaApiClient
        {
            GetProcessDocumentsAsyncHandler = (baseUrl, token, processId, documentId, cancellationToken) =>
                Task.FromResult(
                    new GestionaApiCallResult<IReadOnlyList<ProcessDocument>>(404, false, []))
        };
        var service = CreateService(apiClient);

        var result = await service.GetProcessDocumentsAsync(
            "missing-file",
            documentId: null,
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GetProcessDocumentsFailureKind.NotFound, result.FailureKind);
        Assert.Equal(404, result.UpstreamStatusCode);
    }

    [Fact]
    public async Task GetProcessDocumentsAsync_WithDocumentId_ForwardsDocumentId()
    {
        string? requestedDocumentId = null;
        var apiClient = new TestGestionaApiClient
        {
            GetProcessDocumentsAsyncHandler = (baseUrl, token, processId, documentId, cancellationToken) =>
            {
                requestedDocumentId = documentId;
                return Task.FromResult(
                    new GestionaApiCallResult<IReadOnlyList<ProcessDocument>>(200, true, []));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetProcessDocumentsAsync(
            "file-123",
            "folder-456",
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("folder-456", requestedDocumentId);
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
