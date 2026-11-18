using System;
using UnityEngine;

namespace Betta3D
{
    public enum TailType { HalfMoon, RoseTail, VeilTail, CrownTail, DoubleTail, Plakat, SpadeTail }
    public enum PatternType { Solid, Butterfly, Marble, Dragon }

    /// <summary>Everything that defines how one betta looks and moves.</summary>
    [Serializable]
    public class BettaParams
    {
        public string name = "ベタ";
        public TailType tail = TailType.HalfMoon;
        public PatternType pattern = PatternType.Solid;
        public Color body = new Color32(150, 10, 20, 255);
        public Color fin = new Color32(190, 15, 25, 255);
        public Color edge = new Color32(230, 40, 40, 255);
        public Color patternColor = Color.white;
        public Color sheen = new Color32(80, 140, 255, 255);
        [Range(0f, 1f)] public float sheenAmount = 0.15f;
        [Range(0f, 1f)] public float clear = 0.2f;
        [Range(0.5f, 1.6f)] public float finLength = 1f;
        [Range(0.6f, 1.4f)] public float finFront = 1f;   // length of the front rays of dorsal/anal (individual variation)
        [Range(0.8f, 3f)] public float pectSize = 1f;     // pectoral fin size: 2+ = "dumbo" (elephant ear) betta
        [Range(0.3f, 2.5f)] public float speed = 1f;
        [Range(0.3f, 3f)] public float size = 1f;   // body length in world units
        public int seed = 1;

        public BettaParams Clone() { return (BettaParams)MemberwiseClone(); }

        public static readonly string[] TailNames = {
            "ハーフムーン", "ローズテール", "ベールテール", "クラウンテール", "ダブルテール", "プラカット", "スペードテール" };
        public static readonly string[] PatternNames = { "単色", "バタフライ", "マーブル", "ドラゴン" };
        public static readonly string[] PresetNames = {
            "レッド・ハーフムーン", "ロイヤルブルー・ローズテール", "ピンク・ベールテール", "パープル・クラウンテール",
            "オレンジ・ダブルテール", "イエロー・プラカット", "ターコイズ・ドラゴン", "コイ（マーブル）",
            "ブルー・バタフライ", "ブラックオーキッド", "スーパーホワイト", "ダンボ（ホワイト）", "ダンボ（ブルーマーブル）" };

        static Color C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }

        static BettaParams P(TailType t, PatternType p, Color body, Color fin, Color edge, Color pat, Color sheen, float sheenAmt, float clear)
        {
            return new BettaParams
            {
                tail = t, pattern = p, body = body, fin = fin, edge = edge, patternColor = pat,
                sheen = sheen, sheenAmount = sheenAmt, clear = clear
            };
        }

        public static BettaParams Preset(int i)
        {
            Color W = Color.white;
            BettaParams b;
            switch (i)
            {
                case 1: b = P(TailType.RoseTail, PatternType.Solid, C(25, 50, 150), C(35, 80, 200), C(120, 200, 255), W, C(90, 200, 255), 0.7f, 0.2f); break;
                case 2: b = P(TailType.VeilTail, PatternType.Solid, C(170, 30, 90), C(210, 50, 120), C(255, 170, 200), W, C(150, 120, 255), 0.2f, 0.3f); break;
                case 3: b = P(TailType.CrownTail, PatternType.Solid, C(60, 20, 100), C(90, 30, 160), C(200, 80, 255), W, C(100, 80, 255), 0.4f, 0.2f); break;
                case 4: b = P(TailType.DoubleTail, PatternType.Solid, C(200, 90, 10), C(240, 120, 20), C(255, 200, 80), W, C(255, 220, 120), 0.1f, 0.2f); break;
                case 5: b = P(TailType.Plakat, PatternType.Solid, C(230, 190, 30), C(240, 200, 40), C(255, 240, 150), W, C(255, 255, 200), 0.1f, 0.1f); break;
                case 6: b = P(TailType.SpadeTail, PatternType.Dragon, C(20, 110, 120), C(30, 150, 160), C(150, 240, 230), C(230, 240, 240), C(120, 255, 230), 0.5f, 0.2f); break;
                case 7: b = P(TailType.HalfMoon, PatternType.Marble, C(240, 235, 225), C(245, 240, 235), W, C(220, 40, 30), C(200, 200, 255), 0f, 0.3f); break;
                case 8: b = P(TailType.HalfMoon, PatternType.Butterfly, C(20, 40, 130), C(30, 60, 190), W, C(245, 245, 250), C(90, 160, 255), 0.5f, 0.2f); break;
                case 9: b = P(TailType.HalfMoon, PatternType.Solid, C(28, 22, 34), C(34, 28, 50), C(90, 110, 230), W, C(70, 90, 220), 0.45f, 0.15f); break;
                case 10: b = P(TailType.HalfMoon, PatternType.Solid, C(235, 232, 228), C(245, 243, 240), W, W, C(200, 210, 255), 0.2f, 0.35f); break;
                case 11: b = P(TailType.HalfMoon, PatternType.Solid, C(238, 232, 226), C(246, 240, 236), C(255, 215, 220), W, C(210, 215, 255), 0.25f, 0.3f); b.pectSize = 2.5f; break;
                case 12: b = P(TailType.HalfMoon, PatternType.Marble, C(40, 70, 170), C(50, 95, 210), C(170, 215, 255), C(240, 240, 245), C(100, 180, 255), 0.55f, 0.25f); b.pectSize = 2.3f; break;
                default: b = P(TailType.HalfMoon, PatternType.Solid, C(150, 10, 20), C(190, 15, 25), C(230, 40, 40), W, C(80, 140, 255), 0.15f, 0.2f); i = 0; break;
            }
            b.name = PresetNames[i];
            b.seed = UnityEngine.Random.Range(1, 100000);
            return b;
        }

        public void RandomColors()
        {
            float h = UnityEngine.Random.value;
            float dark = 0.3f + 0.5f * UnityEngine.Random.value;
            body = Color.HSVToRGB(h, 0.55f + 0.45f * UnityEngine.Random.value, dark);
            fin = Color.HSVToRGB(Mathf.Repeat(h + (UnityEngine.Random.value - 0.5f) * 0.1f, 1f), 0.65f + 0.35f * UnityEngine.Random.value, Mathf.Clamp01(dark + 0.2f));
            float e = UnityEngine.Random.value;
            if (e < 0.35f) edge = Color.HSVToRGB(h, 0.3f, 1f);
            else if (e < 0.6f) edge = Color.HSVToRGB(Mathf.Repeat(h + 0.5f + (UnityEngine.Random.value - 0.5f) * 0.15f, 1f), 0.7f, 0.95f);
            else edge = Color.HSVToRGB(Mathf.Repeat(h + (UnityEngine.Random.value - 0.5f) * 0.08f, 1f), 0.85f, 1f);
            patternColor = UnityEngine.Random.value < 0.5f ? new Color(0.96f, 0.95f, 0.94f) : Color.HSVToRGB(Mathf.Repeat(h + 0.33f + UnityEngine.Random.value * 0.33f, 1f), 0.8f, 0.9f);
            sheen = Color.HSVToRGB(Mathf.Repeat(h + (UnityEngine.Random.value - 0.5f) * 0.22f, 1f), 0.6f, 1f);
            sheenAmount = UnityEngine.Random.value * 0.7f;
        }

        public void RandomAll()
        {
            RandomColors();
            tail = (TailType)UnityEngine.Random.Range(0, 7);
            float p = UnityEngine.Random.value;
            pattern = p < 0.55f ? PatternType.Solid : (p < 0.72f ? PatternType.Butterfly : (p < 0.87f ? PatternType.Marble : PatternType.Dragon));
            finLength = 0.8f + UnityEngine.Random.value * 0.5f;
            pectSize = UnityEngine.Random.value < 0.12f ? 2f + UnityEngine.Random.value * 0.8f : 1f;
            clear = 0.1f + UnityEngine.Random.value * 0.35f;
            seed = UnityEngine.Random.Range(1, 100000);
        }
    }
}
