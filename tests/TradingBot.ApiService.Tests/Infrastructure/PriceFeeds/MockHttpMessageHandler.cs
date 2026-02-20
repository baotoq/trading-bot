namespace TradingBot.ApiService.Tests.Infrastructure.PriceFeeds;

/// <summary>
/// A testable HttpMessageHandler that delegates to a provided function,
/// allowing full control over HTTP responses in unit tests.
/// </summary>
public class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _sentRequests = [];

    /// <summary>All requests sent through this handler (for assertion).</summary>
    public IReadOnlyList<HttpRequestMessage> SentRequests => _sentRequests;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _sentRequests.Add(request);
        return Task.FromResult(handler(request));
    }
}

/// <summary>
/// A testable HttpMessageHandler that always throws a specified exception.
/// </summary>
public class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw exception;
}
