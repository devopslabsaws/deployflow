using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;

namespace DeployFlow.Application.Features.SshKeys;

// ─── Queries ──────────────────────────────────────────────────────────────────

public record GetSshKeysQuery : IRequest<Result<List<SshKeyDto>>>;

public class GetSshKeysQueryHandler : IRequestHandler<GetSshKeysQuery, Result<List<SshKeyDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetSshKeysQueryHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<List<SshKeyDto>>> Handle(GetSshKeysQuery request, CancellationToken ct)
    {
        var keys = await _uow.SshKeys.GetByTenantAsync(_currentUser.TenantId, ct);
        var dtos = keys.Select(k => new SshKeyDto(k.Id, k.Name, k.Fingerprint, k.PublicKey, k.CreatedAt)).ToList();
        return Result<List<SshKeyDto>>.Success(dtos);
    }
}

// ─── Create SSH Key ───────────────────────────────────────────────────────────

public record CreateSshKeyCommand(
    string Name,
    string PrivateKey,
    string? Passphrase
) : IRequest<Result<SshKeyDto>>;

public class CreateSshKeyCommandValidator : AbstractValidator<CreateSshKeyCommand>
{
    public CreateSshKeyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PrivateKey).NotEmpty()
            .Must(k => k.Contains("BEGIN") && k.Contains("PRIVATE KEY"))
            .WithMessage("Invalid SSH private key format.");
    }
}

public class CreateSshKeyCommandHandler : IRequestHandler<CreateSshKeyCommand, Result<SshKeyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IEncryptionService _encryption;

    public CreateSshKeyCommandHandler(IUnitOfWork uow, ICurrentUser cu, IEncryptionService enc)
    { _uow = uow; _currentUser = cu; _encryption = enc; }

    public async Task<Result<SshKeyDto>> Handle(CreateSshKeyCommand request, CancellationToken ct)
    {
        // Extract public key and fingerprint from the private key
        var (publicKey, fingerprint) = ExtractKeyInfo(request.PrivateKey);

        var key = new SshKey
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            PublicKey = publicKey,
            PrivateKeyEncrypted = _encryption.Encrypt(request.PrivateKey),
            Fingerprint = fingerprint
        };

        await _uow.SshKeys.AddAsync(key, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<SshKeyDto>.Success(new SshKeyDto(key.Id, key.Name, key.Fingerprint, key.PublicKey, key.CreatedAt));
    }

    private static (string PublicKey, string Fingerprint) ExtractKeyInfo(string privateKey)
    {
        // In a real implementation, use SSH.NET to derive the public key
        // For now return placeholder values
        var fingerprint = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(privateKey)))[..32];
        return ("(stored - generate with ssh-keygen -y)", fingerprint);
    }
}

// ─── Delete SSH Key ───────────────────────────────────────────────────────────

public record DeleteSshKeyCommand(Guid Id) : IRequest<Result>;

public class DeleteSshKeyCommandHandler : IRequestHandler<DeleteSshKeyCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteSshKeyCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(DeleteSshKeyCommand request, CancellationToken ct)
    {
        var key = await _uow.SshKeys.GetByIdAsync(request.Id, ct);
        if (key is null || key.TenantId != _currentUser.TenantId)
            return Result.Failure("SSH key not found.", 404);

        // Check if any server uses this key
        var serversUsingKey = await _uow.Servers.FindAsync(s => s.SshKeyId == request.Id, ct);
        if (serversUsingKey.Any())
            return Result.Failure("Cannot delete SSH key: it is assigned to one or more servers.");

        key.SoftDelete(_currentUser.UserId);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
