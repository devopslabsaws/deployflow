using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;

namespace DeployFlow.Application.Features.Servers.Commands;

// ─── Add Server ───────────────────────────────────────────────────────────────

public record AddServerCommand(
    string Name,
    string IpAddress,
    int SshPort,
    string? SshUser,
    Guid? SshKeyId,
    string Provider,
    string? Region,
    int CpuCount,
    int MemoryGb,
    int DiskGb
) : IRequest<Result<ServerDto>>;

public class AddServerCommandValidator : AbstractValidator<AddServerCommand>
{
    public AddServerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IpAddress).NotEmpty()
            .Must(ip => System.Net.IPAddress.TryParse(ip, out _) || Uri.CheckHostName(ip) != UriHostNameType.Unknown)
            .WithMessage("Must be a valid IP address or hostname.");
        RuleFor(x => x.SshPort).InclusiveBetween(1, 65535);
        RuleFor(x => x.SshUser).MaximumLength(64).When(x => x.SshUser != null);
    }
}

public class AddServerCommandHandler : IRequestHandler<AddServerCommand, Result<ServerDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly ISshService _sshService;
    private readonly IEncryptionService _encryption;

    public AddServerCommandHandler(
        IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper,
        ISshService sshService, IEncryptionService encryption)
    { _uow = uow; _currentUser = currentUser; _mapper = mapper; _sshService = sshService; _encryption = encryption; }

    public async Task<Result<ServerDto>> Handle(AddServerCommand request, CancellationToken ct)
    {
        // Resolve SSH key if provided, verify connection
        if (request.SshKeyId.HasValue)
        {
            var sshKey = await _uow.SshKeys.GetByIdAsync(request.SshKeyId.Value, ct);
            if (sshKey is null || sshKey.TenantId != _currentUser.TenantId)
                return Result<ServerDto>.Failure("SSH key not found.", 404);

            var privateKey = _encryption.Decrypt(sshKey.PrivateKeyEncrypted);
            var reachable = await _sshService.TestConnectionAsync(
                request.IpAddress, request.SshPort, request.SshUser, privateKey, ct);
            if (!reachable)
                return Result<ServerDto>.Failure("Could not connect to server via SSH. Verify connection details.");
        }

        var server = Server.Create(
            tenantId: _currentUser.TenantId,
            name: request.Name,
            ipAddress: request.IpAddress,
            sshPort: request.SshPort,
            sshUser: request.SshUser,
            sshKeyId: request.SshKeyId,
            provider: Enum.TryParse<ServerProvider>(request.Provider, true, out var prov) ? prov : ServerProvider.Custom,
            region: request.Region,
            cpuCount: request.CpuCount,
            memoryGb: request.MemoryGb,
            diskGb: request.DiskGb
        );

        await _uow.Servers.AddAsync(server, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ServerDto>.Success(_mapper.Map<ServerDto>(server));
    }
}

// ─── Update Server ────────────────────────────────────────────────────────────

public record UpdateServerCommand(
    Guid Id,
    string? Name,
    Guid? SshKeyId,
    int? SshPort,
    string? SshUser,
    string? Region
) : IRequest<Result<ServerDto>>;

public class UpdateServerCommandHandler : IRequestHandler<UpdateServerCommand, Result<ServerDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateServerCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    { _uow = uow; _currentUser = currentUser; _mapper = mapper; }

    public async Task<Result<ServerDto>> Handle(UpdateServerCommand request, CancellationToken ct)
    {
        var server = await _uow.Servers.GetByIdAsync(request.Id, ct);
        if (server is null || server.TenantId != _currentUser.TenantId)
            return Result<ServerDto>.Failure("Server not found.", 404);

        server.UpdateDetails(request.Name, request.SshKeyId, request.SshPort, request.SshUser, request.Region);
        await _uow.SaveChangesAsync(ct);

        return Result<ServerDto>.Success(_mapper.Map<ServerDto>(server));
    }
}

// ─── Delete Server ────────────────────────────────────────────────────────────

public record DeleteServerCommand(Guid Id) : IRequest<Result>;

public class DeleteServerCommandHandler : IRequestHandler<DeleteServerCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteServerCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(DeleteServerCommand request, CancellationToken ct)
    {
        var server = await _uow.Servers.GetByIdAsync(request.Id, ct);
        if (server is null || server.TenantId != _currentUser.TenantId)
            return Result.Failure("Server not found.", 404);

        var hasProjects = await _uow.Projects.HasActiveProjectsOnServerAsync(request.Id, ct);
        if (hasProjects)
            return Result.Failure("Cannot delete a server with active projects. Reassign or delete projects first.");

        server.SoftDelete(_currentUser.UserId);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Exec Command on Server ──────────────────────────────────────────────────

public record ExecServerCommand(Guid Id, string Command) : IRequest<Result<ExecResultDto>>;

public class ExecServerCommandValidator : AbstractValidator<ExecServerCommand>
{
    public ExecServerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Command).NotEmpty().MaximumLength(4096);
    }
}

public class ExecServerCommandHandler : IRequestHandler<ExecServerCommand, Result<ExecResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly ISshService _sshService;
    private readonly IEncryptionService _encryption;

    public ExecServerCommandHandler(IUnitOfWork uow, ICurrentUser cu, ISshService ssh, IEncryptionService enc)
    { _uow = uow; _currentUser = cu; _sshService = ssh; _encryption = enc; }

    public async Task<Result<ExecResultDto>> Handle(ExecServerCommand request, CancellationToken ct)
    {
        var server = await _uow.Servers.GetByIdAsync(request.Id, ct);
        if (server is null || server.TenantId != _currentUser.TenantId)
            return Result<ExecResultDto>.Failure("Server not found.", 404);

        if (!server.SshKeyId.HasValue)
            return Result<ExecResultDto>.Failure("Server has no SSH key configured. Add an SSH key in server settings.", 400);

        var sshKey = await _uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct);
        if (sshKey is null)
            return Result<ExecResultDto>.Failure("SSH key not found.", 404);

        try
        {
            var privateKey = _encryption.Decrypt(sshKey.PrivateKeyEncrypted);
            var result = await _sshService.ExecuteCommandAsync(
                server.IpAddress, server.SshPort, server.SshUser ?? "root",
                privateKey, request.Command, ct);

            return Result<ExecResultDto>.Success(new ExecResultDto(
                result.StdOut,
                result.StdErr,
                result.ExitCode,
                result.Success));
        }
        catch (Exception ex)
        {
            return Result<ExecResultDto>.Failure($"SSH execution failed: {ex.Message}", 502);
        }
    }
}

// ─── Test Server Connection ───────────────────────────────────────────────────

public record TestServerConnectionCommand(Guid Id) : IRequest<Result<bool>>;

public class TestServerConnectionCommandHandler : IRequestHandler<TestServerConnectionCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly ISshService _sshService;
    private readonly IEncryptionService _encryption;

    public TestServerConnectionCommandHandler(
        IUnitOfWork uow, ICurrentUser cu, ISshService ssh, IEncryptionService enc)
    { _uow = uow; _currentUser = cu; _sshService = ssh; _encryption = enc; }

    public async Task<Result<bool>> Handle(TestServerConnectionCommand request, CancellationToken ct)
    {
        var server = await _uow.Servers.GetByIdAsync(request.Id, ct);
        if (server is null || server.TenantId != _currentUser.TenantId)
            return Result<bool>.Failure("Server not found.", 404);

        if (!server.SshKeyId.HasValue)
            return Result<bool>.Failure("Server has no SSH key configured.");

        var sshKey = await _uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct);
        if (sshKey is null)
            return Result<bool>.Failure("SSH key not found.");

        var privateKey = _encryption.Decrypt(sshKey.PrivateKeyEncrypted);
        var ok = await _sshService.TestConnectionAsync(
            server.IpAddress, server.SshPort, server.SshUser, privateKey, ct);

        if (ok) server.SetOnline();
        else server.SetOffline();
        await _uow.SaveChangesAsync(ct);

        return Result<bool>.Success(ok);
    }
}
