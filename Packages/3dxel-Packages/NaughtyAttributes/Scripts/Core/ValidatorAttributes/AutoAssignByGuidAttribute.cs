namespace NaughtyAttributes
{
    public class AutoAssignByGuidAttribute : AutoAssignAttributeBase
    {
        public string Guid { get; }

        public AutoAssignByGuidAttribute(string guid, bool verbose = true) : base(verbose)
        {
            Guid = guid;
        }
    }
}