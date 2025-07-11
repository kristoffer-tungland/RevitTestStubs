using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace StubGenerator.Tests;

public class GeneratorTests
{
    private readonly ITestOutputHelper _output;

    public GeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Generates_generic_method_stub()
    {
        var result = GenerateClass(typeof(GenericSample));
        _output.WriteLine(result);

        Assert.Contains("public virtual T GetValue<T>(System.String key)", result);
        Assert.Contains("Func<System.String, System.Type, object?>? GetValue_0", result);
        Assert.Contains("typeof(T)", result);
    }

    private static string GenerateClass(Type type)
    {
        var programType = Assembly.Load("StubGenerator").GetType("StubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateClassStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static string GenerateInterface(Type type)
    {
        var programType = Assembly.Load("StubGenerator").GetType("StubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateInterfaceStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static string GenerateEnum(Type type)
    {
        var programType = Assembly.Load("StubGenerator").GetType("StubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateEnumStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static string GenerateStruct(Type type)
    {
        var programType = Assembly.Load("StubGenerator").GetType("StubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateStructStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static string GenerateDelegate(Type type)
    {
        var programType = Assembly.Load("StubGenerator").GetType("StubGenerator.StubGenerator", true)!;
        var method = programType.GetMethod("GenerateDelegateStub", BindingFlags.Public | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    [Fact]
    public void Generates_basic_configure_class()
    {
        var result = GenerateClass(typeof(SampleClass));
        _output.WriteLine(result);

        Assert.Contains("namespace StubGenerator.Tests", result);
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
    public void Generates_indexer_property()
    {
        var result = GenerateClass(typeof(IndexerSample));
        _output.WriteLine(result);

        Assert.Contains("public virtual System.String this[System.Int32 index]", result);
        Assert.Contains("public Func<System.Int32, System.String>? get_Item", result);
        Assert.Contains("public Action<System.Int32, System.String>? set_Item", result);
    }

    [Fact]
    public void Generates_class_with_interface()
    {
        var result = GenerateClass(typeof(InterfaceImpl));
        _output.WriteLine(result);

        Assert.Contains("public partial class InterfaceImpl : System.IDisposable", result);
    }

    [Fact]
    public void Generates_class_without_duplicate_interfaces()
    {
        var result = GenerateClass(typeof(DerivedImpl));
        _output.WriteLine(result);

        Assert.Contains("public partial class DerivedImpl : BaseImpl, System.IDisposable", result);
        Assert.DoesNotContain("IExample", result);
    }

    [Fact]
    public void Generates_event_stub()
    {
        var result = GenerateClass(typeof(EventSample));
        _output.WriteLine(result);

        Assert.Contains("public event System.EventHandler? Happened", result);
        Assert.Contains("add => Configure.Happened += value", result);
        Assert.Contains("public void RaiseHappened(System.Object sender, System.EventArgs e)", result);
        Assert.Contains("Configure.Happened?.Invoke(sender, e)", result);
        Assert.Contains("public partial class EventSampleConfiguration", result);
        Assert.Contains("public event System.EventHandler? Happened;", result);
    }

    [Fact]
    public void GenerateClassStub_is_public()
    {
        var method = typeof(global::StubGenerator.StubGenerator).GetMethod("GenerateClassStub", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
    }

    [Fact]
    public void Generates_ref_out_methods()
    {
        var result = GenerateClass(typeof(RefOutSample));
        _output.WriteLine(result);

        Assert.Contains("public virtual void Increment(ref System.Int32 value)", result);
        Assert.Contains("public Action<System.Int32>? Increment_0", result);
        Assert.Contains("public virtual System.Boolean TryParse(System.String text, out System.Int32 value)", result);
        Assert.Contains("public Func<System.String, System.Int32, System.Boolean>? TryParse_1", result);
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

    public class EventSample
    {
        public event EventHandler? Happened;
    }

    public class GenericSample
    {
        public T GetValue<T>(string key) => default!;
    }

    public interface IExample
    {
        void Do();
    }

    public class BaseImpl : IExample
    {
        public void Do() { }
    }

    public class DerivedImpl : BaseImpl, IDisposable
    {
        public void Dispose() { }
    }

    public class InterfaceImpl : IDisposable
    {
        public void Dispose() { }
    }

    public class IndexerSample
    {
        public string this[int index]
        {
            get => string.Empty;
            set { }
        }
    }

    public class RefOutSample
    {
        public void Increment(ref int value) { }
        public bool TryParse(string text, out int value) { value = 0; return false; }
    }
}
