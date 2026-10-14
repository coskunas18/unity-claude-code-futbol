using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Football.EditorTools
{
    /// <summary>
    /// Runs the Football EditMode tests synchronously and returns a one-line summary.
    /// Used from the menu and from command-line tooling. The tests are pure C# and finish in well under a second.
    /// Menu: Football > Run EditMode Tests.
    /// </summary>
    public static class FootballTestRunner
    {
        const string TestAssembly = "Football.Tests.EditMode";

        [MenuItem("Football/Run EditMode Tests")]
        static void RunFromMenu() => Debug.Log($"[Football] Tests: {Run()}");

        public static string Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var collector = new Collector();
            api.RegisterCallbacks(collector);
            try
            {
                var filter = new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { TestAssembly } };
                api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
            }
            finally
            {
                api.UnregisterCallbacks(collector);
                Object.DestroyImmediate(api);
            }
            return collector.Summary;
        }

        sealed class Collector : ICallbacks
        {
            int passed;
            int failed;
            readonly StringBuilder failures = new StringBuilder();

            public string Summary { get; private set; } = "Test run did not finish";

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result) =>
                Summary = $"passed={passed} failed={failed} duration={result.Duration:0.000}s{failures}";

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren)
                    return;
                if (result.TestStatus == TestStatus.Passed)
                {
                    passed++;
                }
                else
                {
                    failed++;
                    failures.Append($" | {result.Test.FullName}: {result.Message?.Trim()}");
                }
            }
        }
    }
}
