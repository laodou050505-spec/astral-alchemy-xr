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
            new Recipe(ElementKind.Fire, ElementKind.Fire, "赤阳爆", "爆裂 · 群攻", "burst", 56, 4.4f, new Color(1f,.34f,.22f)),
            new Recipe(ElementKind.Frost, ElementKind.Frost, "冰晶钟", "冻结 · 控场", "freeze", 30, 5.1f, new Color(.45f,.86f,1f)),
            new Recipe(ElementKind.Bloom, ElementKind.Bloom, "翠星脉", "穿透 · 修复", "heal", 38, 3.5f, new Color(.5f,.84f,.43f)),
            new Recipe(ElementKind.Storm, ElementKind.Storm, "雷冠链", "连锁 · 追击", "chain", 46, 5.8f, new Color(1f,.84f,.32f)),
            new Recipe(ElementKind.Void, ElementKind.Void, "吞星涡", "聚怪 · 斩杀", "vortex", 44, 5.4f, new Color(.72f,.48f,1f)),
            new Recipe(ElementKind.Star, ElementKind.Star, "星海雨", "多重 · 覆盖", "rain", 38, 5f, new Color(1f,.55f,.78f)),
            new Recipe(ElementKind.Fire, ElementKind.Frost, "蒸汽彗星", "高速 · 穿透", "comet", 50, 3.1f, new Color(.95f,.9f,.78f)),
            new Recipe(ElementKind.Fire, ElementKind.Star, "日冕贯星", "暴击 · 直线", "lance", 64, 2.8f, new Color(1f,.64f,.32f)),
            new Recipe(ElementKind.Frost, ElementKind.Bloom, "琥珀花钟", "冻结 · 回春", "garden", 34, 4.7f, new Color(.61f,.9f,.77f)),
            new Recipe(ElementKind.Bloom, ElementKind.Storm, "荆雷风暴", "缠绕 · 群攻", "storm", 44, 4.9f, new Color(.72f,.85f,.36f)),
            new Recipe(ElementKind.Storm, ElementKind.Star, "极光连击", "连锁 · 追击", "prism", 52, 6.2f, new Color(1f,.72f,.44f)),
            new Recipe(ElementKind.Void, ElementKind.Star, "月蚀镜界", "定身 · 爆发", "eclipse", 50, 5.3f, new Color(.87f,.62f,.96f))
        };

        private static readonly Recipe[] Miscasts =
        {
            new Recipe(ElementKind.Fire, ElementKind.Bloom, "炽芽暴走", "火雨清场 · 残敌狂暴", "wildfire", 32, 5.2f, new Color(1f,.45f,.28f), true),
            new Recipe(ElementKind.Fire, ElementKind.Storm, "闪燃短路", "闪电连锁 · 近敌超载", "backfire", 45, 6.2f, new Color(1f,.75f,.3f), true),
            new Recipe(ElementKind.Fire, ElementKind.Void, "黯焰裂缝", "聚怪爆裂 · 虚空护盾", "rift", 54, 5.8f, new Color(.85f,.42f,.73f), true),
            new Recipe(ElementKind.Frost, ElementKind.Storm, "极昼风暴", "全场冻结 · 星核修复", "flashfreeze", 26, 99f, new Color(.65f,.95f,1f), true),
            new Recipe(ElementKind.Frost, ElementKind.Void, "碎月镜片", "镜片散射 · 汲能标记", "mirror", 42, 6.6f, new Color(.64f,.72f,1f), true),
            new Recipe(ElementKind.Frost, ElementKind.Star, "彗星霜雾", "霜雾爆发 · 补齐元素", "bubbleburst", 24, 4.8f, new Color(.72f,.9f,1f), true),
            new Recipe(ElementKind.Bloom, ElementKind.Void, "食星藤蔓", "缠绕吸取 · 唤醒护盾", "vine", 36, 6f, new Color(.61f,.84f,.47f), true),
            new Recipe(ElementKind.Bloom, ElementKind.Star, "流星花粉", "回春扩散 · 敌人汲能", "pollen", 20, 5.4f, new Color(1f,.62f,.82f), true),
            new Recipe(ElementKind.Storm, ElementKind.Void, "黑潮雷暴", "连锁过载 · 余波狂暴", "overload", 48, 7f, new Color(.76f,.55f,1f), true)
        };

        public static Recipe GetRecipe(ElementKind a, ElementKind b)
        {
            foreach (var recipe in StableRecipes) if (IsPair(a, b, recipe.a, recipe.b)) return recipe;
            foreach (var recipe in Miscasts) if (IsPair(a, b, recipe.a, recipe.b)) return recipe;
            return new Recipe(a, b, "熵流溢出", "失控 · 星尘爆裂", "chaos", 38, 5.6f, new Color(.79f,.52f,.92f), true);
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

        public static string ChineseName(ElementKind kind)
        {
            return kind switch
            {
                ElementKind.Fire => "炎",
                ElementKind.Frost => "霜",
                ElementKind.Storm => "雷",
                ElementKind.Bloom => "生命",
                ElementKind.Void => "虚空",
                _ => "星辉"
            };
        }

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
