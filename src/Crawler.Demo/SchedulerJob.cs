using Quartz;

namespace Async.Programming;

/// <summary>
/// The scheduler service.
/// </summary>
[DisallowConcurrentExecution]
public class SchedulerJob : IJob
{
    ///<inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        await CrawlerService.Execute(
            context.JobDetail.JobDataMap.GetString("DeviceId"),
            context.JobDetail.JobDataMap.GetString("DeviceIp"),
            context.JobDetail.JobDataMap.GetString("Ip"),
            context.JobDetail.JobDataMap.GetIntValue("Port"),
            context.JobDetail.JobDataMap.GetString("UserName"),
            context.JobDetail.JobDataMap.GetString("Password"));
    }

    /// <summary>
    /// The crawler service.
    /// </summary>
    public static CrawlerService CrawlerService { get; set; }
}