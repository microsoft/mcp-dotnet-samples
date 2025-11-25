using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpSamples.PptTranslator.HybridApp.Prompts;

/// <summary>
/// Provides structured prompts for translation tasks in the PPT Translator MCP server.
/// </summary>
public interface ITranslationPrompt
{
    /// <summary>
    /// Gets a pre-defined prompt for translating extracted PPT JSON text into a target language.
    /// </summary>
    /// <param name="targetLang">The target language code (e.g., "ko", "ja", "fr").</param>
    /// <returns>A formatted translation prompt string.</returns>
    string GetTranslationPrompt(string targetLang);
}

/// <summary>
/// Defines the translation prompt entity for the PPT Translator MCP server.
/// </summary>
[McpServerPromptType]
public class TranslationPrompt : ITranslationPrompt
{
    /// <inheritdoc />
    [McpServerPrompt(
        Name = "get_translation_prompt",
        Title = "Prompt for translating PPT extracted text into a target language")]
    [Description("Get a pre-defined instruction for translating extracted PowerPoint JSON text into the specified target language.")]
    public string GetTranslationPrompt(
        [Description("The target language code (e.g., 'ko', 'ja')")] string targetLang)
    {
        return string.Format("""
        You are a professional translator specialized in PowerPoint presentation content.

        The input is a JSON structure that contains extracted text from slides. 
        Each object in the JSON has the following fields:
        - SlideIndex: The slide number
        - ShapeId: The unique identifier of the text shape
        - Text: The original extracted text

        Your goal is to translate ONLY the `Text` values into **{0}**, 
        while keeping the overall JSON structure (including SlideIndex and ShapeId) unchanged.

        ===============================
        TRANSLATION RULES
        ===============================
        1. JSON FORMAT
        - Do NOT modify keys, structure, or ordering.
        - Output must be valid JSON only.
        - Translate only the value in "Text".

        2. DO NOT TRANSLATE:
        - Brand names: Microsoft, PowerPoint, Azure
        - Acronyms: AI, API, GPU, HTTP, JSON
        - Protocol names: Model Context Protocol, OAuth, WebSocket
        - Model names: GPT-5, GPT-4o, Llama 3
        - Code, paths, URLs, formulas

        3. ACADEMIC TONE
        - Use clear, formal, precise language.
        - Maintain semantic meaning.
        - Preserve sentence length and structure.

        4. MIXED LANGUAGE HANDLING
        For mixed-language like "데이터 분석 (Data Analysis)":
        - Translate the main language into the target language.
        - Preserve the secondary language.
        - Swap their order so the target language comes first.
        - Never delete or shorten content.

        Examples:
        • Target=en → "Data Analysis (데이터 분석)"
        • Target=ko → "소개 (Introduction)"

        5. STRUCTURE PRESERVATION
        - Preserve line breaks (\n)
        - Preserve lists
        - Preserve formatting markers like **bold**

        6. DO NOT ADD ANY CONTENT


        ===============================
        EXAMPLE
        ===============================
        Input:
        {{
            "TotalCount": 2,
            "Items": [
                {{
                    "SlideIndex": 1,
                    "ShapeId": "TextBox 5",
                    "Text": "Project Overview"
                }},
                {{
                    "SlideIndex": 2,
                    "ShapeId": "TextBox 7",
                    "Text": "Q&A and Discussion"
                }}
            ]
        }}

        Output:
        {{
            "TotalCount": 2,
            "Items": [
                {{
                    "SlideIndex": 1,
                    "ShapeId": "TextBox 5",
                    "Text": "프로젝트 개요"
                }},
                {{
                    "SlideIndex": 2,
                    "ShapeId": "TextBox 7",
                    "Text": "Q&A 및 토론"
                }}
            ]
        }}
        """, targetLang);
    }
}
