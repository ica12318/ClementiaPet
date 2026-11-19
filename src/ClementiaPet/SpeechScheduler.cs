using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace ClementiaPet
{
    internal class SpeechClock
    {
        private readonly Stopwatch watch = Stopwatch.StartNew();
        internal virtual DateTimeOffset Now { get { return DateTimeOffset.Now; } }
        internal virtual double Seconds { get { return watch.Elapsed.TotalSeconds; } }
    }

    internal static class SpeechTiming
    {
        internal const int QuietStart = 23, QuietEnd = 8, EveningStart = 18;
        internal const int IdleMin = 25, IdleMax = 45, Cooldown = 300, ResumeDelay = 60;
        internal const int InvitationLifetime = 600, SnoozeMinutes = 10, DefaultInterval = 60, BubbleSeconds = 6;
        internal static readonly int[] Intervals = {30, 60, 90, 120};
    }

    internal sealed class SpeechScheduler
    {
        private readonly SpeechClock clock;
        private readonly Random random;
        private readonly PetSettings settings;
        private readonly Dialogue dialogue;
        private readonly Dictionary<string,List<SpeechLine>> decks = new Dictionary<string,List<SpeechLine>>();
        private readonly HashSet<string> pools = new HashSet<string>();
        private readonly Dictionary<string,string> last = new Dictionary<string,string>();
        private double idleDue, waterDue, allowAfter, pausedAt, invitationUntil;
        private bool paused;
        private SpeechLine invitation;

        internal SpeechScheduler(SpeechClock clock, Random random, PetSettings settings, Dialogue dialogue)
        {
            this.clock = clock;
            this.random = random;
            this.settings = settings;
            this.dialogue = dialogue;
            foreach (SpeechLine line in dialogue.Lines)
                foreach (string form in line.forms)
                    pools.Add(line.category + "/" + form);
            ResetIdle();
            if (settings.WaterEnabled)
            {
                if (settings.WaterDueUtc > 0)
                    waterDue = clock.Seconds + (settings.WaterDueUtc - clock.Now.UtcTicks) / (double)TimeSpan.TicksPerSecond;
                else SetWaterDue(settings.WaterMinutes);
            }
            double since = (clock.Now.UtcTicks - settings.LastSpeechUtc) / (double)TimeSpan.TicksPerSecond;
            allowAfter = clock.Seconds + Math.Max(0, SpeechTiming.Cooldown - Math.Max(0, since));
        }

        internal SpeechLine Invitation { get { return clock.Seconds < invitationUntil ? invitation : null; } }
        internal int WaterRevision { get; private set; }

        private void ResetIdle()
        {
            idleDue = clock.Seconds + random.Next(SpeechTiming.IdleMin * 60, SpeechTiming.IdleMax * 60 + 1);
        }

        internal void SetIdle(bool enabled)
        {
            settings.IdleEnabled = enabled;
            ResetIdle();
            if (paused) pausedAt = clock.Seconds;
        }

        internal void Pause(bool value)
        {
            if (value == paused) return;
            paused = value;
            if (value) pausedAt = clock.Seconds;
            else
            {
                idleDue += clock.Seconds - pausedAt;
                allowAfter = Math.Max(allowAfter, clock.Seconds + SpeechTiming.ResumeDelay);
            }
        }

        private void SetWaterDue(int minutes)
        {
            waterDue = clock.Seconds + minutes * 60;
            settings.WaterDueUtc = clock.Now.UtcDateTime.AddMinutes(minutes).Ticks;
        }

        internal void ConfigureWater(bool enabled, int minutes)
        {
            WaterRevision++;
            settings.WaterEnabled = enabled;
            settings.WaterMinutes = minutes;
            if (enabled) SetWaterDue(minutes);
            else settings.WaterDueUtc = 0;
            if (invitation != null && invitation.category == "water") invitation = null;
        }

        internal bool ReplyWater(string action, int revision)
        {
            if (!settings.WaterEnabled || revision != WaterRevision) return false;
            if (action == "dismiss")
            {
                WaterRevision++;
                invitation = null;
                return true;
            }
            ConfigureWater(action != "off", settings.WaterMinutes);
            if (action == "snooze") SetWaterDue(SpeechTiming.SnoozeMinutes);
            return true;
        }

        internal bool Tick(string form, bool available, Func<string,bool> show)
        {
            if (paused) return false;
            DateTimeOffset now = clock.Now;
            string date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (now.Hour < SpeechTiming.QuietEnd || now.Hour >= SpeechTiming.QuietStart)
            {
                if (clock.Seconds >= idleDue) ResetIdle();
                return false;
            }
            if (!available || clock.Seconds < allowAfter) return false;
            string category = null;
            if (settings.WaterEnabled && clock.Seconds >= waterDue && pools.Contains("water/" + form)) category = "water";
            else if (settings.EveningEnabled && now.Hour >= SpeechTiming.EveningStart &&
                settings.EveningDate != date && pools.Contains("evening/" + form))
                category = "evening";
            else if (settings.IdleEnabled && clock.Seconds >= idleDue && pools.Contains("idle/" + form)) category = "idle";
            if (category == null) return false;

            List<SpeechLine> deck;
            if (!decks.TryGetValue(category, out deck) || !deck.Any(l => l.forms.Contains(form)))
            {
                deck = dialogue.Lines.Where(l => l.category == category).ToList();
                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    SpeechLine swap = deck[i]; deck[i] = deck[j]; deck[j] = swap;
                }
                decks[category] = deck;
            }
            string previous;
            last.TryGetValue(category, out previous);
            SpeechLine line = deck.FirstOrDefault(l => l.forms.Contains(form) && l.id != previous)
                ?? deck.First(l => l.forms.Contains(form));
            if (!show(line.text)) return false;
            deck.Remove(line);
            last[category] = line.id;
            if (category == "water") { SetWaterDue(settings.WaterMinutes); WaterRevision++; }
            if (category == "evening") settings.EveningDate = date;
            if (clock.Seconds >= idleDue) ResetIdle();
            settings.LastSpeechUtc = now.UtcTicks;
            allowAfter = clock.Seconds + SpeechTiming.Cooldown;
            invitation = string.IsNullOrEmpty(line.next) ? null : line;
            invitationUntil = clock.Seconds + SpeechTiming.InvitationLifetime;
            return true;
        }
    }
}
