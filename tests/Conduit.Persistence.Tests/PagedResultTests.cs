using FluentAssertions;
using Conduit.Persistence;

namespace Conduit.Persistence.Tests;

public class PagedResultTests
{
    [Fact]
    public void PagedResult_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var result = new PagedResult<string>();

        // Assert
        result.Items.Should().BeEmpty();
        result.PageNumber.Should().Be(0);
        result.PageSize.Should().Be(0);
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void PagedResult_TotalPages_ShouldCalculateCorrectly()
    {
        // Arrange & Act
        var result1 = new PagedResult<string>
        {
            TotalCount = 100,
            PageSize = 10
        };

        var result2 = new PagedResult<string>
        {
            TotalCount = 95,
            PageSize = 10
        };

        var result3 = new PagedResult<string>
        {
            TotalCount = 100,
            PageSize = 7
        };

        // Assert
        result1.TotalPages.Should().Be(10); // 100 / 10 = 10
        result2.TotalPages.Should().Be(10); // 95 / 10 = 9.5 -> 10
        result3.TotalPages.Should().Be(15); // 100 / 7 = 14.28... -> 15
    }

    [Fact]
    public void PagedResult_TotalPages_WithZeroPageSize_ShouldReturnZero()
    {
        // Arrange & Act
        var result = new PagedResult<string>
        {
            TotalCount = 100,
            PageSize = 0
        };

        // Assert
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public void PagedResult_HasPreviousPage_ShouldReturnCorrectValue()
    {
        // Arrange & Act
        var firstPage = new PagedResult<string> { PageNumber = 1 };
        var secondPage = new PagedResult<string> { PageNumber = 2 };
        var zeroPage = new PagedResult<string> { PageNumber = 0 };

        // Assert
        firstPage.HasPreviousPage.Should().BeFalse();
        secondPage.HasPreviousPage.Should().BeTrue();
        zeroPage.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void PagedResult_HasNextPage_ShouldReturnCorrectValue()
    {
        // Arrange & Act
        var result1 = new PagedResult<string>
        {
            PageNumber = 1,
            TotalCount = 100,
            PageSize = 10 // Total pages = 10
        };

        var result2 = new PagedResult<string>
        {
            PageNumber = 10,
            TotalCount = 100,
            PageSize = 10 // Total pages = 10
        };

        var result3 = new PagedResult<string>
        {
            PageNumber = 5,
            TotalCount = 100,
            PageSize = 10 // Total pages = 10
        };

        // Assert
        result1.HasNextPage.Should().BeTrue(); // Page 1 of 10
        result2.HasNextPage.Should().BeFalse(); // Page 10 of 10
        result3.HasNextPage.Should().BeTrue(); // Page 5 of 10
    }

    [Fact]
    public void PagedResult_WithItems_ShouldStoreItems()
    {
        // Arrange
        var items = new[] { "item1", "item2", "item3" };

        // Act
        var result = new PagedResult<string>
        {
            Items = items,
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 3
        };

        // Assert
        result.Items.Should().BeEquivalentTo(items);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public void PagedResult_CompleteExample_ShouldWorkCorrectly()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).Select(i => $"Item {i}");

        // Act
        var result = new PagedResult<string>
        {
            Items = items,
            PageNumber = 2,
            PageSize = 10,
            TotalCount = 35
        };

        // Assert
        result.Items.Should().HaveCount(10);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(35);
        result.TotalPages.Should().Be(4); // 35 / 10 = 3.5 -> 4
        result.HasPreviousPage.Should().BeTrue(); // Page 2 has previous
        result.HasNextPage.Should().BeTrue(); // Page 2 of 4 has next
    }

    [Fact]
    public void PagedResult_EdgeCases_ShouldHandleCorrectly()
    {
        // Test single item
        var singleItem = new PagedResult<int>
        {
            Items = new[] { 42 },
            PageNumber = 1,
            PageSize = 1,
            TotalCount = 1
        };

        singleItem.TotalPages.Should().Be(1);
        singleItem.HasPreviousPage.Should().BeFalse();
        singleItem.HasNextPage.Should().BeFalse();

        // Test empty result
        var emptyResult = new PagedResult<int>
        {
            Items = Array.Empty<int>(),
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 0
        };

        emptyResult.TotalPages.Should().Be(0);
        emptyResult.HasPreviousPage.Should().BeFalse();
        emptyResult.HasNextPage.Should().BeFalse();
    }
}