namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO.Pipes;
	using System.Linq;
	using System.Threading.Tasks;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;

	/// <summary>A second launch of the program hands its files to the one that is running.</summary>
	public sealed class SingleInstanceTests
	{
		private static String NewPipeName() => "DatExplorerIII.Tests." + Guid.NewGuid().ToString("N");

		[Fact]
		public void TheOptionIsOnByDefault()
		{
			Assert.True(new SettingsData().SingleInstance);
		}

		[Fact]
		public async Task TheFilesReachTheRunningInstance()
		{
			String name = NewPipeName();
			TaskCompletionSource<IReadOnlyList<String>> received = new();
			using SingleInstance server = new(name, paths => received.TrySetResult(paths));
			Assert.True(server.Start());

			String[] files = [@"C:\Games\master.dat", @"D:\Mods\Modul é 日本.dat", @"\\server\share\x.patch1"];
			Assert.True(SingleInstance.TrySend(name, files));

			Assert.Equal(files, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
		}

		[Fact]
		public async Task ALaunchWithoutFiles_StillReachesTheRunningInstance()
		{
			String name = NewPipeName();
			TaskCompletionSource<IReadOnlyList<String>> received = new();
			using SingleInstance server = new(name, paths => received.TrySetResult(paths));
			server.Start();

			Assert.True(SingleInstance.TrySend(name, Array.Empty<String>()));
			Assert.Empty(await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
		}

		[Fact]
		public void SeveralLaunches_AreEachServed()
		{
			String name = NewPipeName();
			List<String> all = new();
			using SingleInstance server = new(name, paths =>
			{
				lock (all)
				{
					all.AddRange(paths);
				}
			});
			server.Start();

			for (Int32 i = 0; i < 20; i++)
			{
				Assert.True(SingleInstance.TrySend(name, ["file" + i + ".dat"]));
			}

			lock (all)
			{
				Assert.Equal(Enumerable.Range(0, 20).Select(i => "file" + i + ".dat"), all);
			}
		}

		[Fact]
		public void WithoutARunningInstance_NothingIsSent()
		{
			Assert.False(SingleInstance.TrySend(NewPipeName(), ["a.dat"], 100));
		}

		[Fact]
		public void WithoutARunningInstance_TheLaunchDoesNotWait()
		{
			// The time out is for an instance that is slow to answer: when none listens, the launch knows it at once.
			System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
			Assert.False(SingleInstance.TrySend(NewPipeName(), ["a.dat"], 5000));
			Assert.True(watch.ElapsedMilliseconds < 1000, $"{watch.ElapsedMilliseconds} ms");
		}

		[Fact]
		public void OnlyTheFirstInstanceListens()
		{
			String name = NewPipeName();
			using SingleInstance first = new(name, _ => { });
			using SingleInstance second = new(name, _ => { });

			Assert.True(first.Start());
			Assert.True(first.IsRunning);
			Assert.False(second.Start());
			Assert.False(second.IsRunning);
		}

		[Fact]
		public void AfterStop_ALaunchIsNotHandedOver_AndListeningCanStartAgain()
		{
			String name = NewPipeName();
			Int32 count = 0;
			using SingleInstance server = new(name, _ => count++);

			Assert.True(server.Start());
			Assert.True(SingleInstance.TrySend(name, ["a.dat"]));

			server.Stop();
			Assert.False(server.IsRunning);
			Assert.False(SingleInstance.TrySend(name, ["b.dat"], 100));
			Assert.Equal(1, count);

			Assert.True(server.Start());
			Assert.True(SingleInstance.TrySend(name, ["c.dat"]));
			Assert.Equal(2, count);
		}

		[Fact]
		public void AnInvalidMessage_DoesNotStopTheServer()
		{
			String name = NewPipeName();
			List<String> received = new();
			using SingleInstance server = new(name, paths =>
			{
				lock (received)
				{
					received.AddRange(paths);
				}
			});
			server.Start();

			// An absurd number of files, a path that is too long, and a sender that leaves in the middle of its message.
			foreach (Byte[] garbage in new[]
			{
				new Byte[] { 0xFF, 0xFF, 0xFF, 0x7F },
				[1, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0x7F],
				[3, 0, 0, 0, 2, 0, 0, 0, 65],
			})
			{
				using NamedPipeClientStream client = new(".", name, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
				client.Connect(2000);
				Byte[] pid = new Byte[4];
				client.ReadExactly(pid);
				client.Write(garbage);
				client.Flush();
			}

			Assert.True(SingleInstance.TrySend(name, ["ok.dat"], 3000));
			lock (received)
			{
				Assert.Equal(new[] { "ok.dat" }, received);
			}
		}
	}
}