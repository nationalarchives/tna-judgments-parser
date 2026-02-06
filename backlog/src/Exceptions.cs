#nullable enable

using System;

namespace Backlog.Src;

public class MoreThanOneFileFoundException(string message) : Exception(message);
