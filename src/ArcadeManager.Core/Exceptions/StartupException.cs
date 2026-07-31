using System;
using System.Runtime.Serialization;

namespace ArcadeManager.Core.Exceptions;

/// <summary>
/// Exception when there is an error during startup
/// </summary>
/// <seealso cref="Exception"/>
[Serializable]
public class StartupException : Exception {

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupException"/> class.
    /// </summary>
    public StartupException() {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StartupException(string message) : base(message) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, or a null reference ( <see
    /// langword="Nothing"/> in Visual Basic) if no inner exception is specified.
    /// </param>
    public StartupException(string message, Exception innerException) : base(message, innerException) {
    }
}