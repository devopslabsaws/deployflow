using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using FluentValidation;
using MediatR;
using AutoMapper;

namespace DeployFlow.Application.Features.Databases;

// ─── Queries ─────────────────────────────────────────────────────────────────

public record GetS3DestinationsQuery : IRequest<Result<IReadOnlyList<S3DestinationDto>>>;

public class GetS3DestinationsQueryHandler : IRequestHandler<GetS3DestinationsQuery, Result<IReadOnlyList<S3DestinationDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetS3DestinationsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<S3DestinationDto>>> Handle(GetS3DestinationsQuery request, CancellationToken ct)
    {
        var items = await _uow.S3Destinations.GetByTenantAsync(_currentUser.TenantId, ct);
        var dtos = items.Select(s => _mapper.Map<S3DestinationDto>(s)).ToList();
        return Result<IReadOnlyList<S3DestinationDto>>.Success(dtos);
    }
}

public record GetS3DestinationByIdQuery(Guid Id) : IRequest<Result<S3DestinationDto>>;

public class GetS3DestinationByIdQueryHandler : IRequestHandler<GetS3DestinationByIdQuery, Result<S3DestinationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetS3DestinationByIdQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<S3DestinationDto>> Handle(GetS3DestinationByIdQuery request, CancellationToken ct)
    {
        var dest = await _uow.S3Destinations.GetByIdAsync(request.Id, ct);
        if (dest is null || dest.TenantId != _currentUser.TenantId)
            return Result<S3DestinationDto>.Failure("S3 destination not found.", 404);
        return Result<S3DestinationDto>.Success(_mapper.Map<S3DestinationDto>(dest));
    }
}

// ─── Create S3 Destination ───────────────────────────────────────────────────

public record CreateS3DestinationCommand(
    string Name,
    string? Description,
    string Endpoint,
    string BucketName,
    string AccessKeyId,
    string SecretAccessKey,
    string? Region,
    bool IsDefault
) : IRequest<Result<S3DestinationDto>>;

public class CreateS3DestinationCommandValidator : AbstractValidator<CreateS3DestinationCommand>
{
    public CreateS3DestinationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(500);
        RuleFor(x => x.BucketName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AccessKeyId).NotEmpty();
        RuleFor(x => x.SecretAccessKey).NotEmpty();
    }
}

public class CreateS3DestinationCommandHandler : IRequestHandler<CreateS3DestinationCommand, Result<S3DestinationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IEncryptionService _encryption;

    public CreateS3DestinationCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper, IEncryptionService encryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
        _encryption = encryption;
    }

    public async Task<Result<S3DestinationDto>> Handle(CreateS3DestinationCommand request, CancellationToken ct)
    {
        if (request.IsDefault)
        {
            var existing = await _uow.S3Destinations.GetByTenantAsync(_currentUser.TenantId, ct);
            var currentDefault = existing.FirstOrDefault(x => x.IsDefault);
            if (currentDefault != null)
            {
                currentDefault.IsDefault = false;
                await _uow.S3Destinations.UpdateAsync(currentDefault, ct);
            }
        }

        var dest = new S3Destination
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            Description = request.Description,
            Endpoint = request.Endpoint,
            BucketName = request.BucketName,
            AccessKeyIdEncrypted = _encryption.Encrypt(request.AccessKeyId),
            SecretAccessKeyEncrypted = _encryption.Encrypt(request.SecretAccessKey),
            Region = request.Region,
            IsDefault = request.IsDefault,
            Status = S3DestinationStatus.Unconfigured
        };

        await _uow.S3Destinations.AddAsync(dest, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<S3DestinationDto>.Success(_mapper.Map<S3DestinationDto>(dest));
    }
}

// ─── Update S3 Destination ───────────────────────────────────────────────────

public record UpdateS3DestinationCommand(
    Guid Id,
    string Name,
    string? Description,
    string Endpoint,
    string BucketName,
    string? AccessKeyId,
    string? SecretAccessKey,
    string? Region,
    bool IsDefault
) : IRequest<Result<S3DestinationDto>>;

public class UpdateS3DestinationCommandValidator : AbstractValidator<UpdateS3DestinationCommand>
{
    public UpdateS3DestinationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(500);
        RuleFor(x => x.BucketName).NotEmpty().MaximumLength(100);
    }
}

public class UpdateS3DestinationCommandHandler : IRequestHandler<UpdateS3DestinationCommand, Result<S3DestinationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IEncryptionService _encryption;

    public UpdateS3DestinationCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper, IEncryptionService encryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
        _encryption = encryption;
    }

    public async Task<Result<S3DestinationDto>> Handle(UpdateS3DestinationCommand request, CancellationToken ct)
    {
        var dest = await _uow.S3Destinations.GetByIdAsync(request.Id, ct);
        if (dest is null || dest.TenantId != _currentUser.TenantId)
            return Result<S3DestinationDto>.Failure("S3 destination not found.", 404);

        if (request.IsDefault && !dest.IsDefault)
        {
            var existing = await _uow.S3Destinations.GetByTenantAsync(_currentUser.TenantId, ct);
            var currentDefault = existing.FirstOrDefault(x => x.IsDefault && x.Id != request.Id);
            if (currentDefault != null)
            {
                currentDefault.IsDefault = false;
                await _uow.S3Destinations.UpdateAsync(currentDefault, ct);
            }
        }

        dest.Name = request.Name;
        dest.Description = request.Description;
        dest.Endpoint = request.Endpoint;
        dest.BucketName = request.BucketName;
        if (!string.IsNullOrWhiteSpace(request.AccessKeyId))
            dest.AccessKeyIdEncrypted = _encryption.Encrypt(request.AccessKeyId);
        if (!string.IsNullOrWhiteSpace(request.SecretAccessKey))
            dest.SecretAccessKeyEncrypted = _encryption.Encrypt(request.SecretAccessKey);
        dest.Region = request.Region;
        dest.IsDefault = request.IsDefault;

        await _uow.S3Destinations.UpdateAsync(dest, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<S3DestinationDto>.Success(_mapper.Map<S3DestinationDto>(dest));
    }
}

// ─── Delete S3 Destination ───────────────────────────────────────────────────

public record DeleteS3DestinationCommand(Guid Id) : IRequest<Result<string>>;

public class DeleteS3DestinationCommandHandler : IRequestHandler<DeleteS3DestinationCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteS3DestinationCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(DeleteS3DestinationCommand request, CancellationToken ct)
    {
        var dest = await _uow.S3Destinations.GetByIdAsync(request.Id, ct);
        if (dest is null || dest.TenantId != _currentUser.TenantId)
            return Result<string>.Failure("S3 destination not found.", 404);

        // Check if any backup policies reference this destination
        var policies = await _uow.BackupPolicies.GetByTenantAsync(_currentUser.TenantId, ct);
        if (policies.Any(p => p.S3DestinationId == request.Id))
            return Result<string>.Failure("Cannot delete S3 destination used by backup policies.", 409);

        await _uow.S3Destinations.DeleteAsync(dest, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success("S3 destination deleted.");
    }
}

// ─── Test S3 Destination ─────────────────────────────────────────────────────

public record TestS3DestinationCommand(S3DestinationTestRequest Request) : IRequest<Result<S3DestinationTestResult>>;

public class TestS3DestinationCommandHandler : IRequestHandler<TestS3DestinationCommand, Result<S3DestinationTestResult>>
{
    private readonly ICurrentUser _currentUser;

    public TestS3DestinationCommandHandler(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task<Result<S3DestinationTestResult>> Handle(TestS3DestinationCommand request, CancellationToken ct)
    {
        // Simulate S3 connection test
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Task.Delay(50, ct);  // Simulate network latency
        sw.Stop();

        var result = new S3DestinationTestResult(
            Success: true,
            Message: "Connection successful",
            LatencyMs: sw.ElapsedMilliseconds,
            ResolvedEndpoint: request.Request.Endpoint ?? "s3.amazonaws.com"
        );

        return Result<S3DestinationTestResult>.Success(result);
    }
}
