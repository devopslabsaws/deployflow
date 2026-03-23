using System.Text.Json;
using DeployFlow.API.Controllers;
using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Auth.Commands;
using DeployFlow.Application.Features.Team;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace DeployFlow.Tests.Application;

public class Sprint5SmokeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static ICurrentUser CreateCurrentUser(string role = "admin")
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.TenantId).Returns(TenantId);
        currentUser.Setup(x => x.UserId).Returns(UserId);
        currentUser.Setup(x => x.Name).Returns("admin-user");
        currentUser.Setup(x => x.Role).Returns(role);
        currentUser.Setup(x => x.Email).Returns("admin@example.com");
        currentUser.Setup(x => x.IsAuthenticated).Returns(true);
        currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        return currentUser.Object;
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    private static (Mock<IUnitOfWork> uow, Mock<ITenantRepository<TeamInvitation>> invitations) CreateInvitationUow(List<TeamInvitation>? seed = null)
    {
        var invitations = new Mock<ITenantRepository<TeamInvitation>>();
        var uow = new Mock<IUnitOfWork>();
        var data = seed ?? new List<TeamInvitation>();

        invitations.Setup(x => x.GetByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);
        invitations.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => data.FirstOrDefault(item => item.Id == id));
        invitations.Setup(x => x.AddAsync(It.IsAny<TeamInvitation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeamInvitation invitation, CancellationToken _) =>
            {
                data.Add(invitation);
                return invitation;
            });
        invitations.Setup(x => x.DeleteAsync(It.IsAny<TeamInvitation>(), It.IsAny<CancellationToken>()))
            .Returns((TeamInvitation invitation, CancellationToken _) =>
            {
                data.Remove(invitation);
                return Task.CompletedTask;
            });

        uow.Setup(x => x.TeamInvitations).Returns(invitations.Object);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (uow, invitations);
    }

    private static Mock<IAuditService> CreateAuditMock()
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(x => x.LogAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return audit;
    }

    private static IConfiguration CreateConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:FrontendUrl"] = "http://localhost:3000"
            })
            .Build();

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"sprint5-smoke-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task InviteTeamMember_CreatesInvitation_SendsEmail_AndAudits()
    {
        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.FindByEmailAsync("new.user@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var email = new Mock<IEmailService>();
        email.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var audit = CreateAuditMock();
        var (uow, _) = CreateInvitationUow();

        var handler = new InviteTeamMemberCommandHandler(
            userManager.Object,
            uow.Object,
            CreateCurrentUser(),
            email.Object,
            audit.Object,
            CreateConfiguration());

        var result = await handler.Handle(
            new InviteTeamMemberCommand("new.user@example.com", "New User", "Developer"),
            default);

        result.IsSuccess.Should().BeTrue();
        var invitation = result.Value;
        invitation.Should().NotBeNull();
        invitation!.Email.Should().Be("new.user@example.com");
        invitation.Role.Should().Be("developer");
        invitation.Status.Should().Be("pending");
        email.Verify(x => x.SendAsync("new.user@example.com", It.IsAny<string>(), It.Is<string>(body => body.Contains("Developer")), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(x => x.LogAsync(UserId, "admin-user", "team.invitation.sent", "team-invitation", invitation.Id, "new.user@example.com", TenantId, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InviteTeamMember_WithExistingPendingInvitation_ReturnsFailure()
    {
        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.FindByEmailAsync("pending@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var existing = TeamInvitation.Create(TenantId, "pending@example.com", "Pending User", "viewer", UserId, "admin-user");
        var (uow, _) = CreateInvitationUow(new List<TeamInvitation> { existing });

        var handler = new InviteTeamMemberCommandHandler(
            userManager.Object,
            uow.Object,
            CreateCurrentUser(),
            Mock.Of<IEmailService>(),
            CreateAuditMock().Object,
            CreateConfiguration());

        var result = await handler.Handle(
            new InviteTeamMemberCommand("pending@example.com", "Pending User", "viewer"),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("pending invitation");
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendTeamInvitation_RotatesToken_AndIncrementsCounter()
    {
        var invitation = TeamInvitation.Create(TenantId, "resend@example.com", "Resend User", "developer", UserId, "admin-user");
        var originalToken = invitation.Token;
        var originalExpiry = invitation.ExpiresAt;
        var (uow, _) = CreateInvitationUow(new List<TeamInvitation> { invitation });

        var email = new Mock<IEmailService>();
        email.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ResendTeamInvitationCommandHandler(uow.Object, CreateCurrentUser(), email.Object, CreateAuditMock().Object, CreateConfiguration());
        var result = await handler.Handle(new ResendTeamInvitationCommand(invitation.Id), default);

        result.IsSuccess.Should().BeTrue();
        invitation.Token.Should().NotBe(originalToken);
        invitation.ExpiresAt.Should().BeAfter(originalExpiry.AddSeconds(-1));
        invitation.ResendCount.Should().Be(1);
        email.Verify(x => x.SendAsync("resend@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeTeamInvitation_DeletesInvitation_AndPersists()
    {
        var invitation = TeamInvitation.Create(TenantId, "revoke@example.com", "Revoke User", "viewer", UserId, "admin-user");
        var seed = new List<TeamInvitation> { invitation };
        var (uow, _) = CreateInvitationUow(seed);

        var handler = new RevokeTeamInvitationCommandHandler(uow.Object, CreateCurrentUser(), CreateAuditMock().Object);
        var result = await handler.Handle(new RevokeTeamInvitationCommand(invitation.Id), default);

        result.IsSuccess.Should().BeTrue();
        seed.Should().BeEmpty();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvitationPreview_ByToken_ReturnsInvitationMetadata()
    {
        var invitation = TeamInvitation.Create(TenantId, "preview-invite@example.com", "Preview Invite", "viewer", UserId, "admin-user");
        var (uow, _) = CreateInvitationUow(new List<TeamInvitation> { invitation });

        var handler = new GetInvitationByTokenQueryHandler(uow.Object);
        var result = await handler.Handle(new GetInvitationByTokenQuery(invitation.Token), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Email.Should().Be("preview-invite@example.com");
        result.Value.Role.Should().Be("viewer");
    }

    [Fact]
    public async Task AcceptInvitation_CreatesUser_DeletesInvitation_AndReturnsTokens()
    {
        var invitation = TeamInvitation.Create(TenantId, "accepted@example.com", "Accepted User", "developer", UserId, "admin-user");
        var invitationData = new List<TeamInvitation> { invitation };
        var (uow, _) = CreateInvitationUow(invitationData);

        var tenants = new Mock<ITenantEntityRepository>();
        tenants.Setup(x => x.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tenant.Create("tenant-a"));
        uow.Setup(x => x.Tenants).Returns(tenants.Object);

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.FindByEmailAsync("accepted@example.com")).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var jwt = new Mock<IJwtService>();
        jwt.Setup(x => x.GenerateTokens(It.IsAny<ApplicationUser>(), It.IsAny<Tenant>()))
            .Returns((ApplicationUser u, Tenant _) =>
                new AuthTokensDto(
                    "access-token",
                    "refresh-token",
                    DateTime.UtcNow.AddHours(1),
                    new AuthUserDto(
                        u.Id,
                        u.Email ?? "",
                        u.FullName,
                        u.Role,
                        u.AvatarUrl ?? "",
                        u.TenantId,
                        "tenant-a",
                        "free",
                        false,
                        u.CreatedAt,
                        true,
                        null)));

        var handler = new AcceptInvitationCommandHandler(userManager.Object, uow.Object, jwt.Object);
        var result = await handler.Handle(new AcceptInvitationCommand(invitation.Token, "Accepted User", "Password1A"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be("access-token");
        invitationData.Should().BeEmpty();
        userManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password1A"), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Permissions_GetUserGrants_ReturnsLowerCaseActions()
    {
        await using var db = CreateDb();
        var targetUser = ApplicationUser.Create("Target User", "target@example.com", "hash", TenantId, "developer");
        db.Users.Add(targetUser);
        db.ResourcePermissions.Add(ResourcePermission.Grant(
            TenantId,
            targetUser.Id,
            PermissionResource.Service,
            Guid.NewGuid(),
            ResourceAction.Read | ResourceAction.Configure));
        await db.SaveChangesAsync();

        var controller = new PermissionsController(
            new Mock<IMediator>().Object,
            Mock.Of<IPermissionService>(),
            db,
            CreateCurrentUser());

        var response = await controller.GetUserGrants(targetUser.Id, default);
        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var payload = JsonSerializer.Serialize(ok.Value);

        payload.Should().Contain("service");
        payload.Should().Contain("read");
        payload.Should().Contain("configure");
    }

    [Fact]
    public async Task Permissions_Preview_ReturnsEffectiveActionsFromPermissionService()
    {
        await using var db = CreateDb();
        var targetUser = ApplicationUser.Create("Preview User", "preview@example.com", "hash", TenantId, "developer");
        db.Users.Add(targetUser);
        await db.SaveChangesAsync();

        var resourceId = Guid.NewGuid();
        var permissionService = new Mock<IPermissionService>();
        permissionService.Setup(x => x.HasPermissionAsync(targetUser.Id, "developer", PermissionResource.Project, resourceId, ResourceAction.Read, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        permissionService.Setup(x => x.HasPermissionAsync(targetUser.Id, "developer", PermissionResource.Project, resourceId, ResourceAction.Configure, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        permissionService.Setup(x => x.HasPermissionAsync(targetUser.Id, "developer", PermissionResource.Project, resourceId, ResourceAction.Deploy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        permissionService.Setup(x => x.HasPermissionAsync(targetUser.Id, "developer", PermissionResource.Project, resourceId, ResourceAction.Delete, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new PermissionsController(
            new Mock<IMediator>().Object,
            permissionService.Object,
            db,
            CreateCurrentUser());

        var response = await controller.Preview(targetUser.Id, PermissionResource.Project, resourceId, default);
        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var preview = ok.Value.Should().BeOfType<PermissionPreviewDto>().Subject;

        preview.EffectiveActions.Should().BeEquivalentTo(["read", "configure"]);
    }

    [Fact]
    public async Task Permissions_GrantBulk_ParsesActionsAndAllowsExplicitDeny()
    {
        await using var db = CreateDb();
        var permissionService = new Mock<IPermissionService>();
        permissionService.Setup(x => x.GrantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<PermissionResource>(), It.IsAny<Guid>(), It.IsAny<ResourceAction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new PermissionsController(
            new Mock<IMediator>().Object,
            permissionService.Object,
            db,
            CreateCurrentUser());

        var resourceA = Guid.NewGuid();
        var resourceB = Guid.NewGuid();
        var response = await controller.GrantBulk(
            new BulkGrantPermissionRequest(
                Guid.NewGuid(),
                new List<BulkGrantPermissionItemRequest>
                {
                    new(PermissionResource.Project, resourceA, new List<string> { "read", "deploy" }),
                    new(PermissionResource.Volume, resourceB, new List<string>()),
                }),
            default);

        response.Should().BeOfType<NoContentResult>();
        permissionService.Verify(x => x.GrantAsync(TenantId, It.IsAny<Guid>(), PermissionResource.Project, resourceA, ResourceAction.Read | ResourceAction.Deploy, It.IsAny<CancellationToken>()), Times.Once);
        permissionService.Verify(x => x.GrantAsync(TenantId, It.IsAny<Guid>(), PermissionResource.Volume, resourceB, ResourceAction.None, It.IsAny<CancellationToken>()), Times.Once);
    }
}