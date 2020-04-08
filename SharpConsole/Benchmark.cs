using System;
using System.Diagnostics;

public static class Benchmark
{
	public static void Run(string name, int iterations, Action action)
	{
		try
		{
			// Perform garbage collection.
			GC.Collect();
			GC.WaitForPendingFinalizers();

			// Force JIT compilation of the method.
			action.Invoke();

			// Run the benchmark.
			Stopwatch watch = Stopwatch.StartNew();
			for (int i = 0; i < iterations; i++)
			{
				action.Invoke();
			}
			watch.Stop();

			// Output results.
			Console.WriteLine($"{watch.Elapsed} {name}");
		}
		catch (OutOfMemoryException)
		{
			Console.WriteLine($"Out of memory!");
		}
	}
}