using DeployFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace DeployFlow.Tests.Application;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccessIsTrue_ErrorIsNull()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failure_IsSuccessIsFalse_ErrorSet()
    {
        var result = Result.Failure("Something went wrong", "ERR_001");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Something went wrong");
        result.ErrorCode.Should().Be("ERR_001");
    }

    [Fact]
    public void Failure_WithStatusCode_StoresCodeAsString()
    {
        var result = Result.Failure("Not found", 404);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("404");
    }

    [Fact]
    public void SuccessOfT_ContainsValue()
    {
        var result = Result.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void FailureOfT_ValueIsDefault()
    {
        var result = Result.Failure<int>("failed");

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default);
        result.Error.Should().Be("failed");
    }

    [Fact]
    public void SuccessOfT_WithComplexType_RetainsReference()
    {
        var dto = new { Id = Guid.NewGuid(), Name = "Test" };
        var result = Result<object>.Success(dto);

        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public void FailureOfT_WithStatusCode_StoresCodeAsString()
    {
        var result = Result.Failure<string>("Unauthorized", 401);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("401");
    }
}
