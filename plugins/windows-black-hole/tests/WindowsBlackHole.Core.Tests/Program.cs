using WindowsBlackHole.Core;
using WindowsBlackHole.Shell;

namespace WindowsBlackHole.Core.Tests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--recycle-smoke", StringComparer.OrdinalIgnoreCase))
        {
            return RunRecycleSmokeTest();
        }

        var tests = new (string Name, Action Run)[]
        {
            ("中心点位于事件视界内", () => Assert(BlackHoleGeometry.IsInsideEventHorizon(260, 260))),
            ("窗口角落不在事件视界内", () => Assert(!BlackHoleGeometry.IsInsideEventHorizon(0, 0))),
            ("中心接近度为 1", () => AssertNearly(1, BlackHoleGeometry.Proximity(260, 260))),
            ("影响范围外接近度为 0", () => AssertNearly(0, BlackHoleGeometry.Proximity(0, 0))),
            ("不存在的路径被拒绝", () =>
            {
                var result = DropValidator.Validate(
                    [Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))]);
                Assert(!result.IsValid);
            }),
            ("磁盘根目录被拒绝", () =>
            {
                var root = Path.GetPathRoot(Environment.SystemDirectory)!;
                var result = DropValidator.Validate([root]);
                Assert(!result.IsValid);
            }),
        };

        var failures = new List<string>();
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures.Add($"{test.Name}: {exception.Message}");
                Console.WriteLine($"FAIL {test.Name}: {exception.Message}");
            }
        }

        Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed");
        return failures.Count == 0 ? 0 : 1;
    }

    private static int RunRecycleSmokeTest()
    {
        var smokeDirectory = Path.Combine(
            Path.GetTempPath(),
            $"WindowsBlackHole-Smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(smokeDirectory);
        File.WriteAllText(
            Path.Combine(smokeDirectory, "recycle-me.txt"),
            "Windows Black Hole recycle-bin smoke test.");

        try
        {
            new RecycleBinService().MoveToRecycleBin([smokeDirectory]);
            Assert(!Directory.Exists(smokeDirectory));
            Console.WriteLine($"PASS 临时目录已移入回收站：{smokeDirectory}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"FAIL 回收站冒烟测试：{exception}");
            return 1;
        }
        finally
        {
            if (Directory.Exists(smokeDirectory))
            {
                Directory.Delete(smokeDirectory, recursive: true);
            }
        }
    }

    private static void Assert(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("断言失败");
        }
    }

    private static void AssertNearly(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 0.0001)
        {
            throw new InvalidOperationException($"期望 {expected}，实际 {actual}");
        }
    }
}
