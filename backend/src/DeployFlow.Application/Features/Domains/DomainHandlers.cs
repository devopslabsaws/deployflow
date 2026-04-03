using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;
using System.Net;

namespace DeployFlow.Application.Features.Domains;

public record DnsCheckResultDto(
    Guid DomainId,
    string DomainName,
    bool IsValid,
    string Status,
    string Message,
    string ExpectedTarget,
    string[] ResolvedAddresses,
    DateTime CheckedAt
);

public record DomainSslActionDto(
    Guid DomainId,
    string DomainName,
    bool SslEnabled,
    DateTime? SslExpiresAt,
    string Status,
    string Message
);

// Queries

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

// Add domain

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

// Verify domain

public record VerifyDomainCommand(Guid Id) : IRequest<Result<DomainDto>>;

public class VerifyDomainCommandHandler : IRequestHandler<VerifyDomainCommand, Result<DomainDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public VerifyDomainCommandHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<DomainDto>> Handle(VerifyDomainCommand request, CancellationToken ct)
    {
        var domain = await _uow.Domains.GetByIdAsync(request.Id, ct);
        if (domain is null || domain.TenantId != _currentUser.TenantId)
            return Result<DomainDto>.Failure("Domain not found.", 404);

        var dns = await DomainDnsChecker.CheckAsync(domain.Name, ct);
        if (!dns.IsValid)
            return Result<DomainDto>.Failure($"DNS verification failed: {dns.Message}", 400);

        domain.DnsVerified = true;
        domain.Status = DomainStatus.Active;
        await _uow.SaveChangesAsync(ct);
        return Result<DomainDto>.Success(_mapper.Map<DomainDto>(domain));
    }
}

// DNS check

public record GetDomainDnsCheckQuery(Guid Id) : IRequest<Result<DnsCheckResultDto>>;

public class GetDomainDnsCheckQueryHandler : IRequestHandler<GetDomainDnsCheckQuery, Result<DnsCheckResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetDomainDnsCheckQueryHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<DnsCheckResultDto>> Handle(GetDomainDnsCheckQuery request, CancellationToken ct)
    {
        var domain = await _uow.Domains.GetByIdAsync(request.Id, ct);
        if (domain is null || domain.TenantId != _currentUser.TenantId)
            return Result<DnsCheckResultDto>.Failure("Domain not found.", 404);

        var dns = await DomainDnsChecker.CheckAsync(domain.Name, ct);
        var dto = new DnsCheckResultDto(
            domain.Id,
            domain.Name,
            dns.IsValid,
            dns.IsValid ? "verified" : "pending",
            dns.Message,
            dns.ExpectedTarget,
            dns.ResolvedAddresses,
            DateTime.UtcNow);

        return Result<DnsCheckResultDto>.Success(dto);
    }
}

// SSL provisioning

public record ProvisionDomainSslCommand(Guid Id) : IRequest<Result<DomainSslActionDto>>;

public class ProvisionDomainSslCommandHandler : IRequestHandler<ProvisionDomainSslCommand, Result<DomainSslActionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ProvisionDomainSslCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<DomainSslActionDto>> Handle(ProvisionDomainSslCommand request, CancellationToken ct)
    {
        var domain = await _uow.Domains.GetByIdAsync(request.Id, ct);
        if (domain is null || domain.TenantId != _currentUser.TenantId)
            return Result<DomainSslActionDto>.Failure("Domain not found.", 404);

        if (!domain.DnsVerified)
            return Result<DomainSslActionDto>.Failure("Domain must be DNS verified before SSL provisioning.", 400);

        domain.SslEnabled = true;
        domain.SslProvider = SslProvider.LetsEncrypt;
        domain.SslExpiresAt = DateTime.UtcNow.AddDays(90);
        domain.Status = DomainStatus.Active;

        await _uow.SaveChangesAsync(ct);
        return Result<DomainSslActionDto>.Success(new DomainSslActionDto(
            domain.Id,
            domain.Name,
            domain.SslEnabled,
            domain.SslExpiresAt,
            domain.Status.ToString().ToLowerInvariant(),
            "SSL certificate provisioned."));
    }
}

// SSL renewal

public record RenewDomainSslCommand(Guid Id) : IRequest<Result<DomainSslActionDto>>;

public class RenewDomainSslCommandHandler : IRequestHandler<RenewDomainSslCommand, Result<DomainSslActionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public RenewDomainSslCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<DomainSslActionDto>> Handle(RenewDomainSslCommand request, CancellationToken ct)
    {
        var domain = await _uow.Domains.GetByIdAsync(request.Id, ct);
        if (domain is null || domain.TenantId != _currentUser.TenantId)
            return Result<DomainSslActionDto>.Failure("Domain not found.", 404);

        if (!domain.SslEnabled)
            return Result<DomainSslActionDto>.Failure("SSL is not enabled for this domain.", 400);

        domain.SslExpiresAt = DateTime.UtcNow.AddDays(90);
        domain.Status = DomainStatus.Active;

        await _uow.SaveChangesAsync(ct);
        return Result<DomainSslActionDto>.Success(new DomainSslActionDto(
            domain.Id,
            domain.Name,
            domain.SslEnabled,
            domain.SslExpiresAt,
            domain.Status.ToString().ToLowerInvariant(),
            "SSL certificate renewed."));
    }
}

// Delete domain

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

internal sealed record DomainDnsCheckResult(bool IsValid, string Message, string ExpectedTarget, string[] ResolvedAddresses);

internal static class DomainDnsChecker
{
    public static async Task<DomainDnsCheckResult> CheckAsync(string domainName, CancellationToken ct)
    {
        var host = domainName.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host) || !host.Contains('.'))
        {
            return new DomainDnsCheckResult(
                false,
                "Domain format is invalid.",
                BuildExpectedTarget(host),
                []);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            var resolved = addresses.Select(a => a.ToString()).Distinct().ToArray();
            return resolved.Length == 0
                ? new DomainDnsCheckResult(false, "No DNS A/AAAA records found.", BuildExpectedTarget(host), resolved)
                : new DomainDnsCheckResult(true, "DNS records found.", BuildExpectedTarget(host), resolved);
        }
        catch
        {
            return new DomainDnsCheckResult(
                false,
                "Could not resolve domain records.",
                BuildExpectedTarget(host),
                []);
        }
    }

    private static string BuildExpectedTarget(string host)
    {
        var safe = string.IsNullOrWhiteSpace(host) ? "domain" : host.Replace("*.", "wildcard-");
        return $"proxy.{safe}";
    }
}
