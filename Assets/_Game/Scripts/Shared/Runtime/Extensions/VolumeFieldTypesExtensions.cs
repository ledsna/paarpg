using UnityEngine.Rendering;

namespace __Project.Shared.Extensions
{
    public static class VolumeFieldTypesExtensions
    {
        public static T GetValueOrDefault<T>(this VolumeParameter<T> parameter, T defaultValue) =>
            parameter.overrideState ? parameter.value : defaultValue;
    }
}