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
    public async Task UploadToFile_WhenGestionaRejectsDocument_ReturnsDetailsInGatewayResult()
    {
        var controller = CreateController(new TestGestionaProcessService
        {
            CreateDocumentInProcessAsyncHandler = (request, processId, folderId, resolveFileId, documentsFolder, accessToken, cancellationToken) =>
                Task.FromResult(new CreateDocumentInProcessResult(
                    false,
                    CreateDocumentInProcessFailureKind.Upstream,
                    "The document could not be created.; The file is locked.",
                    null,
                    400))
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Body = new MemoryStream(
            """{"OperationId":"operation-1","Name":"Document","DocumentSourceType":"EXTERNAL_URL","Url":"https://example.com/document"}"""u8.ToArray());

        var response = await controller.UploadToFile(
            "360f3d62-a745-4135-86cb-1ad41e0657cd",
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var gatewayResponse = Assert.IsType<GatewayResponse>(objectResult.Value);
        Assert.False(gatewayResponse.Success);
        var error = Assert.IsType<UploadDocumentError>(gatewayResponse.Result);
        Assert.Equal("The document could not be created.; The file is locked.", error.Message);
    }

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

    [Fact]
    public async Task RelateProcesses_ReturnsOriginalRequestBody()
    {
        RelatedProcessesRequest? receivedRequest = null;
        var controller = CreateController(new TestGestionaProcessService
        {
            RelateProcessesAsyncHandler = (request, accessTokenOverride, cancellationToken) =>
            {
                receivedRequest = request;
                return Task.FromResult(new RelatedProcessesResult(
                    true,
                    GetProcessFailureKind.None,
                    null,
                    request,
                    null));
            }
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new RelatedProcessesRequest("file-1", "file-2");
        var response = await controller.RelateProcesses(
            request,
            "operation-1",
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(okResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        Assert.Same(request, receivedRequest);
        var relatedProcesses = Assert.IsType<RelatedProcessesRequest>(gatewayResponse.Result);
        Assert.Equal("file-1", relatedProcesses.Id1);
        Assert.Equal("file-2", relatedProcesses.Id2);
    }

    [Fact]
    public async Task RelateProcesses_WhenId1IsMissing_ReturnsGatewayError()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.RelateProcesses(
            new RelatedProcessesRequest(" ", "file-2"),
            "operation-1",
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var gatewayResponse = Assert.IsType<GatewayResponse>(objectResult.Value);
        Assert.False(gatewayResponse.Success);
        var error = Assert.IsType<ProcessError>(gatewayResponse.Result);
        Assert.Equal(GetProcessFailureKind.Validation.ToString(), error.Kind);
        Assert.Equal("id1 is required.", error.Message);
    }

    [Fact]
    public async Task DeleteRelatedProcess_ReturnsGatewayResponseWithEmptyObject()
    {
        string? receivedProcessId = null;
        string? receivedRelatedProcessId = null;
        var controller = CreateController(new TestGestionaProcessService
        {
            DeleteRelatedProcessAsyncHandler = (processId, relatedProcessId, accessTokenOverride, cancellationToken) =>
            {
                receivedProcessId = processId;
                receivedRelatedProcessId = relatedProcessId;
                return Task.FromResult(new DeleteRelatedProcessResult(
                    true,
                    GetProcessFailureKind.None,
                    null,
                    null));
            }
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.DeleteRelatedProcess(
            "file-1",
            "file-2",
            "operation-1",
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(okResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        Assert.Equal("file-1", receivedProcessId);
        Assert.Equal("file-2", receivedRelatedProcessId);
        Assert.NotNull(gatewayResponse.Result);
        Assert.Empty(gatewayResponse.Result.GetType().GetProperties());
    }

    [Fact]
    public async Task GetRelatedProcesses_ReturnsGatewayResponseWithRelatedProcesses()
    {
        string? receivedProcessId = null;
        var controller = CreateController(new TestGestionaProcessService
        {
            GetRelatedProcessesAsyncHandler = (processId, accessTokenOverride, cancellationToken) =>
            {
                receivedProcessId = processId;
                IReadOnlyList<RelatedProcessItem> relatedProcesses =
                [
                    new("file-2", "98/2026")
                ];
                return Task.FromResult(new GetRelatedProcessesResult(
                    true,
                    GetProcessFailureKind.None,
                    null,
                    relatedProcesses,
                    null));
            }
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.GetRelatedProcesses(
            "file-1",
            "operation-1",
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(okResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        Assert.Equal("file-1", receivedProcessId);
        var relatedProcesses = Assert.IsAssignableFrom<IReadOnlyList<RelatedProcessItem>>(gatewayResponse.Result);
        var relatedProcess = Assert.Single(relatedProcesses);
        Assert.Equal("file-2", relatedProcess.Id);
        Assert.Equal("98/2026", relatedProcess.ProcessNumber);
    }

    [Fact]
    public async Task GetDocumentSignatures_ReturnsGatewayResponseWithSignatures()
    {
        string? receivedProcessId = null;
        string? receivedDocumentId = null;
        var controller = CreateController(new TestGestionaProcessService
        {
            GetProcessDocumentSignaturesAsyncHandler = (processId, documentId, accessTokenOverride, cancellationToken) =>
            {
                receivedProcessId = processId;
                receivedDocumentId = documentId;
                IReadOnlyList<ProcessDocumentSignatureResult> signatures =
                [
                    new("2026-09-09 10:12:12", "SIGNED", "081847637", "Luis Silva")
                ];
                return Task.FromResult(new GetProcessDocumentSignaturesResult(
                    true,
                    GetProcessDocumentsFailureKind.None,
                    null,
                    signatures,
                    null));
            }
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.GetDocumentSignatures(
            "file-1",
            "document-1",
            "operation-1",
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(okResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        Assert.Equal("file-1", receivedProcessId);
        Assert.Equal("document-1", receivedDocumentId);
        var signatures = Assert.IsAssignableFrom<IReadOnlyList<ProcessDocumentSignatureResult>>(gatewayResponse.Result);
        var signature = Assert.Single(signatures);
        Assert.Equal("2026-09-09 10:12:12", signature.DateSigned);
        Assert.Equal("SIGNED", signature.SignatureState);
        Assert.Equal("081847637", signature.Username);
        Assert.Equal("Luis Silva", signature.Name);
    }

    [Fact]
    public async Task GetProcedureData_ReturnsGatewayResponseWithMappedItems()
    {
        var controller = CreateController(new TestGestionaProcessService
        {
            GetProcessProcedureDataAsyncHandler = (processId, accessTokenOverride, cancellationToken) =>
            {
                IReadOnlyList<ProcessProcedureData> procedureData =
                [
                    new("field-1", "Descrição", "TEXT")
                ];
                return Task.FromResult(new GetProcessProcedureDataResult(
                    true,
                    GetProcessDocumentsFailureKind.None,
                    null,
                    procedureData,
                    null));
            }
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.GetProcedureData(
            "file-123",
            "operation-1",
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(okResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        var item = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<ProcessProcedureData>>(gatewayResponse.Result));
        Assert.Equal(("field-1", "Descrição", "TEXT"), (item.Reference, item.Caption, item.Type));
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
        public Func<RelatedProcessesRequest, string?, CancellationToken, Task<RelatedProcessesResult>>? RelateProcessesAsyncHandler { get; init; }
        public Func<string, string, string?, CancellationToken, Task<DeleteRelatedProcessResult>>? DeleteRelatedProcessAsyncHandler { get; init; }
        public Func<string, string?, CancellationToken, Task<GetRelatedProcessesResult>>? GetRelatedProcessesAsyncHandler { get; init; }
        public Func<string, string, string?, CancellationToken, Task<GetProcessDocumentSignaturesResult>>? GetProcessDocumentSignaturesAsyncHandler { get; init; }
        public Func<string, string?, CancellationToken, Task<GetProcessProcedureDataResult>>? GetProcessProcedureDataAsyncHandler { get; init; }
        public Func<string, string, string?, CancellationToken, Task<GetProcessProcedureDataValueResult>>? GetProcessProcedureDataValueAsyncHandler { get; init; }
        public Func<UploadDocumentRequest, string, string?, bool, string, string?, CancellationToken, Task<CreateDocumentInProcessResult>>? CreateDocumentInProcessAsyncHandler { get; init; }

        public Task<CreateDocumentInProcessResult> CreateDocumentInProcessAsync(
            UploadDocumentRequest request,
            string processId,
            string? folderId,
            bool resolveFileIdFromProcessCode,
            string documentsFolder,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (CreateDocumentInProcessAsyncHandler is null)
            {
                throw new NotImplementedException();
            }

            return CreateDocumentInProcessAsyncHandler(
                request,
                processId,
                folderId,
                resolveFileIdFromProcessCode,
                documentsFolder,
                accessTokenOverride,
                cancellationToken);
        }

        public Task<CreateProcessResult> CreateProcessAsync(
            CreateProcessRequest request,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<RelatedProcessesResult> RelateProcessesAsync(
            RelatedProcessesRequest request,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (RelateProcessesAsyncHandler is null)
            {
                throw new NotImplementedException();
            }

            return RelateProcessesAsyncHandler(request, accessTokenOverride, cancellationToken);
        }

        public Task<DeleteRelatedProcessResult> DeleteRelatedProcessAsync(
            string processId,
            string relatedProcessId,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (DeleteRelatedProcessAsyncHandler is null)
            {
                throw new NotImplementedException();
            }

            return DeleteRelatedProcessAsyncHandler(processId, relatedProcessId, accessTokenOverride, cancellationToken);
        }

        public Task<GetRelatedProcessesResult> GetRelatedProcessesAsync(
            string processId,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (GetRelatedProcessesAsyncHandler is null)
            {
                throw new NotImplementedException();
            }

            return GetRelatedProcessesAsyncHandler(processId, accessTokenOverride, cancellationToken);
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

        public Task<GetProcessProcedureDataResult> GetProcessProcedureDataAsync(
            string processId,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (GetProcessProcedureDataAsyncHandler is null)
            {
                throw new NotImplementedException();
            }

            return GetProcessProcedureDataAsyncHandler(processId, accessTokenOverride, cancellationToken);
        }

        public Task<GetProcessProcedureDataValueResult> GetProcessProcedureDataValueAsync(
            string processId,
            string referenceId,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (GetProcessProcedureDataValueAsyncHandler is null)
            {
                throw new NotImplementedException();
            }

            return GetProcessProcedureDataValueAsyncHandler(processId, referenceId, accessTokenOverride, cancellationToken);
        }

        public Task<GetProcessDocumentSignaturesResult> GetProcessDocumentSignaturesAsync(
            string processId,
            string documentId,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (GetProcessDocumentSignaturesAsyncHandler is null)
            {
                throw new NotImplementedException();
            }

            return GetProcessDocumentSignaturesAsyncHandler(processId, documentId, accessTokenOverride, cancellationToken);
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
