using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EMI
{
    internal sealed class CompendiumResourceClickTarget : MonoBehaviour, IPointerClickHandler
    {
        private Action _primary;
        private Action _alternate;

        public void Initialize(Action primary, Action alternate)
        {
            _primary = primary;
            _alternate = alternate;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    _primary?.Invoke();
                    break;

                case PointerEventData.InputButton.Middle:
                case PointerEventData.InputButton.Right:
                    _alternate?.Invoke();
                    break;
            }
        }
    }
}
