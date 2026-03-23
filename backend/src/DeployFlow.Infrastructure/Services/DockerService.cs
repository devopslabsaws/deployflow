using Docker.DotNet;
using Docker.DotNet.Models;
using DeployFlow.Application.Common;

namespace DeployFlow.Infrastructure.Services;

public class DockerService : IDockerService
{
    private readonly Dictionary<string, DockerClient> _clients = new();

    private DockerClient GetClient(string endpoint)
    {
        if (!_clients.TryGetValue(endpoint, out var client))
        {
            client = new DockerClientConfiguration(new Uri(endpoint))
                .CreateClient();
            _clients[endpoint] = client;
        }
        return client;
    }

    public async Task<bool> PullImageAsync(string endpoint, string image, string tag = "latest", CancellationToken ct = default)
    {
        var client = GetClient(endpoint);
        await client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = image, Tag = tag },
            null,
            new Progress<JSONMessage>(),
            ct);
        return true;
    }

    public async Task<string> RunContainerAsync(string endpoint, ContainerConfig config, CancellationToken ct = default)
    {
        var client = GetClient(endpoint);

        var portBindings = config.PortBindings.ToDictionary(
            kv => $"{kv.Key}/tcp",
            kv => (IList<PortBinding>)new List<PortBinding> { new PortBinding { HostPort = kv.Value.ToString() } });

        var envVars = config.EnvVars.Select(kv => $"{kv.Key}={kv.Value}").ToList();

        var binds = config.VolumeBindings.Select(kv => $"{kv.Key}:{kv.Value}").ToList();

        var response = await client.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = config.Image,
                Name = config.Name,
                Env = envVars,
                HostConfig = new HostConfig
                {
                    PortBindings = portBindings,
                    Binds = binds,
                    RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
                    Memory = ParseMemoryLimit(config.MemoryLimit),
                    NetworkMode = config.Network
                }
            }, ct);

        await client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), ct);
        return response.ID;
    }

    public async Task StartContainerAsync(string endpoint, string containerId, CancellationToken ct = default)
    {
        var client = GetClient(endpoint);
        await client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct);
    }

    public async Task RestartContainerAsync(string endpoint, string containerId, CancellationToken ct = default)
    {
        var client = GetClient(endpoint);
        await client.Containers.RestartContainerAsync(containerId, new ContainerRestartParameters { WaitBeforeKillSeconds = 10 }, ct);
    }

    public async Task StopContainerAsync(string endpoint, string containerId, CancellationToken ct = default)
    {
        var client = GetClient(endpoint);
        await client.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }, ct);
    }

    public async Task RemoveContainerAsync(string endpoint, string containerId, CancellationToken ct = default)
    {
        var client = GetClient(endpoint);
        await client.Containers.RemoveContainerAsync(containerId,
            new ContainerRemoveParameters { Force = true, RemoveVolumes = false }, ct);
    }

    public async Task<ContainerStats> GetContainerStatsAsync(string endpoint, string containerId, CancellationToken ct = default)
    {
        var client = GetClient(endpoint);
        ContainerStatsResponse? snapshot = null;
        await client.Containers.GetContainerStatsAsync(containerId,
            new ContainerStatsParameters { Stream = false },
            new Progress<ContainerStatsResponse>(s => snapshot = s),
            ct);

        if (snapshot is null) throw new InvalidOperationException("Could not get container stats.");

        var cpuDelta = snapshot.CPUStats.CPUUsage.TotalUsage - snapshot.PreCPUStats.CPUUsage.TotalUsage;
        var systemDelta = snapshot.CPUStats.SystemUsage - snapshot.PreCPUStats.SystemUsage;
        var cpuPercent = systemDelta > 0 ? (cpuDelta / (double)systemDelta) * snapshot.CPUStats.OnlineCPUs * 100.0 : 0;

        return new ContainerStats(
            ContainerId: containerId,
            CpuUsagePercent: cpuPercent,
            MemoryUsageBytes: (long)snapshot.MemoryStats.Usage,
            MemoryLimitBytes: (long)snapshot.MemoryStats.Limit,
            NetworkRxBytes: snapshot.Networks?.Values.Sum(n => (long)n.RxBytes) ?? 0,
            NetworkTxBytes: snapshot.Networks?.Values.Sum(n => (long)n.TxBytes) ?? 0);
    }

    public async IAsyncEnumerable<string> StreamLogsAsync(
        string endpoint, string containerId, bool follow = true,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var client = GetClient(endpoint);
#pragma warning disable CS0618 // MultiplexedStream overload is not suitable for StreamReader-based line iteration
        var stream = await client.Containers.GetContainerLogsAsync(
            containerId,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Follow = follow, Timestamps = true },
            ct);
#pragma warning restore CS0618

        using var reader = new System.IO.StreamReader(stream);
        while (!ct.IsCancellationRequested && !reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is not null) yield return line;
        }
    }

    public async Task<bool> PingServerAsync(string endpoint, CancellationToken ct = default)
    {
        try
        {
            var client = GetClient(endpoint);
            await client.System.PingAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> BuildImageAsync(
        string endpoint, string buildContextPath, string dockerfilePath, string imageTag, CancellationToken ct = default)
    {
        var client = GetClient(endpoint);
        var tarFile = CreateTar(buildContextPath);

        await using var tarStream = System.IO.File.OpenRead(tarFile);
        await client.Images.BuildImageFromDockerfileAsync(
            new ImageBuildParameters { Dockerfile = dockerfilePath, Tags = new[] { imageTag } },
            tarStream,
            null,
            null,
            new Progress<JSONMessage>(),
            ct);

        System.IO.File.Delete(tarFile);
        return imageTag;
    }

    private static long ParseMemoryLimit(string? limit)
    {
        if (string.IsNullOrEmpty(limit)) return 0;
        if (limit.EndsWith("m", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(limit[..^1], out var mb))
            return mb * 1024L * 1024L;
        if (limit.EndsWith("g", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(limit[..^1], out var gb))
            return gb * 1024L * 1024L * 1024L;
        return 0;
    }

    private static string CreateTar(string directory)
    {
        var tarPath = System.IO.Path.GetTempFileName() + ".tar";
        // Simple TAR creation using system tar; in production use SharpZipLib or ICSharpCode.SharpZipLib
        var psi = new System.Diagnostics.ProcessStartInfo("tar", $"-cf \"{tarPath}\" -C \"{directory}\" .")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        proc.WaitForExit();
        return tarPath;
    }
}
