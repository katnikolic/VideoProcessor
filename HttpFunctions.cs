using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace VideoProcessor;

public static class HttpFunctions
{
    [FunctionName(nameof(ProcesVideoStarter))]
    public static async Task<IActionResult> ProcesVideoStarter(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req,
    [DurableClient] IDurableOrchestrationClient starter,
    ILogger log)
    {      
        if(!req.GetQueryParameterDictionary().TryGetValue("video", out string video))
            return new BadRequestObjectResult("Please pass the video location the query string");

        string instanceId = await starter.StartNewAsync("ProcessVideoOrchestrator", null, video);

        log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

        return starter.CreateCheckStatusResponse(req, instanceId);
    }

    [FunctionName(nameof(SubmitVideoApproval))]
    public static async Task<IActionResult> SubmitVideoApproval(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "SubmitVideoApproval/{approvalCode}")] HttpRequest req,
    [Table("Approvals", "Approval", "{approvalCode}", Connection = "AzureWebJobsStorage")] Approval approval,
    [DurableClient] IDurableOrchestrationClient client,
    ILogger log)
    {
        if (!req.GetQueryParameterDictionary().TryGetValue("result", out string result))
            return new BadRequestObjectResult("Please pass the approval result");

        log.LogInformation($"Submitting video for approval - '{approval.OrchestrationId}'.");

        await client.RaiseEventAsync(approval.OrchestrationId, "ApprovalResult", result);

        return new OkResult();
    }
}

