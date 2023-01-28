﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DynamicModule.UnitTests.ExampleCode.ScenarioOne;

public class EnvVariables
{
    private readonly ILogger<Test2> _logger;
    private readonly IConfiguration _configuration;
    public EnvVariables(ILogger<Test2> logger, IConfiguration config)
    {
        _logger = logger;
        _configuration = config;
    }

    public string GetConfig()
    {
        return _configuration.GetConnectionString("Default");
        // return string.Empty;
        // return ((IConfigurationRoot)_configuration)?.GetDebugView();
    }
}

public class Test2
{
    private readonly ILogger<Test2> _logger;
    public Test2(ILogger<Test2> logger)
    {
        _logger = logger;
    }

    public async Task Init()
    {
        await Task.CompletedTask;
        ExecuteSyncCode();
    }

    private void ExecuteSyncCode()
    {
        _logger.LogInformation("this is a test.");
    }
}
