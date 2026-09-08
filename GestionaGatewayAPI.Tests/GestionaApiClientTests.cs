using System.Net;
using System.Text;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionaGatewayAPI.Tests;

public sealed class GestionaApiClientTests
{
    [Fact]
    public async Task OpenProcessFileAsync_WhenSelectableTitleHasValue_SendsSelectableTitle()
    {
        string? capturedRequestBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"file-123","code":"16/2026"}""", Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.OpenProcessFileAsync(
            "https://gestiona.example/rest",
            "token",
            "files/file-123/open",
            new OpenProcessFileRequest
            {
                EntryDate = "1787608800",
                FreeTitle = "Process subject",
                SelectableTitle = "Teste 1",
                UserHref = "https://gestiona.example/rest/users/user-1",
                GroupHref = "https://gestiona.example/rest/groups/group-1"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("\"selectable_title\":\"Teste 1\"", capturedRequestBody);
    }

    [Fact]
    public async Task OpenProcessFileAsync_WhenSelectableTitleIsMissing_OmitsSelectableTitle()
    {
        string? capturedRequestBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"file-123","code":"16/2026"}""", Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.OpenProcessFileAsync(
            "https://gestiona.example/rest",
            "token",
            "files/file-123/open",
            new OpenProcessFileRequest
            {
                EntryDate = "1787608800",
                FreeTitle = "Process subject",
                UserHref = "https://gestiona.example/rest/users/user-1",
                GroupHref = "https://gestiona.example/rest/groups/group-1"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain("selectable_title", capturedRequestBody);
    }

    [Fact]
    public async Task GetProcessAssigneeUserAsync_SendsBodyAndReturnsFirstContentItem()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedRequestBody = null;
        var responseJson = """
            {
              "content": [
                {
                  "id": "8be7a78b-787a-4061-a11c-1bfcdf2d627a",
                  "username": "081847637",
                  "name": "Luis Silva",
                  "links": []
                },
                {
                  "id": "other-user",
                  "username": "other",
                  "name": "Other User",
                  "links": []
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            capturedRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.GetProcessAssigneeUserAsync(
            "https://gestiona.example/rest",
            "token",
            new GetProcessAssigneeUserRequest("081847637"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal(
            "https://gestiona.example/rest/files/assignees/users",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        Assert.Equal("""{"username":"081847637"}""", capturedRequestBody);
        Assert.Equal(
            "application/vnd.gestiona.filter.assignees+json",
            capturedRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("8be7a78b-787a-4061-a11c-1bfcdf2d627a", result.Value!.Id);
        Assert.Equal("081847637", result.Value.Username);
        Assert.Equal("Luis Silva", result.Value.Name);
    }

    [Fact]
    public async Task GetProcessAssigneeGroupsAsync_MapsContentAndUsesExpectedRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var responseJson = """
            {
              "content": [
                {
                  "id": "43f83662-bb73-4c98-915a-de90219036f6",
                  "name": "100. Exemplo",
                  "version": "1",
                  "links": [
                    {
                      "rel": "self",
                      "href": "https://02.g3stiona.com/rest/groups/43f83662-bb73-4c98-915a-de90219036f6"
                    }
                  ]
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.GetProcessAssigneeGroupsAsync(
            "https://gestiona.example/rest",
            "token",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal(
            "https://gestiona.example/rest/files/assignees/groups",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        var group = Assert.Single(result.Value!);
        Assert.Equal("43f83662-bb73-4c98-915a-de90219036f6", group.Id);
        Assert.Equal("100. Exemplo", group.Name);
    }

    [Fact]
    public async Task GetQueueSubscriptionsAsync_MapsContentAndUsesExpectedRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var responseJson = """
            {
              "content": [
                {
                  "name": "connectors#sigma-medidata-file-docs",
                  "links": []
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.GetQueueSubscriptionsAsync(
            "https://gestiona.example/rest",
            "token",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal(
            "https://gestiona.example/rest/queues/subscriptions",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        var subscription = Assert.Single(result.Value!);
        Assert.Equal("connectors#sigma-medidata-file-docs", subscription.Name);
    }

    [Fact]
    public async Task GetQueueConnectorsAsync_MapsContentAndUsesExpectedRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var responseJson = """
            {
              "page": 1,
              "content": [
                {
                  "code": "sigma-medidata-file-docs",
                  "name": "sigma-medidata-file-docs",
                  "fields": [],
                  "links": []
                }
              ],
              "links": []
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.GetQueueConnectorsAsync(
            "https://gestiona.example/rest",
            "token",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal(
            "https://gestiona.example/rest/connectors",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        var connector = Assert.Single(result.Value!);
        Assert.Equal("sigma-medidata-file-docs", connector.Code);
        Assert.Equal("sigma-medidata-file-docs", connector.Name);
    }

    [Fact]
    public async Task SubscribeQueueConnectorAsync_UsesExpectedRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.SubscribeQueueConnectorAsync(
            "https://gestiona.example/rest",
            "token",
            "sigma-medidata-file-docs",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal(
            "https://gestiona.example/rest/queues/connectors/sigma-medidata-file-docs/subscription",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task SubscribeQueueConnectorAsync_WhenUpstreamFails_ReturnsResponseDescription()
    {
        var responseJson = """
            {
              "code": 412,
              "name": "Precondition Failed",
              "description": "already exists other consumer subscribed to queue",
              "technical_details": "http://www.w3.org/Protocols/rfc2616/rfc2616-sec10.html#sec10.4.13"
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.PreconditionFailed)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.SubscribeQueueConnectorAsync(
            "https://gestiona.example/rest",
            "token",
            "sigma-medidata-file-docs",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(412, result.StatusCode);
        Assert.Equal("already exists other consumer subscribed to queue", result.Value);
    }

    [Fact]
    public async Task GetQueueConnectorMessageAsync_UsesExpectedRouteAndContentType()
    {
        HttpRequestMessage? capturedRequest = null;
        var responseJson = """
            {
              "payload": {
                "target": "9376d53a-176e-4716-beaa-bd4486561bbd",
                "links": []
              },
              "entry": "1788444429",
              "next_delivery": "1788515019",
              "prev_delivery": "1788515009",
              "last_delivery": "1788515009",
              "deliveries": "11",
              "links": []
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.GetQueueConnectorMessageAsync(
            "https://gestiona.example/rest",
            "token",
            "sigma-medidata-file-docs",
            "9376d53a-176e-4716-beaa-bd4486561bbd",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal(
            "https://gestiona.example/rest/queues/connectors/sigma-medidata-file-docs/9376d53a-176e-4716-beaa-bd4486561bbd",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        Assert.Null(capturedRequest.Content);
        Assert.Contains(
            capturedRequest.Headers.Accept,
            header => string.Equals(
                header.MediaType,
                "application/vnd.gestiona.queues.message",
                StringComparison.Ordinal));
        Assert.Equal("9376d53a-176e-4716-beaa-bd4486561bbd", result.Value!.Payload!.Target);
        Assert.Equal("1788444429", result.Value.Entry);
        Assert.Equal("1788515019", result.Value.NextDelivery);
    }

    [Fact]
    public async Task GetQueueConnectorMessagesAsync_UsesExpectedPostRouteAndMapsContent()
    {
        HttpRequestMessage? capturedRequest = null;
        var responseJson = """
            {
              "content": [
                {
                  "payload": {
                    "target": "e796e8b5-4f8a-4acd-9459-ee57acf8e383",
                    "links": []
                  },
                  "entry": "1788775851",
                  "next_delivery": "1788776011",
                  "prev_delivery": "1788776007",
                  "last_delivery": "1788776010",
                  "deliveries": "3",
                  "links": []
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.GetQueueConnectorMessagesAsync(
            "https://gestiona.example/rest",
            "token",
            "sigma-medidata-file-docs",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal(
            "https://gestiona.example/rest/queues/connectors/sigma-medidata-file-docs?max-messages=100&hold-seconds=1",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        var message = Assert.Single(result.Value!);
        Assert.Equal("e796e8b5-4f8a-4acd-9459-ee57acf8e383", message.Payload!.Target);
        Assert.Equal("1788775851", message.Entry);
        Assert.Equal("1788776011", message.NextDelivery);
    }

    [Fact]
    public async Task SendQueueConnectorResponseAsync_UsesExpectedRouteContentTypeAndOmitsNullMessage()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedRequestBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            capturedRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.SendQueueConnectorResponseAsync(
            "https://gestiona.example/rest",
            "token",
            "sigma-medidata-file-docs",
            "message-1",
            new QueueConnectorResponseRequest("FALSE", null),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal(
            "https://gestiona.example/rest/queues/connectors/sigma-medidata-file-docs/message-1",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        Assert.Equal(
            "application/vnd.gestiona.connector-response+json",
            capturedRequest.Content!.Headers.ContentType!.ToString());
        Assert.Contains("\"result_success\":\"FALSE\"", capturedRequestBody);
        Assert.DoesNotContain("message", capturedRequestBody);
    }

    [Fact]
    public async Task GetExternalProceduresAsync_MapsContentAndUsesExpectedRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var responseJson = """
            {
              "page": 1,
              "content": [
                {
                  "id": "procedure-1",
                  "title": "Procedimento Generico",
                  "links": []
                },
                {
                  "id": "procedure-2",
                  "title": "Licenciamento",
                  "links": []
                }
              ],
              "links": []
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.GetExternalProceduresAsync(
            "https://gestiona.example/rest",
            "token",
            "activity/123",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            "https://gestiona.example/rest/catalog-2015/procedures/activity%2F123/external-procedures",
            capturedRequest!.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        Assert.Collection(
            result.Value!,
            item => Assert.Equal(("procedure-1", "Procedimento Generico"), (item.Id, item.Title)),
            item => Assert.Equal(("procedure-2", "Licenciamento"), (item.Id, item.Title)));
    }

    [Fact]
    public async Task GetProcessDocumentsAsync_MapsContentAndUsesExpectedRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var responseJson = """
            {
              "page": 1,
              "content": [
                {
                  "type": "DOC",
                  "rel": "POC_SIGMA_Gestiona",
                  "href": "https://gestiona.example/rest/files/file-123/documents/document-1"
                },
                {
                  "type": "FOLDER",
                  "rel": "xxxx",
                  "href": "https://gestiona.example/rest/files/file-123/folders/folder-1"
                }
              ],
              "links": []
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.GetProcessDocumentsAsync(
            "https://gestiona.example/rest",
            "token",
            "file-123",
            documentId: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            "https://gestiona.example/rest/files/file-123/documents-and-folders",
            capturedRequest!.RequestUri!.ToString());
        Assert.Equal("token", capturedRequest.Headers.GetValues("X-Gestiona-Access-Token").Single());
        Assert.Collection(
            result.Value!,
            item => Assert.Equal(("DOC", "POC_SIGMA_Gestiona", "document-1"), (item.Type, item.Name, item.Id)),
            item => Assert.Equal(("FOLDER", "xxxx", "folder-1"), (item.Type, item.Name, item.Id)));
    }

    [Fact]
    public async Task GetProcessDocumentsAsync_WithDocumentId_UsesNestedRoute()
    {
        Uri? capturedRequestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"content":[]}""", Encoding.UTF8, "application/json")
            };
        });
        var client = new GestionaApiClient(
            new StubHttpClientFactory(handler),
            NullLogger<GestionaApiClient>.Instance);

        var result = await client.GetProcessDocumentsAsync(
            "https://gestiona.example/rest",
            "token",
            "file-123",
            "folder/456",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            "https://gestiona.example/rest/files/file-123/documents-and-folders/folder%2F456",
            capturedRequestUri!.ToString());
        Assert.Empty(result.Value!);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
