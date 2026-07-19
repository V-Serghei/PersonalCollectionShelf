using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Infrastructure.Persistence;
using PersonalCollectionShelf.Infrastructure.Repositories;
using PersonalCollectionShelf.Infrastructure.Services;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string databasePath,
        FirebaseOptions? firebaseOptions = null)
    {
        services.AddSingleton(new LocalDatabaseService(databasePath));
        services.AddSingleton<IMediaItemRepository, MediaItemRepository>();
        services.AddSingleton<IPersonRepository, PersonRepository>();
        services.AddSingleton<IStudioRepository, StudioRepository>();
        services.AddSingleton(firebaseOptions ?? FirebaseOptions.Disabled);
        services.AddSingleton(new HttpClient());
        services.AddSingleton<IFirebaseAuthClient, FirebaseAuthClient>();
        services.AddSingleton<IAuthTokenStore, InMemoryAuthTokenStore>();
        services.AddSingleton<IAuthService, FirebaseAuthService>();
        services.AddSingleton<FirestoreSyncService>();
        services.AddSingleton<ISyncService, SyncService>();

        return services;
    }
}
