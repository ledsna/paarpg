using UnityEngine;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    [CustomPropertyDrawer(typeof(HorizontalLineAttribute))]
    public class HorizontalLineDecoratorDrawer : DecoratorDrawer
    {
        // Used for bypass lock on setting attribute in custom Volume Component Editor 
        // ---------------------------------------------------------------------------
        private HorizontalLineAttribute dummyAttributeCopy;

        public HorizontalLineDecoratorDrawer(HorizontalLineAttribute attribute)
        {
            dummyAttributeCopy = attribute;
        }

        public HorizontalLineDecoratorDrawer()
        {
        }
        // ---------------------------------------------------------------------------


        public override float GetHeight()
        {
            HorizontalLineAttribute lineAttr = (HorizontalLineAttribute)attribute;
            if (lineAttr == null)
                lineAttr = dummyAttributeCopy;
            return EditorGUIUtility.singleLineHeight + lineAttr.Height;
        }

        public override void OnGUI(Rect position)
        {
            Rect rect = EditorGUI.IndentedRect(position);
            rect.y += EditorGUIUtility.singleLineHeight / 3.0f;
            HorizontalLineAttribute lineAttr = (HorizontalLineAttribute)attribute;
            if (lineAttr == null)
                lineAttr = dummyAttributeCopy;
            NaughtyEditorGUI.HorizontalLine(rect, lineAttr.Height, lineAttr.Color.GetColor());
        }
    }
}