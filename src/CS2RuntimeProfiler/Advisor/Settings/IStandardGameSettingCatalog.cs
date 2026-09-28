using System;
using System.Collections.Generic;
using CS2RuntimeProfiler.Core.Advisor;

namespace CS2RuntimeProfiler.Advisor.Settings
{
    public interface IStandardGameSettingCatalog
    {
        IReadOnlyList<GameSettingDescriptor> GetCatalog();
    }

    public sealed class SettingCategoryRoot
    {
        public SettingCategoryRoot(string category, object instance)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public string Category { get; }
        public object Instance { get; }
    }
}
