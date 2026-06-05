using System.Runtime.CompilerServices;

// Allows tests to see internal classes and methods. DynamicProxyGenAssembly2 is used by Moq to fake dependencies like ILogger
[assembly: InternalsVisibleTo("test")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
