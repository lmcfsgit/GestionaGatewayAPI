using System.Net;
using System.Text;
using GestionaGateway.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionaGatewayAPI.Tests;

public sealed class GestionaApiClientTests
{
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
