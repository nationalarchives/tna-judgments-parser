using System;

namespace TRE.FindCaseLaw;

public class InvalidTreRequestException(string message) : Exception(message) { }
