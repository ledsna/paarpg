using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    [CustomPropertyDrawer(typeof(InfoBoxAttribute))]
    public class InfoBoxDecoratorDrawer : DecoratorDrawer
    {
        // Used for bypass lock on setting attribute in custom Volume Component Editor 
        // ---------------------------------------------------------------------------
        private InfoBoxAttribute dummyAttributeCopy;

        public InfoBoxDecoratorDrawer(InfoBoxAttribute attribute)
        {
            dummyAttributeCopy = attribute;
        }

        public InfoBoxDecoratorDrawer()
        {
        }
        // ---------------------------------------------------------------------------

        public override float GetHeight()
        {
            return GetHelpBoxHeight();
        }

        public override void OnGUI(Rect rect)
        {
            InfoBoxAttribute infoBoxAttribute = (InfoBoxAttribute)attribute;
            if (infoBoxAttribute == null)
                infoBoxAttribute = dummyAttributeCopy;
            float indentLength = NaughtyEditorGUI.GetIndentLength(rect);
            Rect infoBoxRect = new Rect(
                rect.x + indentLength,
                rect.y,
                rect.width - indentLength,
                GetHelpBoxHeight());

            DrawInfoBox(infoBoxRect, infoBoxAttribute.Text, infoBoxAttribute.Type);
        }

        private float GetHelpBoxHeight()
        {
            InfoBoxAttribute infoBoxAttribute = (InfoBoxAttribute)attribute;
            if (infoBoxAttribute == null)
                infoBoxAttribute = dummyAttributeCopy;
            float minHeight = EditorGUIUtility.singleLineHeight * 2.0f;
            float desiredHeight =
                GUI.skin.box.CalcHeight(new GUIContent(infoBoxAttribute.Text), EditorGUIUtility.currentViewWidth);
            float height = Mathf.Max(minHeight, desiredHeight);

            return height;
        }

        private void DrawInfoBox(Rect rect, string infoText, EInfoBoxType infoBoxType)
        {
            MessageType messageType = MessageType.None;
            switch (infoBoxType)
            {
                case EInfoBoxType.Normal:
                    messageType = MessageType.Info;
                    break;

                case EInfoBoxType.Warning:
                    messageType = MessageType.Warning;
                    break;

                case EInfoBoxType.Error:
                    messageType = MessageType.Error;
                    break;
            }

            NaughtyEditorGUI.HelpBox(rect, infoText, messageType);
        }
    }
}