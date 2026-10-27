using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
    // The original component is renderer metadata, with no lifecycle methods.
    public abstract class UIBloomElement : MonoBehaviour
    {
        [SerializeField] private int mOrder;
        [SerializeField] private UIBloomElementOrder mElementOrder;
    }
}
