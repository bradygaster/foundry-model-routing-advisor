using ModelRoutingAdvisor;

var arguments = args.ToList();

if (arguments.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(
        """
        Model Routing Advisor

        Usage:
          dotnet run --project src/ModelRoutingAdvisor -- [--real] [--prompt "text"]

        Default mode is fully offline and uses a fake transport.
        --real enables the DefaultAzureCredential-backed Microsoft Foundry transport.
        """);
    return 0;
}

var useRealTransport = arguments.Remove("--real");
var promptIndex = arguments.FindIndex(argument =>
    argument.Equals("--prompt", StringComparison.OrdinalIgnoreCase));

var prompt = promptIndex >= 0 && promptIndex + 1 < arguments.Count
    ? arguments[promptIndex + 1]
    : "Summarize why deterministic routing is useful.";

var policy = new DeterministicRoutingPolicy();
var decision = policy.Decide(prompt);

IModelTransport transport;
string selectedModel;

try
{
    if (useRealTransport)
    {
        var foundryOptions = FoundryOptions.FromEnvironment();
        transport = new FoundryModelTransport(new HttpClient(), foundryOptions);
        selectedModel = foundryOptions.DeploymentFor(decision.Path);
    }
    else
    {
        transport = new FakeModelTransport();
        selectedModel = decision.Path == ModelPath.LowCost
            ? "offline-low-cost"
            : "offline-high-capability";
    }
}
catch (ModelTransportException exception)
{
    Console.Error.WriteLine($"error.category={exception.Category}");
    Console.Error.WriteLine($"error.message={exception.Message}");
    return 2;
}

var client = new ResilientModelClient(transport, ResilienceOptions.Default);
var result = await client.ExecuteAsync(new ModelRequest(prompt, decision.Path, selectedModel));

Console.WriteLine($"mode={(useRealTransport ? "foundry" : "offline")}");
Console.WriteLine($"route={decision.Path}");
Console.WriteLine($"score={decision.Score}");
Console.WriteLine($"reasons={string.Join("; ", decision.Reasons)}");
Console.WriteLine($"attempts={result.Attempts}");

if (!result.Succeeded)
{
    Console.Error.WriteLine($"error.category={result.ErrorCategory}");
    Console.Error.WriteLine($"error.message={result.ErrorMessage}");
    return 1;
}

Console.WriteLine($"model={result.Response!.ModelId}");
Console.WriteLine($"response={result.Response.Content}");
return 0;
