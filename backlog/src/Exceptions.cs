#nullable enable

using System;

namespace Backlog;

public class MoreThanOneFileFoundException(string message) : Exception(message);
public class ProblemUploadingFileToS3Exception(string message) : Exception(message);
