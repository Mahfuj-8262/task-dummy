namespace Appifylab.Services;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}