using System.Diagnostics;
using System.Text.RegularExpressions;
using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;

namespace DeployFlow.Infrastructure.Services;

public class S3DestinationValidationService : IS3DestinationValidationService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public S3DestinationValidationService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<S3DestinationTestResult> TestConnectionAsync(S3DestinationTestRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return new S3DestinationTestResult(false, "Destination name is required.", 0, "");

        if (string.IsNullOrWhiteSpace(request.AccessKey) || string.IsNullOrWhiteSpace(request.SecretKey))
            return new S3DestinationTestResult(false, "Access key and secret key are required.", 0, "");

        if (!IsValidBucketName(request.Bucket))
            return new S3DestinationTestResult(false, "Bucket name is invalid for S3-compatible storage.", 0, "");

        var resolvedEndpoint = ResolveEndpoint(request);
        if (!Uri.TryCreate(resolvedEndpoint, UriKind.Absolute, out var endpointUri))
            return new S3DestinationTestResult(false, "Endpoint URL is invalid.", 0, resolvedEndpoint);

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(6);

            var probeUri = new Uri(endpointUri, "/");
            var sw = Stopwatch.StartNew();
            using var response = await client.GetAsync(probeUri, ct);
            sw.Stop();

            if ((int)response.StatusCode >= 500)
            {
                return new S3DestinationTestResult(
                    false,
                    "Endpoint responded but appears unavailable (5xx).",
                    sw.ElapsedMilliseconds,
                    resolvedEndpoint);
            }

            return new S3DestinationTestResult(
                true,
                "Connection successful. Endpoint reachable and destination format looks valid.",
                sw.ElapsedMilliseconds,
                resolvedEndpoint);
        }
        catch (TaskCanceledException)
        {
            return new S3DestinationTestResult(false, "Connection timed out while probing endpoint.", 0, resolvedEndpoint);
        }
        catch (Exception ex)
        {
            return new S3DestinationTestResult(false, $"Connection failed: {ex.Message}", 0, resolvedEndpoint);
        }
    }

    private static string ResolveEndpoint(S3DestinationTestRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Endpoint))
            return request.Endpoint!;

        var scheme = request.UseSsl ? "https" : "http";
        var provider = request.Provider?.Trim().ToLowerInvariant() ?? "s3";
        var region = string.IsNullOrWhiteSpace(request.Region) ? "us-east-1" : request.Region.Trim();

        return provider switch
        {
            "aws" => $"{scheme}://s3.{region}.amazonaws.com",
            "cloudflare-r2" => $"{scheme}://{request.AccessKey}.r2.cloudflarestorage.com",
            _ => $"{scheme}://s3.{region}.amazonaws.com"
        };
    }

    private static bool IsValidBucketName(string bucket)
    {
        if (string.IsNullOrWhiteSpace(bucket)) return false;
        if (bucket.Length < 3 || bucket.Length > 63) return false;
        if (bucket.Contains("..")) return false;

        return Regex.IsMatch(bucket, "^[a-z0-9][a-z0-9.-]*[a-z0-9]$");
    }
}
