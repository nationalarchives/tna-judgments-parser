namespace NationalArchives.FindCaseLaw.Utils;

public class CourtDeserialisationException() : Exception("Could not deserialise courts.json");

public class CourtNotFoundException(string message) : Exception(message);
