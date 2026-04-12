// Copyright (c) Helsincy. All rights reserved.

using Launcher.Shared;

namespace Launcher.Tests.Unit;

/// <summary>
/// Result / Result&lt;T&gt; 基础类型单元测试
/// </summary>
public class ResultTests
{
    [Fact]
    public void Ok_Should_ReturnSuccess()
    {
        var result = Result.Ok();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Fail_WithError_Should_ReturnFailure()
    {
        var error = new Error
        {
            Code = "TEST_001",
            UserMessage = "测试错误"
        };

        var result = Result.Fail(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Fail_WithCodeAndMessage_Should_ReturnFailure()
    {
        var result = Result.Fail("TEST_002", "简短错误");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TEST_002");
        result.Error.UserMessage.Should().Be("简短错误");
    }

    [Fact]
    public void Ok_WithValue_Should_ReturnSuccessAndValue()
    {
        var result = Result.Ok(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Fail_Generic_Should_ReturnFailureAndDefaultValue()
    {
        var result = Result.Fail<int>("TEST_003", "泛型失败");

        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(default(int));
        result.Error!.Code.Should().Be("TEST_003");
    }

    [Fact]
    public void Map_OnSuccess_Should_TransformValue()
    {
        var result = Result.Ok(10);

        var mapped = result.Map(v => v * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(20);
    }

    [Fact]
    public void Map_OnFailure_Should_PropagateError()
    {
        var result = Result.Fail<int>("TEST_004", "原始错误");

        var mapped = result.Map(v => v * 2);

        mapped.IsFailure.Should().BeTrue();
        mapped.Error!.Code.Should().Be("TEST_004");
    }

    [Fact]
    public void Bind_OnSuccess_Should_ChainOperation()
    {
        var result = Result.Ok(5);

        var bound = result.Bind(v =>
            v > 0 ? Result.Ok(v.ToString(System.Globalization.CultureInfo.InvariantCulture)) : Result.Fail<string>("NEG", "负数"));

        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("5");
    }

    [Fact]
    public void Bind_OnFailure_Should_PropagateError()
    {
        var result = Result.Fail<int>("TEST_005", "链式失败");

        var bound = result.Bind(v => Result.Ok(v.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        bound.IsFailure.Should().BeTrue();
        bound.Error!.Code.Should().Be("TEST_005");
    }

    [Fact]
    public void Error_Should_HaveAllProperties()
    {
        var inner = new InvalidOperationException("内部异常");
        var error = new Error
        {
            Code = "TEST_FULL",
            UserMessage = "用户看到的消息",
            TechnicalMessage = "技术细节",
            CanRetry = true,
            Severity = ErrorSeverity.Critical,
            InnerException = inner
        };

        error.Code.Should().Be("TEST_FULL");
        error.UserMessage.Should().Be("用户看到的消息");
        error.TechnicalMessage.Should().Be("技术细节");
        error.CanRetry.Should().BeTrue();
        error.Severity.Should().Be(ErrorSeverity.Critical);
        error.InnerException.Should().BeSameAs(inner);
    }
}
