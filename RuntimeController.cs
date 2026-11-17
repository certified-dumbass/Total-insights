using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryRuntime;

[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("Dreamstreaming/LibraryRuntime")]
public sealed class RuntimeController(ILibraryManager libraryManager) : ControllerBase
{
    private const string DefaultTemplate = "**Dreamstreaming • Bibliotheek**\n{hours} uur aan video ({days} dagen)\nGrootste bibliotheek: {topLibrary}\nGrootste serie: {topSeries}";
    private static readonly HttpClient Discord = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    [HttpGet("Report")]
    public ActionResult GetReport(CancellationToken cancellationToken) => Ok(BuildReport(cancellationToken));

    [HttpGet("Settings")]
    public ActionResult GetSettings()
    {
        var configuration = GetPlugin().Configuration;
        return Ok(new
        {
            template = Template(configuration.Template),
            hasWebhook = !string.IsNullOrWhiteSpace(configuration.Webhook)
        });
    }

    [HttpPost("Settings")]
    public ActionResult SaveSettings(Settings input)
    {
        if (input.Template?.Length > 2000)
        {
            return BadRequest("Het sjabloon mag maximaal 2.000 tekens bevatten.");
        }

        if (!string.IsNullOrWhiteSpace(input.Webhook) && !IsDiscordWebhook(input.Webhook.Trim()))
        {
            return BadRequest("Gebruik een geldige HTTPS-webhook van Discord.");
        }

        var plugin = GetPlugin();
        var configuration = plugin.Configuration;
        configuration.Template = input.Template?.Trim() ?? "";
        if (input.ClearWebhook)
        {
            configuration.Webhook = "";
        }
        else if (!string.IsNullOrWhiteSpace(input.Webhook))
        {
            configuration.Webhook = input.Webhook.Trim();
        }

        plugin.UpdateConfiguration(configuration);
        return NoContent();
    }

    [HttpPost("Preview")]
    public ActionResult Preview(Settings input, CancellationToken cancellationToken)
    {
        if (input.Template?.Length > 2000)
        {
            return BadRequest("Het sjabloon mag maximaal 2.000 tekens bevatten.");
        }

        return Ok(new { message = Render(Template(input.Template), BuildReport(cancellationToken)) });
    }

    [HttpPost("Send")]
    public async Task<ActionResult> Send(CancellationToken cancellationToken)
    {
        var configuration = GetPlugin().Configuration;
        var webhook = configuration.Webhook;
        if (!IsDiscordWebhook(webhook))
        {
            return BadRequest("Sla eerst een geldige Discord-webhook op.");
        }

        var message = Render(Template(configuration.Template), BuildReport(cancellationToken));
        if (message.Length > 2000)
        {
            return BadRequest("Het uiteindelijke bericht is langer dan Discord toestaat (2.000 tekens).");
        }

        try
        {
            using var response = await Discord.PostAsJsonAsync(webhook, new
            {
                content = message,
                allowed_mentions = new { parse = Array.Empty<string>() }
            }, cancellationToken);
            return response.IsSuccessStatusCode
                ? NoContent()
                : StatusCode(502, $"Discord gaf HTTP {(int)response.StatusCode} terug.");
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, "Discord was niet bereikbaar.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, "Discord reageerde niet op tijd.");
        }
    }

    private Report BuildReport(CancellationToken cancellationToken)
    {
        var allItems = new HashSet<Guid>();
        var libraries = new List<Ranking>();
        var series = new Dictionary<Guid, Ranking>();
        long totalTicks = 0;
        int missingRuntime = 0;

        foreach (var folder in libraryManager.GetVirtualFolders())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(folder.ItemId, out var folderId) || folderId == Guid.Empty)
            {
                continue;
            }

            long libraryTicks = 0;
            var libraryItems = new HashSet<Guid>();
            var query = new InternalItemsQuery
            {
                ParentId = folderId,
                Recursive = true,
                IsVirtualItem = false,
                IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Episode, BaseItemKind.Video, BaseItemKind.MusicVideo]
            };

            foreach (var item in libraryManager.GetItemList(query))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.IsVirtualItem || !libraryItems.Add(item.Id))
                {
                    continue;
                }

                bool isNew = allItems.Add(item.Id);
                if (item.RunTimeTicks is not > 0)
                {
                    if (isNew)
                    {
                        missingRuntime++;
                    }
                    continue;
                }

                var ticks = item.RunTimeTicks.Value;
                libraryTicks += ticks;
                if (!isNew)
                {
                    continue;
                }

                totalTicks += ticks;
                if (item is Episode episode && episode.SeriesId != Guid.Empty)
                {
                    if (series.TryGetValue(episode.SeriesId, out var previous))
                    {
                        series[episode.SeriesId] = previous with { Ticks = previous.Ticks + ticks };
                    }
                    else
                    {
                        series[episode.SeriesId] = new Ranking(episode.SeriesName ?? "Onbekende serie", ticks);
                    }
                }
            }

            libraries.Add(new Ranking(folder.Name, libraryTicks));
        }

        return new Report(totalTicks, allItems.Count, missingRuntime, Rank(libraries), Rank(series.Values).Take(25).ToArray());
    }

    private static Plugin GetPlugin() =>
        Plugin.Instance ?? throw new InvalidOperationException("Library Runtime is not initialized.");

    private static Ranking[] Rank(IEnumerable<Ranking> rows) =>
        rows.OrderByDescending(row => row.Ticks).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    private static bool IsDiscordWebhook(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort
        && uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
        && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 && uri.Query.Length == 0
        && Regex.IsMatch(uri.AbsolutePath, @"^/api/webhooks/[0-9]+/[A-Za-z0-9_-]+$");

    private static string Template(string? value) => string.IsNullOrWhiteSpace(value) ? DefaultTemplate : value;
    private static string Number(double value) => value.ToString("N2", CultureInfo.GetCultureInfo("nl-NL"));

    private static string Render(string template, Report report)
    {
        var time = TimeSpan.FromTicks(report.Ticks);
        var fields = new Dictionary<string, string>
        {
            ["hours"] = Number(time.TotalHours),
            ["days"] = Number(time.TotalDays),
            ["weeks"] = Number(time.TotalDays / 7),
            ["months"] = Number(time.TotalDays / (365.2425 / 12)),
            ["years"] = Number(time.TotalDays / 365.2425),
            ["items"] = report.ItemCount.ToString(CultureInfo.InvariantCulture),
            ["topLibrary"] = FormatTop(report.Libraries.FirstOrDefault()),
            ["topSeries"] = FormatTop(report.Series.FirstOrDefault())
        };
        return Regex.Replace(template, @"\{\w+\}", match => fields.GetValueOrDefault(match.Value[1..^1], match.Value));
    }

    private static string FormatTop(Ranking? row) =>
        row is null || row.Ticks == 0 ? "—" : $"{row.Name} ({Number(TimeSpan.FromTicks(row.Ticks).TotalHours)} uur)";

    public sealed record Settings(string? Template, string? Webhook, bool ClearWebhook);
    public sealed record Ranking(string Name, long Ticks);
    public sealed record Report(long Ticks, int ItemCount, int MissingRuntime, Ranking[] Libraries, Ranking[] Series);
}
