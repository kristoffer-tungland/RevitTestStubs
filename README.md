# RevitTestStubs

This repository contains a small utility for generating stub classes from the Autodesk Revit API and a source only package that provides a few hand written stubs used for unit testing. The package exposes minimal `ElementId`, `Element` and `Parameter` implementations with a `Configure` property that allows delegates to be attached for mocking behaviour.

## Projects

- **RevitStubGenerator** – console application that uses `MetadataLoadContext` to read a Revit assembly and emit partial class stubs. The generated files can be added to the source package.
- **RevitTestStubs** – library that ships source files in a `buildTransitive` folder so they become part of any project referencing the package.
- **RevitTestStubs.Tests** – xUnit tests demonstrating usage of the stubs.

To create a package run `dotnet pack src/RevitTestStubs` which will produce a source only NuGet package.
