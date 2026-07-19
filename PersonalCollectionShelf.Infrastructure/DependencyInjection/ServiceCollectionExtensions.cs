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
        services.AddSingleton<ITransactionRunner, LocalDatabaseTransactionRunner>();
        services.AddSingleton<IMediaItemRepository, MediaItemRepository>();
        services.AddSingleton<IPersonRepository, PersonRepository>();
        services.AddSingleton<IPersonRelationRepository, PersonRelationRepository>();
        services.AddSingleton<IProfessionRepository, ProfessionRepository>();
        services.AddSingleton<IStudioRepository, StudioRepository>();
        services.AddSingleton<IMediaCategoryRepository, MediaCategoryRepository>();
        services.AddSingleton<IBookDetailsRepository, BookDetailsRepository>();
        services.AddSingleton<ITagRepository, TagRepository>();
        services.AddSingleton<IMediaContributionRepository, MediaContributionRepository>();
        services.AddSingleton<IMediaCollectionRepository, MediaCollectionRepository>();
        services.AddSingleton<IMediaRelationRepository, MediaRelationRepository>();
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
