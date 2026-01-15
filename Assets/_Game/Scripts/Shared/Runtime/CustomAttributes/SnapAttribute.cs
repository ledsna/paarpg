using UnityEngine;

namespace __Project.Shared.Attributes
{
    public class SnapAttribute : PropertyAttribute
    {
        public int Step;

        public SnapAttribute(int step)
        {
            Step = step;
        }
    }
}