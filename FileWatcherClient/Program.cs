using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

Host.CreateDefaultBuilder(args)
    .UseWindowsService()                     // chạy như service
    .ConfigureServices(s => s.AddHostedService<Worker>())
    .Build()
    .Run();

await host.RunAsync();
