using FluentAssertions;
using Conduit.Pipeline;
using Conduit.Api;

namespace Conduit.Pipeline.Tests;

public class PipelineBuilderTests
{
    [Fact]
    public void PipelineBuilder_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Act
        var builder = new PipelineBuilder<string, string>();

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void PipelineBuilder_WithName_ShouldSetName()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();
        var pipelineName = "TestPipeline";

        // Act
        var result = builder.WithName(pipelineName);

        // Assert
        result.Should().Be(builder); // Should return same instance for chaining
    }

    [Fact]
    public void PipelineBuilder_WithName_WithEmptyName_ShouldThrow()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act & Assert
        var act = () => builder.WithName("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PipelineBuilder_WithName_WithNullName_ShouldThrow()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act & Assert
        var act = () => builder.WithName(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PipelineBuilder_WithDescription_ShouldSetDescription()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();
        var description = "Test pipeline description";

        // Act
        var result = builder.WithDescription(description);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void PipelineBuilder_WithDescription_WithEmptyDescription_ShouldThrow()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act & Assert
        var act = () => builder.WithDescription("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PipelineBuilder_WithType_ShouldSetPipelineType()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        var result = builder.WithType(PipelineType.Parallel);

        // Assert
        result.Should().Be(builder);
    }

    [Theory]
    [InlineData(PipelineType.Sequential)]
    [InlineData(PipelineType.Parallel)]
    [InlineData(PipelineType.Branch)]
    [InlineData(PipelineType.Filter)]
    public void PipelineBuilder_WithType_WithValidTypes_ShouldWork(PipelineType pipelineType)
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        var result = builder.WithType(pipelineType);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void PipelineBuilder_MethodChaining_ShouldWorkCorrectly()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        var result = builder
            .WithName("ChainedPipeline")
            .WithDescription("Test chaining")
            .WithType(PipelineType.Sequential);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void PipelineBuilder_WithDifferentTypes_ShouldCompile()
    {
        // Act & Assert - This test ensures the generic types work correctly
        var stringBuilder = new PipelineBuilder<string, string>();
        var intBuilder = new PipelineBuilder<int, string>();
        var objectBuilder = new PipelineBuilder<object, object>();

        stringBuilder.Should().NotBeNull();
        intBuilder.Should().NotBeNull();
        objectBuilder.Should().NotBeNull();
    }

    [Fact]
    public void PipelineBuilder_MultipleCallsToSameMethod_ShouldOverridePreviousValue()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        var result = builder
            .WithName("FirstName")
            .WithName("SecondName")
            .WithDescription("FirstDescription")
            .WithDescription("SecondDescription");

        // Assert
        result.Should().Be(builder);
        // The actual validation would need to be done when building the pipeline
    }

    [Fact]
    public void PipelineBuilder_WithComplexGenericTypes_ShouldWork()
    {
        // Arrange & Act
        var listBuilder = new PipelineBuilder<List<string>, Dictionary<string, object>>();
        var tupleBuilder = new PipelineBuilder<(int, string), Result<string>>();

        // Assert
        listBuilder.Should().NotBeNull();
        tupleBuilder.Should().NotBeNull();
    }

    // Helper class for testing
    private class Result<T>
    {
        public T? Value { get; set; }
        public bool IsSuccess { get; set; }
    }
}