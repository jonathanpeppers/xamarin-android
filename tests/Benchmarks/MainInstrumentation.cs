using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Benchmarks.Droid;

[Instrumentation(Name = "com.microsoft.android.MainInstrumentation")]
public class MainInstrumentation : Instrumentation
{
	const string Tag = "BENCH";

	public static MainInstrumentation? Instance { get; private set; }
	public static string ExternalDataDirectory { get; private set; } = string.Empty;

	protected MainInstrumentation(IntPtr handle, JniHandleOwnership transfer)
		: base(handle, transfer) { }

	public override void OnCreate(Bundle? arguments)
	{
		base.OnCreate(arguments);

		Instance = this;
		ExternalDataDirectory = Context?.GetExternalFilesDir(null)?.ToString() ?? string.Empty;
		if (string.IsNullOrEmpty(ExternalDataDirectory))
		{
			Log.Error(Tag, "ExternalDataDirectory is failed to be set");
			return;
		}
		Log.Debug(Tag, $"ExternalDataDirectory: {ExternalDataDirectory}");

		Start();
	}

	public async override void OnStart()
	{
		base.OnStart();

		var success = await Task.Factory.StartNew(Run);
		Log.Debug(Tag, $"Benchmark complete, success: {success}");
		Finish(success ? Result.Ok : Result.Canceled, new Bundle());
	}

	static bool Run()
	{
		bool success = false;
		try
		{
			var config = ManualConfig.CreateMinimumViable()
				.AddJob(Job.Default.WithToolchain(new InProcessEmitToolchain(TimeSpan.FromMinutes(10), logOutput: true)))
				.AddDiagnoser(MemoryDiagnoser.Default)
				.WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest, MethodOrderPolicy.Alphabetical));

			// Benchmark classes hardcoded
			BenchmarkRunner.Run<TypeManagerBenchmarks>(config);

			success = true;
		}
		catch (Exception ex)
		{
			Log.Error(Tag, $"Error: {ex}");
		}
		return success;
	}
}