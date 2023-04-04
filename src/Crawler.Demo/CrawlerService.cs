using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Renci.SshNet;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Async.Programming;

/// <summary>
/// Represents the crawler to collect the data of spectro daily in laboratory device.
/// </summary>
public class CrawlerService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrawlerService"/> class.
    /// </summary>
    public CrawlerService(ILogger logger)
    {
        // Get logger
        _logger = logger;

        // Task mapping
        _taskAndLastRunTimeMap = new ConcurrentDictionary<string, DateTime>();
        _taskAndCancellationMap = new ConcurrentDictionary<string, CancellationTokenSource>();

        // Set the CrawlerService to SchedulerJob
        SchedulerJob.CrawlerService = this;

        // Create Scheduler
        StdSchedulerFactory factory = new();
        _scheduler = factory.GetScheduler().GetAwaiter().GetResult();
        _scheduler.Start().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Start a scheduler for devices.
    /// </summary>
    /// <returns></returns>
    public async Task StartAsync()
    {
    }

    /// <summary>
    /// Create a task.
    /// </summary>
    private async Task CreateTaskAsync(DeviceInfoEntity device)
    {
        IJobDetail jobDetail = JobBuilder.Create<SchedulerJob>()
            .UsingJobData(new JobDataMap
            {
                new("DeviceId", device.Di_Id),
                new("DeviceIp", device.Di_DeviceIp),
                new("Ip", device.Di_HostIp),
                new("Port", device.Di_HostPort),
                new("UserName", device.Di_HostUserName),
                new("Password", device.Di_HostPassword)
            })
            .WithIdentity(device.Di_Id)
            .Build();

        ITrigger trigger = TriggerBuilder.Create()
            .WithSimpleSchedule(m =>
            {
                m.WithIntervalInSeconds(device.Di_ScanInterval).RepeatForever();
            })
            .Build();

        // Add job and trigger into the scheduler with cancellation token source
        CancellationTokenSource cancellationTokenSource = new();

        // log last run time
        if (!_taskAndLastRunTimeMap.ContainsKey(device.Di_Id))
            _taskAndLastRunTimeMap.TryAdd(device.Di_Id, TimeHelper.UtcToLocalTime(DateTime.UtcNow));
        else
            _taskAndLastRunTimeMap[device.Di_Id] = TimeHelper.UtcToLocalTime(DateTime.UtcNow);

        // set task and cancellationTokenSource
        if (!_taskAndCancellationMap.ContainsKey(device.Di_Id))
            _taskAndCancellationMap.TryAdd(device.Di_Id, cancellationTokenSource);
        else
            _taskAndCancellationMap[device.Di_Id] = cancellationTokenSource;

        // Set Schedule Job
        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationTokenSource.Token);

        // Watch task
        await WatchTaskAsync(device, cancellationTokenSource);
        _logger.LogInformation("[DeviceId]{deviceId} the task is created.", device.Di_Id);
    }

    /// <summary>
    /// Watch and cancel the task if something wrong.
    /// </summary>
    private Task WatchTaskAsync(DeviceInfoEntity device, CancellationTokenSource cancellationTokenSource)
    {
        int timeOut = device.Di_ScanInterval / 60 + 10;
        Task.Run(async () =>
        {
            bool isRestartSelf = false;
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(1000);
                if ((TimeHelper.UtcToLocalTime(DateTime.UtcNow) - _taskAndLastRunTimeMap[device.Di_Id]).Minutes <= timeOut)
                    continue;

                isRestartSelf = true;
                cancellationTokenSource.Cancel();
            }

            _logger.LogInformation("[DeviceId]{deviceId} 'IsCancellationRequested': {status}", device.Di_Id, cancellationTokenSource.IsCancellationRequested);

            // Try delete current task first if still running
            if (!await _scheduler.DeleteJob(new JobKey(device.Di_Id)))
                await _scheduler.DeleteJob(new JobKey(device.Di_Id));

            // Restart
            if (isRestartSelf)
            {
                await CreateTaskAsync(device);
                _logger.LogInformation("[DeviceId]{deviceId} the task job has been restarted by 'CancellationTokenSource', because it has something wrongs.", device.Di_Id);
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Add a new device task into the scheduler.
    /// </summary>
    public async Task AddScheduleTask(DeviceInfoEntity device)
    {
        await CreateTaskAsync(device);
    }

    /// <summary>
    /// Stop a device task from the scheduler.
    /// </summary>
    public Task StopScheduleTask(string deviceId)
    {
        if (!_taskAndCancellationMap.ContainsKey(deviceId))
            return Task.CompletedTask;

        // cancel
        _taskAndCancellationMap[deviceId].Cancel();
        _logger.LogInformation("[DeviceId]{deviceId} the task job has been stopped by API.", deviceId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Restart the device task.
    /// </summary>
    public async Task RestartScheduleTask(DeviceInfoEntity device)
    {
        if (!_taskAndCancellationMap.ContainsKey(device.Di_Id))
        {
            await CreateTaskAsync(device);
            return;
        }

        // cancel
        _taskAndCancellationMap[device.Di_Id].Cancel();
        JobKey jobKey = new(device.Di_Id);
        while (await _scheduler.CheckExists(jobKey))
            await Task.Delay(500);

        // restart
        await CreateTaskAsync(device);
        _logger.LogInformation("[DeviceId]{deviceId} the task job has been restarted by API.", device.Di_Id);
    }

    /// <summary>
    /// Execute the scheduler task for device.
    /// </summary>
    public async Task Execute(string deviceId, string deviceIp, string ip, int port, string userName, string passWord)
    {
        _logger.LogInformation("[DeviceId]{deviceId} the task job is starting...", deviceId);
        SshClient sshClient = null;

        try
        {
            // connect to ssh host
            sshClient = new SshClient(ip, port, userName, passWord);
            sshClient.Connect();
            _logger.LogInformation("[DeviceId]{deviceId} Connected to {ip}:{port}", deviceId, ip, port);

            // fetch data
            string wGet = string.Format(DAILY_URL, deviceIp);
            List<SpectroDailyEntity> spectroDailyList = await FetchDailyAsync(sshClient, deviceId, deviceIp, wGet);
            _logger.LogInformation("[DeviceId]{deviceId} fetch dailies data count: {count}", deviceId, spectroDailyList.Count);

            // save data
            if (spectroDailyList is { Count: > 0 })
            {
                foreach (SpectroDailyEntity spectroDaily in spectroDailyList)
                {
                    List<SpectroDetailEntity> spectroDetailList = await FetchDetailsAsync(spectroDaily, sshClient, deviceId, deviceIp);
                    _logger.LogInformation("[DeviceId]{deviceId} [DailyId]{dailyId} fetch details data count: {count}", deviceId, spectroDaily.Sd_Id, spectroDetailList.Count);

                    // save details
                    if (spectroDetailList is { Count: > 0 })
                        _logger.LogInformation("[DeviceId]{deviceId} [DailyId]{dailyId} saved details data", deviceId, spectroDaily.Sd_Id);
                }
            }
            _logger.LogInformation("[DeviceId]{deviceId} the task job has been done!", deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError("[DeviceId]{deviceId} [HostIp]{hostIp} [DeviceIp]{deviceIp} has exception: {ex}", deviceId, ip, deviceIp, $"{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // Dispose ssh
            if (sshClient != null)
            {
                sshClient.Disconnect();
                sshClient.Dispose();
            }
        }
    }

    private async Task<List<SpectroDailyEntity>> FetchDailyAsync(SshClient sshClient, string deviceId, string deviceIp, string wGet)
    {
        List<SpectroDailyEntity> spectroDailyList = new();

        // wget url
        SshCommand cmd = null;
        await Task.Run(() => { cmd = sshClient.RunCommand(wGet); }).WaitAsync(TimeSpan.FromMinutes(1.5));
        if (cmd == null)
        {
            _logger.LogError("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, error: {error}", deviceId, deviceIp, wGet, "In Task.Run.Thread: the operation timed out within 1.5 minutes.");
            return spectroDailyList;
        }
        if (cmd.ExitStatus != 0 || string.IsNullOrWhiteSpace(cmd.Result))
        {
            _logger.LogError("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, error: {error}", deviceId, deviceIp, wGet, cmd.Error);
        }
        else
        {
            // parse html
            _logger.LogInformation("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, successfully!", deviceId, deviceIp, wGet);
            foreach (Match tr in Regex.Matches(cmd.Result, TR_PATTERN).Cast<Match>())
            {
                if (!tr.Value.Contains("href")) continue;
                if (!tr.Success) continue;
                int columnIndex = 1;
                SpectroDailyEntity spectroDaily = new() { Sd_DeviceId = deviceId };
                foreach (Match td in Regex.Matches(tr.Value, TD_PATTERN).Cast<Match>())
                {
                    switch (columnIndex)
                    {
                        case 1:
                            if (td.Value.Contains("href"))
                                spectroDaily.Sd_Url = Regex.Match(td.Value, HREF_PATTERN).Groups[1].Value.Trim().Replace("./", "/");
                            break;
                        case 2:
                            string dt = td.Groups[1].Value.Trim(); // 22/08/03 15:09
                            if (dt.IndexOf('/') == 2) dt = $"20{dt}";
                            spectroDaily.Sd_BizDateTime = DateTime.Parse(dt);
                            break;
                        case 3:
                            spectroDaily.Sd_Mode = td.Groups[1].Value.Trim();
                            break;
                        case 4:
                            spectroDaily.Sd_Item = td.Groups[1].Value.Trim();
                            break;
                    }

                    columnIndex++;
                }

                spectroDaily.Sd_CreateDate = TimeHelper.UtcToLocalTime(DateTime.UtcNow);
                spectroDailyList.Add(spectroDaily);
            }
        }

        // dispose
        cmd.Dispose();

        return spectroDailyList;
    }

    private async Task<List<SpectroDetailEntity>> FetchDetailsAsync(SpectroDailyEntity spectroDaily, SshClient sshClient, string deviceId, string deviceIp)
    {
        List<SpectroDetailEntity> spectroDetailList = new();

        // wget url
        string wGet = string.Format(DETAIL_URL, deviceIp, spectroDaily.Sd_Url);
        SshCommand cmd = null;
        await Task.Run(() => { cmd = sshClient.RunCommand(wGet); }).WaitAsync(TimeSpan.FromMinutes(1.5));
        if (cmd == null)
        {
            _logger.LogError("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, error: {error}", deviceId, deviceIp, wGet, "In Task.Run.Thread: the operation timed out within 1.5 minutes.");
            return spectroDetailList;
        }
        if (cmd.ExitStatus != 0 || string.IsNullOrWhiteSpace(cmd.Result))
        {
            _logger.LogError("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, error: {error}", deviceId, deviceIp, wGet, cmd.Error);
        }
        else
        {
            // parse html
            _logger.LogInformation("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, successfully!", deviceId, deviceIp, wGet);
            foreach (Match tr in Regex.Matches(cmd.Result, TR_PATTERN).Cast<Match>())
            {
                string head = tr.Value.ToLower();
                if (head.Contains("no.") || head.Contains("kind")) continue;
                if (!tr.Success) continue;
                int columnIndex = 1;
                SpectroDetailEntity spectroDetail = new SpectroDetailEntity { Sd_DailyId = spectroDaily.Sd_Id, Sd_CreateDate = TimeHelper.UtcToLocalTime(DateTime.UtcNow) };
                foreach (Match td in Regex.Matches(tr.Value, TD_PATTERN).Cast<Match>())
                {
                    switch (columnIndex)
                    {
                        case 1:
                            spectroDetail.Sd_SeqNo = int.Parse(td.Groups[1].Value.Trim());
                            break;
                        case 2:
                            spectroDetail.Sd_Kind = td.Groups[1].Value.Trim();
                            break;
                        case 3:
                            spectroDetail.Sd_IDString = td.Groups[1].Value.Trim().Replace("&nbsp;", "");
                            break;
                        case 4:
                            spectroDetail.Sd_Percent = td.Groups[1].Value.Trim();
                            break;
                    }

                    columnIndex++;
                }

                spectroDetailList.Add(spectroDetail);
            }
        }

        // dispose
        cmd.Dispose();

        return spectroDetailList;
    }

    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly ConcurrentDictionary<string, DateTime> _taskAndLastRunTimeMap;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskAndCancellationMap;

    private const string TR_PATTERN = @"<tr[^>]*>(.*?)<\/tr>";
    private const string TD_PATTERN = @"<td[^>]*>(.*?)<\/td>";
    private const string HREF_PATTERN = "<a.+?href=\"(.+?)\".*>(.+)</a>";

    private const string DAILY_URL = "wget -q -O - http://{0}/cgi/list.cgi?lang=1";
    private const string DETAIL_URL = "wget -q -O - http://{0}/cgi{1}";
}