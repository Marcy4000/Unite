using UnityEngine;

namespace Unite.Updates
{
    [CreateAssetMenu(menuName = "Unite/Balance Tuning")]
    public sealed class BalanceTuning : ScriptableObject
    {
        [Tooltip("Optional overrides keyed by the stable asset ID in the exported JSON. Both cooldown variants must be specified.")]
        public MoveBalance[] moveOverrides = System.Array.Empty<MoveBalance>();

        public BalanceNumber[] numbers =
        {
            new BalanceNumber { id = "held.muscle.cap", value = 360 },
            new BalanceNumber { id = "held.muscle.ratio", value = 0.03f },
            new BalanceNumber { id = "held.focus.ratio", value = 0.12f },
            new BalanceNumber { id = "held.shell.base", value = 80 },
            new BalanceNumber { id = "held.shell.ratio", value = 0.6f },
            new BalanceNumber { id = "held.score.level", value = 30 },
            new BalanceNumber { id = "held.score.ratio", value = 0.1f }
        };
    }
}
