using TrainingCompletion.Application;

namespace TrainingCompletion.UnitTests;

public sealed class ErrorContractTests
{
    [Theory]
    [InlineData(typeof(ValidationException), 400)]
    [InlineData(typeof(NotFoundException), 404)]
    [InlineData(typeof(ConflictException), 409)]
    public void ApplicationErrors_ExposeExpectedStatus(Type errorType, int expectedStatus)
    {
        var exception = (AppException)Activator.CreateInstance(
            errorType,
            "test_code",
            "test message")!;

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.Equal("test_code", exception.ErrorCode);
    }
}

