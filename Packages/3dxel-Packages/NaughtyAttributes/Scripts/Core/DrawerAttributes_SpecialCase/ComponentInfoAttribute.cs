using System;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class ComponentInfoAttribute : Attribute
    {
        public string Description { get; private set; }

        public ComponentInfoAttribute(string description)
        {
            Description = description;
        }
    }
}