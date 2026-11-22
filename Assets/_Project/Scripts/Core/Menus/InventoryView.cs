using System;
using System.Collections.Generic;
using System.Globalization;
using Hollowcrawl.Simulation;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Menus
{
    public struct InventoryRow
    {
        public string Name;
        public string Detail;
        public string IconKey;
    }

    public sealed class InventorySection
    {
        public string Title;
        public readonly List<InventoryRow> Rows = new List<InventoryRow>();
    }

    /// <summary>
    /// Builds the pause-screen inventory (what you carry and your current stats) from a running simulation.
    /// Pure so the HUD and the final uGUI screen show the same thing and it is unit-tested.
    /// </summary>
    public static class InventoryView
    {
        private static readonly (StatId id, string label)[] ShownStats =
        {
            (StatId.Damage, "Damage"), (StatId.FireRate, "Fire rate"), (StatId.ShotSpeed, "Shot speed"), (StatId.Range, "Range"),
            (StatId.MoveSpeed, "Speed"), (StatId.Luck, "Luck"), (StatId.CritChance, "Critical chance"), (StatId.Defense, "Defense"),
        };

        public static List<InventorySection> Build(RunSimulation run)
        {
            var list = new List<InventorySection>();
            if (run == null) return list;
            var e = run.Engine;

            var stats = new InventorySection { Title = "Stats" };
            foreach (var (id, label) in ShownStats)
            {
                float v = e.Stats[id];
                string text = id == StatId.CritChance ? Math.Round(v * 100f).ToString(CultureInfo.InvariantCulture) + "%" : v.ToString("0.##", CultureInfo.InvariantCulture);
                stats.Rows.Add(new InventoryRow { Name = label, Detail = text });
            }
            list.Add(stats);

            if (e.ActiveItem != null)
            {
                var act = new InventorySection { Title = "Active" };
                act.Rows.Add(new InventoryRow { Name = e.ActiveItem.Definition.Name, Detail = e.ActiveItem.Definition.Description, IconKey = e.ActiveItem.Definition.IconKey ?? e.ActiveItem.Definition.Id });
                list.Add(act);
            }

            var passives = new InventorySection { Title = "Items" };
            foreach (var o in e.Passives)
                passives.Rows.Add(new InventoryRow { Name = o.Stacks > 1 ? o.Definition.Name + " x" + o.Stacks : o.Definition.Name, Detail = o.Definition.Description, IconKey = o.Definition.IconKey ?? o.Definition.Id });
            if (passives.Rows.Count > 0) list.Add(passives);

            var trinkets = new InventorySection { Title = "Trinkets" };
            foreach (var o in e.Trinkets) trinkets.Rows.Add(new InventoryRow { Name = o.Definition.Name, Detail = o.Definition.Description, IconKey = o.Definition.IconKey ?? o.Definition.Id });
            if (trinkets.Rows.Count > 0) list.Add(trinkets);

            if (run.ConsumableSlots.Count > 0)
            {
                var cons = new InventorySection { Title = "Consumables" };
                foreach (var id in run.ConsumableSlots) cons.Rows.Add(new InventoryRow { Name = run.ConsumableDisplayName(id), Detail = "", IconKey = id });
                list.Add(cons);
            }

            var forms = new InventorySection { Title = "Transformations" };
            foreach (var t in run.Content.Transformations.Values)
                if (e.HasTransformation(t.Id)) forms.Rows.Add(new InventoryRow { Name = t.Name, Detail = t.Description });
            if (forms.Rows.Count > 0) list.Add(forms);

            var other = new InventorySection { Title = "This floor" };
            if (run.ActiveHex != null) other.Rows.Add(new InventoryRow { Name = run.ActiveHex.Name, Detail = run.ActiveHex.Description });
            if (run.CurrentRoom?.Modifier != null) other.Rows.Add(new InventoryRow { Name = run.CurrentRoom.Modifier.Name, Detail = run.CurrentRoom.Modifier.Description });
            other.Rows.Add(new InventoryRow { Name = run.Floor?.Name ?? "Floor", Detail = "Floor " + (run.FloorIndex + 1) + " of " + run.TotalFloors });
            list.Add(other);
            return list;
        }
    }
}
