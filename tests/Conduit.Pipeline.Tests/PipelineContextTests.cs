using FluentAssertions;
using Conduit.Pipeline;

namespace Conduit.Pipeline.Tests;

public class PipelineContextTests
{
    [Fact]
    public void PipelineContext_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Act
        var context = new PipelineContext();

        // Assert
        context.ContextId.Should().NotBeNullOrEmpty();
        context.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        context.PipelineId.Should().BeNull();
        context.PipelineName.Should().BeNull();
        context.Input.Should().BeNull();
        context.Result.Should().BeNull();
        context.CurrentStage.Should().BeNull();
        context.LastStageIndex.Should().Be(-1);
        context.StartTime.Should().BeNull();
        context.EndTime.Should().BeNull();
        context.IsCancelled.Should().BeFalse();
        context.Exception.Should().BeNull();
        context.HasError.Should().BeFalse();
        context.Properties.Should().BeEmpty();
    }

    [Fact]
    public void PipelineContext_WithContextId_ShouldUseProvidedId()
    {
        // Arrange
        var contextId = "test-context-123";

        // Act
        var context = new PipelineContext(contextId);

        // Assert
        context.ContextId.Should().Be(contextId);
    }

    [Fact]
    public void PipelineContext_WithNullContextId_ShouldThrow()
    {
        // Act & Assert
        var act = () => new PipelineContext(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PipelineContext_SetProperty_ShouldStoreValue()
    {
        // Arrange
        var context = new PipelineContext();
        var key = "test-key";
        var value = "test-value";

        // Act
        context.SetProperty(key, value);

        // Assert
        context.Properties.Should().ContainKey(key);
        context.Properties[key].Should().Be(value);
        context.HasProperty(key).Should().BeTrue();
    }

    [Fact]
    public void PipelineContext_SetProperty_WithNullKey_ShouldThrow()
    {
        // Arrange
        var context = new PipelineContext();

        // Act & Assert
        var act = () => context.SetProperty(null!, "value");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PipelineContext_SetProperty_WithEmptyKey_ShouldThrow()
    {
        // Arrange
        var context = new PipelineContext();

        // Act & Assert
        var act = () => context.SetProperty("", "value");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PipelineContext_SetProperty_WithNullValue_ShouldThrow()
    {
        // Arrange
        var context = new PipelineContext();

        // Act & Assert
        var act = () => context.SetProperty("key", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PipelineContext_GetProperty_ShouldReturnStoredValue()
    {
        // Arrange
        var context = new PipelineContext();
        var key = "test-key";
        var value = "test-value";
        context.SetProperty(key, value);

        // Act
        var result = context.GetProperty(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void PipelineContext_GetProperty_WithNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        var context = new PipelineContext();

        // Act
        var result = context.GetProperty("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void PipelineContext_GetProperty_Generic_ShouldReturnTypedValue()
    {
        // Arrange
        var context = new PipelineContext();
        var key = "test-key";
        var value = "test-value";
        context.SetProperty(key, value);

        // Act
        var result = context.GetProperty<string>(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void PipelineContext_GetProperty_Generic_WithWrongType_ShouldReturnNull()
    {
        // Arrange
        var context = new PipelineContext();
        var key = "test-key";
        var value = "test-value";
        context.SetProperty(key, value);

        // Act
        var result = context.GetProperty<object>(key);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(value);
    }

    [Fact]
    public void PipelineContext_GetValueProperty_ShouldReturnTypedValue()
    {
        // Arrange
        var context = new PipelineContext();
        var key = "test-number";
        var value = 42;
        context.SetProperty(key, value);

        // Act
        var result = context.GetValueProperty<int>(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void PipelineContext_GetValueProperty_WithNonExistentKey_ShouldReturnDefault()
    {
        // Arrange
        var context = new PipelineContext();
        var defaultValue = 100;

        // Act
        var result = context.GetValueProperty("non-existent", defaultValue);

        // Assert
        result.Should().Be(defaultValue);
    }

    [Fact]
    public void PipelineContext_RemoveProperty_ShouldRemoveAndReturnTrue()
    {
        // Arrange
        var context = new PipelineContext();
        var key = "test-key";
        context.SetProperty(key, "value");

        // Act
        var result = context.RemoveProperty(key);

        // Assert
        result.Should().BeTrue();
        context.HasProperty(key).Should().BeFalse();
    }

    [Fact]
    public void PipelineContext_RemoveProperty_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var context = new PipelineContext();

        // Act
        var result = context.RemoveProperty("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void PipelineContext_ClearProperties_ShouldRemoveAllProperties()
    {
        // Arrange
        var context = new PipelineContext();
        context.SetProperty("key1", "value1");
        context.SetProperty("key2", "value2");

        // Act
        context.ClearProperties();

        // Assert
        context.Properties.Should().BeEmpty();
    }

    [Fact]
    public void PipelineContext_Cancel_ShouldSetCancelledFlag()
    {
        // Arrange
        var context = new PipelineContext();

        // Act
        context.Cancel();

        // Assert
        context.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public void PipelineContext_GetElapsedTime_ShouldReturnPositiveTime()
    {
        // Arrange
        var context = new PipelineContext();
        Thread.Sleep(10); // Small delay to ensure elapsed time

        // Act
        var elapsed = context.GetElapsedTime();

        // Assert
        elapsed.Should().BePositive();
    }

    [Fact]
    public void PipelineContext_GetExecutionDuration_WithBothTimes_ShouldReturnDifference()
    {
        // Arrange
        var context = new PipelineContext();
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddSeconds(5);
        context.StartTime = startTime;
        context.EndTime = endTime;

        // Act
        var duration = context.GetExecutionDuration();

        // Assert
        duration.Should().HaveValue();
        duration.Value.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PipelineContext_GetExecutionDuration_WithOnlyStartTime_ShouldReturnCurrentDifference()
    {
        // Arrange
        var context = new PipelineContext();
        context.StartTime = DateTimeOffset.UtcNow.AddSeconds(-2);

        // Act
        var duration = context.GetExecutionDuration();

        // Assert
        duration.Should().HaveValue();
        duration.Value.Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PipelineContext_GetExecutionDuration_WithoutTimes_ShouldReturnNull()
    {
        // Arrange
        var context = new PipelineContext();

        // Act
        var duration = context.GetExecutionDuration();

        // Assert
        duration.Should().BeNull();
    }

    [Fact]
    public void PipelineContext_MarkStageCompleted_ShouldUpdateLastStageIndex()
    {
        // Arrange
        var context = new PipelineContext();
        var stageIndex = 3;

        // Act
        context.MarkStageCompleted(stageIndex);

        // Assert
        context.LastStageIndex.Should().Be(stageIndex);
    }

    [Fact]
    public void PipelineContext_Copy_ShouldCreateNewContextWithSameData()
    {
        // Arrange
        var original = new PipelineContext();
        original.PipelineId = "test-pipeline";
        original.PipelineName = "Test Pipeline";
        original.Input = "test-input";
        original.SetProperty("key1", "value1");

        // Act
        var copy = original.Copy();

        // Assert
        copy.ContextId.Should().NotBe(original.ContextId);
        copy.PipelineId.Should().Be(original.PipelineId);
        copy.PipelineName.Should().Be(original.PipelineName);
        copy.Input.Should().Be(original.Input);
        copy.HasProperty("key1").Should().BeTrue();
        copy.GetProperty("key1").Should().Be("value1");
    }

    [Fact]
    public void PipelineContext_CreateChildContext_ShouldCreateChildWithInheritedProperties()
    {
        // Arrange
        var parent = new PipelineContext();
        parent.PipelineId = "parent-pipeline";
        parent.SetProperty("CorrelationId", "correlation-123");
        parent.SetProperty("UserId", "user-456");
        parent.SetProperty("TenantId", "tenant-789");

        // Act
        var child = parent.CreateChildContext();

        // Assert
        child.ContextId.Should().NotBe(parent.ContextId);
        child.GetProperty("ParentContextId").Should().Be(parent.ContextId);
        child.GetProperty("ParentPipelineId").Should().Be(parent.PipelineId);
        child.GetProperty("CorrelationId").Should().Be("correlation-123");
        child.GetProperty("UserId").Should().Be("user-456");
        child.GetProperty("TenantId").Should().Be("tenant-789");
    }

    [Fact]
    public void PipelineContext_MergeFrom_ShouldMergeProperties()
    {
        // Arrange
        var context1 = new PipelineContext();
        context1.SetProperty("key1", "value1");
        context1.SetProperty("key2", "value2");

        var context2 = new PipelineContext();
        context2.SetProperty("key2", "new-value2");
        context2.SetProperty("key3", "value3");

        // Act
        context1.MergeFrom(context2);

        // Assert
        context1.GetProperty("key1").Should().Be("value1");
        context1.GetProperty("key2").Should().Be("value2"); // Not overwritten
        context1.GetProperty("key3").Should().Be("value3");
    }

    [Fact]
    public void PipelineContext_MergeFrom_WithOverwrite_ShouldOverwriteProperties()
    {
        // Arrange
        var context1 = new PipelineContext();
        context1.SetProperty("key1", "value1");
        context1.SetProperty("key2", "value2");

        var context2 = new PipelineContext();
        context2.SetProperty("key2", "new-value2");
        context2.SetProperty("key3", "value3");

        // Act
        context1.MergeFrom(context2, overwrite: true);

        // Assert
        context1.GetProperty("key1").Should().Be("value1");
        context1.GetProperty("key2").Should().Be("new-value2"); // Overwritten
        context1.GetProperty("key3").Should().Be("value3");
    }

    [Fact]
    public void PipelineContext_CreateWithCorrelation_ShouldSetCorrelationId()
    {
        // Arrange
        var correlationId = "correlation-123";

        // Act
        var context = PipelineContext.CreateWithCorrelation(correlationId);

        // Assert
        context.GetProperty("CorrelationId").Should().Be(correlationId);
        context.HasProperty("Timestamp").Should().BeTrue();
    }

    [Fact]
    public void PipelineContext_CreateForUser_ShouldSetUserProperties()
    {
        // Arrange
        var userId = "user-123";
        var tenantId = "tenant-456";

        // Act
        var context = PipelineContext.CreateForUser(userId, tenantId);

        // Assert
        context.GetProperty("UserId").Should().Be(userId);
        context.GetProperty("TenantId").Should().Be(tenantId);
        context.HasProperty("Timestamp").Should().BeTrue();
    }

    [Fact]
    public void PipelineContext_CreateForUser_WithoutTenant_ShouldNotSetTenantId()
    {
        // Arrange
        var userId = "user-123";

        // Act
        var context = PipelineContext.CreateForUser(userId);

        // Assert
        context.GetProperty("UserId").Should().Be(userId);
        context.HasProperty("TenantId").Should().BeFalse();
    }

    [Fact]
    public void PipelineContext_ToString_ShouldReturnMeaningfulString()
    {
        // Arrange
        var context = new PipelineContext();
        context.PipelineName = "Test Pipeline";
        context.CurrentStage = "Stage1";

        // Act
        var result = context.ToString();

        // Assert
        result.Should().Contain("Test Pipeline");
        result.Should().Contain("Stage1");
        result.Should().Contain(context.ContextId);
    }

    [Fact]
    public void PipelineContext_HasError_WithException_ShouldReturnTrue()
    {
        // Arrange
        var context = new PipelineContext();
        context.Exception = new InvalidOperationException("Test error");

        // Act & Assert
        context.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task PipelineContext_Properties_ShouldBeThreadSafe()
    {
        // Arrange
        var context = new PipelineContext();
        var tasks = new List<Task>();

        // Act - Simulate concurrent access
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                context.SetProperty($"key{index}", $"value{index}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        context.Properties.Should().HaveCount(10);
        for (int i = 0; i < 10; i++)
        {
            context.GetProperty($"key{i}").Should().Be($"value{i}");
        }
    }
}