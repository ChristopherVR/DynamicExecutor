using DynamicModule.Infrastructure;
using DynamicModule.Models;

namespace DynamicModule.Services.Interfaces;

public interface IDynamicCodeService
{
    /// <summary>
    /// Generates and executes the dynamic code.
    /// </summary>
    /// <param name="config">Settings to take into account when generating and executing the dynamic code.</param>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <returns>A list of <see cref="InvocationResult"/> that indicates the Fullname of the type invoked as well as the output value.</returns>
    IList<InvocationResult> ExecuteCode(Action<DynamicCodeConfig> config);

    /// <summary>
    /// Generates and executes the dynamic code.
    /// </summary>
    /// <param name="config">Settings to take into account when generating and executing the dynamic code.</param>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <returns>A list of <see cref="dynamic"/> objects for each method in each defined type executed in the compiled <see cref="System.Reflection.Assembly"/></returns>
    IList<T> ExecuteCode<T>(Action<DynamicCodeConfig> config);

    /// <summary>
    ///  Generates and executes the dynamic code.
    /// </summary>
    /// <param name="config">Settings to take into account when generating and executing the dynamic code.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to pass to prevent further execution in the event we stop.</param>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <returns>A list of <see cref="InvocationResult"/> that indicates the Fullname of the type invoked as well as the output value.</returns>
    Task<IList<InvocationResult>> ExecuteCodeAsync(Action<DynamicCodeConfig> config, CancellationToken cancellationToken = default);

    /// <summary>
    ///  Generates and executes the dynamic code.
    /// </summary>
    /// <param name="config">Settings to take into account when generating and executing the dynamic code.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to pass to prevent further execution in the event we stop.</param>
    /// <returns>A list of <see cref="dynamic"/> objects for each method in each defined type executed in the compiled <see cref="System.Reflection.Assembly"/></returns>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    Task<IList<T>> ExecuteCodeAsync<T>(Action<DynamicCodeConfig> config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compiles and executes the CSharp code and invoke all members for the defined interface.
    /// </summary>
    /// <param name="sourceCode">Raw <see cref="string"/> literal containing the dynamic code.</param>
    /// <param name="config">Settings to take into account when generating and executing the dynamic code.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to pass to prevent further execution in the event we stop.</param>
    /// <param name="args">A list of arguments that can be passed to the dynamic code.</param>
    /// <returns>A <see cref="Task"/> indicating the success status.</returns>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    Task ExecuteCodeAsync(string sourceCode, Action<SourceCodeConfig> config, CancellationToken cancellationToken = default, params object[] args);

    /// <summary>
    /// Compiles and executes the CSharp code and invoke all members for the defined interface.
    /// </summary>
    /// <param name="sourceCode">Raw <see cref="string"/> literal containing the dynamic code.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to pass to prevent further execution in the event we stop.</param>
    /// <param name="args">A list of arguments that can be passed to the dynamic code.</param>
    /// <returns>A <see cref="Task"/> indicating the success status.</returns>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    Task ExecuteCodeAsync(string sourceCode, CancellationToken cancellationToken = default, params object[] args);

    /// <summary>
    /// Generates and executes the dynamic code.
    /// </summary>
    /// <param name="sourceCode">Raw <see cref="string"/> literal containing the dynamic code.</param>
    /// <param name="config">Settings to take into account when generating and executing the dynamic code.</param>
    /// <param name="args">A list of arguments that can be passed to the dynamic code.</param>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    void ExecuteCode(string sourceCode, Action<SourceCodeConfig> config, params object[] args);

    /// <summary>
    /// Generates and executes the dynamic code.
    /// </summary>
    /// <param name="sourceCode">Raw <see cref="string"/> literal containing the dynamic code.</param>
    /// <param name="args">A list of arguments that can be passed to the dynamic code.</param>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    void ExecuteCode(string sourceCode, params object[] args);

    /// <summary>
    /// Generates and executes the dynamic code.
    /// </summary>
    /// <typeparam name="T">The type that the dynamic code output will be mapped to.</typeparam>
    /// <param name="sourceCode">Raw <see cref="string"/> literal containing the dynamic code.</param>
    /// <param name="config">Settings to take into account when generating and executing the dynamic code.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to pass to prevent further execution in the event we stop.</param>
    /// <param name="args">A list of arguments that can be passed to the dynamic code.</param>
    /// <returns>Returns an object mapped to <typeparamref name="T"/>.</returns>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    Task<T> ExecuteCodeAsync<T>(string sourceCode, Action<SourceCodeConfig> config, CancellationToken cancellationToken = default, params object[] args);

    /// <summary>
    /// Generates and executes the dynamic code.
    /// </summary>
    /// <typeparam name="T">The type that the dynamic code output will be mapped to.</typeparam>
    /// <param name="sourceCode">Raw <see cref="string"/> literal containing the dynamic code.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to pass to prevent further execution in the event we stop.</param>
    /// <param name="args">A list of arguments that can be passed to the dynamic code.</param>
    /// <returns>Returns an object mapped to <typeparamref name="T"/>.</returns>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception>  
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    Task<T> ExecuteCodeAsync<T>(string sourceCode, CancellationToken cancellationToken = default, params object[] args);

    /// <summary>
    /// Generates and executes the dynamic code.
    /// </summary>
    /// <typeparam name="T">The type that the dynamic code output will be mapped to.</typeparam>
    /// <param name="sourceCode">Raw <see cref="string"/> literal containing the dynamic code.</param>
    /// <param name="config">Settings to take into account when generating and executing the dynamic code.</param>
    /// <param name="args">A list of arguments that can be passed to the dynamic code.</param>
    /// <returns>Returns an object mapped to <typeparamref name="T"/>.</returns>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    T ExecuteCode<T>(string sourceCode, Action<SourceCodeConfig> config, params object[] args);

    /// <summary>
    /// Generates and executes the dynamic code.
    /// </summary>
    /// <typeparam name="T">The type that the dynamic code output will be mapped to.</typeparam>
    /// <param name="sourceCode">Raw <see cref="string"/> literal containing the dynamic code.</param>
    /// <param name="args">A list of arguments that can be passed to the dynamic code.</param>
    /// <returns>Returns an object mapped to <typeparamref name="T"/>.</returns>
    /// <exception cref="Exceptions.DynamicCodeException">Thrown when an error occured during the generation of the dynamic code.</exception> 
    /// <exception cref="Exception">Thrown when an error occured during the generation of the dynamic code.</exception> 
    T ExecuteCode<T>(string sourceCode, params object[] args);
}
