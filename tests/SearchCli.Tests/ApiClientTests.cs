using System.Net;
using SearchContracts;

namespace SearchCli.Tests;

public class ApiClientTests
{
    [Fact]
    public async Task Error_message_comes_from_json_body()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent($$"""{"error":"{{ApiRoutes.ErrorIndexNotBuilt}}"}""", System.Text.Encoding.UTF8, "application/json"),
        };

        Assert.Equal(ApiRoutes.ErrorIndexNotBuilt, await ApiClient.ErrorMessageAsync(response, default));
    }

    [Fact]
    public async Task Non_json_error_body_falls_back_to_status()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>bad gateway</html>", System.Text.Encoding.UTF8, "text/html"),
        };

        Assert.Equal("server returned 502", await ApiClient.ErrorMessageAsync(response, default));
    }
}
