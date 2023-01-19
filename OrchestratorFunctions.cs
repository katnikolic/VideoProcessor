using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.X86;
using System.Linq;

namespace VideoProcessor;

public static class OrchestratorFunctions
{
    [FunctionName(nameof(ProcessVideoOrchestrator))]
    public static async Task<object> ProcessVideoOrchestrator(
    [OrchestrationTrigger] IDurableOrchestrationContext context,
    ILogger log)
    {
        log = context.CreateReplaySafeLogger(log);

        var videoLocation = context.GetInput<string>();

        string transcodedLocation = null;
        string thumbnailLocation = null;
        string withintroLocation = null;
        string approvalResult = null;

        try
        {
            var transcodeResults = await context.CallSubOrchestratorAsync<VideoFileInfo[]>(nameof(TranscodeVideoOrchestrator), videoLocation);

            transcodedLocation = transcodeResults.OrderByDescending(t => t.BitRate).First().Location;

            log.LogInformation("about to call extract thumbnail activity");
            thumbnailLocation = await context.CallActivityAsync<string>("ExtractThumbnail", transcodedLocation);

            log.LogInformation("about to call prpend intro activity");
            withintroLocation = await context.CallActivityAsync<string>("PrependIntro", transcodedLocation);

            await context.CallActivityAsync("SendAnEmailForApproval", new ApprovalInfo() { VideoWithIntroLocation = withintroLocation, OrchestrationId = context.InstanceId });

            try
            {
                approvalResult = await context.WaitForExternalEvent<string>("ApprovalResult", TimeSpan.FromSeconds(200));
            }
            catch(TimeoutException ex)
            {
                log.LogInformation("Timeout occured. Your video is going to be rejected");
                approvalResult = "Expired";
            }


            if (approvalResult == "Approved")
                await context.CallActivityAsync("PublishVideo", withintroLocation);
            else
                await context.CallActivityAsync("RejectVideo", withintroLocation);

        }
        catch(Exception ex)
        {
            log.LogError($"Caught an error from an activity: {ex.Message}");

            await context.CallActivityAsync("Cleanup", new List<string>{ transcodedLocation, thumbnailLocation, withintroLocation});

            return new
            {
                Error = "Failed to process uploaded video.",
                Message = ex.Message
            };
        }

        return new
        {
            Transcoded = transcodedLocation,
            Thumnali = thumbnailLocation,
            Intro = withintroLocation,
            ApprovalResult = approvalResult
        };
    }

    [FunctionName(nameof(TranscodeVideoOrchestrator))]
    public static async Task<VideoFileInfo[]> TranscodeVideoOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger log)
    {
        var videoLocation = context.GetInput<string>();

        var bitRates = await context.CallActivityAsync<int[]>("GetTranscodeBitrates", null);

        var transcodedTasks = new List<Task<VideoFileInfo>>();

        foreach (var bitRate in bitRates)
        {
            var videoFileInfo = new VideoFileInfo() { BitRate = bitRate, Location = videoLocation };
            var task = context.CallActivityAsync<VideoFileInfo>("TranscodeVideo", videoFileInfo);
            transcodedTasks.Add(task);
        }

        var transcodeResults = await Task.WhenAll(transcodedTasks);

        return transcodeResults;
    }
}

