using System.Text.RegularExpressions;
using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;

namespace DeployFlow.Infrastructure.Services;

internal static class DeploymentPublicUrlResolver
{
    public static string? ResolveProxyHost(Project project, ProxyRoutingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(project.CustomDomain))
            return NormalizeHost(project.CustomDomain);

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.BaseDomain))
            return null;

        var slug = NormalizeLabel(project.Slug);
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        return $"{slug}.{NormalizeHost(options.BaseDomain)}";
    }

    public static string? ResolvePublicUrl(Project project, ProxyRoutingOptions options, string? serverIp, int? hostPort)
    {
        var proxyHost = ResolveProxyHost(project, options);
        if (!string.IsNullOrWhiteSpace(proxyHost))
            return $"{ResolveScheme(options)}://{proxyHost}";

        return !string.IsNullOrWhiteSpace(serverIp) && hostPort.HasValue
            ? $"http://{serverIp}:{hostPort.Value}"
            : null;
    }

    public static string GetRouterName(Project project)
        => $"deployflow-{NormalizeLabel(project.Slug)}";

    public static string GetServiceName(Project project)
        => $"deployflow-{NormalizeLabel(project.Slug)}";

    private static string ResolveScheme(ProxyRoutingOptions options)
        => string.Equals(options.Scheme, "http", StringComparison.OrdinalIgnoreCase) ? "http" : "https";

    private static string NormalizeHost(string value)
        => value.Trim().Trim('.').ToLowerInvariant();

    private static string NormalizeLabel(string value)
    {
        var normalized = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9-]", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "app" : normalized;
    }
}