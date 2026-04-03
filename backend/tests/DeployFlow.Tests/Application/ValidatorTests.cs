using DeployFlow.Application.Features.Auth.Commands;
using DeployFlow.Application.Features.Projects.Commands;
using DeployFlow.Application.Features.Servers.Commands;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DeployFlow.Tests.Application;

public class ValidatorTests
{
    // ─── RegisterCommandValidator ─────────────────────────────────────────────

    private readonly RegisterCommandValidator _registerValidator = new();

    [Fact]
    public void Register_ValidCommand_PassesValidation()
    {
        var cmd = new RegisterCommand("John Doe", "john@example.com", "Password1!", null);
        var result = _registerValidator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "john@example.com", "Password1!")]
    [InlineData("J", "john@example.com", "Password1!")]      // name too short
    [InlineData("John", "not-an-email", "Password1!")]
    [InlineData("John", "john@example.com", "short")]        // password too short
    [InlineData("John", "john@example.com", "alllowercase1")] // no uppercase
    [InlineData("John", "john@example.com", "ALLUPPERCASE!")]  // no digit
    public void Register_InvalidInputs_FailsValidation(string name, string email, string password)
    {
        var cmd = new RegisterCommand(name, email, password, null);
        var result = _registerValidator.TestValidate(cmd);
        result.ShouldHaveAnyValidationError();
    }

    // ─── CreateProjectCommandValidator ───────────────────────────────────────

    private readonly CreateProjectCommandValidator _projectValidator = new();

    [Fact]
    public void CreateProject_ValidCommand_PassesValidation()
    {
        var cmd = new CreateProjectCommand(
            "My App", "A description", "https://github.com/org/repo",
            "main", "npm run build", "npm start", null, null,
            "nextjs", null, 3002, true, Array.Empty<string>(), null);

        var result = _projectValidator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateProject_EmptyName_FailsValidation()
    {
        var cmd = new CreateProjectCommand(
            "", null, null, null, null, null, null, null,
            null, null, null, true, Array.Empty<string>(), null);

        var result = _projectValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateProject_InvalidRepositoryUrl_FailsValidation()
    {
        var cmd = new CreateProjectCommand(
            "My App", null, "not-a-url", null, null, null, null, null,
            null, null, null, true, Array.Empty<string>(), null);

        var result = _projectValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RepositoryUrl);
    }

    [Fact]
    public void CreateProject_InvalidCustomDomain_FailsValidation()
    {
        var cmd = new CreateProjectCommand(
            "My App", null, null, null, null, null, null, null,
            null, "not a valid domain!", null, true, Array.Empty<string>(), null);

        var result = _projectValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.CustomDomain);
    }

    [Fact]
    public void CreateProject_InvalidPort_FailsValidation()
    {
        var cmd = new CreateProjectCommand(
            "My App", null, null, "main", null, null, null, null,
            "nextjs", null, 70000, true, Array.Empty<string>(), null);

        var result = _projectValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Port);
    }

    // ─── AddServerCommandValidator ────────────────────────────────────────────

    private readonly AddServerCommandValidator _serverValidator = new();

    [Fact]
    public void AddServer_ValidCommand_PassesValidation()
    {
        var cmd = new AddServerCommand(
            "My Server", "192.168.1.1", 22, "root",
            null, "Custom", null, 4, 8, 100);

        var result = _serverValidator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddServer_InvalidSshPort_FailsValidation()
    {
        var cmd = new AddServerCommand(
            "My Server", "192.168.1.1", 0, "root",
            null, "Custom", null, 4, 8, 100);

        var result = _serverValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SshPort);
    }

    [Fact]
    public void AddServer_InvalidIpAddress_FailsValidation()
    {
        var cmd = new AddServerCommand(
            "My Server", "not-valid-ip!!!", 22, "root",
            null, "Custom", null, 4, 8, 100);

        var result = _serverValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.IpAddress);
    }
}
