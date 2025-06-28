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

    private static string Generate(Type type)
    {
        var programType = Assembly.Load("RevitStubGenerator").GetType("RevitStubGenerator.Program", true)!;
        var method = programType.GetMethod("GenerateStub", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    [Fact]
    public void Generates_basic_configure_class()
    {
        var result = Generate(typeof(SampleClass));
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

    public class SampleClass
    {
        public int Number { get; set; }
        public string Echo(string text) => text;
    }
}
