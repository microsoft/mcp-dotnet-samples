#!/usr/bin/env node

const fs = require("fs");
const path = require("path");

// Constants
const APPLY_TO_KEY = "applyTo";

// Helper function to process applyTo field values
function processApplyToField(value) {
    if (value.includes(",")) {
        return value
            .split(",")
            .map((item) => item.trim())
            .filter((item) => item.length > 0);
    } else if (value.length > 0) {
        return [value];
    } else {
        return [];
    }
}

// Read the JSON schema to understand the structure
const schemaPath = path.join(__dirname, "frontmatter-schema.json");
const schema = JSON.parse(fs.readFileSync(schemaPath, "utf8"));

// Define the directories to process
const directories = {
    chatmodes: path.join(__dirname, "src", "awesome-copilot", "chatmodes"),
    instructions: path.join(
        __dirname,
        "src",
        "awesome-copilot",
        "instructions"
    ),
    prompts: path.join(__dirname, "src", "awesome-copilot", "prompts"),
    collections: path.join(__dirname, "src", "awesome-copilot", "collections"),
    agents: path.join(__dirname, "src", "awesome-copilot", "agents"),
};

/**
 * Parses a simple YAML frontmatter string into a JavaScript object.
 *
 * This function handles key-value pairs, multi-line values, arrays, and special cases
 * like the `applyTo` key, which is processed into an array of strings. It also removes
 * comments and trims unnecessary whitespace.
 *
 * @param {string} yamlContent - The YAML frontmatter content as a string.
 *                               Each line should represent a key-value pair, an array item,
 *                               or a comment (starting with `#`).
 * @returns {Object} A JavaScript object representing the parsed YAML content.
 *                   Keys are strings, and values can be strings, arrays, or objects.
 *                   Special handling is applied to the `applyTo` key, converting
 *                   comma-separated strings into arrays.
 */
function parseSimpleYaml(yamlContent) {
    const result = {};
    const lines = yamlContent.split("\n");
    let currentKey = null;
    let currentValue = "";
    let inArray = false;
    let arrayItems = [];

    // Helper to parse a bracket-style array string into array items.
    function parseBracketArrayString(str) {
        const items = [];
        const arrayContent = str.slice(1, -1);
        if (!arrayContent.trim()) return items;

        // Split by comma, but be defensive and trim each item and remove trailing commas/quotes
        const rawItems = arrayContent.split(",");
        for (let raw of rawItems) {
            let item = raw.trim();
            if (!item) continue;
            // Remove trailing commas left over (defensive)
            if (item.endsWith(",")) item = item.slice(0, -1).trim();
            // Remove surrounding quotes if present
            if (
                (item.startsWith('"') && item.endsWith('"')) ||
                (item.startsWith("'") && item.endsWith("'"))
            ) {
                item = item.slice(1, -1);
            }
            if (item.length > 0) items.push(item);
        }

        return items;
    }

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        const trimmed = line.trim();

        if (!trimmed || trimmed.startsWith("#")) continue;

        // Check if this is a key-value pair
        const colonIndex = trimmed.indexOf(":");
        if (colonIndex !== -1 && !trimmed.startsWith("-")) {
            // Finish previous key if we were building one
            if (currentKey) {
                if (inArray) {
                    result[currentKey] = arrayItems;
                    arrayItems = [];
                    inArray = false;
                } else {
                    let trimmedValue = currentValue.trim();

                    // If the accumulated value looks like a bracket array (possibly multiline), parse it
                    if (
                        trimmedValue.startsWith("[") &&
                        trimmedValue.endsWith("]")
                    ) {
                        result[currentKey] =
                            parseBracketArrayString(trimmedValue);
                    } else {
                        // Handle comma-separated strings for specific fields that should be arrays
                        if (currentKey === APPLY_TO_KEY) {
                            result[currentKey] =
                                processApplyToField(trimmedValue);
                        } else {
                            result[currentKey] = trimmedValue;
                        }
                    }
                }
            }

            currentKey = trimmed.substring(0, colonIndex).trim();
            currentValue = trimmed.substring(colonIndex + 1).trim();

            // Remove quotes if present
            if (
                (currentValue.startsWith('"') && currentValue.endsWith('"')) ||
                (currentValue.startsWith("'") && currentValue.endsWith("'"))
            ) {
                currentValue = currentValue.slice(1, -1);
            }

            // Check if this is an inline bracket-array
            if (currentValue.startsWith("[") && currentValue.endsWith("]")) {
                result[currentKey] = parseBracketArrayString(currentValue);
                currentKey = null;
                currentValue = "";
            } else if (currentValue === "" || currentValue === "[]") {
                // Empty value or empty array, might be multi-line
                if (currentValue === "[]") {
                    result[currentKey] = [];
                    currentKey = null;
                    currentValue = "";
                } else {
                    // Check if next line starts with a dash (array item)
                    if (
                        i + 1 < lines.length &&
                        lines[i + 1].trim().startsWith("-")
                    ) {
                        inArray = true;
                        arrayItems = [];
                    }
                }
            }
        } else if (trimmed.startsWith("-") && currentKey && inArray) {
            // Array item
            let item = trimmed.substring(1).trim();
            // Remove trailing commas and surrounding quotes
            if (item.endsWith(",")) item = item.slice(0, -1).trim();
            if (
                (item.startsWith('"') && item.endsWith('"')) ||
                (item.startsWith("'") && item.endsWith("'"))
            ) {
                item = item.slice(1, -1);
            }
            arrayItems.push(item);
        } else if (currentKey && !inArray) {
            // Multi-line value
            currentValue += " " + trimmed;
        }
    }

    // Finish the last key
    if (currentKey) {
        if (inArray) {
            result[currentKey] = arrayItems;
        } else {
            let finalValue = currentValue.trim();
            // Remove quotes if present
            if (
                (finalValue.startsWith('"') && finalValue.endsWith('"')) ||
                (finalValue.startsWith("'") && finalValue.endsWith("'"))
            ) {
                finalValue = finalValue.slice(1, -1);
            }

            // If the final value looks like a bracket array, parse it
            if (finalValue.startsWith("[") && finalValue.endsWith("]")) {
                result[currentKey] = parseBracketArrayString(finalValue);
            } else {
                // Handle comma-separated strings for specific fields that should be arrays
                if (currentKey === APPLY_TO_KEY) {
                    result[currentKey] = processApplyToField(finalValue);
                } else {
                    result[currentKey] = finalValue;
                }
            }
        }
    }

    return result;
}

/**
 * Parses a YAML file content into a JavaScript object.
 * This function handles collection YAML files which use standard YAML format.
 *
 * @param {string} yamlContent - The YAML file content as a string.
 * @returns {Object} A JavaScript object representing the parsed YAML content.
 */
function parseCollectionYaml(yamlContent) {
    const result = {};
    const lines = yamlContent.split("\n");
    let currentKey = null;
    let currentValue = "";
    let inArray = false;
    let arrayItems = [];
    let currentObject = {};
    let rootIndent = 0;

    function getIndentLevel(line) {
        return line.match(/^(\s*)/)[1].length;
    }

    function parseValue(value) {
        const trimmed = value.trim();

        // Handle boolean values
        if (trimmed === "true") return true;
        if (trimmed === "false") return false;

        // Handle null values
        if (trimmed === "null" || trimmed === "~") return null;

        // Handle numbers
        if (/^-?\d+$/.test(trimmed)) return parseInt(trimmed, 10);
        if (/^-?\d+\.\d+$/.test(trimmed)) return parseFloat(trimmed);

        // Handle strings (remove quotes if present)
        if (
            (trimmed.startsWith('"') && trimmed.endsWith('"')) ||
            (trimmed.startsWith("'") && trimmed.endsWith("'"))
        ) {
            return trimmed.slice(1, -1);
        }

        return trimmed;
    }

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        const trimmed = line.trim();

        if (!trimmed || trimmed.startsWith("#")) continue;

        const lineIndent = getIndentLevel(line);

        // Check if this is a key-value pair
        const colonIndex = trimmed.indexOf(":");
        if (colonIndex !== -1 && !trimmed.startsWith("-")) {
            // Finish previous key if we were building one
            if (currentKey) {
                if (inArray) {
                    result[currentKey] = arrayItems;
                    arrayItems = [];
                    inArray = false;
                } else {
                    result[currentKey] = parseValue(currentValue);
                }
            }

            currentKey = trimmed.substring(0, colonIndex).trim();
            currentValue = trimmed.substring(colonIndex + 1).trim();
            rootIndent = lineIndent;

            // Check if this is an array
            if (currentValue === "" || currentValue === "[]") {
                // Look ahead to see if next line is an array item (skip comments)
                let nextLineIndex = i + 1;
                while (nextLineIndex < lines.length) {
                    const nextLine = lines[nextLineIndex];
                    const nextTrimmed = nextLine.trim();

                    // Skip comments and empty lines
                    if (!nextTrimmed || nextTrimmed.startsWith("#")) {
                        nextLineIndex++;
                        continue;
                    }

                    const nextIndent = getIndentLevel(nextLine);
                    if (
                        nextTrimmed.startsWith("-") &&
                        nextIndent > lineIndent
                    ) {
                        inArray = true;
                        arrayItems = [];
                    }
                    break;
                }

                if (currentValue === "[]") {
                    result[currentKey] = [];
                    currentKey = null;
                    currentValue = "";
                }
            } else if (
                currentValue.startsWith("[") &&
                currentValue.endsWith("]")
            ) {
                // Inline array
                const arrayContent = currentValue.slice(1, -1);
                if (!arrayContent.trim()) {
                    result[currentKey] = [];
                } else {
                    const items = arrayContent
                        .split(",")
                        .map((item) => parseValue(item));
                    result[currentKey] = items;
                }
                currentKey = null;
                currentValue = "";
            } else if (currentValue === "|") {
                // Handle literal block scalar at root level
                let blockContent = [];
                let blockIndent = null;
                let j = i + 1;

                // Collect all lines that are part of the literal block
                while (j < lines.length) {
                    const blockLine = lines[j];
                    const blockLineIndent = getIndentLevel(blockLine);
                    const blockTrimmed = blockLine.trim();

                    // If we hit an empty line, include it in the block
                    if (!blockTrimmed) {
                        blockContent.push("");
                        j++;
                        continue;
                    }

                    // If we hit a comment, skip it
                    if (blockTrimmed.startsWith("#")) {
                        j++;
                        continue;
                    }

                    // If this is the first content line, establish the block indentation
                    if (blockIndent === null) {
                        blockIndent = blockLineIndent;
                    }

                    // If the line is properly indented for this block, include it
                    if (
                        blockLineIndent >= blockIndent &&
                        blockLineIndent > lineIndent
                    ) {
                        // Remove the base indentation from the content
                        const contentLine = blockLine.substring(blockIndent);
                        blockContent.push(contentLine);
                        j++;
                    } else {
                        // We've reached the end of this block
                        break;
                    }
                }

                // Join the block content with newlines
                result[currentKey] = blockContent.join("\n").trimRight();
                currentKey = null;
                currentValue = "";
                i = j - 1; // Skip the processed lines
            } else {
                // Regular value
                result[currentKey] = parseValue(currentValue);
                currentKey = null;
                currentValue = "";
            }
        } else if (trimmed.startsWith("-") && inArray) {
            // Array item
            let item = trimmed.substring(1).trim();

            if (item.includes(":")) {
                // Object within array - parse the key:value pair
                const colonIdx = item.indexOf(":");
                const key = item.substring(0, colonIdx).trim();
                const value = item.substring(colonIdx + 1).trim();
                currentObject = {};
                currentObject[key] = parseValue(value);

                // Look ahead to see if there are more properties for this object
                let j = i + 1;
                while (j < lines.length) {
                    const nextLine = lines[j];
                    const nextTrimmed = nextLine.trim();

                    // Skip comments
                    if (!nextTrimmed || nextTrimmed.startsWith("#")) {
                        j++;
                        continue;
                    }

                    const nextIndent = getIndentLevel(nextLine);

                    // If next line is indented more than current and contains a colon, it's part of this object
                    if (
                        nextIndent > lineIndent &&
                        nextTrimmed.includes(":") &&
                        !nextTrimmed.startsWith("-")
                    ) {
                        const nextColonIdx = nextTrimmed.indexOf(":");
                        const nextKey = nextTrimmed
                            .substring(0, nextColonIdx)
                            .trim();
                        let nextValue = nextTrimmed
                            .substring(nextColonIdx + 1)
                            .trim();

                        // Handle literal block scalar (|)
                        if (nextValue === "|") {
                            let blockContent = [];
                            let blockIndent = null;
                            let k = j + 1;

                            // Collect all lines that are part of the literal block
                            while (k < lines.length) {
                                const blockLine = lines[k];
                                const blockLineIndent =
                                    getIndentLevel(blockLine);
                                const blockTrimmed = blockLine.trim();

                                // If we hit an empty line, include it in the block
                                if (!blockTrimmed) {
                                    blockContent.push("");
                                    k++;
                                    continue;
                                }

                                // If we hit a comment, skip it
                                if (blockTrimmed.startsWith("#")) {
                                    k++;
                                    continue;
                                }

                                // If this is the first content line, establish the block indentation
                                if (blockIndent === null) {
                                    blockIndent = blockLineIndent;
                                }

                                // If the line is properly indented for this block, include it
                                if (
                                    blockLineIndent >= blockIndent &&
                                    blockLineIndent > nextIndent
                                ) {
                                    // Remove the base indentation from the content
                                    const contentLine =
                                        blockLine.substring(blockIndent);
                                    blockContent.push(contentLine);
                                    k++;
                                } else {
                                    // We've reached the end of this block
                                    break;
                                }
                            }

                            // Join the block content with newlines
                            nextValue = blockContent.join("\n").trimRight();
                            j = k - 1; // Set j to the last processed line
                        } else {
                            nextValue = parseValue(nextValue);
                        }

                        currentObject[nextKey] = nextValue;
                        i = j; // Skip this line in the main loop
                        j++;
                    } else {
                        break;
                    }
                }

                arrayItems.push(currentObject);
            } else {
                // Simple array item
                arrayItems.push(parseValue(item));
            }
        } else if (
            currentKey &&
            lineIndent > rootIndent &&
            trimmed.includes(":") &&
            !trimmed.startsWith("-")
        ) {
            // Property of the last object in array
            if (inArray && arrayItems.length > 0) {
                const lastItem = arrayItems[arrayItems.length - 1];
                if (lastItem && typeof lastItem === "object") {
                    const colonIdx = trimmed.indexOf(":");
                    const key = trimmed.substring(0, colonIdx).trim();
                    const value = trimmed.substring(colonIdx + 1).trim();
                    lastItem[key] = parseValue(value);
                }
            }
        }
    }

    // Finish the last key
    if (currentKey) {
        if (inArray) {
            result[currentKey] = arrayItems;
        } else {
            result[currentKey] = parseValue(currentValue);
        }
    }

    return result;
}

// Function to extract frontmatter from a markdown file
function extractFrontmatter(filePath) {
    let content = fs.readFileSync(filePath, "utf8");

    // Remove BOM if present (handles files with Byte Order Mark)
    if (content.charCodeAt(0) === 0xfeff) {
        content = content.slice(1);
    }

    // Check if the file starts with frontmatter
    if (!content.startsWith("---")) {
        return null;
    }

    const lines = content.split("\n");
    let frontmatterEnd = -1;

    // Find the end of frontmatter
    for (let i = 1; i < lines.length; i++) {
        if (lines[i].trim() === "---") {
            frontmatterEnd = i;
            break;
        }
    }

    if (frontmatterEnd === -1) {
        return null;
    }

    // Extract frontmatter content
    const frontmatterContent = lines.slice(1, frontmatterEnd).join("\n");

    try {
        return parseSimpleYaml(frontmatterContent);
    } catch (error) {
        console.error(
            `Error parsing frontmatter in ${filePath}:`,
            error.message
        );
        return null;
    }
}

// Function to process files in a directory
function processDirectory(dirPath, fileExtension) {
    const files = fs
        .readdirSync(dirPath)
        .filter((file) => file.endsWith(fileExtension))
        .sort();

    const results = [];

    for (const file of files) {
        const filePath = path.join(dirPath, file);
        const frontmatter = extractFrontmatter(filePath);

        if (frontmatter) {
            const result = {
                filename: file,
                ...frontmatter,
            };

            // Ensure description is present (required by schema)
            if (!result.description) {
                console.warn(
                    `Warning: No description found in ${file}, adding placeholder`
                );
                result.description = "No description provided";
            }

            results.push(result);
        } else {
            console.warn(`Warning: No frontmatter found in ${file}, skipping`);
        }
    }

    return results;
}

// Function to process collection files in a directory
function processCollectionDirectory(dirPath) {
    const files = fs
        .readdirSync(dirPath)
        .filter((file) => file.endsWith(".collection.yml"))
        .sort();

    const results = [];

    for (const file of files) {
        const filePath = path.join(dirPath, file);

        try {
            let content = fs.readFileSync(filePath, "utf8");

            // Remove BOM if present
            if (content.charCodeAt(0) === 0xfeff) {
                content = content.slice(1);
            }

            const collectionData = parseCollectionYaml(content);

            if (collectionData) {
                const result = {
                    filename: file,
                    id:
                        collectionData.id ||
                        file.replace(".collection.yml", ""),
                    name: collectionData.name || "Unnamed Collection",
                    description:
                        collectionData.description || "No description provided",
                    tags: collectionData.tags || [],
                    items: collectionData.items || [],
                    display: collectionData.display || {},
                };

                // Ensure description is present (required)
                if (
                    !result.description ||
                    result.description === "No description provided"
                ) {
                    console.warn(
                        `Warning: No description found in ${file}, adding placeholder`
                    );
                    result.description = "No description provided";
                }

                results.push(result);
            } else {
                console.warn(
                    `Warning: Could not parse collection data in ${file}, skipping`
                );
            }
        } catch (error) {
            console.error(
                `Error processing collection file ${file}:`,
                error.message
            );
        }
    }

    return results;
}

// Process all directories
const metadata = {
    chatmodes: processDirectory(directories.chatmodes, ".chatmode.md"),
    instructions: processDirectory(
        directories.instructions,
        ".instructions.md"
    ),
    prompts: processDirectory(directories.prompts, ".prompt.md"),
    collections: processCollectionDirectory(directories.collections),
    agents: processDirectory(directories.agents, ".agent.md"),
};

// Write the metadata.json file
const outputPath = path.join(
    __dirname,
    "src",
    "McpSamples.AwesomeCopilot.HybridApp",
    "metadata.json"
);
fs.writeFileSync(outputPath, JSON.stringify(metadata, null, 2));

console.log(
    `Extracted frontmatter from ${metadata.chatmodes.length} chatmode files`
);
console.log(
    `Extracted frontmatter from ${metadata.instructions.length} instruction files`
);
console.log(
    `Extracted frontmatter from ${metadata.prompts.length} prompt files`
);
console.log(
    `Extracted metadata from ${metadata.collections.length} collection files`
);
console.log(
    `Extracted frontmatter from ${metadata.agents.length} agent files`
);
console.log(`Metadata written to ${outputPath}`);

// Validate that required fields are present
let hasErrors = false;

// Check chatmodes
metadata.chatmodes.forEach((chatmode) => {
    if (!chatmode.filename || !chatmode.description) {
        console.error(
            `Error: Chatmode missing required fields: ${
                chatmode.filename || "unknown"
            }`
        );
        hasErrors = true;
    }
});

// Check instructions
metadata.instructions.forEach((instruction) => {
    if (!instruction.filename || !instruction.description) {
        console.error(
            `Error: Instruction missing required fields: ${
                instruction.filename || "unknown"
            }`
        );
        hasErrors = true;
    }
});

// Check prompts
metadata.prompts.forEach((prompt) => {
    if (!prompt.filename || !prompt.description) {
        console.error(
            `Error: Prompt missing required fields: ${
                prompt.filename || "unknown"
            }`
        );
        hasErrors = true;
    }
});

// Check collections
metadata.collections.forEach((collection) => {
    if (
        !collection.filename ||
        !collection.description ||
        !collection.id ||
        !collection.name
    ) {
        console.error(
            `Error: Collection missing required fields: ${
                collection.filename || "unknown"
            }`
        );
        hasErrors = true;
    }

    // Validate that items array exists and has proper structure
    if (!Array.isArray(collection.items)) {
        console.error(
            `Error: Collection ${collection.filename} has invalid items array`
        );
        hasErrors = true;
    } else {
        collection.items.forEach((item, index) => {
            if (!item.path || !item.kind) {
                console.error(
                    `Error: Collection ${collection.filename} item ${index} missing required fields (path, kind)`
                );
                hasErrors = true;
            }
        });
    }
});

// Check agents
metadata.agents.forEach((agent) => {
    if (!agent.filename || !agent.description) {
        console.error(
            `Error: Agent missing required fields: ${
                agent.filename || "unknown"
            }`
        );
        hasErrors = true;
    }
});

if (hasErrors) {
    console.error(
        "Some files are missing required fields. Please check the output above."
    );
    process.exit(1);
} else {
    console.log(
        "All files have required fields. Metadata extraction completed successfully."
    );
}
