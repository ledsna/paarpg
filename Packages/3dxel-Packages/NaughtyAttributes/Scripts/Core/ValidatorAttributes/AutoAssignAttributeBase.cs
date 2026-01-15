using System;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public abstract class AutoAssignAttributeBase : ValidatorAttribute
    {
        public bool Verbose { get; }

        protected AutoAssignAttributeBase(bool verbose)
        {
            Verbose = verbose;
        }
    }
}