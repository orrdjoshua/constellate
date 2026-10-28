using System;
using System.IO;

namespace Constellate.App.Worlds
{
    internal static class ProjectPathResolver
    {
        public static string ResolveProjectRootPath()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Constellate.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        }

        public static string ResolveProjectLocalWorldsDirectory(string projectRootPath)
        {
            return Path.Combine(projectRootPath, ".constellate", "persistence");
        }
    }
}
