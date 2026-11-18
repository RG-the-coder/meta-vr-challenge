using UnityEngine;
using UnityEngine.EventSystems;
namespace BattlefieldPrototype
{
    internal sealed class BattleUiPointerEventData : PointerEventData
    {
        internal bool HasWorldPosition;
        internal Vector3 WorldPosition;
        internal BattleUiPointerEventData(EventSystem system) : base(system) { }
    }
}
