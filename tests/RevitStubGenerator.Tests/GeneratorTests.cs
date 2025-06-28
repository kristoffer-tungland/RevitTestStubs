using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace RevitStubGenerator.Tests;

public class GeneratorTests
{
    private readonly ITestOutputHelper _output;

    public GeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GenerateClass(Type type)
    {
        var programType = Assembly.Load("RevitStubGenerator").GetType("RevitStubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateClassStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static string GenerateInterface(Type type)
    {
        var programType = Assembly.Load("RevitStubGenerator").GetType("RevitStubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateInterfaceStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static string GenerateEnum(Type type)
    {
        var programType = Assembly.Load("RevitStubGenerator").GetType("RevitStubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateEnumStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static string GenerateStruct(Type type)
    {
        var programType = Assembly.Load("RevitStubGenerator").GetType("RevitStubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateStructStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static string GenerateDelegate(Type type)
    {
        var programType = Assembly.Load("RevitStubGenerator").GetType("RevitStubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateDelegateStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    [Fact]
    public void Generates_basic_configure_class()
    {
        var result = GenerateClass(typeof(SampleClass));
        _output.WriteLine(result);

        Assert.Contains("namespace RevitStubGenerator.Tests", result);
        Assert.Contains("public partial class SampleClass", result);
        Assert.Contains("public SampleClassConfiguration Configure", result);
        Assert.Contains("public virtual System.String Echo(System.String text)", result);
        Assert.Contains("public Func<System.String, System.String>? Echo_0", result);
        Assert.Contains("public virtual System.Int32 Number", result);
        Assert.Contains("public Func<System.Int32>? get_Number", result);
        Assert.Contains("public Action<System.Int32>? set_Number", result);
    }


    [Fact]
    public void Generates_interface_stub()
    {
        var result = GenerateInterface(typeof(ISampleInterface));
        _output.WriteLine(result);

        Assert.Contains("public interface ISampleInterface", result);
        Assert.Contains("System.Int32 Count", result);
        Assert.Contains("System.String Name", result);
        Assert.Contains("void DoSomething", result);
    }

    [Fact]
    public void Generates_enum_stub()
    {
        var result = GenerateEnum(typeof(SampleEnum));
        _output.WriteLine(result);

        Assert.Contains("public enum SampleEnum", result);
        Assert.Contains("One = 1", result);
        Assert.Contains("Two = 2", result);
    }

    [Fact]
    public void Generates_struct_stub()
    {
        var result = GenerateStruct(typeof(SampleStruct));
        _output.WriteLine(result);

        Assert.Contains("public partial struct SampleStruct", result);
        Assert.Contains("public System.Int32 Value", result);
        Assert.Contains("public SampleStruct(System.Int32 value)", result);
        Assert.Contains("public void Do()", result);
    }

    [Fact]
    public void Generates_delegate_stub()
    {
        var result = GenerateDelegate(typeof(SampleDelegate));
        _output.WriteLine(result);

        Assert.Contains("public delegate System.Int32 SampleDelegate(System.String text)", result);
    }

    [Fact]
    public void GenerateClassStub_is_public()
    {
        var method = typeof(RevitStubGenerator.StubGenerator).GetMethod("GenerateClassStub", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
    }

    public class SampleClass
    {
        public int Number { get; set; }
        public string Echo(string text) => text;
    }

    public interface ISampleInterface
    {
        int Count { get; set; }
        string Name { get; }
        void DoSomething(int value);
    }

    public enum SampleEnum
    {
        One = 1,
        Two = 2
    }

    public struct SampleStruct
    {
        public int Value { get; set; }
        public SampleStruct(int value) { Value = value; }
        public void Do() { }
    }

    public delegate int SampleDelegate(string text);
}
