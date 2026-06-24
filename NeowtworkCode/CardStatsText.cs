using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace Neowtwork.NeowtworkCode;

internal static class CardStatsText
{
    public static string Build(CardModel card)
    {
        SaveManager.Instance.Progress.CardStats.TryGetValue(card.Id, out CardStats? stats);

        long victories = stats?.TimesWon ?? 0;
        long losses = stats?.TimesLost ?? 0;
        long picked = stats?.TimesPicked ?? 0;
        long skipped = stats?.TimesSkipped ?? 0;
        long finishedRuns = victories + losses;
        long seen = picked + skipped;
        string winRate = finishedRuns == 0 ? "— Win Rate" : $"{(double)victories / finishedRuns:P0} Win Rate";
        string pickRate = seen == 0 ? "— Pick Rate" : $"{(double)picked / seen:P0} Pick Rate";

        return
            $"{winRate}\n" +
            $"({victories} {Pluralize(victories, "victory", "victories")} - {losses} {Pluralize(losses, "loss", "losses")})\n\n" +
            $"{pickRate}\n" +
            $"({picked} {Pluralize(picked, "pick", "picks")} / {seen} seen)";
    }

    private static string Pluralize(long count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }
}
