using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DarwinPark.Core
{
    public class RightClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public event Action OnRightClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClick?.Invoke();
            }
        }
    }
}