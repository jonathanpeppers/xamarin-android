using BenchmarkDotNet.Attributes;

namespace Benchmarks;

public class TypeManagerBenchmarks
{
    [Benchmark]
    public void NewFoo () => Com.Microsoft.Android.Foo.NewFoo();

    [Benchmark]
    public void SharedFoo () => Com.Microsoft.Android.Foo.SharedFoo();
}