using System;
using System.Collections.Generic;

namespace CursorHunter.Progression
{
    public static class ProgressionMigration
    {
        public static string NodeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            if (id.StartsWith("stat.multiClick.", StringComparison.Ordinal))
                return id.Replace("stat.multiClick.", "stat.cooldown.");
            switch (id)
            {
                case "pet.cursor.01": return "skill.cursorAura";
                case "pet.cursor.02": case "pet.cursor.05": return "skill.cursorAura.radius";
                case "pet.cursor.03": return "skill.cursorAura.damage";
                case "pet.cursor.04": return "skill.cursorAura.cooldown";
            }
            if (IsRetiredNode(id)) return string.Empty;
            return id;
        }


        // Legacy identifiers are recognized only to exclude retired purchases.
        public static bool IsRetiredNode(string id)
        {
            return !string.IsNullOrEmpty(id) &&
                (id.StartsWith("relic.", StringComparison.Ordinal) ||
                 id.StartsWith("loot.", StringComparison.Ordinal));
        }

        public static string CurrencyId(string id)
        {
            return string.IsNullOrEmpty(id) ? "gem.garnet" : id;
        }
    }
}
