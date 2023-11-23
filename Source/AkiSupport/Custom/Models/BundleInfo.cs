﻿namespace Aki.Custom.Models
{
    /// <summary>
    /// Created by: SPT-Aki team
    /// Link: https://dev.sp-tarkov.com/SPT-AKI/Modules/src/branch/master/project/Aki.Custom/Models/BundleInfo.cs
    /// </summary>
    public class BundleInfo
    {
        public string ModPath;
        public string Key { get; }
        public string Path { get; set; }
        public string[] DependencyKeys { get; }

        public BundleInfo(string modPath, string key, string path, string[] dependencyKeys)
        {
            ModPath = modPath;
            Key = key;
            Path = path;
            DependencyKeys = dependencyKeys;
        }
    }
}
