namespace ConsoleApp;

public class Program
{
    public static async Task Main(string[] args)
        => await ConsoleHost.RunAsync<App>(args, CreateHostBuilder, app => app.RunAsync());

    // this function is necessary for Entity Framework Core tools to perform migrations, etc
    // do not change signature!!
    public static IHostBuilder CreateHostBuilder(string[] args)
        => ConsoleHost.CreateHostBuilder(args, ConfigureServices);

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // register your services or Entity Framework data contexts here
    }
}
