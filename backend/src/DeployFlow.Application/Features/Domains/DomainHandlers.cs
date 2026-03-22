using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;

namespace DeployFlow.Application.Features.Domains;

// â”€â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record GetDomainsQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<PaginatedResponse<DomainDto>>>;

public class GetDomainsQueryHandler : IRequestHandler<GetDomainsQuery, Result<PaginatedResponse<DomainDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetDomainsQueryHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<PaginatedResponse<DomainDto>>> Handle(GetDomainsQuery request, CancellationToken ct)
    {
        var items = await _uow.Domains.GetByTenantAsync(_currentUser.TenantId, ct);
        var dtos = items.Select(d => _mapper.Map<DomainDto>(d)).ToList();
        return Result<PaginatedResponse<DomainDto>>.Success(
            PaginatedResponse<DomainDto>.Create(dtos, dtos.Count, request.Page, request.PageSize));
    }
}

// â”€â”€â”€ Add Domain â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record AddDomainCommand(
    string DomainName,
    bool SslEnabled,
    Guid? ServiceId
) : IRequest<Result<DomainDto>>;

public class AddDomainCommandValidator : AbstractValidator<AddDomainCommand>
{
    public AddDomainCommandValidator()
    {
        RuleFor(x => x.DomainName).NotEmpty()
            .Matches(@"^[a-zA-Z0-9][a-zA-Z0-9\-\.]*[a-zA-Z0-9]$")
            .WithMessage("Invalid domain name format.");
    }
}

public class AddDomainCommandHandler : IRequestHandler<AddDomainCommand, Result<DomainDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public AddDomainCommandHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<DomainDto>> Handle(AddDomainCommand request, CancellationToken ct)
    {
        var domain = new DeployFlow.Domain.Entities.Domain
        {
            TenantId = _currentUser.TenantId,
            Name = request.DomainName.ToLowerInvariant().Trim(),
            SslEnabled = request.SslEnabled,
            SslProvider = SslProvider.LetsEncrypt,
            ServiceId = request.ServiceId,
            Status = DomainStatus.Pending
        };

        await _uow.Domains.AddAsync(domain, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<DomainDto>.Success(_mapper.Map<DomainDto>(domain));
    }
}

// â”€â”€â”€ Delete Domain â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record DeleteDomainCommand(Guid Id) : IRequest<Result>;

public class DeleteDomainCommandHandler : IRequestHandler<DeleteDomainCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteDomainCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(DeleteDomainCommand request, CancellationToken ct)
    {
        var domain = await _uow.Domains.GetByIdAsync(request.Id, ct);
        if (domain is null || domain.TenantId != _currentUser.TenantId)
            return Result.Failure("Domain not found.", 404);

        domain.SoftDelete(_currentUser.UserId);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
