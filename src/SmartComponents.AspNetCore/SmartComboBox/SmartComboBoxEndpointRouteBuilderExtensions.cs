// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SmartComponents.Inference;

namespace Microsoft.AspNetCore.Builder;

public static class SmartComboBoxEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSmartComboBox(this IEndpointRouteBuilder builder, string url, Func<SmartComboBoxRequest, IEnumerable<string>> suggestions)
        => MapSmartComboBoxCore(builder, url, req => Task.FromResult(suggestions(req)));

    public static IEndpointRouteBuilder MapSmartComboBox(this IEndpointRouteBuilder builder, string url, Func<SmartComboBoxRequest, Task<IEnumerable<string>>> suggestions)
        => MapSmartComboBoxCore(builder, url, req => suggestions(req));

    private static IEndpointRouteBuilder MapSmartComboBoxCore(this IEndpointRouteBuilder builder, string url, Func<SmartComboBoxRequest, Task<IEnumerable<string>>> suggestions)
    {
        var validateAntiforgery = DefaultSmartComponentsBuilder.HasEnabledAntiForgeryValidation(builder.ServiceProvider);

        var endpoint = builder.MapPost(url, async (HttpContext httpContext,
            [FromServices] IAntiforgery antiforgery) =>
        {
            // See comment in SmartComponentsServiceCollectionExtensions to explain the antiforgery handling
            if (validateAntiforgery)
            {
                await antiforgery.ValidateRequestAsync(httpContext);
            }

            // Can't use [FromForm] on net6.0
            var form = httpContext.Request.Form;
            if (!(form.TryGetValue("inputValue", out var inputValue) && !string.IsNullOrEmpty(inputValue))
                || !(form.TryGetValue("maxResults", out var maxResultsString) && int.TryParse(maxResultsString, out var maxResults))
                || !(form.TryGetValue("similarityThreshold", out var similarityThresholdString) && float.TryParse(similarityThresholdString, out var similarityThreshold)))
            {
                return Results.BadRequest("inputValue, maxResults, and similarityThreshold are required");
            }

            var suggestionsList = await suggestions(new SmartComboBoxRequest
            {
                HttpContext = httpContext,
                Query = new SimilarityQuery
                {
                    SearchText = inputValue.ToString(),
                    MaxResults = maxResults,
                    MinSimilarity = similarityThreshold,
                }
            });

            return Results.Ok(suggestionsList);
        });

#if NET8_0_OR_GREATER
        endpoint.DisableAntiforgery();
#endif

        return builder;
    }
}
