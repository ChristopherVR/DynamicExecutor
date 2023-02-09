<p align="center"> 
  <img src="https://github.com/devicons/devicon/blob/master/icons/dotnetcore/dotnetcore-original.svg" alt="NET Logo" width="80px" height="80px">
</p>
<h1 align="center"> Dynamic Code Compiler & Executor in C# .NET 7 </h1>
<h3 align="center"> This is a demonstration on how to compile dynamic code and execute it at runtime. </h3>  
<h4 align="center"> Note: This project is still a WIP </h4>  
</br>
<!-- TABLE OF CONTENTS -->
<h2 id="table-of-contents"> :book: Table of Contents</h2>
<details open="open">
  <summary>Table of Contents</summary>
  <ol>
    <li><a href="#about-the-project"> ➤ About The Project</a></li>
    <li><a href="#prerequisites"> ➤ Prerequisites</a></li>
    <li><a href="#setup"> ➤ Setup</a></li>
    <li><a href="#examples"> ➤ Examples</a></li>
  </ol>
</details>

![-----------------------------------------------------](https://github.com/ChristopherVR/ChristopherVR/blob/main/rainbow.png)

<!-- ABOUT THE PROJECT -->
<h2 id="about-the-project"> :pencil: About The Project</h2>
<p align="justify"> 
  This project aims to provide a proof of concept on how one can use Roslyn API (CodeAnalysis nuget package) to execute dynamic code with ease. This might be helpful in cases where one needs to build dynamic reports and/or pages.
</p>

![-----------------------------------------------------](https://github.com/ChristopherVR/ChristopherVR/blob/main/rainbow.png)

<!-- PREREQUISITES -->
<h2 id="prerequisites"> :fork_and_knife: Prerequisites</h2>

[![Made with-dot-net](https://img.shields.io/badge/-Made%20with%20.NET-purple)](https://dotnet.microsoft.com/en-us/) <br>
[![build status][buildstatus-image]][buildstatus-url]

[buildstatus-image]: https://github.com/ChristopherVR/DynamicExecutor/blob/main/.github/workflows/badge.svg
[buildstatus-url]: https://github.com/ChristopherVR/DynamicExecutor/actions

<!--This project is written mainly in C# and JavaScript programming languages. <br>-->
The following open source packages are used in this project:
* <a href="https://github.com/dotnet/aspnetcore"> .NET 7</a> 
 
![-----------------------------------------------------](https://github.com/ChristopherVR/ChristopherVR/blob/main/rainbow.png)


<h2 id="setup"> :computer: Setup</h2>

<p align="justify"> 
A: Register the AnalyseCodeService & DynamicCodeService on your startup:

```
builder.Services.AddSingleton<IAnalyseCodeService, AnalyseCodeService>();
builder.Services.AddSingleton<IDynamicCodeService, DynamicCodeService>();
```

Note: IHttpContextAccessor is required for these services.

B: View the *IDynamicCodeService* interface to see what method overload would be most appropriate for you to use.

C: See the Examples on how to execute code in this service and retrieve a response.

</p>

![-----------------------------------------------------](https://github.com/ChristopherVR/ChristopherVR/blob/main/rainbow.png)


<!-- ROADMAP -->
<h2 id="examples"> :dart: Examples</h2>

<p align="justify"> 

* Executing a basic Console application:

```
// Use DI as required.
IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

// Act
int res = await dynamicService.ExecuteCodeAsync<int>($@"using System; Console.WriteLine(""Hello world!""); return int.Parse(args[0]);", c =>
{
    c.ExecuteAsConsoleApplication = true;
}, cancellationToken: CancellationToken.None, new object[] { new string[] { "2" } });

// Assert
Assert.Equal(2, res);
```

See the unit tests file for more examples <a href="https://github.com/ChristopherVR/DynamicExecutor/blob/main/DynamicModule.UnitTests/CSharpAnalysisTests.cs"> here</a>.

</p>

![-----------------------------------------------------](https://github.com/ChristopherVR/ChristopherVR/blob/main/rainbow.png)

