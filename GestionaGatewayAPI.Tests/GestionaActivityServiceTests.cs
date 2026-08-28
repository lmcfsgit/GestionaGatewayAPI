using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionaGatewayAPI.Tests;

public sealed class GestionaActivityServiceTests
{
    [Fact]
    public async Task GetActivitiesAsync_ReturnsActivitiesFromGestionaContent()
    {
        var apiClient = new TestGestionaApiClient
        {
            GetActivitiesAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<Activity>>(
                    200,
                    true,
                    [
                        new Activity("activity-1", "Expediente Geral"),
                        new Activity("activity-2", "Licenciamento")
                    ]))
        };
        var service = CreateService(apiClient);

        var result = await service.GetActivitiesAsync(
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Collection(
            result.Activities!,
            activity =>
            {
                Assert.Equal("activity-1", activity.Id);
                Assert.Equal("Expediente Geral", activity.Name);
            },
            activity =>
            {
                Assert.Equal("activity-2", activity.Id);
                Assert.Equal("Licenciamento", activity.Name);
            });
    }

    [Fact]
    public async Task GetActivitiesAsync_WhenUpstreamFails_ReturnsUpstreamFailure()
    {
        var apiClient = new TestGestionaApiClient
        {
            GetActivitiesAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<Activity>>(
                    502,
                    false,
                    []))
        };
        var service = CreateService(apiClient);

        var result = await service.GetActivitiesAsync(
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GetActivitiesFailureKind.Upstream, result.FailureKind);
        Assert.Equal(502, result.UpstreamStatusCode);
    }

    [Fact]
    public async Task GetActivitiesAsync_WhenAccessTokenOverrideIsProvided_UsesOverrideToken()
    {
        string? receivedToken = null;
        var apiClient = new TestGestionaApiClient
        {
            GetActivitiesAsyncHandler = (baseUrl, token, cancellationToken) =>
            {
                receivedToken = token;
                return Task.FromResult(new GestionaApiCallResult<IReadOnlyList<Activity>>(
                    200,
                    true,
                    []));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetActivitiesAsync(
            accessTokenOverride: "request-token",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("request-token", receivedToken);
    }

    [Fact]
    public async Task GetProceduresAsync_ReturnsProceduresWithTitlesMappedToNames()
    {
        string? receivedActivityId = null;
        var apiClient = new TestGestionaApiClient
        {
            GetExternalProceduresAsyncHandler = (baseUrl, token, activityId, cancellationToken) =>
            {
                receivedActivityId = activityId;
                return Task.FromResult(new GestionaApiCallResult<IReadOnlyList<ExternalProcedure>>(
                    200,
                    true,
                    [
                        new ExternalProcedure("procedure-1", "Procedimento Generico"),
                        new ExternalProcedure("procedure-2", "Licenciamento")
                    ]));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetProceduresAsync(
            "activity-123",
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("activity-123", receivedActivityId);
        Assert.Collection(
            result.Procedures!,
            procedure =>
            {
                Assert.Equal("procedure-1", procedure.Id);
                Assert.Equal("Procedimento Generico", procedure.Name);
            },
            procedure =>
            {
                Assert.Equal("procedure-2", procedure.Id);
                Assert.Equal("Licenciamento", procedure.Name);
            });
    }

    private static GestionaActivityService CreateService(TestGestionaApiClient apiClient)
    {
        return new GestionaActivityService(
            Options.Create(new GestionaOptions
            {
                GestionaApiBaseUrl = "https://gestiona.example/rest",
                AccessToken = "token"
            }),
            apiClient,
            NullLogger<GestionaActivityService>.Instance);
    }
}
