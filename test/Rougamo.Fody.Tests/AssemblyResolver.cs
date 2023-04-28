﻿using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rougamo.Fody.Tests
{
    internal class AssemblyResolver : IAssemblyResolver
    {
        Dictionary<string, string> referenceDictionary;
        Dictionary<string, AssemblyDefinition> assemblyDefinitionCache = new Dictionary<string, AssemblyDefinition>(StringComparer.InvariantCultureIgnoreCase);

        public AssemblyResolver()//IEnumerable<string> splitReferences)
        {
            referenceDictionary = new Dictionary<string, string>();

            //foreach (var filePath in splitReferences)
            //{
            //    referenceDictionary[GetAssemblyName(filePath)] = filePath;
            //}
        }

        string GetAssemblyName(string filePath)
        {
            try
            {
                return GetAssembly(filePath, new ReaderParameters(ReadingMode.Deferred)).Name.Name;
            }
            catch (Exception ex)
            {
                return Path.GetFileNameWithoutExtension(filePath);
            }
        }

        AssemblyDefinition GetAssembly(string file, ReaderParameters parameters)
        {
            if (assemblyDefinitionCache.TryGetValue(file, out var assembly))
            {
                return assembly;
            }

            parameters.AssemblyResolver ??= this;
            try
            {
                return assemblyDefinitionCache[file] = AssemblyDefinition.ReadAssembly(file, parameters);
            }
            catch (Exception exception)
            {
                throw new Exception($"Could not read '{file}'.", exception);
            }
        }

        public virtual AssemblyDefinition? Resolve(AssemblyNameReference assemblyNameReference)
        {
            return Resolve(assemblyNameReference, new ReaderParameters());
        }

        public virtual AssemblyDefinition? Resolve(AssemblyNameReference assemblyNameReference, ReaderParameters? parameters)
        {
            parameters ??= new ReaderParameters();

            if (referenceDictionary.TryGetValue(assemblyNameReference.Name, out var fileFromDerivedReferences))
            {
                return GetAssembly(fileFromDerivedReferences, parameters);
            }

            return null;
        }

        public virtual void Dispose()
        {
            foreach (var value in assemblyDefinitionCache.Values)
            {
                value?.Dispose();
            }
        }
    }
}
