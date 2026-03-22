using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;

namespace DeployFlow.Application.Features.Databases;

// â”€â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record GetDatabasesQuery(
    int Page = 1, int PageSize = 20,
    Guid? ServerId = null
) : IRequest<Result<PaginatedResponse<DatabaseInstanceDto>>>;

public class GetDatabasesQueryHandler : IRequestHandler<GetDatabasesQuery, Result<PaginatedResponse<DatabaseInstanceDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetDatabasesQueryHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<PaginatedResponse<DatabaseInstanceDto>>> Handle(GetDatabasesQuery request, CancellationToken ct)
    {
        IReadOnlyList<DatabaseInstance> items;
        if (request.ServerId.HasValue)
            items = await _uow.Databases.GetByServerAsync(request.ServerId.Value, ct);
        else
            items = await _uow.Databases.GetByTenantAsync(_currentUser.TenantId, ct);

        var filtered = items.Where(d => d.TenantId == _currentUser.TenantId).ToList();
        var dtos = filtered.Select(d => _mapper.Map<DatabaseInstanceDto>(d)).ToList();
        return Result<PaginatedResponse<DatabaseInstanceDto>>.Success(
            PaginatedResponse<DatabaseInstanceDto>.Create(dtos, filtered.Count, request.Page, request.PageSize));
    }
}

public record GetDatabaseByIdQuery(Guid Id) : IRequest<Result<DatabaseInstanceDto>>;

public class GetDatabaseByIdQueryHandler : IRequestHandler<GetDatabaseByIdQuery, Result<DatabaseInstanceDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetDatabaseByIdQueryHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<DatabaseInstanceDto>> Handle(GetDatabaseByIdQuery request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.Id, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<DatabaseInstanceDto>.Failure("Database not found.", 404);
        return Result<DatabaseInstanceDto>.Success(_mapper.Map<DatabaseInstanceDto>(db));
    }
}

// â”€â”€â”€ Create Database â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record CreateDatabaseCommand(
    string Name,
    string Engine,
    string Version,
    string? DatabaseName,
    string? Username,
    string? Password,
    int StorageGb,
    bool AutoBackup,
    string? BackupSchedule,
    Guid? ServerId
) : IRequest<Result<DatabaseInstanceDto>>;

public class CreateDatabaseCommandValidator : AbstractValidator<CreateDatabaseCommand>
{
    public CreateDatabaseCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Engine).NotEmpty()
            .Must(e => Enum.TryParse<DatabaseEngine>(e, true, out _))
            .WithMessage("Invalid database engine. Supported: PostgreSQL, MySQL, MariaDB, MongoDB, Redis, MsSql.");
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.StorageGb).GreaterThan(0).LessThanOrEqualTo(1000);
    }
}

public class CreateDatabaseCommandHandler : IRequestHandler<CreateDatabaseCommand, Result<DatabaseInstanceDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IEncryptionService _encryption;

    public CreateDatabaseCommandHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper, IEncryptionService enc)
    { _uow = uow; _currentUser = cu; _mapper = mapper; _encryption = enc; }

    public async Task<Result<DatabaseInstanceDto>> Handle(CreateDatabaseCommand request, CancellationToken ct)
    {
        Enum.TryParse<DatabaseEngine>(request.Engine, true, out var engine);

        var db = new DatabaseInstance
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            Engine = engine,
            Version = request.Version,
            DatabaseName = request.DatabaseName ?? request.Name.ToLowerInvariant().Replace("-", "_"),
            Username = request.Username ?? "admin",
            PasswordEncrypted = _encryption.Encrypt(request.Password ?? Guid.NewGuid().ToString("N")[..16]),
            StorageGb = request.StorageGb,
            BackupEnabled = request.AutoBackup,
            BackupSchedule = request.BackupSchedule ?? "0 2 * * *",
            ServerId = request.ServerId ?? Guid.Empty,
            Status = DatabaseInstanceStatus.Creating
        };

        await _uow.Databases.AddAsync(db, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<DatabaseInstanceDto>.Success(_mapper.Map<DatabaseInstanceDto>(db));
    }
}

// â”€â”€â”€ Delete Database â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record DeleteDatabaseCommand(Guid Id) : IRequest<Result>;

public class DeleteDatabaseCommandHandler : IRequestHandler<DeleteDatabaseCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteDatabaseCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(DeleteDatabaseCommand request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.Id, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result.Failure("Database not found.", 404);

        db.SoftDelete(_currentUser.UserId);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
