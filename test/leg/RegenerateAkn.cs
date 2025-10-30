using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using CaseLaw = UK.Gov.NationalArchives.CaseLaw;

namespace UK.Gov.Legislation.ImpactAssessments.Test {

public class RegenerateAkn {

    [Fact]
    public void RegenerateAllIATestFiles() {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        
        // Find all IA test DOCX files
        var iaDocxFiles = resourceNames
            .Where(name => name.StartsWith("test.leg.ia.test") && name.EndsWith(".docx"))
            .OrderBy(name => name)
            .ToList();
        
        Console.WriteLine($"Found {iaDocxFiles.Count} IA test DOCX files to regenerate:");
        
        foreach (var docxResource in iaDocxFiles) {
            // Extract test number
            var testNum = docxResource.Replace("test.leg.ia.test", "").Replace(".docx", "");
            var aknFileName = $"test{testNum}.akn";
            
            // Find the project root by going up from the test assembly location
            var assemblyDir = Path.GetDirectoryName(assembly.Location);
            var projectRoot = assemblyDir;
            
            // Look for either .sln file or .git directory to find project root
            while (projectRoot != null && 
                   !File.Exists(Path.Combine(projectRoot, "tna-judgments-parser.sln")) &&
                   !Directory.Exists(Path.Combine(projectRoot, ".git"))) {
                projectRoot = Directory.GetParent(projectRoot)?.FullName;
            }
            
            if (projectRoot == null) {
                Console.WriteLine($"    ✗ Could not find project root for test{testNum}");
                continue;
            }
            
            var aknPath = Path.Combine(projectRoot, "test", "leg", "ia", aknFileName);
            
            Console.WriteLine($"  Processing test{testNum}...");
            
            try {
                // Parse DOCX
                var docx = CaseLaw.Tests.ReadDocx(docxResource);
                var akn = Helper.Parse(docx).Serialize();
                
                // Write to file
                File.WriteAllText(aknPath, akn);
                Console.WriteLine($"    ✓ Regenerated {aknFileName}");
            }
            catch (Exception ex) {
                Console.WriteLine($"    ✗ Error regenerating {aknFileName}: {ex.Message}");
            }
        }
    }
}

}

