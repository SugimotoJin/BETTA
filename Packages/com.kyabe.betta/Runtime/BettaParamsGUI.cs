using UnityEngine;

namespace Betta3D
{
    /// <summary>IMGUI controls for editing one betta (shared by the demo and the desktop app).</summary>
    public class BettaParamsGUI
    {
        static readonly string[] ColorParts = { "体", "ヒレ", "ヒレ先", "模様", "光沢" };

        int colorPart;
        // HSV cache so hue doesn't jump when saturation/brightness hit 0
        object hsvOwner;
        int hsvPart = -1;
        Color hsvColor;
        float hh, ss, vv;

        public static float Slider(string label, float v, float min, float max, string fmt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80));
            v = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(170));
            GUILayout.Label(v.ToString(fmt), GUILayout.Width(40));
            GUILayout.EndHorizontal();
            return v;
        }

        static Color GetPart(BettaParams p, int i)
        {
            switch (i)
            {
                case 0: return p.body;
                case 1: return p.fin;
                case 2: return p.edge;
                case 3: return p.patternColor;
                default: return p.sheen;
            }
        }

        static void SetPart(BettaParams p, int i, Color c)
        {
            switch (i)
            {
                case 0: p.body = c; break;
                case 1: p.fin = c; break;
                case 2: p.edge = c; break;
                case 3: p.patternColor = c; break;
                default: p.sheen = c; break;
            }
        }

        /// <summary>Draws the editor for p. Returns true if anything changed.</summary>
        public bool Draw(BettaParams p, float sizeMin = 0.4f, float sizeMax = 2.5f)
        {
            bool changed = GUI.changed;
            GUI.changed = false;

            GUILayout.Label("名前");
            p.name = GUILayout.TextField(p.name);

            GUILayout.Label("尾の形");
            p.tail = (TailType)GUILayout.SelectionGrid((int)p.tail, BettaParams.TailNames, 2);
            GUILayout.Label("模様");
            p.pattern = (PatternType)GUILayout.SelectionGrid((int)p.pattern, BettaParams.PatternNames, 2);

            GUILayout.Label("色");
            int newPart = GUILayout.SelectionGrid(colorPart, ColorParts, 5);
            if (newPart != colorPart) { colorPart = newPart; hsvPart = -1; }
            Color cur = GetPart(p, colorPart);
            if (hsvOwner != p || hsvPart != colorPart || cur != hsvColor)
            {
                Color.RGBToHSV(cur, out hh, out ss, out vv);
                hsvOwner = p; hsvPart = colorPart;
            }
            Rect sw = GUILayoutUtility.GetRect(300f, 22f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(sw, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, cur, 0f, 0f);
            hh = Slider("色相", hh, 0f, 1f, "0.00");
            ss = Slider("鮮やかさ", ss, 0f, 1f, "0.00");
            vv = Slider("明るさ", vv, 0f, 1f, "0.00");
            Color nc = Color.HSVToRGB(hh, ss, vv);
            if (nc != hsvColor) { hsvColor = nc; SetPart(p, colorPart, nc); }

            GUILayout.Space(6);
            p.size = Slider("大きさ", p.size, sizeMin, sizeMax, "0.00");
            p.finLength = Slider("ヒレの長さ", p.finLength, 0.5f, 1.6f, "0.00");
            p.finFront = Slider("ヒレの前側", p.finFront, 0.6f, 1.4f, "0.00");
            p.pectSize = Slider("胸びれ(ダンボ)", p.pectSize, 0.8f, 3f, "0.00");
            p.speed = Slider("泳ぐ速さ", p.speed, 0.3f, 2.5f, "0.00");
            p.sheenAmount = Slider("光沢", p.sheenAmount, 0f, 1f, "0.00");
            p.clear = Slider("透け感", p.clear, 0f, 1f, "0.00");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("色をおまかせ")) { p.RandomColors(); hsvPart = -1; }
            if (GUILayout.Button("模様の配置を変える")) p.seed = Random.Range(1, 100000);
            GUILayout.EndHorizontal();

            bool result = GUI.changed;
            GUI.changed = changed || result;
            return result;
        }
    }
}
