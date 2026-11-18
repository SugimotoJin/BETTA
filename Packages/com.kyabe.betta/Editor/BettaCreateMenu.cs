using UnityEditor;
using UnityEngine;

namespace Betta3D.EditorTools
{
    /// <summary>GameObject → Betta → … : put a tank or a swimming betta into the scene.</summary>
    public static class BettaCreateMenu
    {
        [MenuItem("GameObject/Betta/水槽（泳ぐ範囲）", false, 10)]
        static void CreateTank(MenuCommand cmd)
        {
            GameObject go = new GameObject("BettaTank");
            go.AddComponent<BettaTank>();
            Place(go, cmd);
        }

        [MenuItem("GameObject/Betta/泳ぐベタ", false, 11)]
        static void CreateFish(MenuCommand cmd)
        {
            GameObject go = new GameObject("Betta");
            BettaFish f = go.AddComponent<BettaFish>();   // Reset() fills in the shaders
            f.Params = BettaParams.Preset(0);
            go.name = f.Params.name;
            BettaSwimmer s = go.AddComponent<BettaSwimmer>();
            s.fish = f;
            // put it into the selected tank if there is one
            GameObject parent = cmd.context as GameObject;
            if (parent == null && Selection.activeGameObject != null) parent = Selection.activeGameObject;
            BettaTank tank = parent != null ? parent.GetComponentInParent<BettaTank>() : null;
            if (tank != null)
            {
                go.transform.SetParent(tank.transform, false);
                go.transform.localPosition = Vector3.zero;
                s.tank = tank;
                Register(go);
            }
            else Place(go, cmd);
        }

        [MenuItem("GameObject/Betta/マウスへの反応（カーソル・エサやり）", false, 12)]
        static void CreateInteraction(MenuCommand cmd)
        {
            GameObject go = new GameObject("BettaInteraction");
            go.AddComponent<BettaInteraction>();   // Reset() fills in the food shader
            Register(go);
        }

        static void Place(GameObject go, MenuCommand cmd)
        {
            GameObject parent = cmd.context as GameObject;
            if (parent != null) GameObjectUtility.SetParentAndAlign(go, parent);
            else if (SceneView.lastActiveSceneView != null)
            {
                Vector3 p = SceneView.lastActiveSceneView.pivot;
                go.transform.position = new Vector3(p.x, p.y, 0f);
            }
            Register(go);
        }

        static void Register(GameObject go)
        {
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }
    }
}
