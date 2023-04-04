// See https://aka.ms/new-console-template for more information

using Async.Programming;

Console.WriteLine("Hello, World!");

/*
entities:
08da9248-bb5d-4132-84b2-c9fadb24e266	com-1	device-1	192.168.10.100	192.168.1.102	22	admin	admin	600	desc	1	2022-12-23 21:05:41.385950	2023-04-04 13:36:07.999321
f35ea202-97c0-4e52-b25b-4a6ed92793a9	com-2	device-2	192.168.10.100	192.168.1.11	22	admin	admin	600	desc	1	2022-12-24 14:49:12.973931	2023-04-04 13:36:09.244772
 */

List<string> deviceList = new()
{
    "08da9248-bb5d-4132-84b2-c9fadb24e266\tcom-1\tdevice-1\t192.168.10.100\t192.168.1.102\t22\tadmin\tadmin\t600\tdesc\t1\t2022-12-23 21:05:41.385950\t2023-04-04 13:36:07.999321",
    "f35ea202-97c0-4e52-b25b-4a6ed92793a9\tcom-2\tdevice-2\t192.168.10.100\t192.168.1.11\t22\tadmin\tadmin\t600\tdesc\t1\t2022-12-24 14:49:12.973931\t2023-04-04 13:36:09.244772"
};

List<DeviceInfoEntity> deviceEntities = deviceList.Select(p => new DeviceInfoEntity
{
    Di_Id = p.Split('\t')[0],
    Di_LabName = p.Split('\t')[1],
    Di_Name = p.Split('\t')[2],
    Di_DeviceIp = p.Split('\t')[3],
    Di_HostIp = p.Split('\t')[4],
    Di_HostPort = int.Parse(p.Split('\t')[5]),
    Di_HostUserName = p.Split('\t')[6],
    Di_HostPassword = p.Split('\t')[7],
    Di_ScanInterval = int.Parse(p.Split('\t')[8]),
    Di_Desc = p.Split('\t')[9],
    Di_Status = Enum.Parse<DeviceStatus>(p.Split('\t')[10]),
    Di_CreateDate = DateTime.Parse(p.Split('\t')[11]),
    Di_LastRunTime = DateTime.Parse(p.Split('\t')[12])
}).ToList();

Console.WriteLine($"create {deviceEntities.Count} devices.");