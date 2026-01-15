using System;
using System.Linq;

namespace NaughtyAttributes
{
    /// <summary>
    /// <para>
    /// Automatically assigns an asset reference to a field if the field is <c>null</c>.
    /// </para>
    /// 
    /// <para>
    /// ⚠ This attribute is <b>Editor-only</b>.  
    /// The assignment occurs once, during the custom inspector's property drawing phase.  
    /// In other words, the asset will be auto-assigned only when the inspector for the target object is opened first time in scene.
    /// </para>
    ///
    /// 
    /// <para>
    /// At runtime (in a build), this attribute has no effect.  
    /// However, since Unity serializes references, any values auto-assigned in the editor will be saved into the scene or prefab, 
    /// and thus remain available in builds.
    /// </para>
    /// 
    /// <para>
    /// If used together with the <see cref="RequiredAttribute"/>, ensure that this attribute is placed <b>before</b> 
    /// <c>[Required]</c> to avoid false validation errors.
    /// </para>
    /// </summary>
    public class AutoAssignByFilterAttribute : AutoAssignAttributeBase
    {
        public string Filter { get; }
        public string[] SearchInFolders { get; }
        public bool IsSearchInFoldersValid { get; private set; }


        public AutoAssignByFilterAttribute(string filter, string[] searchInFolders = null, bool verbose = true) :
            base(verbose)
        {
            Filter = filter;
            SearchInFolders = searchInFolders;
            ValidateSearchInFolders();
        }

        private void ValidateSearchInFolders()
        {
            IsSearchInFoldersValid = true;
            if (SearchInFolders is null)
                return;

            if (SearchInFolders.Any(folder => string.IsNullOrEmpty(folder)))
                IsSearchInFoldersValid = false;
        }
    }
}