using UnityEngine;

namespace Betta3D
{
    /// <summary>
    /// One betta's look and swimming style saved as an asset, so it can be reused in any scene or project.
    /// Create: Project window → Create → Betta → ベタのプリセット, or the "プリセットとして保存" button on a BettaFish.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBetta", menuName = "Betta/ベタのプリセット", order = 0)]
    public class BettaPreset : ScriptableObject
    {
        public BettaParams parameters = new BettaParams();

        /// <summary>A copy of the parameters (fish edit their own copy, never the asset).</summary>
        public BettaParams Create() { return parameters.Clone(); }
    }
}
