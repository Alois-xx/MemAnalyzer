using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Use resource trick to embed dependent assemblies into executable to be able to deploy a single executable with no other dependencies.
    /// </summary>
    class AppDomainResolverFromResources
    {
        PropertyInfo[] PublicGetters;
        public AppDomainResolverFromResources(Type ResGenGeneratedType)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            PublicGetters = ResGenGeneratedType.GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetProperty)
                                               .Where(p => p.GetMethod.ReturnType == typeof(byte[])).ToArray();
        }

        private System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new AssemblyName(args.Name);
            string propName = name.Name.Replace(".", "_");
            foreach (var getter in PublicGetters)
            {
                if (propName == getter.Name)
                {
                    byte[] assemblyData = (byte[]) getter.GetMethod.Invoke(null,null);
                    return Assembly.Load(assemblyData);
                }
            }

            return null;
        }
    }
}
