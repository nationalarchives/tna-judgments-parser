using System.Runtime.CompilerServices;

// Allows tests to see internal classes and methods. DynamicProxyGenAssembly2 is used by Moq to fake dependencies like ILogger
[assembly: InternalsVisibleTo("NationalArchives.FindCaseLaw.Backlog.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
