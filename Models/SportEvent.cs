using System;
using System.Collections.Generic;
using System.Globalization;

namespace SportsMax.Models;

public enum EventStatus
{
    Unknown,
    Live,
    Soon,
    Finished
}

public class StreamLink
{
    public string Name { get; set; } = "Canal";
    public string Url { get; set; } = string.Empty;

    public override string ToString() => Name;
}

public class SportEvent
{
    // Duracion estimada de un evento deportivo tipico
    private static readonly TimeSpan EventDuration = TimeSpan.FromHours(2.5);
    // Tiempo de gracia tras finalizar antes de ocultar
    private static readonly TimeSpan HideGrace = TimeSpan.FromHours(2.5);

    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string StatusRaw { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public List<StreamLink> Links { get; set; } = new();

    /// <summary>
    /// Hora de inicio interpretada como hora LOCAL del usuario.
    /// </summary>
    public DateTime? StartLocal
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Time)) return null;
            var dateStr = string.IsNullOrWhiteSpace(Date)
                ? DateTime.Now.ToString("yyyy-MM-dd")
                : Date;

            var formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd H:mm",
                "dd/MM/yyyy HH:mm",
                "dd-MM-yyyy HH:mm"
            };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact($"{dateStr} {Time}", fmt,
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                    return dt;
            }
            if (DateTime.TryParse($"{dateStr} {Time}", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var fallback))
                return fallback;
            return null;
        }
    }

    /// <summary>
    /// Estado calculado dinamicamente desde la hora local vs ahora.
    /// Ignora el campo "status" del JSON original (suele venir desactualizado).
    /// </summary>
    public EventStatus Status
    {
        get
        {
            var start = StartLocal;
            if (start == null) return EventStatus.Unknown;
            var now = DateTime.Now;
            if (now < start.Value) return EventStatus.Soon;
            if (now < start.Value + EventDuration) return EventStatus.Live;
            return EventStatus.Finished;
        }
    }

    /// <summary>
    /// True si el evento debe ocultarse: termino hace mas de 2:30.
    /// </summary>
    public bool ShouldHide
    {
        get
        {
            var start = StartLocal;
            if (start == null) return false;
            var hideAfter = start.Value + EventDuration + HideGrace;
            return DateTime.Now > hideAfter;
        }
    }

    public string DisplayCategory =>
        string.IsNullOrWhiteSpace(Category) ? "Deportes" : Category;

    public string DisplayTime
    {
        get
        {
            var start = StartLocal;
            if (start != null) return start.Value.ToString("HH:mm");
            if (string.IsNullOrWhiteSpace(Time)) return "--:--";
            return Time.Length >= 5 ? Time.Substring(0, 5) : Time;
        }
    }

    public string FirstUrl => Links.Count > 0 ? Links[0].Url : string.Empty;
}
