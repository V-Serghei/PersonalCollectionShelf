using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Application.Services;

public sealed class PeopleManagementService(
    IPersonRepository people,
    IProfessionRepository professions,
    IPersonRelationRepository relations,
    ITransactionRunner? transactionRunner = null,
    IMediaContributionRepository? contributions = null,
    IMediaItemRepository? mediaItems = null,
    IPersonPhotoRepository? photos = null) : IPeopleManagementService
{
    public async Task<IReadOnlyList<PersonCatalogDto>> GetCatalogAsync(string userId, CancellationToken cancellationToken = default)
    {
        var allPeople = await people.SearchAsync(userId, null, 500, cancellationToken);
        var itemsById = mediaItems is null
            ? new Dictionary<Guid, MediaItem>()
            : (await mediaItems.GetAllAsync(userId, cancellationToken)).ToDictionary(item => item.Id);
        var creditsByPerson = contributions is null
            ? new Dictionary<Guid, IReadOnlyList<MediaContribution>>()
            : (await contributions.GetAllAsync(userId, cancellationToken))
                .GroupBy(credit => credit.PersonId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<MediaContribution>)group.ToList());

        return allPeople.Select(person =>
        {
            var works = creditsByPerson.GetValueOrDefault(person.Id, [])
                .Where(credit => itemsById.ContainsKey(credit.MediaItemId))
                .Select(credit => ToWorkDto(credit, itemsById[credit.MediaItemId]))
                .OrderByDescending(work => work.Rating ?? -1)
                .ThenBy(work => work.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var ratings = works.Where(work => work.Rating.HasValue).Select(work => work.Rating!.Value).ToList();
            return new PersonCatalogDto(
                person.Id,
                person.Name,
                person.PhotoPath,
                person.Tagline,
                person.Country,
                works.Where(work => work.Rating.HasValue).Take(5).ToList(),
                works.Count,
                ratings.Count == 0 ? null : ratings.Average());
        }).OrderBy(person => person.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<PersonDetailsDto?> GetAsync(string userId, Guid id, CancellationToken cancellationToken = default)
    {
        var person = await people.GetByIdAsync(id, userId, cancellationToken);
        return person is null ? null : await ToDetailsAsync(person, cancellationToken);
    }

    public Task<PersonDetailsDto> SaveAsync(SavePersonRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("User and display name are required.");
        }

        return transactionRunner is null
            ? SaveCoreAsync(request, cancellationToken)
            : transactionRunner.ExecuteAsync(ct => SaveCoreAsync(request, ct), cancellationToken);
    }

    public async Task<PersonRelationDto> SaveRelationAsync(SavePersonRelationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PersonId == Guid.Empty || request.RelatedPersonId == Guid.Empty || request.PersonId == request.RelatedPersonId)
        {
            throw new ArgumentException("A relation requires two different people.");
        }

        var relatedPerson = await people.GetByIdAsync(request.RelatedPersonId, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Related person was not found.");
        var now = DateTime.UtcNow;
        var saved = await relations.UpsertAsync(new PersonRelation
        {
            Id = request.Id ?? Guid.NewGuid(),
            UserId = request.UserId,
            PersonId = request.PersonId,
            RelatedPersonId = request.RelatedPersonId,
            Kind = request.Kind,
            InverseKind = request.InverseKind,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Notes = Normalize(request.Notes),
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
        return ToRelationDto(saved, relatedPerson, request.PersonId);
    }

    public Task RemoveRelationAsync(string userId, Guid relationId, CancellationToken cancellationToken = default) =>
        relations.RemoveAsync(relationId, userId, cancellationToken);

    public async Task<PersonPhotoDto> AddPhotoAsync(AddPersonPhotoRequest request, CancellationToken cancellationToken = default)
    {
        if (photos is null || string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new InvalidOperationException("Photo storage is not available.");
        }

        var person = await people.GetByIdAsync(request.PersonId, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Person was not found.");
        var existing = await photos.GetForPersonAsync(person.Id, person.UserId, cancellationToken);
        if (request.Id.HasValue)
        {
            var duplicate = existing.FirstOrDefault(photo => photo.Id == request.Id.Value);
            if (duplicate is not null) return ToPhotoDto(duplicate);
        }
        var photo = await photos.AddAsync(new PersonPhoto
        {
            Id = request.Id ?? Guid.NewGuid(),
            UserId = person.UserId,
            PersonId = person.Id,
            FilePath = request.FilePath.Trim(),
            Caption = Normalize(request.Caption),
            IsPrimary = existing.Count == 0,
            SortOrder = existing.Count,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        if (photo.IsPrimary)
        {
            person.PhotoPath = photo.FilePath;
            await people.UpdateAsync(person, cancellationToken);
        }
        return ToPhotoDto(photo);
    }

    public async Task RemovePhotoAsync(string userId, Guid personId, Guid photoId, CancellationToken cancellationToken = default)
    {
        if (photos is null) return;
        var person = await people.GetByIdAsync(personId, userId, cancellationToken)
            ?? throw new InvalidOperationException("Person was not found.");
        var existing = await photos.GetForPersonAsync(personId, userId, cancellationToken);
        var removed = existing.FirstOrDefault(photo => photo.Id == photoId);
        if (removed is null) return;

        await photos.RemoveAsync(photoId, userId, cancellationToken);
        if (removed.IsPrimary)
        {
            var replacement = existing.FirstOrDefault(photo => photo.Id != photoId);
            if (replacement is not null)
            {
                await photos.SetPrimaryAsync(personId, replacement.Id, userId, cancellationToken);
            }
            person.PhotoPath = replacement?.FilePath;
            await people.UpdateAsync(person, cancellationToken);
        }
    }

    public async Task SetPrimaryPhotoAsync(string userId, Guid personId, Guid photoId, CancellationToken cancellationToken = default)
    {
        if (photos is null) return;
        var person = await people.GetByIdAsync(personId, userId, cancellationToken)
            ?? throw new InvalidOperationException("Person was not found.");
        var photo = (await photos.GetForPersonAsync(personId, userId, cancellationToken)).FirstOrDefault(value => value.Id == photoId)
            ?? throw new InvalidOperationException("Photo was not found.");
        await photos.SetPrimaryAsync(personId, photoId, userId, cancellationToken);
        person.PhotoPath = photo.FilePath;
        await people.UpdateAsync(person, cancellationToken);
    }

    private async Task<PersonDetailsDto> SaveCoreAsync(SavePersonRequest request, CancellationToken cancellationToken)
    {
        var userId = request.UserId.Trim();
        var existing = request.Id.HasValue ? await people.GetByIdAsync(request.Id.Value, userId, cancellationToken) : null;
        var now = DateTime.UtcNow;
        var person = existing ?? new Person { Id = request.Id ?? Guid.NewGuid(), UserId = userId, CreatedAt = now };
        person.Name = request.Name.Trim();
        person.FirstName = Normalize(request.FirstName);
        person.MiddleName = Normalize(request.MiddleName);
        person.LastName = Normalize(request.LastName);
        person.PenName = Normalize(request.PenName);
        person.SortName = Normalize(request.SortName);
        person.BirthYear = request.BirthYear;
        person.DeathYear = request.DeathYear;
        person.Country = Normalize(request.Country);
        person.PlaceOfBirth = Normalize(request.PlaceOfBirth);
        person.Gender = Normalize(request.Gender);
        person.OfficialWebsite = Normalize(request.OfficialWebsite);
        person.PhotoPath = Normalize(request.PhotoPath);
        person.Tagline = Normalize(request.Tagline);
        person.Description = Normalize(request.Description);
        person.Notes = Normalize(request.Notes);
        person.UpdatedAt = now;

        person = existing is null
            ? await people.AddAsync(person, cancellationToken)
            : await people.UpdateAsync(person, cancellationToken);
        await professions.SetForPersonAsync(person.Id, userId, request.Professions, cancellationToken);
        return await ToDetailsAsync(person, cancellationToken);
    }

    private async Task<PersonDetailsDto> ToDetailsAsync(Person person, CancellationToken cancellationToken)
    {
        var professionNames = (await professions.GetForPersonAsync(person.Id, person.UserId, cancellationToken))
            .Select(value => value.DisplayName).ToList();
        var relationDtos = new List<PersonRelationDto>();
        foreach (var relation in await relations.GetForPersonAsync(person.Id, person.UserId, cancellationToken))
        {
            var relatedId = relation.PersonId == person.Id ? relation.RelatedPersonId : relation.PersonId;
            var relatedPerson = await people.GetByIdAsync(relatedId, person.UserId, cancellationToken);
            if (relatedPerson is not null)
            {
                relationDtos.Add(ToRelationDto(relation, relatedPerson, person.Id));
            }
        }

        var works = new List<PersonWorkDto>();
        if (contributions is not null && mediaItems is not null)
        {
            foreach (var contribution in await contributions.GetForPersonAsync(person.Id, person.UserId, cancellationToken))
            {
                var mediaItem = await mediaItems.GetByIdAsync(contribution.MediaItemId, person.UserId, cancellationToken);
                if (mediaItem is not null)
                {
                    works.Add(ToWorkDto(contribution, mediaItem));
                }
            }
        }

        var photoDtos = photos is null
            ? new List<PersonPhotoDto>()
            : (await photos.GetForPersonAsync(person.Id, person.UserId, cancellationToken)).Select(ToPhotoDto).ToList();
        if (photoDtos.Count == 0 && !string.IsNullOrWhiteSpace(person.PhotoPath))
        {
            photoDtos.Add(new PersonPhotoDto(Guid.Empty, person.PhotoPath, null, true, 0));
        }

        return new PersonDetailsDto
        {
            Id = person.Id,
            Name = person.Name,
            FirstName = person.FirstName,
            MiddleName = person.MiddleName,
            LastName = person.LastName,
            PenName = person.PenName,
            SortName = person.SortName,
            BirthYear = person.BirthYear,
            DeathYear = person.DeathYear,
            Country = person.Country,
            PlaceOfBirth = person.PlaceOfBirth,
            Gender = person.Gender,
            OfficialWebsite = person.OfficialWebsite,
            PhotoPath = person.PhotoPath,
            Tagline = person.Tagline,
            Description = person.Description,
            Notes = person.Notes,
            Professions = professionNames,
            Relations = relationDtos,
            Works = works.OrderBy(value => value.Title).ToList(),
            Photos = photoDtos
        };
    }

    private static PersonWorkDto ToWorkDto(MediaContribution contribution, MediaItem mediaItem) => new(
        mediaItem.Id,
        mediaItem.Title,
        mediaItem.MediaType,
        contribution.Role,
        contribution.Details,
        contribution.CreditedAs,
        mediaItem.Rating,
        mediaItem.CoverUrl,
        mediaItem.ReleaseYear);

    private static PersonPhotoDto ToPhotoDto(PersonPhoto photo) => new(
        photo.Id,
        photo.FilePath,
        photo.Caption,
        photo.IsPrimary,
        photo.SortOrder);

    private static PersonRelationDto ToRelationDto(PersonRelation relation, Person relatedPerson, Guid perspectivePersonId)
    {
        var isForward = relation.PersonId == perspectivePersonId;
        return new PersonRelationDto(
            relation.Id,
            relatedPerson.Id,
            relatedPerson.Name,
            isForward ? relation.Kind : relation.InverseKind ?? relation.Kind,
            isForward ? relation.InverseKind : relation.Kind,
            relation.StartDate,
            relation.EndDate,
            relation.Notes);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
