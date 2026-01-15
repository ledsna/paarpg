using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Ledsna
{
    public class UI_Match_Scroll_Wheel_To_Selected_Button : MonoBehaviour
    {
        [SerializeField] GameObject currentlySelected;
        [SerializeField] GameObject previouslySelected;
        [SerializeField] RectTransform currentlySelectedTransform;
        
        [SerializeField] RectTransform contentPanel;
        [SerializeField] ScrollRect scrollRect;

        private void Update()
        {
            currentlySelected = EventSystem.current.currentSelectedGameObject;

            if (currentlySelected != null)
            {
                if (currentlySelected != previouslySelected)
                {
                    previouslySelected = currentlySelected;
                    currentlySelectedTransform = currentlySelected.GetComponent<RectTransform>();
                    SnapTo(currentlySelectedTransform);
                }
            }
        }

        private void SnapTo(RectTransform target)
        {
            Canvas.ForceUpdateCanvases();

            Vector2 newPosition = (Vector2)scrollRect.transform.InverseTransformPoint(contentPanel.position) -
                                  (Vector2)scrollRect.transform.InverseTransformPoint(target.position);

            newPosition.x = 0;
            
            contentPanel.anchoredPosition = newPosition;
        }
    }
}
