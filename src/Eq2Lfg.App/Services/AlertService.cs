using System.IO;
using System.Media;
using Eq2Lfg.Core.Config;
using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Eq2Lfg.App.Services;

/// <summary>Raises Windows toasts and a chime according to the user's alert settings.</summary>
public sealed class AlertService(AppSettings settings)
{
    private static readonly Lazy<Uri?> LogoUri = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        return File.Exists(path) ? new Uri(path) : null;
    });

    private readonly Lazy<byte[]> chimeWav = new(BuildChimeWav);

    public void AlertMatches(LfgPost post, IReadOnlyList<MatchResult> matches)
    {
        var first = matches[0];
        var title = matches.Count == 1
            ? $"{first.Character.Display} matches a group"
            : $"{matches.Count} of your characters match a group";
        var body = $"{post.Advertiser}: \"{post.Message.Text}\" — {string.Join(", ", first.Reasons)}";
        Alert(title, body);
    }

    public void AlertOpportunity(GroupOpportunity opportunity)
    {
        var range = opportunity.MinLevel is null
            ? ""
            : $" around {opportunity.MinLevel}-{opportunity.MaxLevel}";
        var title = $"Potential group: {opportunity.Posts.Count} players LFG{range}";
        var body = string.Join(", ", opportunity.Posts.Select(p =>
            p.Classes.Count > 0
                ? $"{p.Advertiser} ({string.Join("/", p.Classes)})"
                : p.Advertiser));
        Alert(title, body);
    }

    private void Alert(string title, string body)
    {
        if (settings.ToastAlerts)
        {
            try
            {
                var builder = new ToastContentBuilder()
                    .AddText(title)
                    .AddText(body);
                if (LogoUri.Value is { } logo)
                {
                    builder.AddAppLogoOverride(logo, ToastGenericAppLogoCrop.Circle);
                }

                builder.Show();
            }
            catch (Exception)
            {
                // Toasts can fail if notifications are disabled system-wide; never crash on alerting.
            }
        }

        if (settings.SoundAlerts)
        {
            try
            {
                using var stream = new MemoryStream(chimeWav.Value);
                using var player = new SoundPlayer(stream);
                player.Play();
            }
            catch (Exception)
            {
                SystemSounds.Asterisk.Play();
            }
        }
    }

    /// <summary>A short two-note chime (A5 → E6) generated in memory; no bundled media files.</summary>
    private static byte[] BuildChimeWav()
    {
        const int sampleRate = 44100;
        var notes = new[] { (Freq: 880.0, Ms: 140), (Freq: 1318.5, Ms: 200) };
        var samples = new List<short>();

        foreach (var (freq, ms) in notes)
        {
            var count = sampleRate * ms / 1000;
            for (var i = 0; i < count; i++)
            {
                // Quick attack, exponential decay envelope so it sounds like a soft bell.
                var t = (double)i / count;
                var envelope = Math.Min(1.0, t * 20) * Math.Exp(-3.5 * t);
                var value = Math.Sin(2 * Math.PI * freq * i / sampleRate) * envelope;
                samples.Add((short)(value * short.MaxValue * 0.35));
            }
        }

        var dataLength = samples.Count * 2;
        using var ms2 = new MemoryStream();
        using var w = new BinaryWriter(ms2);
        w.Write("RIFF"u8);
        w.Write(36 + dataLength);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(sampleRate);
        w.Write(sampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(dataLength);
        foreach (var s in samples)
        {
            w.Write(s);
        }

        w.Flush();
        return ms2.ToArray();
    }
}
