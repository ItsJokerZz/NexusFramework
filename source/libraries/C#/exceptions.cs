using System;

namespace NexusFramework
{
    /// <summary>
    /// Base exception for the NexusFramework library.
    /// </summary>
    public class NexusFrameworkException : Exception
    {
        public NexusFrameworkException()
        {
        }

        public NexusFrameworkException(string message)
            : base(message)
        {
        }

        public NexusFrameworkException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Thrown when the target device is not connected.
    /// </summary>
    public class TargetNotConnectedException : NexusFrameworkException
    {
        public TargetNotConnectedException()
            : base("The target device is not connected.")
        {
        }

        public TargetNotConnectedException(string message)
            : base(message)
        {
        }

        public TargetNotConnectedException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Thrown when the response from the target is invalid.
    /// </summary>
    public class InvalidResponseException : NexusFrameworkException
    {
        public InvalidResponseException()
            : base("The response from the target was invalid.")
        {
        }

        public InvalidResponseException(string message)
            : base(message)
        {
        }

        public InvalidResponseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Thrown when the response from the target is not valid JSON.
    /// </summary>
    public class InvalidJsonException : NexusFrameworkException
    {
        public InvalidJsonException()
            : base("The response from the target was not valid JSON.")
        {
        }

        public InvalidJsonException(string message)
            : base(message)
        {
        }

        public InvalidJsonException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

}
