using FluentAssertions;
using Conduit.Common.Reflection;
using System.Collections;
using System.Reflection;
using Xunit;

namespace Conduit.Common.Tests;

public class TypeExtensionsTests
{
    // Test interface for Implements tests
    private interface ITestInterface { }
    private interface IGenericTestInterface<T> { }

    // Test classes for inheritance tests
    private class BaseTestClass { }
    private class DerivedTestClass : BaseTestClass, ITestInterface { }
    private class GenericDerivedClass : IGenericTestInterface<string> { }

    // Test collection class
    private class TestCollection : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => throw new NotImplementedException();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // Test attribute
    private class TestAttribute : Attribute { }

    // Test class with attributes
    private class AttributedTestClass
    {
        [TestAttribute]
        public string TestProperty { get; set; } = "";

        public string NonAttributedProperty { get; set; } = "";

        [TestAttribute]
        public void TestMethod() { }

        public void NonAttributedMethod() { }
    }

    #region Implements Tests

    [Fact]
    public void Implements_Generic_WithImplementingType_ShouldReturnTrue()
    {
        // Act
        var result = typeof(DerivedTestClass).Implements<ITestInterface>();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Implements_Generic_WithNonImplementingType_ShouldReturnFalse()
    {
        // Act
        var result = typeof(BaseTestClass).Implements<ITestInterface>();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Implements_WithImplementingType_ShouldReturnTrue()
    {
        // Act
        var result = typeof(DerivedTestClass).Implements(typeof(ITestInterface));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Implements_WithNonImplementingType_ShouldReturnFalse()
    {
        // Act
        var result = typeof(BaseTestClass).Implements(typeof(ITestInterface));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Implements_WithGenericInterface_ShouldReturnTrue()
    {
        // Act
        var result = typeof(GenericDerivedClass).Implements(typeof(IGenericTestInterface<>));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Implements_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).Implements(typeof(ITestInterface));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Implements_WithNullInterfaceType_ShouldThrow()
    {
        // Act & Assert
        var act = () => typeof(DerivedTestClass).Implements(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Implements_WithNonInterfaceType_ShouldThrow()
    {
        // Act & Assert
        var act = () => typeof(DerivedTestClass).Implements(typeof(string));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*is not an interface*");
    }

    #endregion

    #region InheritsFrom Tests

    [Fact]
    public void InheritsFrom_Generic_WithDerivedType_ShouldReturnTrue()
    {
        // Act
        var result = typeof(DerivedTestClass).InheritsFrom<BaseTestClass>();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void InheritsFrom_Generic_WithNonDerivedType_ShouldReturnFalse()
    {
        // Act
        var result = typeof(BaseTestClass).InheritsFrom<DerivedTestClass>();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void InheritsFrom_Generic_WithSameType_ShouldReturnFalse()
    {
        // Act
        var result = typeof(BaseTestClass).InheritsFrom<BaseTestClass>();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void InheritsFrom_WithDerivedType_ShouldReturnTrue()
    {
        // Act
        var result = typeof(DerivedTestClass).InheritsFrom(typeof(BaseTestClass));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void InheritsFrom_WithObjectType_ShouldReturnTrue()
    {
        // Act
        var result = typeof(DerivedTestClass).InheritsFrom(typeof(object));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void InheritsFrom_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).InheritsFrom(typeof(BaseTestClass));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InheritsFrom_WithNullBaseType_ShouldThrow()
    {
        // Act & Assert
        var act = () => typeof(DerivedTestClass).InheritsFrom(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetAssignableTypes Tests

    [Fact]
    public void GetAssignableTypes_WithValidTypes_ShouldReturnAssignableTypes()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var result = assembly.GetAssignableTypes(typeof(ITestInterface));

        // Assert
        result.Should().Contain(typeof(DerivedTestClass));
        result.Should().NotContain(typeof(ITestInterface)); // Interfaces excluded
        result.Should().NotContain(typeof(BaseTestClass)); // Non-implementing types excluded
    }

    [Fact]
    public void GetAssignableTypes_WithNullAssembly_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Assembly)null!).GetAssignableTypes(typeof(ITestInterface));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetAssignableTypes_WithNullTargetType_ShouldThrow()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act & Assert
        var act = () => assembly.GetAssignableTypes(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetDefaultValue Tests

    [Fact]
    public void GetDefaultValue_WithValueType_ShouldReturnDefaultValue()
    {
        // Act
        var result = typeof(int).GetDefaultValue();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetDefaultValue_WithReferenceType_ShouldReturnNull()
    {
        // Act
        var result = typeof(string).GetDefaultValue();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDefaultValue_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).GetDefaultValue();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region IsNullable Tests

    [Fact]
    public void IsNullable_WithReferenceType_ShouldReturnTrue()
    {
        // Act
        var result = typeof(string).IsNullable();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullable_WithNullableValueType_ShouldReturnTrue()
    {
        // Act
        var result = typeof(int?).IsNullable();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullable_WithValueType_ShouldReturnFalse()
    {
        // Act
        var result = typeof(int).IsNullable();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsNullable_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).IsNullable();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetUnderlyingType Tests

    [Fact]
    public void GetUnderlyingType_WithNullableType_ShouldReturnUnderlyingType()
    {
        // Act
        var result = typeof(int?).GetUnderlyingType();

        // Assert
        result.Should().Be(typeof(int));
    }

    [Fact]
    public void GetUnderlyingType_WithNonNullableType_ShouldReturnSameType()
    {
        // Act
        var result = typeof(int).GetUnderlyingType();

        // Assert
        result.Should().Be(typeof(int));
    }

    [Fact]
    public void GetUnderlyingType_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).GetUnderlyingType();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region IsNumeric Tests

    [Theory]
    [InlineData(typeof(byte))]
    [InlineData(typeof(sbyte))]
    [InlineData(typeof(short))]
    [InlineData(typeof(ushort))]
    [InlineData(typeof(int))]
    [InlineData(typeof(uint))]
    [InlineData(typeof(long))]
    [InlineData(typeof(ulong))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    public void IsNumeric_WithNumericTypes_ShouldReturnTrue(Type numericType)
    {
        // Act
        var result = numericType.IsNumeric();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(byte?))]
    [InlineData(typeof(sbyte?))]
    [InlineData(typeof(short?))]
    [InlineData(typeof(ushort?))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(uint?))]
    [InlineData(typeof(long?))]
    [InlineData(typeof(ulong?))]
    [InlineData(typeof(float?))]
    [InlineData(typeof(double?))]
    [InlineData(typeof(decimal?))]
    public void IsNumeric_WithNullableNumericTypes_ShouldReturnTrue(Type nullableNumericType)
    {
        // Act
        var result = nullableNumericType.IsNumeric();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(char))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(object))]
    public void IsNumeric_WithNonNumericTypes_ShouldReturnFalse(Type nonNumericType)
    {
        // Act
        var result = nonNumericType.IsNumeric();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsNumeric_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).IsNumeric();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region IsCollection Tests

    [Fact]
    public void IsCollection_WithStringType_ShouldReturnFalse()
    {
        // Act
        var result = typeof(string).IsCollection();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(HashSet<string>))]
    [InlineData(typeof(TestCollection))]
    public void IsCollection_WithCollectionTypes_ShouldReturnTrue(Type collectionType)
    {
        // Act
        var result = collectionType.IsCollection();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(object))]
    public void IsCollection_WithNonCollectionTypes_ShouldReturnFalse(Type nonCollectionType)
    {
        // Act
        var result = nonCollectionType.IsCollection();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCollection_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).IsCollection();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetCollectionElementType Tests

    [Fact]
    public void GetCollectionElementType_WithArrayType_ShouldReturnElementType()
    {
        // Act
        var result = typeof(int[]).GetCollectionElementType();

        // Assert
        result.Should().Be(typeof(int));
    }

    [Fact]
    public void GetCollectionElementType_WithGenericCollectionType_ShouldReturnElementType()
    {
        // Act
        var result = typeof(List<string>).GetCollectionElementType();

        // Assert
        result.Should().Be(typeof(string));
    }

    [Fact]
    public void GetCollectionElementType_WithNonCollectionType_ShouldReturnNull()
    {
        // Act
        var result = typeof(int).GetCollectionElementType();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCollectionElementType_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).GetCollectionElementType();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetPropertiesWithAttribute Tests

    [Fact]
    public void GetPropertiesWithAttribute_WithAttributedProperties_ShouldReturnCorrectProperties()
    {
        // Act
        var result = typeof(AttributedTestClass).GetPropertiesWithAttribute<TestAttribute>();

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be(nameof(AttributedTestClass.TestProperty));
    }

    [Fact]
    public void GetPropertiesWithAttribute_WithNoAttributedProperties_ShouldReturnEmpty()
    {
        // Act
        var result = typeof(BaseTestClass).GetPropertiesWithAttribute<TestAttribute>();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPropertiesWithAttribute_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).GetPropertiesWithAttribute<TestAttribute>();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetMethodsWithAttribute Tests

    [Fact]
    public void GetMethodsWithAttribute_WithAttributedMethods_ShouldReturnCorrectMethods()
    {
        // Act
        var result = typeof(AttributedTestClass).GetMethodsWithAttribute<TestAttribute>();

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be(nameof(AttributedTestClass.TestMethod));
    }

    [Fact]
    public void GetMethodsWithAttribute_WithNoAttributedMethods_ShouldReturnEmpty()
    {
        // Act
        var result = typeof(BaseTestClass).GetMethodsWithAttribute<TestAttribute>();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetMethodsWithAttribute_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).GetMethodsWithAttribute<TestAttribute>();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region CreateInstance Tests

    [Fact]
    public void CreateInstance_Generic_WithValidType_ShouldCreateInstance()
    {
        // Act
        var result = typeof(List<int>).CreateInstance<IList<int>>();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<int>>();
    }

    [Fact]
    public void CreateInstance_Generic_WithIncompatibleType_ShouldThrow()
    {
        // Act & Assert
        var act = () => typeof(string).CreateInstance<int>();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be assigned to*");
    }

    [Fact]
    public void CreateInstance_WithArgs_ShouldCreateInstanceWithParameters()
    {
        // Act
        var result = typeof(List<int>).CreateInstance<IList<int>>(10);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<int>>();
    }

    [Fact]
    public void CreateInstance_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).CreateInstance<object>();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetFriendlyName Tests

    [Fact]
    public void GetFriendlyName_WithNonGenericType_ShouldReturnTypeName()
    {
        // Act
        var result = typeof(string).GetFriendlyName();

        // Assert
        result.Should().Be("String");
    }

    [Fact]
    public void GetFriendlyName_WithGenericType_ShouldReturnFriendlyName()
    {
        // Act
        var result = typeof(List<int>).GetFriendlyName();

        // Assert
        result.Should().Be("List<Int32>");
    }

    [Fact]
    public void GetFriendlyName_WithNestedGenericType_ShouldReturnFriendlyName()
    {
        // Act
        var result = typeof(Dictionary<string, List<int>>).GetFriendlyName();

        // Assert
        result.Should().Be("Dictionary<String, List<Int32>>");
    }

    [Fact]
    public void GetFriendlyName_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).GetFriendlyName();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region HasParameterlessConstructor Tests

    [Fact]
    public void HasParameterlessConstructor_WithParameterlessConstructor_ShouldReturnTrue()
    {
        // Act
        var result = typeof(List<int>).HasParameterlessConstructor();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasParameterlessConstructor_WithNoParameterlessConstructor_ShouldReturnFalse()
    {
        // Act
        var result = typeof(string).HasParameterlessConstructor();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasParameterlessConstructor_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).HasParameterlessConstructor();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetBaseTypesAndInterfaces Tests

    [Fact]
    public void GetBaseTypesAndInterfaces_WithDerivedClass_ShouldReturnBaseTypesAndInterfaces()
    {
        // Act
        var result = typeof(DerivedTestClass).GetBaseTypesAndInterfaces().ToList();

        // Assert
        result.Should().Contain(typeof(BaseTestClass));
        result.Should().Contain(typeof(object));
        result.Should().Contain(typeof(ITestInterface));
    }

    [Fact]
    public void GetBaseTypesAndInterfaces_WithObjectType_ShouldReturnEmpty()
    {
        // Act
        var result = typeof(object).GetBaseTypesAndInterfaces().ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetBaseTypesAndInterfaces_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).GetBaseTypesAndInterfaces().ToList();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region IsSimpleType Tests

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(TimeSpan))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(ConsoleColor))] // Enum
    public void IsSimpleType_WithSimpleTypes_ShouldReturnTrue(Type simpleType)
    {
        // Act
        var result = simpleType.IsSimpleType();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(Dictionary<string, int>))]
    [InlineData(typeof(BaseTestClass))]
    public void IsSimpleType_WithComplexTypes_ShouldReturnFalse(Type complexType)
    {
        // Act
        var result = complexType.IsSimpleType();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSimpleType_WithNullableSimpleType_ShouldReturnTrue()
    {
        // Act
        var result = typeof(int?).IsSimpleType();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSimpleType_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).IsSimpleType();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetGenericTypeDefinitionSafe Tests

    [Fact]
    public void GetGenericTypeDefinitionSafe_WithGenericType_ShouldReturnDefinition()
    {
        // Act
        var result = typeof(List<int>).GetGenericTypeDefinitionSafe();

        // Assert
        result.Should().Be(typeof(List<>));
    }

    [Fact]
    public void GetGenericTypeDefinitionSafe_WithNonGenericType_ShouldReturnNull()
    {
        // Act
        var result = typeof(int).GetGenericTypeDefinitionSafe();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetGenericTypeDefinitionSafe_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).GetGenericTypeDefinitionSafe();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region IsAnonymousType Tests

    [Fact]
    public void IsAnonymousType_WithAnonymousType_ShouldReturnTrue()
    {
        // Arrange
        var anonymousType = new { Name = "Test", Value = 42 }.GetType();

        // Act
        var result = anonymousType.IsAnonymousType();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAnonymousType_WithRegularType_ShouldReturnFalse()
    {
        // Act
        var result = typeof(BaseTestClass).IsAnonymousType();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAnonymousType_WithNullType_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((Type)null!).IsAnonymousType();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetPropertyValue Tests

    [Fact]
    public void GetPropertyValue_WithValidProperty_ShouldReturnValue()
    {
        // Arrange
        var obj = new AttributedTestClass { TestProperty = "test value" };

        // Act
        var result = obj.GetPropertyValue(nameof(AttributedTestClass.TestProperty));

        // Assert
        result.Should().Be("test value");
    }

    [Fact]
    public void GetPropertyValue_WithNonExistentProperty_ShouldReturnNull()
    {
        // Arrange
        var obj = new AttributedTestClass();

        // Act
        var result = obj.GetPropertyValue("NonExistentProperty");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetPropertyValue_WithNullObject_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((object)null!).GetPropertyValue("Property");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetPropertyValue_WithNullPropertyName_ShouldThrow()
    {
        // Arrange
        var obj = new AttributedTestClass();

        // Act & Assert
        var act = () => obj.GetPropertyValue(null!);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region SetPropertyValue Tests

    [Fact]
    public void SetPropertyValue_WithValidProperty_ShouldSetValue()
    {
        // Arrange
        var obj = new AttributedTestClass();

        // Act
        obj.SetPropertyValue(nameof(AttributedTestClass.TestProperty), "new value");

        // Assert
        obj.TestProperty.Should().Be("new value");
    }

    [Fact]
    public void SetPropertyValue_WithNonExistentProperty_ShouldNotThrow()
    {
        // Arrange
        var obj = new AttributedTestClass();

        // Act & Assert
        var act = () => obj.SetPropertyValue("NonExistentProperty", "value");
        act.Should().NotThrow();
    }

    [Fact]
    public void SetPropertyValue_WithNullObject_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((object)null!).SetPropertyValue("Property", "value");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetPropertyValue_WithNullPropertyName_ShouldThrow()
    {
        // Arrange
        var obj = new AttributedTestClass();

        // Act & Assert
        var act = () => obj.SetPropertyValue(null!, "value");
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region InvokeMethod Tests

    [Fact]
    public void InvokeMethod_WithValidMethod_ShouldInvokeMethod()
    {
        // Arrange
        var obj = new List<int> { 1, 2, 3 };

        // Act
        var result = obj.InvokeMethod("Clear");

        // Assert
        result.Should().BeNull(); // Void method returns null
        obj.Should().BeEmpty();
    }

    [Fact]
    public void InvokeMethod_WithMethodWithParameters_ShouldInvokeMethod()
    {
        // Arrange
        var obj = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var result = obj.InvokeMethod("GetRange", 1, 3);

        // Assert
        result.Should().BeOfType<List<int>>();
        ((List<int>)result!).Should().Equal(2, 3, 4);
    }

    [Fact]
    public void InvokeMethod_WithNonExistentMethod_ShouldReturnNull()
    {
        // Arrange
        var obj = new AttributedTestClass();

        // Act
        var result = obj.InvokeMethod("NonExistentMethod");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void InvokeMethod_WithNullObject_ShouldThrow()
    {
        // Act & Assert
        var act = () => ((object)null!).InvokeMethod("Method");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InvokeMethod_WithNullMethodName_ShouldThrow()
    {
        // Arrange
        var obj = new AttributedTestClass();

        // Act & Assert
        var act = () => obj.InvokeMethod(null!);
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}