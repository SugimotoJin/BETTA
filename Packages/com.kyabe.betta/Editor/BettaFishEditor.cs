using UnityEditor;
using UnityEngine;

namespace Betta3D.EditorTools
{
    /// <summary>Inspector for BettaFish: built-in looks, random looks, save / load presets.</summary>
    [CustomEditor(typeof(BettaFish))]
    public class BettaFishEditor : Editor
    {
        int builtIn;

        public override void OnInspectorGUI()
        {
            BettaFish fish = (BettaFish)target;
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("見た目を決める", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            builtIn = EditorGUILayout.Popup(builtIn, BettaParams.PresetNames);
            if (GUILayout.Button("組み込みの見た目を使う", GUILayout.Width(150)))
            {
                Undo.RecordObject(fish, "Betta built-in look");
                fish.Params = BettaParams.Preset(builtIn);
                EditorUtility.SetDirty(fish);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("色をおまかせ"))
            {
                Undo.RecordObject(fish, "Betta random colors");
                fish.Params.RandomColors();
                EditorUtility.SetDirty(fish);
            }
            if (GUILayout.Button("全部おまかせ"))
            {
                Undo.RecordObject(fish, "Betta random");
                fish.Params.RandomAll();
                EditorUtility.SetDirty(fish);
            }
            if (GUILayout.Button("模様の配置を変える"))
            {
                Undo.RecordObject(fish, "Betta pattern seed");
                fish.Params.seed = Random.Range(1, 100000);
                EditorUtility.SetDirty(fish);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("プリセット（他のシーンや作品で使い回す）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("プリセットとして保存…")) SaveAsPreset(fish);
            using (new EditorGUI.DisabledScope(fish.preset == null))
            {
                if (GUILayout.Button("今の見た目をプリセットに書き戻す"))
                {
                    Undo.RecordObject(fish.preset, "Betta update preset");
                    fish.preset.parameters = fish.Params.Clone();
                    EditorUtility.SetDirty(fish.preset);
                    AssetDatabase.SaveAssets();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
                EditorGUILayout.HelpBox("再生中の変更はその場で反映されます。再生を止めると元に戻るので、残したいときはプリセットとして保存してください。", MessageType.Info);
            else
                EditorGUILayout.HelpBox("再生すると、この設定でベタが作られて泳ぎます。", MessageType.None);
        }

        static void SaveAsPreset(BettaFish fish)
        {
            string name = string.IsNullOrEmpty(fish.Params.name) ? "Betta" : fish.Params.name;
            string path = EditorUtility.SaveFilePanelInProject("ベタのプリセットを保存", name, "asset", "保存先を選んでください");
            if (string.IsNullOrEmpty(path)) return;
            BettaPreset p = CreateInstance<BettaPreset>();
            p.parameters = fish.Params.Clone();
            AssetDatabase.CreateAsset(p, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(p);
            if (!Application.isPlaying)
            {
                Undo.RecordObject(fish, "Betta link preset");
                fish.preset = p;
                EditorUtility.SetDirty(fish);
            }
        }
    }
}
