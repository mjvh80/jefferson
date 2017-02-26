using System;
using System.Collections.Generic;
using Jefferson.FileProcessing;
using Microsoft.CodeAnalysis.Scripting;

namespace Jefferson.Build.MSBuild
{
    public class TemplateContext : FileScopeContext<TemplateContext, SimpleFileProcessor<TemplateContext>>
    {
        public TemplateContext(KeyValuePair<String, String[]>[] buildVariables)
        {
            // Make all of the builds properties and items available to formatting.

            // todo: decide what the best order is: MSBuild props over script or other way round.
            foreach (var pair in buildVariables)
                try
                {
                    KeyValueStore.Add(pair.Key, String.Join(";", pair.Value));


                }
                catch (Exception e)
                {
                    throw; // todo
                           //logger.LogMessage($"Failed to add key {pair.Item1}");
                           //logger.LogErrorFromException(e);
                }
        }
    }
}