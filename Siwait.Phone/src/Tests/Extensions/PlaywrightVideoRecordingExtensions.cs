using System.Reflection;

namespace Siwait.Phone.Tests.Extensions;

public static class PlaywrightVideoRecordingExtensions
{
    public static async Task FinalizeVideoRecording(this PageTest page)
    {
        await FinalizeVideoRecording(page.Context, page.TestContext);
    }

    public static async Task FinalizeVideoRecording(this IBrowserContext browserContext, TestContext testContext)
    {
        await browserContext.CloseAsync();
        if (testContext.CurrentTestOutcome is not UnitTestOutcome.Failed)
        {
            var directory = GetVideoDirectory(testContext);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    public static BrowserNewContextOptions EnableVideoRecording(this BrowserNewContextOptions options, TestContext testContext)
    {
        options.RecordVideoDir = GetVideoDirectory(testContext);
        return options;
    }

    public static BrowserNewContextOptions EnableVideoRecording(this BrowserNewContextOptions options, TestContext testContext, string testMethodFullName)
    {
        options.RecordVideoDir = GetVideoDirectory(testContext, testMethodFullName);
        return options;
    }

    private static string GetVideoDirectory(TestContext testContext)
    {
        var testMethodFullName = $"{testContext.FullyQualifiedTestClassName}.{GetTestMethodName(testContext)}";
        return GetVideoDirectory(testContext, testMethodFullName);
    }

    private static string GetVideoDirectory(TestContext testContext, string testMethodFullName)
    {
        // Remove invalid characters from the test method name
        char[] notAllowedChars = [')', '"', '<', '>', '|', '*', '?', '\r', '\n', .. Path.GetInvalidFileNameChars()];
        testMethodFullName = new string(testMethodFullName.Where(ch => !notAllowedChars.Contains(ch)).ToArray()).Replace('(', '_').Replace(',', '_');

        var dir = Path.Combine(testContext.TestResultsDirectory!, "..", "..", "Videos", testMethodFullName);
        return Path.GetFullPath(dir);
    }

    private static string GetTestMethodName(TestContext testContext)
    {
        return testContext.TestName!;
    }
}
