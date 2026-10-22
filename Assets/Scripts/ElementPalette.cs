using UnityEngine;

namespace AstralAlchemy
{
    public static class ElementPalette
    {
        public struct Recipe
        {
            public ElementKind a, b;
            public string name, role, style;
            public int damage;
            public float radius;
            public Color color;
            public bool unstable;
            public Recipe(ElementKind a, ElementKind b, string name, string role, string style, int damage, float radius, Color color, bool unstable = false)
            { this.a = a; this.b = b; this.name = name; this.role = role; this.style = style; this.damage = damage; this.radius = radius; this.color = color; this.unstable = unstable; }
        }

        private static readonly Recipe[] StableRecipes =
        {
            new Recipe(ElementKind.Fire, ElementKind.Fire, "Solar Burst", "Burst · AoE", "burst", 56, 4.4f, new Color(1f,.34f,.22f)),
            new Recipe(ElementKind.Frost, ElementKind.Frost, "Frost Bell", "Freeze · Control", "freeze", 30, 5.1f, new Color(.45f,.86f,1f)),
            new Recipe(ElementKind.Bloom, ElementKind.Bloom, "Verdant Pulse", "Pierce · Restore", "heal", 38, 3.5f, new Color(.5f,.84f,.43f)),
            new Recipe(ElementKind.Storm, ElementKind.Storm, "Thunder Crown", "Chain · Pursuit", "chain", 46, 5.8f, new Color(1f,.84f,.32f)),
            new Recipe(ElementKind.Void, ElementKind.Void, "Star Vortex", "Gather · Execute", "vortex", 44, 5.4f, new Color(.72f,.48f,1f)),
            new Recipe(ElementKind.Star, ElementKind.Star, "Starfall Rain", "Multi · Coverage", "rain", 38, 5f, new Color(1f,.55f,.78f)),
            new Recipe(ElementKind.Fire, ElementKind.Frost, "Steam Comet", "High-Speed · Pierce", "comet", 50, 3.1f, new Color(.95f,.9f,.78f)),
            new Recipe(ElementKind.Fire, ElementKind.Star, "Corona Lance", "Crit · Linear", "lance", 64, 2.8f, new Color(1f,.64f,.32f)),
            new Recipe(ElementKind.Frost, ElementKind.Bloom, "Amber Bloom", "Freeze · Rejuvenate", "garden", 34, 4.7f, new Color(.61f,.9f,.77f)),
            new Recipe(ElementKind.Bloom, ElementKind.Storm, "Thornstorm", "Ensnare · AoE", "storm", 44, 4.9f, new Color(.72f,.85f,.36f)),
            new Recipe(ElementKind.Storm, ElementKind.Star, "Aurora Chain", "Chain · Pursuit", "prism", 52, 6.2f, new Color(1f,.72f,.44f)),
            new Recipe(ElementKind.Void, ElementKind.Star, "Eclipse Mirror", "Root · Burst", "eclipse", 50, 5.3f, new Color(.87f,.62f,.96f))
        };

        private static readonly Recipe[] Miscasts =
        {
            new Recipe(ElementKind.Fire, ElementKind.Bloom, "Wildfire Rampage", "Rain of Fire · Survivors Frenzy", "wildfire", 32, 5.2f, new Color(1f,.45f,.28f), true),
            new Recipe(ElementKind.Fire, ElementKind.Storm, "Backfire Flash", "Lightning Chain · Overload", "backfire", 45, 6.2f, new Color(1f,.75f,.3f), true),
            new Recipe(ElementKind.Fire, ElementKind.Void, "Dark Rift", "Gather & Burst · Void Ward", "rift", 54, 5.8f, new Color(.85f,.42f,.73f), true),
            new Recipe(ElementKind.Frost, ElementKind.Storm, "Flash Freeze", "Full Freeze · Core Restore", "flashfreeze", 26, 99f, new Color(.65f,.95f,1f), true),
            new Recipe(ElementKind.Frost, ElementKind.Void, "Moon Shard", "Mirror Scatter · Energy Siphon", "mirror", 42, 6.6f, new Color(.64f,.72f,1f), true),
            new Recipe(ElementKind.Frost, ElementKind.Star, "Frost Comet", "Frost Burst · Resupply Elements", "bubbleburst", 24, 4.8f, new Color(.72f,.9f,1f), true),
            new Recipe(ElementKind.Bloom, ElementKind.Void, "Star Vine", "Snare & Drain · Ward Spawn", "vine", 36, 6f, new Color(.61f,.84f,.47f), true),
            new Recipe(ElementKind.Bloom, ElementKind.Star, "Meteor Pollen", "Bloom Spread · Enemy Siphon", "pollen", 20, 5.4f, new Color(1f,.62f,.82f), true),
            new Recipe(ElementKind.Storm, ElementKind.Void, "Dark Surge", "Chain Overload · Frenzy", "overload", 48, 7f, new Color(.76f,.55f,1f), true)
        };

        public static Recipe GetRecipe(ElementKind a, ElementKind b)
        {
            foreach (var recipe in StableRecipes) if (IsPair(a, b, recipe.a, recipe.b)) return recipe;
            foreach (var recipe in Miscasts) if (IsPair(a, b, recipe.a, recipe.b)) return recipe;
            return new Recipe(a, b, "Entropy Overflow", "Wild · Star Dust Burst", "chaos", 38, 5.6f, new Color(.79f,.52f,.92f), true);
        }

        public static Recipe GetSuggestedRecipe(int index) => StableRecipes[index % StableRecipes.Length];
        public static int SuggestedRecipeCount => StableRecipes.Length;
        public static Color ColorOf(ElementKind kind)
        {
            return kind switch
            {
                ElementKind.Fire => new Color(1f, .22f, .08f),
                ElementKind.Frost => new Color(.12f, .78f, 1f),
                ElementKind.Storm => new Color(.93f, .58f, 1f),
                ElementKind.Bloom => new Color(.35f, 1f, .42f),
                ElementKind.Void => new Color(.55f, .17f, 1f),
                _ => new Color(1f, .71f, .15f)
            };
        }

        // EN: Renamed from ChineseName to DisplayName; callers updated accordingly.
        public static string DisplayName(ElementKind kind)
        {
            return kind switch
            {
                ElementKind.Fire => "Fire",
                ElementKind.Frost => "Frost",
                ElementKind.Storm => "Storm",
                ElementKind.Bloom => "Bloom",
                ElementKind.Void => "Void",
                _ => "Star"
            };
        }

        // Backward-compat alias so any remaining call sites still compile.
        public static string ChineseName(ElementKind kind) => DisplayName(kind);

        public static string SpellName(ElementKind first, ElementKind second)
        {
            return GetRecipe(first, second).name;
        }

        public static bool IsPair(ElementKind a, ElementKind b, ElementKind c, ElementKind d)
        {
            return (a == c && b == d) || (a == d && b == c);
        }
    }
}
