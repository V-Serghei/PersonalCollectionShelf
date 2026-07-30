using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Application.Services;

public sealed class PersonService(IPersonRepository personRepository) : IPersonService
{
    public async Task<PersonDto?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken = default)
    {
        var person = await personRepository.GetByIdAsync(id, userId, cancellationToken);
        return person is null ? null : ToDto(person);
    }

    public async Task<IReadOnlyList<PersonDto>> GetByIdsAsync(string userId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var people = await personRepository.GetByIdsAsync(ids, userId, cancellationToken);
        return people.Select(ToDto).ToList();
    }

    public async Task<PersonDto> GetOrCreateAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        var existing = await personRepository.GetByNameAsync(userId, trimmedName, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var person = new Person
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmedName,
            CreatedAt = DateTime.UtcNow
        };

        var created = await personRepository.AddAsync(person, cancellationToken);
        return ToDto(created);
    }

    public async Task<IReadOnlyList<PersonDto>> SearchAsync(string userId, string? searchTerm, int limit = 20, CancellationToken cancellationToken = default)
    {
        var people = await personRepository.SearchAsync(userId, searchTerm, limit, cancellationToken);
        return people.Select(ToDto).ToList();
    }

    public async Task<PersonDto> CreateAsync(string userId, string displayName, CancellationToken cancellationToken = default)
    {
        var trimmedName = displayName.Trim();
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException("A display name is required.", nameof(displayName));
        }

        var existing = await personRepository.GetByNameAsync(userId, trimmedName, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var now = DateTime.UtcNow;
        var created = await personRepository.AddAsync(new Person
        {
            Id = Guid.NewGuid(),
            UserId = userId.Trim(),
            Name = trimmedName,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
        return ToDto(created);
    }

    private static PersonDto ToDto(Person person) => new(person.Id, person.Name)
    {
        SortName = person.SortName,
        PenName = person.PenName,
        BirthYear = person.BirthYear,
        DeathYear = person.DeathYear,
        Country = person.Country,
        PhotoPath = person.PhotoPath,
        Tagline = person.Tagline
    };
}
