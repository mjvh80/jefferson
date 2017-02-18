using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jefferson.Build.MSBuild
{
    // Taken from http://simoncropp.com/howtoaccessbuildvariablesfromanmsbuildtask .
    // Code has been edited to change style and fix issues with later versions of MSBuild.
    internal static class BuildEngineExtensions
    {
        const BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public;

        public static IEnumerable<String> GetEnvironmentVariable(this IBuildEngine buildEngine, String key, Boolean throwIfNotFound)
        {
            var projectInstance = GetProjectInstance(buildEngine);

            var items = projectInstance.Items
                .Where(x => String.Equals(x.ItemType, key, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (items.Count > 0)
            {
                return items.Select(x => x.EvaluatedInclude);
            }

            var properties = projectInstance.Properties
                .Where(x => String.Equals(x.Name, key, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (properties.Count > 0)
            {
                return properties.Select(x => x.EvaluatedValue);
            }

            if (throwIfNotFound)
            {
                throw new Exception($"Could not extract from '{key}' environmental variables.");
            }

            return Enumerable.Empty<String>();
        }

        public static IEnumerable<Tuple<String, IEnumerable<String>>> GetAllEnvironmentVariables(this IBuildEngine buildEngine)
        {
            var projectInstance = GetProjectInstance(buildEngine);

            //var map = new Dictionary<String, List<String>>();
            //foreach (var item in projectInstance.Items)
            //{
            //    List<String> list;
            //    if (!map.TryGetValue(item.ItemType, out list))
            //        map.Add(item.ItemType, list = new List<String>(1));
            //    list.Add(item.EvaluatedInclude);
            //}
            //foreach (var prop in projectInstance.Properties)
            //{
            //    List<String> list;
            //    if (!map.TryGetValue(prop.Name, out list))
            //        map.Add(prop.Name, list = new List<String>(1));
            //    list.Add(prop.EvaluatedValue);
            //}

            //    projectInstance.Items.ToLookup(item => item.ItemType)[]

            return projectInstance.Items.ToLookup(item => item.ItemType)
                .Select(group => Tuple.Create(group.Key, group.Select(item => item.EvaluatedInclude)))
                .Concat(projectInstance.Properties.ToLookup(p => p.Name)
                    .Select(group => Tuple.Create(group.Key, group.Select(p => p.EvaluatedValue))));


            // return map.Select(kvp => Tuple.Create(kvp.Key, (IEnumerable<String>)kvp.Value));
        }

        static ProjectInstance GetProjectInstance(IBuildEngine buildEngine)
        {
            var buildEngineType = buildEngine.GetType();
            var targetBuilderCallbackField = buildEngineType.GetField("targetBuilderCallback", bindingFlags) ??
                                             buildEngineType.GetField("_targetBuilderCallback", bindingFlags); // renamed in later MSBuild
            if (targetBuilderCallbackField == null)
                throw new Exception("Could not extract targetBuilderCallback from " + buildEngineType.FullName);
            var targetBuilderCallback = targetBuilderCallbackField.GetValue(buildEngine);
            var targetCallbackType = targetBuilderCallback.GetType();
            var projectInstanceField = targetCallbackType.GetField("projectInstance", bindingFlags) ??
                                       targetCallbackType.GetField("_projectInstance", bindingFlags); // same rename
            if (projectInstanceField == null)
            {
                throw new Exception("Could not extract projectInstance from " + targetCallbackType.FullName);
            }
            return (ProjectInstance)projectInstanceField.GetValue(targetBuilderCallback);
        }
    }

}
