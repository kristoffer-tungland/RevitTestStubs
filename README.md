# RevitTestStubs

This repository demonstrates how to create mockable stubs for assemblies that do
not expose interfaces. The heart of the repo is a small generator that reads any
.NET assembly and emits partial classes for all public types. Each generated
class is accompanied by a `Configure` helper that exposes delegates for every
public method and property so behaviour can easily be mocked in unit tests.

The repository also contains a few generated Revit stubs under `example/`. They
represent a small portion of a larger stub and illustrate how the approach can
be used in practice.

## Generating stubs

Run the generator with the path to an assembly and an output folder. The tool
uses `MetadataLoadContext` so it can analyse the types without loading any of
the assembly's dependencies at runtime. For example, to process Revit's
`RevitAPI.dll` you could run:

```bash
dotnet tool run stubgen \
    "C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll" Generated\RevitStubs
```

The command above will create a tree of partial classes under the specified
output folder. Each class includes a nested `Configuration` object where you can
assign delegates for all public members.

## Using the stubs

The example stubs in `example/` show how the generated classes can be used in
tests. Below is a simplified unit test demonstrating how a portion of the
`Element` stub can be configured:

```csharp
using Autodesk.Revit.DB;
using Xunit;

[Fact]
public void Can_mock_element_parameter()
{
    var element = new Element(42);
    var expected = new Parameter(1);
    element.Configure.GetParameter = _ => expected;

    var result = element.GetParameter(Guid.NewGuid());

    Assert.Same(expected, result);
}
```


## Projects

- **StubGenerator** – dotnet tool that uses `MetadataLoadContext` to read any .NET assembly and emit partial class stubs. It scans every public type and generates a partial implementation together with a `Configure` class containing delegates for all members.
- **RevitTestStubs** – example library located under the `example` folder containing a few stubbed types from Revit's API.
