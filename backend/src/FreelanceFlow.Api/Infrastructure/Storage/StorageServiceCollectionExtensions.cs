namespace FreelanceFlow.Api.Infrastructure.Storage;

public static class StorageServiceCollectionExtensions
{
    /// <summary>Binds section "Storage" and registers the local file storage (the only provider for now).</summary>
    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }
}
