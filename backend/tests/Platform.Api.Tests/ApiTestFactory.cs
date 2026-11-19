using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Api.Tests;

/// <summary>
/// The Api host in process. Tests add their own services and endpoints with <paramref name="configureServices"/>.
/// The shared test framework (issue #8) is expected to absorb this class.
/// </summary>
public sealed class ApiTestFactory(Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
