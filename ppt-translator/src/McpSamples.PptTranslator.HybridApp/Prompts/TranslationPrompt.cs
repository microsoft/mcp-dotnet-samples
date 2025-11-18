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

        Please follow these rules strictly:
        1. Preserve the original JSON structure and keys exactly.
        2. Translate only the natural-language parts of each "Text" field.
        3. Keep proper nouns, abbreviations, names, and domain-specific terms in their original form if they are:
        - Product or brand names (e.g., Microsoft, PowerPoint, Azure)
        - Acronyms or technical abbreviations (e.g., AI, API, GPU)
        4. When translating academic or research-related slides:
        - Use formal and precise tone suitable for academic presentations.
        - Translate technical terms consistently and contextually.
        5. Do NOT add any explanations, comments, or formatting outside of valid JSON.
        6. Return ONLY valid JSON text in your output.

        Example input:
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

        Example output:
        {{
            "TotalCount": 2,
            "Items": [
                {{
                    "SlideIndex": 1,
                    "ShapeId": "TextBox 5",
                    "TranslatedText": "프로젝트 개요"
                }},
                {{
                    "SlideIndex": 2,
                    "ShapeId": "TextBox 7",
                    "TranslatedText": "Q&A 및 토론"
                }}
            ]
        }}
        """, targetLang);
    }
}
