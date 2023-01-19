using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace VideoProcessor
{
    public static class ActivityFunctions
    {

        [FunctionName(nameof(GetTranscodeBitrates))]
        public static async Task<int[]> GetTranscodeBitrates(
            [ActivityTrigger] string input,
            ILogger log)
        {
            var transcodeBitrates = Environment.GetEnvironmentVariable("TranscodeBitrates").Split(",").Select(x => int.Parse(x)).ToArray();

            log.LogInformation($"Getting bitarates for transcoding video");

            return transcodeBitrates;
        }


        [FunctionName(nameof(TranscodeVideo))]
        public static async Task<VideoFileInfo> TranscodeVideo(
            [ActivityTrigger] VideoFileInfo inputVideo, 
            ILogger log)
        {
            log.LogInformation($"Transcoding {inputVideo.Location} to {inputVideo.BitRate}.");
            // simulate activity
            await Task.Delay(5000);

            var transcodedLocation = $"{Path.GetFileNameWithoutExtension(inputVideo.Location)}-{inputVideo.BitRate}kbps.mp4";

            return new VideoFileInfo() { Location = transcodedLocation, BitRate = inputVideo.BitRate };
        }

        [FunctionName(nameof(ExtractThumbnail))]
        public static async Task<string> ExtractThumbnail(
        [ActivityTrigger] string inputVideo,
        ILogger log)
        {
            log.LogInformation($"Extracting thumbnail from {inputVideo}.");

            if (inputVideo.Contains("error"))
            {
                throw new InvalidOperationException("Failed to extract thumbnail.");
            }

            // simulate activity
            await Task.Delay(5000);
            return $"{Path.GetFileNameWithoutExtension(inputVideo)}-thumbnail.png";

            //throw new Exception("Exctracting thumbnail fails");
        }

        [FunctionName(nameof(PrependIntro))]
        public static async Task<string> PrependIntro(
        [ActivityTrigger] string inputVideo,
        ILogger log)
        {
            var introLocation = Environment.GetEnvironmentVariable("IntroLocation");

            log.LogInformation($"Preparing intro {introLocation} to {inputVideo}.");
            // simulate activity
            await Task.Delay(5000);
            return $"{Path.GetFileNameWithoutExtension(inputVideo)}-withintro.mp4";
        }

        [FunctionName(nameof(Cleanup))]
        public static async Task Cleanup(
        [ActivityTrigger] List<string> files,
        ILogger log)
        {
            log.LogInformation($"Cleanup {files.Count} files");

            foreach (var file in files)
            {
                log.LogInformation($"Cleaning up file {file} due to exception");
                // simulate deleting file
                await Task.Delay(5000);
            }
        }

        [FunctionName(nameof(SendAnEmailForApproval))]
        public static void SendAnEmailForApproval(
            [ActivityTrigger] ApprovalInfo approvalInfo,
            [Table("Approvals", "AzureWebJobsStorage")] out Approval approval,
            ILogger log)
        {
            var approvalCode = Guid.NewGuid().ToString("N");

            approval = new Approval()
            {
                RowKey = approvalCode,
                PartitionKey = "Approval",
                OrchestrationId = approvalInfo.OrchestrationId
            };

            log.LogInformation($"Creating approval - '{ JsonConvert.SerializeObject(approval)}'.");

            log.LogInformation($"Send an email for approval - {approvalInfo.VideoWithIntroLocation}");

            var host = Environment.GetEnvironmentVariable("Host");

            var functionAddress = $"{host}/api/SubmitVideoApproval/{approvalCode}";
            var approvedLink = functionAddress + "?result=Approved";
            var rejectedLink = functionAddress + "?result=Rejected";

            log.LogInformation($"Approved link - {approvedLink}");
            log.LogInformation($"Rejected link - {rejectedLink}");

        }

        [FunctionName(nameof(PublishVideo))]
        public static async Task PublishVideo(
            [ActivityTrigger] string withIntroVideo,
            ILogger log)
        {
            log.LogInformation($"Publishing video - {withIntroVideo}");
            // simulate activity
            await Task.Delay(5000);
        }


        [FunctionName(nameof(RejectVideo))]
        public static async Task RejectVideo(
            [ActivityTrigger] string withIntroVideo,
            ILogger log)
        {
            log.LogInformation($"Rejecting video  - {withIntroVideo}");
            // simulate activity
            await Task.Delay(5000);
        }
    }
}