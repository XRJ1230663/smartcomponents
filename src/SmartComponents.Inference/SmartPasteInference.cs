﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SmartComponents.StaticAssets.Inference;

namespace SmartComponents.Inference;

public class SmartPasteInference
{
    private readonly static JsonSerializerOptions jsonSerializerOptions
        = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    public class SmartPasteRequestData
    {
        public FormField[]? FormFields { get; set; }
        public string? ClipboardContents { get; set; }
    }

    public class FormField
    {
        public string? Identifier { get; set; }
        public string? Description { get; set; }
        public string?[]? AllowedValues { get; set; }
        public string? Type { get; set; }
    }

    public readonly struct SmartPasteResponseData
    {
        public bool BadRequest { get; init; }
        public string? Response { get; init; }
    }

    public Task<SmartPasteResponseData> GetFormCompletionsAsync(IInferenceBackend inferenceBackend, string dataJson)
    {
        var data = JsonSerializer.Deserialize<SmartPasteRequestData>(dataJson, jsonSerializerOptions)!;
        if (data.FormFields is null || data.FormFields.Length == 0 || string.IsNullOrEmpty(data.ClipboardContents))
        {
            return Task.FromResult(new SmartPasteResponseData { BadRequest = true });
        }

        return GetFormCompletionsAsync(inferenceBackend, data);
    }

    public virtual ChatParameters BuildPrompt(SmartPasteRequestData data)
    {
        var systemMessage = @$"
Current date: {DateTime.Today.ToString("D", CultureInfo.InvariantCulture)}

Each response line matches the following format:
FIELD identifier^^^value

Give a response with the following lines only, with values inferred from USER_DATA:
{ToFieldOutputExamples(data.FormFields!)}
END_RESPONSE

Do not explain how the values were determined.
For fields without any corresponding information in USER_DATA, use value value NO_DATA.";

        var prompt = @$"
USER_DATA: {data.ClipboardContents}
";

        return new ChatParameters
        {
            Messages = [
                new (ChatMessageRole.System, systemMessage),
                new (ChatMessageRole.User, prompt),
            ],
            Temperature = 0,
            TopP = 1,
            MaxTokens = 2000,
            FrequencyPenalty = 0.1f,
            PresencePenalty = 0,
            StopSequences = ["END_RESPONSE"],
        };
    }

    public virtual async Task<SmartPasteResponseData> GetFormCompletionsAsync(IInferenceBackend inferenceBackend, SmartPasteRequestData requestData)
    {
        var chatOptions = BuildPrompt(requestData);
        var completionsResponse = await inferenceBackend.GetChatResponseAsync(chatOptions);
        return new SmartPasteResponseData { Response = completionsResponse };
    }

    private static string ToFieldOutputExamples(FormField[] fields)
    {
        var sb = new StringBuilder();

        foreach (var field in fields)
        {
            sb.AppendLine();
            sb.Append($"FIELD {field.Identifier}^^^");

            if (!string.IsNullOrEmpty(field.Description))
            {
                sb.Append($"The {field.Description}");
            }

            if (field.AllowedValues is { Length: > 0 })
            {
                sb.Append($" (multiple choice, with allowed values: ");
                var first = true;
                foreach (var value in field.AllowedValues)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        sb.Append(",");
                    }
                    sb.Append($"\"{value}\"");
                }
                sb.Append(")");
            }
            else
            {
                sb.Append($" of type {field.Type}");
            }
        }

        return sb.ToString();
    }
}
