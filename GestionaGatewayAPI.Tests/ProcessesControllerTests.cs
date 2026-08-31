using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Controllers;
using GestionaGatewayAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionaGatewayAPI.Tests;

public sealed class ProcessesControllerTests
{
    [Fact]
    public async Task GetAssigneeGroups_ReturnsGatewayResponseWithGroups()
    {
        var controller = CreateController(new TestGestionaProcessService
        {
            GetProcessAssigneeGroupsAsyncHandler = (accessTokenOverride, cancellationToken) =>
            {
                IReadOnlyList<ProcessAssigneeGroup> groups =
                [
                    new("43f83662-bb73-4c98-915a-de90219036f6", "100. Exemplo")
                ];
                return Task.FromResult(new GetProcessAssigneeGroupsResult(
                    true,
                    GetProcessFailureKind.None,
                    null,
                    groups,
                    null));
            }
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.GetAssigneeGroups(
            "operation-1",
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(okResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        var groups = Assert.IsAssignableFrom<IReadOnlyList<ProcessAssigneeGroup>>(gatewayResponse.Result);
        var group = Assert.Single(groups);
        Assert.Equal("43f83662-bb73-4c98-915a-de90219036f6", group.Id);
        Assert.Equal("100. Exemplo", group.Name);
    }

    [Fact]
    public async Task GetAssigneeUser_WhenContentTypeIsUnsupported_ReturnsGatewayError()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.ContentType = "application/vnd.gestiona.filter.assignees+json";

        var response = await controller.GetAssigneeUser(
            username: null,
            "operation-1",
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, objectResult.StatusCode);
        var gatewayResponse = Assert.IsType<GatewayResponse>(objectResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.False(gatewayResponse.Success);
        var error = Assert.IsType<ProcessError>(gatewayResponse.Result);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, error.Code);
        Assert.Equal("Unsupported Media Type", error.Name);
        Assert.Equal(GetProcessFailureKind.Validation.ToString(), error.Kind);
        Assert.Equal("Content-Type must be application/json.", error.Message);
    }

    [Fact]
    public async Task GetAssigneeUser_WhenUsernameQueryParameterIsProvided_UsesQueryParameter()
    {
        GetProcessAssigneeUserRequest? receivedRequest = null;
        var controller = CreateController(new TestGestionaProcessService
        {
            GetProcessAssigneeUserAsyncHandler = (request, accessTokenOverride, cancellationToken) =>
            {
                receivedRequest = request;
                return Task.FromResult(new GetProcessAssigneeUserResult(
                    true,
                    GetProcessFailureKind.None,
                    null,
                    new ProcessAssigneeUser("user-1", "081847637", "Luis Silva"),
                    null));
            }
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.GetAssigneeUser(
            "081847637",
            "operation-1",
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(okResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        Assert.Equal("081847637", receivedRequest!.Username);
        var user = Assert.IsType<ProcessAssigneeUser>(gatewayResponse.Result);
        Assert.Equal("user-1", user.Id);
        Assert.Equal("081847637", user.Username);
        Assert.Equal("Luis Silva", user.Name);
    }

    private static ProcessesController CreateController()
    {
        return CreateController(new TestGestionaProcessService());
    }

    private static ProcessesController CreateController(TestGestionaProcessService service)
    {
        return new ProcessesController(
            new ConfigurationBuilder().Build(),
            service,
            NullLogger<ProcessesController>.Instance);
    }

    private sealed class TestGestionaProcessService : IGestionaProcessService
    {
        public Func<string?, CancellationToken, Task<GetProcessAssigneeGroupsResult>>? GetProcessAssigneeGroupsAsyncHandler { get; init; }
        public Func<GetProcessAssigneeUserRequest, string?, CancellationToken, Task<GetProcessAssigneeUserResult>>? GetProcessAssigneeUserAsyncHandler { get; init; }

        public Task<CreateDocumentInProcessResult> CreateDocumentInProcessAsync(
            UploadDocumentRequest request,
            string processId,
            string? folderId,
            bool resolveFileIdFromProcessCode,
            string documentsFolder,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<CreateProcessResult> CreateProcessAsync(
            CreateProcessRequest request,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<GetProcessResult> GetProcessAsync(
            string processNumber,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<GetProcessThirdsResult> GetProcessThirdsAsync(
            string processId,
            bool resolveFileIdFromProcessCode,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<GetProcessDocumentsResult> GetProcessDocumentsAsync(
            string processId,
            string? documentId,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<GetProcessAssigneeUserResult> GetProcessAssigneeUserAsync(
            GetProcessAssigneeUserRequest request,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (GetProcessAssigneeUserAsyncHandler is null)
            {
                throw new NotImplementedException();
            }

            return GetProcessAssigneeUserAsyncHandler(request, accessTokenOverride, cancellationToken);
        }

        public Task<GetProcessAssigneeGroupsResult> GetProcessAssigneeGroupsAsync(
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (GetProcessAssigneeGroupsAsyncHandler is null)
            {
                throw new NotImplementedException();
            }

            return GetProcessAssigneeGroupsAsyncHandler(accessTokenOverride, cancellationToken);
        }
    }
}
